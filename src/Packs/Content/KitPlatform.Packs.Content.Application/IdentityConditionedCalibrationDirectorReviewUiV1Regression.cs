namespace KitPlatform.Packs.Content;

public static class IdentityConditionedCalibrationDirectorReviewUiV1Regression
{
    public const string SuiteId = "FAMIXA_IDENTITY_CONDITIONED_CALIBRATION_DIRECTOR_REVIEW_UI_FIX_V1_REGRESSION";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var pack = IdentityConditionedCalibrationDirectorReviewV1Rules.CompleteRun();
        var workspace = IdentityConditionedCalibrationDirectorReviewV1Rules.BuildPackReview(pack);
        var subject = workspace.Subjects[2];
        var front = subject.Views.First(v => v.View == "FRONT");
        var down = subject.Views.Where(v => v.View != "FRONT").ToList();

        Ok(IdentityConditionedCalibrationDirectorReviewV1Rules.DirectorReviewInfrastructure()
            && workspace.Status == IdentityConditionedCalibrationDirectorReviewV1Rules.ReadyStatus,
            "DR-UI-01 Director Review page loads");
        Ok(workspace.SubjectsFound == 6 && workspace.Subjects.Count == 6, "DR-UI-02 Six subjects are displayed");
        Ok(workspace.Subjects.All(s => s.Views.Count == 4)
            && workspace.ViewsFound == 24, "DR-UI-03 Four views per subject are displayed");
        Ok(front.IsIdentityAnchor && front.View == "FRONT", "DR-UI-04 FRONT is marked IDENTITY ANCHOR");
        Ok(down.Count == 3 && down.All(v => v.IsIdentityConditioned || v.ReferenceRole
                == IdentityConditionedCalibrationV1Rules.ReferenceRoleAnchor),
            "DR-UI-05 Three downstream views are marked IDENTITY CONDITIONED");

        var historical = IdentityConditionedCalibrationLiveV1Rules.Isolate(
            pack with
            {
                CalibrationRunId = null,
                Subjects = pack.Subjects.Select(s => s with
                {
                    Artifacts = s.Artifacts.Select(a => a with
                    {
                        IdentityAnchorSlot = null,
                        IdentityAnchorSha256 = null,
                        ReferenceRole = null,
                    }).ToList(),
                }).ToList(),
            });
        var historicalWs = IdentityConditionedCalibrationDirectorReviewV1Rules.BuildPackReview(historical);
        Ok(historicalWs.Historical.Count == 24
            && historicalWs.IdentityConditionedValid == 0
            && historicalWs.Historical.All(h =>
                h.Label == IdentityConditionedCalibrationDirectorReviewV1Rules.HistoricalLabel),
            "DR-UI-06 Historical artifacts are not eligible");

        Ok(workspace.Subjects.All(s =>
                s.Gates.IdentityGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Pending
                && s.Gates.AgeGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Pending
                && s.Gates.AppearanceGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Pending
                && s.Gates.FaceConsistencyGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Pending
                && s.Gates.ViewConsistencyGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Pending
                && s.Gates.WardrobeConsistencyGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Pending
                && s.Gates.VisualUniverseGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Pending
                && s.Gates.StylizationGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Pending
                && s.Gates.CrossCharacterGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Pending
                && s.Gates.PhotorealismGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Pending),
            "DR-UI-07 All ten visual gates default to PENDING");
        Ok(workspace.Subjects.All(s => s.Decision == IdentityConditionedCalibrationDirectorReviewV1Rules.Pending),
            "DR-UI-08 Decision defaults to PENDING");

