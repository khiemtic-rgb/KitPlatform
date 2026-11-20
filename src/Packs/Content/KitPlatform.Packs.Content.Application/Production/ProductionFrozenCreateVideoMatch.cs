using System.Globalization;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// CREATE_VIDEO freeze integrity. Fail closed — no repair, no refreeze, no retry on a new input.
/// </summary>
public static class ProductionFrozenCreateVideoMatch
{
    public static void Assert(
        ProductionTaskInputSnapshot frozen,
        ShotExecutionInput live,
        IMotionExecutionAdapter adapter)
    {
        var mismatch = FirstMismatch(frozen, live, adapter);
        if (mismatch is not null)
            throw new InvalidOperationException(mismatch);
    }

    public static string? FirstMismatch(
        ProductionTaskInputSnapshot frozen,
        ShotExecutionInput live,
        IMotionExecutionAdapter adapter)
    {
        var liveKfHash = LiveKeyframeHash(live);
        var liveTimingHash = ShotExecutionCompiler.Hash(live.Timing ?? "");

        if (!string.Equals(frozen.ProviderId, adapter.ProviderId, StringComparison.Ordinal))
            return Format(ProductionFailureReasons.ProviderChanged, "ProviderId", frozen.ProviderId, adapter.ProviderId);
        if (!string.Equals(frozen.ModelId, adapter.ModelId, StringComparison.Ordinal))
            return Format(ProductionFailureReasons.ModelChanged, "ModelId", frozen.ModelId, adapter.ModelId);
        if (!string.Equals(frozen.Story, live.Story, StringComparison.Ordinal))
            return Format(ProductionFailureReasons.InputSnapshotMismatch, "Story", frozen.Story, live.Story);
        if (!string.Equals(frozen.ActingBeat, live.ActingBeat, StringComparison.Ordinal))
            return Format(ProductionFailureReasons.InputSnapshotMismatch, "ActingBeat", frozen.ActingBeat, live.ActingBeat);
        if (!string.Equals(frozen.VisualContract, live.VisualContract, StringComparison.Ordinal))
            return Format(ProductionFailureReasons.InputSnapshotMismatch, "VisualContract", frozen.VisualContract, live.VisualContract);
        if (frozen.DurationSec != live.DurationSec)
            return Format(
                ProductionFailureReasons.InputSnapshotMismatch,
                "DurationSec",
                Dec(frozen.DurationSec),
                Dec(live.DurationSec));
        if (!string.Equals(frozen.TimingHash, liveTimingHash, StringComparison.Ordinal))
            return Format(ProductionFailureReasons.InputSnapshotMismatch, "TimingHash", frozen.TimingHash, liveTimingHash);
        if (frozen.PerformanceSnapshotId != live.PerformanceSnapshotId)
            return Format(
                ProductionFailureReasons.InputSnapshotMismatch,
                "PerformanceSnapshotId",
                GuidText(frozen.PerformanceSnapshotId),
                GuidText(live.PerformanceSnapshotId));
        if (!string.Equals(frozen.PerformanceHash ?? "", live.PerformanceHash ?? "", StringComparison.Ordinal))
            return Format(
                ProductionFailureReasons.InputSnapshotMismatch,
                "PerformanceHash",
                frozen.PerformanceHash ?? "",
                live.PerformanceHash ?? "");
        if (frozen.KeyframeArtifactId != live.KeyframeArtifactId)
            return Format(
                ProductionFailureReasons.InputSnapshotMismatch,
                "KeyframeArtifactId",
                GuidText(frozen.KeyframeArtifactId),
                GuidText(live.KeyframeArtifactId));
        if (!string.Equals(frozen.KeyframeHash, liveKfHash, StringComparison.Ordinal))
            return Format(ProductionFailureReasons.InputSnapshotMismatch, "KeyframeHash", frozen.KeyframeHash, liveKfHash);

        var projected = ProjectLive(frozen, live, adapter, liveTimingHash, liveKfHash);
        var liveHash = ShotExecutionCompiler.SnapshotHash(projected);
        if (!string.Equals(frozen.SnapshotHash, liveHash, StringComparison.Ordinal))
            return Format(ProductionFailureReasons.InputSnapshotMismatch, "SnapshotHash", frozen.SnapshotHash, liveHash);
        return null;
    }

    public static string Format(string code, string field, string frozen, string live) =>
        $"{code} · {field} · frozen={frozen} · live={live}";

    public static string LiveKeyframeHash(ShotExecutionInput live) =>
        CreateVideoActionContractGate.IsPresent(live.KeyframeByteSha256)
            ? live.KeyframeByteSha256.Trim()
            : (live.KeyframeHash ?? "").Trim();

    static ProductionTaskInputSnapshot ProjectLive(
        ProductionTaskInputSnapshot frozen,
        ShotExecutionInput live,
        IMotionExecutionAdapter adapter,
        string liveTimingHash,
        string liveKfHash) =>
        frozen with
        {
            Story = live.Story,
            ActingBeat = live.ActingBeat,
            VisualContract = live.VisualContract,
            StoryHash = ShotExecutionCompiler.Hash(live.Story),
            ActingHash = ShotExecutionCompiler.Hash(live.ActingBeat),
            VisualHash = ShotExecutionCompiler.Hash(live.VisualContract),
            KeyframeHash = liveKfHash,
            TimingHash = liveTimingHash,
            ProviderId = adapter.ProviderId,
            ModelId = adapter.ModelId,
            KeyframeArtifactId = live.KeyframeArtifactId,
            DurationSec = live.DurationSec,
            PerformanceSnapshotId = live.PerformanceSnapshotId,
            PerformanceHash = live.PerformanceHash,
        };

    static string GuidText(Guid? id) => id is { } value ? value.ToString("D") : "";

    static string Dec(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
