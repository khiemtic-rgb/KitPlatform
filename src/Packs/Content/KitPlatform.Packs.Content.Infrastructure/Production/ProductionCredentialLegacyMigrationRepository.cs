using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content.Production;
using Npgsql;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Creates a tenant-owned credential and its encrypted version in one transaction.
/// Refuses every legacy source that is not already proven to one Tenant.
/// Does not query legacy configuration and does not commit a bound transaction.
/// </summary>
internal sealed class ProductionCredentialLegacyMigrationRepository : IProductionCredentialLegacyMigration
{
    readonly IDbConnectionFactory? _db;
    readonly IProductionCredentialKeyRing _keys;
    readonly IProductionCallerTenant _caller;
    readonly NpgsqlConnection? _bound;
    readonly NpgsqlTransaction? _boundTx;

    public ProductionCredentialLegacyMigrationRepository(
        IDbConnectionFactory db,
        IProductionCredentialKeyRing keys,
        IProductionCallerTenant? caller = null)
    {
        _db = db;
        _keys = keys;
        _caller = caller ?? ProductionMissingCallerTenant.Instance;
    }

    internal ProductionCredentialLegacyMigrationRepository(
        NpgsqlConnection bound,
        NpgsqlTransaction tx,
        IProductionCredentialKeyRing keys,
        IProductionCallerTenant caller)
    {
        _bound = bound;
        _boundTx = tx;
        _keys = keys;
        _caller = caller;
    }

    public async Task<ProductionCredentialVersion> MigrateAsync(
        ProductionCredentialLegacyEvidence evidence,
        string secret,
        CancellationToken ct = default)
    {
        ProductionCredentialLegacyMigration.RequireEligible(evidence);
        if (_bound is not null && _boundTx is not null)
            return await InSavepoint(_bound, _boundTx, evidence, secret, ct);

        await using var conn = await _db!.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            var created = await Apply(conn, tx, evidence, secret, ct);
            await tx.CommitAsync(ct);
            return created;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    async Task<ProductionCredentialVersion> InSavepoint(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        ProductionCredentialLegacyEvidence evidence,
        string secret,
        CancellationToken ct)
    {
        await using (var mark = new NpgsqlCommand("SAVEPOINT cred_legacy", conn, tx))
            await mark.ExecuteNonQueryAsync(ct);
        try
        {
            var created = await Apply(conn, tx, evidence, secret, ct);
            await using var release = new NpgsqlCommand("RELEASE SAVEPOINT cred_legacy", conn, tx);
            await release.ExecuteNonQueryAsync(ct);
            return created;
        }
        catch
        {
            await using var undo = new NpgsqlCommand("ROLLBACK TO SAVEPOINT cred_legacy", conn, tx);
            await undo.ExecuteNonQueryAsync(ct);
            throw;
        }
    }

    Task<ProductionCredentialVersion> Apply(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        ProductionCredentialLegacyEvidence evidence,
        string secret,
        CancellationToken ct)
    {
        var store = new ProductionCredentialRepository(conn, tx, _caller);
        var rotation = new ProductionCredentialRotationRepository(conn, tx, _keys, _caller);
        return ProductionCredentialLegacyMigration.ApplyAsync(evidence, secret, store, rotation, _caller, ct);
    }
}
