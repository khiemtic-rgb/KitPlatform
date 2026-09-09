using System.Linq;

namespace KitPlatform.Packs.Content;

public static class VisualCalibrationPackV1LiveGenerationReadinessRegression
{
    public const string SuiteId = VisualCalibrationPackV1Rules.LiveGenerationSuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var pack = VisualCalibrationPackV1Rules.CompilePack();
        var plan = VisualCalibrationGenerationServiceV1.CompilePlan(pack, "CAL-EXEC-REG");
        var bySubject = plan.GroupBy(r => r.SubjectId).ToList();
        var views = VisualCalibrationPackV1Rules.CompileSubjectViews(
            VisualCalibrationPackV1Rules.DefaultMatrix[2]);
        var stylePrefix = FamixaVisualUniverseAuthorityV1Rules.ExtractStylePrefix(plan[0].CompiledPrompt);
        var vuaBlock = FamixaVisualUniverseAuthorityV1Rules.AuthorityBlock();
        var cdlBlock = FamixaVisualUniverseAuthorityV1Rules.DesignLanguageBlock();

        Ok(VisualCalibrationPackV1Rules.DefaultMatrix.Count == 6
            && VisualCalibrationPackV1Rules.RequiredViews.Count == 4
            && plan.Count == 24
            && pack.Subjects.SelectMany(s => s.Artifacts).Count() == 24,
            "R-01 6 subjects × 4 views = 24");
        Ok(plan.Select(r => r.VisualUniverseSha).Distinct().Count() == 1
            && plan.All(r => r.VisualUniverseSha == pack.VisualUniverseSha),
            "R-02 all requests share same VUA SHA");
        Ok(plan.Select(r => r.ProjectVisualStyleSha).Distinct().Count() == 1
            && plan.All(r => r.ProjectVisualStyleSha == pack.ProjectVisualStyleSha),
            "R-03 all requests share same PVS SHA");
        Ok(plan.Select(r => r.CharacterDesignLanguageSha).Distinct().Count() == 1
            && plan.All(r => r.CharacterDesignLanguageSha == pack.CharacterDesignLanguageSha),
            "R-04 all requests share same CDL SHA");
        Ok(plan.Select(r => r.SlotId).Distinct().Count() == 24
            && plan.All(r => r.SlotId == VisualCalibrationPackV1Rules.SlotIdOf(r.SubjectId, r.ViewType)),
            "R-05 all SlotId unique");
        Ok(bySubject.Count == 6
            && bySubject.All(g => VisualCalibrationPackV1Rules.RequiredViews.All(v =>
                g.Any(r => string.Equals(r.ViewType, v, StringComparison.OrdinalIgnoreCase)))),
            "R-06 all four views exist for every subject");
        Ok(bySubject.All(g => VisualCalibrationPackV1Rules.ViewsDifferOnlyByCamera(
                g.ToDictionary(r => r.ViewType, r => r.CompiledPrompt, StringComparer.OrdinalIgnoreCase)))
            && VisualCalibrationPackV1Rules.ViewsDifferOnlyByCamera(views),
            "R-07 view changes only camera/composition");
        Ok(plan.All(r => FamixaVisualUniverseAuthorityV1Rules.ExtractStylePrefix(r.CompiledPrompt) == stylePrefix),
            "R-08 style block identical across views");
        Ok(plan.All(r => r.CompiledPrompt.Contains(cdlBlock, StringComparison.Ordinal)),
            "R-09 CDL block identical across views");
        Ok(plan.All(r => r.CompiledPrompt.Contains(vuaBlock, StringComparison.Ordinal)),
            "R-10 VUA block identical across views");

        var blockedMock = new MockVisualCalibrationGenerationProvider();
        var blocked = VisualCalibrationGenerationServiceV1.ExecuteAsync(
            pack, blockedMock, false, true).GetAwaiter().GetResult();
        Ok(blocked.GateCode == VisualCalibrationPackV1Rules.GateConfirmation
            && !blocked.ProviderCalled
            && !blocked.GenerationExecuted
            && blockedMock.CallCount == 0,
            "R-11 confirm=false blocks provider");
        Ok(VisualCalibrationPackV1Rules.GetCoverage(blocked.Pack).Valid == 0
            && blocked.Pack.Subjects.SelectMany(s => s.Artifacts).All(a =>
                !string.Equals(a.Kind, VisualCalibrationPackV1Rules.KindPixel, StringComparison.OrdinalIgnoreCase)),
            "R-12 confirm=false creates no PIXEL artifact");

        var partialMock = new MockVisualCalibrationGenerationProvider { SucceedUpTo = 23 };
        var partial = VisualCalibrationGenerationServiceV1.ExecuteAsync(
            pack, partialMock, true, true).GetAwaiter().GetResult();
        Ok(VisualCalibrationPackV1Rules.GetCoverage(partial.Pack).Valid == 23
            && partial.Pack.Status != VisualCalibrationPackV1Rules.StatusPendingReview
            && partial.GateCode == VisualCalibrationPackV1Rules.GateIncomplete,
            "R-13 incomplete coverage cannot reach PENDING_REVIEW");

