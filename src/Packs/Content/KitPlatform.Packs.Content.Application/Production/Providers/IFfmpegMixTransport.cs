namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// FFmpeg MIX transport. RemuxOnceAsync copies one LIPSYNC input (SH01).
/// ComposeSegmentsOnceAsync executes a frozen MIX_CONTRACT_V1. It must not invent duration or remainder.
/// </summary>
public interface IFfmpegMixTransport
{
    bool RequiresNetwork { get; }
    int ExecutionCount { get; }
    int ComposeExecutionCount { get; }
    int ConcatExecutionCount { get; }
    string? LastCommand { get; }
    string? LastComposeCommand { get; }
    string? LastConcatCommand { get; }
    Task<FfmpegMixTransportResult> RemuxOnceAsync(FfmpegMixTransportRequest request, CancellationToken ct = default);
    Task<FfmpegMixTransportResult> ComposeSegmentsOnceAsync(FfmpegMixComposeRequest request, CancellationToken ct = default);
    Task<FfmpegMixTransportResult> ConcatCopyOnceAsync(FfmpegEpisodeConcatRequest request, CancellationToken ct = default);
}

public sealed record FfmpegMixTransportRequest(
    Guid TaskId,
    Guid AttemptId,
    byte[] LipsyncBytes,
    decimal DurationSec,
    string InputSnapshotHash);

public sealed record FfmpegMixComposeRequest(
    Guid TaskId,
    Guid AttemptId,
    ProductionMixContractV1 Contract,
    IReadOnlyList<byte[]> ParentBytes,
    string InputSnapshotHash);

public sealed record MixStartContext(
    Guid AttemptId,
    byte[] LipsyncBytes,
    byte[]? VideoTakeBytes = null,
    byte[]? KeyframeBytes = null,
    byte[]? VoiceBytes = null);

public sealed record FfmpegMixTransportResult(
    string ProviderJobId,
    byte[] Bytes,
    string MimeType,
    string Command);

public sealed record FfmpegEpisodeConcatRequest(
    Guid TaskId,
    Guid AttemptId,
    ProductionEpisodeAssemblyContractV1 Contract,
    IReadOnlyList<byte[]> ParentBytes,
    string InputSnapshotHash);
