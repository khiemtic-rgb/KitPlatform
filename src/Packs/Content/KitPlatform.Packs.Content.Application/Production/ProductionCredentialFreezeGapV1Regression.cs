using System.Security.Cryptography;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// PRODUCTION-CREDENTIAL-FREEZE-01 gap closure.
/// Official freeze and Veo authentication fail closed without the frozen credential pair.
/// </summary>
public static class ProductionCredentialFreezeGapV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_CREDENTIAL_FREEZE_01_GAP";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        try
        {
            RunAsync(Ok).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            fail.Add("UNCAUGHT:" + ex.GetType().Name + ":" + ex.Message);
        }

        return fail;
    }

    static async Task RunAsync(Action<bool, string> Ok)
    {
        var bytes = new byte[32];
        bytes[0] = 7;
        var hash = KitVideoIntegrityRules.Sha256Hex(bytes);
        var sources = OfficialCreateVideoFreezeV1.FromEp01Sh02Canon(bytes, hash);
        var bare = new InMemoryProductionWorkStore();
        var missing = false;
        try
        {
            await OfficialCreateVideoFreezeV1.FreezeAsync(bare, sources);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialResolver.Unresolved)
        {
            missing = true;
        }
        Ok(missing && await OfficialCreateVideoFreezeV1.CountOfficialCreateVideoSnapshotsAsync(bare) == 0,
            "T1 OfficialCreateVideoFreezeV1 without a credential reference fails before insert");

        var pin = OfficialCreateVideoFreezeCredentialFixture.Pin();
        var created = await OfficialCreateVideoFreezeV1.FreezeAsync(new InMemoryProductionWorkStore(), sources, pin);
        Ok(created.Created
           && created.Snapshot.CredentialId == pin.Reference.CredentialId
           && created.Snapshot.CredentialVersionId == pin.Reference.CredentialVersionId,
            "T2 OfficialCreateVideoFreezeV1 freezes both credential ids");

        var seen = new List<(Guid CredentialId, Guid VersionId)>();
        var rejecting = new RejectingResolver(seen);
        var bypass = new InMemoryProductionWorkStore();
        var rejected = false;
        try
        {
            await OfficialCreateVideoFreezeV1.FreezeAsync(
                bypass, sources, rejecting, new ProductionCredentialReference(Guid.NewGuid(), Guid.NewGuid()));
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialResolver.Unresolved)
        {
            rejected = true;
        }
        Ok(rejected && seen.Count == 1 && await OfficialCreateVideoFreezeV1.CountOfficialCreateVideoSnapshotsAsync(bypass) == 0,
            "T3 OfficialCreateVideoFreezeV1 cannot bypass resolver validation");

        var officialStore = new InMemoryProductionWorkStore();
        var official = ProductionWorkExecutionEngine.CreateOfficial(officialStore, null);
        var officialRequest = await official.CreateRequestAsync(
            Scope(), ProductionActions.CreateVideo, "gap", "gap-official-" + Guid.NewGuid().ToString("N"));
        var officialTask = await official.CreateTaskAsync(officialRequest.RequestId);
        var officialMissing = false;
        try
        {
            await official.FreezeInputAsync(officialTask.TaskId, Input(), FamixaProviderIds.Google, ProductionMotionCatalog.Veo31ModelId);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialResolver.Unresolved)
        {
            officialMissing = true;
        }
        var runwayMissing = false;
        try
        {
            await official.FreezeInputAsync(
                officialTask.TaskId, Input(), FamixaProviderIds.Runway, ProductionMotionCatalog.RunwayGen4Turbo);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialResolver.Unresolved)
        {
            runwayMissing = true;
        }
        var serviceStore = new InMemoryProductionWorkStore();
        var service = new ProductionExecutionService(serviceStore);
        var serviceRequest = await service.CreateRequestAsync(
            Scope(), ProductionActions.CreateVoice, "gap", "gap-service-" + Guid.NewGuid().ToString("N"));
        var serviceTask = await service.CreateTaskAsync(serviceRequest.RequestId);
        var serviceMissing = false;
        try
        {
            await service.FreezeInputAsync(
                serviceTask.TaskId, Input(), FamixaProviderIds.ElevenLabs, ProductionVoiceCatalog.ElevenV3);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialResolver.Unresolved)
        {
            serviceMissing = true;
        }
        Ok(officialMissing && runwayMissing && serviceMissing
           && await officialStore.GetSnapshotByTaskAsync(officialTask.TaskId) is null
           && await serviceStore.GetSnapshotByTaskAsync(serviceTask.TaskId) is null,
            "T4 official engine without a resolver cannot freeze a credential-bearing execution");

        var root = RepoRoot();
        var officialFiles = new[]
        {
            Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "Production", "OfficialCreateVideoFreezeV1.cs"),
            Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "Production", "ProductionWorkExecutionEngine.cs"),
            Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "Production", "Providers", "HttpVeoMotionTransport.cs"),
            Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "Production", "Providers", "ProductionVeoHttpAuthenticator.cs"),
            Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "Production", "Providers", "VeoMotionExecutionAdapter.cs"),
            Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "Production", "OfficialVeoHttpCompositionV1.cs"),
            Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "Production", "OfficialVeoRealVertexStartLiveV1.cs"),
            Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "Production", "OfficialVeoPollRecoverMaterializationLiveV1.cs"),
        };
        var source = string.Join('\n', officialFiles.Select(File.ReadAllText));
        Ok(!source.Contains("org_settings", StringComparison.Ordinal),
            "T5 official freeze and Veo paths do not read org_settings");
        Ok(!source.Contains("ContentVideoConfig", StringComparison.Ordinal)
           && !source.Contains("Content:Secrets", StringComparison.Ordinal)
           && !source.Contains("GetEnvironmentVariable", StringComparison.Ordinal)
           && !source.Contains("RUNWAY_API_KEY", StringComparison.Ordinal)
           && !source.Contains("FAL_KEY", StringComparison.Ordinal)
           && !source.Contains("ELEVENLABS_API_KEY", StringComparison.Ordinal)
           && !source.Contains("GOOGLE_APPLICATION_CREDENTIALS", StringComparison.Ordinal),
            "T6 official freeze and Veo paths do not use environment provider keys");
        Ok(!File.ReadAllText(officialFiles[6]).Contains("OfficialVeoJoinFileHttpAuthenticator", StringComparison.Ordinal)
           && !File.ReadAllText(officialFiles[7]).Contains("OfficialVeoJoinFileHttpAuthenticator", StringComparison.Ordinal),
            "T6 official Veo live paths do not select a Join account");

        var (resolver, credentialId, versionId, laterId) = await GooglePin();
        var calls = new List<(Guid CredentialId, Guid VersionId)>();
        var recording = new RecordingResolver(resolver, calls);
        var auth = new ProductionVeoHttpAuthenticator();
        var handler = new FakeVeoHttpMessageHandler();
        var transport = new HttpVeoMotionTransport(
            new HttpClient(handler), auth, new VeoHttpSettings("gap-project"), recording);
        var adapter = new VeoMotionExecutionAdapter(transport);
        var frozenVeo = Snap(
            FamixaProviderIds.Google, ProductionMotionCatalog.Veo31ModelId, hash, bytes, credentialId, versionId);
        var reached = false;
        try
        {
            await adapter.StartFrozenAsync(frozenVeo, new MotionStartContext(bytes, "image/png"), default);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.InvalidProviderResponse)
        {
            reached = true;
        }
        Ok(reached
           && auth.AcceptedCredentialId is null
           && auth.AcceptedCredentialVersionId is null
           && auth.GetAccessTokenCalls == 0
           && calls.Count == 1
           && calls[0].VersionId == versionId
           && calls[0].VersionId != laterId
           && transport.HttpCallCount == 0
           && handler.Requests.Count == 0,
            "T7 T8 Veo resolves the frozen credential version and rejects a non-service-account secret before HTTP");

        var missingAuth = new ProductionVeoHttpAuthenticator();
        var missingTransport = new HttpVeoMotionTransport(
            new HttpClient(new FakeVeoHttpMessageHandler()), missingAuth, new VeoHttpSettings("gap-project"), recording);
        var missingVeo = false;
        try
        {
            await new VeoMotionExecutionAdapter(missingTransport).StartFrozenAsync(
                Snap(FamixaProviderIds.Google, ProductionMotionCatalog.Veo31ModelId, hash, bytes, null, null),
                new MotionStartContext(bytes, "image/png"),
                default);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialResolver.Unresolved)
        {
            missingVeo = true;
        }
        Ok(missingVeo && missingAuth.GetAccessTokenCalls == 0 && missingTransport.HttpCallCount == 0,
            "T9 Veo missing credential fails before provider authentication");

        var runway = new RecordingRunway();
        var runwaySnap = Snap(
            ProductionMotionCatalog.RunwayProviderId, ProductionMotionCatalog.RunwayGen4Turbo, hash, bytes, credentialId, versionId);
        var runwayStart = await new RunwayMotionExecutionAdapter(runway).StartFrozenAsync(
            runwaySnap, new MotionStartContext(bytes, "image/png"), default);
        Ok(runwayStart.ProviderJobId.Length > 0
           && runway.Seen?.CredentialId == credentialId
           && runway.Seen.CredentialVersionId == versionId
           && runway.HttpCallCount == 0,
            "T10 Runway uses the frozen credential pair");

        var videoId = Guid.NewGuid();
        var voiceId = Guid.NewGuid();
        var speaker = new ProductionLipsyncSpeakerPin("CHAR-003", 0.63, 0.25, 1280, 720, 0);
        var fal = new RecordingFal();
        var falSnap = Snap(
            ProductionLipsyncCatalog.FalProviderId,
            ProductionLipsyncCatalog.SyncLipsyncV3,
            hash,
            bytes,
            credentialId,
            versionId,
            ProductionLipsyncFreeze.Token("L1", "CHAR-003", videoId, voiceId, speaker),
            [videoId, voiceId]);
        var falResult = await new FalLipSyncExecutionAdapter(fal).ExecuteFrozenAsync(
            falSnap,
            new LipsyncStartContext(Guid.NewGuid(), null, "EP01-SC01-SH01", new byte[32], new byte[32]),
            default);
        Ok(falResult.ProviderJobId.Length > 0
           && fal.Seen?.CredentialId == credentialId
           && fal.Seen.CredentialVersionId == versionId
           && fal.StartCount == 0,
            "T11 Fal uses the frozen credential pair");

        const string spoken = "Minh ngập ngừng, đặt cặp xuống ghế rồi chậm chạp kéo khóa.";
        var voice = new RecordingVoice();
        var voiceSnap = Snap(
            ProductionVoiceCatalog.ElevenLabsProviderId,
            ProductionVoiceCatalog.ElevenV3,
            hash,
            bytes,
            credentialId,
            versionId,
            ProductionVoiceFreeze.Token("L1", "CHAR-001", "EXAVITQu4vr4xnSDxMaL", spoken),
            story: spoken);
        var voiceResult = await new ElevenLabsVoiceExecutionAdapter(voice).ExecuteFrozenAsync(
            voiceSnap, new VoiceStartContext(Guid.NewGuid()), default);
        Ok(voiceResult.ProviderJobId.Length > 0
           && voice.Seen?.CredentialId == credentialId
           && voice.Seen.CredentialVersionId == versionId
           && voice.SynthesizeCount == 0,
            "T12 ElevenLabs uses the frozen credential pair");

        var historicalStore = new InMemoryProductionWorkStore();
        var historicalTask = await new ProductionWorkExecutionEngine(historicalStore).CreateTaskAsync(
            (await new ProductionWorkExecutionEngine(historicalStore).CreateRequestAsync(
                Scope(), ProductionActions.CreateVideo, "gap", "gap-hist-" + Guid.NewGuid().ToString("N"))).RequestId);
        var historical = created.Snapshot with
        {
            SnapshotId = Guid.NewGuid(),
            TaskId = historicalTask.TaskId,
            CredentialId = null,
            CredentialVersionId = null,
        };
        historical = historical with { SnapshotHash = ShotExecutionCompiler.SnapshotHash(historical) };
        await historicalStore.InsertSnapshotAsync(historical);
        var historicalUntouched = false;
        try
        {
            await OfficialCreateVideoFreezeV1.FreezeAsync(historicalStore, sources);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialResolver.Unresolved)
        {
            historicalUntouched = true;
        }
        var historicalLoaded = await historicalStore.GetSnapshotByTaskAsync(historicalTask.TaskId);
        Ok(historicalUntouched
           && historicalLoaded is { CredentialId: null, CredentialVersionId: null }
           && historicalLoaded.SnapshotHash == historical.SnapshotHash,
            "T13 historical snapshot remains unpinned");

        var holder = new InMemoryProductionWorkStore();
        await holder.InsertSnapshotAsync(created.Snapshot);
        var replaced = false;
        try
        {
            await holder.InsertSnapshotAsync(created.Snapshot with
            {
                SnapshotId = Guid.NewGuid(),
                CredentialVersionId = laterId,
            });
        }
        catch (InvalidOperationException ex) when (ex.Message == "SNAPSHOT_IMMUTABLE")
        {
            replaced = true;
        }
        Ok(replaced, "T14 frozen credential identity remains immutable");
        Ok(transport.HttpCallCount == 0 && runway.HttpCallCount == 0 && fal.StartCount == 0 && voice.SynthesizeCount == 0,
            "T19 no provider job is executed");
    }

    static async Task<(IProductionCredentialResolver Resolver, Guid CredentialId, Guid VersionId, Guid LaterId)> GooglePin()
    {
        var tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        var caller = new Caller(tenant);
        var key = new byte[ProductionCredentialSecretEnvelope.KeySize];
        RandomNumberGenerator.Fill(key);
        var ring = new MapProductionCredentialKeyRing("v1", new Dictionary<string, byte[]> { ["v1"] = key });
        var identities = new InMemoryProductionCredentialStore(caller);
        var secrets = new InMemoryProductionCredentialSecretStore(caller, identities, ring);
        var credentialId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var laterId = Guid.NewGuid();
        await identities.CreateAsync(ProductionCredentialOwnership.Create(credentialId, tenant, "gap-google"));
        await identities.AddVersionAsync(ProductionCredentialOwnership.CreateVersion(
            versionId, credentialId, tenant, 1, FamixaProviderIds.Google, ProductionMotionCatalog.Veo31ModelId));
        await secrets.PutAsync(versionId, "gap-veo-secret");
        await identities.AddVersionAsync(ProductionCredentialOwnership.CreateVersion(
            laterId, credentialId, tenant, 2, FamixaProviderIds.Google, ProductionMotionCatalog.Veo31ModelId));
        await secrets.PutAsync(laterId, "gap-veo-later");
        return (new ProductionCredentialResolver(identities, secrets, caller), credentialId, versionId, laterId);
    }

    static ProductionTaskInputSnapshot Snap(
        string provider,
        string model,
        string keyframeHash,
        byte[] bytes,
        Guid? credentialId,
        Guid? versionId,
        string? contract = null,
        IReadOnlyList<Guid>? parents = null,
        string? story = null)
    {
        _ = bytes;
        var text = story ?? "Minh ngập ngừng, đặt cặp xuống ghế rồi chậm chạp kéo khóa.";
        var snap = new ProductionTaskInputSnapshot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            text,
            "He stays in place.",
            "sofa 16:9",
            contract ?? "",
            text,
            text,
            ShotExecutionCompiler.Hash(text),
            ShotExecutionCompiler.Hash("He stays in place."),
            ShotExecutionCompiler.Hash("sofa 16:9"),
            ShotExecutionCompiler.Hash(text),
            keyframeHash,
            "",
            "",
            "",
            ShotExecutionCompiler.Hash("10.0s"),
            provider,
            model,
            "",
            DateTimeOffset.UtcNow,
            OfficialCreateVideoFreezeV1.OfficialKeyframeId,
            "16:9",
            10,
            ShotExecutionCompiler.Version,
            parents,
            CredentialId: credentialId,
            CredentialVersionId: versionId);
        return snap with { SnapshotHash = ShotExecutionCompiler.SnapshotHash(snap) };
    }

    static ProductionScope Scope() =>
        new(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH01");

    static ShotExecutionInput Input() =>
        new("Minh ngập ngừng, đặt cặp xuống ghế rồi chậm chạp kéo khóa.", "He stays in place.", "sofa 16:9", "CHAR-001", "kf-d", "5s", "", "hold");

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "KitPlatform.Api", "KitPlatform.Api.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("REPO_ROOT_MISSING");
    }

    sealed class Caller(Guid id) : IProductionCallerTenant
    {
        public Guid RequireTenant() => ProductionTenantBoundary.Require(id);
    }

    sealed class RejectingResolver(List<(Guid CredentialId, Guid VersionId)> seen) : IProductionCredentialResolver
    {
        public Task<ProductionCredentialMaterial> ResolveAsync(
            Guid credentialId, Guid credentialVersionId, string providerId, string modelId, CancellationToken ct = default)
        {
            seen.Add((credentialId, credentialVersionId));
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
        }
    }

    sealed class RecordingResolver(IProductionCredentialResolver inner, List<(Guid CredentialId, Guid VersionId)> seen) : IProductionCredentialResolver
    {
        public Task<ProductionCredentialMaterial> ResolveAsync(
            Guid credentialId, Guid credentialVersionId, string providerId, string modelId, CancellationToken ct = default)
        {
            seen.Add((credentialId, credentialVersionId));
            return inner.ResolveAsync(credentialId, credentialVersionId, providerId, modelId, ct);
        }
    }

    sealed class RecordingRunway : IRunwayTransport
    {
        public ProductionCredentialReference? Seen { get; private set; }
        public bool RequiresNetwork => false;
        public int HttpCallCount => 0;

        public Task<RunwayTransportCreateResult> CreateImageToVideoAsync(RunwayTransportCreateRequest request, CancellationToken ct = default)
        {
            Seen = request.Credential;
            return Task.FromResult(new RunwayTransportCreateResult("local-frozen-" + request.TaskId.ToString("N")));
        }

        public Task<RunwayTransportTaskResult> GetTaskAsync(string providerJobId, CancellationToken ct = default) =>
            Task.FromResult(new RunwayTransportTaskResult(providerJobId, MotionPollStatuses.Succeeded));

        public Task<byte[]> DownloadOutputAsync(string providerJobId, CancellationToken ct = default) =>
            Task.FromResult(Array.Empty<byte>());
    }

    sealed class RecordingFal : IFalLipsyncTransport
    {
        public ProductionCredentialReference? Seen { get; private set; }
        public bool RequiresNetwork => false;
        public int StartCount => 0;

        public Task<FalLipsyncTransportResult> ExecuteOnceAsync(FalLipsyncTransportRequest request, CancellationToken ct = default)
        {
            Seen = request.Credential;
            return Task.FromResult(new FalLipsyncTransportResult(
                "local-frozen-" + request.TaskId.ToString("N"), new byte[32], "video/mp4"));
        }

        public Task<FalLipsyncTransportResult> RecoverOnceAsync(string providerJobId, CancellationToken ct = default) =>
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
    }

    sealed class RecordingVoice : IElevenLabsVoiceTransport
    {
        public ProductionCredentialReference? Seen { get; private set; }
        public bool RequiresNetwork => false;
        public int SynthesizeCount => 0;

        public Task<ElevenLabsVoiceTransportResult> SynthesizeOnceAsync(
            ElevenLabsVoiceTransportRequest request, CancellationToken ct = default)
        {
            Seen = request.Credential;
            return Task.FromResult(new ElevenLabsVoiceTransportResult(
                "local-frozen-" + request.TaskId.ToString("N"), new byte[32], "audio/mpeg"));
        }
    }
}
