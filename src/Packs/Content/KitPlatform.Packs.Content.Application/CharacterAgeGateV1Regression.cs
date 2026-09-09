using System.Linq;

namespace KitPlatform.Packs.Content;

public static class CharacterAgeGateV1Regression
{
    public const string SuiteId = CharacterAgeGateV1Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var style = ProjectVisualStyleV1Rules.PresetOf("3D_STYLIZED_REALISM")!;
        var styleSha = ProjectVisualStyleV1Rules.Sha(style);
        var stylePrompt = ProjectVisualStyleV1Rules.BuildPrompt(style);
        var sha = new string('b', 64);
        var minhMaster = CharacterAuthorityInitializationV1Rules.ProtectedMasterSha;
        var minhDna = CharacterAuthorityInitializationV1Rules.ProtectedDnaSha;
        var minhPrp = CharacterAuthorityInitializationV1Rules.ProtectedPrpSha;
        var minhCrp = CharacterAuthorityInitializationV1Rules.ProtectedCrpSha;
        var evaluator = new DeferredCharacterAgeConsistencyEvaluator();

        var age11 = CharacterAgeConsistencyV1Rules.FromCanonicalAge(11);
        var age27 = CharacterAgeConsistencyV1Rules.FromCanonicalAge(27);
        var age36 = CharacterAgeConsistencyV1Rules.FromCanonicalAge(36);
        var age38 = CharacterAgeConsistencyV1Rules.FromCanonicalAge(38);
        var profile11 = CharacterAgeConsistencyV1Rules.AgeAppearanceProfileText(age11, "male");
        var profile27 = CharacterAgeConsistencyV1Rules.AgeAppearanceProfileText(age27, "female");
        var profile38 = CharacterAgeConsistencyV1Rules.AgeAppearanceProfileText(age38, "male");

        Ok(CharacterAgeGateV1Rules.AgeProfileReady(27, 25, 29, profile27)
            && CharacterAgeGateV1Rules.AgeInstructionValid(11, 10, 12, profile11),
            "01 valid age profile");

        Ok(CharacterAgeGateV1Rules.ValidateProfile(null, 10, 12, profile11)
                == CharacterAgeGateV1Rules.GateProfileNotReady
            && CharacterAgeGateV1Rules.ValidateProfile(11, 10, 12, "")
                == CharacterAgeGateV1Rules.GateProfileNotReady
            && evaluator.Evaluate(new AgeConsistencyEvaluationRequest(
                "CHAR-099", null, 10, 12, null, 4)).Status
                == CharacterAgeGateV1Rules.StatusBlocked,
            "02 missing age profile → BLOCKED AGE_PROFILE_NOT_READY");

        Ok(CharacterAgeGateV1Rules.ValidateChronologicalAge(0)
                == CharacterAgeGateV1Rules.GateInvalidChronological
            && CharacterAgeGateV1Rules.ValidateChronologicalAge(200)
                == CharacterAgeGateV1Rules.GateInvalidChronological,
            "03 invalid chronological age → INVALID_CHRONOLOGICAL_AGE");

        Ok(CharacterAgeGateV1Rules.ValidateTargetRange(29, 25)
                == CharacterAgeGateV1Rules.GateInvalidTargetRange,
            "04 invalid target range → INVALID_TARGET_AGE_RANGE");

        var incomplete = evaluator.Evaluate(new AgeConsistencyEvaluationRequest(
            "CHAR-099", 27, 25, 29, profile27, 0));
        var waiting = CharacterAgeGateV1Rules.Resolve(age27, profile27, 0);
        Ok(CharacterAgeGateV1Rules.ValidateEvaluationInput(27, 25, 29, profile27, 0)
                == CharacterAgeGateV1Rules.GateCrpIncomplete
            && incomplete.Status == CharacterAgeGateV1Rules.StatusBlocked
            && incomplete.Code == CharacterAgeGateV1Rules.GateCrpIncomplete
            && waiting.Status == CharacterAgeGateV1Rules.StatusNotEvaluated
            && !waiting.Pass,
            "05 incomplete CRP → evaluation BLOCKED; Studio display stays NOT_EVALUATED");

