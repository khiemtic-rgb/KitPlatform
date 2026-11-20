using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionOfficialVeoLiveOfficialContractMaterializationV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_OFFICIAL_VEO_LIVE_OFFICIAL_CONTRACT_MATERIALIZATION_V1";

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
        await T_MaterializeAndIdempotent(Ok);
        await T_WrongBuild(Ok);
        await T_WrongShot(Ok);
        await T_WrongActionDoesNotSnapshotImage(Ok);
        await T_ProviderModelMismatch(Ok);
        await T_FrozenMismatchNoPermit(Ok);
        await T_KeyframeMismatch(Ok);
        await T_DuplicateSnapshot(Ok);
        await T_DuplicatePermit(Ok);
        await T_ConsumedPermitNoReplacement(Ok);
        await T_PermitBindingMismatch(Ok);
        await T_AuthDoesNotEnterCk18(Ok);
        await T_SideEffectsZero(Ok);
        T_Ck18aStillGreen(Ok);
    }

    static async Task T_MaterializeAndIdempotent(Action<bool, string> Ok)
    {
        var h = await Harness();
        var first = await Materialize(h);
        Ok(first.Materialized && first.Verdict == OfficialVeoLiveOfficialContractMaterializationV1.VerdictPass,
            "first materialize PASS");
        Ok(first.SnapshotCreated && !first.SnapshotReused && first.SnapshotMethod == OfficialVeoLiveOfficialContractMaterializationV1.FreezeMethod,
            "CK12A FreezeAsync created snapshot");
        Ok(first.PermitCreated && !first.PermitReused && first.PermitMethod == OfficialVeoLiveOfficialContractMaterializationV1.IssueMethod,
            "CK13B IssueAsync issued permit");
        Ok(first.SnapshotAfter == 1 && first.PermitAfter == 1, "exactly one snap + permit");
        Ok(first.FrozenFieldResults.Count == 12 && first.FrozenFieldResults.All(f => f.EndsWith("=PASS", StringComparison.Ordinal)),
            "12-field PASS");
        Ok(first.AuthorizationState == ProductionFailureReasons.ExecutionAuthorized, "CK17 EXECUTION_AUTHORIZED");
        Ok(first.Handoff == OfficialVeoLiveOfficialContractMaterializationV1.ReadyForCk18RealStart, "READY_FOR_CK18_REAL_START");
        Ok(h.Permits.IssueCalls == 1 && h.Permits.ConsumeCalls == 0, "one issue, consume=0");
        var second = await Materialize(h);
        Ok(second.Materialized && second.SnapshotReused && second.PermitReused, "O idempotent reuse");
        Ok(second.SnapshotId == first.SnapshotId && second.PermitId == first.PermitId
            && second.AuthorizationHash == first.AuthorizationHash, "O same identity");
        Ok(await OfficialCreateVideoFreezeV1.CountOfficialCreateVideoSnapshotsAsync(h.Store) == 1, "O no second snapshot");
        Ok(h.Permits.IssueCalls == 1 && h.Permits.ConsumeCalls == 0, "O IssueAsync reuse, no consume");
        Ok((await h.Store.ListAttemptsAsync(first.SnapshotId is Guid id
            ? (await OfficialCreateVideoFreezeV1.FindOfficialCreateVideoSnapshotAsync(h.Store))!.TaskId
            : Guid.Empty)).Count == 0, "no ProductionAttempt");
        AssertZero(Ok, h, "O");
    }

    static async Task T_WrongBuild(Action<bool, string> Ok)
    {
        var h = await Harness();
        var r = await Materialize(h, h.Selection with { BuildId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee") });
        Ok(!r.Materialized && r.SnapshotAfter == 0, "A no snapshot");
        Ok(h.Permits.IssueCalls == 0, "A no permit");
        AssertZero(Ok, h, "A");
    }

    static async Task T_WrongShot(Action<bool, string> Ok)
    {
        var h = await Harness();
        var r = await Materialize(h, h.Selection with { ShotCode = "EP01-SC01-SH99" });
        Ok(!r.Materialized && r.SnapshotAfter == 0, "B no snapshot");
        AssertZero(Ok, h, "B");
    }

    static async Task T_WrongActionDoesNotSnapshotImage(Action<bool, string> Ok)
    {
        var h = await Harness();
        var imageTasks = (await h.Store.ListTasksByScopeAsync(
            RealVertexExecutionV1Lock.BuildId, RealVertexExecutionV1Lock.ShotCode))
            .Count(t => t.Action == ProductionActions.CreateImage);
        var r = await Materialize(h, h.Selection with { Stage = ProductionAiSelectionStages.Voice });
        Ok(!r.Materialized && await OfficialCreateVideoFreezeV1.CountOfficialCreateVideoSnapshotsAsync(h.Store) == 0,
            "C no CREATE_VIDEO snapshot");
        Ok((await h.Store.ListTasksByScopeAsync(RealVertexExecutionV1Lock.BuildId, RealVertexExecutionV1Lock.ShotCode))
            .Count(t => t.Action == ProductionActions.CreateImage) == imageTasks, "C CREATE_IMAGE untouched");
        AssertZero(Ok, h, "C");
    }

    static async Task T_ProviderModelMismatch(Action<bool, string> Ok)
    {
        var h = await Harness();
        var r = await Materialize(h, h.Selection with { ProviderId = "runway", ModelId = "gen4_turbo" });
        Ok(!r.Materialized && r.SnapshotAfter == 0, "D no snapshot");
        AssertZero(Ok, h, "D");
    }

    static async Task T_FrozenMismatchNoPermit(Action<bool, string> Ok)
    {
        var h = await Harness();
        await InsertDriftedOfficialScopeSnapshotAsync(h.Store, h.Bytes);
        var r = await Materialize(h);
        Ok(!r.Materialized && r.Verdict == OfficialVeoLiveOfficialContractMaterializationV1.VerdictSnapshotVerifyFailed,
            "E frozen mismatch");
        Ok(h.Permits.IssueCalls == 0, "E no permit");
        AssertZero(Ok, h, "E");
    }

    static async Task T_KeyframeMismatch(Action<bool, string> Ok)
    {
        var h = await Harness();
        var other = Encoding.UTF8.GetBytes("FAMIXA-KF-18B-OTHER-" + new string('Z', 48));
        var r = await Materialize(h, bytes: other);
        Ok(!r.Materialized && h.Permits.IssueCalls == 0, "F no permit");
        Ok(await OfficialCreateVideoFreezeV1.CountOfficialCreateVideoSnapshotsAsync(h.Store) == 0, "F no snapshot");
        AssertZero(Ok, h, "F");
    }

    static async Task T_DuplicateSnapshot(Action<bool, string> Ok)
    {
        var h = await Harness();
        await InsertOfficialScopeSnapshotAsync(h.Store, h.Bytes);
        await InsertOfficialScopeSnapshotAsync(h.Store, h.Bytes);
        var r = await Materialize(h);
        Ok(!r.Materialized && r.Verdict == OfficialVeoLiveOfficialContractMaterializationV1.VerdictAmbiguous, "G fail closed");
        Ok(h.Permits.IssueCalls == 0, "G no permit");
        AssertZero(Ok, h, "G");
    }

    static async Task T_DuplicatePermit(Action<bool, string> Ok)
    {
        var h = await Harness();
        h.Permits.AddExisting(FakeIssued("ONE"));
        h.Permits.AddExisting(FakeIssued("TWO"));
        var r = await Materialize(h);
        Ok(!r.Materialized && r.Verdict == OfficialVeoLiveOfficialContractMaterializationV1.VerdictAmbiguous, "H fail closed");
        Ok(h.Permits.IssueCalls == 0, "H no latest-pick");
        Ok(await OfficialCreateVideoFreezeV1.CountOfficialCreateVideoSnapshotsAsync(h.Store) == 0, "H no snapshot write");
        AssertZero(Ok, h, "H");
    }

    static async Task T_ConsumedPermitNoReplacement(Action<bool, string> Ok)
    {
        var h = await Harness();
        h.Permits.AddExisting(FakeIssued("CONSUMED") with
        {
            Status = ProductionExecutionPermitStatuses.Consumed,
            ConsumedAt = DateTimeOffset.UnixEpoch,
        });
        var r = await Materialize(h);
        Ok(!r.Materialized && r.Verdict == OfficialVeoLiveOfficialContractMaterializationV1.VerdictPermitFailed,
            "I no replacement");
        Ok(h.Permits.IssueCalls == 0, "I no issue");
        Ok(await OfficialCreateVideoFreezeV1.CountOfficialCreateVideoSnapshotsAsync(h.Store) == 0, "I no snapshot");
        AssertZero(Ok, h, "I");
    }

    static async Task T_PermitBindingMismatch(Action<bool, string> Ok)
    {
        var h = await Harness();
        h.Permits.AddExisting(FakeIssued("OTHER_BINDING"));
        var r = await Materialize(h);
        Ok(!r.Materialized && (r.Verdict == OfficialVeoLiveOfficialContractMaterializationV1.VerdictPermitFailed
            || r.Verdict == OfficialVeoLiveOfficialContractMaterializationV1.VerdictPermitVerifyFailed),
            "J permit binding fail-closed");
        Ok(h.Permits.IssueCalls == 0 || r.PermitCreated == false, "J no replacement permit");
        AssertZero(Ok, h, "J");
    }

    static async Task T_AuthDoesNotEnterCk18(Action<bool, string> Ok)
    {
        var h = await Harness();
        var r = await Materialize(h);
        Ok(r.Materialized && r.AuthorizationState == ProductionFailureReasons.ExecutionAuthorized, "K authorized");
        Ok(h.Graph.Transport.StartCount == 0, "K no CK18 Start");
        RealVertexExecutionV1Lock.ResetForTests();
        Ok(RealVertexExecutionV1Lock.StartCount == 0, "K lock StartCount=0");
        AssertZero(Ok, h, "K");
    }

    static async Task T_SideEffectsZero(Action<bool, string> Ok)
    {
        var h = await Harness();
        var closed = new ProductionVeoHttpAuthenticator();
        var r = await Materialize(h);
        Ok(r.Materialized, "L/M/N materialized");
        Ok(closed.GetAccessTokenCalls == 0, "L unused GetAccessToken=0");
        Ok(r.Authorization?.Authenticator?.Authenticator is ProductionVeoHttpAuthenticator prod
            && prod.GetAccessTokenCalls == 0, "L composed GetAccessToken=0");
        Ok((r.Authorization?.Resolver?.Authenticator?.Calls ?? -1) == 0, "L resolver OAuth=0");
        Ok((r.Authorization?.Resolver?.Transport?.HttpCallCount ?? -1) == 0, "M HTTP=0");
        Ok((r.Authorization?.Resolver?.Handler?.PredictCalls ?? -1) == 0, "N Predict=0");
        Ok((r.Authorization?.Resolver?.Handler?.FetchCalls ?? -1) == 0, "N Fetch=0");
        AssertZero(Ok, h, "LMN");
    }

    static void T_Ck18aStillGreen(Action<bool, string> Ok)
    {
        var ck18a = ProductionOfficialVeoLiveOfficialContractBindV1Regression.Run();
        Ok(ck18a.Count == 0, ck18a.Count == 0 ? "CK18A still green" : "CK18A:" + string.Join(",", ck18a));
    }

    static Task<OfficialVeoLiveOfficialContractMaterializationResult> Materialize(
        ReadyState h, ProductionAiSelection? selection = null, byte[]? bytes = null)
    {
        var pin = OfficialCreateVideoFreezeCredentialFixture.Pin();
        return OfficialVeoLiveOfficialContractMaterializationV1.MaterializeAsync(
            new OfficialVeoLiveOfficialContractMaterializationRequest(
                h.Store,
                h.Permits,
                selection ?? h.Selection,
                bytes ?? h.Bytes,
                ProjectId: ProductionOfficialVeoHttpCompositionV1Regression.ComposeProject,
                Credentials: pin.Resolver,
                Credential: pin.Reference));
    }

    static void AssertZero(Action<bool, string> Ok, ReadyState h, string tag)
    {
        Ok(h.Graph.Transport.StartCount == 0, tag + " Start=0");
        Ok(h.Graph.Transport.PollCount == 0, tag + " Poll=0");
        Ok(h.Graph.Handler.Requests.Count == 0, tag + " HTTP=0");
        Ok(h.Graph.Authenticator.Calls == 0, tag + " OAuth=0");
        Ok(h.Permits.ConsumeCalls == 0, tag + " consume=0");
    }

    static ProductionExecutionPermit FakeIssued(string reason) =>
        new(
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
            reason,
            ProductionExecutionPermitCanon.HashOf(
                RealVertexExecutionV1Lock.BuildId,
                RealVertexExecutionV1Lock.ShotCode,
                ProductionActions.CreateVideo,
                RealVertexExecutionV1Lock.ProviderId,
                RealVertexExecutionV1Lock.ModelId));

    static async Task<ReadyState> Harness()
    {
        var store = new InMemoryProductionWorkStore();
        var permits = new CountingProductionExecutionPermitStore();
        var graph = ProductionVeoReadinessComposition.CreateClosedTestGraph();
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-KF-18B-" + new string('K', 48));
        var hash = KitVideoIntegrityRules.Sha256Hex(bytes);
        var engine = new ProductionWorkExecutionEngine(store);
        var req = await engine.CreateRequestAsync(
            RealVertexExecutionV1Lock.OfficialScope, ProductionActions.CreateImage, "ck18b",
            "kf-18b-" + Guid.NewGuid().ToString("N"));
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
        var selection = new ProductionAiSelection(
            OfficialCreateVideoPermitAuthorizationV1.OfficialSelectionId,
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode, "EP01", "SC01",
            ProductionAiSelectionStages.Video,
            "google", "veo-3.1",
            ProductionAiSelectionModes.Explicit,
            "checkpoint-18b",
            OfficialVeoExecutionAuthorizationV1.OfficialSelectedAt,
            "DIRECTOR_SELECTION",
            ProductionAiSelectionStatuses.Active,
            "SELECTION_HASH_V1:ck18b");
        return new ReadyState(store, permits, selection, bytes, graph);
    }

    static async Task InsertOfficialScopeSnapshotAsync(IProductionWorkStore store, byte[] bytes)
    {
        var engine = new ProductionWorkExecutionEngine(store);
        var req = await engine.CreateRequestAsync(
            RealVertexExecutionV1Lock.OfficialScope, ProductionActions.CreateVideo, "ck18b-dup",
            "dup-" + Guid.NewGuid().ToString("N"));
        var task = await engine.CreateTaskAsync(req.RequestId);
        var hash = KitVideoIntegrityRules.Sha256Hex(bytes);
        await store.InsertSnapshotAsync(OfficialLookingSnap(task.TaskId, hash));
    }

    static async Task InsertDriftedOfficialScopeSnapshotAsync(IProductionWorkStore store, byte[] bytes)
    {
        var engine = new ProductionWorkExecutionEngine(store);
        var req = await engine.CreateRequestAsync(
            RealVertexExecutionV1Lock.OfficialScope, ProductionActions.CreateVideo, "ck18b-drift",
            "drift-" + Guid.NewGuid().ToString("N"));
        var task = await engine.CreateTaskAsync(req.RequestId);
        var hash = KitVideoIntegrityRules.Sha256Hex(bytes);
        await store.InsertSnapshotAsync(OfficialLookingSnap(task.TaskId, hash, ProductionEp01Sh02IngressCanon.Story + " drift"));
    }

    static ProductionTaskInputSnapshot OfficialLookingSnap(Guid taskId, string hash, string? story = null)
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
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId,
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

    sealed record ReadyState(
        InMemoryProductionWorkStore Store,
        CountingProductionExecutionPermitStore Permits,
        ProductionAiSelection Selection,
        byte[] Bytes,
        ProductionVeoTestGraph Graph);
}
