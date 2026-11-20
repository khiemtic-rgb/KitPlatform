namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Read-only bytes of a tenant-NULL verified VOICE.
/// The caller identifies the row. The implementation chooses the file from that row's StorageKey.
/// </summary>
public interface IUnattributedVoiceSource
{
    Task<UnattributedVoice?> ReadAsync(Guid artifactId, CancellationToken ct = default);
}

public sealed record UnattributedVoice(
    Guid ArtifactId,
    string Role,
    string MimeType,
    string ByteSha256,
    decimal DurationSec,
    string ProviderId,
    string ModelId,
    string Story,
    string LineId,
    byte[] Bytes);
