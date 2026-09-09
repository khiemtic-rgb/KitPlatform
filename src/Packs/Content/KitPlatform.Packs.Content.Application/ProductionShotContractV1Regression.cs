using System.Linq;
using System.Text.Json;

namespace KitPlatform.Packs.Content;

public static class ProductionShotContractV1Regression
{
    public const string SuiteId = "PRODUCTION_SHOT_CONTRACT_V1_REGRESSION";

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
        var valid = ProductionShotContractRules.BuildValidFixture("CHAR-099", "FAMIXA", "ERA-01", masterId, sha, dnaId, sha, prpId, sha);
        Ok(ProductionShotContractRules.Validate(valid).Count == 0, "01 valid contract → PASS");
        Ok(ProductionShotContractRules.Validate(ProductionShotContractRules.With(valid, "characterId", "")).Any(i => i.Attribute == "characterId"), "02 missing character → BLOCK");
        Ok(ProductionShotContractRules.Validate(ProductionShotContractRules.With(valid, "identity.masterId", "")).Any(i => i.Attribute == "masterId"), "03 missing Master → BLOCK");
        Ok(ProductionShotContractRules.Validate(ProductionShotContractRules.With(valid, "identity.dnaId", "")).Any(i => i.Attribute == "dnaId"), "04 missing DNA → BLOCK");
        Ok(ProductionShotContractRules.Validate(ProductionShotContractRules.With(valid, "identity.prpId", "")).Any(i => i.Attribute == "prpId"), "05 missing PRP → BLOCK");
        Ok(ProductionShotContractRules.Validate(ProductionShotContractRules.With(valid, "timing.durationSeconds", 0)).Any(i => i.Attribute.Contains("duration")), "06 invalid duration → BLOCK");
        Ok(ProductionShotContractRules.Validate(ProductionShotContractRules.With(valid, "story.action", "")).Any(i => i.Attribute.Contains("action")), "07 missing action → BLOCK");
        var noComp = ProductionShotContractRules.With(valid, "composition", new { });
        Ok(ProductionShotContractRules.Validate(noComp).Any(i => i.Attribute == "composition"), "08 missing composition → BLOCK");

        var dna = CharacterIdentityGovernanceRules.BuildGenericDna("9", ["different hair identity", "long", "shoulder_length"], ["glasses"]);
        var locked = new CharacterIdentityGovernanceRules.AuthorityState("CHAR-099", true, true, true, true, true, true, "LOCKED", "LOCKED", "LOCKED", masterId, dnaId, prpId);
        var govPass = CharacterIdentityGovernanceRules.Evaluate(dna, null, ProductionShotContractRules.ToGovernanceShotSpec(valid), null, ProductionShotContractRules.ToGovernancePrompt(valid), locked);
        Ok(!govPass.Any(c => c.Code is "DNA_SHOT_CONFLICT" or "DNA_FORBIDDEN_ATTRIBUTE"), "09 valid identity → PASS");

        var hairPrompt = ProductionShotContractRules.ToGovernancePrompt(ProductionShotContractRules.With(valid, "story.action", "tóc ngang vai"));
        Ok(CharacterIdentityGovernanceRules.EvaluateRequestsAgainstDna(
            CharacterIdentityGovernanceRules.CollectRequests(default, hairPrompt), dna, "USER_PROMPT")
            .Any(c => c.Attribute == "HAIR_LENGTH"), "10 forbidden hair → BLOCK");
        var ageSpec = JsonSerializer.SerializeToElement(new { identity = new { age = "14" } });
        Ok(CharacterIdentityGovernanceRules.Evaluate(dna, null, ageSpec, null, null, locked)
            .Any(c => c.Code == "DNA_SHOT_CONFLICT" && c.RequestedValue == "14"), "11 wrong age → BLOCK");
        Ok(CharacterIdentityGovernanceRules.EvaluateRequestsAgainstDna(
            CharacterIdentityGovernanceRules.CollectRequests(default, "shoulder-length hair"), dna, "USER_PROMPT")
            .Any(c => c.RequestedValue == "shoulder_length"), "12 semantic forbidden variation → BLOCK");
        var promptOverride = ProductionShotContractRules.With(valid, "character", new { expression = "focused", age = "14" });
        Ok(ProductionShotContractRules.CollectRequestedIdentity(promptOverride).Any(x => x.Attr == "age" && x.Value == "14"), "13 prompt-like identity override collected");
        Ok(CharacterIdentityGovernanceRules.Evaluate(dna, null, ProductionShotContractRules.ToGovernanceShotSpec(promptOverride), null, null, locked)
            .Any(c => c.RequestedValue == "14"), "13b identity override → BLOCK");
        var allowed = ProductionShotContractRules.With(valid, "character", new { expression = "sad", pose = "sitting", gaze = "down", movement = "still" });
        Ok(ProductionShotContractRules.Validate(allowed).Count == 0, "14 allowed variation → PASS");

