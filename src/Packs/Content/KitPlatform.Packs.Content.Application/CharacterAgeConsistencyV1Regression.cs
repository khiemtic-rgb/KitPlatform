using System.Linq;
using System.Text.Json;

namespace KitPlatform.Packs.Content;

public static class CharacterAgeConsistencyV1Regression
{
    public const string SuiteId = CharacterAgeConsistencyV1Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var age11 = CharacterAgeConsistencyV1Rules.FromCanonicalAge(11);
        var age36 = CharacterAgeConsistencyV1Rules.FromCanonicalAge(36);
        var minhMaster = CharacterAuthorityInitializationV1Rules.ProtectedMasterSha;
        var minhDna = CharacterAuthorityInitializationV1Rules.ProtectedDnaSha;
        var minhPrp = CharacterAuthorityInitializationV1Rules.ProtectedPrpSha;
        var minhCrp = CharacterAuthorityInitializationV1Rules.ProtectedCrpSha;
        var style = ProjectVisualStyleV1Rules.PresetOf("3D_STYLIZED_REALISM")!;
        var styleSha = ProjectVisualStyleV1Rules.Sha(style);
        var sha = new string('b', 64);

        Ok(age11.ChronologicalAge == 11
            && age11.TargetAppearanceAgeMin == 10
            && age11.TargetAppearanceAgeMax == 12,
            "01 Age = 11 → default target 10-12");

        Ok(age36.ChronologicalAge == 36
            && age36.TargetAppearanceAgeMin == 33
            && age36.TargetAppearanceAgeMax == 39,
            "02 Age = 36 → default target 33-39");

        Ok(CharacterAgeConsistencyV1Rules.ValidateProfile(null, 10, 12)
                == CharacterAgeConsistencyV1Rules.GateNotReady,
            "03 Missing chronological age → AGE_PROFILE_NOT_READY");

        Ok(CharacterAgeConsistencyV1Rules.ValidateProfile(11, null, 12)
                == CharacterAgeConsistencyV1Rules.GateNotReady
            && CharacterAgeConsistencyV1Rules.ValidateProfile(11, 10, null)
                == CharacterAgeConsistencyV1Rules.GateNotReady,
            "04 Missing target range → AGE_PROFILE_NOT_READY");

        Ok(CharacterAgeConsistencyV1Rules.ValidateProfile(11, 12, 10)
                == CharacterAgeConsistencyV1Rules.GateInvalid,
            "05 Min > Max → INVALID_AGE_PROFILE");

        Ok(CharacterAgeConsistencyV1Rules.GenerationAllowed(11, 10, 12)
            && CharacterAgeConsistencyV1Rules.ValidateProfile(11, 10, 12) is null,
            "06 Valid age profile → generation allowed");

        Ok(!CharacterAgeConsistencyV1Rules.MayMarkPass(0, false)
            && CharacterAgeConsistencyV1Rules.ReviewGate("PASS", 0, false)
                == CharacterAgeConsistencyV1Rules.GateReviewNotReady,
            "07 CRP missing → age PASS review blocked");

        Ok(CharacterAgeConsistencyV1Rules.MayMarkPass(4, false)
            && CharacterAgeConsistencyV1Rules.ReviewGate("PASS", 4, false) is null,
            "08 CRP 4/4 + age review PASS → AGE_CONSISTENCY_PASS");

        Ok(CharacterAgeConsistencyV1Rules.ReviewGate("FAIL", 4, false) is null
            && CharacterAgeConsistencyV1Rules.RejectAgeMismatch == "AGE_MISMATCH",
            "09 CRP 4/4 + age review FAIL → AGE_MISMATCH");

        Ok(!CharacterAgeConsistencyV1Rules.MayBecomeReady(false, CharacterAgeConsistencyV1Rules.StatusFail)
            && !CharacterAgeConsistencyV1Rules.CharacterReadyAllowed(
                true, true, true, true, true, true, true, true, false, true, true),
            "10 Age FAIL → CHARACTER_READY blocked");

        Ok(CharacterAgeConsistencyV1Rules.MayRegenerateAfterAgeMismatch(
                CharacterAgeConsistencyV1Rules.RejectAgeMismatch, false)
            && CharacterStudioV1Rules.MayRegenerate(
                CharacterStudioV1Rules.CrpPendingReview, false, false, true)
            && CharacterStudioV1Rules.RegenerationSlots(
                CharacterAgeConsistencyV1Rules.RejectAgeMismatch, false).Count == 4,
            "11 Age FAIL → regeneration CTA available");

        Ok(CharacterStudioUnifiedGenerationV1Rules.RegenerationKeepsAuthority(
                sha, sha, minhMaster, minhMaster, minhDna, minhDna, minhPrp, minhPrp, styleSha, styleSha),
            "12 Regeneration → Identity SHA unchanged");

        Ok(CharacterAuthorityInitializationV1Rules.SameSha(minhMaster, minhMaster)
            && !CharacterStudioUnifiedGenerationV1Rules.RegeneratesMasterFromCrp(),
            "13 Regeneration → Master SHA unchanged");

        Ok(CharacterAuthorityInitializationV1Rules.SameSha(minhDna, minhDna),
            "14 Regeneration → DNA SHA unchanged");

        Ok(CharacterAuthorityInitializationV1Rules.SameSha(minhPrp, minhPrp),
            "15 Regeneration → PRP SHA unchanged");

        Ok(styleSha == ProjectVisualStyleV1Rules.Sha(style)
            && !ProjectVisualStyleV1Rules.RegeneratesExistingCharacters(),
            "16 Regeneration → Project Visual Style SHA unchanged");

