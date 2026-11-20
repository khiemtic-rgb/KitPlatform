using System.Security.Cryptography;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// In-memory tenant-owned credential pair for official harnesses.
/// The secret stays in the secret store. Snapshots receive only the ids.
/// </summary>
public sealed record ProductionOfficialHarnessCredentialPin(
    Guid TenantId,
    ProductionCredential Credential,
    ProductionCredentialVersion Version,
    ProductionCredentialReference Reference,
    ProductionCredentialResolver Resolver);

public static class ProductionOfficialHarnessCredentialFixture
{
    public const string Secret = "ck-g3-in-memory-fixture-secret";

    public static ProductionOfficialHarnessCredentialPin Pin(
        Guid tenantId,
        Guid credentialId,
        Guid credentialVersionId,
        string providerId,
        string modelId,
        string displayName)
    {
        var caller = new FixtureCaller(tenantId);
        var key = new byte[ProductionCredentialSecretEnvelope.KeySize];
        RandomNumberGenerator.Fill(key);
        var ring = new MapProductionCredentialKeyRing("g3", new Dictionary<string, byte[]> { ["g3"] = key });
        var identities = new InMemoryProductionCredentialStore(caller);
        var secrets = new InMemoryProductionCredentialSecretStore(caller, identities, ring);
        var credential = ProductionCredentialOwnership.Create(credentialId, tenantId, displayName);
        identities.CreateAsync(credential).GetAwaiter().GetResult();
        var version = ProductionCredentialOwnership.CreateVersion(
            credentialVersionId, credentialId, tenantId, 1, providerId, modelId);
        identities.AddVersionAsync(version).GetAwaiter().GetResult();
        secrets.PutAsync(credentialVersionId, Secret).GetAwaiter().GetResult();
        return new ProductionOfficialHarnessCredentialPin(
            tenantId,
            credential,
            version,
            new ProductionCredentialReference(credentialId, credentialVersionId),
            new ProductionCredentialResolver(identities, secrets, caller));
    }

    public static ProductionOfficialHarnessCredentialPin ElevenLabs() =>
        Pin(
            Guid.Parse("a11aa11a-a11a-4a11-8a11-a11aa11aa11a"),
            Guid.Parse("a11b0002-a11b-4a11-8a11-a11b00000002"),
            Guid.Parse("a11c0003-a11c-4a11-8a11-a11c00000003"),
            ProductionVoiceCatalog.ElevenLabsProviderId,
            ProductionVoiceCatalog.ElevenV3,
            "g3-elevenlabs-fixture");

    public static ProductionOfficialHarnessCredentialPin Runway() =>
        Pin(
            Guid.Parse("b22ab22a-b22a-4b22-8b22-b22ab22ab22a"),
            Guid.Parse("b22b0002-b22b-4b22-8b22-b22b00000002"),
            Guid.Parse("b22c0003-b22c-4b22-8b22-b22c00000003"),
            ProductionMotionCatalog.RunwayProviderId,
            ProductionMotionCatalog.RunwayGen4Turbo,
            "g3-runway-fixture");

    sealed class FixtureCaller(Guid id) : IProductionCallerTenant
    {
        public Guid RequireTenant() => ProductionTenantBoundary.Require(id);
    }
}
