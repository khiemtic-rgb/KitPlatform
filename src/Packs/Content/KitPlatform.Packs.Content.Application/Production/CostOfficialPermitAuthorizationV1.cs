using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public enum CostPolicySourceKind
{
    Missing,
    Incomplete,
    Catalog,
}

/// <summary>
/// Read-only inspection of the canonical cost catalog. Never invents a price.
/// google/veo-3.1 VIDEO_ONLY 720p uses <see cref="FamixaVeo31OfficialPricing"/>.
/// </summary>
public sealed record CostPolicySourceInspection(
    string ProviderId,
    string ModelId,
    bool CatalogBound,
    bool IsPaid,
    bool QuoteKnown,
    decimal? Amount,
    string? Currency,
    FamixaCostKind QuoteKind,
    string BillingBasis,
    string? EffectiveVersion,
    string? Validity,
    CostPolicySourceKind SourceKind,
    CostPolicyKind Policy,
    string? ReportCode);

/// <summary>
/// Independent authorization gates. Cost is not a permit. Permit is not a cost.
/// Does not mutate catalog, snapshot, or permit. Does not Start.
/// </summary>
public sealed record CostOfficialPermitAuthorizationReport(
    CostPolicySourceInspection CostSource,
    CostPolicyDecision Cost,
    bool CostGatePass,
    bool PermitGatePass,
    bool PaidNetworkPass,
    bool AuthenticatorPass,
    bool ResolverPass,
    bool FrozenSnapshotPass,
    bool KeyframePass,
    ProductionExecutionPermit? Permit,
    string? PermitBlockReason,
    RealVertexExecutionPreflightResult Preflight,
    string PaidNetworkState,
    bool HttpCompositionPass,
    bool ExecutionAuthorizationPass);

/// <summary>
/// Canonical cost source inspector. Reuses <see cref="FamixaProviderCostCatalog"/> only.
/// </summary>
public static class CostPolicySource
{
    public static CostPolicySourceInspection Inspect(
        string providerId,
        string modelId,
        decimal durationSec = OfficialCreateVideoFreezeV1.OfficialDurationSec)
    {
        var provider = (providerId ?? "").Trim();
        var model = (modelId ?? "").Trim();
        var catalogBound = ProductionMotionCatalog.TryGet(provider, model, out var catalog);
        var isPaid = catalogBound && catalog.IsPaid;
        var quote = FamixaProviderCostCatalog.Quote(provider, model, (double)durationSec);
        var quoteKnown = quote.CostKind != FamixaCostKind.Unknown && quote.EstimatedAmount is not null;
        var boundToRequest =
            string.Equals((quote.ProviderId ?? "").Trim(), provider, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(quote.ModelId)
                || string.Equals(quote.ModelId.Trim(), model, StringComparison.OrdinalIgnoreCase));

        var effectiveVersion = quote.Provenance?.EffectiveVersion;
        var validity = quote.Provenance?.Validity;

        CostPolicySourceKind sourceKind;
        string? report;
        CostPolicyKind policy;
        if (!quoteKnown)
        {
            sourceKind = CostPolicySourceKind.Missing;
            report = ProductionFailureReasons.CostPolicySourceMissing;
            policy = CostPolicyKind.Unknown;
        }
        else if (effectiveVersion is null || validity is null || !boundToRequest)
        {
            sourceKind = CostPolicySourceKind.Incomplete;
            report = ProductionFailureReasons.CostPolicySourceMissing;
            policy = CostPolicyKind.Unknown;
        }
        else
        {
            sourceKind = CostPolicySourceKind.Catalog;
            report = null;
            policy = CostPolicyDecision.FromQuote(quote).Kind;
        }

        return new CostPolicySourceInspection(
            provider,
            model,
            catalogBound,
            isPaid,
            quoteKnown,
            quote.EstimatedAmount,
            quote.Currency,
            quote.CostKind,
            quote.BillingBasis,
            effectiveVersion,
            validity,
            sourceKind,
            policy,
            report);
    }
}

/// <summary>
/// Official CREATE_VIDEO permit validation. Issuance exists on the store /
/// <see cref="ProductionReadinessAuthority.IssuePermitAsync"/> — this checkpoint does not issue.
/// </summary>
public static class ProductionOfficialPermitGate
{
    public const bool CheckpointIssuesOfficialCreateVideoPermit = false;

