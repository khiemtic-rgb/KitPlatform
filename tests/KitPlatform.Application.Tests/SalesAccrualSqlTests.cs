using KitPlatform.Infrastructure.Reports;
using KitPlatform.Packs.Pharmacy.Sales;
using Xunit;

namespace KitPlatform.Application.Tests;

public sealed class SalesAccrualSqlTests
{
    [Fact]
    public void Original_total_adds_back_completed_returns()
    {
        var sql = SalesAccrualSql.OriginalTotalExpr();
        Assert.Contains("total_amount", sql, StringComparison.Ordinal);
        Assert.Contains("sales_return_items", sql, StringComparison.Ordinal);
        Assert.Contains(SalesReturnStatuses.Completed.ToString(), sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Collection_is_later_or_posted_customer_payment()
    {
        var collection = SalesAccrualSql.IsCollection();
        Assert.Contains("order_date + interval '15 minutes'", collection, StringComparison.Ordinal);
        Assert.Contains(">", collection, StringComparison.Ordinal);
        Assert.Contains("customer_payments", collection, StringComparison.Ordinal);
        Assert.Contains("cp.amount", collection, StringComparison.Ordinal);
        Assert.Contains(CustomerPaymentStatuses.Posted.ToString(), collection, StringComparison.Ordinal);
        Assert.Contains("NOT ", SalesAccrualSql.IsCheckout(), StringComparison.Ordinal);
        Assert.Contains("customer_payments", SalesAccrualSql.IsCheckout(), StringComparison.Ordinal);
    }

    [Fact]
    public void Aging_uses_calendar_days_not_interval_extract()
    {
        var sql = SalesAccrualSql.AgingDaysExpr("s.order_date");
        Assert.Contains("Asia/Ho_Chi_Minh", sql, StringComparison.Ordinal);
        Assert.Contains("::date", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("EXTRACT(DAY FROM", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Sold_status_includes_completed_and_refunded()
    {
        var sql = SalesAccrualSql.SoldStatusFilter();
        Assert.Contains(SalesOrderStatuses.Completed.ToString(), sql, StringComparison.Ordinal);
        Assert.Contains(SalesOrderStatuses.Refunded.ToString(), sql, StringComparison.Ordinal);
    }
}