        var fpBase = CharacterStudioUnifiedGenerationV1Rules.Fingerprint(
            "FAMIXA", "CHAR-099", styleSha, sha, sha, sha, sha);
        var fpAgeA = CharacterStudioUnifiedGenerationV1Rules.Fingerprint(
            "FAMIXA", "CHAR-099", styleSha, sha, sha, sha, sha, null,
            CharacterAgeConsistencyV1Rules.AgeProfileSha(age11));
        var fpAgeB = CharacterStudioUnifiedGenerationV1Rules.Fingerprint(
            "FAMIXA", "CHAR-099", styleSha, sha, sha, sha, sha, null,
            CharacterAgeConsistencyV1Rules.AgeProfileSha(age36));
        Ok(fpBase != fpAgeA && fpAgeA != fpAgeB,
            "17 Change Age Profile → generation fingerprint changes");

        var lockedGate = CharacterStudioV1Rules.Evaluate(new CharacterStudioV1Rules.GateInput(
            true, true, true, true, "REGENERATE", CharacterStudioV1Rules.CharacterReady,
            "GEMINI", true, true, false, false, true, false, null, true, styleSha));
        Ok(CharacterAgeConsistencyV1Rules.LockedBlocksRegeneration(true, true)
            && CharacterStudioV1Rules.ProtectedMinhUnchanged(minhMaster, minhDna, minhPrp, minhCrp)
            && lockedGate.Code is not null
            && !lockedGate.MayCallProvider
            && CharacterAgeConsistencyV1Rules.MayBecomeReady(true, CharacterAgeConsistencyV1Rules.StatusNotEvaluated),
            "18 Existing locked character → cannot modify");

        Ok(CharacterStudioV1Rules.ResolveState(new CharacterStudioV1Rules.StudioSnapshot(
                true, true, true, false, "LOCKED", "LOCKED", "LOCKED",
                CharacterStudioV1Rules.CrpPendingReview, 4, false, false, false, true))
                == CharacterStudioV1Rules.CrpPendingReview
            && !CharacterAgeConsistencyV1Rules.AutoApprove(),
            "19 Existing pending character → remains PENDING");

        Ok(CharacterStudioV1Rules.ResolveState(new CharacterStudioV1Rules.StudioSnapshot(
                true, true, true, false, "LOCKED", "LOCKED", "LOCKED",
                CharacterStudioV1Rules.Rejected, 4, false, false, true, true))
                == CharacterStudioV1Rules.Rejected
            && !CharacterAgeConsistencyV1Rules.AutoApprove(),
            "20 Existing rejected character → remains REJECTED");

        Ok(CharacterStudioV1Rules.ResolveState(new CharacterStudioV1Rules.StudioSnapshot(
                true, true, true, false, "LOCKED", "LOCKED", "LOCKED",
                null, 0, false, false, false, false))
                == CharacterStudioV1Rules.PrpReady,
            "21 Profile with PRP ready and 0/4 stays ungenerated");

        Ok(!CharacterAgeConsistencyV1Rules.CallsGeminiForValidation()
            && !CharacterAgeConsistencyV1Rules.InventsApparentAge(),
            "22 No Gemini called by age validation itself");

        Ok(!CharacterAgeConsistencyV1Rules.AutoApprove()
            && !CharacterStudioUnifiedGenerationV1Rules.AutoApprove(),
            "23 No auto approve");

        Ok(!CharacterAgeConsistencyV1Rules.AutoLock()
            && !CharacterStudioUnifiedGenerationV1Rules.AutoLock(),
            "24 No auto lock");

        Ok(!CharacterAgeConsistencyV1Rules.GeneratesVideo()
            && !CharacterStudioV1Rules.GeneratesVideo(),
            "25 No video generation");

        Ok(!CharacterAgeConsistencyV1Rules.GeneratesProduction()
            && !CharacterStudioV1Rules.OpensFirstRealProduction(),
            "26 No production generation");

        var contract = JsonSerializer.Serialize(
            CharacterAgeConsistencyV1Rules.GenerationContractAge(age36, "female"));
        var brief = CharacterStudioV1Rules.IdentityBrief(
            "CHAR-099", "Lan", 36, "female", "STYLE_3D_STYLIZED_REALISM",
            "long dark hair", "mother", "calm");
        var prpWithAge = CharacterAuthorityInitializationV1Rules.CompilePrpSpec(
            "CHAR-099", "Lan", "ERA-01", sha, sha, null, null,
            CharacterAgeConsistencyV1Rules.CompileAgeAppearanceProfile(age36, "female"));
        var prpPlain = CharacterAuthorityInitializationV1Rules.CompilePrpSpec(
            "CHAR-099", "Lan", "ERA-01", sha, sha);
        Ok(contract.Contains("33", StringComparison.Ordinal)
            && contract.Contains("39", StringComparison.Ordinal)
            && brief.Contains("approximately 33-39", StringComparison.OrdinalIgnoreCase)
            && !brief.Contains("young woman", StringComparison.OrdinalIgnoreCase)
            && JsonSerializer.Serialize(prpWithAge).Contains("age_appearance_profile", StringComparison.OrdinalIgnoreCase)
            && CharacterAuthorityInitializationV1Rules.SpecSha(prpPlain)
                != CharacterAuthorityInitializationV1Rules.SpecSha(prpWithAge)
            && !CharacterAgeConsistencyV1Rules.UsesCharacterSpecificBranch()
            && CharacterAgeConsistencyV1Rules.FaceAndAgeIndependent(),
            "27 Age Appearance Profile enters generation / PRP without character branches");

        return fail;
    }
}
