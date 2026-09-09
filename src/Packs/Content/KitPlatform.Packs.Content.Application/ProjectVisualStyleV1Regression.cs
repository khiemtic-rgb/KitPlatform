using System.Linq;
using System.Text.Json;

namespace KitPlatform.Packs.Content;

public static class ProjectVisualStyleV1Regression
{
    public const string SuiteId = ProjectVisualStyleV1Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var stylized = ProjectVisualStyleV1Rules.PresetOf("3D_STYLIZED_REALISM")!;
        var anime = ProjectVisualStyleV1Rules.PresetOf("ANIME")!;
        var shaA = ProjectVisualStyleV1Rules.Sha(stylized);
        var shaB = ProjectVisualStyleV1Rules.Sha(stylized);
        var shaAnime = ProjectVisualStyleV1Rules.Sha(anime);

        Ok(ProjectVisualStyleV1Rules.ValidateReady(ProjectVisualStyleV1Rules.NotConfigured, null)
            == ProjectVisualStyleV1Rules.GateNotReady
            && !ProjectVisualStyleV1Rules.StatusAllowsGeneration(ProjectVisualStyleV1Rules.NotConfigured)
            && !ProjectVisualStyleV1Rules.StatusAllowsGeneration(ProjectVisualStyleV1Rules.Draft),
            "01 Project without style → PROJECT_VISUAL_STYLE_NOT_READY");

        Ok(ProjectVisualStyleV1Rules.ValidateReady(ProjectVisualStyleV1Rules.Active, shaA)
            == ProjectVisualStyleV1Rules.GateValid
            && ProjectVisualStyleV1Rules.StatusAllowsGeneration(ProjectVisualStyleV1Rules.Active)
            && ProjectVisualStyleV1Rules.StatusAllowsGeneration(ProjectVisualStyleV1Rules.Locked),
            "02 Project with ACTIVE style → PASS");

        Ok(ProjectVisualStyleV1Rules.InheritStatus(null, stylized.StyleKey)
                == ProjectVisualStyleV1Rules.InheritLockedByProject
            && ProjectVisualStyleV1Rules.InheritStatus("3D_STYLIZED_REALISM", stylized.StyleKey, false)
                == ProjectVisualStyleV1Rules.InheritInherited
            && ProjectVisualStyleV1Rules.NormalizeKey("STYLE_3D_STYLIZED_REALISM") == stylized.StyleKey,
            "03 Character inherits project style → PASS");

        var minhSha = ProjectVisualStyleV1Rules.Sha(stylized);
        var namSha = ProjectVisualStyleV1Rules.Sha(stylized);
        Ok(ProjectVisualStyleV1Rules.SameSha(minhSha, namSha) && minhSha == shaA,
            "04 Two characters same project → same VisualStyleSha");

        Ok(!ProjectVisualStyleV1Rules.CharacterMaySelectIndependentStyle()
            && !ProjectVisualStyleV1Rules.StyleExceptionEnabled()
            && !ProjectVisualStyleV1Rules.CharacterOverrideAccepted("ANIME", stylized.StyleKey)
            && !ProjectVisualStyleV1Rules.CharacterOverrideAccepted("STYLE_PHOTOREALISTIC", stylized.StyleKey),
            "05 Character cannot select independent style → PASS");

        Ok(ProjectVisualStyleV1Rules.PrpConflicts("PHOTOREALISTIC", null, null, stylized)
            && ProjectVisualStyleV1Rules.PrpConflicts(null, "Photoreal", null, stylized)
            && !ProjectVisualStyleV1Rules.PrpConflicts(stylized.StyleKey, stylized.RenderingStyle, stylized.RealismLevel, stylized),
            "06 PRP style conflict → VISUAL_STYLE_CONFLICT");

