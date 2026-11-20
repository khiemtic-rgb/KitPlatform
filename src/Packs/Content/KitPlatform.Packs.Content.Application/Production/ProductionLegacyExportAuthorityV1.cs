namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// AUTHORITY-STABILIZATION-01F. POST content/series/assemble is export-only.
/// It does not create Production OS state.
/// </summary>
public static class ProductionLegacyExportAuthorityV1
{
    public const string Route = "POST content/series/assemble";
    public const string Classification = "EXPORT_ONLY";

    public static bool MayCreateOfficialArtifact() => false;
    public static bool MayCreateOfficialBinding() => false;
    public static bool MayCreateFilmAssembly() => false;
    public static bool MayMarkCurrentVisibleOrAccepted() => false;
    public static bool MayCreateOfficialEpisode() => false;
    public static bool MayMutateFrozenInput() => false;
    public static bool AssembleOneShotMayExecuteOfficialProduction() => false;
}
