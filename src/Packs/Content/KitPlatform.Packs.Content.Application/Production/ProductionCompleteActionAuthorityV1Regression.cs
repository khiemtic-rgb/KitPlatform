using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionCompleteActionAuthorityV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_COMPLETE_ACTION_AUTHORITY_V1";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }
        try { RunAsync(Ok).GetAwaiter().GetResult(); }
        catch (Exception ex) { fail.Add("UNCAUGHT:" + ex.GetType().Name + ":" + ex.Message + " @ " + ex.StackTrace); }
        return fail;
    }

    static async Task RunAsync(Action<bool, string> Ok)
    {
        Ok(ProductionUiActionMap.RequireAction("Tạo hình") == ProductionActions.CreateImage, "CTA Tạo hình → CREATE_IMAGE");
        Ok(ProductionUiActionMap.RequireAction("Tạo video") == ProductionActions.CreateVideo, "CTA Tạo video → CREATE_VIDEO");
        Ok(ProductionUiActionMap.RequireAction("Nạp thoại") == ProductionActions.CreateVoice, "CTA Nạp thoại → CREATE_VOICE");
        Ok(ProductionUiActionMap.RequireAction("Khớp môi") == ProductionActions.CreateLipsync, "CTA Khớp môi → CREATE_LIPSYNC");
        Ok(ProductionUiActionMap.RequireAction("Hoàn thiện") == ProductionActions.MixVideo, "CTA Hoàn thiện → MIX_VIDEO");
        Ok(ProductionUiActionMap.RequireAction("Dùng kết quả này") == ProductionActions.Accept, "CTA Dùng kết quả này → ACCEPT");
        Ok(ProductionUiActionMap.RequireAction("Lắp tập") == ProductionActions.Assemble, "CTA Lắp tập → ASSEMBLE");
        Ok(ShotDependencyGraph.DownstreamOf(ShotNodeIds.Story).Contains(ShotNodeIds.Voice), "graph STORY → VOICE");
        Ok(ShotDependencyGraph.DownstreamOf(ShotNodeIds.Voice).Contains(ShotNodeIds.Lipsync), "graph VOICE → LIPSYNC");
        Ok(ShotDependencyGraph.DownstreamOf(ShotNodeIds.Motion).Contains(ShotNodeIds.Lipsync), "graph VIDEO_TAKE → LIPSYNC");
        Ok(ShotDependencyGraph.DownstreamOf(ShotNodeIds.Lipsync).Contains(ShotNodeIds.Mix), "graph LIPSYNC → MIX");
        Ok(ShotDependencyGraph.DownstreamOf(ShotNodeIds.Mix).Contains(ShotNodeIds.Accepted), "graph MIX → ACCEPTED");
        Ok(ShotDependencyGraph.DownstreamOf(ShotNodeIds.Accepted).Contains(ShotNodeIds.Assembly), "graph ACCEPTED → ASSEMBLY");
        Ok(new FixtureImageAdapter() is { IsPaid: false, RequiresNetwork: false }, "FixtureImage unpaid");
        Ok(new FixtureVoiceAdapter() is { IsPaid: false, RequiresNetwork: false }, "FixtureVoice unpaid");
        Ok(new FixtureLipSyncAdapter() is { IsPaid: false, RequiresNetwork: false }, "FixtureLipSync unpaid");
        Ok(new FixtureMixAdapter() is { IsPaid: false, RequiresNetwork: false }, "FixtureMix unpaid");
        Ok(new FixtureAssemblyAdapter() is { IsPaid: false, RequiresNetwork: false }, "FixtureAssembly unpaid");
        Ok(new FixtureMotionAdapter() is { IsPaid: false, RequiresNetwork: false }, "FixtureMotion unpaid");

        ScanLegacyBypass(Ok);
        Ok(
            new FilmAssembly(
                Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001"),
                Guid.Parse("aaaaaaaa-0000-4000-8000-000000000002"),
                "TEST-ASSEMBLY",
                new DateTime(2026, 9, 18, 3, 0, 0, DateTimeKind.Utc)).CreatedAt.UtcDateTime.Year == 2026,
            "FilmAssembly DateTime ctor matches Dapper TIMESTAMPTZ");
        await HappyPathAsync(Ok);
        await MissingAndWrongAsync(Ok);
        await CrossShotAndScopeAsync(Ok);
        await IdempotencyAndInFlightAsync(Ok);
        await FailedVerificationRetryAsync(Ok);
        await MutationAsync(Ok);
    }

    static async Task HappyPathAsync(Action<bool, string> Ok)
    {
        var (svc, store, files, os, perf, snaps) = Harness();
        var build = Guid.Parse("cccccccc-0000-4000-8000-000000000372");
        var shot = new ProductionScope(build, "TEST", "SC01", "TEST-SC01-SH02");
        var chain = await RunChainAsync(svc, store, files, shot, "happy", perf, snaps);

        Ok(chain.Image.Request.UserAction == ProductionActions.CreateImage
            && chain.Image.Task.Action == ProductionActions.CreateImage
            && chain.Image.Artifact.Role == ProductionArtifactRoles.Keyframe
            && chain.Image.Artifact.VerificationStatus == ProductionArtifactStatuses.Verified, "IMAGE request/task/verified KEYFRAME");
        Ok(chain.Video.Request.UserAction == ProductionActions.CreateVideo
            && chain.Video.Artifact.Role == ProductionArtifactRoles.VideoTake
            && chain.Video.Artifact.ParentArtifactIds?.Contains(chain.Image.Artifact.ArtifactId) == true, "VIDEO_TAKE parents KEYFRAME");
        Ok(chain.Voice.Request.UserAction == ProductionActions.CreateVoice
            && chain.Voice.Artifact.Role == ProductionArtifactRoles.Voice
            && chain.Voice.Artifact.MimeType == ProductionTestWav.Mime
            && files.Read(chain.Voice.Artifact.StorageKey) is { Length: >= 32 } voiceBytes
            && ProductionTestWav.IsDecodable(voiceBytes, out _), "VOICE verified WAV");
        var voiceCurrent = await store.GetActiveBindingAsync(build, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice));
        var aliasCurrent = await store.GetActiveBindingAsync(build, shot.ShotCode, ProductionBindingRoles.Current);
        Ok(voiceCurrent?.ArtifactId == chain.Voice.Artifact.ArtifactId, "CURRENT:VOICE bound");
        Ok(aliasCurrent?.ArtifactId == chain.Video.Artifact.ArtifactId, "VOICE does not steal CURRENT VIDEO_TAKE");

        Ok(chain.Lipsync.Request.UserAction == ProductionActions.CreateLipsync
            && chain.Lipsync.Artifact.Role == ProductionArtifactRoles.Lipsync
            && chain.Lipsync.Artifact.ParentArtifactIds is { Count: 2 } parents
            && parents.Contains(chain.Video.Artifact.ArtifactId)
            && parents.Contains(chain.Voice.Artifact.ArtifactId), "LIPSYNC parents VIDEO_TAKE+VOICE");
        Ok(chain.Mix.Request.UserAction == ProductionActions.MixVideo
            && chain.Mix.Artifact.Role == ProductionArtifactRoles.Mix
            && chain.Mix.Artifact.ParentArtifactIds?.Contains(chain.Lipsync.Artifact.ArtifactId) == true, "MIX parents LIPSYNC");
        Ok(chain.Accept.Request.UserAction == ProductionActions.Accept
            && chain.Accept.Artifact.ArtifactId == chain.Mix.Artifact.ArtifactId, "ACCEPT names MIX artifactId");
        var accepted = await store.GetActiveBindingAsync(build, shot.ShotCode, ProductionBindingRoles.Accepted);
        Ok(accepted?.ArtifactId == chain.Mix.Artifact.ArtifactId, "ACCEPTED binding");

        Ok(chain.Assembly.Request.UserAction == ProductionActions.Assemble
            && chain.Assembly.AcceptedArtifactIds.Count == 1
            && chain.Assembly.AcceptedArtifactIds[0] == chain.Mix.Artifact.ArtifactId, "ASSEMBLY input is acceptedArtifactId");
        var shots = await store.ListAssemblyShotsAsync(chain.Assembly.Assembly.AssemblyId);
        Ok(shots.Count == 1 && shots[0].ArtifactId == chain.Mix.Artifact.ArtifactId
            && !shots[0].ShotCode.Contains("://", StringComparison.Ordinal), "ASSEMBLY shot row is artifactId not URL");

        var preview = await svc.GetPreviewAsync(shot);
        Ok(!preview.IsEmpty && preview.ArtifactId == chain.Mix.Artifact.ArtifactId, "preview is verified CURRENT:MIX");
        var media = await svc.ReadPreviewMediaAsync(shot);
        Ok(media is { Length: >= 32 } && KitVideoIntegrityRules.Sha256Hex(media) == chain.Mix.Artifact.ByteSha256, "preview bytes = current mix");
        var take = await svc.GetAuthoritativeVideoTakeAsync(shot);
        Ok(take?.ArtifactId == chain.Video.Artifact.ArtifactId, "authoritative video take stays the video");

        foreach (var snapTask in new[] { chain.Image.Task.TaskId, chain.Voice.Task.TaskId, chain.Lipsync.Task.TaskId, chain.Mix.Task.TaskId, chain.Accept.Task.TaskId })
        {
            var snap = await store.GetSnapshotByTaskAsync(snapTask);
            Ok(snap is not null && !string.IsNullOrWhiteSpace(snap.ProviderId) && !string.IsNullOrWhiteSpace(snap.ModelId)
                && !string.IsNullOrWhiteSpace(snap.SnapshotHash), "snapshot+provider frozen " + snapTask.ToString("N")[..8]);
        }

        Ok(chain.Image.Attempt.AttemptId != Guid.Empty
            && chain.Voice.Attempt.AttemptId != Guid.Empty
            && chain.Lipsync.Attempt.AttemptId != Guid.Empty
            && chain.Mix.Attempt.AttemptId != Guid.Empty
            && chain.Accept.Attempt.AttemptId != Guid.Empty
            && chain.Assembly.Attempt.AttemptId != Guid.Empty, "every action has an Attempt");

        var regen = await svc.RegenerateOsAsync(new ProductionActionApiCommand(
            shot, BaseInput() with
            {
                MotionArtifactId = chain.Video.Artifact.ArtifactId,
                ParentArtifactIds = [chain.Video.Artifact.ArtifactId],
            }, "test", "happy-regen"));
        Ok(regen.Request.UserAction == ProductionActions.Regenerate
            && regen.Task.Action == ProductionActions.Regenerate
            && regen.Task.TaskId != chain.Video.Task.TaskId
            && regen.Attempt.AttemptId != chain.Video.Attempt.AttemptId
            && regen.Artifact.ArtifactId != chain.Video.Artifact.ArtifactId
            && regen.Artifact.Role == ProductionArtifactRoles.VideoTake, "REGENERATE new request/task/attempt/artifact");
        Ok((await store.GetArtifactAsync(chain.Video.Artifact.ArtifactId))?.ByteSha256 == chain.Video.Artifact.ByteSha256,
            "REGENERATE leaves prior VIDEO_TAKE immutable");
        var regenReplay = await svc.RegenerateOsAsync(new ProductionActionApiCommand(
            shot, BaseInput() with
            {
                MotionArtifactId = chain.Video.Artifact.ArtifactId,
                ParentArtifactIds = [chain.Video.Artifact.ArtifactId],
            }, "test", "happy-regen"));
        Ok(regenReplay.Request.RequestId == regen.Request.RequestId
            && regenReplay.Task.TaskId == regen.Task.TaskId
            && regenReplay.Artifact.ArtifactId == regen.Artifact.ArtifactId, "REGENERATE same idempotency replays");
    }

    static async Task MissingAndWrongAsync(Action<bool, string> Ok)
    {
        var (svc, store, files, _, perf, snaps) = Harness();
        var build = Guid.Parse("dddddddd-0000-4000-8000-000000000372");
        var shot = new ProductionScope(build, "TEST", "SC01", "TEST-SC01-SH02");

        Ok(await ThrowsReason(async () => await svc.CreateVoiceAsync(VoiceCommand(shot, "", "v1")),
            ProductionFailureReasons.MissingDependency), "VOICE missing lineId");
        Ok(await ThrowsReason(async () => await svc.CreateLipsyncAsync(LipsyncCommand(shot, null, Guid.NewGuid())),
            ProductionFailureReasons.MissingVideoArtifact), "LIPSYNC missing motion");
        Ok(await ThrowsReason(async () => await svc.MixVideoAsync(MixCommand(shot, null)),
            ProductionFailureReasons.MissingLipsyncArtifact), "MIX missing lipsync");
        Ok(await ThrowsReason(async () => await svc.RegenerateOsAsync(new ProductionActionApiCommand(
            shot, BaseInput(), "test", "missing-regen")),
            ProductionFailureReasons.MissingDependency), "REGENERATE missing prior VIDEO_TAKE");
        Ok(await ThrowsReason(async () => await svc.AcceptOsAsync(AcceptCommand(shot, null)),
            ProductionFailureReasons.MissingDependency), "ACCEPT missing artifact");

        var ready = await RunPartialAsync(svc, store, files, shot, "wrong", perf, snaps);
        Ok(await ThrowsReason(async () => await svc.CreateLipsyncAsync(
                LipsyncCommand(shot, ready.Voice.Artifact.ArtifactId, ready.Voice.Artifact.ArtifactId)),
            ProductionFailureReasons.FailedIdentity), "wrong artifact role = VIDEO expected");
        Ok(await ThrowsReason(async () => ProductionIdentityRules.ForbidUrlIdentity("https://cdn.example/takeUrl.mp4"),
            ProductionFailureReasons.UrlAuthorityForbidden), "URL identity rejected");
        Ok(await ThrowsReason(async () => await svc.CreateVoiceAsync(new ProductionActionApiCommand(
            shot, VoiceInput(shot) with { LineId = "https://tts.example/line" }, "test", "url-line")),
            ProductionFailureReasons.UrlAuthorityForbidden), "VOICE URL lineId rejected");
    }

    static async Task CrossShotAndScopeAsync(Action<bool, string> Ok)
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var perf = new InMemoryPerformanceSnapshotStore();
        var snaps = new Dictionary<string, PerformanceSnapshot>();
        var lipsync = new CountingLipSyncAdapter();
        var os = new ProductionExecutionService(store, new FixtureMotionAdapter(), performance: perf);
        var svc = new ProductionCreateVideoService(os, store, files, new FixtureMotionAdapter(), lipsync: lipsync);
        var build = Guid.Parse("eeeeeeee-0000-4000-8000-000000000372");
        var sh01 = new ProductionScope(build, "TEST", "SC01", "TEST-SC01-SH01");
        var sh02 = new ProductionScope(build, "TEST", "SC01", "TEST-SC01-SH02");
        var a = await RunPartialAsync(svc, store, files, sh01, "sh01", perf, snaps);
        var b = await RunPartialAsync(svc, store, files, sh02, "sh02", perf, snaps);
        var before = lipsync.Calls;
        Ok(await ThrowsReason(async () => await svc.CreateLipsyncAsync(LipsyncCommand(sh02, a.Video.Artifact.ArtifactId, b.Voice.Artifact.ArtifactId)),
            ProductionFailureReasons.CrossShotForbidden), "CROSS_SHOT SH01 video → SH02 lipsync");
        Ok(lipsync.Calls == before, "CROSS_SHOT does not execute adapter");

        var filmBuild = Guid.Parse("4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f");
        var film = new ProductionScope(filmBuild, "TEST", "SC01", sh02.ShotCode);
        Ok(await ThrowsReason(async () => await svc.CreateLipsyncAsync(LipsyncCommand(film, b.Video.Artifact.ArtifactId, b.Voice.Artifact.ArtifactId)),
            ProductionFailureReasons.CrossScopeForbidden), "CROSS_SCOPE TEST artifact → FILM task");
        Ok(lipsync.Calls == before, "CROSS_SCOPE does not execute adapter");
    }

    static async Task IdempotencyAndInFlightAsync(Action<bool, string> Ok)
    {
        var (svc, store, files, os, perf, snaps) = Harness();
        var build = Guid.Parse("ffffffff-0000-4000-8000-000000000372");
        var shot = new ProductionScope(build, "TEST", "SC01", "TEST-SC01-SH02");
        var first = await svc.CreateVoiceAsync(new ProductionActionApiCommand(
            shot, await BoundVoice(perf, snaps, shot), "test", "voice-idem-1"));
        var replay = await svc.CreateVoiceAsync(new ProductionActionApiCommand(
            shot, await BoundVoice(perf, snaps, shot), "test", "voice-idem-1"));
        Ok(replay.Request.RequestId == first.Request.RequestId
            && replay.Artifact.ArtifactId == first.Artifact.ArtifactId, "duplicate idempotency returns same VOICE");

        Ok(await ThrowsReason(async () => await svc.CreateVoiceAsync(new ProductionActionApiCommand(
            shot, (await BoundVoice(perf, snaps, shot)) with { Story = "MUTATED STORY — different words here." }, "test", "voice-idem-1")),
            ProductionFailureReasons.PerformanceLineMismatch), "mutated spoken line fails closed");
        var snap = await store.GetSnapshotByTaskAsync(first.Task.TaskId);
        Ok(snap is not null && snap.Story == ProductionTestPerformanceFixture.LineText, "frozen voice line unchanged");

        var inflightReq = await os.CreateRequestAsync(shot, ProductionActions.CreateVoice, "test", "voice-inflight");
        var inflightTask = await os.CreateTaskAsync(inflightReq.RequestId);
        await os.FreezeInputAsync(inflightTask.TaskId, await BoundVoice(perf, snaps, shot), "fixture", "fixture-voice-v1");
        await os.StartAttemptAsync(inflightTask.TaskId, "open");
        Ok(await ThrowsReason(async () => await svc.CreateVoiceAsync(new ProductionActionApiCommand(
            shot, await BoundVoice(perf, snaps, shot), "test", "voice-inflight")),
            ProductionFailureReasons.InFlightReplay), "IN_FLIGHT returns existing / no second task");
        var tasks = await store.ListTasksByRequestAsync(inflightReq.RequestId);
        Ok(tasks.Count == 1, "IN_FLIGHT does not create another task");
        _ = files;
    }

    static async Task FailedVerificationRetryAsync(Action<bool, string> Ok)
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var perf = new InMemoryPerformanceSnapshotStore();
        var snaps = new Dictionary<string, PerformanceSnapshot>();
        var lipsync = new FailThenFixtureLipSyncAdapter();
        var os = new ProductionExecutionService(store, new FixtureMotionAdapter(), performance: perf);
        var svc = new ProductionCreateVideoService(os, store, files, new FixtureMotionAdapter(), lipsync: lipsync);
        var shot = new ProductionScope(Guid.Parse("aaaa3720-0000-4000-8000-000000000372"), "TEST", "SC01", "TEST-SC01-SH02");
        var ready = await RunPartialAsync(svc, store, files, shot, "verify", perf, snaps);
        var failed = false;
        try
        {
            await svc.CreateLipsyncAsync(new ProductionActionApiCommand(
                shot, LipsyncInput(shot, ready.Video.Artifact.ArtifactId, ready.Voice.Artifact.ArtifactId),
                "test", "lipsync-fail-once"));
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.FailedVerification)
        {
            failed = true;
        }
        Ok(failed && lipsync.Calls == 1, "failed verification rejects non-decodable bytes");
        var retried = await svc.CreateLipsyncAsync(new ProductionActionApiCommand(
            shot, LipsyncInput(shot, ready.Video.Artifact.ArtifactId, ready.Voice.Artifact.ArtifactId),
            "test", "lipsync-fail-once"));
        Ok(retried.Artifact.VerificationStatus == ProductionArtifactStatuses.Verified
            && retried.Artifact.Role == ProductionArtifactRoles.Lipsync
            && lipsync.Calls == 2
            && retried.Attempt.AttemptNumber == 2, "retry uses existing retry/new-attempt on same request");
        var old = await store.GetArtifactAsync(ready.Video.Artifact.ArtifactId);
        Ok(old is not null && old.ByteSha256 == ready.Video.Artifact.ByteSha256, "parent VIDEO_TAKE immutable after failed lipsync");
    }

    static async Task MutationAsync(Action<bool, string> Ok)
    {
        var (svc, store, files, os, perf, snaps) = Harness();
        var build = Guid.Parse("bbbb3720-0000-4000-8000-000000000372");
        var shot = new ProductionScope(build, "TEST", "SC01", "TEST-SC01-SH02");
        var first = await RunChainAsync(svc, store, files, shot, "mut", perf, snaps);
        var oldVideoSha = first.Video.Artifact.ByteSha256;
        var oldMixId = first.Mix.Artifact.ArtifactId;
        await os.InvalidateDownstreamAsync(shot, ShotNodeIds.Story);
        var videoTask = await store.GetTaskAsync(first.Video.Task.TaskId);
        var lipsyncTask = await store.GetTaskAsync(first.Lipsync.Task.TaskId);
        var mixTask = await store.GetTaskAsync(first.Mix.Task.TaskId);
        Ok(videoTask?.Status == ProductionTaskStatuses.Superseded
            && lipsyncTask?.Status == ProductionTaskStatuses.Superseded
            && mixTask?.Status == ProductionTaskStatuses.Superseded, "story mutation supersedes downstream tasks");
        var frozenVideo = await store.GetArtifactAsync(first.Video.Artifact.ArtifactId);
        Ok(frozenVideo is not null && frozenVideo.ByteSha256 == oldVideoSha, "old VIDEO_TAKE immutable");
        var frozenMix = await store.GetArtifactAsync(oldMixId);
        Ok(frozenMix is not null && frozenMix.ByteSha256 == first.Mix.Artifact.ByteSha256, "old MIX immutable");

        var image2 = await svc.CreateImageAsync(new ProductionActionApiCommand(
            shot, BaseInput(), "test", "mut-image-2"));
        var video2 = await svc.CreateVideoAsync(new CreateVideoApiCommand(
            shot, (await BoundVideo(perf, snaps, shot, image2.Artifact.ArtifactId)) with
            {
                Story = "TEST STORY — child opens a window and looks outside.",
                ActingBeat = "TEST ACTING — turns and looks out.",
            },
            "test", "mut-video-2", null, null));
        var voice2 = await svc.CreateVoiceAsync(new ProductionActionApiCommand(
            shot, await BoundVoice(perf, snaps, shot), "test", "mut-voice-2"));
        var lipsync2 = await svc.CreateLipsyncAsync(LipsyncCommand(shot, video2.Artifact.ArtifactId, voice2.Artifact.ArtifactId, "mut-lipsync-2"));
        Ok(video2.Artifact.ArtifactId != first.Video.Artifact.ArtifactId
            && lipsync2.Artifact.ParentArtifactIds?.Contains(video2.Artifact.ArtifactId) == true
            && lipsync2.Artifact.ParentArtifactIds?.Contains(first.Video.Artifact.ArtifactId) != true, "new LIPSYNC parents new artifactIds");
        Ok(!lipsync2.Artifact.StorageKey.Contains("http", StringComparison.OrdinalIgnoreCase), "new output is not a URL identity");
    }

    static void ScanLegacyBypass(Action<bool, string> Ok)
    {
        var root = FindRepoRoot();
        if (root is null)
        {
            Ok(false, "repo root for legacy bypass scan");
            return;
        }
        var files = new[]
        {
            Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.Actions.cs"),
            Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/Providers/IProductionMediaAdapters.cs"),
            Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionIdentityRules.cs"),
        };
        var forbidden = new[]
        {
            "/series/still", "/series/turbo", "/series/lipsync", "/series/assemble",
            "MixExplicitAsync", "startContentSeriesTurbo",
            "fal.run", "api.dev.runwayml.com",
        };
        foreach (var path in files)
        {
            var text = File.ReadAllText(path);
            foreach (var token in forbidden)
            {
                if (path.EndsWith("IProductionMediaAdapters.cs", StringComparison.Ordinal) && token == "CreateLipsyncAsync(")
                    continue;
                Ok(!text.Contains(token, StringComparison.OrdinalIgnoreCase), "legacy bypass " + Path.GetFileName(path) + " ! " + token);
            }
        }
        var createVideo = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.cs"));
        Ok(!createVideo.Contains("/series/turbo", StringComparison.OrdinalIgnoreCase)
            && !createVideo.Contains("/series/still", StringComparison.OrdinalIgnoreCase), "CREATE_VIDEO service has no legacy series authority");
        var repo = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/Production/ProductionWorkRepository.cs"));
        Ok(!repo.Contains("QueryAsync<FilmAssembly>", StringComparison.Ordinal)
            && repo.Contains("QueryAsync<AssemblyRow>", StringComparison.Ordinal), "assemblies map via AssemblyRow not FilmAssembly ctor");
    }

    static async Task<Chain> RunChainAsync(
        ProductionCreateVideoService svc, IProductionWorkStore store, InMemoryProductionArtifactFileStore files,
        ProductionScope shot, string tag, InMemoryPerformanceSnapshotStore perf, Dictionary<string, PerformanceSnapshot> snaps)
    {
        var partial = await RunPartialAsync(svc, store, files, shot, tag, perf, snaps);
        var lipsync = await svc.CreateLipsyncAsync(LipsyncCommand(shot, partial.Video.Artifact.ArtifactId, partial.Voice.Artifact.ArtifactId, tag + "-lipsync"));
        var mix = await svc.MixVideoAsync(MixCommand(shot, lipsync.Artifact.ArtifactId, tag + "-mix"));
        var accept = await svc.AcceptOsAsync(AcceptCommand(shot, mix.Artifact.ArtifactId, tag + "-accept"));
        var assembly = await svc.AssembleOsAsync(new ProductionActionApiCommand(
            shot, BaseInput() with { ParentArtifactIds = [mix.Artifact.ArtifactId] }, "test", tag + "-assemble",
            AssemblyShots: [(shot.ShotCode, mix.Artifact.ArtifactId, mix.Artifact.DurationSec)]));
        return new Chain(partial.Image, partial.Video, partial.Voice, lipsync, mix, accept, assembly);
    }

    static async Task<PartialChain> RunPartialAsync(
        ProductionCreateVideoService svc, IProductionWorkStore store, InMemoryProductionArtifactFileStore files,
        ProductionScope shot, string tag, InMemoryPerformanceSnapshotStore perf, Dictionary<string, PerformanceSnapshot> snaps)
    {
        _ = store;
        _ = files;
        var image = await svc.CreateImageAsync(new ProductionActionApiCommand(shot, BaseInput(), "test", tag + "-image"));
        var video = await svc.CreateVideoAsync(new CreateVideoApiCommand(
            shot, await BoundVideo(perf, snaps, shot, image.Artifact.ArtifactId), "test", tag + "-video", null, null));
        var voice = await svc.CreateVoiceAsync(new ProductionActionApiCommand(shot, await BoundVoice(perf, snaps, shot), "test", tag + "-voice"));
        return new PartialChain(image, video, voice, 0);
    }

    static ProductionActionApiCommand LipsyncCommand(ProductionScope shot, Guid? motion, Guid? voice, string? idem = null) =>
        new(shot, LipsyncInput(shot, motion, voice), "test", idem);

    static ProductionActionApiCommand MixCommand(ProductionScope shot, Guid? lipsync, string? idem = null) =>
        new(shot, BaseInput() with { LipsyncArtifactId = lipsync, ParentArtifactIds = lipsync is { } id ? [id] : null }, "test", idem);

    static ProductionActionApiCommand AcceptCommand(ProductionScope shot, Guid? artifact, string? idem = null) =>
        new(shot, BaseInput() with { AcceptArtifactId = artifact, ParentArtifactIds = artifact is { } id ? [id] : null }, "test", idem);

    static ProductionActionApiCommand VoiceCommand(ProductionScope shot, string lineId, string voiceId) =>
        new(shot, VoiceInput(shot) with { LineId = lineId, VoiceId = voiceId }, "test", null);

    static ShotExecutionInput BaseInput() => ProductionTestCreateVideoContext.Input();

    static ShotExecutionInput VoiceInput(ProductionScope shot) =>
        BaseInput() with
        {
            LineId = ProductionTestPerformanceFixture.LineId,
            SpeakerCharacterId = ProductionTestPerformanceFixture.CharacterId,
            Story = ProductionTestPerformanceFixture.LineText,
            VoiceId = ProductionTestPerformanceFixture.VoiceId,
        };

    static ShotExecutionInput LipsyncInput(ProductionScope shot, Guid? motion, Guid? voice) =>
        BaseInput() with { MotionArtifactId = motion, VoiceArtifactId = voice, ParentArtifactIds = new[] { motion, voice }.Where(id => id is not null).Select(id => id!.Value).ToArray() };

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

    static async Task<ShotExecutionInput> BoundVideo(InMemoryPerformanceSnapshotStore perf, Dictionary<string, PerformanceSnapshot> snaps, ProductionScope shot, Guid? kf = null)
        => ProductionTestPerformanceFixture.Attach(ProductionTestCreateVideoContext.Input(kf), await SnapshotOf(perf, snaps, shot));

    static async Task<ShotExecutionInput> BoundVoice(InMemoryPerformanceSnapshotStore perf, Dictionary<string, PerformanceSnapshot> snaps, ProductionScope shot, string? voiceId = null)
        => ProductionTestPerformanceFixture.VoiceInput(VoiceInput(shot), await SnapshotOf(perf, snaps, shot), voiceId ?? ProductionTestPerformanceFixture.VoiceId);

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

    sealed record Chain(
        CreateVideoSliceResult Image,
        CreateVideoSliceResult Video,
        CreateVideoSliceResult Voice,
        CreateVideoSliceResult Lipsync,
        CreateVideoSliceResult Mix,
        CreateVideoSliceResult Accept,
        FilmAssemblySliceResult Assembly);

    sealed record PartialChain(
        CreateVideoSliceResult Image,
        CreateVideoSliceResult Video,
        CreateVideoSliceResult Voice,
        int LipsyncAdapterCalls);

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

    sealed class FailThenFixtureLipSyncAdapter : ILipSyncExecutionAdapter
    {
        public int Calls { get; private set; }
        public string ProviderId => "fixture";
        public string ModelId => "fixture-lipsync-v1";
        public bool IsPaid => false;
        public bool RequiresNetwork => false;

        public Task<ProductionMediaResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
        {
            Calls++;
            if (Calls == 1)
            {
                var junk = Encoding.UTF8.GetBytes("NOT-AN-MP4-" + new string('X', 48));
                return Task.FromResult(new ProductionMediaResult("bad-lipsync", junk, ProductionTestMp4.Mime, 5));
            }
            return new FixtureLipSyncAdapter().ExecuteFrozenAsync(snapshot, ct);
        }
    }
}
