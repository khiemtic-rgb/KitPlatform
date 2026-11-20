namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// CREATE_VIDEO Action Contract enforcement. Evaluates RequiredInputs only.
/// Does not resolve series-graph, localStorage, IndexedDB, or legacy flags.
/// </summary>
public static class CreateVideoActionContractGate
{
    public static IReadOnlyList<string> RequiredInputs => ProductionActionContractRegistry.CreateVideo.RequiredInputs;

    public static string ReasonFor(string requiredInput) => requiredInput switch
    {
        "ApprovedKeyframe" => ProductionFailureReasons.MissingKeyframe,
        "Story" => ProductionFailureReasons.MissingStory,
        "ActingBeat" => ProductionFailureReasons.MissingActingBeat,
        "VisualContract" => ProductionFailureReasons.MissingVisualContract,
        "Timing" => ProductionFailureReasons.MissingTiming,
        "FrozenProviderModel" => ProductionFailureReasons.MissingProviderModel,
        "PerformanceSnapshot" => ProductionFailureReasons.MissingPerformanceSnapshot,
        _ => ProductionFailureReasons.MissingDependency,
    };

    public static bool IsPresent(string? value) => !string.IsNullOrWhiteSpace(value);

    public static bool IsZeroTimingToken(string? timing)
    {
        var t = (timing ?? "").Trim().TrimEnd('s', 'S');
        return t.Length == 0
            || t == "0"
            || t == "0.0"
            || t == "0.00";
    }

    public static bool IsValidTiming(string? timing, decimal durationSec)
    {
        if (durationSec > 0) return true;
        var t = (timing ?? "").Trim();
        if (t.Length == 0 || IsZeroTimingToken(t)) return false;
        if (t.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            && decimal.TryParse(t[..^1], System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var sec))
            return sec > 0;
        return decimal.TryParse(t, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var n) && n > 0;
    }

    public static CreateVideoReadinessClaim ClaimFrom(ShotExecutionInput input) =>
        new(input.Story, input.ActingBeat, input.VisualContract, input.Timing, input.DurationSec, input.KeyframeArtifactId,
            PerformanceSnapshotId: input.PerformanceSnapshotId,
            PerformanceHash: input.PerformanceHash,
            LineId: input.LineId,
            CharacterId: input.SpeakerCharacterId,
            LineText: input.Story,
            VoiceId: input.VoiceId,
            MotionArtifactId: input.MotionArtifactId,
            VoiceArtifactId: input.VoiceArtifactId,
            LipsyncArtifactId: input.LipsyncArtifactId,
            PictureStrategy: input.PictureStrategy,
            SpeechDelivery: input.SpeechDelivery,
            StrategyConfirmed: input.StrategyConfirmed,
            DirectSpeakerCount: input.DirectSpeakerCount);

