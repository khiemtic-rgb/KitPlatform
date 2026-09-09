using System.Linq;

namespace KitPlatform.Packs.Content;

public static class VisualCalibrationPackV1LiveGenerationRegression
{
    public const string SuiteId = VisualCalibrationPackV1Rules.LiveGenerationOfficialSuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var pack = VisualCalibrationPackV1Rules.CompilePack();
        var plan = VisualCalibrationGenerationServiceV1.CompilePlan(pack, "CAL-EXEC-LG");
        var blockedMock = new MockVisualCalibrationGenerationProvider();
        var blocked = VisualCalibrationGenerationServiceV1.ExecuteAsync(
            pack, blockedMock, false, true).GetAwaiter().GetResult();
        Ok(blocked.GateCode == VisualCalibrationPackV1Rules.GateConfirmation
            && !blocked.ProviderCalled
            && !blocked.GenerationExecuted
            && blockedMock.CallCount == 0,
            "LG-01 confirm=false blocks provider");

        var dryMock = new MockVisualCalibrationGenerationProvider();
        var dry = VisualCalibrationGenerationServiceV1.ExecuteAsync(
            pack, dryMock, true, false).GetAwaiter().GetResult();
        Ok(dry.GateCode is null
            && !dry.ProviderCalled
            && !dry.GenerationExecuted
            && dryMock.CallCount == 0
            && VisualCalibrationPackV1Rules.GetCoverage(dry.Pack).Valid == 0,
            "LG-02 confirm=true + generate=false blocks generation");

        var liveMock = new MockVisualCalibrationGenerationProvider();
        var live = VisualCalibrationGenerationServiceV1.ExecuteAsync(
            pack, liveMock, true, true).GetAwaiter().GetResult();
        Ok(live.ProviderCalled
            && live.GenerationExecuted
            && liveMock.CallCount == 24,
            "LG-03 confirm=true + generate=true reaches provider");

        Ok(plan.Count == 24
            && VisualCalibrationPackV1Rules.DefaultMatrix.Count == 6
            && VisualCalibrationPackV1Rules.RequiredViews.Count == 4,
            "LG-04 exactly 24 unique slots");
        Ok(plan.Select(r => r.SlotId).Distinct().Count() == 24
            && plan.All(r => r.SlotId == VisualCalibrationPackV1Rules.SlotIdOf(r.SubjectId, r.ViewType)),
            "LG-05 no duplicate slot IDs");
        Ok(plan.Select(r => r.VisualUniverseSha).Distinct().Count() == 1
            && plan.All(r => r.VisualUniverseSha == pack.VisualUniverseSha)
            && pack.VisualUniverseSha == FamixaVisualUniverseAuthorityV1Rules.Sha(),
            "LG-06 all 24 bind same VUA SHA");
        Ok(plan.Select(r => r.ProjectVisualStyleSha).Distinct().Count() == 1
            && plan.All(r => r.ProjectVisualStyleSha == ProjectVisualStyleV2Rules.ProtectedV1Sha),
            "LG-07 all 24 bind same PVS SHA");
        Ok(plan.Select(r => r.CharacterDesignLanguageSha).Distinct().Count() == 1
            && plan.All(r => r.CharacterDesignLanguageSha == CharacterDesignLanguageV2Rules.Sha()),
            "LG-08 all 24 bind same CDL SHA");

        Ok(live.Pack.Subjects.SelectMany(s => s.Artifacts).Count(a =>
                string.Equals(a.Kind, VisualCalibrationPackV1Rules.KindPixel, StringComparison.OrdinalIgnoreCase)) == 24
            && live.Pack.Subjects.SelectMany(s => s.Artifacts).All(a =>
                VisualCalibrationPackV1Rules.IsValidPixel(live.Pack, a)),
            "LG-09 successful generation creates PIXEL only");

        var promptOnly = VisualCalibrationPackV1Rules.WithPixelCoverage(
            pack with { Status = VisualCalibrationPackV1Rules.StatusPendingReview },
            24, VisualCalibrationPackV1Rules.KindPrompt);
        Ok(VisualCalibrationPackV1Rules.GetCoverage(promptOnly).Valid == 0
            && VisualCalibrationPackV1Rules.EvaluateApprove(
                promptOnly, true, VisualCalibrationPackV1Rules.PhotorealismLow)
                == VisualCalibrationPackV1Rules.GatePixelsNotReady,
            "LG-10 prompt artifact cannot count as PIXEL");

