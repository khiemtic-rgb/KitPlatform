using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionOfficialVeoExecutionAuthorizationV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_OFFICIAL_VEO_EXECUTION_AUTHORIZATION_V1";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }
        try { RunAsync(Ok).GetAwaiter().GetResult(); }
        catch (Exception ex) { fail.Add("UNCAUGHT:" + ex.GetType().Name + ":" + ex.Message); }
        return fail;
    }

    static async Task RunAsync(Action<bool, string> Ok)
    {
        await T_OfficialAuthorized(Ok);
        await T_IdempotentReuse(Ok);
        await T_TenGatesSameContract(Ok);
        await T_ResolverAuthoritativeOnly(Ok);
        await T_WrongBuild(Ok);
        await T_WrongShot(Ok);
        await T_WrongAction(Ok);
        await T_WrongProvider(Ok);
        await T_WrongModel(Ok);
        await T_WrongSelection(Ok);
        await T_MissingSnapshot(Ok);
        await T_SnapshotMismatch(Ok);
        await T_DurationMismatch(Ok);
        await T_TimingMismatch(Ok);
        await T_PerformanceSnapshotMismatch(Ok);
        await T_PerformanceHashMismatch(Ok);
        await T_KeyframeArtifactMismatch(Ok);
        await T_KeyframeHashMismatch(Ok);
        await T_UnknownCost(Ok);
        await T_ForbiddenCost(Ok);
        await T_MissingPermit(Ok);
        await T_ConsumedPermit(Ok);
        await T_PermitBindingMismatch(Ok);
        await T_PaidNetworkForbidden(Ok);
        await T_AuthenticatorNotReady(Ok);
        await T_HttpCompositionUnavailable(Ok);
        await T_UnknownProvider(Ok);
        await T_FixtureRunwayUnselected(Ok);
        await T_MismatchedAuthorizationIdentity(Ok);
        await T_ReplayAfterChange(Ok);
        await T_PartialAuthorizationRejected(Ok);
        await T_NoStartNoMutation(Ok);
        await T_PreflightOfficialAllowedClosedDefaultsStayClosed(Ok);
        T_Ck16StillGreen(Ok);
    }

    static async Task T_OfficialAuthorized(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var issued = ready.Permits.IssueCalls;
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request);
        Ok(r.Authorized && r.State == ProductionFailureReasons.ExecutionAuthorized,
            "exact official target EXECUTION_AUTHORIZED");
        Ok(r.Authorization is not null
            && r.Authorization.BuildId == RealVertexExecutionV1Lock.BuildId
            && r.Authorization.ShotCode == RealVertexExecutionV1Lock.ShotCode
            && r.Authorization.ProviderId == "google"
            && r.Authorization.ModelId == "veo-3.1"
            && r.Authorization.SnapshotId == ready.Frozen.SnapshotId
            && r.Authorization.SnapshotHash == ready.Frozen.SnapshotHash
            && r.Authorization.AuthorizePaidNetwork == false,
            "authorization identity bound; no global paid-network flag");
        Ok(r.AuthorizationHash.StartsWith("AUTH_HASH_V1:", StringComparison.Ordinal), "canonical AUTH_HASH_V1");
        Ok(ready.Frozen.DurationSec == 10
            && r.HttpComposition!.TransportRequest!.OsDurationSec == 10
            && r.HttpComposition.TransportRequest.ProviderDurationSec == 8,
            "frozen OS 10 / provider wire 8");
        Ok(ready.Permits.IssueCalls == issued && ready.Permits.ConsumeCalls == 0
            && ready.Permit.Status == ProductionExecutionPermitStatuses.Issued
            && ready.Permit.ConsumedAt is null,
            "CK13B permit reused ISSUED / unconsumed");
        Ok(!OfficialVeoExecutionAuthorizationV1.AuthorizeStartsVertex
            && RealVertexExecutionV1Lock.StartCount == 0,
            "Authorize does not Start");
    }

    static async Task T_IdempotentReuse(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var first = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request);
        var second = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with { Existing = first });
        var count = await OfficialCreateVideoFreezeV1.CountOfficialCreateVideoSnapshotsAsync(ready.Store);
        Ok(first.Authorized && second.Authorized && second.Reused
            && second.AuthorizationHash == first.AuthorizationHash,
            "identical evaluation reuses authorization");
        Ok(count == 1 && ready.Frozen.SnapshotId == second.Authorization!.SnapshotId,
            "no second snapshot");
        Ok(ready.Permits.IssueCalls == 1 && ready.Permits.ConsumeCalls == 0, "no second permit / no consume");
    }

    static async Task T_TenGatesSameContract(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request);
        Ok(r.Gates.Count == 10 && r.Gates.All(g => g.Pass), "all 10 gates PASS");
        Ok(r.Gates.Select(g => g.Gate).SequenceEqual(
            [
                "OFFICIAL_TARGET", "ACTIVE_VIDEO_SELECTION", "FROZEN_INPUT", "COST", "OFFICIAL_PERMIT",
                "PAID_NETWORK", "AUTHENTICATOR", "HTTP_COMPOSITION", "PROVIDER_RESOLVER", "EXECUTION_AUTHORIZATION",
            ]), "gate order");
        Ok(r.PaidNetwork is { Authorized: true, State: ProductionFailureReasons.PaidNetworkAuthorized }, "CK14 paid network");
        Ok(r.Authenticator is { Ready: true, State: ProductionFailureReasons.AuthenticatorReady }
            && r.Authenticator.Authenticator is ProductionVeoHttpAuthenticator, "CK15 authenticator");
        Ok(r.HttpComposition is { Ready: true, State: ProductionFailureReasons.HttpCompositionReady }
            && r.HttpComposition.StartUrl!.Contains(":predictLongRunning", StringComparison.Ordinal)
            && r.HttpComposition.PollUrl!.Contains(":fetchPredictOperation", StringComparison.Ordinal)
            && r.HttpComposition.Settings!.VertexModel == "veo-3.1-generate-001",
            "CK16 HTTP composition");
        Ok(r.Resolver is { Ready: true, State: ProductionFailureReasons.ProviderResolverReady }
            && r.Resolver.Adapter is VeoMotionExecutionAdapter
            && r.Resolver.Transport is HttpVeoMotionTransport, "official resolver Veo+Http");
        var quote = FamixaProviderCostCatalog.Quote("google", "veo-3.1", null, FamixaVeo31OfficialPricing.ProductionDefault);
        Ok(quote.EstimatedAmount == 0.20m && quote.Currency == "USD" && quote.CostKind == FamixaCostKind.Known,
            "CK13A cost USD 0.20 COUNT");
        ProductionFrozenCreateVideoMatch.Assert(
            ready.Frozen, ready.Live, new VeoMotionExecutionAdapter(new FakeVeoMotionTransport()));
        Ok(ready.Frozen.ProviderId == "google" && ready.Frozen.ModelId == "veo-3.1"
            && ready.Frozen.DurationSec == 10
            && ready.Frozen.PerformanceSnapshotId == OfficialCreateVideoFreezeV1.OfficialPerformanceId
            && ready.Frozen.PerformanceHash == OfficialCreateVideoFreezeV1.OfficialPerformanceHash
            && ready.Frozen.KeyframeArtifactId == OfficialCreateVideoFreezeV1.OfficialKeyframeId,
            "CK12A frozen contract unchanged");
    }

    static async Task T_ResolverAuthoritativeOnly(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var composed = OfficialVeoProviderResolverV1.Compose(
            new OfficialVeoProviderResolverRequest(RealVertexExecutionV1Lock.OfficialScope, OfficialSelection()));
        Ok(composed.Ready && composed.Resolver is OfficialVeoScopedResolver, "official scoped resolver");
        var resolved = composed.Resolver!.Resolve("google", "veo-3.1");
        Ok(resolved is VeoMotionExecutionAdapter && ReferenceEquals(resolved, composed.Adapter),
            "google/veo-3.1 → VeoMotionExecutionAdapter");
        Ok(RejectsResolve(composed.Resolver, "fixture", "fixture-motion-v1", ProductionFailureReasons.ProviderModelMismatch),
            "Fixture cannot satisfy official resolver");
        Ok(RejectsResolve(composed.Resolver, "runway", "gen4_turbo", ProductionFailureReasons.ProviderModelMismatch),
            "Runway cannot satisfy official resolver");
        var production = ProductionVeoReadinessComposition.CreateProductionResolver();
        Ok(RejectsResolve(production, "google", "veo-3.1", ProductionFailureReasons.UnknownProvider),
            "production resolver still UNKNOWN_PROVIDER for google/veo-3.1");
        Ok(!OfficialVeoProviderResolverV1.ProductionDiRegistered
            && !ProductionVeoReadinessComposition.ProductionResolverIncludesVeo,
            "no global Veo DI registration");
        Ok(composed.Transport!.HttpCallCount == 0 && composed.Handler!.Requests.Count == 0
            && composed.Authenticator!.Calls == 0,
            "resolver compose does not Start");
        _ = ready;
    }

    static async Task T_WrongBuild(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with
        {
            Scope = new ProductionScope(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "EP01", "SC01", "EP01-SC01-SH02"),
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.UnauthorizedVertexTarget, "1 wrong build");
    }

    static async Task T_WrongShot(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with
        {
            Scope = new ProductionScope(RealVertexExecutionV1Lock.BuildId, "EP01", "SC01", "EP01-SC01-SH01"),
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.UnauthorizedVertexTarget, "2 wrong shot");
    }

    static async Task T_WrongAction(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with { Action = ProductionActions.CreateImage });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.UnauthorizedVertexTarget, "3 wrong action");
    }

    static async Task T_WrongProvider(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with
        {
            Selection = OfficialSelection("runway", "veo-3.1"),
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.ProviderModelMismatch, "4 wrong provider");
    }

    static async Task T_WrongModel(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with
        {
            Selection = OfficialSelection("google", "veo-2"),
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.ProviderModelMismatch, "5 wrong model");
    }

    static async Task T_WrongSelection(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with
        {
            Selection = OfficialSelection() with { SelectionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") },
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.SelectionConflict, "6 wrong selection");
    }

    static async Task T_MissingSnapshot(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with { Frozen = null });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.InputNotFrozen, "7 missing snapshot");
    }

    static async Task T_SnapshotMismatch(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with
        {
            Live = ready.Live with { Story = ready.Live.Story + " changed" },
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.InputSnapshotMismatch, "8 snapshot mismatch");
    }

    static async Task T_DurationMismatch(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with
        {
            Live = ready.Live with { DurationSec = 8 },
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.InputSnapshotMismatch, "9 duration mismatch");
        Ok(ready.Frozen.DurationSec == 10, "frozen DurationSec stays 10");
    }

    static async Task T_TimingMismatch(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with
        {
            Live = ready.Live with { Timing = "8.0s" },
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.InputSnapshotMismatch, "10 timing mismatch");
    }

    static async Task T_PerformanceSnapshotMismatch(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with
        {
            Live = ready.Live with { PerformanceSnapshotId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc") },
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.InputSnapshotMismatch, "11 performance snapshot mismatch");
    }

    static async Task T_PerformanceHashMismatch(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with
        {
            Live = ready.Live with { PerformanceHash = "PERFORMANCE_HASH_V1:deadbeef" },
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.InputSnapshotMismatch, "12 performance hash mismatch");
    }

    static async Task T_KeyframeArtifactMismatch(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with
        {
            Live = ready.Live with { KeyframeArtifactId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd") },
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.InputSnapshotMismatch, "13 keyframe artifact mismatch");
    }

    static async Task T_KeyframeHashMismatch(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var other = Encoding.UTF8.GetBytes("FAMIXA-KF-17-OTHER-" + new string('X', 48));
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with { KeyframeBytes = other });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.InputSnapshotMismatch, "14 keyframe hash mismatch");
    }

    static async Task T_UnknownCost(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with
        {
            Cost = CostPolicyDecision.ExplicitUnknown(),
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.UnknownCost, "15 unknown cost");
    }

    static async Task T_ForbiddenCost(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with
        {
            Cost = CostPolicyDecision.ExplicitForbidden(ProductionFailureReasons.OverBudget),
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.OverBudget, "16 forbidden cost");
    }

    static async Task T_MissingPermit(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with { Permit = null });
        Ok(!r.Authorized && r.BlockReason == PerformanceContractIds.OfficialFilmForbidden, "17 missing permit");
    }

    static async Task T_ConsumedPermit(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var consumed = ready.Permit with
        {
            Status = ProductionExecutionPermitStatuses.Consumed,
            ConsumedAt = DateTimeOffset.UnixEpoch,
        };
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with { Permit = consumed });
        Ok(!r.Authorized && r.BlockReason == PerformanceContractIds.OfficialFilmForbidden, "18 consumed permit");
        Ok(ready.Permit.ConsumedAt is null && ready.Permits.ConsumeCalls == 0, "store permit not consumed");
    }

    static async Task T_PermitBindingMismatch(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with
        {
            Permit = ready.Permit with { Reason = "OTHER_BINDING" },
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.PermitConflict, "19 permit binding mismatch");
    }

    static async Task T_PaidNetworkForbidden(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with
        {
            PaidNetwork = new OfficialVeoPaidNetworkAuthorizationResult(
                false, ProductionFailureReasons.PaidNetworkForbidden, ProductionFailureReasons.PaidNetworkForbidden, null),
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.PaidNetworkForbidden, "20 paid-network forbidden");
        Ok(!new ProductionPaidNetworkOneShotPermit().IsEligible(
            RealVertexExecutionV1Lock.OfficialScope, RealVertexExecutionV1Lock.ShotCode, "google", "veo-3.1"),
            "OneShotPermit does not authorize official Veo");
    }

    static async Task T_AuthenticatorNotReady(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with
        {
            Authenticator = new OfficialVeoAuthenticatorCompositionResult(
                false, ProductionFailureReasons.AuthenticatorNotReady, ProductionFailureReasons.AuthenticatorNotReady,
                new ProductionVeoHttpAuthenticator(), null),
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.AuthenticatorNotReady, "21 authenticator not ready");
    }

    static async Task T_HttpCompositionUnavailable(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with
        {
            HttpComposition = new OfficialVeoHttpCompositionResult(
                false, ProductionFailureReasons.HttpCompositionUnavailable, ProductionFailureReasons.HttpCompositionUnavailable,
                null, null, null, null, null),
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.HttpCompositionUnavailable, "22 HTTP composition unavailable");
    }

    static async Task T_UnknownProvider(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with
        {
            Resolver = new OfficialVeoProviderResolverResult(
                false, ProductionFailureReasons.UnknownProvider, ProductionFailureReasons.UnknownProvider,
                null, null, null, null, null, "google", "veo-3.1"),
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.UnknownProvider, "23 unknown provider");
    }

    static async Task T_FixtureRunwayUnselected(Action<bool, string> Ok)
    {
        var ready = await Ready();
        Ok(!OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with
        {
            Selection = OfficialSelection("fixture", "fixture-motion-v1"),
        }).Authorized, "24 Fixture provider");
        Ok(!OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with
        {
            Selection = OfficialSelection("runway", "gen4_turbo"),
        }).Authorized, "25 Runway provider");
        Ok(!OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with { Selection = null }).Authorized
            && OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with { Selection = null }).BlockReason
                == ProductionFailureReasons.MissingProductionAiSelection,
            "26 unselected provider/model");
    }

    static async Task T_MismatchedAuthorizationIdentity(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var first = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request);
        var changed = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with
        {
            Existing = first,
            Selection = OfficialSelection() with { SelectionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") },
        });
        Ok(first.Authorized && !changed.Authorized && !changed.Reused
            && changed.State == ProductionFailureReasons.ExecutionAuthorizationRequired,
            "27 mismatched authorization identity fails closed");
    }

    static async Task T_ReplayAfterChange(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var first = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request);
        var cases = new (string Name, OfficialVeoExecutionAuthorizationRequest Req)[]
        {
            ("build", ready.Request with { Existing = first, Scope = new ProductionScope(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "EP01", "SC01", "EP01-SC01-SH02") }),
            ("shot", ready.Request with { Existing = first, Scope = new ProductionScope(RealVertexExecutionV1Lock.BuildId, "EP01", "SC01", "EP01-SC01-SH01") }),
            ("provider", ready.Request with { Existing = first, Selection = OfficialSelection("runway", "veo-3.1") }),
            ("model", ready.Request with { Existing = first, Selection = OfficialSelection("google", "veo-2") }),
            ("selectionId", ready.Request with { Existing = first, Selection = OfficialSelection() with { SelectionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") } }),
            ("story/hash", ready.Request with { Existing = first, Live = ready.Live with { Story = ready.Live.Story + " x" } }),
            ("duration", ready.Request with { Existing = first, Live = ready.Live with { DurationSec = 8 } }),
            ("timing", ready.Request with { Existing = first, Live = ready.Live with { Timing = "8.0s" } }),
            ("performance", ready.Request with { Existing = first, Live = ready.Live with { PerformanceSnapshotId = Guid.NewGuid() } }),
            ("performanceHash", ready.Request with { Existing = first, Live = ready.Live with { PerformanceHash = "PERFORMANCE_HASH_V1:ff" } }),
            ("keyframe", ready.Request with { Existing = first, Live = ready.Live with { KeyframeArtifactId = Guid.NewGuid() } }),
            ("keyframeHash", ready.Request with { Existing = first, KeyframeBytes = Encoding.UTF8.GetBytes("FAMIXA-KF-17-REPLAY-" + new string('Z', 48)) }),
            ("cost", ready.Request with { Existing = first, Variant = FamixaVeo31OfficialPricing.VideoAudio720p() }),
            ("permit", ready.Request with { Existing = first, Permit = ready.Permit with { Reason = "OTHER" } }),
            ("paid-network", ready.Request with { Existing = first, PaidNetwork = new OfficialVeoPaidNetworkAuthorizationResult(false, ProductionFailureReasons.PaidNetworkForbidden, ProductionFailureReasons.PaidNetworkForbidden, null) }),
            ("authenticator", ready.Request with { Existing = first, Authenticator = new OfficialVeoAuthenticatorCompositionResult(false, ProductionFailureReasons.AuthenticatorNotReady, ProductionFailureReasons.AuthenticatorNotReady, new ProductionVeoHttpAuthenticator(), null) }),
            ("http", ready.Request with { Existing = first, HttpComposition = new OfficialVeoHttpCompositionResult(false, ProductionFailureReasons.HttpCompositionUnavailable, ProductionFailureReasons.HttpCompositionUnavailable, null, null, null, null, null) }),
        };
        foreach (var (name, req) in cases)
        {
            var r = OfficialVeoExecutionAuthorizationV1.Evaluate(req);
            Ok(!r.Authorized && !r.Reused, "28 replay after " + name);
        }
        Ok(ready.Frozen.DurationSec == 10
            && await OfficialCreateVideoFreezeV1.CountOfficialCreateVideoSnapshotsAsync(ready.Store) == 1,
            "replay does not refreeze");
    }

    static async Task T_PartialAuthorizationRejected(Action<bool, string> Ok)
    {
        var ready = await Ready();
        Ok(!OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with
        {
            Resolver = new OfficialVeoProviderResolverResult(
                false, ProductionFailureReasons.UnknownProvider, ProductionFailureReasons.UnknownProvider,
                null, null, null, null, null, "google", "veo-3.1"),
        }).Authorized, "cost+permit+resolver FAIL → not authorized");
        Ok(!OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with { Permit = null }).Authorized,
            "resolver+authenticator+permit missing → not authorized");
        Ok(!OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with
        {
            Live = ready.Live with { Story = ready.Live.Story + " drift" },
        }).Authorized, "everything except frozen → not authorized");
        Ok(!OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with
        {
            PaidNetwork = new OfficialVeoPaidNetworkAuthorizationResult(
                false, ProductionFailureReasons.PaidNetworkForbidden, ProductionFailureReasons.PaidNetworkForbidden, null),
        }).Authorized, "everything except paid-network → not authorized");
        Ok(!OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request with
        {
            HttpComposition = new OfficialVeoHttpCompositionResult(
                false, ProductionFailureReasons.HttpCompositionUnavailable, ProductionFailureReasons.HttpCompositionUnavailable,
                null, null, null, null, null),
        }).Authorized, "everything except HTTP → not authorized");
    }

    static async Task T_NoStartNoMutation(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var graph = ProductionVeoReadinessComposition.CreateClosedTestGraph();
        var closed = new ProductionVeoHttpAuthenticator();
        var hash = ready.Frozen.SnapshotHash;
        var issued = ready.Permits.IssueCalls;
        var r = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request);
        var after = await OfficialCreateVideoFreezeV1.FindOfficialCreateVideoSnapshotAsync(ready.Store);
        Ok(r.Authorized, "authorized without Start");
        Ok(after!.SnapshotId == ready.Frozen.SnapshotId && after.SnapshotHash == hash && after.DurationSec == 10,
            "snapshot mutation = 0");
        Ok(ready.Permits.IssueCalls == issued && ready.Permits.ConsumeCalls == 0, "permit issued/consumed = 0");
        Ok(graph.Handler.Requests.Count == 0 && graph.Transport.HttpCallCount == 0
            && graph.Authenticator.Calls == 0 && graph.Transport.StartCount == 0
            && RealVertexExecutionV1Lock.StartCount == 0
            && (r.Resolver!.Transport?.HttpCallCount ?? 1) == 0
            && (r.Resolver.Authenticator?.Calls ?? 1) == 0,
            "provider/network/OAuth/Start = 0");
        Ok(closed.GetAccessTokenCalls == 0
            && r.Authenticator!.Authenticator is ProductionVeoHttpAuthenticator p
            && p.GetAccessTokenCalls == 0,
            "credential reads / GetAccessToken = 0");
        Ok(!ProductionVeoReadinessComposition.RealVertexExecutionOpen, "execution lock unchanged");
    }

    static async Task T_PreflightOfficialAllowedClosedDefaultsStayClosed(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var authz = OfficialVeoExecutionAuthorizationV1.Evaluate(ready.Request);
        var report = CostOfficialPermitAuthorization.Evaluate(
            RealVertexExecutionPreflight.ProductionClosedDefaults(
                RealVertexExecutionV1Lock.OfficialScope, OfficialSelection())
            with
            {
                Frozen = ready.Frozen,
                LiveClaim = ready.Live,
                Adapter = authz.Resolver!.Adapter,
                KeyframeBytes = ready.Bytes,
                Permit = ready.Permit,
                Cost = CostPolicyDecision.FromCatalog("google", "veo-3.1", 10),
                OfficialPaidNetwork = authz.PaidNetwork,
                OfficialAuthenticator = authz.Authenticator,
                OfficialHttpComposition = authz.HttpComposition,
                OfficialResolver = authz.Resolver,
                OfficialExecutionAuthorization = authz,
            });
        Ok(authz.Authorized && report.Preflight.Allowed && report.ExecutionAuthorizationPass
            && report.ResolverPass && report.HttpCompositionPass && report.AuthenticatorPass
            && report.PaidNetworkPass && report.PermitGatePass && report.CostGatePass,
            "official overlays → preflight Allowed");
        Ok(RealVertexExecutionV1Lock.StartCount == 0 && ready.Permits.ConsumeCalls == 0,
            "Allowed is not a Start");
        var closed = RealVertexExecutionPreflight.ProductionClosedDefaults(
            RealVertexExecutionV1Lock.OfficialScope, OfficialSelection());
        Ok(!closed.ResolverEnabled && !closed.ExecutionAuthorizationEnabled && !closed.AuthorizePaidNetwork
            && closed.OfficialResolver is null && closed.OfficialExecutionAuthorization is null
            && !RealVertexExecutionPreflight.Evaluate(closed).Allowed
            && RealVertexExecutionPreflight.Evaluate(closed).Blockers.Contains(ProductionFailureReasons.UnknownProvider)
            && RealVertexExecutionPreflight.Evaluate(closed).Blockers.Contains(ProductionFailureReasons.ExecutionAuthorizationRequired),
            "ClosedDefaults remain closed");
        var other = RealVertexExecutionPreflight.Evaluate(
            closed with
            {
                Scope = new ProductionScope(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "EP01", "SC01", "EP01-SC01-SH02"),
            });
        Ok(!other.Allowed, "unrelated production request remains blocked");
    }

    static void T_Ck16StillGreen(Action<bool, string> Ok)
    {
        var ck16 = ProductionOfficialVeoHttpCompositionV1Regression.Run();
        Ok(ck16.Count == 0, ck16.Count == 0 ? "CK16 still green" : "CK16:" + string.Join(",", ck16));
    }

    static bool RejectsResolve(IProductionMotionAdapterResolver resolver, string provider, string model, string code)
    {
        try
        {
            _ = resolver.Resolve(provider, model);
            return false;
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith(code, StringComparison.Ordinal))
        {
            return true;
        }
    }

    static OfficialVeoExecutionAuthorizationRequest RequestOf(
        ProductionTaskInputSnapshot frozen,
        ShotExecutionInput live,
        byte[] bytes,
        ProductionExecutionPermit permit) =>
        new(
            RealVertexExecutionV1Lock.OfficialScope,
            ProductionActions.CreateVideo,
            OfficialSelection(),
            frozen,
            live,
            bytes,
            "image/png",
            CostPolicyDecision.FromCatalog("google", "veo-3.1", 10),
            FamixaVeo31OfficialPricing.ProductionDefault,
            permit,
            ProjectId: ProductionOfficialVeoHttpCompositionV1Regression.ComposeProject);

    static ProductionAiSelection OfficialSelection(string provider = "google", string model = "veo-3.1") =>
        new(
            OfficialCreateVideoPermitAuthorizationV1.OfficialSelectionId,
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode, "EP01", "SC01",
            ProductionAiSelectionStages.Video,
            provider, model,
            ProductionAiSelectionModes.Explicit,
            "checkpoint-17",
            OfficialVeoExecutionAuthorizationV1.OfficialSelectedAt,
            "DIRECTOR_SELECTION",
            ProductionAiSelectionStatuses.Active,
            "SELECTION_HASH_V1:ck17");

    static ShotExecutionInput Live(OfficialCreateVideoFreezeSources sources) =>
        new(
            sources.Story, sources.ActingBeat, sources.VisualContract, "CHAR-001",
            sources.KeyframeHash, sources.Timing, "", "",
            sources.KeyframeArtifactId, sources.KeyframeHash, "16:9", sources.DurationSec,
            ShotExecutionCompiler.Version, [sources.KeyframeArtifactId],
            PerformanceSnapshotId: sources.PerformanceSnapshotId,
            PerformanceHash: sources.PerformanceHash);

    static async Task<ReadyState> Ready()
    {
        var store = new InMemoryProductionWorkStore();
        var permits = new CountingProductionExecutionPermitStore();
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-KF-17-" + new string('K', 48));
        var hash = KitVideoIntegrityRules.Sha256Hex(bytes);
        var engine = new ProductionWorkExecutionEngine(store);
        var req = await engine.CreateRequestAsync(
            RealVertexExecutionV1Lock.OfficialScope, ProductionActions.CreateImage, "ck17",
            "kf-17-" + Guid.NewGuid().ToString("N"));
        var task = await engine.CreateTaskAsync(req.RequestId);
        await store.InsertArtifactAsync(new ProductionArtifact(
            OfficialCreateVideoFreezeV1.OfficialKeyframeId,
            task.TaskId,
            Guid.NewGuid(),
            ProductionArtifactRoles.Keyframe,
            "image/png",
            bytes.Length,
            0,
            hash,
            "kf/EP01-SC01-SH02.png",
            null,
            DateTimeOffset.UtcNow,
            ProductionArtifactStatuses.Verified,
            hash));
        var sources = OfficialCreateVideoFreezeV1.FromEp01Sh02Canon(bytes, hash);
        var frozen = (await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin())).Snapshot;
        var live = Live(sources);
        var issued = await OfficialCreateVideoPermitAuthorizationV1.IssueAsync(
            permits,
            new OfficialCreateVideoPermitIssueRequest(
                RealVertexExecutionV1Lock.OfficialScope,
                OfficialSelection(),
                frozen,
                live));
        return new ReadyState(
            store, frozen, live, bytes, issued.Permit, permits, RequestOf(frozen, live, bytes, issued.Permit));
    }

    sealed record ReadyState(
        InMemoryProductionWorkStore Store,
        ProductionTaskInputSnapshot Frozen,
        ShotExecutionInput Live,
        byte[] Bytes,
        ProductionExecutionPermit Permit,
        CountingProductionExecutionPermitStore Permits,
        OfficialVeoExecutionAuthorizationRequest Request);
}
