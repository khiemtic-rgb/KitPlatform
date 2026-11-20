using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionOfficialVideoAuthorityBridgeV1Regression
{
    public const string SuiteId = OfficialVideoAuthorityBridgeV1.SuiteId;
    static readonly Guid KeyframeId = Guid.Parse("91daaf4b-5550-4cbc-b9f9-0bd0be556cef");

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
        await T_AcceptedBeatsKeyframeCurrent(Ok);
        await T_MissingAcceptedFailClosed(Ok);
        await T_VisibleVideoTakeStillWinsWithoutAccepted(Ok);
        await T_CurrentAssemblyBeatsVideoTake(Ok);
        await T_CurrentMixBeatsAcceptedVideoTake(Ok);
        await T_MismatchedMixBindingFailClosed(Ok);
        T_NoSecondStore(Ok);
        T_Ck20StillGreen(Ok);
    }

    static async Task T_AcceptedBeatsKeyframeCurrent(Action<bool, string> Ok)
    {
        var ready = await PlantOfficial(bindAccepted: true, bindKeyframeCurrent: true);
        var beforeArts = (await ready.Store.ListArtifactsByTaskAsync(ready.Task.TaskId)).Count;
        var state = await ready.Svc.GetShotStateAsync(RealVertexExecutionV1Lock.OfficialScope);
        var preview = await ready.Svc.GetPreviewAsync(RealVertexExecutionV1Lock.OfficialScope);
        var take = await ready.Svc.GetAuthoritativeVideoTakeAsync(RealVertexExecutionV1Lock.OfficialScope);
        var motion = state.Nodes.First(n => n.Node == ShotNodeIds.Motion);
        Ok(take is not null && take.ArtifactId == ready.Take.ArtifactId
            && take.ArtifactRole == ProductionArtifactRoles.VideoTake
            && take.AcceptanceStatus == OfficialVeoVideoTakeAcceptanceV1.AcceptedStatus, "A/HTTP ACCEPTED:VIDEO_TAKE");
        Ok(state.VideoArtifactId == ready.Take.ArtifactId, "B MOTION/VideoArtifactId = accepted take");
        Ok(state.AcceptedArtifactId == ready.Take.ArtifactId, "B AcceptedArtifactId = ACCEPTED:VIDEO_TAKE");
        Ok(motion.ArtifactId == ready.Take.ArtifactId && motion.Status == ShotNodeStatuses.Accepted, "B MOTION accepted");
        Ok(preview.ArtifactId == ready.Take.ArtifactId
            && preview.Role == OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole, "C preview = ACCEPTED:VIDEO_TAKE");
        Ok(state.CurrentArtifactId == KeyframeId, "A bare CURRENT may stay KEYFRAME");
        Ok(preview.ArtifactId != KeyframeId && motion.ArtifactId != KeyframeId, "A KEYFRAME is not video");
        Ok((await ready.Store.ListArtifactsByTaskAsync(ready.Task.TaskId)).Count == beforeArts, "E no new artifact");
        Ok(ready.Transport.StartCount == 0 && ready.Transport.PollCount == 0, "F/G no Vertex");
    }

    static async Task T_MissingAcceptedFailClosed(Action<bool, string> Ok)
    {
        var ready = await PlantOfficial(bindAccepted: false, bindKeyframeCurrent: true);
        var preview = await ready.Svc.GetPreviewAsync(RealVertexExecutionV1Lock.OfficialScope);
        var state = await ready.Svc.GetShotStateAsync(RealVertexExecutionV1Lock.OfficialScope);
        var take = await ready.Svc.GetAuthoritativeVideoTakeAsync(RealVertexExecutionV1Lock.OfficialScope);
        var motion = state.Nodes.First(n => n.Node == ShotNodeIds.Motion);
        Ok(take is null, "D no ACCEPTED:VIDEO_TAKE lookup");
        Ok(preview.IsEmpty && preview.ArtifactId != KeyframeId, "D preview fail-closed, not KEYFRAME");
        Ok(motion.ArtifactId is null && state.VideoArtifactId is null, "D MOTION not KEYFRAME");
    }

    static async Task T_VisibleVideoTakeStillWinsWithoutAccepted(Action<bool, string> Ok)
    {
        var ready = await PlantOfficial(bindAccepted: false, bindKeyframeCurrent: false, bindVisibleTake: true);
        var preview = await ready.Svc.GetPreviewAsync(RealVertexExecutionV1Lock.OfficialScope);
        Ok(preview.ArtifactId == ready.Take.ArtifactId
            && preview.Role == ProductionBindingRoles.Visible, "VISIBLE VIDEO_TAKE still preview without accepted");
        Ok((await ready.Svc.GetShotStateAsync(RealVertexExecutionV1Lock.OfficialScope)).VideoArtifactId == ready.Take.ArtifactId,
            "VISIBLE VIDEO_TAKE is MOTION");
    }

    static async Task T_CurrentAssemblyBeatsVideoTake(Action<bool, string> Ok)
    {
        var ready = await PlantOfficial(bindAccepted: false, bindKeyframeCurrent: false, bindVisibleTake: true);
        var engine = new ProductionWorkExecutionEngine(ready.Store);
        var req = await engine.CreateRequestAsync(
            RealVertexExecutionV1Lock.OfficialScope, ProductionActions.Assemble, "ck21a", "asm-21a-" + Guid.NewGuid().ToString("N"));
        var task = await engine.CreateTaskAsync(req.RequestId);
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-ASM-21A-" + new string('A', 48));
        var sha = KitVideoIntegrityRules.Sha256Hex(bytes);
        var storage = ready.Files.Write("os-asm-21a", task.TaskId.ToString("N"), bytes);
        var art = new ProductionArtifact(
            Guid.NewGuid(), task.TaskId, Guid.NewGuid(), ProductionArtifactRoles.Assembly,
            "video/mp4", bytes.Length, 8, sha, storage, null,
            DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified, sha);
        await ready.Store.InsertArtifactAsync(art);
        await ready.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), RealVertexExecutionV1Lock.BuildId, RealVertexExecutionV1Lock.ShotCode,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly), art.ArtifactId, task.TaskId,
            DateTimeOffset.UtcNow, null));
        var preview = await ready.Svc.GetPreviewAsync(RealVertexExecutionV1Lock.OfficialScope);
        var take = await ready.Svc.GetAuthoritativeVideoTakeAsync(RealVertexExecutionV1Lock.OfficialScope);
        Ok(!preview.IsEmpty && preview.ArtifactId == art.ArtifactId
            && preview.Role == ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly),
            "CURRENT:ASSEMBLY preview beats CURRENT:VIDEO_TAKE");
        Ok(take is null || take.ArtifactId == ready.Take.ArtifactId, "video-take authority stays the take");
    }

    static async Task T_CurrentMixBeatsAcceptedVideoTake(Action<bool, string> Ok)
    {
        var ready = await PlantOfficial(bindAccepted: true, bindKeyframeCurrent: false);
        var engine = new ProductionWorkExecutionEngine(ready.Store);
        var req = await engine.CreateRequestAsync(
            RealVertexExecutionV1Lock.OfficialScope, ProductionActions.MixVideo, "ck21a", "mix-21a-" + Guid.NewGuid().ToString("N"));
        var task = await engine.CreateTaskAsync(req.RequestId);
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-MIX-21A-" + new string('M', 48));
        var sha = KitVideoIntegrityRules.Sha256Hex(bytes);
        var storage = ready.Files.Write("os-mix-21a", task.TaskId.ToString("N"), bytes);
        var mix = new ProductionArtifact(
            Guid.NewGuid(), task.TaskId, Guid.NewGuid(), ProductionArtifactRoles.Mix,
            "video/mp4", bytes.Length, 8, sha, storage, null,
            DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified, sha);
        await ready.Store.InsertArtifactAsync(mix);
        await ready.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), RealVertexExecutionV1Lock.BuildId, RealVertexExecutionV1Lock.ShotCode,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix), mix.ArtifactId, task.TaskId,
            DateTimeOffset.UtcNow, null));
        var preview = await ready.Svc.GetPreviewAsync(RealVertexExecutionV1Lock.OfficialScope);
        var media = await ready.Svc.ReadPreviewMediaAsync(RealVertexExecutionV1Lock.OfficialScope);
        var take = await ready.Svc.GetAuthoritativeVideoTakeAsync(RealVertexExecutionV1Lock.OfficialScope);
        Ok(preview.ArtifactId == mix.ArtifactId
            && preview.ByteSha256 == sha
            && preview.Role == ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix),
            "CURRENT:MIX playback beats ACCEPTED:VIDEO_TAKE");
        Ok(media is not null && KitVideoIntegrityRules.Sha256Hex(media) == sha, "playback bytes match the mix hash");
        Ok(take is not null && take.ArtifactId == ready.Take.ArtifactId && take.ArtifactRole == ProductionArtifactRoles.VideoTake,
            "authoritative video take stays the take");
    }

    static async Task T_MismatchedMixBindingFailClosed(Action<bool, string> Ok)
    {
        var ready = await PlantOfficial(bindAccepted: true, bindKeyframeCurrent: false);
        await ready.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), RealVertexExecutionV1Lock.BuildId, RealVertexExecutionV1Lock.ShotCode,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix), ready.Take.ArtifactId, ready.Task.TaskId,
            DateTimeOffset.UtcNow, null));
        var preview = await ready.Svc.GetPreviewAsync(RealVertexExecutionV1Lock.OfficialScope);
        Ok(preview.IsEmpty, "CURRENT:MIX pointing at a video take does not play the take");
    }

    static void T_NoSecondStore(Action<bool, string> Ok)
    {
        var root = FindRepoRoot();
        if (root is null)
        {
            Ok(false, "repo root");
            return;
        }
        var bridge = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/OfficialVideoAuthorityBridgeV1.cs"));
        var exec = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionExecutionService.cs"));
        var engine = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionWorkExecutionEngine.cs"));
        Ok(bridge.Contains("OfficialVeoVideoTakeAcceptanceV1.GetAuthoritativeVideoTakeAsync", StringComparison.Ordinal), "reuses CK20 lookup");
        Ok(!bridge.Contains("CREATE TABLE", StringComparison.Ordinal) && !bridge.Contains("AuthoritativeVideoTakeStore", StringComparison.Ordinal), "no second store");
        Ok(!exec.Contains("video_count", StringComparison.Ordinal) && !engine.Contains("video_count", StringComparison.Ordinal), "B no graph video_count");
        Ok(!exec.Contains("previewUrl", StringComparison.Ordinal) && !engine.Contains("takeUrl", StringComparison.Ordinal), "C no legacy URL");
    }

    static void T_Ck20StillGreen(Action<bool, string> Ok)
    {
        var ck20 = ProductionOfficialVeoVideoTakeAcceptanceV1Regression.Run();
        Ok(ck20.Count == 0, ck20.Count == 0 ? "CK20 still green" : "CK20:" + string.Join(",", ck20));
    }

    static async Task<ReadyState> PlantOfficial(bool bindAccepted, bool bindKeyframeCurrent, bool bindVisibleTake = false)
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var transport = new OfficialVeoAcceptanceClosedTransport();
        var engine = new ProductionWorkExecutionEngine(store);
        var kfReq = await engine.CreateRequestAsync(
            RealVertexExecutionV1Lock.OfficialScope, ProductionActions.CreateImage, "ck21a", "kf-21a-" + Guid.NewGuid().ToString("N"));
        var kfTask = await engine.CreateTaskAsync(kfReq.RequestId);
        var kfBytes = Encoding.UTF8.GetBytes("FAMIXA-KF-21A-" + new string('K', 48));
        var kfHash = KitVideoIntegrityRules.Sha256Hex(kfBytes);
        await store.InsertArtifactAsync(new ProductionArtifact(
            KeyframeId, kfTask.TaskId, Guid.NewGuid(), ProductionArtifactRoles.Keyframe,
            "image/png", kfBytes.Length, 0, kfHash, "kf/EP01-SC01-SH02.png", null,
            DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified, kfHash));
        if (bindKeyframeCurrent)
        {
            await store.InsertBindingAsync(new ProductionBinding(
                Guid.NewGuid(), RealVertexExecutionV1Lock.BuildId, RealVertexExecutionV1Lock.ShotCode,
                ProductionBindingRoles.Current, KeyframeId, kfTask.TaskId, DateTimeOffset.UtcNow, null));
            await store.InsertBindingAsync(new ProductionBinding(
                Guid.NewGuid(), RealVertexExecutionV1Lock.BuildId, RealVertexExecutionV1Lock.ShotCode,
                ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Keyframe), KeyframeId, kfTask.TaskId,
                DateTimeOffset.UtcNow, null));
        }

        var req = await engine.CreateRequestAsync(
            RealVertexExecutionV1Lock.OfficialScope, ProductionActions.CreateVideo, "ck21a", "vid-21a-" + Guid.NewGuid().ToString("N"));
        var task = await engine.CreateTaskAsync(req.RequestId);
        var snap = new ProductionTaskInputSnapshot(
            Guid.NewGuid(), task.TaskId,
            "SH02 official story", "acting", "visual", "motion",
            "compiled", "compiled",
            "s", "a", "v", "c", kfHash, "vh", "mh", "", "th",
            "google", "veo-3.1", "snap-hash-ck21a", DateTimeOffset.UtcNow,
            KeyframeId, "16:9", 8, ShotExecutionCompiler.Version, [KeyframeId]);
        snap = snap with { SnapshotHash = ShotExecutionCompiler.SnapshotHash(snap) };
        await store.InsertSnapshotAsync(snap);
        await store.UpdateTaskAsync(task with
        {
            InputSnapshotId = snap.SnapshotId,
            ProviderId = "google",
            ModelId = "veo-3.1",
            Status = ProductionTaskStatuses.ProviderFrozen,
        });
        var attempt = await engine.StartAttemptAsync(task.TaskId, kfHash);
        var mp4 = ProductionTestMp4.Encode("ck21a-out", 8);
        var sha = KitVideoIntegrityRules.Sha256Hex(mp4);
        var storage = files.Write("os-veo-out", attempt.AttemptId.ToString("N"), mp4);
        var art = new ProductionArtifact(
            Guid.NewGuid(), task.TaskId, attempt.AttemptId, ProductionArtifactRoles.VideoTake,
            ProductionTestMp4.Mime, mp4.Length, 8, sha, storage, attempt.ProviderJobId,
            DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified,
            (await store.GetSnapshotByTaskAsync(task.TaskId))!.SnapshotHash, [KeyframeId]);
        await store.InsertArtifactAsync(art);
        await store.UpdateTaskAsync((await store.GetTaskAsync(task.TaskId))! with
        {
            Status = ProductionTaskStatuses.Succeeded,
            CompletedAt = DateTimeOffset.UtcNow,
        });
        if (bindAccepted)
        {
            await store.InsertBindingAsync(new ProductionBinding(
                Guid.NewGuid(), RealVertexExecutionV1Lock.BuildId, RealVertexExecutionV1Lock.ShotCode,
                OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole, art.ArtifactId, task.TaskId,
                DateTimeOffset.UtcNow, null));
        }
        if (bindVisibleTake)
        {
            await store.InsertBindingAsync(new ProductionBinding(
                Guid.NewGuid(), RealVertexExecutionV1Lock.BuildId, RealVertexExecutionV1Lock.ShotCode,
                ProductionBindingRoles.Visible, art.ArtifactId, task.TaskId, DateTimeOffset.UtcNow, null));
            await store.InsertBindingAsync(new ProductionBinding(
                Guid.NewGuid(), RealVertexExecutionV1Lock.BuildId, RealVertexExecutionV1Lock.ShotCode,
                ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake), art.ArtifactId, task.TaskId,
                DateTimeOffset.UtcNow, null));
        }

        var os = new ProductionExecutionService(store, new FixtureMotionAdapter());
        var svc = new ProductionCreateVideoService(os, store, files, new FixtureMotionAdapter());
        return new ReadyState(store, files, transport, os, svc, task, art);
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

    sealed record ReadyState(
        InMemoryProductionWorkStore Store,
        InMemoryProductionArtifactFileStore Files,
        OfficialVeoAcceptanceClosedTransport Transport,
        ProductionExecutionService Os,
        ProductionCreateVideoService Svc,
        ProductionTask Task,
        ProductionArtifact Take);
}
