namespace KitPlatform.Packs.Sales;

public static class SalesPackDefinition
{
    public const string PackCode = "kit_sales";
    public const string TenantPackageCode = "kit_sales";
    public const string DisplayName = "KIT Sales";
    public const string EventSource = "pack:kit_sales";
    public const string PrimaryModuleCode = "kit_sales";
    public const string DedicatedTenantCode = "KIT_SALES";
    public static readonly Guid DedicatedTenantId = Guid.Parse("11111111-1111-1111-1111-111111111107");

    public static IReadOnlyList<string> DefaultEnabledModules { get; } = [PrimaryModuleCode];
    public static IReadOnlyList<string> PackModuleCodes { get; } = [PrimaryModuleCode];
}
