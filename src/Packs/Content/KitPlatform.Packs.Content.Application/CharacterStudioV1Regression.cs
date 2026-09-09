using System.Linq;
using System.Text.Json;

namespace KitPlatform.Packs.Content;

public static class CharacterStudioV1Regression
{
    public const string SuiteId = CharacterStudioV1Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var sha = new string('a', 64);
        var minhMaster = CharacterAuthorityInitializationV1Rules.ProtectedMasterSha;
        var minhDna = CharacterAuthorityInitializationV1Rules.ProtectedDnaSha;
        var minhPrp = CharacterAuthorityInitializationV1Rules.ProtectedPrpSha;
        var minhCrp = CharacterAuthorityInitializationV1Rules.ProtectedCrpSha;

        Ok(CharacterStudioV1Rules.ProfileValid("Lan", 10, "female", "STYLE_3D_STYLIZED_REALISM", "curious girl"),
            "01 Create / profile validation PASS");
        Ok(!CharacterStudioV1Rules.ProfileValid("", 10, "female", "STYLE_3D_STYLIZED_REALISM", "x"),
            "02 Profile missing name FAIL");
        Ok(CharacterStudioV1Rules.StyleValid("STYLE_ANIME") && !CharacterStudioV1Rules.StyleValid("PIXAR")
            && !ProjectVisualStyleV1Rules.CharacterMaySelectIndependentStyle(),
            "03 Style validation — presets exist, character cannot choose independently");

        var mock = new MockCharacterGenerationProvider();
        var master = mock.GenerateMasterAsync(
            new CharacterAuthorityGenerationRequest("CHAR-003", "ERA-01", "MASTER", "brief", "3:4", "V1"),
            CancellationToken.None).GetAwaiter().GetResult();
        Ok(master.Succeeded && mock.MasterCalls == 1 && master.Bytes is { Length: > 8 },
            "04 Master generation via provider");

        var dna = CharacterAuthorityInitializationV1Rules.CompileDnaSpec(
            "CHAR-003", "Lan", "friend", "ERA-01", sha, "V1", "female", "10");
        var dnaSha = CharacterAuthorityInitializationV1Rules.SpecSha(dna);
        Ok(CharacterReferencePackRules.ShaExists(dnaSha), "05 DNA generation deterministic");
        var prp = CharacterAuthorityInitializationV1Rules.CompilePrpSpec("CHAR-003", "Lan", "ERA-01", sha, dnaSha);
        Ok(CharacterReferencePackRules.ShaExists(CharacterAuthorityInitializationV1Rules.SpecSha(prp)),
            "06 PRP generation deterministic");

        Ok(CharacterStudioV1Rules.ChainOrder.SequenceEqual(["FRONT", "FULL_BODY", "THREE_QUARTER", "SIDE"])
            && CharacterStudioV1Rules.ChainDependencies("FULL_BODY").Contains("MASTER")
            && CharacterStudioV1Rules.ChainDependencies("THREE_QUARTER").Contains("FRONT")
            && CharacterStudioV1Rules.ChainDependencies("SIDE").Contains("FRONT"),
            "07 Reference dependency chain");

        var views = CharacterStudioV1Rules.RequiredViews
            .Select(t => new CharacterReferenceViewRequest(t, t, "1:1", []))
            .ToList();
        var set = mock.GenerateViewsAsync(
            new CharacterReferenceSetRequest("CHAR-004", "ERA-01", sha, sha, sha, views),
            CancellationToken.None).GetAwaiter().GetResult();
        Ok(set.Succeeded && set.Views.Count == 4 && mock.ViewCalls == 1, "08 4-reference generation");

        var independent = CharacterStudioV1Rules.ViewsAreIndependent(
            [["MASTER"], ["MASTER"], ["MASTER"], ["MASTER"]]);
        var chained = CharacterStudioV1Rules.ViewsAreIndependent(
        [
            ["MASTER"],
            ["MASTER", "FRONT"],
            ["MASTER", "FULL_BODY"],
            ["MASTER", "FULL_BODY"],
        ]);
        Ok(independent && !chained, "08b independent same-ref set detected");

