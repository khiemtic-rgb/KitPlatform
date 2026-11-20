using System.Security.Cryptography;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// P1 local production path. Fake transports only. No socket, no database, no secret file.
/// </summary>
public static class ProductionTenantEp01ProductionPathV1Regression
{
    public const string SuiteId = "FAMIXA_TENANT_EP01_PRODUCTION_PATH_V1";
    const string Spoken = "Dạ… nay con làm bài không tốt.";

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
        AssertComposition(Ok);
        await AssertVoicePath(Ok);
        await AssertOfficialVideoCopyRejected(Ok);
        await AssertSpeechRequired(Ok);
        await AssertMixSupersede(Ok);
        await AssertFixtureNotAccepted(Ok);
        await AssertOfficialBuildDoesNotResolveTenantCredential(Ok);
    }

    static void AssertComposition(Action<bool, string> Ok)
    {
        var runway = new RunwayMotionExecutionAdapter(new FakeRunwayTransport());
        var veoTransport = new FakeVeoMotionTransport();
        var veo = new VeoMotionExecutionAdapter(veoTransport);
        var fixture = new FixtureMotionAdapter();
        var resolver = new ProductionMotionAdapterResolver([runway, veo, fixture]);
        var selected = resolver.Resolve(ProductionMotionCatalog.GoogleProviderId, ProductionMotionCatalog.Veo31ModelId);
        var runwaySelected = resolver.Resolve(ProductionMotionCatalog.RunwayProviderId, ProductionMotionCatalog.RunwayGen4Turbo);
        Ok(selected is VeoMotionExecutionAdapter && runwaySelected is RunwayMotionExecutionAdapter,
            "tenant video stage selects Veo and Runway selects Runway");
        Ok(veoTransport.CreateCalls == 0, "adapter selection does not call a transport");
        Ok(!ProductionVeoReadinessComposition.ProductionResolverIncludesVeo
            && !OfficialVeoProductionAuthenticatorV1.ProductionDiRegistered,
            "official Veo execution lock stays closed");

        var root = RepoRoot();
        Ok(root is not null, "composition root file is readable");
        if (root is null) return;
        var text = File.ReadAllText(Path.Combine(root,
            "src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentPackDependencyInjection.cs"));
        var runwayAt = text.IndexOf("IRunwayTransport, HttpRunwayTransport", StringComparison.Ordinal);
        var veoAt = text.IndexOf("IMotionExecutionAdapter, VeoMotionExecutionAdapter", StringComparison.Ordinal);
        var fixtureAt = text.IndexOf("IMotionExecutionAdapter, FixtureMotionAdapter", StringComparison.Ordinal);
        Ok(runwayAt > 0 && !text.Contains("AddScoped<IRunwayTransport, DryRunRunwayTransport>", StringComparison.Ordinal),
            "Runway registration is the official HTTP client");
        Ok(veoAt > 0 && fixtureAt > veoAt, "Veo adapter is registered before the fixture motion adapter");
        Ok(text.Contains("ElevenLabsVoiceExecutionAdapter", StringComparison.Ordinal)
            && text.Contains("FalLipSyncExecutionAdapter", StringComparison.Ordinal)
            && text.Contains("TenantGeminiImageExecutionAdapter", StringComparison.Ordinal)
            && !text.Contains("IVoiceExecutionAdapter,", StringComparison.Ordinal)
            && !text.Contains("ILipSyncExecutionAdapter,", StringComparison.Ordinal),
            "paid voice and lipsync stay concrete; unpaid interfaces stay unregistered");
    }

    static async Task AssertVoicePath(Action<bool, string> Ok)
    {
        var harness = VoiceHarness();
        var shot = new ProductionScope(harness.Build, "EP01", "SC01", "EP01-SC01-SH02");
        harness.Store.RegisterBuild(harness.Build, harness.Tenant);
        var eleven = harness.Voice;
        Ok(await Throws(ProductionFailureReasons.PaidNetworkForbidden, () => harness.Svc.CreateVoiceAsync(Voice(shot, "no-permit"))),
            "voice without a permit does not start");
        Ok(eleven.SynthesizeCount == 0, "missing permit does not call ElevenLabs");

        await Issue(harness, shot, ProductionActions.CreateVoice, ProductionVoiceCatalog.ElevenLabsProviderId, ProductionVoiceCatalog.ElevenV3);
        harness.Ai.Resolved = harness.Ai.Resolved with { ProviderId = "runway", ModelId = "gen4_turbo", Credential = harness.Credential };
        Ok(await Throws(ProductionFailureReasons.ProviderModelMismatch, () => harness.Svc.CreateVoiceAsync(Voice(shot, "wrong-model"))),
            "voice rejects a non-elevenlabs resolution");
        Ok(eleven.SynthesizeCount == 0, "wrong model does not call ElevenLabs");
        harness.Ai.Resolved = harness.Ai.Resolved with
        {
            ProviderId = ProductionVoiceCatalog.ElevenLabsProviderId,
            ModelId = ProductionVoiceCatalog.ElevenV3,
            Credential = null,
        };
        Ok(await Throws(ProductionTenantAiReasons.CredentialRequired, () => harness.Svc.CreateVoiceAsync(Voice(shot, "no-credential"))),
            "voice without a tenant credential does not start");
        Ok(eleven.SynthesizeCount == 0 && await harness.Permits.GetIssuedAsync(
            harness.Build, shot.ShotCode, ProductionActions.CreateVoice,
            ProductionVoiceCatalog.ElevenLabsProviderId, ProductionVoiceCatalog.ElevenV3) is not null,
            "credential failure leaves the permit unused");
        harness.Ai.Resolved = harness.Ai.Resolved with { Credential = harness.Credential };

        var first = await harness.Svc.CreateVoiceAsync(Voice(shot, "voice-1"));
        Ok(first.Task.ProviderId == ProductionVoiceCatalog.ElevenLabsProviderId
            && first.Task.ModelId == ProductionVoiceCatalog.ElevenV3
            && first.Artifact.Role == ProductionArtifactRoles.Voice
            && first.Artifact.VerificationStatus == ProductionArtifactStatuses.Verified,
            "tenant voice uses ElevenLabs and persists a verified artifact");
        var snap = await harness.Store.GetSnapshotByTaskAsync(first.Task.TaskId);
        Ok(snap?.CredentialId == harness.Credential.CredentialId
            && snap.CredentialVersionId == harness.Credential.CredentialVersionId
            && eleven.LastRequest?.Credential == harness.Credential
            && eleven.SynthesizeCount == 1,
            "credential is resolved before the one transport call");
        var permits = await harness.Permits.ListByTargetAsync(
            harness.Build, shot.ShotCode, ProductionActions.CreateVoice,
            ProductionVoiceCatalog.ElevenLabsProviderId, ProductionVoiceCatalog.ElevenV3);
        Ok(permits.Count == 1 && permits[0].Status == ProductionExecutionPermitStatuses.Consumed,
            "voice permit is consumed once");

        var replay = await harness.Svc.CreateVoiceAsync(Voice(shot, "voice-1"));
        Ok(replay.Artifact.ArtifactId == first.Artifact.ArtifactId && eleven.SynthesizeCount == 1,
            "repeated voice execution reuses the artifact");
        Ok(await Throws(ProductionFailureReasons.PaidNetworkForbidden, () => harness.Svc.CreateVoiceAsync(Voice(shot, "voice-2"))),
            "a second voice fingerprint is rejected after the permit is consumed");
        Ok(eleven.SynthesizeCount == 1, "rejected repeat does not call ElevenLabs");
    }

    static async Task AssertOfficialVideoCopyRejected(Action<bool, string> Ok)
    {
        var harness = LipHarness(ProductionEp01Sh02LipsyncCanon.LineId);
        var shot = new ProductionScope(harness.Build, "EP01", "SC01", "EP01-SC01-SH02");
        var video = await harness.Svc.RegisterExistingVideoTakeAsync(
            new RegisterExistingVideoTakeCommand(shot, Guid.Parse("b22370a9-6ddd-4b04-a281-c7498cc926ae"), "test"));
        var voice = await harness.Svc.RegisterExistingVoiceAsync(
            new RegisterExistingVoiceCommand(shot, Guid.Parse("bf8d0827-21ff-4daa-83b6-116bc3324ab0"), "test"));
        await SeedSpeech(harness.Snaps, shot, video.DurationSec);
        await Issue(harness.Permits, harness.Build, shot.ShotCode, ProductionActions.CreateLipsync,
            ProductionLipsyncCatalog.FalProviderId, ProductionLipsyncCatalog.SyncLipsyncV3);
        var ownLip = await harness.Svc.CreateLipsyncAsync(Lip(shot, video.ArtifactId, voice.ArtifactId));
        Ok(ownLip.Artifact.Role == ProductionArtifactRoles.Lipsync && harness.Fal.StartCount == 1,
            "a tenant video with a different SHA can still be lip-synced");
        var copyId = Guid.Parse("aa000001-aa00-4aa0-8aa0-aa0000000001");
        await harness.Store.InsertArtifactAsync(new ProductionArtifact(
            copyId, video.TaskId, video.AttemptId, ProductionArtifactRoles.VideoTake,
            ProductionTestMp4.Mime, ProductionEp01Sh02LipsyncCanon.VideoBytes, 8m,
            ProductionEp01Sh02LipsyncCanon.VideoSha256,
            "11111111111141118111111111111111/tenant-sh02-official-copy", null, DateTimeOffset.UtcNow,
            ProductionArtifactStatuses.Verified, video.InputSnapshotHash));
        Ok(await Throws(ProductionFailureReasons.BindingChanged, () => harness.Svc.CreateLipsyncAsync(
            Lip(shot, copyId, voice.ArtifactId))),
            "tenant SH02 lipsync rejects the official video SHA on a different artifact id");
        Ok(harness.Fal.StartCount == 1, "rejected official copy does not call Fal");
        var offeredLip = await harness.Os.EvaluateCreateLipsyncAsync(shot);
        var offeredMix = await harness.Os.EvaluateCreateMixAsync(shot);
        Ok(offeredLip.Available && offeredMix.Available,
            "lipsync and mix stay offered while the current video is not the official copy");
        await harness.Os.BindNamedAsync(copyId, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake));
        var hiddenLip = await harness.Os.EvaluateCreateLipsyncAsync(shot);
        var hiddenMix = await harness.Os.EvaluateCreateMixAsync(shot);
        Ok(!hiddenLip.Available && hiddenLip.BlockReason == ProductionFailureReasons.BindingChanged
            && !hiddenMix.Available && hiddenMix.BlockReason == ProductionFailureReasons.BindingChanged,
            "official video copy is not offered for lipsync or mix");
        Ok(await Throws(ProductionFailureReasons.BindingChanged, () => harness.Svc.MixVideoAsync(Mix(shot, ownLip.Artifact.ArtifactId, "5"))),
            "tenant SH02 mix rejects the official video SHA");
        Ok(harness.Ffmpeg.ComposeExecutionCount == 0, "rejected mix does not run FFmpeg");
    }

    static async Task AssertSpeechRequired(Action<bool, string> Ok)
    {
        var harness = LipHarness();
        var shot = new ProductionScope(harness.Build, "EP01", "SC01", "EP01-SC01-SH01");
        var video = await harness.Svc.RegisterExistingVideoTakeAsync(
            new RegisterExistingVideoTakeCommand(shot, Guid.Parse("b22370a9-6ddd-4b04-a281-c7498cc926ae"), "test"));
        var voice = await harness.Svc.RegisterExistingVoiceAsync(
            new RegisterExistingVoiceCommand(shot, Guid.Parse("bf8d0827-21ff-4daa-83b6-116bc3324ab0"), "test"));
        await Issue(harness.Permits, harness.Build, shot.ShotCode, ProductionActions.CreateLipsync,
            ProductionLipsyncCatalog.FalProviderId, ProductionLipsyncCatalog.SyncLipsyncV3);
        Ok(await Throws(ProductionFailureReasons.MissingTimeline, () => harness.Svc.CreateLipsyncAsync(
            Lip(shot, video.ArtifactId, voice.ArtifactId))),
            "tenant Fal without a director speech interval is rejected");
        Ok(harness.Fal.StartCount == 0, "missing speech interval does not call Fal");
    }

    static async Task AssertMixSupersede(Action<bool, string> Ok)
    {
        var harness = LipHarness();
        var shot = new ProductionScope(harness.Build, "EP01", "SC01", "EP01-SC01-SH01");
        var video = await harness.Svc.RegisterExistingVideoTakeAsync(
            new RegisterExistingVideoTakeCommand(shot, Guid.Parse("b22370a9-6ddd-4b04-a281-c7498cc926ae"), "test"));
        var voice = await harness.Svc.RegisterExistingVoiceAsync(
            new RegisterExistingVoiceCommand(shot, Guid.Parse("bf8d0827-21ff-4daa-83b6-116bc3324ab0"), "test"));
        await SeedSpeech(harness.Snaps, shot, video.DurationSec);
        await Issue(harness.Permits, harness.Build, shot.ShotCode, ProductionActions.CreateLipsync,
            ProductionLipsyncCatalog.FalProviderId, ProductionLipsyncCatalog.SyncLipsyncV3);
        var lip = await harness.Svc.CreateLipsyncAsync(Lip(shot, video.ArtifactId, voice.ArtifactId));
        var first = await harness.Svc.MixVideoAsync(Mix(shot, lip.Artifact.ArtifactId, "5"));
        var oldBytes = harness.Files.Read(first.Artifact.StorageKey);
        var oldSha = first.Artifact.ByteSha256;
        var accept = await harness.Svc.AcceptOsAsync(Accept(shot, first.Artifact.ArtifactId));
        Ok(accept.Artifact.ArtifactId == first.Artifact.ArtifactId, "the first mix can be the OS accepted artifact");
        var second = await harness.Svc.MixVideoAsync(Mix(shot, lip.Artifact.ArtifactId, "8"));
        var kept = await harness.Store.GetArtifactAsync(first.Artifact.ArtifactId);
        var current = await harness.Store.GetActiveBindingAsync(
            harness.Build, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix));
        var accepted = await harness.Store.GetActiveBindingAsync(
            harness.Build, shot.ShotCode, ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Mix));
        var reread = harness.Files.Read(first.Artifact.StorageKey);
        Ok(second.Artifact.ArtifactId != first.Artifact.ArtifactId
            && current?.ArtifactId == second.Artifact.ArtifactId
            && accepted?.ArtifactId == first.Artifact.ArtifactId
            && kept is not null
            && kept.ByteSha256 == oldSha
            && reread is not null
            && oldBytes is not null
            && reread.SequenceEqual(oldBytes)
            && harness.Ffmpeg.ComposeExecutionCount == 2
            && harness.FixtureMix.Calls == 0,
            "a new mix supersedes CURRENT:MIX and leaves the old artifact and its accept binding");
        var secondAccepted = await harness.Store.GetActiveBindingAsync(
            harness.Build, shot.ShotCode, ProductionBindingRoles.Accepted);
        Ok(secondAccepted?.ArtifactId == first.Artifact.ArtifactId,
            "superseding CURRENT:MIX does not accept the new file");
    }

    static async Task AssertFixtureNotAccepted(Action<bool, string> Ok)
    {
        var tenant = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var caller = new SwitchTenant { Id = tenant };
        var store = new InMemoryProductionWorkStore(caller);
        var files = new InMemoryProductionArtifactFileStore(caller);
        var build = Guid.Parse("2615b2da-da9e-486b-9372-8927784df40f");
        store.RegisterBuild(build, tenant);
        var shot = new ProductionScope(build, "EP01", "SC01", "EP01-SC01-SH04");
        var svc = new ProductionCreateVideoService(
            new ProductionExecutionService(store, new GuardMotion()),
            store, files, new GuardMotion(),
            voice: new FixtureVoiceAdapter(),
            targetShots: new Shots(build, shot.ShotCode));
        var created = await svc.CreateVoiceAsync(new ProductionActionApiCommand(
            shot,
            new ShotExecutionInput("paper action", "beat", "visual", "", "", "5", "", "",
                LineId: "line-sh04", SpeakerCharacterId: "CHAR-003", VoiceId: "voice-sh04"),
            "test", "fixture-voice", AuthorizePaidNetwork: false));
        Ok(created.Task.ProviderId == ProductionVoiceCatalog.FixtureProviderId, "unpaid voice stays on the fixture");
        Ok(await Throws(ProductionFailureReasons.FailedIdentity, () => svc.AcceptOsAsync(Accept(shot, created.Artifact.ArtifactId))),
            "fixture output cannot be accepted as production media");
        Ok(await store.GetActiveBindingAsync(build, shot.ShotCode, ProductionBindingRoles.Accepted) is null
            && await store.GetActiveBindingAsync(build, shot.ShotCode, ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Voice)) is null,
            "rejected fixture accept writes no ACCEPTED binding");
    }

    static async Task AssertOfficialBuildDoesNotResolveTenantCredential(Action<bool, string> Ok)
    {
        var harness = VoiceHarness();
        var official = new ProductionScope(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH02");
        var counts = new CountingResolver(harness.Resolver);
        var fal = new FakeFalLipsyncTransport();
        var eleven = new FakeElevenLabsVoiceTransport();
        var svc = new ProductionCreateVideoService(
            new ProductionExecutionService(harness.Store, new GuardMotion(), credentials: counts),
            harness.Store, harness.Files, new GuardMotion(),
            officialPermits: harness.Permits,
            officialVoice: new ElevenLabsVoiceExecutionAdapter(eleven),
            officialLipsync: new FalLipSyncExecutionAdapter(fal),
            tenantAi: harness.Ai,
            targetShots: new Shots(harness.Build, official.ShotCode));
        Ok(!await harness.AiReal.GovernsAsync(official), "official build is outside tenant AI governance");
        Ok(await Throws(ProductionFailureReasons.MissingPerformanceSnapshot, () => svc.CreateVoiceAsync(
            Voice(official, "official-voice") with { Credential = harness.Credential })),
            "official voice does not run on the tenant path");
        Ok(await Throws(ProductionFailureReasons.MissingVideoArtifact, () => svc.CreateLipsyncAsync(
            Lip(official, Guid.NewGuid(), Guid.NewGuid()) with { Credential = harness.Credential })),
            "official lipsync does not run on the tenant path");
        Ok(counts.Resolves == 0 && eleven.SynthesizeCount == 0 && fal.StartCount == 0,
            "official build does not resolve or spend the tenant credential");
    }

    static VoiceBox VoiceHarness()
    {
        var tenant = Guid.Parse("4028941b-49b3-4ba2-8815-778daef4a2d3");
        var caller = new SwitchTenant { Id = tenant };
        var store = new InMemoryProductionWorkStore(caller);
        var files = new InMemoryProductionArtifactFileStore(caller);
        var build = Guid.Parse("30ba4061-a443-4c0d-85aa-1d13ff4fdedb");
        store.RegisterBuild(build, tenant);
        var key = new byte[ProductionCredentialSecretEnvelope.KeySize];
        RandomNumberGenerator.Fill(key);
        var ring = new MapProductionCredentialKeyRing("v1", new Dictionary<string, byte[]> { ["v1"] = key });
        var credentials = new InMemoryProductionCredentialStore(caller);
        var secrets = new InMemoryProductionCredentialSecretStore(caller, credentials, ring);
        var rotation = new InMemoryProductionCredentialRotation(credentials, secrets, ring);
        var registration = new ProductionCredentialRegistrationService(caller, credentials, rotation);
        var registered = registration.RegisterAsync(new RegisterProductionCredentialCommand(
            ProductionVoiceCatalog.ElevenLabsProviderId, ProductionVoiceCatalog.ElevenV3, "tenant-voice-secret", "voice")).GetAwaiter().GetResult();
        var credential = new ProductionCredentialReference(registered.CredentialId, registered.CredentialVersionId);
        var resolver = new ProductionCredentialResolver(credentials, secrets, caller);
        var permits = new InMemoryProductionExecutionPermitStore();
        var eleven = new FakeElevenLabsVoiceTransport();
        var ai = new ScriptedAi(new ProductionResolvedAi(
            ProductionTenantAiCapabilities.Voice,
            ProductionVoiceCatalog.ElevenLabsProviderId,
            ProductionVoiceCatalog.ElevenV3,
            credential,
            "tenant"));
        var configs = new InMemoryProductionTenantAiConfigStore(caller);
        var real = new ProductionTenantAiResolution(
            configs,
            new ProductionTenantAiCatalog(new ProductionMotionAdapterResolver([new FixtureMotionAdapter()])),
            credentials);
        var svc = new ProductionCreateVideoService(
            new ProductionExecutionService(store, new GuardMotion(), credentials: resolver),
            store, files, new GuardMotion(),
            officialPermits: permits,
            officialVoice: new ElevenLabsVoiceExecutionAdapter(eleven),
            tenantAi: ai,
            targetShots: new Shots(build, "EP01-SC01-SH02", "EP01-SC01-SH04"));
        return new VoiceBox(tenant, build, store, files, permits, svc, eleven, ai, real, resolver, credential);
    }

    static LipBox LipHarness(string voiceLineId = "line-SC01-CHAR-003-1")
    {
        var tenant = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var caller = new SwitchTenant { Id = tenant };
        var store = new InMemoryProductionWorkStore(caller);
        var files = new InMemoryProductionArtifactFileStore(caller);
        var build = Guid.Parse("2615b2da-da9e-486b-9372-8927784df40f");
        store.RegisterBuild(build, tenant);
        var falPin = ProductionOfficialHarnessCredentialFixture.Pin(
            tenant,
            Guid.Parse("f42b0002-f42b-4f42-8f42-f42b00000002"),
            Guid.Parse("f42c0003-f42c-4f42-8f42-f42c00000003"),
            ProductionLipsyncCatalog.FalProviderId,
            ProductionLipsyncCatalog.SyncLipsyncV3,
            "path-fal");
        var permits = new InMemoryProductionExecutionPermitStore();
        var snaps = new InMemoryPerformanceSnapshotStore();
        var fal = new FakeFalLipsyncTransport();
        var ffmpeg = new FakeFfmpegMixTransport();
        var fixtureMix = new GuardMix();
        var os = new ProductionExecutionService(store, new GuardMotion(), credentials: falPin.Resolver);
        var svc = new ProductionCreateVideoService(
            os, store, files, new GuardMotion(),
            officialPermits: permits,
            lipsync: new FixtureLipSyncAdapter(),
            mix: fixtureMix,
            officialLipsync: new FalLipSyncExecutionAdapter(fal),
            officialMix: new FfmpegMixExecutionAdapter(ffmpeg),
            historicalVideoTakes: new OneVideo(Take("b22370a9-6ddd-4b04-a281-c7498cc926ae")),
            historicalVoices: new OneVoice(VoiceTake("bf8d0827-21ff-4daa-83b6-116bc3324ab0", voiceLineId)),
            targetShots: new Shots(build, "EP01-SC01-SH01", "EP01-SC01-SH02"),
            performanceSnapshots: snaps);
        return new LipBox(build, store, files, permits, snaps, os, svc, fal, ffmpeg, fixtureMix);
    }

    static async Task Issue(VoiceBox harness, ProductionScope shot, string action, string provider, string model) =>
        await Issue(harness.Permits, harness.Build, shot.ShotCode, action, provider, model);

    static Task Issue(InMemoryProductionExecutionPermitStore permits, Guid build, string shot, string action, string provider, string model) =>
        permits.IssueAsync(new IssueProductionExecutionPermitCommand(
            build, shot, "EP01", "SC01", action, provider, model, "test", shot + ":" + action));

    static async Task SeedSpeech(InMemoryPerformanceSnapshotStore snaps, ProductionScope shot, decimal duration)
    {
        var perf = new PerformanceContractService(snaps);
        var draft = perf.CreateDraft(ProductionTestPerformanceFixture.Source(shot.BuildId, shot.ShotCode));
        await perf.FreezeAsync(draft, "regression", new PerformanceSourceRef(
            true, [true], false, null, duration,
            ProductionDirectorTiming.Source, ProductionDirectorTiming.DecisionType,
            SpeechStartSec: 0m, SpeechEndSec: duration));
    }

    static ProductionActionApiCommand Voice(ProductionScope scope, string idem) =>
        new(scope, new ShotExecutionInput(
            Spoken, "beat", "visual", "", "", "8", "", "",
            LineId: "line-SC01-CHAR-001-2", SpeakerCharacterId: "CHAR-001", VoiceId: "ywBZEqUhld86Jeajq94o"),
            "test", idem, AuthorizePaidNetwork: true);

    static ProductionActionApiCommand Lip(ProductionScope scope, Guid videoId, Guid voiceId)
    {
        var sh02 = string.Equals(scope.ShotCode, "EP01-SC01-SH02", StringComparison.Ordinal);
        return new(scope, new ShotExecutionInput(
            "Recovered line", "beat", "visual", "", "", "5", "", "",
            LineId: sh02 ? ProductionEp01Sh02LipsyncCanon.LineId : "line-SC01-CHAR-003-1",
            SpeakerCharacterId: sh02 ? ProductionEp01Sh02LipsyncCanon.SpeakerCharacterId : "CHAR-003",
            MotionArtifactId: videoId, VoiceArtifactId: voiceId),
            "test", null, AuthorizePaidNetwork: true);
    }

    static ProductionActionApiCommand Mix(ProductionScope scope, Guid lipsyncId, string timing) =>
        new(scope, new ShotExecutionInput(
            "Recovered line", "beat", "visual", "", "", timing, "", "",
            LipsyncArtifactId: lipsyncId),
            "test", null);

    static ProductionActionApiCommand Accept(ProductionScope scope, Guid artifactId) =>
        new(scope, new ShotExecutionInput(
            "Recovered line", "beat", "visual", "", "", "5", "", "",
            AcceptArtifactId: artifactId),
            "test", "accept-" + artifactId.ToString("N"));

    static UnattributedVideoTake Take(string id)
    {
        var bytes = ProductionTestMp4.Encode("path-video", 5);
        return new UnattributedVideoTake(
            Guid.Parse(id), ProductionArtifactRoles.VideoTake, ProductionTestMp4.Mime,
            KitVideoIntegrityRules.Sha256Hex(bytes), 5, "runway", "gen4_turbo", bytes);
    }

    static UnattributedVoice VoiceTake(string id, string lineId = "line-SC01-CHAR-003-1")
    {
        var bytes = new byte[64];
        bytes[0] = 0x49; bytes[1] = 0x44; bytes[2] = 0x33;
        return new UnattributedVoice(
            Guid.Parse(id), ProductionArtifactRoles.Voice, ProductionVoicePhysical.MpegMime,
            KitVideoIntegrityRules.Sha256Hex(bytes), 4.232m, "elevenlabs", "eleven_v3", Spoken, lineId, bytes);
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

    static string? RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName,
                "src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentPackDependencyInjection.cs");
            if (File.Exists(candidate)) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    sealed record VoiceBox(
        Guid Tenant,
        Guid Build,
        InMemoryProductionWorkStore Store,
        InMemoryProductionArtifactFileStore Files,
        InMemoryProductionExecutionPermitStore Permits,
        ProductionCreateVideoService Svc,
        FakeElevenLabsVoiceTransport Voice,
        ScriptedAi Ai,
        ProductionTenantAiResolution AiReal,
        ProductionCredentialResolver Resolver,
        ProductionCredentialReference Credential);

    sealed record LipBox(
        Guid Build,
        InMemoryProductionWorkStore Store,
        InMemoryProductionArtifactFileStore Files,
        InMemoryProductionExecutionPermitStore Permits,
        InMemoryPerformanceSnapshotStore Snaps,
        ProductionExecutionService Os,
        ProductionCreateVideoService Svc,
        FakeFalLipsyncTransport Fal,
        FakeFfmpegMixTransport Ffmpeg,
        GuardMix FixtureMix);

    sealed class ScriptedAi(ProductionResolvedAi resolved) : IProductionTenantAiResolution
    {
        public ProductionResolvedAi Resolved = resolved;
        public Task<bool> GovernsAsync(ProductionScope scope, CancellationToken ct = default) =>
            Task.FromResult(!ProductionScopeAnchorRules.IsOfficialFilm(scope.BuildId));
        public Task<ProductionResolvedAi> ResolveAsync(ProductionScope scope, string capability, CancellationToken ct = default) =>
            Task.FromResult(Resolved);
    }

    sealed class CountingResolver(IProductionCredentialResolver inner) : IProductionCredentialResolver
    {
        public int Resolves;
        public Task<ProductionCredentialMaterial> ResolveAsync(Guid credentialId, Guid credentialVersionId, string providerId, string modelId, CancellationToken ct = default)
        {
            Resolves++;
            return inner.ResolveAsync(credentialId, credentialVersionId, providerId, modelId, ct);
        }
        public Task<ProductionCredentialMaterial> ResolveActiveAsync(Guid tenantId, string providerId, string modelId, CancellationToken ct = default)
        {
            Resolves++;
            return inner.ResolveActiveAsync(tenantId, providerId, modelId, ct);
        }
        public Task<ProductionCredentialMaterial> ResolveActiveAsync(string providerId, string modelId, CancellationToken ct = default)
        {
            Resolves++;
            return inner.ResolveActiveAsync(providerId, modelId, ct);
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

    sealed class OneVideo(UnattributedVideoTake take) : IUnattributedVideoTakeSource
    {
        public Task<UnattributedVideoTake?> ReadAsync(Guid artifactId, CancellationToken ct = default) =>
            Task.FromResult<UnattributedVideoTake?>(take.ArtifactId == artifactId ? take : null);
    }

    sealed class OneVoice(UnattributedVoice voice) : IUnattributedVoiceSource
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
