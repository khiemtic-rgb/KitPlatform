using System.Data;
using Dapper;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.Procurement;
using PharmaCore.Application.Reports;
using PharmaCore.Application.Sales;
using PharmaCore.Infrastructure.Data;

namespace PharmaCore.Infrastructure.Reports;

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
                INNER JOIN sales_orders o ON o.id = sp.sales_order_id
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
                INNER JOIN sales_orders o ON o.id = r.sales_order_id
                WHERE r.tenant_id = @TenantId
                  AND rp.paid_at >= @FromUtc AND rp.paid_at < @ToUtc
                  {warehouseFilter}
                GROUP BY 1
            ),
            orders AS (
                SELECT
                    date_trunc('{trunc}', timezone('Asia/Ho_Chi_Minh', o.order_date)) AS period_start,
                    COUNT(*)::int AS order_count
                FROM sales_orders o
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

        const string sql = """
            WITH sales AS (
                SELECT sp.payment_method AS PaymentMethod, COALESCE(SUM(sp.amount), 0) AS Amount
                FROM sales_payments sp
                INNER JOIN sales_orders o ON o.id = sp.sales_order_id
                WHERE o.tenant_id = @TenantId
                  AND sp.paid_at >= @FromUtc AND sp.paid_at < @ToUtc
                  /**warehouse**/
                GROUP BY sp.payment_method
            ),
            refunds AS (
                SELECT rp.payment_method AS PaymentMethod, COALESCE(SUM(rp.amount), 0) AS Amount
                FROM sales_return_payments rp
                INNER JOIN sales_returns r ON r.id = rp.sales_return_id
                INNER JOIN sales_orders o ON o.id = r.sales_order_id
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
                INNER JOIN sales_orders o ON o.id = i.sales_order_id
                INNER JOIN products p ON p.id = i.product_id AND p.tenant_id = o.tenant_id
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
                INNER JOIN sales_orders o ON o.id = r.sales_order_id
                INNER JOIN products p ON p.id = i.product_id AND p.tenant_id = o.tenant_id
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
                    INNER JOIN sales_orders o ON o.id = sp.sales_order_id
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
            INNER JOIN warehouses w ON w.id = sh.warehouse_id
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
                FROM goods_receipts gr
                INNER JOIN goods_receipt_items gi ON gi.goods_receipt_id = gr.id
                INNER JOIN suppliers s ON s.id = gr.supplier_id
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

        var trunc = groupBy == ReportGroupBy.Month ? "month" : "day";
        var periodSql = $"""
            SELECT
                date_trunc('{trunc}', timezone('Asia/Ho_Chi_Minh', gr.receipt_date)) AS PeriodStart,
                COUNT(DISTINCT gr.id)::int AS GrnCount,
                COALESCE(SUM(gi.quantity), 0) AS TotalQty,
                COALESCE(SUM(gi.line_total), 0) AS PreTaxAmount
            FROM goods_receipts gr
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

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetInventoryStockSnapshotAsync(
        Guid? warehouseId,
        Guid[]? allowedWarehouseIds,
        string? search,
        CancellationToken cancellationToken)
    {
        var extra = new List<string> { "b.quantity_available > 0" };
        if (warehouseId.HasValue)
            extra.Add("b.warehouse_id = @WarehouseId");
        else if (allowedWarehouseIds is { Length: > 0 })
            extra.Add("b.warehouse_id = ANY(@AllowedWarehouseIds)");
        if (!string.IsNullOrWhiteSpace(search))
            extra.Add("(p.product_code ILIKE @Search OR p.product_name ILIKE @Search)");

        var sql = $"""
            SELECT
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                w.warehouse_name AS WarehouseName,
                SUM(b.quantity_available) AS TotalQty,
                SUM(b.quantity_available * b.unit_cost) AS StockValue
            FROM inventory_batches b
            INNER JOIN products p ON p.id = b.product_id AND p.tenant_id = b.tenant_id
            INNER JOIN warehouses w ON w.id = b.warehouse_id
            WHERE b.tenant_id = @TenantId
              AND p.deleted_at IS NULL
              AND {string.Join(" AND ", extra)}
            GROUP BY p.id, p.product_code, p.product_name, w.id, w.warehouse_name
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
                Search = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%",
            });

        return rows.Select(r => new Dictionary<string, object?>
        {
            ["productCode"] = r.ProductCode,
            ["productName"] = r.ProductName,
            ["warehouseName"] = r.WarehouseName,
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
            FROM inventory_batches b
            INNER JOIN products p ON p.id = b.product_id AND p.tenant_id = b.tenant_id
            INNER JOIN warehouses w ON w.id = b.warehouse_id
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

    private sealed class StockSnapshotRow
    {
        public string ProductCode { get; init; } = "";
        public string ProductName { get; init; } = "";
        public string WarehouseName { get; init; } = "";
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
}
