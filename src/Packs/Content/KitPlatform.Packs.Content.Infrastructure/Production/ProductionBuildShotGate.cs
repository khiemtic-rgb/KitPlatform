using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// SELECT-only check that a tenant-owned series_build graph contains the shot code.
/// </summary>
internal sealed class ProductionBuildShotGate : IProductionTargetShotCheck
{
    readonly IDbConnectionFactory _db;
    readonly IProductionCallerTenant _caller;

    public ProductionBuildShotGate(IDbConnectionFactory db, IProductionCallerTenant caller)
    {
        _db = db;
        _caller = caller;
    }

    public async Task RequireAsync(Guid buildId, string shotCode, CancellationToken ct = default)
    {
        var tenant = _caller.RequireTenant();
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition("""
            SELECT tenant_id AS TenantId, graph_json::text AS GraphJson
            FROM pack_content.series_build
            WHERE id = @Id
            """, new { Id = buildId }, cancellationToken: ct));
        if (row is null)
            throw new InvalidOperationException(ProductionTenantBoundary.Forbidden);
        ProductionBuildTenantGate.Require(tenant, row.TenantId);
        var graph = row.GraphJson ?? "";
        var needle = "\"" + shotCode + "\"";
        if (!graph.Contains(needle, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
    }

    sealed class Row
    {
        public Guid? TenantId { get; set; }
        public string? GraphJson { get; set; }
    }
}
