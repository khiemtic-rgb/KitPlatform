namespace KitPlatform.Packs.Content;

/// <summary>
/// KIT Marketing Park — independent content & marketing product (org <c>KIT_MKT</c>).
/// Isolated from Novixa Pharmacy ERP and Famixa Family OS.
/// </summary>
public static class ContentPackDefinition
{
    /// <summary>Technical pack / platform module code (registry + enabled_modules).</summary>
    public const string PackCode = "kit_content";

    /// <summary>Commercial / tenant_package product code.</summary>
    public const string TenantPackageCode = "marketing_park";

    public const string DisplayName = "KIT Marketing Park";
    public const string EventSource = "pack:content";
    public const string PrimaryModuleCode = "kit_content";
    public const string DedicatedTenantCode = "KIT_MKT";
    public const string SolutionPhase = "V1_product_isolation";

    /// <summary>Seed singleton org_settings row id.</summary>
    public static readonly Guid OrgSettingsId = Guid.Parse("a0000000-0000-7000-8000-000000000001");

    public static IReadOnlyList<string> DefaultEnabledModules { get; } = [PrimaryModuleCode];
    public static IReadOnlyList<string> PackModuleCodes { get; } = [PrimaryModuleCode];
}
