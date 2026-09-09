using System.Linq;

namespace KitPlatform.Packs.Content;

public static class VisualCalibrationPackV1HardeningRegression
{
    public const string SuiteId = VisualCalibrationPackV1Rules.HardeningSuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var pack = VisualCalibrationPackV1Rules.CompilePack();
        var draft = pack;
        var generating = pack with { Status = VisualCalibrationPackV1Rules.StatusGenerating };
        var pending0 = pack with { Status = VisualCalibrationPackV1Rules.StatusPendingReview };
        var pending23 = VisualCalibrationPackV1Rules.WithPixelCoverage(pending0, 23);
        var pending24 = VisualCalibrationPackV1Rules.WithPixelCoverage(pending0, 24);
        var approved = VisualCalibrationPackV1Rules.Approve(pending24);
        var rejected = VisualCalibrationPackV1Rules.Reject(pending24, "Director reject");

        Ok(VisualCalibrationPackV1Rules.EvaluateApprove(draft, true, VisualCalibrationPackV1Rules.PhotorealismLow)
            == VisualCalibrationPackV1Rules.GateInvalidState, "H-01 DRAFT cannot approve");
        Ok(VisualCalibrationPackV1Rules.EvaluateLock(draft.Status, true, false)
            == VisualCalibrationPackV1Rules.GateInvalidState, "H-02 DRAFT cannot lock");
        Ok(VisualCalibrationPackV1Rules.EvaluateApprove(generating, true, VisualCalibrationPackV1Rules.PhotorealismLow)
            == VisualCalibrationPackV1Rules.GateInvalidState, "H-03 GENERATING cannot approve");
        Ok(VisualCalibrationPackV1Rules.GetCoverage(pending0).Valid == 0
            && VisualCalibrationPackV1Rules.EvaluateApprove(pending0, true, VisualCalibrationPackV1Rules.PhotorealismLow)
                == VisualCalibrationPackV1Rules.GatePixelsNotReady, "H-04 PENDING_REVIEW 0/24 cannot approve");
        Ok(VisualCalibrationPackV1Rules.GetCoverage(pending23) is { Valid: 23, Required: 24 }
            && VisualCalibrationPackV1Rules.EvaluateApprove(pending23, true, VisualCalibrationPackV1Rules.PhotorealismLow)
                == VisualCalibrationPackV1Rules.GatePixelsNotReady, "H-05 PENDING_REVIEW 23/24 cannot approve");
        Ok(VisualCalibrationPackV1Rules.GetCoverage(pending24) is { Valid: 24, Required: 24, Missing: 0 }
            && VisualCalibrationPackV1Rules.EvaluateApprove(pending24, true, VisualCalibrationPackV1Rules.PhotorealismLow) is null, "H-06 PENDING_REVIEW 24/24 can approve");
        Ok(!VisualCalibrationPackV1Rules.AutoLock()
            && approved.Status == VisualCalibrationPackV1Rules.StatusApproved
            && !approved.AuthorityTransitioned
            && VisualCalibrationPackV1Rules.EvaluateLock(approved.Status, true, false) is null, "H-07 APPROVED cannot auto-lock");
        Ok(rejected.VisualUniverseSha == pack.VisualUniverseSha
            && rejected.CurrentAuthority == pack.CurrentAuthority
            && !rejected.AuthorityTransitioned, "H-08 REJECTED does not change authority");
        Ok(approved.VisualUniverseSha == pack.VisualUniverseSha
            && !approved.CurrentAuthority
            && !approved.AuthorityTransitioned, "H-09 APPROVE does not change authority");
        Ok(VisualCalibrationPackV1Rules.EvaluateLock(approved.Status, false, false)
                == VisualCalibrationPackV1Rules.GateConfirmation
            && VisualCalibrationPackV1Rules.Lock(approved).Status == VisualCalibrationPackV1Rules.StatusLocked
            && !VisualCalibrationPackV1Rules.Lock(approved).AuthorityTransitioned, "H-10 LOCK requires explicit action");
        Ok(!VisualCalibrationPackV1Rules.PretendsPackLockIsUniverseLock()
            && !VisualCalibrationPackV1Rules.ReplacesVisualUniverse()
            && !VisualCalibrationPackV1Rules.Lock(approved).CurrentAuthority
            && FamixaVisualUniverseAuthorityV1Rules.EvaluateAdvance(
                FamixaVisualUniverseAuthorityV1Rules.StatusDraft, "LOCK") is not null, "H-11 LOCK does not pretend VUA authority changed");
        var current = FamixaVisualUniverseAuthorityV1Rules.Sha();
        var oldBound = ProjectVisualStyleV2Rules.ProtectedV1Sha;
        Ok(VisualCalibrationPackV1Rules.VisualUniverseStale(oldBound, current)
            && VisualCalibrationPackV1Rules.MayMaterializeStale(false, true)
            && !VisualCalibrationPackV1Rules.MayMaterializeStale(true, true)
            && !VisualCalibrationPackV1Rules.MayMaterializeStale(false, false), "H-12 Impact stale only after authority transition");
        Ok(FamixaVisualUniverseAuthorityV1Rules.MutationForbidden(true)
            && !VisualCalibrationPackV1Rules.MutatesLockedCharacters()
            && !VisualCalibrationPackV1Rules.MayMaterializeStale(true, true), "H-13 Locked character is never mutated");
        Ok(CharacterAuthorityInitializationV1Rules.ProtectedMasterSha
                == "be439c39e067aa6c7727255e9643ac78cb7c6285917af60dda38bf14a32518f1"
            && CharacterAuthorityInitializationV1Rules.ProtectedDnaSha
                == "75ececad8899211ce31107232fe0288c11a9e113c5bc0e7c0a6c9f749d72f4dc"
            && CharacterAuthorityInitializationV1Rules.ProtectedPrpSha
                == "5e61ad240aaebaa13dcd91463a41ef9f9c0498fabefe86b7e8b1a1ad973a9444"
            && CharacterAuthorityInitializationV1Rules.ProtectedCrpSha
                == "82543a4a4331e32a79a865fc3881c17e8bc3dc74c5dec51c52c2deab1a70c2b7", "H-14 Minh SHA unchanged");
        Ok(!VisualCalibrationPackV1Rules.UsesCharacterName()
            && !VisualCalibrationPackV1Rules.ContainsCharacterName(pending24.Subjects[0].Label)
            && VisualCalibrationPackV1Rules.VisualUniverseStale(oldBound, current), "H-15 No name-based branch");
        Ok(VisualCalibrationPackV1Rules.ValidateGenerate(pack.VisualUniverseSha, pack.ProjectVisualStyleSha,
                pack.CharacterDesignLanguageSha, false)
            == VisualCalibrationPackV1Rules.GateConfirmation, "H-16 confirm=false blocks provider");
        Ok(!VisualCalibrationPackV1Rules.CallsGemini()
            && VisualCalibrationPackV1Rules.ToDto(pack).ProviderCalled == false
            && VisualCalibrationPackV1Rules.ToDto(pack).GenerationExecuted == false, "H-17 No provider call during regression");
        var fakePromptAsPixel = VisualCalibrationPackV1Rules.WithPixelCoverage(pending0, 24, VisualCalibrationPackV1Rules.KindPrompt);
        Ok(VisualCalibrationPackV1Rules.GetCoverage(fakePromptAsPixel).Valid == 0
            && VisualCalibrationPackV1Rules.EvaluateApprove(fakePromptAsPixel, true, VisualCalibrationPackV1Rules.PhotorealismLow)
                == VisualCalibrationPackV1Rules.GatePixelsNotReady, "H-18 No fake artifact counts");
        Ok(VisualCalibrationPackV1Rules.EvaluateApprove(pending0, true, VisualCalibrationPackV1Rules.PhotorealismLow)
            == VisualCalibrationPackV1Rules.GatePixelsNotReady, "H-19 Missing artifact blocks approval");
        var wrongSha = pending24 with
        {
            Subjects = pending24.Subjects.Select((s, i) => i == 0
                ? s with
                {
                    Artifacts = s.Artifacts.Select((a, n) => n == 0
                        ? a with { VisualUniverseSha = new string('0', 64) }
                        : a).ToList(),
                }
                : s).ToList(),
        };
        Ok(VisualCalibrationPackV1Rules.GetCoverage(wrongSha).Valid == 23
            && VisualCalibrationPackV1Rules.EvaluateApprove(wrongSha, true, VisualCalibrationPackV1Rules.PhotorealismLow)
                == VisualCalibrationPackV1Rules.GatePixelsNotReady, "H-20 Wrong SHA artifact blocks approval");
        var wrongPack = pending24 with
        {
            Subjects = pending24.Subjects.Select((s, i) => i == 0
                ? s with
                {
                    Artifacts = s.Artifacts.Select((a, n) => n == 0
                        ? a with { PackId = "OTHER-PACK" }
                        : a).ToList(),
                }
                : s).ToList(),
        };
        Ok(VisualCalibrationPackV1Rules.EvaluateApprove(wrongPack, true, VisualCalibrationPackV1Rules.PhotorealismLow)
            == VisualCalibrationPackV1Rules.GatePixelsNotReady, "H-21 Wrong pack artifact blocks approval");
        var wrongSlot = pending24 with
        {
            Subjects = pending24.Subjects.Select((s, i) => i == 0
                ? s with
                {
                    Artifacts = s.Artifacts.Select((a, n) => n == 0
                        ? a with { SlotId = "CAL-001/SIDE" }
                        : a).ToList(),
                }
                : s).ToList(),
        };
        Ok(VisualCalibrationPackV1Rules.EvaluateApprove(wrongSlot, true, VisualCalibrationPackV1Rules.PhotorealismLow)
            == VisualCalibrationPackV1Rules.GatePixelsNotReady, "H-22 Wrong slot artifact blocks approval");

        return fail;
    }
}
