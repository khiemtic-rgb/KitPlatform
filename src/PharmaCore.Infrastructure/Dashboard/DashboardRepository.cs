using Dapper;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.CustomerApp;
using PharmaCore.Application.Dashboard;
using PharmaCore.Application.Procurement;
using PharmaCore.Application.Sales;
using PharmaCore.Infrastructure.Data;
using PharmaCore.Infrastructure.Security;

namespace PharmaCore.Infrastructure.Dashboard;

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
        CancellationToken cancellationToken)
    {
        if (expiryDays < 1) expiryDays = 30;
        if (lowStockThreshold < 0) lowStockThreshold = 10;

        var utcNow = DateTime.UtcNow;
        var (todayStart, todayEnd) = VietnamBusinessCalendar.TodayRangeUtc(utcNow);
        var (weekStart, weekEnd) = VietnamBusinessCalendar.RollingDaysRangeUtc(utcNow, 7);
        var expiryCutoff = VietnamBusinessCalendar.Today(utcNow).AddDays(expiryDays);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        const string salesTodaySql = """
            SELECT
                COALESCE((
                    SELECT SUM(sp.amount)
                    FROM sales_payments sp
                    INNER JOIN sales_orders o ON o.id = sp.sales_order_id
                    WHERE o.tenant_id = @TenantId
                      AND sp.paid_at >= @TodayStart AND sp.paid_at < @TodayEnd
                ), 0)
                - COALESCE((
                    SELECT SUM(rp.amount)
                    FROM sales_return_payments rp
                    INNER JOIN sales_returns r ON r.id = rp.sales_return_id
                    WHERE r.tenant_id = @TenantId
                      AND rp.paid_at >= @TodayStart AND rp.paid_at < @TodayEnd
                ), 0) AS TodayNetTotal,
                COALESCE((
                    SELECT SUM(sp.amount)
                    FROM sales_payments sp
                    INNER JOIN sales_orders o ON o.id = sp.sales_order_id
                    WHERE o.tenant_id = @TenantId
                      AND sp.paid_at >= @WeekStart AND sp.paid_at < @WeekEnd
                ), 0)
                - COALESCE((
                    SELECT SUM(rp.amount)
                    FROM sales_return_payments rp
                    INNER JOIN sales_returns r ON r.id = rp.sales_return_id
                    WHERE r.tenant_id = @TenantId
                      AND rp.paid_at >= @WeekStart AND rp.paid_at < @WeekEnd
                ), 0) AS WeekNetTotal,
                COALESCE((
                    SELECT COUNT(*)::int
                    FROM sales_orders o
                    WHERE o.tenant_id = @TenantId
                      AND o.status = @OrderCompleted
                      AND o.order_date >= @TodayStart AND o.order_date < @TodayEnd
                ), 0) AS TodayOrderCount
            """;

        var sales = await conn.QuerySingleAsync<(decimal TodayNetTotal, decimal WeekNetTotal, int TodayOrderCount)>(
            salesTodaySql,
            new
            {
                TenantId,
                TodayStart = todayStart,
                TodayEnd = todayEnd,
                WeekStart = weekStart,
                WeekEnd = weekEnd,
                OrderCompleted = SalesOrderStatuses.Completed,
            });

        const string catalogSql = """
            SELECT
                (SELECT COUNT(*)::int FROM products WHERE tenant_id = @TenantId AND deleted_at IS NULL) AS ProductCount,
                (SELECT COUNT(*)::int FROM customers WHERE tenant_id = @TenantId AND deleted_at IS NULL) AS CustomerCount
            """;

        var catalog = await conn.QuerySingleAsync<(int ProductCount, int CustomerCount)>(
            catalogSql, new { TenantId });

        const string inventorySql = """
            SELECT
                COUNT(*) FILTER (WHERE b.quantity_available > 0)::int AS ActiveBatchCount,
                COUNT(*) FILTER (
                    WHERE b.quantity_available > 0
                      AND b.expiry_date IS NOT NULL
                      AND b.expiry_date <= @ExpiryBefore
                )::int AS NearExpiryBatchCount,
                COUNT(*) FILTER (
                    WHERE b.quantity_available > 0
                      AND b.quantity_available <= @LowStockThreshold
                )::int AS LowStockBatchCount
            FROM inventory_batches b
            WHERE b.tenant_id = @TenantId
            """;

        var inventory = await conn.QuerySingleAsync<(int ActiveBatchCount, int NearExpiryBatchCount, int LowStockBatchCount)>(
            inventorySql,
            new { TenantId, ExpiryBefore = expiryCutoff, LowStockThreshold = lowStockThreshold });

        const string procurementSql = """
            SELECT COUNT(*)::int
            FROM purchase_orders p
            WHERE p.tenant_id = @TenantId
              AND p.deleted_at IS NULL
              AND p.status IN (@StatusApproved, @StatusPartial)
              AND EXISTS (
                  SELECT 1 FROM purchase_order_items i
                  WHERE i.purchase_order_id = p.id AND i.received_qty < i.ordered_qty
              )
            """;

        var pendingPo = await conn.QuerySingleAsync<int>(
            procurementSql,
            new
            {
                TenantId,
                StatusApproved = PurchaseOrderStatuses.Approved,
                StatusPartial = PurchaseOrderStatuses.PartiallyReceived,
            });

        const string o2oSql = """
            SELECT
                COALESCE((
                    SELECT COUNT(*)::int FROM customer_draft_orders
                    WHERE tenant_id = @TenantId
                      AND status IN (@DraftSent, @DraftConfirmed)
                ), 0) AS DraftOrdersAwaitingCount,
                COALESCE((
                    SELECT COUNT(*)::int FROM customer_reservations
                    WHERE tenant_id = @TenantId
                      AND status IN (@ResPending, @ResConfirmed, @ResReady)
                ), 0) AS ReservationsAwaitingCount,
                COALESCE((
                    SELECT SUM(staff_unread_count)::int FROM customer_chat_threads
                    WHERE tenant_id = @TenantId
                ), 0) AS ChatUnreadCount
            """;

        var o2o = await conn.QuerySingleAsync<(int DraftOrdersAwaitingCount, int ReservationsAwaitingCount, int ChatUnreadCount)>(
            o2oSql,
            new
            {
                TenantId,
                DraftSent = CustomerDraftOrderStatuses.Sent,
                DraftConfirmed = CustomerDraftOrderStatuses.Confirmed,
                ResPending = CustomerReservationStatuses.Pending,
                ResConfirmed = CustomerReservationStatuses.Confirmed,
                ResReady = CustomerReservationStatuses.Ready,
            });

        return new DashboardOverviewDto(
            new DashboardSalesSnapshotDto(sales.TodayNetTotal, sales.WeekNetTotal, sales.TodayOrderCount),
            new DashboardCatalogSnapshotDto(catalog.ProductCount, catalog.CustomerCount),
            new DashboardInventorySnapshotDto(
                inventory.ActiveBatchCount,
                inventory.NearExpiryBatchCount,
                inventory.LowStockBatchCount,
                expiryDays),
            new DashboardProcurementSnapshotDto(pendingPo),
            new DashboardO2oSnapshotDto(
                o2o.DraftOrdersAwaitingCount,
                o2o.ReservationsAwaitingCount,
                o2o.ChatUnreadCount));
    }
}
