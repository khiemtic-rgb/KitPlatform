using System.Linq;

namespace KitPlatform.Packs.Content;

public static class CharacterDesignLanguageV2Regression
{
    public const string SuiteId = "FAMIXA_CHARACTER_DESIGN_LANGUAGE_V2_REGRESSION";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var def = CharacterDesignLanguageV2Rules.ToDefinitionDto();
        var sha = CharacterDesignLanguageV2Rules.Sha();
        var canonical = CharacterDesignLanguageV2Rules.Canonical();
        var block = CharacterDesignLanguageV2Rules.Compile();
        var blockFromPvs = CharacterDesignLanguageV2Rules.Compile(ProjectVisualStyleV2Rules.Sha());
        var pvsV1 = ProjectVisualStyleV1Rules.Sha(ProjectVisualStyleV1Rules.PresetOf("3D_STYLIZED_REALISM")!);
        var child = CharacterDesignLanguageV2Rules.ComposeProviderPrompt("PVS", "identity child", "age 11", "", "", "FRONT", null);
        var adultMale = CharacterDesignLanguageV2Rules.ComposeProviderPrompt("PVS", "identity adult male", "age 38", "", "", "FRONT", null);
        var adultFemale = CharacterDesignLanguageV2Rules.ComposeProviderPrompt("PVS", "identity adult female", "age 36", "", "", "FRONT", null);
        var youngFemale = CharacterDesignLanguageV2Rules.ComposeProviderPrompt("PVS", "identity young adult female", "age 27", "", "", "FRONT", null);
        var older = CharacterDesignLanguageV2Rules.ComposeProviderPrompt("PVS", "identity older adult", "age 65", "", "", "FRONT", null);
        var scene = CharacterDesignLanguageV2Rules.ResolveSceneStyle("realistic portrait of the speaker");

