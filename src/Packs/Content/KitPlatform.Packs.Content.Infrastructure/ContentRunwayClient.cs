using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentRunwayClient
{
    internal const string ApiVersion = "2024-11-06";
    internal const string TurboModel = "gen4_turbo";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly ContentRepository _repo;
    private readonly IOptions<ContentOptions> _options;
    private readonly IConfiguration _configuration;

    public ContentRunwayClient(
        HttpClient http,
        ContentRepository repo,
        IOptions<ContentOptions> options,
        IConfiguration configuration)
    {
        _http = http;
        _repo = repo;
        _options = options;
        _configuration = configuration;
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri("https://api.dev.runwayml.com/");
    }

    public async Task<ContentVideoResolved> ResolveAsync(CancellationToken cancellationToken)
    {
        var row = await _repo.GetOrgSettingsAsync(cancellationToken);
        return ContentVideoConfigParser.Resolve(
            ContentVideoConfigParser.Parse(row.VideoConfigJson),
            _options.Value,
            _configuration);
    }

    public async Task<(bool Ok, string Message)> TestConnectionAsync(CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(cancellationToken);
        if (!resolved.RunwayConfigured)
            return (false, "Chưa có Runway API key — Model AI → Video, hoặc env RUNWAY_API_KEY.");

        using var req = new HttpRequestMessage(HttpMethod.Get, "v1/organization");
        ApplyAuth(req, resolved.RunwayApiKey!);
        using var res = await _http.SendAsync(req, cancellationToken);
        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        if ((int)res.StatusCode == 401)
            return (false, $"Runway 401: key không hợp lệ. {Truncate(body)}");
        if (res.IsSuccessStatusCode || (int)res.StatusCode == 404)
            return (true, "Runway key đã nhận.");
        return (false, $"Runway {(int)res.StatusCode}: {Truncate(body)}");
    }

    public Task<string> CreateProductionImageToVideoAsync(
        string promptImage,
        string promptText,
        int durationSec,
        string ratio,
        CancellationToken cancellationToken,
        string authorizedKey,
        string? lastImage = null)
    {
        var key = RequireProductionKey(authorizedKey);
        return CreateImageToVideoAsync(
            promptImage, promptText, durationSec, ratio, cancellationToken, lastImage, key);
    }

    public Task<(string Status, string? VideoUrl, string? Error, string? FailureCode)> GetProductionTaskAsync(
        string taskId,
        CancellationToken cancellationToken,
        string authorizedKey)
    {
        var key = RequireProductionKey(authorizedKey);
        return GetTaskAsync(taskId, cancellationToken, key);
    }

    static string RequireProductionKey(string? authorizedKey)
    {
        if (string.IsNullOrWhiteSpace(authorizedKey))
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
        return authorizedKey.Trim();
    }

    public async Task<string> CreateImageToVideoAsync(
        string promptImage,
        string promptText,
        int durationSec,
        string ratio,
        CancellationToken cancellationToken,
        string? lastImage = null,
        string? authorizedKey = null)
    {
        string key;
        if (!string.IsNullOrWhiteSpace(authorizedKey))
            key = authorizedKey.Trim();
        else
        {
            var resolved = await ResolveAsync(cancellationToken);
            key = resolved.RunwayApiKey
                  ?? throw new InvalidOperationException(
                      "Chưa cấu hình RunwayApiKey — Model AI → Video hoặc env RUNWAY_API_KEY.");
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, "v1/image_to_video");
        ApplyAuth(req, key);
        var runwayPrompt = VideoVisualIngressV1Rules.RunwayI2vPrompt(promptText);
        object start = string.IsNullOrWhiteSpace(lastImage)
            ? promptImage
            : new object[]
            {
                new { uri = promptImage, position = "first" },
                new { uri = lastImage, position = "last" },
            };
        object payload = string.IsNullOrWhiteSpace(runwayPrompt)
            ? new { model = TurboModel, promptImage = start, duration = durationSec, ratio }
            : new { model = TurboModel, promptImage = start, promptText = runwayPrompt, duration = durationSec, ratio };
        req.Content = JsonContent.Create(payload, options: JsonOpts);

        using var res = await _http.SendAsync(req, cancellationToken);
        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        EnsureOk(res, body);

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        if (!doc.RootElement.TryGetProperty("id", out var idEl))
            throw new InvalidOperationException("Runway không trả task id.");
        var id = idEl.GetString();
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException("Runway task id trống.");
        return id;
    }

    public async Task<string> CreateTextToImageAsync(
        string promptText,
        string ratio,
        string model,
        int? seed,
        CancellationToken cancellationToken,
        string? authorizedKey = null)
    {
        NovixaE2eIsolation.ThrowIfExternalCall("Runway");
        var built = BtaRunwayTextToImage.Build(promptText, ratio, model, seed);
        string key;
        if (!string.IsNullOrWhiteSpace(authorizedKey))
            key = authorizedKey.Trim();
        else
        {
            var resolved = await ResolveAsync(cancellationToken);
            key = resolved.RunwayApiKey
                  ?? throw new InvalidOperationException(
                      "Chưa cấu hình RunwayApiKey — Model AI → Video hoặc env RUNWAY_API_KEY.");
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, "v1/text_to_image");
        ApplyAuth(req, key);
        object payload = built.Seed is int s
            ? new { model = built.Model, promptText = built.PromptText, ratio = built.Ratio, seed = s }
            : new { model = built.Model, promptText = built.PromptText, ratio = built.Ratio };
        req.Content = JsonContent.Create(payload, options: JsonOpts);

        using var res = await _http.SendAsync(req, cancellationToken);
        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        EnsureOk(res, body);

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        if (!doc.RootElement.TryGetProperty("id", out var idEl))
            throw new InvalidOperationException("Runway không trả task id.");
        var id = idEl.GetString();
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException("Runway task id trống.");
        return id;
    }

    public async Task<(string Status, string? VideoUrl, string? Error, string? FailureCode)> GetTaskAsync(
        string taskId,
        CancellationToken cancellationToken,
        string? authorizedKey = null)
    {
        string key;
        if (!string.IsNullOrWhiteSpace(authorizedKey))
            key = authorizedKey.Trim();
        else
        {
            var resolved = await ResolveAsync(cancellationToken);
            key = resolved.RunwayApiKey
                  ?? throw new InvalidOperationException("Chưa cấu hình RunwayApiKey.");
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, $"v1/tasks/{Uri.EscapeDataString(taskId)}");
        ApplyAuth(req, key);
        using var res = await _http.SendAsync(req, cancellationToken);
        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        EnsureOk(res, body);

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        var read = RunwayTaskResponse.Read(body);
        var video = ReadVideoUrl(doc.RootElement);
        var error = read.Reason;
        if (read.Status is "FAILED" or "CANCELLED" && string.IsNullOrWhiteSpace(error))
            error = Truncate(read.DiagnosticJson);
        if (read.Status == "SUCCEEDED" && string.IsNullOrWhiteSpace(video))
            error = string.IsNullOrWhiteSpace(error)
                ? "SUCCEEDED nhưng không đọc được output URL."
                : error;

        return (read.Status, video, error, read.ProviderFailureCode);
    }

    public async Task<byte[]> DownloadOutputAsync(string videoUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(videoUrl)
            || (!videoUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                && !videoUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(ProductionFailureReasons.UrlAuthorityForbidden);
        using var req = new HttpRequestMessage(HttpMethod.Get, videoUrl);
        using var res = await _http.SendAsync(req, cancellationToken);
        var bytes = await res.Content.ReadAsByteArrayAsync(cancellationToken);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Runway download {(int)res.StatusCode}.");
        if (bytes is null || bytes.Length < 32)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        return bytes;
    }

    private static string? ReadVideoUrl(JsonElement root)
    {
        foreach (var name in new[] { "output", "outputs", "artifacts" })
        {
            if (!root.TryGetProperty(name, out var output)) continue;
            var url = ReadUrlNode(output);
            if (!string.IsNullOrWhiteSpace(url)) return url;
        }
        return ReadUrlNode(root);
    }

    private static string? ReadUrlNode(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.String)
        {
            var s = node.GetString();
            return LooksLikeMediaUrl(s) ? s : null;
        }
        if (node.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "url", "uri", "videoUrl", "href", "video", "mp4", "downloadUrl", "signedUrl" })
            {
                if (node.TryGetProperty(key, out var u) && u.ValueKind == JsonValueKind.String)
                {
                    var s = u.GetString();
                    if (LooksLikeMediaUrl(s)) return s;
                }
            }
            return null;
        }
        if (node.ValueKind != JsonValueKind.Array || node.GetArrayLength() == 0) return null;
        foreach (var item in node.EnumerateArray())
        {
            var url = ReadUrlNode(item);
            if (!string.IsNullOrWhiteSpace(url)) return url;
        }
        return null;
    }

    private static bool LooksLikeMediaUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var t = raw.Trim();
        return t.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyAuth(HttpRequestMessage req, string key)
    {
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        req.Headers.TryAddWithoutValidation("X-Runway-Version", ApiVersion);
    }

    private static void EnsureOk(HttpResponseMessage res, string body)
    {
        if (res.IsSuccessStatusCode) return;
        if ((int)res.StatusCode == 429)
        {
            var retry = ReadRetryAfterSec(res, 30);
            var kind = LooksLikeDailyQuota(body) ? "daily-quota" : "retry-after";
            throw new InvalidOperationException($"Runway 429 {kind}: {retry}: {Truncate(body)}");
        }
        throw new InvalidOperationException($"Runway {(int)res.StatusCode}: {Truncate(body)}");
    }

    private static int ReadRetryAfterSec(HttpResponseMessage res, int fallback)
    {
        var ra = res.Headers.RetryAfter;
        if (ra?.Delta is TimeSpan d)
            return (int)Math.Clamp(d.TotalSeconds, 5, 600);
        if (ra?.Date is DateTimeOffset when)
        {
            var sec = (when - DateTimeOffset.UtcNow).TotalSeconds;
            if (sec > 0) return (int)Math.Clamp(sec, 5, 600);
        }
        return fallback;
    }

    private static bool LooksLikeDailyQuota(string body)
    {
        var t = (body ?? "").ToLowerInvariant();
        return t.Contains("daily")
               || t.Contains("task limit")
               || t.Contains("generation limit")
               || t.Contains("quota exceeded")
               || t.Contains("usage limit")
               || t.Contains("too many generations");
    }

    private static string Truncate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var t = text.Trim();
        return t.Length <= 400 ? t : t[..400];
    }
}
