using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public enum CostPolicyKind
{
    Unknown,
    Allowed,
    Forbidden,
}

public enum RealVertexOutputMode
{
    InlineBytes,
    GcsUri,
    Unsupported,
}

/// <summary>
/// Declared cost policy for preflight. Does not invent catalog prices.
/// Production default is <see cref="FromCatalog"/> — google/veo-3.1 VIDEO_ONLY 720p is official $0.20 / count.
/// </summary>
public sealed record CostPolicyDecision(
    CostPolicyKind Kind,
    string? BlockReason,
    decimal? EstimatedAmount,
    FamixaCostKind QuoteKind,
    string BillingBasis)
{
    public static CostPolicyDecision FromCatalog(string providerId, string modelId, decimal durationSec)
        => FromQuote(FamixaProviderCostCatalog.Quote(providerId, modelId, (double)durationSec));

    public static CostPolicyDecision FromQuote(FamixaProviderCostQuote quote)
    {
        var evaluated = ProductionMotionCostGuard.EvaluateQuote(quote);
        if (quote.CostKind == FamixaCostKind.Unknown || quote.EstimatedAmount is null)
            return new(CostPolicyKind.Unknown, ProductionFailureReasons.UnknownCost, null, quote.CostKind, quote.BillingBasis);
        if (!evaluated.Allowed)
            return new(CostPolicyKind.Forbidden, evaluated.BlockReason ?? ProductionFailureReasons.OverBudget,
                evaluated.EstimatedCredits, quote.CostKind, quote.BillingBasis);
        return new(CostPolicyKind.Allowed, null, evaluated.EstimatedCredits, quote.CostKind, quote.BillingBasis);
    }

    public static CostPolicyDecision ExplicitAllowed(decimal amount) =>
        new(CostPolicyKind.Allowed, null, amount, FamixaCostKind.Estimate, "EXPLICIT_TEST_POLICY");

    public static CostPolicyDecision ExplicitForbidden(string reason) =>
        new(CostPolicyKind.Forbidden, reason, null, FamixaCostKind.Estimate, "EXPLICIT_TEST_POLICY");

    public static CostPolicyDecision ExplicitUnknown() =>
        new(CostPolicyKind.Unknown, ProductionFailureReasons.UnknownCost, null, FamixaCostKind.Unknown, FamixaProviderCostCatalog.BasisUnknown);
}

/// <summary>Permission/reference only. Not a replacement for ProductionTaskInputSnapshot.</summary>
public sealed record RealVertexExecutionAuthorization(
    string ProviderId,
    string ModelId,
    Guid BuildId,
    string ShotCode,
    Guid SnapshotId,
    string SnapshotHash,
    Guid? PerformanceSnapshotId,
    string? PerformanceHash,
    Guid? KeyframeArtifactId,
    string KeyframeHash,
    CostPolicyDecision Cost,
    Guid? PermitId,
    string? PermitHash,
    bool AuthorizePaidNetwork,
    DateTimeOffset AuthorizedAt);

public sealed record RealVertexExecutionPreflightResult(
    bool Allowed,
    IReadOnlyList<string> Blockers,
    RealVertexExecutionAuthorization? Authorization);

public sealed record RealVertexExecutionPreflightRequest(
    ProductionScope Scope,
    ProductionAiSelection? ActiveVideoSelection,
    bool ResolverEnabled,
    IProductionMotionAdapterResolver? Resolver,
    ProductionTaskInputSnapshot? Frozen,
    ShotExecutionInput? LiveClaim,
    IMotionExecutionAdapter? Adapter,
    byte[]? KeyframeBytes,
    CostPolicyDecision Cost,
    ProductionExecutionPermit? Permit,
    bool AuthorizePaidNetwork,
    IVeoHttpAuthenticator? Authenticator,
    bool HttpCompositionAvailable,
    bool ExecutionAuthorizationEnabled,
    RealVertexOutputMode ExpectedOutputMode = RealVertexOutputMode.InlineBytes,
    bool ReplayConflict = false,
    DateTimeOffset? AsOf = null,
    OfficialVeoPaidNetworkAuthorizationResult? OfficialPaidNetwork = null,
    OfficialVeoAuthenticatorCompositionResult? OfficialAuthenticator = null,
    OfficialVeoHttpCompositionResult? OfficialHttpComposition = null,
    OfficialVeoProviderResolverResult? OfficialResolver = null,
    OfficialVeoExecutionAuthorizationResult? OfficialExecutionAuthorization = null);

