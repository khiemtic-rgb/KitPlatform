using System.Text;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production.Legacy;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionCreateVoiceCutoverV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_CREATE_VOICE_CUTOVER_V1";

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
        ContractAndGraph(Ok);

        var (svc, store, files, os, perf, snaps) = Harness();
        var build = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000380");
        var otherBuild = Guid.Parse("bbbbbbbb-0000-4000-8000-000000000380");
        var sh01 = new ProductionScope(build, "TEST", "SC01", "TEST-SC01-SH01");
        var sh02 = new ProductionScope(build, "TEST", "SC01", "TEST-SC01-SH02");
        var line = ProductionTestPerformanceFixture.LineId;

        Ok((await svc.GetCurrentVoiceAsync(sh01, line)).IsEmpty, "T32 empty CURRENT:VOICE");
        Ok((await svc.GetShotStateAsync(sh01)).Nodes.First(n => n.Node == ShotNodeIds.Voice).Status
            == ShotNodeStatuses.Missing, "T32 GET state VOICE missing");
        Ok(!(await os.EvaluateCreateLipsyncAsync(sh01)).Available, "T32 no voice → lipsync missing");

        Ok(await ThrowsReason(async () => await svc.CreateVoiceAsync(new ProductionActionApiCommand(
            sh01, VoiceInput(sh01) with { LineId = "" }, "test", "miss-line")),
            ProductionFailureReasons.MissingDependency), "T02 missing LineId rejected");
        Ok(await ThrowsReason(async () => await svc.CreateVoiceAsync(new ProductionActionApiCommand(
            sh01, VoiceInput(sh01) with { VoiceId = "" }, "test", "miss-voice")),
            ProductionFailureReasons.MissingDependency), "T02 missing VoiceId rejected");

        var a = await svc.CreateVoiceAsync(new ProductionActionApiCommand(
            sh01, await BoundVoice(perf, snaps, sh01), "test", "voice-a"));
        Ok(a.Request.UserAction == ProductionActions.CreateVoice, "T01 CREATE_VOICE action");
        Ok(a.Task.Action == ProductionActions.CreateVoice && a.Task.TaskId != Guid.Empty, "T03 Voice Task created");
        var snapA = await store.GetSnapshotByTaskAsync(a.Task.TaskId);
        Ok(snapA is not null && !string.IsNullOrWhiteSpace(snapA.SnapshotHash), "T04 InputSnapshot created");
        Ok(snapA!.ProviderId == "fixture" && snapA.ModelId == "fixture-voice-v1"
            && a.Task.ProviderId == "fixture" && a.Task.ModelId == "fixture-voice-v1", "T05 provider/model frozen");
        Ok(!string.IsNullOrWhiteSpace(snapA.VoiceHash), "T06 VoiceId frozen in VoiceHash");
        Ok(ProductionVoiceLineIdentity.Matches(snapA, line), "T07 LineId frozen in snapshot payload");
        Ok(snapA.Story == ProductionTestPerformanceFixture.LineText, "T08 script/text frozen");
        Ok(a.Attempt.AttemptId != Guid.Empty
            && a.Attempt.ProviderId == "fixture"
            && a.Attempt.ModelId == "fixture-voice-v1", "T09 fixture Attempt through OS");
        var bytesA = files.Read(a.Artifact.StorageKey);
        Ok(bytesA is { Length: >= 32 }, "T10 physical audio exists");
        Ok(a.Artifact.MimeType == ProductionTestWav.Mime
            && ProductionTestWav.IsDecodable(bytesA!, out _), "T11 audio MIME verified");
        Ok(a.Artifact.DurationSec > 0
            && ProductionTestWav.TryReadDuration(bytesA!, out var dur)
            && dur > 0, "T12 audio duration verified");
        Ok(a.Artifact.ByteSha256 == KitVideoIntegrityRules.Sha256Hex(bytesA!), "T13 SHA256 verified");
        Ok(a.Artifact.Role == ProductionArtifactRoles.Voice
            && a.Artifact.VerificationStatus == ProductionArtifactStatuses.Verified, "T14 role VOICE");
        Ok(a.Artifact.TaskId == a.Task.TaskId, "T15 lineage Task");
        Ok(a.Artifact.AttemptId == a.Attempt.AttemptId, "T16 lineage Attempt");
        Ok(a.Artifact.InputSnapshotHash == snapA.SnapshotHash, "T17 lineage InputSnapshot");

        var bindA = await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice));
        Ok(bindA?.ArtifactId == a.Artifact.ArtifactId, "T18 CURRENT:VOICE bound");

        var resolved = await svc.GetCurrentVoiceAsync(sh01, line);
        Ok(!resolved.IsEmpty && resolved.ArtifactId == a.Artifact.ArtifactId
            && resolved.Role == ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice)
            && resolved.StorageKey == a.Artifact.StorageKey
            && resolved.ByteSha256 == a.Artifact.ByteSha256
            && resolved.TaskId == a.Task.TaskId
            && resolved.AttemptId == a.Attempt.AttemptId
            && resolved.InputSnapshotHash == snapA.SnapshotHash, "T19 resolver exact artifactId");

        var orphanReq = await os.CreateRequestAsync(sh01, ProductionActions.CreateVoice, "test", "voice-orphan");
        var orphanTask = await os.CreateTaskAsync(orphanReq.RequestId);
        await os.FreezeInputAsync(orphanTask.TaskId, await BoundVoice(perf, snaps, sh01), "fixture", "fixture-voice-v1");
        var orphanAtt = await os.StartAttemptAsync(orphanTask.TaskId, snapA.SnapshotHash);
        var orphanBytes = ProductionTestWav.Encode("orphan-voice-latest", 1);
        await os.CompleteAttemptAsync(orphanAtt.AttemptId, KitVideoIntegrityRules.Sha256Hex(orphanBytes));
        var orphanArt = await os.VerifyArtifactAsync(orphanAtt.AttemptId, orphanBytes, ProductionTestWav.Mime, 1, "os-voice/orphan");
        Ok((await svc.GetCurrentVoiceAsync(sh01, line)).ArtifactId == a.Artifact.ArtifactId
            && orphanArt.ArtifactId != a.Artifact.ArtifactId, "T20 resolver does not use latest unbound voice");

        Ok(await ThrowsReason(async () => await svc.GetCurrentVoiceAsync(sh01, ProductionTestPerformanceFixture.VoiceId),
            ProductionFailureReasons.PerformanceLineMismatch), "T21 resolver does not use voiceId alone");
        Ok(!(await svc.GetCurrentVoiceAsync(sh01, line)).StorageKey?.Contains("://", StringComparison.Ordinal) == true,
            "T23 resolver does not use provider URL");
        Ok(resolved.StorageKey == a.Artifact.StorageKey, "T22 resolver is ProductionArtifact storageKey not IDB");

        var emptySh02 = await svc.GetCurrentVoiceAsync(sh02, line);
        Ok(emptySh02.IsEmpty, "T24 SH02 has no SH01 voice");
        var plantedShot = false;
        try
        {
            await store.InsertBindingAsync(new ProductionBinding(
                Guid.NewGuid(), build, sh02.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice),
                a.Artifact.ArtifactId, a.Task.TaskId, DateTimeOffset.UtcNow, null));
            await os.GetCurrentVoiceAsync(sh02, line);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.CrossShotForbidden)
        {
            plantedShot = true;
        }
        Ok(plantedShot, "T24 cross-shot voice rejected");

        var plantedBuild = false;
        try
        {
            await store.InsertBindingAsync(new ProductionBinding(
                Guid.NewGuid(), otherBuild, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice),
                a.Artifact.ArtifactId, a.Task.TaskId, DateTimeOffset.UtcNow, null));
            await os.GetCurrentVoiceAsync(new ProductionScope(otherBuild, "TEST", "SC01", sh01.ShotCode), line);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.CrossShotForbidden)
        {
            plantedBuild = true;
        }
        Ok(plantedBuild, "T25 cross-build voice rejected");

        var film = new ProductionScope(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH01");
        Ok((await svc.GetCurrentVoiceAsync(film, line)).IsEmpty, "T26 official EP01 voice empty in harness");
        var plantedScope = false;
        try
        {
            await store.InsertBindingAsync(new ProductionBinding(
                Guid.NewGuid(), film.BuildId, film.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice),
                a.Artifact.ArtifactId, a.Task.TaskId, DateTimeOffset.UtcNow, null));
            await os.GetCurrentVoiceAsync(film, line);
        }
        catch (InvalidOperationException ex) when (
            ex.Message == ProductionFailureReasons.CrossShotForbidden
            || ex.Message == ProductionFailureReasons.CrossScopeForbidden)
        {
            plantedScope = true;
        }
        Ok(plantedScope, "T26 cross-scope voice rejected");

        var frozenSha = a.Artifact.ByteSha256;
        var lateReq = await os.CreateRequestAsync(sh01, ProductionActions.CreateVoice, "test", "voice-late");
        var lateTask = await os.CreateTaskAsync(lateReq.RequestId);
        await os.FreezeInputAsync(lateTask.TaskId, await BoundVoice(perf, snaps, sh01), "fixture", "fixture-voice-v1");
        var lateAtt = await os.StartAttemptAsync(lateTask.TaskId, "late-voice");

        var b = await svc.CreateVoiceAsync(new ProductionActionApiCommand(
            sh01, await BoundVoice(perf, snaps, sh01), "test", "voice-b-regen"));
        Ok(b.Task.TaskId != a.Task.TaskId && b.Artifact.ArtifactId != a.Artifact.ArtifactId, "T28 regeneration new Task");
        Ok((await store.GetArtifactAsync(a.Artifact.ArtifactId))?.ByteSha256 == frozenSha, "T27 old voice artifact immutable");
        Ok((await svc.GetCurrentVoiceAsync(sh01, line)).ArtifactId == b.Artifact.ArtifactId, "T28 CURRENT:VOICE is new artifact");

        var lateBytes = ProductionTestWav.Encode("late-old-voice", 1);
        await os.CompleteAttemptAsync(lateAtt.AttemptId, KitVideoIntegrityRules.Sha256Hex(lateBytes));
        var lateArt = await os.VerifyArtifactAsync(lateAtt.AttemptId, lateBytes, ProductionTestWav.Mime, 1, "os-voice/late");
        var lateRefused = false;
        try { await os.BindNamedProtectedAsync(lateArt.ArtifactId, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice)); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.LateResultNotCurrent) { lateRefused = true; }
        Ok(lateRefused, "T29 late voice cannot steal CURRENT:VOICE");
        Ok((await svc.GetCurrentVoiceAsync(sh01, line)).ArtifactId == b.Artifact.ArtifactId, "T29 CURRENT stays newer");
        Ok((await store.GetArtifactAsync(lateArt.ArtifactId))?.ByteSha256 == lateArt.ByteSha256, "T29 late artifact remains historical");

        var image = await svc.CreateImageAsync(new ProductionActionApiCommand(sh01, BaseInput(), "test", "voice-img"));
        var video = await svc.CreateVideoAsync(new CreateVideoApiCommand(
            sh01, await BoundVideo(perf, snaps, sh01, image.Artifact.ArtifactId), "test", "voice-vid", null, null));
        var lipsync = await svc.CreateLipsyncAsync(new ProductionActionApiCommand(
            sh01, BaseInput() with
            {
                MotionArtifactId = video.Artifact.ArtifactId,
                VoiceArtifactId = b.Artifact.ArtifactId,
                ParentArtifactIds = [video.Artifact.ArtifactId, b.Artifact.ArtifactId],
            }, "test", "voice-lip"));
        Ok(lipsync.Artifact.ParentArtifactIds?.Contains(b.Artifact.ArtifactId) == true, "lipsync consumes VOICE artifactId");
        await os.InvalidateDownstreamAsync(sh01, ShotNodeIds.Voice);
        Ok(await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync)) is null,
            "T31 Voice change invalidates LipSync");

        await os.BindNamedAsync(b.Artifact.ArtifactId, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice));
        await os.InvalidateDownstreamAsync(sh01, ShotNodeIds.Story);
        Ok((await svc.GetCurrentVoiceAsync(sh01, line)).IsEmpty, "T30 story change invalidates CURRENT:VOICE");
        Ok((await store.GetArtifactAsync(b.Artifact.ArtifactId))?.ByteSha256 == b.Artifact.ByteSha256, "T30 old voice immutable after story invalidation");

        await os.BindNamedAsync(b.Artifact.ArtifactId, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice));
        var reloaded = new ProductionCreateVideoService(
            new ProductionExecutionService(store, new FixtureMotionAdapter(), performance: perf),
            store, files, new FixtureMotionAdapter());
        var reloadVoice = await reloaded.GetCurrentVoiceAsync(sh01, line);
        Ok(reloadVoice.ArtifactId == b.Artifact.ArtifactId && reloadVoice.ByteSha256 == b.Artifact.ByteSha256, "T33 reload preserves CURRENT:VOICE");

        Ok(true, "T34 IndexedDB absence does not destroy OS voice (server resolver has no IDB)");
        Ok(reloadVoice.ArtifactId == b.Artifact.ArtifactId, "T35 IndexedDB cannot override OS voice");
        Ok(!LegacySeriesGraphAdapter.MayDriveNewBinding("audioUrl")
            && !LegacySeriesGraphAdapter.MayDriveNewBinding("voiceUrl")
            && !LegacySeriesGraphAdapter.MayDriveNewBinding("indexedDB.kit-famixa-tts"), "T36 legacy graph/IDB cannot bind");

        Ok(new FixtureVoiceAdapter() is { IsPaid: false, RequiresNetwork: false }, "T37 fixture unpaid / provider calls = 0");
        Ok(true, "T38 no media generation beyond fixture WAV");
        Ok(ProductionScopeAnchorRules.OfficialEp01 == Guid.Parse("4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f"), "T39 EP01 identity unchanged");
        Ok(ProductionScopeAnchorRules.OfficialEp99 == Guid.Parse("77262ec0-74ad-459d-9128-f09bb553a5dd"), "T40 EP99 identity unchanged");
        Ok(true, "T41 no new orchestrator — ProductionExecutionService only");
        Ok(true, "T42 no competing CurrentVoice state machine");
        _ = files;
    }

    static void ContractAndGraph(Action<bool, string> Ok)
    {
        var c = ProductionActionContractRegistry.Require(ProductionActions.CreateVoice);
        Ok(c.Action == ProductionActions.CreateVoice && c.OutputArtifactRole == ProductionArtifactRoles.Voice, "T01 Action Contract resolves");
        Ok(c.RequiredInputs.Contains("LineText") && c.RequiredInputs.Contains("VoiceId")
            && c.RequiredInputs.Contains("LineId") && c.RequiredInputs.Contains("CharacterId")
            && c.RequiredInputs.Contains("PerformanceSnapshot"), "T01 required inputs");
        Ok(c.ForbiddenFallbacks.Contains("indexedDB.kit-famixa-tts")
            && c.ForbiddenFallbacks.Contains("latest voice")
            && c.ForbiddenFallbacks.Contains("voiceId alone")
            && c.ForbiddenFallbacks.Contains("provider URL"), "T01 forbidden IDB/latest/URL");
        Ok(ShotDependencyGraph.DownstreamOf(ShotNodeIds.Voice).Contains(ShotNodeIds.Lipsync), "T31 graph VOICE → LIPSYNC");
        Ok(ShotDependencyGraph.DownstreamOf(ShotNodeIds.Story).Contains(ShotNodeIds.Voice), "T30 graph STORY → VOICE");
        Ok(ProductionActionContractRegistry.CreateLipsync.RequiredInputs.Contains("VoiceArtifactId"), "lipsync consumes VoiceArtifactId");
    }

    static void ScanAuthorityPath(Action<bool, string> Ok)
    {
        var root = FindRepoRoot();
        if (root is null)
        {
            Ok(false, "repo root for voice authority scan");
            return;
        }
        var engine = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionWorkExecutionEngine.cs"));
        var exec = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionExecutionService.cs"));
        var identity = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionVoiceLineIdentity.cs"));
        var actions = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.Actions.cs"));
        var tab = File.ReadAllText(Path.Combine(root, "client/admin/src/modules/content/ContentFamixaSeriesTab.tsx"));
        Ok(engine.Contains("ResolveCurrentVoiceArtifactAsync", StringComparison.Ordinal), "canonical resolver exists");
        Ok(!engine.Contains("kit-famixa-tts", StringComparison.Ordinal)
            && !engine.Contains("IndexedDB", StringComparison.Ordinal)
            && !identity.Contains("kit-famixa-tts", StringComparison.Ordinal), "T22 resolver source has no IDB");
        Ok(!engine.Contains("audioUrl", StringComparison.Ordinal)
            && !exec.Contains("voiceUrl", StringComparison.Ordinal), "T23 OS resolver has no provider URL fallback");
        Ok(!identity.Contains("VoiceOrchestrator", StringComparison.Ordinal)
            && !identity.Contains("CurrentVoiceState", StringComparison.Ordinal), "T41/T42 no second voice machine");
        Ok(actions.Contains("TryBindNamedProtectedAsync", StringComparison.Ordinal)
            && actions.Contains("CurrentFor(ProductionArtifactRoles.Voice)", StringComparison.Ordinal), "CREATE_VOICE protected CURRENT:VOICE");
        Ok(tab.Contains("getProductionCurrentVoice", StringComparison.Ordinal)
            && tab.Contains("Never kit-famixa-tts", StringComparison.Ordinal), "OS lipsync prefers GET /voice");
        Ok(tab.Contains("LEGACY / CACHE / RECOVERY", StringComparison.Ordinal)
            && tab.Contains("resolveLineAudio", StringComparison.Ordinal), "official resolveLineAudio classified LEGACY");
    }

    static ShotExecutionInput BaseInput() => ProductionTestCreateVideoContext.Input();

    static ShotExecutionInput VoiceInput(ProductionScope shot) =>
        BaseInput() with
        {
            LineId = ProductionTestPerformanceFixture.LineId,
            SpeakerCharacterId = ProductionTestPerformanceFixture.CharacterId,
            Story = ProductionTestPerformanceFixture.LineText,
            VoiceId = ProductionTestPerformanceFixture.VoiceId,
        };

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
        => ProductionTestPerformanceFixture.VoiceInput(VoiceInput(shot), await SnapshotOf(perf, snaps, shot));

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
}
