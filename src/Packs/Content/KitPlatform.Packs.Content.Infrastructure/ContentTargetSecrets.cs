using System.Text.Json;
using System.Text.Json.Nodes;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Write-only secrets for content site/channel targets — stored inside config_json.storedSecret
/// and stripped from all API responses.
/// </summary>
internal static class ContentTargetSecrets
{
    public const string StoredSecretKey = "storedSecret";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <param name="writeOnlySecret">
    /// null = keep existing; empty = clear; non-empty = replace.
    /// </param>
    public static string MergeConfig(string? incomingConfigJson, string? existingConfigJson, string? writeOnlySecret)
    {
        var node = ParseObject(incomingConfigJson);
        // Never trust client-sent storedSecret
        node.Remove(StoredSecretKey);

        var existingStored = ExtractStored(existingConfigJson);
        if (writeOnlySecret is null)
        {
            if (!string.IsNullOrWhiteSpace(existingStored))
                node[StoredSecretKey] = existingStored;
        }
        else if (string.IsNullOrWhiteSpace(writeOnlySecret))
        {
            node.Remove(StoredSecretKey);
        }
        else
        {
            node[StoredSecretKey] = writeOnlySecret.Trim();
        }

        return node.ToJsonString();
    }

    public static string? ExtractStored(string? configJson)
    {
        var node = ParseObject(configJson);
        if (node.TryGetPropertyValue(StoredSecretKey, out var v) && v is JsonValue jv)
        {
            var s = jv.GetValue<string?>();
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }
        return null;
    }

    public static (string RedactedJson, bool SecretConfigured) RedactForClient(string? configJson, string? secretRef)
    {
        var node = ParseObject(configJson);
        var hasStored = ExtractStored(configJson) is not null;
        node.Remove(StoredSecretKey);
        var configured = hasStored || !string.IsNullOrWhiteSpace(secretRef);
        return (node.ToJsonString(), configured);
    }

    private static JsonObject ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new JsonObject();
        try
        {
            var node = JsonNode.Parse(json);
            return node as JsonObject ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }
}
