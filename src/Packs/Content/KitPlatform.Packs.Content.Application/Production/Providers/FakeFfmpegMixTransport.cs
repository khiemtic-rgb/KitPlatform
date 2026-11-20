using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>Deterministic server-mix stand-in. Never opens a socket or starts FFmpeg.</summary>
public class FakeFfmpegMixTransport : IFfmpegMixTransport
{
    public const string JobPrefix = "ffmpeg-fake-";
    public const string Command = "ffmpeg -y -hide_banner -loglevel error -i input.mp4 -map 0:v:0 -map 0:a:0 -c copy -movflags +faststart output.mp4";

    public bool RequiresNetwork => false;
    public const string ConcatCommand =
        "ffmpeg -y -hide_banner -loglevel error -f concat -safe 0 -i list.txt -c copy -movflags +faststart output.mp4";

    public int ExecutionCount { get; private set; }
    public int ComposeExecutionCount { get; private set; }
    public int ConcatExecutionCount { get; private set; }
    public string? LastCommand { get; private set; }
    public string? LastComposeCommand { get; private set; }
    public string? LastConcatCommand { get; private set; }
    public bool ForceIncompatibleConcat { get; set; }
    public decimal? ForceConcatDuration { get; set; }
    public int FailConcatProcessRemaining { get; set; }
    public int FailRemuxProcessRemaining { get; set; }
    public int FailComposeProcessRemaining { get; set; }
    public string FailProcessSuffix { get; set; } = "deliberate";

    public Task<FfmpegMixTransportResult> RemuxOnceAsync(
        FfmpegMixTransportRequest request, CancellationToken ct = default)
    {
        _ = ct;
        if (request.LipsyncBytes is not { Length: >= 32 })
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (FailRemuxProcessRemaining > 0)
        {
            FailRemuxProcessRemaining--;
            ExecutionCount++;
            LastCommand = Command;
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity + ":ffmpeg:" + FailProcessSuffix);
        }
        ExecutionCount++;
        LastCommand = Command;
        var duration = request.DurationSec > 0 ? request.DurationSec : 5;
        var bytes = ProductionTestMp4.Encode(
            "mix-remux|" + request.InputSnapshotHash + request.TaskId.ToString("N"), duration);
        var job = JobPrefix + request.AttemptId.ToString("N")[..12];
        return Task.FromResult(new FfmpegMixTransportResult(job, bytes, ProductionTestMp4.Mime, Command));
    }

    public Task<FfmpegMixTransportResult> ComposeSegmentsOnceAsync(
        FfmpegMixComposeRequest request, CancellationToken ct = default)
    {
        _ = ct;
        var err = ProductionMixContract.Validate(request.Contract);
        if (err is not null)
            throw new InvalidOperationException(err);
        if (request.ParentBytes.Count != request.Contract.Parents.Count
            || request.ParentBytes.Any(b => b is not { Length: >= 32 }))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (FailComposeProcessRemaining > 0)
        {
            FailComposeProcessRemaining--;
            ComposeExecutionCount++;
            LastComposeCommand = FfmpegMixComposeGraph.CanonicalCommand(
                request.Contract, request.Contract.Parents.Select((_, i) => "in" + i + ".mp4").ToArray(), "output.mp4");
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity + ":ffmpeg-compose:" + FailProcessSuffix);
        }
        ComposeExecutionCount++;
        LastComposeCommand = FfmpegMixComposeGraph.CanonicalCommand(
            request.Contract, request.Contract.Parents.Select((_, i) => "in" + i + ".mp4").ToArray(), "output.mp4");
        var bytes = ProductionTestMp4.Encode(
            "mix-compose|" + request.InputSnapshotHash + request.TaskId.ToString("N"),
            request.Contract.OutputDurationSec);
        var job = JobPrefix + "c" + request.AttemptId.ToString("N")[..11];
        return Task.FromResult(new FfmpegMixTransportResult(job, bytes, ProductionTestMp4.Mime, LastComposeCommand));
    }

    public Task<FfmpegMixTransportResult> ConcatCopyOnceAsync(
        FfmpegEpisodeConcatRequest request, CancellationToken ct = default)
    {
        _ = ct;
        if (ForceIncompatibleConcat)
            throw new InvalidOperationException(ProductionFailureReasons.UnsupportedCompositionMode);
        var err = ProductionEpisodeAssemblyContract.Validate(request.Contract);
        if (err is not null)
            throw new InvalidOperationException(err);
        if (request.ParentBytes.Count != request.Contract.OrderedInputs.Count
            || request.ParentBytes.Any(b => b is not { Length: >= 32 }))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        for (var i = 0; i < request.Contract.OrderedInputs.Count; i++)
        {
            if (ProductionMixContract.NormSha(KitVideoIntegrityRules.Sha256Hex(request.ParentBytes[i]))
                != ProductionMixContract.NormSha(request.Contract.OrderedInputs[i].Sha256))
                throw new InvalidOperationException(ProductionFailureReasons.HashChanged);
        }
        if (FailConcatProcessRemaining > 0)
        {
            FailConcatProcessRemaining--;
            ConcatExecutionCount++;
            LastConcatCommand = ConcatCommand;
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity + ":ffmpeg-video:" + FailProcessSuffix);
        }
        ConcatExecutionCount++;
        LastConcatCommand = ConcatCommand;
        var duration = ForceConcatDuration ?? request.Contract.OutputDurationSec;
        var bytes = ProductionTestMp4.Encode(
            "episode-concat|" + request.InputSnapshotHash + request.TaskId.ToString("N"), duration);
        var job = JobPrefix + "e" + request.AttemptId.ToString("N")[..11];
        return Task.FromResult(new FfmpegMixTransportResult(job, bytes, ProductionTestMp4.Mime, ConcatCommand));
    }
}
