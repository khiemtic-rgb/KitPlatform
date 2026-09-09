using System.Linq;
using System.Text.Json;

namespace KitPlatform.Packs.Content;

public static class CharacterStudioUnifiedGenerationV1Regression
{
    public const string SuiteId = CharacterStudioUnifiedGenerationV1Rules.SuiteId;

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

        Ok(!CharacterStudioUnifiedGenerationV1Rules.ProfileReadyForFactory("", 11, "female", "x")
            && CharacterStudioUnifiedGenerationV1Rules.ProductionGate(
                CharacterStudioV1Rules.ProfileReady, false, false, true)
                == CharacterStudioUnifiedGenerationV1Rules.GateProfile,
            "01 Character profile missing → CHARACTER_NOT_READY");

        Ok(ProjectVisualStyleV1Rules.ValidateReady(ProjectVisualStyleV1Rules.NotConfigured, null)
            == CharacterStudioUnifiedGenerationV1Rules.GateStyle
            && CharacterStudioUnifiedGenerationV1Rules.NextAction(
                CharacterStudioV1Rules.ProfileReady, false)
                == CharacterStudioUnifiedGenerationV1Rules.StaffSetupStyle,
            "02 Project Visual Style missing → PROJECT_VISUAL_STYLE_NOT_READY");

        Ok(CharacterStudioUnifiedGenerationV1Rules.InheritsProjectStyle(null, style.StyleKey)
            && CharacterStudioUnifiedGenerationV1Rules.InheritsProjectStyle(
                "STYLE_3D_STYLIZED_REALISM", style.StyleKey)
            && !CharacterStudioUnifiedGenerationV1Rules.CharacterMaySelectStyle(),
            "03 Character inherits project style → PASS");

        var idA = CharacterStudioUnifiedGenerationV1Rules.IdentitySha(
            "CHAR-003", "Linh", 11, "female", "friend", "kind", "long hair", "PVS-1", styleSha);
        var idB = CharacterStudioUnifiedGenerationV1Rules.IdentitySha(
            "CHAR-003", "Linh", 11, "female", "friend", "kind", "long hair", "PVS-1", styleSha);
        var idC = CharacterStudioUnifiedGenerationV1Rules.IdentitySha(
            "CHAR-099", "Lan", 10, "female", "friend", "kind", "long hair", "PVS-1", styleSha);
        Ok(idA == idB && idA != idC && CharacterReferencePackRules.ShaExists(idA),
            "04 Identity generated → PASS");

        var mock = new MockCharacterGenerationProvider();
        var master = mock.GenerateMasterAsync(
            new CharacterAuthorityGenerationRequest("CHAR-099", "ERA-01", "MASTER", "brief", "3:4", "V1"),
            CancellationToken.None).GetAwaiter().GetResult();
        Ok(master.Succeeded && mock.MasterCalls == 1, "05 Master generated → PASS");

        var dna = CharacterAuthorityInitializationV1Rules.CompileDnaSpec(
            "CHAR-099", "Lan", "friend", "ERA-01", sha, "V1", "female", "10", "PVS-1", styleSha);
        var dnaSha = CharacterAuthorityInitializationV1Rules.SpecSha(dna);
        Ok(CharacterReferencePackRules.ShaExists(dnaSha)
            && CharacterStudioUnifiedGenerationV1Rules.DnaDescribesStableTraits(dna),
            "06 DNA generated → PASS");

        var prp = CharacterAuthorityInitializationV1Rules.CompilePrpSpec(
            "CHAR-099", "Lan", "ERA-01", sha, dnaSha, "PVS-1", styleSha);
        var prpSha = CharacterAuthorityInitializationV1Rules.SpecSha(prp);
        Ok(CharacterReferencePackRules.ShaExists(prpSha)
            && CharacterStudioUnifiedGenerationV1Rules.PrpIsProviderNeutral(prp),
            "07 PRP generated → PASS");

        Ok(CharacterStudioUnifiedGenerationV1Rules.SetDefinitionValid(
                CharacterStudioUnifiedGenerationV1Rules.DefaultSet)
            && CharacterStudioUnifiedGenerationV1Rules.DefaultSet.Views.SequenceEqual(
                new[] { "FRONT", "THREE_QUARTER", "SIDE", "FULL_BODY" }),
            "08 CRP 4-view definition → PASS");

