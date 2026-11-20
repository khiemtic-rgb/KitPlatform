using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Gemini generateContent with the tenant key passed in. No global or environment key. One request, no model fallback.
/// </summary>
internal sealed class HttpGeminiTenantImageTransport : IGeminiTenantImageTransport
{
    const string ApiBase = "https://generativelanguage.googleapis.com/v1beta";
    /// <summary>Documented generateContent imageSize for the draft still. 0.5K is rejected; 512 is the cheap size.</summary>
    public const string ImageSize = "512";

    static readonly HashSet<string> AspectRatios = new(StringComparer.Ordinal)
    {
        "1:1", "1:4", "4:1", "1:8", "8:1", "2:3", "3:2", "3:4", "4:3", "4:5", "5:4", "9:16", "16:9", "21:9",
    };

    readonly HttpClient _http;
    readonly ILogger<HttpGeminiTenantImageTransport> _logger;
    int _calls;

    public HttpGeminiTenantImageTransport(HttpClient http, ILogger<HttpGeminiTenantImageTransport> logger)
    {
        _http = http;
        _logger = logger;
    }

    public int GenerationCount => Volatile.Read(ref _calls);

    public Task<GeminiProductionStillResult> GenerateOnceAsync(
        string apiKey, string modelId, string prompt, string? aspectRatio, CancellationToken ct = default)
        => SendAsync(apiKey, modelId, aspectRatio, TextOnly(prompt), ct);

    public Task<GeminiProductionStillResult> GenerateWithReferencesAsync(
        string apiKey, string modelId, string prompt, string? aspectRatio,
        IReadOnlyList<GeminiStillReference> references, CancellationToken ct = default)
    {
        if (references is not { Count: > 0 })
            throw new InvalidOperationException(ProductionLockedStillReference.NotSent);
        var parts = new List<object>();
        foreach (var row in references)
        {
            if (row.Bytes is not { Length: > 32 } || string.IsNullOrWhiteSpace(row.Sha256))
                throw new InvalidOperationException(ProductionLockedStillReference.NotSent);
            parts.Add(new
            {
                text = $"REFERENCE — {row.Name} locked master {row.CharacterId}. Match face, hair, age, and wardrobe. Not a pose to copy.",
            });
            parts.Add(new { inline_data = new { mime_type = row.Mime, data = Convert.ToBase64String(row.Bytes) } });
        }
        parts.Add(new { text = prompt });
        return SendAsync(apiKey, modelId, aspectRatio, parts, ct);
    }

    static object[] TextOnly(string prompt) => [new { text = prompt }];

    async Task<GeminiProductionStillResult> SendAsync(
        string apiKey, string modelId, string? aspectRatio, object parts, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
        var model = (modelId ?? "").Trim();
        if (!string.Equals(model, ProductionImageCatalog.GeminiFlashImage, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
        var aspect = string.IsNullOrWhiteSpace(aspectRatio) ? null : aspectRatio.Trim();
        if (aspect is not null && !AspectRatios.Contains(aspect))
            throw new InvalidOperationException("IMAGE_ASPECT_RATIO_UNSUPPORTED");

        var imageSize = ProductionQualityTiers.NormalizeImage(ProductionImageSizeChoice.CurrentOrDraft);
        Interlocked.Increment(ref _calls);
        object imageConfig = aspect is null
            ? new { imageSize }
            : new { aspectRatio = aspect, imageSize };
        var generationConfig = new { responseModalities = new[] { "TEXT", "IMAGE" }, imageConfig };
        var body = new
        {
            contents = new[] { new { role = "user", parts } },
            generationConfig,
        };
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}/models/{model}:generateContent");
            req.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey.Trim());
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var res = await _http.SendAsync(req, ct);
            var raw = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                var snippet = raw.Length > 300 ? raw[..300] : raw;
                return new GeminiProductionStillResult(false, null, null, model,
                    $"GEMINI_TENANT_IMAGE_HTTP_{(int)res.StatusCode}: {snippet}");
            }
            using var doc = JsonDocument.Parse(raw);
            var image = ExtractInlineImage(doc.RootElement);
            if (image is null || image.Value.Bytes.Length == 0)
                return new GeminiProductionStillResult(true, null, null, model, "GEMINI_ACCEPTED_NO_ARTIFACT");
            var mime = image.Value.Mime
                ?? KitVideoArtifactRules.DetectMime(image.Value.Bytes)
                ?? "image/png";
            _logger.LogInformation("TENANT_PRODUCTION_IMAGE Gemini once model={Model} bytes={Len}", model, image.Value.Bytes.Length);
            return new GeminiProductionStillResult(true, image.Value.Bytes, mime, model, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "TENANT_PRODUCTION_IMAGE Gemini once failed");
            return new GeminiProductionStillResult(false, null, null, model, ex.Message);
        }
    }

    static (byte[] Bytes, string? Mime)? ExtractInlineImage(JsonElement data)
    {
        if (!data.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            return null;
        if (!candidates[0].TryGetProperty("content", out var content)
            || !content.TryGetProperty("parts", out var parts))
            return null;
        (byte[] Bytes, string? Mime)? lastThought = null;
        (byte[] Bytes, string? Mime)? lastVisible = null;
        foreach (var part in parts.EnumerateArray())
        {
            if (!TryInlineImage(part, out var bytes, out var mime))
                continue;
            var thought = part.TryGetProperty("thought", out var flag) && flag.ValueKind == JsonValueKind.True;
            if (thought) lastThought = (bytes, mime);
            else lastVisible = (bytes, mime);
        }
        return lastVisible ?? lastThought;
    }

    static bool TryInlineImage(JsonElement part, out byte[] bytes, out string? mime)
    {
        bytes = [];
        mime = null;
        if (!part.TryGetProperty("inlineData", out var inline) && !part.TryGetProperty("inline_data", out inline))
            return false;
        if (!inline.TryGetProperty("data", out var b64) || b64.GetString() is not { Length: > 0 } encoded)
            return false;
        bytes = Convert.FromBase64String(encoded);
        if (inline.TryGetProperty("mimeType", out var mimeType) || inline.TryGetProperty("mime_type", out mimeType))
        {
            var value = mimeType.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                mime = value.Trim();
        }
        return bytes.Length > 0;
    }
}
