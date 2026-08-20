using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentVideoConfigState
{
    [JsonPropertyName("creatomateApiKeySecretRef")]
    public string? CreatomateApiKeySecretRef { get; set; } = "CREATOMATE_API_KEY";

    /// <summary>Write-only stored key — never serialized into read DTOs.</summary>
    [JsonPropertyName("creatomateApiKey")]
    public string? CreatomateApiKey { get; set; }

    [JsonPropertyName("elevenLabsApiKeySecretRef")]
    public string? ElevenLabsApiKeySecretRef { get; set; } = "ELEVENLABS_API_KEY";

    /// <summary>Write-only stored key — never serialized into read DTOs.</summary>
    [JsonPropertyName("elevenLabsApiKey")]
    public string? ElevenLabsApiKey { get; set; }

    [JsonPropertyName("elevenLabsVoiceId")]
    public string? ElevenLabsVoiceId { get; set; }

    [JsonPropertyName("publicMediaBaseUrl")]
    public string? PublicMediaBaseUrl { get; set; }

    [JsonPropertyName("creatomateTemplateId")]
    public string? CreatomateTemplateId { get; set; }

    [JsonPropertyName("runwayApiKeySecretRef")]
    public string? RunwayApiKeySecretRef { get; set; } = "RUNWAY_API_KEY";

    /// <summary>Write-only stored key — never serialized into read DTOs.</summary>
    [JsonPropertyName("runwayApiKey")]
    public string? RunwayApiKey { get; set; }

    [JsonPropertyName("falApiKeySecretRef")]
    public string? FalApiKeySecretRef { get; set; } = "FAL_KEY";

    [JsonPropertyName("falApiKey")]
    public string? FalApiKey { get; set; }
}

internal sealed record ContentVideoResolved(
    string? CreatomateApiKey,
    bool CreatomateConfigured,
    string? CreatomateApiKeySecretRef,
    string? CreatomateTemplateId,
    string? ElevenLabsApiKey,
    bool ElevenLabsConfigured,
    string? ElevenLabsApiKeySecretRef,
    string VoiceId,
    string? PublicMediaBaseUrl,
    string? RunwayApiKey,
    bool RunwayConfigured,
    string? RunwayApiKeySecretRef,
    string? FalApiKey,
    bool FalConfigured,
    string? FalApiKeySecretRef);

internal static class ContentVideoConfigParser
{
    internal const string DefaultVoiceId = "21m00Tcm4TlvDq8ikWAM";
    internal const string CreatomateTemplateCode = "creatomate_9x16_mvp";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static ContentVideoConfigState Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}")
            return new ContentVideoConfigState();
        try
        {
            return JsonSerializer.Deserialize<ContentVideoConfigState>(json, JsonOpts)
                   ?? new ContentVideoConfigState();
        }
        catch
        {
            return new ContentVideoConfigState();
        }
    }

    public static string ToJson(ContentVideoConfigState state) =>
        JsonSerializer.Serialize(state, JsonOpts);

    public static ContentVideoConfigDto ToDto(ContentVideoConfigState state, ContentVideoResolved resolved) =>
        new(
            FirstNonEmpty(state.CreatomateApiKeySecretRef),
            resolved.CreatomateConfigured,
            FirstNonEmpty(state.ElevenLabsApiKeySecretRef),
            resolved.ElevenLabsConfigured,
            FirstNonEmpty(state.ElevenLabsVoiceId) ?? resolved.VoiceId,
            FirstNonEmpty(state.PublicMediaBaseUrl) ?? resolved.PublicMediaBaseUrl,
            FirstNonEmpty(state.CreatomateTemplateId) ?? resolved.CreatomateTemplateId,
            FirstNonEmpty(state.RunwayApiKeySecretRef) ?? resolved.RunwayApiKeySecretRef,
            resolved.RunwayConfigured,
            FirstNonEmpty(state.FalApiKeySecretRef) ?? resolved.FalApiKeySecretRef,
            resolved.FalConfigured);

    public static ContentVideoResolved Resolve(
        ContentVideoConfigState state,
        ContentOptions options,
        IConfiguration configuration)
    {
        var creatomateKey =
            FirstNonEmpty(state.CreatomateApiKey)
            ?? ResolveSecret(state.CreatomateApiKeySecretRef, configuration)
            ?? FirstNonEmpty(options.CreatomateApiKey)
            ?? FirstNonEmpty(configuration["Content:CreatomateApiKey"])
            ?? FirstNonEmpty(Environment.GetEnvironmentVariable("CREATOMATE_API_KEY"));

        var elevenKey =
            FirstNonEmpty(state.ElevenLabsApiKey)
            ?? ResolveSecret(state.ElevenLabsApiKeySecretRef, configuration)
            ?? FirstNonEmpty(options.ElevenLabsApiKey)
            ?? FirstNonEmpty(configuration["Content:ElevenLabsApiKey"])
            ?? FirstNonEmpty(Environment.GetEnvironmentVariable("ELEVENLABS_API_KEY"));

        var voice =
            FirstNonEmpty(state.ElevenLabsVoiceId)
            ?? FirstNonEmpty(options.ElevenLabsVoiceId)
            ?? FirstNonEmpty(configuration["Content:ElevenLabsVoiceId"])
            ?? FirstNonEmpty(Environment.GetEnvironmentVariable("ELEVENLABS_VOICE_ID"))
            ?? DefaultVoiceId;

        var publicBase =
            FirstNonEmpty(state.PublicMediaBaseUrl)
            ?? FirstNonEmpty(options.PublicMediaBaseUrl)
            ?? FirstNonEmpty(configuration["Content:PublicMediaBaseUrl"]);

        var templateId =
            FirstNonEmpty(state.CreatomateTemplateId)
            ?? FirstNonEmpty(configuration["Content:CreatomateTemplateId"]);

        var runwayKey =
            FirstNonEmpty(state.RunwayApiKey)
            ?? ResolveSecret(state.RunwayApiKeySecretRef, configuration)
            ?? FirstNonEmpty(options.RunwayApiKey)
            ?? FirstNonEmpty(configuration["Content:RunwayApiKey"])
            ?? FirstNonEmpty(Environment.GetEnvironmentVariable("RUNWAY_API_KEY"));

        var falKey =
            FirstNonEmpty(state.FalApiKey)
            ?? ResolveSecret(state.FalApiKeySecretRef, configuration)
            ?? FirstNonEmpty(options.FalApiKey)
            ?? FirstNonEmpty(configuration["Content:FalApiKey"])
            ?? FirstNonEmpty(Environment.GetEnvironmentVariable("FAL_KEY"));

        return new ContentVideoResolved(
            creatomateKey,
            !string.IsNullOrWhiteSpace(creatomateKey),
            FirstNonEmpty(state.CreatomateApiKeySecretRef),
            templateId,
            elevenKey,
            !string.IsNullOrWhiteSpace(elevenKey),
            FirstNonEmpty(state.ElevenLabsApiKeySecretRef),
            voice,
            publicBase,
            runwayKey,
            !string.IsNullOrWhiteSpace(runwayKey),
            FirstNonEmpty(state.RunwayApiKeySecretRef),
            falKey,
            !string.IsNullOrWhiteSpace(falKey),
            FirstNonEmpty(state.FalApiKeySecretRef));
    }

    private static string? ResolveSecret(string? secretRef, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(secretRef)) return null;
        var key = secretRef.Trim();
        return FirstNonEmpty(configuration[key])
               ?? FirstNonEmpty(Environment.GetEnvironmentVariable(key))
               ?? FirstNonEmpty(configuration[$"Content:Secrets:{key}"]);
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
