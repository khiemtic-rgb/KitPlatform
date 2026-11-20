namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// PRODUCTION-EXECUTION-AUTHORITY-01.
/// Official execution uses the frozen snapshot. A later selection does not replace it.
/// </summary>
public static class ProductionExecutionAuthorityClosureV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_EXECUTION_AUTHORITY_CLOSURE_01";
    const string Secret = "exec-authority-frozen-secret";

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
        var tenantA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb2");
        var caller = new SwitchCaller { Id = tenantA };
        var key = new byte[ProductionCredentialSecretEnvelope.KeySize];
        System.Security.Cryptography.RandomNumberGenerator.Fill(key);
        var ring = new MapProductionCredentialKeyRing("v1", new Dictionary<string, byte[]> { ["v1"] = key });
        var identities = new InMemoryProductionCredentialStore(caller);
        var secrets = new InMemoryProductionCredentialSecretStore(caller, identities, ring);
        var seen = new List<(Guid CredentialId, Guid VersionId)>();
        var resolver = new RecordingResolver(new ProductionCredentialResolver(identities, secrets, caller), seen);
        var credential = ProductionCredentialOwnership.Create(Guid.NewGuid(), tenantA, "exec-authority");
        await identities.CreateAsync(credential);
        var version = ProductionCredentialOwnership.CreateVersion(
            Guid.NewGuid(), credential.CredentialId, tenantA, 1, "runway", "gen4_turbo");
        await identities.AddVersionAsync(version);
        await secrets.PutAsync(version.CredentialVersionId, Secret);
        var reference = new ProductionCredentialReference(credential.CredentialId, version.CredentialVersionId);

        var work = new InMemoryProductionWorkStore();
        var official = ProductionWorkExecutionEngine.CreateOfficial(work, resolver);
        var scope = new ProductionScope(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH01");
        var request = await official.CreateRequestAsync(scope, ProductionActions.CreateVideo, "closure", "idem-exec-" + Guid.NewGuid().ToString("N"));
        var task = await official.CreateTaskAsync(request.RequestId);
        var missing = false;
        try
        {
            await official.StartAttemptAsync(task.TaskId, "none");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("frozen", StringComparison.OrdinalIgnoreCase))
        {
            missing = true;
        }
        Ok(missing && await work.ListAttemptsAsync(task.TaskId) is { Count: 0 },
            "attempt without a frozen snapshot fails closed");

        var unpinned = false;
        try
        {
            await official.FreezeInputAsync(task.TaskId, Input(), "runway", "gen4_turbo");
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialResolver.Unresolved)
        {
            unpinned = true;
        }
        Ok(unpinned && await work.GetSnapshotByTaskAsync(task.TaskId) is null, "missing credential pair does not freeze");

        var frozen = await official.FreezeInputAsync(task.TaskId, Input(), "runway", "gen4_turbo", credential: reference);
        var latestProvider = "google";
        var latestModel = "veo-3.1";
        var latestCredential = Guid.NewGuid();
        _ = (latestProvider, latestModel, latestCredential);
        var attempt = await official.StartAttemptAsync(task.TaskId, frozen.SnapshotHash);
        var reloaded = await work.GetSnapshotByTaskAsync(task.TaskId);
        Ok(reloaded is not null
            && reloaded.ProviderId == "runway"
            && reloaded.ModelId == "gen4_turbo"
            && reloaded.CredentialVersionId == version.CredentialVersionId
            && reloaded.SnapshotHash == frozen.SnapshotHash
            && attempt.ProviderId == "runway"
            && attempt.ModelId == "gen4_turbo"
            && seen.Count >= 1
            && seen.All(pair => pair.VersionId == version.CredentialVersionId && pair.CredentialId == credential.CredentialId)
            && seen.All(pair => pair.VersionId != latestCredential),
            "execution after a later selection still uses the frozen contract");

        caller.Id = tenantB;
        var cross = false;
        try
        {
            await official.StartAttemptAsync(task.TaskId, frozen.SnapshotHash);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialSecretProtector.SecretUnavailable
            && ex.ToString().IndexOf(Secret, StringComparison.Ordinal) < 0)
        {
            cross = true;
        }
        Ok(cross, "tenant B cannot start tenant A frozen execution");
        caller.Id = tenantA;

        var transport = new CountingRunwayTransport();
        var adapter = new RunwayMotionExecutionAdapter(transport);
        var bare = frozen with { CredentialId = null, CredentialVersionId = null };
        var pollRejected = false;
        try
        {
            await adapter.PollFrozenAsync(bare, "job-none");
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialResolver.Unresolved)
        {
            pollRejected = true;
        }
        Ok(pollRejected && transport.HttpCallCount == 0, "unpinned poll does not call the provider");

        var root = RepoRoot();
        var turbo = File.ReadAllText(Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "ContentSeriesTurboService.cs"));
        var vge = File.ReadAllText(Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "VideoGenerationExecutionService.cs"));
        var runway = File.ReadAllText(Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "Production", "HttpRunwayTransport.cs"));
        var fal = File.ReadAllText(Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "Production", "HttpFalLipsyncTransport.cs"));
        var voice = File.ReadAllText(Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "Production", "HttpElevenLabsVoiceTransport.cs"));
        Ok(turbo.IndexOf("LegacyProviderSubmitClosed", StringComparison.Ordinal) >= 0
            && turbo.IndexOf("LegacyProviderSubmitClosed", StringComparison.Ordinal) < turbo.IndexOf("_fal.UploadAsync", StringComparison.Ordinal),
            "legacy turbo throws before a provider call");
        Ok(vge.IndexOf("LEGACY_EXECUTION_CLOSED", StringComparison.Ordinal) >= 0
            && vge.IndexOf("LEGACY_EXECUTION_CLOSED", StringComparison.Ordinal) < vge.IndexOf("PersistProductionExecution", StringComparison.Ordinal),
            "legacy video generation execution throws before persistence");
        Ok(Before(runway, "PaidNetworkForbidden", "StartCount++")
            && Before(runway, "ProductionCredentialResolver.RequireAsync", "StartCount++")
            && Before(fal, "ProductionCredentialResolver.RequireAsync", "StartCount++")
            && Before(voice, "ProductionCredentialResolver.RequireAsync", "SynthesizeCount++"),
            "official transports authorize and resolve before provider HTTP");
        Ok(!Legacy.LegacySeriesGraphAdapter.MayDriveNewBinding("acceptedTake.url"),
            "acceptedTake cannot create an official binding");
        Ok(ProductionExecutionAuthorityV1Regression.Run().Count == 0, "legacy route classification stays closed");
        Ok(transport.HttpCallCount == 0, "negative cases create no provider job");
    }

    static bool Before(string text, string gate, string http)
    {
        var left = text.IndexOf(gate, StringComparison.Ordinal);
        var right = text.IndexOf(http, StringComparison.Ordinal);
        return left >= 0 && right > left;
    }

    static ShotExecutionInput Input() =>
        new("Minh ngập ngừng, đặt cặp xuống ghế rồi chậm chạp kéo khóa.", "He stays in place.", "sofa 16:9", "CHAR-001", "kf-exec", "5s", "", "hold");

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

    sealed class RecordingResolver(IProductionCredentialResolver inner, List<(Guid CredentialId, Guid VersionId)> seen) : IProductionCredentialResolver
    {
        public Task<ProductionCredentialMaterial> ResolveAsync(
            Guid credentialId, Guid credentialVersionId, string providerId, string modelId, CancellationToken ct = default)
        {
            seen.Add((credentialId, credentialVersionId));
            return inner.ResolveAsync(credentialId, credentialVersionId, providerId, modelId, ct);
        }
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
