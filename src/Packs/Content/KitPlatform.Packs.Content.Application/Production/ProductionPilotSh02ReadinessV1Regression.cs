using System.Security.Cryptography;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// P2 local readiness for one Pilot SH02 pass. Fake transports only.
/// A green result is not a live provider call and not a human accept.
/// The speech interval used here is a test fixture, not the missing director window.
/// </summary>
public static class ProductionPilotSh02ReadinessV1Regression
{
    public const string SuiteId = "FAMIXA_PILOT_SH02_LOCAL_READINESS_V1";
    public static readonly Guid TenantId = Guid.Parse("4028941b-49b3-4ba2-8815-778daef4a2d3");
    public static readonly Guid BuildId = Guid.Parse("30ba4061-a443-4c0d-85aa-1d13ff4fdedb");
    public static readonly Guid OfficialBuildId = Guid.Parse("4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f");
    public const string ShotCode = "EP01-SC01-SH02";

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
        await AssertImageGates(Ok);
        await AssertLockedMasterGate(Ok);
        await AssertVideoGates(Ok);
        await AssertChain(Ok);
        await AssertProviderFailureDoesNotRetry(Ok);
        await AssertOfficialAndMissingTenant(Ok);
    }

    static void AssertComposition(Action<bool, string> Ok)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? root = null;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName,
                "src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentPackDependencyInjection.cs");
            if (File.Exists(candidate)) { root = candidate; break; }
            dir = dir.Parent;
        }
        Ok(root is not null, "composition root is readable");
        if (root is null) return;
        var text = File.ReadAllText(root);
        Ok(text.Contains("AddScoped<IRunwayTransport, HttpRunwayTransport>", StringComparison.Ordinal)
            && !text.Contains("AddScoped<IRunwayTransport, DryRunRunwayTransport>", StringComparison.Ordinal),
            "Runway transport is the official HTTP client");
        Ok(text.Contains("IMotionExecutionAdapter, VeoMotionExecutionAdapter", StringComparison.Ordinal)
            && text.Contains("ElevenLabsVoiceExecutionAdapter", StringComparison.Ordinal)
            && text.Contains("FalLipSyncExecutionAdapter", StringComparison.Ordinal)
            && text.Contains("TenantGeminiImageExecutionAdapter", StringComparison.Ordinal)
            && !text.Contains("IVoiceExecutionAdapter,", StringComparison.Ordinal)
            && !text.Contains("ILipSyncExecutionAdapter,", StringComparison.Ordinal),
            "pilot adapters are registered and unpaid voice or lipsync interfaces stay unregistered");
    }

    static async Task AssertImageGates(Action<bool, string> Ok)
    {
        var box = await Box();
        var shot = Shot();
        Ok(await Throws(ProductionFailureReasons.PaidNetworkForbidden, () => box.Svc.CreateImageAsync(Image(shot, "no-permit"))),
            "still without a permit does not start");
        Ok(box.Gemini.GenerationCount == 0, "missing still permit does not call Gemini");

        await Issue(box, ProductionActions.CreateImage, ProductionImageCatalog.GeminiProviderId, ProductionImageCatalog.GeminiFlashImage);
        box.Ai.Set(ProductionTenantAiCapabilities.Image, box.Ai.Image with { ModelId = "other-model" });
        Ok(await Throws(ProductionTenantAiReasons.ModelNotExecutable, () => box.Svc.CreateImageAsync(Image(shot, "wrong-model"))),
            "still rejects a model the Gemini adapter does not run");
        Ok(box.Gemini.GenerationCount == 0, "wrong still model does not call Gemini");
        box.Ai.Set(ProductionTenantAiCapabilities.Image, box.Ai.Image with
        {
            ModelId = ProductionImageCatalog.GeminiFlashImage,
            Credential = null,
        });
        Ok(await Throws(ProductionTenantAiReasons.CredentialRequired, () => box.Svc.CreateImageAsync(Image(shot, "no-credential"))),
            "still without a tenant credential does not start");
        Ok(box.Gemini.GenerationCount == 0
            && await box.Permits.GetIssuedAsync(BuildId, ShotCode, ProductionActions.CreateImage,
                ProductionImageCatalog.GeminiProviderId, ProductionImageCatalog.GeminiFlashImage) is not null,
            "still credential failure leaves the permit unused");
    }

    static async Task AssertVideoGates(Action<bool, string> Ok)
    {
        var box = await Box();
        var shot = Shot();
        box.Ai.Govern = false;
        Ok(await Throws(ProductionTenantAiReasons.ConfigurationRequired, () => box.Svc.CreateVideoAsync(Video(shot, null, null, "ungoverned"))),
            "tenant video without AI governance does not start");
        Ok(box.VeoTransport.CreateCalls == 0, "ungoverned video does not call Veo");
        box.Ai.Govern = true;

        box.Ai.Set(ProductionTenantAiCapabilities.Video, box.Ai.Video with
        {
            ProviderId = ProductionMotionCatalog.RunwayProviderId,
            ModelId = ProductionMotionCatalog.RunwayGen4Turbo,
        });
        Ok(await Throws(ProductionFailureReasons.ProviderModelMismatch, () => box.Svc.CreateVideoAsync(
            Video(shot, null, null, "wrong-model") with
            {
                ProviderId = ProductionMotionCatalog.GoogleProviderId,
                ModelId = ProductionMotionCatalog.Veo31ModelId,
            })),
            "video rejects a resolution that is not veo-3.1");
        Ok(box.VeoTransport.CreateCalls == 0, "wrong video model does not call Veo");
        box.Ai.Set(ProductionTenantAiCapabilities.Video, box.Ai.Video with
        {
            ProviderId = ProductionMotionCatalog.GoogleProviderId,
            ModelId = ProductionMotionCatalog.Veo31ModelId,
            Credential = null,
        });
        await Issue(box, ProductionActions.CreateVideo, ProductionMotionCatalog.GoogleProviderId, ProductionMotionCatalog.Veo31ModelId);
        Ok(await Throws(ProductionTenantAiReasons.CredentialRequired, () => box.Svc.CreateVideoAsync(Video(shot, null, null, "no-credential"))),
            "video without a tenant credential does not start");
        Ok(box.VeoTransport.CreateCalls == 0
            && await box.Permits.GetIssuedAsync(BuildId, ShotCode, ProductionActions.CreateVideo,
                ProductionMotionCatalog.GoogleProviderId, ProductionMotionCatalog.Veo31ModelId) is not null,
            "video credential failure leaves the permit unused");
        box.Ai.Set(ProductionTenantAiCapabilities.Video, box.Ai.Video with
        {
            ProviderId = ProductionMotionCatalog.GoogleProviderId,
            ModelId = ProductionMotionCatalog.Veo31ModelId,
            Credential = box.VeoCredential,
        });

        var closed = await Box();
        closed.Ai.Set(ProductionTenantAiCapabilities.Video, closed.Ai.Video);
        Ok(await Throws(ProductionFailureReasons.PaidNetworkForbidden, () => closed.Svc.CreateVideoAsync(
            Video(Shot(), null, null, "no-permit") with { AuthorizePaidNetwork = false })),
            "video without authorization does not start");
        Ok(closed.VeoTransport.CreateCalls == 0, "unauthorized video does not call Veo");
    }

    static async Task AssertChain(Action<bool, string> Ok)
    {
        var box = await Box();
        var shot = Shot();
        await Issue(box, ProductionActions.CreateImage, ProductionImageCatalog.GeminiProviderId, ProductionImageCatalog.GeminiFlashImage);
        await Issue(box, ProductionActions.CreateVideo, ProductionMotionCatalog.GoogleProviderId, ProductionMotionCatalog.Veo31ModelId);
        await Issue(box, ProductionActions.CreateVoice, ProductionVoiceCatalog.ElevenLabsProviderId, ProductionVoiceCatalog.ElevenV3);
        await Issue(box, ProductionActions.CreateLipsync, ProductionLipsyncCatalog.FalProviderId, ProductionLipsyncCatalog.SyncLipsyncV3);

        var image = await box.Svc.CreateImageAsync(Image(shot, "still-1"));
        Ok(image.Artifact.Role == ProductionArtifactRoles.Keyframe
            && image.Task.ProviderId == ProductionImageCatalog.GeminiProviderId
            && image.Task.ModelId == ProductionImageCatalog.GeminiFlashImage
            && box.Gemini.GenerationCount == 1
            && box.Gemini.LastModel == ProductionImageCatalog.GeminiFlashImage
            && !string.IsNullOrWhiteSpace(box.Gemini.LastKey),
            "still uses gemini-3.1-flash-image once and pins a secret");
        var imageReplay = await box.Svc.CreateImageAsync(Image(shot, "still-1"));
        Ok(imageReplay.Artifact.ArtifactId == image.Artifact.ArtifactId && box.Gemini.GenerationCount == 1,
            "the same still fingerprint reuses the artifact");
        Ok(await Throws(ProductionFailureReasons.PaidNetworkForbidden, () => box.Svc.CreateImageAsync(Image(shot, "still-2"))),
            "a second still fingerprint is rejected");
        Ok(box.Gemini.GenerationCount == 1, "rejected still fingerprint does not call Gemini");

        var video = await box.Svc.CreateVideoAsync(Video(shot, image.Artifact.ArtifactId, image.Artifact.ByteSha256, "video-1"));
        var videoSnap = await box.Store.GetSnapshotByTaskAsync(video.Task.TaskId);
        Ok(video.Artifact.Role == ProductionArtifactRoles.VideoTake
            && video.Task.ProviderId == ProductionMotionCatalog.GoogleProviderId
            && video.Task.ModelId == ProductionMotionCatalog.Veo31ModelId
            && videoSnap?.CredentialId == box.VeoCredential.CredentialId
            && videoSnap.ParentArtifactIds?.Contains(image.Artifact.ArtifactId) == true
            && box.VeoTransport.CreateCalls == 1
            && box.VeoTransport.LastCreateRequest?.GenerateAudio == false
            && box.VeoTransport.LastCreateRequest.Resolution == "720p"
            && box.VeoTransport.LastCreateRequest.Credential == box.VeoCredential,
            "video uses veo-3.1 once, parents the still, and pins the tenant credential");
        var videoReplay = await box.Svc.CreateVideoAsync(Video(shot, image.Artifact.ArtifactId, image.Artifact.ByteSha256, "video-1"));
        Ok(videoReplay.Artifact.ArtifactId == video.Artifact.ArtifactId && box.VeoTransport.CreateCalls == 1,
            "the same video fingerprint reuses the artifact");
        Ok(await Throws(ProductionFailureReasons.PaidNetworkForbidden, () => box.Svc.CreateVideoAsync(Video(shot, image.Artifact.ArtifactId, image.Artifact.ByteSha256, "video-2"))),
            "a second video fingerprint is rejected");
        Ok(box.VeoTransport.CreateCalls == 1, "rejected video fingerprint does not call Veo");

        var voice = await box.Svc.CreateVoiceAsync(Voice(shot, "voice-1"));
        Ok(voice.Artifact.Role == ProductionArtifactRoles.Voice
            && voice.Task.ProviderId == ProductionVoiceCatalog.ElevenLabsProviderId
            && voice.Task.ModelId == ProductionVoiceCatalog.ElevenV3
            && box.Eleven.SynthesizeCount == 1
            && box.Eleven.LastRequest?.Credential == box.VoiceCredential,
            "voice uses eleven_v3 once and pins the tenant credential");
        Ok(await Throws(ProductionFailureReasons.MissingTimeline, () => box.Svc.CreateLipsyncAsync(Lip(shot, video.Artifact.ArtifactId, voice.Artifact.ArtifactId))),
            "lipsync without a director speech interval is rejected");
        Ok(box.Fal.StartCount == 0, "missing speech interval does not call Fal");

        await SeedSpeech(box.Snaps, shot, video.Artifact.DurationSec);
        var lip = await box.Svc.CreateLipsyncAsync(Lip(shot, video.Artifact.ArtifactId, voice.Artifact.ArtifactId));
        var lipSnap = await box.Store.GetSnapshotByTaskAsync(lip.Task.TaskId);
        Ok(lip.Artifact.Role == ProductionArtifactRoles.Lipsync
            && lip.Task.ProviderId == ProductionLipsyncCatalog.FalProviderId
            && lip.Task.ModelId == ProductionLipsyncCatalog.SyncLipsyncV3
            && lipSnap?.ParentArtifactIds?.Contains(video.Artifact.ArtifactId) == true
            && lipSnap.ParentArtifactIds.Contains(voice.Artifact.ArtifactId)
            && lipSnap.CredentialId == box.FalCredential.CredentialId
            && box.Fal.StartCount == 1,
            "lipsync uses sync-lipsync-v3 once and parents the video and voice");
        var lipReplay = await box.Svc.CreateLipsyncAsync(Lip(shot, video.Artifact.ArtifactId, voice.Artifact.ArtifactId));
        Ok(lipReplay.Artifact.ArtifactId == lip.Artifact.ArtifactId && box.Fal.StartCount == 1,
            "the same lipsync fingerprint reuses the artifact");

        var mix = await box.Svc.MixVideoAsync(Mix(shot, lip.Artifact.ArtifactId));
        var currentMix = await box.Store.GetActiveBindingAsync(
            BuildId, ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix));
        var preview = await box.Svc.GetPreviewAsync(shot);
        var take = await box.Svc.GetAuthoritativeVideoTakeAsync(shot);
        Ok(mix.Artifact.Role == ProductionArtifactRoles.Mix
            && currentMix?.ArtifactId == mix.Artifact.ArtifactId
            && preview.ArtifactId == mix.Artifact.ArtifactId
            && preview.Role == ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix)
            && take?.ArtifactId == video.Artifact.ArtifactId
            && box.Ffmpeg.ComposeExecutionCount == 1
            && box.VeoTransport.CreateCalls == 1
            && box.Eleven.SynthesizeCount == 1
            && box.Fal.StartCount == 1
            && box.Gemini.GenerationCount == 1,
            "preview plays CURRENT:MIX and the video take stays the video artifact");
        var mixReplay = await box.Svc.MixVideoAsync(Mix(shot, lip.Artifact.ArtifactId));
        Ok(mixReplay.Artifact.ArtifactId == mix.Artifact.ArtifactId && box.Ffmpeg.ComposeExecutionCount == 1,
            "the same mix fingerprint does not compose again");
        var attempts = await box.Store.ListAttemptsAsync(video.Task.TaskId);
        Ok(attempts.Count == 1, "the video stage keeps one attempt");
    }

    static async Task AssertProviderFailureDoesNotRetry(Action<bool, string> Ok)
    {
        var imageBox = await Box();
        imageBox.Gemini.Fail = true;
        await Issue(imageBox, ProductionActions.CreateImage, ProductionImageCatalog.GeminiProviderId, ProductionImageCatalog.GeminiFlashImage);
        Ok(await Throws(ProductionFailureReasons.ProviderOutputMissing, () => imageBox.Svc.CreateImageAsync(Image(Shot(), "still-fail"))),
            "a Gemini rejection is stored as a provider failure");
        Ok(await Throws(ProductionFailureReasons.PaidNetworkForbidden, () => imageBox.Svc.CreateImageAsync(Image(Shot(), "still-fail"))),
            "the failed still fingerprint is not sent again");
        var imageTask = (await imageBox.Store.ListTasksByRequestAsync(
            (await imageBox.Store.GetRequestByIdempotencyAsync(Image(Shot(), "still-fail").IdempotencyKey!))!.RequestId)).Last();
        var imageAttempts = await imageBox.Store.ListAttemptsAsync(imageTask.TaskId);
        Ok(imageBox.Gemini.GenerationCount == 1 && imageAttempts.Count == 1
            && imageTask.Status == ProductionTaskStatuses.Failed,
            "the failed still keeps one attempt");

        var videoBox = await Box();
        videoBox.VeoTransport.Scenario = FakeVeoScenario.Failed;
        await Issue(videoBox, ProductionActions.CreateImage, ProductionImageCatalog.GeminiProviderId, ProductionImageCatalog.GeminiFlashImage);
        await Issue(videoBox, ProductionActions.CreateVideo, ProductionMotionCatalog.GoogleProviderId, ProductionMotionCatalog.Veo31ModelId);
        var still = await videoBox.Svc.CreateImageAsync(Image(Shot(), "still-for-fail"));
        Ok(await Throws(ProductionFailureReasons.ProviderUnavailable, () => videoBox.Svc.CreateVideoAsync(Video(Shot(), still.Artifact.ArtifactId, still.Artifact.ByteSha256, "video-fail"))),
            "a Veo rejection is stored as a provider failure");
        Ok(await Throws(ProductionFailureReasons.PaidNetworkForbidden, () => videoBox.Svc.CreateVideoAsync(
            Video(Shot(), still.Artifact.ArtifactId, still.Artifact.ByteSha256, "video-fail") with { RetryAttempt = true })),
            "an explicit retry of the failed video fingerprint does not start another attempt");
        var videoRequest = await videoBox.Store.GetRequestByIdempotencyAsync("video-fail");
        var videoTask = (await videoBox.Store.ListTasksByRequestAsync(videoRequest!.RequestId)).Last();
        var videoAttempts = await videoBox.Store.ListAttemptsAsync(videoTask.TaskId);
        Ok(videoBox.VeoTransport.CreateCalls == 1 && videoAttempts.Count == 1,
            "the failed video keeps one provider start");
    }

    static async Task AssertOfficialAndMissingTenant(Action<bool, string> Ok)
    {
        var box = await Box();
        var official = new ProductionScope(OfficialBuildId, "EP01", "SC01", ShotCode);
        Ok(!await box.Ai.GovernsAsync(official), "official build is outside tenant AI governance");
        Ok(await Throws(ProductionFailureReasons.MissingPerformanceSnapshot, () => box.Svc.CreateVoiceAsync(Voice(official, "official-voice"))),
            "official voice does not run on the tenant path");
        Ok(box.Eleven.SynthesizeCount == 0 && box.VeoTransport.CreateCalls == 0 && box.Fal.StartCount == 0 && box.Gemini.GenerationCount == 0,
            "official build does not call a pilot transport");

        var missing = new InMemoryProductionWorkStore(new EmptyTenant());
        Ok(await Throws(ProductionTenantBoundary.Required, () => missing.GetRequestByIdempotencyAsync("probe")),
            "a missing caller tenant is rejected before a store read");
        Ok(ThrowsSync(ProductionTenantBoundary.Unattributed, () => ProductionBuildTenantGate.Require(TenantId, null)),
            "a NULL build tenant is not given a fallback");
    }

    static ProductionScope Shot() => new(BuildId, "EP01", "SC01", ShotCode);

    static async Task AssertLockedMasterGate(Action<bool, string> Ok)
    {
        var missing = await Box(new NullMasters());
        await Issue(missing, ProductionActions.CreateImage, ProductionImageCatalog.GeminiProviderId, ProductionImageCatalog.GeminiFlashImage);
        Ok(await Throws(ProductionLockedStillReference.Missing, () => missing.Svc.CreateImageAsync(Image(Shot(), "missing-lock"))),
            "a paid still without an official locked master is rejected");
        Ok(missing.Gemini.GenerationCount == 0 && missing.Gemini.ReferenceCalls == 0,
            "the missing lock does not call Gemini");
        var stillIssued = await missing.Permits.GetIssuedAsync(
            BuildId, ShotCode, ProductionActions.CreateImage, ProductionImageCatalog.GeminiProviderId, ProductionImageCatalog.GeminiFlashImage);
        Ok(stillIssued?.Status == ProductionExecutionPermitStatuses.Issued,
            "the missing lock does not consume the image permit");

        var sh03 = new ProductionScope(BuildId, "EP01", "SC01", "EP01-SC01-SH03");
        Ok(await Throws(ProductionLockedStillReference.Missing, () => missing.Svc.CreateImageAsync(Image(sh03, "sh03-approved-line"))),
            "SH03 with the approved line reaches the locked-master gate");
        Ok(missing.Gemini.GenerationCount == 0, "SH03 does not call Gemini without a master");

        var locked = await Box(new FixedMasters());
        await Issue(locked, ProductionActions.CreateImage, ProductionImageCatalog.GeminiProviderId, ProductionImageCatalog.GeminiFlashImage);
        var created = await locked.Svc.CreateImageAsync(Image(Shot(), "locked-still"));
        var snap = await locked.Store.GetSnapshotByTaskAsync(created.Task.TaskId);
        Ok(locked.Gemini.ReferenceCalls == 1 && locked.Gemini.GenerationCount == 0,
            "a locked still sends the master bytes and does not use the text-only call");
        Ok(snap is not null
            && locked.Gemini.LastReferences is { Count: > 0 }
            && locked.Gemini.LastReferences.All(row =>
                row.Bytes.Length > 32
                && ProductionLockedStillReference.Bound(snap.Prompt, row.CharacterId + ":" + row.Sha256)),
            "the frozen prompt contains every sent master sha");
        var consumed = await locked.Permits.GetIssuedAsync(
            BuildId, ShotCode, ProductionActions.CreateImage, ProductionImageCatalog.GeminiProviderId, ProductionImageCatalog.GeminiFlashImage);
        Ok(consumed is null, "a valid locked still consumes the issued permit once");
    }

    static async Task<PilotBox> Box(IProductionLockedMasterSource? masters = null)
    {
        var caller = new SwitchTenant(TenantId);
        var store = new InMemoryProductionWorkStore(caller);
        var files = new InMemoryProductionArtifactFileStore(caller);
        store.RegisterBuild(BuildId, TenantId);
        var key = new byte[ProductionCredentialSecretEnvelope.KeySize];
        RandomNumberGenerator.Fill(key);
        var ring = new MapProductionCredentialKeyRing("v1", new Dictionary<string, byte[]> { ["v1"] = key });
        var credentials = new InMemoryProductionCredentialStore(caller);
        var secrets = new InMemoryProductionCredentialSecretStore(caller, credentials, ring);
        var rotation = new InMemoryProductionCredentialRotation(credentials, secrets, ring);
        var registration = new ProductionCredentialRegistrationService(caller, credentials, rotation);
        var geminiCredential = await Register(registration, ProductionImageCatalog.GeminiProviderId, ProductionImageCatalog.GeminiFlashImage, "gemini-secret");
        var veoCredential = await Register(registration, ProductionMotionCatalog.GoogleProviderId, ProductionMotionCatalog.Veo31ModelId, "veo-secret");
        var voiceCredential = await Register(registration, ProductionVoiceCatalog.ElevenLabsProviderId, ProductionVoiceCatalog.ElevenV3, "voice-secret");
        var falCredential = await Register(registration, ProductionLipsyncCatalog.FalProviderId, ProductionLipsyncCatalog.SyncLipsyncV3, "fal-secret");
        var resolver = new ProductionCredentialResolver(credentials, secrets, caller);
        var ai = new StageAi(geminiCredential, veoCredential, voiceCredential, falCredential);
        var gemini = new ScriptGemini();
        var veoTransport = new FakeVeoMotionTransport(requiresNetwork: true) { SucceedImmediately = true };
        var veo = new VeoMotionExecutionAdapter(veoTransport);
        var eleven = new FakeElevenLabsVoiceTransport();
        var fal = new FakeFalLipsyncTransport();
        var ffmpeg = new FakeFfmpegMixTransport();
        var snaps = new InMemoryPerformanceSnapshotStore();
        var permits = new InMemoryProductionExecutionPermitStore();
        var os = new ProductionExecutionService(store, veo, credentials: resolver, tenantAi: ai, performance: snaps);
        var svc = new ProductionCreateVideoService(
            os, store, files, veo,
            resolver: new ProductionMotionAdapterResolver([veo, new RunwayMotionExecutionAdapter(new FakeRunwayTransport())]),
            officialPermits: permits,
            officialVoice: new ElevenLabsVoiceExecutionAdapter(eleven),
            lipsync: new FixtureLipSyncAdapter(),
            officialLipsync: new FalLipSyncExecutionAdapter(fal),
            mix: new FixtureMixAdapter(),
            officialMix: new FfmpegMixExecutionAdapter(ffmpeg),
            tenantAi: ai,
            tenantImage: new TenantGeminiImageExecutionAdapter(gemini, resolver, masters),
            lockedMasters: masters,
            performanceSnapshots: snaps,
            targetShots: new Shots(BuildId, ShotCode));
        return new PilotBox(store, permits, snaps, svc, ai, gemini, veoTransport, veoCredential, eleven, voiceCredential, fal, falCredential, ffmpeg);
    }

    static async Task<ProductionCredentialReference> Register(
        ProductionCredentialRegistrationService registration, string provider, string model, string secret)
    {
        var registered = await registration.RegisterAsync(new RegisterProductionCredentialCommand(provider, model, secret, "pilot"));
        return new ProductionCredentialReference(registered.CredentialId, registered.CredentialVersionId);
    }

    static Task Issue(PilotBox box, string action, string provider, string model) =>
        box.Permits.IssueAsync(new IssueProductionExecutionPermitCommand(
            BuildId, ShotCode, "EP01", "SC01", action, provider, model, "test", ShotCode + ":" + action));

    static async Task SeedSpeech(InMemoryPerformanceSnapshotStore snaps, ProductionScope shot, decimal duration)
    {
        var perf = new PerformanceContractService(snaps);
        var draft = perf.CreateDraft(ProductionTestPerformanceFixture.Source(shot.BuildId, shot.ShotCode));
        await perf.FreezeAsync(draft, "regression", new PerformanceSourceRef(
            true, [true], false, null, duration,
            ProductionDirectorTiming.Source, ProductionDirectorTiming.DecisionType,
            SpeechStartSec: 0m, SpeechEndSec: duration));
    }

    static ProductionActionApiCommand Image(ProductionScope scope, string idem) =>
        new(scope, new ShotExecutionInput(
            ProductionEp01Sh02IngressCanon.Story,
            ShortProductionQualityPipelineV1.Sh02FutureActing,
            ProductionEp01Sh02IngressCanon.VisualContract,
            "", "", "8", "", ""),
            "test", idem, AuthorizePaidNetwork: true);

    static CreateVideoApiCommand Video(ProductionScope scope, Guid? keyframeId, string? keyframeSha, string idem) =>
        new(scope, new ShotExecutionInput(
            ProductionEp01Sh02IngressCanon.Story,
            ShortProductionQualityPipelineV1.Sh02FutureActing,
            ProductionEp01Sh02IngressCanon.VisualContract,
            "", keyframeSha ?? "", "5", "", "",
            keyframeId, keyframeSha ?? "", "16:9", 5m),
            "test", idem, null, null,
            AuthorizePaidNetwork: true);

    static ProductionActionApiCommand Voice(ProductionScope scope, string idem) =>
        new(scope, new ShotExecutionInput(
            ProductionEp01Sh02VoiceIngressCanon.LineText,
            ShortProductionQualityPipelineV1.Sh02FutureActing,
            ProductionEp01Sh02IngressCanon.VisualContract,
            "", "", "8", "", "",
            LineId: ProductionEp01Sh02VoiceIngressCanon.LineId,
            SpeakerCharacterId: ProductionEp01Sh02VoiceIngressCanon.SpeakerCharacterId,
            VoiceId: ProductionEp01Sh02VoiceIngressCanon.VoiceId),
            "test", idem, AuthorizePaidNetwork: true);

    static ProductionActionApiCommand Lip(ProductionScope scope, Guid videoId, Guid voiceId) =>
        new(scope, new ShotExecutionInput(
            ProductionEp01Sh02VoiceIngressCanon.LineText,
            ShortProductionQualityPipelineV1.Sh02FutureActing,
            ProductionEp01Sh02IngressCanon.VisualContract,
            "", "", "8", "", "",
            LineId: ProductionEp01Sh02LipsyncCanon.LineId,
            SpeakerCharacterId: ProductionEp01Sh02LipsyncCanon.SpeakerCharacterId,
            MotionArtifactId: videoId,
            VoiceArtifactId: voiceId),
            "test", null, AuthorizePaidNetwork: true);

    static ProductionActionApiCommand Mix(ProductionScope scope, Guid lipsyncId) =>
        new(scope, new ShotExecutionInput(
            ProductionEp01Sh02IngressCanon.Story,
            ShortProductionQualityPipelineV1.Sh02FutureActing,
            ProductionEp01Sh02IngressCanon.VisualContract,
            "", "", "8", "", "",
            LipsyncArtifactId: lipsyncId),
            "test", null);

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

    static bool ThrowsSync(string message, Action work)
    {
        try
        {
            work();
            return false;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message == message;
        }
    }

    sealed record PilotBox(
        InMemoryProductionWorkStore Store,
        InMemoryProductionExecutionPermitStore Permits,
        InMemoryPerformanceSnapshotStore Snaps,
        ProductionCreateVideoService Svc,
        StageAi Ai,
        ScriptGemini Gemini,
        FakeVeoMotionTransport VeoTransport,
        ProductionCredentialReference VeoCredential,
        FakeElevenLabsVoiceTransport Eleven,
        ProductionCredentialReference VoiceCredential,
        FakeFalLipsyncTransport Fal,
        ProductionCredentialReference FalCredential,
        FakeFfmpegMixTransport Ffmpeg);

    sealed class StageAi(
        ProductionCredentialReference image,
        ProductionCredentialReference video,
        ProductionCredentialReference voice,
        ProductionCredentialReference lipsync) : IProductionTenantAiResolution
    {
        public bool Govern = true;
        public ProductionResolvedAi Image = Resolved(ProductionTenantAiCapabilities.Image, ProductionImageCatalog.GeminiProviderId, ProductionImageCatalog.GeminiFlashImage, image);
        public ProductionResolvedAi Video = Resolved(ProductionTenantAiCapabilities.Video, ProductionMotionCatalog.GoogleProviderId, ProductionMotionCatalog.Veo31ModelId, video);
        public ProductionResolvedAi Voice = Resolved(ProductionTenantAiCapabilities.Voice, ProductionVoiceCatalog.ElevenLabsProviderId, ProductionVoiceCatalog.ElevenV3, voice);
        public ProductionResolvedAi Lipsync = Resolved(ProductionTenantAiCapabilities.Lipsync, ProductionLipsyncCatalog.FalProviderId, ProductionLipsyncCatalog.SyncLipsyncV3, lipsync);

        public void Set(string capability, ProductionResolvedAi value)
        {
            if (capability == ProductionTenantAiCapabilities.Image) Image = value;
            else if (capability == ProductionTenantAiCapabilities.Video) Video = value;
            else if (capability == ProductionTenantAiCapabilities.Voice) Voice = value;
            else Lipsync = value;
        }

        public Task<bool> GovernsAsync(ProductionScope scope, CancellationToken ct = default) =>
            Task.FromResult(Govern && !ProductionScopeAnchorRules.IsOfficialFilm(scope.BuildId));

        public Task<ProductionResolvedAi> ResolveAsync(ProductionScope scope, string capability, CancellationToken ct = default)
        {
            var hit = capability switch
            {
                ProductionTenantAiCapabilities.Image => Image,
                ProductionTenantAiCapabilities.Video => Video,
                ProductionTenantAiCapabilities.Voice => Voice,
                ProductionTenantAiCapabilities.Lipsync => Lipsync,
                _ => throw new InvalidOperationException(ProductionTenantAiReasons.UnknownCapability),
            };
            return Task.FromResult(hit);
        }

        static ProductionResolvedAi Resolved(string capability, string provider, string model, ProductionCredentialReference credential) =>
            new(capability, provider, model, credential, "tenant");
    }

    sealed class ScriptGemini : IGeminiTenantImageTransport
    {
        public int GenerationCount { get; private set; }
        public int ReferenceCalls { get; private set; }
        public IReadOnlyList<GeminiStillReference>? LastReferences { get; private set; }
        public bool Fail { get; set; }
        public string? LastModel { get; private set; }
        public string? LastKey { get; private set; }

        public Task<GeminiProductionStillResult> GenerateOnceAsync(
            string apiKey, string modelId, string prompt, string? aspectRatio, CancellationToken ct = default)
        {
            GenerationCount++;
            LastModel = modelId;
            LastKey = apiKey;
            if (Fail || string.IsNullOrWhiteSpace(apiKey))
                return Task.FromResult(new GeminiProductionStillResult(false, null, null, modelId, ProductionFailureReasons.ProviderOutputMissing));
            var bytes = new byte[64];
            RandomNumberGenerator.Fill(bytes);
            return Task.FromResult(new GeminiProductionStillResult(true, bytes, "image/png", modelId, null));
        }

        public Task<GeminiProductionStillResult> GenerateWithReferencesAsync(
            string apiKey, string modelId, string prompt, string? aspectRatio,
            IReadOnlyList<GeminiStillReference> references, CancellationToken ct = default)
        {
            ReferenceCalls++;
            LastReferences = references;
            if (references.Any(row => row.Bytes is not { Length: > 32 }))
                throw new InvalidOperationException(ProductionLockedStillReference.NotSent);
            var bytes = new byte[64];
            RandomNumberGenerator.Fill(bytes);
            return Task.FromResult(new GeminiProductionStillResult(true, bytes, "image/png", modelId, null));
        }
    }

    sealed class NullMasters : IProductionLockedMasterSource
    {
        public Task<GeminiStillReference?> ReadOfficialAsync(string characterId, string name, CancellationToken ct = default)
            => Task.FromResult<GeminiStillReference?>(null);
    }

    sealed class FixedMasters : IProductionLockedMasterSource
    {
        public Task<GeminiStillReference?> ReadOfficialAsync(string characterId, string name, CancellationToken ct = default)
        {
            var id = characterId.Trim().ToUpperInvariant();
            var bytes = new byte[40];
            for (var i = 0; i < bytes.Length; i++)
                bytes[i] = (byte)(id[i % id.Length] + i);
            return Task.FromResult<GeminiStillReference?>(
                new GeminiStillReference(id, name, "image/jpeg", KitVideoIntegrityRules.Sha256Hex(bytes), bytes));
        }
    }

    sealed class SwitchTenant(Guid id) : IProductionCallerTenant
    {
        public Guid RequireTenant() => ProductionTenantBoundary.Require(id);
    }

    sealed class EmptyTenant : IProductionCallerTenant
    {
        public Guid RequireTenant() => ProductionTenantBoundary.Require(Guid.Empty);
    }

    sealed class Shots(Guid buildId, params string[] codes) : IProductionTargetShotCheck
    {
        public Task RequireAsync(Guid build, string shotCode, CancellationToken ct = default)
        {
            if (build == buildId && !codes.Contains(shotCode, StringComparer.Ordinal))
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
            return Task.CompletedTask;
        }
    }
}
