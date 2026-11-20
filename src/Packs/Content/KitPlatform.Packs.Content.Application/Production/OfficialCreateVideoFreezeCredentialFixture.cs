using System.Security.Cryptography;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Explicit credential pair passed into <see cref="OfficialCreateVideoFreezeV1"/>.
/// Production freeze does not invent this value.
/// </summary>
public sealed record OfficialCreateVideoFreezeCredentialPin(
    IProductionCredentialResolver Resolver,
    ProductionCredentialReference Reference);

/// <summary>
/// In-memory google/veo-3.1 pin for tests of the official freeze.
/// Not an official Production execution path and not an org_settings or environment secret.
/// </summary>
public static class OfficialCreateVideoFreezeCredentialFixture
{
    public const string Secret = "official-freeze-fixture-secret";
    public static readonly Guid TenantId = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc1");
    public static readonly Guid CredentialId = Guid.Parse("dddddddd-dddd-dddd-dddd-ddddddddddd1");
    public static readonly Guid CredentialVersionId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeee1");

    public static OfficialCreateVideoFreezeCredentialPin Pin()
    {
        var caller = new FixtureCaller(TenantId);
        var key = new byte[ProductionCredentialSecretEnvelope.KeySize];
        RandomNumberGenerator.Fill(key);
        var ring = new MapProductionCredentialKeyRing("fixture", new Dictionary<string, byte[]> { ["fixture"] = key });
        var identities = new InMemoryProductionCredentialStore(caller);
        var secrets = new InMemoryProductionCredentialSecretStore(caller, identities, ring);
        var credential = ProductionCredentialOwnership.Create(CredentialId, TenantId, "official-freeze-fixture");
        identities.CreateAsync(credential).GetAwaiter().GetResult();
        var version = ProductionCredentialOwnership.CreateVersion(
            CredentialVersionId,
            CredentialId,
            TenantId,
            1,
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId);
        identities.AddVersionAsync(version).GetAwaiter().GetResult();
        secrets.PutAsync(CredentialVersionId, Secret).GetAwaiter().GetResult();
        var resolver = new ProductionCredentialResolver(identities, secrets, caller);
        return new OfficialCreateVideoFreezeCredentialPin(
            resolver,
            new ProductionCredentialReference(CredentialId, CredentialVersionId));
    }

    sealed class FixtureCaller(Guid id) : IProductionCallerTenant
    {
        public Guid RequireTenant() => ProductionTenantBoundary.Require(id);
    }
}
