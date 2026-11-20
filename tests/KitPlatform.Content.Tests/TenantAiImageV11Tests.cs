using System.Security.Cryptography;
using System.Text.Json;
using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class TenantAiImageV11Tests
{
    static readonly Guid TenantA = Guid.Parse("11111111-1111-4111-8111-111111111111");
    static readonly Guid TenantB = Guid.Parse("22222222-2222-4222-8222-222222222222");
    static readonly Guid Build = Guid.Parse("7c0e7a51-3d8e-4a53-9a4e-1f2b3c4d5e6f");
    static readonly ProductionScope Sh01 = new(Build, "EP01", "SC01", "EP01-SC01-SH01");
    const string Gemini = ProductionImageCatalog.GeminiProviderId;
    const string FlashImage = ProductionImageCatalog.GeminiFlashImage;

    [Fact]
    public async Task T01_tenant_a_image_runs_on_tenant_a_credential()
    {
        var h = Open();
        var cred = await Register(h, "a-gemini", "tenant-a-gemini-key");
        await h.Settings.SaveAsync(Save(cred.CredentialId));
        await IssuePermit(h);

        var result = await h.Svc.CreateImageAsync(Command("t01"));
        var snap = await SnapshotOf(h, result);

        Assert.Equal(["tenant-a-gemini-key"], h.Transport.Keys);
        Assert.Equal([FlashImage], h.Transport.Models);
        Assert.Equal((Gemini, FlashImage), (snap.ProviderId, snap.ModelId));
        Assert.Equal(cred.CredentialId, snap.CredentialId);
        Assert.Equal(cred.CredentialVersionId, snap.CredentialVersionId);
        Assert.Equal(ProductionArtifactRoles.Keyframe, result.Artifact.Role);
        Assert.Equal(0, h.Legacy.RequireCount + h.Legacy.GenerationCount);
    }

    [Fact]
    public async Task T02_tenant_b_cannot_use_tenant_a_credential()
    {
        var h = Open();
        var foreign = await Register(h, "a-gemini", "tenant-a-gemini-key");
        await h.Settings.SaveAsync(Save(foreign.CredentialId));
        await IssuePermit(h);
        var frozenForeign = await SnapshotOf(h, await h.Svc.CreateImageAsync(Command("t02-a")));
        h.Transport.Keys.Clear();
        h.Caller.Id = TenantB;

        var save = await Assert.ThrowsAsync<InvalidOperationException>(() => h.Settings.SaveAsync(Save(foreign.CredentialId)));
        Assert.Equal(ProductionTenantAiReasons.CredentialNotOwned, save.Message);

        await h.Configs.UpsertAsync(new ProductionTenantAiConfig(
            TenantB, ProductionTenantAiCapabilities.Image, Gemini, FlashImage, foreign.CredentialId, true, "t", DateTimeOffset.UtcNow));
        var use = await Assert.ThrowsAsync<InvalidOperationException>(() => h.Svc.CreateImageAsync(Command("t02")));
        Assert.Equal(ProductionTenantAiReasons.CredentialNotOwned, use.Message);

        await Assert.ThrowsAnyAsync<Exception>(() => h.TenantImage.ExecuteFrozenAsync(frozenForeign, null, null));
        Assert.Empty(h.Transport.Keys);
    }

    [Fact]
    public async Task T03_missing_image_configuration_blocks_create_image()
    {
        var h = Open();
        await Register(h, "a-gemini", "tenant-a-gemini-key");
        await IssuePermit(h);

        var blocked = await Assert.ThrowsAsync<InvalidOperationException>(() => h.Svc.CreateImageAsync(Command("t03")));
        var unpaid = await Assert.ThrowsAsync<InvalidOperationException>(
            () => h.Svc.CreateImageAsync(Command("t03-unpaid", authorize: false)));

        Assert.Equal(ProductionTenantAiReasons.ConfigurationRequired, blocked.Message);
        Assert.Equal(ProductionTenantAiReasons.ConfigurationRequired, unpaid.Message);
        Assert.Empty(await h.Work.ListTasksByScopeAsync(Build, Sh01.ShotCode));
        Assert.Empty(h.Transport.Keys);
    }

    [Fact]
    public async Task T04_disabled_image_capability_blocks_create_image()
    {
        var h = Open();
        var cred = await Register(h, "a-gemini", "tenant-a-gemini-key");
        await h.Settings.SaveAsync(Save(cred.CredentialId, enabled: false));
        await IssuePermit(h);

        var blocked = await Assert.ThrowsAsync<InvalidOperationException>(() => h.Svc.CreateImageAsync(Command("t04")));

        Assert.Equal(ProductionTenantAiReasons.CapabilityDisabled, blocked.Message);
        Assert.Empty(await h.Work.ListTasksByScopeAsync(Build, Sh01.ShotCode));
        Assert.Empty(h.Transport.Keys);
    }

    [Fact]
    public async Task T05_snapshot_keeps_provider_model_credential_after_configuration_changes()
    {
        var h = Open();
        var first = await Register(h, "gemini-1", "first-gemini-key");
        await h.Settings.SaveAsync(Save(first.CredentialId));
        await IssuePermit(h);
        var created = await h.Svc.CreateImageAsync(Command("t05"));

        var second = await Register(h, "gemini-2", "second-gemini-key");
        await h.Settings.SaveAsync(Save(second.CredentialId));
        var reread = await h.Work.GetSnapshotByTaskAsync(created.Task.TaskId);
        await h.TenantImage.ExecuteFrozenAsync(reread!, null, null);

        Assert.Equal((Gemini, FlashImage), (reread!.ProviderId, reread.ModelId));
        Assert.Equal(first.CredentialId, reread.CredentialId);
        Assert.Equal(first.CredentialVersionId, reread.CredentialVersionId);
        Assert.Equal((await SnapshotOf(h, created)).SnapshotHash, reread.SnapshotHash);
        Assert.Equal(["first-gemini-key", "first-gemini-key"], h.Transport.Keys);
        Assert.DoesNotContain("first-gemini-key", JsonSerializer.Serialize(reread), StringComparison.Ordinal);
    }

    [Fact]
    public void T06_tenant_image_execution_does_not_read_org_settings_or_global_keys()
    {
        foreach (var type in new[] { typeof(TenantGeminiImageExecutionAdapter), typeof(ProductionTenantAiCatalog) })
        {
            var deps = type.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType.Name).ToList();
            Assert.DoesNotContain(deps, d => d.Contains("OrgSettings", StringComparison.OrdinalIgnoreCase)
                || d.Contains("ContentGeminiClient", StringComparison.Ordinal)
                || d.Contains("GeminiProductionStillClient", StringComparison.Ordinal)
                || d.Contains("ContentOptions", StringComparison.Ordinal)
                || d.Contains("Configuration", StringComparison.Ordinal) && !d.Contains("TenantAi", StringComparison.Ordinal));
        }

        var root = RepoRoot();
        var sources = string.Concat(
            File.ReadAllText(Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "Production", "Providers", "TenantGeminiImageExecutionAdapter.cs")),
            File.ReadAllText(Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "Production", "HttpGeminiTenantImageTransport.cs")));
        foreach (var forbidden in new[]
                 {
                     "org_settings", "ai_config_json", "GetOrgSettingsAsync", "ResolveConfigAsync", "ContentAiConfigParser",
                     "ContentGeminiClient", "GEMINI_API_KEY", "Environment.GetEnvironmentVariable", "IConfiguration", "ContentOptions",
                 })
            Assert.DoesNotContain(forbidden, sources, StringComparison.Ordinal);
    }

    [Fact]
    public async Task T07_no_global_gemini_fallback_when_tenant_image_cannot_resolve()
    {
        var h = Open();
        await IssuePermit(h);
        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => h.Svc.CreateImageAsync(Command("t07-missing")));
        Assert.Equal(ProductionTenantAiReasons.ConfigurationRequired, missing.Message);

        var cred = await Register(h, "a-gemini", "tenant-a-gemini-key");
        await h.Settings.SaveAsync(Save(cred.CredentialId));
        await h.Rotation.RevokeVersionAsync(cred.CredentialVersionId);
        var revoked = await Assert.ThrowsAsync<InvalidOperationException>(() => h.Svc.CreateImageAsync(Command("t07-revoked")));
        Assert.Equal(ProductionCredentialResolver.Unresolved, revoked.Message);

        var noAdapter = Open(tenantImage: false);
        Assert.Empty(noAdapter.Catalog.List(ProductionTenantAiCapabilities.Image));
        var notExecutable = await Assert.ThrowsAsync<InvalidOperationException>(() => noAdapter.Settings.SaveAsync(Save(null)));
        Assert.Equal(ProductionTenantAiReasons.ModelNotExecutable, notExecutable.Message);

        Assert.Equal(0, h.Legacy.RequireCount + h.Legacy.GenerationCount);
        Assert.Empty(h.Transport.Keys);
        Assert.Empty(await h.Work.ListTasksByScopeAsync(Build, Sh01.ShotCode));
    }

    [Fact]
    public async Task T08_catalog_and_registration_list_only_the_adapter_model()
    {
        var h = Open();

        var entry = Assert.Single(h.Catalog.List(ProductionTenantAiCapabilities.Image));
        Assert.Equal((Gemini, FlashImage, true, true), (entry.ProviderId, entry.ModelId, entry.IsPaid, entry.RequiresCredential));
        Assert.Equal(h.TenantImage.ModelId, entry.ModelId);
        Assert.Equal((Gemini, FlashImage), ProductionCredentialRegistrationRules.RequireCatalogModel("gemini", FlashImage));
        foreach (var model in new[] { "gemini-2.5-flash-image", "gemini-2.0-flash-preview-image-generation", "imagen-4" })
        {
            var rejected = await Assert.ThrowsAsync<InvalidOperationException>(() => Register(h, "x", "k", model));
            Assert.Equal(ProductionCredentialRegistrationRules.ModelRejected, rejected.Message);
        }
        var noCredential = await Assert.ThrowsAsync<InvalidOperationException>(() => h.Settings.SaveAsync(Save(null)));
        Assert.Equal(ProductionTenantAiReasons.CredentialRequired, noCredential.Message);
        Assert.False(ProductionFrozenCredential.RequiresPin(Gemini));
    }

    [Fact]
    public async Task T09_official_test_and_unwired_paths_keep_their_behavior()
    {
        var h = Open();
        Assert.False(await h.Resolution.GovernsAsync(Sh01 with { BuildId = ProductionScopeAnchorRules.OfficialEp01 }));

        var scopes = new InMemoryProductionScopeFoundationStore();
        var testBuild = Guid.NewGuid();
        await scopes.InsertAnchorAsync(new ProductionScopeAnchor(testBuild, "TEST", null, Guid.NewGuid(), DateTimeOffset.UtcNow));
        var scoped = new ProductionTenantAiResolution(h.Configs, h.Catalog, h.Credentials, h.Selections, scopes);
        Assert.False(await scoped.GovernsAsync(Sh01 with { BuildId = testBuild }));
        Assert.True(await scoped.GovernsAsync(Sh01));

        var legacy = new ProductionCreateVideoService(
            h.Os, h.Work, new InMemoryProductionArtifactFileStore(h.Caller), new FixtureMotionAdapter(),
            officialPermits: h.Permits, geminiImage: new GeminiImageExecutionAdapter(h.Legacy));
        await h.Permits.IssueAsync(new IssueProductionExecutionPermitCommand(
            Build, Sh01.ShotCode, "EP01", "SC01", ProductionActions.CreateImage, Gemini, FlashImage, "test", "legacy"));
        var still = await legacy.CreateImageAsync(Command("t09-legacy"));
        Assert.Equal(1, h.Legacy.RequireCount);
        Assert.Equal(1, h.Legacy.GenerationCount);
        Assert.Null((await SnapshotOf(h, still)).CredentialId);
        Assert.Empty(h.Transport.Keys);

        var fixture = await legacy.CreateImageAsync(Command("t09-fixture", authorize: false));
        Assert.Equal(new FixtureImageAdapter().ProviderId, (await SnapshotOf(h, fixture)).ProviderId);
    }

    static async Task<ProductionTaskInputSnapshot> SnapshotOf(Harness h, CreateVideoSliceResult result) =>
        await h.Work.GetSnapshotByTaskAsync(result.Task.TaskId) ?? throw new InvalidOperationException("snapshot");

    static SaveProductionTenantAiConfigCommand Save(Guid? credentialId, bool enabled = true) =>
        new(ProductionTenantAiCapabilities.Image, Gemini, FlashImage, credentialId, enabled, "test");

    static Task<ProductionCredentialRegistrationResult> Register(Harness h, string name, string secret, string model = FlashImage) =>
        h.Registration.RegisterAsync(new RegisterProductionCredentialCommand(Gemini, model, secret, name));

    static Task IssuePermit(Harness h) =>
        h.Permits.IssueAsync(new IssueProductionExecutionPermitCommand(
            Build, Sh01.ShotCode, "EP01", "SC01", ProductionActions.CreateImage, Gemini, FlashImage, "test", "tenant-image"));

    static ProductionActionApiCommand Command(string idem, bool authorize = true) =>
        new(Sh01, Input(), "test", idem, AuthorizePaidNetwork: authorize);

    static ShotExecutionInput Input() =>
        new("Mother waits in the kitchen at dusk", "beat", "wide kitchen, warm light", "", "", "5", "", "",
            null, "", "16:9", 5, ShotExecutionCompiler.Version, null,
            "line-SC01-CHAR-003-1", "CHAR-003", "");

    static Harness Open(bool tenantImage = true)
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
        var motion = new ProductionMotionAdapterResolver([new FixtureMotionAdapter()]);
        var transport = new RecordingTransport();
        var image = new TenantGeminiImageExecutionAdapter(transport, resolver);
        var catalog = new ProductionTenantAiCatalog(motion, image: tenantImage ? image : null);
        var configs = new InMemoryProductionTenantAiConfigStore(caller);
        var selections = new InMemoryProductionAiSelectionStore();
        var resolution = new ProductionTenantAiResolution(configs, catalog, credentials, selections);
        var permits = new InMemoryProductionExecutionPermitStore();
        var legacy = new LegacyStillClient();
        var os = new ProductionExecutionService(work, new FixtureMotionAdapter(), selections: selections, credentials: resolver, tenantAi: resolution);
        var svc = new ProductionCreateVideoService(
            os, work, new InMemoryProductionArtifactFileStore(caller), new FixtureMotionAdapter(),
            resolver: motion, officialPermits: permits,
            geminiImage: new GeminiImageExecutionAdapter(legacy),
            tenantAi: resolution, tenantImage: tenantImage ? image : null);
        return new Harness(
            caller, credentials, new ProductionCredentialRegistrationService(caller, credentials, rotation), rotation,
            work, catalog, configs, selections,
            new ProductionTenantAiSettingsService(caller, configs, catalog, credentials),
            resolution, permits, os, svc, image, transport, legacy);
    }

    sealed record Harness(
        SwitchCaller Caller,
        InMemoryProductionCredentialStore Credentials,
        IProductionCredentialRegistration Registration,
        IProductionCredentialRotation Rotation,
        InMemoryProductionWorkStore Work,
        IProductionTenantAiCatalog Catalog,
        IProductionTenantAiConfigStore Configs,
        InMemoryProductionAiSelectionStore Selections,
        IProductionTenantAiSettings Settings,
        IProductionTenantAiResolution Resolution,
        InMemoryProductionExecutionPermitStore Permits,
        ProductionExecutionService Os,
        ProductionCreateVideoService Svc,
        TenantGeminiImageExecutionAdapter TenantImage,
        RecordingTransport Transport,
        LegacyStillClient Legacy);

    sealed class SwitchCaller : IProductionCallerTenant
    {
        public Guid Id { get; set; }
        public Guid RequireTenant() => ProductionTenantBoundary.Require(Id);
    }

    static byte[] Png()
    {
        var bytes = new byte[64];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        RandomNumberGenerator.Fill(bytes.AsSpan(8));
        return bytes;
    }

    sealed class RecordingTransport : IGeminiTenantImageTransport
    {
        public List<string> Keys { get; } = [];
        public List<string> Models { get; } = [];
        public int GenerationCount => Keys.Count;

        public Task<GeminiProductionStillResult> GenerateOnceAsync(
            string apiKey, string modelId, string prompt, string? aspectRatio, CancellationToken ct = default)
        {
            Keys.Add(apiKey);
            Models.Add(modelId);
            return Task.FromResult(new GeminiProductionStillResult(true, Png(), "image/png", modelId, null));
        }
    }

    sealed class LegacyStillClient : IGeminiProductionStillClient
    {
        public int RequireCount { get; private set; }
        public int GenerationCount { get; private set; }

        public Task<string> RequireModelAsync(CancellationToken ct = default)
        {
            RequireCount++;
            return Task.FromResult(FlashImage);
        }

        public Task<GeminiProductionStillResult> GenerateOnceAsync(string prompt, string? aspectRatio, CancellationToken ct = default)
        {
            GenerationCount++;
            return Task.FromResult(new GeminiProductionStillResult(true, Png(), "image/png", FlashImage, null));
        }
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "KitPlatform.Api", "KitPlatform.Api.csproj")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root");
    }
}
