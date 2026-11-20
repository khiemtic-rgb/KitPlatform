using System.Security.Cryptography;
using KitPlatform.Packs.Content.Production;
using Npgsql;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// One rolled-back PostgreSQL pass through the Dapper credential repositories and the resolver.
/// Does not leave a row. Does not print the secret.
/// </summary>
public static class ProductionCredentialResolutionLiveProbe
{
    public static async Task<string> RunAsync(string connectionString, CancellationToken ct = default)
    {
        var key = new byte[ProductionCredentialSecretEnvelope.KeySize];
        RandomNumberGenerator.Fill(key);
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        var tenant = await new CommandDefinitionTenant().ReadKitLocalAsync(conn, ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var caller = new ProbeCaller(tenant);
        var identities = new ProductionCredentialRepository(conn, tx, caller);
        var ring = new MapProductionCredentialKeyRing("v1", new Dictionary<string, byte[]> { ["v1"] = key });
        var secrets = new ProductionCredentialSecretRepository(conn, tx, ring, caller);
        var resolver = new ProductionCredentialResolver(identities, secrets, caller);
        var credentialId = Guid.Parse("378c0000-0000-7000-8000-0000000000c1");
        var versionId = Guid.Parse("378c0000-0000-7000-8000-0000000000c2");
        const string plain = "gate-c-probe-secret";
        var credential = ProductionCredentialOwnership.Create(credentialId, tenant, "gate-c-probe");
        await identities.CreateAsync(credential, ct);
        await identities.AddVersionAsync(ProductionCredentialOwnership.CreateVersion(
            versionId, credentialId, tenant, 1, "runway", "gen4_turbo"), ct);
        await secrets.PutAsync(versionId, plain, ct);
        var material = await resolver.ResolveAsync(credentialId, versionId, "runway", "gen4_turbo", ct);
        if (material.Secret != plain || material.ToString().Contains(plain, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionCredentialSecretProtector.CiphertextInvalid);
        caller.Id = Guid.Parse("378c0000-0000-7000-8000-0000000000c9");
        try
        {
            await resolver.ResolveAsync(credentialId, versionId, "runway", "gen4_turbo", ct);
            throw new InvalidOperationException("CROSS_TENANT_WAS_ALLOWED");
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialSecretProtector.SecretUnavailable)
        {
        }

        await tx.RollbackAsync(ct);
        await using var check = new NpgsqlConnection(connectionString);
        await check.OpenAsync(ct);
        var left = await Dapper.SqlMapper.ExecuteScalarAsync<int>(check,
            "SELECT COUNT(*) FROM pack_content.production_credentials WHERE credential_id = @id",
            new { id = credentialId });
        if (left != 0)
            throw new InvalidOperationException("PROBE_ROW_REMAINED");
        return "PROBE_OK";
    }

    sealed class ProbeCaller : IProductionCallerTenant
    {
        public ProbeCaller(Guid id) => Id = id;
        public Guid Id { get; set; }
        public Guid RequireTenant() => ProductionTenantBoundary.Require(Id);
    }

    sealed class CommandDefinitionTenant
    {
        public async Task<Guid> ReadKitLocalAsync(NpgsqlConnection conn, CancellationToken ct)
        {
            var id = await Dapper.SqlMapper.ExecuteScalarAsync<Guid?>(conn,
                "SELECT id FROM public.tenants WHERE tenant_code = 'KIT_LOCAL'");
            if (id is null || id == Guid.Empty)
                throw new InvalidOperationException(ProductionTenantBoundary.Required);
            return id.Value;
        }
    }
}
