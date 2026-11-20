using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed record ContentYoutubeResolved(string? ClientId, string? ClientSecret, string? RedirectUri);

/// <summary>Google OAuth or YouTube API error. <see cref="Error"/> is the OAuth error code; <see cref="Reason"/> the API errors[0].reason.</summary>
internal sealed class ContentYoutubeException : InvalidOperationException
{
    public ContentYoutubeException(string message, int statusCode, string? error = null, string? reason = null)
        : base(message)
    {
        StatusCode = statusCode;
        Error = error;
        Reason = reason;
    }

    public int StatusCode { get; }
    public string? Error { get; }
    public string? Reason { get; }
    public bool IsInvalidGrant => string.Equals(Error, "invalid_grant", StringComparison.Ordinal);
}

/// <summary>YouTube calls made after the video is uploaded.</summary>
internal interface IContentYoutubeApi
{
    Task<string> CreatePlaylistAsync(string accessToken, string title, string privacy, CancellationToken ct);
    Task AddToPlaylistAsync(string accessToken, string playlistId, string videoId, CancellationToken ct);
    Task InsertCaptionAsync(string accessToken, string videoId, string language, string name, string filePath, CancellationToken ct);
    Task SetThumbnailAsync(string accessToken, string videoId, string filePath, string contentType, CancellationToken ct);
}

/// <summary>Google OAuth + YouTube Data API v3 (resumable upload). Client ID/Secret đọc từ cấu hình / biến môi trường.</summary>
internal sealed class ContentYoutubeClient : IContentYoutubeApi
{
    internal const string DevelopmentRedirectUri = "http://localhost:5173/content/youtube/callback";
    internal const string Scopes =
        "https://www.googleapis.com/auth/youtube.upload https://www.googleapis.com/auth/youtube.readonly " +
        "https://www.googleapis.com/auth/youtube.force-ssl";

    private const string Api = "https://www.googleapis.com/youtube/v3";
    private const string UploadApi = "https://www.googleapis.com/upload/youtube/v3";

    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _cfg;
    private readonly IHostEnvironment _env;

    public ContentYoutubeClient(IHttpClientFactory http, IConfiguration cfg, IHostEnvironment env)
    {
        _http = http;
        _cfg = cfg;
        _env = env;
    }

    public ContentYoutubeResolved Resolve()
    {
        static string? F(params string?[] v) => v.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim();
        return new ContentYoutubeResolved(
            F(_cfg["Content:YoutubeClientId"], Environment.GetEnvironmentVariable("YOUTUBE_CLIENT_ID")),
            F(_cfg["Content:YoutubeClientSecret"], Environment.GetEnvironmentVariable("YOUTUBE_CLIENT_SECRET")),
            ResolveRedirectUri(_cfg["Content:YoutubeRedirectUri"], Environment.GetEnvironmentVariable("YOUTUBE_REDIRECT_URI"),
                _env.IsDevelopment()));
    }

    /// <summary>Configured URI first. Only a Development host falls back to the Vite dev server.</summary>
    internal static string? ResolveRedirectUri(string? configured, string? environmentValue, bool isDevelopment)
    {
        var value = new[] { configured, environmentValue }.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim();
        return value ?? (isDevelopment ? DevelopmentRedirectUri : null);
    }

