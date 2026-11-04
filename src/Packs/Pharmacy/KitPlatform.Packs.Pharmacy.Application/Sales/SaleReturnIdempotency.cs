namespace KitPlatform.Packs.Pharmacy.Sales;

public static class SaleReturnIdempotency
{
    public static void EnsureSameOrder(Guid existingOrderId, Guid requestedOrderId)
    {
        if (existingOrderId != requestedOrderId)
            throw new InvalidOperationException("clientReturnId đã dùng cho một đơn khác.");
    }
}
