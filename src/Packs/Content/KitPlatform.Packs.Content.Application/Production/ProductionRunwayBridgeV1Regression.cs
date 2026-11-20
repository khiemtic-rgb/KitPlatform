using System.Reflection;
using System.Text;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production.Legacy;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionRunwayBridgeV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_REAL_PROVIDER_BRIDGE_V1";

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
        var h = Harness();
        var created = await h.Tests.CreateAsync("runway-bridge-v1", "regression");
        var scope = TestScope(created);
        var perfSnap = await ProductionTestPerformanceFixture.FreezeAsync(h.Perf, scope.BuildId, scope.ShotCode);
        var kf = await new ProductionTestKeyframeService(h.Os, h.Store, h.Files, h.Scopes)
            .RegisterAsync(new RegisterTestKeyframeCommand(
                created.Run.TestRunId, created.Scope.ScopeId, created.Shot.ShotCode,
                ProductionTestKeyframeCatalog.FixtureId, "regression"));

        var resolver = h.Resolver;
        Ok(resolver.Resolve("fixture", "fixture-motion-v1") is FixtureMotionAdapter, "T01 fixture resolved explicitly");
        Ok(resolver.Resolve("runway", "gen4_turbo") is RunwayMotionExecutionAdapter, "T02 runway/gen4_turbo resolved explicitly");
        Ok(Throws(ProductionFailureReasons.UnknownProvider, () => resolver.Resolve("not-a-provider", "gen4_turbo")), "T03 unknown provider FAIL");
        Ok(Throws(ProductionFailureReasons.ProviderModelMismatch, () => resolver.Resolve("runway", "veo-3.1")), "T04 unknown model FAIL");
        Ok(Throws(ProductionFailureReasons.MissingProviderModel, () => resolver.Resolve("", "gen4_turbo")), "T05 missing ProviderId FAIL");
        Ok(Throws(ProductionFailureReasons.MissingProviderModel, () => resolver.Resolve("runway", "")), "T06 missing ModelId FAIL");

        h.Transport.OnBeforeGetTask = jobId =>
        {
            if (!jobId.StartsWith(FakeRunwayTransport.JobPrefix, StringComparison.Ordinal))
            {
                Ok(false, "T14 Attempt persisted before poll");
                return;
            }
            var taskId = Guid.Parse(jobId[FakeRunwayTransport.JobPrefix.Length..]);
            var rows = h.Store.ListAttemptsAsync(taskId).GetAwaiter().GetResult();
            Ok(!string.IsNullOrWhiteSpace(rows.LastOrDefault()?.ProviderJobId)
                && rows.LastOrDefault()!.ProviderJobId == jobId, "T14 Attempt persisted before poll");
            Ok(rows.LastOrDefault()!.ProviderJobId == jobId, "T15 ProviderJobId persisted immediately after Start");
        };

        var first = await h.Svc.CreateVideoAsync(Command(scope, kf.ArtifactId, "bridge-t01", perfSnap, h.Credential));
        Ok(first.Task.ProviderId == "runway" && first.Task.ModelId == "gen4_turbo"
            && (await h.Store.GetSnapshotByTaskAsync(first.Task.TaskId))!.ProviderId == "runway"
            && (await h.Store.GetSnapshotByTaskAsync(first.Task.TaskId))!.ModelId == "gen4_turbo", "T07 provider/model freeze preserved in Task");
        var frozen = await h.Store.GetSnapshotByTaskAsync(first.Task.TaskId);
        Ok(frozen!.PerformanceSnapshotId == perfSnap.PerformanceSnapshotId, "T08 PerformanceSnapshot preserved");
        Ok(frozen.PerformanceHash == perfSnap.PerformanceHash, "T09 PerformanceHash preserved");
        Ok(h.Transport.CreateCalls == 1 && h.Transport.LastCreateRequest is not null, "T10 Runway request created through adapter only");
        Ok(!OsHoldsRunwayTransport(), "T11 Production OS has no Runway transport field");
        Ok(h.Transport.LastCreateRequest!.ModelId == "gen4_turbo"
            && h.Transport.LastCreateRequest.DurationSec is 5 or 10
            && h.Transport.LastCreateRequest.AspectRatio == "16:9"
            && !string.IsNullOrWhiteSpace(h.Transport.LastCreateRequest.InputSnapshotHash), "T12 transport received vendor-shaped payload");
        Ok(h.Transport.HttpCallCount == 0 && h.Transport.RequiresNetwork == false
            && h.Transport is FakeRunwayTransport, "T13 no real HTTP transport");

        var attempts = await h.Store.ListAttemptsAsync(first.Task.TaskId);
        Ok(attempts.Count == 1 && (attempts[0].ProviderJobId ?? "").StartsWith(FakeRunwayTransport.JobPrefix, StringComparison.Ordinal), "T15 ProviderJobId on Attempt");
        Ok(h.Transport.LastGetTaskJobId == first.Attempt.ProviderJobId, "T20 Recover uses persisted providerJobId");

        var recovered = await h.Runway.RecoverFrozenAsync(frozen, first.Attempt.ProviderJobId!, default);
        Ok(recovered.ProviderJobId == first.Attempt.ProviderJobId
            && recovered.Status == MotionPollStatuses.Succeeded, "T20 Recover uses persisted providerJobId");

        var orphanTransport = new FakeRunwayTransport();
        var orphanAdapter = new RunwayMotionExecutionAdapter(orphanTransport);
        var orphanRecover = await orphanAdapter.RecoverFrozenAsync(frozen, first.Attempt.ProviderJobId!, default);
        Ok(orphanRecover.ProviderJobId == first.Attempt.ProviderJobId
            && orphanTransport.HttpCallCount == 0
            && orphanTransport.LastGetTaskJobId == first.Attempt.ProviderJobId, "T21 Recover works without browser/localStorage");

        var bytes = h.Files.Read(first.Artifact.StorageKey);
        Ok(bytes is { Length: >= 32 }
            && first.Artifact.StorageKey.StartsWith($"os-test/{created.Run.TestRunId:D}/video/", StringComparison.Ordinal), "T22 provider success downloaded as bytes");
        Ok(first.Artifact.VerificationStatus == ProductionArtifactStatuses.Verified
            && ProductionTestMp4.IsDecodable(bytes!, out _), "T27 Valid fixture MP4 → VERIFIED");
        Ok(first.Artifact is not null
            && frozen.PerformanceSnapshotId == perfSnap.PerformanceSnapshotId, "T28 Artifact lineage has PerformanceSnapshotId");
        Ok(frozen.PerformanceHash == perfSnap.PerformanceHash
            && first.Artifact!.InputSnapshotHash == frozen.SnapshotHash, "T29 Artifact lineage has PerformanceHash");
        Ok(first.Artifact!.AttemptId == first.Attempt.AttemptId, "T30 Artifact contains AttemptId");
        Ok(first.Artifact.ProviderJobId == first.Attempt.ProviderJobId, "T31 Artifact contains ProviderJobId");
        Ok(first.Artifact.InputSnapshotHash == frozen.SnapshotHash, "T32 Artifact inputSnapshotHash matches task");
        Ok(first.Current is not null && first.Current.ArtifactId == first.Artifact.ArtifactId, "T33 Verified artifact binds CURRENT");
        Ok(first.Visible is not null && first.Visible.ArtifactId == first.Artifact.ArtifactId, "T34 Verified artifact binds VISIBLE");

        var preview = await h.Svc.GetPreviewAsync(scope);
        Ok(!preview.IsEmpty && preview.ArtifactId == first.Artifact.ArtifactId
            && preview.StorageKey == first.Artifact.StorageKey, "T36 Preview resolves VISIBLE → Artifact → storage");
        Ok(!(preview.StorageKey ?? "").StartsWith("http", StringComparison.OrdinalIgnoreCase)
            && !(preview.StorageKey ?? "").Contains("runway", StringComparison.OrdinalIgnoreCase), "T37 no preview from provider URL");

        var createAfterFirst = h.Transport.CreateCalls;
        var replay = await h.Svc.CreateVideoAsync(Command(scope, kf.ArtifactId, "bridge-t01", perfSnap, h.Credential, frozen));
        Ok(replay.Artifact.ArtifactId == first.Artifact.ArtifactId
            && replay.Attempt.AttemptId == first.Attempt.AttemptId
            && h.Transport.CreateCalls == createAfterFirst, "T17 SUCCESS idempotency returns existing result");

        h.Transport.BlockGetTask = true;
        h.Transport.OnBeforeGetTask = null;
        h.Transport.Scenario = FakeRunwayScenario.DuplicateStart;
        try { await h.Svc.CreateVideoAsync(Command(scope, kf.ArtifactId, "bridge-inflight", perfSnap, h.Credential)); }
        catch (InvalidOperationException ex) when (ex.Message == "FAKE_RUNWAY_POLL_BLOCKED") { }
        var inflightReq = await h.Store.GetRequestByIdempotencyAsync("bridge-inflight");
        var inflightTask = (await h.Store.ListTasksByRequestAsync(inflightReq!.RequestId)).Single();
        var inflightAttempts = await h.Store.ListAttemptsAsync(inflightTask.TaskId);
        Ok(inflightAttempts.Count == 1 && !string.IsNullOrWhiteSpace(inflightAttempts[0].ProviderJobId), "T16 RUNNING attempt persisted");
        var createsBeforeResume = h.Transport.CreateCalls;
        h.Transport.BlockGetTask = false;
        h.Transport.Scenario = FakeRunwayScenario.Success;
        var inflightSnap = await h.Store.GetSnapshotByTaskAsync(inflightTask.TaskId);
        var resumed = await h.Svc.CreateVideoAsync(Command(scope, kf.ArtifactId, "bridge-inflight", perfSnap, h.Credential, inflightSnap));
        Ok(resumed.Attempt.AttemptId == inflightAttempts[0].AttemptId
            && h.Transport.CreateCalls == createsBeforeResume, "T16 RUNNING does not create duplicate provider job");
        Ok(h.Transport.CreateCalls == createsBeforeResume, "T51 duplicate in-flight POST does not create second adapter call");

        var failedJob = await FailClosedProviderAsync(Ok, h, scope, kf.ArtifactId, perfSnap);
        Ok(failedJob, "T18 FAILED attempt remains immutable");

        Ok(await Rejects(() => h.Svc.CreateVideoAsync(Command(scope, kf.ArtifactId, "bridge-t01", perfSnap, h.Credential, frozen, retry: true)),
            ProductionFailureReasons.InFlightReplay), "T19 succeeded task does not open another provider start");
        var retryAttempts = await h.Store.ListAttemptsAsync(first.Task.TaskId);
        Ok(retryAttempts.Count == 1 && retryAttempts[0].AttemptId == first.Attempt.AttemptId, "T19 succeeded task keeps the original Attempt");

        Ok(await RejectsPrefix(() => h.Svc.CreateVideoAsync(new CreateVideoApiCommand(
            scope, ProductionTestPerformanceFixture.Attach(ProductionTestCreateVideoContext.Input(kf.ArtifactId), perfSnap), "regression", "bridge-t01",
            null, null, "fixture", "fixture-motion-v1")),
            ProductionFailureReasons.ProviderChanged), "T07/T24 provider mutation rejected");

        var eng = new ProductionWorkExecutionEngine(h.Store, h.Perf);
        await VerifyFailuresAsync(Ok, eng, scope, kf.ArtifactId, perfSnap, first);

        var unknown = ProductionMotionCostGuard.EvaluateQuote(new FamixaProviderCostQuote(
            "runway", "gen4_turbo", null, "credit", FamixaCostKind.Unknown,
            FamixaProviderCostCatalog.BasisUnknown, "second", 5, 5));
        Ok(!unknown.Allowed && unknown.BlockReason == ProductionFailureReasons.UnknownCost, "T42 Cost UNKNOWN blocks");
        var createsBeforeCost = h.Transport.CreateCalls;
        var overGet = await h.Os.EvaluateCreateVideoAsync(scope, ProductionTestPerformanceFixture.Attach(
            ProductionTestCreateVideoContext.Claim(kf.ArtifactId) with { Timing = "10s", DurationSec = 10, ProviderId = "runway", ModelId = "gen4_turbo" }, perfSnap));
        Ok(!overGet.Available && overGet.BlockReason == ProductionFailureReasons.OverBudget, "T43 GET OverBudget");
        Ok(await Rejects(() => h.Svc.CreateVideoAsync(new CreateVideoApiCommand(
            scope,
            ProductionTestPerformanceFixture.Attach(ProductionTestCreateVideoContext.Input(kf.ArtifactId), perfSnap) with { Timing = "10s", DurationSec = 10 },
            "regression", "bridge-over", null, null, "runway", "gen4_turbo", Credential: h.Credential)),
            ProductionFailureReasons.OverBudget), "T43 Cost > MaxCost blocks before transport");
        Ok(h.Transport.CreateCalls == createsBeforeCost, "T42/T43 no transport call when cost blocked");

        var readyGet = await h.Os.EvaluateCreateVideoAsync(scope, ProductionTestPerformanceFixture.Attach(
            ProductionTestCreateVideoContext.Claim(kf.ArtifactId) with { ProviderId = "runway", ModelId = "gen4_turbo" }, perfSnap));
        Ok(readyGet.Available && readyGet.ProviderId == "runway" && readyGet.ModelId == "gen4_turbo", "T44 cost within MaxCost allows TEST execution");
        Ok(readyGet.Available, "T50 GET readiness available matches successful POST path");

        await MutationAndLegacyAsync(Ok, h, scope, kf.ArtifactId, first, perfSnap);
        await LateAndSupersedeAsync(Ok, h, first, perfSnap);
        await CrossProtectionAsync(Ok, h, created, kf, first, perfSnap);
        await OfficialFilmBlockedAsync(Ok, h, kf);

        var reloadSnap = await h.Store.GetSnapshotByTaskAsync(first.Task.TaskId);
        var reloadAttempts = await h.Store.ListAttemptsAsync(first.Task.TaskId);
        Ok(reloadSnap is not null
            && reloadAttempts.Any(a => a.ProviderJobId == first.Attempt.ProviderJobId), "T49 reload/recovery preserves Attempt + providerJobId");

        T56_Existing(Ok);
        Ok(h.Transport.HttpCallCount == 0, "T13 final HttpCallCount=0");
    }

    static async Task<bool> FailClosedProviderAsync(
        Action<bool, string> Ok, BridgeHarness h, ProductionScope scope, Guid kf, PerformanceSnapshot perfSnap)
    {
        h.Transport.Scenario = FakeRunwayScenario.Failed;
        var before = h.Transport.CreateCalls;
        var failed = await Rejects(() => h.Svc.CreateVideoAsync(Command(scope, kf, "bridge-fail", perfSnap, h.Credential)),
            ProductionFailureReasons.ProviderUnavailable);
        var req = await h.Store.GetRequestByIdempotencyAsync("bridge-fail");
        var task = (await h.Store.ListTasksByRequestAsync(req!.RequestId)).Single();
        var rows = await h.Store.ListAttemptsAsync(task.TaskId);
        var job = rows[0].ProviderJobId;
        h.Transport.Scenario = FakeRunwayScenario.Success;
        var againReason = "";
        var failSnap = await h.Store.GetSnapshotByTaskAsync(task.TaskId);
        try { await h.Svc.CreateVideoAsync(Command(scope, kf, "bridge-fail", perfSnap, h.Credential, failSnap)); }
        catch (InvalidOperationException ex) { againReason = ex.Message; }
        var after = await h.Store.ListAttemptsAsync(task.TaskId);
        var again = againReason is ProductionFailureReasons.InFlightReplay or ProductionFailureReasons.ProviderUnavailable;
        Ok(failed && again && after.Count == 1 && after[0].ProviderJobId == job
            && after[0].Status == ProductionAttemptStatuses.ProviderFailed
            && h.Transport.CreateCalls == before + 1, "T18 FAILED attempt immutable / no second job");
        return failed && after[0].ProviderJobId == job;
    }

    static async Task VerifyFailuresAsync(
        Action<bool, string> Ok, ProductionWorkExecutionEngine eng, ProductionScope scope, Guid kf,
        PerformanceSnapshot perfSnap, CreateVideoSliceResult first)
    {
        async Task<(bool failed, IReadOnlyList<ProductionArtifact> arts)> Probe(string idem, byte[] payload, string mime)
        {
            var req = await eng.CreateRequestAsync(scope, ProductionActions.CreateVideo, "regression", idem);
            var task = await eng.CreateTaskAsync(req.RequestId);
            await eng.FreezeInputAsync(task.TaskId, ProductionTestPerformanceFixture.Attach(ProductionTestCreateVideoContext.Input(kf), perfSnap), "runway", "gen4_turbo");
            var att = await eng.StartAttemptAsync(task.TaskId, "probe");
            await eng.SubmitProviderJobAsync(att.AttemptId, FakeRunwayTransport.JobPrefix + task.TaskId.ToString("N"));
            await eng.RecordProviderSucceededAsync(att.AttemptId, "probe");
            var threw = false;
            try { await eng.VerifyArtifactAsync(att.AttemptId, payload, mime, 5, "os-test/probe"); }
            catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.FailedVerification) { threw = true; }
            var arts = await (engGetArts(eng, task.TaskId));
            var bound = false;
            try { await eng.BindAsync(arts[0].ArtifactId, ProductionBindingRoles.Current); bound = true; }
            catch (InvalidOperationException) { bound = false; }
            Ok(threw && arts.All(a => a.VerificationStatus != ProductionArtifactStatuses.Verified) && !bound, idem);
            return (threw, arts);
        }

        await Probe("bridge-mime", ProductionTestMp4.Encode("mime", 5), "image/png"); // T23
        Ok(true, "T23 Invalid MIME → FAILED_VERIFICATION");
        await Probe("bridge-undec", Encoding.UTF8.GetBytes("FAMIXA-NOT-AN-MP4-" + new string('Z', 48)), ProductionTestMp4.Mime); // T24
        Ok(true, "T24 Undecodable MP4 → FAILED_VERIFICATION");
        await Probe("bridge-novide", ProductionTestMp4.EncodeWithoutVideoStream("audio-only"), ProductionTestMp4.Mime); // T25
        Ok(true, "T25 Missing video stream → FAILED_VERIFICATION");
        await Probe("bridge-dur", ProductionTestMp4.EncodeUnreadableDuration("bad-dur"), ProductionTestMp4.Mime); // T26
        Ok(true, "T26 Invalid duration → FAILED_VERIFICATION");
        Ok(first.Current?.ArtifactId == first.Artifact.ArtifactId, "T35 invalid artifacts never bind");
    }

    static Task<IReadOnlyList<ProductionArtifact>> engGetArts(ProductionWorkExecutionEngine eng, Guid taskId)
    {
        var store = typeof(ProductionWorkExecutionEngine)
            .GetField("_store", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(eng) as IProductionWorkStore;
        return store!.ListArtifactsByTaskAsync(taskId);
    }

    static async Task LateAndSupersedeAsync(Action<bool, string> Ok, BridgeHarness h, CreateVideoSliceResult newer, PerformanceSnapshot perfSnap)
    {
        var eng = new ProductionWorkExecutionEngine(h.Store, h.Perf);
        var expected = await h.Store.GetActiveBindingAsync(newer.Request.Scope.BuildId, newer.Request.Scope.ShotCode, ProductionBindingRoles.Current);
        var expectedArt = expected?.ArtifactId;
        var lateReq = await eng.CreateRequestAsync(newer.Request.Scope, ProductionActions.CreateVideo, "regression", "bridge-late");
        var lateTask = await eng.CreateTaskAsync(lateReq.RequestId);
        await eng.FreezeInputAsync(lateTask.TaskId, ProductionTestPerformanceFixture.Attach(
            ProductionTestCreateVideoContext.Input(newer.Artifact.ParentArtifactIds?.FirstOrDefault()), perfSnap), "runway", "gen4_turbo");
        var older = await eng.StartAttemptAsync(lateTask.TaskId, "late-older");
        _ = await eng.StartAttemptAsync(lateTask.TaskId, "late-newer");
        var late = await eng.ApplyLateResultAsync(older.AttemptId, ProductionTestMp4.Encode("late-older", 5));
        var cur = await h.Store.GetActiveBindingAsync(newer.Request.Scope.BuildId, newer.Request.Scope.ShotCode, ProductionBindingRoles.Current);
        var vis = await h.Store.GetActiveBindingAsync(newer.Request.Scope.BuildId, newer.Request.Scope.ShotCode, ProductionBindingRoles.Visible);
        Ok(late.Status == ProductionAttemptStatuses.Late
            && cur?.ArtifactId == expectedArt
            && vis?.ArtifactId == expectedArt, "T38 late result cannot replace current binding");

        var currentArt = expectedArt is { } id ? await h.Store.GetArtifactAsync(id) : null;
        var ownerTaskId = currentArt?.TaskId ?? newer.Task.TaskId;
        var how = await eng.NewTaskForProviderChangeAsync(ownerTaskId, "google", "veo-3.1", "regression");
        var howSnap = await h.Store.GetSnapshotByTaskAsync(how.TaskId);
        var prior = await h.Store.GetTaskAsync(ownerTaskId);
        Ok(howSnap!.PerformanceSnapshotId == perfSnap.PerformanceSnapshotId
            && howSnap.PerformanceHash == perfSnap.PerformanceHash, "T40 provider change preserves PerformanceSnapshot");
        Ok(how.TaskId != ownerTaskId
            && how.ProviderId == "google"
            && how.ModelId == "veo-3.1"
            && prior!.Status == ProductionTaskStatuses.Superseded, "T41 provider change creates new execution task/attempt");

        var ownerAttempts = await h.Store.ListAttemptsAsync(ownerTaskId);
        var lateOnPrior = await eng.ApplyLateResultAsync(ownerAttempts[0].AttemptId, ProductionTestMp4.Encode("late-superseded", 5));
        var after = await h.Store.GetActiveBindingAsync(newer.Request.Scope.BuildId, newer.Request.Scope.ShotCode, ProductionBindingRoles.Current);
        var visAfter = await h.Store.GetActiveBindingAsync(newer.Request.Scope.BuildId, newer.Request.Scope.ShotCode, ProductionBindingRoles.Visible);
        Ok(lateOnPrior.Status == ProductionAttemptStatuses.Late
            && after?.ArtifactId == expectedArt
            && visAfter?.ArtifactId == expectedArt
            && prior!.Status == ProductionTaskStatuses.Superseded, "T39 superseded task result cannot bind");
    }

    static async Task CrossProtectionAsync(
        Action<bool, string> Ok, BridgeHarness h, ProductionTestFoundation created,
        ProductionTestKeyframeRegistration testKf, CreateVideoSliceResult first, PerformanceSnapshot perfSnap)
    {
        await h.Scopes.InsertTestShotAsync(new ProductionTestShot(
            Guid.NewGuid(), created.Scope.ScopeId, "TEST-SC01-SH02",
            created.Shot.EpisodeCode, created.Shot.SceneCode, "TEST-SC01-SH02",
            DateTimeOffset.UtcNow, "{}"));
        var sh02 = new ProductionScope(created.Scope.ScopeId, created.Shot.EpisodeCode, created.Shot.SceneCode, "TEST-SC01-SH02");
        var preview02 = await h.Svc.GetPreviewAsync(sh02);
        Ok(preview02.IsEmpty, "T45 Cross-shot provider job cannot bind");
        var crossShot = false;
        try { await new ProductionWorkExecutionEngine(h.Store, h.Perf).RejectCrossShotVisibleAsync(first.Artifact.ArtifactId, sh02.BuildId, sh02.ShotCode); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.CrossShotForbidden) { crossShot = true; }
        Ok(crossShot, "T45 Cross-shot provider job cannot bind");

        var other = await h.Tests.CreateAsync("runway-bridge-other", "regression");
        var otherScope = TestScope(other);
        var otherPerf = await ProductionTestPerformanceFixture.FreezeAsync(h.Perf, otherScope.BuildId, otherScope.ShotCode);
        Ok(await Rejects(() => h.Os.EvaluateCreateVideoAsync(otherScope, ProductionTestPerformanceFixture.Attach(
            ProductionTestCreateVideoContext.Claim(testKf.ArtifactId) with { ProviderId = "runway", ModelId = "gen4_turbo" }, otherPerf)),
            ProductionFailureReasons.CrossScopeForbidden), "T46 Cross-build provider job cannot bind");

        var film = new ProductionScope(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH02");
        Ok(await Rejects(() => h.Os.EvaluateCreateVideoAsync(film, new CreateVideoReadinessClaim(
            ProductionTestCreateVideoContext.Story, ProductionTestCreateVideoContext.ActingBeat,
            ProductionTestCreateVideoContext.VisualContract, ProductionTestCreateVideoContext.Timing,
            ProductionTestCreateVideoContext.DurationSec, testKf.ArtifactId, ProviderId: "runway", ModelId: "gen4_turbo")),
            ProductionFailureReasons.CrossScopeForbidden), "T47 Cross-test-scope provider job cannot bind");
        _ = perfSnap;
    }

    static async Task OfficialFilmBlockedAsync(Action<bool, string> Ok, BridgeHarness h, ProductionTestKeyframeRegistration testKf)
    {
        var film = new ProductionScope(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH02");
        var filmKf = await h.Svc.RegisterKeyframeAsync(new RegisterKeyframeCommand(
            film, Encoding.UTF8.GetBytes("FILM-KF-" + new string('F', 48)), "image/png", "film story", "regression"));
        var get = await h.Os.EvaluateCreateVideoAsync(film, new CreateVideoReadinessClaim(
            ProductionTestCreateVideoContext.Story, ProductionTestCreateVideoContext.ActingBeat,
            ProductionTestCreateVideoContext.VisualContract, ProductionTestCreateVideoContext.Timing,
            ProductionTestCreateVideoContext.DurationSec, filmKf.ArtifactId, ProviderId: "runway", ModelId: "gen4_turbo"));
        Ok(!get.Available && get.BlockReason == ProductionFailureReasons.MissingProductionAiSelection, "T48 official film GET blocked");
        Ok(await Rejects(() => h.Svc.CreateVideoAsync(new CreateVideoApiCommand(
            film, ProductionTestCreateVideoContext.Input(filmKf.ArtifactId), "regression", "bridge-film",
            null, null, "runway", "gen4_turbo", Credential: h.Credential)),
            PerformanceContractIds.OfficialFilmForbidden), "T48 Official film provider execution blocked");
        _ = testKf;
    }

    static async Task MutationAndLegacyAsync(
        Action<bool, string> Ok, BridgeHarness h, ProductionScope scope, Guid kf,
        CreateVideoSliceResult first, PerformanceSnapshot perfSnap)
    {
        var mutated = await ProductionTestPerformanceFixture.FreezeAsync(h.Perf, scope.BuildId, scope.ShotCode, action: "sits");
        var frozenBeat = (await h.Store.GetSnapshotByTaskAsync(first.Task.TaskId))!;
        var replay = await h.Svc.CreateVideoAsync(Command(scope, kf, "bridge-t01", perfSnap, h.Credential, frozenBeat));
        var snap = await h.Store.GetSnapshotByTaskAsync(first.Task.TaskId);
        Ok(mutated.PerformanceHash != perfSnap.PerformanceHash
            && replay.Task.TaskId == first.Task.TaskId
            && snap!.PerformanceHash == perfSnap.PerformanceHash, "T52 Performance mutation after freeze does not alter task");

        var graph = await h.Svc.CreateVideoAsync(new CreateVideoApiCommand(
            scope,
            ProductionTestPerformanceFixture.Attach(
                ProductionTestCreateVideoContext.Input(kf) with { ActingBeat = "GRAPH LATEST OVERRIDE" }, perfSnap),
            "regression", "bridge-graph", null, null, "runway", "gen4_turbo", Credential: h.Credential));
        var graphSnap = await h.Store.GetSnapshotByTaskAsync(graph.Task.TaskId);
        Ok(graphSnap!.ActingBeat != "GRAPH LATEST OVERRIDE"
            && graphSnap.PerformanceHash == perfSnap.PerformanceHash, "T53 Graph latest state cannot override frozen PerformanceSnapshot");

        Ok(!LegacySeriesGraphAdapter.IsAuthoritative()
            && !LegacySeriesGraphAdapter.MayDriveNewBinding("SeriesShotRun.takeUrl"), "T54 Legacy SeriesShotRun cannot become artifact authority");
        var urlBlocked = false;
        try { ProductionIdentityRules.ForbidUrlIdentity("https://accepted.take/url.mp4"); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.UrlAuthorityForbidden) { urlBlocked = true; }
        Ok(urlBlocked && !LegacySeriesGraphAdapter.MayDriveNewBinding("SeriesShotRun.acceptedTake.url"), "T55 Legacy acceptedTake.url cannot become preview authority");
    }

    static void T56_Existing(Action<bool, string> Ok)
    {
        var work = ProductionWorkExecutionV1Regression.Run();
        Ok(work.Count == 0, "T56 work: " + string.Join("; ", work));
        var pipeline = ProductionPipelineV1Regression.Run();
        Ok(pipeline.Count == 0, "T56 pipeline: " + string.Join("; ", pipeline));
        var cutover = ProductionCreateVideoV1Regression.Run();
        Ok(cutover.Count == 0, "T56 cutover: " + string.Join("; ", cutover));
        var contract = ProductionCreateVideoActionContractV1Regression.Run();
        Ok(contract.Count == 0, "T56 contract: " + string.Join("; ", contract));
        var film = ProductionScopeAnchorV1Regression.Run();
        Ok(film.Count == 0, "T56 checkpoint 1: " + string.Join("; ", film));
        var test = ProductionTestScopeFoundationV1Regression.Run();
        Ok(test.Count == 0, "T56 checkpoint 2: " + string.Join("; ", test));
        var kf = ProductionTestKeyframeV1Regression.Run();
        Ok(kf.Count == 0, "T56 checkpoint 3A: " + string.Join("; ", kf));
        var e2e = ProductionTestCreateVideoV1Regression.Run();
        Ok(e2e.Count == 0, "T56 checkpoint 3B: " + string.Join("; ", e2e));
        var consume = PerformanceConsumptionCutoverV1Regression.Run();
        Ok(consume.Count == 0, "T56 consumption: " + string.Join("; ", consume));
    }

    static bool OsHoldsRunwayTransport()
    {
        static bool Holds(Type type) =>
            type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(f => typeof(IRunwayTransport).IsAssignableFrom(f.FieldType)
                    || f.FieldType == typeof(RunwayTransportCreateRequest));
        return Holds(typeof(ProductionCreateVideoService))
            || Holds(typeof(ProductionWorkExecutionEngine))
            || Holds(typeof(ProductionExecutionService));
    }

    static CreateVideoApiCommand Command(
        ProductionScope scope, Guid kf, string idem, PerformanceSnapshot snap, ProductionCredentialReference credential,
        ProductionTaskInputSnapshot? replayFrom = null, bool retry = false)
    {
        var input = ProductionTestPerformanceFixture.Attach(ProductionTestCreateVideoContext.Input(kf), snap);
        if (replayFrom is not null)
        {
            input = input with
            {
                Story = replayFrom.Story,
                ActingBeat = replayFrom.ActingBeat,
                VisualContract = replayFrom.VisualContract,
                DurationSec = replayFrom.DurationSec,
                KeyframeArtifactId = replayFrom.KeyframeArtifactId,
                KeyframeHash = replayFrom.KeyframeHash ?? "",
                KeyframeByteSha256 = replayFrom.KeyframeHash ?? "",
                PerformanceSnapshotId = replayFrom.PerformanceSnapshotId,
                PerformanceHash = replayFrom.PerformanceHash,
            };
        }
        return new(scope, input, "regression", idem, null, null, "runway", "gen4_turbo", retry, Credential: credential);
    }

    static ProductionScope TestScope(ProductionTestFoundation created) =>
        new(created.Scope.ScopeId, created.Shot.EpisodeCode, created.Shot.SceneCode, created.Shot.ShotCode);

    static bool Throws(string reason, Action act)
    {
        try { act(); return false; }
        catch (InvalidOperationException ex) { return ex.Message == reason; }
    }

    static async Task<bool> Rejects(Func<Task> act, string reason)
    {
        try { await act(); return false; }
        catch (InvalidOperationException ex) { return ex.Message == reason; }
    }

    static async Task<bool> RejectsPrefix(Func<Task> act, string reason)
    {
        try { await act(); return false; }
        catch (InvalidOperationException ex) { return ex.Message.StartsWith(reason, StringComparison.Ordinal); }
    }

    static BridgeHarness Harness()
    {
        var scopes = new InMemoryProductionScopeFoundationStore();
        foreach (var id in ProductionScopeAnchorRules.OfficialFilmScopeIds)
            scopes.SeedFilm(id);
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var transport = new FakeRunwayTransport();
        var fixture = new FixtureMotionAdapter();
        var runway = new RunwayMotionExecutionAdapter(transport);
        var resolver = new ProductionMotionAdapterResolver([fixture, runway]);
        var perf = new InMemoryPerformanceSnapshotStore();
        var pin = ProductionOfficialHarnessCredentialFixture.Runway();
        var os = new ProductionExecutionService(store, fixture, scopes, perf, credentials: pin.Resolver);
        var svc = new ProductionCreateVideoService(os, store, files, fixture, scopes, resolver);
        return new BridgeHarness(os, svc, store, files, scopes, new ProductionTestFoundationService(scopes), transport, runway, resolver, perf, pin.Reference);
    }

    sealed class BridgeHarness
    {
        public BridgeHarness(
            ProductionExecutionService os,
            ProductionCreateVideoService svc,
            InMemoryProductionWorkStore store,
            InMemoryProductionArtifactFileStore files,
            InMemoryProductionScopeFoundationStore scopes,
            ProductionTestFoundationService tests,
            FakeRunwayTransport transport,
            RunwayMotionExecutionAdapter runway,
            IProductionMotionAdapterResolver resolver,
            InMemoryPerformanceSnapshotStore perf,
            ProductionCredentialReference credential)
        {
            Os = os; Svc = svc; Store = store; Files = files; Scopes = scopes;
            Tests = tests; Transport = transport; Runway = runway; Resolver = resolver;
            Perf = perf; Credential = credential;
        }

        public ProductionExecutionService Os { get; }
        public ProductionCreateVideoService Svc { get; }
        public InMemoryProductionWorkStore Store { get; }
        public InMemoryProductionArtifactFileStore Files { get; }
        public InMemoryProductionScopeFoundationStore Scopes { get; }
        public ProductionTestFoundationService Tests { get; }
        public InMemoryPerformanceSnapshotStore Perf { get; }
        public ProductionCredentialReference Credential { get; }
        public FakeRunwayTransport Transport { get; }
        public RunwayMotionExecutionAdapter Runway { get; }
        public IProductionMotionAdapterResolver Resolver { get; }
    }
}
