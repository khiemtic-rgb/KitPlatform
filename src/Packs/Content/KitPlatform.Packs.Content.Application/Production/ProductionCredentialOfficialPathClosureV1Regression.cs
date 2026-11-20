namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// PRODUCTION-CREDENTIAL-OFFICIAL-PATH-CLOSURE-01.
/// Official Production credential files must not name a legacy credential source.
/// </summary>
public static class ProductionCredentialOfficialPathClosureV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_CREDENTIAL_OFFICIAL_PATH_CLOSURE_01";
    const string Secret = "gate-closure-owned-secret";

    static readonly string[] Forbidden =
    [
        "org_settings",
        "GetOrgSettingsAsync",
        "ContentVideoConfig",
        "ContentAiConfig",
        "Content:Secrets",
        "GetEnvironmentVariable",
        "RUNWAY_API_KEY",
        "FAL_KEY",
        "ELEVENLABS_API_KEY",
        "VEO_JOIN_FILE",
        "GEMINI_API_KEY",
        "GetLatestCredentialVersion",
        "storedSecret",
        "gemini_api_key",
    ];

    static readonly string[] OfficialFiles =
    [
        "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionWorkExecutionEngine.cs",
        "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionExecutionService.cs",
        "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.cs",
        "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.Actions.cs",
        "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCredentialResolution.cs",
        "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/OfficialCreateVideoFreezeV1.cs",
        "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCredentialRotation.cs",
        "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/Providers/RunwayMotionExecutionAdapter.cs",
        "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/Providers/VeoMotionExecutionAdapter.cs",
        "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/Providers/FalLipSyncExecutionAdapter.cs",
        "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/Providers/ElevenLabsVoiceExecutionAdapter.cs",
        "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/Providers/HttpVeoMotionTransport.cs",
        "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/Providers/ProductionVeoHttpAuthenticator.cs",
        "src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/Production/HttpRunwayTransport.cs",
        "src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/Production/HttpFalLipsyncTransport.cs",
        "src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/Production/HttpElevenLabsVoiceTransport.cs",
        "src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/Production/OfficialVeoRealVertexStartLiveV1.cs",
        "src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/Production/OfficialVeoPollRecoverMaterializationLiveV1.cs",
    ];

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
        var root = RepoRoot();
        foreach (var relative in OfficialFiles)
        {
            var text = File.ReadAllText(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            var hit = Forbidden.FirstOrDefault(token => text.Contains(token, StringComparison.Ordinal));
            Ok(hit is null, "official file has no legacy credential source: " + relative + " " + hit);
        }

        foreach (var relative in new[]
        {
            "src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/Production/HttpRunwayTransport.cs",
            "src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/Production/HttpFalLipsyncTransport.cs",
            "src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/Production/HttpElevenLabsVoiceTransport.cs",
        })
        {
            var text = File.ReadAllText(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            var require = text.IndexOf("ProductionCredentialResolver.RequireAsync", StringComparison.Ordinal);
            var client = text.IndexOf("material.Secret", StringComparison.Ordinal);
            Ok(require >= 0 && client > require && !text.Contains(".ResolveAsync(", StringComparison.Ordinal),
                "transport resolves the frozen pair before the provider client: " + relative);
        }

        var runway = File.ReadAllText(Path.Combine(root,
            "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "Production", "HttpRunwayTransport.cs"));
        var requireAt = runway.IndexOf("ProductionCredentialResolver.RequireAsync", StringComparison.Ordinal);
        var httpAt = runway.IndexOf("StartCount++", StringComparison.Ordinal);
        Ok(requireAt >= 0 && httpAt > requireAt, "Runway HTTP starts only after the frozen credential resolves");

        var tenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
        var caller = new SwitchCaller { Id = tenantA };
        var key = new byte[ProductionCredentialSecretEnvelope.KeySize];
        System.Security.Cryptography.RandomNumberGenerator.Fill(key);
        var ring = new MapProductionCredentialKeyRing("v1", new Dictionary<string, byte[]> { ["v1"] = key });
        var identities = new InMemoryProductionCredentialStore(caller);
        var secrets = new InMemoryProductionCredentialSecretStore(caller, identities, ring);
        var resolver = new ProductionCredentialResolver(identities, secrets, caller);
        var credential = ProductionCredentialOwnership.Create(Guid.NewGuid(), tenantA, "closure-tenant-a");
        await identities.CreateAsync(credential);
        var version = ProductionCredentialOwnership.CreateVersion(
            Guid.NewGuid(), credential.CredentialId, tenantA, 1, "runway", "gen4_turbo");
        await identities.AddVersionAsync(version);
        await secrets.PutAsync(version.CredentialVersionId, Secret);

        caller.Id = tenantB;
        var cross = false;
        try
        {
            await resolver.ResolveAsync(credential.CredentialId, version.CredentialVersionId, "runway", "gen4_turbo");
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialSecretProtector.SecretUnavailable
            && ex.ToString().IndexOf(Secret, StringComparison.Ordinal) < 0)
        {
            cross = true;
        }
        Ok(cross, "tenant B cannot resolve tenant A credential");
        caller.Id = tenantA;

        var missing = false;
        try
        {
            await ProductionCredentialResolver.RequireAsync(resolver, null, "runway", "gen4_turbo");
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialResolver.Unresolved)
        {
            missing = true;
        }
        Ok(missing, "missing frozen pair fails closed");

        var transport = new CountingRunwayTransport();
        var adapter = new RunwayMotionExecutionAdapter(transport);
        var unpinned = new ProductionTaskInputSnapshot(
            Guid.NewGuid(), Guid.NewGuid(),
            "story", "acting", "visual", "shot", "prompt", "compiler",
            "sh", "ah", "vh", "ch", "kh", "voice", "motion", "lip", "timing",
            "runway", "gen4_turbo", "hash", DateTimeOffset.UtcNow);
        var rejected = false;
        try
        {
            await adapter.PollFrozenAsync(unpinned, "job-1");
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialResolver.Unresolved)
        {
            rejected = true;
        }
        Ok(rejected && transport.HttpCallCount == 0, "unpinned retry does not call the provider");

        var work = new InMemoryProductionWorkStore();
        var official = ProductionWorkExecutionEngine.CreateOfficial(work, resolver);
        var request = await official.CreateRequestAsync(
            new ProductionScope(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH01"),
            ProductionActions.CreateVideo, "closure", "idem-" + Guid.NewGuid().ToString("N"));
        var task = await official.CreateTaskAsync(request.RequestId);
        var pinned = await official.FreezeInputAsync(task.TaskId, Input(), "runway", "gen4_turbo");
        Ok(pinned.CredentialId == credential.CredentialId
            && pinned.CredentialVersionId == version.CredentialVersionId
            && pinned.ToString().IndexOf(Secret, StringComparison.Ordinal) < 0
            && transport.HttpCallCount == 0,
            "official freeze without an explicit pair pins the single active tenant credential and does not call a provider");
    }

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
        return @"E:\KitPlatform";
    }

    sealed class SwitchCaller : IProductionCallerTenant
    {
        public Guid Id { get; set; }
        public Guid RequireTenant() => ProductionTenantBoundary.Require(Id);
    }

    sealed class CountingRunwayTransport : IRunwayTransport
    {
        public bool RequiresNetwork => true;
        public int HttpCallCount { get; private set; }

        public Task<RunwayTransportCreateResult> CreateImageToVideoAsync(RunwayTransportCreateRequest request, CancellationToken ct = default)
        {
            HttpCallCount++;
            return Task.FromResult(new RunwayTransportCreateResult("should-not-run"));
        }

        public Task<RunwayTransportTaskResult> GetTaskAsync(string providerJobId, CancellationToken ct = default)
        {
            HttpCallCount++;
            return Task.FromResult(new RunwayTransportTaskResult(providerJobId, MotionPollStatuses.Succeeded));
        }

        public Task<byte[]> DownloadOutputAsync(string providerJobId, CancellationToken ct = default)
        {
            HttpCallCount++;
            return Task.FromResult(Array.Empty<byte>());
        }
    }
}
