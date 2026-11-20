namespace KitPlatform.Packs.Content.Production;

public static class ProductionStateMachines
{
    public static bool CanTransitionRequest(string from, string to) => (from, to) switch
    {
        (ProductionRequestStatuses.Draft, ProductionRequestStatuses.Submitted) => true,
        (ProductionRequestStatuses.Submitted, ProductionRequestStatuses.InProgress) => true,
        (ProductionRequestStatuses.InProgress, ProductionRequestStatuses.Completed) => true,
        (ProductionRequestStatuses.InProgress, ProductionRequestStatuses.Failed) => true,
        (ProductionRequestStatuses.Draft, ProductionRequestStatuses.Cancelled) => true,
        (ProductionRequestStatuses.Submitted, ProductionRequestStatuses.Cancelled) => true,
        _ => from == to,
    };

    public static bool CanTransitionTask(string from, string to) => (from, to) switch
    {
        (ProductionTaskStatuses.Created, ProductionTaskStatuses.InputFrozen) => true,
        (ProductionTaskStatuses.InputFrozen, ProductionTaskStatuses.ProviderFrozen) => true,
        (ProductionTaskStatuses.ProviderFrozen, ProductionTaskStatuses.Running) => true,
        (ProductionTaskStatuses.Running, ProductionTaskStatuses.Succeeded) => true,
        (ProductionTaskStatuses.Running, ProductionTaskStatuses.FailedIdentity) => true,
        (ProductionTaskStatuses.Running, ProductionTaskStatuses.Failed) => true,
        (ProductionTaskStatuses.Created, ProductionTaskStatuses.Superseded) => true,
        (ProductionTaskStatuses.InputFrozen, ProductionTaskStatuses.Superseded) => true,
        (ProductionTaskStatuses.ProviderFrozen, ProductionTaskStatuses.Superseded) => true,
        (ProductionTaskStatuses.Running, ProductionTaskStatuses.Superseded) => true,
        (ProductionTaskStatuses.Succeeded, ProductionTaskStatuses.Superseded) => true,
        (ProductionTaskStatuses.FailedIdentity, ProductionTaskStatuses.Superseded) => true,
        _ => from == to,
    };

    public static bool CanTransitionAttempt(string from, string to) => (from, to) switch
    {
        (ProductionAttemptStatuses.Started, ProductionAttemptStatuses.ProviderSubmitted) => true,
        (ProductionAttemptStatuses.Started, ProductionAttemptStatuses.ProviderFailed) => true,
        (ProductionAttemptStatuses.ProviderSubmitted, ProductionAttemptStatuses.ProviderSucceeded) => true,
        (ProductionAttemptStatuses.ProviderSubmitted, ProductionAttemptStatuses.ProviderFailed) => true,
        (ProductionAttemptStatuses.ProviderSucceeded, ProductionAttemptStatuses.Late) => true,
        (ProductionAttemptStatuses.ProviderFailed, ProductionAttemptStatuses.Late) => true,
        _ => from == to,
    };

    public static bool ProviderSuccessIsProductionSuccess() => false;

    public static bool ProviderSuccessIsCurrent() => false;

    public static bool ProviderSuccessIsVisible() => false;

    public static bool ProviderSuccessIsAccepted() => false;

    public static bool GenericInputChangedIsLegalState() => false;

    public static string ClassifyInputDrift(
        ProductionTaskInputSnapshot frozen,
        ProductionTaskInputSnapshot live)
    {
        if (frozen.StoryHash != live.StoryHash) return ProductionFailureReasons.StoryChanged;
        if (!string.Equals(frozen.PerformanceHash ?? "", live.PerformanceHash ?? "", StringComparison.Ordinal))
            return ProductionFailureReasons.ActingChanged;
        if (frozen.ActingHash != live.ActingHash) return ProductionFailureReasons.ActingChanged;
        if (frozen.KeyframeHash != live.KeyframeHash) return ProductionFailureReasons.KeyframeChanged;
        if (frozen.VoiceHash != live.VoiceHash) return ProductionFailureReasons.VoiceChanged;
        if (frozen.MotionHash != live.MotionHash) return ProductionFailureReasons.MotionChanged;
        if (frozen.ProviderId != live.ProviderId) return ProductionFailureReasons.ProviderChanged;
        if (frozen.ModelId != live.ModelId) return ProductionFailureReasons.ModelChanged;
        if (frozen.SnapshotHash != live.SnapshotHash) return ProductionFailureReasons.InputSnapshotMismatch;
        return "";
    }
}
