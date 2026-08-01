using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.CustomerApp;
using KitPlatform.Application.Dashboard;
using KitPlatform.Packs.Pharmacy.Inventory;
using KitPlatform.Packs.Pharmacy.Procurement;
using KitPlatform.Packs.Pharmacy.Sales;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Infrastructure.Security;

namespace KitPlatform.Infrastructure.Dashboard;

internal sealed class DashboardRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public DashboardRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<DashboardOverviewDto> GetOverviewAsync(
        int expiryDays,
        decimal lowStockThreshold,
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        if (expiryDays < 1) expiryDays = 30;
        if (lowStockThreshold < 0) lowStockThreshold = 10;

        var utcNow = DateTime.UtcNow;
        var (todayStart, todayEnd) = VietnamBusinessCalendar.TodayRangeUtc(utcNow);
        var (weekStart, weekEnd) = VietnamBusinessCalendar.RollingDaysRangeUtc(utcNow, 7);
        var expiryCutoff = VietnamBusinessCalendar.Today(utcNow).AddDays(expiryDays);

        var orderWarehouseFilter = allowedWarehouseIds is { Length: > 0 }
            ? "AND o.warehouse_id = ANY(@AllowedWarehouseIds)"
            : string.Empty;
        var batchWarehouseFilter = allowedWarehouseIds is { Length: > 0 }
            ? "AND b.warehouse_id = ANY(@AllowedWarehouseIds)"
            : string.Empty;
        var warehouseJoinFilter = allowedWarehouseIds is { Length: > 0 }
            ? "AND w.id = ANY(@AllowedWarehouseIds)"
            : string.Empty;
        var poWarehouseFilter = allowedWarehouseIds is { Length: > 0 }
            ? "AND p.warehouse_id = ANY(@AllowedWarehouseIds)"
            : string.Empty;
        var draftWarehouseFilter = allowedWarehouseIds is { Length: > 0 }
            ? "AND warehouse_id = ANY(@AllowedWarehouseIds)"
            : string.Empty;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        var salesTodaySql = $"""
            SELECT
                COALESCE((
                    SELECT SUM(sp.amount)
                    FROM sales_payments sp
                    INNER JOIN sales_orders o ON o.id = sp.sales_order_id
                    WHERE o.tenant_id = @TenantId
                      AND sp.paid_at >= @TodayStart AND sp.paid_at < @TodayEnd
                      {orderWarehouseFilter}
                ), 0)
                - COALESCE((
                    SELECT SUM(rp.amount)
                    FROM sales_return_payments rp
                    INNER JOIN sales_returns r ON r.id = rp.sales_return_id
                    INNER JOIN sales_orders o ON o.id = r.sales_order_id
                    WHERE r.tenant_id = @TenantId
                      AND rp.paid_at >= @TodayStart AND rp.paid_at < @TodayEnd
                      {orderWarehouseFilter}
                ), 0) AS TodayNetTotal,
                COALESCE((
                    SELECT SUM(sp.amount)
                    FROM sales_payments sp
                    INNER JOIN sales_orders o ON o.id = sp.sales_order_id
                    WHERE o.tenant_id = @TenantId
                      AND sp.paid_at >= @WeekStart AND sp.paid_at < @WeekEnd
                      {orderWarehouseFilter}
                ), 0)
                - COALESCE((
                    SELECT SUM(rp.amount)
                    FROM sales_return_payments rp
                    INNER JOIN sales_returns r ON r.id = rp.sales_return_id
                    INNER JOIN sales_orders o ON o.id = r.sales_order_id
                    WHERE r.tenant_id = @TenantId
                      AND rp.paid_at >= @WeekStart AND rp.paid_at < @WeekEnd
                      {orderWarehouseFilter}
                ), 0) AS WeekNetTotal,
                COALESCE((
                    SELECT COUNT(*)::int
                    FROM sales_orders o
                    WHERE o.tenant_id = @TenantId
                      AND o.status = @OrderCompleted
                      AND o.order_date >= @TodayStart AND o.order_date < @TodayEnd
                      {orderWarehouseFilter}
                ), 0) AS TodayOrderCount
            """;

        var queryParams = new
        {
            TenantId,
            TodayStart = todayStart,
            TodayEnd = todayEnd,
            WeekStart = weekStart,
            WeekEnd = weekEnd,
            OrderCompleted = SalesOrderStatuses.Completed,
            AllowedWarehouseIds = allowedWarehouseIds,
        };

        var sales = await conn.QuerySingleAsync<(decimal TodayNetTotal, decimal WeekNetTotal, int TodayOrderCount)>(
            salesTodaySql,
            queryParams);

        const string catalogSql = """
            SELECT
                (SELECT COUNT(*)::int FROM products WHERE tenant_id = @TenantId AND deleted_at IS NULL) AS ProductCount,
                (SELECT COUNT(*)::int FROM customers WHERE tenant_id = @TenantId AND deleted_at IS NULL) AS CustomerCount
            """;

        var catalog = await conn.QuerySingleAsync<(int ProductCount, int CustomerCount)>(
            catalogSql, new { TenantId });

        var inventorySql = $"""
            SELECT
                COUNT(*) FILTER (WHERE b.quantity_available > 0)::int AS ActiveBatchCount,
                COUNT(*) FILTER (
                    WHERE b.quantity_available > 0
                      AND b.expiry_date IS NOT NULL
                      AND b.expiry_date <= @ExpiryBefore
                )::int AS NearExpiryBatchCount,
                COUNT(*) FILTER (
                    WHERE b.quantity_available > 0
                      AND b.quantity_available <= {LowStockThresholdSql.EffectiveMinStockExpr}
                )::int AS LowStockBatchCount
            FROM inventory_batches b
            INNER JOIN products p ON p.id = b.product_id AND p.tenant_id = b.tenant_id
            INNER JOIN warehouses w ON w.id = b.warehouse_id AND w.tenant_id = b.tenant_id
            LEFT JOIN product_categories c
              ON c.id = p.category_id AND c.tenant_id = p.tenant_id AND c.deleted_at IS NULL
            WHERE b.tenant_id = @TenantId
              {batchWarehouseFilter}
            """;

        var inventory = await conn.QuerySingleAsync<(int ActiveBatchCount, int NearExpiryBatchCount, int LowStockBatchCount)>(
            inventorySql,
            new { TenantId, ExpiryBefore = expiryCutoff, FallbackThreshold = lowStockThreshold, AllowedWarehouseIds = allowedWarehouseIds });

        var lowStockProductSql = $"""
            SELECT COUNT(*)::int FROM (
                SELECT p.id, w.id
                FROM products p
                INNER JOIN warehouses w
                  ON w.tenant_id = p.tenant_id AND w.deleted_at IS NULL AND w.status = 1
                  {warehouseJoinFilter}
                LEFT JOIN product_categories c
                  ON c.id = p.category_id AND c.tenant_id = p.tenant_id AND c.deleted_at IS NULL
                LEFT JOIN inventory_batches b
                  ON b.product_id = p.id AND b.warehouse_id = w.id AND b.tenant_id = p.tenant_id
                WHERE p.tenant_id = @TenantId AND p.deleted_at IS NULL AND p.status = 1
                GROUP BY
                    p.id, p.min_stock_qty,
                    w.id, w.min_stock_qty,
                    c.min_stock_qty
                HAVING COALESCE(SUM(b.quantity_available), 0)
                    <= {LowStockThresholdSql.EffectiveMinStockExpr}
            ) sub
            """;

        var lowStockProductCount = await conn.QuerySingleAsync<int>(
            lowStockProductSql,
            new { TenantId, FallbackThreshold = lowStockThreshold, AllowedWarehouseIds = allowedWarehouseIds });

        var procurementSql = $"""
            SELECT COUNT(*)::int
            FROM purchase_orders p
            WHERE p.tenant_id = @TenantId
              AND p.deleted_at IS NULL
              AND p.status IN (@StatusApproved, @StatusPartial)
              AND EXISTS (
                  SELECT 1 FROM purchase_order_items i
                  WHERE i.purchase_order_id = p.id AND i.received_qty < i.ordered_qty
              )
              {poWarehouseFilter}
            """;

        var pendingPo = await conn.QuerySingleAsync<int>(
            procurementSql,
            new
            {
                TenantId,
                StatusApproved = PurchaseOrderStatuses.Approved,
                StatusPartial = PurchaseOrderStatuses.PartiallyReceived,
                AllowedWarehouseIds = allowedWarehouseIds,
            });

        var o2oSql = $"""
            SELECT
                COALESCE((
                    SELECT COUNT(*)::int FROM customer_draft_orders
                    WHERE tenant_id = @TenantId
                      AND status IN (@DraftSent, @DraftConfirmed)
                      {draftWarehouseFilter}
                ), 0) AS DraftOrdersAwaitingCount,
                COALESCE((
                    SELECT COUNT(*)::int FROM customer_reservations
                    WHERE tenant_id = @TenantId
                      AND status IN (@ResPending, @ResConfirmed, @ResReady)
                ), 0) AS ReservationsAwaitingCount,
                COALESCE((
                    SELECT SUM(staff_unread_count)::int FROM customer_chat_threads
                    WHERE tenant_id = @TenantId
                ), 0) AS ChatUnreadCount,
                COALESCE((
                    SELECT COUNT(*)::int FROM repurchase_suggestions rs
                    WHERE rs.tenant_id = @TenantId
                      AND (
                            (rs.status = 'pending'
                             AND rs.suggested_for_date IS NOT NULL
                             AND rs.suggested_for_date <= (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date)
                         OR (rs.status = 'snoozed'
                             AND (
                                 rs.snoozed_until IS NULL
                                 OR (rs.snoozed_until AT TIME ZONE 'Asia/Ho_Chi_Minh')::date
                                     <= (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date
                             ))
                      )
                ), 0) AS RefillOpportunityCount
            """;

        var o2o = await conn.QuerySingleAsync<(
            int DraftOrdersAwaitingCount,
            int ReservationsAwaitingCount,
            int ChatUnreadCount,
            int RefillOpportunityCount)>(
            o2oSql,
            new
            {
                TenantId,
                DraftSent = CustomerDraftOrderStatuses.Sent,
                DraftConfirmed = CustomerDraftOrderStatuses.Confirmed,
                ResPending = CustomerReservationStatuses.Pending,
                ResConfirmed = CustomerReservationStatuses.Confirmed,
                ResReady = CustomerReservationStatuses.Ready,
                AllowedWarehouseIds = allowedWarehouseIds,
            });

        return new DashboardOverviewDto(
            new DashboardSalesSnapshotDto(sales.TodayNetTotal, sales.WeekNetTotal, sales.TodayOrderCount),
            new DashboardCatalogSnapshotDto(catalog.ProductCount, catalog.CustomerCount),
            new DashboardInventorySnapshotDto(
                inventory.ActiveBatchCount,
                inventory.NearExpiryBatchCount,
                inventory.LowStockBatchCount,
                lowStockProductCount,
                expiryDays),
            new DashboardProcurementSnapshotDto(pendingPo),
            new DashboardO2oSnapshotDto(
                o2o.DraftOrdersAwaitingCount,
                o2o.ReservationsAwaitingCount,
                o2o.ChatUnreadCount,
                o2o.RefillOpportunityCount));
    }
}
