using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentFalClient
{
    internal const string WanModel = "wan-2.1";
    internal const string WanEndpoint = "fal-ai/wan-i2v";
    internal const string TaskPrefix = "wan_";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly ContentRepository _repo;
    private readonly IOptions<ContentOptions> _options;
    private readonly IConfiguration _configuration;

    public ContentFalClient(
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
            _http.BaseAddress = new Uri("https://queue.fal.run/");
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
        if (!resolved.FalConfigured)
            return (false, "Chưa có Fal API key — Model AI → Video, hoặc env FAL_KEY.");

        using var req = new HttpRequestMessage(HttpMethod.Get, $"{WanEndpoint}/requests/not-a-real-request/status");
        ApplyAuth(req, resolved.FalApiKey!);
        using var res = await _http.SendAsync(req, cancellationToken);
        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        if ((int)res.StatusCode is 401 or 403)
            return (false, $"Fal 401: key không hợp lệ. {Truncate(body)}");
        return (true, "Fal key đã nhận · Wan 2.1 I2V.");
    }

    public async Task<string> CreateImageToVideoAsync(
        string imageDataUrl,
        string prompt,
        string? negative,
        int seconds,
        string ratio,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(cancellationToken);
        var key = resolved.FalApiKey
                  ?? throw new InvalidOperationException(
                      "Chưa cấu hình FalApiKey — Model AI → Video hoặc env FAL_KEY.");

        var frames = seconds >= 8 ? 100 : 81;
        var aspect = ratio.Contains("9:16", StringComparison.OrdinalIgnoreCase)
                     || ratio.Contains("720:1280", StringComparison.OrdinalIgnoreCase)
            ? "9:16"
            : "16:9";

        using var req = new HttpRequestMessage(HttpMethod.Post, WanEndpoint);
        ApplyAuth(req, key);
        req.Content = JsonContent.Create(new
        {
            prompt,
            negative_prompt = string.IsNullOrWhiteSpace(negative) ? null : negative.Trim(),
            image_url = imageDataUrl,
            num_frames = frames,
            frames_per_second = 16,
            resolution = "720p",
            aspect_ratio = aspect,
            enable_prompt_expansion = false,
            acceleration = "regular",
        }, options: JsonOpts);

        using var res = await _http.SendAsync(req, cancellationToken);
        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Fal {(int)res.StatusCode}: {Truncate(body)}");

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        var id = ReadRequestId(doc.RootElement);
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException("Fal không trả request id.");
        return TaskPrefix + id;
    }

    public async Task<(string Status, string? VideoUrl, string? Error)> GetTaskAsync(
        string taskId,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(cancellationToken);
        var key = resolved.FalApiKey
                  ?? throw new InvalidOperationException("Chưa cấu hình FalApiKey.");
        var id = StripPrefix(taskId);

        using var statusReq = new HttpRequestMessage(HttpMethod.Get, $"{WanEndpoint}/requests/{Uri.EscapeDataString(id)}/status");
        ApplyAuth(statusReq, key);
        using var statusRes = await _http.SendAsync(statusReq, cancellationToken);
        var statusBody = await statusRes.Content.ReadAsStringAsync(cancellationToken);
        if (!statusRes.IsSuccessStatusCode)
            throw new InvalidOperationException($"Fal {(int)statusRes.StatusCode}: {Truncate(statusBody)}");

        using var statusDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(statusBody) ? "{}" : statusBody);
        var raw = statusDoc.RootElement.TryGetProperty("status", out var st)
            ? st.GetString() ?? "UNKNOWN"
            : "UNKNOWN";
        var status = raw.Trim().ToUpperInvariant() switch
        {
            "COMPLETED" or "COMPLETE" or "SUCCESS" => "SUCCEEDED",
            "IN_QUEUE" or "IN_PROGRESS" or "QUEUED" => "PENDING",
            "FAILED" or "ERROR" => "FAILED",
            var s => s,
        };
        if (status != "SUCCEEDED")
        {
            string? err = null;
            if (statusDoc.RootElement.TryGetProperty("error", out var e))
                err = e.ValueKind == JsonValueKind.String ? e.GetString() : Truncate(e.GetRawText());
            if (status is "FAILED" && string.IsNullOrWhiteSpace(err))
                err = Truncate(statusBody);
            return (status, null, err);
        }

        using var resultReq = new HttpRequestMessage(HttpMethod.Get, $"{WanEndpoint}/requests/{Uri.EscapeDataString(id)}");
        ApplyAuth(resultReq, key);
        using var resultRes = await _http.SendAsync(resultReq, cancellationToken);
        var resultBody = await resultRes.Content.ReadAsStringAsync(cancellationToken);
        if (!resultRes.IsSuccessStatusCode)
            throw new InvalidOperationException($"Fal result {(int)resultRes.StatusCode}: {Truncate(resultBody)}");

        using var resultDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(resultBody) ? "{}" : resultBody);
        var video = ReadVideoUrl(resultDoc.RootElement);
        if (string.IsNullOrWhiteSpace(video))
            return ("FAILED", null, "Fal xong nhưng không có URL video.");
        return ("SUCCEEDED", video, null);
    }

    public static bool IsWanTask(string? taskId) =>
        (taskId ?? "").StartsWith(TaskPrefix, StringComparison.OrdinalIgnoreCase);

    private static string StripPrefix(string taskId)
    {
        var t = taskId.Trim();
        return t.StartsWith(TaskPrefix, StringComparison.OrdinalIgnoreCase)
            ? t[TaskPrefix.Length..]
            : t;
    }

    private static string? ReadRequestId(JsonElement root)
    {
        if (root.TryGetProperty("request_id", out var a) && a.ValueKind == JsonValueKind.String)
            return a.GetString();
        if (root.TryGetProperty("requestId", out var b) && b.ValueKind == JsonValueKind.String)
            return b.GetString();
        return null;
    }

    private static string? ReadVideoUrl(JsonElement root)
    {
        if (root.TryGetProperty("video", out var video))
        {
            if (video.ValueKind == JsonValueKind.String) return video.GetString();
            if (video.ValueKind == JsonValueKind.Object
                && video.TryGetProperty("url", out var u)
                && u.ValueKind == JsonValueKind.String)
                return u.GetString();
        }
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            return ReadVideoUrl(data);
        return null;
    }

    private static void ApplyAuth(HttpRequestMessage req, string key)
    {
        var token = key.StartsWith("Key ", StringComparison.OrdinalIgnoreCase) ? key[4..].Trim() : key.Trim();
        req.Headers.Authorization = new AuthenticationHeaderValue("Key", token);
    }

    private static string Truncate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var t = text.Trim();
        return t.Length <= 400 ? t : t[..400];
    }
}
