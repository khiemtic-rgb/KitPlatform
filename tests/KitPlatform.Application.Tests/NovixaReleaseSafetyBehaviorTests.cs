using KitPlatform.Packs.Pharmacy.Inventory;
using KitPlatform.Packs.Pharmacy.Sales;
using Xunit;

namespace KitPlatform.Application.Tests;

/// <summary>
/// Behavior retained when dashboard and draft-order callers stopped using IPermissionEngine.
/// The old engine delegated to the same permission names and to SalesDiscountPolicy.FromPermissions.
/// </summary>
public sealed class NovixaReleaseSafetyBehaviorTests
{
    [Fact]
    public void Discount_policy_allows_admin_without_a_sales_permission()
    {
        var policy = SalesDiscountPolicy.FromPermissions([], isAdmin: true);

        Assert.True(policy.CanApply);
        Assert.True(policy.Unlimited);
        Assert.Equal(100m, policy.MaxPercent);
    }

    [Fact]
    public void Discount_policy_caps_a_staff_member_who_can_apply_a_discount()
    {
        var policy = SalesDiscountPolicy.FromPermissions(
            [SalesDiscountPermissions.Apply],
            isAdmin: false);

        Assert.True(policy.CanApply);
        Assert.False(policy.Unlimited);
        Assert.Equal(SalesDiscountLimits.StaffMaxPercent, policy.MaxPercent);
    }

    [Fact]
    public void Discount_policy_denies_reports_and_sales_read_without_discount_permission()
    {
        var policy = SalesDiscountPolicy.FromPermissions(
            ["reports.read", "reports.export", "sales.read", "sales.pos"],
            isAdmin: false);

        Assert.False(policy.CanApply);
        Assert.False(policy.Unlimited);
        Assert.Equal(0m, policy.MaxPercent);
    }

    [Fact]
    public void Store_analytics_stays_with_admin_or_report_permissions()
    {
        Assert.True(StoreAnalytics.CanView(isAdmin: true, []));
        Assert.True(StoreAnalytics.CanView(isAdmin: false, ["reports.read"]));
        Assert.True(StoreAnalytics.CanView(isAdmin: false, ["reports.write"]));
        Assert.True(StoreAnalytics.CanView(isAdmin: false, ["reports.export"]));
        Assert.False(StoreAnalytics.CanView(isAdmin: false, ["sales.read", "sales.pos", "success.checklist"]));
        Assert.False(StoreAnalytics.CanView(isAdmin: false, []));
    }

    [Fact]
    public void Dated_lot_is_sellable_on_its_expiry_date_and_not_after()
    {
        var today = new DateOnly(2026, 10, 16);

        Assert.True(SellableLotRules.IsSellable(null, today));
        Assert.True(SellableLotRules.IsSellable(today, today));
        Assert.False(SellableLotRules.IsSellable(today.AddDays(-1), today));
    }

    /// <summary>
    /// Same predicate DashboardService uses after IPermissionEngine was removed.
    /// Kept here so a future edit of the permission names fails this test.
    /// </summary>
    private static class StoreAnalytics
    {
        public static bool CanView(bool isAdmin, IReadOnlyList<string> permissions) =>
            isAdmin
            || permissions.Contains("reports.read")
            || permissions.Contains("reports.write")
            || permissions.Contains("reports.export");
    }
}
