using System.Linq;

namespace KitPlatform.Packs.Content;

public static class CharacterAppearanceProfileV1Regression
{
    public const string SuiteId = CharacterAppearanceProfileV1Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var sha = new string('a', 64);
        var style = ProjectVisualStyleV1Rules.PresetOf("3D_STYLIZED_REALISM")!;
        var styleSha = ProjectVisualStyleV1Rules.Sha(style);
        var minhMaster = CharacterAuthorityInitializationV1Rules.ProtectedMasterSha;
        var minhDna = CharacterAuthorityInitializationV1Rules.ProtectedDnaSha;
        var minhPrp = CharacterAuthorityInitializationV1Rules.ProtectedPrpSha;
        var minhCrp = CharacterAuthorityInitializationV1Rules.ProtectedCrpSha;

        CharacterAppearanceProfile Compile(int age, string gender, string? role = null, string? description = null) =>
            CharacterAppearanceProfileV1Rules.Compile(new CharacterAppearanceProfileV1Rules.AppearanceSource(
                age, gender, role, null, description, null, styleSha));

        var age11 = Compile(11, "male");
        var age27 = Compile(27, "female");
        var age36 = Compile(36, "female");
        var age38 = Compile(38, "male", "Bố");
        var age38b = Compile(38, "male", "Bố");
        var age65 = Compile(65, "male");

        Ok(age11.TargetAppearanceAgeMin == 10 && age11.TargetAppearanceAgeMax == 12
            && age11.FacialMaturityProfile.Contains("child", StringComparison.OrdinalIgnoreCase)
            && age11.PositiveAppearanceConstraints.Any(p => p.Contains("boy", StringComparison.OrdinalIgnoreCase))
            && CharacterAppearanceProfileV1Rules.IsReady(age11),
            "01 age 11 → 10–12 child appearance");

        Ok(age27.TargetAppearanceAgeMin == 25 && age27.TargetAppearanceAgeMax == 29
            && age27.FacialMaturityProfile.Contains("clearly adult", StringComparison.OrdinalIgnoreCase)
            && age27.NegativeAppearanceConstraints.Any(n => n.Contains("teenager", StringComparison.OrdinalIgnoreCase))
            && age27.NegativeAppearanceConstraints.Any(n => n.Contains("middle-aged", StringComparison.OrdinalIgnoreCase))
            && age27.PositiveAppearanceConstraints.Any(p => p.Contains("woman", StringComparison.OrdinalIgnoreCase)),
            "02 age 27 → 25–29 young adult woman, not teen / not middle-aged");

        Ok(age36.TargetAppearanceAgeMin == 33 && age36.TargetAppearanceAgeMax == 39
            && age36.FacialMaturityProfile.Contains("clearly adult", StringComparison.OrdinalIgnoreCase)
            && age36.NegativeAppearanceConstraints.Any(n => n.Contains("elderly", StringComparison.OrdinalIgnoreCase)),
            "03 age 36 → 33–39 adult woman, not elderly");

        Ok(age38.TargetAppearanceAgeMin == 35 && age38.TargetAppearanceAgeMax == 41
            && age38.Role == "Bố"
            && age38.LifestyleProfile.Contains("Contemporary Vietnamese", StringComparison.Ordinal)
            && age38.EnergyProfile.Contains("warm", StringComparison.OrdinalIgnoreCase)
            && age38.GroomingProfile.Contains("contemporary", StringComparison.OrdinalIgnoreCase)
            && age38.ClothingProfile.Contains("modern casual", StringComparison.OrdinalIgnoreCase)
            && age38.NegativeAppearanceConstraints.Any(n => n.Contains("elderly", StringComparison.OrdinalIgnoreCase))
            && age38.NegativeAppearanceConstraints.Any(n => n.Contains("55–65", StringComparison.Ordinal))
            && age38.NegativeAppearanceConstraints.Any(n => n.Contains("farmer", StringComparison.OrdinalIgnoreCase))
            && age38.NegativeAppearanceConstraints.Any(n => n.Contains("rural elderly", StringComparison.OrdinalIgnoreCase))
            && age38.NegativeAppearanceConstraints.Any(n => n.Contains("deep age wrinkles", StringComparison.OrdinalIgnoreCase))
            && !CharacterAppearanceProfileV1Rules.RoleSetsAge()
            && !CharacterAppearanceProfileV1Rules.RoleSetsLifestyle()
            && CharacterAppearanceProfileV1Rules.DescriptionOverridesAge(
                new CharacterAppearanceProfileV1Rules.AppearanceSource(38, "male", "Bố", null, null, null, styleSha), age38),
            "04 age 38 role Bố → 35–41 contemporary adult man, not elderly stereotype");