        var fullMock = new MockVisualCalibrationGenerationProvider();
        var full = VisualCalibrationGenerationServiceV1.ExecuteAsync(
            pack, fullMock, true, true).GetAwaiter().GetResult();
        Ok(VisualCalibrationPackV1Rules.GetCoverage(full.Pack) is { Valid: 24, Required: 24, Missing: 0 }
            && full.Pack.Status == VisualCalibrationPackV1Rules.StatusPendingReview
            && VisualCalibrationPackV1Rules.EvaluateApprove(
                full.Pack, true, VisualCalibrationPackV1Rules.PhotorealismLow) is null,
            "R-14 24/24 valid coverage can reach PENDING_REVIEW");

        var promptOnly = VisualCalibrationPackV1Rules.WithPixelCoverage(
            pack with { Status = VisualCalibrationPackV1Rules.StatusPendingReview },
            24, VisualCalibrationPackV1Rules.KindPrompt);
        Ok(VisualCalibrationPackV1Rules.GetCoverage(promptOnly).Valid == 0
            && VisualCalibrationPackV1Rules.EvaluateApprove(
                promptOnly, true, VisualCalibrationPackV1Rules.PhotorealismLow)
                == VisualCalibrationPackV1Rules.GatePixelsNotReady,
            "R-15 PROMPT artifact does not count");

        var wrongSha = VisualCalibrationPackV1Rules.WithPixelCoverage(
            pack with { Status = VisualCalibrationPackV1Rules.StatusPendingReview }, 24);
        wrongSha = wrongSha with
        {
            Subjects = wrongSha.Subjects.Select((s, i) => i == 0
                ? s with
                {
                    Artifacts = s.Artifacts.Select((a, n) => n == 0
                        ? a with { VisualUniverseSha = new string('0', 64) }
                        : a).ToList(),
                }
                : s).ToList(),
        };
        Ok(VisualCalibrationPackV1Rules.GetCoverage(wrongSha).Valid == 23, "R-16 wrong SHA does not count");

        var wrongPack = VisualCalibrationPackV1Rules.WithPixelCoverage(
            pack with { Status = VisualCalibrationPackV1Rules.StatusPendingReview }, 24);
        wrongPack = wrongPack with
        {
            Subjects = wrongPack.Subjects.Select((s, i) => i == 0
                ? s with
                {
                    Artifacts = s.Artifacts.Select((a, n) => n == 0
                        ? a with { PackId = "OTHER-PACK" }
                        : a).ToList(),
                }
                : s).ToList(),
        };
        Ok(VisualCalibrationPackV1Rules.GetCoverage(wrongPack).Valid == 23, "R-17 wrong pack does not count");

        var wrongSlot = VisualCalibrationPackV1Rules.WithPixelCoverage(
            pack with { Status = VisualCalibrationPackV1Rules.StatusPendingReview }, 24);
        wrongSlot = wrongSlot with
        {
            Subjects = wrongSlot.Subjects.Select((s, i) => i == 0
                ? s with
                {
                    Artifacts = s.Artifacts.Select((a, n) => n == 0
                        ? a with { SlotId = "CAL-001/SIDE" }
                        : a).ToList(),
                }
                : s).ToList(),
        };
        Ok(VisualCalibrationPackV1Rules.GetCoverage(wrongSlot).Valid == 23, "R-18 wrong slot does not count");

        var dupMock = new MockVisualCalibrationGenerationProvider();
        var again = VisualCalibrationGenerationServiceV1.ExecuteAsync(
            full.Pack, dupMock, true, true).GetAwaiter().GetResult();
        Ok(again.GateCode == VisualCalibrationPackV1Rules.GateDuplicate
            && dupMock.CallCount == 0
            && again.ProviderCalled == false,
            "R-19 duplicate completed slot blocks regeneration");

        Ok(!VisualCalibrationPackV1Rules.UsesCharacterName()
            && !VisualCalibrationPackV1Rules.ContainsCharacterName(plan[0].CompiledPrompt)
            && plan.All(r => r.SubjectId.StartsWith("CAL-", StringComparison.Ordinal)),
            "R-20 calibration does not mutate Character");
        Ok(!VisualCalibrationPackV1Rules.CreatesCharacterMaster()
            && full.Pack.Subjects.All(s => s.CalibrationSubjectId.StartsWith("CAL-", StringComparison.Ordinal)),
            "R-21 calibration does not mutate Master");
        Ok(!VisualCalibrationPackV1Rules.CreatesDna(), "R-22 calibration does not mutate DNA");
        Ok(!VisualCalibrationPackV1Rules.CreatesPrp(), "R-23 calibration does not mutate PRP");
        Ok(!VisualCalibrationPackV1Rules.CreatesCrp(), "R-24 calibration does not mutate CRP");
        Ok(!VisualCalibrationPackV1Rules.CallsGemini()
            && !VisualCalibrationGenerationServiceV1.CallsGemini()
            && blockedMock.CallCount == 0
            && typeof(IVisualCalibrationGenerationProvider).IsAssignableFrom(typeof(MockVisualCalibrationGenerationProvider))
            && !typeof(VisualCalibrationPackV1Rules).Assembly.GetTypes()
                .Any(t => t.Name is "GeminiImageGenerator" or "GeminiCharacterGenerationProvider"
                    or "ContentGeminiClient"),
            "R-25 no provider call during regression");

        return fail;
    }
}
