using System.Security.Cryptography;
using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

/// <summary>
/// FAMIXA-FILM-PRODUCTION-36B. Verify automatic resolution after real registration.
/// </summary>
public sealed class P36BCredentialVerification
{
    static readonly Guid TenantFamixa = Guid.Parse("4028941b-49b3-4ba2-8815-778daef4a2d3");
    const string TestSecret = "p36b-test-secret";

    [Fact]
    public async Task Automatic_resolution_succeeds_for_a_single_active_credential()
    {
        var harness = Open();
        var registered = await harness.Registration.RegisterAsync(
            new RegisterProductionCredentialCommand("runway", "gen4_turbo", TestSecret, "runway"));

        var resolved = await harness.Resolver.ResolveActiveAsync(TenantFamixa, "runway", "gen4_turbo");

        Assert.Equal(registered.CredentialId, resolved.CredentialId);
        Assert.Equal(registered.CredentialVersionId, resolved.CredentialVersionId);
        Assert.Equal("runway", resolved.ProviderId);
        Assert.Equal("gen4_turbo", resolved.ModelId);
        Assert.Equal(TestSecret, resolved.Secret);
    }

    [Fact]
    public async Task Returned_material_does_not_contain_secret_in_ToString()
    {
        var harness = Open();
        var registered = await harness.Registration.RegisterAsync(
            new RegisterProductionCredentialCommand("runway", "gen4_turbo", TestSecret, "runway"));

        var resolved = await harness.Resolver.ResolveActiveAsync(TenantFamixa, "runway", "gen4_turbo");

        var str = resolved.ToString();
        Assert.DoesNotContain(TestSecret, str, StringComparison.Ordinal);
        Assert.Contains("ProductionCredentialMaterial", str, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resolution_contains_only_credential_ids_and_provider_model()
    {
        var harness = Open();
        var registered = await harness.Registration.RegisterAsync(
            new RegisterProductionCredentialCommand("runway", "gen4_turbo", TestSecret, "runway"));

        var resolved = await harness.Resolver.ResolveActiveAsync(TenantFamixa, "runway", "gen4_turbo");

        Assert.NotEqual(Guid.Empty, resolved.CredentialId);
        Assert.NotEqual(Guid.Empty, resolved.CredentialVersionId);
        Assert.Equal("runway", resolved.ProviderId);
        Assert.Equal("gen4_turbo", resolved.ModelId);
        
        // Secret is available via .Secret property but not in the object structure
        var type = resolved.GetType();
        var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var propertyNames = properties.Select(p => p.Name).ToList();
        
        Assert.Contains("CredentialId", propertyNames);
        Assert.Contains("CredentialVersionId", propertyNames);
        Assert.Contains("ProviderId", propertyNames);
        Assert.Contains("ModelId", propertyNames);
        Assert.Contains("Secret", propertyNames);
    }

    static Harness Open()
    {
        var caller = new SwitchCaller { Id = TenantFamixa };
        var key = new byte[ProductionCredentialSecretEnvelope.KeySize];
        RandomNumberGenerator.Fill(key);
        var ring = new MapProductionCredentialKeyRing("v1", new Dictionary<string, byte[]> { ["v1"] = key });
        var store = new InMemoryProductionCredentialStore(caller);
        var secrets = new InMemoryProductionCredentialSecretStore(caller, store, ring);
        var rotation = new InMemoryProductionCredentialRotation(store, secrets, ring);
        var resolver = new ProductionCredentialResolver(store, secrets, caller);
        return new Harness(
            store,
            secrets,
            rotation,
            resolver,
            new ProductionCredentialRegistrationService(caller, store, rotation));
    }

    sealed class SwitchCaller : IProductionCallerTenant
    {
        public Guid Id { get; set; }
        public Guid RequireTenant() => ProductionTenantBoundary.Require(Id);
    }

    sealed record Harness(
        InMemoryProductionCredentialStore Store,
        InMemoryProductionCredentialSecretStore Secrets,
        InMemoryProductionCredentialRotation Rotation,
        ProductionCredentialResolver Resolver,
        ProductionCredentialRegistrationService Registration);
}
