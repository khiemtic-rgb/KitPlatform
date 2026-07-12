namespace KitPlatform.Packs.Connect;

/// <summary>
/// Novixa Connect — Healthcare Collaboration Pack.
/// Independent of Pharmacy; does not issue e-Rx or perform clinical care.
/// </summary>
public static class ConnectPackDefinition
{
    public const string PackCode = "novixa_connect";
    public const string TenantPackageCode = "novixa_connect";
    public const string DisplayName = "Novixa Connect";

    /// <summary>Platform module that gates Connect admin + API.</summary>
    public const string PrimaryModuleCode = "novixa_connect";

    public static IReadOnlyList<string> DefaultEnabledModules { get; } =
    [
        PrimaryModuleCode,
    ];

    public static IReadOnlyList<string> PackModuleCodes { get; } =
    [
        PrimaryModuleCode,
    ];
}
