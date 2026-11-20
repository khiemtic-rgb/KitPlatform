using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ProductionTenantAiConfigRepository : IProductionTenantAiConfigStore
{
    const string Columns = """
        tenant_id AS TenantId, capability AS Capability, provider_id AS ProviderId, model_id AS ModelId,
        credential_id AS CredentialId, enabled AS Enabled, updated_by AS UpdatedBy, updated_at AS UpdatedAt
        """;

    readonly IDbConnectionFactory _db;
    readonly IProductionCallerTenant _caller;

    public ProductionTenantAiConfigRepository(IDbConnectionFactory db, IProductionCallerTenant? caller = null)
    {
        _db = db;
        _caller = caller ?? ProductionMissingCallerTenant.Instance;
    }

    public async Task<IReadOnlyList<ProductionTenantAiConfig>> ListAsync(CancellationToken ct = default)
    {
        var tenant = _caller.RequireTenant();
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<Row>(new CommandDefinition($"""
            SELECT {Columns}
            FROM pack_content.production_tenant_ai_config
            WHERE tenant_id = @Tenant
            ORDER BY capability
            """, new { Tenant = tenant }, cancellationToken: ct));
        return rows.Select(Map).ToList();
    }

    public async Task<ProductionTenantAiConfig?> GetAsync(string capability, CancellationToken ct = default)
    {
        var tenant = _caller.RequireTenant();
        var cap = ProductionTenantAiCapabilities.Require(capability);
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition($"""
            SELECT {Columns}
            FROM pack_content.production_tenant_ai_config
            WHERE tenant_id = @Tenant AND capability = @Capability
            """, new { Tenant = tenant, Capability = cap }, cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    public async Task<ProductionTenantAiConfig> UpsertAsync(ProductionTenantAiConfig row, CancellationToken ct = default)
    {
        var tenant = _caller.RequireTenant();
        ProductionCredentialOwnership.RequireOwned(tenant, row.TenantId);
        var cap = ProductionTenantAiCapabilities.Require(row.Capability);
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var saved = await conn.QuerySingleAsync<Row>(new CommandDefinition($"""
            INSERT INTO pack_content.production_tenant_ai_config (
                tenant_id, capability, provider_id, model_id, credential_id, enabled, updated_by, updated_at)
            VALUES (@Tenant, @Capability, @ProviderId, @ModelId, @CredentialId, @Enabled, @UpdatedBy, @UpdatedAt)
            ON CONFLICT (tenant_id, capability) DO UPDATE SET
                provider_id = EXCLUDED.provider_id,
                model_id = EXCLUDED.model_id,
                credential_id = EXCLUDED.credential_id,
                enabled = EXCLUDED.enabled,
                updated_by = EXCLUDED.updated_by,
                updated_at = EXCLUDED.updated_at
            RETURNING {Columns}
            """, new
        {
            Tenant = tenant,
            Capability = cap,
            row.ProviderId,
            row.ModelId,
            row.CredentialId,
            row.Enabled,
            row.UpdatedBy,
            row.UpdatedAt,
        }, cancellationToken: ct));
        return Map(saved);
    }

    public async Task<bool> DeleteAsync(string capability, CancellationToken ct = default)
    {
        var tenant = _caller.RequireTenant();
        var cap = ProductionTenantAiCapabilities.Require(capability);
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var n = await conn.ExecuteAsync(new CommandDefinition("""
            DELETE FROM pack_content.production_tenant_ai_config
            WHERE tenant_id = @Tenant AND capability = @Capability
            """, new { Tenant = tenant, Capability = cap }, cancellationToken: ct));
        return n > 0;
    }

    static ProductionTenantAiConfig Map(Row r) =>
        new(r.TenantId, r.Capability, r.ProviderId, r.ModelId, r.CredentialId, r.Enabled, r.UpdatedBy, r.UpdatedAt);

    sealed class Row
    {
        public Guid TenantId { get; set; }
        public string Capability { get; set; } = "";
        public string ProviderId { get; set; } = "";
        public string ModelId { get; set; } = "";
        public Guid? CredentialId { get; set; }
        public bool Enabled { get; set; }
        public string UpdatedBy { get; set; } = "";
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
