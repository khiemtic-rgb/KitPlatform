using System.Linq;

namespace KitPlatform.Packs.Content;

public static class CharacterAgeGenerationIntegrationV1Regression
{
    public const string SuiteId = CharacterAgeGenerationIntegrationV1Rules.SuiteId;

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

        CharacterAgeGenerationIntegrationV1Rules.AgeAwareGenerationRequest Matrix(
            int age, string gender, string id = "CHAR-099")
        {
            var target = CharacterAgeConsistencyV1Rules.FromCanonicalAge(age);
            var identity = CharacterStudioV1Rules.IdentityBrief(
                id, "Lan", age, gender, "STYLE_3D_STYLIZED_REALISM",
                "canonical identity", "role", "calm");
            return CharacterAgeGenerationIntegrationV1Rules.BuildRequest(
                id, "ERA-01", styleSha, stylePrompt, identity, sha, sha, sha, sha, target, gender);
        }

        IReadOnlyDictionary<string, string> Prompts(
            CharacterAgeGenerationIntegrationV1Rules.AgeAwareGenerationRequest req) =>
            CharacterAgeGenerationIntegrationV1Rules.RequiredViews.ToDictionary(
                v => v,
                v => CharacterAgeGenerationIntegrationV1Rules.ComposeViewPrompt(req, v),
                StringComparer.OrdinalIgnoreCase);

        var age11f = Matrix(11, "female");
        var age38m = Matrix(38, "male");
        var age36f = Matrix(36, "female");
        var age11m = Matrix(11, "male");
        var age27f = Matrix(27, "female");

        Ok(age11f.ChronologicalAge == 11
            && age11f.TargetAppearanceAgeMin == 10
            && age11f.TargetAppearanceAgeMax == 12,
            "01 Matrix age 11 → target 10-12");

        Ok(age38m.ChronologicalAge == 38
            && age38m.TargetAppearanceAgeMin == 35
            && age38m.TargetAppearanceAgeMax == 41,
            "02 Matrix age 38 → target 35-41");

        Ok(age36f.ChronologicalAge == 36
            && age36f.TargetAppearanceAgeMin == 33
            && age36f.TargetAppearanceAgeMax == 39,
            "03 Matrix age 36 → target 33-39");

        Ok(age11m.ChronologicalAge == 11
            && age11m.TargetAppearanceAgeMin == 10
            && age11m.TargetAppearanceAgeMax == 12,
            "04 Matrix age 11 (second) → target 10-12");

        Ok(age27f.ChronologicalAge == 27
            && age27f.TargetAppearanceAgeMin == 25
            && age27f.TargetAppearanceAgeMax == 29,
            "05 Matrix age 27 → target 25-29");

        Ok(CharacterAgeGenerationIntegrationV1Rules.RequestHasRequiredFields(age27f)
            && CharacterAgeGenerationIntegrationV1Rules.RequestHasAgeProfile(age27f),
            "06 Generation request has CharacterId / style / identity / Master / DNA / PRP / Age / views");

        var prompts27 = Prompts(age27f);
        Ok(prompts27.Values.All(p =>
                CharacterAgeGenerationIntegrationV1Rules.PromptContainsAge(p, age27f))
            && prompts27["FRONT"].Contains("Young Vietnamese adult woman", StringComparison.Ordinal)
            && prompts27["FRONT"].Contains("approximately 25-29", StringComparison.OrdinalIgnoreCase)
            && prompts27["FRONT"].Contains("clearly adult but youthful", StringComparison.OrdinalIgnoreCase)
            && prompts27["FRONT"].Contains("late twenties", StringComparison.OrdinalIgnoreCase)
            && prompts27["FRONT"].Contains("no teenage appearance", StringComparison.OrdinalIgnoreCase)
            && prompts27["FRONT"].Contains("no middle-aged appearance", StringComparison.OrdinalIgnoreCase)
            && prompts27["FRONT"].Contains("no elderly appearance", StringComparison.OrdinalIgnoreCase)
            && !prompts27["FRONT"].Contains("Thảo", StringComparison.OrdinalIgnoreCase),
            "07 Prompt Builder emits Age Profile for age 27 (not a character name)");

