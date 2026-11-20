using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionOfficialVeoLiveOfficialContractBindV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_OFFICIAL_VEO_LIVE_OFFICIAL_CONTRACT_BIND_V1";

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
        await T_OfficialContractBound(Ok);
        await T_MissingSnapshot(Ok);
        await T_SnapshotOtherBuild(Ok);
        await T_SnapshotOtherShot(Ok);
        await T_ProviderModelMismatch(Ok);
        await T_FrozenFieldMismatch(Ok);
        await T_KeyframeIntegrityMismatch(Ok);
        await T_MissingPermit(Ok);
        await T_ConsumedPermit(Ok);
        await T_PermitSnapshotMismatch(Ok);
        await T_AmbiguousPermits(Ok);
        await T_AuthHashMismatch(Ok);
        await T_CredentialReadZero(Ok);
        await T_HttpZero(Ok);
        await T_IdempotentRepeat(Ok);
        T_Ck18StillGreen(Ok);
    }

    static async Task T_OfficialContractBound(Action<bool, string> Ok)
    {
        var ready = await SeedOfficial();
        var beforeSnaps = await OfficialCreateVideoFreezeV1.CountOfficialCreateVideoSnapshotsAsync(ready.Store);
        var beforeIssue = ready.Permits.IssueCalls;
        var r = await Bind(ready);
        Ok(r.Bound && r.State == ProductionFailureReasons.LiveOfficialContractBound, "LIVE_OFFICIAL_CONTRACT_BOUND");
        Ok(r.Handoff == OfficialVeoLiveOfficialContractBindV1.ReadyForCk18RealStart, "READY_FOR_CK18_REAL_START");
        Ok(ready.Frozen is not null && r.SnapshotId == ready.Frozen.SnapshotId && r.SnapshotHash == ready.Frozen.SnapshotHash, "same snapshot identity");
        Ok(ready.Permit is not null && r.PermitId == ready.Permit.PermitId && r.PermitHash == ready.Permit.PermitHash, "same permit identity");
        Ok(r.PermitStatus == ProductionExecutionPermitStatuses.Issued, "permit remains ISSUED");
        Ok(r.DurationSec == OfficialCreateVideoFreezeV1.OfficialDurationSec, "DurationSec stays 10");
        Ok(r.AuthorizationHash == ready.ExpectedHash, "AUTH_HASH matches CK17 recompute");
        Ok(r.Authorization is { Authorized: true, State: ProductionFailureReasons.ExecutionAuthorized },
            "CK17 EXECUTION_AUTHORIZED");
        Ok(await OfficialCreateVideoFreezeV1.CountOfficialCreateVideoSnapshotsAsync(ready.Store) == beforeSnaps,
            "snapshot create = 0");
        Ok(ready.Permits.IssueCalls == beforeIssue && ready.Permits.ConsumeCalls == 0, "permit issue/consume = 0");
        Ok((await ready.Store.ListAttemptsAsync(ready.Frozen!.TaskId)).Count == 0, "provider attempts = 0");
        AssertZeroSideEffects(Ok, ready, r, "happy-path");
    }

    static async Task T_MissingSnapshot(Action<bool, string> Ok)
    {
        var ready = await SeedOfficial(freeze: false, issue: false);
        var r = await Bind(ready);
        Ok(!r.Bound && r.State == ProductionFailureReasons.LiveOfficialSnapshotNotBound, "A snapshot missing");
        Ok(r.PermitId is null, "no permit bind");
        Ok(ready.Permits.IssueCalls == 0 && ready.Permits.ConsumeCalls == 0, "no permit issue");
        AssertZeroSideEffects(Ok, ready, r, "A");
    }

    static async Task T_SnapshotOtherBuild(Action<bool, string> Ok)
    {
        var ready = await SeedOfficial(freeze: false, issue: false);
        await InsertForeignSnapshotAsync(ready.Store, OtherBuildScope(), ready.Bytes);
        var r = await Bind(ready);
        Ok(!r.Bound && r.State == ProductionFailureReasons.LiveOfficialSnapshotNotBound, "B other build fail-closed");
        Ok(r.SnapshotId is null, "official snapshot not selected");
        AssertZeroSideEffects(Ok, ready, r, "B");
    }

    static async Task T_SnapshotOtherShot(Action<bool, string> Ok)
    {
        var ready = await SeedOfficial(freeze: false, issue: false);
        await InsertForeignSnapshotAsync(ready.Store, OtherShotScope(), ready.Bytes);
        var r = await Bind(ready);
        Ok(!r.Bound && r.State == ProductionFailureReasons.LiveOfficialSnapshotNotBound, "C other shot fail-closed");
        AssertZeroSideEffects(Ok, ready, r, "C");
    }

    static async Task T_ProviderModelMismatch(Action<bool, string> Ok)
    {
        var ready = await SeedOfficial(freeze: false, issue: false);
        await InsertOfficialScopeSnapshotAsync(ready.Store, ready.Bytes, provider: "runway", model: "gen3");
        var r = await Bind(ready);
        Ok(!r.Bound && r.State == ProductionFailureReasons.ProviderModelMismatch, "D provider/model mismatch");
        AssertZeroSideEffects(Ok, ready, r, "D");
    }

    static async Task T_FrozenFieldMismatch(Action<bool, string> Ok)
    {
        var ready = await SeedOfficial(freeze: false, issue: false);
        await InsertOfficialScopeSnapshotAsync(ready.Store, ready.Bytes, story: ProductionEp01Sh02IngressCanon.Story + " drift");
        var before = await OfficialCreateVideoFreezeV1.FindOfficialCreateVideoSnapshotAsync(ready.Store);
        var r = await Bind(ready);
        Ok(!r.Bound && r.State == ProductionFailureReasons.InputSnapshotMismatch, "E INPUT_SNAPSHOT_MISMATCH");
        var after = await OfficialCreateVideoFreezeV1.FindOfficialCreateVideoSnapshotAsync(ready.Store);
        Ok(after is not null && after.SnapshotId == before!.SnapshotId && after.Story == before.Story,
            "E no snapshot mutation");
        AssertZeroSideEffects(Ok, ready, r, "E");
    }

    static async Task T_KeyframeIntegrityMismatch(Action<bool, string> Ok)
    {
        var ready = await SeedOfficial(issue: false);
        var other = Encoding.UTF8.GetBytes("FAMIXA-KF-18A-OTHER-" + new string('Z', 48));
        var r = await Bind(ready, other);
        Ok(!r.Bound && r.State == ProductionFailureReasons.InputSnapshotMismatch, "F keyframe integrity");
        Ok(r.PermitId is null, "F no permit bind");
        AssertZeroSideEffects(Ok, ready, r, "F");
    }

    static async Task T_MissingPermit(Action<bool, string> Ok)
    {
        var ready = await SeedOfficial(issue: false);
        var r = await Bind(ready);
        Ok(!r.Bound && r.State == ProductionFailureReasons.LiveOfficialPermitNotBound, "G permit missing");
        Ok(ready.Permits.IssueCalls == 0, "G no permit issue");
        AssertZeroSideEffects(Ok, ready, r, "G");
    }

    static async Task T_ConsumedPermit(Action<bool, string> Ok)
    {
        var ready = await SeedOfficial();
        Ok(await ready.Permits.TryConsumeAsync(
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode,
            ProductionActions.CreateVideo,
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId), "seed consume");
        var consumeAfterSeed = ready.Permits.ConsumeCalls;
        var r = await Bind(ready);
        Ok(!r.Bound && r.State == PerformanceContractIds.OfficialFilmForbidden, "H consumed permit blocked");
        Ok(ready.Permits.ConsumeCalls == consumeAfterSeed && ready.Permits.IssueCalls == 1,
            "H bind does not consume or reissue");
        AssertZeroSideEffects(Ok, ready, r, "H");
    }

    static async Task T_PermitSnapshotMismatch(Action<bool, string> Ok)
    {
        var ready = await SeedOfficial(issue: false);
        ready.Permits.AddExisting(new ProductionExecutionPermit(
            Guid.NewGuid(),
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode, "EP01", "SC01",
            ProductionActions.CreateVideo,
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId,
            ProductionExecutionPermitStatuses.Issued,
            OfficialCreateVideoPermitAuthorizationV1.IssuedBy,
            DateTimeOffset.UtcNow,
            null,
            "OTHER_BINDING",
            ProductionExecutionPermitCanon.HashOf(
                RealVertexExecutionV1Lock.BuildId,
                RealVertexExecutionV1Lock.ShotCode,
                ProductionActions.CreateVideo,
                RealVertexExecutionV1Lock.ProviderId,
                RealVertexExecutionV1Lock.ModelId)));
        var r = await Bind(ready);
        Ok(!r.Bound && r.State == ProductionFailureReasons.LiveOfficialPermitSnapshotMismatch,
            "I LIVE_OFFICIAL_PERMIT_SNAPSHOT_MISMATCH");
        Ok(ready.Permits.IssueCalls == 0, "I no new permit");
        AssertZeroSideEffects(Ok, ready, r, "I");
    }

    static async Task T_AmbiguousPermits(Action<bool, string> Ok)
    {
        var ready = await SeedOfficial();
        ready.Permits.AddExisting(new ProductionExecutionPermit(
            Guid.NewGuid(),
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode, "EP01", "SC01",
            ProductionActions.CreateVideo,
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId,
            ProductionExecutionPermitStatuses.Issued,
            OfficialCreateVideoPermitAuthorizationV1.IssuedBy,
            DateTimeOffset.UtcNow,
            null,
            ready.Permit!.Reason,
            ready.Permit.PermitHash));
        var r = await Bind(ready);
        Ok(!r.Bound && r.State == ProductionFailureReasons.LiveOfficialPermitAmbiguous, "J LIVE_OFFICIAL_PERMIT_AMBIGUOUS");
        Ok(ready.Permits.IssueCalls == 1, "J no latest-pick / no issue");
        AssertZeroSideEffects(Ok, ready, r, "J");
    }

    static async Task T_AuthHashMismatch(Action<bool, string> Ok)
    {
        var ready = await SeedOfficial();
        var r = await OfficialVeoLiveOfficialContractBindV1.BindAsync(new OfficialVeoLiveOfficialContractBindRequest(
            ready.Store, ready.Permits, ready.Selection, KeyframeBytes: null,
            ProjectId: ProductionOfficialVeoHttpCompositionV1Regression.ComposeProject));
        Ok(!r.Bound && r.State == ProductionFailureReasons.ExecutionAuthorizationRequired,
            "K EXECUTION_AUTHORIZATION_REQUIRED");
        Ok(ready.Permits.ConsumeCalls == 0 && ready.Permits.IssueCalls == 1, "K no permit mutation");
        AssertZeroSideEffects(Ok, ready, r, "K");
    }

    static async Task T_CredentialReadZero(Action<bool, string> Ok)
    {
        var ready = await SeedOfficial();
        var closed = new ProductionVeoHttpAuthenticator();
        var r = await Bind(ready);
        Ok(r.Bound, "L bind succeeded");
        Ok(closed.GetAccessTokenCalls == 0, "L unused production authenticator GetAccessToken = 0");
        Ok(r.Authorization?.Authenticator?.Authenticator is ProductionVeoHttpAuthenticator prod
            && prod.GetAccessTokenCalls == 0, "L composed production authenticator GetAccessToken = 0");
        Ok(r.Authorization?.Resolver?.Authenticator is { Calls: 0 }, "L resolver FakeVeoHttpAuthenticator Calls = 0");
        AssertZeroSideEffects(Ok, ready, r, "L");
    }

    static async Task T_HttpZero(Action<bool, string> Ok)
    {
        var ready = await SeedOfficial();
        var r = await Bind(ready);
        Ok(r.Bound, "M bind succeeded");
        Ok((r.Authorization?.Resolver?.Transport?.HttpCallCount ?? -1) == 0, "M composed transport HTTP = 0");
        Ok((r.Authorization?.Resolver?.Handler?.Requests.Count ?? -1) == 0, "M composed handler requests = 0");
        Ok((r.Authorization?.Resolver?.Handler?.PredictCalls ?? -1) == 0, "M PredictCalls = 0");
        Ok((r.Authorization?.Resolver?.Handler?.FetchCalls ?? -1) == 0, "M FetchCalls = 0");
        AssertZeroSideEffects(Ok, ready, r, "M");
    }

    static async Task T_IdempotentRepeat(Action<bool, string> Ok)
    {
        var ready = await SeedOfficial();
        var first = await Bind(ready);
        var second = await Bind(ready);
        Ok(first.Bound && second.Bound, "N both bound");
        Ok(first.SnapshotId == second.SnapshotId && first.SnapshotHash == second.SnapshotHash, "N same snapshot");
        Ok(first.PermitId == second.PermitId && first.PermitHash == second.PermitHash, "N same permit");
        Ok(first.AuthorizationHash == second.AuthorizationHash && first.AuthorizationHash == ready.ExpectedHash,
            "N same AUTH_HASH");
        Ok(await OfficialCreateVideoFreezeV1.CountOfficialCreateVideoSnapshotsAsync(ready.Store) == 1, "N snapshot count 1");
        Ok(ready.Permits.IssueCalls == 1 && ready.Permits.ConsumeCalls == 0, "N no new permit artifacts");
        Ok((await ready.Store.ListAttemptsAsync(ready.Frozen!.TaskId)).Count == 0, "N no attempts");
        AssertZeroSideEffects(Ok, ready, first, "N-1");
        AssertZeroSideEffects(Ok, ready, second, "N-2");
    }

    static void T_Ck18StillGreen(Action<bool, string> Ok)
    {
        var ck18 = ProductionOfficialVeoRealVertexStartV1Regression.Run();
        Ok(ck18.Count == 0, ck18.Count == 0 ? "CK18 still green" : "CK18:" + string.Join(",", ck18));
    }

    static Task<OfficialVeoLiveOfficialContractBindResult> Bind(ReadyState ready, byte[]? bytes = null) =>
        OfficialVeoLiveOfficialContractBindV1.BindAsync(new OfficialVeoLiveOfficialContractBindRequest(
            ready.Store,
            ready.Permits,
            ready.Selection,
            bytes ?? ready.Bytes,
            ProductionOfficialVeoHttpCompositionV1Regression.ComposeProject));

    static void AssertZeroSideEffects(
        Action<bool, string> Ok,
        ReadyState ready,
        OfficialVeoLiveOfficialContractBindResult r,
        string tag)
    {
        Ok(ready.Graph.Transport.StartCount == 0, tag + " unused Start = 0");
        Ok(ready.Graph.Transport.PollCount == 0, tag + " unused Poll = 0");
        Ok(ready.Graph.Handler.Requests.Count == 0, tag + " unused HTTP requests = 0");
        Ok(ready.Graph.Authenticator.Calls == 0, tag + " unused OAuth = 0");
        Ok(r.Handoff == (r.Bound
            ? OfficialVeoLiveOfficialContractBindV1.ReadyForCk18RealStart
            : OfficialVeoLiveOfficialContractBindV1.NotReadyForCk18RealStart), tag + " handoff");
    }

    static async Task<ReadyState> SeedOfficial(bool freeze = true, bool issue = true)
    {
        var store = new InMemoryProductionWorkStore();
        var permits = new CountingProductionExecutionPermitStore();
        var graph = ProductionVeoReadinessComposition.CreateClosedTestGraph();
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-KF-18A-" + new string('K', 48));
        var hash = KitVideoIntegrityRules.Sha256Hex(bytes);
        var engine = new ProductionWorkExecutionEngine(store);
        var req = await engine.CreateRequestAsync(
            RealVertexExecutionV1Lock.OfficialScope, ProductionActions.CreateImage, "ck18a",
            "kf-18a-" + Guid.NewGuid().ToString("N"));
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
        ProductionTaskInputSnapshot? frozen = null;
        ProductionExecutionPermit? permit = null;
        if (freeze)
            frozen = (await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin())).Snapshot;
        var live = new ShotExecutionInput(
            sources.Story, sources.ActingBeat, sources.VisualContract, "CHAR-001",
            sources.KeyframeHash, sources.Timing, "", "",
            sources.KeyframeArtifactId, sources.KeyframeHash, "16:9", sources.DurationSec,
            ShotExecutionCompiler.Version, [sources.KeyframeArtifactId],
            PerformanceSnapshotId: sources.PerformanceSnapshotId,
            PerformanceHash: sources.PerformanceHash);
        var selection = new ProductionAiSelection(
            OfficialCreateVideoPermitAuthorizationV1.OfficialSelectionId,
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode, "EP01", "SC01",
            ProductionAiSelectionStages.Video,
            "google", "veo-3.1",
            ProductionAiSelectionModes.Explicit,
            "checkpoint-18a",
            OfficialVeoExecutionAuthorizationV1.OfficialSelectedAt,
            "DIRECTOR_SELECTION",
            ProductionAiSelectionStatuses.Active,
            "SELECTION_HASH_V1:ck18a");
        if (freeze && issue)
        {
            permit = (await OfficialCreateVideoPermitAuthorizationV1.IssueAsync(
                permits,
                new OfficialCreateVideoPermitIssueRequest(
                    RealVertexExecutionV1Lock.OfficialScope, selection, frozen, live))).Permit;
        }

        string? expectedHash = null;
        if (frozen is not null && permit is not null)
        {
            var authRequest = new OfficialVeoExecutionAuthorizationRequest(
                RealVertexExecutionV1Lock.OfficialScope,
                ProductionActions.CreateVideo,
                selection,
                frozen,
                live,
                bytes,
                "image/png",
                CostPolicyDecision.FromCatalog("google", "veo-3.1", 10),
                FamixaVeo31OfficialPricing.ProductionDefault,
                permit,
                ProjectId: ProductionOfficialVeoHttpCompositionV1Regression.ComposeProject);
            expectedHash = OfficialVeoExecutionAuthorizationV1.Evaluate(authRequest).AuthorizationHash;
        }

        return new ReadyState(store, frozen, live, bytes, permit, permits, selection, graph, expectedHash);
    }

    static async Task InsertForeignSnapshotAsync(IProductionWorkStore store, ProductionScope scope, byte[] bytes)
    {
        var engine = new ProductionWorkExecutionEngine(store);
        var req = await engine.CreateRequestAsync(scope, ProductionActions.CreateVideo, "ck18a-foreign",
            "foreign-" + Guid.NewGuid().ToString("N"));
        var task = await engine.CreateTaskAsync(req.RequestId);
        var hash = KitVideoIntegrityRules.Sha256Hex(bytes);
        await store.InsertSnapshotAsync(OfficialLookingSnap(task.TaskId, bytes, hash));
    }

    static async Task InsertOfficialScopeSnapshotAsync(
        IProductionWorkStore store,
        byte[] bytes,
        string provider = "google",
        string model = "veo-3.1",
        string? story = null)
    {
        var engine = new ProductionWorkExecutionEngine(store);
        var req = await engine.CreateRequestAsync(
            RealVertexExecutionV1Lock.OfficialScope, ProductionActions.CreateVideo, "ck18a-mismatch",
            "mismatch-" + Guid.NewGuid().ToString("N"));
        var task = await engine.CreateTaskAsync(req.RequestId);
        var hash = KitVideoIntegrityRules.Sha256Hex(bytes);
        await store.InsertSnapshotAsync(OfficialLookingSnap(task.TaskId, bytes, hash, provider, model, story));
    }

    static ProductionTaskInputSnapshot OfficialLookingSnap(
        Guid taskId,
        byte[] bytes,
        string hash,
        string provider = "google",
        string model = "veo-3.1",
        string? story = null)
    {
        var snap = new ProductionTaskInputSnapshot(
            Guid.NewGuid(),
            taskId,
            story ?? ProductionEp01Sh02IngressCanon.Story,
            ProductionEp01Sh02IngressCanon.ActingBeat,
            ProductionEp01Sh02IngressCanon.VisualContract,
            "",
            "",
            "",
            ShotExecutionCompiler.Hash(story ?? ProductionEp01Sh02IngressCanon.Story),
            ShotExecutionCompiler.Hash(ProductionEp01Sh02IngressCanon.ActingBeat),
            ShotExecutionCompiler.Hash(ProductionEp01Sh02IngressCanon.VisualContract),
            "",
            hash,
            "",
            "",
            "",
            ShotExecutionCompiler.Hash(OfficialCreateVideoFreezeV1.OfficialTiming),
            provider,
            model,
            "",
            DateTimeOffset.UtcNow,
            OfficialCreateVideoFreezeV1.OfficialKeyframeId,
            "16:9",
            OfficialCreateVideoFreezeV1.OfficialDurationSec,
            ShotExecutionCompiler.Version,
            [OfficialCreateVideoFreezeV1.OfficialKeyframeId],
            OfficialCreateVideoFreezeV1.OfficialPerformanceId,
            OfficialCreateVideoFreezeV1.OfficialPerformanceHash,
            null);
        return snap with { SnapshotHash = ShotExecutionCompiler.SnapshotHash(snap) };
    }

    static ProductionScope OtherBuildScope() =>
        new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), "EP01", "SC01", RealVertexExecutionV1Lock.ShotCode);

    static ProductionScope OtherShotScope() =>
        new(RealVertexExecutionV1Lock.BuildId, "EP01", "SC01", "EP01-SC01-SH99");

    sealed record ReadyState(
        InMemoryProductionWorkStore Store,
        ProductionTaskInputSnapshot? Frozen,
        ShotExecutionInput Live,
        byte[] Bytes,
        ProductionExecutionPermit? Permit,
        CountingProductionExecutionPermitStore Permits,
        ProductionAiSelection Selection,
        ProductionVeoTestGraph Graph,
        string? ExpectedHash);
}
