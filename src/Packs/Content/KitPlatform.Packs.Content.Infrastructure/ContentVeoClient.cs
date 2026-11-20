using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>Vertex Veo I2V. Join = service account, not Gemini. Never logs private_key.</summary>
internal sealed class ContentVeoClient
{
    internal const string CatalogModel = "veo-3.1";
    internal const string VertexModel = "veo-3.1-generate-001";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient _http;
    private readonly ContentRepository _repo;
    private readonly IOptions<ContentOptions> _options;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ContentVeoClient> _logger;
    private readonly KitVideoVideoStore _videos;

    public ContentVeoClient(
        HttpClient http,
        ContentRepository repo,
        IOptions<ContentOptions> options,
        IConfiguration configuration,
        ILogger<ContentVeoClient> logger,
        KitVideoVideoStore videos)
    {
        _http = http;
        _repo = repo;
        _options = options;
        _configuration = configuration;
        _logger = logger;
        _videos = videos;
        _http.Timeout = TimeSpan.FromMinutes(4);
    }

    public async Task<ContentVideoResolved> ResolveAsync(CancellationToken cancellationToken)
    {
        var row = await _repo.GetOrgSettingsAsync(cancellationToken);
        return ContentVideoConfigParser.Resolve(
            ContentVideoConfigParser.Parse(row.VideoConfigJson),
            _options.Value,
            _configuration);
    }

    /// <summary>Auth + model GET only. Does not start Veo generate.</summary>
    public async Task<(bool Ok, string Message)> TestConnectionAsync(CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(cancellationToken);
        var account = FamixaVeoJoinCredentialsV1.TryParseAccount(resolved.VeoJoinFile);
        if (account is null)
        {
            return resolved.VeoJoinConfigured
                ? (false, "File Join đã lưu nhưng không phải service account Vertex.")
                : (false, "Chưa chọn file Join Veo.");
        }

        string token;
        try
        {
            token = await AccessTokenAsync(account, cancellationToken);
        }
        catch (Exception ex)
        {
            return (false, "Veo auth: " + (ex.Message ?? "").Replace("private_key", "redacted", StringComparison.OrdinalIgnoreCase));
        }

        var usageUrl =
            $"https://serviceusage.googleapis.com/v1/projects/{account.ProjectId}/services/aiplatform.googleapis.com";
        using (var usageReq = new HttpRequestMessage(HttpMethod.Get, usageUrl))
        {
            usageReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var usageRes = await _http.SendAsync(usageReq, cancellationToken);
            var usageBody = await usageRes.Content.ReadAsStringAsync(cancellationToken);
            if (usageRes.IsSuccessStatusCode)
            {
                var state = ReadJsonString(usageBody, "state");
                if (string.Equals(state, "ENABLED", StringComparison.OrdinalIgnoreCase))
                    return (true, "Vertex AI API đã bật. Máy Veo 3 sẵn sàng — chưa gửi shot.");
                return (false, $"Vertex AI API state={state ?? "UNKNOWN"}. Chưa ENABLED — không gửi Runway / Wan.");
            }
        }

        var location = Location();
        var dryUrl =
            $"https://{location}-aiplatform.googleapis.com/v1/projects/{account.ProjectId}/locations/{location}/publishers/google/models/{VertexModel}:predictLongRunning";
        using var dry = new HttpRequestMessage(HttpMethod.Post, dryUrl);
        dry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        dry.Content = new StringContent("""{"instances":[]}""", Encoding.UTF8, "application/json");
        using var dryRes = await _http.SendAsync(dry, cancellationToken);
        var dryBody = await dryRes.Content.ReadAsStringAsync(cancellationToken);
        if ((int)dryRes.StatusCode == 403
            && (dryBody.Contains("has not been used", StringComparison.OrdinalIgnoreCase)
                || dryBody.Contains("is disabled", StringComparison.OrdinalIgnoreCase)))
        {
            return (false, "Vertex AI API vẫn tắt hoặc chưa có hiệu lực trên project Join. Đợi vài phút rồi thử lại — không gửi Runway / Wan.");
        }
        if ((int)dryRes.StatusCode is 400 or 404)
            return (true, "Vertex AI API đã nhận máy. Máy Veo 3 sẵn sàng — chưa gửi shot.");
        if (dryRes.IsSuccessStatusCode)
            return (true, "Vertex AI API đã bật. Máy Veo 3 sẵn sàng — chưa gửi shot.");
        if ((int)dryRes.StatusCode == 403)
            return (false, "Vertex 403: service account thiếu quyền Vertex AI User. Không gửi Runway / Wan.");
        return (false, SummarizeVertex("Veo probe", dryRes.StatusCode, dryBody));
    }

