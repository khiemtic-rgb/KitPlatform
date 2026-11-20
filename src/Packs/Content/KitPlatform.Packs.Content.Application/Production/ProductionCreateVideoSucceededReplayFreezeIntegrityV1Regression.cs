using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// CHECKPOINT 5A: SUCCEEDED CREATE_VIDEO replay must AssertFrozenMatch before returning the old artifact.
/// Seeds SUCCEEDED via the work engine only — no adapter StartFrozen / no paid provider.
/// </summary>
public static class ProductionCreateVideoSucceededReplayFreezeIntegrityV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_SUCCEEDED_REPLAY_FREEZE_INTEGRITY_V1";
    const string SeedJobId = "replay-seed-no-provider";

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
        await T_SucceededReplayIdenticalPass(Ok);
        await T_SucceededReplayDurationSecFails(Ok);
        await T_SucceededReplayPerformanceIdHashFails(Ok);
        await T_SucceededReplayKeyframeArtifactIdFails(Ok);
        await T_SucceededReplayKeyframeHashFails(Ok);
        await T_SucceededReplayStoryFails(Ok);
    }

    static async Task T_SucceededReplayIdenticalPass(Action<bool, string> Ok)
    {
        var h = await SeedSucceeded();
        var live = LiveOf(h.Frozen, h.Seed);
        var replay = await h.Svc.CreateVideoAsync(new CreateVideoApiCommand(h.Scope, live, "test", h.Idem, null, null));
        Ok(replay.Request.RequestId == h.Request.RequestId, "SUCCEEDED replay identical reuses request");
        Ok(replay.Artifact.ArtifactId == h.Artifact.ArtifactId, "SUCCEEDED replay identical returns old artifact");
        Ok(replay.Task.Status == ProductionTaskStatuses.Succeeded, "SUCCEEDED replay identical keeps SUCCEEDED");
        var attempts = await h.Store.ListAttemptsAsync(h.Task.TaskId);
        Ok(attempts.Count == 1 && attempts[0].ProviderJobId == SeedJobId,
            "SUCCEEDED replay identical does not start a new job");
    }

    static async Task T_SucceededReplayDurationSecFails(Action<bool, string> Ok)
        => await ReplayMutatedFails(Ok, live => live with { DurationSec = 5 }, "DurationSec");

    static async Task T_SucceededReplayPerformanceIdHashFails(Action<bool, string> Ok)
        => await ReplayMutatedFails(Ok, live => live with
        {
            PerformanceSnapshotId = live.PerformanceSnapshotId,
            PerformanceHash = live.PerformanceHash,
        }, "PerformanceSnapshotId", overlayLater: true);

    static async Task T_SucceededReplayKeyframeArtifactIdFails(Action<bool, string> Ok)
        => await ReplayMutatedFails(Ok, live => live with { KeyframeArtifactId = Guid.NewGuid() }, "KeyframeArtifactId");

    static async Task T_SucceededReplayKeyframeHashFails(Action<bool, string> Ok)
        => await ReplayMutatedFails(Ok, live =>
        {
            var other = ShotExecutionCompiler.Hash("other-kf-bytes-5a");
            return live with { KeyframeByteSha256 = other, KeyframeHash = other };
        }, "KeyframeHash");

    static async Task T_SucceededReplayStoryFails(Action<bool, string> Ok)
        => await ReplayMutatedFails(Ok, live => live with { Story = live.Story + " changed" }, "Story");

    static async Task ReplayMutatedFails(
        Action<bool, string> Ok,
        Func<ShotExecutionInput, ShotExecutionInput> mutate,
        string field,
        bool overlayLater = false)
    {
        var h = await SeedSucceeded();
        var live = LiveOf(h.Frozen, h.Seed);
        if (overlayLater)
        {
            live = live with
            {
                PerformanceSnapshotId = h.Later.PerformanceSnapshotId,
                PerformanceHash = h.Later.PerformanceHash,
                PerformancePayloadJson = h.Later.PayloadJson,
            };
        }
        else
        {
            live = mutate(live);
        }

        var failed = false;
        try
        {
            await h.Svc.CreateVideoAsync(new CreateVideoApiCommand(h.Scope, live, "test", h.Idem, null, null));
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains(field, StringComparison.Ordinal)
            && ex.Message.Contains("frozen=", StringComparison.Ordinal)
            && ex.Message.Contains("live=", StringComparison.Ordinal))
        {
            failed = true;
        }

        var attempts = await h.Store.ListAttemptsAsync(h.Task.TaskId);
        var arts = await h.Store.ListArtifactsByTaskAsync(h.Task.TaskId);
        Ok(failed, "SUCCEEDED replay " + field + " FAIL CLOSED");
        Ok(attempts.Count == 1 && attempts[0].ProviderJobId == SeedJobId,
            "SUCCEEDED replay " + field + " does not start a new job");
        Ok(arts.Count == 1 && arts[0].ArtifactId == h.Artifact.ArtifactId,
            "SUCCEEDED replay " + field + " does not return or mint a new artifact");
    }

    static async Task<Harness> SeedSucceeded()
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var perf = new InMemoryPerformanceSnapshotStore();
        var motion = new FixtureMotionAdapter();
        var os = new ProductionExecutionService(store, motion, performance: perf);
        var svc = new ProductionCreateVideoService(os, store, files, motion);
        var scope = Scope();
        var first = await ProductionTestPerformanceFixture.FreezeAsync(perf, scope.BuildId, scope.ShotCode);
        var later = await ProductionTestPerformanceFixture.FreezeAsync(perf, scope.BuildId, scope.ShotCode, "sits");
        var kf = await SeedKeyframe(os, scope, Attach(Input(10), first));
        var seed = Attach(Input(10) with
        {
            KeyframeArtifactId = kf.ArtifactId,
            KeyframeByteSha256 = kf.ByteSha256,
            KeyframeHash = kf.ByteSha256,
        }, first);
        var idem = "5a-succeeded-replay";
        var request = await os.CreateRequestAsync(scope, ProductionActions.CreateVideo, "test", idem);
        var task = await os.CreateTaskAsync(request.RequestId);
        var frozen = await os.FreezeInputAsync(task.TaskId, seed, motion.ProviderId, motion.ModelId);
        var att = await os.StartAttemptAsync(task.TaskId, frozen.SnapshotHash);
        var bytes = ProductionTestMp4.Encode("5a-replay-seed", 10);
        await os.SubmitProviderJobAsync(att.AttemptId, SeedJobId);
        await os.CompleteAttemptAsync(att.AttemptId, KitVideoIntegrityRules.Sha256Hex(bytes));
        var art = await os.VerifyArtifactAsync(att.AttemptId, bytes, ProductionTestMp4.Mime, 10, "5a/" + scope.ShotCode + ".mp4");
        var succeeded = await store.GetTaskAsync(task.TaskId)
            ?? throw new InvalidOperationException("TASK_MISSING");
        if (succeeded.Status != ProductionTaskStatuses.Succeeded)
            throw new InvalidOperationException("SEED_NOT_SUCCEEDED:" + succeeded.Status);
        return new Harness(store, svc, scope, seed, frozen, request, succeeded, art, idem, later);
    }

    sealed record Harness(
        InMemoryProductionWorkStore Store,
        ProductionCreateVideoService Svc,
        ProductionScope Scope,
        ShotExecutionInput Seed,
        ProductionTaskInputSnapshot Frozen,
        ProductionRequest Request,
        ProductionTask Task,
        ProductionArtifact Artifact,
        string Idem,
        PerformanceSnapshot Later);

    static ShotExecutionInput LiveOf(ProductionTaskInputSnapshot frozen, ShotExecutionInput seed) =>
        seed with
        {
            Story = frozen.Story,
            ActingBeat = frozen.ActingBeat,
            VisualContract = frozen.VisualContract,
            Timing = seed.Timing,
            DurationSec = frozen.DurationSec,
            KeyframeArtifactId = frozen.KeyframeArtifactId,
            KeyframeByteSha256 = frozen.KeyframeHash,
            KeyframeHash = frozen.KeyframeHash,
            PerformanceSnapshotId = frozen.PerformanceSnapshotId,
            PerformanceHash = frozen.PerformanceHash,
            PerformancePayloadJson = frozen.PerformancePayloadJson,
        };

    static ShotExecutionInput Attach(ShotExecutionInput input, PerformanceSnapshot snap) =>
        ProductionTestPerformanceFixture.Attach(input, snap);

    static ProductionScope Scope() =>
        new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), "EP99", "SC01", "EP99-SC01-SH99");

    const string Story = "Minh ngập ngừng, đặt cặp xuống ghế rồi chậm chạp kéo khóa.";
    const string Acting = "He stays in place.";
    const string Visual = "visual 16:9";

    static ShotExecutionInput Input(decimal duration) =>
        new(Story, Acting, Visual, "CHAR-001", "",
            duration.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s",
            "", "motion",
            null, "", "16:9", duration, ShotExecutionCompiler.Version, null);

    static async Task<ProductionArtifact> SeedKeyframe(
        IProductionExecutionService os,
        ProductionScope scope,
        ShotExecutionInput input)
    {
        var req = await os.CreateRequestAsync(scope, ProductionActions.CreateImage, "test", "kf-" + Guid.NewGuid().ToString("N"));
        var task = await os.CreateTaskAsync(req.RequestId);
        await os.FreezeInputAsync(task.TaskId, input, "import", "existing-pixels");
        var att = await os.StartAttemptAsync(task.TaskId, "kf-payload");
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-KF-5A-" + scope.ShotCode + new string('K', 48));
        await os.SubmitProviderJobAsync(att.AttemptId, "img-5a");
        await os.CompleteAttemptAsync(att.AttemptId, KitVideoIntegrityRules.Sha256Hex(bytes));
        var art = await os.VerifyArtifactAsync(att.AttemptId, bytes, "image/png", 0, "kf/" + scope.ShotCode + ".png");
        await os.BindCurrentAsync(art.ArtifactId);
        return art;
    }
}
