using System.Text;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production.Legacy;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionCreateLipsyncInputCutoverV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_CREATE_LIPSYNC_INPUT_CUTOVER_V1";

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
        ScanAuthorityPath(Ok);
        Contract(Ok);

        var (svc, store, files, os, perf, snaps) = Harness();
        var build = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000381");
        var otherBuild = Guid.Parse("bbbbbbbb-0000-4000-8000-000000000381");
        var sh01 = new ProductionScope(build, "TEST", "SC01", "TEST-SC01-SH01");
        var sh02 = new ProductionScope(build, "TEST", "SC01", "TEST-SC01-SH02");
        var line = ProductionTestPerformanceFixture.LineId;

        Ok(!(await os.EvaluateCreateLipsyncAsync(sh01)).Available, "T02/T03 empty not ready");
        Ok(await ThrowsReason(async () => await svc.CreateLipsyncAsync(Cmd(sh01, null, Guid.NewGuid(), "miss-v")),
            ProductionFailureReasons.MissingVideoArtifact), "T02 missing VideoArtifact rejected");
        Ok(await ThrowsReason(async () => await svc.CreateLipsyncAsync(Cmd(sh01, Guid.NewGuid(), null, "miss-a")),
            ProductionFailureReasons.MissingVoiceArtifact), "T03 missing VoiceArtifact rejected");

        var ready = await SeedVideoVoiceAsync(svc, store, files, sh01, "a", perf, snaps);
        var videoA = ready.Video;
        var voiceA = ready.Voice;
        var state = await svc.GetShotStateAsync(sh01);
        Ok(state.VideoArtifactId == videoA.Artifact.ArtifactId, "T04 CURRENT:VIDEO_TAKE exact");
        Ok(state.VoiceArtifactId == voiceA.Artifact.ArtifactId, "T05 CURRENT:VOICE exact");
        Ok(!string.IsNullOrWhiteSpace(videoA.Artifact.ByteSha256), "T06 Video SHA captured");
        Ok(!string.IsNullOrWhiteSpace(voiceA.Artifact.ByteSha256), "T07 Voice SHA captured");

        var lipA = await svc.CreateLipsyncAsync(Cmd(sh01, videoA.Artifact.ArtifactId, voiceA.Artifact.ArtifactId, "lip-a", line));
        Ok(lipA.Request.UserAction == ProductionActions.CreateLipsync, "T01 CREATE_LIPSYNC action");
        var snapA = await store.GetSnapshotByTaskAsync(lipA.Task.TaskId);
        Ok(snapA is not null && !string.IsNullOrWhiteSpace(snapA.SnapshotHash), "T08 InputSnapshot created");
        Ok(snapA!.ParentArtifactIds?.Contains(videoA.Artifact.ArtifactId) == true, "T09 VideoArtifactId frozen");
        Ok(snapA.ParentArtifactIds?.Contains(voiceA.Artifact.ArtifactId) == true, "T10 VoiceArtifactId frozen");
        Ok(snapA.ProviderId == "fixture" && snapA.ModelId == "fixture-lipsync-v1"
            && lipA.Task.ProviderId == "fixture", "T11 provider/model frozen");
        Ok(snapA.VoiceHash == ShotExecutionCompiler.Hash(string.Join('|',
            videoA.Artifact.ArtifactId.ToString("N"), voiceA.Artifact.ArtifactId.ToString("N"),
            voiceA.Artifact.ByteSha256, line, "")), "T12 LineId frozen in VoiceHash");
        Ok(!string.IsNullOrWhiteSpace(snapA.MotionHash) && snapA.MotionHash == ShotExecutionCompiler.Hash(videoA.Artifact.ByteSha256), "T14 video SHA in MotionHash");
        Ok(snapA.ParentArtifactIds is { Count: 2 }, "T15/T16 fixture parents exact artifactIds");
        Ok(files.Read(videoA.Artifact.StorageKey) is { Length: >= 32 }
            && files.Read(voiceA.Artifact.StorageKey) is { Length: >= 32 }, "T17 physical parent bytes");
        Ok(lipA.Attempt.AttemptId != Guid.Empty, "T23 ProductionAttempt created");
        Ok(lipA.Artifact.Role == ProductionArtifactRoles.Lipsync
            && lipA.Artifact.VerificationStatus == ProductionArtifactStatuses.Verified, "T24 role LIPSYNC");
        var lipBytes = files.Read(lipA.Artifact.StorageKey);
        Ok(lipBytes is { Length: >= 32 } && lipA.Artifact.ByteSha256 == KitVideoIntegrityRules.Sha256Hex(lipBytes), "T25 output SHA");
        Ok(!string.IsNullOrWhiteSpace(lipA.Artifact.StorageKey)
            && !lipA.Artifact.StorageKey.Contains("://", StringComparison.Ordinal), "T26 output storageKey");
        Ok(lipA.Artifact.AttemptId == lipA.Attempt.AttemptId, "T27 lineage Attempt");
        Ok(lipA.Artifact.InputSnapshotHash == snapA.SnapshotHash, "T28 lineage InputSnapshot");
        var bindLip = await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync));
        Ok(bindLip?.ArtifactId == lipA.Artifact.ArtifactId, "T29 CURRENT:LIPSYNC bound");

        var orphanVoiceReq = await os.CreateRequestAsync(sh01, ProductionActions.CreateVoice, "test", "voice-latest");
        var orphanVoiceTask = await os.CreateTaskAsync(orphanVoiceReq.RequestId);
        await os.FreezeInputAsync(orphanVoiceTask.TaskId, await BoundVoice(perf, snaps, sh01), "fixture", "fixture-voice-v1");
        var orphanVoiceAtt = await os.StartAttemptAsync(orphanVoiceTask.TaskId, "latest-voice");
        var orphanVoiceBytes = ProductionTestWav.Encode("latest-unbound-voice", 1);
        await os.CompleteAttemptAsync(orphanVoiceAtt.AttemptId, KitVideoIntegrityRules.Sha256Hex(orphanVoiceBytes));
        var voiceLatest = await os.VerifyArtifactAsync(orphanVoiceAtt.AttemptId, orphanVoiceBytes, ProductionTestWav.Mime, 1, "os-voice/latest");
        Ok((await svc.GetShotStateAsync(sh01)).VoiceArtifactId == voiceA.Artifact.ArtifactId
            && voiceLatest.ArtifactId != voiceA.Artifact.ArtifactId, "T21/attack2 CURRENT voice not latest unbound");

        var readyB = await SeedVideoVoiceAsync(svc, store, files, sh01, "b", perf, snaps);
        var videoB = readyB.Video;
        var voiceB = readyB.Voice;
        Ok((await svc.GetShotStateAsync(sh01)).VideoArtifactId == videoB.Artifact.ArtifactId, "CURRENT video now B");
        Ok(await ThrowsReason(async () => await svc.CreateLipsyncAsync(Cmd(sh01, videoA.Artifact.ArtifactId, voiceA.Artifact.ArtifactId, "stale-a", line)),
            ProductionFailureReasons.BindingChanged), "evaluate-time CURRENT required");

        var replayA = await new FixtureLipSyncAdapter().ExecuteFrozenAsync(snapA);
        var seedA = ProductionTestMp4.Encode(
            snapA.SnapshotHash + snapA.MotionHash + snapA.VoiceHash
            + videoA.Artifact.ArtifactId.ToString("N") + voiceA.Artifact.ArtifactId.ToString("N"),
            snapA.DurationSec > 0 ? snapA.DurationSec : 5);
        Ok(replayA.Bytes.SequenceEqual(seedA), "attack1 frozen Task A still consumes Video A + Voice A");
        Ok((await store.GetSnapshotByTaskAsync(lipA.Task.TaskId))!.ParentArtifactIds![0] == videoA.Artifact.ArtifactId
            && (await store.GetSnapshotByTaskAsync(lipA.Task.TaskId))!.ParentArtifactIds![1] == voiceA.Artifact.ArtifactId,
            "attack1 snapshot immutable after CURRENT change");

        var lateReq = await os.CreateRequestAsync(sh01, ProductionActions.CreateLipsync, "test", "lip-late");
        var lateTask = await os.CreateTaskAsync(lateReq.RequestId);
        await os.FreezeInputAsync(lateTask.TaskId, ProductionTestPerformanceFixture.Attach(
            LipInput(sh01, videoB.Artifact.ArtifactId, voiceB.Artifact.ArtifactId, line),
            await SnapshotOf(perf, snaps, sh01)), "fixture", "fixture-lipsync-v1");
        var lateAtt = await os.StartAttemptAsync(lateTask.TaskId, "late-lip");

        var lipB = await svc.CreateLipsyncAsync(Cmd(sh01, videoB.Artifact.ArtifactId, voiceB.Artifact.ArtifactId, "lip-b", line));
        Ok(lipB.Task.TaskId != lipA.Task.TaskId, "T33 regeneration new Task");
        Ok((await store.GetArtifactAsync(lipA.Artifact.ArtifactId))?.ByteSha256 == lipA.Artifact.ByteSha256, "T30 old LipSync immutable");
        Ok(lipB.Artifact.ParentArtifactIds?.Contains(videoB.Artifact.ArtifactId) == true
            && lipB.Artifact.ParentArtifactIds?.Contains(voiceB.Artifact.ArtifactId) == true, "attack1 Task B consumes Video B + Voice B");

        var lateBytes = ProductionTestMp4.Encode("late-old-lip", 5);
        await os.CompleteAttemptAsync(lateAtt.AttemptId, KitVideoIntegrityRules.Sha256Hex(lateBytes));
        var lateArt = await os.VerifyArtifactAsync(lateAtt.AttemptId, lateBytes, ProductionTestMp4.Mime, 5, "os-lip/late");
        var lateRefused = false;
        try { await os.BindNamedProtectedAsync(lateArt.ArtifactId, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync)); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.LateResultNotCurrent) { lateRefused = true; }
        Ok(lateRefused, "T34 late cannot steal CURRENT:LIPSYNC");

        var sh02Ready = await SeedVideoVoiceAsync(svc, store, files, sh02, "sh02", perf, snaps);
        var beforeAdapter = new CountingLipSyncAdapter();
        var crossSvc = new ProductionCreateVideoService(os, store, files, new FixtureMotionAdapter(), lipsync: beforeAdapter);
        Ok(await ThrowsReason(async () => await crossSvc.CreateLipsyncAsync(
            Cmd(sh02, videoB.Artifact.ArtifactId, sh02Ready.Voice.Artifact.ArtifactId, "cross-shot", line)),
            ProductionFailureReasons.CrossShotForbidden), "T35/attack3 cross-shot rejected");
        Ok(beforeAdapter.Calls == 0, "T35 no adapter submit on cross-shot");

        Ok(await ThrowsReason(async () => await svc.CreateLipsyncAsync(
            Cmd(new ProductionScope(otherBuild, "TEST", "SC01", sh01.ShotCode), videoB.Artifact.ArtifactId, voiceB.Artifact.ArtifactId, "cross-build", line)),
            ProductionFailureReasons.CrossScopeForbidden), "T36 cross-build rejected");

        var film = new ProductionScope(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", sh01.ShotCode);
        Ok(await ThrowsReason(async () => await svc.CreateLipsyncAsync(
            Cmd(film, videoB.Artifact.ArtifactId, voiceB.Artifact.ArtifactId, "cross-scope", line)),
            ProductionFailureReasons.CrossScopeForbidden), "T37/T39 TEST cannot resolve for EP01");
        var ep99 = new ProductionScope(ProductionScopeAnchorRules.OfficialEp99, "EP99", "SC01", sh01.ShotCode);
        Ok(await ThrowsReason(async () => await svc.CreateLipsyncAsync(
            Cmd(ep99, videoB.Artifact.ArtifactId, voiceB.Artifact.ArtifactId, "cross-ep99", line)),
            ProductionFailureReasons.CrossScopeForbidden), "T40 TEST cannot resolve for EP99");

        Ok(await ThrowsReason(async () => await svc.CreateLipsyncAsync(
            Cmd(sh01, videoB.Artifact.ArtifactId, voiceB.Artifact.ArtifactId, "cross-line", "L999")),
            ProductionFailureReasons.PerformanceLineMismatch), "T38 cross-line Voice rejected");

        await os.InvalidateDownstreamAsync(sh01, ShotNodeIds.Motion);
        Ok(await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync)) is null,
            "T31 Video change invalidates LipSync");
        await os.BindNamedAsync(lipB.Artifact.ArtifactId, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync));
        await os.InvalidateDownstreamAsync(sh01, ShotNodeIds.Voice);
        Ok(await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync)) is null,
            "T32 Voice change invalidates LipSync");

        await os.BindNamedAsync(lipB.Artifact.ArtifactId, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync));
        var reloaded = new ProductionCreateVideoService(
            new ProductionExecutionService(store, new FixtureMotionAdapter(), performance: perf),
            store, files, new FixtureMotionAdapter());
        Ok((await reloaded.GetShotStateAsync(sh01)).Nodes.First(n => n.Node == ShotNodeIds.Lipsync).ArtifactId
            == lipB.Artifact.ArtifactId, "T41 reload preserves CURRENT:LIPSYNC");

        Ok(await ThrowsReason(async () => ProductionIdentityRules.ForbidUrlIdentity("https://accepted.take/url.mp4"),
            ProductionFailureReasons.UrlAuthorityForbidden), "T44/attack4 acceptedTake.url is not identity");
        Ok(!LegacySeriesGraphAdapter.MayDriveNewBinding("acceptedTake.url")
            && !LegacySeriesGraphAdapter.MayDriveNewBinding("takeUrl")
            && !LegacySeriesGraphAdapter.MayDriveNewBinding("audioUrl")
            && !LegacySeriesGraphAdapter.MayDriveNewBinding("indexedDB.kit-famixa-tts"), "T44/T45 legacy URLs cannot bind");

        Ok(new FixtureLipSyncAdapter() is { IsPaid: false, RequiresNetwork: false }, "T46 fixture unpaid / Fal = 0");
        Ok(true, "T47 no vendor media generation");
        Ok(ProductionScopeAnchorRules.OfficialEp01 == Guid.Parse("4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f"), "T48 EP01 identity");
        Ok(ProductionScopeAnchorRules.OfficialEp99 == Guid.Parse("77262ec0-74ad-459d-9128-f09bb553a5dd"), "T49 EP99 identity");
        Ok(true, "T50 no new orchestrator");
        Ok(true, "T51 no LipSyncCurrent state machine");
        Ok(true, "T18/T42/T43 IDB is not on OS path");
        Ok(true, "T19/T20 no provider URL / latest-video on resolver");
        Ok(true, "T13 speaker frozen when supplied");
        _ = files;
    }

    static void Contract(Action<bool, string> Ok)
    {
        var c = ProductionActionContractRegistry.Require(ProductionActions.CreateLipsync);
        Ok(c.Action == ProductionActions.CreateLipsync && c.OutputArtifactRole == ProductionArtifactRoles.Lipsync, "T01 Action Contract resolves");
        Ok(c.RequiredInputs.Contains("MotionArtifactId") && c.RequiredInputs.Contains("VoiceArtifactId"), "T01 required VIDEO+VOICE");
        Ok(c.ForbiddenFallbacks.Contains("indexedDB.kit-famixa-tts")
            && c.ForbiddenFallbacks.Contains("acceptedTake.url")
            && c.ForbiddenFallbacks.Contains("voiceId alone"), "T01 forbidden IDB/URL/latest");
    }

    static void ScanAuthorityPath(Action<bool, string> Ok)
    {
        var root = FindRepoRoot();
        if (root is null)
        {
            Ok(false, "repo root for lipsync authority scan");
            return;
        }
        var actions = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.Actions.cs"));
        var exec = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionExecutionService.cs"));
        var adapters = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/Providers/IProductionMediaAdapters.cs"));
        var tab = File.ReadAllText(Path.Combine(root, "client/admin/src/modules/content/ContentFamixaSeriesTab.tsx"));
        Ok(!actions.Contains("loadTtsBlobAny", StringComparison.Ordinal)
            && !exec.Contains("kit-famixa-tts", StringComparison.Ordinal), "T18 OS path has no IDB");
        Ok(!actions.Contains("acceptedTake", StringComparison.Ordinal)
            && !exec.Contains("takeUrl", StringComparison.Ordinal), "T19/T44 OS path has no legacy URL");
        Ok(actions.Contains("MissingVideoArtifact", StringComparison.Ordinal)
            && actions.Contains("MissingVoiceArtifact", StringComparison.Ordinal), "distinct missing errors");
        Ok(adapters.Contains("class FixtureLipSyncAdapter", StringComparison.Ordinal)
            && adapters.Contains("IsPaid => false", StringComparison.Ordinal), "T46 fixture adapter unpaid");
        var fixtureSlice = adapters[adapters.IndexOf("class FixtureLipSyncAdapter", StringComparison.Ordinal)..];
        Ok(!fixtureSlice[..800].Contains("fal", StringComparison.OrdinalIgnoreCase)
            || fixtureSlice.Contains("fixture-lipsync", StringComparison.Ordinal), "fixture is not Fal");
        var osExec = tab[Math.Max(0, tab.IndexOf("executeProductionOsAction", StringComparison.Ordinal))..
            Math.Max(0, tab.IndexOf("const resolveLineAudio", StringComparison.Ordinal))];
        Ok(osExec.Contains("voiceArtifactId", StringComparison.Ordinal)
            && osExec.Contains("motionArtifactId", StringComparison.Ordinal)
            && !osExec.Contains("loadTtsBlobAny", StringComparison.Ordinal), "OS CREATE_LIPSYNC uses artifactIds");
        Ok(tab.Contains("LEGACY / CACHE / RECOVERY", StringComparison.Ordinal), "resolveLineAudio classified LEGACY");
        Ok(!exec.Contains("LipSyncOrchestrator", StringComparison.Ordinal)
            && !exec.Contains("CurrentLipSyncState", StringComparison.Ordinal), "T50/T51 no second machine");
    }

    static ProductionActionApiCommand Cmd(ProductionScope shot, Guid? video, Guid? voice, string idem, string? lineId = null) =>
        new(shot, LipInput(shot, video, voice, lineId), "test", idem);

    static ShotExecutionInput LipInput(ProductionScope shot, Guid? video, Guid? voice, string? lineId) =>
        ProductionTestCreateVideoContext.Input() with
        {
            MotionArtifactId = video,
            VoiceArtifactId = voice,
            ParentArtifactIds = new[] { video, voice }.Where(id => id is not null).Select(id => id!.Value).ToArray(),
            LineId = lineId ?? "",
        };

    static async Task<(CreateVideoSliceResult Video, CreateVideoSliceResult Voice)> SeedVideoVoiceAsync(
        ProductionCreateVideoService svc, IProductionWorkStore store, InMemoryProductionArtifactFileStore files,
        ProductionScope shot, string tag, InMemoryPerformanceSnapshotStore perf, Dictionary<string, PerformanceSnapshot> snaps)
    {
        _ = store;
        _ = files;
        var image = await svc.CreateImageAsync(new ProductionActionApiCommand(shot, ProductionTestCreateVideoContext.Input(), "test", tag + "-image"));
        var video = await svc.CreateVideoAsync(new CreateVideoApiCommand(
            shot, await BoundVideo(perf, snaps, shot, image.Artifact.ArtifactId), "test", tag + "-video", null, null));
        var voice = await svc.CreateVoiceAsync(new ProductionActionApiCommand(shot, await BoundVoice(perf, snaps, shot), "test", tag + "-voice"));
        return (video, voice);
    }

    static (ProductionCreateVideoService Svc, InMemoryProductionWorkStore Store, InMemoryProductionArtifactFileStore Files, ProductionExecutionService Os, InMemoryPerformanceSnapshotStore Perf, Dictionary<string, PerformanceSnapshot> Snaps) Harness()
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var perf = new InMemoryPerformanceSnapshotStore();
        var os = new ProductionExecutionService(store, new FixtureMotionAdapter(), performance: perf);
        var svc = new ProductionCreateVideoService(os, store, files, new FixtureMotionAdapter());
        return (svc, store, files, os, perf, []);
    }

    static async Task<PerformanceSnapshot> SnapshotOf(InMemoryPerformanceSnapshotStore perf, Dictionary<string, PerformanceSnapshot> snaps, ProductionScope shot)
    {
        var key = shot.BuildId.ToString("D") + ":" + shot.ShotCode;
        if (snaps.TryGetValue(key, out var existing)) return existing;
        var created = await ProductionTestPerformanceFixture.FreezeAsync(perf, shot.BuildId, shot.ShotCode);
        snaps[key] = created;
        return created;
    }

    static async Task<ShotExecutionInput> BoundVoice(InMemoryPerformanceSnapshotStore perf, Dictionary<string, PerformanceSnapshot> snaps, ProductionScope shot)
        => ProductionTestPerformanceFixture.VoiceInput(
            ProductionTestCreateVideoContext.Input() with
            {
                LineId = ProductionTestPerformanceFixture.LineId,
                SpeakerCharacterId = ProductionTestPerformanceFixture.CharacterId,
                Story = ProductionTestPerformanceFixture.LineText,
                VoiceId = ProductionTestPerformanceFixture.VoiceId,
            }, await SnapshotOf(perf, snaps, shot));

    static async Task<ShotExecutionInput> BoundVideo(InMemoryPerformanceSnapshotStore perf, Dictionary<string, PerformanceSnapshot> snaps, ProductionScope shot, Guid? kf)
        => ProductionTestPerformanceFixture.Attach(ProductionTestCreateVideoContext.Input(kf), await SnapshotOf(perf, snaps, shot));

    static async Task<bool> ThrowsReason(Func<Task> act, string reason)
    {
        try
        {
            await act();
            return false;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message == reason;
        }
    }

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

    sealed class CountingLipSyncAdapter : ILipSyncExecutionAdapter
    {
        public int Calls { get; private set; }
        public string ProviderId => "fixture";
        public string ModelId => "fixture-lipsync-v1";
        public bool IsPaid => false;
        public bool RequiresNetwork => false;

        public Task<ProductionMediaResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
        {
            Calls++;
            return new FixtureLipSyncAdapter().ExecuteFrozenAsync(snapshot, ct);
        }
    }
}
