using Dapper;
using PharmaCore.Application.CustomerApp;
using PharmaCore.Application.Sales;
using PharmaCore.Infrastructure.Data;

namespace PharmaCore.Infrastructure.CustomerApp;

internal sealed class CustomerPurchaseRepository
{
    private readonly IDbConnectionFactory _db;

    public CustomerPurchaseRepository(IDbConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<CustomerPurchaseListItemDto>> ListAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                o.id AS Id,
                o.order_number AS OrderNumber,
                o.status AS Status,
                o.order_date AS OrderDate,
                o.total_amount AS TotalAmount,
                (SELECT COUNT(*)::int FROM sales_order_items i WHERE i.sales_order_id = o.id) AS ItemCount,
                COALESCE((
                    SELECT SUM(ri.refund_amount)
                    FROM sales_returns sr
                    INNER JOIN sales_return_items ri ON ri.sales_return_id = sr.id
                    WHERE sr.sales_order_id = o.id AND sr.status = @ReturnCompleted
                ), 0) AS TotalRefunded
            FROM sales_orders o
            WHERE o.tenant_id = @TenantId
              AND o.customer_id = @CustomerId
              AND o.status IN (@Completed, @Refunded)
            ORDER BY o.order_date DESC
            LIMIT 100
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<ListRow>(sql, new
        {
            TenantId = tenantId,
            CustomerId = customerId,
            Completed = SalesOrderStatuses.Completed,
            Refunded = SalesOrderStatuses.Refunded,
            ReturnCompleted = SalesReturnStatuses.Completed,
        });

        return rows.Select(row => new CustomerPurchaseListItemDto(
            row.Id,
            row.OrderNumber,
            row.Status,
            ToOffset(row.OrderDate),
            row.TotalAmount,
            row.ItemCount,
            row.TotalRefunded)).ToList();
    }

    public async Task<CustomerPurchaseDetailDto?> GetAsync(
        Guid tenantId,
        Guid customerId,
        Guid salesOrderId,
        CancellationToken cancellationToken)
    {
        const string headerSql = """
            SELECT
                o.id AS Id,
                o.order_number AS OrderNumber,
                o.status AS Status,
                o.order_date AS OrderDate,
                o.subtotal AS Subtotal,
                o.discount_amount AS DiscountAmount,
                o.total_amount AS TotalAmount,
                o.notes AS Notes,
                COALESCE((
                    SELECT SUM(ri.refund_amount)
                    FROM sales_returns sr
                    INNER JOIN sales_return_items ri ON ri.sales_return_id = sr.id
                    WHERE sr.sales_order_id = o.id AND sr.status = @ReturnCompleted
                ), 0) AS TotalRefunded,
                (
                    SELECT lt.points
                    FROM loyalty_transactions lt
                    WHERE lt.sales_order_id = o.id AND lt.transaction_type = 1
                    LIMIT 1
                ) AS LoyaltyPointsEarned,
                o.loyalty_points_redeemed AS LoyaltyPointsRedeemed,
                o.loyalty_discount_amount AS LoyaltyDiscountAmount,
                o.voucher_discount_amount AS VoucherDiscountAmount,
                v.voucher_code AS VoucherCode
            FROM sales_orders o
            LEFT JOIN vouchers v ON v.id = o.voucher_id
            WHERE o.id = @Id
              AND o.tenant_id = @TenantId
              AND o.customer_id = @CustomerId
              AND o.status IN (@Completed, @Refunded)
            """;

        const string itemsSql = """
            SELECT
                i.id AS Id,
                p.product_name AS ProductName,
                u.unit_name AS UnitName,
                i.quantity AS Quantity,
                i.unit_price AS UnitPrice,
                i.line_total AS LineTotal,
                COALESCE((
                    SELECT SUM(ri.quantity)
                    FROM sales_return_items ri
                    INNER JOIN sales_returns r ON r.id = ri.sales_return_id
                    WHERE ri.sales_order_item_id = i.id AND r.status = @ReturnCompleted
                ), 0) AS ReturnedQuantity
            FROM sales_order_items i
            INNER JOIN products p ON p.id = i.product_id
            INNER JOIN product_units u ON u.id = i.product_unit_id
            WHERE i.sales_order_id = @OrderId
            ORDER BY p.product_name
            """;

        const string paymentsSql = """
            SELECT payment_method AS PaymentMethod, amount AS Amount
            FROM sales_payments
            WHERE sales_order_id = @OrderId
            ORDER BY paid_at
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var header = await conn.QuerySingleOrDefaultAsync<DetailHeaderRow>(headerSql, new
        {
            Id = salesOrderId,
            TenantId = tenantId,
            CustomerId = customerId,
            Completed = SalesOrderStatuses.Completed,
            Refunded = SalesOrderStatuses.Refunded,
            ReturnCompleted = SalesReturnStatuses.Completed,
        });
        if (header is null)
            return null;

        var items = (await conn.QueryAsync<LineRow>(itemsSql, new
        {
            OrderId = salesOrderId,
            ReturnCompleted = SalesReturnStatuses.Completed,
        })).Select(row => new CustomerPurchaseLineDto(
            row.Id,
            row.ProductName,
            row.UnitName,
            row.Quantity,
            row.UnitPrice,
            row.LineTotal,
            row.ReturnedQuantity)).ToList();

        var payments = (await conn.QueryAsync<PaymentRow>(paymentsSql, new { OrderId = salesOrderId }))
            .Select(row => new CustomerPurchasePaymentDto(row.PaymentMethod, row.Amount))
            .ToList();

        return new CustomerPurchaseDetailDto(
            header.Id,
            header.OrderNumber,
            header.Status,
            ToOffset(header.OrderDate),
            header.Subtotal,
            header.DiscountAmount,
            header.TotalAmount,
            header.TotalRefunded,
            header.Notes,
            header.LoyaltyPointsEarned,
            header.LoyaltyPointsRedeemed,
            header.LoyaltyDiscountAmount,
            header.VoucherDiscountAmount,
            header.VoucherCode,
            items,
            payments);
    }

    private static DateTimeOffset ToOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed record ListRow
    {
        public Guid Id { get; init; }
        public string OrderNumber { get; init; } = "";
        public short Status { get; init; }
        public DateTime OrderDate { get; init; }
        public decimal TotalAmount { get; init; }
        public int ItemCount { get; init; }
        public decimal TotalRefunded { get; init; }
    }

    private sealed record DetailHeaderRow
    {
        public Guid Id { get; init; }
        public string OrderNumber { get; init; } = "";
        public short Status { get; init; }
        public DateTime OrderDate { get; init; }
        public decimal Subtotal { get; init; }
        public decimal DiscountAmount { get; init; }
        public decimal TotalAmount { get; init; }
        public decimal TotalRefunded { get; init; }
        public string? Notes { get; init; }
        public int? LoyaltyPointsEarned { get; init; }
        public decimal LoyaltyPointsRedeemed { get; init; }
        public decimal LoyaltyDiscountAmount { get; init; }
        public decimal VoucherDiscountAmount { get; init; }
        public string? VoucherCode { get; init; }
    }

    private sealed record LineRow
    {
        public Guid Id { get; init; }
        public string ProductName { get; init; } = "";
        public string UnitName { get; init; } = "";
        public decimal Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal LineTotal { get; init; }
        public decimal ReturnedQuantity { get; init; }
    }

    private sealed record PaymentRow
    {
        public short PaymentMethod { get; init; }
        public decimal Amount { get; init; }
    }
}
