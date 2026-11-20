using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class TenantAiConfigurationV1Tests
{
    static readonly Guid TenantA = Guid.Parse("11111111-1111-4111-8111-111111111111");
    static readonly Guid TenantB = Guid.Parse("22222222-2222-4222-8222-222222222222");
    static readonly Guid Build = Guid.Parse("7c0e7a51-3d8e-4a53-9a4e-1f2b3c4d5e6f");
    static readonly ProductionScope Sh01 = new(Build, "EP01", "SC01", "EP01-SC01-SH01");

    [Fact]
    public async Task T01_tenant_a_does_not_see_tenant_b_credentials()
    {
        var h = Open();
        h.Caller.Id = TenantB;
        await Register(h, "runway", "gen4_turbo", "b-runway");
        h.Caller.Id = TenantA;
        var mine = await Register(h, "runway", "gen4_turbo", "a-runway");

        var video = (await h.Settings.GetAsync()).Single(v => v.Capability == ProductionTenantAiCapabilities.Video);

        var only = Assert.Single(video.Credentials);
        Assert.Equal(mine.CredentialId, only.CredentialId);
        Assert.Equal(TenantA, (await h.Credentials.GetAsync(only.CredentialId))!.TenantId);
    }

    [Fact]
    public async Task T02_saved_video_default_resolves_provider_model_and_credential()
    {
        var h = Open();
        var cred = await Register(h, "runway", "gen4_turbo", "a-runway");
        await h.Settings.SaveAsync(Save(ProductionTenantAiCapabilities.Video, "runway", "gen4_turbo", cred.CredentialId));

        var resolved = await h.Resolution.ResolveAsync(Sh01, ProductionTenantAiCapabilities.Video);

        Assert.Equal("runway", resolved.ProviderId);
        Assert.Equal("gen4_turbo", resolved.ModelId);
        Assert.Equal(ProductionTenantAiSources.Tenant, resolved.Source);
        Assert.Equal(new ProductionCredentialReference(cred.CredentialId, cred.CredentialVersionId), resolved.Credential);
    }

    [Fact]
    public async Task T03_missing_or_disabled_video_default_blocks()
    {
        var h = Open();
        var missing = await Assert.ThrowsAsync<InvalidOperationException>(
            () => h.Resolution.ResolveAsync(Sh01, ProductionTenantAiCapabilities.Video));
        Assert.Equal(ProductionTenantAiReasons.ConfigurationRequired, missing.Message);

        var cred = await Register(h, "runway", "gen4_turbo", "a-runway");
        await h.Settings.SaveAsync(Save(ProductionTenantAiCapabilities.Video, "runway", "gen4_turbo", cred.CredentialId, enabled: false));
        var disabled = await Assert.ThrowsAsync<InvalidOperationException>(
            () => h.Resolution.ResolveAsync(Sh01, ProductionTenantAiCapabilities.Video));
        Assert.Equal(ProductionTenantAiReasons.CapabilityDisabled, disabled.Message);
    }

    [Fact]
    public async Task T04_shot_override_wins_over_build_override_and_tenant_default()
    {
        var h = Open(withVeo: true);
        var runway = await Register(h, "runway", "gen4_turbo", "a-runway");
        var google = await Register(h, "google", "veo-3.1", "a-veo");
        await h.Settings.SaveAsync(Save(ProductionTenantAiCapabilities.Video, "runway", "gen4_turbo", runway.CredentialId));
        await Select(h, ProductionAiSelectionScopes.BuildShotCode, "runway", "gen4_turbo");
        await Select(h, Sh01.ShotCode, "google", "veo-3.1");

        var resolved = await h.Resolution.ResolveAsync(Sh01, ProductionTenantAiCapabilities.Video);

        Assert.Equal(ProductionTenantAiSources.Shot, resolved.Source);
        Assert.Equal(("google", "veo-3.1"), (resolved.ProviderId, resolved.ModelId));
        Assert.Equal(google.CredentialId, resolved.Credential!.CredentialId);
    }

    [Fact]
    public async Task T05_build_override_wins_over_tenant_default()
    {
        var h = Open(withVeo: true);
        var runway = await Register(h, "runway", "gen4_turbo", "a-runway");
        await Register(h, "google", "veo-3.1", "a-veo");
        await h.Settings.SaveAsync(Save(ProductionTenantAiCapabilities.Video, "runway", "gen4_turbo", runway.CredentialId));
        await Select(h, ProductionAiSelectionScopes.BuildShotCode, "google", "veo-3.1");

        var resolved = await h.Resolution.ResolveAsync(Sh01, ProductionTenantAiCapabilities.Video);

        Assert.Equal(ProductionTenantAiSources.Build, resolved.Source);
        Assert.Equal(("google", "veo-3.1"), (resolved.ProviderId, resolved.ModelId));
        var otherShot = await h.Resolution.ResolveAsync(Sh01 with { ShotCode = "EP01-SC01-SH02" }, ProductionTenantAiCapabilities.Video);
        Assert.Equal(ProductionTenantAiSources.Build, otherShot.Source);
    }

    [Fact]
    public async Task T06_freeze_snapshot_keeps_resolved_provider_model_credential_after_config_changes()
    {
        var h = Open();
        const string secret = "tenant-ai-v1-secret";
        var first = await Register(h, "fal", "sync-lipsync-v3", "fal-a", secret);
        await h.Settings.SaveAsync(Save(ProductionTenantAiCapabilities.Lipsync, "fal", "sync-lipsync-v3", first.CredentialId));
        var resolved = await h.Resolution.ResolveAsync(Sh01, ProductionTenantAiCapabilities.Lipsync);

        var request = await h.Engine.CreateRequestAsync(Sh01, ProductionActions.CreateLipsync, "test", Guid.NewGuid().ToString("N"));
        var task = await h.Engine.CreateTaskAsync(request.RequestId);
        var snap = await h.Engine.FreezeInputAsync(
            task.TaskId, Input(), resolved.ProviderId, resolved.ModelId, default, resolved.Credential);

        var second = await Register(h, "fal", "sync-lipsync-v3", "fal-b");
        await h.Settings.SaveAsync(Save(ProductionTenantAiCapabilities.Lipsync, "fal", "sync-lipsync-v3", second.CredentialId));
        var now = await h.Resolution.ResolveAsync(Sh01, ProductionTenantAiCapabilities.Lipsync);
        var reread = await h.Work.GetSnapshotByTaskAsync(task.TaskId);

        Assert.Equal(second.CredentialId, now.Credential!.CredentialId);
        Assert.NotNull(reread);
        Assert.Equal(("fal", "sync-lipsync-v3"), (reread!.ProviderId, reread.ModelId));
        Assert.Equal(first.CredentialId, reread.CredentialId);
        Assert.Equal(first.CredentialVersionId, reread.CredentialVersionId);
        Assert.Equal(snap.SnapshotHash, reread.SnapshotHash);
        Assert.DoesNotContain(secret, JsonSerializer.Serialize(reread), StringComparison.Ordinal);
    }

    [Fact]
    public async Task T07_tenant_production_without_configuration_never_falls_back_to_fixture()
    {
        var h = Open();
        var os = new ProductionExecutionService(
            h.Work, new FixtureMotionAdapter(), selections: h.Selections, credentials: h.Resolver, tenantAi: h.Resolution);
        var svc = new ProductionCreateVideoService(
            os, h.Work, new InMemoryProductionArtifactFileStore(), new FixtureMotionAdapter(),
            resolver: h.Motion, tenantAi: h.Resolution);

        var verdict = await os.EvaluateCreateVideoAsync(Sh01);
        Assert.False(verdict.Available);
        Assert.Equal(ProductionTenantAiReasons.ConfigurationRequired, verdict.BlockReason);
        Assert.NotEqual(ProductionMotionCatalog.FixtureProviderId, verdict.ProviderId);

        var blocked = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateVideoAsync(
            new CreateVideoApiCommand(Sh01, Input(), "test", null, null, null)));
        Assert.Equal(ProductionTenantAiReasons.ConfigurationRequired, blocked.Message);
        Assert.Empty(await h.Work.ListTasksByScopeAsync(Build, Sh01.ShotCode));

        await Select(h, Sh01.ShotCode, ProductionMotionCatalog.FixtureProviderId, ProductionMotionCatalog.FixtureModelId);
        var fixture = await Assert.ThrowsAsync<InvalidOperationException>(
            () => h.Resolution.ResolveAsync(Sh01, ProductionTenantAiCapabilities.Video));
        Assert.Equal(ProductionTenantAiReasons.ModelNotExecutable, fixture.Message);
    }

    [Fact]
    public async Task T08_models_outside_the_tenant_catalog_cannot_be_saved()
    {
        var h = Open();
        var cred = await Register(h, "runway", "gen4_turbo", "a-runway");

        foreach (var (provider, model) in new[]
                 {
                     ("wan", "wan-2.1"),
                     ("google", "veo-3.1"),
                     (ProductionMotionCatalog.FixtureProviderId, ProductionMotionCatalog.FixtureModelId),
                 })
        {
            var failed = await Assert.ThrowsAsync<InvalidOperationException>(() => h.Settings.SaveAsync(
                Save(ProductionTenantAiCapabilities.Video, provider, model, cred.CredentialId)));
            Assert.Equal(ProductionTenantAiReasons.ModelNotExecutable, failed.Message);
            var overrideFailed = Assert.Throws<InvalidOperationException>(
                () => h.Settings.RequireSelectableOverride(Build, "VIDEO", provider, model));
            Assert.Equal(ProductionTenantAiReasons.ModelNotExecutable, overrideFailed.Message);
        }

        Assert.Equal(["runway:gen4_turbo"], h.Catalog.List("VIDEO").Select(e => $"{e.ProviderId}:{e.ModelId}"));
        Assert.Empty(h.Catalog.List("TEXT"));
        Assert.Empty(h.Catalog.List("IMAGE"));
        Assert.Empty(h.Catalog.List("VOICE"));
        var unknown = await Assert.ThrowsAsync<InvalidOperationException>(() => h.Settings.SaveAsync(
            Save("AUDIO", "runway", "gen4_turbo", cred.CredentialId)));
        Assert.Equal(ProductionTenantAiReasons.UnknownCapability, unknown.Message);
        h.Settings.RequireSelectableOverride(ProductionScopeAnchorRules.OfficialEp01, "VIDEO", "google", "veo-3.1");
    }

    [Fact]
    public async Task T09_credential_of_another_tenant_cannot_be_saved_or_used()
    {
        var h = Open();
        h.Caller.Id = TenantB;
        var foreign = await Register(h, "runway", "gen4_turbo", "b-runway");
        h.Caller.Id = TenantA;

        var save = await Assert.ThrowsAsync<InvalidOperationException>(() => h.Settings.SaveAsync(
            Save(ProductionTenantAiCapabilities.Video, "runway", "gen4_turbo", foreign.CredentialId)));
        Assert.Equal(ProductionTenantAiReasons.CredentialNotOwned, save.Message);

        await h.Configs.UpsertAsync(new ProductionTenantAiConfig(
            TenantA, ProductionTenantAiCapabilities.Video, "runway", "gen4_turbo", foreign.CredentialId, true, "t", DateTimeOffset.UtcNow));
        var use = await Assert.ThrowsAsync<InvalidOperationException>(
            () => h.Resolution.ResolveAsync(Sh01, ProductionTenantAiCapabilities.Video));
        Assert.Equal(ProductionTenantAiReasons.CredentialNotOwned, use.Message);

        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Configs.UpsertAsync(new ProductionTenantAiConfig(
            TenantB, ProductionTenantAiCapabilities.Video, "runway", "gen4_turbo", foreign.CredentialId, true, "t", DateTimeOffset.UtcNow)));
    }

    [Fact]
    public void T10_legacy_org_settings_flows_still_exist_and_are_not_the_tenant_source()
    {
        var infra = Assembly.Load("KitPlatform.Packs.Content.Infrastructure");
        foreach (var name in new[]
                 {
                     "ContentOrgSettingsService", "ContentGeminiClient", "ContentCreatomateClient",
                     "ContentFacebookClient", "ContentRunwayClient", "ContentFalClient", "ContentElevenLabsClient",
                 })
            Assert.NotNull(infra.GetType("KitPlatform.Packs.Content.Infrastructure." + name));

        foreach (var type in new[] { typeof(ProductionTenantAiResolution), typeof(ProductionTenantAiSettingsService), typeof(ProductionTenantAiCatalog) })
        {
            var deps = type.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType.Name).ToList();
            Assert.DoesNotContain(deps, d => d.Contains("OrgSettings", StringComparison.OrdinalIgnoreCase)
                || d.Contains("AiConfig", StringComparison.Ordinal) && !d.Contains("TenantAiConfig", StringComparison.Ordinal)
                || d.Contains("VideoConfig", StringComparison.Ordinal)
                || d.Contains("Gemini", StringComparison.Ordinal) && d != nameof(TenantGeminiImageExecutionAdapter));
        }
    }

    static SaveProductionTenantAiConfigCommand Save(string capability, string provider, string model, Guid? credentialId, bool enabled = true) =>
        new(capability, provider, model, credentialId, enabled, "test");

    static Task<ProductionAiSelection> Select(Harness h, string shotCode, string provider, string model) =>
        h.Selections.UpsertActiveAsync(new UpsertProductionAiSelectionCommand(
            Build, shotCode, "EP01", "SC01", ProductionAiSelectionStages.Video, provider, model, "test", "override"));

    static Task<ProductionCredentialRegistrationResult> Register(Harness h, string provider, string model, string name, string secret = "tenant-ai-secret") =>
        h.Registration.RegisterAsync(new RegisterProductionCredentialCommand(provider, model, secret, name));

    static ShotExecutionInput Input() =>
        new("Recovered line", "beat", "visual", "", "", "5", "", "",
            null, "", "16:9", 5, ShotExecutionCompiler.Version, null,
            "line-SC01-CHAR-003-1", "CHAR-003", "");

    static Harness Open(bool withVeo = false)
    {
        var caller = new SwitchCaller { Id = TenantA };
        var key = new byte[ProductionCredentialSecretEnvelope.KeySize];
        RandomNumberGenerator.Fill(key);
        var ring = new MapProductionCredentialKeyRing("v1", new Dictionary<string, byte[]> { ["v1"] = key });
        var credentials = new InMemoryProductionCredentialStore(caller);
        var secrets = new InMemoryProductionCredentialSecretStore(caller, credentials, ring);
        var rotation = new InMemoryProductionCredentialRotation(credentials, secrets, ring);
        var resolver = new ProductionCredentialResolver(credentials, secrets, caller);
        var work = new InMemoryProductionWorkStore(caller);
        work.RegisterBuild(Build, TenantA);
        IMotionExecutionAdapter[] adapters = withVeo
            ? [new FixtureMotionAdapter(), new NoCallMotion("runway", "gen4_turbo"), new NoCallMotion("google", "veo-3.1")]
            : [new FixtureMotionAdapter(), new NoCallMotion("runway", "gen4_turbo")];
        var motion = new ProductionMotionAdapterResolver(adapters);
        var catalog = new ProductionTenantAiCatalog(motion, new NoCallLipsync());
        var configs = new InMemoryProductionTenantAiConfigStore(caller);
        var selections = new InMemoryProductionAiSelectionStore();
        return new Harness(
            caller,
            credentials,
            new ProductionCredentialRegistrationService(caller, credentials, rotation),
            resolver,
            work,
            ProductionWorkExecutionEngine.CreateOfficial(work, resolver),
            motion,
            catalog,
            configs,
            selections,
            new ProductionTenantAiSettingsService(caller, configs, catalog, credentials),
            new ProductionTenantAiResolution(configs, catalog, credentials, selections));
    }

    sealed record Harness(
        SwitchCaller Caller,
        InMemoryProductionCredentialStore Credentials,
        IProductionCredentialRegistration Registration,
        IProductionCredentialResolver Resolver,
        InMemoryProductionWorkStore Work,
        ProductionWorkExecutionEngine Engine,
        IProductionMotionAdapterResolver Motion,
        IProductionTenantAiCatalog Catalog,
        IProductionTenantAiConfigStore Configs,
        InMemoryProductionAiSelectionStore Selections,
        IProductionTenantAiSettings Settings,
        IProductionTenantAiResolution Resolution);

    sealed class SwitchCaller : IProductionCallerTenant
    {
        public Guid Id { get; set; }
        public Guid RequireTenant() => ProductionTenantBoundary.Require(Id);
    }

    sealed class NoCallMotion(string providerId, string modelId) : IMotionExecutionAdapter
    {
        public string ProviderId => providerId;
        public string ModelId => modelId;
        public bool IsPaid => true;
        public bool RequiresNetwork => true;
        public Task<MotionStartResult> StartFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default) => throw new InvalidOperationException("NO_PROVIDER_CALL");
        public Task<MotionPollResult> PollFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default) => throw new InvalidOperationException("NO_PROVIDER_CALL");
        public Task<MotionPollResult> RecoverFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default) => throw new InvalidOperationException("NO_PROVIDER_CALL");
        public Task<MotionAdapterResult> FetchOutputAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default) => throw new InvalidOperationException("NO_PROVIDER_CALL");
        public Task<MotionAdapterResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default) => throw new InvalidOperationException("NO_PROVIDER_CALL");
    }

    sealed class NoCallLipsync : ILipSyncExecutionAdapter
    {
        public string ProviderId => ProductionLipsyncCatalog.FalProviderId;
        public string ModelId => ProductionLipsyncCatalog.SyncLipsyncV3;
        public bool IsPaid => true;
        public bool RequiresNetwork => true;
        public Task<ProductionMediaResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default) =>
            throw new InvalidOperationException("NO_PROVIDER_CALL");
    }
}
