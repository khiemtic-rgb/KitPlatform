using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public sealed record OfficialCreateVideoPermitIssueRequest(
    ProductionScope Scope,
    ProductionAiSelection Selection,
    ProductionTaskInputSnapshot? Frozen,
    ShotExecutionInput? Live,
    CostPolicyDecision? Cost = null,
    FamixaVeoCostVariant? Variant = null,
    string IssuedBy = "checkpoint-13b");

public sealed record OfficialCreateVideoPermitIssueResult(
    ProductionExecutionPermit Permit,
    bool Created,
    bool Reused,
    string BindingReason);

/// <summary>
/// Official CREATE_VIDEO permit issuance only. Reuses
/// <see cref="IProductionExecutionPermitStore"/> / existing permit rows.
/// Does not consume, Start, or open the production resolver.
/// Extra binding (selection / snapshot / cost) lives in Reason — the canonical
/// row has no those columns.
/// </summary>
public static class OfficialCreateVideoPermitAuthorizationV1
{
    public const string ReasonPrefix = "OFFICIAL_CREATE_VIDEO_PERMIT_V1";
    public static readonly Guid OfficialSelectionId = Guid.Parse("c52b646f-86b9-4a43-834d-d94ab9f013b8");
    public const string IssuedBy = "checkpoint-13b";