        Ok(VisualCalibrationPackV1Rules.GetCoverage(live.Pack) is { Valid: 24, Required: 24, Missing: 0 }
            && live.Pack.Status == VisualCalibrationPackV1Rules.StatusPendingReview,
            "LG-11 24/24 valid → PENDING_REVIEW");
        Ok(live.Pack.Status != VisualCalibrationPackV1Rules.StatusApproved
            && live.Pack.DirectorPass != true
            && !VisualCalibrationPackV1Rules.AutoApprove(),
            "LG-12 generation does not APPROVE");
        Ok(live.Pack.Status != VisualCalibrationPackV1Rules.StatusLocked
            && !VisualCalibrationPackV1Rules.AutoLock(),
            "LG-13 generation does not LOCK");
        Ok(!live.Pack.AuthorityTransitioned
            && !live.Pack.CurrentAuthority
            && !VisualCalibrationPackV1Rules.ReplacesVisualUniverse()
            && live.Pack.VisualUniverseSha == FamixaVisualUniverseAuthorityV1Rules.Sha(),
            "LG-14 generation does not promote VUA");
        Ok(!VisualCalibrationPackV1Rules.UsesCharacterName()
            && !VisualCalibrationPackV1Rules.CreatesCharacterMaster()
            && live.Pack.Subjects.All(s => s.CalibrationSubjectId.StartsWith("CAL-", StringComparison.Ordinal)),
            "LG-15 generation does not mutate characters");
        Ok(CharacterStudioV1Rules.ProtectedMinhUnchanged(
                CharacterAuthorityInitializationV1Rules.ProtectedMasterSha,
                CharacterAuthorityInitializationV1Rules.ProtectedDnaSha,
                CharacterAuthorityInitializationV1Rules.ProtectedPrpSha,
                CharacterAuthorityInitializationV1Rules.ProtectedCrpSha)
            && CharacterAuthorityInitializationV1Rules.ProtectedMasterSha
                == "be439c39e067aa6c7727255e9643ac78cb7c6285917af60dda38bf14a32518f1",
            "LG-16 Minh SHA unchanged");

        var dupMock = new MockVisualCalibrationGenerationProvider();
        var again = VisualCalibrationGenerationServiceV1.ExecuteAsync(
            live.Pack, dupMock, true, true).GetAwaiter().GetResult();
        Ok(again.GateCode == VisualCalibrationPackV1Rules.GateDuplicate
            && dupMock.CallCount == 0
            && !again.ProviderCalled,
            "LG-17 duplicate completed slot is blocked");

        var partialMock = new MockVisualCalibrationGenerationProvider { SucceedUpTo = 23 };
        var partial = VisualCalibrationGenerationServiceV1.ExecuteAsync(
            pack, partialMock, true, true).GetAwaiter().GetResult();
        var retryMock = new MockVisualCalibrationGenerationProvider();
        var retried = VisualCalibrationGenerationServiceV1.ExecuteAsync(
            partial.Pack, retryMock, true, true).GetAwaiter().GetResult();
        Ok(VisualCalibrationPackV1Rules.GetCoverage(partial.Pack).Valid == 23
            && VisualCalibrationPackV1Rules.MayGenerate(partial.Pack.Status)
            && retryMock.CallCount == 1
            && VisualCalibrationPackV1Rules.GetCoverage(retried.Pack).Valid == 24,
            "LG-18 missing slot can retry");

        var failMock = new MockVisualCalibrationGenerationProvider { SucceedUpTo = 0 };
        var failed = VisualCalibrationGenerationServiceV1.ExecuteAsync(
            pack, failMock, true, true).GetAwaiter().GetResult();
        Ok(failMock.CallCount == 6
            && VisualCalibrationPackV1Rules.GetCoverage(failed.Pack).Valid == 0
            && failed.Pack.Subjects.SelectMany(s => s.Artifacts).All(a =>
                !VisualCalibrationPackV1Rules.IsValidPixel(failed.Pack, a))
            && failed.Pack.Subjects.SelectMany(s => s.Artifacts)
                .Where(a => a.ViewType != "FRONT")
                .All(a => a.GenerationStatus == IdentityConditionedCalibrationV1Rules.GateAnchor),
            "LG-19 failed provider call does not create fake PIXEL");

