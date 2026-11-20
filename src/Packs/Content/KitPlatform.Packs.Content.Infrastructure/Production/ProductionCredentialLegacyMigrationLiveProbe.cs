using System.Security.Cryptography;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content.Production;
using Npgsql;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Rolled-back PostgreSQL check. Does not copy org_settings secrets and does not leave a row.
/// </summary>
public static class ProductionCredentialLegacyMigrationLiveProbe
{
    public static async Task<string> RunAsync(string connectionString, CancellationToken ct = default)
    {
        if (ProductionCredentialLegacyCatalog.EligibleCount() != 0)
            throw new InvalidOperationException("LEGACY_ELIGIBLE_UNEXPECTED");

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        var beforeCredentials = await Scalar(conn, null, "SELECT COUNT(*) FROM pack_content.production_credentials", ct);
        var beforeVersions = await Scalar(conn, null, "SELECT COUNT(*) FROM pack_content.production_credential_versions", ct);
        var beforeSnapshots = await Scalar(conn, null, "SELECT COUNT(*) FROM pack_content.production_task_input_snapshots", ct);
        var beforeNull = await Scalar(conn, null, """
            SELECT COUNT(*) FROM pack_content.production_task_input_snapshots WHERE credential_id IS NULL
            """, ct);
        var orgRows = await Scalar(conn, null, "SELECT COUNT(*) FROM pack_content.org_settings", ct);
        var tenantColumn = await Scalar(conn, null, """
            SELECT COUNT(*) FROM information_schema.columns
            WHERE table_schema = 'pack_content' AND table_name = 'org_settings' AND column_name = 'tenant_id'
            """, ct);
        if (orgRows != 1 || tenantColumn != 0)
            throw new InvalidOperationException("LEGACY_ORG_SCOPE_CHANGED");

        var tenant = await Dapper.SqlMapper.ExecuteScalarAsync<Guid?>(conn,
            "SELECT id FROM public.tenants WHERE tenant_code = 'KIT_LOCAL'");
        if (tenant is null || tenant == Guid.Empty)
            throw new InvalidOperationException(ProductionTenantBoundary.Required);

        var key = new byte[ProductionCredentialSecretEnvelope.KeySize];
        RandomNumberGenerator.Fill(key);
        var ring = new MapProductionCredentialKeyRing("probe", new Dictionary<string, byte[]> { ["probe"] = key });
        await using var tx = await conn.BeginTransactionAsync(ct);
        var caller = new ProbeCaller(tenant.Value);
        var migration = new ProductionCredentialLegacyMigrationRepository(conn, tx, ring, caller);
        var store = new ProductionCredentialRepository(conn, tx, caller);
        var secrets = new ProductionCredentialSecretRepository(conn, tx, ring, caller);
        var resolver = new ProductionCredentialResolver(store, secrets, caller);

        var unscoped = new ProductionCredentialLegacyEvidence(
            ProductionCredentialLegacyClass.UnscopedUnknown,
            tenant.Value,
            Guid.Parse("380f0000-0000-7000-8000-0000000000f8"),
            Guid.Parse("380f0000-0000-7000-8000-0000000000f9"),
            "runway",
            "gen4_turbo",
            "org-settings-not-proven");
        var refused = false;
        try
        {
            await migration.MigrateAsync(unscoped, "gate-f-unscoped", ct);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialLegacyCatalog.NotEligible)
        {
            refused = true;
        }
        if (!refused)
            throw new InvalidOperationException("LEGACY_UNSCOPED_MIGRATED");

        const string secret = "gate-f-probe-owned";
        var credentialId = Guid.Parse("380f0000-0000-7000-8000-0000000000f1");
        var versionId = Guid.Parse("380f0000-0000-7000-8000-0000000000f2");
        var owned = new ProductionCredentialLegacyEvidence(
            ProductionCredentialLegacyClass.ProvablyTenantOwned,
            tenant.Value, credentialId, versionId, "runway", "gen4_turbo", "synthetic-proven");
        var created = await migration.MigrateAsync(owned, secret, ct);
        var again = await migration.MigrateAsync(owned, secret, ct);
        if (created.TenantId != tenant.Value || created.CredentialId != credentialId || again.CredentialVersionId != versionId)
            throw new InvalidOperationException("LEGACY_IDENTITY");
        var versionCount = await Scalar(conn, tx, """
            SELECT COUNT(*) FROM pack_content.production_credential_versions
            WHERE credential_id = '380f0000-0000-7000-8000-0000000000f1'
            """, ct);
        if (versionCount != 1)
            throw new InvalidOperationException("LEGACY_DUPLICATE");

        var material = await resolver.ResolveAsync(credentialId, versionId, "runway", "gen4_turbo", ct);
        if (material.Secret != secret || material.CredentialVersionId != versionId || material.ToString().Contains(secret, StringComparison.Ordinal))
            throw new InvalidOperationException("LEGACY_DECRYPT");

        var outsider = new ProductionCredentialLegacyMigrationRepository(
            conn, tx, ring, new ProbeCaller(Guid.Parse("11111111-1111-1111-1111-111111111111")));
        var cross = false;
        try
        {
            await outsider.MigrateAsync(owned, secret, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionTenantBoundary.Forbidden
            && !ex.ToString().Contains(secret, StringComparison.Ordinal))
        {
            cross = true;
        }
        if (!cross)
            throw new InvalidOperationException("LEGACY_CROSS_TENANT");
        var afterCross = await Scalar(conn, tx, """
            SELECT COUNT(*) FROM pack_content.production_credential_versions
            WHERE credential_id = '380f0000-0000-7000-8000-0000000000f1'
            """, ct);
        if (afterCross != 1)
            throw new InvalidOperationException("LEGACY_CROSS_TENANT");

        await tx.RollbackAsync(ct);
        var afterCredentials = await Scalar(conn, null, "SELECT COUNT(*) FROM pack_content.production_credentials", ct);
        var afterVersions = await Scalar(conn, null, "SELECT COUNT(*) FROM pack_content.production_credential_versions", ct);
        var afterSnapshots = await Scalar(conn, null, "SELECT COUNT(*) FROM pack_content.production_task_input_snapshots", ct);
        var afterNull = await Scalar(conn, null, """
            SELECT COUNT(*) FROM pack_content.production_task_input_snapshots WHERE credential_id IS NULL
            """, ct);
        var orgAfter = await Scalar(conn, null, "SELECT COUNT(*) FROM pack_content.org_settings", ct);
        if (afterCredentials != beforeCredentials || afterVersions != beforeVersions
            || afterSnapshots != beforeSnapshots || afterNull != beforeNull || orgAfter != orgRows)
            throw new InvalidOperationException("LEGACY_PROBE_PERSISTED");
        return "PROBE_OK eligible=0 migrated_live=0 refused_unscoped=1 synthetic_rolled_back=1 persisted=0 snapshots=" + afterSnapshots;
    }

    static async Task<int> Scalar(NpgsqlConnection conn, NpgsqlTransaction? tx, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        var value = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(value);
    }

    sealed class ProbeCaller(Guid id) : IProductionCallerTenant
    {
        public Guid RequireTenant() => ProductionTenantBoundary.Require(id);
    }
}