        Ok(def.DocumentId == CharacterDesignLanguageV2Rules.DocumentId
            && def.Name == CharacterDesignLanguageV2Rules.Name, "01 Definition exists");
        Ok(def.Version == "V2" && CharacterDesignLanguageV2Rules.Version == "V2", "02 Version correct");
        Ok(sha == CharacterDesignLanguageV2Rules.Sha()
            && sha == KitVideoIntegrityRules.Sha256Hex(System.Text.Encoding.UTF8.GetBytes(canonical))
            && ProjectVisualStyleV1Rules.LookLikeSha(sha)
            && block == blockFromPvs, "03 SHA deterministic");
        Ok(!CharacterDesignLanguageV2Rules.UsesCharacterName()
            && !CharacterDesignLanguageV2Rules.ContainsCharacterName(canonical)
            && !CharacterDesignLanguageV2Rules.ContainsCharacterName(block), "04 Character name absent");
        Ok(!CharacterDesignLanguageV2Rules.UsesCharacterId()
            && !CharacterDesignLanguageV2Rules.ContainsCharacterId(canonical)
            && !CharacterDesignLanguageV2Rules.ContainsCharacterId(block), "05 Character ID absent");
        Ok(!CharacterDesignLanguageV2Rules.UsesRole()
            && !CharacterDesignLanguageV2Rules.ContainsRole(canonical)
            && !CharacterDesignLanguageV2Rules.ContainsRole(block), "06 Role absent");
        Ok(!CharacterDesignLanguageV2Rules.UsesAge()
            && !canonical.Contains("ChronologicalAge", StringComparison.Ordinal)
            && !block.Contains("ChronologicalAge", StringComparison.Ordinal), "07 Age absent");
        Ok(!CharacterDesignLanguageV2Rules.UsesGender()
            && !canonical.Contains("male", StringComparison.OrdinalIgnoreCase)
            && !canonical.Contains("female", StringComparison.OrdinalIgnoreCase), "08 Gender absent");
        Ok(!CharacterDesignLanguageV2Rules.ContainsLifestyleStereotype(canonical)
            && canonical.Contains("contemporary Vietnamese", StringComparison.OrdinalIgnoreCase),
            "09 Lifestyle-specific stereotype absent");
        Ok(def.StylizationTarget == "STRONG_STYLIZED_3D" && def.StylizationScale == 4, "10 Stylization target exists");
        Ok(def.PhotorealismCeiling == "LOW"
            && def.RealismCeilingLanguage.Contains("PhotorealismCeiling=LOW", StringComparison.Ordinal),
            "11 Photorealism ceiling exists");
        Ok(def.CartoonFloor == "CONTROLLED"
            && def.CartoonFloorLanguage.Contains("CartoonFloor=CONTROLLED", StringComparison.Ordinal),
            "12 Cartoon ceiling exists");
        Ok(!string.IsNullOrWhiteSpace(def.CrossCharacterInvariants)
            && def.CrossCharacterInvariants.Contains("Design language must not", StringComparison.OrdinalIgnoreCase),
            "13 Cross-character invariants exist");
        Ok(CharacterDesignLanguageV2Rules.PromptHasDesignLanguage(block)
            && block.Contains("strong but controlled stylization", StringComparison.OrdinalIgnoreCase),
            "14 Positive style constraints exist");
        Ok(!string.IsNullOrWhiteSpace(def.NegativePromptBlock)
            && def.NegativePromptBlock.Contains("photorealistic human", StringComparison.OrdinalIgnoreCase)
            && def.NegativePromptBlock.Contains("digital human", StringComparison.OrdinalIgnoreCase),
            "15 Negative constraints exist");
        Ok(CharacterDesignLanguageV2Rules.IsValid()
            && CharacterDesignLanguageV2Rules.Validate() is null, "16 Provider prompt block compiles");
        Ok(CharacterDesignLanguageV2Rules.PromptHasDesignLanguage(child)
            && CharacterDesignLanguageV2Rules.PromptHasDesignLanguage(adultMale),
            "17 Prompt contains Character Design Language");
        Ok(CharacterDesignLanguageV2Rules.PromptHasAntiPhotorealism(block)
            && CharacterDesignLanguageV2Rules.PromptHasAntiPhotorealism(adultMale),
            "18 Prompt contains explicit anti-photorealism constraints");
        Ok(pvsV1 == "d48e4884f6ac3315c887dfd139aae86510822d15ec8cc8705e8980629547de58"
            && !CharacterDesignLanguageV2Rules.ChangesProjectVisualStyle()
            && !CharacterDesignLanguageV2Rules.AutoActivate(),
            "19 Existing PVS authority unchanged");
        Ok(!CharacterDesignLanguageV2Rules.MutatesMaster()
            && !CharacterDesignLanguageV2Rules.MutatesDna()
            && !CharacterDesignLanguageV2Rules.MutatesPrp()
            && !CharacterDesignLanguageV2Rules.MutatesCrp()
            && !CharacterDesignLanguageV2Rules.Persists()
            && CharacterAuthorityInitializationV1Rules.ProtectedMasterSha
                == "be439c39e067aa6c7727255e9643ac78cb7c6285917af60dda38bf14a32518f1"
            && CharacterAuthorityInitializationV1Rules.ProtectedDnaSha
                == "75ececad8899211ce31107232fe0288c11a9e113c5bc0e7c0a6c9f749d72f4dc"
            && CharacterAuthorityInitializationV1Rules.ProtectedPrpSha
                == "5e61ad240aaebaa13dcd91463a41ef9f9c0498fabefe86b7e8b1a1ad973a9444"
            && CharacterAuthorityInitializationV1Rules.ProtectedCrpSha
                == "82543a4a4331e32a79a865fc3881c17e8bc3dc74c5dec51c52c2deab1a70c2b7",
            "20 No character artifact mutation");
        Ok(!CharacterDesignLanguageV2Rules.CreatesPixels() && !def.ProviderCalled, "21 No provider call");
        Ok(!CharacterDesignLanguageV2Rules.CallsGemini()
            && !CharacterStyleConsistencyV2Rules.CallsGemini()
            && !def.GeminiCalled, "22 No Gemini call");
        Ok(!CharacterDesignLanguageV2Rules.AutoApprove()
            && !CharacterStyleConsistencyV2Rules.AutoApprove(), "23 No auto approve");
        Ok(!CharacterDesignLanguageV2Rules.AutoLock()
            && !CharacterStyleConsistencyV2Rules.AutoLock(), "24 No auto lock");
        Ok(!CharacterDesignLanguageV2Rules.AutoRegenerate(), "25 No generate");

