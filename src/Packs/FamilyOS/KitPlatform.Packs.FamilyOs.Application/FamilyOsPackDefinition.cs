namespace KitPlatform.Packs.FamilyOs;

/// <summary>
/// FamilyOS Starter — One Family. One Plan. One Daily Flow.
/// Independent of Pharmacy care wallet; does not manage finance/health/GPS/school.
/// </summary>
public static class FamilyOsPackDefinition
{
    public const string PackCode = "family_os";
    public const string TenantPackageCode = "family_os";
    public const string DisplayName = "FamilyOS Starter";
    public const string EventSource = "pack:family_os";

    /// <summary>Platform module that gates FamilyOS admin + API.</summary>
    public const string PrimaryModuleCode = "family_os";

    public static IReadOnlyList<string> DefaultEnabledModules { get; } =
    [
        PrimaryModuleCode,
    ];

    public static IReadOnlyList<string> PackModuleCodes { get; } =
    [
        PrimaryModuleCode,
    ];
}