    public static string BindingReason(
        ProductionAiSelection selection,
        ProductionTaskInputSnapshot frozen,
        FamixaProviderCostQuote quote)
    {
        var variant = quote.Variant ?? FamixaVeo31OfficialPricing.ProductionDefault;
        var provenance = quote.Provenance;
        return string.Join(';',
            ReasonPrefix,
            "selectionId=" + selection.SelectionId.ToString("D"),
            "snapshotId=" + frozen.SnapshotId.ToString("D"),
            "snapshotHash=" + (frozen.SnapshotHash ?? "").Trim(),
            "provider=" + RealVertexExecutionV1Lock.ProviderId,
            "model=" + RealVertexExecutionV1Lock.ModelId,
            "variant=" + variant.GenerationMode,
            "resolution=" + variant.Resolution,
            "audio=" + (variant.GenerateAudio ? "true" : "false"),
            "sampleCount=" + variant.SampleCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "amount=" + (quote.EstimatedAmount ?? 0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            "currency=" + (quote.Currency ?? ""),
            "unit=" + quote.BillingUnit,
            "costKind=" + quote.CostKind,
            "costAsOf=" + (provenance?.RetrievedOn ?? ""));
    }

    public static async Task<OfficialCreateVideoPermitIssueResult> IssueAsync(
        IProductionExecutionPermitStore permits,
        OfficialCreateVideoPermitIssueRequest request,
        CancellationToken ct = default)
    {
        var quote = RequireIssuanceGates(request);
        var frozen = request.Frozen!;
        var reason = BindingReason(request.Selection, frozen, quote);

        var latest = await permits.GetLatestAsync(
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode,
            ProductionActions.CreateVideo,
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId,
            ct);
        if (latest is not null)
        {
            if (!string.Equals(latest.Status, ProductionExecutionPermitStatuses.Issued, StringComparison.Ordinal)
                || latest.ConsumedAt is not null)
                throw new InvalidOperationException(ProductionFailureReasons.PermitConflict);
            if (!string.Equals(latest.Reason, reason, StringComparison.Ordinal))
                throw new InvalidOperationException(ProductionFailureReasons.PermitConflict);
            return new OfficialCreateVideoPermitIssueResult(latest, Created: false, Reused: true, reason);
        }

        var issued = await permits.IssueAsync(new IssueProductionExecutionPermitCommand(
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode,
            RealVertexExecutionV1Lock.EpisodeCode,
            RealVertexExecutionV1Lock.SceneCode,
            ProductionActions.CreateVideo,
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId,
            string.IsNullOrWhiteSpace(request.IssuedBy) ? IssuedBy : request.IssuedBy.Trim(),
            reason), ct);
        if (!string.Equals(issued.Reason, reason, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.PermitConflict);
        return new OfficialCreateVideoPermitIssueResult(issued, Created: true, Reused: false, reason);
    }

    public static FamixaProviderCostQuote RequireIssuanceGates(OfficialCreateVideoPermitIssueRequest request)
    {
        var scope = request.Scope;
        if (scope.BuildId != RealVertexExecutionV1Lock.BuildId)
            throw new InvalidOperationException(ProductionFailureReasons.UnauthorizedVertexTarget);
        if (!string.Equals(scope.ShotCode, RealVertexExecutionV1Lock.ShotCode, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.UnauthorizedVertexTarget);

        var selection = request.Selection;
        if (selection.SelectionId != OfficialSelectionId
            || !string.Equals(selection.Stage, ProductionAiSelectionStages.Video, StringComparison.Ordinal)
            || !string.Equals(selection.Status, ProductionAiSelectionStatuses.Active, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.SelectionConflict);
        if (!string.Equals(selection.ProviderId.Trim(), RealVertexExecutionV1Lock.ProviderId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(selection.ModelId.Trim(), RealVertexExecutionV1Lock.ModelId, StringComparison.OrdinalIgnoreCase)
            || selection.BuildId != RealVertexExecutionV1Lock.BuildId
            || !string.Equals(selection.ShotCode, RealVertexExecutionV1Lock.ShotCode, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);

        var frozen = request.Frozen;
        if (frozen is null)
            throw new InvalidOperationException(ProductionFailureReasons.InputNotFrozen);
        if (!string.Equals(frozen.ProviderId, RealVertexExecutionV1Lock.ProviderId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(frozen.ModelId, RealVertexExecutionV1Lock.ModelId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
        if (frozen.DurationSec != OfficialCreateVideoFreezeV1.OfficialDurationSec)
            throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
        if (!string.Equals(frozen.TimingHash, ShotExecutionCompiler.Hash(OfficialCreateVideoFreezeV1.OfficialTiming), StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
        if (frozen.PerformanceSnapshotId != OfficialCreateVideoFreezeV1.OfficialPerformanceId
            || !string.Equals(frozen.PerformanceHash, OfficialCreateVideoFreezeV1.OfficialPerformanceHash, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
        if (frozen.KeyframeArtifactId != OfficialCreateVideoFreezeV1.OfficialKeyframeId
            || !CreateVideoActionContractGate.IsPresent(frozen.KeyframeHash))
            throw new InvalidOperationException(ProductionFailureReasons.MissingKeyframe);

        if (request.Live is null)
            throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
        var adapter = new VeoMotionExecutionAdapter(new FakeVeoMotionTransport());
        ProductionFrozenCreateVideoMatch.Assert(frozen, request.Live, adapter);

        var variant = request.Variant ?? FamixaVeo31OfficialPricing.ProductionDefault;
        var quote = FamixaProviderCostCatalog.Quote(
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId,
            null,
            variant);
        RequireAuthoritativeCost(quote, variant);
        if (request.Cost is { } declared)
        {
            if (declared.Kind == CostPolicyKind.Unknown)
                throw new InvalidOperationException(ProductionFailureReasons.UnknownCost);
            if (declared.Kind == CostPolicyKind.Forbidden)
                throw new InvalidOperationException(declared.BlockReason ?? ProductionFailureReasons.OverBudget);
            if (declared.Kind != CostPolicyKind.Allowed
                || string.Equals(declared.BillingBasis, "EXPLICIT_TEST_POLICY", StringComparison.Ordinal))
                throw new InvalidOperationException(ProductionFailureReasons.UnknownCost);
        }

        return quote;
    }

    static void RequireAuthoritativeCost(FamixaProviderCostQuote quote, FamixaVeoCostVariant variant)
    {
        if (quote.CostKind == FamixaCostKind.Unknown || quote.EstimatedAmount is null)
            throw new InvalidOperationException(ProductionFailureReasons.UnknownCost);
        var decision = CostPolicyDecision.FromQuote(quote);
        if (decision.Kind == CostPolicyKind.Forbidden)
            throw new InvalidOperationException(decision.BlockReason ?? ProductionFailureReasons.OverBudget);
        if (decision.Kind != CostPolicyKind.Allowed)
            throw new InvalidOperationException(ProductionFailureReasons.UnknownCost);
        if (quote.EstimatedAmount != FamixaVeo31OfficialPricing.VideoOnly720Or1080Usd
            || !string.Equals(quote.Currency, FamixaVeo31OfficialPricing.Currency, StringComparison.Ordinal)
            || !string.Equals(quote.BillingUnit, FamixaVeo31OfficialPricing.Unit, StringComparison.Ordinal)
            || variant.GenerateAudio
            || !string.Equals(variant.GenerationMode, FamixaVeo31OfficialPricing.VideoOnly, StringComparison.Ordinal)
            || FamixaVeo31OfficialPricing.NormalizeResolution(variant.Resolution) != FamixaVeo31OfficialPricing.Res720p
            || variant.SampleCount != 1)
            throw new InvalidOperationException(ProductionFailureReasons.UnknownCost);
    }
}
