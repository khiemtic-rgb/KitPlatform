namespace KitPlatform.Packs.Content.Production;

/// <summary>AUTHORITY-STABILIZATION-01F. Legacy assemble is export-only.</summary>
public static class ProductionLegacyExportAuthorityV1Regression
{
    public const string SuiteId = "FAMIXA_LEGACY_EXPORT_AUTHORITY_01F";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        Ok(ProductionLegacyExportAuthorityV1.Classification == "EXPORT_ONLY", "T12 legacy assemble is export-only");
        Ok(!ProductionLegacyExportAuthorityV1.MayCreateOfficialArtifact(), "T1 no official artifact");
        Ok(!ProductionLegacyExportAuthorityV1.MayCreateOfficialBinding(), "T2 no official binding");
        Ok(!ProductionLegacyExportAuthorityV1.MayCreateFilmAssembly(), "T3 no FilmAssembly");
        Ok(!ProductionLegacyExportAuthorityV1.MayMarkCurrentVisibleOrAccepted(), "T4/T5/T6 no CURRENT VISIBLE ACCEPTED");
        Ok(!ProductionLegacyExportAuthorityV1.MayCreateOfficialEpisode(), "T7 no official episode");
        Ok(!ProductionLegacyExportAuthorityV1.AssembleOneShotMayExecuteOfficialProduction(), "T8 assembleOneShot is not official production");
        Ok(!ProductionLegacyExportAuthorityV1.MayMutateFrozenInput(), "T10 frozen input stays untouched");
        Ok(ProductionLegacyExportAuthorityV1.Route == "POST content/series/assemble", "route stays the legacy export route");
        return fail;
    }
}
