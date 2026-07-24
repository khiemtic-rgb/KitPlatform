namespace KitPlatform.Packs.Pharmacy.Inventory;

public static class StockMovementTypes
{
    public const short In = 1;
    public const short Out = 2;
}

public static class StockReferenceTypes
{
    public const string OpeningBalance = "opening_balance";
    public const string OpeningBalanceVoid = "opening_balance_void";
    public const string GoodsReceipt = "goods_receipt";
    public const string InventoryTransfer = "inventory_transfer";
    public const string InventoryAdjustment = "inventory_adjustment";
    public const string SalesOrder = "sales_order";
    public const string SalesReturn = "sales_return";
    public const string ProductMerge = "product_merge";
}

public static class WarehouseTypes
{
    public const short Main = 1;
    public const short Retail = 2;
    public const short Rx = 3;
    public const short Cold = 4;
    public const short Returned = 5;
}

public static class TransferStatuses
{
    public const short Draft = 1;
    public const short Pending = 2;
    public const short Completed = 3;
    public const short Cancelled = 4;
}

public static class AdjustmentStatuses
{
    public const short Draft = 1;
    public const short Counting = 2;
    public const short Approved = 3;
    public const short Cancelled = 4;
}
