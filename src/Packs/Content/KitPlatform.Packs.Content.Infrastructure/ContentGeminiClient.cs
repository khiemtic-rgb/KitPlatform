using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentGeminiClient
{
    private const string ApiBase = "https://generativelanguage.googleapis.com/v1beta";

    private static readonly string[] TextFallbacks =
    [
        "gemini-3.6-flash",
        "gemini-flash-latest",
        "gemini-2.0-flash",
    ];

    /// <summary>
    /// Gemini native image models via generateContent.
    /// Do NOT use imagen-*:predict on Developer API — returns 404 on v1beta.
    /// </summary>
    private static readonly string[] ImageModels =
    [
        "gemini-2.5-flash-image",
        "gemini-3.1-flash-lite-image",
        "gemini-3.1-flash-image",
        "gemini-2.0-flash-preview-image-generation",
    ];

    private static readonly string[][] ImageModalities =
    [
        ["IMAGE"],
        ["TEXT", "IMAGE"],
    ];

    private readonly HttpClient _http;
    private readonly ContentRepository _repo;
    private readonly ContentOptions _options;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ContentGeminiClient> _logger;

    public ContentGeminiClient(
        HttpClient http,
        ContentRepository repo,
        IOptions<ContentOptions> options,
        IConfiguration configuration,
        ILogger<ContentGeminiClient> logger)
    {
        _http = http;
        _repo = repo;
        _options = options.Value;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ContentAiResolved> ResolveConfigAsync(CancellationToken ct)
    {
        var row = await _repo.GetOrgSettingsAsync(ct);
        var state = ContentAiConfigParser.Parse(row.AiConfigJson);
        return ContentAiConfigParser.Resolve(state, _options, _configuration);
    }

    public async Task<string> GenerateJsonAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct,
        int maxOutputTokens = 4096)
    {
        var resolved = await ResolveConfigAsync(ct);
        var preferred = resolved.TextModel;
        var models = string.IsNullOrWhiteSpace(preferred)
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
                        maxOutputTokens,
                    },
                };
                var data = await PostAsync(resolved.ApiKey, $"/models/{model}:generateContent", body, ct);
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
        var resolved = await ResolveConfigAsync(ct);
        var preferred = string.IsNullOrWhiteSpace(resolved.ImageModel)
            ? null
            : resolved.ImageModel!.Trim();

        var preferPollinations = preferred is not null
            && preferred.StartsWith("pollinations", StringComparison.OrdinalIgnoreCase);

        if (preferPollinations)
        {
            try
            {
                return await GenerateViaPollinationsAsync(prompt, preferred, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Pollinations preferred image failed — will try Gemini if key exists");
            }
        }

        // Skip broken imagen / pollinations labels when iterating Gemini list.
        if (preferred is not null
            && (preferred.StartsWith("imagen", StringComparison.OrdinalIgnoreCase)
                || preferred.StartsWith("pollinations", StringComparison.OrdinalIgnoreCase)))
        {
            if (preferred.StartsWith("imagen", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Image model {Model} is Imagen predict — not supported on Gemini Developer API",
                    preferred);
            }
            preferred = null;
        }

        Exception? first = null;
        if (resolved.ApiKeyConfigured)
        {
            var models = preferred is null
                ? ImageModels
                : new[] { preferred }.Concat(ImageModels.Where(m => m != preferred)).ToArray();

            foreach (var model in models)
            {
                foreach (var modalities in ImageModalities)
                {
                    try
                    {
                        var data = await PostAsync(resolved.ApiKey, $"/models/{model}:generateContent", new
                        {
                            contents = new[]
                            {
                                new { role = "user", parts = new[] { new { text = prompt } } },
                            },
                            generationConfig = new { responseModalities = modalities },
                        }, ct);

                        var bytes = ExtractInlineImage(data);
                        if (bytes is null || bytes.Length == 0)
                            throw new InvalidOperationException($"Model {model} returned no image bytes");

                        _logger.LogInformation(
                            "Content Park image model {Model} modalities={Modalities}",
                            model,
                            string.Join('+', modalities));
                        return (bytes, model);
                    }
                    catch (Exception ex)
                    {
                        first ??= ex;
                        _logger.LogWarning(
                            ex,
                            "Content Park image model {Model} modalities={Modalities} failed",
                            model,
                            string.Join('+', modalities));
                    }
                }

                try
                {
                    var (bytes, used) = await GenerateViaInteractionsAsync(resolved.ApiKey, model, prompt, ct);
                    if (bytes is { Length: > 0 })
                    {
                        _logger.LogInformation("Content Park image via interactions {Model}", used);
                        return (bytes, used);
                    }
                }
                catch (Exception ex)
                {
                    first ??= ex;
                    _logger.LogWarning(ex, "Content Park interactions image {Model} failed", model);
                }
            }
        }

        // Free fallback when Gemini quota/billing fails or no key.
        try
        {
            _logger.LogInformation("Falling back to Pollinations free image API");
            return await GenerateViaPollinationsAsync(prompt, "pollinations:flux", ct);
        }
        catch (Exception pollEx)
        {
            first ??= pollEx;
            _logger.LogWarning(pollEx, "Pollinations fallback image failed");
        }

        throw first ?? new InvalidOperationException(
            "Không tạo được ảnh (Gemini hết quota và Pollinations cũng lỗi). Thử lại sau ~15s hoặc bật billing Gemini.");
    }

    private static readonly SemaphoreSlim PollinationsGate = new(1, 1);
    private static DateTimeOffset _pollinationsNextAllowed = DateTimeOffset.MinValue;

    private async Task<(byte[] Bytes, string Model)> GenerateViaPollinationsAsync(
        string prompt,
        string? modelRef,
        CancellationToken ct)
    {
        await PollinationsGate.WaitAsync(ct);
        try
        {
            var wait = _pollinationsNextAllowed - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, ct);

            // Anonymous tier ~1 req / 15s
            _pollinationsNextAllowed = DateTimeOffset.UtcNow.AddSeconds(16);

            var model = "flux";
            if (!string.IsNullOrWhiteSpace(modelRef) && modelRef.Contains(':'))
            {
                var part = modelRef.Split(':', 2)[1].Trim();
                if (!string.IsNullOrWhiteSpace(part)) model = part;
            }

            var clean = prompt.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (clean.Length > 450) clean = clean[..450];
            var encoded = Uri.EscapeDataString(clean);
            var url =
                $"https://image.pollinations.ai/prompt/{encoded}" +
                $"?width=1280&height=720&model={Uri.EscapeDataString(model)}&nologo=true&private=true&safe=true";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("Accept", "image/*");
            req.Headers.TryAddWithoutValidation("User-Agent", "KitPlatform-ContentPark/1.0");

            using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            var bytes = await res.Content.ReadAsByteArrayAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                var hint = Encoding.UTF8.GetString(bytes.AsSpan(0, Math.Min(200, bytes.Length)));
                throw new InvalidOperationException(
                    $"Pollinations failed ({(int)res.StatusCode}): {hint}");
            }

            if (bytes.Length < 1024)
                throw new InvalidOperationException("Pollinations returned empty/too-small image");
            var ctHeader = res.Content.Headers.ContentType?.MediaType ?? "";
            if (ctHeader.Contains("json", StringComparison.OrdinalIgnoreCase)
                || ctHeader.Contains("text", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Pollinations returned non-image content-type: {ctHeader}");

            return (bytes, $"pollinations:{model}");
        }
        finally
        {
            PollinationsGate.Release();
        }
    }

    private async Task<(byte[] Bytes, string Model)> GenerateViaInteractionsAsync(
        string? apiKey,
        string model,
        string prompt,
        CancellationToken ct)
    {
        // Interactions API docs use gemini-3.1-flash-image family; skip clearly incompatible models.
        if (model.Contains("preview", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("skip interactions for preview model");

        var data = await PostAsync(apiKey, "/interactions", new
        {
            model,
            input = new object[]
            {
                new { type = "text", text = prompt },
            },
        }, ct);

        var bytes = ExtractInteractionImage(data);
        if (bytes is null || bytes.Length == 0)
            throw new InvalidOperationException("Interactions API returned no image");
        return (bytes, model);
    }

    private async Task<JsonElement> PostAsync(string? apiKey, string path, object body, CancellationToken ct)
    {
        var key = string.IsNullOrWhiteSpace(apiKey)
            ? throw new InvalidOperationException(
                "Gemini API key missing — đặt Secret ref / key trong Cấu hình AI, hoặc env GEMINI_API_KEY")
            : apiKey.Trim();

        using var req = new HttpRequestMessage(HttpMethod.Post, ApiBase + path);
        req.Headers.TryAddWithoutValidation("x-goog-api-key", key);
        req.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        using var res = await _http.SendAsync(req, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            var snippet = raw.Length > 500 ? raw[..500] : raw;
            if ((int)res.StatusCode == 429
                || snippet.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase)
                || snippet.Contains("exceeded your current quota", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Hết hạn mức Gemini (quota/billing) cho model ảnh. " +
                    "Vào https://aistudio.google.com/ → API key → bật Billing, hoặc đổi model ảnh / đợi reset quota free tier. " +
                    "Chi tiết: https://ai.google.dev/gemini-api/docs/rate-limits");
            }
            throw new InvalidOperationException($"Gemini {path} failed ({(int)res.StatusCode}): {snippet}");
        }

        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.Clone();
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

    private static byte[]? ExtractInteractionImage(JsonElement data)
    {
        // Prefer convenience field if present.
        if (data.TryGetProperty("output_image", out var outputImage)
            || data.TryGetProperty("outputImage", out outputImage))
        {
            if (outputImage.TryGetProperty("data", out var b64))
            {
                var s = b64.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    return Convert.FromBase64String(s);
            }
        }

        // Walk outputs / content blocks.
        foreach (var propName in new[] { "outputs", "output", "contents", "content" })
        {
            if (!data.TryGetProperty(propName, out var node)) continue;
            var found = FindBase64Image(node);
            if (found is not null) return found;
        }

        return FindBase64Image(data);
    }

    private static byte[]? FindBase64Image(JsonElement node)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var type = node.TryGetProperty("type", out var t) ? t.GetString() : null;
                if (string.Equals(type, "image", StringComparison.OrdinalIgnoreCase)
                    && node.TryGetProperty("data", out var d))
                {
                    var s = d.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        return Convert.FromBase64String(s);
                }

                if (node.TryGetProperty("inlineData", out var inline)
                    || node.TryGetProperty("inline_data", out inline))
                {
                    if (inline.TryGetProperty("data", out var b64))
                    {
                        var s = b64.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                            return Convert.FromBase64String(s);
                    }
                }

                foreach (var p in node.EnumerateObject())
                {
                    var nested = FindBase64Image(p.Value);
                    if (nested is not null) return nested;
                }
                break;
            }
            case JsonValueKind.Array:
                foreach (var item in node.EnumerateArray())
                {
                    var nested = FindBase64Image(item);
                    if (nested is not null) return nested;
                }
                break;
        }

        return null;
    }
}