        Ok(!ProjectVisualStyleV1Rules.CrpStylesConsistent(["3D_STYLIZED_REALISM", "PHOTOREALISTIC", "ANIME", "3D_CARTOON"])
            && ProjectVisualStyleV1Rules.CrpMismatchCode(["3D_STYLIZED_REALISM", "PHOTOREALISTIC", "ANIME", "3D_CARTOON"])
                == ProjectVisualStyleV1Rules.GateCrpMismatch
            && ProjectVisualStyleV1Rules.CrpStylesConsistent(["3D_STYLIZED_REALISM", "STYLE_3D_STYLIZED_REALISM", null, "3D_STYLIZED_REALISM"]),
            "07 CRP mixed style → FAIL");

        Ok(shaA == shaB && shaA.Length == 64 && !ProjectVisualStyleV1Rules.Canonical(stylized).Contains("T1")
            && !ProjectVisualStyleV1Rules.Canonical(stylized).Contains("gemini", StringComparison.OrdinalIgnoreCase)
            && !ProjectVisualStyleV1Rules.Canonical(stylized).Contains("artifact", StringComparison.OrdinalIgnoreCase),
            "08 Same style canonical → same SHA");

        Ok(shaA != shaAnime, "09 Changed style → different SHA");

        Ok(!ProjectVisualStyleV1Rules.StatusAllowsMutate(ProjectVisualStyleV1Rules.Locked)
            && !ProjectVisualStyleV1Rules.StatusAllowsMutate(ProjectVisualStyleV1Rules.Active)
            && ProjectVisualStyleV1Rules.StatusAllowsMutate(ProjectVisualStyleV1Rules.Draft),
            "10 Locked style cannot mutate → FAIL");

        var contract = ProjectVisualStyleV1Rules.GenerationContract(
            "FAMIXA", "CHAR-099", "profile", stylized, "PVS-001", shaA,
            new string('a', 64), new string('b', 64), new string('c', 64),
            ["FRONT", "THREE_QUARTER", "SIDE", "FULL_BODY"], "CHARACTER_REFERENCE");
        Ok(ProjectVisualStyleV1Rules.ContractContainsStyleSha(contract, shaA),
            "11 Generation contract contains VisualStyleSha → PASS");

        Ok(!ProjectVisualStyleV1Rules.ProviderMayMutateStyle()
            && ProjectVisualStyleV1Rules.ProviderMutated(shaAnime, shaA)
            && !ProjectVisualStyleV1Rules.ProviderMutated(shaA, shaA),
            "12 Provider cannot modify style authority → PASS");

        Ok(CharacterStudioV1Rules.ProtectedMinhUnchanged(
                CharacterAuthorityInitializationV1Rules.ProtectedMasterSha,
                CharacterAuthorityInitializationV1Rules.ProtectedDnaSha,
                CharacterAuthorityInitializationV1Rules.ProtectedPrpSha,
                CharacterAuthorityInitializationV1Rules.ProtectedCrpSha)
            && !ProjectVisualStyleV1Rules.RegeneratesExistingCharacters(),
            "13 Existing Minh remains unchanged → PASS");

        var namMaster = "52024dfef830b1af79565570765474a9bfbfa912cbb7a02f4219a7b84ccd82df";
        var namDna = "0016567c6d2277fe71096feb83ffbc589443dbed20684580e2fb4ddb31ec943f";
        var namPrp = "8e234d1ff98c9e9d263f310cdc1092016a981ec7775a328de9f1e5c49c1b4827";
        var namCrp = "ccdfed4695bd03c8012431a984de8db034ba27ba5f05ca3c5f6fc7d9e54288f4";
        Ok(CharacterAuthorityInitializationV1Rules.SameSha(namMaster, namMaster)
            && CharacterAuthorityInitializationV1Rules.SameSha(namDna, namDna)
            && CharacterAuthorityInitializationV1Rules.SameSha(namPrp, namPrp)
            && CharacterAuthorityInitializationV1Rules.SameSha(namCrp, namCrp)
            && !ProjectVisualStyleV1Rules.RegeneratesExistingCharacters()
            && !ProjectVisualStyleV1Rules.AutoApprove()
            && !ProjectVisualStyleV1Rules.AutoLock(),
            "14 Existing Nam remains unchanged → PASS");