        var saved = new Dictionary<string, IdentityConditionedCalibrationDirectorDecision>(StringComparer.OrdinalIgnoreCase)
        {
            [subject.SubjectId] = IdentityConditionedCalibrationDirectorReviewV1Rules.AllPassDecision(
                subject.SubjectId, pack.CalibrationRunId!, pack.PackId),
        };
        var afterSave = IdentityConditionedCalibrationDirectorReviewV1Rules.BuildPackReview(pack, saved);
        Ok(afterSave.Subjects.First(s => s.SubjectId == subject.SubjectId).Decision
            == IdentityConditionedCalibrationDirectorReviewV1Rules.Pass
            && afterSave.Subjects.First(s => s.SubjectId == subject.SubjectId).Gates.AllPass(),
            "DR-UI-09 Save Review persists to the existing in-memory review service");
        var reloaded = IdentityConditionedCalibrationDirectorReviewV1Rules.BuildPackReview(pack, saved);
        Ok(reloaded.Subjects.First(s => s.SubjectId == subject.SubjectId).Decision
            == afterSave.Subjects.First(s => s.SubjectId == subject.SubjectId).Decision
            && reloaded.Subjects.First(s => s.SubjectId == subject.SubjectId).Gates.IdentityGate
                == afterSave.Subjects.First(s => s.SubjectId == subject.SubjectId).Gates.IdentityGate,
            "DR-UI-10 Reload displays saved review state");

        var failDecision = IdentityConditionedCalibrationDirectorReviewV1Rules.ApplyDecision(
            IdentityConditionedCalibrationDirectorReviewV1Rules.EvaluateTechnical(
                pack,
                VisualCalibrationPackV1Rules.DefaultMatrix.First(s => s.CalibrationSubjectId == subject.SubjectId),
                "FRONT",
                IdentityConditionedCalibrationDirectorReviewV1Rules.CurrentArtifact(pack, subject.SubjectId, "FRONT")),
            saved[subject.SubjectId] with
            {
                Decision = IdentityConditionedCalibrationDirectorReviewV1Rules.Fail,
                Gates = saved[subject.SubjectId].Gates with
                {
                    IdentityGate = IdentityConditionedCalibrationDirectorReviewV1Rules.Fail,
                },
            },
            null);
        Ok(failDecision.Decision == IdentityConditionedCalibrationDirectorReviewV1Rules.Fail
            && !IdentityConditionedCalibrationDirectorReviewV1Rules.RegeneratesOnFail()
            && !IdentityConditionedCalibrationDirectorReviewV1Rules.CreatesPixels(),
            "DR-UI-11 FAIL does not trigger generation");
        Ok(!IdentityConditionedCalibrationDirectorReviewV1Rules.CreatesPixels()
            && IdentityConditionedCalibrationDirectorReviewV1Rules.NormalizeDecision("REVIEW_REQUIRED")
                == IdentityConditionedCalibrationDirectorReviewV1Rules.ReviewRequired,
            "DR-UI-12 REVIEW_REQUIRED does not trigger generation");

        Ok(!IdentityConditionedCalibrationDirectorReviewV1Rules.CanMarkVisualPass(workspace)
            && !workspace.VisualPass, "DR-UI-13 Visual Calibration Pass is disabled until pack eligibility is true");
        Ok(!IdentityConditionedCalibrationDirectorReviewV1Rules.CallsGemini()
            && !workspace.GeminiCalled, "DR-UI-14 Visual Calibration Pass does not call Gemini");
        Ok(!IdentityConditionedCalibrationDirectorReviewV1Rules.ProviderCalled()
            && !workspace.ProviderCalled, "DR-UI-15 Visual Calibration Pass does not call an image provider");
        Ok(!IdentityConditionedCalibrationDirectorReviewV1Rules.MutatesAuthority()
            && IdentityConditionedCalibrationDirectorReviewV1Rules.AuthorityShaUnchanged(),
            "DR-UI-16 Visual Calibration Pass does not mutate authority");
        Ok(!IdentityConditionedCalibrationDirectorReviewV1Rules.MutatesCharacters()
            && !VisualCalibrationPackV1Rules.CreatesCharacterMaster(),
            "DR-UI-17 No Master Revision is created by Director Review");
        Ok(!IdentityConditionedCalibrationDirectorReviewV1Rules.RegeneratesOnFail()
            && !IdentityConditionedCalibrationDirectorReviewV1Rules.CreatesPixels(),
            "DR-UI-18 No regeneration occurs from Director Review");
        Ok(IdentityConditionedCalibrationV1Regression.Run().Count == 0,
            "DR-UI-19 Existing IdentityConditionedCalibration regressions remain PASS");
        Ok(VisualFoundationFinalizationV1Regression.Run().Count == 0,
            "DR-UI-20 Existing Foundation Finalization regressions remain PASS");

        return fail;
    }
}