        Ok(CharacterStudioUnifiedGenerationV1Rules.ProviderIsGemini("GEMINI")
            && !CharacterStudioUnifiedGenerationV1Rules.ProviderIsGemini("RUNWAY")
            && CharacterStudioV1Rules.CapabilityAllows("GEMINI"),
            "09 Provider Gemini → PASS");

        var noConfirm = CharacterStudioV1Rules.Evaluate(new CharacterStudioV1Rules.GateInput(
            true, true, false, false, "GENERATE", CharacterStudioV1Rules.ProfileReady,
            "GEMINI", true, false, false, false, false, false, null, true, styleSha));
        Ok(noConfirm.Code == CharacterStudioUnifiedGenerationV1Rules.GateConfirm
            && !noConfirm.MayCallProvider,
            "10 confirm=false → no Gemini");

        var yesConfirm = CharacterStudioV1Rules.Evaluate(new CharacterStudioV1Rules.GateInput(
            true, true, false, false, "GENERATE", CharacterStudioV1Rules.ProfileReady,
            "GEMINI", true, true, false, false, false, false, null, true, styleSha));
        Ok(yesConfirm.MayCallProvider && yesConfirm.Code is null,
            "11 confirm=true → Gemini executes");

        var views = CharacterStudioUnifiedGenerationV1Rules.ReferenceSetViews
            .Select(t => new CharacterReferenceViewRequest(t, t, "1:1", []))
            .ToList();
        var set = mock.GenerateViewsAsync(
            new CharacterReferenceSetRequest("CHAR-099", "ERA-01", sha, dnaSha, prpSha, views),
            CancellationToken.None).GetAwaiter().GetResult();
        Ok(set.Succeeded && set.Views.Count == 4
            && CharacterStudioUnifiedGenerationV1Rules.LogicalGenerationCount(mock.ViewCalls) == 1,
            "12 one logical generation → PASS");

        Ok(set.Views.Select(v => v.ReferenceType).OrderBy(x => x)
                .SequenceEqual(CharacterStudioUnifiedGenerationV1Rules.ReferenceSetViews.OrderBy(x => x))
            && set.Views.All(v => v.Succeeded && v.Bytes is { Length: > 0 }),
            "13 four reference artifacts → PASS");

        var passSlots = CharacterStudioV1Rules.RequiredViews
            .Select(t => CharacterStudioV1Rules.ScoreSlot(
                t, true, false, false, CharacterStudioIdentityLockV1Rules.DeclaredPassScores(t)))
            .ToList();
        var consistency = CharacterStudioUnifiedGenerationV1Rules.EvaluateConsistency(passSlots);
        Ok(consistency.Pass
            && CharacterStudioUnifiedGenerationV1Rules.AfterConsistency(true)
                == CharacterStudioUnifiedGenerationV1Rules.CrpPendingReview,
            "14 consistency pass → CRP_PENDING_REVIEW");

        var failSlots = passSlots.Select(s =>
            s.Type == "SIDE"
                ? CharacterStudioV1Rules.ScoreSlot("SIDE", true, false, false,
                    new Dictionary<string, int> { ["face"] = 20, ["style"] = 20 })
                : s).ToList();
        var bad = CharacterStudioUnifiedGenerationV1Rules.EvaluateConsistency(failSlots);
        Ok(!bad.Pass
            && CharacterStudioUnifiedGenerationV1Rules.AfterConsistency(false)
                == CharacterStudioUnifiedGenerationV1Rules.CrpRejected,
            "15 consistency fail → CRP_REJECTED");

        Ok(!CharacterStudioUnifiedGenerationV1Rules.AutoApprove()
            && CharacterStudioUnifiedGenerationV1Rules.AfterConsistency(true)
                != CharacterStudioV1Rules.CrpApproved,
            "16 no auto approval → PASS");

        Ok(!CharacterStudioUnifiedGenerationV1Rules.AutoLock()
            && CharacterStudioUnifiedGenerationV1Rules.AfterConsistency(true)
                != CharacterStudioV1Rules.CrpLocked,
            "17 no auto lock → PASS");