/// <summary>
/// Read-only Real Vertex preflight. Never starts transport, never reads secrets, never consumes permit.
/// </summary>
public static class RealVertexExecutionPreflight
{
    public const string TargetProvider = FamixaProviderIds.Google;
    public const string TargetModel = ProductionMotionCatalog.Veo31ModelId;

    public static RealVertexExecutionPreflightRequest ProductionClosedDefaults(
        ProductionScope scope,
        ProductionAiSelection? selection = null,
        ProductionTaskInputSnapshot? frozen = null,
        ShotExecutionInput? live = null) =>
        new(
            scope,
            selection,
            ResolverEnabled: ProductionVeoReadinessComposition.ProductionResolverIncludesVeo,
            Resolver: ProductionVeoReadinessComposition.CreateProductionResolver(),
            Frozen: frozen,
            LiveClaim: live,
            Adapter: null,
            KeyframeBytes: null,
            Cost: CostPolicyDecision.FromCatalog(TargetProvider, TargetModel, frozen?.DurationSec > 0 ? frozen.DurationSec : 10),
            Permit: null,
            AuthorizePaidNetwork: false,
            Authenticator: new ProductionVeoHttpAuthenticator(),
            HttpCompositionAvailable: false,
            ExecutionAuthorizationEnabled: ProductionVeoReadinessComposition.RealVertexExecutionOpen);

    public static RealVertexExecutionPreflightResult Evaluate(RealVertexExecutionPreflightRequest request)
    {
        var blockers = new List<string>();
        Collect(request, blockers);
        var allowed = blockers.Count == 0;
        return new RealVertexExecutionPreflightResult(
            allowed,
            blockers,
            allowed ? AuthorizationOf(request) : null);
    }

    static void Collect(RealVertexExecutionPreflightRequest request, List<string> blockers)
    {
        if (!ProductionScopeAnchorRules.IsOfficialFilm(request.Scope.BuildId))
            Add(blockers, PerformanceContractIds.OfficialFilmForbidden);

        var selection = request.ActiveVideoSelection;
        if (selection is null
            || !CreateVideoActionContractGate.IsPresent(selection.ProviderId)
            || !CreateVideoActionContractGate.IsPresent(selection.ModelId))
            Add(blockers, ProductionFailureReasons.MissingProductionAiSelection);
        else
        {
            if (!string.Equals(selection.ProviderId.Trim(), TargetProvider, StringComparison.OrdinalIgnoreCase))
                Add(blockers, ProductionFailureReasons.ProviderModelMismatch);
            if (!string.Equals(selection.ModelId.Trim(), TargetModel, StringComparison.OrdinalIgnoreCase))
                Add(blockers, ProductionFailureReasons.ProviderModelMismatch);
        }

        if (!IsResolverReady(request))
        {
            if (!request.ResolverEnabled)
                Add(blockers, ProductionFailureReasons.UnknownProvider);
            else if (request.Resolver is null)
                Add(blockers, ProductionFailureReasons.UnknownProvider);
            else
            {
                try
                {
                    var resolved = request.Resolver.Resolve(TargetProvider, TargetModel);
                    if (resolved is null)
                        Add(blockers, ProductionFailureReasons.UnknownProvider);
                }
                catch (InvalidOperationException ex) when (
                    ex.Message.StartsWith(ProductionFailureReasons.UnknownProvider, StringComparison.Ordinal)
                    || ex.Message.StartsWith(ProductionFailureReasons.ProviderModelMismatch, StringComparison.Ordinal))
                {
                    Add(blockers, ProductionFailureReasons.UnknownProvider);
                }
            }
        }

        if (request.Frozen is null)
            Add(blockers, ProductionFailureReasons.InputNotFrozen);
        else
        {
            if (request.LiveClaim is null || request.Adapter is null)
                Add(blockers, ProductionFailureReasons.InputSnapshotMismatch);
            else
            {
                var mismatch = ProductionFrozenCreateVideoMatch.FirstMismatch(
                    request.Frozen, request.LiveClaim, request.Adapter);
                if (mismatch is not null)
                    Add(blockers, CodeOf(mismatch));
            }

            if (request.Frozen.KeyframeArtifactId is null || request.Frozen.KeyframeArtifactId == Guid.Empty)
                Add(blockers, ProductionFailureReasons.MissingKeyframe);
            else if (request.KeyframeBytes is null || request.KeyframeBytes.Length < 32)
                Add(blockers, ProductionFailureReasons.KeyframeBytesMissing);
            else if (!string.Equals(
                request.Frozen.KeyframeHash,
                KitVideoIntegrityRules.Sha256Hex(request.KeyframeBytes),
                StringComparison.OrdinalIgnoreCase))
            {
                Add(blockers, ProductionFailureReasons.InputSnapshotMismatch);
            }
        }

        if (request.Cost.Kind == CostPolicyKind.Unknown)
            Add(blockers, ProductionFailureReasons.UnknownCost);
        else if (request.Cost.Kind != CostPolicyKind.Allowed)
            Add(blockers, request.Cost.BlockReason ?? ProductionFailureReasons.OverBudget);

        var permitBlock = ProductionOfficialPermitGate.BlockReason(
            request.Permit,
            request.Scope.BuildId,
            request.Scope.ShotCode,
            ProductionActions.CreateVideo,
            TargetProvider,
            TargetModel);
        if (permitBlock is not null)
            Add(blockers, permitBlock);

        if (!IsPaidNetworkAuthorized(request))
            Add(blockers, ProductionFailureReasons.PaidNetworkForbidden);

        if (!IsAuthenticatorReady(request))
            Add(blockers, ProductionFailureReasons.AuthenticatorNotReady);

        if (!IsHttpCompositionAvailable(request))
            Add(blockers, ProductionFailureReasons.HttpCompositionUnavailable);

        if (!IsExecutionAuthorizationEnabled(request))
            Add(blockers, ProductionFailureReasons.ExecutionAuthorizationRequired);

        if (request.ExpectedOutputMode != RealVertexOutputMode.InlineBytes)
            Add(blockers, ProductionFailureReasons.ProviderOutputUnsupported);

        if (request.ReplayConflict)
            Add(blockers, ProductionFailureReasons.InFlightReplay);
    }

