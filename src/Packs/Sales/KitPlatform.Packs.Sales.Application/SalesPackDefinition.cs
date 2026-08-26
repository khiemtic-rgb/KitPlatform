namespace KitPlatform.Packs.Sales;

public static class SalesPackDefinition
{
    public const string PackCode = "kit_sales";
    public const string TenantPackageCode = "kit_sales";
    public const string DisplayName = "KIT Sales";
    public const string EventSource = "pack:kit_sales";
    public const string PrimaryModuleCode = "kit_sales";

    public static IReadOnlyList<string> DefaultEnabledModules { get; } = [PrimaryModuleCode];
    public static IReadOnlyList<string> PackModuleCodes { get; } = [PrimaryModuleCode];
}
