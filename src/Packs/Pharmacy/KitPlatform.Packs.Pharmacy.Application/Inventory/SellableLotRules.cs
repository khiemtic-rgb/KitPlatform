namespace KitPlatform.Packs.Pharmacy.Inventory;

/// <summary>
/// Which on-hand lots may be offered or consumed by a counter sale.
/// A lot with no expiry stays sellable. That policy is unchanged.
/// A dated lot is sellable on its expiry date and not after.
/// </summary>
public static class SellableLotRules
{
    public static bool IsSellable(DateOnly? expiryDate, DateOnly today) =>
        expiryDate is null || expiryDate.Value >= today;

    /// <summary>Predicate for <c>inventory_batches</c> with no table alias. Null expiry stays included.</summary>
    public const string UnaliasedSql =
        "(expiry_date IS NULL OR expiry_date >= CURRENT_DATE)";

    /// <summary>Predicate for <c>inventory_batches b</c>. Null expiry stays included.</summary>
    public const string BatchSql =
        "(b.expiry_date IS NULL OR b.expiry_date >= CURRENT_DATE)";
}
