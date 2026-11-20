using System.Security.Cryptography;
using System.Text.Json;
using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

/// <summary>
/// FAMIXA-FILM-PRODUCTION-31. In-memory registration only. No provider call and no live credential.
/// </summary>
public sealed class P31CredentialRegistration
{
    static readonly Guid Famixa = Guid.Parse("4028941b-49b3-4ba2-8815-778daef4a2d3");
    static readonly Guid KitMkt = Guid.Parse("11111111-1111-4111-8111-111111111111");
    const string Secret = "p31-test-secret-not-a-provider-key";

    [Fact]
    public async Task Famixa_admin_registers_a_pinnable_runway_credential_without_returning_the_secret()
    {
        var caller = new SwitchCaller { Id = Famixa };
        var harness = Open(caller);
        var registered = await harness.Registration.RegisterAsync(new RegisterProductionCredentialCommand(
            "Runway", "GEN4_TURBO", Secret, "runway"));

        Assert.Equal(ProductionMotionCatalog.RunwayProviderId, registered.ProviderId);
        Assert.Equal(ProductionMotionCatalog.RunwayGen4Turbo, registered.ModelId);
        Assert.Equal(ProductionCredentialStatuses.Active, registered.Status);
        Assert.Equal(1, registered.Version);
        Assert.NotEqual(Guid.Empty, registered.CredentialId);
        Assert.NotEqual(Guid.Empty, registered.CredentialVersionId);

        var identity = await harness.Store.GetAsync(registered.CredentialId);
        var version = await harness.Versions.GetVersionAsync(registered.CredentialVersionId);
        Assert.NotNull(identity);
        Assert.Equal(Famixa, identity.TenantId);
        Assert.Equal(ProductionCredentialStatuses.Active, identity.Status);
        Assert.NotNull(version);
        Assert.Equal(Famixa, version.TenantId);
        Assert.Equal(ProductionCredentialStatuses.Active, version.Status);
        Assert.Equal("runway", version.ProviderId);
        Assert.Equal("gen4_turbo", version.ModelId);

        var json = JsonSerializer.Serialize(registered);
        Assert.DoesNotContain(Secret, json, StringComparison.Ordinal);
        Assert.DoesNotContain("ciphertext", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nonce", json, StringComparison.OrdinalIgnoreCase);

        var material = await harness.Resolver.ResolveAsync(
            registered.CredentialId, registered.CredentialVersionId, "runway", "gen4_turbo");
        Assert.Equal(Secret, material.Secret);
        Assert.DoesNotContain(Secret, material.ToString(), StringComparison.Ordinal);

        var engine = ProductionWorkExecutionEngine.CreateOfficial(new InMemoryProductionWorkStore(), harness.Resolver);
        var scope = new ProductionScope(
            Guid.Parse("30ba4061-a443-4c0d-85aa-1d13ff4fdedb"), "EP01", "SC01", "EP01-SC01-SH01");
        var request = await engine.CreateRequestAsync(scope, ProductionActions.CreateVideo, "p31", "p31-" + Guid.NewGuid().ToString("N"));
        var task = await engine.CreateTaskAsync(request.RequestId);
        var frozen = await engine.FreezeInputAsync(
            task.TaskId,
            new ShotExecutionInput("story", "acting", "visual", "", "kf", "5", "", ""),
            "runway",
            "gen4_turbo",
            credential: new ProductionCredentialReference(registered.CredentialId, registered.CredentialVersionId));
        Assert.Equal(registered.CredentialId, frozen.CredentialId);
        Assert.Equal(registered.CredentialVersionId, frozen.CredentialVersionId);
        Assert.DoesNotContain(Secret, frozen.ToString(), StringComparison.Ordinal);

        var bare = await engine.CreateRequestAsync(scope, ProductionActions.CreateVideo, "p31", "p31-bare-" + Guid.NewGuid().ToString("N"));
        var bareTask = await engine.CreateTaskAsync(bare.RequestId);
        var automatic = await engine.FreezeInputAsync(
            bareTask.TaskId,
            new ShotExecutionInput("story", "acting", "visual", "", "kf", "5", "", ""),
            "runway",
            "gen4_turbo");
        Assert.Equal(registered.CredentialId, automatic.CredentialId);
        Assert.Equal(registered.CredentialVersionId, automatic.CredentialVersionId);
        Assert.DoesNotContain(Secret, automatic.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tenant_comes_from_the_caller_and_another_tenant_cannot_use_the_credential()
    {
        var famixa = new SwitchCaller { Id = Famixa };
        var harness = Open(famixa);
        var registered = await harness.Registration.RegisterAsync(new RegisterProductionCredentialCommand(
            "runway", "gen4_turbo", Secret, null));
        Assert.Equal(Famixa, (await harness.Store.GetAsync(registered.CredentialId))!.TenantId);

        var kit = new SwitchCaller { Id = KitMkt };
        var outsider = Open(kit);
        Assert.Null(await outsider.Store.GetAsync(registered.CredentialId));
        var cross = false;
        try
        {
            await outsider.Registration.AddVersionAsync(new AddProductionCredentialVersionCommand(
                registered.CredentialId, "runway", "gen4_turbo", "other-tenant-secret"));
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialOwnership.VersionCredentialRequired)
        {
            cross = true;
        }
        Assert.True(cross);

        var own = await outsider.Registration.RegisterAsync(new RegisterProductionCredentialCommand(
            "runway", "gen4_turbo", "kit-mkt-test-secret", null));
        Assert.Equal(KitMkt, (await outsider.Store.GetAsync(own.CredentialId))!.TenantId);
        Assert.NotEqual(registered.CredentialId, own.CredentialId);
    }

    [Fact]
    public async Task Missing_tenant_unsupported_pair_and_duplicate_identity_are_rejected()
    {
        var missing = new SwitchCaller { Id = Guid.Empty };
        var missingRejected = false;
        try
        {
            await Open(missing).Registration.RegisterAsync(new RegisterProductionCredentialCommand(
                "runway", "gen4_turbo", Secret, null));
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionTenantBoundary.Required)
        {
            missingRejected = true;
        }
        Assert.True(missingRejected);

        var httpTenant = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "Production", "HttpProductionCallerTenant.cs"));
        Assert.Contains("!_tenant.IsAuthenticated", httpTenant, StringComparison.Ordinal);
        Assert.Contains("UnauthorizedAccessException", httpTenant, StringComparison.Ordinal);
        Assert.Contains("_tenant.TenantId", httpTenant, StringComparison.Ordinal);
        Assert.DoesNotContain("TenantId", File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "Production", "ProductionCredentialRegistration.cs")).Split("RequireTenant()")[0], StringComparison.Ordinal);