        var promptReady = CharacterAgeGateV1Rules.FromPrompt(true);
        Ok(promptReady.Status == CharacterAgeGateV1Rules.StatusNotEvaluated
            && !promptReady.Pass
            && CharacterAgeGateV1Rules.PromptDoesNotImplyPass(
                "AgeAppearanceProfile: " + profile27, age27, "female")
            && !CharacterAgeGateV1Rules.AgePass(CharacterAgeGateV1Rules.StatusNotEvaluated),
            "06 NOT_EVALUATED cannot become PASS because the prompt is correct");

        Ok(!CharacterAgeGateV1Rules.MayBecomeReady(false, CharacterAgeGateV1Rules.StatusFail)
            && !CharacterAgeGateV1Rules.CharacterReadyAllowed(
                true, true, true, true, true, true, true, true, false, true, true),
            "07 AGE FAIL blocks readiness");

        Ok(!CharacterAgeGateV1Rules.ReadyAllowed(false, true, false, true, false, false)
            && !CharacterAgeGateV1Rules.CharacterReadyAllowed(
                true, true, true, true, true, true, false, false, true, false, false),
            "08 AGE PASS alone does not make character ready");

        Ok(!CharacterAgeGateV1Rules.ReadyAllowed(true, false, true, true, true, true),
            "09 FACE PASS + AGE FAIL → NOT READY");

        Ok(!CharacterAgeGateV1Rules.ReadyAllowed(false, true, true, true, true, true),
            "10 FACE FAIL + AGE PASS → NOT READY");

        Ok(!CharacterAgeGateV1Rules.ReadyAllowed(true, true, false, true, true, true),
            "11 AGE PASS + FACE PASS + STYLE FAIL → NOT READY");

        var identity = CharacterStudioV1Rules.IdentityBrief(
            "CHAR-099", "Lan", 27, "female", "STYLE_3D_STYLIZED_REALISM", "canonical");
        var request = CharacterAgeGenerationIntegrationV1Rules.BuildRequest(
            "CHAR-099", "ERA-01", styleSha, stylePrompt, identity, sha, sha, sha, sha, age27, "female");
        var prompts = CharacterAgeGenerationIntegrationV1Rules.RequiredViews.ToDictionary(
            v => v,
            v => CharacterAgeGenerationIntegrationV1Rules.ComposeViewPrompt(request, v),
            StringComparer.OrdinalIgnoreCase);
        Ok(CharacterAgeGenerationIntegrationV1Rules.ViewsShareAgeProfile(prompts, request)
            && CharacterAgeGenerationIntegrationV1Rules.ViewsDifferOnlyByCamera(prompts),
            "12 all views share the same AgeAppearanceProfile");

        Ok(!CharacterAgeGateV1Rules.UsesCharacterNameOrId(profile27, "Thảo", "CHAR-006", "mother")
            && !CharacterAgeGateV1Rules.UsesCharacterNameOrId(profile11, "Minh", "CHAR-001", "son")
            && !CharacterAgeGateV1Rules.UsesCharacterNameOrId(profile38, "Nam", "CHAR-002", "father")
            && !CharacterAgeGateV1Rules.UsesCharacterSpecificBranch()
            && profile27.Contains("Young Vietnamese adult woman", StringComparison.Ordinal)
            && profile27.Contains("late twenties", StringComparison.OrdinalIgnoreCase)
            && profile11.Contains("Vietnamese boy", StringComparison.Ordinal)
            && profile11.Contains("clearly a child", StringComparison.OrdinalIgnoreCase)
            && profile11.Contains("11-year-old", StringComparison.Ordinal)
            && profile38.Contains("Vietnamese adult man", StringComparison.Ordinal)
            && profile38.Contains("late thirties", StringComparison.OrdinalIgnoreCase)
            && age11.TargetAppearanceAgeMin == 10 && age11.TargetAppearanceAgeMax == 12
            && age27.TargetAppearanceAgeMin == 25 && age27.TargetAppearanceAgeMax == 29
            && age36.TargetAppearanceAgeMin == 33 && age36.TargetAppearanceAgeMax == 39
            && age38.TargetAppearanceAgeMin == 35 && age38.TargetAppearanceAgeMax == 41,
            "13 age profile is character-name independent");

