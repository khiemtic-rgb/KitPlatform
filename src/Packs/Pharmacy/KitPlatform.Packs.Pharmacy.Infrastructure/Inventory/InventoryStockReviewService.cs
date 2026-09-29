using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Core.Engines;
using KitPlatform.Packs.Pharmacy.Inventory;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.Pharmacy.Infrastructure;

internal sealed class InventoryStockReviewService : IInventoryStockReviewService
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;
    private readonly IBranchAccessService _branchAccess;
    private readonly IAuditEngine _audit;

    public InventoryStockReviewService(
        IDbConnectionFactory db,
        ITenantContext tenant,
        IBranchAccessService branchAccess,
        IAuditEngine audit)
    {
        _db = db;
        _tenant = tenant;
        _branchAccess = branchAccess;
        _audit = audit;
    }

    public async Task<IReadOnlyList<InventoryStockReviewDto>> ListConfirmedAsync(
        CancellationToken cancellationToken = default)
    {
        var scope = await _branchAccess.GetScopeAsync(cancellationToken);
        const string sql = """
            SELECT
                r.product_id AS ProductId,
                r.warehouse_id AS WarehouseId,
                r.confirmed_at AS ConfirmedAt,
                r.confirmed_by AS ConfirmedBy,
                r.qty_at_confirm AS QtyAtConfirm,
                r.value_at_confirm AS ValueAtConfirm
            FROM inventory_stock_reviews r
            WHERE r.tenant_id = @TenantId
            ORDER BY r.confirmed_at DESC
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = (await conn.QueryAsync<InventoryStockReviewDto>(sql, new { TenantId = _tenant.TenantId })).ToList();
        if (scope.Unrestricted)
            return rows;
        return rows.Where(r => scope.WarehouseIds.Contains(r.WarehouseId)).ToList();
    }

    public async Task<InventoryStockReviewDto> ConfirmAsync(
        ConfirmStockReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ProductId == Guid.Empty || request.WarehouseId == Guid.Empty)
            throw new InvalidOperationException("Thiếu sản phẩm hoặc kho.");
        if (request.Qty < 0 || request.Value < 0)
            throw new InvalidOperationException("Số lượng / giá trị không hợp lệ.");

        await _branchAccess.EnsureWarehouseAccessAsync(request.WarehouseId, cancellationToken);

        const string productSql = """
            SELECT EXISTS(
                SELECT 1 FROM products
                WHERE id = @ProductId AND tenant_id = @TenantId AND deleted_at IS NULL)
            """;
        const string warehouseSql = """
            SELECT EXISTS(
                SELECT 1 FROM warehouses
                WHERE id = @WarehouseId AND tenant_id = @TenantId AND deleted_at IS NULL)
            """;
        await using (var check = await _db.CreateOpenConnectionAsync(cancellationToken))
        {
            var productOk = await check.ExecuteScalarAsync<bool>(new CommandDefinition(
                productSql,
                new { TenantId = _tenant.TenantId, request.ProductId },
                cancellationToken: cancellationToken));
            var warehouseOk = await check.ExecuteScalarAsync<bool>(new CommandDefinition(
                warehouseSql,
                new { TenantId = _tenant.TenantId, request.WarehouseId },
                cancellationToken: cancellationToken));
            if (!productOk || !warehouseOk)
                throw new InvalidOperationException("Sản phẩm hoặc kho không thuộc nhà thuốc này.");
        }

        const string sql = """
            INSERT INTO inventory_stock_reviews (
                tenant_id, product_id, warehouse_id, confirmed_at, confirmed_by,
                qty_at_confirm, value_at_confirm)
            VALUES (
                @TenantId, @ProductId, @WarehouseId, NOW(), @UserId,
                @Qty, @Value)
            ON CONFLICT (tenant_id, product_id, warehouse_id) DO UPDATE
            SET confirmed_at = NOW(),
                confirmed_by = EXCLUDED.confirmed_by,
                qty_at_confirm = EXCLUDED.qty_at_confirm,
                value_at_confirm = EXCLUDED.value_at_confirm
            RETURNING
                product_id AS ProductId,
                warehouse_id AS WarehouseId,
                confirmed_at AS ConfirmedAt,
                confirmed_by AS ConfirmedBy,
                qty_at_confirm AS QtyAtConfirm,
                value_at_confirm AS ValueAtConfirm
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleAsync<InventoryStockReviewDto>(sql, new
        {
            TenantId = _tenant.TenantId,
            UserId = _tenant.UserId == Guid.Empty ? (Guid?)null : _tenant.UserId,
            request.ProductId,
            request.WarehouseId,
            request.Qty,
            request.Value,
        });

        await _audit.WriteAsync(
            "inventory_stock_review",
            request.ProductId,
            "confirm_clean",
            new { request.ProductId, request.WarehouseId, request.Qty, request.Value },
            cancellationToken);

        return row;
    }

    public async Task UnconfirmAsync(
        Guid productId,
        Guid warehouseId,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty || warehouseId == Guid.Empty)
            throw new InvalidOperationException("Thiếu sản phẩm hoặc kho.");

        await _branchAccess.EnsureWarehouseAccessAsync(warehouseId, cancellationToken);

        const string sql = """
            DELETE FROM inventory_stock_reviews
            WHERE tenant_id = @TenantId AND product_id = @ProductId AND warehouse_id = @WarehouseId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            TenantId = _tenant.TenantId,
            ProductId = productId,
            WarehouseId = warehouseId,
        });

        await _audit.WriteAsync(
            "inventory_stock_review",
            productId,
            "unconfirm_clean",
            new { productId, warehouseId },
            cancellationToken);
    }
}
