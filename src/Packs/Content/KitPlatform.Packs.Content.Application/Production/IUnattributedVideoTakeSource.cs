namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Read-only bytes of a tenant-NULL verified VIDEO_TAKE.
/// The caller identifies the row. The implementation chooses the file from that row's StorageKey.
/// </summary>
public interface IUnattributedVideoTakeSource
{
    Task<UnattributedVideoTake?> ReadAsync(Guid artifactId, CancellationToken ct = default);
}

public sealed record UnattributedVideoTake(
    Guid ArtifactId,
    string Role,
    string MimeType,
    string ByteSha256,
    decimal DurationSec,
    string ProviderId,
    string ModelId,
    byte[] Bytes);
