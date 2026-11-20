using System.Security.Cryptography;
using System.Text.Json;
using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

/// <summary>
/// FAMIXA-FILM-PRODUCTION-34. In-memory tenant credentials only. No provider call and no live FAMIXA credential.
/// </summary>
public sealed class P34CredentialUnification
{
    static readonly Guid TenantA = Guid.Parse("4028941b-49b3-4ba2-8815-778daef4a2d3");
    static readonly Guid TenantB = Guid.Parse("11111111-1111-4111-8111-111111111111");
    const string Secret = "p34-test-secret-not-a-provider-key";
    static readonly ProductionScope Scope = new(
        Guid.Parse("30ba4061-a443-4c0d-85aa-1d13ff4fdedb"), "EP01", "SC01", "EP01-SC01-SH01");

    [Fact]
    public async Task Tenant_a_create_video_pins_the_active_runway_credential_without_an_explicit_reference()
    {
        var caller = new SwitchCaller { Id = TenantA };
        var harness = Open(caller);
        var registered = await harness.Registration.RegisterAsync(new RegisterProductionCredentialCommand(
            "runway", "gen4_turbo", Secret, "runway"));

        var listed = await harness.Registration.ListAsync();
        Assert.Single(listed);
        Assert.Equal(registered.CredentialId, listed[0].CredentialId);
        Assert.Equal(registered.CredentialVersionId, listed[0].CredentialVersionId);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(listed), StringComparison.Ordinal);
        Assert.DoesNotContain("ciphertext", JsonSerializer.Serialize(listed), StringComparison.OrdinalIgnoreCase);

        var active = await harness.Resolver.ResolveActiveAsync(TenantA, "runway", "gen4_turbo");
        Assert.Equal(Secret, active.Secret);
        Assert.DoesNotContain(Secret, active.ToString(), StringComparison.Ordinal);

        var engine = ProductionWorkExecutionEngine.CreateOfficial(new InMemoryProductionWorkStore(), harness.Resolver);
        var frozen = await Freeze(engine);
        Assert.Equal(registered.CredentialId, frozen.CredentialId);
        Assert.Equal(registered.CredentialVersionId, frozen.CredentialVersionId);
        Assert.Equal("runway", frozen.ProviderId);
        Assert.Equal("gen4_turbo", frozen.ModelId);
        var text = frozen.ToString();
        Assert.DoesNotContain(Secret, text, StringComparison.Ordinal);
        Assert.DoesNotContain("ciphertext", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nonce", text, StringComparison.OrdinalIgnoreCase);

        var explicitFrozen = await Freeze(engine, new ProductionCredentialReference(
            registered.CredentialId, registered.CredentialVersionId));
        Assert.Equal(registered.CredentialVersionId, explicitFrozen.CredentialVersionId);
    }

    [Fact]
    public async Task Tenant_b_cannot_read_or_resolve_tenant_a_credential()
    {
        var owner = Open(new SwitchCaller { Id = TenantA });
        var registered = await owner.Registration.RegisterAsync(new RegisterProductionCredentialCommand(
            "runway", "gen4_turbo", Secret, "runway"));

        var other = Open(new SwitchCaller { Id = TenantB });
        Assert.Null(await other.Store.GetAsync(registered.CredentialId));
        Assert.Empty(await other.Registration.ListAsync());
        var forbidden = false;
        try
        {
            await other.Resolver.ResolveActiveAsync(TenantA, "runway", "gen4_turbo");
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionTenantBoundary.Forbidden
            && !ex.ToString().Contains(Secret, StringComparison.Ordinal))
        {
            forbidden = true;
        }
        Assert.True(forbidden);

        var missing = false;
        try
        {
            await other.Resolver.ResolveActiveAsync(TenantB, "runway", "gen4_turbo");
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialResolver.Unresolved)
        {
            missing = true;
        }
        Assert.True(missing);
    }

