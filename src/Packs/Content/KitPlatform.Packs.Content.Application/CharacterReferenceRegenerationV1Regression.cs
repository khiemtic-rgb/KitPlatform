namespace KitPlatform.Packs.Content;

public static class CharacterReferenceRegenerationV1Regression
{
    public const string SuiteId = CharacterReferenceRegenerationV1Rules.SuiteId;

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

        CharacterReferenceRegenerationV1Rules.GateInput Ready(
            string status = CharacterReferenceRegenerationV1Rules.PendingReview,
            string action = "REJECT",
            string? provider = null,
            bool confirm = false,
            bool reason = true,
            bool master = true,
            bool dna = true,
            bool prp = true,
            bool crp = true,
            bool dup = false) =>
            new(true, master, dna, prp, crp, status, provider, confirm, dup, reason, action);

        var rejectOk = CharacterReferenceRegenerationV1Application.Reject(
            Ready(), "V1", sha, sha, sha);
        Ok(rejectOk.Status == CharacterReferenceRegenerationV1Rules.Rejected && rejectOk.CanUse == false
            && rejectOk.Version == "V1"
            && !CharacterReferenceRegenerationV1Rules.RejectIncrementsVersion()
            && !CharacterReferenceRegenerationV1Rules.RejectCreatesHistoryVersion()
            && rejectOk.HistoryPreserved,
            "01 Reject pending CRP → success");

        var rejectLocked = CharacterReferenceRegenerationV1Rules.Evaluate(
            Ready(CharacterReferenceRegenerationV1Rules.Locked));
        Ok(rejectLocked.Code == "CRP_LOCKED" && !rejectLocked.MayReject,
            "02 Reject locked CRP → BLOCK");

        var regenPending = CharacterReferenceRegenerationV1Rules.Evaluate(
            Ready(action: "REGENERATE", provider: "GEMINI", confirm: true));
        Ok(regenPending.Code == "CRP_NOT_REJECTED",
            "03 Regenerate chưa reject → BLOCK");

        Ok(CharacterReferenceRegenerationV1Rules.Evaluate(
            Ready(CharacterReferenceRegenerationV1Rules.Rejected, "REGENERATE", "GEMINI", true, true, false)).Code
            == "MASTER_NOT_READY", "04 Regenerate khi Master missing → BLOCK");
        Ok(CharacterReferenceRegenerationV1Rules.Evaluate(
            Ready(CharacterReferenceRegenerationV1Rules.Rejected, "REGENERATE", "GEMINI", true, true, true, false)).Code
            == "DNA_NOT_READY", "05 Regenerate khi DNA missing → BLOCK");
        Ok(CharacterReferenceRegenerationV1Rules.Evaluate(
            Ready(CharacterReferenceRegenerationV1Rules.Rejected, "REGENERATE", "GEMINI", true, true, true, true, false)).Code
            == "PRP_NOT_READY", "06 Regenerate khi PRP missing → BLOCK");
        Ok(CharacterReferenceRegenerationV1Rules.Evaluate(
            Ready(CharacterReferenceRegenerationV1Rules.Rejected, "REGENERATE")).Code
            == "PROVIDER_REQUIRED", "07 Regenerate không provider → BLOCK");
        Ok(CharacterReferenceRegenerationV1Rules.Evaluate(
            Ready(CharacterReferenceRegenerationV1Rules.Rejected, "REGENERATE", "RUNWAY", true)).Code
            == "PROVIDER_CAPABILITY_UNSUPPORTED", "08 Gemini unsupported / Runway → BLOCK");
        Ok(CharacterReferenceRegenerationV1Rules.Evaluate(
            Ready(CharacterReferenceRegenerationV1Rules.Rejected, "EXECUTE", "GEMINI")).Code
            == "CONFIRMATION_REQUIRED", "09 confirm=false → BLOCK");

