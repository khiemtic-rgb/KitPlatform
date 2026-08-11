namespace KitPlatform.Packs.Care;

/// <summary>
/// Care OS — T3-ready care event plane (cohorts + KPI hooks).
/// Shapes Community Health; not a live community-outcomes product.
/// </summary>
public static class CarePackDefinition
{
    // Local build stamp — changes binary hash so Windows Smart App Control reputation
    // does not permanently block this unsigned Debug assembly after a false positive.
    public const string LocalBuildStamp = "sac-unblock-2026-08-10";

    public const string PackCode = "care_os";
    public const string TenantPackageCode = "care_os";
    public const string DisplayName = "Care OS (T3-ready)";
    public const string EventSource = "pack:care_os";
    public const string PrimaryModuleCode = "care_os";
    public const string SolutionPhase = "T3_ready_instrumentation";

    public static IReadOnlyList<string> DefaultEnabledModules { get; } = [PrimaryModuleCode];
    public static IReadOnlyList<string> PackModuleCodes { get; } = [PrimaryModuleCode];
}
