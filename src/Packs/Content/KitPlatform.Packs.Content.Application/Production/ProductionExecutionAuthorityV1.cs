namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// AUTHORITY-STABILIZATION-01E. Provider routes may still run.
/// They do not establish Official Production Execution authority.
/// </summary>
public static class ProductionExecutionAuthorityV1
{
    public const string Official = "OFFICIAL_PRODUCTION_EXECUTION";
    public const string Legacy = "LEGACY_EXECUTION";
    public const string ProviderImplementation = "PROVIDER_IMPLEMENTATION";
    public const string Editorial = "EDITORIAL";
    public const string History = "HISTORY";

    public static readonly string[] LegacyDirectRoutes =
    [
        "POST content/series/turbo",
        "POST content/series/lipsync",
        "POST content/video-engine/shots/{id}/video-generation-execution/execute",
    ];

    public static readonly string[] OfficialOsRoutes =
    [
        "POST content/production/requests CREATE_VIDEO",
        "POST content/production/requests CREATE_VOICE",
        "POST content/production/requests CREATE_LIPSYNC",
        "POST content/production/requests ACCEPT",
    ];

    public const string LegacyProviderSubmitClosed =
        "LEGACY · NON-AUTHORITY · không gửi provider job. Production OS là execution authority.";

    public static bool LegacyDirectEstablishesOfficialBinding(string route)
    {
        _ = route;
        return false;
    }

    public static bool LegacyDirectMaySubmitProviderJob(string route)
    {
        _ = route;
        return false;
    }

    public static bool ProviderCompletionEstablishesOfficialBinding() => false;
}
