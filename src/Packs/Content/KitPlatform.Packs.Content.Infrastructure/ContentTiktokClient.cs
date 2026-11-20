using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed record ContentTiktokResolved(string? ClientKey, string? ClientSecret, string RedirectUri, bool DirectPost = false);

/// <summary>Thông tin tài khoản TikTok trước khi đăng thẳng (bắt buộc hỏi theo quy định Content Posting API).</summary>
internal sealed record ContentTiktokCreatorInfo(
    string? Username,
    IReadOnlyList<string> PrivacyOptions,
    bool CommentDisabled,
    bool DuetDisabled,
    bool StitchDisabled,
    int? MaxDurationSeconds);

/// <summary>Nội dung bài đăng thẳng lên TikTok (Direct Post).</summary>
internal sealed record ContentTiktokPostInfo(
    string Title,
    string PrivacyLevel,
    bool DisableComment,
    bool DisableDuet,
    bool DisableStitch,
    int CoverTimestampMs,
    bool IsAigc,
    bool BrandOrganic);

/// <summary>TikTok từ chối vì token chưa có quyền video.publish hoặc ứng dụng chưa được duyệt đăng thẳng.</summary>
internal sealed class ContentTiktokScopeException(string message) : InvalidOperationException(message);

internal sealed record ContentTiktokTokens(string AccessToken, string RefreshToken, string OpenId);

/// <summary>TikTok Login Kit (OAuth v2) + Content Posting API (upload vào hộp nháp). Client key/secret đọc từ cấu hình / biến môi trường.</summary>
internal sealed class ContentTiktokClient
{
    internal const string DefaultRedirectUri = "https://kittech.vn/tiktok/callback/";
    internal const string Scopes = "user.info.basic,video.upload";
    /// <summary>Quyền khi bật đăng thẳng (Content:TiktokDirectPost=true). Ứng dụng TikTok phải bật Direct Post trong cổng nhà phát triển.</summary>
    internal const string DirectScopes = "user.info.basic,video.upload,video.publish";
    private const long MaxSingleChunk = 64L * 1024 * 1024;
    private const long BigChunk = 32L * 1024 * 1024;

    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _cfg;

    public ContentTiktokClient(IHttpClientFactory http, IConfiguration cfg)
    {
        _http = http;
        _cfg = cfg;
    }

    public ContentTiktokResolved Resolve()
    {
        static string? F(params string?[] v) => v.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim();
        return new ContentTiktokResolved(
            F(_cfg["Content:TiktokClientKey"], Environment.GetEnvironmentVariable("TIKTOK_CLIENT_KEY")),
            F(_cfg["Content:TiktokClientSecret"], Environment.GetEnvironmentVariable("TIKTOK_CLIENT_SECRET")),
            F(_cfg["Content:TiktokRedirectUri"]) ?? DefaultRedirectUri,
            IsOn(F(_cfg["Content:TiktokDirectPost"], Environment.GetEnvironmentVariable("TIKTOK_DIRECT_POST"))));
    }

    private static bool IsOn(string? value) =>
        value is not null && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");

    /// <summary>Có bật đăng thẳng (Direct Post) trên máy chủ này không.</summary>
    public bool DirectPostEnabled => Resolve().DirectPost;

    public string BuildAuthUrl(ContentTiktokResolved cfg, string state) =>
        "https://www.tiktok.com/v2/auth/authorize/" +
        $"?client_key={Uri.EscapeDataString(cfg.ClientKey!)}" +
        $"&scope={Uri.EscapeDataString(cfg.DirectPost ? DirectScopes : Scopes)}" +
        "&response_type=code" +
        $"&redirect_uri={Uri.EscapeDataString(cfg.RedirectUri)}" +
        $"&state={Uri.EscapeDataString(state)}";

