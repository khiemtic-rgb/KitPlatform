using System.Linq;

namespace KitPlatform.Packs.Content;

public static class VisualCalibrationPackV1Regression
{
    public const string SuiteId = VisualCalibrationPackV1Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var style = ProjectVisualStyleV1Rules.BuildPrompt(ProjectVisualStyleV1Rules.PresetOf("3D_STYLIZED_REALISM")!);
        var pack = VisualCalibrationPackV1Rules.CompilePack(null, style);
        var child = VisualCalibrationPackV1Rules.DefaultMatrix[0];
        var adult = VisualCalibrationPackV1Rules.DefaultMatrix[2];
        var older = VisualCalibrationPackV1Rules.DefaultMatrix[4];
        var views = VisualCalibrationPackV1Rules.CompileSubjectViews(adult, style);

        Ok(pack.PackId == VisualCalibrationPackV1Rules.DefaultPackId
            && pack.Status == VisualCalibrationPackV1Rules.StatusDraft, "CASE-001 Creates calibration pack");
        Ok(pack.Subjects.Select(s => pack.VisualUniverseSha).Distinct().Count() == 1
            && pack.VisualUniverseSha == FamixaVisualUniverseAuthorityV1Rules.Sha(),
            "CASE-002 Uses one Visual Universe");
        Ok(pack.Subjects.All(s => s.Artifacts.All(a =>
                a.Prompt.Contains(style.Split(' ')[0], StringComparison.Ordinal)
                || a.Prompt.Contains("[FAMIXA VISUAL UNIVERSE AUTHORITY V1]", StringComparison.Ordinal)))
            && VisualCalibrationConsistencyV1Rules.SamePvs(pack),
            "CASE-003 All subjects inherit same PVS");
        Ok(VisualCalibrationConsistencyV1Rules.SameCdl(pack)
            && pack.CharacterDesignLanguageSha == CharacterDesignLanguageV2Rules.Sha(),
            "CASE-004 All subjects inherit same CDL");
        Ok(VisualCalibrationConsistencyV1Rules.SameVisualUniverse(pack)
            && pack.Subjects.All(s => s.Artifacts.All(a =>
                a.Prompt.Contains("[FAMIXA VISUAL UNIVERSE AUTHORITY V1]", StringComparison.Ordinal))),
            "CASE-005 All subjects inherit same VUA");
        Ok(child.ChronologicalAge == 11 && child.TargetAppearanceAgeMin == 10
            && adult.ChronologicalAge == 35 && adult.TargetAppearanceAgeMin == 32
            && older.ChronologicalAge == 65 && older.TargetAppearanceAgeMin == 61
            && older.TargetAppearanceAgeMax == 69, "CASE-006 Age profiles differ correctly");
        Ok(VisualCalibrationPackV1Rules.AgeDoesNotChangeStyle(pack)
            && FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(
                views["FRONT"], VisualCalibrationPackV1Rules.CompilePrompt(child, "FRONT", style)),
            "CASE-007 Age does not change visual style");
        Ok(VisualCalibrationPackV1Rules.GenderDoesNotChangeStyle(pack)
            && FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(
                VisualCalibrationPackV1Rules.CompilePrompt(VisualCalibrationPackV1Rules.DefaultMatrix[1], "FRONT", style),
                VisualCalibrationPackV1Rules.CompilePrompt(adult, "FRONT", style)),
            "CASE-008 Gender does not change visual style");
        Ok(VisualCalibrationPackV1Rules.ViewsDifferOnlyByCamera(views)
            && VisualCalibrationPackV1Rules.ViewsShareVisualUniverse(views)
            && VisualCalibrationPackV1Rules.ViewsShareAgeProfile(views, adult),
            "CASE-009 View prompts differ only by camera");
        Ok(VisualCalibrationPackV1Rules.ValidateGenerate(null, pack.ProjectVisualStyleSha,
                pack.CharacterDesignLanguageSha, true)
            == VisualCalibrationPackV1Rules.GateUniverseNotReady, "CASE-010 Missing VUA blocks generation");
        Ok(VisualCalibrationPackV1Rules.ValidateGenerate(pack.VisualUniverseSha, null,
                pack.CharacterDesignLanguageSha, true)
            == VisualCalibrationPackV1Rules.GatePvsNotReady, "CASE-011 Missing PVS blocks generation");
        Ok(VisualCalibrationPackV1Rules.ValidateGenerate(pack.VisualUniverseSha, pack.ProjectVisualStyleSha,
                null, true)
            == VisualCalibrationPackV1Rules.GateCdlNotReady, "CASE-012 Missing CDL blocks generation");
        Ok(VisualCalibrationPackV1Rules.ValidateGenerate(pack.VisualUniverseSha, pack.ProjectVisualStyleSha,
                pack.CharacterDesignLanguageSha, false)
            == VisualCalibrationPackV1Rules.GateConfirmation, "CASE-013 confirm=false blocks provider");
        Ok(!VisualCalibrationPackV1Rules.AutoApprove()
            && VisualCalibrationPackV1Rules.EvaluateApprove(pack, false, VisualCalibrationPackV1Rules.PhotorealismLow)
                == VisualCalibrationPackV1Rules.GateInvalidState, "CASE-014 No auto-approve");
        Ok(!VisualCalibrationPackV1Rules.AutoLock()
            && VisualCalibrationPackV1Rules.EvaluateLock(VisualCalibrationPackV1Rules.StatusApproved, false, false)
                == VisualCalibrationPackV1Rules.GateConfirmation, "CASE-015 No auto-lock");
        var rejected = VisualCalibrationPackV1Rules.Reject(pack, "Adult too photorealistic.");
        Ok(rejected.Status == VisualCalibrationPackV1Rules.StatusRejected
            && rejected.CurrentAuthority == pack.CurrentAuthority
            && rejected.VisualUniverseSha == pack.VisualUniverseSha, "CASE-016 Reject preserves authority");
        var approved = VisualCalibrationPackV1Rules.Approve(pack);
        Ok(approved.Status == VisualCalibrationPackV1Rules.StatusApproved
            && !approved.CurrentAuthority
            && VisualCalibrationPackV1Rules.EvaluateLock(approved.Status, true, false) is null,
            "CASE-017 Approve does not automatically lock");
        var locked = VisualCalibrationPackV1Rules.Lock(approved);
        Ok(locked.Status == VisualCalibrationPackV1Rules.StatusLocked
            && !locked.AuthorityTransitioned
            && !locked.CurrentAuthority
            && VisualCalibrationPackV1Rules.EvaluateLock(VisualCalibrationPackV1Rules.StatusDraft, true, false)
                == VisualCalibrationPackV1Rules.GateInvalidState, "CASE-018 Lock is explicit and does not pretend VUA switched");
        Ok(VisualCalibrationPackV1Rules.EvaluateLock(VisualCalibrationPackV1Rules.StatusApproved, true, true)
                == VisualCalibrationPackV1Rules.GateLockedImmutable
            && !VisualCalibrationPackV1Rules.ReplacesVisualUniverse(),
            "CASE-019 Locked VUA is immutable");
        Ok(CharacterAuthorityInitializationV1Rules.ProtectedMasterSha
                == "be439c39e067aa6c7727255e9643ac78cb7c6285917af60dda38bf14a32518f1"
            && CharacterAuthorityInitializationV1Rules.ProtectedDnaSha
                == "75ececad8899211ce31107232fe0288c11a9e113c5bc0e7c0a6c9f749d72f4dc"
            && CharacterAuthorityInitializationV1Rules.ProtectedPrpSha
                == "5e61ad240aaebaa13dcd91463a41ef9f9c0498fabefe86b7e8b1a1ad973a9444"
            && CharacterAuthorityInitializationV1Rules.ProtectedCrpSha
                == "82543a4a4331e32a79a865fc3881c17e8bc3dc74c5dec51c52c2deab1a70c2b7"
            && !VisualCalibrationPackV1Rules.MutatesLockedCharacters(),
            "CASE-020 Minh remains unchanged");
        Ok(!VisualCalibrationPackV1Rules.CreatesCharacterMaster(), "CASE-021 No real Character Master is created");
        Ok(!VisualCalibrationPackV1Rules.CreatesDna(), "CASE-022 No DNA is created");
        Ok(!VisualCalibrationPackV1Rules.CreatesPrp(), "CASE-023 No PRP is created");
        Ok(!VisualCalibrationPackV1Rules.CreatesCrp(), "CASE-024 No CRP is created");
        Ok(!VisualCalibrationPackV1Rules.CreatesVideo(), "CASE-025 No video is created");
        var reviewReady = VisualCalibrationPackV1Rules.WithPixelCoverage(
            pack with { Status = VisualCalibrationPackV1Rules.StatusPendingReview }, 24);
        Ok(VisualCalibrationPhotorealismGateV1.BlocksApproval(VisualCalibrationPackV1Rules.PhotorealismHigh)
            && VisualCalibrationPackV1Rules.EvaluateApprove(reviewReady, true, VisualCalibrationPackV1Rules.PhotorealismHigh)
                == VisualCalibrationPackV1Rules.GatePhotorealism, "CASE-026 Photorealism violation blocks approval");
        var broken = reviewReady with
        {
            Subjects = reviewReady.Subjects.Select((s, i) => i == 0
                ? s with
                {
                    Artifacts = s.Artifacts.Select(a => a with { Prompt = "photorealistic human portrait only" }).ToList(),
                }
                : s).ToList(),
        };
        Ok(VisualCalibrationConsistencyV1Rules.Validate(broken)
                == VisualCalibrationPackV1Rules.GateUniverseMismatch
            && VisualCalibrationPackV1Rules.EvaluateApprove(broken, true, VisualCalibrationPackV1Rules.PhotorealismLow)
                == VisualCalibrationPackV1Rules.GateUniverseMismatch, "CASE-027 Visual universe mismatch blocks approval");
        Ok(!VisualCalibrationPackV1Rules.AgeEqualsStyle()
            && CharacterAgeConsistencyV1Rules.ValidateProfile(11, 25, 20) is not null
            && VisualCalibrationPackV1Rules.SameUniverse(pack), "CASE-028 Age failure does not imply style failure");
        Ok(!VisualCalibrationPackV1Rules.FaceEqualsUniverse()
            && VisualCalibrationConsistencyV1Rules.Validate(broken) is not null, "CASE-029 Style failure does not imply face failure");
        Ok(pack.Subjects.Count == 6
            && pack.Subjects.SelectMany(s => s.Artifacts).Count() == 24
            && pack.Subjects.All(s => s.CalibrationSubjectId.StartsWith("CAL-", StringComparison.Ordinal))
            && pack.Subjects.All(s => !VisualCalibrationPackV1Rules.ContainsCharacterName(s.Label))
            && !VisualCalibrationPackV1Rules.ContainsCharacterName(views["FRONT"])
            && !VisualCalibrationPackV1Rules.UsesCharacterName(),
            "CASE-030 All six archetypes belong to one calibration pack");

        Ok(!VisualCalibrationPackV1Rules.CallsGemini()
            && !VisualCalibrationPackV1Rules.CreatesPixels()
            && VisualCalibrationPackV1Rules.ToDto(pack).ProviderCalled == false
            && VisualCalibrationPackV1Rules.MatrixOf(null).Count == 6
            && VisualCalibrationPackV1Rules.MatrixOf([child]).Count == 1,
            "CASE-031 Dry compile / matrix is data");

        return fail;
    }
}
