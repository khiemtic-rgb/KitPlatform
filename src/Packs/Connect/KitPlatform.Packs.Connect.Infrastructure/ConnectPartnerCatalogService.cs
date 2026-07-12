using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Connect;

namespace KitPlatform.Packs.Connect.Infrastructure;

internal sealed class ConnectPartnerCatalogService : IConnectPartnerCatalogService
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;
    private readonly IConnectOrgProfileService _profiles;
    private readonly ConnectDoctorMembershipRepository _memberships;

    public ConnectPartnerCatalogService(
        IDbConnectionFactory db,
        ITenantContext tenant,
        IConnectOrgProfileService profiles,
        ConnectDoctorMembershipRepository memberships)
    {
        _db = db;
        _tenant = tenant;
        _profiles = profiles;
        _memberships = memberships;
    }

    public async Task<IReadOnlyList<ConnectPartnerProductDto>> SearchPharmacyProductsAsync(
        Guid pharmacyTenantId,
        string? query,
        CancellationToken cancellationToken = default)
    {
        if (pharmacyTenantId == Guid.Empty)
            throw new InvalidOperationException("pharmacyTenantId không hợp lệ.");

        var kind = await _profiles.GetOrgKindAsync(pharmacyTenantId, cancellationToken);
        if (!string.Equals(kind, ConnectOrgKinds.Pharmacy, StringComparison.Ordinal))
            throw new InvalidOperationException("Đối tác phải là nhà thuốc (org_kind=pharmacy).");

        if (pharmacyTenantId != _tenant.TenantId)
        {
            var linked = await _memberships.HasActiveOrgLinkWithAsync(pharmacyTenantId, cancellationToken);
            if (!linked)
                throw new InvalidOperationException(
                    "Chỉ xem được danh mục thuốc của nhà thuốc đã liên kết active trên Connect.");
        }

        var q = query?.Trim();
        if (string.IsNullOrEmpty(q) || q.Length < 2)
            return Array.Empty<ConnectPartnerProductDto>();

        const string sql = """
            SELECT
                p.id AS ProductId,
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                p.generic_name AS GenericName,
                u.unit_name AS DefaultUnitName,
                COALESCE((
                    SELECT SUM(b.quantity_available)
                    FROM inventory_batches b
                    WHERE b.tenant_id = p.tenant_id
                      AND b.product_id = p.id
                      AND b.quantity_available > 0
                ), 0)::numeric AS StockAvailableQty
            FROM products p
            LEFT JOIN LATERAL (
                SELECT pu.unit_name
                FROM product_units pu
                WHERE pu.product_id = p.id
                  AND pu.tenant_id = p.tenant_id
                  AND pu.status = 1
                ORDER BY pu.is_base_unit DESC, pu.unit_name ASC
                LIMIT 1
            ) u ON TRUE
            WHERE p.tenant_id = @PharmacyTenantId
              AND p.deleted_at IS NULL
              AND p.status = 1
              AND COALESCE(
                    p.dispensing_class,
                    CASE p.drug_type WHEN 2 THEN 'prescription' WHEN 3 THEN 'controlled' ELSE 'otc' END
                  ) <> 'controlled'
              AND (
                    p.product_code ILIKE @Search
                 OR p.product_name ILIKE @Search
                 OR COALESCE(p.generic_name, '') ILIKE @Search
              )
            ORDER BY
                CASE WHEN COALESCE((
                    SELECT SUM(b.quantity_available)
                    FROM inventory_batches b
                    WHERE b.tenant_id = p.tenant_id
                      AND b.product_id = p.id
                      AND b.quantity_available > 0
                ), 0) > 0 THEN 0 ELSE 1 END,
                p.product_name
            LIMIT 30
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<ConnectPartnerProductDto>(sql, new
        {
            PharmacyTenantId = pharmacyTenantId,
            Search = $"%{q}%",
        })).ToList();
    }
}