        Ok(age65.TargetAppearanceAgeMin == 61 && age65.TargetAppearanceAgeMax == 69
            && age65.FacialMaturityProfile.Contains("older adult", StringComparison.OrdinalIgnoreCase)
            && CharacterAppearanceProfileV1Rules.IsReady(age65),
            "05 age 65 → older adult appearance from Age Policy");

        Ok(age38.ProfileSha == age38b.ProfileSha
            && age38.ProfileSha.Length == 64
            && age38.SourceAgePolicySha == CharacterAgeConsistencyV1Rules.AgeProfileSha(
                CharacterAgeConsistencyV1Rules.FromCanonicalAge(38))
            && age38.SourceProjectVisualStyleSha == styleSha
            && age11.ProfileSha != age38.ProfileSha,
            "06 deterministic ProfileSha");

        var sourceType = typeof(CharacterAppearanceProfileV1Rules.AppearanceSource);
        Ok(sourceType.GetProperty("Name") is null
            && sourceType.GetProperty("CharacterName") is null
            && sourceType.GetProperty("CharacterId") is null
            && !CharacterAppearanceProfileV1Rules.UsesCharacterName()
            && !CharacterAppearanceProfileV1Rules.UsesCharacterSpecificBranch()
            && !CharacterAppearanceConsistencyV1Rules.UsesCharacterSpecificBranch(),
            "07 no character-name compiler input or branch");

        var stylePrompt = ProjectVisualStyleV1Rules.BuildPrompt(style);
        Ok(CharacterAppearanceProfileV1Rules.StyleBlockIndependent(stylePrompt, age38)
            && !CharacterAppearanceProfileV1Rules.StyleSetsAppearance()
            && stylePrompt.Contains("stylized", StringComparison.OrdinalIgnoreCase)
            && !stylePrompt.Contains("LifestyleProfile:", StringComparison.Ordinal),
            "08 Project Visual Style remains independent");

        var identity = CharacterStudioV1Rules.IdentityBrief(
            "CHAR-099", "Lan", 38, "male", "STYLE_3D_STYLIZED_REALISM", "adult", "Bố");
        var request = CharacterAgeGenerationIntegrationV1Rules.BuildRequest(
            "CHAR-099", "ERA-01", styleSha, stylePrompt, identity, sha, sha, sha, sha,
            CharacterAgeConsistencyV1Rules.FromCanonicalAge(38), "male", age38);
        var prompts = CharacterAgeGenerationIntegrationV1Rules.RequiredViews.ToDictionary(
            v => v,
            v => CharacterAgeGenerationIntegrationV1Rules.ComposeViewPrompt(request, v),
            StringComparer.OrdinalIgnoreCase);
        Ok(CharacterAgeGenerationIntegrationV1Rules.ViewsShareAgeProfile(prompts, request)
            && CharacterAppearanceProfileV1Rules.ViewsShareAppearance(prompts, age38)
            && CharacterAgeGenerationIntegrationV1Rules.ViewsDifferOnlyByCamera(prompts)
            && prompts["FRONT"].Contains("AppearanceProfileSha:", StringComparison.Ordinal)
            && prompts["FRONT"].IndexOf("LifestyleProfile:", StringComparison.Ordinal)
                < prompts["FRONT"].IndexOf("View: FRONT", StringComparison.Ordinal),
            "09 four views share Appearance Profile; only camera changes");

