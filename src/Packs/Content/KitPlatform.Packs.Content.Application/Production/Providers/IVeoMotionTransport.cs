namespace KitPlatform.Packs.Content.Production;

public interface IVeoMotionTransport
{
    bool RequiresNetwork { get; }
    int HttpCallCount { get; }
    Task<VeoMotionTransportCreateResult> CreateImageToVideoAsync(VeoMotionTransportCreateRequest request, CancellationToken ct = default);
    Task<VeoMotionTransportPollResult> GetOperationAsync(string providerJobId, CancellationToken ct = default);
    Task<VeoMotionTransportPollResult> GetOperationAsync(
        string providerJobId, ProductionCredentialReference? credential, CancellationToken ct = default)
        => GetOperationAsync(providerJobId, ct);
    Task<byte[]> DownloadOutputAsync(string providerJobId, CancellationToken ct = default);
}

public sealed record VeoMotionTransportCreateRequest(
    Guid TaskId,
    Guid AttemptId,
    string ProviderId,
    string ModelId,
    string Prompt,
    string ImageDataUri,
    string ImageMime,
    int ProviderDurationSec,
    decimal OsDurationSec,
    string AspectRatio,
    int SampleCount,
    string Resolution,
    bool GenerateAudio,
    string SnapshotHash,
    string KeyframeHash,
    Guid? KeyframeArtifactId,
    ProductionCredentialReference? Credential = null);

public sealed record VeoMotionTransportCreateResult(string ProviderJobId);

public sealed record VeoMotionTransportPollResult(
    string ProviderJobId,
    string Status,
    string? FailureReason = null);
