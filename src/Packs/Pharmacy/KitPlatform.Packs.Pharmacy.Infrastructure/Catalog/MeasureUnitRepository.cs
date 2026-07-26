using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.Pharmacy.Catalog;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.Pharmacy.Infrastructure;

internal sealed class MeasureUnitRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public MeasureUnitRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<MeasureUnitDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, unit_name AS UnitName, sort_order AS SortOrder, status AS Status
            FROM measure_units
            WHERE tenant_id = @TenantId AND deleted_at IS NULL
            ORDER BY sort_order, unit_name
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<MeasureUnitDto>(sql, new { TenantId = _tenant.TenantId })).ToList();
    }

    public async Task<MeasureUnitDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, unit_name AS UnitName, sort_order AS SortOrder, status AS Status
            FROM measure_units
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<MeasureUnitDto>(sql, new { Id = id, TenantId = _tenant.TenantId });
    }

    public async Task<bool> ExistsByNameAsync(string unitName, Guid? excludeId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM measure_units
                WHERE tenant_id = @TenantId AND lower(unit_name) = lower(@UnitName)
                  AND deleted_at IS NULL
                  AND (@ExcludeId::uuid IS NULL OR id <> @ExcludeId)
            )
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<bool>(sql, new
        {
            TenantId = _tenant.TenantId,
            UnitName = unitName,
            ExcludeId = excludeId,
        });
    }

    public async Task<Guid> CreateAsync(string unitName, int sortOrder, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO measure_units (tenant_id, unit_name, sort_order)
            VALUES (@TenantId, @UnitName, @SortOrder)
            RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId = _tenant.TenantId,
            UnitName = unitName,
            SortOrder = sortOrder,
        });
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateMeasureUnitRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE measure_units SET
                unit_name = @UnitName,
                sort_order = @SortOrder,
                status = @Status,
                updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new
        {
            Id = id,
            TenantId = _tenant.TenantId,
            UnitName = request.UnitName.Trim(),
            request.SortOrder,
            request.Status,
        }) > 0;
    }

    /// <summary>Số sản phẩm đang dùng đơn vị này (đối chiếu theo tên trong product_units).</summary>
    public async Task<int> CountProductUsagesAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)::int
            FROM product_units pu
            INNER JOIN measure_units m ON m.id = @Id AND m.tenant_id = @TenantId
            WHERE pu.tenant_id = @TenantId
              AND pu.status = 1
              AND lower(pu.unit_name) = lower(m.unit_name)
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<int>(sql, new { Id = id, TenantId = _tenant.TenantId });
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE measure_units SET deleted_at = NOW(), status = 2, updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new { Id = id, TenantId = _tenant.TenantId }) > 0;
    }
}
