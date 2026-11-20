using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionTenantEp01AssembleV1Regression
{
    public const string SuiteId = "FAMIXA_TENANT_EP01_ASSEMBLE_V1";

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
        var ffmpeg = new FakeFfmpegMixTransport();
        var build = Guid.Parse("2615b2da-da9e-486b-9372-8927784df40f");
        var historical = Guid.Parse("4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f");
        store.RegisterBuild(build, tenantA);
        store.RegisterBuild(historical, tenantA);
        var shots = new Shots(build, "EP01-SC01-SH01");
        var snaps = new InMemoryPerformanceSnapshotStore();
        var falCredential = ProductionOfficialHarnessCredentialFixture.Pin(
            tenantA,
            Guid.Parse("f43b0002-f43b-4f43-8f43-f43b00000002"),
            Guid.Parse("f43c0003-f43c-4f43-8f43-f43c00000003"),
            ProductionLipsyncCatalog.FalProviderId,
            ProductionLipsyncCatalog.SyncLipsyncV3,
            "tenant-ep01-assemble-fal-fixture");
        var os = new ProductionExecutionService(store, motion, credentials: falCredential.Resolver);
        var permits = new InMemoryProductionExecutionPermitStore();
        await permits.IssueAsync(new IssueProductionExecutionPermitCommand(
            build, "EP01-SC01-SH01", "EP01", "SC01", ProductionActions.CreateLipsync,
            ProductionLipsyncCatalog.FalProviderId, ProductionLipsyncCatalog.SyncLipsyncV3, "test", "tenant-ep01-assemble-lipsync"));
        var svc = new ProductionCreateVideoService(
            os, store, files, motion,
            officialPermits: permits,
            lipsync: new GuardLipSync(),
            officialLipsync: new FalLipSyncExecutionAdapter(new FakeFalLipsyncTransport()),
            officialMix: new FfmpegMixExecutionAdapter(ffmpeg),
            officialAssembly: new FfmpegAssemblyExecutionAdapter(ffmpeg),
            historicalVideoTakes: new FakeVideo(VideoTake("b22370a9-6ddd-4b04-a281-c7498cc926ae")),
            historicalVoices: new FakeVoice(Voice(
                "bf8d0827-21ff-4daa-83b6-116bc3324ab0",
                "line-SC01-CHAR-003-1",
                "Về rồi hả con? Sao cái mặt tiu nghỉu như mất sổ gạo thế kia?")),
            targetShots: shots,
            performanceSnapshots: snaps);

        var sh01 = new ProductionScope(build, "EP01", "SC01", "EP01-SC01-SH01");
        var video = await svc.RegisterExistingVideoTakeAsync(
            new RegisterExistingVideoTakeCommand(sh01, Guid.Parse("b22370a9-6ddd-4b04-a281-c7498cc926ae"), "test"));
        var voice = await svc.RegisterExistingVoiceAsync(
            new RegisterExistingVoiceCommand(sh01, Guid.Parse("bf8d0827-21ff-4daa-83b6-116bc3324ab0"), "test"));
        await SeedFullTakeSpeech(snaps, sh01, video.DurationSec);
        var lip = await svc.CreateLipsyncAsync(Command(sh01, video.ArtifactId, voice.ArtifactId, null, null, true));
        var mix = await svc.MixVideoAsync(Command(sh01, null, null, lip.Artifact.ArtifactId, null, false));
        var mixBind = await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix));
        var videoBind = await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake));
        var voiceBind = await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice));
        var lipBind = await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync));
        var bare = await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.Current);

        Ok(await Throws(ProductionFailureReasons.MissingDependency, () => svc.AcceptOsAsync(
            Command(sh01, null, null, null, ProductionEp01Sh01AssemblyCanon.MixArtifactId, false))),
            "historical mix is not accepted");
        Ok(ffmpeg.ExecutionCount == 0, "rejected accept does not remux");

        var accept = await svc.AcceptOsAsync(Command(sh01, null, null, null, mix.Artifact.ArtifactId, false));
        Ok(accept.Artifact.ArtifactId == mix.Artifact.ArtifactId && accept.Artifact.Role == ProductionArtifactRoles.Mix, "accept keeps the mix artifact");
        var accepted = await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.Accepted);
        var acceptedMix = await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Mix));
        Ok(accepted?.ArtifactId == mix.Artifact.ArtifactId && acceptedMix?.ArtifactId == mix.Artifact.ArtifactId, "ACCEPTED and ACCEPTED:MIX");
        Ok((await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix)))?.ArtifactId == mixBind?.ArtifactId
            && (await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake)))?.ArtifactId == videoBind?.ArtifactId
            && (await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice)))?.ArtifactId == voiceBind?.ArtifactId
            && (await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync)))?.ArtifactId == lipBind?.ArtifactId
            && (await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.Current))?.ArtifactId == bare?.ArtifactId,
            "current parents unchanged");

        var acceptAgain = await svc.AcceptOsAsync(Command(sh01, null, null, null, mix.Artifact.ArtifactId, false));
        Ok(acceptAgain.Request.RequestId == accept.Request.RequestId
            && (await store.ListTasksByScopeAsync(build, sh01.ShotCode)).Count(t => t.Action == ProductionActions.Accept) == 1,
            "accept replay is stable");

        var state = await os.GetShotStateAsync(sh01);
        var assembleAction = state.Actions.First(a => a.Action == ProductionActions.Assemble);
        Ok(assembleAction.Available && assembleAction.BlockReason is null, "assemble available after accept");
        Ok(!state.Actions.Any(a => a.Action == ProductionActions.AssembleEpisode && a.Available), "episode assembly is not this path");

        var first = await svc.AssembleOsAsync(Assemble(sh01, mix.Artifact.ArtifactId, mix.Artifact.DurationSec));
        Ok(first.Output is not null
            && first.Output.Role == ProductionArtifactRoles.Assembly
            && first.Output.VerificationStatus == ProductionArtifactStatuses.Verified
            && first.Output.ArtifactId != mix.Artifact.ArtifactId, "assembly artifact");
        var stored = files.Read(first.Output!.StorageKey);
        Ok(stored is { Length: >= 32 } && KitVideoIntegrityRules.Sha256Hex(stored) == first.Output.ByteSha256, "physical sha");
        var asmBind = await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly));
        Ok(asmBind?.ArtifactId == first.Output.ArtifactId, "CURRENT:ASSEMBLY");
        Ok((await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Mix)))?.ArtifactId == mix.Artifact.ArtifactId
            && (await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix)))?.ArtifactId == mix.Artifact.ArtifactId,
            "mix authority unchanged by assembly");
        var films = await store.ListAssembliesByBuildAsync(build);
        Ok(films.Count == 1, "one shot membership");
        var members = await store.ListAssemblyShotsAsync(films[0].AssemblyId);
        Ok(members.Count == 1 && members[0].ArtifactId == mix.Artifact.ArtifactId, "membership is the accepted mix");
        Ok(ffmpeg.ExecutionCount == 1 && ffmpeg.ComposeExecutionCount == 1, "one remux after the mix compose");
        Ok((await store.ListTasksByScopeAsync(historical, sh01.ShotCode)).Count == 0, "historical build untouched");

        var again = await svc.AssembleOsAsync(Assemble(sh01, mix.Artifact.ArtifactId, mix.Artifact.DurationSec));
        Ok(again.Output?.ArtifactId == first.Output.ArtifactId
            && again.Request.RequestId == first.Request.RequestId
            && again.Task.TaskId == first.Task.TaskId
            && again.Attempt.AttemptId == first.Attempt.AttemptId
            && ffmpeg.ExecutionCount == 1
            && (await store.ListAssembliesByBuildAsync(build)).Count == 1, "assembly replay");
        Ok((await store.ListTasksByScopeAsync(build, sh01.ShotCode)).Count(t => t.Action == ProductionActions.AssembleEpisode) == 0,
            "no episode assembly task");

        caller.Id = tenantB;
        Ok(await store.GetArtifactAsync(first.Output.ArtifactId) is null, "other tenant cannot read the assembly");
        Ok(await store.GetTaskAsync(first.Task.TaskId) is null, "other tenant cannot read the assembly task");
        Ok(files.Read(first.Output.StorageKey) is null, "other tenant cannot read the storage key");
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

    static ProductionActionApiCommand Command(
        ProductionScope scope, Guid? videoId, Guid? voiceId, Guid? lipsyncId, Guid? acceptId, bool paid) =>
        new(scope, new ShotExecutionInput(
            "Recovered line", "beat", "visual", "", "", "5", "", "",
            null, "", "16:9", 5, ShotExecutionCompiler.Version, null,
            "line-SC01-CHAR-003-1", "CHAR-003", "", videoId, voiceId, lipsyncId, acceptId),
            "test", null, AuthorizePaidNetwork: paid);

    static ProductionActionApiCommand Assemble(ProductionScope scope, Guid mixId, decimal duration) =>
        new(scope, new ShotExecutionInput(
            "Recovered line", "beat", "visual", "", "", "5", "", "",
            null, "", "16:9", duration, ShotExecutionCompiler.Version, [mixId],
            "", "", "", null, null, null, null),
            "test", null, null, null, [(scope.ShotCode, mixId, duration)]);

    static UnattributedVideoTake VideoTake(string id)
    {
        var bytes = ProductionTestMp4.Encode("tenant-assemble-video", 5);
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
        public string ProviderId => "runway";
        public string ModelId => "gen4_turbo";
        public bool IsPaid => true;
        public bool RequiresNetwork => true;
        public Task<MotionStartResult> StartFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
            => Task.FromResult(new MotionStartResult("no"));
        public Task<MotionPollResult> PollFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
            => Task.FromResult(new MotionPollResult(MotionPollStatuses.Failed, providerJobId));
        public Task<MotionPollResult> RecoverFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
            => Task.FromResult(new MotionPollResult(MotionPollStatuses.Failed, providerJobId));
        public Task<MotionAdapterResult> FetchOutputAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
            => Task.FromResult(new MotionAdapterResult(providerJobId, [], "video/mp4", 0, "os/no"));
        public Task<MotionAdapterResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
            => Task.FromResult(new MotionAdapterResult("no", [], "video/mp4", 0, "os/no"));
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
}
