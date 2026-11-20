using System.Linq;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content.Production;
using Npgsql;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// ADR-04 Gate A persistence. Inserts identity rows only.
/// Does not read org_settings, does not store a secret, and does not resolve a provider.
/// </summary>
internal sealed class ProductionCredentialRepository : IProductionCredentialStore
{
    readonly IDbConnectionFactory? _db;
    readonly IProductionCallerTenant _caller;
    readonly NpgsqlConnection? _bound;
    readonly NpgsqlTransaction? _boundTx;

    public ProductionCredentialRepository(IDbConnectionFactory db, IProductionCallerTenant? caller = null)
    {
        _db = db;
        _caller = caller ?? ProductionMissingCallerTenant.Instance;
    }

    internal ProductionCredentialRepository(NpgsqlConnection bound, NpgsqlTransaction tx, IProductionCallerTenant caller)
    {
        _bound = bound;
        _boundTx = tx;
        _caller = caller;
    }

    public async Task<ProductionCredential> CreateAsync(ProductionCredential credential, CancellationToken ct = default)
    {
        var caller = _caller.RequireTenant();
        ProductionCredentialOwnership.RequireOwned(caller, credential.TenantId);
        var existing = await InDb((conn, tx, token) => conn.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("""
            SELECT tenant_id
            FROM pack_content.production_credentials
            WHERE credential_id = @CredentialId
            """, new { credential.CredentialId }, tx, cancellationToken: token)), ct);
        if (existing is not null)
        {
            ProductionCredentialOwnership.RejectOwnerChange(existing.Value, credential.TenantId);
            throw new InvalidOperationException(ProductionCredentialOwnership.IdentityExists);
        }

