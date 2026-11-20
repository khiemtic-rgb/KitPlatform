using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionCreateVideoActionContractV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_CREATE_VIDEO_ACTION_CONTRACT_ENFORCEMENT_V1";

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
        Ok(CreateVideoActionContractGate.RequiredInputs.SequenceEqual(
                ProductionActionContractRegistry.CreateVideo.RequiredInputs),
            "gate RequiredInputs == contract RequiredInputs");
        Ok(ProductionActionContractRegistry.CreateVideo.ExpectedOutputType == ProductionOutputTypes.VideoTake,
            "output stays VIDEO_TAKE");
        Ok(ProductionActionContractRegistry.CreateVideo.ForbiddenFallbacks.Contains(ProductionActions.CreateImage)
            && ProductionActionContractRegistry.CreateVideo.ForbiddenFallbacks.Contains(ProductionActions.CreateVoice)
            && ProductionActionContractRegistry.CreateVideo.ForbiddenFallbacks.Contains(ProductionActions.CreateLipsync),
            "forbidden fallbacks unchanged");

        await T01_MissingKeyframe(Ok);
        await T02_MissingStory(Ok);
        await T03_MissingActingBeat(Ok);
        await T04_MissingVisualContract(Ok);
        await T05_MissingTiming(Ok);
        await T06_MissingProviderModel(Ok);
        await T07_AllValidAvailable(Ok);
        await T08_PostMutatedAfterGetAvailable(Ok);
        await T09_LegacyKfApprovedWithoutOsKeyframe(Ok);
        await T10_LocalTakeDoesNotSatisfy(Ok);
        await T11_ProviderModelChangeNoSilentReuse(Ok);
    }

    static async Task T01_MissingKeyframe(Action<bool, string> Ok)
    {
        var (os, svc, scope, _, _) = await Harness();
        var actions = await os.GetAvailableActionsAsync(scope, FullClaim());
        var create = actions.Single(a => a.Action == ProductionActions.CreateVideo);
        Ok(!create.Available && create.BlockReason == ProductionFailureReasons.MissingKeyframe
            && create.MissingInputs!.Contains("ApprovedKeyframe"), "T01 GET MISSING_KEYFRAME");
        Ok(await PostFails(svc, scope, FullInput(), ProductionFailureReasons.MissingKeyframe), "T01 POST MISSING_KEYFRAME");
    }

    static async Task T02_MissingStory(Action<bool, string> Ok)
    {
        var (os, svc, scope, store, _) = await Harness();
        await SeedKeyframe(os, scope, Input("", "He stays in place.", "visual 16:9", "5s", 5), store);
        var actions = await os.GetAvailableActionsAsync(scope);
        var create = actions.Single(a => a.Action == ProductionActions.CreateVideo);
        Ok(!create.Available && create.BlockReason == ProductionFailureReasons.MissingStory
            && create.MissingInputs!.Contains("Story"), "T02 GET MISSING_STORY");
        Ok(await PostFails(svc, scope, Input("", "He stays in place.", "visual 16:9", "5s", 5), ProductionFailureReasons.MissingStory),
            "T02 POST MISSING_STORY");
    }

    static async Task T03_MissingActingBeat(Action<bool, string> Ok)
    {
        var (os, svc, scope, _, _) = await Harness();
        await SeedKeyframe(os, scope, Input(Story, "", "visual 16:9", "5s", 5));
        var actions = await os.GetAvailableActionsAsync(scope);
        var create = actions.Single(a => a.Action == ProductionActions.CreateVideo);
        Ok(!create.Available && create.BlockReason == ProductionFailureReasons.MissingActingBeat
            && create.MissingInputs!.Contains("ActingBeat"), "T03 GET MISSING_ACTING_BEAT");
        Ok(await PostFails(svc, scope, Input(Story, "", "visual 16:9", "5s", 5), ProductionFailureReasons.MissingActingBeat),
            "T03 POST MISSING_ACTING_BEAT");
    }

    static async Task T04_MissingVisualContract(Action<bool, string> Ok)
    {
        var (os, svc, scope, _, _) = await Harness();
        await SeedKeyframe(os, scope, Input(Story, "He stays in place.", "", "5s", 5));
        var actions = await os.GetAvailableActionsAsync(scope);
        var create = actions.Single(a => a.Action == ProductionActions.CreateVideo);
        Ok(!create.Available && create.BlockReason == ProductionFailureReasons.MissingVisualContract
            && create.MissingInputs!.Contains("VisualContract"), "T04 GET MISSING_VISUAL_CONTRACT");
        Ok(await PostFails(svc, scope, Input(Story, "He stays in place.", "", "5s", 5), ProductionFailureReasons.MissingVisualContract),
            "T04 POST MISSING_VISUAL_CONTRACT");
    }

    static async Task T05_MissingTiming(Action<bool, string> Ok)
    {
        var (os, svc, scope, _, _) = await Harness();
        await SeedKeyframe(os, scope, Input(Story, "He stays in place.", "visual 16:9", "0", 0));
        var actions = await os.GetAvailableActionsAsync(scope);
        var create = actions.Single(a => a.Action == ProductionActions.CreateVideo);
        Ok(!create.Available && create.BlockReason == ProductionFailureReasons.MissingTiming
            && create.MissingInputs!.Contains("Timing"), "T05 GET MISSING_TIMING");
        Ok(await PostFails(svc, scope, Input(Story, "He stays in place.", "visual 16:9", "0", 0), ProductionFailureReasons.MissingTiming),
            "T05 POST MISSING_TIMING");
    }

    static async Task T06_MissingProviderModel(Action<bool, string> Ok)
    {
        var store = new InMemoryProductionWorkStore();
        var perf = new InMemoryPerformanceSnapshotStore();
        var blank = new NamedMotionAdapter("", "");
        var os = new ProductionExecutionService(store, blank, performance: perf);
        var svc = new ProductionCreateVideoService(os, store, new InMemoryProductionArtifactFileStore(), blank);
        var scope = Scope();
        var snap = await ProductionTestPerformanceFixture.FreezeAsync(perf, scope.BuildId, scope.ShotCode);
        await SeedKeyframe(os, scope, Attach(FullInput(), snap));
        var actions = await os.GetAvailableActionsAsync(scope, Attach(FullClaim(), snap));
        var create = actions.Single(a => a.Action == ProductionActions.CreateVideo);
        Ok(!create.Available && create.BlockReason == ProductionFailureReasons.MissingProviderModel
            && create.MissingInputs!.Contains("FrozenProviderModel"), "T06 GET MISSING_PROVIDER_MODEL");
        Ok(await PostFails(svc, scope, Attach(FullInput(), snap), ProductionFailureReasons.MissingProviderModel),
            "T06 POST MISSING_PROVIDER_MODEL");
    }

    static async Task T07_AllValidAvailable(Action<bool, string> Ok)
    {
        var (os, svc, scope, _, snap) = await Harness();
        var kf = await SeedKeyframe(os, scope, Attach(FullInput(), snap));
        var claim = Attach(FullClaim(), snap);
        var actions = await os.GetAvailableActionsAsync(scope, claim);
        var create = actions.Single(a => a.Action == ProductionActions.CreateVideo);
        Ok(create.Available && create.BlockReason is null && create.Action == ProductionActions.CreateVideo, "T07 GET available");
        Ok(create.RequiredInputs!.SequenceEqual(ProductionActionContractRegistry.CreateVideo.RequiredInputs),
            "T07 GET requiredInputs == contract");
        var verdict = await os.EvaluateCreateVideoAsync(scope, claim);
        Ok(verdict.Available && verdict.KeyframeArtifactId == kf.ArtifactId, "T07 evaluate matches OS keyframe");
        var made = await svc.CreateVideoAsync(new CreateVideoApiCommand(scope, Attach(FullInput(), snap), "test", null, null, null));
        Ok(made.Request.UserAction == ProductionActions.CreateVideo
            && made.Task.ProviderId == "fixture"
            && made.Task.ModelId == "fixture-motion-v1"
            && made.Artifact.Role == ProductionArtifactRoles.VideoTake, "T07 POST freeze fixture VIDEO_TAKE");
    }

    static async Task T08_PostMutatedAfterGetAvailable(Action<bool, string> Ok)
    {
        var (os, svc, scope, _, snap) = await Harness();
        var kf = await SeedKeyframe(os, scope, Attach(FullInput(), snap));
        var actions = await os.GetAvailableActionsAsync(scope, Attach(FullClaim(), snap));
        Ok(actions.Single(a => a.Action == ProductionActions.CreateVideo).Available, "T08 GET available before mutate");
        var mutated = Attach(FullInput(), snap) with { KeyframeArtifactId = Guid.NewGuid() };
        Ok(await PostFails(svc, scope, mutated, ProductionFailureReasons.InputMutated),
            "T08 POST mutated keyframe identity rejected");
        _ = kf;
        await SeedEmptyActingShot(Ok);
    }

    static async Task<(ProductionExecutionService os, ProductionCreateVideoService svc, ProductionScope scope)> SeedEmptyActingShot(Action<bool, string> Ok)
    {
        var (os, svc, scope, _, snap) = await Harness();
        await SeedKeyframe(os, scope, Input(Story, "", "visual 16:9", "5s", 5));
        var get = await os.GetAvailableActionsAsync(scope, Attach(new CreateVideoReadinessClaim(Story, "He stays in place.", "visual 16:9", "5s", 5), snap));
        Ok(get.Single(a => a.Action == ProductionActions.CreateVideo).Available, "T08 GET available via explicit acting claim");
        Ok(await PostFails(svc, scope, Input(Story, "", "visual 16:9", "5s", 5), ProductionFailureReasons.MissingActingBeat),
            "T08 POST omitted acting after GET claim is fail-closed");
        return (os, svc, scope);
    }

    static async Task T09_LegacyKfApprovedWithoutOsKeyframe(Action<bool, string> Ok)
    {
        var (os, _, scope, _, _) = await Harness();
        var verdict = await os.EvaluateCreateVideoAsync(scope, new CreateVideoReadinessClaim(
            Story, "He stays in place.", "visual 16:9", "5s", 5, null, KfApproved: true));
        Ok(!verdict.Available && verdict.BlockReason == ProductionFailureReasons.MissingKeyframe, "T09 kfApproved ignored");
    }

    static async Task T10_LocalTakeDoesNotSatisfy(Action<bool, string> Ok)
    {
        var (os, _, scope, _, _) = await Harness();
        var empty = await os.EvaluateCreateVideoAsync(scope, new CreateVideoReadinessClaim(
            TakeUrl: "blob:old-take", PreviewUrl: "https://legacy/preview.mp4", AcceptedTake: "take-1", TurboTaskId: "turbo"));
        Ok(!empty.Available && empty.BlockReason == ProductionFailureReasons.MissingKeyframe, "T10 local take does not satisfy");
        var (osReady, _, scopeReady, _, snapReady) = await Harness();
        await SeedKeyframe(osReady, scopeReady, Attach(FullInput(), snapReady));
        var ready = await osReady.EvaluateCreateVideoAsync(scopeReady, Attach(new CreateVideoReadinessClaim(
            TakeUrl: "blob:old-take", PreviewUrl: "https://legacy/preview.mp4", AcceptedTake: "take-1", TurboTaskId: "turbo"), snapReady));
        Ok(ready.Available, "T10 local take does not change a valid OS verdict");
    }

    static async Task T11_ProviderModelChangeNoSilentReuse(Action<bool, string> Ok)
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var perf = new InMemoryPerformanceSnapshotStore();
        var motion = new NamedMotionAdapter("fixture", "fixture-motion-v1");
        var pin = ProductionOfficialHarnessCredentialFixture.Runway();
        var os = new ProductionExecutionService(store, motion, performance: perf, credentials: pin.Resolver);
        var svc = new ProductionCreateVideoService(os, store, files, motion);
        var scope = Scope();
        var snap = await ProductionTestPerformanceFixture.FreezeAsync(perf, scope.BuildId, scope.ShotCode);
        await SeedKeyframe(os, scope, Attach(FullInput(), snap));
        var first = await svc.CreateVideoAsync(new CreateVideoApiCommand(scope, Attach(FullInput(), snap), "test", "idem-t11", null, null));
        Ok(first.Task.ProviderId == "fixture" && first.Task.ModelId == "fixture-motion-v1", "T11 initial freeze");

        motion.ProviderId = "runway";
        motion.ModelId = "gen4_turbo";
        var replayFailed = false;
        try
        {
            await svc.CreateVideoAsync(new CreateVideoApiCommand(
                scope, Attach(FullInput(), snap), "test", "idem-t11", null, null, RetryAttempt: true));
        }
        catch (InvalidOperationException ex) when (
            ex.Message.StartsWith(ProductionFailureReasons.ProviderChanged, StringComparison.Ordinal)
            || ex.Message.StartsWith(ProductionFailureReasons.ModelChanged, StringComparison.Ordinal))
        {
            replayFailed = true;
        }
        Ok(replayFailed, "T11 same idempotency after provider change fails closed");

        var next = await svc.CreateVideoAsync(new CreateVideoApiCommand(
            scope, Attach(FullInput(), snap), "test", null, null, null, Credential: pin.Reference));
        Ok(next.Task.TaskId != first.Task.TaskId
            && next.Task.ProviderId == "runway"
            && next.Task.ModelId == "gen4_turbo", "T11 new Task freezes the new provider/model");
    }

    const string Story = "Minh ngập ngừng, đặt cặp xuống ghế rồi chậm chạp kéo khóa.";

    static async Task<(ProductionExecutionService os, ProductionCreateVideoService svc, ProductionScope scope, InMemoryProductionWorkStore store, PerformanceSnapshot snap)> Harness()
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var perf = new InMemoryPerformanceSnapshotStore();
        var motion = new FixtureMotionAdapter();
        var scope = Scope();
        var snap = await ProductionTestPerformanceFixture.FreezeAsync(perf, scope.BuildId, scope.ShotCode);
        var os = new ProductionExecutionService(store, motion, performance: perf);
        return (os, new ProductionCreateVideoService(os, store, files, motion), scope, store, snap);
    }

    static ShotExecutionInput Attach(ShotExecutionInput input, PerformanceSnapshot snap) =>
        ProductionTestPerformanceFixture.Attach(input, snap);

    static CreateVideoReadinessClaim Attach(CreateVideoReadinessClaim claim, PerformanceSnapshot snap) =>
        ProductionTestPerformanceFixture.Attach(claim, snap);

    static ProductionScope Scope() =>
        new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), "EP99", "SC01", "EP99-SC01-SH99");

    static CreateVideoReadinessClaim FullClaim() =>
        new(Story, "He stays in place.", "visual 16:9", "5s", 5);

    static ShotExecutionInput FullInput() => Input(Story, "He stays in place.", "visual 16:9", "5s", 5);

    static ShotExecutionInput Input(string story, string acting, string visual, string timing, decimal duration) =>
        new(story, acting, visual, "CHAR-001", "", timing, "", "motion",
            null, "", "16:9", duration, ShotExecutionCompiler.Version, null);

    static async Task<ProductionArtifact> SeedKeyframe(
        IProductionExecutionService os,
        ProductionScope scope,
        ShotExecutionInput input,
        IProductionWorkStore? store = null)
    {
        var req = await os.CreateRequestAsync(scope, ProductionActions.CreateImage, "test", "kf-" + Guid.NewGuid().ToString("N"));
        var task = await os.CreateTaskAsync(req.RequestId);
        if (store is not null && !CreateVideoActionContractGate.IsPresent(input.Story))
        {
            var snap = new ProductionTaskInputSnapshot(
                Guid.NewGuid(), task.TaskId,
                input.Story, input.ActingBeat, input.VisualContract, "",
                "", "",
                ShotExecutionCompiler.Hash(input.Story),
                ShotExecutionCompiler.Hash(input.ActingBeat),
                ShotExecutionCompiler.Hash(input.VisualContract),
                ShotExecutionCompiler.Hash(""),
                input.KeyframeHash, "", "", "",
                ShotExecutionCompiler.Hash(input.Timing),
                "import", "existing-pixels", "", DateTimeOffset.UtcNow,
                null, input.AspectRatio, input.DurationSec, input.CompilerVersion, null);
            snap = snap with { SnapshotHash = ShotExecutionCompiler.SnapshotHash(snap) };
            await store.InsertSnapshotAsync(snap);
            await store.UpdateTaskAsync(task with
            {
                InputSnapshotId = snap.SnapshotId,
                ProviderId = "import",
                ModelId = "existing-pixels",
                Status = ProductionTaskStatuses.ProviderFrozen,
            });
        }
        else
        {
            await os.FreezeInputAsync(task.TaskId, input, "import", "existing-pixels");
        }
        var att = await os.StartAttemptAsync(task.TaskId, "kf-payload");
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-KF-CONTRACT-" + scope.ShotCode + new string('K', 48));
        await os.SubmitProviderJobAsync(att.AttemptId, "img-contract");
        await os.CompleteAttemptAsync(att.AttemptId, KitVideoIntegrityRules.Sha256Hex(bytes));
        var art = await os.VerifyArtifactAsync(att.AttemptId, bytes, "image/png", 0, "kf/" + scope.ShotCode + ".png");
        await os.BindCurrentAsync(art.ArtifactId);
        return art;
    }

    static async Task<bool> PostFails(ProductionCreateVideoService svc, ProductionScope scope, ShotExecutionInput input, string reason)
    {
        try
        {
            await svc.CreateVideoAsync(new CreateVideoApiCommand(scope, input, "test", null, null, null));
            return false;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message == reason;
        }
    }

    sealed class NamedMotionAdapter : IMotionExecutionAdapter
    {
        public NamedMotionAdapter(string providerId, string modelId)
        {
            ProviderId = providerId;
            ModelId = modelId;
        }

        public string ProviderId { get; set; }
        public string ModelId { get; set; }
        public bool IsPaid => false;
        public bool RequiresNetwork => false;

        public Task<MotionStartResult> StartFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
            => Task.FromResult(new MotionStartResult("fixture-motion-" + snapshot.TaskId.ToString("N")[..12]));

        public Task<MotionPollResult> PollFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
            => Task.FromResult(new MotionPollResult(MotionPollStatuses.Succeeded, providerJobId));

        public Task<MotionPollResult> RecoverFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
            => PollFrozenAsync(snapshot, providerJobId, ct);

        public Task<MotionAdapterResult> FetchOutputAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
        {
            var duration = snapshot.DurationSec > 0 ? snapshot.DurationSec : 5;
            var bytes = ProductionTestMp4.Encode(snapshot.SnapshotHash + snapshot.TaskId.ToString("N"), duration);
            return Task.FromResult(new MotionAdapterResult(providerJobId, bytes, ProductionTestMp4.Mime, duration, string.Empty));
        }

        public Task<MotionAdapterResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
            => MotionExecutionLifecycle.ExecuteViaLifecycleAsync(this, snapshot, ct);
    }
}
