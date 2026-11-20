using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Checkpoint 12 runner. Preflight first. Catalog cost only — never invents a quote.
/// If Allowed=false: no OAuth, no Vertex, no permit consume, no Start.
/// </summary>
public static class RealVertexExecutionV1Runner
{
    public static RealVertexExecutionPreflightRequest ArmOfficialTarget(
        ProductionAiSelection? selection,
        ProductionTaskInputSnapshot? frozen = null,
        ShotExecutionInput? live = null,
        byte[]? keyframeBytes = null,
        ProductionExecutionPermit? permit = null,
        IProductionMotionAdapterResolver? resolver = null,
        IMotionExecutionAdapter? adapter = null,
        IVeoHttpAuthenticator? authenticator = null,
        bool httpCompositionAvailable = true)
    {
        var veo = adapter ?? new VeoMotionExecutionAdapter(new FakeVeoMotionTransport());
        resolver ??= new ProductionMotionAdapterResolver(
        [
            new FixtureMotionAdapter(),
            new RunwayMotionExecutionAdapter(new FakeRunwayTransport()),
            veo,
        ]);
        return new RealVertexExecutionPreflightRequest(
            RealVertexExecutionV1Lock.OfficialScope,
            selection,
            ResolverEnabled: true,
            Resolver: resolver,
            Frozen: frozen,
            LiveClaim: live,
            Adapter: veo,
            KeyframeBytes: keyframeBytes,
            Cost: CostPolicyDecision.FromCatalog(
                RealVertexExecutionV1Lock.ProviderId,
                RealVertexExecutionV1Lock.ModelId,
                frozen?.DurationSec > 0 ? frozen.DurationSec : 10),
            Permit: permit,
            AuthorizePaidNetwork: true,
            Authenticator: authenticator ?? new ProductionVeoHttpAuthenticator(),
            HttpCompositionAvailable: httpCompositionAvailable,
            ExecutionAuthorizationEnabled: true,
            ExpectedOutputMode: RealVertexOutputMode.InlineBytes,
            ReplayConflict: false);
    }

    public static RealVertexExecutionV1Result Attempt(
        RealVertexExecutionPreflightRequest request,
        CountingVeoMotionTransport? transport = null,
        CountingProductionExecutionPermitStore? permits = null)
    {
        var provider = request.ActiveVideoSelection?.ProviderId ?? request.Frozen?.ProviderId;
        var model = request.ActiveVideoSelection?.ModelId ?? request.Frozen?.ModelId;
        if (!RealVertexExecutionV1Lock.IsAuthorizedTarget(request.Scope, provider, model))
        {
            var blockedTarget = RealVertexExecutionPreflight.Evaluate(request);
            return Stop(blockedTarget, transport, permits, ProductionFailureReasons.UnauthorizedVertexTarget);
        }

        if (string.Equals(request.Cost.BillingBasis, "EXPLICIT_TEST_POLICY", StringComparison.Ordinal))
        {
            request = request with
            {
                Cost = CostPolicyDecision.FromCatalog(
                    RealVertexExecutionV1Lock.ProviderId,
                    RealVertexExecutionV1Lock.ModelId,
                    request.Frozen?.DurationSec > 0 ? request.Frozen.DurationSec : 10),
            };
        }

        var preflight = RealVertexExecutionPreflight.Evaluate(request);
        if (!preflight.Allowed)
            return Stop(preflight, transport, permits, preflight.Blockers.FirstOrDefault());

        var catalog = CostPolicyDecision.FromCatalog(
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId,
            request.Frozen?.DurationSec > 0 ? request.Frozen.DurationSec : 10);
        if (catalog.Kind != CostPolicyKind.Allowed)
        {
            return Stop(
                new RealVertexExecutionPreflightResult(
                    false,
                    Merge(preflight.Blockers, ProductionFailureReasons.UnknownCost),
                    null),
                transport,
                permits,
                ProductionFailureReasons.UnknownCost);
        }

        RealVertexExecutionV1Lock.RecordStart();
        throw new InvalidOperationException(ProductionFailureReasons.StartOutcomeUncertain);
    }

    static RealVertexExecutionV1Result Stop(
        RealVertexExecutionPreflightResult preflight,
        CountingVeoMotionTransport? transport,
        CountingProductionExecutionPermitStore? permits,
        string? extra)
    {
        var blockers = extra is null ? preflight.Blockers : Merge(preflight.Blockers, extra);
        return new RealVertexExecutionV1Result(
            false,
            new RealVertexExecutionPreflightResult(false, blockers, null),
            RealVertexExecutionV1Lock.StartCount,
            transport?.HttpCallCount ?? 0,
            permits?.ConsumeCalls ?? 0,
            extra ?? preflight.Blockers.FirstOrDefault());
    }

    static IReadOnlyList<string> Merge(IReadOnlyList<string> existing, string extra)
    {
        var list = existing.ToList();
        if (!list.Contains(extra, StringComparer.Ordinal))
            list.Add(extra);
        return list;
    }
}