    public static CreateVideoContractVerdict Evaluate(
        ProductionScope scope,
        CreateVideoOsFacts os,
        CreateVideoReadinessClaim? claim,
        string providerId,
        string modelId)
    {
        _ = claim?.KfApproved;
        _ = claim?.TakeUrl;
        _ = claim?.PreviewUrl;
        _ = claim?.AcceptedTake;
        _ = claim?.TurboTaskId;
        _ = claim?.LegacyGraphReady;

        var missing = new List<string>();
        string? mutated = null;

        var kf = os.Keyframe;
        var kfOk = kf is not null
            && kf.Role == ProductionArtifactRoles.Keyframe
            && kf.VerificationStatus == ProductionArtifactStatuses.Verified
            && os.KeyframeOwnerScope is { } owner
            && owner.BuildId == scope.BuildId
            && string.Equals(owner.ShotCode, scope.ShotCode, StringComparison.Ordinal);
        if (!kfOk) missing.Add("ApprovedKeyframe");

        if (claim?.KeyframeArtifactId is { } claimedKf)
        {
            if (!kfOk || kf!.ArtifactId != claimedKf)
                mutated = ProductionFailureReasons.InputMutated;
        }

        var story = Pick(os.Story, claim?.Story);
        if (!IsPresent(story)) missing.Add("Story");

        var acting = Pick(os.ActingBeat, claim?.ActingBeat);
        if (!IsPresent(acting)) missing.Add("ActingBeat");

        var visual = Pick(os.VisualContract, claim?.VisualContract);
        if (!IsPresent(visual)) missing.Add("VisualContract");

        var duration = os.DirectorTimingAuthoritative && os.DurationSec > 0
            ? os.DurationSec
            : claim is { DurationSec: > 0 } ? claim.DurationSec.Value : os.DurationSec;
        var timingClaim = (claim?.Timing ?? "").Trim();
        var osTiming = (os.Timing ?? "").Trim();
        var timing = os.DirectorTimingAuthoritative && IsValidTiming(osTiming, os.DurationSec)
            ? (IsPresent(osTiming) && !IsZeroTimingToken(osTiming) ? osTiming : FormatDuration(duration))
            : IsValidTiming(timingClaim, 0) ? timingClaim
            : IsValidTiming(osTiming, os.DurationSec) ? (IsPresent(osTiming) && !IsZeroTimingToken(osTiming) ? osTiming : FormatDuration(duration))
            : "";
        if (!IsValidTiming(timing, duration)) missing.Add("Timing");
        if (!IsPresent(timing) && duration > 0) timing = FormatDuration(duration);

        var provider = (providerId ?? "").Trim();
        var model = (modelId ?? "").Trim();
        if (!IsPresent(provider) || !IsPresent(model)) missing.Add("FrozenProviderModel");

        ProductionPerformanceConsumption.CollectMissing(scope, ProductionActions.CreateVideo, claim, missing);
        var snapshotReason = missing.Contains(ProductionPerformanceConsumption.RequiredInput)
            ? ProductionPerformanceConsumption.ReasonForMissing(claim)
            : null;

        var reasons = missing.Select(m =>
            m == ProductionPerformanceConsumption.RequiredInput && snapshotReason is not null
                ? snapshotReason
                : ReasonFor(m)).ToArray();
        var block = mutated ?? (reasons.Length == 0 ? null : reasons[0]);
        var available = mutated is null && missing.Count == 0;
        return new CreateVideoContractVerdict(
            available,
            block,
            missing,
            reasons,
            story,
            acting,
            visual,
            timing,
            duration > 0 ? duration : 0,
            kfOk ? kf!.ArtifactId : null,
            kfOk ? kf!.ByteSha256 : "",
            provider,
            model,
            claim?.PerformanceSnapshotId,
            (claim?.PerformanceHash ?? "").Trim(),
            SpeechStartSec: os.DirectorTimingAuthoritative ? os.SpeechStartSec : null,
            SpeechEndSec: os.DirectorTimingAuthoritative ? os.SpeechEndSec : null);
    }

    static string Pick(string? osValue, string? claimValue)
    {
        var claimed = (claimValue ?? "").Trim();
        if (claimed.Length > 0) return claimed;
        return (osValue ?? "").Trim();
    }

    static string FormatDuration(decimal durationSec) =>
        durationSec > 0 ? durationSec.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "s" : "";
}

public sealed record CreateVideoReadinessClaim(
    string? Story = null,
    string? ActingBeat = null,
    string? VisualContract = null,
    string? Timing = null,
    decimal? DurationSec = null,
    Guid? KeyframeArtifactId = null,
    bool? KfApproved = null,
    string? TakeUrl = null,
    string? PreviewUrl = null,
    string? AcceptedTake = null,
    string? TurboTaskId = null,
    bool? LegacyGraphReady = null,
    string? ProviderId = null,
    string? ModelId = null,
    Guid? PerformanceSnapshotId = null,
    string? PerformanceHash = null,
    string? LineId = null,
    string? CharacterId = null,
    string? LineText = null,
    string? VoiceId = null,
    Guid? MotionArtifactId = null,
    Guid? VoiceArtifactId = null,
    Guid? LipsyncArtifactId = null,
    string? PictureStrategy = null,
    string? SpeechDelivery = null,
    bool StrategyConfirmed = false,
    int DirectSpeakerCount = 0);

public sealed record CreateVideoOsFacts(
    ProductionArtifact? Keyframe,
    ProductionScope? KeyframeOwnerScope,
    string? Story,
    string? ActingBeat,
    string? VisualContract,
    string? Timing,
    decimal DurationSec,
    bool DirectorTimingAuthoritative = false,
    decimal? SpeechStartSec = null,
    decimal? SpeechEndSec = null);

public sealed record CreateVideoContractVerdict(
    bool Available,
    string? BlockReason,
    IReadOnlyList<string> MissingInputs,
    IReadOnlyList<string> MissingReasons,
    string Story,
    string ActingBeat,
    string VisualContract,
    string Timing,
    decimal DurationSec,
    Guid? KeyframeArtifactId,
    string KeyframeByteSha256,
    string ProviderId,
    string ModelId,
    Guid? PerformanceSnapshotId = null,
    string PerformanceHash = "",
    string DerivedActingBeat = "",
    decimal? SpeechStartSec = null,
    decimal? SpeechEndSec = null);