        var passSlots = CharacterStudioV1Rules.RequiredViews
            .Select(t => CharacterStudioV1Rules.ScoreSlot(
                t, true, false, false, CharacterStudioIdentityLockV1Rules.DeclaredPassScores(t)))
            .ToList();
        Ok(CharacterStudioV1Rules.ConsistencyPass(passSlots), "09 Consistency PASS");

        var failSlots = passSlots.Select(s =>
            s.Type == "SIDE"
                ? CharacterStudioV1Rules.ScoreSlot("SIDE", true, false, false, new Dictionary<string, int> { ["face"] = 40, ["crossView"] = 40 })
                : s).ToList();
        Ok(!CharacterStudioV1Rules.ConsistencyPass(failSlots)
            && CharacterStudioV1Rules.FailedSlots(failSlots).SequenceEqual(["SIDE"]),
            "10 Consistency FAIL one slot");

        Ok(CharacterStudioV1Rules.MayRepairSlot("CONTENT_INCONSISTENCY", 0)
            && !CharacterStudioV1Rules.MayRepairSlot("PROVIDER_EXECUTION_FAILURE", 0)
            && CharacterStudioV1Rules.RegenerationSlots("FULL_BODY_INVALID", false).SequenceEqual(["FULL_BODY"]),
            "11 Single-slot regeneration");
        Ok(CharacterStudioV1Rules.RegenerationSlots("FACE_MISMATCH", false).Count == 4
            && CharacterStudioV1Rules.RegenerationSlots("OTHER", true).Count == 4,
            "12 Full-set regeneration");

        var fp1 = CharacterStudioV1Rules.ExecutionFingerprint(
            "FAMIXA", "CHAR-003", "p", sha, sha, sha, "STYLE_ANIME", "V1");
        var fp2 = CharacterStudioV1Rules.ExecutionFingerprint(
            "FAMIXA", "CHAR-003", "p", sha, sha, sha, "STYLE_ANIME", "V1");
        var fp3 = CharacterStudioV1Rules.ExecutionFingerprint(
            "FAMIXA", "CHAR-003", "p", sha, sha, sha, "STYLE_CARTOON", "V1");
        Ok(fp1 == fp2 && fp1 != fp3 && !fp1.Contains("T", StringComparison.Ordinal),
            "13 Duplicate protection / no timestamp");

        var failProv = new MockCharacterGenerationProvider { FailMaster = true };
        var bad = failProv.GenerateMasterAsync(
            new CharacterAuthorityGenerationRequest("CHAR-004", "ERA-01", "MASTER", "x", "3:4", "V1"),
            CancellationToken.None).GetAwaiter().GetResult();
        Ok(!bad.Succeeded && bad.ErrorCode == "MASTER_GENERATION_FAILED"
            && CharacterStudioV1Rules.FailureClass(true, false) == "PROVIDER_EXECUTION_FAILURE",
            "14 Provider failure");

        var cap = CharacterStudioV1Rules.Evaluate(Ready("GENERATE", provider: "RUNWAY", confirm: true));
        Ok(cap.Code == "PROVIDER_UNAVAILABLE" && !cap.MayCallProvider, "15 Provider capability failure");

        var reject = CharacterStudioV1Rules.Evaluate(Ready("REJECT", state: CharacterStudioV1Rules.CrpPendingReview));
        Ok(reject.MayReject && CharacterStudioV1Rules.RejectReasonValid("FACE_MISMATCH", "sai mặt"),
            "16 Reject");
        var approve = CharacterStudioV1Rules.Evaluate(Ready("APPROVE", state: CharacterStudioV1Rules.CrpPendingReview));
        Ok(approve.MayApprove && !CharacterStudioV1Rules.AutoApprove(), "17 Approve");
        var lockGate = CharacterStudioV1Rules.Evaluate(Ready("LOCK", state: CharacterStudioV1Rules.CrpApproved));
        Ok(lockGate.MayLock && !CharacterStudioV1Rules.AutoLock(), "18 Lock");