    public static bool IsAuthenticatorReady(IVeoHttpAuthenticator? authenticator) =>
        authenticator is FakeVeoHttpAuthenticator;

    public static bool IsAuthenticatorReady(RealVertexExecutionPreflightRequest request)
    {
        if (request.OfficialAuthenticator is { Ready: true }
            && OfficialVeoProductionAuthenticatorV1.IsBoundTo(request))
            return true;
        return request.Authenticator is FakeVeoHttpAuthenticator;
    }

    public static bool IsResolverReady(RealVertexExecutionPreflightRequest request)
    {
        if (request.OfficialResolver is { Ready: true }
            && OfficialVeoProviderResolverV1.IsBoundTo(request))
            return true;
        if (!request.ResolverEnabled || request.Resolver is null)
            return false;
        try
        {
            return request.Resolver.Resolve(TargetProvider, TargetModel) is not null;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static bool IsExecutionAuthorizationEnabled(RealVertexExecutionPreflightRequest request)
    {
        if (request.OfficialExecutionAuthorization is { Authorized: true }
            && OfficialVeoExecutionAuthorizationV1.IsBoundTo(request))
            return true;
        return request.ExecutionAuthorizationEnabled;
    }

    public static bool IsHttpCompositionAvailable(RealVertexExecutionPreflightRequest request)
    {
        if (request.OfficialHttpComposition is { Ready: true }
            && OfficialVeoHttpCompositionV1.IsBoundTo(request))
            return true;
        return request.HttpCompositionAvailable;
    }

    public static bool IsPaidNetworkAuthorized(RealVertexExecutionPreflightRequest request)
    {
        if (request.OfficialPaidNetwork is { } official)
            return official.Authorized;
        return request.AuthorizePaidNetwork;
    }

    static RealVertexExecutionAuthorization AuthorizationOf(RealVertexExecutionPreflightRequest request)
    {
        var frozen = request.Frozen!;
        return new RealVertexExecutionAuthorization(
            TargetProvider,
            TargetModel,
            request.Scope.BuildId,
            request.Scope.ShotCode,
            frozen.SnapshotId,
            frozen.SnapshotHash,
            frozen.PerformanceSnapshotId,
            frozen.PerformanceHash,
            frozen.KeyframeArtifactId,
            frozen.KeyframeHash,
            request.Cost,
            request.Permit?.PermitId,
            request.Permit?.PermitHash,
            request.AuthorizePaidNetwork,
            request.AsOf ?? DateTimeOffset.UnixEpoch);
    }

    static void Add(List<string> blockers, string code)
    {
        if (!blockers.Contains(code, StringComparer.Ordinal))
            blockers.Add(code);
    }

    static string CodeOf(string mismatch)
    {
        var sep = mismatch.IndexOf(" · ", StringComparison.Ordinal);
        return sep < 0 ? mismatch : mismatch[..sep];
    }
}
