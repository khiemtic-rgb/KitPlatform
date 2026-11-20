namespace KitPlatform.Packs.Content.Production;

public sealed record ProductionCreateVideoPreflight(
    string ScopeKind,
    Guid TestRunId,
    Guid ScopeId,
    string Shot,
    Guid KeyframeArtifactId,
    string KeyframeSha,
    string KeyframeStatus,
    string KeyframeBinding,
    string Story,
    string Acting,
    string VisualContract,
    string Timing,
    string ProviderId,
    string ModelId,
    string ProviderCapability,
    decimal ProviderDuration,
    string Aspect,
    decimal EstimatedCost,
    decimal MaxCost,
    Guid InputSnapshotId,
    string InputSnapshotHash,
    Guid TaskId,
    Guid AttemptId,
    string Confirmation,
    string Network,
    string FilmMutation);

public static class ProductionCreateVideoPreflightRules
{
    public const string ConfirmationExplicit = "EXPLICIT";
    public const string NetworkRealThisTestOnly = "REAL RUNWAY HTTP ENABLED FOR THIS TEST ONLY";
    public const string FilmMutationForbidden = "FORBIDDEN";

    public static void RequireComplete(ProductionCreateVideoPreflight row)
    {
        if (row.ScopeKind != ProductionScopeAnchorRules.Test)
            throw new InvalidOperationException(ProductionFailureReasons.TestScopeRequired);
        if (row.TestRunId != ProductionTestSandboxIds.TestRunId
            || row.ScopeId != ProductionTestSandboxIds.ScopeId
            || row.Shot != ProductionTestSandboxIds.ShotCode)
            throw new InvalidOperationException(ProductionFailureReasons.TestRunMismatch);
        if (row.KeyframeArtifactId == Guid.Empty
            || string.IsNullOrWhiteSpace(row.KeyframeSha)
            || row.KeyframeStatus != ProductionArtifactStatuses.Verified
            || row.KeyframeBinding != ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Keyframe))
            throw new InvalidOperationException(ProductionFailureReasons.MissingKeyframe);
        if (string.IsNullOrWhiteSpace(row.Story)
            || string.IsNullOrWhiteSpace(row.Acting)
            || string.IsNullOrWhiteSpace(row.VisualContract)
            || row.ProviderDuration <= 0
            || row.EstimatedCost <= 0
            || row.EstimatedCost > row.MaxCost
            || row.InputSnapshotId == Guid.Empty
            || string.IsNullOrWhiteSpace(row.InputSnapshotHash)
            || row.TaskId == Guid.Empty
            || row.AttemptId == Guid.Empty)
            throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);
        if (row.ProviderId != ProductionMotionCatalog.RunwayProviderId
            || row.ModelId != ProductionMotionCatalog.RunwayGen4Turbo)
            throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
        if (row.Confirmation != ConfirmationExplicit
            || row.Network != NetworkRealThisTestOnly
            || row.FilmMutation != FilmMutationForbidden)
            throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
    }
}
