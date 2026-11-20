using System.Text.Json;
using System.Text.Json.Nodes;

namespace KitPlatform.Packs.Content;

/// <summary>
/// AUTHORITY-STABILIZATION-01A — Famixa series_build / series_pilot persist only.
/// Incoming script is kept. Incoming media authority mutations are ignored.
/// Existing stored media keys are preserved. Not a generic JSON merge.
/// </summary>
public static class SeriesGraphMediaWriteCutover
{
    public const string SuiteId = "FAMIXA_SERIES_GRAPH_MEDIA_WRITE_CUTOVER_01A";

    public static readonly HashSet<string> MediaAuthorityKeys = new(StringComparer.Ordinal)
    {
        "takeUrl",
        "previewUrl",
        "lipsyncUrl",
        "acceptedTake",
        "localVideoPath",
        "turboTaskId",
        "turboStatus",
        "turboError",
        "lipsyncTaskId",
        "lipsyncStatus",
        "lipsyncError",
        "takeHistory",
        "runwayAttempts",
        "videoPipe",
        "videoVerified",
        "videoBytes",
        "videoMime",
    };

    static readonly JsonSerializerOptions Compact = new() { WriteIndented = false };

    public static string Apply(string? existingJson, string incomingJson)
    {
        var incoming = ParseObject(incomingJson);
        var existing = ParseObject(existingJson);
        RestoreRunsMedia(existing, incoming);
        return incoming.ToJsonString(Compact);
    }

    static JsonObject ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonNode.Parse(json) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    static void RestoreRunsMedia(JsonObject existing, JsonObject incoming)
    {
        var incomingRuns = incoming["runs"] as JsonObject;
        var existingRuns = existing["runs"] as JsonObject;
        if (incomingRuns is null)
        {
            if (existingRuns is not null)
                incoming["runs"] = existingRuns.DeepClone();
            return;
        }

        foreach (var pair in incomingRuns.ToList())
        {
            if (pair.Value is not JsonObject incomingRun)
                continue;
            var existingRun = existingRuns?[pair.Key] as JsonObject;
            ApplyRunMedia(existingRun, incomingRun);
        }

        if (existingRuns is null)
            return;
        foreach (var pair in existingRuns)
        {
            if (incomingRuns.ContainsKey(pair.Key) || pair.Value is null)
                continue;
            incomingRuns[pair.Key] = pair.Value.DeepClone();
        }
    }

    static void ApplyRunMedia(JsonObject? existing, JsonObject incoming)
    {
        foreach (var key in MediaAuthorityKeys)
        {
            if (existing is not null && existing.TryGetPropertyValue(key, out var kept) && kept is not null)
                incoming[key] = kept.DeepClone();
            else
                incoming.Remove(key);
        }

        RestoreNestedMediaUrl(existing, incoming, "lipsyncFrozenInput", "motionUrl");
        RestoreNestedMediaUrl(existing, incoming, "lipsyncExecutionAttempt", "motionUrl");
    }

    static void RestoreNestedMediaUrl(JsonObject? existing, JsonObject incoming, string objectKey, string urlKey)
    {
        if (incoming[objectKey] is not JsonObject incomingObj)
            return;
        var existingUrl = existing?[objectKey] is JsonObject existingObj
            && existingObj.TryGetPropertyValue(urlKey, out var kept)
            && kept is not null
            ? kept
            : null;
        if (existingUrl is not null)
            incomingObj[urlKey] = existingUrl.DeepClone();
        else
            incomingObj.Remove(urlKey);
    }
}
