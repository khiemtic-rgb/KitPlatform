using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionPipelineV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_PIPELINE_V1_REGRESSION";

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
        Ok(ShotDependencyGraph.DownstreamOf(ShotNodeIds.Keyframe).Contains(ShotNodeIds.Motion), "graph keyframe→motion");
        Ok(!ShotDependencyGraph.DownstreamOf(ShotNodeIds.Motion).Contains(ShotNodeIds.Keyframe), "graph does not reverse");

        var store = new InMemoryProductionWorkStore();
        var svc = new ProductionExecutionService(store, new FixtureMotionAdapter());
        // Generic fixture-motion slice. OfficialEp01 requires an active video selection.
        var build = FixtureBuild;
        Ok(!ProductionScopeAnchorRules.IsOfficialFilm(build), "fixture build is not official film");
        var sh02 = new ProductionScope(build, "EP01", "SC01", "EP01-SC01-SH02");
        var sh01 = new ProductionScope(build, "EP01", "SC01", "EP01-SC01-SH01");
        var story = "Minh ngập ngừng, đặt cặp xuống ghế rồi chậm chạp kéo khóa.";

        var blocked = false;
        try
        {
            await svc.ExecuteCreateVideoFixtureAsync(new CreateVideoSliceCommand(
                sh02, Input(story, null), "test", "vid-no-kf", true, true));
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.MissingKeyframe)
        {
            blocked = true;
        }
        Ok(blocked, "CREATE_VIDEO blocked without keyframe artifact");

        var actions0 = await svc.GetAvailableActionsAsync(sh02);
        var create0 = actions0.Single(a => a.Action == ProductionActions.CreateVideo);
        Ok(create0.Available == false, "actions hide CREATE_VIDEO until keyframe");
        Ok(create0.BlockReason == ProductionFailureReasons.MissingKeyframe, "GET /actions reason is MISSING_KEYFRAME");
        Ok(create0.MissingInputs is not null && create0.MissingInputs.Contains("ApprovedKeyframe"), "missingInputs lists ApprovedKeyframe");
        Ok(create0.UiCta == "Tạo video", "CTA Tạo video stays CREATE_VIDEO");
        Ok(actions0.Single(a => a.Action == ProductionActions.CreateLipsync).UiCta == "Khớp môi", "CTA Khớp môi stays CREATE_LIPSYNC");

        var kf = await SeedKeyframe(svc, sh02, story);
        await svc.BindCurrentAsync(kf.ArtifactId);
        var actions1 = await svc.GetAvailableActionsAsync(sh02);
        Ok(actions1.Single(a => a.Action == ProductionActions.CreateVideo).Available, "CREATE_VIDEO available after keyframe CURRENT");

        var slice = await svc.ExecuteCreateVideoFixtureAsync(new CreateVideoSliceCommand(
            sh02, Input(story, kf.ArtifactId, kf.ByteSha256), "test", "vid-sh02", true, true));
        Ok(slice.Request.UserAction == ProductionActions.CreateVideo, "slice action is CREATE_VIDEO");
        Ok(slice.Artifact.ParentArtifactIds?.Contains(kf.ArtifactId) == true, "video lineage names keyframe artifact");
        Ok(slice.Visible is not null && slice.Current is not null, "explicit CURRENT+VISIBLE binds");
        Ok(slice.Attempt.ProviderId == "fixture" && !new FixtureMotionAdapter().IsPaid, "fixture adapter unpaid");

        var preview = await svc.GetPreviewAsync(sh02);
        Ok(preview.ArtifactId == slice.Artifact.ArtifactId && preview.ByteSha256 == slice.Artifact.ByteSha256, "preview is VISIBLE artifact");

        var emptySh01 = await svc.GetPreviewAsync(sh01);
        Ok(emptySh01.IsEmpty, "SH01 preview empty — no SH02 steal");

        var kf1 = await SeedKeyframe(svc, sh01, "Minh đứng ở cửa nhìn Linh.");
        await svc.BindCurrentAsync(kf1.ArtifactId);
        var a = await svc.ExecuteCreateVideoFixtureAsync(new CreateVideoSliceCommand(
            sh01, Input("Minh đứng ở cửa nhìn Linh.", kf1.ArtifactId, kf1.ByteSha256), "test", "vid-sh01", true, true));
        Ok(a.Artifact.ArtifactId != slice.Artifact.ArtifactId, "A and B distinct artifacts");

        var afterReload = await svc.GetPreviewAsync(sh02);
        Ok(afterReload.ArtifactId == slice.Artifact.ArtifactId, "reload uses VISIBLE binding");

        await svc.InvalidateDownstreamAsync(sh02, ShotNodeIds.Story);
        var state = await svc.GetShotStateAsync(sh02);
        var motion = state.Nodes.First(n => n.Node == ShotNodeIds.Motion);
        Ok(motion.Status == ShotNodeStatuses.Stale || state.VisibleArtifactId is null, "story change invalidates motion current/visible");
        var old = await store.GetArtifactAsync(slice.Artifact.ArtifactId);
        Ok(old is not null && old.ByteSha256 == slice.Artifact.ByteSha256, "old video artifact immutable after invalidate");
    }

    static readonly Guid FixtureBuild = Guid.Parse("11111111-1111-4111-8111-111111111101");

    static async Task<ProductionArtifact> SeedKeyframe(IProductionExecutionService svc, ProductionScope scope, string story)
    {
        var req = await svc.CreateRequestAsync(scope, ProductionActions.CreateImage, "test", "kf-" + scope.ShotCode);
        var task = await svc.CreateTaskAsync(req.RequestId);
        await svc.FreezeInputAsync(task.TaskId, Input(story, null), "fixture", "fixture-image-v1");
        var att = await svc.StartAttemptAsync(task.TaskId, "kf-payload");
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-KF-" + scope.ShotCode + new string('K', 48));
        await svc.SubmitProviderJobAsync(att.AttemptId, "img-" + scope.ShotCode);
        await svc.CompleteAttemptAsync(att.AttemptId, KitVideoIntegrityRules.Sha256Hex(bytes));
        return await svc.VerifyArtifactAsync(att.AttemptId, bytes, "image/png", 0, "kf/" + scope.ShotCode + ".png");
    }

    static ShotExecutionInput Input(string story, Guid? keyframeId, string keyframeSha = "") =>
        new(story, "He stays in place.", "visual 16:9", "CHAR-001",
            keyframeSha.Length == 64 ? keyframeSha : ShotExecutionCompiler.Hash("kf-" + story),
            "5s", "", "motion", keyframeId, keyframeSha, "16:9", 5, ShotExecutionCompiler.Version,
            keyframeId is { } id ? [id] : null);
}
