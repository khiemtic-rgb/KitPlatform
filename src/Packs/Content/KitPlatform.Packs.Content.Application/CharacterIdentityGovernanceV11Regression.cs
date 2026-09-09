using System.Linq;
using System.Text.Json;

namespace KitPlatform.Packs.Content;

public static class CharacterIdentityGovernanceV11Regression
{
    public const string SuiteId = "CHARACTER_IDENTITY_GOVERNANCE_V1_1_REGRESSION";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var dna = CharacterIdentityGovernanceRules.BuildGenericDna("9", ["different hair identity", "long", "shoulder_length"], ["glasses"]);
        var locked = new CharacterIdentityGovernanceRules.AuthorityState("CHAR-099", true, true, true, true, true, true, "LOCKED", "LOCKED", "LOCKED", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var missing = new CharacterIdentityGovernanceRules.AuthorityState("CHAR-098", false, false, false, false, false, false, "MISSING", "MISSING", "MISSING");

        var passConflicts = CharacterIdentityGovernanceRules.Evaluate(dna, null, JsonSerializer.SerializeToElement(new { camera = "3/4 view", lighting = "soft" }), null, "góc thấp, lighting ấm", locked);
        Ok(locked.MasterLocked && passConflicts.All(c => c.Attribute != "status" || c.Code != "MASTER_CONFLICT"), "01 Master LOCKED → authority accepted");
        Ok(CharacterIdentityGovernanceRules.Evaluate(dna, null, null, null, null, missing).Any(c => c.Code == "MASTER_CONFLICT"), "02 Master missing → BLOCK");
        var shaBad = locked with { MasterShaMatch = false };
        Ok(CharacterIdentityGovernanceRules.Evaluate(dna, null, null, null, null, shaBad).Any(c => c.Code == "SHA_MISMATCH" && c.Attribute == "sha256"), "03 Master SHA mismatch → BLOCK");

        Ok(CharacterIdentityGovernanceRules.Evaluate(dna, null, null, null, null, locked).All(c => c.Source != "CHARACTER_DNA" || c.Attribute != "status"), "04 DNA LOCKED → no DNA status conflict");
        Ok(CharacterIdentityGovernanceRules.Evaluate(dna, null, null, null, null, locked with { DnaLocked = false, DnaStatus = "MISSING" }).Any(c => c.Code == "DNA_CONFLICT"), "05 DNA missing → BLOCK");
        Ok(CharacterIdentityGovernanceRules.Evaluate(dna, null, null, null, null, locked with { DnaShaMatch = false }).Any(c => c.Code == "SHA_MISMATCH"), "06 DNA SHA mismatch → BLOCK");

        Ok(CharacterIdentityGovernanceRules.Evaluate(dna, null, null, null, null, locked).All(c => c.Code != "PRP_CONFLICT" || c.Attribute != "status"), "07 PRP LOCKED → no PRP status conflict");
        Ok(CharacterIdentityGovernanceRules.Evaluate(dna, null, null, null, null, locked with { PrpLocked = false, PrpStatus = "MISSING" }).Any(c => c.Code == "PRP_CONFLICT"), "08 PRP missing → BLOCK");

        var inheritShot = JsonSerializer.SerializeToElement(new { identity = new { age = dna.GetProperty("age") } });
        Ok(!CharacterIdentityGovernanceRules.Evaluate(dna, null, inheritShot, null, null, locked).Any(c => c.Code is "DNA_SHOT_CONFLICT" or "DNA_FORBIDDEN_ATTRIBUTE"), "15 inherited DNA blob is not a requested override");
        var ageShot = JsonSerializer.SerializeToElement(new { identity = new { age = "9" }, camera = "3/4 view" });
        var identityPass = CharacterIdentityGovernanceRules.Evaluate(dna, null, ageShot, null, null, locked);
        Ok(!identityPass.Any(c => c.Attribute is "age" or "AGE"), "09 valid identity → no age conflict");

        var hairConflicts = CharacterIdentityGovernanceRules.EvaluateRequestsAgainstDna(
            CharacterIdentityGovernanceRules.CollectRequests(default, "tóc ngang vai"), dna, "USER_PROMPT");
        Ok(hairConflicts.Any(c => c.Code == "DNA_FORBIDDEN_ATTRIBUTE" && c.Attribute == "HAIR_LENGTH"), "10 forbidden hair → BLOCK");
        Ok(hairConflicts.Any(c => c.RequestedValue == "shoulder_length"), "12 forbidden attribute semantic variant → BLOCK");

        var ageConflicts = CharacterIdentityGovernanceRules.Evaluate(
            dna, null, JsonSerializer.SerializeToElement(new { identity = new { age = "14" } }), null, null, locked);
        Ok(ageConflicts.Any(c => c.Code == "DNA_SHOT_CONFLICT" && c.RequestedValue == "14" && c.AuthoritativeValue == "9"), "11 forbidden age → BLOCK from DNA not fallback");

        var allowedFirst = JsonSerializer.Deserialize<JsonElement>("""{"age":{"allowed":["camera"],"forbidden":["different age"],"invariant":["age appearance approximately 7 years old"]}}""");
        Ok(CharacterIdentityGovernanceRules.ExtractAuthoritativeAge(allowedFirst) == "7", "32 age from DNA invariant, not Compact order");
        var collectThrew = false;
        try
        {
            _ = CharacterIdentityGovernanceRules.CollectRequests(JsonSerializer.SerializeToElement(new { identity = new { body = "same proportion" } }), null);
        }
        catch (ArgumentNullException)
        {
            collectThrew = true;
        }
        Ok(!collectThrew, "18 CollectRequests does not throw on body-only spec");

        var faceConflicts = CharacterIdentityGovernanceRules.Evaluate(
            dna, null, JsonSerializer.SerializeToElement(new { identity = new { face = "different adult face" } }), null, null, locked);
        Ok(faceConflicts.Any(c => c.Code is "IDENTITY_CONFLICT" or "DNA_FORBIDDEN_ATTRIBUTE" or "DNA_SHOT_CONFLICT"), "13 invariant mutation → BLOCK");

        var allowed = CharacterIdentityGovernanceRules.Evaluate(dna, null, JsonSerializer.SerializeToElement(new { camera = "low angle", pose = "walk" }), null, "3/4 view lighting", locked);
        Ok(!allowed.Any(c => c.Attribute is "CAMERA" or "LIGHTING"), "14 allowed variation → PASS");

        Ok(!CharacterIdentityGovernanceRules.AutoFix() && !CharacterIdentityGovernanceRules.AutoApprove() && !CharacterIdentityGovernanceRules.AutoLock(), "23 no auto-fix/approve/lock");
        Ok(!CharacterIdentityGovernanceRules.CreatesPixels("SPEC") && CharacterIdentityGovernanceRules.CreatesPixels("GEMINI") && CharacterIdentityGovernanceRules.CreatesPixels("RUNWAY"), "26 no generation");
        Ok(!CharacterIdentityGovernanceRules.TouchesGolden("kit-video-master/CHAR-099/ERA-01/cand.jpg") && CharacterIdentityGovernanceRules.TouchesGolden("GOLDEN-SH01-01"), "27 Golden untouched");
        Ok(CharacterIdentityGovernanceRules.DirectorApproval() == "PENDING", "24 no auto-approve director");
        Ok(!CharacterIdentityGovernanceRules.AutoRegenerate() && !CharacterIdentityGovernanceRules.ModelHasIdentityAuthority(), "25 no auto-lock / model authority");

        var multi = CharacterIdentityGovernanceRules.Evaluate(
            dna, null,
            JsonSerializer.SerializeToElement(new { identity = new { age = "14" }, glasses = "round glasses" }),
            null, "tóc ngang vai", locked);
        Ok(multi.Count >= 2, "29 all conflicts returned");

        var prev = JsonSerializer.SerializeToElement(new { master_id = locked.MasterId, identity = new { age = "9" }, continuity = new { master_id = locked.MasterId } });
        var curOk = JsonSerializer.SerializeToElement(new { master_id = locked.MasterId, identity = new { age = "9" }, camera = "3/4", continuity = new { master_id = locked.MasterId } });
        var curBad = JsonSerializer.SerializeToElement(new { master_id = locked.MasterId, identity = new { age = "14" }, continuity = new { master_id = locked.MasterId } });
        Ok(!CharacterIdentityGovernanceRules.EvaluateContinuity(prev, curOk, dna, locked).Any(), "19 valid continuity → PASS");
        Ok(CharacterIdentityGovernanceRules.EvaluateContinuity(prev, curBad, dna, locked).Any(c => c.Attribute == "age"), "20 identity continuity conflict → BLOCK");

        Ok(CharacterIdentityGovernanceRules.StressState("10/10 PASS", true) == "STRESS_PASS", "21 verified stress → PASS");
        Ok(CharacterIdentityGovernanceRules.StressState(null, true) == "STRESS_DEFINED" && !CharacterIdentityGovernanceRules.StressGatePass("STRESS_DEFINED"), "22 missing stress verification → BLOCK");
        Ok(CharacterIdentityGovernanceRules.StressState(null, false) == "IDENTITY_STRESS_NOT_VERIFIED", "22b no stress record → IDENTITY_STRESS_NOT_VERIFIED");

        Ok(CharacterIdentityGovernanceRules.NormalizeCharacterId("") == "", "31 no hardcoded default character");
        Ok(CharacterIdentityGovernanceRules.NormalizeCharacterId("CHAR-099-TEST") == "CHAR-099", "31b normalize without Minh fallback");
        Ok(CharacterIdentityGovernanceRules.HierarchyHolds(), "32 hierarchy / no model authority");
        Ok(!CharacterIdentityGovernanceRules.AutoOverride(), "18 no bypass auto-override");

        var gates = CharacterIdentityGovernanceRules.EvaluateGates(locked, true, "STRESS_PASS", true, true, passConflicts.Where(c => false).ToList());
        Ok(gates.Select(g => g.Code).SequenceEqual(CharacterIdentityGovernanceRules.GateCodes), "08b all named gates present");
        Ok(CharacterIdentityGovernanceRules.ProductionReady(gates, 0) && !CharacterIdentityGovernanceRules.CreatesPixels("CREATE"), "26b production ready ≠ generate");

        var unread = CharacterIdentityGovernanceRules.EvaluateRequestsAgainstDna(
            [new CharacterIdentityGovernanceRules.AttributeRequest("AGE", "14", "shot")], default, "SHOT_SPECIFICATION");
        Ok(unread.Any(c => c.Message.Contains("unreadable", StringComparison.OrdinalIgnoreCase)), "32 no keyword-only / unreadable DNA blocks");

        return fail;
    }
}
