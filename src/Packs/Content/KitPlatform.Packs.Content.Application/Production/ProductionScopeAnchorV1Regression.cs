namespace KitPlatform.Packs.Content.Production;

public static class ProductionScopeAnchorV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_TEST_SCOPE_FOUNDATION_CHECKPOINT_1";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        Ok(ProductionWorkIds.SchemaVersion == "372", "schema version 372");
        Ok(ProductionScopeAnchorRules.Film == "FILM", "kind FILM");
        Ok(ProductionScopeAnchorRules.FilmScopeId(ProductionScopeAnchorRules.OfficialEp01)
            == ProductionScopeAnchorRules.OfficialEp01, "EP01 FILM scope_id = series_build.id");
        Ok(ProductionScopeAnchorRules.FilmScopeId(ProductionScopeAnchorRules.OfficialEp99)
            == ProductionScopeAnchorRules.OfficialEp99, "EP99 FILM scope_id = series_build.id");
        Ok(ProductionScopeAnchorRules.IsOfficialFilm(ProductionScopeAnchorRules.OfficialEp01), "EP01 is official FILM");
        Ok(ProductionScopeAnchorRules.IsOfficialFilm(ProductionScopeAnchorRules.OfficialEp99), "EP99 is official FILM");
        Ok(ProductionScopeAnchorRules.OfficialFilmScopeIds.Distinct().Count() == 4, "four known FILM builds");

        Ok(ProductionActionContractRegistry.CreateVideo.RequiredInputs.SequenceEqual(
            (string[])["ApprovedKeyframe", "Story", "ActingBeat", "VisualContract", "Timing", "FrozenProviderModel", "PerformanceSnapshot"]),
            "CREATE_VIDEO RequiredInputs include PerformanceSnapshot");
        Ok(ProductionActionContractRegistry.CreateVideo.ExpectedOutputType == ProductionOutputTypes.VideoTake,
            "CREATE_VIDEO output VIDEO_TAKE");
        Ok(CreateVideoActionContractGate.RequiredInputs.SequenceEqual(
            ProductionActionContractRegistry.CreateVideo.RequiredInputs),
            "gate RequiredInputs == contract");

        var work = ProductionWorkExecutionV1Regression.Run();
        Ok(work.Count == 0, "work execution still passes: " + string.Join("; ", work));
        var pipeline = ProductionPipelineV1Regression.Run();
        Ok(pipeline.Count == 0, "pipeline still passes: " + string.Join("; ", pipeline));
        var cutover = ProductionCreateVideoV1Regression.Run();
        Ok(cutover.Count == 0, "create-video cutover still passes: " + string.Join("; ", cutover));
        var contract = ProductionCreateVideoActionContractV1Regression.Run();
        Ok(contract.Count == 0, "action contract T01-T11 still pass: " + string.Join("; ", contract));

        return fail;
    }
}
