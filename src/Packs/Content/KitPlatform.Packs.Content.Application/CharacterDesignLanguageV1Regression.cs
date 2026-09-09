using System.Linq;

namespace KitPlatform.Packs.Content;

public static class CharacterDesignLanguageV1Regression
{
    public const string SuiteId = CharacterDesignLanguageV1Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var src = FileExists(RulesPath()) ? File.ReadAllText(RulesPath()) : "";
        var def = CharacterDesignLanguageV1Rules.ToDefinitionDto();
        var sha = CharacterDesignLanguageV1Rules.Sha();
        var child = CharacterDesignLanguageV1Rules.Compile(11);
        var young = CharacterDesignLanguageV1Rules.Compile(27);
        var adult = CharacterDesignLanguageV1Rules.Compile(36);
        var adultMale = CharacterDesignLanguageV1Rules.Compile(38, "male");
        var older = CharacterDesignLanguageV1Rules.Compile(65);
        var childAgain = CharacterDesignLanguageV1Rules.Compile(11);
        var identity = CharacterStudioV1Rules.IdentityBrief(
            "CHAR-099", "Lan", 38, "male", "3D_STYLIZED_REALISM", "curious adult");
        var pvsSha = ProjectVisualStyleV1Rules.Sha(ProjectVisualStyleV1Rules.PresetOf("3D_STYLIZED_REALISM")!);

