using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Gọi API Runway cho M1 (chuyển động và ảnh cảnh). Tự gửi yêu cầu, không qua <see cref="ContentRunwayClient"/> vì client
/// đó cố định mô hình gen4_turbo và tự sửa câu lệnh. Khóa: khóa Runway đang hoạt động của tổ chức trong kho khóa sản xuất
/// (cùng chỗ nhánh video Famixa cũ dùng); không có thì dùng khóa trong cài đặt Video. Bài học 2026-11-04: khóa trong cài
/// đặt Video không phải khóa API (không bắt đầu bằng key_), Runway trả 401.
/// </summary>
internal sealed class RunwayLinesheetApi
{
    public const string HttpClientName = "famixa-linesheet-runway";
    const string Version = "2024-11-06";
    readonly ContentRunwayClient _runway;
    readonly IHttpClientFactory _http;
    readonly IProductionCredentialResolver? _credentials;
    string? _key;

    public RunwayLinesheetApi(ContentRunwayClient runway, IHttpClientFactory http, IProductionCredentialResolver? credentials)
    {
        _runway = runway;
        _http = http;
        _credentials = credentials;
    }

    async Task<string> KeyAsync(CancellationToken ct)
    {
        if (_key is not null) return _key;
        if (_credentials is not null)
        {
            try
            {
                var m = await _credentials.ResolveActiveAsync(FamixaProviderIds.Runway, ProductionMotionCatalog.RunwayGen4Turbo, ct);
                if (!string.IsNullOrWhiteSpace(m.Secret)) return _key = m.Secret.Trim();
            }
            catch (InvalidOperationException)
            {
                // chưa có khóa sản xuất: dùng khóa trong cài đặt Video
            }
        }

        var resolved = await _runway.ResolveAsync(ct);
        return _key = resolved.RunwayApiKey?.Trim() is { Length: > 0 } k
            ? k
            : throw new InvalidOperationException("LINESHEET_RUNWAY_NO_KEY: chưa có khóa Runway.");
    }

    async Task<HttpRequestMessage> Request(HttpMethod method, string path, CancellationToken ct)
    {
        var req = new HttpRequestMessage(method, "https://api.dev.runwayml.com/" + path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await KeyAsync(ct));
        req.Headers.Add("X-Runway-Version", Version);
        return req;
    }

    /// <summary>Gửi một việc (image_to_video, text_to_image), trả mã việc.</summary>
    public async Task<string> StartAsync(string path, object body, CancellationToken ct)
    {
        NovixaE2eIsolation.ThrowIfExternalCall("Runway");
        using var req = await Request(HttpMethod.Post, path, ct);
        req.Content = JsonContent.Create(body);
        using var res = await _http.CreateClient(HttpClientName).SendAsync(req, ct);
        var text = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"LINESHEET_RUNWAY {(int)res.StatusCode}: {Cut(text)}");
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } s
            ? s
            : throw new InvalidOperationException("LINESHEET_RUNWAY: không trả mã việc.");
    }

    public async Task<LinesheetVideoGenStatus> PollAsync(string taskId, CancellationToken ct)
    {
        using var req = await Request(HttpMethod.Get, "v1/tasks/" + Uri.EscapeDataString(taskId), ct);
        using var res = await _http.CreateClient(HttpClientName).SendAsync(req, ct);
        var text = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"LINESHEET_RUNWAY {(int)res.StatusCode}: {Cut(text)}");
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        var status = root.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "";
        string? url = null;
        if (root.TryGetProperty("output", out var o) && o.ValueKind == JsonValueKind.Array && o.GetArrayLength() > 0)
            url = o[0].GetString();
        string? error = root.TryGetProperty("failure", out var f) ? f.ToString() : null;
        if (root.TryGetProperty("failureCode", out var fc)) error = (error is null ? "" : error + " ") + fc;
        return new LinesheetVideoGenStatus(status, url, error);
    }

    /// <summary>Gửi, chờ xong (hỏi 5 giây một lần, tối đa 12 phút), tải kết quả.</summary>
    public async Task<byte[]> RunAsync(string path, object body, CancellationToken ct)
    {
        var task = await StartAsync(path, body, ct);
        var waited = TimeSpan.Zero;
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            waited += TimeSpan.FromSeconds(5);
            var st = await PollAsync(task, ct);
            if (st.Status == "SUCCEEDED" && !string.IsNullOrWhiteSpace(st.VideoUrl)) return await DownloadAsync(st.VideoUrl, ct);
            if (st.Status is "FAILED" or "CANCELLED") throw new InvalidOperationException($"LINESHEET_RUNWAY_FAILED: {st.Error}");
            if (waited > TimeSpan.FromMinutes(12)) throw new InvalidOperationException("LINESHEET_RUNWAY_TIMEOUT: quá 12 phút, mã việc " + task);
        }
    }

    public async Task<byte[]> DownloadAsync(string url, CancellationToken ct)
    {
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("LINESHEET_RUNWAY: đường dẫn kết quả lạ.");
        return await _http.CreateClient(HttpClientName).GetByteArrayAsync(url, ct);
    }

    /// <summary>
    /// Số credit còn lại của tổ chức (GET v1/organization, trường creditBalance). Thêm 2026-11-09 (sổ lỗi C3: hết credit giữa lô).
    /// Lỗi mạng hoặc không đọc được thì trả null để không chặn việc chạy.
    /// </summary>
    public async Task<int?> CreditBalanceAsync(CancellationToken ct)
    {
        try
        {
            NovixaE2eIsolation.ThrowIfExternalCall("Runway");
            using var req = await Request(HttpMethod.Get, "v1/organization", ct);
            using var res = await _http.CreateClient(HttpClientName).SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            return doc.RootElement.TryGetProperty("creditBalance", out var b) && b.TryGetInt32(out var n) ? n : null;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return null;
        }
    }

    static string Cut(string s) => s.Length > 300 ? s[..300] : s;
}

