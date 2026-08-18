using System.Text.Json;
using System.Text.Json.Nodes;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentFacebookLinkState
{
    public string Status { get; set; } = "DISCONNECTED";
    public string? PageId { get; set; }
    public string? PageName { get; set; }
    public List<string> Scopes { get; set; } = [];
    public DateTimeOffset? ConnectedAt { get; set; }
    public DateTimeOffset? LastVerifiedAt { get; set; }
    public string? LastError { get; set; }
    public string TokenType { get; set; } = "page";
}

internal static class ContentFacebookLink
{
    public const string Key = "facebook";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static ContentFacebookLinkState Parse(string? configJson)
    {
        var node = ParseObject(configJson);
        if (!node.TryGetPropertyValue(Key, out var raw) || raw is null)
            return new ContentFacebookLinkState();
        try
        {
            return raw.Deserialize<ContentFacebookLinkState>(JsonOpts) ?? new ContentFacebookLinkState();
        }
        catch
        {
            return new ContentFacebookLinkState();
        }
    }

    public static string Apply(string? configJson, ContentFacebookLinkState link)
    {
        var node = ParseObject(configJson);
        node[Key] = JsonSerializer.SerializeToNode(link, JsonOpts);
        return node.ToJsonString();
    }

    private static JsonObject ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new JsonObject();
        try
        {
            return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }
}