    private static string? ReadJsonString(string body, string name)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            return doc.RootElement.TryGetProperty(name, out var el) ? el.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string> CreateImageToVideoAsync(
        string imageDataUrl,
        string prompt,
        int seconds,
        string ratio,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(cancellationToken);
        var account = FamixaVeoJoinCredentialsV1.TryParseAccount(resolved.VeoJoinFile)
                      ?? throw new InvalidOperationException(
                          "Join Veo không phải service account Vertex — Cấu hình AI → Video. Không gửi Runway / Wan.");
        var (mime, b64) = SplitImage(imageDataUrl);
        var duration = seconds >= 8 ? 8 : seconds >= 6 ? 6 : 4;
        var aspect = MapAspect(ratio);
        var location = Location();
        var token = await AccessTokenAsync(account, cancellationToken);
        var url =
            $"https://{location}-aiplatform.googleapis.com/v1/projects/{account.ProjectId}/locations/{location}/publishers/google/models/{VertexModel}:predictLongRunning";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                instances = new[]
                {
                    new
                    {
                        prompt,
                        image = new { bytesBase64Encoded = b64, mimeType = mime },
                    },
                },
                parameters = new
                {
                    sampleCount = 1,
                    aspectRatio = aspect,
                    durationSeconds = duration,
                    resolution = "720p",
                    generateAudio = false,
                },
            }, JsonOpts),
            Encoding.UTF8,
            "application/json");
        using var res = await _http.SendAsync(req, cancellationToken);
        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(SummarizeVertex("Veo start", res.StatusCode, body));
        var name = ReadOperationName(body)
                   ?? throw new InvalidOperationException("Veo không trả operation name.");
        _logger.LogInformation("Veo LRO started project={Project} location={Location}", account.ProjectId, location);
        return EncodeTaskId(name);
    }

    public async Task<(string Status, string? VideoUrl, string? Error)> GetTaskAsync(
        string taskId,
        CancellationToken cancellationToken)
    {
        var operation = DecodeTaskId(taskId);
        var resolved = await ResolveAsync(cancellationToken);
        var account = FamixaVeoJoinCredentialsV1.TryParseAccount(resolved.VeoJoinFile)
                      ?? throw new InvalidOperationException("Join Veo không đọc được khi hỏi lại task.");
        var location = Location();
        var token = await AccessTokenAsync(account, cancellationToken);
        var url =
            $"https://{location}-aiplatform.googleapis.com/v1/projects/{account.ProjectId}/locations/{location}/publishers/google/models/{VertexModel}:fetchPredictOperation";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = new StringContent(
            JsonSerializer.Serialize(new { operationName = operation }, JsonOpts),
            Encoding.UTF8,
            "application/json");
        using var res = await _http.SendAsync(req, cancellationToken);
        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        if (!res.IsSuccessStatusCode)
            return ("FAILED", null, SummarizeVertex("Veo poll", res.StatusCode, body));

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        var root = doc.RootElement;
        if (root.TryGetProperty("error", out var err))
        {
            var msg = err.TryGetProperty("message", out var m) ? m.GetString() : "Veo FAILED.";
            return ("FAILED", null, msg);
        }

        var done = root.TryGetProperty("done", out var doneEl) && doneEl.ValueKind == JsonValueKind.True;
        if (!done)
            return ("RUNNING", null, null);

        if (!TryReadVideoBytes(root, out var bytes, out var gcs))
        {
            if (!string.IsNullOrWhiteSpace(gcs))
                return ("FAILED", null, "Veo trả GCS — chưa tải gs://. Không chuyển Runway / Wan.");
            return ("FAILED", null, "Veo xong nhưng không có file video.");
        }

        var fileId = FamixaVeoTakePlaybackV1.FileId(taskId);
        _videos.PersistNamed("veo", fileId, bytes);
        return ("SUCCEEDED", FamixaVeoTakePlaybackV1.PublicPath(taskId), null);
    }

    internal static string EncodeTaskId(string operationName)
    {
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(operationName))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return "veo_" + b64;
    }

    internal static string DecodeTaskId(string taskId)
    {
        var raw = (taskId ?? "").Trim();
        if (raw.StartsWith("veo_", StringComparison.OrdinalIgnoreCase))
            raw = raw[4..];
        raw = raw.Replace('-', '+').Replace('_', '/');
        while (raw.Length % 4 != 0) raw += "=";
        return Encoding.UTF8.GetString(Convert.FromBase64String(raw));
    }

    private string Location()
    {
        var raw = FirstNonEmpty(
            _options.Value.VeoLocation,
            _configuration["Content:VeoLocation"],
            "us-central1");
        return raw!.Trim();
    }

    private async Task<string> AccessTokenAsync(FamixaVeoJoinCredentialsV1.Account account, CancellationToken cancellationToken)
    {
        var jwt = SignJwt(account);
        using var req = new HttpRequestMessage(HttpMethod.Post, account.TokenUri);
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = jwt,
        });
        using var res = await _http.SendAsync(req, cancellationToken);
        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(SummarizeVertex("Veo auth", res.StatusCode, body));
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        var token = doc.RootElement.TryGetProperty("access_token", out var t) ? t.GetString() : null;
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Veo auth không trả access_token.");
        return token;
    }

    private static string SignJwt(FamixaVeoJoinCredentialsV1.Account account)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = Base64Url(Encoding.UTF8.GetBytes("""{"alg":"RS256","typ":"JWT"}"""));
        var payload = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            iss = account.ClientEmail,
            scope = "https://www.googleapis.com/auth/cloud-platform",
            aud = account.TokenUri,
            iat = now,
            exp = now + 3600,
        })));
        var unsigned = header + "." + payload;
        using var rsa = RSA.Create();
        rsa.ImportFromPem(account.PrivateKey);
        var sig = rsa.SignData(Encoding.UTF8.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return unsigned + "." + Base64Url(sig);
    }

    private static (string Mime, string Base64) SplitImage(string imageDataUrl)
    {
        var raw = (imageDataUrl ?? "").Trim();
        if (!raw.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Veo cần ảnh data-URI — không gửi URL. Không chuyển Runway.");
        var comma = raw.IndexOf(',');
        if (comma < 0)
            throw new InvalidOperationException("Ảnh Veo không đọc được.");
        var header = raw[..comma];
        var mime = "image/jpeg";
        if (header.StartsWith("data:image/png", StringComparison.OrdinalIgnoreCase))
            mime = "image/png";
        else if (header.StartsWith("data:image/webp", StringComparison.OrdinalIgnoreCase))
            mime = "image/webp";
        return (mime, raw[(comma + 1)..]);
    }

    private static string MapAspect(string? ratio)
    {
        var r = (ratio ?? "").Trim().ToLowerInvariant();
        if (r is "9:16" or "9x16" or "720:1280") return "9:16";
        return "16:9";
    }

    private static string? ReadOperationName(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            return doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryReadVideoBytes(JsonElement root, out byte[] bytes, out string? gcsUri)
    {
        bytes = [];
        gcsUri = null;
        if (!root.TryGetProperty("response", out var response))
            return false;

        if (response.TryGetProperty("videos", out var videos) && videos.ValueKind == JsonValueKind.Array)
        {
            foreach (var video in videos.EnumerateArray())
            {
                if (TryBytes(video, out bytes, out gcsUri))
                    return bytes.Length > 0;
            }
        }

        if (response.TryGetProperty("generatedSamples", out var samples) && samples.ValueKind == JsonValueKind.Array)
        {
            foreach (var sample in samples.EnumerateArray())
            {
                if (sample.TryGetProperty("video", out var video) && TryBytes(video, out bytes, out gcsUri))
                    return bytes.Length > 0;
            }
        }

        return TryBytes(response, out bytes, out gcsUri) && bytes.Length > 0;
    }

    private static bool TryBytes(JsonElement video, out byte[] bytes, out string? gcsUri)
    {
        bytes = [];
        gcsUri = video.TryGetProperty("uri", out var uri) ? uri.GetString()
            : video.TryGetProperty("gcsUri", out var gcs) ? gcs.GetString()
            : null;
        var b64 = video.TryGetProperty("bytesBase64Encoded", out var encoded) ? encoded.GetString() : null;
        if (string.IsNullOrWhiteSpace(b64))
            return false;
        try
        {
            bytes = Convert.FromBase64String(b64);
            return bytes.Length > 32;
        }
        catch
        {
            return false;
        }
    }

    private static string SummarizeVertex(string step, System.Net.HttpStatusCode status, string body)
    {
        var excerpt = (body ?? "").Trim();
        if (excerpt.Length > 240) excerpt = excerpt[..240];
        excerpt = excerpt.Replace("private_key", "redacted", StringComparison.OrdinalIgnoreCase);
        return $"{step} {(int)status}: {excerpt}";
    }

    private static string Base64Url(byte[] raw) =>
        Convert.ToBase64String(raw).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v)) return v;
        }
        return null;
    }
}
