namespace KitPlatform.Api.Authorization;

public static class SalesPolicies
{
    public const string Read = "SalesRead";
    public const string Write = "SalesWrite";
    /// <summary>AC5 — cancel draft sales orders (sales.cancel or ADMIN).</summary>
    public const string Cancel = "SalesCancel";
}