        Ok(CharacterAgeGenerationIntegrationV1Rules.ViewsShareAgeProfile(prompts27, age27f)
            && CharacterAgeGenerationIntegrationV1Rules.ViewsDifferOnlyByCamera(prompts27)
            && prompts27["FRONT"].Contains("View: FRONT", StringComparison.Ordinal)
            && prompts27["SIDE"].Contains("View: SIDE", StringComparison.Ordinal)
            && prompts27["FULL_BODY"].Contains("View: FULL_BODY", StringComparison.Ordinal),
            "08 All four views share the same Age Appearance Profile");

        var views = CharacterAgeGenerationIntegrationV1Rules.RequiredViews
            .Select(v => new CharacterReferenceViewRequest(
                v, CharacterAgeGenerationIntegrationV1Rules.ComposeViewPrompt(age27f, v),
                v == "FULL_BODY" ? "3:4" : "1:1", []))
            .ToList();
        var providerReq = CharacterAgeGenerationIntegrationV1Rules.ToProviderRequest(age27f, views);
        Ok(CharacterAgeGenerationIntegrationV1Rules.ProviderRequestHasAge(providerReq)
            && providerReq.ChronologicalAge == 27
            && providerReq.TargetAppearanceAgeMin == 25
            && providerReq.TargetAppearanceAgeMax == 29
            && providerReq.AgeAppearanceProfile == age27f.AgeAppearanceProfile
            && providerReq.Views.All(v =>
                CharacterAgeGenerationIntegrationV1Rules.PromptContainsAge(v.CanonicalText, age27f)),
            "09 Provider generation request carries ChronologicalAge / target / AgeAppearanceProfile");

        var missingAge = new CharacterReferenceSetRequest("CHAR-099", "ERA-01", sha, sha, sha, views);
        Ok(!CharacterAgeGenerationIntegrationV1Rules.ProviderRequestHasAge(missingAge)
            && CharacterAgeGenerationIntegrationV1Rules.ValidateRequest(null)
                == CharacterAgeConsistencyV1Rules.GateNotReady,
            "10 Generation request without Age Profile is blocked");

        var invalid = age27f with { TargetAppearanceAgeMin = 40, TargetAppearanceAgeMax = 20 };
        Ok(CharacterAgeGenerationIntegrationV1Rules.ValidateRequest(invalid)
                == CharacterAgeConsistencyV1Rules.GateInvalid
            && CharacterAgeGenerationIntegrationV1Rules.ValidateRequest(
                    age27f with { AgeAppearanceProfile = "" })
                == CharacterAgeConsistencyV1Rules.GateNotReady,
            "11 Missing / invalid Age Profile blocks generation");

        var injected = CharacterAgeGenerationIntegrationV1Rules.EnsureAgeOnProviderRequest(
            missingAge with
            {
                ChronologicalAge = 27,
                TargetAppearanceAgeMin = 25,
                TargetAppearanceAgeMax = 29,
                AgeAppearanceProfile = age27f.AgeAppearanceProfile,
                Views = [new CharacterReferenceViewRequest("FRONT", "camera only", "1:1", [])],
            });
        Ok(CharacterAgeGenerationIntegrationV1Rules.PromptContainsAge(
                injected.Views[0].CanonicalText, age27f)
            && CharacterAgeGenerationIntegrationV1Rules.ProviderPrompt(
                injected, injected.Views[0]).Contains("ChronologicalAge: 27", StringComparison.Ordinal),
            "12 Gemini adapter prompt composition includes Age Profile");

        Ok(CharacterAgeGenerationIntegrationV1Rules.PromptContainsAge(
                CharacterStudioV1Rules.IdentityBrief(
                    "CHAR-099", "Lan", 27, "female", "STYLE_3D_STYLIZED_REALISM", "desc"),
                age27f),
            "13 Identity / Master brief includes labeled Age Profile");

        Ok(!CharacterAgeGenerationIntegrationV1Rules.ReadyAllowed(true, false, true)
            && !CharacterAgeConsistencyV1Rules.CharacterReadyAllowed(
                true, true, true, true, true, true, true, true, false, true, true),
            "14 FACE PASS + AGE FAIL → not READY");

        Ok(!CharacterAgeGenerationIntegrationV1Rules.ReadyAllowed(false, true, true)
            && !CharacterAgeConsistencyV1Rules.CharacterReadyAllowed(
                true, true, true, true, true, true, false, true, true, true, true),
            "15 AGE PASS + FACE FAIL → not READY");