/// <summary>
/// Chuyển động cho M1 (ảnh thành video, không tiếng). gen4_turbo 5 credit/giây; gen4.5 12 credit/giây, ổn định hơn (thêm 2026-11-05).
/// Ảnh gửi dạng data URI JPEG 1280×720.
/// </summary>
internal sealed class RunwayLinesheetVideoGen : ILinesheetVideoGen
{
    readonly RunwayLinesheetApi _api;

    public RunwayLinesheetVideoGen(RunwayLinesheetApi api) => _api = api;

    public string Model => "gen4_turbo";
    public IReadOnlyList<string> Models { get; } = ["gen4_turbo", "gen4.5"];
    public int CreditsPerSecond(string model) => model == "gen4.5" ? 12 : 5;

    public Task<string> StartAsync(byte[] jpeg, string prompt, int seconds, string model, CancellationToken ct)
    {
        if (!Models.Contains(model)) throw new InvalidOperationException("LINESHEET_MOTION_MODEL: " + model);
        var dataUri = "data:image/jpeg;base64," + Convert.ToBase64String(jpeg);
        return _api.StartAsync("v1/image_to_video",
            new { model, promptImage = dataUri, promptText = prompt, duration = Math.Clamp(seconds, 2, 10), ratio = "1280:720" }, ct);
    }

    public Task<LinesheetVideoGenStatus> PollAsync(string taskId, CancellationToken ct) => _api.PollAsync(taskId, ct);

    public Task<byte[]> DownloadAsync(string url, CancellationToken ct) => _api.DownloadAsync(url, ct);

    public Task<int?> CreditBalanceAsync(CancellationToken ct) => _api.CreditBalanceAsync(ct);
}

/// <summary>
/// Ảnh cảnh cho M1 qua Runway (thêm 2026-11-05), gọi ảnh mẫu trong câu lệnh bằng @tag:
/// gen4_image (tối đa 3 ảnh tham chiếu; 5 credit ảnh 720p, 8 credit ảnh 1080p) hoặc gpt_image_2 (mô hình ảnh của OpenAI,
/// tối đa 16 ảnh tham chiếu, câu lệnh tới 32.000 ký tự).
/// Giá gpt_image_2 sửa 2026-11-09 theo trang Usage của Runway: mức high khổ 1920:1088 thực trừ 20 credit/ảnh
/// (8/11: 77 ảnh = 1.540 credit; trước đây ghi 41 nên mọi lần duyệt ảnh bị thổi gấp đôi). Mức low/medium chưa đối chiếu,
/// tạm giữ số cũ (có thể cao hơn thực tế); đối chiếu khi dùng lần đầu rồi sửa.
/// Bài học 2026-11-05: gen4_image giữ chỗ ngồi và chi tiết nhưng da mặt kém mịn, hay thêm kính, cười lộ răng.
/// </summary>
internal sealed class RunwayLinesheetImageGen : ILinesheetImageGen
{
    readonly RunwayLinesheetApi _api;

    public RunwayLinesheetImageGen(RunwayLinesheetApi api) => _api = api;

    public IReadOnlyList<string> Models { get; } = ["gen4_image", "gpt_image_2"];

    public int MaxRefs(string model) => model == "gpt_image_2" ? 16 : 3;

    public Task<int?> CreditBalanceAsync(CancellationToken ct) => _api.CreditBalanceAsync(ct);

    public int CreditsPerImage(string model, string ratio, string quality) => model == "gpt_image_2"
        ? quality switch { "low" => 5, "medium" => 15, _ => 20 }
        : ratio is "1920:1080" or "1080:1920" or "1440:1080" or "1080:1440" or "1080:1080" or "2112:912" ? 8 : 5;

    public Task<byte[]> GenerateAsync(string prompt, IReadOnlyList<LinesheetImageRef> refs, string ratio, string model, string quality, int? seed, CancellationToken ct)
    {
        if (!Models.Contains(model)) throw new InvalidOperationException("LINESHEET_IMAGE_MODEL: " + model);
        if (refs.Count > MaxRefs(model)) throw new InvalidOperationException($"LINESHEET_IMAGE_REFS: tối đa {MaxRefs(model)} ảnh tham chiếu.");
        var referenceImages = refs.Select(r => new { uri = "data:image/jpeg;base64," + Convert.ToBase64String(r.Jpeg), tag = r.Tag }).ToArray();
        object body = model == "gpt_image_2"
            ? new { model, promptText = prompt, ratio = ratio == "1920:1080" ? "1920:1088" : ratio, quality, referenceImages }
            : seed is { } s
                ? new { model, promptText = prompt, ratio, referenceImages, seed = s }
                : new { model, promptText = prompt, ratio, referenceImages };
        return _api.RunAsync("v1/text_to_image", body, ct);
    }
}