        await InDb((conn, tx, token) => conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO pack_content.production_credentials (
                credential_id, tenant_id, display_name, status)
            VALUES (
                @CredentialId, @TenantId, @DisplayName, @Status)
            """, credential, tx, cancellationToken: token)), ct);
        return credential;
    }

    public async Task<ProductionCredential?> GetAsync(Guid credentialId, CancellationToken ct = default)
    {
        var row = await InDb((conn, tx, token) => conn.QuerySingleOrDefaultAsync<CredentialRow>(new CommandDefinition("""
            SELECT credential_id AS CredentialId, tenant_id AS TenantId,
                   display_name AS DisplayName, status AS Status
            FROM pack_content.production_credentials
            WHERE credential_id = @credentialId AND tenant_id = @TenantId
            """, new { credentialId, TenantId = _caller.RequireTenant() }, tx, cancellationToken: token)), ct);
        return row is null ? null : new ProductionCredential(row.CredentialId, row.TenantId, row.DisplayName, row.Status);
    }

    public async Task<ProductionCredentialVersion> AddVersionAsync(
        ProductionCredentialVersion version, CancellationToken ct = default)
    {
        var caller = _caller.RequireTenant();
        ProductionCredentialOwnership.RequireOwned(caller, version.TenantId);
        var owner = await InDb((conn, tx, token) => conn.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("""
            SELECT tenant_id
            FROM pack_content.production_credentials
            WHERE credential_id = @CredentialId AND tenant_id = @TenantId
            """, new { version.CredentialId, TenantId = caller }, tx, cancellationToken: token)), ct);
        if (owner is null)
            throw new InvalidOperationException(ProductionCredentialOwnership.VersionCredentialRequired);

        var existing = await InDb((conn, tx, token) => conn.QuerySingleOrDefaultAsync<VersionOwnerRow>(new CommandDefinition("""
            SELECT credential_id AS CredentialId, version_number AS Version
            FROM pack_content.production_credential_versions
            WHERE credential_version_id = @CredentialVersionId
            """, new { version.CredentialVersionId }, tx, cancellationToken: token)), ct);
        if (existing is not null)
        {
            ProductionCredentialOwnership.RejectVersionReassignment(existing.CredentialId, version.CredentialId);
            throw new InvalidOperationException(ProductionCredentialOwnership.VersionExists);
        }

        await InDb((conn, tx, token) => conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO pack_content.production_credential_versions (
                credential_version_id, credential_id, tenant_id, version_number,
                provider_id, model_id, status)
            VALUES (
                @CredentialVersionId, @CredentialId, @TenantId, @Version,
                @ProviderId, @ModelId, @Status)
            """, version, tx, cancellationToken: token)), ct);
        return version;
    }

    public async Task<ProductionCredentialVersion?> GetVersionAsync(
        Guid credentialVersionId, CancellationToken ct = default)
    {
        var row = await InDb((conn, tx, token) => conn.QuerySingleOrDefaultAsync<VersionRow>(new CommandDefinition("""
            SELECT credential_version_id AS CredentialVersionId, credential_id AS CredentialId,
                   tenant_id AS TenantId, version_number AS Version,
                   provider_id AS ProviderId, model_id AS ModelId, status AS Status
            FROM pack_content.production_credential_versions
            WHERE credential_version_id = @credentialVersionId AND tenant_id = @TenantId
            """, new { credentialVersionId, TenantId = _caller.RequireTenant() }, tx, cancellationToken: token)), ct);
        return row is null
            ? null
            : new ProductionCredentialVersion(
                row.CredentialVersionId, row.CredentialId, row.TenantId, row.Version,
                row.ProviderId, row.ModelId, row.Status);
    }

    public async Task<IReadOnlyList<ProductionCredentialActiveMatch>> ListEligibleAsync(
        string providerId, string modelId, CancellationToken ct = default)
    {
        var tenant = _caller.RequireTenant();
        var rows = await InDb((conn, tx, token) => conn.QueryAsync<ActiveMatchRow>(new CommandDefinition("""
            SELECT v.credential_id AS CredentialId, v.credential_version_id AS CredentialVersionId
            FROM pack_content.production_credential_versions v
            JOIN pack_content.production_credentials c
              ON c.credential_id = v.credential_id AND c.tenant_id = v.tenant_id
            WHERE v.tenant_id = @tenant
              AND c.status = 'ACTIVE'
              AND v.status = 'ACTIVE'
              AND lower(v.provider_id) = lower(@providerId)
              AND lower(v.model_id) = lower(@modelId)
            ORDER BY v.created_at, v.credential_version_id
            """, new { tenant, providerId, modelId }, tx, cancellationToken: token)), ct);
        return rows.Select(row => new ProductionCredentialActiveMatch(row.CredentialId, row.CredentialVersionId)).ToList();
    }

    public async Task<IReadOnlyList<ProductionCredentialCatalogEntry>> ListCatalogAsync(CancellationToken ct = default)
    {
        var tenant = _caller.RequireTenant();
        var rows = await InDb((conn, tx, token) => conn.QueryAsync<CatalogRow>(new CommandDefinition("""
            SELECT c.credential_id AS CredentialId,
                   v.credential_version_id AS CredentialVersionId,
                   c.display_name AS DisplayName,
                   c.status AS IdentityStatus,
                   v.version_number AS Version,
                   v.provider_id AS ProviderId,
                   v.model_id AS ModelId,
                   v.status AS Status,
                   v.created_at AS CreatedAt
            FROM pack_content.production_credential_versions v
            JOIN pack_content.production_credentials c
              ON c.credential_id = v.credential_id AND c.tenant_id = v.tenant_id
            WHERE v.tenant_id = @tenant
            ORDER BY v.created_at, v.credential_version_id
            """, new { tenant }, tx, cancellationToken: token)), ct);
        return rows.Select(row => new ProductionCredentialCatalogEntry(
            row.CredentialId, row.CredentialVersionId, row.DisplayName, row.IdentityStatus,
            row.Version, row.ProviderId ?? "", row.ModelId ?? "", row.Status, row.CreatedAt)).ToList();
    }

    async Task<T> InDb<T>(Func<NpgsqlConnection, NpgsqlTransaction?, CancellationToken, Task<T>> work, CancellationToken ct)
    {
        if (_bound is not null)
            return await work(_bound, _boundTx, ct);
        await using var conn = await _db!.CreateOpenConnectionAsync(ct);
        return await work(conn, null, ct);
    }

    sealed class VersionOwnerRow
    {
        public Guid CredentialId { get; set; }
        public int Version { get; set; }
    }

    sealed class CredentialRow
    {
        public Guid CredentialId { get; set; }
        public Guid TenantId { get; set; }
        public string DisplayName { get; set; } = "";
        public string Status { get; set; } = "";
    }

    sealed class VersionRow
    {
        public Guid CredentialVersionId { get; set; }
        public Guid CredentialId { get; set; }
        public Guid TenantId { get; set; }
        public int Version { get; set; }
        public string? ProviderId { get; set; }
        public string? ModelId { get; set; }
        public string Status { get; set; } = "";
    }

    sealed class ActiveMatchRow
    {
        public Guid CredentialId { get; set; }
        public Guid CredentialVersionId { get; set; }
    }

    sealed class CatalogRow
    {
        public Guid CredentialId { get; set; }
        public Guid CredentialVersionId { get; set; }
        public string DisplayName { get; set; } = "";
        public string IdentityStatus { get; set; } = "";
        public int Version { get; set; }
        public string? ProviderId { get; set; }
        public string? ModelId { get; set; }
        public string Status { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
    }
}
