using System.Security.Cryptography;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionTenantEp01LipsyncV1Regression
{
    public const string SuiteId = "FAMIXA_TENANT_EP01_LIPSYNC_V1";

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
        var fixture = new GuardLipSync();
        var fal = new FakeFalLipsyncTransport();
        var build = Guid.Parse("2615b2da-da9e-486b-9372-8927784df40f");
        store.RegisterBuild(build, tenantA);
        var shots = new Shots(build, "EP01-SC01-SH01", "EP01-SC01-SH02");
        var key = new byte[ProductionCredentialSecretEnvelope.KeySize];
        RandomNumberGenerator.Fill(key);
        var ring = new MapProductionCredentialKeyRing("v1", new Dictionary<string, byte[]> { ["v1"] = key });
        var credentialStore = new InMemoryProductionCredentialStore(caller);
        var secrets = new InMemoryProductionCredentialSecretStore(caller, credentialStore, ring);
        var rotation = new InMemoryProductionCredentialRotation(credentialStore, secrets, ring);
        var registration = new ProductionCredentialRegistrationService(caller, credentialStore, rotation);
        var permits = new InMemoryProductionExecutionPermitStore();
        var sandbox = new RecordingSandboxPermit();
        var snaps = new InMemoryPerformanceSnapshotStore();
        var svc = new ProductionCreateVideoService(
            new ProductionExecutionService(store, motion, credentials: new ProductionCredentialResolver(credentialStore, secrets, caller)),
            store, files, motion,
            permit: sandbox,
            officialPermits: permits,
            lipsync: fixture,
            officialLipsync: new FalLipSyncExecutionAdapter(fal),
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
        var bareBefore = await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.Current);
        ProductionActionApiCommand Sh01Lipsync(string? idem = null) =>
            Lipsync(sh01, video.ArtifactId, voice.ArtifactId, "line-SC01-CHAR-003-1", "CHAR-003", idem);
        Task Issue(Guid buildId, string shot, string action, string provider, string model) =>
            permits.IssueAsync(new IssueProductionExecutionPermitCommand(
                buildId, shot, "EP01", "SC01", action, provider, model, "test", $"{shot}:{action}:{provider}:{model}"));
        const string fal3 = ProductionLipsyncCatalog.SyncLipsyncV3;
        var falId = ProductionLipsyncCatalog.FalProviderId;

        Ok(await Throws(ProductionFailureReasons.PaidNetworkForbidden, () => svc.CreateLipsyncAsync(Sh01Lipsync())),
            "no permit: paid network forbidden");
        await Issue(build, sh01.ShotCode, ProductionActions.CreateLipsync, falId, "fal-ai/sync-lipsync");
        await Issue(build, sh01.ShotCode, ProductionActions.CreateLipsync, "runway", "gen4_turbo");
        await Issue(build, sh01.ShotCode, ProductionActions.CreateVideo, falId, fal3);
        Ok(await Throws(ProductionFailureReasons.PaidNetworkForbidden, () => svc.CreateLipsyncAsync(Sh01Lipsync())),
            "wrong provider/model/action permit rejected");
        await Issue(build, "EP01-SC01-SH02", ProductionActions.CreateLipsync, falId, fal3);
        await Issue(Guid.Parse("44444444-4444-4444-8444-444444444444"), sh01.ShotCode, ProductionActions.CreateLipsync, falId, fal3);
        Ok(await Throws(ProductionFailureReasons.PaidNetworkForbidden, () => svc.CreateLipsyncAsync(Sh01Lipsync())),
            "other shot/build permit rejected");
        Ok(fal.StartCount == 0, "permit rejections never reach Fal");

        await Issue(build, sh01.ShotCode, ProductionActions.CreateLipsync, falId, fal3);
        Ok(await Throws(ProductionCredentialResolver.Unresolved, () => svc.CreateLipsyncAsync(Sh01Lipsync("t3-no-credential"))),
            "permit without credential: unresolved");
        Ok(await permits.GetIssuedAsync(build, sh01.ShotCode, ProductionActions.CreateLipsync, falId, fal3) is not null,
            "unresolved credential does not consume permit");
        var falA = await registration.RegisterAsync(new RegisterProductionCredentialCommand(falId, fal3, "fal-a-secret", "fal-a"));
        var falB = await registration.RegisterAsync(new RegisterProductionCredentialCommand(falId, fal3, "fal-b-secret", "fal-b"));
        Ok(await Throws(ProductionCredentialResolver.Ambiguous, () => svc.CreateLipsyncAsync(Sh01Lipsync("t4-ambiguous"))),
            "permit with two credentials: ambiguous");
        Ok(await permits.GetIssuedAsync(build, sh01.ShotCode, ProductionActions.CreateLipsync, falId, fal3) is not null,
            "ambiguous credential does not consume permit");
        Ok(fal.StartCount == 0, "credential failures never reach Fal");
        await rotation.RevokeVersionAsync(falB.CredentialVersionId);
        var falCredential = new ProductionCredentialReference(falA.CredentialId, falA.CredentialVersionId);

        var first = await svc.CreateLipsyncAsync(Sh01Lipsync());
        var afterFirst = await permits.ListByTargetAsync(build, sh01.ShotCode, ProductionActions.CreateLipsync, falId, fal3);
        Ok(afterFirst.Count == 1 && afterFirst[0].Status == ProductionExecutionPermitStatuses.Consumed,
            "matching permit consumed exactly once");
        Ok(fal.StartCount == 1, "consumed permit reaches Fal once");
        Ok(first.Artifact.Role == ProductionArtifactRoles.Lipsync, "role LIPSYNC");
        Ok(first.Artifact.VerificationStatus == ProductionArtifactStatuses.Verified, "verified");
        Ok(first.Artifact.ArtifactId != ProductionEp01Sh01LipsyncCanon.VideoArtifactId
            && first.Artifact.ArtifactId != ProductionEp01Sh01LipsyncCanon.VoiceArtifactId, "new artifact");
        Ok(first.Task.ProviderId == ProductionLipsyncCatalog.FalProviderId
            && first.Task.ModelId == ProductionLipsyncCatalog.SyncLipsyncV3, "provider fal sync-lipsync-v3");
        Ok(first.Attempt.ProviderJobId is not null && first.Attempt.ProviderJobId.StartsWith("fal-fake-", StringComparison.Ordinal), "provider job id");
        Ok(fal.LastRequest?.VideoBytes is { Length: >= 32 } && fal.LastRequest.VoiceBytes is { Length: >= 32 }, "physical parents sent");
        var frozen = await store.GetSnapshotByTaskAsync(first.Task.TaskId);
        Ok(frozen?.CredentialId == falCredential.CredentialId
            && frozen.CredentialVersionId == falCredential.CredentialVersionId, "tenant Fal credential pinned");
        Ok(fal.LastRequest?.Credential == falCredential, "transport receives frozen Fal credential");

        var recovery = new CountingNetworkFal();
        var recoveryAdapter = new FalLipSyncExecutionAdapter(recovery);
        Ok(frozen is not null && await Throws(ProductionCredentialResolver.Unresolved, () => recoveryAdapter.RecoverFrozenAsync(
            frozen with { CredentialId = null, CredentialVersionId = null }, "fal-net-job")),
            "recovery without frozen credential fails closed");
        Ok(recovery.BareRecoveries == 0 && recovery.PinnedRecoveries == 0, "no credential-less recovery call");
        if (frozen is not null)
            await recoveryAdapter.RecoverFrozenAsync(frozen, "fal-net-job");
        Ok(recovery.BareRecoveries == 0 && recovery.PinnedRecoveries == 1
            && recovery.LastCredential == falCredential, "recovery uses frozen credential");
        var lip = await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync));
        Ok(lip is not null && lip.ArtifactId == first.Artifact.ArtifactId, "CURRENT:LIPSYNC");
        var videoBind = await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake));
        var voiceBind = await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice));
        var bare = await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.Current);
        Ok(videoBind?.ArtifactId == video.ArtifactId && voiceBind?.ArtifactId == voice.ArtifactId && bare?.ArtifactId == bareBefore?.ArtifactId,
            "video and voice current bindings unchanged");
        Ok(fixture.Calls == 0, "fixture lipsync adapter not called");

        var again = await svc.CreateLipsyncAsync(Sh01Lipsync());
        Ok(again.Artifact.ArtifactId == first.Artifact.ArtifactId && fal.StartCount == 1, "replay reuses the artifact and does not start again");
        var afterReplay = await permits.ListByTargetAsync(build, sh01.ShotCode, ProductionActions.CreateLipsync, falId, fal3);
        Ok(afterReplay.Count == 1 && afterReplay[0].Status == ProductionExecutionPermitStatuses.Consumed,
            "replay does not consume another permit");
        Ok((await store.ListTasksByScopeAsync(build, sh01.ShotCode))
            .Count(t => t.Action == ProductionActions.CreateLipsync && t.Status == ProductionTaskStatuses.Succeeded) == 1,
            "one succeeded lipsync task");
        Ok(sandbox.Calls == 0, "sandbox permit never consulted");

        var mix = await new ProductionExecutionService(store, motion).EvaluateCreateMixAsync(sh01);
        Ok(mix.Available
            && mix.VideoArtifactId == video.ArtifactId
            && mix.LipsyncArtifactId == first.Artifact.ArtifactId
            && mix.BlockReason != ProductionFailureReasons.MissingVideoArtifact
            && mix.BlockReason != ProductionFailureReasons.MissingVoiceArtifact
            && mix.BlockReason != ProductionFailureReasons.MissingLipsyncArtifact, "mix evaluator sees parents");

        Ok(await Throws(ProductionFailureReasons.MissingDependency, () => svc.CreateLipsyncAsync(Lipsync(
            sh01, ProductionEp01Sh01LipsyncCanon.VideoArtifactId, voice.ArtifactId, "line-SC01-CHAR-003-1", "CHAR-003"))),
            "historical video parent not selected");
        Ok(fal.StartCount == 1, "rejected historical parent does not start");

        caller.Id = tenantB;
        Ok(await store.GetArtifactAsync(first.Artifact.ArtifactId) is null, "other tenant cannot read the lipsync artifact");
        caller.Id = tenantA;
        Ok(motion.Calls == 0, "motion adapter not called");
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

    static ProductionActionApiCommand Lipsync(
        ProductionScope scope, Guid videoId, Guid voiceId, string lineId, string speaker, string? idem = null) =>
        new(scope, new ShotExecutionInput(
            "Recovered line", "beat", "visual", "", "", "5", "", "",
            null, "", "16:9", 5, ShotExecutionCompiler.Version, null,
            lineId, speaker, "", videoId, voiceId),
            "test", idem, AuthorizePaidNetwork: true);

    static UnattributedVideoTake VideoTake(string id)
    {
        var bytes = ProductionTestMp4.Encode("tenant-lipsync-video", 5);
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

    sealed class RecordingSandboxPermit : IProductionPaidNetworkPermit
    {
        public int Calls;
        public int StartCount => 0;

        public bool IsEligible(ProductionScope scope, string shotCode, string providerId, string modelId)
        {
            Calls++;
            return false;
        }

        public bool TryConsumeStart(ProductionScope scope, string shotCode, string providerId, string modelId)
        {
            Calls++;
            return false;
        }
    }

    sealed class CountingNetworkFal : IFalLipsyncTransport
    {
        public int BareRecoveries;
        public int PinnedRecoveries;
        public ProductionCredentialReference? LastCredential;
        public bool RequiresNetwork => true;
        public int StartCount => 0;

        public Task<FalLipsyncTransportResult> ExecuteOnceAsync(FalLipsyncTransportRequest request, CancellationToken ct = default) =>
            throw new InvalidOperationException("START_FORBIDDEN");

        public Task<FalLipsyncTransportResult> RecoverOnceAsync(string providerJobId, CancellationToken ct = default)
        {
            BareRecoveries++;
            return Task.FromResult(Recovered(providerJobId));
        }

        public Task<FalLipsyncTransportResult> RecoverOnceAsync(
            string providerJobId, ProductionCredentialReference credential, CancellationToken ct = default)
        {
            PinnedRecoveries++;
            LastCredential = credential;
            return Task.FromResult(Recovered(providerJobId));
        }

        static FalLipsyncTransportResult Recovered(string providerJobId) =>
            new(providerJobId, ProductionTestMp4.Encode("tenant-lipsync-recovered", 5), ProductionTestMp4.Mime);
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
        public int Calls;
        public string ProviderId => "fixture";
        public string ModelId => "fixture-lipsync-v1";
        public bool IsPaid => false;
        public bool RequiresNetwork => false;
        public Task<ProductionMediaResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new ProductionMediaResult("no", [], "video/mp4", 0));
        }
    }
}
