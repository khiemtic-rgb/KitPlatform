using System.Linq;

namespace KitPlatform.Packs.Content;

public static class FamixaVisualUniverseAuthorityV1Regression
{
    public const string SuiteId = "FAMIXA_VISUAL_UNIVERSE_AUTHORITY_V1_REGRESSION";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var sha = FamixaVisualUniverseAuthorityV1Rules.Sha();
        var cdlSha = FamixaVisualUniverseAuthorityV1Rules.DesignLanguageSha();
        var refSha = FamixaVisualUniverseAuthorityV1Rules.StyleReferencePackSha();
        var calSha = FamixaVisualUniverseAuthorityV1Rules.StyleCalibrationPackSha();
        var pvsV1 = ProjectVisualStyleV1Rules.Sha(ProjectVisualStyleV1Rules.PresetOf("3D_STYLIZED_REALISM")!);
        var style = ProjectVisualStyleV1Rules.BuildPrompt(ProjectVisualStyleV1Rules.PresetOf("3D_STYLIZED_REALISM")!);
        var prefix = FamixaVisualUniverseAuthorityV1Rules.CompileStylePrefix(style);
        var adultMale = FamixaVisualUniverseAuthorityV1Rules.CompileCalibrationPrompt(
            FamixaVisualUniverseAuthorityV1Rules.CalibrationArchetypes[0]);
        var adultFemale = FamixaVisualUniverseAuthorityV1Rules.CompileCalibrationPrompt(
            FamixaVisualUniverseAuthorityV1Rules.CalibrationArchetypes[1]);
        var childBoy = FamixaVisualUniverseAuthorityV1Rules.CompileCalibrationPrompt(
            FamixaVisualUniverseAuthorityV1Rules.CalibrationArchetypes[2]);
        var childGirl = FamixaVisualUniverseAuthorityV1Rules.CompileCalibrationPrompt(
            FamixaVisualUniverseAuthorityV1Rules.CalibrationArchetypes[3]);

        Ok(!string.IsNullOrWhiteSpace(FamixaVisualUniverseAuthorityV1Rules.AuthorityBlock())
            && sha == FamixaVisualUniverseAuthorityV1Rules.Sha()
            && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(sha), "01 Authority compiler");
        Ok(FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(cdlSha)
            && FamixaVisualUniverseAuthorityV1Rules.DesignLanguageBlock().Contains("FACE:", StringComparison.Ordinal)
            && FamixaVisualUniverseAuthorityV1Rules.DesignLanguageBlock().Contains(
                CharacterDesignLanguageV2Rules.PromptBlock, StringComparison.Ordinal)
            && !FamixaVisualUniverseAuthorityV1Rules.ContainsCharacterName(
                FamixaVisualUniverseAuthorityV1Rules.DesignLanguageCanonical())
            && !FamixaVisualUniverseAuthorityV1Rules.ContainsRole(
                FamixaVisualUniverseAuthorityV1Rules.DesignLanguageCanonical()), "02 Design Language compiler");
        Ok(FamixaVisualUniverseAuthorityV1Rules.StyleExemplars.Count == 6
            && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(refSha)
            && FamixaVisualUniverseAuthorityV1Rules.StyleExemplars.All(e =>
                !FamixaVisualUniverseAuthorityV1Rules.ContainsCharacterName(e.Label)),
            "03 Style Reference Pack");
        Ok(FamixaVisualUniverseAuthorityV1Rules.CalibrationArchetypes.Count == 4
            && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(calSha)
            && FamixaVisualUniverseAuthorityV1Rules.CalibrationArchetypes.All(e =>
                !FamixaVisualUniverseAuthorityV1Rules.ContainsCharacterName(e.Code)),
            "04 Calibration Pack");
        Ok(sha == KitVideoIntegrityRules.Sha256Hex(
                System.Text.Encoding.UTF8.GetBytes(FamixaVisualUniverseAuthorityV1Rules.Canonical())),
            "05 Authority SHA");

        var bindOk = FamixaVisualUniverseAuthorityV1Rules.ValidateMasterGeneration(
            FamixaVisualUniverseAuthorityV1Rules.StatusLocked, sha, cdlSha, refSha, calSha, true, true, true);
        var bindDraft = FamixaVisualUniverseAuthorityV1Rules.ValidateMasterGeneration(
            FamixaVisualUniverseAuthorityV1Rules.StatusDraft, sha, cdlSha, refSha, calSha, true, true, true);
        Ok(bindOk is null
            && bindDraft == FamixaVisualUniverseAuthorityV1Rules.GateAuthorityNotLocked
            && FamixaVisualUniverseAuthorityV1Rules.ValidateMasterGeneration(
                FamixaVisualUniverseAuthorityV1Rules.StatusLocked, new string('a', 64), cdlSha, refSha, calSha,
                true, true, true) == FamixaVisualUniverseAuthorityV1Rules.GateAuthorityShaMismatch,
            "06 Character request binding");