        Ok(def.DocumentId == CharacterDesignLanguageV1Rules.DocumentId
            && def.Name == CharacterDesignLanguageV1Rules.Name
            && def.Status == CharacterDesignLanguageV1Rules.Status,
            "CDL-001 Definition exists");
        Ok(CharacterDesignLanguageV1Rules.IsValid()
            && CharacterDesignLanguageV1Rules.Validate() is null,
            "CDL-002 Definition valid");
        Ok(sha == CharacterDesignLanguageV1Rules.Sha()
            && sha == child.DefinitionSha
            && sha == adultMale.DefinitionSha
            && ProjectVisualStyleV1Rules.LookLikeSha(sha)
            && child.PositivePromptBlock == childAgain.PositivePromptBlock,
            "CDL-003 SHA deterministic");
        Ok(!string.IsNullOrWhiteSpace(def.PositivePromptBlock)
            && CharacterDesignLanguageV1Rules.HasStylizedLanguage(child.PositivePromptBlock),
            "CDL-004 Positive prompt exists");
        Ok(!string.IsNullOrWhiteSpace(def.NegativePromptBlock)
            && CharacterDesignLanguageV1Rules.HasPhotorealismSuppression(child.NegativePromptBlock),
            "CDL-005 Negative prompt exists");
        Ok(child.AgeAdaptiveBlock.Contains("Stronger stylization", StringComparison.Ordinal)
            && adultMale.AgeAdaptiveBlock.Contains("Moderate stylization", StringComparison.Ordinal)
            && older.AgeAdaptiveBlock.Contains("Stylized mature", StringComparison.Ordinal),
            "CDL-006 Age adaptive language exists");
        Ok(CharacterDesignLanguageV1Rules.HasPhotorealismSuppression(def.NegativePromptBlock)
            && !child.PositivePromptBlock.Contains("photorealistic human", StringComparison.OrdinalIgnoreCase),
            "CDL-007 Photorealism suppression exists");
        Ok(!CharacterDesignLanguageV1Rules.UsesCharacterName()
            && !CharacterDesignLanguageV1Rules.ContainsCharacterName(def.Canonical)
            && !CharacterDesignLanguageV1Rules.ContainsCharacterName(child.PositivePromptBlock)
            && !CharacterDesignLanguageV1Rules.ContainsCharacterName(adultMale.CombinedWithAgeAppearance),
            "CDL-008 No character name");
        Ok(!CharacterDesignLanguageV1Rules.UsesCharacterSpecificBranch()
            && !CharacterDesignLanguageV1Rules.RoleSetsDesign()
            && !CharacterDesignLanguageV1Rules.ContainsRoleBranch(child.PositivePromptBlock)
            && !CharacterDesignLanguageV1Rules.ContainsRoleBranch(adultMale.CombinedWithAgeAppearance)
            && !src.Contains("if (characterName", StringComparison.Ordinal)
            && !src.Contains("if (characterId", StringComparison.Ordinal),
            "CDL-009 No role branching");
        Ok(child.ChronologicalAge == 11
            && child.TargetAppearanceAgeMin == 10
            && child.TargetAppearanceAgeMax == 12
            && child.StylizationBand == CharacterDesignLanguageV1Rules.BandChild
            && child.CombinedWithAgeAppearance.Contains("Age Appearance Profile", StringComparison.Ordinal),
            "CDL-010 Child profile valid");
        Ok(young.ChronologicalAge == 27
            && young.TargetAppearanceAgeMin == 25
            && young.TargetAppearanceAgeMax == 29
            && young.StylizationBand == CharacterDesignLanguageV1Rules.BandYoungAdult,
            "CDL-011 Young adult profile valid");
        Ok(adult.ChronologicalAge == 36
            && adult.TargetAppearanceAgeMin == 33
            && adult.TargetAppearanceAgeMax == 39
            && adultMale.ChronologicalAge == 38
            && adultMale.TargetAppearanceAgeMin == 35
            && adultMale.TargetAppearanceAgeMax == 41
            && adultMale.StylizationBand == CharacterDesignLanguageV1Rules.BandAdult,
            "CDL-012 Adult profile valid");
        Ok(older.ChronologicalAge == 65
            && older.TargetAppearanceAgeMin == 61
            && older.TargetAppearanceAgeMax == 69
            && older.StylizationBand == CharacterDesignLanguageV1Rules.BandOlder
            && older.AgeAdaptiveBlock.Contains("not become a photorealistic elderly", StringComparison.OrdinalIgnoreCase),
            "CDL-013 Older adult profile valid");
        Ok(CharacterDesignLanguageV1Rules.SameDefinition(child, young)
            && CharacterDesignLanguageV1Rules.SameDefinition(child, adultMale)
            && CharacterDesignLanguageV1Rules.SameDefinition(child, older)
            && child.PositivePromptBlock != adultMale.PositivePromptBlock,
            "CDL-014 Same CDL across profiles");
        Ok(identity.Contains("Character: Lan", StringComparison.Ordinal)
            && !child.PositivePromptBlock.Contains("Character:", StringComparison.Ordinal)
            && !CharacterDesignLanguageV1Rules.ReplacesIdentity(),
            "CDL-015 Character identity remains separate");
        Ok(sha != pvsSha
            && !CharacterDesignLanguageV1Rules.ChangesProjectVisualStyle()
            && !CharacterDesignLanguageV1Rules.ReplacesProjectVisualStyle()
            && def.CorePrinciple != ProjectVisualStyleV2Rules.StylePromptBlock,
            "CDL-016 PVS remains separate");
        Ok(!CharacterDesignLanguageV1Rules.MutatesMaster()
            && !CharacterDesignLanguageV1Rules.ReplacesMasterAuthority()
            && !CharacterDesignLanguageV1Rules.ReplacesAgePolicy(),
            "CDL-017 Master authority remains separate");
        Ok(!CharacterDesignLanguageV1Rules.CallsGemini()
            && !CharacterDesignLanguageV1Rules.CreatesPixels(),
            "CDL-018 No Gemini call");
        Ok(!CharacterDesignLanguageV1Rules.Persists()
            && !def.Persisted
            && !CharacterDesignLanguageV1Rules.ToCompileDto(child).Persisted,
            "CDL-019 No persistence mutation");
        Ok(CharacterAuthorityInitializationV1Rules.ProtectedMasterSha
                == "be439c39e067aa6c7727255e9643ac78cb7c6285917af60dda38bf14a32518f1"
            && CharacterAuthorityInitializationV1Rules.ProtectedDnaSha
                == "75ececad8899211ce31107232fe0288c11a9e113c5bc0e7c0a6c9f749d72f4dc"
            && CharacterAuthorityInitializationV1Rules.ProtectedPrpSha
                == "5e61ad240aaebaa13dcd91463a41ef9f9c0498fabefe86b7e8b1a1ad973a9444"
            && CharacterAuthorityInitializationV1Rules.ProtectedCrpSha
                == "82543a4a4331e32a79a865fc3881c17e8bc3dc74c5dec51c52c2deab1a70c2b7"
            && !CharacterDesignLanguageV1Rules.MutatesDna()
            && !CharacterDesignLanguageV1Rules.MutatesPrp()
            && !CharacterDesignLanguageV1Rules.MutatesCrp()
            && !CharacterDesignLanguageV1Rules.AutoApprove()
            && !CharacterDesignLanguageV1Rules.AutoLock()
            && !CharacterDesignLanguageV1Rules.AutoRegenerate(),
            "CDL-020 Locked character SHA unchanged");

        Ok(child.StylizationBand == CharacterDesignLanguageV1Rules.BandChild
            && adultMale.StylizationBand == CharacterDesignLanguageV1Rules.BandAdult
            && CharacterDesignLanguageV1Rules.Compile(36, "female").StylizationBand
                == CharacterDesignLanguageV1Rules.BandAdult
            && CharacterDesignLanguageV1Rules.SameDefinition(child, CharacterDesignLanguageV1Rules.Compile(36, "female"))
            && child.AgeAdaptiveBlock.Contains("larger eyes", StringComparison.OrdinalIgnoreCase)
            && !adultMale.AgeAdaptiveBlock.Contains("larger head-to-body", StringComparison.OrdinalIgnoreCase),
            "CDL-021 Three-character style test is semantic only");

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
                "CharacterDesignLanguageV1Rules.cs");
            if (File.Exists(candidate)) return candidate;
            dir = Directory.GetParent(dir)?.FullName ?? "";
        }
        return Path.Combine(
            AppContext.BaseDirectory, "CharacterDesignLanguageV1Rules.cs");
    }

    private static bool FileExists(string path) => File.Exists(path);
}
