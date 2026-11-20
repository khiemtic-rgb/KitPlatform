namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Recovers frozen CREATE_VOICE line identity from the existing snapshot.
/// LineId is not a first-class snapshot column. It is read from PerformancePayloadJson,
/// imported shot-contract provenance, or the CREATE_VOICE freeze token. No schema change.
/// </summary>
public static class ProductionVoiceLineIdentity
{
    public static string? LineIdFromSnapshot(ProductionTaskInputSnapshot? snap)
    {
        return ReadLineId(snap?.PerformancePayloadJson)
            ?? ReadLineId(snap?.ShotContract)
            ?? PipeLineId(snap?.ShotContract);
    }

    static string? ReadLineId(string? json)
    {
        var text = json ?? "";
        const string key = "\"lineId\":\"";
        var i = text.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return null;
        var start = i + key.Length;
        var end = text.IndexOf('"', start);
        if (end <= start) return null;
        var lineId = text[start..end].Trim();
        return lineId.Length == 0 ? null : lineId;
    }

    static string? PipeLineId(string? raw)
    {
        if (!ProductionVoiceFreeze.TryParse(raw, out var lineId, out _, out _, out _))
            return null;
        return string.IsNullOrWhiteSpace(lineId) ? null : lineId.Trim();
    }

    public static bool Matches(ProductionTaskInputSnapshot? snap, string? lineId)
    {
        var wanted = (lineId ?? "").Trim();
        if (wanted.Length == 0) return false;
        var frozen = LineIdFromSnapshot(snap);
        return !string.IsNullOrWhiteSpace(frozen)
            && string.Equals(frozen, wanted, StringComparison.Ordinal);
    }
}

/// <summary>
/// CURRENT:VOICE resolution. Empty if no binding. Throws on scope/line/identity mismatch.
/// Never reads IndexedDB, provider URL, latest voice, or voiceId alone.
/// </summary>
public sealed record ProductionVoiceResolution(
    Guid BuildId,
    string ShotCode,
    string? LineId,
    Guid? ArtifactId,
    Guid? TaskId,
    Guid? AttemptId,
    string? InputSnapshotHash,
    string? ProviderId,
    string? ModelId,
    string? ByteSha256,
    string? StorageKey,
    string? MimeType,
    decimal? DurationSec,
    string? Role = null,
    string? Reason = null)
{
    public bool IsEmpty => ArtifactId is null;

    public static ProductionVoiceResolution Empty(Guid buildId, string shotCode, string? lineId, string reason) =>
        new(buildId, shotCode, lineId, null, null, null, null, null, null, null, null, null, null, null, reason);
}
