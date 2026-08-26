using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Success;
using KitPlatform.Infrastructure.Dashboard;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Pharmacy.Sales;

namespace KitPlatform.Infrastructure.Success;

internal sealed class OwnerCockpitRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public OwnerCockpitRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<(
        OwnerCockpitSalesExtrasDto Sales,
        OwnerCockpitInventoryExtrasDto Inventory,
        OwnerCockpitCustomerExtrasDto Customers,
        OwnerCockpitPeakHoursDto PeakHours,
        OwnerCockpitAssessmentSnapshotDto? Assessment)> GetExtrasAsync(
        int expiryDays,
        int dormantDays,
        int peakHoursWindowDays,
        int urgentExpiryDays,
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        if (expiryDays < 1) expiryDays = 30;
        if (dormantDays < 7) dormantDays = 7;
        if (dormantDays > 365) dormantDays = 365;
        if (peakHoursWindowDays < 7) peakHoursWindowDays = 7;
        if (peakHoursWindowDays > 90) peakHoursWindowDays = 90;
        if (urgentExpiryDays < 1) urgentExpiryDays = 7;
        if (urgentExpiryDays > expiryDays) urgentExpiryDays = expiryDays;

        var utcNow = DateTime.UtcNow;
        var (weekStart, weekEnd) = VietnamBusinessCalendar.RollingDaysRangeUtc(utcNow, 7);
        var (monthStart, monthEnd) = VietnamBusinessCalendar.MonthToDateRangeUtc(utcNow);
        var (peakStart, peakEnd) = VietnamBusinessCalendar.RollingDaysRangeUtc(utcNow, peakHoursWindowDays);
        var (_, todayEnd) = VietnamBusinessCalendar.TodayRangeUtc(utcNow);
        var todayVn = VietnamBusinessCalendar.Today(utcNow);
        var expiryCutoff = todayVn.AddDays(expiryDays);
        var urgentCutoff = todayVn.AddDays(urgentExpiryDays);
        var dormantBefore = VietnamBusinessCalendar.RollingDaysRangeUtc(utcNow, dormantDays).StartUtc;

        var orderWarehouseFilter = allowedWarehouseIds is { Length: > 0 }
            ? "AND o.warehouse_id = ANY(@AllowedWarehouseIds)"
            : string.Empty;
        var batchWarehouseFilter = allowedWarehouseIds is { Length: > 0 }
            ? "AND b.warehouse_id = ANY(@AllowedWarehouseIds)"
            : string.Empty;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        var salesSql = $"""
            SELECT
                COALESCE((
                    SELECT SUM(sp.amount)
                    FROM sales_payments sp
                    INNER JOIN sales_orders o ON o.id = sp.sales_order_id
                    WHERE o.tenant_id = @TenantId
                      AND sp.paid_at >= @MonthStart AND sp.paid_at < @MonthEnd
                      {orderWarehouseFilter}
                ), 0)
                - COALESCE((
                    SELECT SUM(rp.amount)
                    FROM sales_return_payments rp
                    INNER JOIN sales_returns r ON r.id = rp.sales_return_id
                    INNER JOIN sales_orders o ON o.id = r.sales_order_id
                    WHERE r.tenant_id = @TenantId
                      AND rp.paid_at >= @MonthStart AND rp.paid_at < @MonthEnd
                      {orderWarehouseFilter}
                ), 0) AS MonthNetTotal,
                COALESCE((
                    SELECT COUNT(*)::int
                    FROM sales_orders o
                    WHERE o.tenant_id = @TenantId
                      AND o.status = @OrderCompleted
                      AND o.order_date >= @WeekStart AND o.order_date < @WeekEnd
                      {orderWarehouseFilter}
                ), 0) AS WeekOrderCount,
                COALESCE((
                    SELECT COUNT(*)::int
                    FROM sales_orders o
                    WHERE o.tenant_id = @TenantId
                      AND o.status = @OrderCompleted
                      AND o.order_date >= @MonthStart AND o.order_date < @MonthEnd
                      {orderWarehouseFilter}
                ), 0) AS MonthOrderCount
            """;

        var sales = await conn.QuerySingleAsync<(decimal MonthNetTotal, int WeekOrderCount, int MonthOrderCount)>(
            salesSql,
            new
            {
                TenantId,
                WeekStart = weekStart,
                WeekEnd = weekEnd,
                MonthStart = monthStart,
                MonthEnd = monthEnd,
                OrderCompleted = SalesOrderStatuses.Completed,
                AllowedWarehouseIds = allowedWarehouseIds,
            });

        var invSql = $"""
            SELECT
                COUNT(DISTINCT b.product_id) FILTER (
                    WHERE b.expiry_date <= @ExpiryBefore
                )::int AS NearExpirySkuCount,
                COALESCE(SUM(b.quantity_available * COALESCE(b.unit_cost, 0)) FILTER (
                    WHERE b.expiry_date <= @ExpiryBefore
                ), 0) AS NearExpiryStockValue,
                COUNT(DISTINCT b.product_id) FILTER (
                    WHERE b.expiry_date <= @UrgentBefore
                )::int AS UrgentNearExpirySkuCount
            FROM inventory_batches b
            INNER JOIN products p ON p.id = b.product_id AND p.tenant_id = b.tenant_id AND p.deleted_at IS NULL
            WHERE b.tenant_id = @TenantId
              AND b.quantity_available > 0
              AND b.expiry_date IS NOT NULL
              AND b.expiry_date <= @ExpiryBefore
              {batchWarehouseFilter}
            """;

        var inventory = await conn.QuerySingleAsync<(
            int NearExpirySkuCount,
            decimal NearExpiryStockValue,
            int UrgentNearExpirySkuCount)>(
            invSql,
            new
            {
                TenantId,
                ExpiryBefore = expiryCutoff,
                UrgentBefore = urgentCutoff,
                AllowedWarehouseIds = allowedWarehouseIds,
            });

        const string customerSql = """
            SELECT
                COALESCE((
                    SELECT COUNT(*)::int
                    FROM customers c
                    WHERE c.tenant_id = @TenantId
                      AND c.deleted_at IS NULL
                      AND c.created_at >= @WeekStart AND c.created_at < @WeekEnd
                ), 0) AS NewCustomers7d,
                COALESCE((
                    SELECT COUNT(DISTINCT o.customer_id)::int
                    FROM sales_orders o
                    WHERE o.tenant_id = @TenantId
                      AND o.status = @OrderCompleted
                      AND o.customer_id IS NOT NULL
                      AND o.order_date >= @WeekStart AND o.order_date < @WeekEnd
                      AND EXISTS (
                          SELECT 1
                          FROM sales_orders prior
                          WHERE prior.tenant_id = o.tenant_id
                            AND prior.customer_id = o.customer_id
                            AND prior.status = @OrderCompleted
                            AND prior.order_date < @WeekStart
                      )
                ), 0) AS ReturningCustomers7d,
                COALESCE((
                    SELECT COUNT(*)::int
                    FROM (
                        SELECT o.customer_id
                        FROM sales_orders o
                        WHERE o.tenant_id = @TenantId
                          AND o.status = @OrderCompleted
                          AND o.customer_id IS NOT NULL
                        GROUP BY o.customer_id
                        HAVING MAX(o.order_date) < @DormantBefore
                    ) dormant
                ), 0) AS DormantBuyerCount,
                COALESCE((
                    SELECT COUNT(DISTINCT o.customer_id)::int
                    FROM sales_orders o
                    WHERE o.tenant_id = @TenantId
                      AND o.status = @OrderCompleted
                      AND o.customer_id IS NOT NULL
                      AND o.order_date >= @DormantBefore
                      AND o.order_date < @TodayEnd
                ), 0) AS ActiveBuyerCount
            """;

        var customers = await conn.QuerySingleAsync<(
            int NewCustomers7d,
            int ReturningCustomers7d,
            int DormantBuyerCount,
            int ActiveBuyerCount)>(
            customerSql,
            new
            {
                TenantId,
                WeekStart = weekStart,
                WeekEnd = weekEnd,
                DormantBefore = dormantBefore,
                TodayEnd = todayEnd,
                OrderCompleted = SalesOrderStatuses.Completed,
            });

        var peakSql = $"""
            SELECT
                EXTRACT(HOUR FROM o.order_date AT TIME ZONE 'Asia/Ho_Chi_Minh')::int AS Hour,
                COUNT(*)::int AS OrderCount,
                COALESCE(SUM(o.total_amount), 0) AS Revenue
            FROM sales_orders o
            WHERE o.tenant_id = @TenantId
              AND o.status = @OrderCompleted
              AND o.order_date >= @PeakStart AND o.order_date < @PeakEnd
              {orderWarehouseFilter}
            GROUP BY 1
            ORDER BY 1
            """;

        var peakRows = (await conn.QueryAsync<(int Hour, int OrderCount, decimal Revenue)>(
            peakSql,
            new
            {
                TenantId,
                PeakStart = peakStart,
                PeakEnd = peakEnd,
                OrderCompleted = SalesOrderStatuses.Completed,
                AllowedWarehouseIds = allowedWarehouseIds,
            })).ToList();

        var hours = new OwnerCockpitHourBucketDto[24];
        for (var h = 0; h < 24; h++)
            hours[h] = new OwnerCockpitHourBucketDto(h, 0, 0);
        foreach (var row in peakRows)
        {
            if (row.Hour is >= 0 and <= 23)
                hours[row.Hour] = new OwnerCockpitHourBucketDto(row.Hour, row.OrderCount, row.Revenue);
        }

        OwnerCockpitHourBucketDto? peak = null;
        foreach (var bucket in hours)
        {
            if (bucket.OrderCount <= 0) continue;
            if (peak is null
                || bucket.OrderCount > peak.OrderCount
                || (bucket.OrderCount == peak.OrderCount && bucket.Revenue > peak.Revenue))
            {
                peak = bucket;
            }
        }

        var peakHours = new OwnerCockpitPeakHoursDto(
            peakHoursWindowDays,
            peak?.Hour,
            peak?.OrderCount ?? 0,
            peak?.Revenue ?? 0,
            hours);

        OwnerCockpitAssessmentSnapshotDto? assessment = null;
        var hasAssessment = await conn.ExecuteScalarAsync<bool>("""
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'assessment_submission'
            )
            """);
        if (hasAssessment)
        {
            assessment = await conn.QuerySingleOrDefaultAsync<OwnerCockpitAssessmentSnapshotDto>("""
                SELECT
                    s.id AS SubmissionId,
                    s.overall_score AS OverallScore,
                    s.completed_at AS CompletedAt,
                    s.status AS Status
                FROM assessment_submission s
                WHERE s.tenant_id = @TenantId
                  AND s.archived_at IS NULL
                  AND s.status IN ('completed', 'lead_captured', 'report_ready')
                ORDER BY COALESCE(s.completed_at, s.started_at) DESC
                LIMIT 1
                """,
                new { TenantId });
        }

        return (
            new OwnerCockpitSalesExtrasDto(sales.MonthNetTotal, sales.WeekOrderCount, sales.MonthOrderCount),
            new OwnerCockpitInventoryExtrasDto(
                inventory.NearExpirySkuCount,
                inventory.NearExpiryStockValue,
                inventory.UrgentNearExpirySkuCount,
                urgentExpiryDays),
            new OwnerCockpitCustomerExtrasDto(
                customers.NewCustomers7d,
                customers.ReturningCustomers7d,
                customers.DormantBuyerCount,
                dormantDays,
                customers.ActiveBuyerCount),
            peakHours,
            assessment);
    }
}
