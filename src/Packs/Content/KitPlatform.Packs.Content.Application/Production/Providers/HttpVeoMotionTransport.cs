using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Vertex-shaped HTTP transport. Tests inject HttpMessageHandler + FakeVeoHttpAuthenticator.
/// Does not persist KitVideoVideoStore / legacy public URL. Does not read credentials.
/// </summary>
public sealed class HttpVeoMotionTransport : IVeoMotionTransport
{
    static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    readonly HttpClient _http;
    readonly IVeoHttpAuthenticator _auth;
    readonly VeoHttpSettings _settings;
    readonly IProductionCredentialResolver? _credentials;
    readonly ConcurrentDictionary<string, byte[]> _bytes = new(StringComparer.Ordinal);
    int _httpCalls;

    public HttpVeoMotionTransport(
        HttpClient http,
        IVeoHttpAuthenticator authenticator,
        VeoHttpSettings settings,
        IProductionCredentialResolver? credentials = null)
    {
        _http = http;
        _auth = authenticator;
        _settings = settings;
        _credentials = credentials;
    }

    public bool RequiresNetwork => true;
    public int HttpCallCount => _httpCalls;
    public bool SuppressSend { get; set; }

    public async Task<VeoMotionTransportCreateResult> CreateImageToVideoAsync(
        VeoMotionTransportCreateRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ImageDataUri))
            throw Fail(ProductionFailureReasons.MissingKeyframe);
        var (mime, b64) = SplitDataUri(request.ImageDataUri);
        var token = await RequireTokenAsync(request.Credential, ct);
        var url = VeoVertexEndpoints.PredictLongRunning(EndpointSettings());
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = new StringContent(PreviewStartBody(request, mime, b64), Encoding.UTF8, "application/json");
        using var res = await SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw Fail(MapHttp((int)res.StatusCode, start: true), (int)res.StatusCode);
        var name = ReadOperationName(body);
        if (string.IsNullOrWhiteSpace(name))
            throw Fail(ProductionFailureReasons.InvalidProviderResponse, (int)res.StatusCode);
        return new VeoMotionTransportCreateResult(VeoVertexOperationId.Encode(name));
    }

    public Task<VeoMotionTransportPollResult> GetOperationAsync(
        string providerJobId, CancellationToken ct = default)
        => GetOperationAsync(providerJobId, null, ct);

    public async Task<VeoMotionTransportPollResult> GetOperationAsync(
        string providerJobId, ProductionCredentialReference? credential, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerJobId))
            return new VeoMotionTransportPollResult(providerJobId, FakeVeoMotionTransport.WireFailed, ProductionFailureReasons.UnknownProvider);
        string operation;
        try
        {
            operation = VeoVertexOperationId.Decode(providerJobId);
        }
        catch
        {
            return new VeoMotionTransportPollResult(
                providerJobId, FakeVeoMotionTransport.WireFailed, ProductionFailureReasons.UnknownProvider);
        }

        try
        {
            var token = await RequireTokenAsync(credential, ct);
            var url = VeoVertexEndpoints.FetchPredictOperation(EndpointSettings());
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = new StringContent(
                JsonSerializer.Serialize(new { operationName = operation }, JsonOpts),
                Encoding.UTF8,
                "application/json");
            using var res = await SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                return new VeoMotionTransportPollResult(
                    providerJobId, FakeVeoMotionTransport.WireFailed, MapHttp((int)res.StatusCode, start: false));
            }

            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var err))
            {
                var msg = err.TryGetProperty("message", out var m) ? m.GetString() : ProductionFailureReasons.ProviderUnavailable;
                return new VeoMotionTransportPollResult(
                    providerJobId, FakeVeoMotionTransport.WireFailed, FamixaProviderErrorCodes.ProviderError + " · " + (msg ?? ""));
            }

            var done = root.TryGetProperty("done", out var doneEl) && doneEl.ValueKind == JsonValueKind.True;
            if (!done)
                return new VeoMotionTransportPollResult(providerJobId, FakeVeoMotionTransport.WireRunning);

            if (!TryReadVideoBytes(root, out var bytes, out var gcs))
            {
                var code = !string.IsNullOrWhiteSpace(gcs)
                    ? ProductionFailureReasons.ProviderOutputUnsupported
                    : ProductionFailureReasons.ProviderOutputMissing;
                return new VeoMotionTransportPollResult(providerJobId, FakeVeoMotionTransport.WireFailed, code);
            }

            _bytes[providerJobId] = bytes;
            return new VeoMotionTransportPollResult(providerJobId, FakeVeoMotionTransport.WireSucceeded);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested || ex is TaskCanceledException)
        {
            return new VeoMotionTransportPollResult(
                providerJobId, FakeVeoMotionTransport.WireFailed, FamixaProviderErrorCodes.Timeout);
        }
        catch (JsonException)
        {
            return new VeoMotionTransportPollResult(
                providerJobId, FakeVeoMotionTransport.WireFailed, ProductionFailureReasons.InvalidProviderResponse);
        }
    }

    public async Task<byte[]> DownloadOutputAsync(string providerJobId, CancellationToken ct = default)
    {
        if (_auth is ProductionVeoHttpAuthenticator)
        {
            if (_bytes.TryGetValue(providerJobId, out var authorized) && authorized.Length >= 32)
                return authorized;
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
        }
        if (_bytes.TryGetValue(providerJobId, out var cached) && cached.Length >= 32)
            return cached;
        var poll = await GetOperationAsync(providerJobId, ct);
        if (!string.Equals(poll.Status, FakeVeoMotionTransport.WireSucceeded, StringComparison.Ordinal))
            throw Fail(poll.FailureReason ?? ProductionFailureReasons.ProviderOutputMissing, operation: providerJobId);
        if (_bytes.TryGetValue(providerJobId, out var bytes) && bytes.Length >= 32)
            return bytes;
        throw Fail(ProductionFailureReasons.ProviderOutputMissing, operation: providerJobId);
    }

    async Task<string> RequireTokenAsync(ProductionCredentialReference? credential, CancellationToken ct)
    {
        if (_auth is FakeVeoHttpAuthenticator)
        {
            var token = (await _auth.GetAccessTokenAsync(ct) ?? "").Trim();
            if (token.Length == 0)
                throw Fail(FamixaProviderErrorCodes.AuthFailed);
            return token;
        }

        if (_auth is not ProductionVeoHttpAuthenticator official || _credentials is null || credential is null)
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
        var material = await ProductionCredentialResolver.RequireAsync(
            _credentials,
            credential,
            FamixaProviderIds.Google,
            ProductionMotionCatalog.Veo31ModelId,
            ct);
        if (material.CredentialId != credential.CredentialId
            || material.CredentialVersionId != credential.CredentialVersionId)
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
        return await official.AuthorizeFrozenAsync(material, ct);
    }

    VeoHttpSettings EndpointSettings()
    {
        if (_auth is ProductionVeoHttpAuthenticator official
            && !string.IsNullOrWhiteSpace(official.AcceptedProjectId))
            return _settings with { ProjectId = official.AcceptedProjectId };
        return _settings;
    }

    async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        if (SuppressSend)
            throw new HttpRequestException("VEO_HTTP_NOT_SENT");
        Interlocked.Increment(ref _httpCalls);
        try
        {
            return await _http.SendAsync(req, ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw Fail(FamixaProviderErrorCodes.Timeout);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw Fail(FamixaProviderErrorCodes.Timeout);
        }
    }

    public static string PreviewStartBody(VeoMotionTransportCreateRequest request)
    {
        var (mime, b64) = SplitDataUri(request.ImageDataUri);
        return PreviewStartBody(request, mime, b64);
    }

    public static string PreviewPollBody(string operationName) =>
        JsonSerializer.Serialize(new { operationName }, JsonOpts);

    static string PreviewStartBody(VeoMotionTransportCreateRequest request, string mime, string b64) =>
        JsonSerializer.Serialize(new
        {
            instances = new[]
            {
                new
                {
                    prompt = request.Prompt,
                    image = new { bytesBase64Encoded = b64, mimeType = mime },
                },
            },
            parameters = new
            {
                sampleCount = request.SampleCount > 0 ? request.SampleCount : 1,
                aspectRatio = request.AspectRatio,
                durationSeconds = request.ProviderDurationSec,
                resolution = string.IsNullOrWhiteSpace(request.Resolution) ? "720p" : request.Resolution,
                generateAudio = request.GenerateAudio,
            },
        }, JsonOpts);

    static string MapHttp(int status, bool start) => status switch
    {
        401 or 403 => FamixaProviderErrorCodes.AuthFailed,
        429 => FamixaProviderErrorCodes.RateLimited,
        400 => FamixaProviderErrorCodes.InvalidInput,
        404 => start ? FamixaProviderErrorCodes.ProviderError : ProductionFailureReasons.UnknownProvider,
        >= 500 => FamixaProviderErrorCodes.ProviderError,
        _ => FamixaProviderErrorCodes.ProviderError,
    };

    static InvalidOperationException Fail(string code, int? http = null, string? operation = null)
    {
        var parts = new List<string> { code, "provider=" + FamixaProviderIds.Google };
        if (http is int status) parts.Add("http=" + status);
        if (!string.IsNullOrWhiteSpace(operation)) parts.Add("operation=" + operation);
        return new InvalidOperationException(string.Join(" · ", parts));
    }

    static (string Mime, string Base64) SplitDataUri(string imageDataUrl)
    {
        var raw = (imageDataUrl ?? "").Trim();
        if (!raw.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            throw Fail(ProductionFailureReasons.FailedIdentity);
        var comma = raw.IndexOf(',');
        if (comma < 0)
            throw Fail(ProductionFailureReasons.FailedIdentity);
        var header = raw[..comma];
        var mime = "image/jpeg";
        if (header.StartsWith("data:image/png", StringComparison.OrdinalIgnoreCase))
            mime = "image/png";
        else if (header.StartsWith("data:image/webp", StringComparison.OrdinalIgnoreCase))
            mime = "image/webp";
        var b64 = raw[(comma + 1)..];
        if (b64.Length == 0)
            throw Fail(ProductionFailureReasons.MissingKeyframe);
        return (mime, b64);
    }

    static string? ReadOperationName(string body)
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

    static bool TryReadVideoBytes(JsonElement root, out byte[] bytes, out string? gcsUri)
    {
        bytes = [];
        gcsUri = null;
        if (!root.TryGetProperty("response", out var response))
            return false;

        if (response.TryGetProperty("videos", out var videos) && videos.ValueKind == JsonValueKind.Array)
        {
            foreach (var video in videos.EnumerateArray())
            {
                if (TryBytes(video, out bytes, out var foundGcs))
                    return bytes.Length > 0;
                if (!string.IsNullOrWhiteSpace(foundGcs))
                {
                    gcsUri = foundGcs;
                    return false;
                }
            }
        }

        if (response.TryGetProperty("generatedSamples", out var samples) && samples.ValueKind == JsonValueKind.Array)
        {
            foreach (var sample in samples.EnumerateArray())
            {
                if (!sample.TryGetProperty("video", out var video))
                    continue;
                if (TryBytes(video, out bytes, out var foundGcs))
                    return bytes.Length > 0;
                if (!string.IsNullOrWhiteSpace(foundGcs))
                {
                    gcsUri = foundGcs;
                    return false;
                }
            }
        }

        if (TryBytes(response, out bytes, out var responseGcs))
            return bytes.Length > 0;
        gcsUri = responseGcs;
        return false;
    }

    static bool TryBytes(JsonElement video, out byte[] bytes, out string? gcsUri)
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
            return bytes.Length >= 32;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>In-memory HttpMessageHandler. Never opens a socket.</summary>
public sealed class FakeVeoHttpMessageHandler : HttpMessageHandler
{
    public sealed record Capture(string Method, string Url, string? Scheme, string? Token, string Body);

    public List<Capture> Requests { get; } = [];
    public Capture? Last => Requests.Count == 0 ? null : Requests[^1];
    public Func<Capture, HttpResponseMessage>? OnSend { get; set; }
    public bool ThrowCanceled { get; set; }
    public bool ThrowBeforeCapture { get; set; }

    public int PredictCalls => Requests.Count(r => r.Url.Contains(":predictLongRunning", StringComparison.Ordinal));
    public int FetchCalls => Requests.Count(r => r.Url.Contains(":fetchPredictOperation", StringComparison.Ordinal));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ThrowBeforeCapture)
            throw new HttpRequestException("FAKE_VEO_HTTP_NOT_SENT");
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        var auth = request.Headers.Authorization;
        var capture = new Capture(
            request.Method.Method,
            request.RequestUri?.ToString() ?? "",
            auth?.Scheme,
            auth?.Parameter,
            body);
        Requests.Add(capture);
        if (ThrowCanceled)
            throw new TaskCanceledException("FAKE_VEO_HTTP_TIMEOUT");
        if (OnSend is not null)
            return OnSend(capture);
        return Json(HttpStatusCode.OK, """{"name":"projects/test-project/locations/us-central1/operations/op-1"}""");
    }

    public static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
}