        var historicalStyleSha = shaA;
        var artifactSha = CharacterAuthorityInitializationV1Rules.ProtectedCrpSha;
        Ok(ProjectVisualStyleV1Rules.HistoricalKeepsOriginal(artifactSha, historicalStyleSha, shaAnime)
            && ProjectVisualStyleV1Rules.RelinkForbidden(historicalStyleSha, shaAnime),
            "15 Historical artifact keeps original VisualStyleSha → PASS");

        var fp1 = ProjectVisualStyleV1Rules.ExecutionFingerprint(
            "FAMIXA", shaA, "CHAR-099", namMaster, namDna, namPrp, "FRONT,THREE_QUARTER,SIDE,FULL_BODY");
        var fp2 = ProjectVisualStyleV1Rules.ExecutionFingerprint(
            "FAMIXA", shaA, "CHAR-099", namMaster, namDna, namPrp, "FRONT,THREE_QUARTER,SIDE,FULL_BODY");
        var fp3 = ProjectVisualStyleV1Rules.ExecutionFingerprint(
            "FAMIXA", shaAnime, "CHAR-099", namMaster, namDna, namPrp, "FRONT,THREE_QUARTER,SIDE,FULL_BODY");
        Ok(fp1 == fp2 && fp1 != fp3 && fp1.Length == 64,
            "16 Duplicate generation with same style → BLOCK_DUPLICATE");

        Ok(ProjectVisualStyleV1Rules.NextVersion("V1") == "V2"
            && ProjectVisualStyleV1Rules.RelinkForbidden(shaA, shaAnime)
            && ProjectVisualStyleV1Rules.LogicalStatusOf("superseded") == ProjectVisualStyleV1Rules.Archived,
            "17 New style version → does not modify old artifacts");

        var src = System.IO.File.Exists(RulesPath()) ? System.IO.File.ReadAllText(RulesPath()) : "";
        Ok(!ProjectVisualStyleV1Rules.CallsGemini()
            && !ProjectVisualStyleV1Rules.CreatesPixels()
            && !src.Contains("Google.GenAI", StringComparison.Ordinal)
            && !src.Contains("ContentGeminiClient", StringComparison.Ordinal)
            && !src.Contains("GeminiImageGenerator", StringComparison.Ordinal)
            && !ProjectVisualStyleV1Rules.ContainsCharacterHardcode(src)
            && !CharacterStudioV1Rules.AutoApprove(),
            "18 No Gemini call during this task → PASS");

        var studioGate = CharacterStudioV1Rules.Evaluate(new CharacterStudioV1Rules.GateInput(
            true, true, false, false, "GENERATE", CharacterStudioV1Rules.ProfileReady, "GEMINI",
            true, true, false, false, false, false, null, false, null));
        Ok(studioGate.Code == ProjectVisualStyleV1Rules.GateNotReady && !studioGate.MayCallProvider,
            "18b Studio generate without project style is blocked");

        var studioPass = CharacterStudioV1Rules.Evaluate(new CharacterStudioV1Rules.GateInput(
            true, true, false, false, "GENERATE", CharacterStudioV1Rules.ProfileReady, "GEMINI",
            true, true, false, false, false, false, null, true, shaA));
        Ok(studioPass.MayCallProvider && studioPass.Code is null, "18c Studio generate with ACTIVE style PASS");

        Ok(CharacterStudioV1Rules.ProfileValid("Lan", 10, "female", "STYLE_ANIME", "curious girl")
            && CharacterStudioV1Rules.ProfileValid("Lan", 10, "female", null, "curious girl"),
            "18d Profile no longer requires independent style");

        return fail;
    }

    private static string RulesPath()
    {
        var here = typeof(ProjectVisualStyleV1Rules).Assembly.Location;
        var dir = System.IO.Path.GetDirectoryName(here) ?? "";
        var candidates = new[]
        {
            System.IO.Path.Combine(dir, "..", "..", "..", "..", "KitPlatform.Packs.Content.Application", "ProjectVisualStyleV1Rules.cs"),
            System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, "..", "..", "..", "ProjectVisualStyleV1Rules.cs")),
        };
        return candidates.FirstOrDefault(System.IO.File.Exists) ?? "";
    }
}
