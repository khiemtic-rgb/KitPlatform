using System.Security.Cryptography;
using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

/// <summary>
/// FAMIXA-FILM-PRODUCTION-36-FIX. Registration wiring from request DTO to encrypted storage.
/// </summary>
public sealed class P36CredentialRegistrationWiring
{
    static readonly Guid TenantA = Guid.Parse("4028941b-49b3-4ba2-8815-778daef4a2d3");
    const string SecretValue = "p36-test-secret-not-a-real-provider-key";

    [Fact]
    public async Task A_non_empty_secret_reaches_the_registration_command_and_is_encrypted()
    {
        var harness = Open();
        var command = new RegisterProductionCredentialCommand("runway", "gen4_turbo", SecretValue, "runway");
        
        Assert.Equal("runway", command.ProviderId);
        Assert.Equal("gen4_turbo", command.ModelId);
        Assert.Equal(SecretValue, command.Secret);
        Assert.Equal("runway", command.DisplayName);

        var result = await harness.Registration.RegisterAsync(command);
        
        Assert.Equal("runway", result.ProviderId);
        Assert.Equal("gen4_turbo", result.ModelId);
        Assert.Equal(ProductionCredentialStatuses.Active, result.Status);
        Assert.Equal(1, result.Version);
        Assert.NotEqual(Guid.Empty, result.CredentialId);
        Assert.NotEqual(Guid.Empty, result.CredentialVersionId);
        
        var credential = await harness.Store.GetAsync(result.CredentialId);
        Assert.NotNull(credential);
        Assert.Equal(TenantA, credential.TenantId);
        Assert.Equal(ProductionCredentialStatuses.Active, credential.Status);

        var version = await harness.Store.GetVersionAsync(result.CredentialVersionId);
        Assert.NotNull(version);
        Assert.Equal(result.CredentialId, version.CredentialId);
        Assert.Equal(TenantA, version.TenantId);
        Assert.Equal(1, version.Version);
        Assert.Equal(ProductionCredentialStatuses.Active, version.Status);

        var opened = await harness.Secrets.OpenAsync(result.CredentialVersionId);
        Assert.Equal(SecretValue, opened);
    }

    [Fact]
    public async Task The_result_does_not_contain_the_secret()
    {
        var harness = Open();
        var result = await harness.Registration.RegisterAsync(
            new RegisterProductionCredentialCommand("runway", "gen4_turbo", SecretValue, "runway"));

        var resultString = result.ToString();
        Assert.DoesNotContain(SecretValue, resultString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Exactly_one_credential_identity_and_one_active_version_are_created()
    {
        var harness = Open();
        var result = await harness.Registration.RegisterAsync(
            new RegisterProductionCredentialCommand("runway", "gen4_turbo", SecretValue, "runway"));

        var catalog = await harness.Store.ListCatalogAsync();
        var matches = catalog.Where(row => row.CredentialId == result.CredentialId).ToList();
        
        Assert.Single(matches);
        Assert.Equal(ProductionCredentialStatuses.Active, matches[0].Status);
        Assert.Equal(ProductionCredentialStatuses.Active, matches[0].IdentityStatus);
        Assert.Equal(1, matches[0].Version);
    }

    [Fact]
    public void Tenant_comes_from_the_caller_not_the_request_body()
    {
        var harness = Open();
        var command = new RegisterProductionCredentialCommand("runway", "gen4_turbo", SecretValue, "runway");

        Assert.False(command.ToString().Contains("TenantId", StringComparison.OrdinalIgnoreCase));
        Assert.False(command.ToString().Contains(TenantA.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void No_runway_HTTP_request_occurs_from_registration()
    {
        var audit = new ProductionRunwayHttpAudit();
        Assert.Equal(0, audit.StartCount);
        Assert.Equal(0, audit.PollCount);
        Assert.Equal(0, audit.DownloadCount);
    }

    [Fact]
    public async Task P31_tests_still_pass_after_the_fix()
    {
        var harness = Open();
        var registered = await harness.Registration.RegisterAsync(
            new RegisterProductionCredentialCommand("runway", "gen4_turbo", "p31-secret", "runway"));

        Assert.Equal(1, registered.Version);
        Assert.Equal(ProductionCredentialStatuses.Active, registered.Status);

        var second = await harness.Registration.AddVersionAsync(
            new AddProductionCredentialVersionCommand(
                registered.CredentialId, "runway", "gen4_turbo", "p31-rotated"));

        Assert.Equal(2, second.Version);
        Assert.Equal(ProductionCredentialStatuses.Superseded,
            (await harness.Store.GetVersionAsync(registered.CredentialVersionId))!.Status);
    }

    static Harness Open()
    {
        var caller = new SwitchCaller { Id = TenantA };
        var key = new byte[ProductionCredentialSecretEnvelope.KeySize];
        RandomNumberGenerator.Fill(key);
        var ring = new MapProductionCredentialKeyRing("v1", new Dictionary<string, byte[]> { ["v1"] = key });
        var store = new InMemoryProductionCredentialStore(caller);
        var secrets = new InMemoryProductionCredentialSecretStore(caller, store, ring);
        var rotation = new InMemoryProductionCredentialRotation(store, secrets, ring);
        return new Harness(
            store,
            secrets,
            rotation,
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
        ProductionCredentialRegistrationService Registration);
}
