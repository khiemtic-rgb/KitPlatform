using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionCreateVideoFreezeIntegrityV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_FREEZE_INTEGRITY_V1";

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
        await T_DirectorTimingAuthoritativeWinsClaim(Ok);
        await T_AllTwelveFieldsMatchPass(Ok);
        await T_EachFieldMismatchFailsClosed(Ok);
        await T_LatestPerformanceSnapshotAfterFreezeFails(Ok);
        await T_DirectorTimingAfterFreezeFails(Ok);
        await T_KeyframeIdOrHashOnlyChangeFails(Ok);
        await T_NormalCreateVideoRetryFailClosed(Ok);
        await T_PreparedCreateVideoFailClosed(Ok);
    }

    static async Task T_DirectorTimingAuthoritativeWinsClaim(Action<bool, string> Ok)
    {
        var kf = FakeKeyframe();
        var os = new CreateVideoOsFacts(
            kf, Scope(), Story, Acting, Visual, "10.0s", 10, DirectorTimingAuthoritative: true);
        var claim = new CreateVideoReadinessClaim(
            Story, Acting, Visual, "5s", 5, kf.ArtifactId, PerformanceSnapshotId: Guid.NewGuid(),
            PerformanceHash: "PERFORMANCE_HASH_V1:" + new string('a', 64));
        var verdict = CreateVideoActionContractGate.Evaluate(Scope(), os, claim, "fixture", "fixture-motion-v1");
        Ok(verdict.DurationSec == 10 && verdict.Timing == "10.0s", "director timing 10s wins claim 5s");

        var noDirector = os with { DirectorTimingAuthoritative = false, DurationSec = 0, Timing = "" };
        var claimed = CreateVideoActionContractGate.Evaluate(Scope(), noDirector, claim, "fixture", "fixture-motion-v1");
        Ok(claimed.DurationSec == 5 && claimed.Timing == "5s", "without director, claim duration still applies");
    }

    static async Task T_AllTwelveFieldsMatchPass(Action<bool, string> Ok)
    {
        var (frozen, live, adapter, _) = await FreezeFixture();
        Ok(ProductionFrozenCreateVideoMatch.FirstMismatch(frozen, live, adapter) is null, "all 12 fields match PASS");
        ProductionFrozenCreateVideoMatch.Assert(frozen, live, adapter);
        await Task.CompletedTask;
    }

    static async Task T_EachFieldMismatchFailsClosed(Action<bool, string> Ok)
    {
        var (frozen, live, adapter, snapB) = await FreezeFixture();
        Expect(Ok, frozen, live, new NamedMotionAdapter("runway", adapter.ModelId), "ProviderId", ProductionFailureReasons.ProviderChanged);
        Expect(Ok, frozen, live, new NamedMotionAdapter(adapter.ProviderId, "gen4_turbo"), "ModelId", ProductionFailureReasons.ModelChanged);
        Expect(Ok, frozen, live with { Story = live.Story + " changed" }, adapter, "Story");
        Expect(Ok, frozen, live with { ActingBeat = live.ActingBeat + " changed" }, adapter, "ActingBeat");
        Expect(Ok, frozen, live with { VisualContract = live.VisualContract + " changed" }, adapter, "VisualContract");
        Expect(Ok, frozen, live with { DurationSec = 5 }, adapter, "DurationSec");
        Expect(Ok, frozen, live with { Timing = "5s" }, adapter, "TimingHash");
        Expect(Ok, frozen, live with { PerformanceSnapshotId = snapB.PerformanceSnapshotId, PerformanceHash = snapB.PerformanceHash }, adapter, "PerformanceSnapshotId");
        Expect(Ok, frozen, live with { PerformanceHash = snapB.PerformanceHash }, adapter, "PerformanceHash");
        Expect(Ok, frozen, live with { KeyframeArtifactId = Guid.NewGuid() }, adapter, "KeyframeArtifactId");
        Expect(Ok, frozen, live with { KeyframeByteSha256 = snapB.PerformanceHash.Replace("PERFORMANCE_HASH_V1:", "", StringComparison.Ordinal) }, adapter, "KeyframeHash");
        var tampered = frozen with { SnapshotHash = ShotExecutionCompiler.Hash("tampered-snapshot") };
        Expect(Ok, tampered, live, adapter, "SnapshotHash");
        await Task.CompletedTask;
    }

    static async Task T_LatestPerformanceSnapshotAfterFreezeFails(Action<bool, string> Ok)
    {
        var (frozen, live, adapter, later) = await FreezeFixture();
        var drifted = live with
        {
            PerformanceSnapshotId = later.PerformanceSnapshotId,
            PerformanceHash = later.PerformanceHash,
        };
        var msg = ProductionFrozenCreateVideoMatch.FirstMismatch(frozen, drifted, adapter);
        Ok(msg is not null
            && msg.Contains("PerformanceSnapshotId", StringComparison.Ordinal)
            && msg.Contains(frozen.PerformanceSnapshotId!.Value.ToString("D"), StringComparison.Ordinal)
            && msg.Contains(later.PerformanceSnapshotId.ToString("D"), StringComparison.Ordinal),
            "latest PerformanceSnapshot after freeze FAIL CLOSED");
        await Task.CompletedTask;
    }

    static async Task T_DirectorTimingAfterFreezeFails(Action<bool, string> Ok)
    {
        var (frozen, live, adapter, _) = await FreezeFixture();
        var drifted = live with { DurationSec = 5, Timing = "5.0s" };
        var msg = ProductionFrozenCreateVideoMatch.FirstMismatch(frozen, drifted, adapter);
        Ok(msg is not null
            && msg.Contains("DurationSec", StringComparison.Ordinal)
            && msg.Contains("frozen=10", StringComparison.Ordinal)
            && msg.Contains("live=5", StringComparison.Ordinal),
            "director timing change after freeze FAIL CLOSED");
        await Task.CompletedTask;
    }

    static async Task T_KeyframeIdOrHashOnlyChangeFails(Action<bool, string> Ok)
    {
        var (frozen, live, adapter, _) = await FreezeFixture();
        var otherId = live with { KeyframeArtifactId = Guid.NewGuid() };
        var idMsg = ProductionFrozenCreateVideoMatch.FirstMismatch(frozen, otherId, adapter);
        Ok(idMsg is not null && idMsg.Contains("KeyframeArtifactId", StringComparison.Ordinal),
            "KeyframeArtifactId change same hash FAIL CLOSED");

        var otherHash = live with { KeyframeByteSha256 = ShotExecutionCompiler.Hash("other-kf-bytes") };
        var hashMsg = ProductionFrozenCreateVideoMatch.FirstMismatch(frozen, otherHash, adapter);
        Ok(hashMsg is not null && hashMsg.Contains("KeyframeHash", StringComparison.Ordinal),
            "KeyframeHash change same id FAIL CLOSED");
        await Task.CompletedTask;
    }

    static async Task T_NormalCreateVideoRetryFailClosed(Action<bool, string> Ok)
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var perf = new InMemoryPerformanceSnapshotStore();
        var motion = new FixtureMotionAdapter();
        var os = new ProductionExecutionService(store, motion, performance: perf);
        var svc = new ProductionCreateVideoService(os, store, files, motion);
        var scope = Scope();
        var snap = await ProductionTestPerformanceFixture.FreezeAsync(perf, scope.BuildId, scope.ShotCode);
        var kf = await SeedKeyframe(os, scope, Attach(Input(10), snap));
        var input = Attach(Input(10) with
        {
            KeyframeArtifactId = kf.ArtifactId,
            KeyframeByteSha256 = kf.ByteSha256,
            KeyframeHash = kf.ByteSha256,
        }, snap);
        var first = await svc.CreateVideoAsync(new CreateVideoApiCommand(scope, input, "test", "freeze-retry", null, null));
        Ok(first.Attempt.ProviderJobId is not null
            && first.Attempt.ProviderJobId.StartsWith(FixtureMotionAdapter.JobPrefix, StringComparison.Ordinal),
            "normal CREATE_VIDEO uses fixture only");

        var frozen = await store.GetSnapshotByTaskAsync(first.Task.TaskId)
            ?? throw new InvalidOperationException("INPUT_NOT_FROZEN");
        var frozenLive = LiveOf(frozen, input);
        var beforeAttempts = await store.ListAttemptsAsync(first.Task.TaskId);
        var same = await svc.CreateVideoAsync(new CreateVideoApiCommand(scope, frozenLive, "test", "freeze-retry", null, null));
        var afterReplay = await store.GetSnapshotByTaskAsync(first.Task.TaskId);
        var replayAttempts = await store.ListAttemptsAsync(first.Task.TaskId);
        Ok(same.Request.RequestId == first.Request.RequestId
            && same.Task.TaskId == first.Task.TaskId
            && same.Task.Status == ProductionTaskStatuses.Succeeded
            && same.Artifact.ArtifactId == first.Artifact.ArtifactId
            && afterReplay is not null
            && afterReplay.SnapshotId == frozen.SnapshotId
            && afterReplay.SnapshotHash == frozen.SnapshotHash
            && replayAttempts.Count == beforeAttempts.Count
            && replayAttempts.Count == 1
            && replayAttempts[0].AttemptId == first.Attempt.AttemptId
            && replayAttempts[0].ProviderJobId == first.Attempt.ProviderJobId,
            "retry same frozen input PASSES");

        var mutated = frozenLive with { DurationSec = 5, Timing = "5s" };
        var durationFailed = false;
        try
        {
            await svc.CreateVideoAsync(new CreateVideoApiCommand(scope, mutated, "test", "freeze-retry", null, null));
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("DurationSec", StringComparison.Ordinal)
            && ex.Message.Contains("frozen=", StringComparison.Ordinal)
            && ex.Message.Contains("live=", StringComparison.Ordinal))
        {
            durationFailed = true;
        }
        var timingOnly = frozenLive with { Timing = "5s" };
        var timingFailed = false;
        try
        {
            await svc.CreateVideoAsync(new CreateVideoApiCommand(scope, timingOnly, "test", "freeze-retry", null, null));
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("TimingHash", StringComparison.Ordinal)
            && ex.Message.Contains("frozen=", StringComparison.Ordinal)
            && ex.Message.Contains("live=", StringComparison.Ordinal))
        {
            timingFailed = true;
        }
        var after = await store.GetSnapshotByTaskAsync(first.Task.TaskId);
        var attempts = await store.ListAttemptsAsync(first.Task.TaskId);
        var arts = await store.ListArtifactsByTaskAsync(first.Task.TaskId);
        var task = await store.GetTaskAsync(first.Task.TaskId);
        Ok(durationFailed && timingFailed, "normal CREATE_VIDEO retry mutated timing FAIL CLOSED");
        Ok(attempts.Count == 1
            && attempts[0].AttemptId == first.Attempt.AttemptId
            && attempts[0].ProviderJobId == first.Attempt.ProviderJobId
            && after is not null
            && after.SnapshotId == frozen.SnapshotId
            && after.SnapshotHash == frozen.SnapshotHash
            && arts.Count == 1
            && arts[0].ArtifactId == first.Artifact.ArtifactId
            && task is not null
            && task.Status == ProductionTaskStatuses.Succeeded,
            "mutated timing leaves the successful execution unchanged");
        Ok(attempts.All(a => (a.ProviderJobId ?? "").StartsWith(FixtureMotionAdapter.JobPrefix, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(a.ProviderJobId)),
            "no paid provider job on retry fail-closed");
    }

    static async Task T_PreparedCreateVideoFailClosed(Action<bool, string> Ok)
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var perf = new InMemoryPerformanceSnapshotStore();
        var motion = new FixtureMotionAdapter();
        var os = new ProductionExecutionService(store, motion, performance: perf);
        var svc = new ProductionCreateVideoService(os, store, files, motion);
        var scope = Scope();
        var snap = await ProductionTestPerformanceFixture.FreezeAsync(perf, scope.BuildId, scope.ShotCode);
        var kf = await SeedKeyframe(os, scope, Attach(Input(10), snap));
        var input = Attach(Input(10) with
        {
            KeyframeArtifactId = kf.ArtifactId,
            KeyframeByteSha256 = kf.ByteSha256,
            KeyframeHash = kf.ByteSha256,
        }, snap);
        var req = await os.CreateRequestAsync(scope, ProductionActions.CreateVideo, "test", "prepared-freeze");
        var task = await os.CreateTaskAsync(req.RequestId);
        var frozen = await os.FreezeInputAsync(task.TaskId, input, motion.ProviderId, motion.ModelId);
        var attempt = await os.StartAttemptAsync(task.TaskId, frozen.SnapshotHash);
        Ok(string.IsNullOrWhiteSpace(attempt.ProviderJobId), "prepared attempt has no provider job");

        var frozenLive = LiveOf(frozen, input);
        ProductionFrozenCreateVideoMatch.Assert(frozen, frozenLive, motion);
        Ok(true, "prepared matching frozen input PASS (assert only, no Start)");

        var mutated = input with { Story = input.Story + " late" };
        var failed = false;
        try
        {
            await svc.ExecutePreparedCreateVideoAsync(attempt.AttemptId, new CreateVideoApiCommand(
                scope, mutated, "test", "prepared-freeze", null, null, PreparedAttemptId: attempt.AttemptId));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Story", StringComparison.Ordinal))
        {
            failed = true;
        }
        var after = await store.GetAttemptAsync(attempt.AttemptId);
        Ok(failed && string.IsNullOrWhiteSpace(after?.ProviderJobId),
            "prepared CREATE_VIDEO late story FAIL CLOSED before Start");
    }

    static void Expect(
        Action<bool, string> Ok,
        ProductionTaskInputSnapshot frozen,
        ShotExecutionInput live,
        IMotionExecutionAdapter adapter,
        string field,
        string? code = null)
    {
        var msg = ProductionFrozenCreateVideoMatch.FirstMismatch(frozen, live, adapter);
        Ok(msg is not null
            && msg.Contains(field, StringComparison.Ordinal)
            && msg.Contains("frozen=", StringComparison.Ordinal)
            && msg.Contains("live=", StringComparison.Ordinal)
            && (code is null || msg.StartsWith(code, StringComparison.Ordinal)),
            field + " mismatch FAIL CLOSED");
    }

    static async Task<(ProductionTaskInputSnapshot Frozen, ShotExecutionInput Live, IMotionExecutionAdapter Adapter, PerformanceSnapshot Later)> FreezeFixture()
    {
        var store = new InMemoryProductionWorkStore();
        var perf = new InMemoryPerformanceSnapshotStore();
        var eng = new ProductionWorkExecutionEngine(store, perf);
        var scope = Scope();
        var first = await ProductionTestPerformanceFixture.FreezeAsync(perf, scope.BuildId, scope.ShotCode, "turns");
        var later = await ProductionTestPerformanceFixture.FreezeAsync(perf, scope.BuildId, scope.ShotCode, "sits");
        var kfId = Guid.Parse("91daaf4b-5550-4cbc-b9f9-0bd0be556cef");
        var kfHash = ShotExecutionCompiler.Hash("kf-91daaf4b");
        var input = Attach(Input(10) with
        {
            KeyframeArtifactId = kfId,
            KeyframeByteSha256 = kfHash,
            KeyframeHash = kfHash,
        }, first);
        var req = await eng.CreateRequestAsync(scope, ProductionActions.CreateVideo, "test");
        var task = await eng.CreateTaskAsync(req.RequestId);
        var frozen = await eng.FreezeInputAsync(task.TaskId, input, "fixture", "fixture-motion-v1");
        return (frozen, LiveOf(frozen, input), new FixtureMotionAdapter(), later);
    }

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

    static ProductionArtifact FakeKeyframe() =>
        new(
            Guid.Parse("91daaf4b-5550-4cbc-b9f9-0bd0be556cef"),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ProductionArtifactRoles.Keyframe,
            "image/png",
            64,
            0,
            "kf-sha",
            "kf/x.png",
            null,
            DateTimeOffset.UtcNow,
            ProductionArtifactStatuses.Verified,
            "");

    static async Task<ProductionArtifact> SeedKeyframe(
        IProductionExecutionService os,
        ProductionScope scope,
        ShotExecutionInput input)
    {
        var req = await os.CreateRequestAsync(scope, ProductionActions.CreateImage, "test", "kf-" + Guid.NewGuid().ToString("N"));
        var task = await os.CreateTaskAsync(req.RequestId);
        await os.FreezeInputAsync(task.TaskId, input, "import", "existing-pixels");
        var att = await os.StartAttemptAsync(task.TaskId, "kf-payload");
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-KF-FREEZE-" + scope.ShotCode + new string('K', 48));
        await os.SubmitProviderJobAsync(att.AttemptId, "img-freeze");
        await os.CompleteAttemptAsync(att.AttemptId, KitVideoIntegrityRules.Sha256Hex(bytes));
        var art = await os.VerifyArtifactAsync(att.AttemptId, bytes, "image/png", 0, "kf/" + scope.ShotCode + ".png");
        await os.BindCurrentAsync(art.ArtifactId);
        return art;
    }

    sealed class NamedMotionAdapter : IMotionExecutionAdapter
    {
        public NamedMotionAdapter(string providerId, string modelId)
        {
            ProviderId = providerId;
            ModelId = modelId;
        }

        public string ProviderId { get; }
        public string ModelId { get; }
        public bool IsPaid => false;
        public bool RequiresNetwork => false;

        public Task<MotionStartResult> StartFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
            => Task.FromResult(new MotionStartResult(FixtureMotionAdapter.JobPrefix + snapshot.TaskId.ToString("N")[..12]));

        public Task<MotionPollResult> PollFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
            => Task.FromResult(new MotionPollResult(MotionPollStatuses.Succeeded, providerJobId));

        public Task<MotionPollResult> RecoverFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
            => PollFrozenAsync(snapshot, providerJobId, ct);

        public Task<MotionAdapterResult> FetchOutputAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
            => Task.FromResult(new MotionAdapterResult(providerJobId, ProductionTestMp4.Encode("x", 5), ProductionTestMp4.Mime, 5, ""));

        public Task<MotionAdapterResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
            => MotionExecutionLifecycle.ExecuteViaLifecycleAsync(this, snapshot, ct);
    }
}
