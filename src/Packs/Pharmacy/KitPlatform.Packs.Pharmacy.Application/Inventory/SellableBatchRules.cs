namespace KitPlatform.Packs.Pharmacy.Inventory;

/// <summary>
/// Tồn bán được: lô không có hạn hoặc hạn còn trong ngày (cùng mốc CURRENT_DATE với màn hết hạn).
/// Sổ kho vẫn giữ lô hết hạn để kiểm kê và xuất hủy — không lọc ở danh sách lô.
/// </summary>
public static class SellableBatchRules
{
    public const string SqlOnAliasB = "(b.expiry_date IS NULL OR b.expiry_date >= CURRENT_DATE)";

    public const string SqlUnqualified = "(expiry_date IS NULL OR expiry_date >= CURRENT_DATE)";

    public static bool IsSellableOn(DateOnly? expiryDate, DateOnly today) =>
        expiryDate is null || expiryDate.Value >= today;
}
