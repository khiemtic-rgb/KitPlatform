using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionOfficialVeoPaidNetworkAuthorizationV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_OFFICIAL_VEO_PAID_NETWORK_AUTHORIZATION_V1";

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
        await T_OfficialTargetAuthorized(Ok);
        await T_Idempotent(Ok);
        await T_WrongBuild(Ok);
        await T_WrongShot(Ok);
        await T_WrongProvider(Ok);
        await T_WrongModel(Ok);
        await T_WrongSelection(Ok);
        await T_MissingFrozen(Ok);
        await T_FrozenMismatch(Ok);
        await T_MissingPermit(Ok);
        await T_ConsumedPermit(Ok);
        await T_PermitBindingMismatch(Ok);
        await T_UnknownCost(Ok);
        await T_ForbiddenCost(Ok);
        await T_DifferentVariant(Ok);
        await T_DifferentResolution(Ok);
        await T_GenerateAudio(Ok);
        await T_DifferentSampleCount(Ok);
        await T_FixtureAndUnselected(Ok);
        await T_NoGlobalBypass(Ok);
        await T_SafetyAndClosedExecution(Ok);
        T_Ck13BStillGreen(Ok);
    }

    static async Task T_OfficialTargetAuthorized(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var first = OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request);
        Ok(first.Authorized && first.State == ProductionFailureReasons.PaidNetworkAuthorized,
            "exact official target AUTHORIZED");
        Ok(first.BindingIdentity is not null
            && first.BindingIdentity.Contains("selectionId=" + OfficialCreateVideoPermitAuthorizationV1.OfficialSelectionId.ToString("D"), StringComparison.Ordinal)
            && first.BindingIdentity.Contains("amount=0.20", StringComparison.Ordinal)
            && first.BindingIdentity.Contains("variant=VIDEO_ONLY", StringComparison.Ordinal)
            && first.BindingIdentity.Contains("resolution=720p", StringComparison.Ordinal)
            && first.BindingIdentity.Contains("audio=false", StringComparison.Ordinal)
            && first.BindingIdentity.Contains("sampleCount=1", StringComparison.Ordinal),
            "binding includes selection + official VIDEO_ONLY 720p USD 0.20");
        Ok(ready.Permit.Status == ProductionExecutionPermitStatuses.Issued
            && ready.Permit.ConsumedAt is null,
            "permit remains ISSUED / unconsumed");
        Ok(ready.Frozen.DurationSec == 10
            && ready.Frozen.ProviderId == "google"
            && ready.Frozen.ModelId == "veo-3.1",
            "frozen input required and unchanged");
    }

    static async Task T_Idempotent(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var a = OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request);
        var b = OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request);
        Ok(a.Authorized && b.Authorized && a.State == b.State && a.BindingIdentity == b.BindingIdentity,
            "repeated authorization is identical");
        Ok(ready.Permits.IssueCalls == 1, "repeat does not issue another permit");
    }

    static async Task T_WrongBuild(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request with
        {
            Scope = new ProductionScope(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "EP01", "SC01", "EP01-SC01-SH02"),
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.UnauthorizedVertexTarget, "wrong build");
    }

    static async Task T_WrongShot(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request with
        {
            Scope = new ProductionScope(RealVertexExecutionV1Lock.BuildId, "EP01", "SC01", "EP01-SC01-SH01"),
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.UnauthorizedVertexTarget, "wrong shot");
    }

    static async Task T_WrongProvider(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request with
        {
            Selection = OfficialSelection("google", "veo-3.1") with { ProviderId = "runway" },
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.ProviderModelMismatch, "wrong provider");
    }

    static async Task T_WrongModel(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request with
        {
            Selection = OfficialSelection() with { ModelId = "veo-2" },
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.ProviderModelMismatch, "wrong model");
    }

    static async Task T_WrongSelection(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request with
        {
            Selection = OfficialSelection() with { SelectionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") },
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.SelectionConflict, "wrong selectionId");
    }

    static async Task T_MissingFrozen(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request with { Frozen = null });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.InputNotFrozen, "missing frozen");
    }

    static async Task T_FrozenMismatch(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request with
        {
            Live = ready.Live with { Story = ready.Live.Story + " changed" },
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.InputSnapshotMismatch, "frozen mismatch");
    }

    static async Task T_MissingPermit(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request with { Permit = null });
        Ok(!r.Authorized && r.BlockReason == PerformanceContractIds.OfficialFilmForbidden, "missing permit");
    }

    static async Task T_ConsumedPermit(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var consumed = ready.Permit with
        {
            Status = ProductionExecutionPermitStatuses.Consumed,
            ConsumedAt = DateTimeOffset.UnixEpoch,
        };
        var r = OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request with { Permit = consumed });
        Ok(!r.Authorized && r.BlockReason == PerformanceContractIds.OfficialFilmForbidden, "consumed permit");
        Ok(ready.Permit.Status == ProductionExecutionPermitStatuses.Issued
            && ready.Permits.ConsumeCalls == 0,
            "original permit not consumed");
    }

    static async Task T_PermitBindingMismatch(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request with
        {
            Permit = ready.Permit with { Reason = "OTHER_BINDING" },
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.PermitConflict, "permit binding mismatch");
    }

    static async Task T_UnknownCost(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request with
        {
            Cost = CostPolicyDecision.ExplicitUnknown(),
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.UnknownCost, "UNKNOWN cost");
    }

    static async Task T_ForbiddenCost(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request with
        {
            Cost = CostPolicyDecision.ExplicitForbidden(ProductionFailureReasons.OverBudget),
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.OverBudget, "FORBIDDEN cost");
    }

    static async Task T_DifferentVariant(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request with
        {
            Variant = FamixaVeo31OfficialPricing.VideoAudio720p(),
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.UnknownCost, "audio/other variant");
    }

    static async Task T_DifferentResolution(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request with
        {
            Variant = FamixaVeo31OfficialPricing.VideoOnly4k(),
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.UnknownCost, "4K does not inherit 720p");
    }

    static async Task T_GenerateAudio(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request with
        {
            Variant = new FamixaVeoCostVariant("VIDEO_ONLY", "720p", true, 1),
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.UnknownCost, "generateAudio=true");
    }

    static async Task T_DifferentSampleCount(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request with
        {
            Variant = FamixaVeo31OfficialPricing.VideoOnly720p(2),
        });
        Ok(!r.Authorized && r.BlockReason == ProductionFailureReasons.UnknownCost, "sampleCount != 1");
    }

    static async Task T_FixtureAndUnselected(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var fixture = OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request with
        {
            Selection = OfficialSelection("fixture", "fixture-motion-v1"),
        });
        Ok(!fixture.Authorized && fixture.BlockReason == ProductionFailureReasons.ProviderModelMismatch,
            "Fixture provider rejected");
        var missing = OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request with { Selection = null });
        Ok(!missing.Authorized && missing.BlockReason == ProductionFailureReasons.MissingProductionAiSelection,
            "unselected provider/model rejected");
        var otherAction = OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request with
        {
            Action = ProductionActions.CreateVoice,
        });
        Ok(!otherAction.Authorized && otherAction.BlockReason == PerformanceContractIds.OfficialFilmForbidden,
            "other action rejected");
    }

    static async Task T_NoGlobalBypass(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var oneShot = new ProductionPaidNetworkOneShotPermit();
        Ok(!oneShot.IsEligible(
                RealVertexExecutionV1Lock.OfficialScope,
                RealVertexExecutionV1Lock.ShotCode,
                "google",
                "veo-3.1"),
            "TEST one-shot permit does not authorize official Veo");
        Ok(!oneShot.IsEligible(RealVertexExecutionV1Lock.OfficialScope, RealVertexExecutionV1Lock.ShotCode, "runway", "gen4_turbo"),
            "official film is not Runway-eligible");
        Ok(!CostOfficialPermitAuthorization.AuthorizePaidNetworkProductionDefault
            && !new ProductionCreateVideoRequestDto(
                RealVertexExecutionV1Lock.BuildId, "EP01", "SC01", "EP01-SC01-SH02",
                "", "", "", "", "16:9", 10, null, null, null).AuthorizePaidNetwork
            && !RealVertexExecutionPreflight.ProductionClosedDefaults(RealVertexExecutionV1Lock.OfficialScope)
                .AuthorizePaidNetwork,
            "production AuthorizePaidNetwork default remains false");
        Ok(!OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request with
        {
            Selection = OfficialSelection("runway", "gen4_turbo"),
        }).Authorized, "runway not authorized");
        Ok(!OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request with
        {
            Scope = new ProductionScope(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH01"),
        }).Authorized, "another shot not authorized");
    }

    static async Task T_SafetyAndClosedExecution(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var graph = ProductionVeoReadinessComposition.CreateClosedTestGraph();
        var closed = new ProductionVeoHttpAuthenticator();
        var issueBefore = ready.Permits.IssueCalls;
        var consumeBefore = ready.Permits.ConsumeCalls;
        var hash = ready.Frozen.SnapshotHash;
        var paid = OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request);
        var paidAgain = OfficialVeoPaidNetworkAuthorizationV1.Evaluate(ready.Request);
        Ok(paid.Authorized && paidAgain.Authorized, "policy AUTHORIZED");
        Ok(ready.Permits.IssueCalls == issueBefore, "permit issued = 0 in this checkpoint");
        Ok(ready.Permits.ConsumeCalls == consumeBefore && ready.Permit.ConsumedAt is null,
            "permit consumed = 0");
        Ok(ready.Frozen.SnapshotHash == hash, "canonical snapshot mutation = 0");
        Ok(graph.Transport.StartCount == 0 && graph.Handler.Requests.Count == 0
            && graph.Transport.HttpCallCount == 0 && graph.Authenticator.Calls == 0
            && closed.GetAccessTokenCalls == 0,
            "provider/network/OAuth/credential = 0");
        Ok(!ProductionVeoReadinessComposition.ProductionResolverIncludesVeo
            && !ProductionVeoReadinessComposition.RealVertexExecutionOpen,
            "resolver / execution lock unchanged");

        var report = CostOfficialPermitAuthorization.Evaluate(
            RealVertexExecutionPreflight.ProductionClosedDefaults(
                RealVertexExecutionV1Lock.OfficialScope, OfficialSelection())
            with
            {
                Frozen = ready.Frozen,
                LiveClaim = ready.Live,
                Adapter = new VeoMotionExecutionAdapter(new FakeVeoMotionTransport()),
                KeyframeBytes = ready.Bytes,
                Permit = ready.Permit,
                Cost = CostPolicyDecision.FromCatalog("google", "veo-3.1", 10),
                AuthorizePaidNetwork = false,
                OfficialPaidNetwork = paid,
                Authenticator = closed,
            });
        Ok(report.CostGatePass && report.PermitGatePass && report.PaidNetworkPass
            && report.PaidNetworkState == ProductionFailureReasons.PaidNetworkAuthorized,
            "preflight paid-network AUTHORIZED for exact official target");
        Ok(!report.AuthenticatorPass && !report.ResolverPass && !report.Preflight.Allowed
            && report.Preflight.Blockers.Contains(ProductionFailureReasons.AuthenticatorNotReady)
            && report.Preflight.Blockers.Contains(ProductionFailureReasons.UnknownProvider)
            && report.Preflight.Blockers.Contains(ProductionFailureReasons.HttpCompositionUnavailable)
            && report.Preflight.Blockers.Contains(ProductionFailureReasons.ExecutionAuthorizationRequired)
            && !report.Preflight.Blockers.Contains(ProductionFailureReasons.PaidNetworkForbidden),
            "Vertex remains impossible; PAID_NETWORK_FORBIDDEN removed");
    }

    static void T_Ck13BStillGreen(Action<bool, string> Ok)
    {
        var ck13b = ProductionOfficialCreateVideoPermitAuthorizationV1Regression.Run();
        Ok(ck13b.Count == 0, ck13b.Count == 0 ? "CK13B still green" : "CK13B:" + string.Join(",", ck13b));
    }

    static async Task<ReadyState> Ready()
    {
        var store = new InMemoryProductionWorkStore();
        var permits = new CountingProductionExecutionPermitStore();
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-KF-14-" + new string('K', 48));
        var hash = KitVideoIntegrityRules.Sha256Hex(bytes);
        var engine = new ProductionWorkExecutionEngine(store);
        var req = await engine.CreateRequestAsync(
            RealVertexExecutionV1Lock.OfficialScope, ProductionActions.CreateImage, "ck14",
            "kf-14-" + Guid.NewGuid().ToString("N"));
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
            frozen,
            live,
            bytes,
            issued.Permit,
            permits,
            new OfficialVeoPaidNetworkAuthorizationRequest(
                RealVertexExecutionV1Lock.OfficialScope,
                ProductionActions.CreateVideo,
                OfficialSelection(),
                frozen,
                live,
                issued.Permit));
    }

    static ProductionAiSelection OfficialSelection(string provider = "google", string model = "veo-3.1") =>
        new(
            OfficialCreateVideoPermitAuthorizationV1.OfficialSelectionId,
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode, "EP01", "SC01",
            ProductionAiSelectionStages.Video,
            provider, model,
            ProductionAiSelectionModes.Explicit,
            "checkpoint-14",
            DateTimeOffset.UnixEpoch,
            "DIRECTOR_SELECTION",
            ProductionAiSelectionStatuses.Active,
            "SELECTION_HASH_V1:ck14");

    static ShotExecutionInput Live(OfficialCreateVideoFreezeSources sources) =>
        new(
            sources.Story, sources.ActingBeat, sources.VisualContract, "CHAR-001",
            sources.KeyframeHash, sources.Timing, "", "",
            sources.KeyframeArtifactId, sources.KeyframeHash, "16:9", sources.DurationSec,
            ShotExecutionCompiler.Version, [sources.KeyframeArtifactId],
            PerformanceSnapshotId: sources.PerformanceSnapshotId,
            PerformanceHash: sources.PerformanceHash);

    sealed record ReadyState(
        ProductionTaskInputSnapshot Frozen,
        ShotExecutionInput Live,
        byte[] Bytes,
        ProductionExecutionPermit Permit,
        CountingProductionExecutionPermitStore Permits,
        OfficialVeoPaidNetworkAuthorizationRequest Request);
}
