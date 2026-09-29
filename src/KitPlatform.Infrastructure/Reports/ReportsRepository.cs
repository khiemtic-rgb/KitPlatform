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
        var originalTotal = SalesAccrualSql.OriginalTotalExpr();
        var soldStatus = SalesAccrualSql.SoldStatusFilter();
        var isCheckout = SalesAccrualSql.IsCheckout();
        var isCollection = SalesAccrualSql.IsCollection();
        var cash = SalesPaymentMethods.Cash;
        var card = SalesPaymentMethods.Card;
        var transfer = SalesPaymentMethods.Transfer;
        var ewallet = SalesPaymentMethods.EWallet;

        var sql = $"""
            WITH sales AS (
                SELECT
                    date_trunc('{trunc}', timezone('Asia/Ho_Chi_Minh', o.order_date)) AS period_start,
                    COUNT(*)::int AS order_count,
                    COALESCE(SUM({originalTotal}), 0) AS sales_amount,
                    COALESCE(SUM(COALESCE(ck.checkout_paid, 0)), 0) AS checkout_paid
                FROM public.sales_orders o
                LEFT JOIN LATERAL (
                    SELECT COALESCE(SUM(sp.amount), 0) AS checkout_paid
                    FROM sales_payments sp
                    WHERE sp.sales_order_id = o.id
                      AND {isCheckout}
                ) ck ON TRUE
                WHERE o.tenant_id = @TenantId
                  AND {soldStatus}
                  AND o.order_date >= @FromUtc AND o.order_date < @ToUtc
                  {warehouseFilter}
                GROUP BY 1
            ),
            collections AS (
                SELECT
                    date_trunc('{trunc}', timezone('Asia/Ho_Chi_Minh', sp.paid_at)) AS period_start,
                    COALESCE(SUM(sp.amount), 0) AS collection_amount
                FROM sales_payments sp
                INNER JOIN public.sales_orders o ON o.id = sp.sales_order_id
                WHERE o.tenant_id = @TenantId
                  AND {isCollection}
                  AND sp.paid_at >= @FromUtc AND sp.paid_at < @ToUtc
                  {warehouseFilter}
                GROUP BY 1
            ),
            receipts AS (
                SELECT
                    date_trunc('{trunc}', timezone('Asia/Ho_Chi_Minh', sp.paid_at)) AS period_start,
                    COALESCE(SUM(sp.amount) FILTER (WHERE sp.payment_method = {cash}), 0) AS cash_amount,
                    COALESCE(SUM(sp.amount) FILTER (WHERE sp.payment_method = {card}), 0) AS card_amount,
                    COALESCE(SUM(sp.amount) FILTER (WHERE sp.payment_method = {transfer}), 0) AS transfer_amount,
                    COALESCE(SUM(sp.amount) FILTER (WHERE sp.payment_method = {ewallet}), 0) AS ewallet_amount
                FROM sales_payments sp
                INNER JOIN public.sales_orders o ON o.id = sp.sales_order_id
                WHERE o.tenant_id = @TenantId
                  AND sp.paid_at >= @FromUtc AND sp.paid_at < @ToUtc
                  {warehouseFilter}
                GROUP BY 1
            ),
            refunds AS (
                SELECT
                    x.period_start,
                    COALESCE(SUM(x.refund_amount), 0) AS refund_amount,
                    COALESCE(SUM(x.refund_cash), 0) AS refund_cash
                FROM (
                    SELECT
                        date_trunc('{trunc}', timezone('Asia/Ho_Chi_Minh', r.return_date)) AS period_start,
                        r.id,
                        COALESCE((
                            SELECT SUM(ri.refund_amount)
                            FROM sales_return_items ri
                            WHERE ri.sales_return_id = r.id
                        ), 0) AS refund_amount,
                        COALESCE((
                            SELECT SUM(rp.amount)
                            FROM sales_return_payments rp
                            WHERE rp.sales_return_id = r.id
                        ), 0) AS refund_cash
                    FROM sales_returns r
                    INNER JOIN public.sales_orders o ON o.id = r.sales_order_id
                    WHERE r.tenant_id = @TenantId
                      AND r.status = @ReturnCompleted
                      AND r.return_date >= @FromUtc AND r.return_date < @ToUtc
                      {warehouseFilter}
                ) x
                GROUP BY 1
            )
            SELECT
                COALESCE(s.period_start, c.period_start, rec.period_start, rf.period_start) AS PeriodStart,
                COALESCE(s.order_count, 0) AS OrderCount,
                COALESCE(s.sales_amount, 0) AS SalesAmount,
                COALESCE(s.checkout_paid, 0) AS CheckoutPaid,
                GREATEST(0, COALESCE(s.sales_amount, 0) - COALESCE(s.checkout_paid, 0)) AS NewDebt,
                COALESCE(c.collection_amount, 0) AS CollectionAmount,
                COALESCE(rec.cash_amount, 0) AS CashAmount,
                COALESCE(rec.card_amount, 0) AS CardAmount,
                COALESCE(rec.transfer_amount, 0) AS TransferAmount,
                COALESCE(rec.ewallet_amount, 0) AS EwalletAmount,
                COALESCE(rf.refund_amount, 0) AS RefundAmount,
                COALESCE(rf.refund_cash, 0) AS RefundCash,
                GREATEST(0, COALESCE(rf.refund_amount, 0) - COALESCE(rf.refund_cash, 0)) AS RefundDebt,
                COALESCE(s.sales_amount, 0) - COALESCE(rf.refund_amount, 0) AS NetAmount
            FROM sales s
            FULL OUTER JOIN collections c ON c.period_start = s.period_start
            FULL OUTER JOIN receipts rec
                ON rec.period_start = COALESCE(s.period_start, c.period_start)
            FULL OUTER JOIN refunds rf
                ON rf.period_start = COALESCE(s.period_start, c.period_start, rec.period_start)
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
                ReturnCompleted = SalesReturnStatuses.Completed,
            });

        return rows.Select(r => new Dictionary<string, object?>
        {
            ["periodLabel"] = FormatPeriodLabel(r.PeriodStart, groupBy),
            ["orderCount"] = r.OrderCount,
            ["salesAmount"] = r.SalesAmount,
            ["checkoutPaid"] = r.CheckoutPaid,
            ["newDebt"] = r.NewDebt,
            ["collectionAmount"] = r.CollectionAmount,
            ["cashAmount"] = r.CashAmount,
            ["cardAmount"] = r.CardAmount,
            ["transferAmount"] = r.TransferAmount,
            ["ewalletAmount"] = r.EwalletAmount,
            ["refundAmount"] = r.RefundAmount,
            ["refundCash"] = r.RefundCash,
            ["refundDebt"] = r.RefundDebt,
            ["netAmount"] = r.NetAmount,
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
        var isCheckout = SalesAccrualSql.IsCheckout();
        var isCollection = SalesAccrualSql.IsCollection();

        var sql = $"""
            WITH sales AS (
                SELECT
                    sp.payment_method AS PaymentMethod,
                    COALESCE(SUM(sp.amount) FILTER (WHERE {isCheckout}), 0) AS CheckoutAmount,
                    COALESCE(SUM(sp.amount) FILTER (WHERE {isCollection}), 0) AS CollectionAmount,
                    COALESCE(SUM(sp.amount), 0) AS Amount
                FROM sales_payments sp
                INNER JOIN public.sales_orders o ON o.id = sp.sales_order_id
                WHERE o.tenant_id = @TenantId
                  AND sp.paid_at >= @FromUtc AND sp.paid_at < @ToUtc
                  {warehouseFilter}
                GROUP BY sp.payment_method
            ),
            refunds AS (
                SELECT rp.payment_method AS PaymentMethod, COALESCE(SUM(rp.amount), 0) AS Amount
                FROM sales_return_payments rp
                INNER JOIN sales_returns r ON r.id = rp.sales_return_id
                INNER JOIN public.sales_orders o ON o.id = r.sales_order_id
                WHERE r.tenant_id = @TenantId
                  AND rp.paid_at >= @FromUtc AND rp.paid_at < @ToUtc
                  {warehouseFilter}
                GROUP BY rp.payment_method
            )
            SELECT
                COALESCE(s.PaymentMethod, r.PaymentMethod) AS PaymentMethod,
                COALESCE(s.CheckoutAmount, 0) AS CheckoutAmount,
                COALESCE(s.CollectionAmount, 0) AS CollectionAmount,
                COALESCE(s.Amount, 0) AS SalesAmount,
                COALESCE(r.Amount, 0) AS RefundAmount,
                COALESCE(s.Amount, 0) - COALESCE(r.Amount, 0) AS NetAmount
            FROM sales s
            FULL OUTER JOIN refunds r ON r.PaymentMethod = s.PaymentMethod
            ORDER BY PaymentMethod
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<PaymentMethodRow>(
            sql,
            new { TenantId, FromUtc = fromUtc, ToUtc = toUtc, WarehouseId = warehouseId, AllowedWarehouseIds = allowedWarehouseIds });

        return rows.Select(r => new Dictionary<string, object?>
        {
            ["paymentMethod"] = r.PaymentMethod,
            ["paymentMethodLabel"] = PaymentMethodLabel(r.PaymentMethod),
            ["checkoutAmount"] = r.CheckoutAmount,
            ["collectionAmount"] = r.CollectionAmount,
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
        var soldStatus = SalesAccrualSql.SoldStatusFilter();
        var lineRevenue = SalesAccrualSql.AllocatedLineRevenue();

        var sql = $"""
            WITH sales AS (
                SELECT
                    COALESCE(c.id::text, 'uncategorized') AS CategoryKey,
                    COALESCE(c.category_name, 'Chưa phân loại') AS CategoryLabel,
                    COALESCE(SUM({lineRevenue}), 0) AS SalesAmount
                FROM sales_order_items i
                INNER JOIN public.sales_orders o ON o.id = i.sales_order_id
                INNER JOIN {PackPharmacyReadViews.Product} p ON p.id = i.product_id AND p.tenant_id = o.tenant_id
                LEFT JOIN product_categories c
                    ON c.id = p.category_id AND c.tenant_id = p.tenant_id AND c.deleted_at IS NULL
                WHERE o.tenant_id = @TenantId
                  AND {soldStatus}
                  AND o.order_date >= @FromUtc AND o.order_date < @ToUtc
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
                INNER JOIN public.sales_orders o ON o.id = r.sales_order_id
                INNER JOIN {PackPharmacyReadViews.Product} p ON p.id = i.product_id AND p.tenant_id = o.tenant_id
                LEFT JOIN product_categories c
                    ON c.id = p.category_id AND c.tenant_id = p.tenant_id AND c.deleted_at IS NULL
                WHERE r.tenant_id = @TenantId
                  AND r.status = @ReturnCompleted
                  AND r.return_date >= @FromUtc AND r.return_date < @ToUtc
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
                ReturnCompleted = SalesReturnStatuses.Completed,
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
        var soldStatus = SalesAccrualSql.SoldStatusFilter();
        var originalTotal = SalesAccrualSql.OriginalTotalExpr();

        var sql = $"""
            WITH base AS (
                SELECT
                    o.id AS OrderId,
                    o.order_date,
                    {originalTotal} AS OriginalTotal,
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
                  AND {soldStatus}
                  {warehouseFilter}
            ),
            sales AS (
                SELECT
                    b.ClinicName,
                    b.DoctorName,
                    COUNT(*)::int AS OrderCount,
                    COALESCE(SUM(b.OriginalTotal), 0) AS SalesAmount
                FROM base b
                WHERE b.order_date >= @FromUtc AND b.order_date < @ToUtc
                GROUP BY b.ClinicName, b.DoctorName
            ),
            refunds AS (
                SELECT
                    b.ClinicName,
                    b.DoctorName,
                    COALESCE(SUM(ri.refund_amount), 0) AS RefundAmount
                FROM base b
                INNER JOIN sales_returns r ON r.sales_order_id = b.OrderId
                INNER JOIN sales_return_items ri ON ri.sales_return_id = r.id
                WHERE r.status = @ReturnCompleted
                  AND r.return_date >= @FromUtc AND r.return_date < @ToUtc
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
                ReturnCompleted = SalesReturnStatuses.Completed,
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
        var soldStatus = SalesAccrualSql.SoldStatusFilter();
        var originalTotal = SalesAccrualSql.OriginalTotalExpr();

        var sql = $"""
            WITH sales AS (
                SELECT
                    o.employee_id AS EmployeeId,
                    COUNT(*)::int AS OrderCount,
                    COUNT(*) FILTER (WHERE o.customer_id IS NOT NULL)::int AS NamedOrderCount,
                    COALESCE(SUM({originalTotal}), 0) AS SalesAmount
                FROM public.sales_orders o
                WHERE o.tenant_id = @TenantId
                  AND {soldStatus}
                  AND o.order_date >= @FromUtc AND o.order_date < @ToUtc
                  {warehouseFilter}
                  {employeeFilter}
                GROUP BY o.employee_id
            ),
            refunds AS (
                SELECT
                    o.employee_id AS EmployeeId,
                    COALESCE(SUM(ri.refund_amount), 0) AS RefundAmount
                FROM sales_return_items ri
                INNER JOIN sales_returns r ON r.id = ri.sales_return_id
                INNER JOIN public.sales_orders o ON o.id = r.sales_order_id
                WHERE r.tenant_id = @TenantId
                  AND r.status = @ReturnCompleted
                  AND r.return_date >= @FromUtc AND r.return_date < @ToUtc
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
                ReturnCompleted = SalesReturnStatuses.Completed,
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
        var soldStatus = SalesAccrualSql.SoldStatusFilter();
        var lineRevenue = SalesAccrualSql.AllocatedLineRevenue();

        var sql = $"""
            WITH sales AS (
                SELECT
                    o.employee_id AS EmployeeId,
                    p.id AS ProductId,
                    p.product_code AS ProductCode,
                    p.product_name AS ProductName,
                    COUNT(DISTINCT o.id)::int AS OrderCount,
                    COALESCE(SUM(i.quantity), 0) AS Qty,
                    COALESCE(SUM({lineRevenue}), 0) AS SalesAmount
                FROM sales_order_items i
                INNER JOIN public.sales_orders o ON o.id = i.sales_order_id
                INNER JOIN {PackPharmacyReadViews.Product} p ON p.id = i.product_id AND p.tenant_id = o.tenant_id
                WHERE o.tenant_id = @TenantId
                  AND {soldStatus}
                  AND o.order_date >= @FromUtc AND o.order_date < @ToUtc
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
                  AND r.status = @ReturnCompleted
                  AND r.return_date >= @FromUtc AND r.return_date < @ToUtc
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
                ReturnCompleted = SalesReturnStatuses.Completed,
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

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetSalesShiftCloseByEmployeeAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? warehouseId,
        Guid[]? allowedWarehouseIds,
        Guid? employeeId,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        var warehouseFilter = BuildWarehouseFilter("o.warehouse_id", warehouseId, allowedWarehouseIds);
        var employeeFilter = """
              AND (@EmployeeId IS NULL OR o.employee_id = @EmployeeId)
              """;
        var branchFilter = branchId.HasValue
            ? """
              AND (
                    o.branch_id = @BranchId
                 OR EXISTS (
                        SELECT 1
                        FROM warehouses bw
                        WHERE bw.id = o.warehouse_id
                          AND bw.tenant_id = @TenantId
                          AND bw.deleted_at IS NULL
                          AND bw.branch_id = @BranchId
                    )
              )
              """
            : "";
        var cash = SalesPaymentMethods.Cash;
        var transfer = SalesPaymentMethods.Transfer;
        var soldStatus = SalesAccrualSql.SoldStatusFilter();
        var originalTotal = SalesAccrualSql.OriginalTotalExpr();
        var isCheckout = SalesAccrualSql.IsCheckout();
        var isCollection = SalesAccrualSql.IsCollection();

        var sql = $"""
            WITH order_metrics AS (
                SELECT
                    o.employee_id AS EmployeeId,
                    o.warehouse_id AS WarehouseId,
                    o.sales_shift_id AS ShiftId,
                    CASE
                        WHEN o.sales_shift_id IS NULL
                        THEN date_trunc('day', timezone('Asia/Ho_Chi_Minh', o.order_date))
                        ELSE NULL
                    END AS LooseDay,
                    COUNT(*)::int AS OrderCount,
                    COALESCE(SUM({originalTotal}), 0) AS RevenueAmount,
                    COALESCE(SUM(COALESCE(ck.checkout_paid, 0)), 0) AS CheckoutPaid
                FROM public.sales_orders o
                LEFT JOIN LATERAL (
                    SELECT COALESCE(SUM(sp.amount), 0) AS checkout_paid
                    FROM sales_payments sp
                    WHERE sp.sales_order_id = o.id
                      AND {isCheckout}
                ) ck ON TRUE
                WHERE o.tenant_id = @TenantId
                  AND {soldStatus}
                  AND o.order_date >= @FromUtc AND o.order_date < @ToUtc
                  {warehouseFilter}
                  {employeeFilter}
                  {branchFilter}
                GROUP BY o.employee_id, o.warehouse_id, o.sales_shift_id,
                    CASE
                        WHEN o.sales_shift_id IS NULL
                        THEN date_trunc('day', timezone('Asia/Ho_Chi_Minh', o.order_date))
                        ELSE NULL
                    END
            ),
            sales AS (
                SELECT
                    x.EmployeeId,
                    x.WarehouseId,
                    x.ShiftId,
                    x.LooseDay,
                    COALESCE(SUM(x.amount), 0) AS SalesAmount,
                    COALESCE(SUM(x.amount) FILTER (WHERE x.is_collection), 0) AS CollectionAmount,
                    COALESCE(SUM(x.amount) FILTER (WHERE x.payment_method = {cash}), 0) AS CashSales,
                    COALESCE(SUM(x.amount) FILTER (WHERE x.payment_method = {transfer}), 0) AS TransferSales,
                    COALESCE(SUM(x.amount) FILTER (WHERE x.payment_method NOT IN ({cash}, {transfer})), 0) AS OtherSales
                FROM (
                    SELECT
                        o.employee_id AS EmployeeId,
                        o.warehouse_id AS WarehouseId,
                        CASE
                            WHEN {isCollection} THEN coll_shift.id
                            ELSE o.sales_shift_id
                        END AS ShiftId,
                        CASE
                            WHEN {isCollection} AND coll_shift.id IS NULL
                            THEN date_trunc('day', timezone('Asia/Ho_Chi_Minh', sp.paid_at))
                            WHEN NOT {isCollection} AND o.sales_shift_id IS NULL
                            THEN date_trunc('day', timezone('Asia/Ho_Chi_Minh', sp.paid_at))
                            ELSE NULL
                        END AS LooseDay,
                        sp.amount,
                        {isCollection} AS is_collection,
                        sp.payment_method
                    FROM public.sales_orders o
                    INNER JOIN sales_payments sp ON sp.sales_order_id = o.id
                    LEFT JOIN LATERAL (
                        SELECT sh.id
                        FROM sales_shifts sh
                        WHERE sh.tenant_id = o.tenant_id
                          AND sh.warehouse_id = o.warehouse_id
                          AND sp.paid_at >= sh.opened_at
                          AND sp.paid_at < COALESCE(sh.closed_at, TIMESTAMPTZ 'infinity')
                        ORDER BY sh.opened_at DESC
                        LIMIT 1
                    ) coll_shift ON {isCollection}
                    WHERE o.tenant_id = @TenantId
                      AND {soldStatus}
                      AND sp.paid_at >= @FromUtc AND sp.paid_at < @ToUtc
                      {warehouseFilter}
                      {employeeFilter}
                      {branchFilter}
                ) x
                GROUP BY x.EmployeeId, x.WarehouseId, x.ShiftId, x.LooseDay
            ),
            refunds AS (
                SELECT
                    o.employee_id AS EmployeeId,
                    o.warehouse_id AS WarehouseId,
                    r.sales_shift_id AS ShiftId,
                    CASE
                        WHEN r.sales_shift_id IS NULL
                        THEN date_trunc('day', timezone('Asia/Ho_Chi_Minh', rp.paid_at))
                        ELSE NULL
                    END AS LooseDay,
                    COALESCE(SUM(rp.amount), 0) AS RefundAmount,
                    COALESCE(SUM(rp.amount) FILTER (WHERE rp.payment_method = {cash}), 0) AS CashRefunds,
                    COALESCE(SUM(rp.amount) FILTER (WHERE rp.payment_method = {transfer}), 0) AS TransferRefunds,
                    COALESCE(SUM(rp.amount) FILTER (WHERE rp.payment_method NOT IN ({cash}, {transfer})), 0) AS OtherRefunds
                FROM sales_return_payments rp
                INNER JOIN sales_returns r ON r.id = rp.sales_return_id
                INNER JOIN public.sales_orders o ON o.id = r.sales_order_id
                WHERE r.tenant_id = @TenantId
                  AND rp.paid_at >= @FromUtc AND rp.paid_at < @ToUtc
                  {warehouseFilter}
                  {employeeFilter}
                  {branchFilter}
                GROUP BY o.employee_id, o.warehouse_id, r.sales_shift_id,
                    CASE
                        WHEN r.sales_shift_id IS NULL
                        THEN date_trunc('day', timezone('Asia/Ho_Chi_Minh', rp.paid_at))
                        ELSE NULL
                    END
            )
            SELECT
                COALESCE(om.EmployeeId, s.EmployeeId, r.EmployeeId) AS EmployeeId,
                COALESCE(e.full_name, 'Chưa gắn nhân viên') AS EmployeeName,
                w.branch_id AS BranchId,
                COALESCE(b.branch_name, '—') AS BranchName,
                COALESCE(om.WarehouseId, s.WarehouseId, r.WarehouseId) AS WarehouseId,
                COALESCE(w.warehouse_name, '—') AS WarehouseName,
                COALESCE(om.ShiftId, s.ShiftId, r.ShiftId) AS ShiftId,
                COALESCE(sh.shift_number, 'Ngoài ca') AS ShiftNumber,
                COALESCE(om.LooseDay, s.LooseDay, r.LooseDay) AS LooseDay,
                COALESCE(sh.opened_at, (COALESCE(om.LooseDay, s.LooseDay, r.LooseDay) AT TIME ZONE 'Asia/Ho_Chi_Minh')) AS OpenedAt,
                sh.closed_at AS ClosedAt,
                sh.status AS ShiftStatus,
                COALESCE(om.OrderCount, 0) AS OrderCount,
                COALESCE(om.RevenueAmount, 0) AS RevenueAmount,
                COALESCE(om.CheckoutPaid, 0) AS CheckoutPaid,
                GREATEST(0, COALESCE(om.RevenueAmount, 0) - COALESCE(om.CheckoutPaid, 0)) AS NewDebt,
                COALESCE(s.CollectionAmount, 0) AS CollectionAmount,
                COALESCE(s.SalesAmount, 0) AS SalesAmount,
                COALESCE(r.RefundAmount, 0) AS RefundAmount,
                COALESCE(s.CashSales, 0) - COALESCE(r.CashRefunds, 0) AS CashNet,
                COALESCE(s.TransferSales, 0) - COALESCE(r.TransferRefunds, 0) AS TransferNet,
                COALESCE(s.OtherSales, 0) - COALESCE(r.OtherRefunds, 0) AS OtherNet,
                COALESCE(s.SalesAmount, 0) - COALESCE(r.RefundAmount, 0) AS NetAmount
            FROM order_metrics om
            FULL OUTER JOIN sales s
                ON COALESCE(s.EmployeeId, '00000000-0000-0000-0000-000000000000')
                 = COALESCE(om.EmployeeId, '00000000-0000-0000-0000-000000000000')
               AND s.WarehouseId = om.WarehouseId
               AND COALESCE(s.ShiftId, '00000000-0000-0000-0000-000000000000')
                 = COALESCE(om.ShiftId, '00000000-0000-0000-0000-000000000000')
               AND COALESCE(s.LooseDay, TIMESTAMP '1970-01-01')
                 = COALESCE(om.LooseDay, TIMESTAMP '1970-01-01')
            FULL OUTER JOIN refunds r
                ON COALESCE(r.EmployeeId, '00000000-0000-0000-0000-000000000000')
                 = COALESCE(om.EmployeeId, s.EmployeeId, '00000000-0000-0000-0000-000000000000')
               AND r.WarehouseId = COALESCE(om.WarehouseId, s.WarehouseId)
               AND COALESCE(r.ShiftId, '00000000-0000-0000-0000-000000000000')
                 = COALESCE(om.ShiftId, s.ShiftId, '00000000-0000-0000-0000-000000000000')
               AND COALESCE(r.LooseDay, TIMESTAMP '1970-01-01')
                 = COALESCE(om.LooseDay, s.LooseDay, TIMESTAMP '1970-01-01')
            LEFT JOIN employees e ON e.id = COALESCE(om.EmployeeId, s.EmployeeId, r.EmployeeId)
            LEFT JOIN {PackPharmacyReadViews.Warehouse} w ON w.id = COALESCE(om.WarehouseId, s.WarehouseId, r.WarehouseId)
            LEFT JOIN branches b ON b.id = w.branch_id AND b.deleted_at IS NULL
            LEFT JOIN sales_shifts sh ON sh.id = COALESCE(om.ShiftId, s.ShiftId, r.ShiftId)
            ORDER BY OpenedAt DESC NULLS LAST, EmployeeName
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<ShiftCloseByEmployeeRow>(
            sql,
            new
            {
                TenantId,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                WarehouseId = warehouseId,
                AllowedWarehouseIds = allowedWarehouseIds,
                EmployeeId = employeeId,
                BranchId = branchId,
                OrderCompleted = SalesOrderStatuses.Completed,
            });

        return rows.Select(r => new Dictionary<string, object?>
        {
            ["employeeId"] = r.EmployeeId,
            ["employeeName"] = r.EmployeeName,
            ["branchId"] = r.BranchId,
            ["branchName"] = r.BranchName,
            ["warehouseId"] = r.WarehouseId,
            ["warehouseName"] = r.WarehouseName,
            ["shiftId"] = r.ShiftId,
            ["shiftNumber"] = r.ShiftNumber,
            ["openedAt"] = r.OpenedAt,
            ["closedAt"] = r.ClosedAt,
            ["status"] = r.ShiftStatus,
            ["statusLabel"] = r.ShiftId is null ? "Ngoài ca" : ShiftStatusLabel(r.ShiftStatus ?? 0),
            ["orderCount"] = r.OrderCount,
            ["revenueAmount"] = r.RevenueAmount,
            ["checkoutPaid"] = r.CheckoutPaid,
            ["newDebt"] = r.NewDebt,
            ["collectionAmount"] = r.CollectionAmount,
            ["salesAmount"] = r.SalesAmount,
            ["refundAmount"] = r.RefundAmount,
            ["cashNet"] = r.CashNet,
            ["transferNet"] = r.TransferNet,
            ["otherNet"] = r.OtherNet,
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
        var looseWarehouseFilter = BuildWarehouseFilter("o.warehouse_id", warehouseId, allowedWarehouseIds);
        var soldStatus = SalesAccrualSql.SoldStatusFilter();
        var originalTotal = SalesAccrualSql.OriginalTotalExpr();
        var isCheckout = SalesAccrualSql.IsCheckout();
        var isCollection = SalesAccrualSql.IsCollection();
        var isCollectionOx = SalesAccrualSql.IsCollection("sp", "ox");

        var sql = $"""
            SELECT * FROM (
            SELECT
                sh.id AS ShiftId,
                sh.shift_number AS ShiftNumber,
                w.warehouse_name AS WarehouseName,
                sh.opened_at AS OpenedAt,
                sh.closed_at AS ClosedAt,
                sh.opening_cash AS OpeningCash,
                sh.closing_cash AS ClosingCash,
                sh.cash_variance AS CashVariance,
                sh.status AS Status,
                COALESCE((
                    SELECT SUM({originalTotal})
                    FROM public.sales_orders o
                    WHERE o.tenant_id = @TenantId
                      AND o.warehouse_id = sh.warehouse_id
                      AND o.sales_shift_id = sh.id
                      AND {soldStatus}
                ), 0) AS RevenueAmount,
                COALESCE((
                    SELECT SUM({originalTotal}) - SUM(COALESCE(ck.checkout_paid, 0))
                    FROM public.sales_orders o
                    LEFT JOIN LATERAL (
                        SELECT COALESCE(SUM(sp.amount), 0) AS checkout_paid
                        FROM sales_payments sp
                        WHERE sp.sales_order_id = o.id AND {isCheckout}
                    ) ck ON TRUE
                    WHERE o.tenant_id = @TenantId
                      AND o.warehouse_id = sh.warehouse_id
                      AND o.sales_shift_id = sh.id
                      AND {soldStatus}
                ), 0) AS NewDebt,
                COALESCE((
                    SELECT SUM(sp.amount)
                    FROM sales_payments sp
                    INNER JOIN public.sales_orders o ON o.id = sp.sales_order_id
                    WHERE o.tenant_id = @TenantId
                      AND o.warehouse_id = sh.warehouse_id
                      AND {isCollection}
                      AND sp.paid_at >= sh.opened_at
                      AND sp.paid_at < COALESCE(sh.closed_at, @ToUtc)
                ), 0) AS CollectionAmount,
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
              AND (
                    (sh.opened_at >= @FromUtc AND sh.opened_at < @ToUtc)
                    OR EXISTS (
                        SELECT 1
                        FROM public.sales_orders o
                        WHERE o.sales_shift_id = sh.id
                          AND o.tenant_id = @TenantId
                          AND {soldStatus}
                          AND o.order_date >= @FromUtc AND o.order_date < @ToUtc
                    )
                    OR EXISTS (
                        SELECT 1
                        FROM sales_payments sp
                        INNER JOIN public.sales_orders o ON o.id = sp.sales_order_id
                        WHERE o.tenant_id = @TenantId
                          AND o.warehouse_id = sh.warehouse_id
                          AND {isCollection}
                          AND sp.paid_at >= GREATEST(sh.opened_at, @FromUtc)
                          AND sp.paid_at < LEAST(COALESCE(sh.closed_at, @ToUtc), @ToUtc)
                    )
              )
              {warehouseFilter}

            UNION ALL

            SELECT
                NULL::uuid AS ShiftId,
                'Ngoài ca' AS ShiftNumber,
                w.warehouse_name AS WarehouseName,
                MIN(o.order_date) AS OpenedAt,
                NULL::timestamptz AS ClosedAt,
                0::numeric AS OpeningCash,
                NULL::numeric AS ClosingCash,
                NULL::numeric AS CashVariance,
                NULL::smallint AS Status,
                COALESCE(SUM({originalTotal}), 0) AS RevenueAmount,
                COALESCE(SUM({originalTotal} - COALESCE(ck.checkout_paid, 0)), 0) AS NewDebt,
                COALESCE((
                    SELECT SUM(sp.amount)
                    FROM sales_payments sp
                    INNER JOIN public.sales_orders ox ON ox.id = sp.sales_order_id
                    WHERE ox.tenant_id = @TenantId
                      AND ox.warehouse_id = w.id
                      AND ox.sales_shift_id IS NULL
                      AND {isCollectionOx}
                      AND sp.paid_at >= @FromUtc AND sp.paid_at < @ToUtc
                ), 0) AS CollectionAmount,
                COALESCE((
                    SELECT SUM(sp.amount)
                    FROM sales_payments sp
                    INNER JOIN public.sales_orders ox ON ox.id = sp.sales_order_id
                    WHERE ox.tenant_id = @TenantId
                      AND ox.warehouse_id = w.id
                      AND ox.sales_shift_id IS NULL
                      AND sp.paid_at >= @FromUtc AND sp.paid_at < @ToUtc
                ), 0)
                - COALESCE((
                    SELECT SUM(rp.amount)
                    FROM sales_return_payments rp
                    INNER JOIN sales_returns r ON r.id = rp.sales_return_id
                    INNER JOIN public.sales_orders ox ON ox.id = r.sales_order_id
                    WHERE r.tenant_id = @TenantId
                      AND ox.warehouse_id = w.id
                      AND ox.sales_shift_id IS NULL
                      AND rp.paid_at >= @FromUtc AND rp.paid_at < @ToUtc
                ), 0) AS NetAmount
            FROM public.sales_orders o
            INNER JOIN {PackPharmacyReadViews.Warehouse} w ON w.id = o.warehouse_id
            LEFT JOIN LATERAL (
                SELECT COALESCE(SUM(sp.amount), 0) AS checkout_paid
                FROM sales_payments sp
                WHERE sp.sales_order_id = o.id AND {isCheckout}
            ) ck ON TRUE
            WHERE o.tenant_id = @TenantId
              AND o.sales_shift_id IS NULL
              AND {soldStatus}
              AND o.order_date >= @FromUtc AND o.order_date < @ToUtc
              {looseWarehouseFilter}
            GROUP BY w.id, w.warehouse_name
            ) q
            ORDER BY OpenedAt DESC
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<ShiftReportRow>(
            sql,
            new { TenantId, FromUtc = fromUtc, ToUtc = toUtc, WarehouseId = warehouseId, AllowedWarehouseIds = allowedWarehouseIds });

        return rows.Select(r => new Dictionary<string, object?>
        {
            ["shiftId"] = r.ShiftId,
            ["shiftNumber"] = r.ShiftNumber,
            ["warehouseName"] = r.WarehouseName,
            ["openedAt"] = r.OpenedAt,
            ["closedAt"] = r.ClosedAt,
            ["openingCash"] = r.OpeningCash,
            ["closingCash"] = r.ClosingCash,
            ["cashVariance"] = r.CashVariance,
            ["status"] = r.Status,
            ["statusLabel"] = r.ShiftId is null ? "Ngoài ca" : ShiftStatusLabel(r.Status ?? 0),
            ["revenueAmount"] = r.RevenueAmount,
            ["newDebt"] = r.NewDebt,
            ["collectionAmount"] = r.CollectionAmount,
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
        var soldStatus = SalesAccrualSql.SoldStatusFilter();
        var originalTotal = SalesAccrualSql.OriginalTotalExpr();

        var sql = $"""
            WITH sales AS (
                SELECT
                    o.customer_id AS CustomerId,
                    COUNT(*)::int AS OrderCount,
                    COALESCE(SUM({originalTotal}), 0) AS SalesAmount,
                    MAX(o.order_date) AS LastOrderAt,
                    MIN(o.order_date) AS FirstInPeriodAt
                FROM public.sales_orders o
                WHERE o.tenant_id = @TenantId
                  AND {soldStatus}
                  AND o.customer_id IS NOT NULL
                  AND o.order_date >= @FromUtc AND o.order_date < @ToUtc
                  {warehouseFilter}
                GROUP BY o.customer_id
            ),
            refunds AS (
                SELECT
                    o.customer_id AS CustomerId,
                    COALESCE(SUM(ri.refund_amount), 0) AS RefundAmount
                FROM sales_return_items ri
                INNER JOIN sales_returns r ON r.id = ri.sales_return_id
                INNER JOIN public.sales_orders o ON o.id = r.sales_order_id
                WHERE r.tenant_id = @TenantId
                  AND r.status = @ReturnCompleted
                  AND o.customer_id IS NOT NULL
                  AND r.return_date >= @FromUtc AND r.return_date < @ToUtc
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
                ReturnCompleted = SalesReturnStatuses.Completed,
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
                  AND {SalesAccrualSql.SoldStatusFilter()}
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

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetSalesReceivablesMovementAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? warehouseId,
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        var warehouseFilter = BuildWarehouseFilter("o.warehouse_id", warehouseId, allowedWarehouseIds);
        var soldStatus = SalesAccrualSql.SoldStatusFilter();
        var originalTotal = SalesAccrualSql.OriginalTotalExpr();
        var isCheckout = SalesAccrualSql.IsCheckout();
        var isCollectionOnSold = SalesAccrualSql.IsCollection("sp", "s");
        var agingDays = SalesAccrualSql.AgingDaysExpr("s.order_date");

        var sql = $"""
            WITH sold AS (
                SELECT
                    o.id,
                    o.customer_id,
                    o.order_date,
                    o.outstanding,
                    {originalTotal} AS original_total,
                    COALESCE(ck.checkout_paid, 0) AS checkout_paid
                FROM public.sales_orders o
                LEFT JOIN LATERAL (
                    SELECT COALESCE(SUM(sp.amount), 0) AS checkout_paid
                    FROM sales_payments sp
                    WHERE sp.sales_order_id = o.id AND {isCheckout}
                ) ck ON TRUE
                WHERE o.tenant_id = @TenantId
                  AND {soldStatus}
                  AND o.customer_id IS NOT NULL
                  {warehouseFilter}
            ),
            coll AS (
                SELECT
                    s.customer_id,
                    sp.amount,
                    sp.paid_at
                FROM sales_payments sp
                INNER JOIN sold s ON s.id = sp.sales_order_id
                WHERE {isCollectionOnSold}
            ),
            ret AS (
                SELECT
                    s.customer_id,
                    r.return_date,
                    GREATEST(0,
                        COALESCE((
                            SELECT SUM(ri.refund_amount)
                            FROM sales_return_items ri
                            WHERE ri.sales_return_id = r.id
                        ), 0)
                        - COALESCE((
                            SELECT SUM(rp.amount)
                            FROM sales_return_payments rp
                            WHERE rp.sales_return_id = r.id
                        ), 0)
                    ) AS debt_reduced
                FROM sales_returns r
                INNER JOIN sold s ON s.id = r.sales_order_id
                WHERE r.status = @ReturnCompleted
            ),
            order_agg AS (
                SELECT
                    s.customer_id,
                    COALESCE(SUM(s.original_total - s.checkout_paid) FILTER (WHERE s.order_date < @FromUtc), 0) AS DebtBefore,
                    COALESCE(SUM(s.original_total - s.checkout_paid) FILTER (
                        WHERE s.order_date >= @FromUtc AND s.order_date < @ToUtc
                    ), 0) AS CreditSales,
                    COALESCE(SUM(s.outstanding) FILTER (WHERE s.outstanding > 0.009), 0) AS CurrentOutstanding,
                    COALESCE(SUM(s.outstanding) FILTER (
                        WHERE s.outstanding > 0.009
                          AND {agingDays} <= 30
                    ), 0) AS AgingCurrent,
                    COALESCE(SUM(s.outstanding) FILTER (
                        WHERE s.outstanding > 0.009
                          AND {agingDays} BETWEEN 31 AND 60
                    ), 0) AS Aging31To60,
                    COALESCE(SUM(s.outstanding) FILTER (
                        WHERE s.outstanding > 0.009
                          AND {agingDays} BETWEEN 61 AND 90
                    ), 0) AS Aging61To90,
                    COALESCE(SUM(s.outstanding) FILTER (
                        WHERE s.outstanding > 0.009
                          AND {agingDays} > 90
                    ), 0) AS AgingOver90,
                    COUNT(*) FILTER (WHERE s.outstanding > 0.009)::int AS OpenDocuments
                FROM sold s
                GROUP BY s.customer_id
            ),
            coll_agg AS (
                SELECT
                    customer_id,
                    COALESCE(SUM(amount) FILTER (WHERE paid_at < @FromUtc), 0) AS Before,
                    COALESCE(SUM(amount) FILTER (WHERE paid_at >= @FromUtc AND paid_at < @ToUtc), 0) AS InPeriod
                FROM coll
                GROUP BY customer_id
            ),
            ret_agg AS (
                SELECT
                    customer_id,
                    COALESCE(SUM(debt_reduced) FILTER (WHERE return_date < @FromUtc), 0) AS Before,
                    COALESCE(SUM(debt_reduced) FILTER (WHERE return_date >= @FromUtc AND return_date < @ToUtc), 0) AS InPeriod
                FROM ret
                GROUP BY customer_id
            )
            SELECT
                oa.customer_id AS CustomerId,
                COALESCE(c.customer_code, '') AS CustomerCode,
                COALESCE(c.full_name, 'Khách đã xóa') AS CustomerName,
                GREATEST(0, oa.DebtBefore - COALESCE(ca.Before, 0) - COALESCE(ra.Before, 0)) AS Opening,
                oa.CreditSales,
                COALESCE(ca.InPeriod, 0) AS Collections,
                COALESCE(ra.InPeriod, 0) AS ReturnAgainstDebt,
                GREATEST(0,
                    oa.DebtBefore - COALESCE(ca.Before, 0) - COALESCE(ra.Before, 0)
                    + oa.CreditSales
                    - COALESCE(ca.InPeriod, 0)
                    - COALESCE(ra.InPeriod, 0)
                ) AS Closing,
                oa.CurrentOutstanding,
                oa.AgingCurrent,
                oa.Aging31To60,
                oa.Aging61To90,
                oa.AgingOver90,
                oa.OpenDocuments
            FROM order_agg oa
            LEFT JOIN coll_agg ca ON ca.customer_id = oa.customer_id
            LEFT JOIN ret_agg ra ON ra.customer_id = oa.customer_id
            LEFT JOIN customers c ON c.id = oa.customer_id
            WHERE GREATEST(0, oa.DebtBefore - COALESCE(ca.Before, 0) - COALESCE(ra.Before, 0)) > 0.009
               OR oa.CreditSales > 0.009
               OR COALESCE(ca.InPeriod, 0) > 0.009
               OR COALESCE(ra.InPeriod, 0) > 0.009
               OR oa.CurrentOutstanding > 0.009
            ORDER BY Closing DESC, CustomerName
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<ReceivablesMovementRow>(
            sql,
            new
            {
                TenantId,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                WarehouseId = warehouseId,
                AllowedWarehouseIds = allowedWarehouseIds,
                ReturnCompleted = SalesReturnStatuses.Completed,
            });

        return rows.Select(r => new Dictionary<string, object?>
        {
            ["customerId"] = r.CustomerId,
            ["customerCode"] = r.CustomerCode,
            ["customerName"] = r.CustomerName,
            ["opening"] = r.Opening,
            ["creditSales"] = r.CreditSales,
            ["collections"] = r.Collections,
            ["returnAgainstDebt"] = r.ReturnAgainstDebt,
            ["closing"] = r.Closing,
            ["currentOutstanding"] = r.CurrentOutstanding,
            ["agingCurrent"] = r.AgingCurrent,
            ["aging31To60"] = r.Aging31To60,
            ["aging61To90"] = r.Aging61To90,
            ["agingOver90"] = r.AgingOver90,
            ["openDocuments"] = r.OpenDocuments,
        }).ToList();
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
        SalesPaymentMethods.Credit => "Ghi nợ",
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
        public int OrderCount { get; init; }
        public decimal SalesAmount { get; init; }
        public decimal CheckoutPaid { get; init; }
        public decimal NewDebt { get; init; }
        public decimal CollectionAmount { get; init; }
        public decimal CashAmount { get; init; }
        public decimal CardAmount { get; init; }
        public decimal TransferAmount { get; init; }
        public decimal EwalletAmount { get; init; }
        public decimal RefundAmount { get; init; }
        public decimal RefundCash { get; init; }
        public decimal RefundDebt { get; init; }
        public decimal NetAmount { get; init; }
    }

    private sealed class PaymentMethodRow
    {
        public short PaymentMethod { get; init; }
        public decimal CheckoutAmount { get; init; }
        public decimal CollectionAmount { get; init; }
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

    private sealed class ShiftCloseByEmployeeRow
    {
        public Guid? EmployeeId { get; init; }
        public string EmployeeName { get; init; } = "";
        public Guid? BranchId { get; init; }
        public string BranchName { get; init; } = "";
        public Guid WarehouseId { get; init; }
        public string WarehouseName { get; init; } = "";
        public Guid? ShiftId { get; init; }
        public string ShiftNumber { get; init; } = "";
        public DateTime? LooseDay { get; init; }
        public DateTime? OpenedAt { get; init; }
        public DateTime? ClosedAt { get; init; }
        public short? ShiftStatus { get; init; }
        public int OrderCount { get; init; }
        public decimal RevenueAmount { get; init; }
        public decimal CheckoutPaid { get; init; }
        public decimal NewDebt { get; init; }
        public decimal CollectionAmount { get; init; }
        public decimal SalesAmount { get; init; }
        public decimal RefundAmount { get; init; }
        public decimal CashNet { get; init; }
        public decimal TransferNet { get; init; }
        public decimal OtherNet { get; init; }
        public decimal NetAmount { get; init; }
    }

    private sealed class ShiftReportRow
    {
        public Guid? ShiftId { get; init; }
        public string ShiftNumber { get; init; } = "";
        public string WarehouseName { get; init; } = "";
        public DateTime OpenedAt { get; init; }
        public DateTime? ClosedAt { get; init; }
        public decimal OpeningCash { get; init; }
        public decimal? ClosingCash { get; init; }
        public decimal? CashVariance { get; init; }
        public short? Status { get; init; }
        public decimal RevenueAmount { get; init; }
        public decimal NewDebt { get; init; }
        public decimal CollectionAmount { get; init; }
        public decimal NetAmount { get; init; }
    }

    private sealed class ReceivablesMovementRow
    {
        public Guid CustomerId { get; init; }
        public string CustomerCode { get; init; } = "";
        public string CustomerName { get; init; } = "";
        public decimal Opening { get; init; }
        public decimal CreditSales { get; init; }
        public decimal Collections { get; init; }
        public decimal ReturnAgainstDebt { get; init; }
        public decimal Closing { get; init; }
        public decimal CurrentOutstanding { get; init; }
        public decimal AgingCurrent { get; init; }
        public decimal Aging31To60 { get; init; }
        public decimal Aging61To90 { get; init; }
        public decimal AgingOver90 { get; init; }
        public int OpenDocuments { get; init; }
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