        Ok(!CharacterAgeGenerationIntegrationV1Rules.ReadyAllowed(true, true, false)
            && !CharacterAgeConsistencyV1Rules.CharacterReadyAllowed(
                true, true, true, true, true, true, true, false, true, true, true),
            "16 AGE PASS + FACE PASS + STYLE FAIL → not READY");

        Ok(CharacterAgeGenerationIntegrationV1Rules.ReadyAllowed(true, true, true)
            && CharacterAgeConsistencyV1Rules.FaceAndAgeIndependent(),
            "17 Independent FACE / AGE / STYLE gates; all PASS required");

        Ok(CharacterStudioV1Rules.ProtectedMinhUnchanged(minhMaster, minhDna, minhPrp, minhCrp)
            && CharacterAgeConsistencyV1Rules.LockedBlocksRegeneration(true, true)
            && !CharacterAgeGenerationIntegrationV1Rules.RegeneratesExistingCharacters(),
            "18 Locked character stays unchanged");

        Ok(CharacterStudioV1Rules.ResolveState(new CharacterStudioV1Rules.StudioSnapshot(
                true, true, true, false, "LOCKED", "LOCKED", "LOCKED",
                CharacterStudioV1Rules.Rejected, 4, false, false, true, true))
                == CharacterStudioV1Rules.Rejected
            && !CharacterAgeGenerationIntegrationV1Rules.AutoApprove(),
            "19 Rejected character is not auto-regenerated");

        Ok(CharacterStudioV1Rules.ResolveState(new CharacterStudioV1Rules.StudioSnapshot(
                true, true, true, false, "LOCKED", "LOCKED", "LOCKED",
                CharacterStudioV1Rules.CrpPendingReview, 4, false, false, false, true))
                == CharacterStudioV1Rules.CrpPendingReview
            && !CharacterAgeGenerationIntegrationV1Rules.AutoApprove(),
            "20 Pending review is not auto-approved");

        Ok(CharacterStudioV1Rules.ResolveState(new CharacterStudioV1Rules.StudioSnapshot(
                true, true, true, false, "LOCKED", "LOCKED", "LOCKED",
                null, 0, false, false, false, false))
                == CharacterStudioV1Rules.PrpReady
            && !CharacterAgeGenerationIntegrationV1Rules.RegeneratesExistingCharacters(),
            "21 PRP READY / 0/4 is not auto-generated");

        Ok(!CharacterAgeGenerationIntegrationV1Rules.CallsGemini()
            && !CharacterAgeConsistencyV1Rules.CallsGeminiForValidation(),
            "22 Integration check does not call Gemini");

        Ok(!CharacterAgeGenerationIntegrationV1Rules.AutoLock()
            && !CharacterAgeGenerationIntegrationV1Rules.GeneratesVideo()
            && !CharacterAgeGenerationIntegrationV1Rules.GeneratesProduction()
            && !CharacterAgeGenerationIntegrationV1Rules.ChangesProjectVisualStyle()
            && !CharacterAgeGenerationIntegrationV1Rules.UsesCharacterSpecificBranch(),
            "23 No auto-lock / video / production / PVS change / character branch");

        var childPrompt = CharacterAgeGenerationIntegrationV1Rules.ComposeViewPrompt(age11f, "FRONT");
        var adultPrompt = CharacterAgeGenerationIntegrationV1Rules.ComposeViewPrompt(age38m, "FRONT");
        Ok(childPrompt.Contains("Vietnamese girl", StringComparison.Ordinal)
            && childPrompt.Contains("approximately 10-12", StringComparison.OrdinalIgnoreCase)
            && adultPrompt.Contains("Vietnamese adult man", StringComparison.Ordinal)
            && adultPrompt.Contains("approximately 35-41", StringComparison.OrdinalIgnoreCase)
            && Prompts(age11f).Values.All(p =>
                CharacterAgeGenerationIntegrationV1Rules.PromptContainsAge(p, age11f))
            && Prompts(age38m).Values.All(p =>
                CharacterAgeGenerationIntegrationV1Rules.PromptContainsAge(p, age38m))
            && Prompts(age36f).Values.All(p =>
                CharacterAgeGenerationIntegrationV1Rules.PromptContainsAge(p, age36f))
            && Prompts(age11m).Values.All(p =>
                CharacterAgeGenerationIntegrationV1Rules.PromptContainsAge(p, age11m)),
            "24 Full matrix prompts contain ChronologicalAge / target / AgeAppearanceProfile");

        return fail;
    }
}
