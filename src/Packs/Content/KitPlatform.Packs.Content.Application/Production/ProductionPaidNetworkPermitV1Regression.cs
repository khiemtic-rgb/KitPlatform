namespace KitPlatform.Packs.Content.Production;

public static class ProductionPaidNetworkPermitV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_PAID_NETWORK_PERMIT_V1";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }
        var permit = new ProductionPaidNetworkOneShotPermit();
        var test = new ProductionScope(ProductionTestSandboxIds.ScopeId, "TEST", "SC01", ProductionTestSandboxIds.ShotCode);
        var film = new ProductionScope(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH01");
        Ok(permit.IsEligible(test, ProductionTestSandboxIds.ShotCode, "runway", "gen4_turbo"), "TEST runway eligible");
        Ok(!permit.IsEligible(film, "EP01-SC01-SH01", "runway", "gen4_turbo"), "FILM not eligible");
        Ok(!permit.IsEligible(test, ProductionTestSandboxIds.ShotCode, "fixture", "fixture-motion-v1"), "fixture not eligible");
        Ok(permit.TryConsumeStart(test, ProductionTestSandboxIds.ShotCode, "runway", "gen4_turbo"), "first Start consumed");
        Ok(!permit.TryConsumeStart(test, ProductionTestSandboxIds.ShotCode, "runway", "gen4_turbo"), "second Start rejected");
        Ok(permit.StartCount == 1, "Start count is 1");
        Ok(!permit.IsEligible(test, ProductionTestSandboxIds.ShotCode, "runway", "gen4_turbo"), "no longer eligible");
        return fail;
    }
}
