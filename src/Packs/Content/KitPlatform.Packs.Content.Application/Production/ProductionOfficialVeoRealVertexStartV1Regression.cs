using System.Net;
using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionOfficialVeoRealVertexStartV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_OFFICIAL_VEO_REAL_VERTEX_START_V1";
    const string OperationName = "projects/ck16-compose-project/locations/us-central1/operations/op-ck18";

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
        await T_OneShotStart(Ok);
        await T_AuthHashMismatch(Ok);
        await T_FrozenMismatch(Ok);
        await T_MissingPermit(Ok);
        await T_ConsumedPermit(Ok);
        await T_PermitConflict(Ok);
        await T_ExactRequestAndDurationAndKeyframe(Ok);
        await T_NoAutomaticRetry(Ok);
        await T_AmbiguousOutcome(Ok);
        await T_NotSentDoesNotConsume(Ok);
        await T_ConcurrentDuplicate(Ok);
        await T_ReplayAfterStart(Ok);
        await T_SecretRedactionAndProvenance(Ok);
        await T_PermitConsumptionSemantics(Ok);
        T_RetryConfiguration(Ok);
        T_Ck17StillGreen(Ok);
    }

    static async Task T_OneShotStart(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var first = await OfficialVeoRealVertexStartV1.StartAsync(ready.Start);
        Ok(first.Started && first.Outcome == OfficialVeoStartOutcomes.Running,
            "one-shot Start STARTED/RUNNING");
        Ok(first.StartAttemptCount == 1 && first.ProviderJobCount == 1, "Start=1 job=1");
        Ok(first.PermitStateBefore == ProductionExecutionPermitStatuses.Issued
            && first.PermitStateAfter == ProductionExecutionPermitStatuses.Consumed,
            "permit consumed with Start");
        Ok(first.ProviderOperationName == OperationName
            && first.EncodedOperationId == VeoVertexOperationId.Encode(OperationName),
            "operation identity persisted");
        Ok(ready.Handler.PredictCalls == 1 && ready.Handler.FetchCalls == 0, "START only — no poll");
        Ok(first.RetryCount == 0, "retry count 0");
        Ok(ready.Frozen.DurationSec == 10 && first.OsDurationSec == 10 && first.ProviderDurationSec == 8,
            "DurationSec stays 10; wire 8");
        Ok(await OfficialCreateVideoFreezeV1.CountOfficialCreateVideoSnapshotsAsync(ready.Store) == 1,
            "no second snapshot");
        Ok(ready.Permits.IssueCalls == 1, "no second permit");
    }

    static async Task T_AuthHashMismatch(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var expected = ready.Authz with { AuthorizationHash = "AUTH_HASH_V1:deadbeef" };
        var r = await OfficialVeoRealVertexStartV1.StartAsync(ready.Start with { ExpectedAuthorization = expected });
        Ok(!r.Started && r.BlockReason == ProductionFailureReasons.ExecutionAuthorizationRequired,
            "authorization hash mismatch fail-closed");
        Ok(ready.Handler.PredictCalls == 0 && ready.Permits.ConsumeCalls == 0, "no provider / permit activity");
    }

    static async Task T_FrozenMismatch(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = await OfficialVeoRealVertexStartV1.StartAsync(ready.Start with
        {
            AuthorizationRequest = ready.AuthRequest with
            {
                Live = ready.Live with { Story = ready.Live.Story + " drift" },
            },
        });
        Ok(!r.Started && r.BlockReason == ProductionFailureReasons.InputSnapshotMismatch, "frozen snapshot mismatch");
        Ok(ready.Handler.PredictCalls == 0 && ready.Permits.ConsumeCalls == 0, "no Start / consume");
    }

    static async Task T_MissingPermit(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = await OfficialVeoRealVertexStartV1.StartAsync(ready.Start with
        {
            AuthorizationRequest = ready.AuthRequest with { Permit = null },
        });
        Ok(!r.Started && !string.IsNullOrWhiteSpace(r.BlockReason), "missing permit blocked");
        Ok(ready.Handler.PredictCalls == 0 && ready.Permits.ConsumeCalls == 0, "no Start / consume");
    }

    static async Task T_ConsumedPermit(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var consumed = ready.Permit with
        {
            Status = ProductionExecutionPermitStatuses.Consumed,
            ConsumedAt = DateTimeOffset.UnixEpoch,
        };
        var r = await OfficialVeoRealVertexStartV1.StartAsync(ready.Start with
        {
            AuthorizationRequest = ready.AuthRequest with { Permit = consumed },
        });
        Ok(!r.Started, "consumed permit blocked");
        Ok(ready.Handler.PredictCalls == 0 && ready.Permits.ConsumeCalls == 0, "store not consumed again");
    }

    static async Task T_PermitConflict(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = await OfficialVeoRealVertexStartV1.StartAsync(ready.Start with
        {
            AuthorizationRequest = ready.AuthRequest with
            {
                Permit = ready.Permit with { Reason = "OTHER_BINDING" },
            },
        });
        Ok(!r.Started && r.BlockReason is ProductionFailureReasons.PermitConflict
            or ProductionFailureReasons.ExecutionAuthorizationRequired, "permit conflict");
        Ok(ready.Handler.PredictCalls == 0, "no Start on permit conflict");
    }

    static async Task T_ExactRequestAndDurationAndKeyframe(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = await OfficialVeoRealVertexStartV1.StartAsync(ready.Start);
        var cap = ready.Handler.Last!;
        Ok(r.Started && cap.Url == r.StartUrl && cap.Url.Contains(":predictLongRunning", StringComparison.Ordinal),
            "exact CK16 Start URL");
        using var doc = System.Text.Json.JsonDocument.Parse(cap.Body);
        var inst = doc.RootElement.GetProperty("instances")[0];
        var p = doc.RootElement.GetProperty("parameters");
        var img = Convert.FromBase64String(inst.GetProperty("image").GetProperty("bytesBase64Encoded").GetString()!);
        Ok(inst.GetProperty("prompt").GetString() == VeoMotionPrompt.FromFrozen(ready.Frozen), "prompt from frozen");
        Ok(img.SequenceEqual(ready.Bytes) && KitVideoIntegrityRules.Sha256Hex(img) == ready.Frozen.KeyframeHash,
            "exact frozen keyframe hash");
        Ok(p.GetProperty("durationSeconds").GetInt32() == 8
            && ready.Frozen.DurationSec == 10
            && r.OsDurationSec == 10
            && r.ProviderDurationSec == 8, "10 → 8 mapping; snapshot stays 10");
        Ok(r.RequestPayloadHash == "REQ_HASH_V1:" + KitVideoIntegrityRules.Sha256Hex(System.Text.Encoding.UTF8.GetBytes(cap.Body)),
            "payload hash == outbound body");
        Ok(!cap.Body.Contains("seed", StringComparison.OrdinalIgnoreCase)
            && !cap.Body.Contains("gs://", StringComparison.Ordinal),
            "no seed / GCS");
    }

    static async Task T_NoAutomaticRetry(Action<bool, string> Ok)
    {
        var ready = await Ready(HttpStatusCode.InternalServerError);
        var r = await OfficialVeoRealVertexStartV1.StartAsync(ready.Start);
        Ok(!r.Started && ready.Handler.PredictCalls == 1, "500 is one Start, no retry");
        Ok(r.RetryCount == 0 && r.AutomaticRetryConfiguration == 0, "automatic retry configuration = 0");
    }

    static async Task T_AmbiguousOutcome(Action<bool, string> Ok)
    {
        var ready = await Ready();
        ready.Handler.ThrowCanceled = true;
        var r = await OfficialVeoRealVertexStartV1.StartAsync(ready.Start);
        Ok(!r.Started && r.Outcome == OfficialVeoStartOutcomes.Unknown, "ambiguous → START_OUTCOME_UNKNOWN");
        Ok(ready.Handler.PredictCalls == 1, "no second Start after unknown");
        var replay = await OfficialVeoRealVertexStartV1.StartAsync(ready.Start);
        Ok(!replay.Started && ready.Handler.PredictCalls == 1, "unknown claim blocks second Start");
    }

    static async Task T_NotSentDoesNotConsume(Action<bool, string> Ok)
    {
        var ready = await Ready();
        ready.Transport.SuppressSend = true;
        var r = await OfficialVeoRealVertexStartV1.StartAsync(ready.Start);
        Ok(!r.Started && r.Outcome == OfficialVeoStartOutcomes.NotSent, "definitive NOT_SENT");
        Ok(ready.Permits.ConsumeCalls == 0 && r.PermitStateAfter == ProductionExecutionPermitStatuses.Issued,
            "NOT_SENT does not consume permit");
        Ok(ready.Handler.PredictCalls == 0, "no provider job");
        ready.Transport.SuppressSend = false;
        var rematch = await OfficialVeoRealVertexStartV1.StartAsync(ready.Start);
        Ok(rematch.Started && ready.Handler.PredictCalls == 1,
            "proven NOT_SENT rematch is the one authorized Start");
    }

    static async Task T_ConcurrentDuplicate(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var a = OfficialVeoRealVertexStartV1.StartAsync(ready.Start);
        var b = OfficialVeoRealVertexStartV1.StartAsync(ready.Start);
        await Task.WhenAll(a, b);
        var started = new[] { a.Result, b.Result }.Count(x => x.Started);
        var jobs = ready.Handler.PredictCalls;
        Ok(started == 1 && jobs == 1, "concurrent duplicate Start = 1");
        Ok(new[] { a.Result, b.Result }.Any(x => !x.Started), "loser fails before second provider Start");
    }

    static async Task T_ReplayAfterStart(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var first = await OfficialVeoRealVertexStartV1.StartAsync(ready.Start);
        var second = await OfficialVeoRealVertexStartV1.StartAsync(ready.Start);
        Ok(first.Started && second.Replayed && !second.Started, "replay after successful Start");
        Ok(ready.Handler.PredictCalls == 1 && ready.Permits.ConsumeCalls == 1, "no second job / consume");
        Ok(second.ProviderOperationName == first.ProviderOperationName
            || second.Outcome == OfficialVeoStartOutcomes.Replay,
            "replay returns existing operation identity");
    }

    static async Task T_SecretRedactionAndProvenance(Action<bool, string> Ok)
    {
        var ready = await Ready();
        var r = await OfficialVeoRealVertexStartV1.StartAsync(ready.Start);
        Ok(r.Provenance is not null && r.Provenance.AttemptId != Guid.Empty, "provenance persisted");
        Ok(r.Provenance!.AuthorizationHash == r.AuthorizationHash
            && r.Provenance.RequestPayloadHash == r.RequestPayloadHash
            && r.Provenance.PermitId == r.PermitId
            && r.Provenance.SnapshotId == ready.Frozen.SnapshotId,
            "provenance binds attempt / auth / permit / snapshot");
        Ok(r.RedactionCheck.Count == 0, "no token / PEM / Authorization header in provenance");
        var events = await ready.Store.ListEventsByTaskAsync(ready.Frozen.TaskId);
        Ok(events.Any(e => e.EventType == ProductionEventTypes.AttemptStarted
            && (e.PayloadJson ?? "").Contains("REQ_HASH_V1:", StringComparison.Ordinal)
            && !(e.PayloadJson ?? "").Contains("BEGIN PRIVATE", StringComparison.Ordinal)),
            "event payload has request hash and no secrets");
        var attempts = await ready.Store.ListAttemptsAsync(ready.Frozen.TaskId);
        Ok(attempts.Count == 1
            && r.RequestPayloadHash is { } req
            && req.EndsWith(attempts[0].RequestPayloadHash, StringComparison.Ordinal)
            && attempts[0].RequestPayloadHash.Length == 64
            && attempts[0].ProviderJobId == r.EncodedOperationId,
            "canonical attempt holds 64-char payload SHA + operation id");
    }

    static async Task T_PermitConsumptionSemantics(Action<bool, string> Ok)
    {
        var ready = await Ready();
        Ok(ready.Permit.Status == ProductionExecutionPermitStatuses.Issued && ready.Permit.ConsumedAt is null,
            "permit ISSUED / unconsumed before Start");
        var blocked = await OfficialVeoRealVertexStartV1.StartAsync(ready.Start with
        {
            ExpectedAuthorization = ready.Authz with { AuthorizationHash = "AUTH_HASH_V1:no" },
        });
        Ok(!blocked.Started && ready.Permits.ConsumeCalls == 0, "preflight fail does not consume");
        var started = await OfficialVeoRealVertexStartV1.StartAsync(ready.Start);
        Ok(started.Started && ready.Permits.ConsumeCalls == 1
            && started.PermitStateAfter == ProductionExecutionPermitStatuses.Consumed,
            "consume is atomic with attempted Start");
    }

    static void T_RetryConfiguration(Action<bool, string> Ok)
    {
        Ok(OfficialVeoRealVertexStartV1.AutomaticRetryCount == 0
            && ProductionVeoHttpClientPolicy.StartAutomaticRetryCount == 0,
            "application / HTTP Start retry = 0");
        Ok(!typeof(HttpVeoMotionTransport).Assembly.GetTypes().Any(t =>
                (t.Name.Contains("Polly", StringComparison.OrdinalIgnoreCase)
                 || t.Name.Contains("RetryHandler", StringComparison.OrdinalIgnoreCase))
                && t.Namespace?.Contains("Production", StringComparison.Ordinal) == true),
            "no Production Polly / retry handler type");
    }

    static void T_Ck17StillGreen(Action<bool, string> Ok)
    {
        var ck17 = ProductionOfficialVeoExecutionAuthorizationV1Regression.Run();
        Ok(ck17.Count == 0, ck17.Count == 0 ? "CK17 still green" : "CK17:" + string.Join(",", ck17));
    }

    static async Task<ReadyState> Ready(HttpStatusCode startStatus = HttpStatusCode.OK)
    {
        var store = new InMemoryProductionWorkStore();
        var permits = new CountingProductionExecutionPermitStore();
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-KF-18-" + new string('K', 48));
        var hash = KitVideoIntegrityRules.Sha256Hex(bytes);
        var engine = new ProductionWorkExecutionEngine(store);
        var req = await engine.CreateRequestAsync(
            RealVertexExecutionV1Lock.OfficialScope, ProductionActions.CreateImage, "ck18",
            "kf-18-" + Guid.NewGuid().ToString("N"));
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
            "checkpoint-18",
            OfficialVeoExecutionAuthorizationV1.OfficialSelectedAt,
            "DIRECTOR_SELECTION",
            ProductionAiSelectionStatuses.Active,
            "SELECTION_HASH_V1:ck18");
        var issued = await OfficialCreateVideoPermitAuthorizationV1.IssueAsync(
            permits,
            new OfficialCreateVideoPermitIssueRequest(
                RealVertexExecutionV1Lock.OfficialScope, selection, frozen, live));
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
            issued.Permit,
            ProjectId: ProductionOfficialVeoHttpCompositionV1Regression.ComposeProject);
        var authz = OfficialVeoExecutionAuthorizationV1.Evaluate(authRequest);
        var handler = new FakeVeoHttpMessageHandler
        {
            OnSend = _ => startStatus == HttpStatusCode.OK
                ? FakeVeoHttpMessageHandler.Json(HttpStatusCode.OK, $$"""{"name":"{{OperationName}}"}""")
                : FakeVeoHttpMessageHandler.Json(startStatus, """{"error":{"message":"provider"}}"""),
        };
        var auth = new FakeVeoHttpAuthenticator();
        var transport = new HttpVeoMotionTransport(new HttpClient(handler), auth, authz.HttpComposition!.Settings!);
        var start = new OfficialVeoStartRequest(
            authRequest, authz, store, permits, new OfficialVeoStartLedger(), transport, auth);
        return new ReadyState(store, frozen, live, bytes, issued.Permit, permits, authRequest, authz, start, handler, transport);
    }

    sealed record ReadyState(
        InMemoryProductionWorkStore Store,
        ProductionTaskInputSnapshot Frozen,
        ShotExecutionInput Live,
        byte[] Bytes,
        ProductionExecutionPermit Permit,
        CountingProductionExecutionPermitStore Permits,
        OfficialVeoExecutionAuthorizationRequest AuthRequest,
        OfficialVeoExecutionAuthorizationResult Authz,
        OfficialVeoStartRequest Start,
        FakeVeoHttpMessageHandler Handler,
        HttpVeoMotionTransport Transport);
}
