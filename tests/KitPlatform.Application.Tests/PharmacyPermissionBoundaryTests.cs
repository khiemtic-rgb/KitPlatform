using System.Reflection;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.CustomerApp;
using KitPlatform.Infrastructure.Dashboard;
using KitPlatform.Packs.Pharmacy.Infrastructure;
using KitPlatform.Packs.Pharmacy.Sales;
using Xunit;

namespace KitPlatform.Tests;

public sealed class PharmacyPermissionBoundaryTests
{
    [Fact]
    public void Admin_discount_is_unlimited()
    {
        var policy = SalesDiscountPolicy.FromPermissions([], isAdmin: true);
        Assert.True(policy.CanApply);
        Assert.True(policy.Unlimited);
        Assert.Equal(100m, policy.MaxPercent);
    }

    [Fact]
    public void Staff_sales_discount_stays_capped()
    {
        var policy = SalesDiscountPolicy.FromPermissions([SalesDiscountPermissions.Apply], isAdmin: false);
        Assert.True(policy.CanApply);
        Assert.False(policy.Unlimited);
        Assert.Equal(SalesDiscountLimits.StaffMaxPercent, policy.MaxPercent);
    }

    [Fact]
    public void Unlimited_discount_permission_stays_unlimited()
    {
        var policy = SalesDiscountPolicy.FromPermissions([SalesDiscountPermissions.Unlimited], isAdmin: false);
        Assert.True(policy.CanApply);
        Assert.True(policy.Unlimited);
        Assert.Equal(100m, policy.MaxPercent);
    }

    [Fact]
    public void Caller_without_discount_permission_cannot_discount()
    {
        var policy = SalesDiscountPolicy.FromPermissions([], isAdmin: false);
        Assert.False(policy.CanApply);
        Assert.False(policy.Unlimited);
        Assert.Equal(0m, policy.MaxPercent);
    }

    [Fact]
    public void Admin_and_manager_can_override_price()
    {
        Assert.True(Override([], isAdmin: true, isManager: false));
        Assert.True(Override([], isAdmin: false, isManager: true));
    }

    [Fact]
    public void Sales_price_override_permission_can_override_price()
    {
        Assert.True(Override([SalesPriceOverridePermissions.Override], isAdmin: false, isManager: false));
    }

    [Fact]
    public void Unlimited_discount_permission_can_override_price()
    {
        Assert.True(Override([SalesDiscountPermissions.Unlimited], isAdmin: false, isManager: false));
    }

    [Fact]
    public void Caller_without_override_permission_cannot_override_price()
    {
        Assert.False(Override([SalesDiscountPermissions.Apply], isAdmin: false, isManager: false));
    }

    [Fact]
    public void Sales_service_uses_the_same_discount_and_override_rules()
    {
        var admin = new UserAccessor([], "ADMIN");
        var staff = new UserAccessor([SalesDiscountPermissions.Apply]);
        var denied = new UserAccessor([]);

        Assert.True(SalesDiscount(admin).Unlimited);
        Assert.Equal(SalesDiscountLimits.StaffMaxPercent, SalesDiscount(staff).MaxPercent);
        Assert.False(SalesDiscount(denied).CanApply);
        Assert.True(SalesOverride(new UserAccessor([], "MANAGER")));
        Assert.True(SalesOverride(new UserAccessor([SalesPriceOverridePermissions.Override])));
        Assert.False(SalesOverride(denied));
    }

    [Fact]
    public void Customer_draft_uses_the_same_discount_rule()
    {
        var service = new CustomerDraftOrderService(null!, null!, null!, null!, new UserAccessor([], "ADMIN"), null!, null!, null!);
        var policy = (SalesDiscountPolicy)typeof(CustomerDraftOrderService)
            .GetProperty("DiscountPolicy", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(service)!;
        Assert.True(policy.Unlimited);
    }

    [Fact]
    public async Task Purchase_order_direct_approval_stays_admin_only()
    {
        var denied = new PurchaseOrderService(null!, null!, null!, null!, null!, new UserAccessor([]));
        var admin = new PurchaseOrderService(null!, null!, null!, null!, null!, new UserAccessor([], "ADMIN"));

        var deniedDirect = await Assert.ThrowsAsync<InvalidOperationException>(() => denied.ApproveAsync(Guid.NewGuid(), null));
        var deniedWorkflow = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            denied.DecideApprovalWorkflowAsync(Guid.NewGuid(), approved: true));
        Assert.Contains("quản trị viên", deniedDirect.Message, StringComparison.Ordinal);
        Assert.Contains("quản trị viên", deniedWorkflow.Message, StringComparison.Ordinal);

        var adminResult = await Record.ExceptionAsync(() => admin.ApproveAsync(Guid.NewGuid(), null));
        Assert.IsNotType<InvalidOperationException>(adminResult);
    }

    [Fact]
    public void Dashboard_store_analytics_keeps_admin_and_reports_permissions()
    {
        Assert.True(Dashboard(new UserAccessor([], "ADMIN")));
        Assert.True(Dashboard(new UserAccessor(["reports.read"])));
        Assert.True(Dashboard(new UserAccessor(["reports.write"])));
        Assert.True(Dashboard(new UserAccessor(["reports.export"])));
        Assert.False(Dashboard(new UserAccessor(["sales.read"])));
    }

    private static bool Override(IReadOnlyList<string> permissions, bool isAdmin, bool isManager) =>
        SalesPriceOverridePolicy.FromPermissions(permissions, isAdmin, isManager).CanOverride;

    private static SalesDiscountPolicy SalesDiscount(UserAccessor user)
    {
        var service = new SalesService(null!, null!, null!, null!, user, null!, null!);
        return (SalesDiscountPolicy)typeof(SalesService)
            .GetProperty("DiscountPolicy", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(service)!;
    }

    private static bool SalesOverride(UserAccessor user)
    {
        var service = new SalesService(null!, null!, null!, null!, user, null!, null!);
        return (bool)typeof(SalesService)
            .GetProperty("AllowPriceOverride", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(service)!;
    }

    private static bool Dashboard(UserAccessor user)
    {
        var service = new DashboardService(null!, null!, user);
        return (bool)typeof(DashboardService)
            .GetMethod("CanViewStoreAnalytics", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(service, null)!;
    }

    private sealed class UserAccessor(IReadOnlyList<string> permissions, params string[] roles) : ICurrentUserAccessor
    {
        public IReadOnlyList<string> Permissions { get; } = permissions;

        public bool IsInRole(string role) => roles.Contains(role);
    }
}
