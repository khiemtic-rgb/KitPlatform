using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentAiConfigState
{
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "gemini";

    [JsonPropertyName("textModel")]
    public string TextModel { get; set; } = "gemini-3.6-flash";

    [JsonPropertyName("imageModel")]
    public string? ImageModel { get; set; }

    [JsonPropertyName("imagesEnabled")]
    public bool ImagesEnabled { get; set; } = true;

    [JsonPropertyName("geminiApiKeySecretRef")]
    public string? GeminiApiKeySecretRef { get; set; } = "GEMINI_API_KEY";

    /// <summary>Write-only stored key — never serialized into read DTOs.</summary>
    [JsonPropertyName("geminiApiKey")]
    public string? GeminiApiKey { get; set; }
}

internal sealed record ContentAiResolved(
    string Provider,
    string TextModel,
    string? ImageModel,
    bool ImagesEnabled,
    string? ApiKeySecretRef,
    string? ApiKey,
    bool ApiKeyConfigured);

internal static class ContentAiConfigParser
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static ContentAiConfigState Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}")
            return new ContentAiConfigState();
        try
        {
            return JsonSerializer.Deserialize<ContentAiConfigState>(json, JsonOpts) ?? new ContentAiConfigState();
        }
        catch
        {
            return new ContentAiConfigState();
        }
    }

    public static string ToJson(ContentAiConfigState state) =>
        JsonSerializer.Serialize(state, JsonOpts);

    public static ContentAiConfigDto ToDto(ContentAiConfigState state, bool apiKeyConfigured) =>
        new(
            string.IsNullOrWhiteSpace(state.Provider) ? "gemini" : state.Provider.Trim(),
            RewriteRetiredTextModel(state.TextModel),
            string.IsNullOrWhiteSpace(state.ImageModel) ? null : state.ImageModel.Trim(),
            state.ImagesEnabled,
            string.IsNullOrWhiteSpace(state.GeminiApiKeySecretRef) ? null : state.GeminiApiKeySecretRef.Trim(),
            apiKeyConfigured);

    public static ContentAiResolved Resolve(
        ContentAiConfigState state,
        ContentOptions options,
        IConfiguration configuration)
    {
        var key =
            FirstNonEmpty(state.GeminiApiKey)
            ?? ResolveSecret(state.GeminiApiKeySecretRef, configuration)
            ?? FirstNonEmpty(options.GeminiApiKey)
            ?? FirstNonEmpty(Environment.GetEnvironmentVariable("GEMINI_API_KEY"))
            ?? FirstNonEmpty(Environment.GetEnvironmentVariable("GOOGLE_API_KEY"))
            ?? FirstNonEmpty(Environment.GetEnvironmentVariable("GOOGLE_GENERATIVE_AI_API_KEY"));

        var textModel = RewriteRetiredTextModel(
            FirstNonEmpty(state.TextModel)
            ?? FirstNonEmpty(options.TextModel)
            ?? "gemini-3.6-flash");
        var imageModel = FirstNonEmpty(state.ImageModel) ?? FirstNonEmpty(options.ImageModel);

        return new ContentAiResolved(
            string.IsNullOrWhiteSpace(state.Provider) ? "gemini" : state.Provider.Trim(),
            textModel!,
            imageModel,
            state.ImagesEnabled,
            FirstNonEmpty(state.GeminiApiKeySecretRef),
            key,
            !string.IsNullOrWhiteSpace(key));
    }

    private static string? ResolveSecret(string? secretRef, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(secretRef)) return null;
        var key = secretRef.Trim();
        return FirstNonEmpty(configuration[key])
               ?? FirstNonEmpty(Environment.GetEnvironmentVariable(key))
               ?? FirstNonEmpty(configuration[$"Content:Secrets:{key}"]);
    }

    /// <summary>Gemini retired 2.5-flash for new keys — map to the current flash id.</summary>
    private static string RewriteRetiredTextModel(string? model)
    {
        var m = (model ?? "").Trim();
        if (m.Length == 0) return "gemini-3.6-flash";
        if (m.Equals("gemini-2.5-flash", StringComparison.OrdinalIgnoreCase)
            || m.Equals("models/gemini-2.5-flash", StringComparison.OrdinalIgnoreCase))
            return "gemini-3.6-flash";
        return m;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        }
        return null;
    }
}
