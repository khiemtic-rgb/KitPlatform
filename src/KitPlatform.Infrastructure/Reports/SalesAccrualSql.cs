using KitPlatform.Packs.Pharmacy.Sales;

namespace KitPlatform.Infrastructure.Reports;

/// <summary>
/// Accrual sales fragments: revenue on sale date, cash on paid_at.
/// Collection = later than 15 minutes after order_date, or a posted customer_payments row
/// matched to the same sales_payments timestamp (AR voucher, even if posted immediately).
/// Checkout = every other payment on the order.
/// Original order total = current total_amount + completed return refunds (returns mutate the header).
/// </summary>
internal static class SalesAccrualSql
{
    public const string CheckoutWindow = "interval '15 minutes'";
    public const string CollectionMatchSeconds = "120";

    public static string SoldStatusFilter(string orderAlias = "o") =>
        $"{orderAlias}.status IN ({SalesOrderStatuses.Completed}, {SalesOrderStatuses.Refunded})";

    public static string OriginalTotalExpr(string orderAlias = "o") =>
        $"""
        ({orderAlias}.total_amount + COALESCE((
            SELECT SUM(ri.refund_amount)
            FROM sales_return_items ri
            INNER JOIN sales_returns r ON r.id = ri.sales_return_id
            WHERE r.sales_order_id = {orderAlias}.id
              AND r.status = {SalesReturnStatuses.Completed}
        ), 0))
        """;

    public static string AllocatedLineRevenue(string itemAlias = "i", string orderAlias = "o") =>
        $"""
        ({itemAlias}.line_total * {OriginalTotalExpr(orderAlias)}
            / NULLIF((
                SELECT SUM(x.line_total)
                FROM sales_order_items x
                WHERE x.sales_order_id = {orderAlias}.id
            ), 0))
        """;

    public static string IsCollection(string paymentAlias = "sp", string orderAlias = "o") =>
        $"""
        (
            {paymentAlias}.paid_at > {orderAlias}.order_date + {CheckoutWindow}
            OR EXISTS (
                SELECT 1
                FROM customer_payments cp
                WHERE cp.sales_order_id = {orderAlias}.id
                  AND cp.status = {CustomerPaymentStatuses.Posted}
                  AND cp.deleted_at IS NULL
                  AND ABS(cp.amount - {paymentAlias}.amount) < 0.01
                  AND ABS(EXTRACT(EPOCH FROM (
                        COALESCE(cp.posted_at, cp.payment_date) - {paymentAlias}.paid_at
                  ))) < {CollectionMatchSeconds}
            )
        )
        """;

    public static string IsCheckout(string paymentAlias = "sp", string orderAlias = "o") =>
        $"NOT {IsCollection(paymentAlias, orderAlias)}";

    public static string AgingDaysExpr(string orderDateExpr) =>
        $"(timezone('Asia/Ho_Chi_Minh', CURRENT_TIMESTAMP)::date - timezone('Asia/Ho_Chi_Minh', {orderDateExpr})::date)";
}
