using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Connect;

namespace KitPlatform.Packs.Connect.Infrastructure;

internal sealed class ConnectOrgProfileRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public ConnectOrgProfileRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public Guid CurrentTenantId => _tenant.TenantId;

    public async Task<ConnectOrgProfileDto?> GetByTenantIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                t.id AS TenantId,
                t.tenant_code AS TenantCode,
                t.tenant_name AS TenantName,
                p.org_kind AS OrgKind,
                p.display_name AS DisplayName
            FROM pack_connect.org_profiles p
            INNER JOIN public.tenants t ON t.id = p.tenant_id
            WHERE p.tenant_id = @TenantId
              AND t.deleted_at IS NULL
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<ConnectOrgProfileDto>(sql, new { TenantId = tenantId });
    }

    public Task<ConnectOrgProfileDto?> GetMyProfileAsync(CancellationToken cancellationToken) =>
        GetByTenantIdAsync(CurrentTenantId, cancellationToken);

    public async Task<string?> GetOrgKindAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT org_kind
            FROM pack_connect.org_profiles
            WHERE tenant_id = @TenantId
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<string?>(sql, new { TenantId = tenantId });
    }
}