        Ok(FamixaVisualUniverseAuthorityV1Rules.PromptHasAuthorityOrder(adultMale)
            && FamixaVisualUniverseAuthorityV1Rules.PromptHasAuthorityOrder(
                FamixaVisualUniverseAuthorityV1Rules.CompileCharacterPrompt(style, "ChronologicalAge: 38.", "identity", "", "FRONT")),
            "07 Prompt ordering");
        Ok(prefix.Contains(FamixaVisualUniverseAuthorityV1Rules.NegativeConstraints, StringComparison.Ordinal)
            && adultMale.Contains("photorealistic human", StringComparison.OrdinalIgnoreCase),
            "08 Global negative constraints");
        Ok(prefix.Contains("LOW_TO_MODERATE", StringComparison.Ordinal)
            && FamixaVisualUniverseAuthorityV1Rules.PromptBlocksAdultPhotorealEscalation(adultMale),
            "09 Realism ceiling");
        Ok(CharacterStyleConformanceV1Rules.StatusWithoutEvaluator()
                == CharacterStyleConformanceV1Rules.StatusNotEvaluated
            && !CharacterStyleConformanceV1Rules.CompilationImpliesPass()
            && !CharacterStyleConformanceV1Rules.FaceEqualsStyle()
            && !CharacterStyleConformanceV1Rules.ReadyAllowed(true, true, true, true,
                CharacterStyleConformanceV1Rules.StatusNotEvaluated, true, true)
            && CharacterStyleConformanceV1Rules.ReadyAllowed(true, true, true, true,
                CharacterStyleConformanceV1Rules.StatusPass, true, true),
            "10 Style Conformance");
        Ok(!FamixaVisualUniverseAuthorityV1Rules.AgeChangesVisualLanguage()
            && FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(adultMale, childBoy)
            && FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(adultMale, adultFemale),
            "11 Age independence");
        Ok(!FamixaVisualUniverseAuthorityV1Rules.GenderChangesVisualLanguage()
            && FamixaVisualUniverseAuthorityV1Rules.ExtractStylePrefix(adultMale)
                == FamixaVisualUniverseAuthorityV1Rules.ExtractStylePrefix(childGirl),
            "12 Gender independence");
        Ok(!FamixaVisualUniverseAuthorityV1Rules.RoleChangesVisualLanguage()
            && !FamixaVisualUniverseAuthorityV1Rules.ContainsRole(prefix),
            "13 Role independence");
        Ok(FamixaVisualUniverseAuthorityV1Rules.MutationForbidden(true)
            && !FamixaVisualUniverseAuthorityV1Rules.MutatesLockedCharacters()
            && CharacterAuthorityInitializationV1Rules.ProtectedMasterSha
                == "be439c39e067aa6c7727255e9643ac78cb7c6285917af60dda38bf14a32518f1"
            && pvsV1 == ProjectVisualStyleV2Rules.ProtectedV1Sha,
            "14 Locked character protection");

        var age = CharacterAgeConsistencyV1Rules.FromCanonicalAge(38);
        var appearance = CharacterAppearanceProfileV1Rules.Compile(
            new CharacterAppearanceProfileV1Rules.AppearanceSource(38, "male", "role", "calm", "desc", null, pvsV1));
        var identity = CharacterStudioV1Rules.IdentityBrief(
            "CHAR-099", "Lan", 38, "male", "STYLE_3D_STYLIZED_REALISM", "adult");
        var rev = new CharacterMasterRevisionV1Rules.RevisionSource(
            true, "CHAR-099", true, "MASTER-V1", new string('a', 64),
            appearance, age, new string('b', 64), identity, pvsV1, true,
            CharacterMasterRevisionV1Rules.ReasonStyle, "notes", true, "GEMINI", false, false, null);
        var req = CharacterMasterRevisionV1Rules.BuildGenerationRequest(rev);
        Ok(req.CanonicalText.Contains("[FAMIXA VISUAL UNIVERSE AUTHORITY V1]", StringComparison.Ordinal)
            && req.VisualUniverseAuthoritySha == sha
            && CharacterMasterRevisionV1Rules.PromptForbidsExactMasterFace(req.CanonicalText, rev.RevisionReason),
            "15 Master revision integration");

