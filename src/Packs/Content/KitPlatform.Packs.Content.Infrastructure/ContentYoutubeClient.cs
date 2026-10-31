using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed record ContentYoutubeResolved(string? ClientId, string? ClientSecret, string RedirectUri);

/// <summary>Google OAuth + YouTube Data API v3 (resumable upload). Client ID/Secret đọc từ cấu hình / biến môi trường.</summary>
internal sealed class ContentYoutubeClient
{
    internal const string DefaultRedirectUri = "http://localhost:5173/content/youtube/callback";
    internal const string Scopes =
        "https://www.googleapis.com/auth/youtube.upload https://www.googleapis.com/auth/youtube.readonly";

    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _cfg;

    public ContentYoutubeClient(IHttpClientFactory http, IConfiguration cfg)
    {
        _http = http;
        _cfg = cfg;
    }

    public ContentYoutubeResolved Resolve()
    {
        static string? F(params string?[] v) => v.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim();
        return new ContentYoutubeResolved(
            F(_cfg["Content:YoutubeClientId"], Environment.GetEnvironmentVariable("YOUTUBE_CLIENT_ID")),
            F(_cfg["Content:YoutubeClientSecret"], Environment.GetEnvironmentVariable("YOUTUBE_CLIENT_SECRET")),
            F(_cfg["Content:YoutubeRedirectUri"]) ?? DefaultRedirectUri);
    }

    public string BuildAuthUrl(ContentYoutubeResolved cfg, string state) =>
        "https://accounts.google.com/o/oauth2/v2/auth" +
        $"?client_id={Uri.EscapeDataString(cfg.ClientId!)}" +
        $"&redirect_uri={Uri.EscapeDataString(cfg.RedirectUri)}" +
        "&response_type=code&access_type=offline&prompt=consent&include_granted_scopes=true" +
        $"&scope={Uri.EscapeDataString(Scopes)}" +
        $"&state={Uri.EscapeDataString(state)}";

    /// <returns>refresh_token (bắt buộc) từ mã OAuth.</returns>
    public async Task<string> ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct)
    {
        var cfg = Require();
        using var doc = await TokenAsync(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = cfg.ClientId!,
            ["client_secret"] = cfg.ClientSecret!,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code",
        }, ct);
        if (!doc.RootElement.TryGetProperty("refresh_token", out var rt) || string.IsNullOrWhiteSpace(rt.GetString()))
            throw new InvalidOperationException(
                "Google không trả refresh token. Vào myaccount.google.com/permissions gỡ quyền của app rồi Kết nối lại.");
        return rt.GetString()!;
    }

    public async Task<string> AccessTokenAsync(string refreshToken, CancellationToken ct)
    {
        var cfg = Require();
        using var doc = await TokenAsync(new Dictionary<string, string>
        {
            ["refresh_token"] = refreshToken,
            ["client_id"] = cfg.ClientId!,
            ["client_secret"] = cfg.ClientSecret!,
            ["grant_type"] = "refresh_token",
        }, ct);
        return doc.RootElement.TryGetProperty("access_token", out var t) && !string.IsNullOrWhiteSpace(t.GetString())
            ? t.GetString()!
            : throw new InvalidOperationException("Google không trả access token.");
    }

    public async Task<(string Id, string Title)> GetMyChannelAsync(string accessToken, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,
            "https://www.googleapis.com/youtube/v3/channels?part=snippet&mine=true");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var res = await Http().SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(GoogleMessage(body) ?? $"YouTube trả {(int)res.StatusCode}.");
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
            throw new InvalidOperationException(
                "Tài khoản Google này chưa có kênh YouTube. Khi cấp quyền hãy chọn đúng kênh «Người Giữ Vốn».");
        var it = items[0];
        return (it.GetProperty("id").GetString()!, it.GetProperty("snippet").GetProperty("title").GetString() ?? "");
    }

    /// <summary>Resumable upload; trả (videoId, privacyStatus thực tế).</summary>
    public async Task<(string VideoId, string? Privacy)> UploadVideoAsync(
        string accessToken,
        string filePath,
        string contentType,
        string title,
        string description,
        string privacy,
        CancellationToken ct)
    {
        var meta = JsonSerializer.Serialize(new
        {
            snippet = new { title, description, categoryId = "27" },
            status = new { privacyStatus = privacy, selfDeclaredMadeForKids = false },
        });
        var length = new FileInfo(filePath).Length;
        using var init = new HttpRequestMessage(HttpMethod.Post,
            "https://www.googleapis.com/upload/youtube/v3/videos?uploadType=resumable&part=snippet,status");
        init.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        init.Headers.Add("X-Upload-Content-Type", contentType);
        init.Headers.Add("X-Upload-Content-Length", length.ToString());
        init.Content = new StringContent(meta, Encoding.UTF8, "application/json");
        using var initRes = await Http().SendAsync(init, ct);
        if (!initRes.IsSuccessStatusCode)
        {
            var b = await initRes.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(GoogleMessage(b) ?? $"YouTube từ chối khởi tạo upload ({(int)initRes.StatusCode}).");
        }

        var location = initRes.Headers.Location
                       ?? throw new InvalidOperationException("YouTube không trả địa chỉ upload.");
        await using var fs = File.OpenRead(filePath);
        using var put = new HttpRequestMessage(HttpMethod.Put, location) { Content = new StreamContent(fs) };
        put.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        put.Content.Headers.ContentLength = length;
        using var putRes = await Http().SendAsync(put, ct);
        var putBody = await putRes.Content.ReadAsStringAsync(ct);
        if (!putRes.IsSuccessStatusCode)
            throw new InvalidOperationException(GoogleMessage(putBody) ?? $"Upload YouTube lỗi {(int)putRes.StatusCode}.");
        using var doc = JsonDocument.Parse(putBody);
        var id = doc.RootElement.GetProperty("id").GetString()!;
        string? actual = null;
        if (doc.RootElement.TryGetProperty("status", out var st) && st.TryGetProperty("privacyStatus", out var ps))
            actual = ps.GetString();
        return (id, actual);
    }

    private ContentYoutubeResolved Require()
    {
        var cfg = Resolve();
        if (string.IsNullOrWhiteSpace(cfg.ClientId) || string.IsNullOrWhiteSpace(cfg.ClientSecret))
            throw new InvalidOperationException(
                "Chưa cấu hình Google OAuth — đặt YOUTUBE_CLIENT_ID và YOUTUBE_CLIENT_SECRET rồi khởi động lại API.");
        return cfg;
    }

    private async Task<JsonDocument> TokenAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        using var res = await Http().PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(form), ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            var msg = "Google OAuth " + (int)res.StatusCode;
            try
            {
                using var d = JsonDocument.Parse(body);
                var e = d.RootElement.TryGetProperty("error_description", out var ed) ? ed.GetString() : null;
                if (!string.IsNullOrWhiteSpace(e)) msg += ": " + e;
            }
            catch { /* keep generic */ }
            throw new InvalidOperationException(msg);
        }
        return JsonDocument.Parse(body);
    }

    private HttpClient Http() => _http.CreateClient("content-youtube");

    private static string? GoogleMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            if (doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Object
                && err.TryGetProperty("message", out var m))
                return m.GetString();
        }
        catch { /* ignore */ }
        return null;
    }
}
