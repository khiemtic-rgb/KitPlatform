using System.Linq;
using System.Text.Json;

namespace KitPlatform.Packs.Content;

public static class ProductionContractPromptV2LifecycleRegression
{
    public const string SuiteId = "PRODUCTION_CONTRACT_PROMPT_V2_LIFECYCLE_TEST_V1";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var masterId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var dnaId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var prpId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var sha = new string('a', 64);
        var v1 = ProductionShotContractRules.BuildValidFixture("CHAR-099", "FAMIXA", "ERA-01", masterId, sha, dnaId, sha, prpId, sha);
        var v2 = ProductionShotContractRules.With(v1, "timing.durationSeconds", 8);
        var contractSha1 = ProductionShotContractRules.HashCanonical(v1);
        var contractSha2 = ProductionShotContractRules.HashCanonical(v2);
        var prompt1 = ProductionPromptCompilerRules.CompilePrompt(v1, "CHAR-099");
        var prompt2 = ProductionPromptCompilerRules.CompilePrompt(v2, "CHAR-099");
        var promptSha1 = ProductionPromptCompilerRules.HashPrompt(prompt1);
        var promptSha2 = ProductionPromptCompilerRules.HashPrompt(prompt2);

        Ok(ProductionShotContractRules.IsApproved("DIRECTOR_APPROVED") && !ProductionShotContractRules.CanEdit("DIRECTOR_APPROVED"), "01 approved Contract V1 cannot be edited in place");
        Ok(ProductionShotContractRules.NextVersion("V1") == "V2", "02 change requires Contract V2");
        Ok(contractSha1 != contractSha2 && contractSha1.Length == 64, "03 allowed fact change changes Contract SHA");
        Ok(promptSha1 != promptSha2 && prompt1 != prompt2, "06 Prompt V2 SHA differs from Prompt V1");
        Ok(prompt1 == ProductionPromptCompilerRules.CompilePrompt(v1, "CHAR-099") && promptSha1 == ProductionPromptCompilerRules.HashPrompt(prompt1), "07 Prompt V1 text+SHA unchanged after V2 compile");
        Ok(promptSha2 == ProductionPromptCompilerRules.HashPrompt(prompt2), "08 same V2 input → same Prompt SHA");
        Ok(!ProductionPromptCompilerRules.CanOverwrite("COMPILED"), "09 compiled Prompt V1 cannot overwrite");
        Ok(!ProductionShotContractRules.AutoFix() && !ProductionPromptCompilerRules.AutoFix(), "09 no auto-fix");
        Ok(!ProductionPromptCompilerRules.CreatesPixels("SPEC") && !ProductionPromptCompilerRules.ContainsProviderSyntax(prompt2), "11 no Gemini/Runway in compiled V2");
        Ok(ProductionPromptCompilerRules.Evaluate(new ProductionPromptCompilerRules.CompileInput(
            "SUPERSEDED", v1, contractSha1, contractSha1, sha, sha, sha, sha, sha, sha,
            true, true, true, true, [], "CHAR-099", Guid.NewGuid(), Guid.NewGuid(), "V1",
            masterId, dnaId, prpId, [])).Status == "BLOCKED", "05 superseded Contract V1 cannot compile");
        var dna = CharacterIdentityGovernanceRules.BuildGenericDna("9", ["long", "shoulder_length"]);
        var age = ProductionShotContractRules.With(v1, "character", new { expression = "focused", pose = "sitting", gaze = "paper", movement = "lift", age = "14" });
        var locked = new CharacterIdentityGovernanceRules.AuthorityState("CHAR-099", true, true, true, true, true, true, "LOCKED", "LOCKED", "LOCKED", masterId, dnaId, prpId);
        var ageGov = CharacterIdentityGovernanceRules.Evaluate(dna, null, ProductionShotContractRules.ToGovernanceShotSpec(age), null, null, locked);
        Ok(ageGov.Any(c => c.Attribute == "age"), "04 identity change is not a valid V2 fact");
        Ok(prompt2.Contains("durationSeconds: 8") && !prompt1.Contains("durationSeconds: 8"), "06b V2 prompt contains new duration only");

        return fail;
    }
}