        var view = CharacterAgeGenerationIntegrationV1Rules.ComposeViewPrompt(
            CharacterAgeGenerationIntegrationV1Rules.BuildRequest(
                "CHAR-099", "ERA-01", pvsV1, style,
                CharacterStudioV1Rules.IdentityBrief("CHAR-099", "Lan", 38, "male", "STYLE_3D_STYLIZED_REALISM", "desc"),
                new string('b', 64), new string('b', 64), new string('b', 64), new string('b', 64), age, "male"),
            "FRONT");
        Ok(view.Contains("[FAMIXA VISUAL UNIVERSE AUTHORITY V1]", StringComparison.Ordinal)
            && CharacterAgeGenerationIntegrationV1Rules.PromptContainsAge(view,
                CharacterAgeGenerationIntegrationV1Rules.BuildRequest(
                    "CHAR-099", "ERA-01", pvsV1, style,
                    CharacterStudioV1Rules.IdentityBrief("CHAR-099", "Lan", 38, "male", "STYLE_3D_STYLIZED_REALISM", "desc"),
                    new string('b', 64), new string('b', 64), new string('b', 64), new string('b', 64), age, "male")),
            "16 CRP integration");

        Ok(FamixaVisualUniverseAuthorityV1Rules.MasterGenerationBlocked(FamixaVisualUniverseAuthorityV1Rules.StatusDraft)
            && !FamixaVisualUniverseAuthorityV1Rules.CallsGemini()
            && FamixaVisualUniverseAuthorityV1Rules.EvaluateCalibrate(
                FamixaVisualUniverseAuthorityV1Rules.StatusCalibrationPending, false)
                == FamixaVisualUniverseAuthorityV1Rules.GateConfirmation,
            "17 Provider blocking");
        Ok(FamixaVisualUniverseAuthorityV1Rules.ToDto().DocumentId == FamixaVisualUniverseAuthorityV1Rules.DocumentId
            && FamixaVisualUniverseAuthorityV1Rules.ToCalibrationDto().Slots.Count == 4,
            "18 API regression");
        Ok(!FamixaVisualUniverseAuthorityV1Rules.ReplacesProjectVisualStyleV1()
            && FamixaVisualUniverseAuthorityV1Rules.CurrentPvsRemainsAuthority(FamixaVisualUniverseAuthorityV1Rules.StatusDraft)
            && !FamixaVisualUniverseAuthorityV1Rules.DraftIsProductionAuthority(),
            "19 PVS V1 remains until lock");
        Ok(!FamixaVisualUniverseAuthorityV1Rules.AutoApprove()
            && !FamixaVisualUniverseAuthorityV1Rules.AutoLock()
            && !FamixaVisualUniverseAuthorityV1Rules.CreatesPixels(),
            "20 No Gemini / auto during compile");

        Ok(FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(adultMale, adultFemale)
            && FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(adultMale, childBoy)
            && FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(adultMale, childGirl)
            && adultMale != adultFemale,
            "21 Cross-archetype style identical");

        var prompts = new[] { "Nam", "Linh", "Minh", "An", "Thảo" }.Select(name =>
            FamixaVisualUniverseAuthorityV1Rules.CompileCharacterPrompt(
                style,
                "ChronologicalAge: 11.",
                CharacterStudioV1Rules.IdentityBrief("CHAR-099", name, 11, "female", "STYLE_3D_STYLIZED_REALISM", "desc"),
                "",
                "FRONT")).ToList();
        Ok(prompts.All(p => FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(p, prompts[0]))
            && prompts.Select(p => p.Contains("Role:", StringComparison.Ordinal)).Distinct().Count() >= 1,
            "22 Named-character prompts share style prefix");

        var p38 = FamixaVisualUniverseAuthorityV1Rules.CompileCharacterPrompt(style, "ChronologicalAge: 38.", "adult male identity", "", "");
        var p36 = FamixaVisualUniverseAuthorityV1Rules.CompileCharacterPrompt(style, "ChronologicalAge: 36.", "adult female identity", "", "");
        var p27 = FamixaVisualUniverseAuthorityV1Rules.CompileCharacterPrompt(style, "ChronologicalAge: 27.", "young adult identity", "", "");
        Ok(FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(p38, p36)
            && FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(p38, p27)
            && FamixaVisualUniverseAuthorityV1Rules.PromptBlocksAdultPhotorealEscalation(p38)
            && FamixaVisualUniverseAuthorityV1Rules.PromptBlocksAdultPhotorealEscalation(p27),
            "23 Adult age does not escalate photorealism");

        Ok(!FamixaVisualUniverseAuthorityV1Rules.ContainsCharacterName(FamixaVisualUniverseAuthorityV1Rules.Canonical())
            && !FamixaVisualUniverseAuthorityV1Rules.ContainsCharacterName(prefix)
            && !FamixaVisualUniverseAuthorityV1Rules.UsesCharacterName(),
            "24 No character-name branching");

        return fail;
    }
}