        Ok(ProductionShotContractRules.HasSeparatedCamera(valid), "15 camera fields separate from action");
        Ok(ProductionShotContractRules.HasSeparatedMotion(valid), "16 motion fields separate from action");
        Ok(ProductionShotContractRules.PropsHaveState(valid), "17 props have state");
        Ok(ProductionShotContractRules.ContinuityHasPrevious(valid), "18 continuity has previousShot");
        Ok(ProductionShotContractRules.ConstraintsSeparated(valid), "19 mandatory/allowed/forbidden separated");
        Ok(!ProductionShotContractRules.ContainsForbiddenModelFields(valid), "20 no provider-specific model fields");
        Ok(!ProductionShotContractRules.ContainsPromptCompilerFields(valid), "21 no FINAL_PROMPT");
        var withModel = ProductionShotContractRules.With(valid, "production", new { aspectRatio = "16:9", runway_model = "gen4_turbo" });
        Ok(ProductionShotContractRules.ContainsForbiddenModelFields(withModel)
            && ProductionShotContractRules.Validate(withModel).Any(i => i.Message.Contains("provider")), "20b model field → BLOCK");

        var h1 = ProductionShotContractRules.HashCanonical(valid);
        var h2 = ProductionShotContractRules.HashCanonical(JsonSerializer.Deserialize<JsonElement>(valid.GetRawText()));
        Ok(h1.Length == 64 && h1 == h2, "22 canonical SHA stable");
        var changed = ProductionShotContractRules.With(valid, "timing.durationSeconds", 8);
        Ok(ProductionShotContractRules.HashCanonical(changed) != h1, "23 changed payload changes SHA");
        Ok(ProductionShotContractRules.IsApproved("DIRECTOR_APPROVED") && !ProductionShotContractRules.CanEdit("DIRECTOR_APPROVED"), "24 approved contract immutable");
        Ok(ProductionShotContractRules.NextVersion("V1") == "V2", "25 V2 required for change");
        Ok(valid.GetProperty("identity").GetProperty("masterSha256").GetString()?.Length == 64
            && valid.GetProperty("identity").GetProperty("dnaId").GetString()?.Length > 0, "26 provenance complete");
        Ok(ProductionShotContractRules.SameTenant("CHAR-099", "CHAR-099", "FAMIXA", "FAMIXA")
            && !ProductionShotContractRules.SameTenant("CHAR-099", "CHAR-001", "FAMIXA", "FAMIXA"), "27 tenant ownership enforced");

        Ok(!ProductionShotContractRules.AutoFix() && !ProductionShotContractRules.AutoApprove() && !ProductionShotContractRules.AutoLock(), "32 no auto-fix/approve/lock");
        Ok(!ProductionShotContractRules.CreatesPixels("SPEC") && ProductionShotContractRules.CreatesPixels("GEMINI") && ProductionShotContractRules.CreatesPixels("RUNWAY"), "33 no Gemini / 34 no Runway / 35 no generation");
        Ok(!ProductionShotContractRules.TouchesGolden("kit-video-master/CHAR-099/era.jpg") && ProductionShotContractRules.TouchesGolden("GOLDEN-SH01-01"), "36 Golden SH01-01 untouched");
        Ok(CharacterIdentityGovernanceRules.DirectorApproval() == "PENDING", "31 approve is director, not auto");
        var cameraInAction = ProductionShotContractRules.With(valid, "story.action", "camera slowly zooms while looking at paper");
        Ok(ProductionShotContractRules.Validate(cameraInAction).Any(i => i.Message.Contains("camera")), "15b camera in action → BLOCK");

        return fail;
    }
}
