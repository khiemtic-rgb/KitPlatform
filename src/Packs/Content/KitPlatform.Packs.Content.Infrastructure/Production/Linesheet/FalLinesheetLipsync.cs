using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Ghép môi cho M1 bằng fal.ai sync (lipsync-2, lipsync-2-pro hoặc sync-3, chọn bằng settings.lipsync_model; thêm 2026-11-04). Mỗi lần gửi một đoạn hình chỉ có một khuôn mặt (M1 đã
/// cắt sẵn vùng mặt người nói) và tiếng của câu. Tải file lên kho của fal qua <see cref="ContentFalClient.UploadAsync"/>,
/// gửi vào hàng đợi, hỏi trạng thái 4 giây một lần, tối đa 10 phút. Không tự gửi lại khi lỗi: mỗi lần gửi được nhận là
/// một lần trả tiền. Khóa: khóa fal đang hoạt động trong kho khóa sản xuất (cùng chỗ nhánh ghép môi Famixa cũ dùng),
/// không có thì dùng khóa fal trong cài đặt Video.
/// </summary>
internal sealed class FalLinesheetLipsync : ILinesheetLipsync
{
    public const string HttpClientName = "famixa-linesheet-lipsync";
    const string Queue = "https://queue.fal.run/";
    static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(4);
    static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(10);

    readonly ContentFalClient _fal;
    readonly IHttpClientFactory _http;
    readonly IProductionCredentialResolver? _credentials;
    string? _key;

    public FalLinesheetLipsync(ContentFalClient fal, IHttpClientFactory http, IProductionCredentialResolver? credentials = null)
    {
        _fal = fal;
        _http = http;
        _credentials = credentials;
    }

    public IReadOnlyList<string> Models { get; } = ["lipsync-2", "lipsync-2-pro", "sync-3"];

    /// <summary>Giá theo sync.so (2026-11): lipsync-2 0,05; lipsync-2-pro 0,083; sync-3 0,133 USD mỗi giây (lấy mức cao).</summary>
    public decimal UsdPerSecond(string model) => model switch { "sync-3" => 0.133m, "lipsync-2-pro" => 0.083m, _ => 0.05m };

    async Task<string> KeyAsync(CancellationToken ct)
    {
        if (_key is not null) return _key;
        if (_credentials is not null)
        {
            try
            {
                var m = await _credentials.ResolveActiveAsync(FamixaProviderIds.Fal, ProductionLipsyncCatalog.SyncLipsyncV3, ct);
                if (!string.IsNullOrWhiteSpace(m.Secret)) return _key = m.Secret.Trim();
            }
            catch (InvalidOperationException)
            {
                // chưa có khóa sản xuất: dùng khóa trong cài đặt Video
            }
        }

        var resolved = await _fal.ResolveAsync(ct);
        return _key = resolved.FalApiKey?.Trim() is { Length: > 0 } k
            ? k
            : throw new InvalidOperationException("LINESHEET_LIPSYNC_NO_KEY: chưa có khóa fal (kho khóa sản xuất hoặc Model AI → Video).");
    }

    public async Task<byte[]> SyncAsync(byte[] videoMp4, byte[] audioMp3, string model, CancellationToken ct)
    {
        if (!Models.Contains(model)) throw new InvalidOperationException("LINESHEET_LIPSYNC_MODEL: " + model);
        NovixaE2eIsolation.ThrowIfExternalCall("Fal");
        var key = await KeyAsync(ct);
        var videoUrl = await _fal.UploadAsync(videoMp4, "video/mp4", "face.mp4", ct, key);
        var audioUrl = await _fal.UploadAsync(audioMp3, "audio/mpeg", "voice.mp3", ct, key);
        var http = _http.CreateClient(HttpClientName);

        // sync-3 ở đường riêng (v3), không có trường model; lipsync-2 và lipsync-2-pro chung đường v2, chọn bằng trường model
        using var submit = new HttpRequestMessage(HttpMethod.Post, Queue + (model == "sync-3" ? "fal-ai/sync-lipsync/v3" : "fal-ai/sync-lipsync/v2"));
        submit.Headers.Authorization = new AuthenticationHeaderValue("Key", key);
        submit.Content = model == "sync-3"
            ? JsonContent.Create(new { video_url = videoUrl, audio_url = audioUrl, sync_mode = "cut_off" })
            : JsonContent.Create(new { model, video_url = videoUrl, audio_url = audioUrl, sync_mode = "cut_off" });
        using var res = await http.SendAsync(submit, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if ((int)res.StatusCode == 402) throw new InvalidOperationException("LINESHEET_LIPSYNC_NO_FUNDS: fal hết tiền (402), nạp trên fal.ai.");
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"LINESHEET_LIPSYNC_FAILED: fal {(int)res.StatusCode}: {Cut(body)}");
        using var doc = JsonDocument.Parse(body);
        var id = Str(doc.RootElement, "request_id") ?? throw new InvalidOperationException("LINESHEET_LIPSYNC_FAILED: fal không trả request_id.");
        var statusUrl = Str(doc.RootElement, "status_url") ?? $"https://queue.fal.run/fal-ai/sync-lipsync/requests/{id}/status";
        var responseUrl = Str(doc.RootElement, "response_url") ?? $"https://queue.fal.run/fal-ai/sync-lipsync/requests/{id}";

        var waited = TimeSpan.Zero;
        while (true)
        {
            await Task.Delay(PollEvery, ct);
            waited += PollEvery;
            using var st = new HttpRequestMessage(HttpMethod.Get, statusUrl);
            st.Headers.Authorization = new AuthenticationHeaderValue("Key", key);
            using var sres = await http.SendAsync(st, ct);
            var sbody = await sres.Content.ReadAsStringAsync(ct);
            if (sres.IsSuccessStatusCode)
            {
                using var sdoc = JsonDocument.Parse(sbody);
                var status = (Str(sdoc.RootElement, "status") ?? "").ToUpperInvariant();
                if (status == "COMPLETED") break;
                if (status is "FAILED" or "ERROR") throw new InvalidOperationException($"LINESHEET_LIPSYNC_FAILED: {Cut(sbody)}");
            }

            if (waited > MaxWait) throw new InvalidOperationException($"LINESHEET_LIPSYNC_TIMEOUT: quá 10 phút, mã fal {id}.");
        }

        using var rq = new HttpRequestMessage(HttpMethod.Get, responseUrl);
        rq.Headers.Authorization = new AuthenticationHeaderValue("Key", key);
        using var rres = await http.SendAsync(rq, ct);
        var rbody = await rres.Content.ReadAsStringAsync(ct);
        if (!rres.IsSuccessStatusCode) throw new InvalidOperationException($"LINESHEET_LIPSYNC_FAILED: fal {(int)rres.StatusCode}: {Cut(rbody)}");
        using var rdoc = JsonDocument.Parse(rbody);
        var url = rdoc.RootElement.TryGetProperty("video", out var v) ? Str(v, "url") : null;
        if (string.IsNullOrWhiteSpace(url)) throw new InvalidOperationException($"LINESHEET_LIPSYNC_FAILED: fal không trả video: {Cut(rbody)}");
        var bytes = await http.GetByteArrayAsync(url, ct);
        return bytes.Length >= 32 ? bytes : throw new InvalidOperationException("LINESHEET_LIPSYNC_FAILED: video rỗng.");
    }

    static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    static string Cut(string s) => s.Length > 300 ? s[..300] : s;
}
