using Dapper;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.Configuration;
using PharmaCore.Application.Inventory;
using PharmaCore.Infrastructure.Configuration;
using PharmaCore.Infrastructure.Data;

namespace PharmaCore.Infrastructure.Inventory;

internal sealed class LowStockSettingsService : ILowStockSettingsService
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;
    private readonly ITenantSettingsService _tenantSettings;
    private readonly Catalog.CategoryRepository _categories;
    private readonly InventoryRepository _inventory;
    private readonly IBranchAccessService _branchAccess;

    public LowStockSettingsService(
        IDbConnectionFactory db,
        ITenantContext tenant,
        ITenantSettingsService tenantSettings,
        Catalog.CategoryRepository categories,
        InventoryRepository inventory,
        IBranchAccessService branchAccess)
    {
        _db = db;
        _tenant = tenant;
        _tenantSettings = tenantSettings;
        _categories = categories;
        _inventory = inventory;
        _branchAccess = branchAccess;
    }

    public async Task<LowStockSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var tenantDefault = await _tenantSettings.GetDefaultMinStockAsync(cancellationToken);
        var scope = await _branchAccess.GetScopeAsync(cancellationToken);

        const string sql = """
            SELECT
                c.id AS Id,
                c.category_code AS CategoryCode,
                c.category_name AS CategoryName,
                c.min_stock_qty AS MinStockQty,
                (SELECT COUNT(*)::int FROM products p
                 WHERE p.category_id = c.id AND p.tenant_id = c.tenant_id AND p.deleted_at IS NULL) AS ProductCount
            FROM product_categories c
            WHERE c.tenant_id = @TenantId AND c.deleted_at IS NULL
            ORDER BY c.sort_order, c.category_name
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var categories = (await conn.QueryAsync<CategoryLowStockSettingDto>(sql, new { TenantId = _tenant.TenantId })).ToList();

        const string warehouseSql = """
            SELECT
                w.id AS Id,
                w.warehouse_code AS WarehouseCode,
                w.warehouse_name AS WarehouseName,
                w.branch_id AS BranchId,
                b.branch_name AS BranchName,
                w.min_stock_qty AS MinStockQty,
                w.is_default AS IsDefault
            FROM warehouses w
            LEFT JOIN branches b
              ON b.id = w.branch_id AND b.tenant_id = w.tenant_id AND b.deleted_at IS NULL
            WHERE w.tenant_id = @TenantId AND w.deleted_at IS NULL AND w.status = 1
            ORDER BY w.is_default DESC, w.warehouse_name
            """;

        var warehouses = (await conn.QueryAsync<WarehouseLowStockSettingDto>(warehouseSql, new { TenantId = _tenant.TenantId })).ToList();
        if (!scope.Unrestricted)
            warehouses = warehouses.Where(w => scope.WarehouseIds.Contains(w.Id)).ToList();

        return new LowStockSettingsDto(
            tenantDefault.DefaultMinStockQty,
            LowStockThresholdSql.SystemFallback,
            categories,
            warehouses);
    }

    public Task<TenantDefaultMinStockDto> UpdateDefaultAsync(
        UpdateTenantDefaultMinStockRequest request,
        CancellationToken cancellationToken = default) =>
        _tenantSettings.UpdateDefaultMinStockAsync(request, cancellationToken);

    public async Task<ApplyLowStockResultDto> ApplyDefaultToProductsAsync(
        ApplyLowStockToProductsRequest request,
        CancellationToken cancellationToken = default)
    {
        decimal? qty = request.MinStockQty;
        if (qty is null)
        {
            var tenantDefault = await _tenantSettings.GetDefaultMinStockAsync(cancellationToken);
            qty = tenantDefault.DefaultMinStockQty;
        }

        if (qty is null)
            throw new InvalidOperationException("Chưa cấu hình ngưỡng chung. Nhập giá trị hoặc lưu ngưỡng mặc định trước.");

        if (qty < 0)
            throw new InvalidOperationException("Ngưỡng tồn tối thiểu không được âm.");

        const string sql = """
            UPDATE products
            SET min_stock_qty = @Qty, updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND deleted_at IS NULL
              AND (@OnlyUnset = FALSE OR min_stock_qty IS NULL)
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var updated = await conn.ExecuteAsync(sql, new
        {
            TenantId = _tenant.TenantId,
            Qty = qty,
            request.OnlyUnset,
        });

        return new ApplyLowStockResultDto(updated);
    }

    public async Task<ApplyLowStockResultDto> ApplyCategoryToProductsAsync(
        Guid categoryId,
        ApplyLowStockToProductsRequest request,
        CancellationToken cancellationToken = default)
    {
        const string categorySql = """
            SELECT min_stock_qty AS MinStockQty
            FROM product_categories
            WHERE id = @CategoryId AND tenant_id = @TenantId AND deleted_at IS NULL
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var categoryQty = await conn.QuerySingleOrDefaultAsync<decimal?>(
            categorySql, new { CategoryId = categoryId, TenantId = _tenant.TenantId });

        var qty = request.MinStockQty ?? categoryQty;
        if (qty is null)
            throw new InvalidOperationException("Danh mục chưa có ngưỡng tồn. Nhập ngưỡng cho danh mục trước.");

        if (qty < 0)
            throw new InvalidOperationException("Ngưỡng tồn tối thiểu không được âm.");

        const string sql = """
            UPDATE products
            SET min_stock_qty = @Qty, updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND category_id = @CategoryId
              AND deleted_at IS NULL
              AND (@OnlyUnset = FALSE OR min_stock_qty IS NULL)
            """;

        var updated = await conn.ExecuteAsync(sql, new
        {
            TenantId = _tenant.TenantId,
            CategoryId = categoryId,
            Qty = qty,
            request.OnlyUnset,
        });

        return new ApplyLowStockResultDto(updated);
    }

    public async Task<CategoryLowStockSettingDto?> UpdateCategoryMinStockAsync(
        Guid categoryId,
        decimal? minStockQty,
        CancellationToken cancellationToken = default)
    {
        if (minStockQty is < 0)
            throw new InvalidOperationException("Ngưỡng tồn tối thiểu không được âm.");

        var updated = await _categories.UpdateMinStockAsync(categoryId, minStockQty, cancellationToken);
        if (!updated) return null;

        const string sql = """
            SELECT
                c.id AS Id,
                c.category_code AS CategoryCode,
                c.category_name AS CategoryName,
                c.min_stock_qty AS MinStockQty,
                (SELECT COUNT(*)::int FROM products p
                 WHERE p.category_id = c.id AND p.tenant_id = c.tenant_id AND p.deleted_at IS NULL) AS ProductCount
            FROM product_categories c
            WHERE c.id = @CategoryId AND c.tenant_id = @TenantId AND c.deleted_at IS NULL
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<CategoryLowStockSettingDto>(sql, new
        {
            CategoryId = categoryId,
            TenantId = _tenant.TenantId,
        });
    }

    public async Task<WarehouseLowStockSettingDto?> UpdateWarehouseMinStockAsync(
        Guid warehouseId,
        decimal? minStockQty,
        CancellationToken cancellationToken = default)
    {
        if (minStockQty is < 0)
            throw new InvalidOperationException("Ngưỡng tồn tối thiểu không được âm.");

        await _branchAccess.EnsureWarehouseAccessAsync(warehouseId, cancellationToken);
        var updated = await _inventory.UpdateWarehouseMinStockAsync(warehouseId, minStockQty, cancellationToken);
        if (!updated) return null;

        const string sql = """
            SELECT
                w.id AS Id,
                w.warehouse_code AS WarehouseCode,
                w.warehouse_name AS WarehouseName,
                w.branch_id AS BranchId,
                b.branch_name AS BranchName,
                w.min_stock_qty AS MinStockQty,
                w.is_default AS IsDefault
            FROM warehouses w
            LEFT JOIN branches b
              ON b.id = w.branch_id AND b.tenant_id = w.tenant_id AND b.deleted_at IS NULL
            WHERE w.id = @WarehouseId AND w.tenant_id = @TenantId AND w.deleted_at IS NULL
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<WarehouseLowStockSettingDto>(sql, new
        {
            WarehouseId = warehouseId,
            TenantId = _tenant.TenantId,
        });
    }
}