    public static string? BlockReason(
        ProductionExecutionPermit? permit,
        Guid buildId,
        string shotCode,
        string action,
        string providerId,
        string modelId)
    {
        if (permit is null)
            return PerformanceContractIds.OfficialFilmForbidden;
        if (string.Equals(permit.Status, ProductionExecutionPermitStatuses.Expired, StringComparison.Ordinal))
            return ProductionFailureReasons.PermitExpired;
        if (string.Equals(permit.Status, ProductionExecutionPermitStatuses.Consumed, StringComparison.Ordinal)
            || string.Equals(permit.Status, ProductionExecutionPermitStatuses.Revoked, StringComparison.Ordinal))
            return PerformanceContractIds.OfficialFilmForbidden;

        var identity = ProductionExecutionPermitCanon.HashOf(
            permit.BuildId, permit.ShotCode, permit.Action, permit.ProviderId, permit.ModelId);
        if (!string.Equals(permit.PermitHash, identity, StringComparison.Ordinal))
            return PerformanceContractIds.OfficialFilmForbidden;

        if (!ProductionExecutionPermitCanon.Matches(permit, buildId, shotCode, action, providerId, modelId))
            return PerformanceContractIds.OfficialFilmForbidden;
        return null;
    }
}

/// <summary>
/// Read-only cost + official-permit authorization. Never invents a catalog price,
/// never issues/consumes a permit, never starts Vertex.
/// </summary>
public static class CostOfficialPermitAuthorization
{
    public const bool AuthorizePaidNetworkProductionDefault = false;

    public static CostOfficialPermitAuthorizationReport Evaluate(RealVertexExecutionPreflightRequest request)
    {
        var duration = request.Frozen?.DurationSec > 0
            ? request.Frozen.DurationSec
            : OfficialCreateVideoFreezeV1.OfficialDurationSec;
        var source = CostPolicySource.Inspect(
            RealVertexExecutionPreflight.TargetProvider,
            RealVertexExecutionPreflight.TargetModel,
            duration);
        var permitBlock = ProductionOfficialPermitGate.BlockReason(
            request.Permit,
            request.Scope.BuildId,
            request.Scope.ShotCode,
            ProductionActions.CreateVideo,
            RealVertexExecutionPreflight.TargetProvider,
            RealVertexExecutionPreflight.TargetModel);
        var preflight = RealVertexExecutionPreflight.Evaluate(request);
        var paidPass = RealVertexExecutionPreflight.IsPaidNetworkAuthorized(request);
        return new CostOfficialPermitAuthorizationReport(
            source,
            request.Cost,
            CostGatePass: request.Cost.Kind == CostPolicyKind.Allowed,
            PermitGatePass: permitBlock is null,
            PaidNetworkPass: paidPass,
            AuthenticatorPass: RealVertexExecutionPreflight.IsAuthenticatorReady(request),
            ResolverPass: ResolverReady(request),
            FrozenSnapshotPass: FrozenReady(request),
            KeyframePass: KeyframeReady(request),
            request.Permit,
            permitBlock,
            preflight,
            paidPass
                ? ProductionFailureReasons.PaidNetworkAuthorized
                : ProductionFailureReasons.PaidNetworkForbidden,
            HttpCompositionPass: RealVertexExecutionPreflight.IsHttpCompositionAvailable(request),
            ExecutionAuthorizationPass: RealVertexExecutionPreflight.IsExecutionAuthorizationEnabled(request));
    }

    static bool ResolverReady(RealVertexExecutionPreflightRequest request) =>
        RealVertexExecutionPreflight.IsResolverReady(request);

    static bool FrozenReady(RealVertexExecutionPreflightRequest request)
    {
        if (request.Frozen is null || request.LiveClaim is null || request.Adapter is null)
            return false;
        return ProductionFrozenCreateVideoMatch.FirstMismatch(
            request.Frozen, request.LiveClaim, request.Adapter) is null;
    }

    static bool KeyframeReady(RealVertexExecutionPreflightRequest request)
    {
        var frozen = request.Frozen;
        if (frozen?.KeyframeArtifactId is null || frozen.KeyframeArtifactId == Guid.Empty)
            return false;
        if (request.KeyframeBytes is null || request.KeyframeBytes.Length < 32)
            return false;
        return string.Equals(
            frozen.KeyframeHash,
            KitVideoIntegrityRules.Sha256Hex(request.KeyframeBytes),
            StringComparison.OrdinalIgnoreCase);
    }
}