        var harness = Open(new SwitchCaller { Id = Famixa });
        await AssertRejected(harness, "gemini", "gemini-2.5-flash-image", ProductionCredentialRegistrationRules.ModelRejected);
        await AssertRejected(harness, "gemini", "gemini-3.6-flash", ProductionCredentialRegistrationRules.ModelRejected);
        await AssertRejected(harness, "", "gen4_turbo", ProductionCredentialRegistrationRules.ProviderRejected);
        await AssertRejected(harness, "fixture", "fixture-motion-v1", ProductionCredentialRegistrationRules.ProviderRejected);
        await AssertRejected(harness, "fixture", "fixture-voice-v1", ProductionCredentialRegistrationRules.ProviderRejected);
        await AssertRejected(harness, "fixture", "fixture-lipsync-v1", ProductionCredentialRegistrationRules.ProviderRejected);
        await AssertRejected(harness, "google", "gen4_turbo", ProductionCredentialRegistrationRules.ModelRejected);
        await AssertRejected(harness, "fal", "fal-ai/sync-lipsync", ProductionCredentialRegistrationRules.ModelRejected);
        await AssertRejected(harness, "runway", "gen3", ProductionCredentialRegistrationRules.ModelRejected);
        await AssertRejected(harness, "runway", "veo-3.1", ProductionCredentialRegistrationRules.ModelRejected);
        await AssertRejected(harness, "elevenlabs", "sync-lipsync-v3", ProductionCredentialRegistrationRules.ModelRejected);

        var registered = await harness.Registration.RegisterAsync(new RegisterProductionCredentialCommand(
            "runway", "gen4_turbo", Secret, "runway"));
        var duplicate = false;
        try
        {
            await harness.Store.CreateAsync(ProductionCredentialOwnership.Create(
                registered.CredentialId, Famixa, "again"));
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialOwnership.IdentityExists)
        {
            duplicate = true;
        }
        Assert.True(duplicate);