        var lockedGen = CharacterStudioV1Rules.Evaluate(Ready("GENERATE", official: true, confirm: true, provider: "GEMINI"));
        Ok(lockedGen.Code == "MASTER_ALREADY_LOCKED" && !lockedGen.MayCallProvider, "19 Locked character protection");
        Ok(CharacterStudioV1Rules.NextVersion("V1") == "V2" && CharacterStudioV1Rules.NextVersion("V3") == "V4",
            "20 Versioning");

        Ok(CharacterStudioV1Rules.ProtectedMinhUnchanged(minhMaster, minhDna, minhPrp, minhCrp)
            && !CharacterStudioV1Rules.ProtectedMinhUnchanged(sha, minhDna, minhPrp, minhCrp),
            "21 Authority SHA");

        Ok(CharacterStudioV1Rules.RejectHistoricalStill(CharacterStudioV1Rules.HistoricalStillId)
            && CharacterStudioV1Rules.ScoreSlot("FRONT", true, true, false).Verdict == "FAIL"
            && !CharacterStudioV1Rules.MasterCropIsNotFullBody(sha, sha),
            "22 Historical still rejection");

        var snapA = new CharacterStudioV1Rules.StudioSnapshot(
            true, true, true, false, "LOCKED", "LOCKED", "LOCKED",
            CharacterStudioV1Rules.CrpPendingReview, 4, false, false, false, true);
        var snapB = snapA with { };
        Ok(CharacterStudioV1Rules.ResolveState(snapA) == CharacterStudioV1Rules.CrpPendingReview
            && CharacterStudioV1Rules.SameInputsSameState(snapA, snapB)
            && CharacterStudioV1Rules.ResolveState(snapA with { OfficialLocked = true, CrpLocked = true })
                == CharacterStudioV1Rules.CharacterReady,
            "23 CharacterId genericity — same inputs same state");

        var minhSnap = new CharacterStudioV1Rules.StudioSnapshot(
            true, true, true, true, "LOCKED", "LOCKED", "LOCKED", "LOCKED", 4, true, true, false, true);
        Ok(CharacterStudioV1Rules.ResolveState(minhSnap) == CharacterStudioV1Rules.CharacterReady
            && !CharacterStudioV1Rules.TransitionAllowed(CharacterStudioV1Rules.MasterReady, CharacterStudioV1Rules.CharacterReady)
            && CharacterStudioV1Rules.ProtectedMinhUnchanged(minhMaster, minhDna, minhPrp, minhCrp),
            "24 Minh backward compatibility");

        var namSnap = new CharacterStudioV1Rules.StudioSnapshot(
            true, true, true, false, "LOCKED", "LOCKED", "LOCKED",
            CharacterReferenceRegenerationV1Rules.PendingReview, 4, false, false, false, true);
        var namDup = CharacterStudioV1Rules.Evaluate(Ready(
            "GENERATE", state: CharacterStudioV1Rules.CrpPendingReview, confirm: true, provider: "GEMINI", crpComplete: true));
        Ok(CharacterStudioV1Rules.ResolveState(namSnap) == CharacterStudioV1Rules.CrpPendingReview
            && namDup.Code == "BLOCK_DUPLICATE",
            "25 Nam backward compatibility — pending, no silent generate");
        Ok(CharacterStudioV1Rules.StaffMessageFor(CharacterStudioV1Rules.Rejected)
            == CharacterStudioV1Rules.StaffReject
            && CharacterStudioV1Rules.StaffMessageFor(CharacterStudioV1Rules.Rejected) != CharacterStudioV1Rules.StaffRegenerate,
            "25b Rejected status label is Không đạt");

