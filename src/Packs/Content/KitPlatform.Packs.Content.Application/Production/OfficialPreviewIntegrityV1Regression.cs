using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class OfficialPreviewIntegrityV1Regression
{
    public const string SuiteId = OfficialPreviewIntegrityV1.SuiteId;

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
        ScanOfficialPreviewPath(Ok);

        var files = new OverlayProductionArtifactFileStore(new InMemoryProductionArtifactFileStore());
        var store = new InMemoryProductionWorkStore();
        var perf = new InMemoryPerformanceSnapshotStore();
        var os = new ProductionExecutionService(store, new FixtureMotionAdapter(), performance: perf);
        var svc = new ProductionCreateVideoService(os, store, files, new FixtureMotionAdapter());
        var build = Guid.Parse("aaaaaaaa-0000-4000-8000-00000000001b");
        var shot = new ProductionScope(build, "TEST", "SC01", "TEST-SC01-SH01");
        var line = ProductionTestPerformanceFixture.LineId;

        Ok((await svc.GetPreviewAsync(shot)).IsEmpty, "T4 empty OS preview before artifact");
        Ok(await svc.ReadPreviewMediaAsync(shot) is null, "T4 no media without OS artifact");

        var perfSnap = await ProductionTestPerformanceFixture.FreezeAsync(perf, build, shot.ShotCode);
        var created = await svc.CreateVideoAsync(new CreateVideoApiCommand(
            shot, ProductionTestPerformanceFixture.Attach(Input("TEST STORY — child walks into a room."), perfSnap), "test", "01b-video",
            Encoding.UTF8.GetBytes("KF-01B-" + new string('P', 48)), "image/png"));
        var preview = await svc.GetPreviewAsync(shot);
        var media = await svc.ReadPreviewMediaAsync(shot);
        Ok(!preview.IsEmpty && preview.ArtifactId == created.Artifact.ArtifactId, "T1/T12 official preview resolves artifact");
        Ok(media is { Length: >= 32 }
            && KitVideoIntegrityRules.Sha256Hex(media) == created.Artifact.ByteSha256
            && preview.ByteSha256 == created.Artifact.ByteSha256, "T1 matching SHA streams");
        Ok(created.Artifact.ArtifactId.ToString("N") != created.Artifact.StorageKey
            && created.Artifact.StorageKey != created.Artifact.ByteSha256
            && created.Artifact.ArtifactId.ToString("N") != created.Artifact.ByteSha256,
            "ArtifactId != StorageKey != ByteSha256");

        var snapBefore = await store.GetSnapshotByTaskAsync(created.Task.TaskId);
        var visBefore = await store.GetActiveBindingAsync(build, shot.ShotCode, ProductionBindingRoles.Visible);
        var curBefore = await store.GetActiveBindingAsync(build, shot.ShotCode, ProductionBindingRoles.Current);
        var accBefore = await store.GetActiveBindingAsync(build, shot.ShotCode, OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole);
        var shaBefore = created.Artifact.ByteSha256;
        var artsBefore = (await store.ListArtifactsByTaskAsync(created.Task.TaskId)).Count;

        files.Replace[created.Artifact.StorageKey] = ProductionTestMp4.Encode("01b-wrong-sha", 5);
        var mismatchPreview = await svc.GetPreviewAsync(shot);
        var mismatchMedia = await svc.ReadPreviewMediaAsync(shot);
        Ok(mismatchPreview.IsEmpty && mismatchPreview.Reason == ProductionFailureReasons.FailedVerification, "T2 SHA mismatch preview fail-closed");
        Ok(mismatchMedia is null, "T2 SHA mismatch does not stream");

        var artAfterMismatch = await store.GetArtifactAsync(created.Artifact.ArtifactId);
        Ok(artAfterMismatch is not null && artAfterMismatch.ByteSha256 == shaBefore, "T9 mismatch does not mutate ByteSha256");
        Ok((await store.GetActiveBindingAsync(build, shot.ShotCode, ProductionBindingRoles.Visible))?.BindingId == visBefore?.BindingId
            && (await store.GetActiveBindingAsync(build, shot.ShotCode, ProductionBindingRoles.Current))?.BindingId == curBefore?.BindingId
            && (await store.GetActiveBindingAsync(build, shot.ShotCode, OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole))?.BindingId == accBefore?.BindingId,
            "T10 mismatch does not create a binding");
        var snapAfterMismatch = await store.GetSnapshotByTaskAsync(created.Task.TaskId);
        Ok(snapAfterMismatch is not null
            && snapBefore is not null
            && snapAfterMismatch.SnapshotId == snapBefore.SnapshotId
            && snapAfterMismatch.SnapshotHash == snapBefore.SnapshotHash, "T11 mismatch does not refreeze");
        Ok((await store.ListArtifactsByTaskAsync(created.Task.TaskId)).Count == artsBefore, "T9/T10 no new artifact");

        files.Replace.Remove(created.Artifact.StorageKey);
        files.Hide.Add(created.Artifact.StorageKey);
        var missingPreview = await svc.GetPreviewAsync(shot);
        var missingMedia = await svc.ReadPreviewMediaAsync(shot);
        Ok(missingPreview.IsEmpty && missingMedia is null, "T3 missing physical bytes fail-closed");
        Ok((await store.GetArtifactAsync(created.Artifact.ArtifactId))!.ByteSha256 == shaBefore, "T3 missing bytes do not rewrite SHA");

        files.Hide.Remove(created.Artifact.StorageKey);
        var restored = await svc.ReadPreviewMediaAsync(shot);
        var restoredPreview = await svc.GetPreviewAsync(shot);
        Ok(restored is { Length: >= 32 }
            && KitVideoIntegrityRules.Sha256Hex(restored) == shaBefore
            && restoredPreview.ArtifactId == created.Artifact.ArtifactId, "T12 valid OS preview remains intact");

        var voice = await svc.CreateVoiceAsync(new ProductionActionApiCommand(
            shot, ProductionTestPerformanceFixture.VoiceInput(Input("Thì sao?"), perfSnap), "test", "01b-voice"));
        var voiceBytes = await svc.ReadCurrentVoiceMediaAsync(shot, line);
        Ok(voiceBytes is { Length: >= 32 }
            && KitVideoIntegrityRules.Sha256Hex(voiceBytes) == voice.Artifact.ByteSha256, "T7 voice SHA matches");
        var voiceSha = voice.Artifact.ByteSha256;
        var voiceBind = await store.GetActiveBindingAsync(build, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice));
        var voiceSnapBefore = await store.GetSnapshotByTaskAsync(voice.Task.TaskId);

        files.Replace[voice.Artifact.StorageKey] = ProductionTestWav.Encode("01b-wrong-voice", 1);
        Ok(await svc.ReadCurrentVoiceMediaAsync(shot, line) is null, "T8 voice SHA mismatch fail-closed");
        Ok((await store.GetArtifactAsync(voice.Artifact.ArtifactId))!.ByteSha256 == voiceSha, "T8/T9 voice mismatch does not mutate ByteSha256");
        Ok((await store.GetActiveBindingAsync(build, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice)))?.BindingId
            == voiceBind?.BindingId, "T10 voice mismatch does not rebind");
        var voiceSnapAfter = await store.GetSnapshotByTaskAsync(voice.Task.TaskId);
        Ok(voiceSnapBefore is not null && voiceSnapAfter is not null
            && voiceSnapAfter.SnapshotHash == voiceSnapBefore.SnapshotHash, "T11 voice mismatch does not refreeze");
    }

    static void ScanOfficialPreviewPath(Action<bool, string> Ok)
    {
        var root = FindRepoRoot();
        if (root is null)
        {
            Ok(false, "repo root for official preview scan");
            return;
        }

        string Read(string rel) => File.ReadAllText(Path.Combine(root, rel));
        var integrity = Read("src/Packs/Content/KitPlatform.Packs.Content.Application/Production/OfficialPreviewIntegrityV1.cs");
        var create = Read("src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.cs");
        var controller = Read("src/KitPlatform.Api/Controllers/Content/ContentProductionController.cs");
        var workspace = Read("client/admin/src/modules/content/ContentFamixaShotProduction/ShotProductionWorkspace.tsx");
        var series = Read("client/admin/src/modules/content/ContentFamixaSeriesTab.tsx");

        Ok(create.Contains("OfficialPreviewIntegrityV1.ReadVerifiedPhysicalBytes", StringComparison.Ordinal),
            "T1/T7 SHA gate on official read");
        Ok(create.Contains("ReadPreviewMediaAsync", StringComparison.Ordinal)
            && create.Contains("ReadCurrentVoiceMediaAsync", StringComparison.Ordinal), "video+voice share integrity helper");
        Ok(!integrity.Contains("takeUrl", StringComparison.Ordinal)
            && !integrity.Contains("previewUrl", StringComparison.Ordinal)
            && !integrity.Contains("lipsyncUrl", StringComparison.Ordinal)
            && !integrity.Contains("acceptedTake", StringComparison.Ordinal),
            "NEGATIVE graph URL != official preview authority");
        Ok(!integrity.Contains("indexedDB", StringComparison.OrdinalIgnoreCase)
            && !integrity.Contains("kit-famixa", StringComparison.OrdinalIgnoreCase)
            && !integrity.Contains("FinalStore", StringComparison.Ordinal),
            "NEGATIVE IDB/FinalStore != official preview authority");
        Ok(!integrity.Contains("BindCurrent", StringComparison.Ordinal)
            && !integrity.Contains("BindVisible", StringComparison.Ordinal)
            && !integrity.Contains("FreezeInput", StringComparison.Ordinal)
            && !integrity.Contains("ByteSha256 =", StringComparison.Ordinal),
            "NEGATIVE mismatch cannot repair/rebind/refreeze");
        Ok(!create.Contains("assembleVideoUrl", StringComparison.Ordinal)
            && !controller.Contains("takeUrl", StringComparison.Ordinal)
            && !controller.Contains("acceptedTake", StringComparison.Ordinal),
            "T4/T6 official API does not read graph media");
        Ok(workspace.Contains("looksLikeProductionShotCode(current.id) || osAcceptedVideo", StringComparison.Ordinal),
            "T4 official production shots are OS-only");
        Ok(workspace.Contains("osPreviewAuthority")
            && workspace.Contains("motionUrl: undefined"),
            "T4/T5/T6 official empty does not use graph/IDB/FinalStore");
        Ok(workspace.Contains("osPreviewAuthority")
            && workspace.Contains("(osVoiceUrl || '').trim() || undefined"),
            "T7 official voice player is OS-only");
        Ok(!workspace.Contains("kit-famixa-tts", StringComparison.Ordinal), "NEGATIVE IDB blob != official preview authority");
        Ok(series.Contains("getProductionPreview", StringComparison.Ordinal)
            && series.Contains("fetchProductionPreviewMedia", StringComparison.Ordinal)
            && series.Contains("delete next[shotId]", StringComparison.Ordinal),
            "hydrate fail-closed clears official preview");
        Ok(!series.Contains("acceptedTake.url", StringComparison.Ordinal)
            || series.Contains("osPreviewAuthority", StringComparison.Ordinal)
            || series.Contains("fetchProductionPreviewMedia", StringComparison.Ordinal),
            "NEGATIVE acceptedTake != official preview authority");
        Ok(!integrity.Contains("video-generation", StringComparison.Ordinal)
            && !integrity.Contains("Vge", StringComparison.Ordinal)
            && !create.Contains("executeVideoGenerationExecution", StringComparison.Ordinal),
            "NEGATIVE VGE URL != official preview authority");
    }

    static ShotExecutionInput Input(string story) =>
        new(story, "He stays in place.", "visual", "CHAR-001", "", "5s", "", "motion",
            null, "", "16:9", 5, ShotExecutionCompiler.Version, null);

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

    sealed class OverlayProductionArtifactFileStore : IProductionArtifactFileStore
    {
        readonly IProductionArtifactFileStore _inner;
        public OverlayProductionArtifactFileStore(IProductionArtifactFileStore inner) => _inner = inner;
        public HashSet<string> Hide { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, byte[]> Replace { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string Write(string folder, string fileId, byte[] bytes) => _inner.Write(folder, fileId, bytes);
        public byte[]? Read(string storageKey)
        {
            if (Hide.Contains(storageKey)) return null;
            if (Replace.TryGetValue(storageKey, out var bytes)) return bytes;
            return _inner.Read(storageKey);
        }
    }
}