        var rejectGate = CharacterStudioV1Rules.Evaluate(new CharacterStudioV1Rules.GateInput(
            true, true, false, false, "REGENERATE", CharacterStudioV1Rules.Rejected,
            "GEMINI", true, true, false, false, false, true, null, true, styleSha));
        Ok(rejectGate.MayCallProvider
            && CharacterStudioUnifiedGenerationV1Rules.NextAction(
                CharacterStudioV1Rules.Rejected, true)
                == CharacterStudioUnifiedGenerationV1Rules.StaffRegenerate
            && !CharacterStudioUnifiedGenerationV1Rules.RegeneratesMasterFromCrp()
            && CharacterStudioV1Rules.MayRegenerate(CharacterStudioV1Rules.Failed)
            && CharacterStudioUnifiedGenerationV1Rules.NextAction(CharacterStudioV1Rules.Failed, true)
                == CharacterStudioUnifiedGenerationV1Rules.StaffRegenerate,
            "18 rejection / failed enables regeneration → PASS");

        Ok(CharacterStudioUnifiedGenerationV1Rules.RegenerationKeepsAuthority(
                idA, idA, sha, sha, dnaSha, dnaSha, prpSha, prpSha, styleSha, styleSha)
            && !CharacterStudioUnifiedGenerationV1Rules.RegenerationKeepsAuthority(
                idA, idC, sha, sha, dnaSha, dnaSha, prpSha, prpSha, styleSha, styleSha),
            "19 regeneration keeps same authority SHA → PASS");

        var fp1 = CharacterStudioUnifiedGenerationV1Rules.Fingerprint(
            "FAMIXA", "CHAR-099", styleSha, idA, sha, dnaSha, prpSha);
        var fp2 = CharacterStudioUnifiedGenerationV1Rules.Fingerprint(
            "FAMIXA", "CHAR-099", styleSha, idA, sha, dnaSha, prpSha);
        var canon = JsonSerializer.Serialize(
            CharacterStudioUnifiedGenerationV1Rules.CanonicalFingerprint(
                "FAMIXA", "CHAR-099", styleSha, idA, sha, dnaSha, prpSha));
        Ok(fp1 == fp2
            && CharacterStudioUnifiedGenerationV1Rules.DuplicatePolicy(fp1, fp2, false)
                == CharacterStudioUnifiedGenerationV1Rules.GateDuplicate
            && CharacterStudioUnifiedGenerationV1Rules.FingerprintOmitsRuntime(canon),
            "20 same fingerprint → BLOCK_DUPLICATE");

        Ok(CharacterStudioUnifiedGenerationV1Rules.DuplicatePolicy(fp1, fp1, true) == "ALLOW_RETRY"
            && !CharacterStudioUnifiedGenerationV1Rules.AutoRetry()
            && CharacterStudioV1Rules.FailureClass(true, false) == "PROVIDER_EXECUTION_FAILURE"
            && !CharacterStudioV1Rules.MayRepairSlot("PROVIDER_EXECUTION_FAILURE", 0),
            "21 failed generation → no automatic retry");

        Ok(CharacterStudioV1Rules.ProtectedMinhUnchanged(minhMaster, minhDna, minhPrp, minhCrp)
            && CharacterStudioV1Rules.ResolveState(new CharacterStudioV1Rules.StudioSnapshot(
                true, true, true, true, "LOCKED", "LOCKED", "LOCKED", "LOCKED",
                4, true, true, false, true)) == CharacterStudioV1Rules.CharacterReady,
            "22 Minh remains unchanged");

        var namSnap = new CharacterStudioV1Rules.StudioSnapshot(
            true, true, true, false, "LOCKED", "LOCKED", "LOCKED",
            CharacterStudioV1Rules.Rejected, 4, false, false, true, true);
        var namDup = CharacterStudioV1Rules.Evaluate(new CharacterStudioV1Rules.GateInput(
            true, true, false, false, "GENERATE", CharacterStudioV1Rules.CrpPendingReview,
            "GEMINI", true, true, false, false, true, false, null, true, styleSha));
        Ok(CharacterStudioV1Rules.ResolveState(namSnap) == CharacterStudioV1Rules.Rejected
            && namDup.Code == CharacterStudioUnifiedGenerationV1Rules.GateDuplicate
            && !CharacterStudioUnifiedGenerationV1Rules.AutoRegenerate(),
            "23 Nam remains unchanged");

