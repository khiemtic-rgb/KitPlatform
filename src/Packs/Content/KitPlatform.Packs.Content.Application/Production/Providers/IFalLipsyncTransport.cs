namespace KitPlatform.Packs.Content.Production;

public interface IFalLipsyncTransport
{
    bool RequiresNetwork { get; }
    int StartCount { get; }
    Task<FalLipsyncTransportResult> ExecuteOnceAsync(FalLipsyncTransportRequest request, CancellationToken ct = default);
    Task<FalLipsyncTransportResult> RecoverOnceAsync(string providerJobId, CancellationToken ct = default);
    Task<FalLipsyncTransportResult> RecoverOnceAsync(
        string providerJobId, ProductionCredentialReference credential, CancellationToken ct = default)
        => throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
}

public sealed record FalLipsyncTransportRequest(
    Guid TaskId,
    Guid AttemptId,
    byte[] VideoBytes,
    byte[] VoiceBytes,
    ProductionLipsyncSpeakerPin Pin,
    decimal VideoDurationSec,
    string InputSnapshotHash,
    ProductionScope? Scope = null,
    string? ShotCode = null,
    int[]? FaceBoxXyxy = null,
    string? SyncMode = null,
    ProductionCredentialReference? Credential = null);

public sealed record LipsyncStartContext(
    Guid AttemptId,
    ProductionScope? Scope,
    string? ShotCode,
    byte[] VideoBytes,
    byte[] VoiceBytes);

public sealed record FalLipsyncTransportResult(
    string ProviderJobId,
    byte[] Bytes,
    string MimeType);
