using System.Linq;

namespace KitPlatform.Packs.Content;

public static class ProductionWorkflowHardeningV1Regression
{
    public const string SuiteId = ProductionWorkflowHardeningRules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var oneScene = ProductionWorkflowHardeningRules.CountScenesAndShots(
            [new ProductionWorkflowHardeningRules.SceneShotGroup("SC01", 11)]);
        Ok(oneScene.SceneCount == 1 && oneScene.ShotCount == 11, "01 1 scene + 11 shots");

        var twoScenes = ProductionWorkflowHardeningRules.CountScenesAndShots(
        [
            new ProductionWorkflowHardeningRules.SceneShotGroup("SC01", 6),
            new ProductionWorkflowHardeningRules.SceneShotGroup("SC02", 5),
        ]);
        Ok(twoScenes.SceneCount == 2 && twoScenes.ShotCount == 11, "02 2 scenes + 11 shots");

        var emptyStills = Enumerable.Repeat(new ProductionWorkflowHardeningRules.ShotProgressInput(false, true, false, false), 11);
        var progress = ProductionWorkflowHardeningRules.EvaluateProgress(
            [new ProductionWorkflowHardeningRules.SceneShotGroup("SC01", 11)],
            emptyStills);
        Ok(progress.ImageMade == 0 && progress.ImageApproved == 0, "03 image progress 0/11");
        Ok(progress.VideoMade == 0 && progress.VideoApproved == 0, "04 clip-only is not video progress");
        Ok(progress.Complete == 0, "05 completion is not 11/11 from clip records");

        var saved = ProductionWorkflowHardeningRules.AssignSceneCharacters(
            ["CHAR-099", "CHAR-098"], ["CHAR-099", "CHAR-098", "CHAR-097"]);
        var again = ProductionWorkflowHardeningRules.AssignSceneCharacters(
            saved.CharacterIds, ["CHAR-099", "CHAR-098", "CHAR-097"]);
        Ok(saved.Ok && again.Ok && string.Join(",", again.CharacterIds) == "CHAR-099,CHAR-098", "06 persist CHAR-099 + CHAR-098");

        var unknown = ProductionWorkflowHardeningRules.AssignSceneCharacters(["CHAR-000"], ["CHAR-099"]);
        Ok(!unknown.Ok && unknown.Error == "Không thể sử dụng nhân vật này" && unknown.CharacterId == "CHAR-000", "07 unknown character blocked");
        Ok(!ProductionWorkflowHardeningRules.AssignSceneCharacters([""], ["CHAR-099"]).CharacterIds.Contains("CHAR-001"), "08 no CHAR-001 fallback");

        var draft = CharacterProductionLibraryRules.EvaluateReadiness(
            new CharacterProductionLibraryRules.AuthoritySnapshot(
                true, true, true, true, true, true, "DRAFT", "V1", false, true, 3, 4, 1, ["FULL_BODY"]));
        Ok(!draft.CanUse && draft.Code == "REFERENCE_MISSING" && draft.Reason.Contains("Toàn thân", StringComparison.Ordinal), "09 CRP 3/4 canUse=false");

        var four = CharacterProductionLibraryRules.EvaluateReadiness(
            new CharacterProductionLibraryRules.AuthoritySnapshot(
                true, true, true, true, true, true, "DRAFT", "V1", true, true, 4, 4, 1));
        Ok(!four.CanUse && four.Code == "CRP_DRAFT", "10 4/4 draft still not canUse");
        var validated = CharacterProductionLibraryRules.EvaluateReadiness(
            new CharacterProductionLibraryRules.AuthoritySnapshot(
                true, true, true, true, true, true, "VALIDATED", "V1", true, true, 4, 4, 1));
        Ok(validated.Code == "CRP_VALIDATED" && validated.CanUse && validated.Label.Contains("hoàn tất", StringComparison.Ordinal), "11 4/4 validated canUse without lock");

        Ok(!ProductionWorkflowHardeningRules.Generate
            && !ProductionWorkflowHardeningRules.GeminiCalled
            && !ProductionWorkflowHardeningRules.RunwayCalled
            && !ProductionWorkflowHardeningRules.VeoCalled
            && !ProductionWorkflowHardeningRules.AutoApprove
            && !ProductionWorkflowHardeningRules.AutoLock, "12 generate=false");

        Ok(ProductionWorkflowHardeningRules.StaffStatus("READY_FOR_DIRECTOR") == "CẦN DUYỆT", "13 staff READY_FOR_DIRECTOR");
        Ok(ProductionWorkflowHardeningRules.StaffBlockReason("REFERENCE_MISSING").Contains("Bộ ảnh", StringComparison.Ordinal), "14 staff REFERENCE_MISSING");
        Ok(ProductionWorkflowHardeningRules.StaffBlockReason("VIDEO_GENERATION_NOT_READY").Contains("duyệt hình", StringComparison.Ordinal), "15 staff video block");

        return fail;
    }
}
