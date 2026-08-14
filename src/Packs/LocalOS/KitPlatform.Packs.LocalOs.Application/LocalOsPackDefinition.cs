namespace KitPlatform.Packs.LocalOs;

public static class LocalOsPackDefinition
{
    public const string PackCode = "local_os";
    public const string TenantPackageCode = "local_os";
    public const string DisplayName = "KIT Local OS";
    public const string EventSource = "pack:local_os";
    public const string PrimaryModuleCode = "local_os";
    public const string DedicatedTenantCode = "KIT_LOCAL";
    public const string DefaultCityCode = "thai_nguyen";
    public const string PublicBrand = "Thái Nguyên Life";

    public static IReadOnlyList<string> DefaultEnabledModules { get; } = [PrimaryModuleCode];
    public static IReadOnlyList<string> PackModuleCodes { get; } = [PrimaryModuleCode];
}
