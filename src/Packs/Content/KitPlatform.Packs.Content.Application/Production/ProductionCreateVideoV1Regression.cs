using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionCreateVideoV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_CREATE_VIDEO_CUTOVER_V1";

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
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var os = new ProductionExecutionService(store, new FixtureMotionAdapter());
        var svc = new ProductionCreateVideoService(os, store, files, new FixtureMotionAdapter());
        var build = FixtureBuild;
        Ok(!ProductionScopeAnchorRules.IsOfficialFilm(build), "fixture build is not official film");
        var sh01 = new ProductionScope(build, "EP01", "SC01", "EP01-SC01-SH01");
        var sh02 = new ProductionScope(build, "EP01", "SC01", "EP01-SC01-SH02");
        var storyA = "Minh ngập ngừng, đặt cặp xuống ghế rồi chậm chạp kéo khóa.";
        var storyB = "Minh ngồi sofa cầm giấy nhìn Linh.";
        var kfBytes = Encoding.UTF8.GetBytes("KF-PIXELS-" + new string('P', 48));

        var a = await svc.CreateVideoAsync(new CreateVideoApiCommand(
            sh01, Input(storyA), "test", null, kfBytes, "image/png"));
        Ok(a.Request.UserAction == ProductionActions.CreateVideo, "request is CREATE_VIDEO");
        Ok(a.Visible is not null && a.Current is not null, "CURRENT+VISIBLE bound");
        var mediaA = await svc.ReadPreviewMediaAsync(sh01);
        Ok(mediaA is { Length: >= 32 } && KitVideoIntegrityRules.Sha256Hex(mediaA) == a.Artifact.ByteSha256, "preview bytes = artifact");

        var frozenA = await store.GetSnapshotByTaskAsync(a.Task.TaskId);
        Ok(frozenA!.ActingBeat == ShortProductionQualityPipelineV1.Sh01FutureActing
            && frozenA.VisualContract == ShortProductionQualityPipelineV1.Sh01FutureVisual
            && frozenA.ActingBeat != "He stays in place.",
            "SH01 freezes approved continuity, not the stale hold line");
        var replayInput = Input(storyA) with
        {
            KeyframeArtifactId = frozenA!.KeyframeArtifactId,
            KeyframeByteSha256 = frozenA.KeyframeHash ?? "",
            ParentArtifactIds = frozenA.KeyframeArtifactId is { } replayKf ? [replayKf] : null,
        };
        var replay = await svc.CreateVideoAsync(new CreateVideoApiCommand(
            sh01, replayInput, "test", a.Request.IdempotencyKey, null, null));
        Ok(replay.Request.RequestId == a.Request.RequestId && replay.Artifact.ArtifactId == a.Artifact.ArtifactId, "duplicate idempotent");
        await ActingDriftStaysFailClosed(Ok, svc, store, build);

        var b = await svc.CreateVideoAsync(new CreateVideoApiCommand(
            sh02, Input(storyB), "test", null, Encoding.UTF8.GetBytes("KF-SH02-" + new string('Q', 48)), "image/png"));
        var prev1 = await svc.GetPreviewAsync(sh01);
        var prev2 = await svc.GetPreviewAsync(sh02);
        Ok(prev1.ArtifactId == a.Artifact.ArtifactId && prev2.ArtifactId == b.Artifact.ArtifactId, "cross-shot isolation");

        await os.InvalidateDownstreamAsync(sh01, ShotNodeIds.Story);
        var after = await svc.GetPreviewAsync(sh01);
        Ok(after.IsEmpty, "story change clears VISIBLE");
        var old = await store.GetArtifactAsync(a.Artifact.ArtifactId);
        Ok(old is not null && old.ByteSha256 == a.Artifact.ByteSha256, "old artifact immutable");

        var again = await svc.CreateVideoAsync(new CreateVideoApiCommand(
            sh01, Input("Minh mở cửa và bước vào nhà."), "test", null, kfBytes, "image/png"));
        Ok(again.Task.TaskId != a.Task.TaskId, "new story → new task");

        var actions = await svc.GetActionsAsync(sh02);
        var create = actions.Single(x => x.Action == ProductionActions.CreateVideo);
        Ok(create.Available && create.UiCta == "Tạo video", "GET actions CREATE_VIDEO from contract");

        var eng = new ProductionWorkExecutionEngine(store);
        var lateReq = await eng.CreateRequestAsync(sh02, ProductionActions.CreateVideo, "test", "late-two-attempts");
        var lateTask = await eng.CreateTaskAsync(lateReq.RequestId);
        await eng.FreezeInputAsync(lateTask.TaskId, Input("late"), "fixture", "fixture-motion-v1");
        var att1 = await eng.StartAttemptAsync(lateTask.TaskId, "payload-1");
        var att2 = await eng.StartAttemptAsync(lateTask.TaskId, "payload-2");
        await eng.SubmitProviderJobAsync(att2.AttemptId, "job-2");
        await eng.RecordProviderSucceededAsync(att2.AttemptId, "hash-2");
        var bytes2 = ProductionTestMp4.Encode("late-a2", 5);
        var art2 = await eng.VerifyArtifactAsync(att2.AttemptId, bytes2, "video/mp4", 5, "os/late-a2");
        await os.BindCurrentAsync(art2.ArtifactId);
        await os.BindVisibleAsync(art2.ArtifactId);
        var late = await eng.ApplyLateResultAsync(att1.AttemptId, Encoding.UTF8.GetBytes("FAMIXA-LATE-A1-" + new string('A', 48)));
        var cur = await store.GetActiveBindingAsync(build, sh02.ShotCode, ProductionBindingRoles.Current);
        var vis = await store.GetActiveBindingAsync(build, sh02.ShotCode, ProductionBindingRoles.Visible);
        Ok(late.Status == ProductionAttemptStatuses.Late
            && cur?.ArtifactId == art2.ArtifactId
            && vis?.ArtifactId == art2.ArtifactId, "late Attempt 1 cannot replace CURRENT/VISIBLE");
    }

    static async Task ActingDriftStaysFailClosed(
        Action<bool, string> Ok, ProductionCreateVideoService svc, InMemoryProductionWorkStore store, Guid build)
    {
        var heldScope = new ProductionScope(build, "EP01", "SC01", "EP01-SC01-SH99");
        var held = await svc.CreateVideoAsync(new CreateVideoApiCommand(
            heldScope, Input("A child stays by the door."), "test", null,
            Encoding.UTF8.GetBytes("KF-HELD-" + new string('H', 48)), "image/png"));
        var frozenHeld = await store.GetSnapshotByTaskAsync(held.Task.TaskId);
        Ok(frozenHeld!.ActingBeat == "He stays in place.", "non-contract shot freezes the caller acting");
        var drifted = false;
        try
        {
            var driftedInput = Input("A child stays by the door.") with
            {
                ActingBeat = "He walks away.",
                KeyframeArtifactId = frozenHeld.KeyframeArtifactId,
                KeyframeByteSha256 = frozenHeld.KeyframeHash ?? "",
            };
            await svc.CreateVideoAsync(new CreateVideoApiCommand(
                heldScope, driftedInput, "test", held.Request.IdempotencyKey, null, null));
        }
        catch (InvalidOperationException ex) when (
            ex.Message.StartsWith(ProductionFailureReasons.InputSnapshotMismatch + " · ActingBeat ·", StringComparison.Ordinal)
            && ex.Message.Contains("frozen=He stays in place.", StringComparison.Ordinal)
            && ex.Message.Contains("live=He walks away.", StringComparison.Ordinal))
        {
            drifted = true;
        }
        Ok(drifted, "acting drift remains INPUT_SNAPSHOT_MISMATCH");
    }

    static readonly Guid FixtureBuild = Guid.Parse("11111111-1111-4111-8111-111111111101");

    static ShotExecutionInput Input(string story) =>
        new(story, "He stays in place.", "visual", "CHAR-001", "", "5s", "", "motion",
            null, "", "16:9", 5, ShotExecutionCompiler.Version, null);
}
