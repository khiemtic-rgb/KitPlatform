using KitPlatform.Packs.Pharmacy.Sales;

namespace KitPlatform.Application.Core.Engines;

/// <summary>
/// Compile surface for committed Infrastructure code that already names this type.
/// The isolated Second Brain host does not register an implementation and does not resolve it.
/// </summary>
public interface IPermissionEngine
{
    bool IsAdmin();
    bool HasPermission(string permission);
    SalesDiscountPolicy GetSalesDiscountPolicy();
}