    public Task<ContentTiktokTokens> ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct)
    {
        var cfg = Require();
        return TokenAsync(new Dictionary<string, string>
        {
            ["client_key"] = cfg.ClientKey!,
            ["client_secret"] = cfg.ClientSecret!,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri,
        }, ct);
    }

    public Task<ContentTiktokTokens> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var cfg = Require();
        return TokenAsync(new Dictionary<string, string>
        {
            ["client_key"] = cfg.ClientKey!,
            ["client_secret"] = cfg.ClientSecret!,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        }, ct);
    }

    public async Task<string?> GetDisplayNameAsync(string accessToken, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,
            "https://open.tiktokapis.com/v2/user/info/?fields=open_id,display_name");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var res = await Http().SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(ApiMessage(body) ?? $"TikTok trả {(int)res.StatusCode}.");
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("data", out var d) && d.TryGetProperty("user", out var u)
            && u.TryGetProperty("display_name", out var n))
            return n.GetString();
        return null;
    }

    /// <summary>Gửi video vào hộp nháp của người dùng (FILE_UPLOAD). Trả publish_id.</summary>
    public Task<string> UploadInboxAsync(string accessToken, string filePath, CancellationToken ct) =>
        UploadAsync(accessToken, filePath, "https://open.tiktokapis.com/v2/post/publish/inbox/video/init/", null, ct);

    /// <summary>
    /// Đăng thẳng lên TikTok (Direct Post, FILE_UPLOAD) kèm chú thích, quyền xem, nhãn AI. Trả publish_id.
    /// Cần quyền video.publish; ứng dụng chưa qua duyệt của TikTok chỉ đăng được ở chế độ riêng tư (SELF_ONLY).
    /// </summary>
    public Task<string> DirectPostAsync(string accessToken, string filePath, ContentTiktokPostInfo post, CancellationToken ct) =>
        UploadAsync(accessToken, filePath, "https://open.tiktokapis.com/v2/post/publish/video/init/", new
        {
            title = post.Title,
            privacy_level = post.PrivacyLevel,
            disable_comment = post.DisableComment,
            disable_duet = post.DisableDuet,
            disable_stitch = post.DisableStitch,
            video_cover_timestamp_ms = post.CoverTimestampMs,
            is_aigc = post.IsAigc,
            brand_content_toggle = false,
            brand_organic_toggle = post.BrandOrganic,
        }, ct);

    /// <summary>Hỏi thông tin tài khoản trước khi đăng thẳng: các chế độ xem được phép, bình luận/duet/stitch có bị tắt không.</summary>
    public async Task<ContentTiktokCreatorInfo> QueryCreatorInfoAsync(string accessToken, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://open.tiktokapis.com/v2/post/publish/creator_info/query/")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var res = await Http().SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw Fail(body, $"TikTok không trả thông tin tài khoản ({(int)res.StatusCode}).");
        using var doc = JsonDocument.Parse(body);
        var d = doc.RootElement.GetProperty("data");
        var options = new List<string>();
        if (d.TryGetProperty("privacy_level_options", out var p) && p.ValueKind == JsonValueKind.Array)
            options.AddRange(p.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0));
        bool B(string k) => d.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;
        int? max = d.TryGetProperty("max_video_post_duration_sec", out var m) && m.TryGetInt32(out var mv) ? mv : null;
        var user = d.TryGetProperty("creator_username", out var u) ? u.GetString() : null;
        return new ContentTiktokCreatorInfo(user, options, B("comment_disabled"), B("duet_disabled"), B("stitch_disabled"), max);
    }

    private async Task<string> UploadAsync(string accessToken, string filePath, string initUrl, object? postInfo, CancellationToken ct)
    {
        var size = new FileInfo(filePath).Length;
        if (size <= 0) throw new InvalidOperationException("File video rỗng.");
        var chunk = size <= MaxSingleChunk ? size : BigChunk;
        var total = (int)(size / chunk);

        var source = new { source = "FILE_UPLOAD", video_size = size, chunk_size = chunk, total_chunk_count = total };
        var init = postInfo is null
            ? JsonSerializer.Serialize(new { source_info = source })
            : JsonSerializer.Serialize(new { post_info = postInfo, source_info = source });
        using var req = new HttpRequestMessage(HttpMethod.Post, initUrl)
        {
            Content = new StringContent(init, Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var res = await Http().SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw Fail(body, $"TikTok từ chối khởi tạo upload ({(int)res.StatusCode}).");
        using var doc = JsonDocument.Parse(body);
        var data = doc.RootElement.GetProperty("data");
        var uploadUrl = data.GetProperty("upload_url").GetString()
                        ?? throw new InvalidOperationException("TikTok không trả địa chỉ upload.");
        var publishId = data.GetProperty("publish_id").GetString()!;

        await using var fs = File.OpenRead(filePath);
        for (var i = 0; i < total; i++)
        {
            var start = i * chunk;
            var len = i == total - 1 ? size - start : chunk;
            var buf = new byte[len];
            await fs.ReadExactlyAsync(buf, ct);
            using var put = new HttpRequestMessage(HttpMethod.Put, uploadUrl) { Content = new ByteArrayContent(buf) };
            put.Content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
            put.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, start + len - 1, size);
            using var putRes = await Http().SendAsync(put, ct);
            if (!putRes.IsSuccessStatusCode)
            {
                var pb = await putRes.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(ApiMessage(pb) ?? $"Upload TikTok lỗi {(int)putRes.StatusCode} ở phần {i + 1}/{total}.");
            }
        }

        return publishId;
    }

    public async Task<(string Status, string? FailReason)> FetchStatusAsync(
        string accessToken, string publishId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://open.tiktokapis.com/v2/post/publish/status/fetch/")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { publish_id = publishId }), Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var res = await Http().SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(ApiMessage(body) ?? $"TikTok trả {(int)res.StatusCode}.");
        using var doc = JsonDocument.Parse(body);
        var d = doc.RootElement.GetProperty("data");
        var status = d.TryGetProperty("status", out var s) ? s.GetString() ?? "UNKNOWN" : "UNKNOWN";
        var fail = d.TryGetProperty("fail_reason", out var f) ? f.GetString() : null;
        return (status, fail);
    }

    private ContentTiktokResolved Require()
    {
        var cfg = Resolve();
        if (string.IsNullOrWhiteSpace(cfg.ClientKey) || string.IsNullOrWhiteSpace(cfg.ClientSecret))
            throw new InvalidOperationException(
                "Chưa cấu hình TikTok — đặt TIKTOK_CLIENT_KEY và TIKTOK_CLIENT_SECRET rồi khởi động lại API.");
        return cfg;
    }

    private async Task<ContentTiktokTokens> TokenAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        HttpResponseMessage res;
        string body;
        try
        {
            res = await Http().PostAsync("https://open.tiktokapis.com/v2/oauth/token/", new FormUrlEncodedContent(form), ct);
            body = await res.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            throw new ContentTiktokTransientException("TikTok chưa trả lời (lỗi mạng hoặc quá thời gian): " + ex.Message, ex);
        }
        using var response = res;
        // Lỗi phía TikTok hoặc bị giới hạn tốc độ: chưa mất kết nối, thử lại sau.
        if ((int)res.StatusCode >= 500 || (int)res.StatusCode == 429)
            throw new ContentTiktokTransientException("TikTok OAuth " + (int)res.StatusCode + " (tạm thời).");
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        }
        catch (JsonException)
        {
            throw new ContentTiktokTransientException("TikTok OAuth trả về dữ liệu không đọc được (" + (int)res.StatusCode + ").");
        }
        using var parsed = doc;
        var r = doc.RootElement;
        if (!res.IsSuccessStatusCode || r.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
        {
            var msg = "TikTok OAuth " + (int)res.StatusCode;
            var desc = r.TryGetProperty("error_description", out var ed) ? ed.GetString() : null;
            if (!string.IsNullOrWhiteSpace(desc)) msg += ": " + desc;
            throw new ContentTiktokAuthException(msg);
        }

        string Get(string k) => r.TryGetProperty(k, out var v) ? v.GetString() ?? "" : "";
        var access = Get("access_token");
        if (string.IsNullOrWhiteSpace(access))
            throw new InvalidOperationException("TikTok không trả access token.");
        return new ContentTiktokTokens(access, Get("refresh_token"), Get("open_id"));
    }

    private HttpClient Http() => _http.CreateClient("content-tiktok");

    private static InvalidOperationException Fail(string body, string fallback)
    {
        var message = ApiMessage(body) ?? fallback;
        return message.Contains("scope_not_authorized", StringComparison.OrdinalIgnoreCase)
               || message.Contains("unaudited_client", StringComparison.OrdinalIgnoreCase)
               || message.Contains("access_token_invalid_scope", StringComparison.OrdinalIgnoreCase)
            ? new ContentTiktokScopeException(message)
            : new InvalidOperationException(message);
    }

    private static string? ApiMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            if (doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Object
                && err.TryGetProperty("message", out var m) && !string.IsNullOrWhiteSpace(m.GetString()))
            {
                var code = err.TryGetProperty("code", out var c) ? c.GetString() : null;
                return string.IsNullOrWhiteSpace(code) ? m.GetString() : $"{code}: {m.GetString()}";
            }
        }
        catch { /* ignore */ }
        return null;
    }
}