        var age11 = CharacterAgeConsistencyV1Rules.FromCanonicalAge(11);
        var age38 = CharacterAgeConsistencyV1Rules.FromCanonicalAge(38);
        var age36 = CharacterAgeConsistencyV1Rules.FromCanonicalAge(36);
        var age27 = CharacterAgeConsistencyV1Rules.FromCanonicalAge(27);
        var age65 = CharacterAgeConsistencyV1Rules.FromCanonicalAge(65);
        Ok(age11.TargetAppearanceAgeMin == 10 && age11.TargetAppearanceAgeMax == 12
            && CharacterDesignLanguageV2Rules.AgeInterpretation(11).Contains("stylized FAMIXA child", StringComparison.Ordinal)
            && !CharacterDesignLanguageV2Rules.ReplacesAgePolicy(),
            "26 Case generic child 11");
        Ok(age38.TargetAppearanceAgeMin == 35 && age38.TargetAppearanceAgeMax == 41
            && CharacterDesignLanguageV2Rules.AgeInterpretation(38).Contains("stylized FAMIXA adult", StringComparison.Ordinal),
            "27 Case generic adult male 38");
        Ok(age36.TargetAppearanceAgeMin == 33 && age36.TargetAppearanceAgeMax == 39
            && CharacterDesignLanguageV2Rules.AgeInterpretation(36).Contains("stylized FAMIXA adult", StringComparison.Ordinal),
            "28 Case generic adult female 36");
        Ok(age27.TargetAppearanceAgeMin == 25 && age27.TargetAppearanceAgeMax == 29
            && CharacterDesignLanguageV2Rules.AgeInterpretation(27).Contains("clearly adult", StringComparison.OrdinalIgnoreCase)
            && CharacterDesignLanguageV2Rules.AgeInterpretation(27).Contains("not teenage", StringComparison.OrdinalIgnoreCase),
            "29 Case generic young adult 27");
        Ok(age65.TargetAppearanceAgeMin == 61 && age65.TargetAppearanceAgeMax == 69
            && CharacterDesignLanguageV2Rules.AgeInterpretation(65).Contains("61–69", StringComparison.Ordinal)
            && CharacterDesignLanguageV2Rules.AgeInterpretation(65).Contains("stylized FAMIXA older adult", StringComparison.Ordinal),
            "30 Case generic older adult 65");

        var blocks = new[]
        {
            CharacterDesignLanguageV2Rules.Compile("CHILD_MALE"),
            CharacterDesignLanguageV2Rules.Compile("CHILD_FEMALE"),
            CharacterDesignLanguageV2Rules.Compile("ADULT_MALE"),
            CharacterDesignLanguageV2Rules.Compile("ADULT_FEMALE"),
            CharacterDesignLanguageV2Rules.Compile("OLDER_ADULT"),
        };
        Ok(blocks.All(b => b == CharacterDesignLanguageV2Rules.PromptBlock),
            "31 Cross-character DesignLanguageBlock identical");

        Ok(scene.Contains("stylized 3D character portrait", StringComparison.OrdinalIgnoreCase)
            && !scene.Contains("realistic portrait of the speaker", StringComparison.OrdinalIgnoreCase)
            && CharacterDesignLanguageV2Rules.PromptHasDesignLanguage(scene),
            "32 Scene realistic portrait cannot override CDL");

        Ok(CharacterStyleConsistencyV2Rules.StatusWithoutEvaluator()
                == CharacterStyleConsistencyV2Rules.StatusNotEvaluated
            && CharacterStyleConsistencyV2Rules.ScoreWithoutEvaluator() is null
            && !CharacterStyleConsistencyV2Rules.CompilationImpliesPass()
            && !CharacterStyleConsistencyV2Rules.FaceEqualsStyle()
            && !CharacterStyleConsistencyV2Rules.ReadyAllowed(true, true, true, CharacterStyleConsistencyV2Rules.StatusNotEvaluated)
            && CharacterStyleConsistencyV2Rules.ReadyAllowed(true, true, true, CharacterStyleConsistencyV2Rules.StatusPass),
            "33 Style gate is independent and NOT_EVALUATED without evaluator");

        var rulesSrc = File.Exists(RulesPath()) ? File.ReadAllText(RulesPath()) : canonical;
        Ok(!CharacterDesignLanguageV2Rules.AutoRegenerate()
            && !CharacterDesignLanguageV2Rules.Persists()
            && !CharacterDesignLanguageV2Rules.ReplacesMasterAuthority()
            && !rulesSrc.Contains("if (characterName", StringComparison.Ordinal)
            && !rulesSrc.Contains("characterName ==", StringComparison.Ordinal)
            && !rulesSrc.Contains("NamAppearance", StringComparison.Ordinal),
            "34 Compiler has no character-name branch; CHAR-001..006 artifacts stay untouched");

        Ok(CharacterAuthorityInitializationV1Rules.IsLocked(CharacterAuthorityInitializationV1Rules.Locked)
            && !CharacterDesignLanguageV2Rules.MutatesMaster()
            && !CharacterDesignLanguageV2Rules.MutatesCrp(),
            "35 Existing character protection: no generate / approve / lock / stale / rebuild");

        return fail;
    }

    private static string RulesPath()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && !string.IsNullOrWhiteSpace(dir); i++)
        {
            var candidate = Path.Combine(
                dir, "src", "Packs", "Content",
                "KitPlatform.Packs.Content.Application",
                "CharacterDesignLanguageV2Rules.cs");
            if (File.Exists(candidate)) return candidate;
            dir = Directory.GetParent(dir)?.FullName ?? "";
        }
        return Path.Combine(AppContext.BaseDirectory, "CharacterDesignLanguageV2Rules.cs");
    }
}
