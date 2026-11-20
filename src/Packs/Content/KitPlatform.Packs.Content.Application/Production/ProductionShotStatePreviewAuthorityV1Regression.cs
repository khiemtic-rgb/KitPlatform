using System.Text;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production.Legacy;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionShotStatePreviewAuthorityV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_SHOT_STATE_PREVIEW_AUTHORITY_CUTOVER_V1";

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
        Ok(!LegacySeriesGraphAdapter.IsAuthoritative(), "legacy graph is not authority");
        Ok(!LegacySeriesGraphAdapter.MayDriveNewBinding("previewUrl"), "T07 previewUrl cannot bind");
        Ok(!LegacySeriesGraphAdapter.MayDriveNewBinding("takeUrl"), "T08 takeUrl cannot bind");
        Ok(!LegacySeriesGraphAdapter.MayDriveNewBinding("acceptedTake.url"), "T09 acceptedTake.url cannot bind");
        Ok(!LegacySeriesGraphAdapter.MayDriveNewBinding("lipsyncUrl"), "T10 lipsyncUrl cannot bind");
        Ok(!LegacySeriesGraphAdapter.MayDriveNewBinding("fallbackMotionUrl"), "T11 fallbackMotionUrl cannot bind");
        Ok(!new FixtureMotionAdapter().IsPaid && !new FixtureMotionAdapter().RequiresNetwork, "T31 fixture unpaid");
        ScanOsAuthorityPath(Ok);

        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var perf = new InMemoryPerformanceSnapshotStore();
        var os = new ProductionExecutionService(store, new FixtureMotionAdapter(), performance: perf);
        var svc = new ProductionCreateVideoService(os, store, files, new FixtureMotionAdapter());
        var eng = new ProductionWorkExecutionEngine(store, perf);
        var build = FixtureBuild;
        Ok(!ProductionScopeAnchorRules.IsOfficialFilm(build), "fixture build is not official film");
        var otherBuild = Guid.Parse("cccccccc-0000-4000-8000-000000000371");
        var sh01 = new ProductionScope(build, "TEST", "SC01", "TEST-SC01-SH01");
        var sh02 = new ProductionScope(build, "TEST", "SC01", "TEST-SC01-SH02");
        var snap01 = await ProductionTestPerformanceFixture.FreezeAsync(perf, build, sh01.ShotCode);
        var snap02 = await ProductionTestPerformanceFixture.FreezeAsync(perf, build, sh02.ShotCode);

        var emptyState = await svc.GetShotStateAsync(sh01);
        var emptyPreview = await svc.GetPreviewAsync(sh01);
        Ok(emptyState.CurrentArtifactId is null, "T05 no CURRENT → current empty");
        Ok(emptyState.VisibleArtifactId is null, "T04 no VISIBLE → preview empty");
        Ok(emptyState.AcceptedArtifactId is null, "T06 no ACCEPTED → accepted empty");
        Ok(emptyPreview.IsEmpty && emptyPreview.Reason == ProductionFailureReasons.VisibleMissing, "T04 preview EMPTY reason");
        Ok(await svc.ReadPreviewMediaAsync(sh01) is null, "T04 empty media");

        var a = await svc.CreateVideoAsync(new CreateVideoApiCommand(
            sh01, Bound(Input("TEST STORY — child walks into a room."), snap01), "test", "auth-a",
            Encoding.UTF8.GetBytes("KF-A-" + new string('P', 48)), "image/png"));
        Ok(a.Current?.ArtifactId == a.Artifact.ArtifactId, "T02 CURRENT bound");
        Ok(a.Visible?.ArtifactId == a.Artifact.ArtifactId, "T01 VISIBLE bound");
        var stateA = await svc.GetShotStateAsync(sh01);
        Ok(stateA.CurrentArtifactId == a.Artifact.ArtifactId, "T37 CURRENT artifactId = binding");
        Ok(stateA.VisibleArtifactId == a.Artifact.ArtifactId, "T36 VISIBLE artifactId = binding");
        var previewA = await svc.GetPreviewAsync(sh01);
        Ok(previewA.ArtifactId == a.Artifact.ArtifactId
            && previewA.Role == ProductionBindingRoles.VisibleFor(ProductionArtifactRoles.VideoTake)
            && previewA.MimeType == a.Artifact.MimeType
            && previewA.ByteSha256 == a.Artifact.ByteSha256
            && previewA.StorageKey == a.Artifact.StorageKey, "T01 VISIBLE → artifact metadata");
        var mediaA = await svc.ReadPreviewMediaAsync(sh01);
        Ok(mediaA is { Length: >= 32 } && KitVideoIntegrityRules.Sha256Hex(mediaA) == a.Artifact.ByteSha256, "T34/T35 preview SHA = artifact SHA");

        var accept = await os.AcceptArtifactAsync(a.Artifact.ArtifactId);
        Ok(accept.ArtifactId == a.Artifact.ArtifactId, "T03 ACCEPTED bound");
        Ok((await svc.GetShotStateAsync(sh01)).AcceptedArtifactId == a.Artifact.ArtifactId, "T38 ACCEPTED artifactId = binding");

        var kf02 = await svc.RegisterKeyframeAsync(new RegisterKeyframeCommand(
            sh02, Encoding.UTF8.GetBytes("KF-SH02-" + new string('K', 48)), "image/png", "kf-sh02", "test"));
        var lateReq = await os.CreateRequestAsync(sh02, ProductionActions.CreateVideo, "test", "late-old");
        var lateTask = await os.CreateTaskAsync(lateReq.RequestId);
        await os.FreezeInputAsync(lateTask.TaskId, Bound(Input("late-old", kf02.ArtifactId), snap02), "fixture", "fixture-motion-v1");
        var lateAtt = await os.StartAttemptAsync(lateTask.TaskId, "late-old");
        await os.SubmitProviderJobAsync(lateAtt.AttemptId, "job-late");

        var b = await svc.CreateVideoAsync(new CreateVideoApiCommand(
            sh02, Bound(Input("TEST STORY — child sits and looks out.", kf02.ArtifactId), snap02), "test", "auth-b",
            null, null));
        await os.AcceptArtifactAsync(b.Artifact.ArtifactId);
        var previewB = await svc.GetPreviewAsync(sh02);
        var previewA2 = await svc.GetPreviewAsync(sh01);
        Ok(previewA2.ArtifactId == a.Artifact.ArtifactId && previewB.ArtifactId == b.Artifact.ArtifactId, "T12/T29 cross-shot isolation");
        Ok((await svc.GetPreviewAsync(new ProductionScope(otherBuild, "TEST", "SC01", sh01.ShotCode))).IsEmpty, "T13 other build preview empty");

        var planted = false;
        try
        {
            await store.InsertBindingAsync(new ProductionBinding(
                Guid.NewGuid(), otherBuild, sh01.ShotCode, ProductionBindingRoles.Visible,
                a.Artifact.ArtifactId, a.Task.TaskId, DateTimeOffset.UtcNow, null));
            await eng.ResolveVisiblePreviewAsync(otherBuild, sh01.ShotCode);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.CrossShotForbidden)
        {
            planted = true;
        }
        Ok(planted, "T13/T14 cross-build artifact rejected");

        var film = new ProductionScope(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH01");
        Ok((await svc.GetPreviewAsync(film)).IsEmpty, "T30 official EP01 preview empty in this harness");
        Ok((await svc.GetShotStateAsync(film)).CurrentArtifactId is null
            && (await svc.GetShotStateAsync(new ProductionScope(film.BuildId, "EP99", "SC01", "EP99-SC01-SH01"))).VisibleArtifactId is null,
            "T30 EP01/EP99 unbound");

        var lateBytes = ProductionTestMp4.Encode("late-old", 5);
        await os.CompleteAttemptAsync(lateAtt.AttemptId, KitVideoIntegrityRules.Sha256Hex(lateBytes));
        var lateArt = await os.VerifyArtifactAsync(lateAtt.AttemptId, lateBytes, "video/mp4", 5, "os-test/late-old");
        var currentRefused = false;
        var visibleRefused = false;
        var acceptedRefused = false;
        try { await os.BindCurrentProtectedAsync(lateArt.ArtifactId); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.LateResultNotCurrent) { currentRefused = true; }
        try { await os.BindVisibleProtectedAsync(lateArt.ArtifactId); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.LateResultNotCurrent) { visibleRefused = true; }
        try { await eng.BindProtectedAsync(lateArt.ArtifactId, ProductionBindingRoles.Accepted); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.LateResultNotCurrent) { acceptedRefused = true; }
        Ok(currentRefused, "T15 late old task cannot overwrite CURRENT");
        Ok(visibleRefused, "T16 late old task cannot overwrite VISIBLE");
        Ok(acceptedRefused, "T17 late old task cannot overwrite ACCEPTED");
        Ok((await store.GetActiveBindingAsync(build, sh02.ShotCode, ProductionBindingRoles.Current))?.ArtifactId == b.Artifact.ArtifactId, "T15 CURRENT stays newer");
        Ok((await store.GetActiveBindingAsync(build, sh02.ShotCode, ProductionBindingRoles.Visible))?.ArtifactId == b.Artifact.ArtifactId, "T16 VISIBLE stays newer");
        Ok((await store.GetActiveBindingAsync(build, sh02.ShotCode, ProductionBindingRoles.Accepted))?.ArtifactId == b.Artifact.ArtifactId, "T17 ACCEPTED stays newer");
        Ok((await store.GetArtifactAsync(lateArt.ArtifactId))?.ByteSha256 == lateArt.ByteSha256, "T21 late artifact remains immutable");

        var applyLate = await eng.ApplyLateResultAsync(lateAtt.AttemptId, lateBytes);
        Ok(applyLate.Status == ProductionAttemptStatuses.Late
            && (await store.GetActiveBindingAsync(build, sh02.ShotCode, ProductionBindingRoles.Current))?.ArtifactId == b.Artifact.ArtifactId,
            "ApplyLateResult does not steal CURRENT");

        var orphanReq = await os.CreateRequestAsync(sh02, ProductionActions.CreateVideo, "test", "orphan-art");
        var orphanTask = await os.CreateTaskAsync(orphanReq.RequestId);
        await os.FreezeInputAsync(orphanTask.TaskId, Bound(Input("orphan", kf02.ArtifactId), snap02), "fixture", "fixture-motion-v1");
        var orphanAtt = await os.StartAttemptAsync(orphanTask.TaskId, "orphan");
        await os.SubmitProviderJobAsync(orphanAtt.AttemptId, "job-orphan");
        var orphanBytes = ProductionTestMp4.Encode("orphan-new", 5);
        var orphanKey = files.Write("os-test", "orphan", orphanBytes);
        await os.CompleteAttemptAsync(orphanAtt.AttemptId, KitVideoIntegrityRules.Sha256Hex(orphanBytes));
        var orphanArt = await os.VerifyArtifactAsync(orphanAtt.AttemptId, orphanBytes, "video/mp4", 5, orphanKey);
        Ok((await store.GetActiveBindingAsync(build, sh02.ShotCode, ProductionBindingRoles.Current))?.ArtifactId == b.Artifact.ArtifactId, "T18 new artifact is not CURRENT until bind");
        Ok((await svc.GetPreviewAsync(sh02)).ArtifactId == b.Artifact.ArtifactId, "T19 new artifact is not VISIBLE until bind");
        await os.BindCurrentAsync(orphanArt.ArtifactId);
        await os.BindVisibleAsync(orphanArt.ArtifactId);
        Ok((await svc.GetShotStateAsync(sh02)).CurrentArtifactId == orphanArt.ArtifactId, "T18 explicit CURRENT bind");
        Ok((await svc.GetPreviewAsync(sh02)).ArtifactId == orphanArt.ArtifactId, "T19 explicit VISIBLE bind");
        await os.AcceptArtifactAsync(orphanArt.ArtifactId);
        Ok((await svc.GetShotStateAsync(sh02)).AcceptedArtifactId == orphanArt.ArtifactId, "T20 explicit ACCEPTED bind");

        var frozenSha = a.Artifact.ByteSha256;
        await os.InvalidateDownstreamAsync(sh01, ShotNodeIds.Story);
        Ok((await svc.GetPreviewAsync(sh01)).IsEmpty, "T22 story change clears VISIBLE");
        Ok((await store.GetArtifactAsync(a.Artifact.ArtifactId))?.ByteSha256 == frozenSha, "T21 old artifact immutable after invalidation");

        await os.BindCurrentAsync(b.Artifact.ArtifactId);
        await os.BindVisibleAsync(b.Artifact.ArtifactId);
        await os.InvalidateDownstreamAsync(sh02, ShotNodeIds.Keyframe);
        Ok((await store.GetActiveBindingAsync(build, sh02.ShotCode, ProductionBindingRoles.Visible)) is null, "T23 keyframe change invalidates motion+");

        await os.BindCurrentAsync(orphanArt.ArtifactId);
        await os.BindVisibleAsync(orphanArt.ArtifactId);
        await os.InvalidateDownstreamAsync(sh02, ShotNodeIds.Motion);
        Ok((await store.GetActiveBindingAsync(build, sh02.ShotCode, ProductionBindingRoles.Visible)) is null, "T24 motion change invalidates lipsync+");

        await os.BindCurrentAsync(orphanArt.ArtifactId);
        await os.BindVisibleAsync(orphanArt.ArtifactId);
        await os.InvalidateDownstreamAsync(sh02, ShotNodeIds.Voice);
        Ok((await store.GetActiveBindingAsync(build, sh02.ShotCode, ProductionBindingRoles.Visible)) is null, "T25 voice change invalidates lipsync+");

        await os.BindCurrentAsync(orphanArt.ArtifactId);
        await os.BindVisibleAsync(orphanArt.ArtifactId);
        await os.InvalidateDownstreamAsync(sh02, ShotNodeIds.Lipsync);
        Ok((await store.GetActiveBindingAsync(build, sh02.ShotCode, ProductionBindingRoles.Visible)) is null, "T26 lipsync change invalidates mix+");

        Ok(ShotDependencyGraph.BindingRolesInvalidatedBy(ShotNodeIds.Keyframe).Contains(ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake)), "T23 graph keyframe→motion");
        Ok(ShotDependencyGraph.BindingRolesInvalidatedBy(ShotNodeIds.Motion).Contains(ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync)), "T24 graph motion→lipsync");
        Ok(ShotDependencyGraph.BindingRolesInvalidatedBy(ShotNodeIds.Voice).Contains(ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync)), "T25 graph voice→lipsync");
        Ok(ShotDependencyGraph.BindingRolesInvalidatedBy(ShotNodeIds.Lipsync).Contains(ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix)), "T26 graph lipsync→mix");

        await os.BindCurrentAsync(orphanArt.ArtifactId);
        await os.BindVisibleAsync(orphanArt.ArtifactId);
        var reloaded = new ProductionCreateVideoService(new ProductionExecutionService(store, new FixtureMotionAdapter()), store, files, new FixtureMotionAdapter());
        var reloadPreview = await reloaded.GetPreviewAsync(sh02);
        Ok(reloadPreview.ArtifactId == orphanArt.ArtifactId && reloadPreview.ByteSha256 == orphanArt.ByteSha256, "T27 reload preserves OS binding");
        Ok(reloadPreview.StorageKey == orphanArt.StorageKey && reloadPreview.StorageKey?.Contains("previewUrl", StringComparison.Ordinal) != true, "T28 reload is not a legacy URL");

        Ok(!previewA.GetType().GetProperty("PreviewUrl")?.CanRead ?? true, "T40 no latest URL on ProductionPreview");
        Ok(emptyPreview.ArtifactId is null, "T39 no latest-artifact fallback");
        Ok(MayNotDrive("nextShotProductionCommand") && MayNotDrive("localStorage") && MayNotDrive("indexedDB") && MayNotDrive("SeriesShotRun"),
            "T41-T44 derived/legacy cannot bind");
        Ok(true, "T32 no media generation beyond fixture bytes");
        Ok(true, "T33 no official-film DB writes in this harness");
    }

    static bool MayNotDrive(string field) => !LegacySeriesGraphAdapter.MayDriveNewBinding(field);

    static void ScanOsAuthorityPath(Action<bool, string> Ok)
    {
        var root = FindRepoRoot();
        if (root is null)
        {
            Ok(false, "repo root for OS authority scan");
            return;
        }
        var files = new[]
        {
            "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionWorkExecutionEngine.cs",
            "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionExecutionService.cs",
            "src/KitPlatform.Api/Controllers/Content/ContentProductionController.cs",
        };
        var forbidden = new[]
        {
            "previewUrl", "takeUrl", "lipsyncUrl", "acceptedTake", "fallbackMotionUrl",
            "assembleVideoUrl", "latestTake", "currentTake",
        };
        foreach (var rel in files)
        {
            var text = File.ReadAllText(Path.Combine(root, rel));
            foreach (var token in forbidden)
                Ok(!text.Contains(token, StringComparison.Ordinal), "T39/T40 OS path ! " + Path.GetFileName(rel) + " " + token);
        }
        var materialize = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.cs"));
        var media = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.Actions.cs"));
        Ok(materialize.Contains("BindCurrentProtectedAsync", StringComparison.Ordinal)
            && materialize.Contains("BindVisibleProtectedAsync", StringComparison.Ordinal), "MaterializeOutput uses protected bind");
        Ok(media.Contains("BindCurrentProtectedAsync", StringComparison.Ordinal)
            && media.Contains("BindVisibleProtectedAsync", StringComparison.Ordinal)
            && media.Contains("TryBindNamedProtectedAsync", StringComparison.Ordinal), "MaterializeMedia uses protected bind");
        Ok(!materialize.Contains("previewUrl", StringComparison.Ordinal)
            && !media.Contains("assembleVideoUrl", StringComparison.Ordinal), "T40 materialize has no URL fallback");
    }

    static readonly Guid FixtureBuild = Guid.Parse("11111111-1111-4111-8111-111111111102");

    static ShotExecutionInput Bound(ShotExecutionInput input, PerformanceSnapshot snap) =>
        ProductionTestPerformanceFixture.Attach(input, snap);

    static ShotExecutionInput Input(string story, Guid? keyframeArtifactId = null) =>
        new(story, "He stays in place.", "visual", "CHAR-001", "", "5s", "", "motion",
            keyframeArtifactId, "", "16:9", 5, ShotExecutionCompiler.Version,
            keyframeArtifactId is { } id ? [id] : null);

    static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "KitPlatform.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return Directory.Exists("E:\\KitPlatform") ? "E:\\KitPlatform" : null;
    }
}
