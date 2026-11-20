namespace KitPlatform.Packs.Content.Production;

public interface IRunwayTransport
{
    bool RequiresNetwork { get; }
    int HttpCallCount { get; }
    Task<RunwayTransportCreateResult> CreateImageToVideoAsync(RunwayTransportCreateRequest request, CancellationToken ct = default);
    Task<RunwayTransportTaskResult> GetTaskAsync(string providerJobId, CancellationToken ct = default);
    Task<RunwayTransportTaskResult> GetTaskAsync(
        string providerJobId, ProductionCredentialReference credential, string modelId, CancellationToken ct = default)
        => GetTaskAsync(providerJobId, ct);
    Task<byte[]> DownloadOutputAsync(string providerJobId, CancellationToken ct = default);
    Task<byte[]> DownloadOutputAsync(
        string providerJobId, ProductionCredentialReference credential, string modelId, CancellationToken ct = default)
        => DownloadOutputAsync(providerJobId, ct);
}

public sealed record RunwayTransportCreateRequest(
    Guid TaskId,
    Guid AttemptId,
    string ModelId,
    decimal DurationSec,
    string AspectRatio,
    string InputSnapshotHash,
    string KeyframeByteSha256,
    byte[]? KeyframeBytes = null,
    string? KeyframeMime = null,
    string? PromptText = null,
    ProductionScope? Scope = null,
    string? ShotCode = null,
    ProductionCredentialReference? Credential = null);

public sealed record RunwayTransportCreateResult(string ProviderJobId);

public sealed record RunwayTransportTaskResult(
    string ProviderJobId,
    string Status,
    string? FailureReason = null,
    string? ProviderFailureCode = null);