        var second = await harness.Registration.AddVersionAsync(new AddProductionCredentialVersionCommand(
            registered.CredentialId, "runway", "gen4_turbo", "p31-second-version-secret"));
        Assert.Equal(2, second.Version);
        Assert.NotEqual(registered.CredentialVersionId, second.CredentialVersionId);
        Assert.Equal(ProductionCredentialStatuses.Active, second.Status);
    }

    [Fact]
    public void Admin_route_takes_tenant_from_the_caller_and_does_not_return_secret_material()
    {
        var controller = File.ReadAllText(Path.Combine(RepoRoot(), "src", "KitPlatform.Api", "Controllers", "Content", "ContentProductionController.cs"));
        var registration = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "Production", "ProductionCredentialRegistration.cs"));
        Assert.Contains("[Authorize(Roles = \"ADMIN\")]", controller, StringComparison.Ordinal);
        Assert.Contains("[HttpPost(\"credentials\")]", controller, StringComparison.Ordinal);
        Assert.Contains("[HttpPost(\"credentials/{credentialId:guid}/versions\")]", controller, StringComparison.Ordinal);
        Assert.Contains("[HttpGet(\"credentials\")]", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("[AllowAnonymous]", controller, StringComparison.Ordinal);
        Assert.Contains("ProductionTenantActionFilter", controller, StringComparison.Ordinal);
        Assert.False(typeof(RegisterProductionCredentialRequestDto).GetProperties().Any(p => p.Name == "TenantId"));
        Assert.False(typeof(ProductionCredentialRegistrationDto).GetProperties().Any(p =>
            p.Name is "Secret" or "Ciphertext" or "Nonce" or "Tag" or "TenantId"));
        Assert.False(typeof(ProductionCredentialCatalogDto).GetProperties().Any(p =>
            p.Name is "Secret" or "Ciphertext" or "Nonce" or "Tag" or "TenantId"));
        Assert.Contains("RequireTenant()", registration, StringComparison.Ordinal);
        Assert.DoesNotContain("org_settings", registration, StringComparison.Ordinal);
        Assert.DoesNotContain("video_config_json", registration, StringComparison.Ordinal);
        Assert.DoesNotContain("RUNWAY_API_KEY", registration, StringComparison.Ordinal);

        Assert.False(ProductionFrozenCredential.RequiresPin("GEMINI"));
        Assert.True(ProductionFrozenCredential.RequiresPin("elevenlabs"));
        Assert.True(ProductionFrozenCredential.RequiresPin("runway"));
        Assert.False(ProductionPaidNetworkGate.AllowsUnpreparedPaidStart(Famixa, "EP01", false, true));
        Assert.False(ProductionPaidNetworkGate.AllowsUnpreparedPaidStart(Famixa, "EP01", true, false));
        var runway = ProductionCredentialLegacyCatalog.Inventory().Single(row =>
            row.Source == "pack_content.org_settings.video_config_json" && row.Field == "runwayApiKey");
        Assert.Equal(ProductionCredentialLegacyClass.UnscopedUnknown, runway.Class);
        Assert.Equal(0, ProductionCredentialLegacyCatalog.EligibleCount());
    }

    [Theory]
    [InlineData("runway", "gen4_turbo", "Runway", "GEN4_TURBO")]
    [InlineData("google", "veo-3.1", "Google", "VEO-3.1")]
    [InlineData("elevenlabs", "eleven_v3", "ElevenLabs", "Eleven_V3")]
    [InlineData("fal", "sync-lipsync-v3", "FAL", "Sync-Lipsync-V3")]
    public async Task Every_canonical_production_model_registers_with_canonical_ids_and_no_secret(
        string provider, string model, string inputProvider, string inputModel)
    {
        var harness = Open(new SwitchCaller { Id = Famixa });
        var registered = await harness.Registration.RegisterAsync(new RegisterProductionCredentialCommand(
            inputProvider, inputModel, Secret, null));

        Assert.Equal(provider, registered.ProviderId);
        Assert.Equal(model, registered.ModelId);
        Assert.Equal(ProductionCredentialStatuses.Active, registered.Status);
        Assert.Equal(1, registered.Version);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(registered), StringComparison.Ordinal);

        var material = await harness.Resolver.ResolveActiveAsync(Famixa, provider, model);
        Assert.Equal(registered.CredentialVersionId, material.CredentialVersionId);
        Assert.DoesNotContain(Secret, material.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Disabled_catalog_entry_is_rejected()
    {
        var disabled = new ProductionMotionModel(
            "runway", "gen4_turbo", KitPlatform.Packs.Content.FamixaProviderCapability.Motion,
            "Runway Gen-4 Turbo", ProductionActions.CreateVideo, Enabled: false, IsPaid: true);
        var rejected = Assert.Throws<InvalidOperationException>(() =>
            ProductionCredentialRegistrationRules.RequireCatalogModel("runway", "gen4_turbo", [disabled]));
        Assert.Equal(ProductionCredentialRegistrationRules.ModelRejected, rejected.Message);
        Assert.Equal(("runway", "gen4_turbo"), ProductionCredentialRegistrationRules.RequireCatalogModel("runway", "gen4_turbo"));
    }

    [Fact]
    public async Task Rotation_keeps_the_credential_provider_and_model()
    {
        var harness = Open(new SwitchCaller { Id = Famixa });
        var runway = await harness.Registration.RegisterAsync(new RegisterProductionCredentialCommand(
            "runway", "gen4_turbo", Secret, null));

        var crossed = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Registration.AddVersionAsync(
            new AddProductionCredentialVersionCommand(runway.CredentialId, "fal", "sync-lipsync-v3", "p31-fal-secret")));
        Assert.Equal(ProductionCredentialRegistrationRules.ProviderModelImmutable, crossed.Message);
        Assert.DoesNotContain("p31-fal-secret", crossed.ToString(), StringComparison.Ordinal);
        var afterReject = (await harness.Registration.ListAsync()).Where(r => r.CredentialId == runway.CredentialId).ToList();
        Assert.Single(afterReject);
        Assert.Equal(ProductionCredentialStatuses.Active, afterReject[0].Status);

        var runway2 = await harness.Registration.AddVersionAsync(new AddProductionCredentialVersionCommand(
            runway.CredentialId, "Runway", "GEN4_TURBO", "p31-runway-v2-secret"));
        Assert.Equal(runway.CredentialId, runway2.CredentialId);
        Assert.Equal(2, runway2.Version);
        Assert.Equal("runway", runway2.ProviderId);
        Assert.Equal("gen4_turbo", runway2.ModelId);
        Assert.DoesNotContain("p31-runway-v2-secret", JsonSerializer.Serialize(runway2), StringComparison.Ordinal);

        var fal = await harness.Registration.RegisterAsync(new RegisterProductionCredentialCommand(
            "fal", "sync-lipsync-v3", Secret, null));
        var fal2 = await harness.Registration.AddVersionAsync(new AddProductionCredentialVersionCommand(
            fal.CredentialId, "fal", "sync-lipsync-v3", "p31-fal-v2-secret"));
        Assert.Equal(fal.CredentialId, fal2.CredentialId);
        Assert.Equal(2, fal2.Version);
        Assert.Equal("fal", fal2.ProviderId);
        Assert.Equal("sync-lipsync-v3", fal2.ModelId);

        var falToRunway = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Registration.AddVersionAsync(
            new AddProductionCredentialVersionCommand(fal.CredentialId, "runway", "gen4_turbo", "p31-runway-secret")));
        Assert.Equal(ProductionCredentialRegistrationRules.ProviderModelImmutable, falToRunway.Message);
    }

    [Fact]
    public async Task List_returns_all_canonical_tenant_credentials_and_stays_tenant_scoped()
    {
        var famixa = new SwitchCaller { Id = Famixa };
        var harness = Open(famixa);
        foreach (var (provider, model) in new[]
                 {
                     ("runway", "gen4_turbo"), ("google", "veo-3.1"),
                     ("elevenlabs", "eleven_v3"), ("fal", "sync-lipsync-v3"),
                 })
        {
            await harness.Registration.RegisterAsync(new RegisterProductionCredentialCommand(provider, model, Secret, null));
        }

        var legacy = ProductionCredentialOwnership.Create(Guid.NewGuid(), Famixa, "legacy");
        await harness.Store.CreateAsync(legacy);
        await harness.Store.AddVersionAsync(ProductionCredentialOwnership.CreateVersion(
            Guid.NewGuid(), legacy.CredentialId, Famixa, 1, "elevenlabs", "default"));

        var rows = await harness.Registration.ListAsync();
        Assert.Equal(4, rows.Count);
        Assert.Equal(
            ["elevenlabs/eleven_v3", "fal/sync-lipsync-v3", "google/veo-3.1", "runway/gen4_turbo"],
            rows.Select(r => $"{r.ProviderId}/{r.ModelId}").Order().ToArray());
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(rows), StringComparison.Ordinal);

        famixa.Id = KitMkt;
        Assert.Empty(await harness.Registration.ListAsync());
        var falOnly = await harness.Registration.RegisterAsync(new RegisterProductionCredentialCommand(
            "fal", "sync-lipsync-v3", "kit-mkt-test-secret", null));
        var kitRows = await harness.Registration.ListAsync();
        Assert.Single(kitRows);
        Assert.Equal(falOnly.CredentialId, kitRows[0].CredentialId);

        famixa.Id = Famixa;
        Assert.DoesNotContain(await harness.Registration.ListAsync(), r => r.CredentialId == falOnly.CredentialId);
    }

    [Theory]
    [InlineData("runway", "gen4_turbo")]
    [InlineData("fal", "sync-lipsync-v3")]
    [InlineData("google", "veo-3.1")]
    [InlineData("elevenlabs", "eleven_v3")]
    public async Task Rotate_by_model_keeps_identity_and_adds_a_version(string provider, string model)
    {
        var harness = Open(new SwitchCaller { Id = Famixa });
        var v1 = await harness.Registration.RegisterAsync(new RegisterProductionCredentialCommand(provider, model, Secret, null));
        const string next = "p31-rotate-by-model-secret";

        var v2 = await harness.Registration.RotateActiveAsync(new RotateProductionCredentialCommand(
            provider.ToUpperInvariant(), model.ToUpperInvariant(), next));

        Assert.Equal(v1.CredentialId, v2.CredentialId);
        Assert.NotEqual(v1.CredentialVersionId, v2.CredentialVersionId);
        Assert.Equal(2, v2.Version);
        Assert.Equal(provider, v2.ProviderId);
        Assert.Equal(model, v2.ModelId);
        Assert.Equal(ProductionCredentialStatuses.Active, v2.Status);
        Assert.DoesNotContain(next, JsonSerializer.Serialize(v2), StringComparison.Ordinal);
        Assert.DoesNotContain(next, JsonSerializer.Serialize(
            new ProductionCredentialRotationDto(v2.ProviderId, v2.ModelId, v2.Status, v2.Version)), StringComparison.Ordinal);

        var rows = await harness.Registration.ListAsync();
        Assert.Equal([v1.CredentialId], rows.Select(r => r.CredentialId).Distinct().ToArray());
        Assert.Equal(ProductionCredentialStatuses.Superseded, rows.Single(r => r.Version == 1).Status);
        Assert.Equal(ProductionCredentialStatuses.Active, rows.Single(r => r.Version == 2).Status);
        var eligible = await harness.Store.ListEligibleAsync(provider, model);
        Assert.Single(eligible);
        Assert.Equal(v2.CredentialVersionId, eligible[0].CredentialVersionId);
        var material = await harness.Resolver.ResolveActiveAsync(Famixa, provider, model);
        Assert.Equal(v2.CredentialVersionId, material.CredentialVersionId);
    }

    [Fact]
    public async Task Rotate_by_model_without_a_matching_credential_is_not_configured()
    {
        var harness = Open(new SwitchCaller { Id = Famixa });
        var runway = await harness.Registration.RegisterAsync(new RegisterProductionCredentialCommand(
            "runway", "gen4_turbo", Secret, null));

        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Registration.RotateActiveAsync(
            new RotateProductionCredentialCommand("fal", "sync-lipsync-v3", "p31-missing-secret")));
        Assert.Equal(ProductionCredentialResolver.Unresolved, missing.Message);
        Assert.DoesNotContain("p31-missing-secret", missing.ToString(), StringComparison.Ordinal);

        var rows = await harness.Registration.ListAsync();
        Assert.Single(rows);
        Assert.Equal(runway.CredentialVersionId, rows[0].CredentialVersionId);
        Assert.Equal(ProductionCredentialStatuses.Active, rows[0].Status);

        var fixture = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Registration.RotateActiveAsync(
            new RotateProductionCredentialCommand("fixture", "fixture-motion-v1", "p31-missing-secret")));
        Assert.Equal(ProductionCredentialRegistrationRules.ProviderRejected, fixture.Message);
    }

    [Fact]
    public async Task Rotate_by_model_with_several_active_credentials_is_ambiguous_and_rotates_nothing()
    {
        var harness = Open(new SwitchCaller { Id = Famixa });
        var first = await harness.Registration.RegisterAsync(new RegisterProductionCredentialCommand(
            "fal", "sync-lipsync-v3", Secret, "fal-a"));
        var second = await harness.Registration.RegisterAsync(new RegisterProductionCredentialCommand(
            "fal", "sync-lipsync-v3", Secret, "fal-b"));

        var ambiguous = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Registration.RotateActiveAsync(
            new RotateProductionCredentialCommand("fal", "sync-lipsync-v3", "p31-ambiguous-secret")));
        Assert.Equal(ProductionCredentialResolver.Ambiguous, ambiguous.Message);

        var rows = await harness.Registration.ListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(1, r.Version));
        Assert.All(rows, r => Assert.Equal(ProductionCredentialStatuses.Active, r.Status));
        Assert.Equal(
            new[] { first.CredentialVersionId, second.CredentialVersionId }.Order().ToArray(),
            rows.Select(r => r.CredentialVersionId).Order().ToArray());
    }

    [Fact]
    public async Task Rotate_by_model_cannot_reach_another_tenant_or_change_provider_model()
    {
        var caller = new SwitchCaller { Id = KitMkt };
        var harness = Open(caller);
        var kitMkt = await harness.Registration.RegisterAsync(new RegisterProductionCredentialCommand(
            "runway", "gen4_turbo", "kit-mkt-test-secret", null));

        caller.Id = Famixa;
        var foreign = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Registration.RotateActiveAsync(
            new RotateProductionCredentialCommand("runway", "gen4_turbo", "p31-foreign-secret")));
        Assert.Equal(ProductionCredentialResolver.Unresolved, foreign.Message);

        var famixa = await harness.Registration.RegisterAsync(new RegisterProductionCredentialCommand(
            "runway", "gen4_turbo", Secret, null));
        var crossed = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Registration.RotateActiveAsync(
            new RotateProductionCredentialCommand("fal", "sync-lipsync-v3", "p31-crossed-secret")));
        Assert.Equal(ProductionCredentialResolver.Unresolved, crossed.Message);
        var rotated = await harness.Registration.RotateActiveAsync(
            new RotateProductionCredentialCommand("runway", "gen4_turbo", "p31-own-secret"));
        Assert.Equal(famixa.CredentialId, rotated.CredentialId);
        Assert.Equal(("runway", "gen4_turbo"), (rotated.ProviderId, rotated.ModelId));

        caller.Id = KitMkt;
        var kitRows = await harness.Registration.ListAsync();
        Assert.Single(kitRows);
        Assert.Equal(kitMkt.CredentialVersionId, kitRows[0].CredentialVersionId);
        Assert.Equal(ProductionCredentialStatuses.Active, kitRows[0].Status);
    }

    [Fact]
    public void Rotate_route_takes_no_identifiers_and_stays_off_providers_and_artifacts()
    {
        var controller = File.ReadAllText(Path.Combine(RepoRoot(), "src", "KitPlatform.Api", "Controllers", "Content", "ContentProductionController.cs"));
        Assert.Contains("[HttpPost(\"credentials/rotate\")]", controller, StringComparison.Ordinal);
        Assert.Contains("RotateActiveAsync(new RotateProductionCredentialCommand(", controller, StringComparison.Ordinal);

        string[] forbidden = ["TenantId", "CredentialId", "CredentialVersionId", "Secret", "Ciphertext", "Nonce", "Tag"];
        Assert.Equal(["ProviderId", "ModelId", "Secret"],
            typeof(RotateProductionCredentialRequestDto).GetProperties().Select(p => p.Name).ToArray());
        Assert.Equal(["ProviderId", "ModelId", "Secret"],
            typeof(RotateProductionCredentialCommand).GetProperties().Select(p => p.Name).ToArray());
        Assert.DoesNotContain(typeof(ProductionCredentialRotationDto).GetProperties(), p => forbidden.Contains(p.Name));

        var dependencies = typeof(ProductionCredentialRegistrationService).GetConstructors().Single()
            .GetParameters().Select(p => p.ParameterType).ToArray();
        Assert.Equal(
            [typeof(IProductionCallerTenant), typeof(IProductionCredentialStore), typeof(IProductionCredentialRotation)],
            dependencies);
    }

    static async Task AssertRejected(Harness harness, string provider, string model, string code)
    {
        var rejected = false;
        try
        {
            await harness.Registration.RegisterAsync(new RegisterProductionCredentialCommand(provider, model, Secret, null));
        }
        catch (InvalidOperationException ex) when (ex.Message == code && !ex.ToString().Contains(Secret, StringComparison.Ordinal))
        {
            rejected = true;
        }
        Assert.True(rejected);
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
        IProductionCredentialStore Store,
        InMemoryProductionCredentialStore Versions,
        IProductionCredentialResolver Resolver,
        IProductionCredentialRegistration Registration);
}
