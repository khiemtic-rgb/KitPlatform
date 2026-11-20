using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionTenantEp01MixV1Regression
{
    public const string SuiteId = "FAMIXA_TENANT_EP01_MIX_V1";

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
        var fixtureMix = new GuardMix();
        var fal = new FakeFalLipsyncTransport();
        var ffmpeg = new FakeFfmpegMixTransport();
        var build = Guid.Parse("2615b2da-da9e-486b-9372-8927784df40f");
        var historical = Guid.Parse("4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f");
        var otherBuild = Guid.Parse("33333333-3333-4333-8333-333333333333");
        store.RegisterBuild(build, tenantA);
        store.RegisterBuild(historical, tenantA);
        store.RegisterBuild(otherBuild, tenantA);
        var shots = new Shots(build, "EP01-SC01-SH01", "EP01-SC01-SH02");
        var snaps = new InMemoryPerformanceSnapshotStore();
        var falCredential = ProductionOfficialHarnessCredentialFixture.Pin(
            tenantA,
            Guid.Parse("f42b0002-f42b-4f42-8f42-f42b00000002"),
            Guid.Parse("f42c0003-f42c-4f42-8f42-f42c00000003"),
            ProductionLipsyncCatalog.FalProviderId,
            ProductionLipsyncCatalog.SyncLipsyncV3,
            "tenant-ep01-mix-fal-fixture");
        var os = new ProductionExecutionService(store, motion, credentials: falCredential.Resolver);
        var permits = new InMemoryProductionExecutionPermitStore();
        await permits.IssueAsync(new IssueProductionExecutionPermitCommand(
            build, "EP01-SC01-SH01", "EP01", "SC01", ProductionActions.CreateLipsync,
            ProductionLipsyncCatalog.FalProviderId, ProductionLipsyncCatalog.SyncLipsyncV3, "test", "tenant-ep01-mix-lipsync"));
        var svc = new ProductionCreateVideoService(
            os, store, files, motion,
            officialPermits: permits,
            lipsync: new GuardLipSync(),
            mix: fixtureMix,
            officialLipsync: new FalLipSyncExecutionAdapter(fal),
            officialMix: new FfmpegMixExecutionAdapter(ffmpeg),
            historicalVideoTakes: new FakeVideo(VideoTake("b22370a9-6ddd-4b04-a281-c7498cc926ae")),
            historicalVoices: new FakeVoice(Voice(
                "bf8d0827-21ff-4daa-83b6-116bc3324ab0",
                "line-SC01-CHAR-003-1",
                "Về rồi hả con? Sao cái mặt tiu nghỉu như mất sổ gạo thế kia?")),
            targetShots: shots,
            performanceSnapshots: snaps);

        var sh01 = new ProductionScope(build, "EP01", "SC01", "EP01-SC01-SH01");
        var sh02 = new ProductionScope(build, "EP01", "SC01", "EP01-SC01-SH02");
        var video = await svc.RegisterExistingVideoTakeAsync(
            new RegisterExistingVideoTakeCommand(sh01, Guid.Parse("b22370a9-6ddd-4b04-a281-c7498cc926ae"), "test"));
        var voice = await svc.RegisterExistingVoiceAsync(
            new RegisterExistingVoiceCommand(sh01, Guid.Parse("bf8d0827-21ff-4daa-83b6-116bc3324ab0"), "test"));
        await SeedFullTakeSpeech(snaps, sh01, video.DurationSec);
        var lip = await svc.CreateLipsyncAsync(Lipsync(sh01, video.ArtifactId, voice.ArtifactId));
        var bareBefore = await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.Current);
        var videoBefore = await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake));
        var voiceBefore = await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice));
        var lipBefore = await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync));

        Ok(await Throws(ProductionFailureReasons.MissingDependency, () => svc.MixVideoAsync(
            Mix(sh01, ProductionEp01Sh01MixCanon.LipsyncArtifactId))),
            "historical lipsync parent not selected");
        Ok(await Throws(ProductionFailureReasons.CrossShotForbidden, () => svc.MixVideoAsync(Mix(sh02, lip.Artifact.ArtifactId))),
            "shot validation rejects a parent from another shot");
        Ok(await Throws(ProductionFailureReasons.MissingLipsyncArtifact, () => svc.MixVideoAsync(
            Mix(new ProductionScope(otherBuild, "EP01", "SC01", "EP01-SC01-SH01"), null))),
            "build validation rejects a build without the parents");
        Ok(ffmpeg.ComposeExecutionCount == 0 && fixtureMix.Calls == 0, "rejected mix does not execute");

        var first = await svc.MixVideoAsync(Mix(sh01, lip.Artifact.ArtifactId));
        Ok(first.Artifact.Role == ProductionArtifactRoles.Mix, "role MIX");
        Ok(first.Artifact.VerificationStatus == ProductionArtifactStatuses.Verified, "verified");
        Ok(first.Task.ProviderId == ProductionMixCatalog.ServerProviderId
            && first.Task.ModelId == ProductionMixCatalog.FfmpegModelId, "adapter server ffmpeg");
        Ok(first.Attempt.ProviderJobId is not null && first.Attempt.ProviderJobId.StartsWith("ffmpeg-fake-", StringComparison.Ordinal),
            "ffmpeg job id");
        var stored = files.Read(first.Artifact.StorageKey);
        Ok(stored is { Length: >= 32 }
            && KitVideoIntegrityRules.Sha256Hex(stored) == first.Artifact.ByteSha256
            && first.Artifact.ByteSha256 != video.ByteSha256
            && first.Artifact.ByteSha256 != lip.Artifact.ByteSha256, "physical sha differs from parents");
        Ok(first.Artifact.DurationSec == video.DurationSec && first.Artifact.MimeType == ProductionTestMp4.Mime, "duration and mime");
        var snap = await store.GetSnapshotByTaskAsync(first.Task.TaskId);
        Ok(snap is not null
            && snap.ParentArtifactIds is not null
            && snap.ParentArtifactIds.Contains(video.ArtifactId)
            && snap.ParentArtifactIds.Contains(lip.Artifact.ArtifactId)
            && !snap.ParentArtifactIds.Contains(voice.ArtifactId), "frozen parents are video and lipsync");
        var mixBind = await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix));
        Ok(mixBind is not null && mixBind.ArtifactId == first.Artifact.ArtifactId, "CURRENT:MIX");
        Ok((await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.Current))?.ArtifactId == bareBefore?.ArtifactId
            && (await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake)))?.ArtifactId == videoBefore?.ArtifactId
            && (await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice)))?.ArtifactId == voiceBefore?.ArtifactId
            && (await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync)))?.ArtifactId == lipBefore?.ArtifactId,
            "parent current bindings unchanged");
        Ok(first.Visible is not null && first.Visible.ArtifactId == first.Artifact.ArtifactId, "VISIBLE follows mix");
        Ok(first.Artifact.ArtifactId != ProductionEp01Sh01AssemblyCanon.MixArtifactId
            && first.Artifact.ArtifactId != ProductionEp01Sh02AssembleCanon.MixArtifactId, "not a historical mix id");
        Ok((await store.ListTasksByScopeAsync(historical, sh01.ShotCode)).Count == 0, "historical build has no new tasks");
        Ok(ffmpeg.ComposeExecutionCount == 1 && ffmpeg.ExecutionCount == 0 && fixtureMix.Calls == 0, "one compose, fixture unused");

        var again = await svc.MixVideoAsync(Mix(sh01, lip.Artifact.ArtifactId));
        Ok(again.Request.RequestId == first.Request.RequestId
            && again.Task.TaskId == first.Task.TaskId
            && again.Attempt.AttemptId == first.Attempt.AttemptId
            && again.Artifact.ArtifactId == first.Artifact.ArtifactId
            && ffmpeg.ComposeExecutionCount == 1, "replay reuses the same mix");
        Ok((await store.ListTasksByScopeAsync(build, sh01.ShotCode)).Count(t => t.Action == ProductionActions.MixVideo) == 1,
            "one mix task");

        var state = await os.GetShotStateAsync(sh01);
        var mixNode = state.Nodes.First(n => n.Node == ShotNodeIds.Mix);
        var assemble = state.Actions.First(a => a.Action == ProductionActions.Assemble);
        Ok(mixNode.Status == ShotNodeStatuses.Current && mixNode.ArtifactId == first.Artifact.ArtifactId, "assembly evaluator sees MIX");
        Ok(!assemble.Available && assemble.BlockReason == ProductionFailureReasons.MissingDependency, "assembly stays blocked without accept");
        Ok(ffmpeg.ConcatExecutionCount == 0, "assembly was not executed");

        caller.Id = tenantB;
        Ok(await store.GetArtifactAsync(first.Artifact.ArtifactId) is null, "other tenant cannot read the mix artifact");
        Ok(await store.GetTaskAsync(first.Task.TaskId) is null, "other tenant cannot read the mix task");
        Ok(files.Read(first.Artifact.StorageKey) is null, "other tenant cannot read the mix storage key");
        caller.Id = tenantA;
        Ok(fal.StartCount == 1 && motion.Calls == 0, "mix does not call lipsync or motion");
        await AssertFixtureLipsyncExcluded(Ok);
    }

    static async Task AssertFixtureLipsyncExcluded(Action<bool, string> Ok)
    {
        var tenant = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var caller = new SwitchTenant { Id = tenant };
        var store = new InMemoryProductionWorkStore(caller);
        var files = new InMemoryProductionArtifactFileStore(caller);
        var build = Guid.Parse("2615b2da-da9e-486b-9372-8927784df40f");
        store.RegisterBuild(build, tenant);
        var snaps = new InMemoryPerformanceSnapshotStore();
        var ffmpeg = new FakeFfmpegMixTransport();
        var os = new ProductionExecutionService(store, new GuardMotion());
        var svc = new ProductionCreateVideoService(
            os, store, files, new GuardMotion(),
            lipsync: new FixtureLipSyncAdapter(),
            mix: new GuardMix(),
            officialMix: new FfmpegMixExecutionAdapter(ffmpeg),
            historicalVideoTakes: new FakeVideo(VideoTake("b22370a9-6ddd-4b04-a281-c7498cc926ae")),
            historicalVoices: new FakeVoice(Voice(
                "bf8d0827-21ff-4daa-83b6-116bc3324ab0",
                "line-SC01-CHAR-003-1",
                "Về rồi hả con? Sao cái mặt tiu nghỉu như mất sổ gạo thế kia?")),
            targetShots: new Shots(build, "EP01-SC01-SH01"),
            performanceSnapshots: snaps);
        var sh01 = new ProductionScope(build, "EP01", "SC01", "EP01-SC01-SH01");
        var video = await svc.RegisterExistingVideoTakeAsync(
            new RegisterExistingVideoTakeCommand(sh01, Guid.Parse("b22370a9-6ddd-4b04-a281-c7498cc926ae"), "test"));
        var voice = await svc.RegisterExistingVoiceAsync(
            new RegisterExistingVoiceCommand(sh01, Guid.Parse("bf8d0827-21ff-4daa-83b6-116bc3324ab0"), "test"));
        var unpaid = Lipsync(sh01, video.ArtifactId, voice.ArtifactId) with { AuthorizePaidNetwork = false };
        var fixtureLip = await svc.CreateLipsyncAsync(unpaid);
        Ok(fixtureLip.Task.ProviderId == ProductionLipsyncCatalog.FixtureProviderId, "unpaid lipsync stays on the fixture");
        var mix = await os.EvaluateCreateMixAsync(sh01);
        Ok(!mix.Available && mix.BlockReason == ProductionFailureReasons.FailedIdentity
            && mix.LipsyncArtifactId == fixtureLip.Artifact.ArtifactId,
            "fixture lipsync is not offered for mix");
        Ok(await Throws(ProductionFailureReasons.FailedIdentity, () => svc.MixVideoAsync(Mix(sh01, fixtureLip.Artifact.ArtifactId))),
            "mix refuses the fixture lipsync");
        Ok(ffmpeg.ComposeExecutionCount == 0, "refused fixture lipsync does not run FFmpeg");
    }

    static async Task SeedFullTakeSpeech(InMemoryPerformanceSnapshotStore snaps, ProductionScope shot, decimal duration)
    {
        var perf = new PerformanceContractService(snaps);
        var draft = perf.CreateDraft(ProductionTestPerformanceFixture.Source(shot.BuildId, shot.ShotCode));
        var source = new PerformanceSourceRef(
            true, [true], false, null,
            duration,
            ProductionDirectorTiming.Source,
            ProductionDirectorTiming.DecisionType,
            SpeechStartSec: 0m,
            SpeechEndSec: duration);
        await perf.FreezeAsync(draft, "regression", source);
    }

    static ProductionActionApiCommand Lipsync(ProductionScope scope, Guid videoId, Guid voiceId) =>
        new(scope, new ShotExecutionInput(
            "Recovered line", "beat", "visual", "", "", "5", "", "",
            null, "", "16:9", 5, ShotExecutionCompiler.Version, null,
            "line-SC01-CHAR-003-1", "CHAR-003", "", videoId, voiceId),
            "test", null, AuthorizePaidNetwork: true);

    static ProductionActionApiCommand Mix(ProductionScope scope, Guid? lipsyncId) =>
        new(scope, new ShotExecutionInput(
            "Recovered line", "beat", "visual", "", "", "5", "", "",
            null, "", "16:9", 5, ShotExecutionCompiler.Version, null,
            "", "", "", null, null, lipsyncId),
            "test", null);

    static UnattributedVideoTake VideoTake(string id)
    {
        var bytes = ProductionTestMp4.Encode("tenant-mix-video", 5);
        return new UnattributedVideoTake(
            Guid.Parse(id), ProductionArtifactRoles.VideoTake, ProductionTestMp4.Mime,
            KitVideoIntegrityRules.Sha256Hex(bytes), 5, "runway", "gen4_turbo", bytes);
    }

    static UnattributedVoice Voice(string id, string lineId, string story)
    {
        var bytes = new byte[64];
        bytes[0] = 0x49; bytes[1] = 0x44; bytes[2] = 0x33;
        return new UnattributedVoice(
            Guid.Parse(id), ProductionArtifactRoles.Voice, ProductionVoicePhysical.MpegMime,
            KitVideoIntegrityRules.Sha256Hex(bytes), 4.232m, "elevenlabs", "eleven_v3", story, lineId, bytes);
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

    sealed class FakeVideo(UnattributedVideoTake take) : IUnattributedVideoTakeSource
    {
        public Task<UnattributedVideoTake?> ReadAsync(Guid artifactId, CancellationToken ct = default) =>
            Task.FromResult<UnattributedVideoTake?>(take.ArtifactId == artifactId ? take : null);
    }

    sealed class FakeVoice(UnattributedVoice voice) : IUnattributedVoiceSource
    {
        public Task<UnattributedVoice?> ReadAsync(Guid artifactId, CancellationToken ct = default) =>
            Task.FromResult<UnattributedVoice?>(voice.ArtifactId == artifactId ? voice : null);
    }

    sealed class GuardMotion : IMotionExecutionAdapter
    {
        public int Calls;
        public string ProviderId => "runway";
        public string ModelId => "gen4_turbo";
        public bool IsPaid => true;
        public bool RequiresNetwork => true;
        public Task<MotionStartResult> StartFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
        { Calls++; return Task.FromResult(new MotionStartResult("no")); }
        public Task<MotionPollResult> PollFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
        { Calls++; return Task.FromResult(new MotionPollResult(MotionPollStatuses.Failed, providerJobId)); }
        public Task<MotionPollResult> RecoverFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
        { Calls++; return Task.FromResult(new MotionPollResult(MotionPollStatuses.Failed, providerJobId)); }
        public Task<MotionAdapterResult> FetchOutputAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
        { Calls++; return Task.FromResult(new MotionAdapterResult(providerJobId, [], "video/mp4", 0, "os/no")); }
        public Task<MotionAdapterResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
        { Calls++; return Task.FromResult(new MotionAdapterResult("no", [], "video/mp4", 0, "os/no")); }
    }

    sealed class GuardLipSync : ILipSyncExecutionAdapter
    {
        public string ProviderId => "fixture";
        public string ModelId => "fixture-lipsync-v1";
        public bool IsPaid => false;
        public bool RequiresNetwork => false;
        public Task<ProductionMediaResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
            => Task.FromResult(new ProductionMediaResult("no", [], "video/mp4", 0));
    }

    sealed class GuardMix : IMixExecutionAdapter
    {
        public int Calls;
        public string ProviderId => "fixture";
        public string ModelId => "fixture-mix-v1";
        public bool IsPaid => false;
        public bool RequiresNetwork => false;
        public Task<ProductionMediaResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new ProductionMediaResult("no", [], "video/mp4", 0));
        }
    }
}
