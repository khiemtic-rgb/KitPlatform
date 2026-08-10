using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentGeminiClient
{
    private const string ApiBase = "https://generativelanguage.googleapis.com/v1beta";

    private static readonly string[] TextFallbacks =
    [
        "gemini-flash-latest",
        "gemini-2.0-flash",
        "gemini-2.5-flash",
    ];

    private static readonly (string Model, string Mode)[] ImageFallbacks =
    [
        ("gemini-2.0-flash-preview-image-generation", "generateContent"),
        ("gemini-2.5-flash-image", "generateContent"),
        ("imagen-4.0-generate-001", "predict"),
        ("imagen-3.0-generate-002", "predict"),
    ];

    private readonly HttpClient _http;
    private readonly ContentOptions _options;
    private readonly ILogger<ContentGeminiClient> _logger;

    public ContentGeminiClient(
        HttpClient http,
        IOptions<ContentOptions> options,
        ILogger<ContentGeminiClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public bool HasApiKey => !string.IsNullOrWhiteSpace(ResolveApiKey());

    public async Task<string> GenerateJsonAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct)
    {
        var preferred = string.IsNullOrWhiteSpace(_options.TextModel)
            ? null
            : _options.TextModel.Trim();
        var models = preferred is null
            ? TextFallbacks
            : new[] { preferred }.Concat(TextFallbacks.Where(m => m != preferred)).ToArray();

        Exception? last = null;
        foreach (var model in models)
        {
            try
            {
                var body = new
                {
                    systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
                    contents = new[]
                    {
                        new { role = "user", parts = new[] { new { text = userPrompt } } },
                    },
                    generationConfig = new
                    {
                        temperature = 0.7,
                        responseMimeType = "application/json",
                    },
                };
                var data = await PostAsync($"/models/{model}:generateContent", body, ct);
                var text = ExtractText(data);
                if (string.IsNullOrWhiteSpace(text))
                    throw new InvalidOperationException("Gemini returned empty text");
                _logger.LogInformation("Content Park text model {Model}", model);
                return text;
            }
            catch (Exception ex)
            {
                last = ex;
                _logger.LogWarning(ex, "Content Park text model {Model} failed", model);
            }
        }

        throw last ?? new InvalidOperationException("All Gemini text models failed");
    }

    public async Task<(byte[] Bytes, string Model)> GenerateImageAsync(string prompt, CancellationToken ct)
    {
        var preferred = string.IsNullOrWhiteSpace(_options.ImageModel)
            ? null
            : _options.ImageModel.Trim();
        var models = preferred is null
            ? ImageFallbacks
            : new[]
                {
                    (preferred, preferred.StartsWith("imagen", StringComparison.OrdinalIgnoreCase)
                        ? "predict"
                        : "generateContent"),
                }
                .Concat(ImageFallbacks.Where(x => x.Model != preferred))
                .ToArray();

        Exception? last = null;
        foreach (var (model, mode) in models)
        {
            try
            {
                JsonElement data;
                if (mode == "predict")
                {
                    data = await PostAsync($"/models/{model}:predict", new
                    {
                        instances = new[] { new { prompt } },
                        parameters = new { sampleCount = 1, aspectRatio = "16:9" },
                    }, ct);
                }
                else
                {
                    data = await PostAsync($"/models/{model}:generateContent", new
                    {
                        contents = new[]
                        {
                            new { role = "user", parts = new[] { new { text = prompt } } },
                        },
                        generationConfig = new { responseModalities = new[] { "TEXT", "IMAGE" } },
                    }, ct);
                }

                var bytes = mode == "predict" ? ExtractPredictImage(data) : ExtractInlineImage(data);
                if (bytes is null || bytes.Length == 0)
                    throw new InvalidOperationException("Gemini returned no image bytes");
                _logger.LogInformation("Content Park image model {Model}", model);
                return (bytes, model);
            }
            catch (Exception ex)
            {
                last = ex;
                _logger.LogWarning(ex, "Content Park image model {Model} failed", model);
            }
        }

        throw last ?? new InvalidOperationException("All Gemini image models failed");
    }

    private async Task<JsonElement> PostAsync(string path, object body, CancellationToken ct)
    {
        var key = ResolveApiKey()
                  ?? throw new InvalidOperationException(
                      "Gemini API key missing — set Content:GeminiApiKey or GEMINI_API_KEY");

        using var req = new HttpRequestMessage(HttpMethod.Post, ApiBase + path);
        req.Headers.TryAddWithoutValidation("x-goog-api-key", key);
        req.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        using var res = await _http.SendAsync(req, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Gemini {path} failed ({(int)res.StatusCode}): {raw[..Math.Min(500, raw.Length)]}");

        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.Clone();
    }

    private string? ResolveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(_options.GeminiApiKey))
            return _options.GeminiApiKey.Trim();
        return Environment.GetEnvironmentVariable("GEMINI_API_KEY")?.Trim()
               ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY")?.Trim()
               ?? Environment.GetEnvironmentVariable("GOOGLE_GENERATIVE_AI_API_KEY")?.Trim();
    }

    private static string? ExtractText(JsonElement data)
    {
        if (!data.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            return null;
        if (!candidates[0].TryGetProperty("content", out var content))
            return null;
        if (!content.TryGetProperty("parts", out var parts))
            return null;
        var sb = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var text))
                sb.AppendLine(text.GetString());
        }
        var s = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static byte[]? ExtractInlineImage(JsonElement data)
    {
        if (!data.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            return null;
        if (!candidates[0].TryGetProperty("content", out var content))
            return null;
        if (!content.TryGetProperty("parts", out var parts))
            return null;
        foreach (var part in parts.EnumerateArray())
        {
            JsonElement inline;
            if (part.TryGetProperty("inlineData", out inline)
                || part.TryGetProperty("inline_data", out inline))
            {
                if (inline.TryGetProperty("data", out var b64))
                {
                    var s = b64.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        return Convert.FromBase64String(s);
                }
            }
        }
        return null;
    }

    private static byte[]? ExtractPredictImage(JsonElement data)
    {
        if (!data.TryGetProperty("predictions", out var preds) || preds.GetArrayLength() == 0)
            return null;
        var pred = preds[0];
        if (pred.TryGetProperty("bytesBase64Encoded", out var b64)
            || (pred.TryGetProperty("image", out var image)
                && image.TryGetProperty("bytesBase64Encoded", out b64)))
        {
            var s = b64.GetString();
            if (!string.IsNullOrWhiteSpace(s))
                return Convert.FromBase64String(s);
        }
        return null;
    }
}
