using System.Net;
using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionOfficialVeoPollRecoverMaterializationV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_OFFICIAL_VEO_POLL_RECOVER_MATERIALIZATION_V1";
    const string OperationName = "projects/ck16-compose-project/locations/us-central1/operations/op-ck19";

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
        await T_SuccessAndIdempotent(Ok);
        await T_MissingOperation(Ok);
        await T_MismatchedOperation(Ok);
        await T_MismatchedProvider(Ok);
        await T_MissingOutput(Ok);
        await T_UnsupportedOutput(Ok);
        await T_Malformed(Ok);
        await T_Timeout(Ok);
        await T_ProviderError(Ok);
        await T_StartReplayBlocked(Ok);
        await T_MissingSnapshot(Ok);
        await T_SnapshotHashMismatch(Ok);
        await T_PermitNotMutated(Ok);
        T_Ck18StillGreen(Ok);
    }

    static async Task T_SuccessAndIdempotent(Action<bool, string> Ok)
    {
        var ready = await ReadyDone();
        var first = await OfficialVeoPollRecoverMaterializationV1.PollAsync(ready.Poll);
        Ok(first.Succeeded && first.ArtifactId is not null, "first Poll materializes");
        Ok(first.PollCount >= 1 && first.StartCount == 0 && ready.Handler.PredictCalls == 1,
            "Poll >= 1 and Start stays 1");
        Ok(ready.Handler.FetchCalls >= 1, "real fetchPredictOperation");
        Ok(first.ArtifactSha256 == KitVideoIntegrityRules.Sha256Hex(ready.Mp4)
            && first.ArtifactBytes == ready.Mp4.Length, "exact provider bytes hashed");
        var arts = await ready.Store.ListArtifactsByTaskAsync(ready.Frozen.TaskId);
        Ok(arts.Count(a => a.VerificationStatus == ProductionArtifactStatuses.Verified) == 1, "one verified artifact");
        var second = await OfficialVeoPollRecoverMaterializationV1.PollAsync(ready.Poll);
        Ok(second.Replayed && second.ArtifactId == first.ArtifactId, "I second invocation replays artifact");
        Ok(ready.Handler.PredictCalls == 1 && second.StartCount == 0 && ready.Permits.ConsumeCalls == 1
            && ready.Permits.IssueCalls == 1, "no second Start / job / permit");
        Ok((await ready.Store.ListArtifactsByTaskAsync(ready.Frozen.TaskId))
            .Count(a => a.VerificationStatus == ProductionArtifactStatuses.Verified) == 1, "no second artifact");
        Ok(await OfficialCreateVideoFreezeV1.CountOfficialCreateVideoSnapshotsAsync(ready.Store) == 1, "no second snapshot");
        var restart = new OfficialVeoStartLedger();
        restart.TryClaim(ready.Authz.AuthorizationHash!, ready.Permit.PermitId, "REQ_HASH_V1:replay");
        restart.BindAttempt(ready.Authz.AuthorizationHash!, ready.Permit.PermitId, first.AttemptId);
        restart.Complete(ready.Authz.AuthorizationHash!, ready.Permit.PermitId, OfficialVeoStartOutcomes.Succeeded,
            first.ProviderOperationName, first.EncodedOperationId, 200, null);
        var recovered = await OfficialVeoPollRecoverMaterializationV1.PollAsync(ready.Poll with { Ledger = restart });
        Ok(recovered.Replayed && recovered.ArtifactId == first.ArtifactId, "restart recover replays terminal");
    }

    static async Task T_MissingOperation(Action<bool, string> Ok)
    {
        var ready = await ReadyDone();
        var r = await OfficialVeoPollRecoverMaterializationV1.PollAsync(ready.Poll with
        {
            ExpectedOperationName = "",
            ExpectedEncodedOperationId = "",
        });
        Ok(!r.Succeeded && r.BlockReason == ProductionFailureReasons.UnknownProvider, "A missing operation");
        Ok(ready.Handler.FetchCalls == 0 && ready.Handler.PredictCalls == 1, "A no Poll / no Start");
    }

    static async Task T_MismatchedOperation(Action<bool, string> Ok)
    {
        var ready = await ReadyDone();
        var r = await OfficialVeoPollRecoverMaterializationV1.PollAsync(ready.Poll with
        {
            ExpectedOperationName = "projects/other/locations/us-central1/operations/nope",
        });
        Ok(!r.Succeeded && r.BlockReason == ProductionFailureReasons.FailedIdentity, "B mismatched operation");
        Ok(ready.Handler.FetchCalls == 0, "B no Poll");
    }

    static async Task T_MismatchedProvider(Action<bool, string> Ok)
    {
        var ready = await ReadyDone();
        var sel = ready.Poll.AuthorizationRequest.Selection! with { ProviderId = "runway", ModelId = "gen4_turbo" };
        var r = await OfficialVeoPollRecoverMaterializationV1.PollAsync(ready.Poll with
        {
            AuthorizationRequest = ready.Poll.AuthorizationRequest with { Selection = sel },
        });
        Ok(!r.Succeeded && r.BlockReason == ProductionFailureReasons.ProviderModelMismatch, "C mismatched provider/model");
        Ok(ready.Handler.FetchCalls == 0, "C no Poll");
    }

    static async Task T_MissingOutput(Action<bool, string> Ok)
    {
        var ready = await ReadyPoll("""{"done":true,"response":{"videos":[{}]}}""");
        var r = await OfficialVeoPollRecoverMaterializationV1.PollAsync(ready.Poll);
        Ok(!r.Succeeded && r.Outcome == ProductionFailureReasons.ProviderOutputMissing, "D PROVIDER_OUTPUT_MISSING");
        Ok(ready.Handler.PredictCalls == 1 && r.StartCount == 0, "D no Start");
    }

    static async Task T_UnsupportedOutput(Action<bool, string> Ok)
    {
        var ready = await ReadyPoll("""{"done":true,"response":{"videos":[{"gcsUri":"gs://bucket/out.mp4"}]}}""");
        var r = await OfficialVeoPollRecoverMaterializationV1.PollAsync(ready.Poll);
        Ok(!r.Succeeded && r.Outcome == ProductionFailureReasons.ProviderOutputUnsupported, "E PROVIDER_OUTPUT_UNSUPPORTED");
        Ok(ready.Handler.PredictCalls == 1, "E no Start");
    }

    static async Task T_Malformed(Action<bool, string> Ok)
    {
        var ready = await ReadyPoll("not-json{");
        var r = await OfficialVeoPollRecoverMaterializationV1.PollAsync(ready.Poll);
        Ok(!r.Succeeded && r.Outcome == ProductionFailureReasons.InvalidProviderResponse, "F INVALID_PROVIDER_RESPONSE");
    }

    static async Task T_Timeout(Action<bool, string> Ok)
    {
        var ready = await ReadyPoll("""{"done":false}""", max: 2);
        var r = await OfficialVeoPollRecoverMaterializationV1.PollAsync(ready.Poll);
        Ok(!r.Succeeded && r.Outcome == OfficialVeoPollStates.PollTimeout
            && r.BlockReason == ProductionFailureReasons.PollTimeout, "G POLL_TIMEOUT");
        Ok(r.PollCount == 2 && ready.Handler.PredictCalls == 1, "G bounded Poll, no Start");
        var attempt = await ready.Store.GetAttemptAsync(ready.StartResult.AttemptId!.Value);
        Ok(attempt!.Status == ProductionAttemptStatuses.ProviderSubmitted, "G timeout leaves attempt submitted");
    }

    static async Task T_ProviderError(Action<bool, string> Ok)
    {
        var ready = await ReadyPoll("""{"error":{"message":"VERTEX_FAILED"}}""");
        var r = await OfficialVeoPollRecoverMaterializationV1.PollAsync(ready.Poll);
        Ok(!r.Succeeded && r.Outcome == OfficialVeoPollStates.ProviderFailed, "H provider terminal error");
        var attempt = await ready.Store.GetAttemptAsync(ready.StartResult.AttemptId!.Value);
        Ok(attempt!.Status == ProductionAttemptStatuses.ProviderFailed, "H attempt PROVIDER_FAILED");
        Ok(ready.Handler.PredictCalls == 1, "H no Start");
    }

    static async Task T_StartReplayBlocked(Action<bool, string> Ok)
    {
        var ready = await ReadyDone();
        _ = await OfficialVeoPollRecoverMaterializationV1.PollAsync(ready.Poll);
        var again = await OfficialVeoRealVertexStartV1.StartAsync(ready.Start);
        Ok(again.Replayed && again.BlockReason == ProductionFailureReasons.StartReplay, "J START_REPLAY");
        Ok(ready.Handler.PredictCalls == 1, "J no second Start");
    }

    static async Task T_MissingSnapshot(Action<bool, string> Ok)
    {
        var store = new InMemoryProductionWorkStore();
        var engine = new ProductionWorkExecutionEngine(store);
        var req = await engine.CreateRequestAsync(
            RealVertexExecutionV1Lock.OfficialScope, ProductionActions.CreateVideo, "ck19-miss",
            "ck19-miss-" + Guid.NewGuid().ToString("N"));
        var task = await engine.CreateTaskAsync(req.RequestId);
        await store.UpdateTaskAsync(task with { ProviderId = "google", ModelId = "veo-3.1" });
        var attempt = await engine.StartAttemptAsync(task.TaskId, "deadbeef");
        await engine.SubmitProviderJobAsync(attempt.AttemptId, VeoVertexOperationId.Encode(OperationName));
        var ledger = new OfficialVeoStartLedger();
        ledger.TryClaim("AUTH_HASH_V1:dead", Guid.NewGuid(), "REQ_HASH_V1:x");
        var ready = await ReadyDone();
        var r = await OfficialVeoPollRecoverMaterializationV1.PollAsync(ready.Poll with
        {
            Store = store,
            ExpectedTaskId = task.TaskId,
            ExpectedAttemptId = attempt.AttemptId,
            ExpectedSnapshotId = Guid.NewGuid(),
        });
        Ok(!r.Succeeded && r.BlockReason == ProductionFailureReasons.InputNotFrozen, "K INPUT_NOT_FROZEN");
        Ok(ready.Handler.FetchCalls == 0, "K no Poll");
    }

    static async Task T_SnapshotHashMismatch(Action<bool, string> Ok)
    {
        var ready = await ReadyDone();
        var r = await OfficialVeoPollRecoverMaterializationV1.PollAsync(ready.Poll with
        {
            ExpectedSnapshotHash = "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
        });
        Ok(!r.Succeeded && r.BlockReason == ProductionFailureReasons.FreezeMismatch, "L FREEZE_MISMATCH");
        Ok(ready.Handler.FetchCalls == 0, "L no Poll");
    }

    static async Task T_PermitNotMutated(Action<bool, string> Ok)
    {
        var ready = await ReadyDone();
        var beforeIssue = ready.Permits.IssueCalls;
        var beforeConsume = ready.Permits.ConsumeCalls;
        _ = await OfficialVeoPollRecoverMaterializationV1.PollAsync(ready.Poll);
        Ok(ready.Permits.IssueCalls == beforeIssue && ready.Permits.ConsumeCalls == beforeConsume,
            "M CK19 never issues or consumes a permit");
    }

    static void T_Ck18StillGreen(Action<bool, string> Ok)
    {
        var ck18 = ProductionOfficialVeoRealVertexStartV1Regression.Run();
        Ok(ck18.Count == 0, ck18.Count == 0 ? "CK18 still green" : "CK18:" + string.Join(",", ck18));
    }

    static Task<ReadyState> ReadyDone() =>
        ReadyPoll(DoneBody(ProductionTestMp4.Encode("ck19-out", 10)));

    static async Task<ReadyState> ReadyPoll(string pollBody, int max = 90)
    {
        var mp4 = ProductionTestMp4.Encode("ck19-out", 10);
        var store = new InMemoryProductionWorkStore();
        var permits = new CountingProductionExecutionPermitStore();
        var files = new InMemoryProductionArtifactFileStore();
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-KF-19-" + new string('K', 48));
        var hash = KitVideoIntegrityRules.Sha256Hex(bytes);
        var engine = new ProductionWorkExecutionEngine(store);
        var req = await engine.CreateRequestAsync(
            RealVertexExecutionV1Lock.OfficialScope, ProductionActions.CreateImage, "ck19",
            "kf-19-" + Guid.NewGuid().ToString("N"));
        var task = await engine.CreateTaskAsync(req.RequestId);
        await store.InsertArtifactAsync(new ProductionArtifact(
            OfficialCreateVideoFreezeV1.OfficialKeyframeId,
            task.TaskId, Guid.NewGuid(), ProductionArtifactRoles.Keyframe,
            "image/png", bytes.Length, 0, hash, "kf/EP01-SC01-SH02.png", null,
            DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified, hash));
        var sources = OfficialCreateVideoFreezeV1.FromEp01Sh02Canon(bytes, hash);
        var frozen = (await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin())).Snapshot;
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
            ProductionAiSelectionStages.Video, "google", "veo-3.1",
            ProductionAiSelectionModes.Explicit, "checkpoint-19",
            OfficialVeoExecutionAuthorizationV1.OfficialSelectedAt,
            "DIRECTOR_SELECTION", ProductionAiSelectionStatuses.Active, "SELECTION_HASH_V1:ck19");
        var issued = await OfficialCreateVideoPermitAuthorizationV1.IssueAsync(
            permits, new OfficialCreateVideoPermitIssueRequest(
                RealVertexExecutionV1Lock.OfficialScope, selection, frozen, live));
        var authRequest = new OfficialVeoExecutionAuthorizationRequest(
            RealVertexExecutionV1Lock.OfficialScope, ProductionActions.CreateVideo, selection, frozen, live,
            bytes, "image/png", CostPolicyDecision.FromCatalog("google", "veo-3.1", 10),
            FamixaVeo31OfficialPricing.ProductionDefault, issued.Permit,
            ProjectId: ProductionOfficialVeoHttpCompositionV1Regression.ComposeProject);
        var authz = OfficialVeoExecutionAuthorizationV1.Evaluate(authRequest);
        var handler = new FakeVeoHttpMessageHandler
        {
            OnSend = cap => cap.Url.Contains(":predictLongRunning", StringComparison.Ordinal)
                ? FakeVeoHttpMessageHandler.Json(HttpStatusCode.OK, $$"""{"name":"{{OperationName}}"}""")
                : FakeVeoHttpMessageHandler.Json(HttpStatusCode.OK, pollBody),
        };
        var auth = new FakeVeoHttpAuthenticator();
        var inner = new HttpVeoMotionTransport(new HttpClient(handler), auth, authz.HttpComposition!.Settings!);
        var ledger = new OfficialVeoStartLedger();
        var startReq = new OfficialVeoStartRequest(authRequest, authz, store, permits, ledger, inner, auth);
        var started = await OfficialVeoRealVertexStartV1.StartAsync(startReq);
        var pollOnly = new OfficialVeoPollOnlyTransport(inner);
        var poll = new OfficialVeoPollRequest(
            authRequest, authz, store, permits, ledger, pollOnly, auth, files,
            frozen.TaskId, started.AttemptId!.Value, authz.AuthorizationHash!,
            OperationName, VeoVertexOperationId.Encode(OperationName),
            frozen.SnapshotId, frozen.SnapshotHash, issued.Permit.PermitId, issued.Permit.PermitHash,
            max, TimeSpan.Zero);
        return new ReadyState(store, frozen, issued.Permit, permits, authz, startReq, started, handler, poll, mp4);
    }

    static string DoneBody(byte[] mp4) =>
        "{\"done\":true,\"response\":{\"videos\":[{\"bytesBase64Encoded\":\"" + Convert.ToBase64String(mp4) + "\"}]}}";

    sealed record ReadyState(
        InMemoryProductionWorkStore Store,
        ProductionTaskInputSnapshot Frozen,
        ProductionExecutionPermit Permit,
        CountingProductionExecutionPermitStore Permits,
        OfficialVeoExecutionAuthorizationResult Authz,
        OfficialVeoStartRequest Start,
        OfficialVeoStartResult StartResult,
        FakeVeoHttpMessageHandler Handler,
        OfficialVeoPollRequest Poll,
        byte[] Mp4);
}