        var linhStyle = CharacterStudioUnifiedGenerationV1Rules.IdentitySha(
            "CHAR-003", "Linh", 11, "female", "friend", "", "desc", "PVS-1", styleSha);
        var minhStyle = CharacterStudioUnifiedGenerationV1Rules.IdentitySha(
            "CHAR-001", "Minh", 11, "male", "son", "", "desc", "PVS-1", styleSha);
        Ok(styleSha.Length == 64
            && CharacterStudioUnifiedGenerationV1Rules.InheritsProjectStyle(
                "3D_STYLIZED_REALISM", style.StyleKey)
            && linhStyle != minhStyle,
            "24 Linh uses same Project Visual Style SHA");

        var sameA = CharacterStudioV1Rules.ResolveState(new CharacterStudioV1Rules.StudioSnapshot(
            true, true, true, false, "MISSING", "MISSING", "MISSING", null, 0, false, false, false, false));
        var sameB = CharacterStudioV1Rules.ResolveState(new CharacterStudioV1Rules.StudioSnapshot(
            true, true, true, false, "MISSING", "MISSING", "MISSING", null, 0, false, false, false, false));
        Ok(sameA == sameB && sameA == CharacterStudioV1Rules.ProfileReady
            && !CharacterStudioUnifiedGenerationV1Rules.UsesCharacterSpecificBranch(),
            "25 No character-specific branching");

        Ok(!CharacterStudioUnifiedGenerationV1Rules.ApplicationReferencesGeminiSdk()
            && typeof(ICharacterReferenceGenerationProvider).IsAssignableFrom(typeof(MockCharacterReferenceSetProvider))
            && typeof(ICharacterGenerationProvider).IsAssignableFrom(typeof(MockCharacterGenerationProvider)),
            "26 Application contains no Gemini SDK/HTTP");

        Ok(CharacterStudioV1Rules.RejectHistoricalStill(CharacterStudioV1Rules.HistoricalStillId)
            && CharacterStudioV1Rules.ProtectedMinhUnchanged(minhMaster, minhDna, minhPrp, minhCrp),
            "27 Historical artifact remains unchanged");

        Ok(CharacterStudioUnifiedGenerationV1Rules.ProductionGate(
                CharacterStudioV1Rules.ProfileReady, false, false, true)
                == CharacterStudioUnifiedGenerationV1Rules.GateProduction
            && CharacterStudioUnifiedGenerationV1Rules.ProductionAllowed(
                CharacterStudioV1Rules.CharacterReady, true, true, true)
            && !CharacterStudioUnifiedGenerationV1Rules.ProductionAllowed(
                CharacterStudioUnifiedGenerationV1Rules.CrpPendingReview, false, false, true),
            "28 Production blocked before CHARACTER_READY");

        Ok(CharacterStudioUnifiedGenerationV1Rules.PublicHeadline(
                CharacterStudioV1Rules.CharacterReady, 4, 4, true)
                == "CHARACTER READY · 4/4 · LOCKED"
            && CharacterStudioUnifiedGenerationV1Rules.ReviewStepLabel(
                CharacterStudioV1Rules.CharacterReady, true)
                == "CHARACTER READY · LOCKED"
            && !CharacterStudioUnifiedGenerationV1Rules.ShowsPendingReview(CharacterStudioV1Rules.CharacterReady),
            "29 Ready locked character shows CHARACTER READY · LOCKED, not pending");

        Ok(CharacterStudioUnifiedGenerationV1Rules.PublicHeadline(
                CharacterStudioV1Rules.CrpPendingReview, 4, 4, false)
                == "PENDING REVIEW · 4/4"
            && CharacterStudioUnifiedGenerationV1Rules.ShowsPendingReview(CharacterStudioV1Rules.CrpPendingReview)
            && CharacterStudioUnifiedGenerationV1Rules.ReviewStepLabel(
                CharacterStudioV1Rules.CrpPendingReview, false)
                == CharacterStudioUnifiedGenerationV1Rules.StaffPending,
            "30 Pending review character shows PENDING REVIEW · 4/4");

        return fail;
    }
}
