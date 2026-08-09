using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Core.Engines;
using KitPlatform.Packs.Pharmacy.Sales;

namespace KitPlatform.Infrastructure.Core.Engines;

internal sealed class PermissionEngine : IPermissionEngine
{
    private readonly ICurrentUserAccessor _user;

    public PermissionEngine(ICurrentUserAccessor user) => _user = user;

    public bool HasPermission(string permissionCode)
        => _user.Permissions.Contains(permissionCode);

    public bool IsAdmin() => _user.IsInRole("ADMIN");

    public bool IsManager() => _user.IsInRole("MANAGER");

    public SalesDiscountPolicy GetSalesDiscountPolicy()
        => SalesDiscountPolicy.FromPermissions(_user.Permissions, IsAdmin());

    public SalesPriceOverridePolicy GetSalesPriceOverridePolicy()
        => SalesPriceOverridePolicy.FromPermissions(_user.Permissions, IsAdmin(), IsManager());
}