    public string BuildAuthUrl(ContentYoutubeResolved cfg, string state) =>
        "https://accounts.google.com/o/oauth2/v2/auth" +
        $"?client_id={Uri.EscapeDataString(cfg.ClientId!)}" +
        $"&redirect_uri={Uri.EscapeDataString(cfg.RedirectUri!)}" +
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
        var (access, _) = await AccessTokenWithScopeAsync(refreshToken, ct);
        return access;
    }

    /// <summary>Access token và scope Google trả về. Scope rỗng khi Google không liệt kê quyền.</summary>
    public async Task<(string AccessToken, string? Scope)> AccessTokenWithScopeAsync(string refreshToken, CancellationToken ct)
    {
        var cfg = Require();
        using var doc = await TokenAsync(new Dictionary<string, string>
        {
            ["refresh_token"] = refreshToken,
            ["client_id"] = cfg.ClientId!,
            ["client_secret"] = cfg.ClientSecret!,
            ["grant_type"] = "refresh_token",
        }, ct);
        var access = doc.RootElement.TryGetProperty("access_token", out var t) ? t.GetString() : null;
        if (string.IsNullOrWhiteSpace(access))
            throw new InvalidOperationException("Google không trả access token.");
        var scope = doc.RootElement.TryGetProperty("scope", out var s) && s.ValueKind == JsonValueKind.String
            ? s.GetString()
            : null;
        return (access, scope);
    }

    /// <summary>GET YouTube Data API. Query không được chứa token.</summary>
    internal async Task<string> GetJsonAsync(string accessToken, string query, CancellationToken ct)
    {
        if (query.Contains("access_token", StringComparison.OrdinalIgnoreCase)
            || query.Contains("://", StringComparison.Ordinal))
            throw new InvalidOperationException("YouTube query không hợp lệ.");
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{Api}/{query.TrimStart('/')}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var res = await Http().SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw ApiError(body, (int)res.StatusCode, $"YouTube trả {(int)res.StatusCode}.");
        return string.IsNullOrWhiteSpace(body) ? "{}" : body;
    }

    public async Task<(string Id, string Title)> GetMyChannelAsync(string accessToken, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{Api}/channels?part=snippet&mine=true");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var res = await Http().SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw ApiError(body, (int)res.StatusCode, $"YouTube trả {(int)res.StatusCode}.");
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
        IReadOnlyList<string>? tags,
        bool? containsSyntheticMedia,
        CancellationToken ct,
        string? categoryId = null,
        string? language = null)
    {
        var meta = BuildUploadMetadata(title, description, privacy, tags, containsSyntheticMedia, categoryId, language);
        var length = new FileInfo(filePath).Length;
        using var init = new HttpRequestMessage(HttpMethod.Post,
            $"{UploadApi}/videos?uploadType=resumable&part=snippet,status");
        init.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        init.Headers.Add("X-Upload-Content-Type", contentType);
        init.Headers.Add("X-Upload-Content-Length", length.ToString());
        init.Content = new StringContent(meta, Encoding.UTF8, "application/json");
        using var initRes = await Http().SendAsync(init, ct);
        if (!initRes.IsSuccessStatusCode)
        {
            var b = await initRes.Content.ReadAsStringAsync(ct);
            throw ApiError(b, (int)initRes.StatusCode, $"YouTube từ chối khởi tạo upload ({(int)initRes.StatusCode}).");
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
            throw ApiError(putBody, (int)putRes.StatusCode, $"Upload YouTube lỗi {(int)putRes.StatusCode}.");
        using var doc = JsonDocument.Parse(putBody);
        var id = doc.RootElement.GetProperty("id").GetString()!;
        string? actual = null;
        if (doc.RootElement.TryGetProperty("status", out var st) && st.TryGetProperty("privacyStatus", out var ps))
            actual = ps.GetString();
        return (id, actual);
    }

    internal static string BuildUploadMetadata(
        string title,
        string description,
        string privacy,
        IReadOnlyList<string>? tags,
        bool? containsSyntheticMedia,
        string? categoryId = null,
        string? language = null)
    {
        var category = string.IsNullOrWhiteSpace(categoryId) || !categoryId.Trim().All(char.IsDigit) ? "27" : categoryId.Trim();
        var snippet = new Dictionary<string, object> { ["title"] = title, ["description"] = description, ["categoryId"] = category };
        if (tags is { Count: > 0 }) snippet["tags"] = tags;
        if (!string.IsNullOrWhiteSpace(language))
        {
            // Ngôn ngữ tiêu đề/mô tả và ngôn ngữ giọng đọc: YouTube dùng để gợi ý đúng người xem và bật phụ đề tự động.
            snippet["defaultLanguage"] = language.Trim();
            snippet["defaultAudioLanguage"] = language.Trim();
        }
        var status = new Dictionary<string, object>
        {
            ["privacyStatus"] = privacy,
            ["selfDeclaredMadeForKids"] = false,
            ["embeddable"] = true,
            ["publicStatsViewable"] = true,
            ["license"] = "youtube",
        };
        if (containsSyntheticMedia is bool synthetic) status["containsSyntheticMedia"] = synthetic;
        return JsonSerializer.Serialize(new { snippet, status });
    }

    public async Task<string> CreatePlaylistAsync(string accessToken, string title, string privacy, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new
        {
            snippet = new { title },
            status = new { privacyStatus = privacy },
        });
        using var doc = await SendJsonAsync(accessToken, $"{Api}/playlists?part=snippet,status", body, ct);
        return doc.RootElement.GetProperty("id").GetString()
               ?? throw new InvalidOperationException("YouTube không trả id playlist.");
    }

    public async Task<string> CreateDescribedPlaylistAsync(
        string accessToken, string title, string? description, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new
        {
            snippet = new { title, description = description ?? "" },
            status = new { privacyStatus = "public" },
        });
        using var doc = await SendJsonAsync(accessToken, $"{Api}/playlists?part=snippet,status", body, ct);
        return doc.RootElement.GetProperty("id").GetString()
               ?? throw new InvalidOperationException("YouTube không trả id playlist.");
    }

    public async Task<List<(string Id, string Title, string Description)>> ListMyPlaylistsAsync(
        string accessToken, CancellationToken ct)
    {
        var found = new List<(string Id, string Title, string Description)>();
        string? page = null;
        for (var i = 0; i < 2; i++)
        {
            var url = $"{Api}/playlists?part=snippet&mine=true&maxResults=50"
                      + (page is null ? "" : $"&pageToken={Uri.EscapeDataString(page)}");
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var res = await Http().SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                throw ApiError(body, (int)res.StatusCode, $"YouTube playlists {(int)res.StatusCode}.");
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                    var sn = item.TryGetProperty("snippet", out var snippet) ? snippet : default;
                    var title = sn.ValueKind == JsonValueKind.Object && sn.TryGetProperty("title", out var t) ? t.GetString() : null;
                    var description = sn.ValueKind == JsonValueKind.Object && sn.TryGetProperty("description", out var d) ? d.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(title))
                        found.Add((id, title.Trim(), (description ?? "").Trim()));
                }
            }
            page = doc.RootElement.TryGetProperty("nextPageToken", out var np) ? np.GetString() : null;
            if (string.IsNullOrWhiteSpace(page)) break;
        }
        return found;
    }

    /// <summary>Tìm danh sách phát của kênh theo đúng tên (không phân biệt hoa thường). Không có thì trả null.</summary>
    public async Task<string?> FindPlaylistByTitleAsync(string accessToken, string title, CancellationToken ct)
    {
        var want = title.Trim();
        string? page = null;
        for (var i = 0; i < 10; i++)
        {
            var url = $"{Api}/playlists?part=snippet&mine=true&maxResults=50" + (page is null ? "" : $"&pageToken={Uri.EscapeDataString(page)}");
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var res = await Http().SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                throw ApiError(body, (int)res.StatusCode, $"YouTube playlists {(int)res.StatusCode}.");
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    var name = item.TryGetProperty("snippet", out var sn) && sn.TryGetProperty("title", out var t) ? t.GetString() : null;
                    if (name is not null && string.Equals(name.Trim(), want, StringComparison.OrdinalIgnoreCase))
                        return item.GetProperty("id").GetString();
                }
            }
            page = doc.RootElement.TryGetProperty("nextPageToken", out var np) ? np.GetString() : null;
            if (string.IsNullOrWhiteSpace(page)) break;
        }
        return null;
    }

    public async Task AddToPlaylistAsync(string accessToken, string playlistId, string videoId, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new
        {
            snippet = new { playlistId, resourceId = new { kind = "youtube#video", videoId } },
        });
        using var _ = await SendJsonAsync(accessToken, $"{Api}/playlistItems?part=snippet", body, ct);
    }

    public async Task InsertCaptionAsync(
        string accessToken, string videoId, string language, string name, string filePath, CancellationToken ct)
    {
        var meta = JsonSerializer.Serialize(new { snippet = new { videoId, language, name, isDraft = false } });
        using var form = new MultipartContent("related");
        form.Add(new StringContent(meta, Encoding.UTF8, "application/json"));
        var file = new ByteArrayContent(await File.ReadAllBytesAsync(filePath, ct));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file);
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{UploadApi}/captions?uploadType=multipart&part=snippet")
        {
            Content = form,
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var res = await Http().SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
            throw ApiError(await res.Content.ReadAsStringAsync(ct), (int)res.StatusCode, $"YouTube captions {(int)res.StatusCode}.");
    }

    public async Task<string> InsertCommentAsync(string accessToken, string videoId, string text, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new
        {
            snippet = new
            {
                videoId,
                topLevelComment = new { snippet = new { textOriginal = text } },
            },
        });
        using var doc = await SendJsonAsync(accessToken, $"{Api}/commentThreads?part=snippet", body, ct);
        return doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "";
    }

    public async Task<string?> VideoPrivacyAsync(string accessToken, string videoId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"{Api}/videos?part=status&id={Uri.EscapeDataString(videoId)}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var res = await Http().SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw ApiError(body, (int)res.StatusCode, $"YouTube videos {(int)res.StatusCode}.");
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
            return null;
        var status = items[0].TryGetProperty("status", out var st) ? st : default;
        return status.ValueKind == JsonValueKind.Object && status.TryGetProperty("privacyStatus", out var ps)
            ? ps.GetString()
            : null;
    }

    public async Task SetThumbnailAsync(
        string accessToken, string videoId, string filePath, string contentType, CancellationToken ct)
    {
        var content = new ByteArrayContent(await File.ReadAllBytesAsync(filePath, ct));
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"{UploadApi}/thumbnails/set?videoId={Uri.EscapeDataString(videoId)}&uploadType=media")
        {
            Content = content,
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var res = await Http().SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
            throw ApiError(await res.Content.ReadAsStringAsync(ct), (int)res.StatusCode, $"YouTube thumbnails {(int)res.StatusCode}.");
    }

    private async Task<JsonDocument> SendJsonAsync(string accessToken, string url, string json, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var res = await Http().SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw ApiError(body, (int)res.StatusCode, $"YouTube trả {(int)res.StatusCode}.");
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
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
            string? error = null;
            try
            {
                using var d = JsonDocument.Parse(body);
                error = d.RootElement.TryGetProperty("error", out var er) && er.ValueKind == JsonValueKind.String ? er.GetString() : null;
                var e = d.RootElement.TryGetProperty("error_description", out var ed) ? ed.GetString() : null;
                if (!string.IsNullOrWhiteSpace(e)) msg += ": " + e;
            }
            catch { /* keep generic */ }
            throw new ContentYoutubeException(msg, (int)res.StatusCode, error);
        }
        return JsonDocument.Parse(body);
    }

    private HttpClient Http() => _http.CreateClient("content-youtube");

    private static ContentYoutubeException ApiError(string body, int statusCode, string fallback)
    {
        string? message = null;
        string? reason = null;
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            if (doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Object)
            {
                if (err.TryGetProperty("message", out var m)) message = m.GetString();
                if (err.TryGetProperty("errors", out var list) && list.ValueKind == JsonValueKind.Array && list.GetArrayLength() > 0
                    && list[0].TryGetProperty("reason", out var r))
                    reason = r.GetString();
            }
        }
        catch { /* ignore */ }
        return new ContentYoutubeException(message ?? fallback, statusCode, reason: reason);
    }
}