    [Fact]
    public async Task Rotation_supersedes_v1_and_the_next_automatic_freeze_uses_v2()
    {
        var harness = Open(new SwitchCaller { Id = TenantA });
        var registered = await harness.Registration.RegisterAsync(new RegisterProductionCredentialCommand(
            "runway", "gen4_turbo", Secret, "runway"));
        var store = new InMemoryProductionWorkStore();
        var engine = ProductionWorkExecutionEngine.CreateOfficial(store, harness.Resolver);
        var frozen = await Freeze(engine);
        Assert.Equal(registered.CredentialVersionId, frozen.CredentialVersionId);

        var rotated = await harness.Registration.AddVersionAsync(new AddProductionCredentialVersionCommand(
            registered.CredentialId, "runway", "gen4_turbo", "p34-rotated-secret-not-a-key"));
        Assert.Equal(2, rotated.Version);
        Assert.Equal(ProductionCredentialStatuses.Active, rotated.Status);
        Assert.Equal(ProductionCredentialStatuses.Superseded, (await harness.Store.GetVersionAsync(registered.CredentialVersionId))!.Status);
        Assert.Equal(registered.CredentialVersionId, (await store.GetSnapshotByTaskAsync(frozen.TaskId))!.CredentialVersionId);

        var stillOld = await Freeze(engine, new ProductionCredentialReference(
            registered.CredentialId, registered.CredentialVersionId));
        Assert.Equal(registered.CredentialVersionId, stillOld.CredentialVersionId);
        Assert.DoesNotContain("p34-rotated-secret-not-a-key", stillOld.ToString(), StringComparison.Ordinal);

        var next = await Freeze(engine);
        Assert.Equal(rotated.CredentialVersionId, next.CredentialVersionId);
        Assert.DoesNotContain("p34-rotated-secret-not-a-key", next.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Zero_credentials_and_two_credential_identities_fail_closed()
    {
        var empty = Open(new SwitchCaller { Id = TenantA });
        var emptyEngine = ProductionWorkExecutionEngine.CreateOfficial(new InMemoryProductionWorkStore(), empty.Resolver);
        var zero = false;
        try
        {
            await Freeze(emptyEngine);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialResolver.Unresolved)
        {
            zero = true;
        }
        Assert.True(zero);

        var harness = Open(new SwitchCaller { Id = TenantA });
        await harness.Registration.RegisterAsync(new RegisterProductionCredentialCommand("runway", "gen4_turbo", Secret, "one"));
        await harness.Registration.RegisterAsync(new RegisterProductionCredentialCommand("runway", "gen4_turbo", "p34-second-identity-secret", "two"));
        var engine = ProductionWorkExecutionEngine.CreateOfficial(new InMemoryProductionWorkStore(), harness.Resolver);
        var many = false;
        try
        {
            await Freeze(engine);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialResolver.Ambiguous)
        {
            many = true;
        }
        Assert.True(many);
    }

    [Fact]
    public void Production_resolution_does_not_read_org_settings_and_routes_stay_admin_only()
    {
        var root = RepoRoot();
        foreach (var relative in new[]
        {
            "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCredentialResolution.cs",
            "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionWorkExecutionEngine.cs",
            "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCredentialRegistration.cs",
            "src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/Production/ProductionCredentialRepository.cs",
        })
        {
            var text = File.ReadAllText(Path.Combine(root, relative));
            Assert.DoesNotContain("GetOrgSettingsAsync", text, StringComparison.Ordinal);
            Assert.DoesNotContain("video_config_json", text, StringComparison.Ordinal);
            Assert.DoesNotContain("runwayApiKey", text, StringComparison.Ordinal);
            Assert.DoesNotContain("FROM pack_content.org_settings", text, StringComparison.Ordinal);
        }

        var runway = ProductionCredentialLegacyCatalog.Inventory().Single(row =>
            row.Source == "pack_content.org_settings.video_config_json" && row.Field == "runwayApiKey");
        Assert.Equal(ProductionCredentialLegacyClass.UnscopedUnknown, runway.Class);
        Assert.Equal(0, ProductionCredentialLegacyCatalog.EligibleCount());

        var controller = File.ReadAllText(Path.Combine(root, "src", "KitPlatform.Api", "Controllers", "Content", "ContentProductionController.cs"));
        Assert.Contains("[Authorize(Roles = \"ADMIN\")]", controller, StringComparison.Ordinal);
        Assert.Contains("[HttpGet(\"credentials\")]", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("[AllowAnonymous]", controller, StringComparison.Ordinal);
        var caller = File.ReadAllText(Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "Production", "HttpProductionCallerTenant.cs"));
        Assert.Contains("!_tenant.IsAuthenticated", caller, StringComparison.Ordinal);
        Assert.Contains("UnauthorizedAccessException", caller, StringComparison.Ordinal);

        var center = File.ReadAllText(Path.Combine(root, "client", "admin", "src", "modules", "content", "FamixaProductionCredentialCenter.tsx"));
        var status = File.ReadAllText(Path.Combine(root, "client", "admin", "src", "modules", "content", "FamixaProductionCredentialStatus.tsx"));
        var picker = File.ReadAllText(Path.Combine(root, "client", "admin", "src", "modules", "content", "FamixaMotionEnginePicker.tsx"));
        var settings = File.ReadAllText(Path.Combine(root, "client", "admin", "src", "modules", "content", "ContentAiSettingsPage.tsx"));
        var marketing = File.ReadAllText(Path.Combine(root, "client", "admin", "src", "modules", "content", "ContentMarketingAiSettingsPage.tsx"));
        Assert.Contains("data-production-credential-center", center, StringComparison.Ordinal);
        Assert.DoesNotContain("dataIndex: 'secret'", center, StringComparison.Ordinal);
        Assert.DoesNotContain("dataIndex: \"secret\"", center, StringComparison.Ordinal);
        Assert.Contains("data-production-credential-status", status, StringComparison.Ordinal);
        Assert.Contains("to=\"/content/ai\"", status, StringComparison.Ordinal);
        Assert.Contains("Not configured", status, StringComparison.Ordinal);
        Assert.Contains("PRODUCTION_CREDENTIAL_AMBIGUOUS", status, StringComparison.Ordinal);
        Assert.DoesNotContain("runwayApiKey", picker, StringComparison.Ordinal);
        Assert.DoesNotContain("Input.Password", picker, StringComparison.Ordinal);
        Assert.Contains("FamixaProductionCredentialStatus", picker, StringComparison.Ordinal);
        // /content/ai is tenant production Model AI. Legacy org-settings keys stay on Marketing AI.
        Assert.Contains("<TenantAiConfigurationCard", settings, StringComparison.Ordinal);
        Assert.Contains("submitModelAiCredential", settings, StringComparison.Ordinal);
        Assert.Contains("production-ai-catalog.api", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("data-legacy-ai-settings", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("runwayApiKey", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("fetchContentSettings", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("updateContentSettings", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("org_settings", settings, StringComparison.Ordinal);
        Assert.Contains("runwayApiKey", marketing, StringComparison.Ordinal);
        Assert.DoesNotContain("production-ai-catalog", marketing, StringComparison.Ordinal);
        Assert.DoesNotContain("FamixaProductionCredentialCenter", marketing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unauthenticated_caller_is_rejected()
    {
        var harness = Open(new SwitchCaller { Id = Guid.Empty });
        var rejected = false;
        try
        {
            await harness.Resolver.ResolveActiveAsync("runway", "gen4_turbo");
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionTenantBoundary.Required)
        {
            rejected = true;
        }
        Assert.True(rejected);
    }

    static async Task<ProductionTaskInputSnapshot> Freeze(
        ProductionWorkExecutionEngine engine, ProductionCredentialReference? credential = null)
    {
        var request = await engine.CreateRequestAsync(
            Scope, ProductionActions.CreateVideo, "p34", "p34-" + Guid.NewGuid().ToString("N"));
        var task = await engine.CreateTaskAsync(request.RequestId);
        return await engine.FreezeInputAsync(
            task.TaskId,
            new ShotExecutionInput("story", "acting", "visual", "", "kf", "5", "", ""),
            "runway",
            "gen4_turbo",
            credential: credential);
    }

    static Harness Open(IProductionCallerTenant caller)
    {
        var key = new byte[ProductionCredentialSecretEnvelope.KeySize];
        RandomNumberGenerator.Fill(key);
        var ring = new MapProductionCredentialKeyRing("v1", new Dictionary<string, byte[]> { ["v1"] = key });
        var store = new InMemoryProductionCredentialStore(caller);
        var secrets = new InMemoryProductionCredentialSecretStore(caller, store, ring);
        var rotation = new InMemoryProductionCredentialRotation(store, secrets, ring);
        return new Harness(
            store,
            new ProductionCredentialResolver(store, secrets, caller),
            new ProductionCredentialRegistrationService(caller, store, rotation));
    }

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

    sealed record Harness(
        InMemoryProductionCredentialStore Store,
        ProductionCredentialResolver Resolver,
        ProductionCredentialRegistrationService Registration);
}
