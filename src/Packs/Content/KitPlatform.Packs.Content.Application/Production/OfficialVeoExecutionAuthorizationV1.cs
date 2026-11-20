using System.Security.Cryptography;
using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public sealed record OfficialVeoExecutionAuthorizationRequest(
    ProductionScope Scope,
    string Action,
    ProductionAiSelection? Selection,
    ProductionTaskInputSnapshot? Frozen,
    ShotExecutionInput? Live,
    byte[]? KeyframeBytes,
    string KeyframeMime = "image/png",
    CostPolicyDecision? Cost = null,
    FamixaVeoCostVariant? Variant = null,
    ProductionExecutionPermit? Permit = null,
    OfficialVeoPaidNetworkAuthorizationResult? PaidNetwork = null,
    OfficialVeoAuthenticatorCompositionResult? Authenticator = null,
    OfficialVeoHttpCompositionResult? HttpComposition = null,
    OfficialVeoProviderResolverResult? Resolver = null,
    string? ProjectId = null,
    OfficialVeoExecutionAuthorizationResult? Existing = null);

public sealed record OfficialVeoGateResult(string Gate, bool Pass, string State, string? BlockReason);

public sealed record OfficialVeoExecutionAuthorizationResult(
    bool Authorized,
    string State,
    string? BlockReason,
    string AuthorizationHash,
    bool Reused,
    IReadOnlyList<OfficialVeoGateResult> Gates,
    OfficialVeoProviderResolverResult? Resolver,
    OfficialVeoHttpCompositionResult? HttpComposition,
    OfficialVeoAuthenticatorCompositionResult? Authenticator,
    OfficialVeoPaidNetworkAuthorizationResult? PaidNetwork,
    RealVertexExecutionAuthorization? Authorization);

/// <summary>
/// Official CREATE_VIDEO execution authorization. Permission artifact only.
/// Does not Start, OAuth, issue/consume a permit, or freeze.
/// </summary>
public static class OfficialVeoExecutionAuthorizationV1
{
    public const bool AuthorizeStartsVertex = false;
    public static readonly DateTimeOffset OfficialSelectedAt =
        DateTimeOffset.Parse("2026-09-22T05:04:15.816478+00:00", System.Globalization.CultureInfo.InvariantCulture);

    public static OfficialVeoExecutionAuthorizationResult Evaluate(OfficialVeoExecutionAuthorizationRequest request)
    {
        var next = EvaluateCore(request);
        if (request.Existing is { Authorized: true } existing)
        {
            if (next.Authorized
                && string.Equals(existing.AuthorizationHash, next.AuthorizationHash, StringComparison.Ordinal))
            {
                return existing with { Reused = true };
            }

            return new OfficialVeoExecutionAuthorizationResult(
                false,
                ProductionFailureReasons.ExecutionAuthorizationRequired,
                next.BlockReason ?? ProductionFailureReasons.ExecutionAuthorizationRequired,
                next.AuthorizationHash,
                false,
                next.Gates,
                next.Resolver,
                next.HttpComposition,
                next.Authenticator,
                next.PaidNetwork,
                null);
        }

        return next;
    }

