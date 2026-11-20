using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content.Production;
using Npgsql;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Persists a new credential version and its ciphertext in one transaction.
/// Version number allocation is insert identity. It does not select an execution credential.
/// </summary>
internal sealed class ProductionCredentialRotationRepository : IProductionCredentialRotation
{
    readonly IDbConnectionFactory? _db;
    readonly IProductionCallerTenant _caller;
    readonly IProductionCredentialKeyRing _keys;
    readonly NpgsqlConnection? _bound;
    readonly NpgsqlTransaction? _boundTx;

    public ProductionCredentialRotationRepository(
        IDbConnectionFactory db,
        IProductionCredentialKeyRing keys,
        IProductionCallerTenant? caller = null)
    {
        _db = db;
        _keys = keys;
        _caller = caller ?? ProductionMissingCallerTenant.Instance;
    }

    internal ProductionCredentialRotationRepository(
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

    public async Task<ProductionCredentialVersion> RotateAsync(
        Guid credentialId,
        Guid credentialVersionId,
        string providerId,
        string modelId,
        string secret,
        CancellationToken ct = default)
    {
        var tenant = _caller.RequireTenant();
        if (credentialVersionId == Guid.Empty)
            throw new InvalidOperationException(ProductionCredentialOwnership.VersionIdRequired);
        var provider = (providerId ?? "").Trim();
        var model = (modelId ?? "").Trim();
        if (provider.Length == 0 || model.Length == 0)
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
        RequireBackupBeforeCiphertext();
        var envelope = ProductionCredentialSecretProtector.Encrypt(secret, _keys.RequireCurrent());

        return await InTx(async (conn, tx, token) =>
        {
            var owner = await conn.QuerySingleOrDefaultAsync<CredentialState>(new CommandDefinition("""
                SELECT tenant_id AS TenantId, status AS Status
                FROM pack_content.production_credentials
                WHERE credential_id = @credentialId AND tenant_id = @tenant
                """, new { credentialId, tenant }, tx, cancellationToken: token));
            if (owner is null)
                throw new InvalidOperationException(ProductionCredentialOwnership.VersionCredentialRequired);
            ProductionCredentialOwnership.RequireOwned(tenant, owner.TenantId);
            if (!string.Equals(owner.Status, ProductionCredentialStatuses.Active, StringComparison.Ordinal))
                throw new InvalidOperationException(ProductionCredentialResolver.Revoked);

            await conn.ExecuteAsync(new CommandDefinition("""
                UPDATE pack_content.production_credential_versions
                SET status = @Superseded
                WHERE credential_id = @credentialId
                  AND tenant_id = @tenant
                  AND status = @Active
                """, new
            {
                Superseded = ProductionCredentialStatuses.Superseded,
                Active = ProductionCredentialStatuses.Active,
                credentialId,
                tenant,
            }, tx, cancellationToken: token));

            var next = await conn.ExecuteScalarAsync<int>(new CommandDefinition("""
                SELECT COALESCE(MAX(version_number), 0) + 1
                FROM pack_content.production_credential_versions
                WHERE credential_id = @credentialId AND tenant_id = @tenant
                """, new { credentialId, tenant }, tx, cancellationToken: token));
            var version = ProductionCredentialOwnership.CreateVersion(
                credentialVersionId, credentialId, tenant, next, provider, model);
            try
            {
                await conn.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO pack_content.production_credential_versions (
                        credential_version_id, credential_id, tenant_id, version_number,
                        provider_id, model_id, status,
                        secret_envelope_version, secret_key_id, secret_nonce, secret_ciphertext, secret_tag)
                    VALUES (
                        @CredentialVersionId, @CredentialId, @TenantId, @Version,
                        @ProviderId, @ModelId, @Status,
                        @EnvelopeVersion, @KeyId, @Nonce, @Ciphertext, @Tag)
                    """, new
                {
                    version.CredentialVersionId,
                    version.CredentialId,
                    version.TenantId,
                    version.Version,
                    version.ProviderId,
                    version.ModelId,
                    version.Status,
                    EnvelopeVersion = envelope.Version,
                    envelope.KeyId,
                    envelope.Nonce,
                    envelope.Ciphertext,
                    envelope.Tag,
                }, tx, cancellationToken: token));
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new InvalidOperationException(ProductionCredentialOwnership.VersionExists);
            }

            return version;
        }, ct);
    }

    public async Task<ProductionCredentialVersion> RevokeVersionAsync(
        Guid credentialVersionId, CancellationToken ct = default)
    {
        var tenant = _caller.RequireTenant();
        return await InTx(async (conn, tx, token) =>
        {
            var row = await conn.QuerySingleOrDefaultAsync<VersionState>(new CommandDefinition("""
                UPDATE pack_content.production_credential_versions
                SET status = @Revoked
                WHERE credential_version_id = @credentialVersionId
                  AND tenant_id = @tenant
                RETURNING credential_version_id AS CredentialVersionId, credential_id AS CredentialId,
                          tenant_id AS TenantId, version_number AS Version,
                          provider_id AS ProviderId, model_id AS ModelId, status AS Status
                """, new
            {
                Revoked = ProductionCredentialStatuses.Revoked,
                credentialVersionId,
                tenant,
            }, tx, cancellationToken: token));
            if (row is null)
                throw new InvalidOperationException(ProductionCredentialSecretProtector.SecretUnavailable);
            return new ProductionCredentialVersion(
                row.CredentialVersionId, row.CredentialId, row.TenantId, row.Version,
                row.ProviderId, row.ModelId, row.Status);
        }, ct);
    }

    void RequireBackupBeforeCiphertext()
    {
        if (_keys is ConfigurationProductionCredentialKeyRing configured)
            configured.RequireBackupSatisfied();
    }

    async Task<T> InTx<T>(Func<NpgsqlConnection, NpgsqlTransaction, CancellationToken, Task<T>> work, CancellationToken ct)
    {
        if (_bound is not null && _boundTx is not null)
        {
            await using var save = new NpgsqlCommand("SAVEPOINT cred_rotate", _bound, _boundTx);
            await save.ExecuteNonQueryAsync(ct);
            try
            {
                var result = await work(_bound, _boundTx, ct);
                await using var release = new NpgsqlCommand("RELEASE SAVEPOINT cred_rotate", _bound, _boundTx);
                await release.ExecuteNonQueryAsync(ct);
                return result;
            }
            catch
            {
                await using var undo = new NpgsqlCommand("ROLLBACK TO SAVEPOINT cred_rotate", _bound, _boundTx);
                await undo.ExecuteNonQueryAsync(ct);
                throw;
            }
        }

        await using var conn = await _db!.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            var result = await work(conn, tx, ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    sealed class CredentialState
    {
        public Guid TenantId { get; set; }
        public string Status { get; set; } = "";
    }

    sealed class VersionState
    {
        public Guid CredentialVersionId { get; set; }
        public Guid CredentialId { get; set; }
        public Guid TenantId { get; set; }
        public int Version { get; set; }
        public string? ProviderId { get; set; }
        public string? ModelId { get; set; }
        public string Status { get; set; } = "";
    }
}