        var provider = CharacterAgeGenerationIntegrationV1Rules.ToProviderRequest(
            request,
            [new CharacterReferenceViewRequest("FRONT", prompts["FRONT"], "1:1", [])]);
        Ok(CharacterAgeGenerationIntegrationV1Rules.ProviderRequestHasAppearance(provider)
            && provider.AppearanceProfileSha == age38.ProfileSha
            && CharacterAppearanceProfileV1Rules.Validate(age38) is null,
            "10 provider request carries Appearance Profile + SHA");

        var missing = new CharacterReferenceSetRequest("CHAR-099", "ERA-01", sha, sha, sha,
            [new CharacterReferenceViewRequest("FRONT", "x", "1:1", [])]);
        Ok(!CharacterAgeGenerationIntegrationV1Rules.ProviderRequestHasAppearance(missing)
            && CharacterAgeGenerationIntegrationV1Rules.ValidateAppearance(null)
                == CharacterAppearanceProfileV1Rules.GateNotReady,
            "11 missing Appearance Profile → APPEARANCE_PROFILE_NOT_READY");

        Ok(!CharacterAppearanceProfileV1Rules.MayRebuild(true)
            && CharacterAppearanceConsistencyV1Rules.MayBecomeReady(true, CharacterAppearanceConsistencyV1Rules.StatusNotEvaluated)
            && !CharacterAppearanceConsistencyV1Rules.MayBecomeReady(false, CharacterAppearanceConsistencyV1Rules.StatusNotEvaluated)
            && CharacterStudioV1Rules.ProtectedMinhUnchanged(minhMaster, minhDna, minhPrp, minhCrp)
            && !CharacterAppearanceProfileV1Rules.AutoRegenerate()
            && !CharacterAppearanceProfileV1Rules.RegeneratesExistingCharacters(),
            "12 locked Minh / existing characters are not rebuilt or auto-generated");

        Ok(CharacterStudioV1Rules.ResolveState(new CharacterStudioV1Rules.StudioSnapshot(
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
            "13 Nam / Linh / An-Thảo factory states stay pending or PRP-ready");

        var display = CharacterAppearanceConsistencyV1Rules.Resolve(age38, 0, null, false);
        var locked = CharacterAppearanceConsistencyV1Rules.Resolve(age38, 4, null, true);
        Ok(display.Status == CharacterAppearanceConsistencyV1Rules.StatusNotEvaluated
            && !display.Pass
            && locked.Status == CharacterAppearanceConsistencyV1Rules.StatusNotEvaluated
            && CharacterAppearanceConsistencyV1Rules.PromptDoesNotImplyPass()
            && !CharacterAppearanceConsistencyV1Rules.InventsAppearanceScore(),
            "14 Appearance Consistency does not invent PASS");

        Ok(!CharacterAppearanceProfileV1Rules.CallsGemini()
            && !CharacterAppearanceConsistencyV1Rules.CallsGemini()
            && !CharacterAppearanceProfileV1Rules.AutoApprove()
            && !CharacterAppearanceProfileV1Rules.AutoLock()
            && !CharacterAppearanceProfileV1Rules.GeneratesVideo()
            && !CharacterAppearanceProfileV1Rules.ChangesProjectVisualStyle()
            && CharacterAppearanceConsistencyV1Rules.FaceAgeStyleAppearanceIndependent()
            && !CharacterAppearanceConsistencyV1Rules.ReadyAllowed(true, true, true, false, true, true, true),
            "15 no Gemini / no auto / Appearance FAIL blocks READY");

        Ok(!CharacterAppearanceProfileV1Rules.UsesCharacterName()
            && typeof(CharacterAppearanceProfileV1Rules.AppearanceSource).GetProperty("Name") is null
            && !typeof(CharacterAppearanceProfile).GetProperties()
                .Any(p => p.Name is "CharacterName" or "Name"),
            "16 compiler source has no character-name branches");

        return fail;
    }
}
