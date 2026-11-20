using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content.Production;
using Npgsql;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Persists AES-256-GCM envelopes on production_credential_versions.
/// Does not call a provider.
/// </summary>
internal sealed class ProductionCredentialSecretRepository : IProductionCredentialSecretStore
{
    readonly IDbConnectionFactory? _db;
    readonly IProductionCallerTenant _caller;
    readonly IProductionCredentialKeyRing _keys;
    readonly NpgsqlConnection? _bound;
    readonly NpgsqlTransaction? _boundTx;

    public ProductionCredentialSecretRepository(
        IDbConnectionFactory db,
        IProductionCredentialKeyRing keys,
        IProductionCallerTenant? caller = null)
    {
        _db = db;
        _keys = keys;
        _caller = caller ?? ProductionMissingCallerTenant.Instance;
    }

    internal ProductionCredentialSecretRepository(
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

    public async Task PutAsync(Guid credentialVersionId, string secret, CancellationToken ct = default)
    {
        var tenant = _caller.RequireTenant();
        if (_keys is ConfigurationProductionCredentialKeyRing configured)
            configured.RequireBackupSatisfied();
        var envelope = ProductionCredentialSecretProtector.Encrypt(secret, _keys.RequireCurrent());
        var updated = await InDb((conn, tx, token) => conn.ExecuteAsync(new CommandDefinition("""
            UPDATE pack_content.production_credential_versions
            SET secret_envelope_version = @Version,
                secret_key_id = @KeyId,
                secret_nonce = @Nonce,
                secret_ciphertext = @Ciphertext,
                secret_tag = @Tag
            WHERE credential_version_id = @CredentialVersionId
              AND tenant_id = @TenantId
              AND secret_ciphertext IS NULL
            """, new
        {
            envelope.Version,
            envelope.KeyId,
            envelope.Nonce,
            envelope.Ciphertext,
            envelope.Tag,
            CredentialVersionId = credentialVersionId,
            TenantId = tenant,
        }, tx, cancellationToken: token)), ct);
        if (updated == 1)
            return;
        var exists = await InDb((conn, tx, token) => conn.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*)
            FROM pack_content.production_credential_versions
            WHERE credential_version_id = @credentialVersionId
              AND tenant_id = @TenantId
            """, new { credentialVersionId, TenantId = tenant }, tx, cancellationToken: token)), ct);
        throw new InvalidOperationException(exists == 1
            ? ProductionCredentialSecretProtector.SecretImmutable
            : ProductionCredentialSecretProtector.SecretUnavailable);
    }

    public async Task<string> OpenAsync(Guid credentialVersionId, CancellationToken ct = default)
    {
        var tenant = _caller.RequireTenant();
        var row = await InDb((conn, tx, token) => conn.QuerySingleOrDefaultAsync<EnvelopeRow>(new CommandDefinition("""
            SELECT secret_envelope_version AS Version, secret_key_id AS KeyId,
                   secret_nonce AS Nonce, secret_ciphertext AS Ciphertext, secret_tag AS Tag
            FROM pack_content.production_credential_versions
            WHERE credential_version_id = @credentialVersionId
              AND tenant_id = @TenantId
            """, new { credentialVersionId, TenantId = tenant }, tx, cancellationToken: token)), ct);
        if (row?.Ciphertext is null || row.Nonce is null || row.Tag is null || string.IsNullOrWhiteSpace(row.KeyId))
            throw new InvalidOperationException(ProductionCredentialSecretProtector.SecretUnavailable);

        var envelope = new ProductionCredentialSecretEnvelope(row.Version, row.KeyId, row.Nonce, row.Ciphertext, row.Tag);
        return ProductionCredentialSecretProtector.Decrypt(envelope, _keys.Require(envelope.KeyId));
    }

    async Task<T> InDb<T>(Func<NpgsqlConnection, NpgsqlTransaction?, CancellationToken, Task<T>> work, CancellationToken ct)
    {
        if (_bound is not null)
            return await work(_bound, _boundTx, ct);
        await using var conn = await _db!.CreateOpenConnectionAsync(ct);
        return await work(conn, null, ct);
    }

    sealed class EnvelopeRow
    {
        public int Version { get; set; }
        public string? KeyId { get; set; }
        public byte[]? Nonce { get; set; }
        public byte[]? Ciphertext { get; set; }
        public byte[]? Tag { get; set; }
    }
}
