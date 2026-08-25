using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.Configuration;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Infrastructure.Configuration;

/// <summary>
/// Shared Gemini key resolution for Content Park (pack_content.org_settings + Content:* appsettings).
/// Used temporarily by Pharmacy POS consultation when tenant has no dedicated key.
/// </summary>
public static class ContentParkGeminiKeyResolver
{
    public sealed record ResolvedSettings(string? ApiKey, string? TextModel);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task<string?> ResolveAsync(
        IDbConnectionFactory db,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var settings = await ResolveSettingsAsync(db, configuration, cancellationToken);
        return settings.ApiKey;
    }

    public static async Task<ResolvedSettings> ResolveSettingsAsync(
        IDbConnectionFactory db,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await db.CreateOpenConnectionAsync(cancellationToken);
        var json = await conn.QuerySingleOrDefaultAsync<string?>(
            """
            SELECT ai_config_json::text
            FROM pack_content.org_settings
            ORDER BY updated_at DESC
            LIMIT 1
            """);
        return ResolveSettingsFromOrgJson(json, configuration);
    }

    public static string? ResolveFromOrgJson(string? aiConfigJson, IConfiguration configuration) =>
        ResolveSettingsFromOrgJson(aiConfigJson, configuration).ApiKey;

    public static ResolvedSettings ResolveSettingsFromOrgJson(string? aiConfigJson, IConfiguration configuration)
    {
        string? textModel = null;
        if (!string.IsNullOrWhiteSpace(aiConfigJson))
        {
            try
            {
                var state = JsonSerializer.Deserialize<ContentAiState>(aiConfigJson, JsonOpts);
                if (state is not null)
                {
                    textModel = FirstNonEmpty(state.TextModel);
                    var fromDb = FirstNonEmpty(state.GeminiApiKey);
                    if (fromDb is not null)
                        return new ResolvedSettings(fromDb, textModel);

                    var fromRef = ResolveSecret(state.GeminiApiKeySecretRef, configuration);
                    if (fromRef is not null)
                        return new ResolvedSettings(fromRef, textModel);
                }
            }
            catch
            {
                // ignore malformed json
            }
        }

        var key = FirstNonEmpty(
            configuration["Content:GeminiApiKey"],
            Environment.GetEnvironmentVariable("GEMINI_API_KEY"),
            Environment.GetEnvironmentVariable("GOOGLE_API_KEY"),
            Environment.GetEnvironmentVariable("GOOGLE_GENERATIVE_AI_API_KEY"));
        textModel ??= FirstNonEmpty(configuration["Content:TextModel"]);
        return new ResolvedSettings(key, textModel);
    }

    private static string? ResolveSecret(string? secretRef, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(secretRef))
            return null;

        var key = secretRef.Trim();
        return FirstNonEmpty(
            configuration[key],
            Environment.GetEnvironmentVariable(key),
            configuration[$"Content:Secrets:{key}"]);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }

        return null;
    }

    private sealed class ContentAiState
    {
        [JsonPropertyName("geminiApiKey")]
        public string? GeminiApiKey { get; set; }

        [JsonPropertyName("geminiApiKeySecretRef")]
        public string? GeminiApiKeySecretRef { get; set; }

        [JsonPropertyName("textModel")]
        public string? TextModel { get; set; }
    }
}
