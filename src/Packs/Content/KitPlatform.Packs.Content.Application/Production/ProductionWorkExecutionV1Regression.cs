using System.Text;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production.Legacy;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionWorkExecutionV1Regression
{
    public const string SuiteId = ProductionWorkIds.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        try
        {
            RunAsync(Ok).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            fail.Add("UNCAUGHT:" + ex.GetType().Name + ":" + ex.Message);
        }

        return fail;
    }

    static async Task RunAsync(Action<bool, string> Ok)
    {
        Ok(ProductionWorkIds.ArchitectureId == "FAMIXA_PRODUCTION_OS_WORK_EXECUTION_V1", "arch id");
        Ok(!ProductionStateMachines.ProviderSuccessIsProductionSuccess(), "T06 provider SUCCESS ≠ production success");
        Ok(!ProductionStateMachines.ProviderSuccessIsVisible(), "T06 provider SUCCESS ≠ VISIBLE");
        Ok(!ProductionStateMachines.ProviderSuccessIsAccepted(), "T06 provider SUCCESS ≠ ACCEPTED");
        Ok(!ProductionStateMachines.GenericInputChangedIsLegalState(), "INPUT_CHANGED retired");
        Ok(!LegacySeriesGraphAdapter.IsAuthoritative(), "legacy graph is not authority");
        Ok(ProductionUiActionMap.RequireAction("Tạo video") == ProductionActions.CreateVideo, "T12 CTA Tạo video");
        Ok(ProductionUiActionMap.RequireAction("Khớp môi") == ProductionActions.CreateLipsync, "T13 CTA Khớp môi");
        Ok(ProductionActionContractRegistry.CreateVideo.ForbiddenFallbacks.Contains(ProductionActions.CreateLipsync), "CREATE_VIDEO cannot fall to LipSync");

        var storyA = "Minh ngập ngừng, đặt cặp xuống ghế rồi chậm chạp kéo khóa.";
        var compiled = ShotExecutionCompiler.Compile(new ShotExecutionInput(
            storyA, "He stays in place.", "sofa 16:9", "CHAR-001", Hash("kf-a"), "5s", "", "hold"));
        Ok(compiled.Story == storyA && compiled.CompilerOutput.Contains("cặp", StringComparison.Ordinal), "T14 story retained");
        Ok(compiled.CompilerOutput.Contains("STORY:", StringComparison.Ordinal)
            && compiled.CompilerOutput.Contains(storyA, StringComparison.Ordinal), "T14 hold-body cannot drop story");

        var store = new InMemoryProductionWorkStore();
        var eng = new ProductionWorkExecutionEngine(store);
        var build = Guid.Parse("4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f");
        var sh01 = new ProductionScope(build, "EP01", "SC01", "EP01-SC01-SH01");
        var sh02 = new ProductionScope(build, "EP01", "SC01", "EP01-SC01-SH02");
        var bytesA = ProductionTestMp4.Encode("work-a", 5);
        var bytesB = ProductionTestMp4.Encode("work-b", 5);
        Ok(KitVideoIntegrityRules.Sha256Hex(bytesA) == KitVideoIntegrityRules.Sha256Hex(bytesA), "T16 same bytes same sha");
        Ok(KitVideoIntegrityRules.Sha256Hex(bytesA) != KitVideoIntegrityRules.Sha256Hex(bytesB), "T17 different bytes different sha");

        var a = await SliceCreateVideo(eng, sh01, storyA, bytesA, "idem-a");
        Ok(a.Task.Scope.ShotCode == sh01.ShotCode && a.Artifact.TaskId == a.Task.TaskId, "T01 CREATE_VIDEO A belongs to A");

        var b = await SliceCreateVideo(eng, sh02, "Minh ngồi sofa cầm giấy nhìn Linh.", bytesB, "idem-b");
        Ok(b.Task.Scope.ShotCode == sh02.ShotCode && b.Artifact.TaskId == b.Task.TaskId, "T02 CREATE_VIDEO B belongs to B");
        Ok(a.Artifact.ArtifactId != b.Artifact.ArtifactId
            && a.Artifact.ByteSha256 != b.Artifact.ByteSha256, "T03 concurrent no contamination");

        var cross = false;
        try { await eng.RejectCrossShotVisibleAsync(a.Artifact.ArtifactId, sh02.BuildId, sh02.ShotCode); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.CrossShotForbidden) { cross = true; }
        Ok(cross, "T04 SH01 artifact cannot become SH02 visible");

        var emptyPreview = await eng.ResolveVisiblePreviewAsync(build, sh02.ShotCode);
        Ok(emptyPreview.IsEmpty, "T06 SUCCESS artifact is not VISIBLE until bind");

        var noBytes = false;
        try
        {
            var req = await eng.CreateRequestAsync(sh02, ProductionActions.CreateVideo, "test", "idem-noid");
            var task = await eng.CreateTaskAsync(req.RequestId);
            await eng.FreezeInputAsync(task.TaskId, Input(storyA, "kf-empty"), "google", "veo-3.1");
            var att = await eng.StartAttemptAsync(task.TaskId, "p");
            await eng.SubmitProviderJobAsync(att.AttemptId, "veo_empty");
            await eng.RecordProviderSucceededAsync(att.AttemptId, "r");
            await eng.VerifyArtifactAsync(att.AttemptId, [], "video/mp4", 0, "missing");
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.FailedIdentity) { noBytes = true; }
        Ok(noBytes, "T05 SUCCESS without artifact → FAILED_IDENTITY");

        await eng.BindAsync(b.Artifact.ArtifactId, ProductionBindingRoles.Current);
        await eng.BindAsync(b.Artifact.ArtifactId, ProductionBindingRoles.Visible);
        var preview = await eng.ResolveVisiblePreviewAsync(build, sh02.ShotCode);
        Ok(preview.ArtifactId == b.Artifact.ArtifactId && preview.ByteSha256 == b.Artifact.ByteSha256, "T07 VISIBLE preview is exact artifact");

        var retry = await eng.RetryAsync(b.Task.TaskId, "retry-payload");
        Ok(retry.AttemptNumber == 2 && retry.AttemptId != b.Attempt.AttemptId, "T11 retry new Attempt");
        var late = await eng.ApplyLateResultAsync(b.Attempt.AttemptId, bytesA);
        var current = await store.GetActiveBindingAsync(build, sh02.ShotCode, ProductionBindingRoles.Current);
        Ok(late.Status == ProductionAttemptStatuses.Late
            && current is not null && current.ArtifactId == b.Artifact.ArtifactId, "T08 late result cannot replace CURRENT");

        var modelTask = await eng.NewTaskForProviderChangeAsync(b.Task.TaskId, "google", "veo-3.2", "test");
        Ok(modelTask.TaskId != b.Task.TaskId && modelTask.ModelId == "veo-3.2", "T09 model change new Task");

        var inputTask = await eng.NewTaskForChangedInputAsync(
            a.Task.TaskId,
            Input("Minh mở cửa và bước vào nhà.", "kf-changed"),
            "test");
        Ok(inputTask.TaskId != a.Task.TaskId, "T10 input change new Task");
        var superseded = await store.GetTaskAsync(a.Task.TaskId);
        Ok(superseded?.Status == ProductionTaskStatuses.Superseded
            && superseded.FailureCode == ProductionFailureReasons.StoryChanged, "T10 explicit STORY_CHANGED");

        var remap = false;
        try { await eng.RefuseRemapAsync(ProductionActions.CreateVideo, ProductionActions.CreateLipsync); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.ActionRemapForbidden) { remap = true; }
        Ok(remap, "T12 request cannot remap to LipSync");

        var again = await eng.CreateRequestAsync(sh01, ProductionActions.CreateVideo, "test", "idem-a");
        Ok(again.RequestId == a.Request.RequestId, "T15 duplicate request idempotent");

        var cache = new Dictionary<string, string> { ["localStorage.takeUrl"] = "blob:old" };
        cache.Clear();
        var afterReload = await eng.ResolveVisiblePreviewAsync(build, sh02.ShotCode);
        Ok(afterReload.ArtifactId == b.Artifact.ArtifactId, "T18 reload uses binding not localStorage");
        Ok(afterReload.ArtifactId == b.Artifact.ArtifactId, "T19 IndexedDB cleared metadata remains");

        var unavail = false;
        var reqU = await eng.CreateRequestAsync(sh01, ProductionActions.CreateVideo, "test", "idem-unavail");
        var taskU = await eng.CreateTaskAsync(reqU.RequestId);
        await eng.FreezeInputAsync(taskU.TaskId, Input(storyA, "kf-u"), "runway", "gen4_turbo");
        await eng.FailClosedProviderUnavailableAsync(taskU.TaskId);
        var failed = await store.GetTaskAsync(taskU.TaskId);
        try { await eng.RefuseRemapAsync(ProductionActions.CreateVideo, ProductionActions.CreateImage); }
        catch (InvalidOperationException) { unavail = true; }
        Ok(failed?.Status == ProductionTaskStatuses.Failed
            && failed.FailureCode == ProductionFailureReasons.ProviderUnavailable
            && unavail, "T20 unavailable fail closed no fallback");

        var mixReq = await eng.CreateRequestAsync(sh02, ProductionActions.MixVideo, "test", "idem-mix");
        var mix = await eng.MixExplicitAsync(mixReq.RequestId, b.Artifact.ArtifactId, ProductionTestMp4.Encode("work-mix", 5));
        Ok(mix.Role == ProductionArtifactRoles.Mix && mix.TaskId != b.Task.TaskId, "T21 mix uses explicit artifactId");

        var film = await eng.AssembleAsync(build, "EP01",
        [
            (sh01.ShotCode, a.Artifact.ArtifactId, 6),
            (sh02.ShotCode, b.Artifact.ArtifactId, 5),
        ]);
        var ordered = await store.ListAssemblyShotsAsync(film.AssemblyId);
        Ok(ordered.Count == 2 && ordered[0].ArtifactId == a.Artifact.ArtifactId && ordered[1].ShotCode == sh02.ShotCode, "T22 assembly order");

        var accepted = await eng.AcceptAsync(b.Artifact.ArtifactId);
        Ok(accepted.Role == ProductionBindingRoles.Accepted && accepted.ArtifactId == b.Artifact.ArtifactId, "T23 accept exact artifactId");

        var oldSha = a.Artifact.ByteSha256;
        var still = await store.GetArtifactAsync(a.Artifact.ArtifactId);
        Ok(still is not null && still.ByteSha256 == oldSha && inputTask.TaskId != a.Task.TaskId, "T24 regenerate leaves old artifact immutable");

        var urlRefuse = false;
        try { await eng.RefuseUrlAuthorityAsync("https://example/take.mp4"); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.UrlAuthorityForbidden) { urlRefuse = true; }
        Ok(urlRefuse, "URL is not binding authority");

        var actions = await eng.ListAvailableActionsAsync(build, sh02.ShotCode, true, true);
        Ok(actions.All(x => x.Action != "run")
            && actions.Any(x => x.Action == ProductionActions.CreateVideo)
            && actions.Single(x => x.Action == ProductionActions.CreateVideo).Available == false, "availability is not generic run() and kfApproved is not authority");
    }

    static async Task<(ProductionRequest Request, ProductionTask Task, ProductionAttempt Attempt, ProductionArtifact Artifact)> SliceCreateVideo(
        ProductionWorkExecutionEngine eng, ProductionScope scope, string story, byte[] bytes, string idem)
    {
        var req = await eng.CreateRequestAsync(scope, ProductionActions.CreateVideo, "test", idem);
        var task = await eng.CreateTaskAsync(req.RequestId);
        await eng.FreezeInputAsync(task.TaskId, Input(story, "kf-" + scope.ShotCode), "google", "veo-3.1");
        var att = await eng.StartAttemptAsync(task.TaskId, "payload-" + scope.ShotCode);
        await eng.SubmitProviderJobAsync(att.AttemptId, "veo_" + scope.ShotCode);
        await eng.RecordProviderSucceededAsync(att.AttemptId, "resp-" + scope.ShotCode);
        var art = await eng.VerifyArtifactAsync(att.AttemptId, bytes, "video/mp4", 5, "veo/" + scope.ShotCode + ".mp4");
        return (req, task, att, art);
    }

    static ShotExecutionInput Input(string story, string kfSeed) =>
        new(story, "He stays in place.", "visual", "CHAR-001", Hash(kfSeed), "5s", "", "motion");

    static string Hash(string value) => ShotExecutionCompiler.Hash(value);
}
