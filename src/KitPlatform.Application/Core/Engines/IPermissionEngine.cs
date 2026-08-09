using KitPlatform.Packs.Pharmacy.Sales;

namespace KitPlatform.Application.Core.Engines;

/// <summary>
/// Core Permission Engine — RBAC helpers used by business rules (BR-SEC-*, BR-PRC-002).
/// </summary>
public interface IPermissionEngine
{
    bool HasPermission(string permissionCode);

    bool IsAdmin();

    /// <summary>BR-PRC-002: staff discount limits from permissions.</summary>
    SalesDiscountPolicy GetSalesDiscountPolicy();

    /// <summary>Allow POS line unit price different from catalog (sales.price.override or ADMIN).</summary>
    SalesPriceOverridePolicy GetSalesPriceOverridePolicy();
}