        var views = CharacterReferenceRegenerationV1Rules.RequiredTypes
            .Select(t => new CharacterReferenceViewRequest(t, t, "1:1", []))
            .ToList();
        var request = new CharacterReferenceSetRequest("CHAR-002", "ERA-01", sha, sha, sha, views);
        var mock = new MockCharacterReferenceSetProvider();
        var regen = CharacterReferenceRegenerationV1Application.RegenerateAsync(
            Ready(CharacterReferenceRegenerationV1Rules.Rejected, "EXECUTE", "GEMINI", true),
            "V1", sha, sha, sha, mock, request).GetAwaiter().GetResult();
        Ok(regen.ProviderCalled && mock.CallCount == 1 && regen.GeminiCalled && !regen.RunwayCalled && !regen.VeoCalled,
            "10 confirm=true + Gemini + valid authority → exactly 1 provider call");
        Ok(regen.Generation && regen.Coverage == 4, "11 Successful generation → exactly 1 reference set");
        Ok(regen.Types.Count == 4, "12 Reference set contains exactly 4 slots");
        Ok(regen.Types.Contains("FRONT", StringComparer.OrdinalIgnoreCase), "13 FRONT present");
        Ok(regen.Types.Contains("THREE_QUARTER", StringComparer.OrdinalIgnoreCase), "14 THREE_QUARTER present");
        Ok(regen.Types.Contains("SIDE", StringComparer.OrdinalIgnoreCase), "15 SIDE present");
        Ok(regen.Types.Contains("FULL_BODY", StringComparer.OrdinalIgnoreCase), "16 FULL_BODY present");
        Ok(regen.Status == CharacterReferenceRegenerationV1Rules.PendingReview, "17 New set status = PENDING_REVIEW");
        Ok(!regen.CanUse, "18 New set canUse=false");
        Ok(!regen.Approved && !CharacterReferenceRegenerationV1Rules.AutoApprove(), "19 New set not auto-approved");
        Ok(!regen.Locked && !CharacterReferenceRegenerationV1Rules.AutoLock(), "20 New set not auto-locked");
        Ok(regen.HistoryPreserved, "21 Old rejected set remains in history");
        Ok(!regen.OverwroteOld && !CharacterReferenceRegenerationV1Rules.OverwritesRejectedSet(),
            "22 Old rejected set is not overwritten");
        Ok(regen.MasterSha == sha && !CharacterReferenceRegenerationV1Rules.MutatesMaster(), "23 Master SHA unchanged");
        Ok(regen.DnaSha == sha && !CharacterReferenceRegenerationV1Rules.MutatesDna(), "24 DNA SHA unchanged");
        Ok(regen.PrpSha == sha && !CharacterReferenceRegenerationV1Rules.MutatesPrp(), "25 PRP SHA unchanged");
        Ok(CharacterAuthorityInitializationV1Rules.MinhShaUnchanged(minhMaster, minhDna, minhPrp, minhCrp),
            "26 Minh CRP SHA unchanged");
        Ok(!CharacterReferenceRegenerationV1Rules.GeneratesShot(), "27 SHOT-001 unchanged");
        Ok(!CharacterReferenceRegenerationV1Rules.OpensFirstRealProduction(),
            "28 Production generation not called");
        Ok(!CharacterReferenceRegenerationV1Rules.GeneratesVideo() && !regen.VeoCalled, "29 Video generation not called");
        Ok(regen.GeminiCalled && !regen.RunwayCalled, "30 Runway not called when Gemini selected");
        Ok(!regen.VeoCalled, "31 Veo not called when Gemini selected");

        var failMock = new MockCharacterReferenceSetProvider { Succeed = false };
        var failed = CharacterReferenceRegenerationV1Application.RegenerateAsync(
            Ready(CharacterReferenceRegenerationV1Rules.Rejected, "EXECUTE", "GEMINI", true),
            "V1", sha, sha, sha, failMock, request).GetAwaiter().GetResult();
        Ok(failMock.CallCount == 1 && !failed.Generation && failed.Status == CharacterReferenceRegenerationV1Rules.Rejected
            && !CharacterReferenceRegenerationV1Rules.AutoRetry(),
            "32 Failed Gemini does not retry automatically");

        var fp1 = CharacterReferenceRegenerationV1Rules.ExecutionFingerprint("CHAR-002", "ERA-01", sha, sha, sha, "V2", "GEMINI");
        Ok(CharacterReferenceRegenerationV1Rules.DuplicatePolicy(fp1, fp1) == "BLOCK_DUPLICATE",
            "33 Same successful fingerprint → BLOCK_DUPLICATE");
        var fp2 = CharacterReferenceRegenerationV1Rules.ExecutionFingerprint("CHAR-002", "ERA-01", sha, sha, sha, "V3", "GEMINI");
        Ok(fp1 != fp2 && CharacterReferenceRegenerationV1Rules.DuplicatePolicy(fp2, fp1) == "NEW",
            "34 Different ReferenceSetVersion → valid new execution");
        Ok(CharacterReferenceRegenerationV1Rules.Evaluate(
            Ready(CharacterReferenceRegenerationV1Rules.Locked, "REGENERATE", "GEMINI", true)).Code == "CRP_LOCKED",
            "35 LOCKED CRP cannot regenerate");
        Ok(!CharacterReferenceRegenerationV1Rules.CanUseProduction(CharacterReferenceRegenerationV1Rules.PendingReview, false, 4)
            && !CharacterReferenceRegenerationV1Rules.CanUseWhilePending(),
            "36 Pending review cannot be used for production");
        Ok(!CharacterReferenceRegenerationV1Rules.CanUseProduction(CharacterReferenceRegenerationV1Rules.Rejected, false, 4),
            "37 Rejected CRP cannot be used for production");
        Ok(CharacterReferenceRegenerationV1Rules.CanUseProduction("LOCKED", true, 4)
            && !CharacterReferenceRegenerationV1Rules.CanUseProduction("LOCKED", true, 3)
            && !CharacterReferenceRegenerationV1Rules.CanUseProduction("LOCKED", false, 4),
            "38 Only LOCKED + canUse=true + 4/4 can pass production gate");

        Ok(!CharacterReferenceRegenerationV1Rules.AllowsPerSlotGenerate()
            && !CharacterReferenceRegenerationV1Rules.AutoFallbackProvider()
            && CharacterReferenceRegenerationV1Rules.RejectReasonValid("Toàn thân không đồng nhất")
            && !CharacterReferenceRegenerationV1Rules.RejectReasonValid("no"),
            "reject reason + no per-slot generate");

        var app = typeof(CharacterReferenceRegenerationV1Rules).Assembly;
        Ok(!app.GetTypes().Any(t =>
                t.Name.Contains("ContentGemini", StringComparison.Ordinal)
                || t.FullName?.Contains("Google.GenAI", StringComparison.Ordinal) == true),
            "Application does not reference Gemini SDK");

        return fail;
    }
}
