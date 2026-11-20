using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionOfficialCreateVideoPermitAuthorizationV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_OFFICIAL_CREATE_VIDEO_PERMIT_AUTHORIZATION_V1";

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
        await T_IssueAndIdempotent(Ok);
        await T_BindingAndCost(Ok);
        await T_ExistingMismatchFails(Ok);
        await T_WrongBuild(Ok);
        await T_WrongShot(Ok);
        await T_WrongProvider(Ok);
        await T_WrongModel(Ok);
        await T_WrongSelection(Ok);
        await T_MissingFrozen(Ok);
        await T_FrozenMismatch(Ok);
        await T_UnknownCost(Ok);
        await T_ForbiddenCost(Ok);
        await T_NoConsumeNoNetwork(Ok);
        await T_SnapshotUnchanged(Ok);
        await T_PreflightStillClosed(Ok);
        T_Ck13StillCostPassPermitFailUntilIssued(Ok);
    }

    static async Task T_IssueAndIdempotent(Action<bool, string> Ok)
    {
        var (store, sources, _, permits) = await Harness();
        var frozen = (await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin())).Snapshot;
        var req = Request(frozen, sources);
        var first = await OfficialCreateVideoPermitAuthorizationV1.IssueAsync(permits, req);
        var second = await OfficialCreateVideoPermitAuthorizationV1.IssueAsync(permits, req);
        var issued = await permits.GetIssuedAsync(
            RealVertexExecutionV1Lock.BuildId, RealVertexExecutionV1Lock.ShotCode,
            ProductionActions.CreateVideo, "google", "veo-3.1");
        Ok(first.Created && !first.Reused, "first issue creates permit");
        Ok(second.Reused && !second.Created && second.Permit.PermitId == first.Permit.PermitId,
            "identical unconsumed permit is reused");
        Ok(issued is not null && issued.PermitId == first.Permit.PermitId, "exactly one issued row");
        Ok(first.Permit.Status == ProductionExecutionPermitStatuses.Issued
            && first.Permit.ConsumedAt is null,
            "permit state ISSUED / unconsumed");
        Ok(permits.IssueCalls == 1 && permits.ConsumeCalls == 0, "second call does not Issue or Consume");
        Ok(ProductionExecutionPermitCanon.Matches(
            first.Permit,
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode,
            ProductionActions.CreateVideo,
            "google",
            "veo-3.1"), "canonical Matches official target");
    }

    static async Task T_BindingAndCost(Action<bool, string> Ok)
    {
        var (store, sources, _, permits) = await Harness();
        var frozen = (await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin())).Snapshot;
        var result = await OfficialCreateVideoPermitAuthorizationV1.IssueAsync(permits, Request(frozen, sources));
        var quote = FamixaProviderCostCatalog.Quote(
            "google", "veo-3.1", null, FamixaVeo31OfficialPricing.ProductionDefault);
        Ok(result.Permit.BuildId == RealVertexExecutionV1Lock.BuildId
            && result.Permit.ShotCode == RealVertexExecutionV1Lock.ShotCode
            && result.Permit.ProviderId == "google"
            && result.Permit.ModelId == "veo-3.1"
            && result.Permit.Action == ProductionActions.CreateVideo,
            "permit bound to official build/shot/provider/model");
        Ok(result.BindingReason.Contains("selectionId=" + OfficialCreateVideoPermitAuthorizationV1.OfficialSelectionId.ToString("D"), StringComparison.Ordinal)
            && result.BindingReason.Contains("snapshotId=" + frozen.SnapshotId.ToString("D"), StringComparison.Ordinal)
            && result.BindingReason.Contains("snapshotHash=" + frozen.SnapshotHash, StringComparison.Ordinal),
            "permit bound to selection + frozen snapshot identity");
        Ok(quote.EstimatedAmount == 0.20m && quote.Currency == "USD" && quote.BillingUnit == "count"
            && quote.CostKind == FamixaCostKind.Known
            && result.BindingReason.Contains("amount=0.20", StringComparison.Ordinal)
            && result.BindingReason.Contains("variant=VIDEO_ONLY", StringComparison.Ordinal)
            && result.BindingReason.Contains("resolution=720p", StringComparison.Ordinal)
            && result.BindingReason.Contains("audio=false", StringComparison.Ordinal)
            && result.BindingReason.Contains("sampleCount=1", StringComparison.Ordinal),
            "permit bound to official VIDEO_ONLY 720p USD 0.20 COUNT");
        Ok(result.Permit.IssuedAt != default && result.Permit.PermitHash.StartsWith("PERMIT_HASH_V1:", StringComparison.Ordinal),
            "issued timestamp + permit hash");
        Ok(!ProductionVeoReadinessComposition.RealVertexExecutionOpen
            && !ProductionVeoReadinessComposition.ProductionResolverIncludesVeo,
            "issuance does not imply provider execution authorization");
    }

    static async Task T_ExistingMismatchFails(Action<bool, string> Ok)
    {
        var (store, sources, _, permits) = await Harness();
        var frozen = (await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin())).Snapshot;
        await permits.IssueAsync(new IssueProductionExecutionPermitCommand(
            RealVertexExecutionV1Lock.BuildId, RealVertexExecutionV1Lock.ShotCode,
            "EP01", "SC01", ProductionActions.CreateVideo, "google", "veo-3.1",
            "ck13b", "OTHER_BINDING"));
        var failed = false;
        try { await OfficialCreateVideoPermitAuthorizationV1.IssueAsync(permits, Request(frozen, sources)); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.PermitConflict)
        {
            failed = true;
        }
        var latest = await permits.GetIssuedAsync(
            RealVertexExecutionV1Lock.BuildId, RealVertexExecutionV1Lock.ShotCode,
            ProductionActions.CreateVideo, "google", "veo-3.1");
        Ok(failed, "existing binding mismatch fail-closed");
        Ok(latest is { Reason: "OTHER_BINDING" }, "existing permit not overwritten / not repaired");
    }

    static async Task T_WrongBuild(Action<bool, string> Ok)
    {
        var (store, sources, _, permits) = await Harness();
        var frozen = (await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin())).Snapshot;
        var req = Request(frozen, sources) with
        {
            Scope = new ProductionScope(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "EP01", "SC01", "EP01-SC01-SH02"),
        };
        Ok(await Rejects(permits, req, ProductionFailureReasons.UnauthorizedVertexTarget)
            && permits.IssueCalls == 0,
            "wrong build rejected");
    }

    static async Task T_WrongShot(Action<bool, string> Ok)
    {
        var (store, sources, _, permits) = await Harness();
        var frozen = (await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin())).Snapshot;
        var req = Request(frozen, sources) with
        {
            Scope = new ProductionScope(RealVertexExecutionV1Lock.BuildId, "EP01", "SC01", "EP01-SC01-SH01"),
        };
        Ok(await Rejects(permits, req, ProductionFailureReasons.UnauthorizedVertexTarget)
            && permits.IssueCalls == 0,
            "wrong shot rejected");
    }

    static async Task T_WrongProvider(Action<bool, string> Ok)
    {
        var (store, sources, _, permits) = await Harness();
        var frozen = (await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin())).Snapshot;
        var req = Request(frozen, sources) with { Selection = OfficialSelection("runway", "veo-3.1") };
        Ok(await Rejects(permits, req, ProductionFailureReasons.ProviderModelMismatch)
            && permits.IssueCalls == 0,
            "wrong provider rejected — no Fixture fallback");
    }

    static async Task T_WrongModel(Action<bool, string> Ok)
    {
        var (store, sources, _, permits) = await Harness();
        var frozen = (await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin())).Snapshot;
        var req = Request(frozen, sources) with { Selection = OfficialSelection("google", "veo-2") };
        Ok(await Rejects(permits, req, ProductionFailureReasons.ProviderModelMismatch)
            && permits.IssueCalls == 0,
            "wrong model rejected");
    }

    static async Task T_WrongSelection(Action<bool, string> Ok)
    {
        var (store, sources, _, permits) = await Harness();
        var frozen = (await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin())).Snapshot;
        var sel = OfficialSelection() with { SelectionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") };
        Ok(await Rejects(permits, Request(frozen, sources) with { Selection = sel }, ProductionFailureReasons.SelectionConflict)
            && permits.IssueCalls == 0,
            "wrong selection rejected");
    }

    static async Task T_MissingFrozen(Action<bool, string> Ok)
    {
        var (_, sources, _, permits) = await Harness();
        var req = Request(null, sources) with { Frozen = null };
        Ok(await Rejects(permits, req, ProductionFailureReasons.InputNotFrozen)
            && permits.IssueCalls == 0,
            "missing frozen snapshot rejected");
    }

    static async Task T_FrozenMismatch(Action<bool, string> Ok)
    {
        var (store, sources, _, permits) = await Harness();
        var frozen = (await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin())).Snapshot;
        var live = Live(sources) with { Story = sources.Story + " changed" };
        var failed = false;
        try { await OfficialCreateVideoPermitAuthorizationV1.IssueAsync(permits, Request(frozen, sources) with { Live = live }); }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith(ProductionFailureReasons.InputSnapshotMismatch, StringComparison.Ordinal))
        {
            failed = true;
        }
        Ok(failed && permits.IssueCalls == 0, "frozen snapshot mismatch rejected");
    }

    static async Task T_UnknownCost(Action<bool, string> Ok)
    {
        var (store, sources, _, permits) = await Harness();
        var frozen = (await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin())).Snapshot;
        Ok(await Rejects(permits, Request(frozen, sources) with { Cost = CostPolicyDecision.ExplicitUnknown() },
            ProductionFailureReasons.UnknownCost) && permits.IssueCalls == 0,
            "UNKNOWN cost rejected");
    }

    static async Task T_ForbiddenCost(Action<bool, string> Ok)
    {
        var (store, sources, _, permits) = await Harness();
        var frozen = (await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin())).Snapshot;
        Ok(await Rejects(permits, Request(frozen, sources) with { Cost = CostPolicyDecision.ExplicitForbidden(ProductionFailureReasons.OverBudget) },
            ProductionFailureReasons.OverBudget) && permits.IssueCalls == 0,
            "FORBIDDEN cost rejected");
    }

    static async Task T_NoConsumeNoNetwork(Action<bool, string> Ok)
    {
        var (store, sources, _, permits) = await Harness();
        var graph = ProductionVeoReadinessComposition.CreateClosedTestGraph();
        var closed = new ProductionVeoHttpAuthenticator();
        var frozen = (await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin())).Snapshot;
        var result = await OfficialCreateVideoPermitAuthorizationV1.IssueAsync(permits, Request(frozen, sources));
        Ok(permits.ConsumeCalls == 0 && result.Permit.ConsumedAt is null, "permit consumed = 0");
        Ok(graph.Transport.StartCount == 0 && graph.Handler.Requests.Count == 0
            && graph.Transport.HttpCallCount == 0, "no HTTP / provider job");
        Ok(graph.Authenticator.Calls == 0 && closed.GetAccessTokenCalls == 0, "no OAuth / credential read");
        Ok(!ProductionVeoReadinessComposition.ProductionResolverIncludesVeo, "production resolver unchanged");
    }

    static async Task T_SnapshotUnchanged(Action<bool, string> Ok)
    {
        var (store, sources, _, permits) = await Harness();
        var first = await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin());
        var hash = first.Snapshot.SnapshotHash;
        var story = first.Snapshot.Story;
        _ = await OfficialCreateVideoPermitAuthorizationV1.IssueAsync(permits, Request(first.Snapshot, sources));
        var after = await OfficialCreateVideoFreezeV1.FindOfficialCreateVideoSnapshotAsync(store);
        Ok(after!.SnapshotId == first.Snapshot.SnapshotId
            && after.SnapshotHash == hash
            && after.Story == story
            && after.TimingHash == first.Snapshot.TimingHash
            && after.DurationSec == 10
            && await OfficialCreateVideoFreezeV1.CountOfficialCreateVideoSnapshotsAsync(store) == 1,
            "canonical snapshot unchanged");
    }

    static async Task T_PreflightStillClosed(Action<bool, string> Ok)
    {
        var (store, sources, bytes, permits) = await Harness();
        var frozen = (await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin())).Snapshot;
        var issued = await OfficialCreateVideoPermitAuthorizationV1.IssueAsync(permits, Request(frozen, sources));
        var closed = RealVertexExecutionPreflight.ProductionClosedDefaults(
            RealVertexExecutionV1Lock.OfficialScope, OfficialSelection());
        var withPermit = CostOfficialPermitAuthorization.Evaluate(closed with
        {
            Frozen = frozen,
            LiveClaim = Live(sources),
            Adapter = new VeoMotionExecutionAdapter(new FakeVeoMotionTransport()),
            KeyframeBytes = bytes,
            Permit = issued.Permit,
            Cost = CostPolicyDecision.FromCatalog("google", "veo-3.1", 10),
        });
        Ok(withPermit.CostGatePass && withPermit.PermitGatePass, "cost + permit gates PASS after issue");
        Ok(!withPermit.PaidNetworkPass && !withPermit.AuthenticatorPass && !withPermit.ResolverPass
            && !withPermit.Preflight.Allowed,
            "Real Vertex remains CLOSED");
        Ok(withPermit.Preflight.Blockers.Contains(ProductionFailureReasons.PaidNetworkForbidden)
            && withPermit.Preflight.Blockers.Contains(ProductionFailureReasons.AuthenticatorNotReady)
            && withPermit.Preflight.Blockers.Contains(ProductionFailureReasons.UnknownProvider)
            && withPermit.Preflight.Blockers.Contains(ProductionFailureReasons.ExecutionAuthorizationRequired),
            "remaining blockers unchanged");
        Ok(permits.ConsumeCalls == 0, "preflight does not consume");
    }

    static void T_Ck13StillCostPassPermitFailUntilIssued(Action<bool, string> Ok)
    {
        var ck13 = ProductionCostOfficialPermitAuthorizationV1Regression.Run();
        Ok(ck13.Count == 0, ck13.Count == 0 ? "CK13 suite still green" : "CK13:" + string.Join(",", ck13));
        var report = CostOfficialPermitAuthorization.Evaluate(
            RealVertexExecutionPreflight.ProductionClosedDefaults(
                RealVertexExecutionV1Lock.OfficialScope, OfficialSelection()));
        Ok(report.CostGatePass && !report.PermitGatePass && !report.Preflight.Allowed,
            "ClosedDefaults still permit-missing until this issuer is invoked");
    }

    static async Task<bool> Rejects(
        CountingProductionExecutionPermitStore permits,
        OfficialCreateVideoPermitIssueRequest request,
        string code)
    {
        try
        {
            await OfficialCreateVideoPermitAuthorizationV1.IssueAsync(permits, request);
            return false;
        }
        catch (InvalidOperationException ex) when (ex.Message == code
            || ex.Message.StartsWith(code, StringComparison.Ordinal))
        {
            return true;
        }
    }

    static OfficialCreateVideoPermitIssueRequest Request(
        ProductionTaskInputSnapshot? frozen,
        OfficialCreateVideoFreezeSources sources) =>
        new(
            RealVertexExecutionV1Lock.OfficialScope,
            OfficialSelection(),
            frozen,
            Live(sources));

    static ProductionAiSelection OfficialSelection(string provider = "google", string model = "veo-3.1") =>
        new(
            OfficialCreateVideoPermitAuthorizationV1.OfficialSelectionId,
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode, "EP01", "SC01",
            ProductionAiSelectionStages.Video,
            provider, model,
            ProductionAiSelectionModes.Explicit,
            "checkpoint-13b",
            DateTimeOffset.UnixEpoch,
            "DIRECTOR_SELECTION",
            ProductionAiSelectionStatuses.Active,
            "SELECTION_HASH_V1:ck13b");

    static ShotExecutionInput Live(OfficialCreateVideoFreezeSources sources) =>
        new(
            sources.Story, sources.ActingBeat, sources.VisualContract, "CHAR-001",
            sources.KeyframeHash, sources.Timing, "", "",
            sources.KeyframeArtifactId, sources.KeyframeHash, "16:9", sources.DurationSec,
            ShotExecutionCompiler.Version, [sources.KeyframeArtifactId],
            PerformanceSnapshotId: sources.PerformanceSnapshotId,
            PerformanceHash: sources.PerformanceHash);

    static async Task<(InMemoryProductionWorkStore Store, OfficialCreateVideoFreezeSources Sources, byte[] Bytes, CountingProductionExecutionPermitStore Permits)>
        Harness()
    {
        var store = new InMemoryProductionWorkStore();
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-KF-13B-" + new string('K', 48));
        var hash = KitVideoIntegrityRules.Sha256Hex(bytes);
        var engine = new ProductionWorkExecutionEngine(store);
        var req = await engine.CreateRequestAsync(
            RealVertexExecutionV1Lock.OfficialScope, ProductionActions.CreateImage, "ck13b",
            "kf-13b-" + Guid.NewGuid().ToString("N"));
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
        return (store, OfficialCreateVideoFreezeV1.FromEp01Sh02Canon(bytes, hash), bytes,
            new CountingProductionExecutionPermitStore());
    }
}