        Ok(CharacterStudioUnifiedGenerationV1Rules.RegenerationKeepsAuthority(
                sha, sha, minhMaster, minhMaster, minhDna, minhDna, minhPrp, minhPrp, styleSha, styleSha)
            && !CharacterAgeGateV1Rules.AutoRegenerate(),
            "14 regeneration preserves authority SHA");

        Ok(CharacterStudioV1Rules.ProtectedMinhUnchanged(minhMaster, minhDna, minhPrp, minhCrp)
            && CharacterAgeConsistencyV1Rules.LockedBlocksRegeneration(true, true)
            && CharacterAgeGateV1Rules.MayBecomeReady(true, CharacterAgeGateV1Rules.StatusNotEvaluated)
            && CharacterStudioV1Rules.ResolveState(new CharacterStudioV1Rules.StudioSnapshot(
                true, true, true, false, "LOCKED", "LOCKED", "LOCKED",
                CharacterStudioV1Rules.Rejected, 4, false, false, true, true))
                == CharacterStudioV1Rules.Rejected
            && CharacterStudioV1Rules.ResolveState(new CharacterStudioV1Rules.StudioSnapshot(
                true, true, true, false, "LOCKED", "LOCKED", "LOCKED",
                CharacterStudioV1Rules.CrpPendingReview, 4, false, false, false, true))
                == CharacterStudioV1Rules.CrpPendingReview
            && CharacterStudioV1Rules.ResolveState(new CharacterStudioV1Rules.StudioSnapshot(
                true, true, true, false, "LOCKED", "LOCKED", "LOCKED",
                null, 0, false, false, false, false))
                == CharacterStudioV1Rules.PrpReady,
            "15 existing locked / rejected / pending / PRP-ready characters unchanged");

        var deferred = evaluator.Evaluate(new AgeConsistencyEvaluationRequest(
            "CHAR-099", 27, 25, 29, profile27, 4));
        Ok(!CharacterAgeGateV1Rules.CallsGemini()
            && !CharacterAgeGateV1Rules.InventsApparentAge()
            && deferred.Status == CharacterAgeGateV1Rules.StatusNotEvaluated
            && !deferred.Pass
            && deferred.Evaluator == CharacterAgeGateV1Rules.EvaluatorDeferred
            && typeof(ICharacterAgeConsistencyEvaluator).IsAssignableFrom(
                typeof(DeferredCharacterAgeConsistencyEvaluator)),
            "16 no Gemini call during regression / MVP evaluator does not PASS");

        Ok(CharacterAgeGateV1Rules.ReadyAllowed(true, true, true, true, true, true)
            && !CharacterAgeGateV1Rules.MayBecomeReady(false, CharacterAgeGateV1Rules.StatusNotEvaluated)
            && !CharacterAgeGateV1Rules.MayBecomeReady(false, CharacterAgeGateV1Rules.StatusBlocked)
            && CharacterAgeGateV1Rules.VisualAgeReason(25, 29, 32, 36)
                == CharacterAgeGateV1Rules.ReasonOlder
            && CharacterAgeGateV1Rules.VisualAgeReason(25, 29, 18, 20)
                == CharacterAgeGateV1Rules.ReasonYounger
            && !CharacterAgeGateV1Rules.AutoApprove()
            && !CharacterAgeGateV1Rules.AutoLock()
            && !CharacterAgeGateV1Rules.GeneratesVideo()
            && !CharacterAgeGateV1Rules.ChangesProjectVisualStyle(),
            "17 readiness requires Age PASS; FAIL reasons stay visual; no auto actions");

        return fail;
    }
}