    public static bool IsBoundTo(RealVertexExecutionPreflightRequest request) =>
        OfficialVeoProductionAuthenticatorV1.IsBoundTo(request)
        && request.Frozen is not null
        && string.Equals(request.Frozen.ProviderId, RealVertexExecutionV1Lock.ProviderId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(request.Frozen.ModelId, RealVertexExecutionV1Lock.ModelId, StringComparison.OrdinalIgnoreCase);

    static OfficialVeoExecutionAuthorizationResult EvaluateCore(OfficialVeoExecutionAuthorizationRequest request)
    {
        var gates = new List<OfficialVeoGateResult>(10);
        var g1 = GateTarget(request);
        gates.Add(g1);
        var g2 = GateSelection(request);
        gates.Add(g2);
        var g3 = GateFrozen(request);
        gates.Add(g3);
        var g4 = GateCost(request);
        gates.Add(g4);
        var g5 = GatePermit(request);
        gates.Add(g5);
        var paid = request.PaidNetwork ?? OfficialVeoPaidNetworkAuthorizationV1.Evaluate(
            new OfficialVeoPaidNetworkAuthorizationRequest(
                request.Scope,
                request.Action,
                request.Selection,
                request.Frozen,
                request.Live,
                request.Permit,
                request.Cost,
                request.Variant));
        gates.Add(new OfficialVeoGateResult(
            "PAID_NETWORK",
            paid.Authorized,
            paid.State,
            paid.Authorized ? null : paid.BlockReason));
        var auth = request.Authenticator ?? OfficialVeoProductionAuthenticatorV1.Compose(
            new OfficialVeoAuthenticatorCompositionRequest(request.Scope, request.Selection));
        var authClosed = true;
        if (auth.Authenticator is ProductionVeoHttpAuthenticator production)
            authClosed = production.GetAccessTokenCalls == 0;
        gates.Add(new OfficialVeoGateResult(
            "AUTHENTICATOR",
            auth.Ready && authClosed,
            auth.Ready ? ProductionFailureReasons.AuthenticatorReady : ProductionFailureReasons.AuthenticatorNotReady,
            auth.Ready
                ? (authClosed ? null : ProductionFailureReasons.VertexExecutionClosed)
                : auth.BlockReason));
        var http = request.HttpComposition ?? OfficialVeoHttpCompositionV1.Compose(
            new OfficialVeoHttpCompositionRequest(
                request.Scope,
                request.Selection,
                request.Frozen,
                request.Live,
                request.KeyframeBytes,
                request.KeyframeMime,
                request.Cost,
                request.Variant,
                request.ProjectId));
        var durationOk = http.Ready
            && request.Frozen is { DurationSec: OfficialCreateVideoFreezeV1.OfficialDurationSec }
            && http.TransportRequest is { OsDurationSec: OfficialCreateVideoFreezeV1.OfficialDurationSec, ProviderDurationSec: 8 };
        gates.Add(new OfficialVeoGateResult(
            "HTTP_COMPOSITION",
            http.Ready && durationOk,
            http.Ready ? ProductionFailureReasons.HttpCompositionReady : ProductionFailureReasons.HttpCompositionUnavailable,
            http.Ready
                ? (durationOk ? null : ProductionFailureReasons.InputSnapshotMismatch)
                : http.BlockReason));
        var resolver = request.Resolver ?? OfficialVeoProviderResolverV1.Compose(
            new OfficialVeoProviderResolverRequest(request.Scope, request.Selection, http.Ready ? http : null));
        var resolverOk = resolver.Ready
            && resolver.Adapter is VeoMotionExecutionAdapter
            && resolver.Transport is HttpVeoMotionTransport
            && string.Equals(resolver.ProviderId, RealVertexExecutionV1Lock.ProviderId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(resolver.ModelId, RealVertexExecutionV1Lock.ModelId, StringComparison.OrdinalIgnoreCase)
            && (resolver.Transport?.HttpCallCount ?? 0) == 0;
        gates.Add(new OfficialVeoGateResult(
            "PROVIDER_RESOLVER",
            resolverOk,
            resolverOk ? ProductionFailureReasons.ProviderResolverReady : ProductionFailureReasons.UnknownProvider,
            resolverOk ? null : resolver.BlockReason ?? ProductionFailureReasons.UnknownProvider));

        var allPrior = gates.All(g => g.Pass);
        gates.Add(new OfficialVeoGateResult(
            "EXECUTION_AUTHORIZATION",
            allPrior,
            allPrior
                ? ProductionFailureReasons.ExecutionAuthorized
                : ProductionFailureReasons.ExecutionAuthorizationRequired,
            allPrior ? null : FirstBlock(gates)));

        var hash = ComputeAuthorizationHash(request, paid, auth, http);
        if (!allPrior)
        {
            return new OfficialVeoExecutionAuthorizationResult(
                false,
                ProductionFailureReasons.ExecutionAuthorizationRequired,
                FirstBlock(gates),
                hash,
                false,
                gates,
                resolver,
                http,
                auth,
                paid,
                null);
        }

        var frozen = request.Frozen!;
        return new OfficialVeoExecutionAuthorizationResult(
            true,
            ProductionFailureReasons.ExecutionAuthorized,
            null,
            hash,
            false,
            gates,
            resolver,
            http,
            auth,
            paid,
            new RealVertexExecutionAuthorization(
                RealVertexExecutionV1Lock.ProviderId,
                RealVertexExecutionV1Lock.ModelId,
                RealVertexExecutionV1Lock.BuildId,
                RealVertexExecutionV1Lock.ShotCode,
                frozen.SnapshotId,
                frozen.SnapshotHash,
                frozen.PerformanceSnapshotId,
                frozen.PerformanceHash,
                frozen.KeyframeArtifactId,
                frozen.KeyframeHash,
                request.Cost ?? CostPolicyDecision.FromCatalog(
                    RealVertexExecutionV1Lock.ProviderId,
                    RealVertexExecutionV1Lock.ModelId,
                    frozen.DurationSec),
                request.Permit?.PermitId,
                request.Permit?.PermitHash,
                AuthorizePaidNetwork: false,
                DateTimeOffset.UnixEpoch));
    }

    static OfficialVeoGateResult GateTarget(OfficialVeoExecutionAuthorizationRequest request)
    {
        if (request.Scope.BuildId != RealVertexExecutionV1Lock.BuildId
            || !string.Equals(request.Scope.ShotCode, RealVertexExecutionV1Lock.ShotCode, StringComparison.Ordinal)
            || !string.Equals((request.Action ?? "").Trim(), ProductionActions.CreateVideo, StringComparison.Ordinal))
        {
            return new("OFFICIAL_TARGET", false, ProductionFailureReasons.UnauthorizedVertexTarget,
                ProductionFailureReasons.UnauthorizedVertexTarget);
        }

        return new("OFFICIAL_TARGET", true, ProductionFailureReasons.ExecutionAuthorized, null);
    }

    static OfficialVeoGateResult GateSelection(OfficialVeoExecutionAuthorizationRequest request)
    {
        try
        {
            OfficialVeoProductionAuthenticatorV1.RequireOfficialTarget(
                new OfficialVeoAuthenticatorCompositionRequest(request.Scope, request.Selection));
            return new("ACTIVE_VIDEO_SELECTION", true, ProductionAiSelectionStatuses.Active, null);
        }
        catch (InvalidOperationException ex)
        {
            return new("ACTIVE_VIDEO_SELECTION", false, CodeOf(ex.Message), CodeOf(ex.Message));
        }
    }

    static OfficialVeoGateResult GateFrozen(OfficialVeoExecutionAuthorizationRequest request)
    {
        var frozen = request.Frozen;
        if (frozen is null)
            return new("FROZEN_INPUT", false, ProductionFailureReasons.InputNotFrozen, ProductionFailureReasons.InputNotFrozen);
        if (request.Live is null)
            return new("FROZEN_INPUT", false, ProductionFailureReasons.InputSnapshotMismatch, ProductionFailureReasons.InputSnapshotMismatch);
        if (request.KeyframeBytes is null || request.KeyframeBytes.Length < 32)
            return new("FROZEN_INPUT", false, ProductionFailureReasons.KeyframeBytesMissing, ProductionFailureReasons.KeyframeBytesMissing);
        if (!string.Equals(frozen.KeyframeHash, KitVideoIntegrityRules.Sha256Hex(request.KeyframeBytes), StringComparison.OrdinalIgnoreCase)
            || frozen.KeyframeArtifactId != OfficialCreateVideoFreezeV1.OfficialKeyframeId)
            return new("FROZEN_INPUT", false, ProductionFailureReasons.InputSnapshotMismatch, ProductionFailureReasons.InputSnapshotMismatch);

        var adapter = new VeoMotionExecutionAdapter(new FakeVeoMotionTransport());
        var mismatch = ProductionFrozenCreateVideoMatch.FirstMismatch(frozen, request.Live, adapter);
        if (mismatch is not null)
            return new("FROZEN_INPUT", false, ProductionFailureReasons.InputSnapshotMismatch, CodeOf(mismatch));
        return new("FROZEN_INPUT", true, "PASS", null);
    }

    static OfficialVeoGateResult GateCost(OfficialVeoExecutionAuthorizationRequest request)
    {
        try
        {
            var variant = request.Variant ?? FamixaVeo31OfficialPricing.ProductionDefault;
            var quote = FamixaProviderCostCatalog.Quote(
                RealVertexExecutionV1Lock.ProviderId,
                RealVertexExecutionV1Lock.ModelId,
                null,
                variant);
            if (quote.CostKind == FamixaCostKind.Unknown || quote.EstimatedAmount is null)
                return new("COST", false, ProductionFailureReasons.UnknownCost, ProductionFailureReasons.UnknownCost);
            var decision = request.Cost ?? CostPolicyDecision.FromQuote(quote);
            if (decision.Kind == CostPolicyKind.Unknown)
                return new("COST", false, ProductionFailureReasons.UnknownCost, ProductionFailureReasons.UnknownCost);
            if (decision.Kind == CostPolicyKind.Forbidden)
                return new("COST", false, ProductionFailureReasons.OverBudget, decision.BlockReason ?? ProductionFailureReasons.OverBudget);
            if (decision.Kind != CostPolicyKind.Allowed
                || string.Equals(decision.BillingBasis, "EXPLICIT_TEST_POLICY", StringComparison.Ordinal)
                || quote.EstimatedAmount != FamixaVeo31OfficialPricing.VideoOnly720Or1080Usd
                || variant.GenerateAudio
                || !string.Equals(variant.GenerationMode, FamixaVeo31OfficialPricing.VideoOnly, StringComparison.Ordinal)
                || FamixaVeo31OfficialPricing.NormalizeResolution(variant.Resolution) != FamixaVeo31OfficialPricing.Res720p
                || variant.SampleCount != 1)
                return new("COST", false, ProductionFailureReasons.UnknownCost, ProductionFailureReasons.UnknownCost);
            return new("COST", true, CostPolicyKind.Allowed.ToString().ToUpperInvariant(), null);
        }
        catch (InvalidOperationException ex)
        {
            return new("COST", false, CodeOf(ex.Message), CodeOf(ex.Message));
        }
    }

    static OfficialVeoGateResult GatePermit(OfficialVeoExecutionAuthorizationRequest request)
    {
        var block = ProductionOfficialPermitGate.BlockReason(
            request.Permit,
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode,
            ProductionActions.CreateVideo,
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId);
        if (block is not null)
            return new("OFFICIAL_PERMIT", false, block, block);
        if (request.Permit is null || request.Selection is null || request.Frozen is null)
            return new("OFFICIAL_PERMIT", false, PerformanceContractIds.OfficialFilmForbidden, PerformanceContractIds.OfficialFilmForbidden);
        if (request.Permit.ConsumedAt is not null)
            return new("OFFICIAL_PERMIT", false, PerformanceContractIds.OfficialFilmForbidden, PerformanceContractIds.OfficialFilmForbidden);
        var quote = FamixaProviderCostCatalog.Quote(
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId,
            null,
            request.Variant ?? FamixaVeo31OfficialPricing.ProductionDefault);
        var expected = OfficialCreateVideoPermitAuthorizationV1.BindingReason(
            request.Selection, request.Frozen, quote);
        if (!string.Equals(request.Permit.Reason, expected, StringComparison.Ordinal))
            return new("OFFICIAL_PERMIT", false, ProductionFailureReasons.PermitConflict, ProductionFailureReasons.PermitConflict);
        return new("OFFICIAL_PERMIT", true, ProductionExecutionPermitStatuses.Issued, null);
    }

    public static string ComputeAuthorizationHash(
        OfficialVeoExecutionAuthorizationRequest request,
        OfficialVeoPaidNetworkAuthorizationResult paid,
        OfficialVeoAuthenticatorCompositionResult auth,
        OfficialVeoHttpCompositionResult http)
    {
        var variant = request.Variant ?? FamixaVeo31OfficialPricing.ProductionDefault;
        var canonical = string.Join('|',
            RealVertexExecutionV1Lock.BuildId.ToString("D"),
            RealVertexExecutionV1Lock.ShotCode,
            ProductionActions.CreateVideo,
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId,
            (request.Selection?.SelectionId ?? Guid.Empty).ToString("D"),
            (request.Frozen?.SnapshotId ?? Guid.Empty).ToString("D"),
            request.Frozen?.SnapshotHash ?? "",
            request.Permit?.PermitHash ?? "",
            request.Permit?.Reason ?? "",
            variant.GenerationMode,
            variant.Resolution,
            variant.GenerateAudio ? "true" : "false",
            variant.SampleCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            paid.State,
            paid.BindingIdentity ?? "",
            auth.State,
            http.State,
            http.StartUrl ?? "",
            http.PollUrl ?? "",
            VeoVertexEndpoints.VertexModel);
        return "AUTH_HASH_V1:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    static string? FirstBlock(IEnumerable<OfficialVeoGateResult> gates) =>
        gates.FirstOrDefault(g => !g.Pass)?.BlockReason
        ?? ProductionFailureReasons.ExecutionAuthorizationRequired;

    static string CodeOf(string message)
    {
        var raw = (message ?? "").Trim();
        var sep = raw.IndexOf(" · ", StringComparison.Ordinal);
        return sep < 0 ? raw : raw[..sep];
    }
}
