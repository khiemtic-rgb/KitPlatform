using System.Data;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.Pharmacy.Inventory;
using KitPlatform.Packs.Pharmacy.Procurement;
using KitPlatform.Application.Reports;
using KitPlatform.Packs.Pharmacy.Sales;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Infrastructure.Kernel.Pharmacy;

namespace KitPlatform.Infrastructure.Reports;

internal sealed class ReportsRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public ReportsRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    private static string BuildWarehouseFilter(string column, Guid? warehouseId, Guid[]? allowedWarehouseIds)
    {
        if (warehouseId.HasValue) return $"AND {column} = @WarehouseId";
        if (allowedWarehouseIds is { Length: > 0 }) return $"AND {column} = ANY(@AllowedWarehouseIds)";
        return string.Empty;
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetSalesRevenueByPeriodAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string groupBy,
        Guid? warehouseId,
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        var trunc = groupBy switch
        {
            ReportGroupBy.Week => "week",
            ReportGroupBy.Month => "month",
            _ => "day",
        };

        var warehouseFilter = BuildWarehouseFilter("o.warehouse_id", warehouseId, allowedWarehouseIds);

        var sql = $"""
            WITH sales AS (
                SELECT
                    date_trunc('{trunc}', timezone('Asia/Ho_Chi_Minh', sp.paid_at)) AS period_start,
                    COALESCE(SUM(sp.amount), 0) AS sales_amount
                FROM sales_payments sp
                INNER JOIN {PackPharmacyReadViews.SalesOrder} o ON o.id = sp.sales_order_id
                WHERE o.tenant_id = @TenantId
                  AND sp.paid_at >= @FromUtc AND sp.paid_at < @ToUtc
                  {warehouseFilter}
                GROUP BY 1
            ),
            refunds AS (
                SELECT
                    date_trunc('{trunc}', timezone('Asia/Ho_Chi_Minh', rp.paid_at)) AS period_start,
                    COALESCE(SUM(rp.amount), 0) AS refund_amount
                FROM sales_return_payments rp
                INNER JOIN sales_returns r ON r.id = rp.sales_return_id
                INNER JOIN {PackPharmacyReadViews.SalesOrder} o ON o.id = r.sales_order_id
                WHERE r.tenant_id = @TenantId
                  AND rp.paid_at >= @FromUtc AND rp.paid_at < @ToUtc
                  {warehouseFilter}
                GROUP BY 1
            ),
            orders AS (
                SELECT
                    date_trunc('{trunc}', timezone('Asia/Ho_Chi_Minh', o.order_date)) AS period_start,
                    COUNT(*)::int AS order_count
                FROM {PackPharmacyReadViews.SalesOrder} o
                WHERE o.tenant_id = @TenantId
                  AND o.status = @OrderCompleted
                  AND o.order_date >= @FromUtc AND o.order_date < @ToUtc
                  {warehouseFilter}
                GROUP BY 1
            )
            SELECT
                COALESCE(s.period_start, r.period_start, ord.period_start) AS PeriodStart,
                COALESCE(s.sales_amount, 0) AS SalesAmount,
                COALESCE(r.refund_amount, 0) AS RefundAmount,
                COALESCE(s.sales_amount, 0) - COALESCE(r.refund_amount, 0) AS NetAmount,
                COALESCE(ord.order_count, 0) AS OrderCount
            FROM sales s
            FULL OUTER JOIN refunds r ON r.period_start = s.period_start
            FULL OUTER JOIN orders ord ON ord.period_start = COALESCE(s.period_start, r.period_start)
            ORDER BY PeriodStart
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<SalesPeriodRow>(
            sql,
            new
            {
                TenantId,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                WarehouseId = warehouseId,
                AllowedWarehouseIds = allowedWarehouseIds,
                OrderCompleted = SalesOrderStatuses.Completed,
            });

        return rows.Select(r => new Dictionary<string, object?>
        {
            ["periodLabel"] = FormatPeriodLabel(r.PeriodStart, groupBy),
            ["salesAmount"] = r.SalesAmount,
            ["refundAmount"] = r.RefundAmount,
            ["netAmount"] = r.NetAmount,
            ["orderCount"] = r.OrderCount,
        }).ToList();
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetSalesRevenueByPaymentMethodAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? warehouseId,
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        var warehouseFilter = BuildWarehouseFilter("o.warehouse_id", warehouseId, allowedWarehouseIds);

        var sql = $"""
            WITH sales AS (
                SELECT sp.payment_method AS PaymentMethod, COALESCE(SUM(sp.amount), 0) AS Amount
                FROM sales_payments sp
                INNER JOIN {PackPharmacyReadViews.SalesOrder} o ON o.id = sp.sales_order_id
                WHERE o.tenant_id = @TenantId
                  AND sp.paid_at >= @FromUtc AND sp.paid_at < @ToUtc
                  /**warehouse**/
                GROUP BY sp.payment_method
            ),
            refunds AS (
                SELECT rp.payment_method AS PaymentMethod, COALESCE(SUM(rp.amount), 0) AS Amount
                FROM sales_return_payments rp
                INNER JOIN sales_returns r ON r.id = rp.sales_return_id
                INNER JOIN {PackPharmacyReadViews.SalesOrder} o ON o.id = r.sales_order_id
                WHERE r.tenant_id = @TenantId
                  AND rp.paid_at >= @FromUtc AND rp.paid_at < @ToUtc
                  /**warehouse**/
                GROUP BY rp.payment_method
            )
            SELECT
                COALESCE(s.PaymentMethod, r.PaymentMethod) AS PaymentMethod,
                COALESCE(s.Amount, 0) AS SalesAmount,
                COALESCE(r.Amount, 0) AS RefundAmount,
                COALESCE(s.Amount, 0) - COALESCE(r.Amount, 0) AS NetAmount
            FROM sales s
            FULL OUTER JOIN refunds r ON r.PaymentMethod = s.PaymentMethod
            ORDER BY PaymentMethod
            """;

        var finalSql = sql.Replace("/**warehouse**/", warehouseFilter);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<PaymentMethodRow>(
            finalSql,
            new { TenantId, FromUtc = fromUtc, ToUtc = toUtc, WarehouseId = warehouseId, AllowedWarehouseIds = allowedWarehouseIds });

        return rows.Select(r => new Dictionary<string, object?>
        {
            ["paymentMethod"] = r.PaymentMethod,
            ["paymentMethodLabel"] = PaymentMethodLabel(r.PaymentMethod),
            ["salesAmount"] = r.SalesAmount,
            ["refundAmount"] = r.RefundAmount,
            ["netAmount"] = r.NetAmount,
        }).ToList();
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetSalesRevenueByCategoryAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? warehouseId,
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        var warehouseFilter = BuildWarehouseFilter("o.warehouse_id", warehouseId, allowedWarehouseIds);

        var sql = $"""
            WITH sales AS (
                SELECT
                    COALESCE(c.id::text, 'uncategorized') AS CategoryKey,
                    COALESCE(c.category_name, 'Chưa phân loại') AS CategoryLabel,
                    COALESCE(SUM(i.line_total), 0) AS SalesAmount
                FROM sales_order_items i
                INNER JOIN {PackPharmacyReadViews.SalesOrder} o ON o.id = i.sales_order_id
                INNER JOIN {PackPharmacyReadViews.Product} p ON p.id = i.product_id AND p.tenant_id = o.tenant_id
                LEFT JOIN product_categories c
                    ON c.id = p.category_id AND c.tenant_id = p.tenant_id AND c.deleted_at IS NULL
                WHERE o.tenant_id = @TenantId
                  AND o.status = @OrderCompleted
                  AND EXISTS (
                      SELECT 1
                      FROM sales_payments sp
                      WHERE sp.sales_order_id = o.id
                        AND sp.paid_at >= @FromUtc AND sp.paid_at < @ToUtc
                  )
                  {warehouseFilter}
                GROUP BY c.id, c.category_name
            ),
            refunds AS (
                SELECT
                    COALESCE(c.id::text, 'uncategorized') AS CategoryKey,
                    COALESCE(c.category_name, 'Chưa phân loại') AS CategoryLabel,
                    COALESCE(SUM(ri.refund_amount), 0) AS RefundAmount
                FROM sales_return_items ri
                INNER JOIN sales_returns r ON r.id = ri.sales_return_id
                INNER JOIN sales_order_items i ON i.id = ri.sales_order_item_id
                INNER JOIN {PackPharmacyReadViews.SalesOrder} o ON o.id = r.sales_order_id
                INNER JOIN {PackPharmacyReadViews.Product} p ON p.id = i.product_id AND p.tenant_id = o.tenant_id
                LEFT JOIN product_categories c
                    ON c.id = p.category_id AND c.tenant_id = p.tenant_id AND c.deleted_at IS NULL
                WHERE r.tenant_id = @TenantId
                  AND EXISTS (
                      SELECT 1
                      FROM sales_return_payments rp
                      WHERE rp.sales_return_id = r.id
                        AND rp.paid_at >= @FromUtc AND rp.paid_at < @ToUtc
                  )
                  {warehouseFilter}
                GROUP BY c.id, c.category_name
            )
            SELECT
                COALESCE(s.CategoryKey, r.CategoryKey) AS CategoryKey,
                COALESCE(s.CategoryLabel, r.CategoryLabel) AS CategoryLabel,
                COALESCE(s.SalesAmount, 0) AS SalesAmount,
                COALESCE(r.RefundAmount, 0) AS RefundAmount,
                COALESCE(s.SalesAmount, 0) - COALESCE(r.RefundAmount, 0) AS NetAmount
            FROM sales s
            FULL OUTER JOIN refunds r ON r.CategoryKey = s.CategoryKey
            ORDER BY NetAmount DESC, CategoryLabel
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<CategoryRevenueRow>(
            sql,
            new
            {
                TenantId,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                WarehouseId = warehouseId,
                AllowedWarehouseIds = allowedWarehouseIds,
                OrderCompleted = SalesOrderStatuses.Completed,
            });

        return rows.Select(r => new Dictionary<string, object?>
        {
            ["categoryKey"] = r.CategoryKey,
            ["categoryLabel"] = r.CategoryLabel,
            ["salesAmount"] = r.SalesAmount,
            ["refundAmount"] = r.RefundAmount,
            ["netAmount"] = r.NetAmount,
        }).ToList();
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetSalesRevenueByClinicDoctorAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? warehouseId,
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        var warehouseFilter = BuildWarehouseFilter("o.warehouse_id", warehouseId, allowedWarehouseIds);

        var sql = $"""
            WITH base AS (
                SELECT
                    o.id AS OrderId,
                    o.warehouse_id,
                    COALESCE(NULLIF(BTRIM(ct.tenant_name), ''), '(Không rõ PK)') AS ClinicName,
                    COALESCE(
                        NULLIF(BTRIM(h.provider_display_name), ''),
                        '(Chưa có bác sĩ)'
                    ) AS DoctorName
                FROM public.sales_orders o
                INNER JOIN pack_connect.rx_handoffs h
                    ON h.id = o.connect_rx_handoff_id
                   AND h.pharmacy_tenant_id = o.tenant_id
                INNER JOIN public.tenants ct ON ct.id = h.clinic_tenant_id
                WHERE o.tenant_id = @TenantId
                  AND o.connect_rx_handoff_id IS NOT NULL
                  AND o.status = @OrderCompleted
                  {warehouseFilter}
            ),
            sales AS (
                SELECT
                    b.ClinicName,
                    b.DoctorName,
                    COUNT(DISTINCT b.OrderId)::int AS OrderCount,
                    COALESCE(SUM(sp.amount), 0) AS SalesAmount
                FROM base b
                INNER JOIN sales_payments sp ON sp.sales_order_id = b.OrderId
                WHERE sp.paid_at >= @FromUtc AND sp.paid_at < @ToUtc
                GROUP BY b.ClinicName, b.DoctorName
            ),
            refunds AS (
                SELECT
                    b.ClinicName,
                    b.DoctorName,
                    COALESCE(SUM(rp.amount), 0) AS RefundAmount
                FROM base b
                INNER JOIN sales_returns r ON r.sales_order_id = b.OrderId
                INNER JOIN sales_return_payments rp ON rp.sales_return_id = r.id
                WHERE rp.paid_at >= @FromUtc AND rp.paid_at < @ToUtc
                GROUP BY b.ClinicName, b.DoctorName
            )
            SELECT
                COALESCE(s.ClinicName, r.ClinicName) AS ClinicName,
                COALESCE(s.DoctorName, r.DoctorName) AS DoctorName,
                COALESCE(s.OrderCount, 0) AS OrderCount,
                COALESCE(s.SalesAmount, 0) AS SalesAmount,
                COALESCE(r.RefundAmount, 0) AS RefundAmount,
                COALESCE(s.SalesAmount, 0) - COALESCE(r.RefundAmount, 0) AS NetAmount
            FROM sales s
            FULL OUTER JOIN refunds r
                ON r.ClinicName = s.ClinicName AND r.DoctorName = s.DoctorName
            ORDER BY NetAmount DESC, ClinicName, DoctorName
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<ClinicDoctorRevenueRow>(
            sql,
            new
            {
                TenantId,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                WarehouseId = warehouseId,
                AllowedWarehouseIds = allowedWarehouseIds,
                OrderCompleted = SalesOrderStatuses.Completed,
            });

        return rows.Select(r => new Dictionary<string, object?>
        {
            ["clinicName"] = r.ClinicName,
            ["doctorName"] = r.DoctorName,
            ["orderCount"] = r.OrderCount,
            ["salesAmount"] = r.SalesAmount,
            ["refundAmount"] = r.RefundAmount,
            ["netAmount"] = r.NetAmount,
        }).ToList();
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetSalesRevenueByEmployeeAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? warehouseId,
        Guid[]? allowedWarehouseIds,
        Guid? employeeId,
        CancellationToken cancellationToken)
    {
        var warehouseFilter = BuildWarehouseFilter("o.warehouse_id", warehouseId, allowedWarehouseIds);
        var employeeFilter = employeeId.HasValue ? "AND o.employee_id = @EmployeeId" : "";

        var sql = $"""
            WITH sales AS (
                SELECT
                    o.employee_id AS EmployeeId,
                    COUNT(DISTINCT o.id)::int AS OrderCount,
                    COUNT(DISTINCT o.id) FILTER (WHERE o.customer_id IS NOT NULL)::int AS NamedOrderCount,
                    COALESCE(SUM(sp.amount), 0) AS SalesAmount
                FROM public.sales_orders o
                INNER JOIN sales_payments sp ON sp.sales_order_id = o.id
                WHERE o.tenant_id = @TenantId
                  AND o.status = @OrderCompleted
                  AND sp.paid_at >= @FromUtc AND sp.paid_at < @ToUtc
                  {warehouseFilter}
                  {employeeFilter}
                GROUP BY o.employee_id
            ),
            refunds AS (
                SELECT
                    o.employee_id AS EmployeeId,
                    COALESCE(SUM(rp.amount), 0) AS RefundAmount
                FROM sales_return_payments rp
                INNER JOIN sales_returns r ON r.id = rp.sales_return_id
                INNER JOIN public.sales_orders o ON o.id = r.sales_order_id
                WHERE r.tenant_id = @TenantId
                  AND rp.paid_at >= @FromUtc AND rp.paid_at < @ToUtc
                  {warehouseFilter}
                  {employeeFilter}
                GROUP BY o.employee_id
            )
            SELECT
                COALESCE(s.EmployeeId, r.EmployeeId) AS EmployeeId,
                COALESCE(e.full_name, 'Chưa gắn nhân viên') AS EmployeeName,
                COALESCE(s.OrderCount, 0) AS OrderCount,
                COALESCE(s.NamedOrderCount, 0) AS NamedOrderCount,
                COALESCE(s.SalesAmount, 0) AS SalesAmount,
                COALESCE(r.RefundAmount, 0) AS RefundAmount,
                COALESCE(s.SalesAmount, 0) - COALESCE(r.RefundAmount, 0) AS NetAmount
            FROM sales s
            FULL OUTER JOIN refunds r
                ON COALESCE(r.EmployeeId, '00000000-0000-0000-0000-000000000000')
                 = COALESCE(s.EmployeeId, '00000000-0000-0000-0000-000000000000')
            LEFT JOIN employees e ON e.id = COALESCE(s.EmployeeId, r.EmployeeId)
            ORDER BY NetAmount DESC, EmployeeName
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<EmployeeRevenueRow>(
            sql,
            new
            {
                TenantId,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                WarehouseId = warehouseId,
                AllowedWarehouseIds = allowedWarehouseIds,
                EmployeeId = employeeId,
                OrderCompleted = SalesOrderStatuses.Completed,
            });

        return rows.Select(r =>
        {
            var aov = r.OrderCount > 0 ? Math.Round(r.NetAmount / r.OrderCount, 0) : 0m;
            return new Dictionary<string, object?>
            {
                ["employeeId"] = r.EmployeeId,
                ["employeeName"] = r.EmployeeName,
                ["orderCount"] = r.OrderCount,
                ["namedOrderCount"] = r.NamedOrderCount,
                ["salesAmount"] = r.SalesAmount,
                ["refundAmount"] = r.RefundAmount,
                ["netAmount"] = r.NetAmount,
                ["aov"] = aov,
            };
        }).ToList();
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetSalesRevenueByEmployeeProductAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? warehouseId,
        Guid[]? allowedWarehouseIds,
        Guid? employeeId,
        string? search,
        CancellationToken cancellationToken)
    {
        var warehouseFilter = BuildWarehouseFilter("o.warehouse_id", warehouseId, allowedWarehouseIds);
        var employeeFilter = employeeId.HasValue ? "AND o.employee_id = @EmployeeId" : "";
        var searchFilter = string.IsNullOrWhiteSpace(search)
            ? ""
            : """
              AND (
                    p.product_code ILIKE '%' || @Search || '%'
                 OR p.product_name ILIKE '%' || @Search || '%'
              )
              """;

        var sql = $"""
            WITH sales AS (
                SELECT
                    o.employee_id AS EmployeeId,
                    p.id AS ProductId,
                    p.product_code AS ProductCode,
                    p.product_name AS ProductName,
                    COUNT(DISTINCT o.id)::int AS OrderCount,
                    COALESCE(SUM(i.quantity), 0) AS Qty,
                    COALESCE(SUM(i.line_total), 0) AS SalesAmount
                FROM sales_order_items i
                INNER JOIN public.sales_orders o ON o.id = i.sales_order_id
                INNER JOIN {PackPharmacyReadViews.Product} p ON p.id = i.product_id AND p.tenant_id = o.tenant_id
                WHERE o.tenant_id = @TenantId
                  AND o.status = @OrderCompleted
                  AND EXISTS (
                      SELECT 1 FROM sales_payments sp
                      WHERE sp.sales_order_id = o.id
                        AND sp.paid_at >= @FromUtc AND sp.paid_at < @ToUtc
                  )
                  {warehouseFilter}
                  {employeeFilter}
                  {searchFilter}
                GROUP BY o.employee_id, p.id, p.product_code, p.product_name
            ),
            refunds AS (
                SELECT
                    o.employee_id AS EmployeeId,
                    p.id AS ProductId,
                    p.product_code AS ProductCode,
                    p.product_name AS ProductName,
                    COALESCE(SUM(ri.quantity), 0) AS RefundQty,
                    COALESCE(SUM(ri.refund_amount), 0) AS RefundAmount
                FROM sales_return_items ri
                INNER JOIN sales_returns r ON r.id = ri.sales_return_id
                INNER JOIN sales_order_items i ON i.id = ri.sales_order_item_id
                INNER JOIN public.sales_orders o ON o.id = r.sales_order_id
                INNER JOIN {PackPharmacyReadViews.Product} p ON p.id = i.product_id AND p.tenant_id = o.tenant_id
                WHERE r.tenant_id = @TenantId
                  AND EXISTS (
                      SELECT 1 FROM sales_return_payments rp
                      WHERE rp.sales_return_id = r.id
                        AND rp.paid_at >= @FromUtc AND rp.paid_at < @ToUtc
                  )
                  {warehouseFilter}
                  {employeeFilter}
                  {searchFilter}
                GROUP BY o.employee_id, p.id, p.product_code, p.product_name
            )
            SELECT
                COALESCE(s.EmployeeId, r.EmployeeId) AS EmployeeId,
                COALESCE(e.full_name, 'Chưa gắn nhân viên') AS EmployeeName,
                COALESCE(s.ProductCode, r.ProductCode) AS ProductCode,
                COALESCE(s.ProductName, r.ProductName) AS ProductName,
                COALESCE(s.OrderCount, 0) AS OrderCount,
                COALESCE(s.Qty, 0) AS Qty,
                COALESCE(r.RefundQty, 0) AS RefundQty,
                COALESCE(s.Qty, 0) - COALESCE(r.RefundQty, 0) AS NetQty,
                COALESCE(s.SalesAmount, 0) AS SalesAmount,
                COALESCE(r.RefundAmount, 0) AS RefundAmount,
                COALESCE(s.SalesAmount, 0) - COALESCE(r.RefundAmount, 0) AS NetAmount
            FROM sales s
            FULL OUTER JOIN refunds r
                ON COALESCE(r.EmployeeId, '00000000-0000-0000-0000-000000000000')
                 = COALESCE(s.EmployeeId, '00000000-0000-0000-0000-000000000000')
               AND r.ProductId = s.ProductId
            LEFT JOIN employees e ON e.id = COALESCE(s.EmployeeId, r.EmployeeId)
            ORDER BY EmployeeName, NetAmount DESC, ProductName
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<EmployeeProductRevenueRow>(
            sql,
            new
            {
                TenantId,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                WarehouseId = warehouseId,
                AllowedWarehouseIds = allowedWarehouseIds,
                EmployeeId = employeeId,
                Search = search?.Trim(),
                OrderCompleted = SalesOrderStatuses.Completed,
            });

        return rows.Select(r => new Dictionary<string, object?>
        {
            ["employeeId"] = r.EmployeeId,
            ["employeeName"] = r.EmployeeName,
            ["productCode"] = r.ProductCode,
            ["productName"] = r.ProductName,
            ["orderCount"] = r.OrderCount,
            ["qty"] = r.Qty,
            ["refundQty"] = r.RefundQty,
            ["netQty"] = r.NetQty,
            ["salesAmount"] = r.SalesAmount,
            ["refundAmount"] = r.RefundAmount,
            ["netAmount"] = r.NetAmount,
        }).ToList();
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetSalesShiftsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? warehouseId,
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        var warehouseFilter = BuildWarehouseFilter("sh.warehouse_id", warehouseId, allowedWarehouseIds);

        var sql = $"""
            SELECT
                sh.shift_number AS ShiftNumber,
                w.warehouse_name AS WarehouseName,
                sh.opened_at AS OpenedAt,
                sh.closed_at AS ClosedAt,
                sh.opening_cash AS OpeningCash,
                sh.closing_cash AS ClosingCash,
                sh.cash_variance AS CashVariance,
                sh.status AS Status,
                COALESCE((
                    SELECT SUM(sp.amount)
                    FROM sales_payments sp
                    INNER JOIN {PackPharmacyReadViews.SalesOrder} o ON o.id = sp.sales_order_id
                    WHERE o.tenant_id = @TenantId
                      AND o.warehouse_id = sh.warehouse_id
                      AND sp.paid_at >= sh.opened_at
                      AND sp.paid_at < COALESCE(sh.closed_at, @ToUtc)
                ), 0)
                - COALESCE((
                    SELECT SUM(rp.amount)
                    FROM sales_return_payments rp
                    INNER JOIN sales_returns r ON r.id = rp.sales_return_id
                    WHERE r.tenant_id = @TenantId
                      AND rp.paid_at >= sh.opened_at
                      AND rp.paid_at < COALESCE(sh.closed_at, @ToUtc)
                ), 0) AS NetAmount
            FROM sales_shifts sh
            INNER JOIN {PackPharmacyReadViews.Warehouse} w ON w.id = sh.warehouse_id
            WHERE sh.tenant_id = @TenantId
              AND sh.opened_at >= @FromUtc AND sh.opened_at < @ToUtc
              {warehouseFilter}
            ORDER BY sh.opened_at DESC
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<ShiftReportRow>(
            sql,
            new { TenantId, FromUtc = fromUtc, ToUtc = toUtc, WarehouseId = warehouseId, AllowedWarehouseIds = allowedWarehouseIds });

        return rows.Select(r => new Dictionary<string, object?>
        {
            ["shiftNumber"] = r.ShiftNumber,
            ["warehouseName"] = r.WarehouseName,
            ["openedAt"] = r.OpenedAt,
            ["closedAt"] = r.ClosedAt,
            ["openingCash"] = r.OpeningCash,
            ["closingCash"] = r.ClosingCash,
            ["cashVariance"] = r.CashVariance,
            ["status"] = r.Status,
            ["statusLabel"] = ShiftStatusLabel(r.Status),
            ["netAmount"] = r.NetAmount,
        }).ToList();
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetProcurementGrnValueAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string groupBy,
        Guid? supplierId,
        Guid? warehouseId,
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        var warehouseFilter = BuildWarehouseFilter("gr.warehouse_id", warehouseId, allowedWarehouseIds);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        if (groupBy == ReportGroupBy.Supplier)
        {
            var sql = $"""
                SELECT
                    s.supplier_code AS SupplierCode,
                    s.supplier_name AS SupplierName,
                    COUNT(DISTINCT gr.id)::int AS GrnCount,
                    COALESCE(SUM(gi.quantity), 0) AS TotalQty,
                    COALESCE(SUM(gi.line_total), 0) AS PreTaxAmount
                FROM {PackPharmacyReadViews.GoodsReceipt} gr
                INNER JOIN goods_receipt_items gi ON gi.goods_receipt_id = gr.id
                INNER JOIN {PackPharmacyReadViews.Supplier} s ON s.id = gr.supplier_id
                WHERE gr.tenant_id = @TenantId
                  AND gr.deleted_at IS NULL
                  AND gr.status = @GrnCompleted
                  AND gr.receipt_date >= @FromDate AND gr.receipt_date < @ToDate
                  AND (@SupplierId IS NULL OR gr.supplier_id = @SupplierId)
                  {warehouseFilter}
                GROUP BY s.id, s.supplier_code, s.supplier_name
                ORDER BY PreTaxAmount DESC, s.supplier_name
                """;

            var rows = await conn.QueryAsync<GrnSupplierRow>(
                sql,
                new
                {
                    TenantId,
                    FromDate = fromUtc,
                    ToDate = toUtc,
                    SupplierId = supplierId,
                    WarehouseId = warehouseId,
                    AllowedWarehouseIds = allowedWarehouseIds,
                    GrnCompleted = GoodsReceiptStatuses.Completed,
                });

            return rows.Select(r => new Dictionary<string, object?>
            {
                ["supplierCode"] = r.SupplierCode,
                ["supplierName"] = r.SupplierName,
                ["grnCount"] = r.GrnCount,
                ["totalQty"] = r.TotalQty,
                ["preTaxAmount"] = r.PreTaxAmount,
            }).ToList();
        }

        var trunc = groupBy switch
        {
            ReportGroupBy.Month => "month",
            ReportGroupBy.Week => "week",
            _ => "day",
        };
        var periodSql = $"""
            SELECT
                date_trunc('{trunc}', timezone('Asia/Ho_Chi_Minh', gr.receipt_date)) AS PeriodStart,
                COUNT(DISTINCT gr.id)::int AS GrnCount,
                COALESCE(SUM(gi.quantity), 0) AS TotalQty,
                COALESCE(SUM(gi.line_total), 0) AS PreTaxAmount
            FROM {PackPharmacyReadViews.GoodsReceipt} gr
            INNER JOIN goods_receipt_items gi ON gi.goods_receipt_id = gr.id
            WHERE gr.tenant_id = @TenantId
              AND gr.deleted_at IS NULL
              AND gr.status = @GrnCompleted
              AND gr.receipt_date >= @FromDate AND gr.receipt_date < @ToDate
              AND (@SupplierId IS NULL OR gr.supplier_id = @SupplierId)
              {warehouseFilter}
            GROUP BY 1
            ORDER BY PeriodStart
            """;

        var periodRows = await conn.QueryAsync<GrnPeriodRow>(
            periodSql,
            new
            {
                TenantId,
                FromDate = fromUtc,
                ToDate = toUtc,
                SupplierId = supplierId,
                WarehouseId = warehouseId,
                AllowedWarehouseIds = allowedWarehouseIds,
                GrnCompleted = GoodsReceiptStatuses.Completed,
            });

        return periodRows.Select(r => new Dictionary<string, object?>
        {
            ["periodLabel"] = FormatPeriodLabel(r.PeriodStart, groupBy),
            ["grnCount"] = r.GrnCount,
            ["totalQty"] = r.TotalQty,
            ["preTaxAmount"] = r.PreTaxAmount,
        }).ToList();
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetProcurementGrnDocumentsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string groupBy,
        Guid? supplierId,
        Guid? warehouseId,
        Guid[]? allowedWarehouseIds,
        string? search,
        CancellationToken cancellationToken)
    {
        var warehouseFilter = BuildWarehouseFilter("gr.warehouse_id", warehouseId, allowedWarehouseIds);
        var searchFilter = string.IsNullOrWhiteSpace(search)
            ? ""
            : "AND (gr.grn_number ILIKE @Search OR s.supplier_name ILIKE @Search OR s.supplier_code ILIKE @Search)";
        var trunc = groupBy switch
        {
            ReportGroupBy.Month => "month",
            ReportGroupBy.Week => "week",
            _ => "day",
        };
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        var sql = $"""
            SELECT
                gr.id AS GrnId,
                gr.grn_number AS GrnNumber,
                date_trunc('{trunc}', timezone('Asia/Ho_Chi_Minh', gr.receipt_date)) AS PeriodStart,
                gr.receipt_date AS ReceiptDate,
                s.supplier_name AS SupplierName,
                w.warehouse_name AS WarehouseName,
                COALESCE(SUM(gi.quantity), 0) AS TotalQty,
                COALESCE(NULLIF(gh.merchandise_net, 0), SUM(gi.line_total), 0) AS PreTaxAmount,
                COALESCE(gh.tax_amount, 0) AS TaxAmount,
                COALESCE(NULLIF(gh.total_amount, 0), COALESCE(NULLIF(gh.merchandise_net, 0), SUM(gi.line_total), 0) + COALESCE(gh.tax_amount, 0)) AS TotalAmount,
                gr.status AS Status
            FROM {PackPharmacyReadViews.GoodsReceipt} gr
            INNER JOIN public.goods_receipts gh ON gh.id = gr.id
            INNER JOIN goods_receipt_items gi ON gi.goods_receipt_id = gr.id
            INNER JOIN {PackPharmacyReadViews.Supplier} s ON s.id = gr.supplier_id
            INNER JOIN warehouses w ON w.id = gr.warehouse_id
            WHERE gr.tenant_id = @TenantId
              AND gr.deleted_at IS NULL
              AND gr.status = @GrnCompleted
              AND gr.receipt_date >= @FromDate AND gr.receipt_date < @ToDate
              AND (@SupplierId IS NULL OR gr.supplier_id = @SupplierId)
              {warehouseFilter}
              {searchFilter}
            GROUP BY gr.id, gr.grn_number, gr.receipt_date, s.supplier_name, w.warehouse_name,
                     gh.merchandise_net, gh.tax_amount, gh.total_amount, gr.status
            ORDER BY gr.receipt_date DESC, gr.grn_number DESC
            """;

        var rows = await conn.QueryAsync<GrnDocumentRow>(
            sql,
            new
            {
                TenantId,
                FromDate = fromUtc,
                ToDate = toUtc,
                SupplierId = supplierId,
                WarehouseId = warehouseId,
                AllowedWarehouseIds = allowedWarehouseIds,
                GrnCompleted = GoodsReceiptStatuses.Completed,
                Search = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%",
            });

        return rows.Select(r => new Dictionary<string, object?>
        {
            ["grnId"] = r.GrnId,
            ["periodLabel"] = FormatPeriodLabel(r.PeriodStart, groupBy),
            ["grnNumber"] = r.GrnNumber,
            ["receiptDate"] = r.ReceiptDate,
            ["supplierName"] = r.SupplierName,
            ["warehouseName"] = r.WarehouseName,
            ["totalQty"] = r.TotalQty,
            ["preTaxAmount"] = r.PreTaxAmount,
            ["taxAmount"] = r.TaxAmount,
            ["totalAmount"] = r.TotalAmount,
            ["status"] = r.Status,
            ["statusLabel"] = GrnStatusLabel(r.Status),
        }).ToList();
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetSalesRevenueByCustomerAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? warehouseId,
        Guid[]? allowedWarehouseIds,
        string? search,
        CancellationToken cancellationToken)
    {
        var warehouseFilter = BuildWarehouseFilter("o.warehouse_id", warehouseId, allowedWarehouseIds);
        var searchFilter = string.IsNullOrWhiteSpace(search)
            ? ""
            : "AND (c.customer_code ILIKE @Search OR c.full_name ILIKE @Search)";

        var sql = $"""
            WITH sales AS (
                SELECT
                    o.customer_id AS CustomerId,
                    COUNT(DISTINCT o.id)::int AS OrderCount,
                    COALESCE(SUM(sp.amount), 0) AS SalesAmount,
                    MAX(o.order_date) AS LastOrderAt,
                    MIN(o.order_date) AS FirstInPeriodAt
                FROM public.sales_orders o
                INNER JOIN sales_payments sp ON sp.sales_order_id = o.id
                WHERE o.tenant_id = @TenantId
                  AND o.status = @OrderCompleted
                  AND o.customer_id IS NOT NULL
                  AND sp.paid_at >= @FromUtc AND sp.paid_at < @ToUtc
                  {warehouseFilter}
                GROUP BY o.customer_id
            ),
            refunds AS (
                SELECT
                    o.customer_id AS CustomerId,
                    COALESCE(SUM(rp.amount), 0) AS RefundAmount
                FROM sales_return_payments rp
                INNER JOIN sales_returns r ON r.id = rp.sales_return_id
                INNER JOIN public.sales_orders o ON o.id = r.sales_order_id
                WHERE r.tenant_id = @TenantId
                  AND o.customer_id IS NOT NULL
                  AND rp.paid_at >= @FromUtc AND rp.paid_at < @ToUtc
                  {warehouseFilter}
                GROUP BY o.customer_id
            ),
            prior AS (
                SELECT DISTINCT o.customer_id
                FROM public.sales_orders o
                WHERE o.tenant_id = @TenantId
                  AND o.status = @OrderCompleted
                  AND o.customer_id IS NOT NULL
                  AND o.order_date < @FromUtc
            )
            SELECT
                COALESCE(s.CustomerId, r.CustomerId) AS CustomerId,
                COALESCE(c.customer_code, '') AS CustomerCode,
                COALESCE(c.full_name, 'Khách đã xóa') AS CustomerName,
                COALESCE(s.OrderCount, 0) AS OrderCount,
                COALESCE(s.SalesAmount, 0) AS SalesAmount,
                COALESCE(r.RefundAmount, 0) AS RefundAmount,
                COALESCE(s.SalesAmount, 0) - COALESCE(r.RefundAmount, 0) AS NetAmount,
                s.LastOrderAt,
                s.FirstInPeriodAt,
                (p.customer_id IS NOT NULL) AS IsReturning
            FROM sales s
            FULL OUTER JOIN refunds r ON r.CustomerId = s.CustomerId
            LEFT JOIN customers c ON c.id = COALESCE(s.CustomerId, r.CustomerId)
            LEFT JOIN prior p ON p.customer_id = COALESCE(s.CustomerId, r.CustomerId)
            WHERE 1 = 1
              {searchFilter}
            ORDER BY NetAmount DESC, CustomerName
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<CustomerRevenueRow>(
            sql,
            new
            {
                TenantId,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                WarehouseId = warehouseId,
                AllowedWarehouseIds = allowedWarehouseIds,
                Search = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%",
                OrderCompleted = SalesOrderStatuses.Completed,
            });

        return rows.Select(r => new Dictionary<string, object?>
        {
            ["customerId"] = r.CustomerId,
            ["customerCode"] = r.CustomerCode,
            ["customerName"] = r.CustomerName,
            ["orderCount"] = r.OrderCount,
            ["salesAmount"] = r.SalesAmount,
            ["refundAmount"] = r.RefundAmount,
            ["netAmount"] = r.NetAmount,
            ["lastOrderAt"] = r.LastOrderAt,
            ["firstInPeriodAt"] = r.FirstInPeriodAt,
            ["isReturning"] = r.IsReturning,
            ["segmentLabel"] = r.IsReturning ? "Quay lại" : "Lần đầu",
        }).ToList();
    }

    public async Task<CustomerInsightRow> GetSalesCustomerInsightAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? warehouseId,
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        var warehouseFilter = BuildWarehouseFilter("o.warehouse_id", warehouseId, allowedWarehouseIds);
        var sql = $"""
            WITH orders AS (
                SELECT
                    o.customer_id,
                    timezone('Asia/Ho_Chi_Minh', o.order_date) AS vn_at
                FROM public.sales_orders o
                WHERE o.tenant_id = @TenantId
                  AND o.status = @OrderCompleted
                  AND o.order_date >= @FromUtc AND o.order_date < @ToUtc
                  {warehouseFilter}
            )
            SELECT
                COUNT(*)::int AS AllOrderCount,
                COUNT(*) FILTER (WHERE customer_id IS NULL)::int AS WalkInOrderCount,
                COUNT(*) FILTER (WHERE EXTRACT(DOW FROM vn_at) NOT IN (0, 6))::int AS WeekdayOrderCount,
                COUNT(*) FILTER (WHERE EXTRACT(DOW FROM vn_at) IN (0, 6))::int AS WeekendOrderCount,
                (
                    SELECT EXTRACT(HOUR FROM vn_at)::int
                    FROM orders
                    WHERE customer_id IS NOT NULL
                    GROUP BY 1
                    ORDER BY COUNT(*) DESC, 1
                    LIMIT 1
                ) AS PeakHour,
                (
                    SELECT COUNT(*)::int
                    FROM orders o2
                    WHERE o2.customer_id IS NOT NULL
                      AND EXTRACT(HOUR FROM o2.vn_at) = (
                          SELECT EXTRACT(HOUR FROM vn_at)
                          FROM orders
                          WHERE customer_id IS NOT NULL
                          GROUP BY 1
                          ORDER BY COUNT(*) DESC, 1
                          LIMIT 1
                      )
                ) AS PeakHourOrders
            FROM orders
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<CustomerInsightRow>(
            sql,
            new
            {
                TenantId,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                WarehouseId = warehouseId,
                AllowedWarehouseIds = allowedWarehouseIds,
                OrderCompleted = SalesOrderStatuses.Completed,
            });
        return row ?? new CustomerInsightRow();
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetInventoryStockSnapshotAsync(
        Guid? warehouseId,
        Guid[]? allowedWarehouseIds,
        string? search,
        Guid? categoryId,
        CancellationToken cancellationToken)
    {
        var extra = new List<string> { "b.quantity_available > 0" };
        if (warehouseId.HasValue)
            extra.Add("b.warehouse_id = @WarehouseId");
        else if (allowedWarehouseIds is { Length: > 0 })
            extra.Add("b.warehouse_id = ANY(@AllowedWarehouseIds)");
        if (categoryId.HasValue)
            extra.Add("p.category_id = @CategoryId");
        if (!string.IsNullOrWhiteSpace(search))
            extra.Add("(p.product_code ILIKE @Search OR p.product_name ILIKE @Search)");

        var sql = $"""
            SELECT
                p.id AS ProductId,
                w.id AS WarehouseId,
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                COALESCE(c.category_name, 'Chưa phân loại') AS CategoryLabel,
                p.category_id AS CategoryId,
                w.warehouse_name AS WarehouseName,
                (
                    SELECT pu.unit_name
                    FROM product_units pu
                    WHERE pu.product_id = p.id
                      AND pu.tenant_id = @TenantId
                      AND pu.is_sale_unit = TRUE
                    ORDER BY pu.conversion_factor
                    LIMIT 1
                ) AS UnitName,
                p.updated_at AS UpdatedAt,
                SUM(b.quantity_available) AS TotalQty,
                SUM(b.quantity_available * b.unit_cost) AS StockValue
            FROM {PackPharmacyReadViews.InventoryBatch} b
            INNER JOIN {PackPharmacyReadViews.Product} p ON p.id = b.product_id AND p.tenant_id = b.tenant_id
            INNER JOIN {PackPharmacyReadViews.Warehouse} w ON w.id = b.warehouse_id
            LEFT JOIN product_categories c
                ON c.id = p.category_id AND c.tenant_id = p.tenant_id AND c.deleted_at IS NULL
            WHERE b.tenant_id = @TenantId
              AND p.deleted_at IS NULL
              AND {string.Join(" AND ", extra)}
            GROUP BY p.id, p.tenant_id, p.product_code, p.product_name, p.category_id, c.category_name, w.id, w.warehouse_name, p.updated_at
            ORDER BY StockValue DESC, p.product_name
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<StockSnapshotRow>(
            sql,
            new
            {
                TenantId,
                WarehouseId = warehouseId,
                AllowedWarehouseIds = allowedWarehouseIds,
                CategoryId = categoryId,
                Search = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%",
            });

        return rows.Select(r => new Dictionary<string, object?>
        {
            ["productId"] = r.ProductId,
            ["warehouseId"] = r.WarehouseId,
            ["productCode"] = r.ProductCode,
            ["productName"] = r.ProductName,
            ["categoryLabel"] = r.CategoryLabel,
            ["categoryId"] = r.CategoryId,
            ["warehouseName"] = r.WarehouseName,
            ["unitName"] = r.UnitName,
            ["updatedAt"] = r.UpdatedAt,
            ["totalQty"] = r.TotalQty,
            ["stockValue"] = r.StockValue,
        }).ToList();
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetInventoryNearExpiryAsync(
        Guid? warehouseId,
        Guid[]? allowedWarehouseIds,
        DateOnly expiryBefore,
        CancellationToken cancellationToken)
    {
        var warehouseFilter = BuildWarehouseFilter("b.warehouse_id", warehouseId, allowedWarehouseIds);

        var sql = $"""
            SELECT
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                w.warehouse_name AS WarehouseName,
                b.batch_number AS BatchNumber,
                b.expiry_date AS ExpiryDate,
                b.quantity_available AS TotalQty,
                b.quantity_available * b.unit_cost AS StockValue
            FROM {PackPharmacyReadViews.InventoryBatch} b
            INNER JOIN {PackPharmacyReadViews.Product} p ON p.id = b.product_id AND p.tenant_id = b.tenant_id
            INNER JOIN {PackPharmacyReadViews.Warehouse} w ON w.id = b.warehouse_id
            WHERE b.tenant_id = @TenantId
              AND b.quantity_available > 0
              AND b.expiry_date IS NOT NULL
              AND b.expiry_date <= @ExpiryBefore
              AND p.deleted_at IS NULL
              {warehouseFilter}
            ORDER BY b.expiry_date, p.product_name, b.batch_number
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<NearExpiryRow>(
            sql,
            new { TenantId, WarehouseId = warehouseId, AllowedWarehouseIds = allowedWarehouseIds, ExpiryBefore = expiryBefore });

        return rows.Select(r => new Dictionary<string, object?>
        {
            ["productCode"] = r.ProductCode,
            ["productName"] = r.ProductName,
            ["warehouseName"] = r.WarehouseName,
            ["batchNumber"] = r.BatchNumber,
            ["expiryDate"] = r.ExpiryDate,
            ["totalQty"] = r.TotalQty,
            ["stockValue"] = r.StockValue,
        }).ToList();
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetInventoryMovementSummaryAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? warehouseId,
        Guid[]? allowedWarehouseIds,
        string? search,
        CancellationToken cancellationToken)
    {
        var warehouseFilter = BuildWarehouseFilter("sm.warehouse_id", warehouseId, allowedWarehouseIds);
        var searchFilter = string.IsNullOrWhiteSpace(search)
            ? string.Empty
            : "AND (p.product_code ILIKE @Search OR p.product_name ILIKE @Search)";

        var sql = $"""
            WITH movement_agg AS (
                SELECT
                    sm.product_id,
                    sm.warehouse_id,
                    SUM(CASE
                        WHEN sm.movement_date < @FromDate THEN
                            CASE
                                WHEN sm.movement_type = @MovementIn THEN sm.quantity
                                WHEN sm.movement_type = @MovementOut THEN -sm.quantity
                                ELSE 0
                            END
                        ELSE 0
                    END) AS OpeningQty,
                    SUM(CASE
                        WHEN sm.movement_type = @MovementIn
                             AND sm.movement_date >= @FromDate
                             AND sm.movement_date < @ToDate
                        THEN sm.quantity
                        ELSE 0
                    END) AS InQty,
                    SUM(CASE
                        WHEN sm.movement_type = @MovementOut
                             AND sm.movement_date >= @FromDate
                             AND sm.movement_date < @ToDate
                        THEN sm.quantity
                        ELSE 0
                    END) AS OutQty
                FROM stock_movements sm
                INNER JOIN {PackPharmacyReadViews.Product} p ON p.id = sm.product_id AND p.tenant_id = sm.tenant_id
                WHERE sm.tenant_id = @TenantId
                  AND p.deleted_at IS NULL
                  {warehouseFilter}
                  {searchFilter}
                GROUP BY sm.product_id, sm.warehouse_id
            )
            SELECT
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                w.warehouse_name AS WarehouseName,
                ma.OpeningQty,
                ma.InQty,
                ma.OutQty,
                ma.OpeningQty + ma.InQty - ma.OutQty AS ClosingQty
            FROM movement_agg ma
            INNER JOIN {PackPharmacyReadViews.Product} p ON p.id = ma.product_id AND p.tenant_id = @TenantId
            INNER JOIN {PackPharmacyReadViews.Warehouse} w ON w.id = ma.warehouse_id
            WHERE ma.OpeningQty <> 0 OR ma.InQty <> 0 OR ma.OutQty <> 0
            ORDER BY p.product_name, w.warehouse_name
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<MovementSummaryRow>(
            sql,
            new
            {
                TenantId,
                FromDate = fromUtc,
                ToDate = toUtc,
                WarehouseId = warehouseId,
                AllowedWarehouseIds = allowedWarehouseIds,
                Search = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%",
                MovementIn = StockMovementTypes.In,
                MovementOut = StockMovementTypes.Out,
            });

        return rows.Select(r => new Dictionary<string, object?>
        {
            ["productCode"] = r.ProductCode,
            ["productName"] = r.ProductName,
            ["warehouseName"] = r.WarehouseName,
            ["openingQty"] = r.OpeningQty,
            ["inQty"] = r.InQty,
            ["outQty"] = r.OutQty,
            ["closingQty"] = r.ClosingQty,
        }).ToList();
    }

    private static string FormatPeriodLabel(DateTime periodStart, string groupBy) =>
        groupBy switch
        {
            ReportGroupBy.Month => periodStart.ToString("MM/yyyy"),
            ReportGroupBy.Week => $"Tuần {periodStart:dd/MM/yyyy}",
            _ => periodStart.ToString("dd/MM/yyyy"),
        };

    private static string PaymentMethodLabel(short method) => method switch
    {
        SalesPaymentMethods.Cash => "Tiền mặt",
        SalesPaymentMethods.Card => "Thẻ",
        SalesPaymentMethods.Transfer => "Chuyển khoản",
        SalesPaymentMethods.EWallet => "Ví điện tử",
        _ => method.ToString(),
    };

    private static string ShiftStatusLabel(short status) => status switch
    {
        SalesShiftStatuses.Open => "Đang mở",
        SalesShiftStatuses.Closed => "Đã đóng",
        _ => status.ToString(),
    };

    private static string GrnStatusLabel(short status) => status switch
    {
        GoodsReceiptStatuses.Draft => "Chờ nhập kho",
        GoodsReceiptStatuses.Completed => "Hoàn tất",
        GoodsReceiptStatuses.Cancelled => "Đã hủy",
        _ => status.ToString(),
    };

    private sealed class SalesPeriodRow
    {
        public DateTime PeriodStart { get; init; }
        public decimal SalesAmount { get; init; }
        public decimal RefundAmount { get; init; }
        public decimal NetAmount { get; init; }
        public int OrderCount { get; init; }
    }

    private sealed class PaymentMethodRow
    {
        public short PaymentMethod { get; init; }
        public decimal SalesAmount { get; init; }
        public decimal RefundAmount { get; init; }
        public decimal NetAmount { get; init; }
    }

    private sealed class CategoryRevenueRow
    {
        public string CategoryKey { get; init; } = "";
        public string CategoryLabel { get; init; } = "";
        public decimal SalesAmount { get; init; }
        public decimal RefundAmount { get; init; }
        public decimal NetAmount { get; init; }
    }

    private sealed class ClinicDoctorRevenueRow
    {
        public string ClinicName { get; init; } = "";
        public string DoctorName { get; init; } = "";
        public int OrderCount { get; init; }
        public decimal SalesAmount { get; init; }
        public decimal RefundAmount { get; init; }
        public decimal NetAmount { get; init; }
    }

    private sealed class EmployeeRevenueRow
    {
        public Guid? EmployeeId { get; init; }
        public string EmployeeName { get; init; } = "";
        public int OrderCount { get; init; }
        public int NamedOrderCount { get; init; }
        public decimal SalesAmount { get; init; }
        public decimal RefundAmount { get; init; }
        public decimal NetAmount { get; init; }
    }

    private sealed class EmployeeProductRevenueRow
    {
        public Guid? EmployeeId { get; init; }
        public string EmployeeName { get; init; } = "";
        public string ProductCode { get; init; } = "";
        public string ProductName { get; init; } = "";
        public int OrderCount { get; init; }
        public decimal Qty { get; init; }
        public decimal RefundQty { get; init; }
        public decimal NetQty { get; init; }
        public decimal SalesAmount { get; init; }
        public decimal RefundAmount { get; init; }
        public decimal NetAmount { get; init; }
    }

    private sealed class ShiftReportRow
    {
        public string ShiftNumber { get; init; } = "";
        public string WarehouseName { get; init; } = "";
        public DateTime OpenedAt { get; init; }
        public DateTime? ClosedAt { get; init; }
        public decimal OpeningCash { get; init; }
        public decimal? ClosingCash { get; init; }
        public decimal? CashVariance { get; init; }
        public short Status { get; init; }
        public decimal NetAmount { get; init; }
    }

    private sealed class GrnSupplierRow
    {
        public string SupplierCode { get; init; } = "";
        public string SupplierName { get; init; } = "";
        public int GrnCount { get; init; }
        public decimal TotalQty { get; init; }
        public decimal PreTaxAmount { get; init; }
    }

    private sealed class GrnPeriodRow
    {
        public DateTime PeriodStart { get; init; }
        public int GrnCount { get; init; }
        public decimal TotalQty { get; init; }
        public decimal PreTaxAmount { get; init; }
    }

    private sealed class GrnDocumentRow
    {
        public Guid GrnId { get; init; }
        public string GrnNumber { get; init; } = "";
        public DateTime PeriodStart { get; init; }
        public DateTime ReceiptDate { get; init; }
        public string SupplierName { get; init; } = "";
        public string WarehouseName { get; init; } = "";
        public decimal TotalQty { get; init; }
        public decimal PreTaxAmount { get; init; }
        public decimal TaxAmount { get; init; }
        public decimal TotalAmount { get; init; }
        public short Status { get; init; }
    }

    internal sealed class CustomerInsightRow
    {
        public int AllOrderCount { get; init; }
        public int WalkInOrderCount { get; init; }
        public int WeekdayOrderCount { get; init; }
        public int WeekendOrderCount { get; init; }
        public int? PeakHour { get; init; }
        public int PeakHourOrders { get; init; }
    }

    private sealed class CustomerRevenueRow
    {
        public Guid? CustomerId { get; init; }
        public string CustomerCode { get; init; } = "";
        public string CustomerName { get; init; } = "";
        public int OrderCount { get; init; }
        public decimal SalesAmount { get; init; }
        public decimal RefundAmount { get; init; }
        public decimal NetAmount { get; init; }
        public DateTime? LastOrderAt { get; init; }
        public DateTime? FirstInPeriodAt { get; init; }
        public bool IsReturning { get; init; }
    }

    private sealed class StockSnapshotRow
    {
        public Guid ProductId { get; init; }
        public Guid WarehouseId { get; init; }
        public string ProductCode { get; init; } = "";
        public string ProductName { get; init; } = "";
        public string CategoryLabel { get; init; } = "";
        public Guid? CategoryId { get; init; }
        public string WarehouseName { get; init; } = "";
        public string? UnitName { get; init; }
        public DateTime? UpdatedAt { get; init; }
        public decimal TotalQty { get; init; }
        public decimal StockValue { get; init; }
    }

    private sealed class NearExpiryRow
    {
        public string ProductCode { get; init; } = "";
        public string ProductName { get; init; } = "";
        public string WarehouseName { get; init; } = "";
        public string BatchNumber { get; init; } = "";
        public DateOnly ExpiryDate { get; init; }
        public decimal TotalQty { get; init; }
        public decimal StockValue { get; init; }
    }

    private sealed class MovementSummaryRow
    {
        public string ProductCode { get; init; } = "";
        public string ProductName { get; init; } = "";
        public string WarehouseName { get; init; } = "";
        public decimal OpeningQty { get; init; }
        public decimal InQty { get; init; }
        public decimal OutQty { get; init; }
        public decimal ClosingQty { get; init; }
    }
}
