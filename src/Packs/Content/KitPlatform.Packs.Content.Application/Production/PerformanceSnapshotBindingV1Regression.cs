namespace KitPlatform.Packs.Content.Production;

public static class PerformanceSnapshotBindingV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_PERFORMANCE_SNAPSHOT_BINDING_V1";

    static readonly Guid BuildA = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-0000000000a1");
    static readonly Guid BuildB = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-0000000000b2");

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
        var perf = new InMemoryPerformanceSnapshotStore();
        var work = new InMemoryProductionWorkStore();
        var svc = new PerformanceContractService(perf);
        var eng = new ProductionWorkExecutionEngine(work, perf);

        var p001 = await svc.FreezeAsync(svc.CreateDraft(Source(BuildA, "TEST-SC01-SH01")), "regression");
        var taskA = await TaskOf(eng, BuildA, "TEST-SC01-SH01", ProductionActions.CreateVideo);
        var snapA = await eng.FreezeInputAsync(taskA.TaskId, VideoInput(p001), "runway", "gen4_turbo");
        Ok(snapA.PerformanceSnapshotId == p001.PerformanceSnapshotId
            && snapA.PerformanceHash == p001.PerformanceHash
            && snapA.PerformancePayloadJson == p001.PayloadJson, "T01 valid snapshot binds");

        Ok(PerformanceContractCanon.IsValidPerformanceHash(snapA.PerformanceHash)
            && snapA.PerformanceHash == PerformanceContractIds.HashPrefix
                + PerformanceContractCanon.Sha256Hex(System.Text.Encoding.UTF8.GetBytes(snapA.PerformancePayloadJson!)),
            "T02 valid payload hash");
        var mutated = false;
        try
        {
            var t = await TaskOf(eng, BuildA, "TEST-SC01-SH01", ProductionActions.CreateImage);
            await eng.FreezeInputAsync(t.TaskId, VideoInput(p001) with { PerformancePayloadJson = snapA.PerformancePayloadJson + " " }, "fixture", "fixture-image-v1");
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.FailedIdentity) { mutated = true; }
        Ok(mutated, "T02 mutated payload FAIL");

        var wrongHash = false;
        try
        {
            var t = await TaskOf(eng, BuildA, "TEST-SC01-SH01", ProductionActions.CreateImage);
            var hex = p001.PerformanceHash[PerformanceContractIds.HashPrefix.Length..];
            var flipped = (hex[0] == 'a' ? 'b' : 'a') + hex[1..];
            await eng.FreezeInputAsync(t.TaskId, VideoInput(p001) with { PerformanceHash = PerformanceContractIds.HashPrefix + flipped }, "fixture", "fixture-image-v1");
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.FailedIdentity) { wrongHash = true; }
        Ok(wrongHash, "T03 hash mismatch FAIL");

        var missing = false;
        try
        {
            var t = await TaskOf(eng, BuildA, "TEST-SC01-SH01", ProductionActions.CreateImage);
            await eng.FreezeInputAsync(t.TaskId, VideoInput(p001) with { PerformanceSnapshotId = Guid.NewGuid() }, "fixture", "fixture-image-v1");
        }
        catch (InvalidOperationException ex) when (ex.Message == PerformanceContractIds.SnapshotNotFound) { missing = true; }
        Ok(missing, "T04 unknown snapshotId FAIL");

        var crossShot = false;
        try
        {
            var t = await TaskOf(eng, BuildA, "TEST-SC01-SH02", ProductionActions.CreateVideo);
            await eng.FreezeInputAsync(t.TaskId, VideoInput(p001), "runway", "gen4_turbo");
        }
        catch (InvalidOperationException ex) when (ex.Message == PerformanceContractIds.CrossShotForbidden) { crossShot = true; }
        var sh02 = (await work.ListTasksByScopeAsync(BuildA, "TEST-SC01-SH02")).LastOrDefault();
        Ok(crossShot && sh02 is not null && sh02.InputSnapshotId is null && sh02.Status == ProductionTaskStatuses.Created, "T05 cross-shot FAIL closed");

        var crossBuild = false;
        try
        {
            var t = await TaskOf(eng, BuildB, "TEST-SC01-SH01", ProductionActions.CreateVideo);
            await eng.FreezeInputAsync(t.TaskId, VideoInput(p001), "runway", "gen4_turbo");
        }
        catch (InvalidOperationException ex) when (ex.Message == PerformanceContractIds.CrossScopeForbidden) { crossBuild = true; }
        Ok(crossBuild, "T06 cross-build FAIL");

        var p002 = await svc.FreezeAsync(svc.CreateDraft(Source(BuildA, "TEST-SC01-SH01", "walks away")), "regression");
        var afterP002 = await work.GetSnapshotByTaskAsync(taskA.TaskId);
        Ok(p002.PerformanceSnapshotId != p001.PerformanceSnapshotId
            && afterP002!.PerformanceSnapshotId == p001.PerformanceSnapshotId
            && afterP002.PerformanceHash == p001.PerformanceHash, "T07 Task A remains P001");

        _ = svc.CreateDraft(Source(BuildA, "TEST-SC01-SH01", "camera gaze"));
        var afterGraph = await work.GetSnapshotByTaskAsync(taskA.TaskId);
        Ok(afterGraph!.PerformancePayloadJson == p001.PayloadJson
            && afterGraph.PerformanceSnapshotId == p001.PerformanceSnapshotId, "T08 graph mutation does not change frozen input");

        var taskB = await TaskOf(eng, BuildA, "TEST-SC01-SH01", ProductionActions.CreateImage);
        var snapB = await eng.FreezeInputAsync(taskB.TaskId, VideoInput(p001), "fixture", "fixture-image-v1");
        Ok(snapB.PerformanceSnapshotId == snapA.PerformanceSnapshotId
            && snapB.PerformanceHash == snapA.PerformanceHash, "T09 same snapshot reused");

        var p001b = await svc.FreezeAsync(svc.CreateDraft(Source(BuildA, "TEST-SC01-SH01")), "regression");
        Ok(p001b.PerformanceHash == p001.PerformanceHash && p001b.PerformanceSnapshotId == p001.PerformanceSnapshotId, "T10 identical content same SnapshotId");
        var taskC = await TaskOf(eng, BuildA, "TEST-SC01-SH01", ProductionActions.CreateImage);
        var snapC = await eng.FreezeInputAsync(taskC.TaskId, VideoInput(p001b), "fixture", "fixture-image-v1");
        Ok(snapC.PerformanceSnapshotId == p001b.PerformanceSnapshotId
            && snapC.PerformanceSnapshotId == p001.PerformanceSnapshotId, "T10 task froze exact SnapshotId");

        Ok(p002.PerformanceHash != p001.PerformanceHash
            && afterP002!.PerformanceHash != p002.PerformanceHash, "T11 P001 cannot silently become P002");

        var legacy = 0;
        foreach (var fake in new[]
        {
            ProductionTestCreateVideoContext.ActingHash,
            "a1b2c3d4",
            ShotExecutionCompiler.Hash("voice"),
            snapA.SnapshotHash,
        })
        {
            try
            {
                var t = await TaskOf(eng, BuildA, "TEST-SC01-SH01", ProductionActions.CreateImage);
                await eng.FreezeInputAsync(t.TaskId, VideoInput(p001) with { PerformanceHash = fake }, "fixture", "fixture-image-v1");
            }
            catch (InvalidOperationException ex) when (ex.Message is PerformanceContractIds.InvalidPerformanceHash
                or ProductionFailureReasons.FailedIdentity)
            {
                legacy++;
            }
        }
        Ok(legacy == 4, "T12 legacy hashes rejected");

        Ok(!(snapA.PerformancePayloadJson ?? "").Contains("runway", StringComparison.OrdinalIgnoreCase)
            && !(snapA.PerformancePayloadJson ?? "").Contains("gen4", StringComparison.OrdinalIgnoreCase)
            && !(snapA.PerformancePayloadJson ?? "").Contains("providerId", StringComparison.OrdinalIgnoreCase)
            && PerformanceContractCanon.ScanForbidden(snapA.PerformancePayloadJson).Count == 0, "T13 provider independence");

        var taskHow = await TaskOf(eng, BuildA, "TEST-SC01-SH01", ProductionActions.CreateImage);
        var veo = await eng.FreezeInputAsync(taskHow.TaskId, VideoInput(p001), "google", "veo-3.1");
        Ok(veo.ProviderId == "google" && veo.ModelId == "veo-3.1"
            && veo.PerformanceSnapshotId == p001.PerformanceSnapshotId
            && veo.PerformanceHash == p001.PerformanceHash, "T14 provider change keeps PerformanceSnapshot");

        var taskTiming = await TaskOf(eng, BuildA, "TEST-SC01-SH01", ProductionActions.CreateImage);
        var timed = await eng.FreezeInputAsync(taskTiming.TaskId, VideoInput(p001) with { Timing = "10s", DurationSec = 10 }, "runway", "gen4_turbo");
        Ok(timed.TimingHash != snapA.TimingHash
            && timed.PerformanceHash == p001.PerformanceHash, "T15 timing change keeps PerformanceSnapshot");

        var taskVisual = await TaskOf(eng, BuildA, "TEST-SC01-SH01", ProductionActions.CreateImage);
        var visual = await eng.FreezeInputAsync(taskVisual.TaskId, VideoInput(p001) with { VisualContract = "TEST VISUAL — other frame" }, "runway", "gen4_turbo");
        Ok(visual.VisualHash != snapA.VisualHash
            && visual.PerformanceHash == p001.PerformanceHash, "T16 visual change keeps PerformanceSnapshot");

        var reload = await work.GetSnapshotByTaskAsync(taskA.TaskId);
        Ok(reload is not null
            && reload.PerformanceSnapshotId == p001.PerformanceSnapshotId
            && reload.PerformanceHash == p001.PerformanceHash
            && reload.PerformancePayloadJson == p001.PayloadJson, "T17 reload identical");

        var crossScope = false;
        try
        {
            var t = await TaskOf(eng, BuildB, "TEST-SC01-SH01", ProductionActions.CreateImage);
            await eng.FreezeInputAsync(t.TaskId, VideoInput(p001), "fixture", "fixture-image-v1");
        }
        catch (InvalidOperationException ex) when (ex.Message == PerformanceContractIds.CrossScopeForbidden) { crossScope = true; }
        Ok(crossScope, "T18 cross-test-scope FAIL");

        var official = 0;
        try
        {
            var t = await TaskOf(eng, ProductionScopeAnchorRules.OfficialEp01, "EP01-SC01-SH01", ProductionActions.CreateVideo);
            await eng.FreezeInputAsync(t.TaskId, VideoInput(p001), "runway", "gen4_turbo");
        }
        catch (InvalidOperationException ex) when (ex.Message == PerformanceContractIds.CrossScopeForbidden) { official++; }
        try
        {
            await svc.FreezeAsync(svc.CreateDraft(Source(ProductionScopeAnchorRules.OfficialEp99, "EP99-SC01-SH01")), "regression");
        }
        catch (InvalidOperationException ex) when (ex.Message == PerformanceContractIds.OfficialFilmForbidden) { official++; }
        Ok(official == 2, "T19 official film forbidden");

        Ok(ProductionActionContractRegistry.CreateVideo.RequiredInputs.Contains("ActingBeat")
            && ProductionActionContractRegistry.CreateVideo.RequiredInputs.Contains("PerformanceSnapshot")
            && ProductionActionContractRegistry.CreateVoice.RequiredInputs.Contains("PerformanceSnapshot")
            && ProductionActionContractRegistry.CreateLipsync.RequiredInputs.Contains("MotionArtifactId")
            && ProductionActionContractRegistry.CreateLipsync.RequiredInputs.Contains("PerformanceSnapshot"), "Phase C action-contract requires PerformanceSnapshot");

        var video = ProductionTestCreateVideoV1Regression.Run();
        Ok(video.Count == 0, "T20 CREATE_VIDEO TEST FAIL=" + video.Count + (video.Count == 0 ? "" : ":" + string.Join(",", video.Take(3))));
        var authority = ProductionCompleteActionAuthorityV1Regression.Run();
        Ok(authority.Count == 0, "T21 Complete Action Authority FAIL=" + authority.Count + (authority.Count == 0 ? "" : ":" + string.Join(",", authority.Take(3))));
        Ok(ProductionActionContractRegistry.CreateImage.UiCta == "Tạo hình"
            && ProductionActionContractRegistry.CreateVideo.UiCta == "Tạo video"
            && ProductionActionContractRegistry.CreateLipsync.UiCta == "Khớp môi", "T22 UI CTA map unchanged");
    }

    static async Task<ProductionTask> TaskOf(ProductionWorkExecutionEngine eng, Guid buildId, string shot, string action)
    {
        var req = await eng.CreateRequestAsync(new ProductionScope(buildId, "TEST", "SC01", shot), action, "regression");
        return await eng.CreateTaskAsync(req.RequestId);
    }

    static ShotExecutionInput VideoInput(PerformanceSnapshot snap) =>
        ProductionTestCreateVideoContext.Input() with
        {
            PerformanceSnapshotId = snap.PerformanceSnapshotId,
            PerformanceHash = snap.PerformanceHash,
        };

    static PerformanceProjectionSource Source(Guid buildId, string shot, string? action = null) => new(
        buildId,
        shot,
        "TEST",
        "SC01",
        ["CHAR-001"],
        ["L1"],
        [
            new LineProjectionSource("L1", "CHAR-001", "Thì sao?", "tense", 3, "fast", "mid", 0.12, PerformanceEmotions.LabelOf("tense", 3), Locked: true),
        ],
        new ActingBeatSource(action ?? "turns", "stands", null, "window", "kitchen", "uneasy", 2, null, "down"));
}