        var redoRejected = CharacterStudioV1Rules.Evaluate(Ready(
            "REGENERATE", state: CharacterStudioV1Rules.Rejected, confirm: true, provider: "GEMINI", rejected: true));
        var redoFailed = CharacterStudioV1Rules.Evaluate(Ready(
            "REGENERATE", state: CharacterStudioV1Rules.Failed, confirm: true, provider: "GEMINI"));
        var redoPending = CharacterStudioV1Rules.Evaluate(Ready(
            "REGENERATE", state: CharacterStudioV1Rules.CrpPendingReview, confirm: true, provider: "GEMINI"));
        var redoLocked = CharacterStudioV1Rules.Evaluate(Ready(
            "REGENERATE", state: CharacterStudioV1Rules.Rejected, confirm: true, provider: "GEMINI", official: true, rejected: true));
        Ok(CharacterStudioV1Rules.MayRegenerate(CharacterStudioV1Rules.Rejected)
            && CharacterStudioV1Rules.MayRegenerate(CharacterStudioV1Rules.Failed)
            && CharacterStudioV1Rules.MayRegenerate(CharacterStudioV1Rules.CrpPendingReview)
            && redoRejected.MayCallProvider
            && redoFailed.MayCallProvider
            && redoPending.MayCallProvider
            && redoLocked.Code == "AUTHORITY_LOCKED",
            "25c Rejected / Failed / Pending can regenerate; locked cannot");

        var staleSha = new string('c', 64);
        var staleReady = CharacterStudioV1Rules.Evaluate(new CharacterStudioV1Rules.GateInput(
            true, true, false, false, "REGENERATE", CharacterStudioV1Rules.MasterReady,
            "GEMINI", true, true, false, false, true, false, null, true, sha, false, true));
        var staleGenerate = CharacterStudioV1Rules.Evaluate(new CharacterStudioV1Rules.GateInput(
            true, true, false, false, "GENERATE", CharacterStudioV1Rules.MasterReady,
            "GEMINI", true, true, true, false, true, false, null, true, sha, false, true));
        var keepGenerate = CharacterStudioV1Rules.Evaluate(Ready(
            "GENERATE", state: CharacterStudioV1Rules.MasterReady, confirm: true, provider: "GEMINI",
            crpComplete: true));
        Ok(CharacterStudioV1Rules.MayRegenerate(CharacterStudioV1Rules.MasterReady, crpStale: true)
            && !CharacterStudioV1Rules.MayRegenerate(CharacterStudioV1Rules.MasterReady)
            && staleReady.MayCallProvider
            && staleGenerate.MayCallProvider
            && keepGenerate.Code == "BLOCK_DUPLICATE"
            && CharacterMasterRevisionV1Rules.CrpStaleForCurrentMaster(staleSha, sha)
            && !CharacterMasterRevisionV1Rules.CrpStaleForCurrentMaster(sha, sha),
            "25d Stale CRP after new Master may replace all views");

        var close = CharacterStudioV1Rules.SelectReferences(new("CLOSE_UP", "MCU", "talk", null, null));
        var full = CharacterStudioV1Rules.SelectReferences(new("FULL_BODY", "WIDE", "run", null, null));
        var side = CharacterStudioV1Rules.SelectReferences(new("SIDE", "PROFILE", "turn", null, null));
        Ok(close.Types.Contains("MASTER") && close.Types.Contains("FRONT")
            && full.Types.Contains("FULL_BODY")
            && side.Types.Contains("SIDE"),
            "26 Production reference selector");

        Ok(!CharacterStudioV1Rules.AutoApprove() && !CharacterStudioV1Rules.AutoLock()
            && !CharacterStudioV1Rules.GeneratesShot() && !CharacterStudioV1Rules.GeneratesVideo()
            && !CharacterStudioV1Rules.OpensFirstRealProduction()
            && typeof(ICharacterGenerationProvider).IsAssignableFrom(typeof(MockCharacterGenerationProvider)),
            "27 No auto / no production / provider abstraction");

        Ok(CharacterStudioV1Rules.FailureClass(false, true) == "CONTENT_INCONSISTENCY"
            && !CharacterStudioV1Rules.MayRepairSlot("PROVIDER_EXECUTION_FAILURE", 1),
            "28 Repair only content inconsistency");

        return fail;
    }

    private static CharacterStudioV1Rules.GateInput Ready(
        string action,
        string state = CharacterStudioV1Rules.ProfileReady,
        string? provider = null,
        bool confirm = false,
        bool official = false,
        bool crpComplete = false,
        bool rejected = false) =>
        new(true, true, official, official, action, state, provider,
            provider is null || CharacterStudioV1Rules.CapabilityAllows(provider),
            confirm, false, false, crpComplete, rejected, null, true, new string('a', 64));
}
