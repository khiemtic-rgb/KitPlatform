using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionTestCreateVideoV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_TEST_CREATE_VIDEO_E2E_CHECKPOINT_3B";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }
        try { RunAsync(Ok).GetAwaiter().GetResult(); }
        catch (Exception ex) { fail.Add("UNCAUGHT:" + ex.Message); }
        return fail;
    }

    static async Task RunAsync(Action<bool, string> Ok)
    {
        var (os, svc, store, files, scopes, tests, motion, perf) = Harness();
        var created = await tests.CreateAsync("checkpoint-3b", "regression");
        var scope = TestScope(created);
        var perfSnap = await ProductionTestPerformanceFixture.FreezeAsync(perf, scope.BuildId, scope.ShotCode);
        var kf = await new ProductionTestKeyframeService(os, store, files, scopes)
            .RegisterAsync(new RegisterTestKeyframeCommand(
                created.Run.TestRunId, created.Scope.ScopeId, created.Shot.ShotCode,
                ProductionTestKeyframeCatalog.FixtureId, "regression"));

        var claim = ProductionTestPerformanceFixture.Attach(ProductionTestCreateVideoContext.Claim(kf.ArtifactId), perfSnap);
        var before = await os.EvaluateCreateVideoAsync(scope, claim);
        Ok(before.Available
            && before.KeyframeArtifactId == kf.ArtifactId
            && before.Story == ProductionTestCreateVideoContext.Story
            && before.ActingBeat == ProductionTestCreateVideoContext.ActingBeat
            && before.VisualContract == ProductionTestCreateVideoContext.VisualContract
            && CreateVideoActionContractGate.IsValidTiming(before.Timing, before.DurationSec)
            && before.ProviderId == "fixture"
            && before.ModelId == "fixture-motion-v1", "T01 CREATE_VIDEO available");
        Ok(before.MissingInputs is null || before.MissingInputs.Count == 0, "T01 no missing inputs");

        var first = await svc.CreateVideoAsync(new CreateVideoApiCommand(
            scope, ProductionTestPerformanceFixture.Attach(ProductionTestCreateVideoContext.Input(kf.ArtifactId), perfSnap), "regression", null, null, null));
        Ok(first.Request.RequestId != Guid.Empty
            && first.Request.UserAction == ProductionActions.CreateVideo
            && first.Request.Scope.BuildId == created.Scope.ScopeId
            && first.Request.Scope.ShotCode == created.Shot.ShotCode, "T02 request created");
        Ok(first.Task.TaskId != Guid.Empty
            && first.Task.RequestId == first.Request.RequestId
            && first.Task.Action == ProductionActions.CreateVideo, "T03 task created");

        var snap = await store.GetSnapshotByTaskAsync(first.Task.TaskId);
        Ok(snap is not null
            && snap.Story == ProductionTestCreateVideoContext.Story
            && snap.VisualContract == ProductionTestCreateVideoContext.VisualContract
            && snap.StoryHash == ProductionTestCreateVideoContext.StoryHash
            && snap.VisualHash == ProductionTestCreateVideoContext.VisualHash
            && snap.KeyframeArtifactId == kf.ArtifactId
            && snap.PerformanceSnapshotId == perfSnap.PerformanceSnapshotId
            && snap.PerformanceHash == perfSnap.PerformanceHash
            && snap.ActingBeat == PerformanceActingProjection.FromDraft(perfSnap.Draft)
            && !string.IsNullOrWhiteSpace(snap.SnapshotHash), "T04 immutable InputSnapshot");
        Ok(snap!.ProviderId == "fixture" && snap.ModelId == "fixture-motion-v1"
            && first.Task.ProviderId == "fixture" && first.Task.ModelId == "fixture-motion-v1"
            && !motion.IsPaid, "T05 provider/model frozen");

        var attempts = await store.ListAttemptsAsync(first.Task.TaskId);
        Ok(attempts.Count == 1
            && attempts[0].AttemptId == first.Attempt.AttemptId
            && attempts[0].AttemptNumber == 1
            && attempts[0].Status == ProductionAttemptStatuses.ProviderSucceeded
            && (attempts[0].ProviderJobId ?? "").StartsWith(FixtureMotionAdapter.JobPrefix, StringComparison.Ordinal), "T06 attempt created");
        var events = await store.ListEventsByTaskAsync(first.Task.TaskId);
        Ok(events.Any(e => e.EventType == ProductionEventTypes.AttemptStarted)
            && events.Any(e => e.EventType == ProductionEventTypes.ProviderSubmitted)
            && events.Any(e => e.EventType == ProductionEventTypes.ProviderSucceeded), "T06 STARTED→SUBMITTED→SUCCEEDED");
        Ok(motion.Calls == 1, "T07 FixtureMotionAdapter once");

        var bytes = files.Read(first.Artifact.StorageKey);
        Ok(bytes is { Length: >= 32 }
            && first.Artifact.StorageKey.StartsWith($"os-test/{created.Run.TestRunId:D}/video/", StringComparison.Ordinal)
            && !first.Artifact.StorageKey.StartsWith("os/", StringComparison.Ordinal)
            && !first.Artifact.StorageKey.StartsWith("os-kf/", StringComparison.Ordinal), "T08 physical MP4 exists");
        Ok(bytes is not null && ProductionTestMp4.IsDecodable(bytes, out _), "T09 MP4 decodable");
        Ok(first.Artifact.MimeType == ProductionTestMp4.Mime, "T10 MIME verified");
        Ok(first.Artifact.DurationSec == ProductionTestCreateVideoContext.DurationSec, "T11 duration verified");
        Ok(bytes is not null
            && KitVideoIntegrityRules.Sha256Hex(bytes) == first.Artifact.ByteSha256
            && first.Artifact.SizeBytes == bytes.Length, "T12 SHA256 verified");
        Ok(first.Artifact.Role == ProductionArtifactRoles.VideoTake
            && first.Artifact.ArtifactId != Guid.Empty
            && first.Artifact.AttemptId == first.Attempt.AttemptId
            && first.Artifact.ParentArtifactIds is { } parents && parents.Contains(kf.ArtifactId), "T13 VIDEO_TAKE created");
        Ok(first.Artifact.VerificationStatus == ProductionArtifactStatuses.Verified
            && first.Artifact.InputSnapshotHash == snap.SnapshotHash, "T14 artifact VERIFIED");

        var currentTake = await store.GetActiveBindingAsync(
            scope.BuildId, scope.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake));
        var currentAlias = await store.GetActiveBindingAsync(scope.BuildId, scope.ShotCode, ProductionBindingRoles.Current);
        Ok(currentTake is not null && currentTake.ArtifactId == first.Artifact.ArtifactId, "T15 CURRENT:VIDEO_TAKE");
        Ok(currentAlias is not null && currentAlias.ArtifactId == first.Artifact.ArtifactId, "T15 CURRENT alias");

        var visibleTake = await store.GetActiveBindingAsync(
            scope.BuildId, scope.ShotCode, ProductionBindingRoles.VisibleFor(ProductionArtifactRoles.VideoTake));
        var visibleAlias = await store.GetActiveBindingAsync(scope.BuildId, scope.ShotCode, ProductionBindingRoles.Visible);
        Ok(visibleTake is not null && visibleTake.ArtifactId == first.Artifact.ArtifactId, "T16 VISIBLE:VIDEO_TAKE");
        Ok(visibleAlias is not null && visibleAlias.ArtifactId == first.Artifact.ArtifactId, "T16 VISIBLE alias");
        Ok(currentTake!.ArtifactId == visibleTake!.ArtifactId
            && currentTake.ArtifactId == first.Artifact.ArtifactId, "T17 CURRENT == VISIBLE");

        var preview = await svc.GetPreviewAsync(scope);
        Ok(!preview.IsEmpty && preview.ArtifactId == first.Artifact.ArtifactId
            && preview.StorageKey == first.Artifact.StorageKey, "T18 preview VISIBLE artifact");
        var previewBytes = await svc.ReadPreviewMediaAsync(scope);
        Ok(previewBytes is not null
            && KitVideoIntegrityRules.Sha256Hex(previewBytes) == first.Artifact.ByteSha256
            && preview.ByteSha256 == first.Artifact.ByteSha256, "T19 preview SHA == artifact SHA");

        var reloaded = new ProductionCreateVideoService(
            new ProductionExecutionService(store, motion, scopes, perf), store, files, motion, scopes);
        var reloadPreview = await reloaded.GetPreviewAsync(scope);
        var reloadState = await reloaded.GetShotStateAsync(scope, claim);
        Ok(reloadPreview.ArtifactId == first.Artifact.ArtifactId
            && reloadState.CurrentArtifactId == first.Artifact.ArtifactId
            && reloadState.VisibleArtifactId == first.Artifact.ArtifactId, "T20 reload preserves binding");

        var replay = await svc.CreateVideoAsync(new CreateVideoApiCommand(
            scope, FrozenReplay(ProductionTestPerformanceFixture.Attach(ProductionTestCreateVideoContext.Input(kf.ArtifactId), perfSnap), snap!), "regression",
            first.Request.IdempotencyKey, null, null));
        Ok(replay.Request.RequestId == first.Request.RequestId
            && replay.Task.TaskId == first.Task.TaskId
            && replay.Artifact.ArtifactId == first.Artifact.ArtifactId
            && motion.Calls == 1, "T21 duplicate request does not execute twice");

        await T26_LateResult(Ok, store, first, perf, perfSnap);
        await T25_CrossShot(Ok, scopes, created, svc, first);
        await T24_CrossScope(Ok, os, kf);
        await T22_T23_StoryMutation(Ok, os, store, svc, scope, first);
        await T28_ExistingSuites(Ok);
        T27_FilmUnchanged(Ok, created);
        T29_NoPaid(Ok, motion, first);
        Ok(await Rejects(() => svc.CreateVideoAsync(new CreateVideoApiCommand(
            scope, ProductionTestPerformanceFixture.Attach(ProductionTestCreateVideoContext.Input(kf.ArtifactId), perfSnap), "regression", null,
            Encoding.UTF8.GetBytes("NOT-A-TEST-KEYFRAME-" + new string('X', 48)), "image/png")),
            ProductionFailureReasons.UrlAuthorityForbidden), "TEST refuses keyframe dataUrl");
    }

    static ShotExecutionInput FrozenReplay(ShotExecutionInput input, ProductionTaskInputSnapshot frozen) =>
        input with
        {
            Story = frozen.Story,
            ActingBeat = frozen.ActingBeat,
            VisualContract = frozen.VisualContract,
            DurationSec = frozen.DurationSec,
            KeyframeArtifactId = frozen.KeyframeArtifactId,
            KeyframeHash = frozen.KeyframeHash ?? "",
            KeyframeByteSha256 = frozen.KeyframeHash ?? "",
            PerformanceSnapshotId = frozen.PerformanceSnapshotId,
            PerformanceHash = frozen.PerformanceHash,
        };

    static async Task T22_T23_StoryMutation(
        Action<bool, string> Ok, ProductionExecutionService os, InMemoryProductionWorkStore store,
        ProductionCreateVideoService svc, ProductionScope scope, CreateVideoSliceResult first)
    {
        await os.InvalidateDownstreamAsync(scope, ShotNodeIds.Story);
        var after = await svc.GetPreviewAsync(scope);
        Ok(after.IsEmpty, "T22 story mutation unbinds VISIBLE");
        var currentTake = await store.GetActiveBindingAsync(
            scope.BuildId, scope.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake));
        var visibleTake = await store.GetActiveBindingAsync(
            scope.BuildId, scope.ShotCode, ProductionBindingRoles.VisibleFor(ProductionArtifactRoles.VideoTake));
        Ok(currentTake is null && visibleTake is null, "T22 CURRENT/VISIBLE:VIDEO_TAKE unbound");
        var task = await store.GetTaskAsync(first.Task.TaskId);
        Ok(task is { Status: ProductionTaskStatuses.Superseded }, "T22 old CREATE_VIDEO task superseded");
        var old = await store.GetArtifactAsync(first.Artifact.ArtifactId);
        Ok(old is not null
            && old.ByteSha256 == first.Artifact.ByteSha256
            && old.VerificationStatus == ProductionArtifactStatuses.Verified, "T23 old artifact immutable");
    }

    static async Task T24_CrossScope(Action<bool, string> Ok, ProductionExecutionService os, ProductionTestKeyframeRegistration kf)
    {
        var film = new ProductionScope(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH02");
        Ok(await Rejects(() => os.EvaluateCreateVideoAsync(film, new CreateVideoReadinessClaim(
            ProductionTestCreateVideoContext.Story, ProductionTestCreateVideoContext.ActingBeat,
            ProductionTestCreateVideoContext.VisualContract, ProductionTestCreateVideoContext.Timing,
            ProductionTestCreateVideoContext.DurationSec, kf.ArtifactId)),
            ProductionFailureReasons.CrossScopeForbidden), "T24 cross-scope artifact rejected");
    }

    static async Task T25_CrossShot(
        Action<bool, string> Ok, InMemoryProductionScopeFoundationStore scopes, ProductionTestFoundation created,
        ProductionCreateVideoService svc, CreateVideoSliceResult first)
    {
        await scopes.InsertTestShotAsync(new ProductionTestShot(
            Guid.NewGuid(), created.Scope.ScopeId, "TEST-SC01-SH02",
            created.Shot.EpisodeCode, created.Shot.SceneCode, "TEST-SC01-SH02",
            DateTimeOffset.UtcNow, "{}"));
        var sh02 = new ProductionScope(created.Scope.ScopeId, created.Shot.EpisodeCode, created.Shot.SceneCode, "TEST-SC01-SH02");
        var preview02 = await svc.GetPreviewAsync(sh02);
        var preview01 = await svc.GetPreviewAsync(TestScope(created));
        Ok(preview02.IsEmpty && preview01.ArtifactId == first.Artifact.ArtifactId, "T25 SH01 artifact is not SH02 VISIBLE");
    }

    static async Task T26_LateResult(Action<bool, string> Ok, InMemoryProductionWorkStore store, CreateVideoSliceResult first,
        InMemoryPerformanceSnapshotStore perf, PerformanceSnapshot perfSnap)
    {
        var eng = new ProductionWorkExecutionEngine(store, perf);
        var lateReq = await eng.CreateRequestAsync(first.Request.Scope, ProductionActions.CreateVideo, "regression", "late-3b-" + first.Request.RequestId.ToString("N"));
        var lateTask = await eng.CreateTaskAsync(lateReq.RequestId);
        await eng.FreezeInputAsync(lateTask.TaskId, ProductionTestPerformanceFixture.Attach(
            ProductionTestCreateVideoContext.Input(first.Artifact.ParentArtifactIds?.FirstOrDefault()), perfSnap), "fixture", "fixture-motion-v1");
        var older = await eng.StartAttemptAsync(lateTask.TaskId, "late-older");
        _ = await eng.StartAttemptAsync(lateTask.TaskId, "late-newer");
        var late = await eng.ApplyLateResultAsync(older.AttemptId, Encoding.UTF8.GetBytes("FAMIXA-LATE-IGNORE-" + new string('L', 48)));
        var cur = await store.GetActiveBindingAsync(first.Request.Scope.BuildId, first.Request.Scope.ShotCode, ProductionBindingRoles.Current);
        var vis = await store.GetActiveBindingAsync(first.Request.Scope.BuildId, first.Request.Scope.ShotCode, ProductionBindingRoles.Visible);
        Ok(late.Status == ProductionAttemptStatuses.Late
            && cur?.ArtifactId == first.Artifact.ArtifactId
            && vis?.ArtifactId == first.Artifact.ArtifactId, "T26 late result cannot steal VISIBLE");
    }

    static Task T28_ExistingSuites(Action<bool, string> Ok)
    {
        var work = ProductionWorkExecutionV1Regression.Run();
        Ok(work.Count == 0, "T28 work: " + string.Join("; ", work));
        var pipeline = ProductionPipelineV1Regression.Run();
        Ok(pipeline.Count == 0, "T28 pipeline: " + string.Join("; ", pipeline));
        var cutover = ProductionCreateVideoV1Regression.Run();
        Ok(cutover.Count == 0, "T28 cutover: " + string.Join("; ", cutover));
        var contract = ProductionCreateVideoActionContractV1Regression.Run();
        Ok(contract.Count == 0, "T28 contract: " + string.Join("; ", contract));
        var film = ProductionScopeAnchorV1Regression.Run();
        Ok(film.Count == 0, "T28 checkpoint 1: " + string.Join("; ", film));
        var test = ProductionTestScopeFoundationV1Regression.Run();
        Ok(test.Count == 0, "T28 checkpoint 2: " + string.Join("; ", test));
        var kf = ProductionTestKeyframeV1Regression.Run();
        Ok(kf.Count == 0, "T28 checkpoint 3A: " + string.Join("; ", kf));
        var previewAuth = ProductionShotStatePreviewAuthorityV1Regression.Run();
        Ok(previewAuth.Count == 0, "T28 preview authority: " + string.Join("; ", previewAuth));
        var voiceCutover = ProductionCreateVoiceCutoverV1Regression.Run();
        Ok(voiceCutover.Count == 0, "T28 voice cutover: " + string.Join("; ", voiceCutover));
        var lipsyncCutover = ProductionCreateLipsyncInputCutoverV1Regression.Run();
        Ok(lipsyncCutover.Count == 0, "T28 lipsync input cutover: " + string.Join("; ", lipsyncCutover));
        var mixCutover = ProductionMixInputCutoverV1Regression.Run();
        Ok(mixCutover.Count == 0, "T28 mix input cutover: " + string.Join("; ", mixCutover));
        var vertical = ProductionFullFilmVerticalRunV1Regression.Run();
        Ok(vertical.Count == 0, "T28 full film vertical run: " + string.Join("; ", vertical));
        var ingress = ProductionEp01Sh01ProductionIngressV1Regression.Run();
        Ok(ingress.Count == 0, "T28 EP01 SH01 production ingress: " + string.Join("; ", ingress));
        var voiceFilm = ProductionEp01Sh01RealFilmVoiceV1Regression.Run();
        Ok(voiceFilm.Count == 0, "T28 EP01 SH01 real film voice: " + string.Join("; ", voiceFilm));
        var voiceIngress = ProductionEp01Sh01VoiceProductionIngressV1Regression.Run();
        Ok(voiceIngress.Count == 0, "T28 EP01 SH01 voice production ingress: " + string.Join("; ", voiceIngress));
        var voiceV2 = ProductionEp01Sh01RealFilmRunV2VoiceRegression.Run();
        Ok(voiceV2.Count == 0, "T28 EP01 SH01 real film voice V2: " + string.Join("; ", voiceV2));
        var sh02Voice = ProductionOfficialSh02CreateVoiceV1Regression.Run();
        Ok(sh02Voice.Count == 0, "T28 EP01 SH02 official CREATE_VOICE: " + string.Join("; ", sh02Voice));
        var sh02Lipsync = ProductionOfficialSh02CreateLipsyncV1Regression.Run();
        Ok(sh02Lipsync.Count == 0, "T28 EP01 SH02 official CREATE_LIPSYNC: " + string.Join("; ", sh02Lipsync));
        return Task.CompletedTask;
    }

    static void T27_FilmUnchanged(Action<bool, string> Ok, ProductionTestFoundation created)
    {
        Ok(ProductionScopeAnchorRules.OfficialFilmScopeIds.Count == 4, "T27 four FILM builds");
        Ok(created.Anchor.Kind == ProductionScopeAnchorRules.Test && created.Anchor.SeriesBuildId is null, "T27 TEST has no series_build");
        Ok(ProductionScopeAnchorRules.OfficialEp01 == Guid.Parse("4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f"), "T27 EP01 unchanged");
        Ok(ProductionScopeAnchorRules.OfficialEp99 == Guid.Parse("77262ec0-74ad-459d-9128-f09bb553a5dd"), "T27 EP99 unchanged");
    }

    static void T29_NoPaid(Action<bool, string> Ok, CountingMotionAdapter motion, CreateVideoSliceResult first)
    {
        Ok(!motion.IsPaid && motion.ProviderId == "fixture" && motion.ModelId == "fixture-motion-v1", "T29 fixture unpaid");
        Ok(motion.Calls == 1, "T29 fixture generation = 1");
        Ok(!(first.Attempt.ProviderJobId ?? "").Contains("runway", StringComparison.OrdinalIgnoreCase)
            && !(first.Attempt.ProviderJobId ?? "").Contains("veo", StringComparison.OrdinalIgnoreCase)
            && !(first.Attempt.ProviderJobId ?? "").Contains("fal", StringComparison.OrdinalIgnoreCase), "T29 no paid job id");
    }

    static (ProductionExecutionService os, ProductionCreateVideoService svc, InMemoryProductionWorkStore store,
        InMemoryProductionArtifactFileStore files, InMemoryProductionScopeFoundationStore scopes,
        ProductionTestFoundationService tests, CountingMotionAdapter motion, InMemoryPerformanceSnapshotStore perf) Harness()
    {
        var scopes = new InMemoryProductionScopeFoundationStore();
        foreach (var id in ProductionScopeAnchorRules.OfficialFilmScopeIds)
            scopes.SeedFilm(id);
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var perf = new InMemoryPerformanceSnapshotStore();
        var motion = new CountingMotionAdapter(new FixtureMotionAdapter());
        var os = new ProductionExecutionService(store, motion, scopes, perf);
        var svc = new ProductionCreateVideoService(os, store, files, motion, scopes);
        return (os, svc, store, files, scopes, new ProductionTestFoundationService(scopes), motion, perf);
    }

    static ProductionScope TestScope(ProductionTestFoundation created) =>
        new(created.Scope.ScopeId, created.Shot.EpisodeCode, created.Shot.SceneCode, created.Shot.ShotCode);

    static async Task<bool> Rejects(Func<Task> act, string reason)
    {
        try { await act(); return false; }
        catch (InvalidOperationException ex) { return ex.Message == reason; }
    }

    sealed class CountingMotionAdapter : IMotionExecutionAdapter
    {
        readonly IMotionExecutionAdapter _inner;
        public int Calls;
        public CountingMotionAdapter(IMotionExecutionAdapter inner) => _inner = inner;
        public string ProviderId => _inner.ProviderId;
        public string ModelId => _inner.ModelId;
        public bool IsPaid => _inner.IsPaid;
        public bool RequiresNetwork => _inner.RequiresNetwork;
        public Task<MotionStartResult> StartFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
        {
            Calls++;
            return _inner.StartFrozenAsync(snapshot, ct);
        }
        public Task<MotionPollResult> PollFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
            => _inner.PollFrozenAsync(snapshot, providerJobId, ct);
        public Task<MotionPollResult> RecoverFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
            => _inner.RecoverFrozenAsync(snapshot, providerJobId, ct);
        public Task<MotionAdapterResult> FetchOutputAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
            => _inner.FetchOutputAsync(snapshot, providerJobId, ct);
        public Task<MotionAdapterResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
            => MotionExecutionLifecycle.ExecuteViaLifecycleAsync(this, snapshot, ct);
    }
}
