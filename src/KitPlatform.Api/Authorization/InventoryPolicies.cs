namespace KitPlatform.Api.Authorization;

public static class InventoryPolicies
{
    public const string Read = "InventoryRead";
    public const string Write = "InventoryWrite";
    /// <summary>AC5 — approve adjustments / internal-issue stock outs (inventory.approve or ADMIN).</summary>
    public const string Approve = "InventoryApprove";
    /// <summary>POS / list warehouses — cashiers with sales.pos may pick a sell warehouse.</summary>
    public const string WarehouseLookup = "InventoryWarehouseLookup";
    /// <summary>INV-01 worklist — reports or inventory, not sales.pos.</summary>
    public const string ReviewRead = "InventoryStockReviewRead";
}