        Ok(VisualCalibrationPackV1Rules.GetCoverage(partial.Pack) is { Valid: 23, Generated: 23, Missing: 1 }
            && partial.GateCode == VisualCalibrationPackV1Rules.GateIncomplete
            && partial.Pack.Status != VisualCalibrationPackV1Rules.StatusPendingReview,
            "LG-20 partial generation reports exact coverage");

        var wrongVua = VisualCalibrationPackV1Rules.WithPixelCoverage(
            pack with { Status = VisualCalibrationPackV1Rules.StatusPendingReview }, 24);
        wrongVua = wrongVua with
        {
            Subjects = wrongVua.Subjects.Select((s, i) => i == 0
                ? s with
                {
                    Artifacts = s.Artifacts.Select((a, n) => n == 0
                        ? a with { VisualUniverseSha = new string('0', 64) }
                        : a).ToList(),
                }
                : s).ToList(),
        };
        Ok(VisualCalibrationPackV1Rules.GetCoverage(wrongVua).Valid == 23, "LG-21 wrong VUA SHA invalid");

        var wrongPvs = VisualCalibrationPackV1Rules.WithPixelCoverage(
            pack with { Status = VisualCalibrationPackV1Rules.StatusPendingReview }, 24);
        wrongPvs = wrongPvs with
        {
            Subjects = wrongPvs.Subjects.Select((s, i) => i == 0
                ? s with
                {
                    Artifacts = s.Artifacts.Select((a, n) => n == 0
                        ? a with { ProjectVisualStyleSha = new string('1', 64) }
                        : a).ToList(),
                }
                : s).ToList(),
        };
        Ok(VisualCalibrationPackV1Rules.GetCoverage(wrongPvs).Valid == 23, "LG-22 wrong PVS SHA invalid");

        var wrongCdl = VisualCalibrationPackV1Rules.WithPixelCoverage(
            pack with { Status = VisualCalibrationPackV1Rules.StatusPendingReview }, 24);
        wrongCdl = wrongCdl with
        {
            Subjects = wrongCdl.Subjects.Select((s, i) => i == 0
                ? s with
                {
                    Artifacts = s.Artifacts.Select((a, n) => n == 0
                        ? a with { CharacterDesignLanguageSha = new string('2', 64) }
                        : a).ToList(),
                }
                : s).ToList(),
        };
        Ok(VisualCalibrationPackV1Rules.GetCoverage(wrongCdl).Valid == 23, "LG-23 wrong CDL SHA invalid");

        var wrongBind = VisualCalibrationPackV1Rules.WithPixelCoverage(
            pack with { Status = VisualCalibrationPackV1Rules.StatusPendingReview }, 24);
        wrongBind = wrongBind with
        {
            Subjects = wrongBind.Subjects.Select((s, i) => i == 0
                ? s with
                {
                    Artifacts = s.Artifacts.Select((a, n) => n == 0
                        ? a with { PackId = "OTHER-PACK", SlotId = "CAL-001/SIDE" }
                        : a).ToList(),
                }
                : s).ToList(),
        };
        Ok(VisualCalibrationPackV1Rules.GetCoverage(wrongBind).Valid == 23, "LG-24 wrong pack/slot binding invalid");

        Ok(live.Pack.DirectorPass != true
            && VisualCalibrationPackV1Rules.EvaluateApprove(
                live.Pack, false, VisualCalibrationPackV1Rules.PhotorealismLow)
                == VisualCalibrationPackV1Rules.GateDirector
            && !VisualCalibrationPhotorealismGateV1.InventsScore()
            && plan.All(r => r.CompiledPrompt.Contains(
                VisualCalibrationPackV1Rules.SharedVisualLanguage, StringComparison.Ordinal))
            && !VisualCalibrationPackV1Rules.CallsGemini()
            && !VisualCalibrationGenerationServiceV1.CallsGemini(),
            "LG-25 no automatic visual PASS");

        return fail;
    }
}
