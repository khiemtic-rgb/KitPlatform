namespace KitPlatform.Packs.Content.Production;

/// <summary>AUTHORITY-STABILIZATION-01E. Direct provider routes are not official binding authority.</summary>
public static class ProductionExecutionAuthorityV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_EXECUTION_AUTHORITY_01E";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        Ok(ProductionExecutionAuthorityV1.LegacyDirectRoutes.Length == 3, "T1/T2/T3 three legacy direct routes are classified");
        foreach (var route in ProductionExecutionAuthorityV1.LegacyDirectRoutes)
        {
            Ok(!ProductionExecutionAuthorityV1.LegacyDirectEstablishesOfficialBinding(route), "T1/T2/T3/T4 " + route + " does not establish an official binding");
            Ok(!ProductionExecutionAuthorityV1.LegacyDirectMaySubmitProviderJob(route), "T1/T2/T3 " + route + " does not submit a provider job");
        }
        Ok(ProductionExecutionAuthorityV1.LegacyProviderSubmitClosed.Contains("NON-AUTHORITY", StringComparison.Ordinal)
            && ProductionExecutionAuthorityV1.LegacyProviderSubmitClosed.Contains("không gửi provider job", StringComparison.Ordinal),
            "closed legacy execution names the non-authority boundary");
        Ok(!ProductionExecutionAuthorityV1.ProviderCompletionEstablishesOfficialBinding(), "T4/T5 provider completion is not official artifact or binding authority");
        foreach (var route in ProductionExecutionAuthorityV1.OfficialOsRoutes)
            Ok(!ProductionExecutionAuthorityV1.LegacyDirectRoutes.Contains(route), "T6 official OS routes are not the legacy direct routes");
        Ok(ProductionExecutionAuthorityV1.Official == "OFFICIAL_PRODUCTION_EXECUTION"
            && ProductionExecutionAuthorityV1.Legacy == "LEGACY_EXECUTION", "classification labels stay explicit");
        return fail;
    }
}
