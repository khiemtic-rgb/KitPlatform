namespace KitPlatform.Packs.Content.Production;

public static class PerformanceConsumptionCutoverV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_PERFORMANCE_CONSUMPTION_CUTOVER_V1";

    static readonly Guid BuildA = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-0000000000c1");
    static readonly Guid BuildB = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-0000000000c2");

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
        var files = new InMemoryProductionArtifactFileStore();
        var motion = new FixtureMotionAdapter();
        var os = new ProductionExecutionService(work, motion, performance: perf);
        var svc = new ProductionCreateVideoService(os, work, files, motion);
        var sh01 = new ProductionScope(BuildA, "TEST", "SC01", "TEST-SC01-SH01");
        var sh02 = new ProductionScope(BuildA, "TEST", "SC01", "TEST-SC01-SH02");
        var other = new ProductionScope(BuildB, "TEST", "SC01", "TEST-SC01-SH01");

        var p001 = await ProductionTestPerformanceFixture.FreezeAsync(perf, BuildA, sh01.ShotCode);
        var p002 = await ProductionTestPerformanceFixture.FreezeAsync(perf, BuildA, sh01.ShotCode, "walks away");
        var p001Sh02 = await ProductionTestPerformanceFixture.FreezeAsync(perf, BuildA, sh02.ShotCode);
        var p001B = await ProductionTestPerformanceFixture.FreezeAsync(perf, BuildB, other.ShotCode);

        var kf = await svc.CreateImageAsync(new ProductionActionApiCommand(sh01, ProductionTestCreateVideoContext.Input(), "regression", "c-kf"));
        var readyInput = ProductionTestPerformanceFixture.Attach(ProductionTestCreateVideoContext.Input(kf.Artifact.ArtifactId), p001);
        var readyClaim = ProductionTestPerformanceFixture.Attach(ProductionTestCreateVideoContext.Claim(kf.Artifact.ArtifactId), p001);

        var t01Get = await os.EvaluateCreateVideoAsync(sh01, ProductionTestCreateVideoContext.Claim(kf.Artifact.ArtifactId));
        Ok(!t01Get.Available && t01Get.MissingInputs.Contains("PerformanceSnapshot")
            && t01Get.BlockReason == ProductionFailureReasons.MissingPerformanceSnapshot, "T01 GET without snapshot FAIL");
        Ok(await Throws(svc.CreateVideoAsync(new CreateVideoApiCommand(sh01, ProductionTestCreateVideoContext.Input(kf.Artifact.ArtifactId), "regression", "c-t01", null, null)),
            ProductionFailureReasons.MissingPerformanceSnapshot), "T01 POST without snapshot FAIL");

        var t02Get = await os.EvaluateCreateVideoAsync(sh01, readyClaim);
        Ok(t02Get.Available && t02Get.PerformanceSnapshotId == p001.PerformanceSnapshotId, "T02 GET valid P001 READY");
        var video1 = await svc.CreateVideoAsync(new CreateVideoApiCommand(sh01, readyInput, "regression", "c-t02", null, null));
        var videoSnap = await work.GetSnapshotByTaskAsync(video1.Task.TaskId);
        Ok(video1.Artifact.Role == ProductionArtifactRoles.VideoTake
            && videoSnap?.PerformanceSnapshotId == p001.PerformanceSnapshotId
            && videoSnap.PerformanceHash == p001.PerformanceHash, "T02 POST P001 executable");

        var badHash = readyInput with { PerformanceHash = PerformanceContractIds.HashPrefix + new string('a', 64) };
        Ok(await Throws(svc.CreateVideoAsync(new CreateVideoApiCommand(sh01, badHash, "regression", "c-t03", null, null)),
            ProductionFailureReasons.FailedIdentity), "T03 wrong snapshot hash FAIL");

        Ok(await Throws(svc.CreateVideoAsync(new CreateVideoApiCommand(sh02, readyInput, "regression", "c-t04", null, null)),
            PerformanceContractIds.CrossShotForbidden), "T04 wrong shot FAIL");

        Ok(await Throws(svc.CreateVideoAsync(new CreateVideoApiCommand(other, readyInput, "regression", "c-t05", null, null)),
            PerformanceContractIds.CrossScopeForbidden), "T05 wrong build FAIL");

        var actingOnly = ProductionTestCreateVideoContext.Input(kf.Artifact.ArtifactId) with
        {
            PerformanceHash = ProductionTestCreateVideoContext.ActingHash,
        };
        var t06 = await os.EvaluateCreateVideoAsync(sh01, CreateVideoActionContractGate.ClaimFrom(actingOnly));
        Ok(!t06.Available && (t06.BlockReason == ProductionFailureReasons.MissingPerformanceSnapshot
            || t06.BlockReason == PerformanceContractIds.InvalidPerformanceHash), "T06 ActingHash only FAIL");

        Ok(videoSnap!.PerformanceSnapshotId == p001.PerformanceSnapshotId
            && videoSnap.PerformanceHash == p001.PerformanceHash
            && videoSnap.PerformancePayloadJson == p001.PayloadJson, "T07 freeze preserves P001");

        _ = await ProductionTestPerformanceFixture.FreezeAsync(perf, BuildA, sh01.ShotCode, "camera gaze");
        var afterGraph = await work.GetSnapshotByTaskAsync(video1.Task.TaskId);
        Ok(afterGraph!.PerformanceSnapshotId == p001.PerformanceSnapshotId
            && afterGraph.PerformanceHash == p001.PerformanceHash, "T08 graph mutation keeps P001");

        Ok(await Throws(svc.CreateVoiceAsync(new ProductionActionApiCommand(
            sh01, ProductionTestCreateVideoContext.Input() with
            {
                LineId = ProductionTestPerformanceFixture.LineId,
                SpeakerCharacterId = ProductionTestPerformanceFixture.CharacterId,
                Story = ProductionTestPerformanceFixture.LineText,
                VoiceId = ProductionTestPerformanceFixture.VoiceId,
            }, "regression", "c-t09")),
            ProductionFailureReasons.MissingPerformanceSnapshot), "T09 VOICE without snapshot FAIL");

        var voice1 = await svc.CreateVoiceAsync(new ProductionActionApiCommand(
            sh01, ProductionTestPerformanceFixture.VoiceInput(ProductionTestCreateVideoContext.Input(), p001), "regression", "c-t10"));
        var voiceSnap = await work.GetSnapshotByTaskAsync(voice1.Task.TaskId);
        Ok(voice1.Artifact.Role == ProductionArtifactRoles.Voice
            && voiceSnap?.PerformanceHash == p001.PerformanceHash, "T10 VOICE P001 + L001 PASS");

        Ok(await Throws(svc.CreateVoiceAsync(new ProductionActionApiCommand(
            sh01, ProductionTestPerformanceFixture.VoiceInput(ProductionTestCreateVideoContext.Input(), p001) with { LineId = "L999" }, "regression", "c-t11")),
            ProductionFailureReasons.PerformanceLineMismatch), "T11 wrong lineId FAIL");

        Ok(await Throws(svc.CreateVoiceAsync(new ProductionActionApiCommand(
            sh01, ProductionTestPerformanceFixture.VoiceInput(ProductionTestCreateVideoContext.Input(), p001) with { SpeakerCharacterId = "CHAR-999" }, "regression", "c-t12")),
            ProductionFailureReasons.PerformanceLineMismatch), "T12 wrong characterId FAIL");

        Ok(await Throws(svc.CreateVoiceAsync(new ProductionActionApiCommand(
            sh01, ProductionTestPerformanceFixture.VoiceInput(ProductionTestCreateVideoContext.Input(), p001) with { Story = "Khác rồi." }, "regression", "c-t13")),
            ProductionFailureReasons.PerformanceLineMismatch), "T13 wrong LineText FAIL");

        var unlockedSrc = ProductionTestPerformanceFixture.Source(BuildA, sh01.ShotCode);
        unlockedSrc = unlockedSrc with
        {
            Lines = [unlockedSrc.Lines[0] with { Locked = false }],
        };
        var unlockedDraft = new PerformanceContractService(perf).CreateDraft(unlockedSrc);
        var unlockedOk = false;
        try { await new PerformanceContractService(perf).FreezeAsync(unlockedDraft, "regression"); }
        catch (InvalidOperationException ex) when (ex.Message == PerformanceContractIds.SpokenLineUnlocked) { unlockedOk = true; }
        Ok(unlockedOk, "T14 unlocked line cannot freeze / FAIL");

        var voiceAlt = await svc.CreateVoiceAsync(new ProductionActionApiCommand(
            sh01, ProductionTestPerformanceFixture.VoiceInput(ProductionTestCreateVideoContext.Input(), p001, "VOICE-OTHER"), "regression", "c-t15"));
        var voiceAltSnap = await work.GetSnapshotByTaskAsync(voiceAlt.Task.TaskId);
        Ok(voiceAltSnap!.PerformanceHash == p001.PerformanceHash
            && voiceAltSnap.VoiceHash != voiceSnap!.VoiceHash, "T15 VoiceId change keeps PerformanceHash, changes VoiceHash");

        await os.InvalidateDownstreamAsync(sh01, ShotNodeIds.Acting);
        var videoTask = await work.GetTaskAsync(video1.Task.TaskId);
        var voiceTask = await work.GetTaskAsync(voice1.Task.TaskId);
        var oldVideo = await work.GetArtifactAsync(video1.Artifact.ArtifactId);
        var oldVoice = await work.GetArtifactAsync(voice1.Artifact.ArtifactId);
        Ok(videoTask?.Status == ProductionTaskStatuses.Superseded
            && voiceTask?.Status == ProductionTaskStatuses.Superseded, "T16 P001 VIDEO+VOICE stale after acting change");
        Ok(oldVideo?.ByteSha256 == video1.Artifact.ByteSha256
            && oldVoice?.ByteSha256 == voice1.Artifact.ByteSha256, "T16 old artifacts immutable");

        var videoP001 = await svc.CreateVideoAsync(new CreateVideoApiCommand(sh01, readyInput, "regression", "c-t17-v", null, null));
        var voiceP001 = await svc.CreateVoiceAsync(new ProductionActionApiCommand(
            sh01, ProductionTestPerformanceFixture.VoiceInput(ProductionTestCreateVideoContext.Input(), p001), "regression", "c-t17-a"));
        var lipsyncReady = await os.EvaluateCreateLipsyncAsync(sh01, new CreateVideoReadinessClaim(
            MotionArtifactId: videoP001.Artifact.ArtifactId, VoiceArtifactId: voiceP001.Artifact.ArtifactId,
            PerformanceSnapshotId: p001.PerformanceSnapshotId, PerformanceHash: p001.PerformanceHash));
        Ok(lipsyncReady.Available && lipsyncReady.PerformanceHash == p001.PerformanceHash, "T17 LipSync same PerformanceHash PASS");
        var lipsync1 = await svc.CreateLipsyncAsync(new ProductionActionApiCommand(
            sh01, ProductionTestPerformanceFixture.Attach(ProductionTestCreateVideoContext.Input() with
            {
                MotionArtifactId = videoP001.Artifact.ArtifactId,
                VoiceArtifactId = voiceP001.Artifact.ArtifactId,
            }, p001), "regression", "c-t17"));
        Ok(lipsync1.Artifact.Role == ProductionArtifactRoles.Lipsync, "T17 LipSync executes");

        var kf2 = await svc.CreateImageAsync(new ProductionActionApiCommand(sh02, ProductionTestCreateVideoContext.Input(), "regression", "c-kf2"));
        var videoP002 = await svc.CreateVideoAsync(new CreateVideoApiCommand(
            sh01, ProductionTestPerformanceFixture.Attach(ProductionTestCreateVideoContext.Input(kf.Artifact.ArtifactId), p002), "regression", "c-t18-v", null, null));
        Ok(await Throws(svc.CreateLipsyncAsync(new ProductionActionApiCommand(
            sh01, ProductionTestPerformanceFixture.Attach(ProductionTestCreateVideoContext.Input() with
            {
                MotionArtifactId = videoP002.Artifact.ArtifactId,
                VoiceArtifactId = voiceP001.Artifact.ArtifactId,
            }, p002), "regression", "c-t18")),
            ProductionFailureReasons.PerformanceHashMismatch), "T18 LipSync different PerformanceHash FAIL");

        Ok(p001.Draft.Lines[0].TextIdentity.Text == p002.Draft.Lines[0].TextIdentity.Text
            && p001.PerformanceHash != p002.PerformanceHash, "T19 same line different PerformanceHash");
        Ok(await Throws(svc.CreateLipsyncAsync(new ProductionActionApiCommand(
            sh01, ProductionTestPerformanceFixture.Attach(ProductionTestCreateVideoContext.Input() with
            {
                MotionArtifactId = videoP002.Artifact.ArtifactId,
                VoiceArtifactId = voiceP001.Artifact.ArtifactId,
            }, p001), "regression", "c-t19")),
            ProductionFailureReasons.PerformanceHashMismatch), "T19 same line not enough FAIL");

        Ok(p001.Draft.Lines[0].CharacterId == p002.Draft.Lines[0].CharacterId, "T20 same character");
        Ok(await Throws(svc.CreateLipsyncAsync(new ProductionActionApiCommand(
            sh01, ProductionTestPerformanceFixture.Attach(ProductionTestCreateVideoContext.Input() with
            {
                MotionArtifactId = videoP002.Artifact.ArtifactId,
                VoiceArtifactId = voiceP001.Artifact.ArtifactId,
            }, p002), "regression", "c-t20")),
            ProductionFailureReasons.PerformanceHashMismatch), "T20 same character not enough FAIL");
        _ = kf2;
        _ = p001Sh02;
        _ = p001B;

        var eng = new ProductionWorkExecutionEngine(work, perf);
        var howTask = await eng.NewTaskForProviderChangeAsync(videoP001.Task.TaskId, "google", "veo-3.1", "regression");
        var how = await work.GetSnapshotByTaskAsync(howTask.TaskId);
        Ok(how is not null
            && how.PerformanceHash == p001.PerformanceHash
            && how.ProviderId == "google", "T21 provider change keeps PerformanceHash");

        var timed = await svc.CreateVideoAsync(new CreateVideoApiCommand(
            sh01, readyInput with { Timing = "10s", DurationSec = 10 }, "regression", "c-t22", null, null));
        var timedSnap = await work.GetSnapshotByTaskAsync(timed.Task.TaskId);
        Ok(timedSnap!.PerformanceHash == p001.PerformanceHash
            && timedSnap.TimingHash != videoSnap.TimingHash, "T22 timing change keeps PerformanceHash");

        var visual = await svc.CreateVideoAsync(new CreateVideoApiCommand(
            sh01, readyInput with { VisualContract = "TEST VISUAL — other frame" }, "regression", "c-t23", null, null));
        var visualSnap = await work.GetSnapshotByTaskAsync(visual.Task.TaskId);
        Ok(visualSnap!.PerformanceHash == p001.PerformanceHash
            && visualSnap.VisualHash != videoSnap.VisualHash, "T23 visual change keeps PerformanceHash");

        var frozen = await work.GetSnapshotByTaskAsync(video1.Task.TaskId);
        var immutable = false;
        try
        {
            await work.InsertSnapshotAsync(frozen! with { SnapshotId = Guid.NewGuid(), PerformanceHash = p002.PerformanceHash });
        }
        catch (InvalidOperationException ex) when (ex.Message == "SNAPSHOT_IMMUTABLE") { immutable = true; }
        var again = await work.GetSnapshotByTaskAsync(video1.Task.TaskId);
        Ok(immutable && again!.SnapshotId == frozen!.SnapshotId
            && again.PerformanceHash == p001.PerformanceHash, "T24 old task cannot mutate Performance identity");

        Ok(await Throws(svc.CreateVideoAsync(new CreateVideoApiCommand(
            sh02, ProductionTestPerformanceFixture.Attach(ProductionTestCreateVideoContext.Input(kf.Artifact.ArtifactId), p001), "regression", "c-t25", null, null)),
            PerformanceContractIds.CrossShotForbidden), "T25 cross-shot FAIL");

        Ok(await Throws(svc.CreateVideoAsync(new CreateVideoApiCommand(
            other, ProductionTestPerformanceFixture.Attach(ProductionTestCreateVideoContext.Input(), p001), "regression", "c-t26", null, null)),
            PerformanceContractIds.CrossScopeForbidden), "T26 cross-build FAIL");

        Ok(await Throws(svc.CreateVideoAsync(new CreateVideoApiCommand(
            other, ProductionTestPerformanceFixture.Attach(ProductionTestCreateVideoContext.Input(), p001), "regression", "c-t27", null, null)),
            PerformanceContractIds.CrossScopeForbidden), "T27 cross-test-scope FAIL");

        var official = 0;
        try
        {
            await new PerformanceContractService(perf).FreezeAsync(
                new PerformanceContractService(perf).CreateDraft(ProductionTestPerformanceFixture.Source(ProductionScopeAnchorRules.OfficialEp01, "EP01-SC01-SH01")),
                "regression");
        }
        catch (InvalidOperationException ex) when (ex.Message == PerformanceContractIds.OfficialFilmForbidden) { official++; }
        try
        {
            var film = new ProductionScope(ProductionScopeAnchorRules.OfficialEp99, "EP99", "SC01", "EP99-SC01-SH01");
            await ProductionPerformanceConsumption.RequireBoundAsync(perf, readyInput, film);
        }
        catch (InvalidOperationException ex) when (ex.Message == PerformanceContractIds.CrossScopeForbidden) { official++; }
        Ok(official == 2, "T28 official film FAIL");

        var reloaded = await work.GetSnapshotByTaskAsync(video1.Task.TaskId);
        Ok(reloaded!.PerformanceSnapshotId == p001.PerformanceSnapshotId
            && reloaded.PerformanceHash == p001.PerformanceHash, "T29 reload preserves SnapshotId + Hash");

        var replay = await svc.CreateVideoAsync(new CreateVideoApiCommand(sh01, readyInput with
        {
            Timing = "10s",
            DurationSec = 10,
            ActingBeat = timedSnap!.ActingBeat,
            KeyframeArtifactId = timedSnap.KeyframeArtifactId,
            KeyframeHash = timedSnap.KeyframeHash ?? "",
            KeyframeByteSha256 = timedSnap.KeyframeHash ?? "",
        }, "regression", "c-t22", null, null));
        Ok(replay.Request.RequestId == timed.Request.RequestId
            && replay.Task.TaskId == timed.Task.TaskId, "T30 duplicate idempotency");

        var getReady = await os.EvaluateCreateVideoAsync(sh01, readyClaim);
        var getBlocked = await os.EvaluateCreateVideoAsync(sh01, ProductionTestCreateVideoContext.Claim(kf.Artifact.ArtifactId));
        var postBlocked = await Throws(svc.CreateVideoAsync(new CreateVideoApiCommand(
            sh01, ProductionTestCreateVideoContext.Input(kf.Artifact.ArtifactId), "regression", "c-t31", null, null)),
            getBlocked.BlockReason ?? "");
        Ok(getReady.Available && !getBlocked.Available && postBlocked
            && getBlocked.BlockReason == ProductionFailureReasons.MissingPerformanceSnapshot, "T31 GET readiness == POST execution gate");

        var legacy = ProductionTestCreateVideoContext.Input(kf.Artifact.ArtifactId) with
        {
            PerformanceSnapshotId = p001.PerformanceSnapshotId,
            PerformanceHash = ProductionTestCreateVideoContext.ActingHash,
        };
        var t32 = await os.EvaluateCreateVideoAsync(sh01, CreateVideoActionContractGate.ClaimFrom(legacy));
        Ok(!t32.Available && (t32.BlockReason == PerformanceContractIds.InvalidPerformanceHash
            || t32.BlockReason == ProductionFailureReasons.FailedIdentity), "T32 legacy fallback FAIL");

        await RegressionSuites(Ok);
    }

    static async Task RegressionSuites(Action<bool, string> Ok)
    {
        var video = ProductionTestCreateVideoV1Regression.Run();
        Ok(video.Count == 0, "REGRESSION CREATE_VIDEO TEST FAIL=" + video.Count + (video.Count == 0 ? "" : ":" + string.Join(",", video.Take(3))));
        var authority = ProductionCompleteActionAuthorityV1Regression.Run();
        Ok(authority.Count == 0, "REGRESSION Complete Action Authority FAIL=" + authority.Count + (authority.Count == 0 ? "" : ":" + string.Join(",", authority.Take(3))));
        var phaseA = PerformanceContractV1Regression.Run();
        Ok(phaseA.Count == 0, "REGRESSION Phase A FAIL=" + phaseA.Count + (phaseA.Count == 0 ? "" : ":" + string.Join(",", phaseA.Take(3))));
        var phaseB = PerformanceSnapshotBindingV1Regression.Run();
        Ok(phaseB.Count == 0, "REGRESSION Phase B FAIL=" + phaseB.Count + (phaseB.Count == 0 ? "" : ":" + string.Join(",", phaseB.Take(3))));
    }

    static async Task<bool> Throws(Task act, string reason)
    {
        try
        {
            await act;
            return false;
        }
        catch (InvalidOperationException ex)
        {
            return reason.Length == 0 || ex.Message == reason;
        }
    }
}
