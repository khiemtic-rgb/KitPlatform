using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentFacebookConfigState
{
    [JsonPropertyName("appId")]
    public string? AppId { get; set; }

    [JsonPropertyName("appIdSecretRef")]
    public string? AppIdSecretRef { get; set; } = "FACEBOOK_APP_ID";

    [JsonPropertyName("appSecretSecretRef")]
    public string? AppSecretSecretRef { get; set; } = "FACEBOOK_APP_SECRET";

    /// <summary>Write-only — never serialized into read DTOs.</summary>
    [JsonPropertyName("appSecret")]
    public string? AppSecret { get; set; }

    [JsonPropertyName("redirectUri")]
    public string? RedirectUri { get; set; }
}

internal sealed record ContentFacebookResolved(
    string? AppId,
    string? AppSecret,
    bool AppSecretConfigured,
    string RedirectUri);

internal static class ContentFacebookConfigParser
{
    internal const string DefaultRedirectUri = "http://localhost:5173/content/facebook/callback";
    internal const string GraphVersion = "v21.0";
    internal const string Scopes =
        "pages_show_list,pages_manage_posts,pages_read_engagement,business_management";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static ContentFacebookConfigState Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}")
            return new ContentFacebookConfigState();
        try
        {
            return JsonSerializer.Deserialize<ContentFacebookConfigState>(json, JsonOpts)
                   ?? new ContentFacebookConfigState();
        }
        catch
        {
            return new ContentFacebookConfigState();
        }
    }

    public static string ToJson(ContentFacebookConfigState state) =>
        JsonSerializer.Serialize(state, JsonOpts);

    public static ContentFacebookConfigDto ToDto(ContentFacebookConfigState state, ContentFacebookResolved resolved) =>
        new(
            FirstNonEmpty(resolved.AppId),
            resolved.AppSecretConfigured,
            FirstNonEmpty(state.AppIdSecretRef),
            FirstNonEmpty(state.AppSecretSecretRef),
            resolved.RedirectUri);

    public static ContentFacebookResolved Resolve(
        ContentFacebookConfigState state,
        ContentOptions options,
        IConfiguration configuration)
    {
        var appId =
            FirstNonEmpty(state.AppId)
            ?? ResolveSecret(state.AppIdSecretRef, configuration)
            ?? FirstNonEmpty(options.FacebookAppId)
            ?? FirstNonEmpty(configuration["Content:FacebookAppId"])
            ?? FirstNonEmpty(Environment.GetEnvironmentVariable("FACEBOOK_APP_ID"));

        var secret =
            FirstNonEmpty(state.AppSecret)
            ?? ResolveSecret(state.AppSecretSecretRef, configuration)
            ?? FirstNonEmpty(options.FacebookAppSecret)
            ?? FirstNonEmpty(configuration["Content:FacebookAppSecret"])
            ?? FirstNonEmpty(Environment.GetEnvironmentVariable("FACEBOOK_APP_SECRET"));

        var redirect =
            FirstNonEmpty(state.RedirectUri)
            ?? FirstNonEmpty(options.FacebookRedirectUri)
            ?? FirstNonEmpty(configuration["Content:FacebookRedirectUri"])
            ?? DefaultRedirectUri;

        return new ContentFacebookResolved(appId, secret, !string.IsNullOrWhiteSpace(secret), redirect);
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
