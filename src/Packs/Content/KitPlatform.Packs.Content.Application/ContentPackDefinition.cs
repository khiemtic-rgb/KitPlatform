namespace KitPlatform.Packs.Content;

/// <summary>
/// KIT Content Park — multi-brand content factory, dynamic budget/targets.
/// Isolated from Pharmacy ERP and Family OS.
/// </summary>
public static class ContentPackDefinition
{
    public const string PackCode = "kit_content";
    public const string DisplayName = "KIT Content Park";
    public const string EventSource = "pack:content";
    public const string PrimaryModuleCode = "kit_content";
    public const string SolutionPhase = "V1_wave0_settings";

    /// <summary>Seed singleton org_settings row id.</summary>
    public static readonly Guid OrgSettingsId = Guid.Parse("a0000000-0000-7000-8000-000000000001");

    public static IReadOnlyList<string> DefaultEnabledModules { get; } = [PrimaryModuleCode];
    public static IReadOnlyList<string> PackModuleCodes { get; } = [PrimaryModuleCode];
}
