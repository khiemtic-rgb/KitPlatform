using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentFalClient
{
    internal const string WanModel = "wan-2.1";
    internal const string WanEndpoint = "fal-ai/wan-i2v";
    internal const string TaskPrefix = "wan_";
    internal const string LipsyncModel = "sync-lipsync-1.9";
    internal const string LipsyncEndpoint = "fal-ai/sync-lipsync";
    internal const string LipsyncVariant = "lipsync-1.9.0-beta";
    internal const string LipsyncPrefix = "lipsync_";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly ContentRepository _repo;
    private readonly IOptions<ContentOptions> _options;
    private readonly IConfiguration _configuration;
    private readonly KitVideoVideoStore _videos;

    public ContentFalClient(
        HttpClient http,
        ContentRepository repo,
        IOptions<ContentOptions> options,
        IConfiguration configuration,
        KitVideoVideoStore videos)
    {
        _http = http;
        _repo = repo;
        _options = options;
        _configuration = configuration;
        _videos = videos;
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

    public async Task<string> UploadAsync(
        byte[] bytes,
        string contentType,
        string fileName,
        CancellationToken cancellationToken,
        string? authorizedKey = null)
    {
        if (bytes.Length is < 32 or > 12_000_000)
            throw new InvalidOperationException($"File Fal upload {bytes.Length} byte — cần 32B–12MB.");
        string key;
        if (!string.IsNullOrWhiteSpace(authorizedKey))
            key = authorizedKey.Trim();
        else
        {
            var resolved = await ResolveAsync(cancellationToken);
            key = resolved.FalApiKey
                  ?? throw new InvalidOperationException("Chưa cấu hình FalApiKey.");
        }
        var mime = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
        var name = string.IsNullOrWhiteSpace(fileName) ? "asset.bin" : fileName.Trim();

        using var init = new HttpRequestMessage(
            HttpMethod.Post,
            "https://rest.alpha.fal.ai/storage/upload/initiate?storage_type=fal-cdn-v3");
        ApplyAuth(init, key);
        init.Content = JsonContent.Create(new { content_type = mime, file_name = name }, options: JsonOpts);
        using var initRes = await _http.SendAsync(init, cancellationToken);
        var initBody = await initRes.Content.ReadAsStringAsync(cancellationToken);
        if (!initRes.IsSuccessStatusCode)
            throw new InvalidOperationException($"Fal upload {(int)initRes.StatusCode}: {Truncate(initBody)}");

        using var initDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(initBody) ? "{}" : initBody);
        var uploadUrl = ReadString(initDoc.RootElement, "upload_url") ?? ReadString(initDoc.RootElement, "uploadUrl");
        var fileUrl = ReadString(initDoc.RootElement, "file_url")
                      ?? ReadString(initDoc.RootElement, "fileUrl")
                      ?? ReadString(initDoc.RootElement, "access_url");
        if (string.IsNullOrWhiteSpace(uploadUrl) || string.IsNullOrWhiteSpace(fileUrl))
            throw new InvalidOperationException("Fal upload không trả URL.");

        using var put = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
        put.Content = new ByteArrayContent(bytes);
        put.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mime);
        using var putRes = await _http.SendAsync(put, cancellationToken);
        if (!putRes.IsSuccessStatusCode)
        {
            var putBody = await putRes.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Fal PUT {(int)putRes.StatusCode}: {Truncate(putBody)}");
        }
        return fileUrl;
    }

    public async Task<(string ProviderJobId, byte[] Bytes, string MimeType)> ExecuteProductionLipsyncOnceAsync(
        byte[] videoBytes,
        byte[] audioBytes,
        double speakerX,
        double speakerY,
        int speakerWidth,
        int speakerHeight,
        int speakerFrame,
        double videoDurationSec,
        CancellationToken cancellationToken,
        string? syncMode = null,
        int[]? faceBoxXyxy = null,
        string? authorizedKey = null)
    {
        authorizedKey = RequireProductionKey(authorizedKey);
        var videoUrl = await UploadAsync(videoBytes, "video/mp4", "take.mp4", cancellationToken, authorizedKey);
        var audioUrl = await UploadAsync(audioBytes, "audio/mpeg", "voice.mp3", cancellationToken, authorizedKey);
        var frameCount = FamixaFalLipsyncSpeakerPinV1.FrameCountFromTake(videoBytes, videoDurationSec);
        var created = await CreateLipsyncAsync(
            videoUrl, audioUrl, string.IsNullOrWhiteSpace(syncMode) ? "silence" : syncMode, "v3", cancellationToken,
            speakerX, speakerY, speakerWidth, speakerHeight, speakerFrame, videoDurationSec, frameCount, faceBoxXyxy, authorizedKey);
        var bytes = TryReadStoredFalBytes(created.TaskId);
        if (bytes is null && !string.IsNullOrWhiteSpace(created.VideoUrl))
            bytes = await TryDownloadOutputAsync(created.VideoUrl, cancellationToken);
        if (bytes is null && !string.IsNullOrWhiteSpace(created.TaskId))
        {
            var polled = await GetTaskAsync(created.TaskId, cancellationToken, authorizedKey);
            bytes = TryReadStoredFalBytes(created.TaskId);
            if (bytes is null && !string.IsNullOrWhiteSpace(polled.VideoUrl))
                bytes = await TryDownloadOutputAsync(polled.VideoUrl, cancellationToken);
            if (bytes is null
                && string.Equals(polled.Status, "FAILED", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(polled.Error ?? "FAILED_IDENTITY");
        }
        if (bytes is not { Length: >= 32 })
            throw new InvalidOperationException("FAILED_IDENTITY");
        return (created.TaskId, bytes, "video/mp4");
    }

    public async Task<(string Status, string ProviderJobId, byte[]? Bytes, string MimeType, string? Error)>
        RecoverProductionLipsyncOutputAsync(string providerJobId, CancellationToken cancellationToken, string? authorizedKey = null)
    {
        authorizedKey = RequireProductionKey(authorizedKey);
        var original = (providerJobId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(original))
            return ("FAILED", original, null, "video/mp4", ProductionFailureReasons.RecoveryFailed);
        var pollId = NormalizeLipsyncRecoverTaskId(original);
        string? lastError = null;
        for (var i = 0; i < 30; i++)
        {
            try
            {
                var polled = await GetTaskAsync(pollId, cancellationToken, authorizedKey);
                lastError = polled.Error;
                var bytes = TryReadStoredFalBytes(pollId);
                if (bytes is null && !string.IsNullOrWhiteSpace(polled.VideoUrl))
                    bytes = await TryDownloadOutputAsync(polled.VideoUrl, cancellationToken);
                if (bytes is { Length: >= 32 })
                    return ("SUCCEEDED", original, bytes, "video/mp4", null);
                var status = string.IsNullOrWhiteSpace(polled.Status) ? "PENDING" : polled.Status.Trim().ToUpperInvariant();
                if (status is "FAILED" or "ERROR" or "NOT_FOUND" or "EXPIRED" or "UNAVAILABLE")
                    return (status, original, null, "video/mp4", polled.Error ?? ProductionFailureReasons.RecoveryFailed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex.Message;
                var upper = lastError.ToUpperInvariant();
                if (upper.Contains("NOT_FOUND") || upper.Contains("404") || upper.Contains("EXPIRED") || upper.Contains("UNAVAILABLE"))
                    return ("FAILED", original, null, "video/mp4", lastError);
            }
            await Task.Delay(4000, cancellationToken);
        }
        return ("UNAVAILABLE", original, null, "video/mp4", lastError ?? ProductionFailureReasons.RecoveryFailed);
    }

    static string RequireProductionKey(string? authorizedKey)
    {
        if (string.IsNullOrWhiteSpace(authorizedKey))
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
        return authorizedKey.Trim();
    }

    public static string NormalizeLipsyncRecoverTaskId(string providerJobId)
    {
        var t = (providerJobId ?? "").Trim();
        if (t.Length == 0 || IsLipsyncTask(t)) return t;
        return Guid.TryParse(t, out var id) ? "lipsync_v3_" + id.ToString("D") : t;
    }

    byte[]? TryReadStoredFalBytes(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId)) return null;
        var fileId = FamixaFalLipsyncPlaybackV1.FileId(taskId);
        var bytes = _videos.TryReadNamed("fal", fileId);
        return bytes is { Length: >= 32 } ? bytes : null;
    }

    public async Task<(string TaskId, string Status, string? VideoUrl, string Model)> CreateLipsyncAsync(
        string videoUrl,
        string audioUrl,
        string? syncMode,
        string? modelKind,
        CancellationToken cancellationToken,
        double? speakerX = null,
        double? speakerY = null,
        int? speakerVideoWidth = null,
        int? speakerVideoHeight = null,
        int? speakerFrameNumber = null,
        double? speakerDurationSec = null,
        int? speakerFrameCount = null,
        int[]? faceBoxXyxy = null,
        string? authorizedKey = null)
    {
        string key;
        if (!string.IsNullOrWhiteSpace(authorizedKey))
            key = authorizedKey.Trim();
        else
        {
            var resolved = await ResolveAsync(cancellationToken);
            key = resolved.FalApiKey
                  ?? throw new InvalidOperationException(
                      "Chưa cấu hình FalApiKey — Model AI → Video, hoặc env FAL_KEY.");
        }

        var kind = NormalizeLipsyncKind(modelKind);
        var endpoint = kind switch
        {
            "v3" => "fal-ai/sync-lipsync/v3",
            "ls" => "fal-ai/latentsync",
            _ => LipsyncEndpoint,
        };
        var prefix = kind switch
        {
            "v3" => "lipsync_v3_",
            "ls" => "lipsync_ls_",
            _ => LipsyncPrefix,
        };
        var modelLabel = kind switch
        {
            "v3" => "sync-lipsync-v3",
            "ls" => "latentsync",
            _ => LipsyncModel,
        };
        var mode = NormalizeSyncMode(syncMode);
        var clipUrl = videoUrl.Trim();
        var audio = audioUrl.Trim();

        // One POST. Queue first (returns request_id). fal.run only if queue rejects the same route.
        // Never retry 504 / never fan-out to another model — each 200 is a paid Fal job.
        using var queueReq = new HttpRequestMessage(HttpMethod.Post, endpoint);
        ApplyAuth(queueReq, key);
        queueReq.Content = LipsyncPayload(kind, clipUrl, audio, mode, speakerX, speakerY, speakerVideoWidth, speakerVideoHeight, speakerFrameNumber, speakerDurationSec, speakerFrameCount, faceBoxXyxy);
        using var queueRes = await _http.SendAsync(queueReq, cancellationToken);
        var queueBody = await queueRes.Content.ReadAsStringAsync(cancellationToken);
        var queueCode = (int)queueRes.StatusCode;
        if (queueCode == 402)
            throw new InvalidOperationException("Fal hết tiền (402). Nạp trên fal.ai. Đừng gửi lại job cũ.");
        if (queueRes.IsSuccessStatusCode)
            return await FinishLipsyncCreateAsync(key, queueBody, endpoint, prefix, modelLabel, cancellationToken);
        if (queueCode is not (404 or 405))
            throw LipsyncAcceptOrFail(queueCode, queueBody);

        using var syncReq = new HttpRequestMessage(HttpMethod.Post, $"https://fal.run/{endpoint}");
        ApplyAuth(syncReq, key);
        syncReq.Content = LipsyncPayload(kind, clipUrl, audio, mode, speakerX, speakerY, speakerVideoWidth, speakerVideoHeight, speakerFrameNumber, speakerDurationSec, speakerFrameCount, faceBoxXyxy);
        using var syncRes = await _http.SendAsync(syncReq, cancellationToken);
        var syncBody = await syncRes.Content.ReadAsStringAsync(cancellationToken);
        var syncCode = (int)syncRes.StatusCode;
        if (syncCode == 402)
            throw new InvalidOperationException("Fal hết tiền (402). Nạp trên fal.ai. Đừng gửi lại job cũ.");
        if (!syncRes.IsSuccessStatusCode)
            throw LipsyncAcceptOrFail(syncCode, syncBody);
        return await FinishLipsyncCreateAsync(key, syncBody, endpoint, prefix, modelLabel, cancellationToken);
    }

    private async Task<(string TaskId, string Status, string? VideoUrl, string Model)> FinishLipsyncCreateAsync(
        string key,
        string body,
        string endpoint,
        string prefix,
        string modelLabel,
        CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        var video = FamixaFalLipsyncResultV1.ReadVideoUrl(doc.RootElement);
        var id = ReadRequestId(doc.RootElement);
        if (!string.IsNullOrWhiteSpace(video))
        {
            var taskId = prefix + (string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id);
            return (taskId, "SUCCEEDED", await PersistFalOutputAsync(taskId, video, cancellationToken), modelLabel);
        }
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException("Fal lipsync không trả request id. Kiểm tra fal.ai → Usage trước khi gửi job mới.");
        var waited = await WaitLipsyncAsync(
            key,
            id,
            endpoint,
            ReadString(doc.RootElement, "status_url") ?? ReadString(doc.RootElement, "statusUrl"),
            ReadString(doc.RootElement, "response_url") ?? ReadString(doc.RootElement, "responseUrl"),
            cancellationToken,
            240);
        if (!string.IsNullOrWhiteSpace(waited.VideoUrl))
            return (prefix + id, "SUCCEEDED", await PersistFalOutputAsync(prefix + id, waited.VideoUrl, cancellationToken), modelLabel);
        if (waited.Status == "FAILED"
            && !string.IsNullOrWhiteSpace(waited.Error)
            && !waited.Error.Contains("405", StringComparison.OrdinalIgnoreCase)
            && !waited.Error.Contains("vẫn chạy", StringComparison.OrdinalIgnoreCase)
            && !waited.Error.Contains("quá ", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(waited.Error);
        }
        return (prefix + id, "PENDING", null, modelLabel);
    }

    private static InvalidOperationException LipsyncAcceptOrFail(int code, string body)
    {
        if (code is 502 or 503 or 504 || LooksLikeDownstreamDown(body))
            return new InvalidOperationException(
                "Fal quá tải (504). Có thể đã nhận job và trừ. Mở fal.ai → Usage. Đừng bấm Khớp môi lại.");
        return new InvalidOperationException($"Fal lipsync {code}: {Truncate(body)}");
    }

    private async Task<(int StatusCode, string Body)> FalGetAsync(
        string key,
        string url,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(6));
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyAuth(req, key);
        req.Headers.Accept.ParseAdd("application/json");
        using var res = await _http.SendAsync(req, cts.Token);
        var body = await res.Content.ReadAsStringAsync(cts.Token);
        return ((int)res.StatusCode, body);
    }

    private async Task<(string Status, string? VideoUrl, string? Error)> PeekLipsyncAsync(
        string key,
        string requestId,
        string endpoint,
        string? statusUrl,
        string? resultUrl,
        CancellationToken cancellationToken)
    {
        var urls = FamixaFalLipsyncPollV1.PeekPaths(
            FamixaFalLipsyncPollV1.QueueApp(endpoint),
            requestId,
            statusUrl,
            resultUrl);

        var hits = await Task.WhenAll(urls.Select(async url =>
        {
            try
            {
                return await FalGetAsync(key, url!, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return (StatusCode: 0, Body: "");
            }
            catch (HttpRequestException)
            {
                return (StatusCode: 0, Body: "");
            }
        }));

        string? last = null;
        string? resultKeys = null;
        foreach (var hit in hits)
        {
            if (hit.StatusCode is 0) continue;
            if (hit.StatusCode is 404 or 405)
            {
                last = $"Fal đã nhận job {requestId} (có thể đã trừ). KIT hỏi sai URL ({hit.StatusCode}). Gắn Fal · 0$ từ Usage.";
                continue;
            }
            if (hit.StatusCode is < 200 or >= 300)
            {
                last = $"Fal {hit.StatusCode}: {Truncate(hit.Body)}";
                continue;
            }
            var video = FamixaFalLipsyncResultV1.ReadVideoUrl(hit.Body);
            if (!string.IsNullOrWhiteSpace(video))
                return ("SUCCEEDED", video, null);
            resultKeys ??= FamixaFalLipsyncResultV1.DescribeKeys(hit.Body);
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(hit.Body) ? "{}" : hit.Body);
                var raw = doc.RootElement.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "";
                var status = raw.Trim().ToUpperInvariant() switch
                {
                    "COMPLETED" or "COMPLETE" or "SUCCESS" => "SUCCEEDED",
                    "IN_QUEUE" or "IN_PROGRESS" or "QUEUED" => "PENDING",
                    "FAILED" or "ERROR" => "FAILED",
                    var s => string.IsNullOrWhiteSpace(s) ? "" : s,
                };
                if (status == "FAILED")
                    return ("FAILED", null, ReadString(doc.RootElement, "error") ?? Truncate(hit.Body));
                if (status == "PENDING")
                    return ("PENDING", null, null);
                var responseUrl = ReadString(doc.RootElement, "response_url")
                                  ?? ReadString(doc.RootElement, "responseUrl");
                foreach (var follow in FamixaFalLipsyncPollV1.FollowPaths(endpoint, requestId, responseUrl))
                {
                    try
                    {
                        var followed = await FalGetAsync(key, follow, cancellationToken);
                        var followedVideo = FamixaFalLipsyncResultV1.ReadVideoUrl(followed.Body);
                        if (!string.IsNullOrWhiteSpace(followedVideo))
                            return ("SUCCEEDED", followedVideo, null);
                    }
                    catch (OperationCanceledException)
                    {
                        /* keep scanning */
                    }
                }
            }
            catch (JsonException)
            {
                /* not a status body */
            }
        }

        if (!string.IsNullOrWhiteSpace(resultKeys))
            return ("PENDING", null, $"Fal xong — KIT chưa lấy file (keys: {resultKeys}). Gắn Fal · 0$ từ Usage.");
        if (last is not null && (last.Contains("405", StringComparison.Ordinal) || last.Contains("404", StringComparison.Ordinal)))
            return ("FAILED", null, last);
        return ("PENDING", null, last);
    }

    private async Task<(string Status, string? VideoUrl, string? Error)> TryPlatformUsageAsync(
        string key,
        string requestId,
        string endpoint,
        CancellationToken cancellationToken)
    {
        string? last = null;
        foreach (var url in FamixaFalLipsyncUsageV1.PeekUrls(requestId, endpoint))
        {
            try
            {
                var hit = await FalGetAsync(key, url, cancellationToken);
                if (hit.StatusCode is < 200 or >= 300)
                {
                    last = $"Fal Usage {hit.StatusCode}: {Truncate(hit.Body)}";
                    continue;
                }

                TryWriteUsagePeek(url, hit.StatusCode, hit.Body, requestId);
                var video = FamixaFalLipsyncResultV1.ReadPlatformVideoUrl(hit.Body, requestId);
                if (!string.IsNullOrWhiteSpace(video))
                    return ("SUCCEEDED", video, null);
                var dead = FamixaFalLipsyncResultV1.ReadPlatformNoVideoReason(hit.Body, requestId);
                if (!string.IsNullOrWhiteSpace(dead))
                    return ("FAILED", null, dead);
                last = $"Fal Usage không có file ({FamixaFalLipsyncResultV1.DescribePlatformPeek(hit.Body, requestId)}). Gắn Fal · 0$.";
            }
            catch (OperationCanceledException)
            {
                last = "Fal Usage quá chậm. Gắn Fal · 0$ từ Usage.";
            }
            catch (HttpRequestException)
            {
                last = "Fal Usage không gọi được. Gắn Fal · 0$ từ Usage.";
            }
        }

        return ("PENDING", null, last);
    }

    private async Task<(string Status, string? VideoUrl, string? Error)> WaitLipsyncAsync(
        string key,
        string requestId,
        string endpoint,
        string? statusUrl,
        string? resultUrl,
        CancellationToken cancellationToken,
        int maxSeconds = 75)
    {
        var t0 = DateTime.UtcNow;
        string? last = null;
        while ((DateTime.UtcNow - t0).TotalSeconds < Math.Max(8, maxSeconds))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var peek = await PeekLipsyncAsync(key, requestId, endpoint, statusUrl, resultUrl, cancellationToken);
            if (!string.IsNullOrWhiteSpace(peek.VideoUrl))
                return peek;
            if (peek.Status == "FAILED"
                && !string.IsNullOrWhiteSpace(peek.Error)
                && peek.Error.Contains("405", StringComparison.Ordinal))
                return peek;
            if (peek.Status == "FAILED")
                return peek;
            if (peek.Error is not null
                && (peek.Error.Contains("chưa lấy file", StringComparison.OrdinalIgnoreCase)
                    || peek.Error.Contains("chưa thấy URL", StringComparison.OrdinalIgnoreCase)))
            {
                var usage = await TryPlatformUsageAsync(key, requestId, endpoint, cancellationToken);
                if (!string.IsNullOrWhiteSpace(usage.VideoUrl))
                    return usage;
                return peek;
            }
            last = peek.Error;
            await Task.Delay(4000, cancellationToken);
        }
        return ("PENDING", null, last ?? $"Fal job {requestId} vẫn chạy. Hỏi lại · 0$ — đừng gửi job mới.");
    }

    public async Task<(string Status, string? VideoUrl, string? Error)> GetTaskAsync(
        string taskId,
        CancellationToken cancellationToken,
        string? authorizedKey = null)
    {
        if (IsLipsyncTask(taskId))
        {
            var cached = TryReadStoredFal(taskId);
            if (cached is not null)
                return cached.Value;
        }
        string key;
        if (!string.IsNullOrWhiteSpace(authorizedKey))
            key = authorizedKey.Trim();
        else
        {
            var resolved = await ResolveAsync(cancellationToken);
            key = resolved.FalApiKey
                  ?? throw new InvalidOperationException("Chưa cấu hình FalApiKey.");
        }
        var id = StripPrefix(taskId);
        var endpoint = LipsyncPollEndpoint(taskId);
        if (IsLipsyncTask(taskId))
        {
            string? last = null;
            foreach (var ep in LipsyncRecoverEndpoints(taskId))
            {
                var peeked = await PeekLipsyncAsync(key, id, ep, null, null, cancellationToken);
                if (!string.IsNullOrWhiteSpace(peeked.VideoUrl))
                    return ("SUCCEEDED", await PersistFalOutputAsync(taskId, peeked.VideoUrl, cancellationToken), null);
                last = peeked.Error;
                if (peeked.Status == "FAILED"
                    && peeked.Error is not null
                    && (peeked.Error.Contains("405", StringComparison.OrdinalIgnoreCase)
                        || peeked.Error.Contains("404", StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (peeked.Status == "PENDING"
                    && peeked.Error is not null
                    && (peeked.Error.Contains("chưa thấy URL", StringComparison.OrdinalIgnoreCase)
                        || peeked.Error.Contains("chưa lấy file", StringComparison.OrdinalIgnoreCase)))
                    continue;
                return (string.IsNullOrWhiteSpace(peeked.Status) ? "PENDING" : peeked.Status, peeked.VideoUrl, peeked.Error);
            }
            var usage = await TryPlatformUsageAsync(key, id, endpoint, cancellationToken);
            if (!string.IsNullOrWhiteSpace(usage.VideoUrl))
                return ("SUCCEEDED", await PersistFalOutputAsync(taskId, usage.VideoUrl, cancellationToken), null);
            if (string.Equals(usage.Status, "FAILED", StringComparison.OrdinalIgnoreCase))
                return usage;
            return ("PENDING", null, usage.Error ?? last);
        }

        using var statusReq = new HttpRequestMessage(HttpMethod.Get, $"{endpoint}/requests/{Uri.EscapeDataString(id)}/status");
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

        using var resultReq = new HttpRequestMessage(HttpMethod.Get, $"{endpoint}/requests/{Uri.EscapeDataString(id)}");
        ApplyAuth(resultReq, key);
        using var resultRes = await _http.SendAsync(resultReq, cancellationToken);
        var resultBody = await resultRes.Content.ReadAsStringAsync(cancellationToken);
        if (!resultRes.IsSuccessStatusCode)
            throw new InvalidOperationException($"Fal result {(int)resultRes.StatusCode}: {Truncate(resultBody)}");

        var video = FamixaFalLipsyncResultV1.ReadVideoUrl(resultBody);
        if (string.IsNullOrWhiteSpace(video))
            return ("FAILED", null, "Fal xong nhưng không có URL video.");
        return ("SUCCEEDED", video, null);
    }

    public static bool IsWanTask(string? taskId) =>
        (taskId ?? "").StartsWith(TaskPrefix, StringComparison.OrdinalIgnoreCase);

    public static bool IsLipsyncTask(string? taskId)
    {
        var t = taskId ?? "";
        return t.StartsWith(LipsyncPrefix, StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("lipsync_v3_", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("lipsync_v1_", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("lipsync_ls_", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsFalTask(string? taskId) => IsWanTask(taskId) || IsLipsyncTask(taskId);

    private static string LipsyncPollEndpoint(string taskId)
    {
        var t = taskId ?? "";
        if (t.StartsWith("lipsync_ls_", StringComparison.OrdinalIgnoreCase)) return "fal-ai/latentsync";
        if (t.StartsWith("lipsync_v3_", StringComparison.OrdinalIgnoreCase)) return "fal-ai/sync-lipsync/v3";
        if (t.StartsWith("lipsync_v1_", StringComparison.OrdinalIgnoreCase)) return "fal-ai/sync-lipsync";
        if (IsLipsyncTask(t)) return LipsyncEndpoint;
        return WanEndpoint;
    }

    private static string[] LipsyncRecoverEndpoints(string taskId)
    {
        var app = FamixaFalLipsyncPollV1.QueueApp(LipsyncPollEndpoint(taskId));
        return app.Equals(LipsyncEndpoint, StringComparison.OrdinalIgnoreCase)
            ? new[] { app }
            : new[] { app, LipsyncEndpoint };
    }

    private static string StripPrefix(string taskId)
    {
        var t = taskId.Trim();
        foreach (var prefix in new[] { "lipsync_ls_", "lipsync_v3_", "lipsync_v1_", LipsyncPrefix, TaskPrefix })
        {
            if (t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return t[prefix.Length..];
        }
        return t;
    }

    private static bool LooksLikeDownstreamDown(string? body) =>
        (body ?? "").Contains("downstream_service_unavailable", StringComparison.OrdinalIgnoreCase)
        || (body ?? "").Contains("Gateway Timeout", StringComparison.OrdinalIgnoreCase);

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static string? ReadRequestId(JsonElement root)
    {
        if (root.TryGetProperty("request_id", out var a) && a.ValueKind == JsonValueKind.String)
            return a.GetString();
        if (root.TryGetProperty("requestId", out var b) && b.ValueKind == JsonValueKind.String)
            return b.GetString();
        return null;
    }

    private static string NormalizeLipsyncKind(string? raw)
    {
        var m = (raw ?? "").Trim().ToLowerInvariant();
        if (m is "v3" or "sync-lipsync-v3") return "v3";
        if (m is "ls" or "latentsync" or "latent") return "ls";
        return "1.9";
    }

    private static JsonContent LipsyncPayload(
        string kind,
        string videoUrl,
        string audioUrl,
        string mode,
        double? speakerX = null,
        double? speakerY = null,
        int? speakerVideoWidth = null,
        int? speakerVideoHeight = null,
        int? speakerFrameNumber = null,
        double? speakerDurationSec = null,
        int? speakerFrameCount = null,
        int[]? faceBoxXyxy = null)
    {
        if (kind == "ls")
        {
            var loop = mode == "loop" ? "loop" : mode == "bounce" ? "pingpong" : null;
            return loop is null
                ? JsonContent.Create(new { video_url = videoUrl, audio_url = audioUrl }, options: JsonOpts)
                : JsonContent.Create(new { video_url = videoUrl, audio_url = audioUrl, loop_mode = loop }, options: JsonOpts);
        }
        if (kind == "v3")
        {
            var explicitBox = faceBoxXyxy is { Length: 4 } && faceBoxXyxy[2] > faceBoxXyxy[0] && faceBoxXyxy[3] > faceBoxXyxy[1]
                ? faceBoxXyxy
                : null;
            if (explicitBox is not null
                || FamixaFalLipsyncSpeakerPinV1.TryActiveSpeakerDetection(
                    speakerX,
                    speakerY,
                    speakerVideoWidth,
                    speakerVideoHeight,
                    speakerFrameNumber,
                    out _,
                    out _))
            {
                var box = explicitBox
                    ?? FamixaFalLipsyncSpeakerPinV1.FaceBoundingBox(
                        speakerX!.Value,
                        speakerY!.Value,
                        speakerVideoWidth,
                        speakerVideoHeight);
                var frames = speakerFrameCount is > 0
                    ? Math.Clamp(speakerFrameCount.Value, 1, 400)
                    : FamixaFalLipsyncSpeakerPinV1.FrameCountOf(speakerDurationSec);
                var boxes = FamixaFalLipsyncSpeakerPinV1.RepeatFaceBox(box, frames);
                return JsonContent.Create(
                    new
                    {
                        video_url = videoUrl,
                        audio_url = audioUrl,
                        sync_mode = mode,
                        options = new
                        {
                            active_speaker_detection = new
                            {
                                auto_detect = false,
                                bounding_boxes = boxes,
                            },
                        },
                    },
                    options: JsonOpts);
            }
            return JsonContent.Create(
                new
                {
                    video_url = videoUrl,
                    audio_url = audioUrl,
                    sync_mode = mode,
                    options = new
                    {
                        active_speaker_detection = new { auto_detect = true },
                    },
                },
                options: JsonOpts);
        }
        return JsonContent.Create(
            new { video_url = videoUrl, audio_url = audioUrl, sync_mode = mode, model = LipsyncVariant },
            options: JsonOpts);
    }

    private static string NormalizeSyncMode(string? raw)
    {
        var m = (raw ?? "silence").Trim().ToLowerInvariant();
        return m is "cut_off" or "loop" or "bounce" or "silence" or "remap" ? m : "silence";
    }

    private (string Status, string? VideoUrl, string? Error)? TryReadStoredFal(string taskId)
    {
        var fileId = FamixaFalLipsyncPlaybackV1.FileId(taskId);
        var bytes = _videos.TryReadNamed("fal", fileId);
        if (bytes is not { Length: >= 800 }) return null;
        return ("SUCCEEDED", FamixaFalLipsyncPlaybackV1.PublicPath(taskId), null);
    }

    private async Task<string> PersistFalOutputAsync(string taskId, string videoUrl, CancellationToken cancellationToken)
    {
        var cached = TryReadStoredFal(taskId);
        if (cached?.VideoUrl is { Length: > 0 } path)
            return path;
        var localReadable = false;
        try
        {
            var bytes = await TryDownloadOutputAsync(videoUrl, cancellationToken);
            if (bytes is { Length: >= 800 })
            {
                var fileId = FamixaFalLipsyncPlaybackV1.FileId(taskId);
                _videos.PersistNamed("fal", fileId, bytes);
                localReadable = _videos.TryReadNamed("fal", fileId) is { Length: >= 800 };
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            /* CDN chậm — giữ video.url Fal */
        }
        catch (HttpRequestException)
        {
            /* giữ URL Fal */
        }

        return FamixaFalLipsyncPlaybackV1.CanonicalPlayableUrl(videoUrl, localReadable, taskId);
    }

    private async Task<byte[]?> TryDownloadOutputAsync(string videoUrl, CancellationToken cancellationToken)
    {
        if (!FamixaFalLipsyncResultV1.LooksLikeFalOutputVideo(videoUrl)) return null;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(45));
        using var req = new HttpRequestMessage(HttpMethod.Get, videoUrl);
        using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        if (!res.IsSuccessStatusCode) return null;
        var bytes = await res.Content.ReadAsByteArrayAsync(cts.Token);
        return bytes.Length >= 800 ? bytes : null;
    }

    private static void TryWriteUsagePeek(string url, int status, string body, string requestId)
    {
        try
        {
            var dest = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".dev", "fal-usage-peek.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            var peek = FamixaFalLipsyncResultV1.DescribePlatformPeek(body, requestId);
            File.AppendAllText(dest, $"{DateTime.UtcNow:o} {status} {url} {peek} bytes={body?.Length ?? 0}{Environment.NewLine}");
        }
        catch
        {
            /* local peek only */
        }
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
