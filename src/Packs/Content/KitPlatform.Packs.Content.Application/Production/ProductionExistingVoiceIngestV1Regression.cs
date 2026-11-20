using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionExistingVoiceIngestV1Regression
{
    public const string SuiteId = "FAMIXA_EXISTING_VOICE_INGEST_V1";

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
        var tenantA = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var tenantB = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var caller = new SwitchTenant { Id = tenantA };
        var store = new InMemoryProductionWorkStore(caller);
        var files = new InMemoryProductionArtifactFileStore(caller);
        var motion = new GuardMotion();
        var voice = new GuardVoice();
        var lipsync = new GuardLipSync();
        var videoSourceId = Guid.Parse("b22370a9-6ddd-4b04-a281-c7498cc926ae");
        var voiceSourceId = Guid.Parse("bf8d0827-21ff-4daa-83b6-116bc3324ab0");
        var videoBytes = ProductionTestMp4.Encode("existing-video-take-beside-voice", 5);
        var videoSha = KitVideoIntegrityRules.Sha256Hex(videoBytes);
        var voiceBytes = Mpeg();
        var voiceSha = KitVideoIntegrityRules.Sha256Hex(voiceBytes);
        const string lineId = "line-SC01-CHAR-003-1";
        const string story = "Về rồi hả con? Sao cái mặt tiu nghỉu như mất sổ gạo thế kia?";
        var held = new UnattributedVoice(
            voiceSourceId, ProductionArtifactRoles.Voice, ProductionVoicePhysical.MpegMime,
            voiceSha, 4.232m, "elevenlabs", "eleven_v3", story, lineId, voiceBytes);
        var voices = new FakeVoice(held);
        var os = new ProductionExecutionService(store, motion);
        var build = Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");
        store.RegisterBuild(build, tenantA);
        var shots = new Shots(build, "EP01-SC01-SH01", "EP01-SC01-SH02");
        var svc = new ProductionCreateVideoService(
            os, store, files, motion, voice: voice, lipsync: lipsync,
            historicalVideoTakes: new FakeVideo(new UnattributedVideoTake(
                videoSourceId, ProductionArtifactRoles.VideoTake, ProductionTestMp4.Mime, videoSha, 5, "runway", "gen4_turbo", videoBytes)),
            historicalVoices: voices, targetShots: shots);
        var scope = new ProductionScope(build, "EP01", "SC01", "EP01-SC01-SH01");

        var video = await svc.RegisterExistingVideoTakeAsync(new RegisterExistingVideoTakeCommand(scope, videoSourceId, "test"), default);
        var art = await svc.RegisterExistingVoiceAsync(new RegisterExistingVoiceCommand(scope, voiceSourceId, "test"), default);
        Ok(art.ArtifactId != voiceSourceId && art.ArtifactId != Guid.Empty, "1 new voice artifact id");
        Ok(art.Role == ProductionArtifactRoles.Voice, "1 role VOICE");
        Ok(art.VerificationStatus == ProductionArtifactStatuses.Verified, "3 VERIFIED");
        Ok(art.MimeType == ProductionVoicePhysical.MpegMime, "3 mime audio/mpeg");
        Ok(art.DurationSec == 4.232m, "3 artifact duration is the source duration");
        Ok(art.SizeBytes == voiceBytes.Length, "2 byte size preserved");
        Ok(string.Equals(art.ByteSha256, voiceSha, StringComparison.OrdinalIgnoreCase), "2 SHA256 preserved");
        Ok(art.StorageKey.Contains("/os-voice/", StringComparison.OrdinalIgnoreCase), "1 tenant voice storage");
        Ok(art.StorageKey.StartsWith(ProductionTenantBoundary.Prefix(tenantA) + "/", StringComparison.OrdinalIgnoreCase), "1 tenant prefix");
        var stored = files.Read(art.StorageKey);
        Ok(stored is not null && KitVideoIntegrityRules.Sha256Hex(stored) == art.ByteSha256, "3 stored bytes match SHA");

        var voiceBind = await store.GetActiveBindingAsync(build, scope.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice));
        Ok(voiceBind is not null && voiceBind.ArtifactId == art.ArtifactId, "4 CURRENT:VOICE");
        var videoBind = await store.GetActiveBindingAsync(build, scope.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake));
        Ok(videoBind is not null && videoBind.ArtifactId == video.ArtifactId, "5 CURRENT:VIDEO_TAKE unchanged");
        var bare = await store.GetActiveBindingAsync(build, scope.ShotCode, ProductionBindingRoles.Current);
        Ok(bare is not null && bare.ArtifactId == video.ArtifactId, "5 bare CURRENT stays on the video take");

        var task = await store.GetTaskAsync(art.TaskId);
        Ok(task is not null && task.Action == ProductionActions.CreateVoice && task.ProviderId == "import" && task.ModelId == "existing-voice", "provider import/existing-voice");
        var snap = task is null ? null : await store.GetSnapshotByTaskAsync(task.TaskId);
        Ok(snap is not null && snap.Story == story, "spoken line preserved");
        Ok(snap is not null && snap.ShotContract.Contains("RECOVER_VOICE", StringComparison.Ordinal), "provenance op");
        Ok(snap is not null && snap.ShotContract.Contains(voiceSourceId.ToString("D"), StringComparison.Ordinal), "provenance source artifact");
        Ok(snap is not null && snap.ShotContract.Contains(voiceSha, StringComparison.Ordinal), "provenance source sha");
        Ok(snap is not null && snap.ShotContract.Contains(lineId, StringComparison.Ordinal), "provenance lineId");
        Ok(snap is not null && snap.ShotContract.Contains("elevenlabs", StringComparison.Ordinal) && snap.ShotContract.Contains("eleven_v3", StringComparison.Ordinal), "provenance original provider");
        Ok(snap is not null && !snap.ShotContract.Contains("voiceId", StringComparison.Ordinal), "no invented voiceId");
        Ok(snap is not null && snap.ParentArtifactIds is null, "no parent artifact link");
        Ok(snap is not null && snap.DurationSec == 4.232m, "snapshot duration is audio duration");
        Ok(ProductionVoiceLineIdentity.Matches(snap, lineId), "line identity readable from snapshot");
        var attempts = task is null ? [] : await store.ListAttemptsAsync(task.TaskId);
        Ok(attempts.Count == 1, "one attempt");

        var again = await svc.RegisterExistingVoiceAsync(new RegisterExistingVoiceCommand(scope, voiceSourceId, "test"), default);
        Ok(again.ArtifactId == art.ArtifactId, "9 replay returns the same artifact");
        Ok(voices.Reads == 1, "9 replay does not read the source again");
        var voiceTasks = (await store.ListTasksByScopeAsync(build, scope.ShotCode)).Where(t => t.Action == ProductionActions.CreateVoice).ToArray();
        Ok(voiceTasks.Length == 1, "9 one voice task");
        Ok(await store.GetSnapshotByTaskAsync(art.TaskId) is not null && attempts.Count == 1, "9 one snapshot");

        var lipsyncVerdict = await os.EvaluateCreateLipsyncAsync(scope);
        Ok(lipsyncVerdict.Available && lipsyncVerdict.VideoArtifactId == video.ArtifactId && lipsyncVerdict.VoiceArtifactId == art.ArtifactId, "11 lipsync evaluator sees both parents");
        Ok(motion.Calls == 0 && voice.Calls == 0 && lipsync.Calls == 0, "10 provider adapters not called");

        caller.Id = tenantB;
        Ok(await store.GetArtifactAsync(art.ArtifactId) is null, "7 tenant B cannot read the voice artifact");
        Ok(await svc.GetCurrentVoiceAsync(scope, lineId) is { IsEmpty: true }, "7 tenant B voice resolution is empty");
        caller.Id = tenantA;
        var resolved = await svc.GetCurrentVoiceAsync(scope, lineId);
        Ok(!resolved.IsEmpty && resolved.ArtifactId == art.ArtifactId && resolved.ByteSha256 == voiceSha, "owner can read the voice");

        Ok(held.ArtifactId == voiceSourceId && held.ByteSha256 == voiceSha && held.Bytes.Length == voiceBytes.Length && held.Story == story, "8 source record unchanged");

        var otherBuild = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
        store.RegisterBuild(otherBuild, tenantA);
        var other = new ProductionScope(otherBuild, "EP01", "SC01", "EP01-SC01-SH02");
        var badSha = held with { ByteSha256 = new string('a', 64) };
        var svcBad = new ProductionCreateVideoService(os, store, files, motion, voice: voice, lipsync: lipsync, historicalVoices: new FakeVoice(badSha), targetShots: new Shots(otherBuild, other.ShotCode));
        var readsBefore = voices.Reads;
        Ok(await Throws(ProductionFailureReasons.FailedIdentity, () =>
            svcBad.RegisterExistingVoiceAsync(new RegisterExistingVoiceCommand(other, voiceSourceId, "test"), default)),
            "2 sha mismatch rejected");
        Ok((await store.ListTasksByScopeAsync(otherBuild, other.ShotCode)).Count == 0, "2 sha mismatch writes nothing");
        Ok(await store.GetActiveBindingAsync(otherBuild, other.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice)) is null, "2 sha mismatch does not bind");
        Ok(voices.Reads == readsBefore, "2 sha mismatch uses its own source");

        var missingShot = new ProductionScope(build, "EP01", "SC01", "EP01-SC01-SH09");
        var beforeMissing = voices.Reads;
        Ok(await Throws(ProductionFailureReasons.FailedIdentity, () =>
            svc.RegisterExistingVoiceAsync(new RegisterExistingVoiceCommand(missingShot, voiceSourceId, "test"), default)),
            "6 missing shot rejected");
        Ok(voices.Reads == beforeMissing, "6 missing shot does not read the source");

        store.RegisterBuild(ProductionScopeAnchorRules.OfficialEp01, tenantA);
        var historical = new ProductionScope(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH01");
        Ok(await Throws(ProductionTenantBoundary.Unattributed, () =>
            svc.RegisterExistingVoiceAsync(new RegisterExistingVoiceCommand(historical, voiceSourceId, "test"), default)),
            "6 historical build rejected");
        Ok(voices.Reads == beforeMissing, "6 historical build does not read the source");

        var unregistered = new ProductionScope(Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd"), "EP01", "SC01", "EP01-SC01-SH01");
        Ok(await Throws(ProductionTenantBoundary.Unattributed, () =>
            svc.RegisterExistingVoiceAsync(new RegisterExistingVoiceCommand(unregistered, voiceSourceId, "test"), default)),
            "6 missing build rejected");
        store.RegisterBuild(unregistered.BuildId, tenantB);
        Ok(await Throws(ProductionTenantBoundary.Forbidden, () =>
            svc.RegisterExistingVoiceAsync(new RegisterExistingVoiceCommand(unregistered, voiceSourceId, "test"), default)),
            "7 other tenant build rejected");
        var wrongEpisode = new ProductionScope(build, "EP99", "SC01", "EP99-SC01-SH01");
        Ok(await Throws(ProductionFailureReasons.MissingPerformanceSnapshot, () =>
            svc.RegisterExistingVoiceAsync(new RegisterExistingVoiceCommand(wrongEpisode, voiceSourceId, "test"), default)),
            "6 non-EP01 target rejected");
        Ok(motion.Calls == 0 && voice.Calls == 0 && lipsync.Calls == 0, "10 still no provider call");
    }

    static byte[] Mpeg()
    {
        var bytes = new byte[64];
        bytes[0] = 0x49;
        bytes[1] = 0x44;
        bytes[2] = 0x33;
        return bytes;
    }

    static async Task<bool> Throws(string message, Func<Task> work)
    {
        try
        {
            await work();
            return false;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message == message;
        }
    }

    sealed class SwitchTenant : IProductionCallerTenant
    {
        public Guid Id { get; set; }
        public Guid RequireTenant() => ProductionTenantBoundary.Require(Id);
    }

    sealed class Shots(Guid buildId, params string[] codes) : IProductionTargetShotCheck
    {
        public Task RequireAsync(Guid build, string shotCode, CancellationToken ct = default)
        {
            if (build != buildId) return Task.CompletedTask;
            if (!codes.Contains(shotCode, StringComparer.Ordinal))
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
            return Task.CompletedTask;
        }
    }

    sealed class FakeVoice(UnattributedVoice? voice) : IUnattributedVoiceSource
    {
        public int Reads;
        public Task<UnattributedVoice?> ReadAsync(Guid artifactId, CancellationToken ct = default)
        {
            Reads++;
            if (voice is null || voice.ArtifactId != artifactId) return Task.FromResult<UnattributedVoice?>(null);
            return Task.FromResult<UnattributedVoice?>(voice);
        }
    }

    sealed class FakeVideo(UnattributedVideoTake take) : IUnattributedVideoTakeSource
    {
        public Task<UnattributedVideoTake?> ReadAsync(Guid artifactId, CancellationToken ct = default) =>
            Task.FromResult<UnattributedVideoTake?>(take.ArtifactId == artifactId ? take : null);
    }

    sealed class GuardMotion : IMotionExecutionAdapter
    {
        public int Calls;
        public string ProviderId => "runway";
        public string ModelId => "gen4_turbo";
        public bool IsPaid => true;
        public bool RequiresNetwork => true;
        public Task<MotionStartResult> StartFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new MotionStartResult("no"));
        }
        public Task<MotionPollResult> PollFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new MotionPollResult(MotionPollStatuses.Failed, providerJobId));
        }
        public Task<MotionPollResult> RecoverFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new MotionPollResult(MotionPollStatuses.Failed, providerJobId));
        }
        public Task<MotionAdapterResult> FetchOutputAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new MotionAdapterResult(providerJobId, [], "video/mp4", 0, "os/no"));
        }
        public Task<MotionAdapterResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new MotionAdapterResult("no", [], "video/mp4", 0, "os/no"));
        }
    }

    sealed class GuardVoice : IVoiceExecutionAdapter
    {
        public int Calls;
        public string ProviderId => "elevenlabs";
        public string ModelId => "eleven_v3";
        public bool IsPaid => true;
        public bool RequiresNetwork => true;
        public Task<ProductionMediaResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new ProductionMediaResult("no", [], "audio/mpeg", 0));
        }
    }

    sealed class GuardLipSync : ILipSyncExecutionAdapter
    {
        public int Calls;
        public string ProviderId => "fal";
        public string ModelId => "fal-ai/sync-lipsync";
        public bool IsPaid => true;
        public bool RequiresNetwork => true;
        public Task<ProductionMediaResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new ProductionMediaResult("no", [], "video/mp4", 0));
        }
    }
}
