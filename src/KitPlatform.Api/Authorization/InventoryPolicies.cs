namespace KitPlatform.Api.Authorization;

public static class InventoryPolicies
{
    public const string Read = "InventoryRead";
    public const string Write = "InventoryWrite";
    /// <summary>AC5 — approve adjustments / internal-issue stock outs (inventory.approve or ADMIN).</summary>
    public const string Approve = "InventoryApprove";
}
