using System.Text.Json;

namespace KitPlatform.Packs.Content;

public static class FirstRealProductionV2Regression
{
    public const string SuiteId = FirstRealProductionV2Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        FirstRealProductionV2Rules.GateInput Ready(
            bool shotExists = true,
            bool shotReady = true,
            string shotCode = "CHAR-001-MINH-ERA01-SHOT-001",
            bool master = true,
            bool dna = true,
            bool prp = true,
            bool masterSha = true,
            bool dnaSha = true,
            bool prpSha = true,
            bool crp = true,
            string crpStatus = "LOCKED",
            int coverage = 4,
            bool shot = true,
            bool intent = true,
            string? provider = "GEMINI",
            bool capability = true,
            bool confirm = true,
            bool dup = false,
            string character = "CHAR-001",
            string requested = "CHAR-001",
            int count = 1) =>
            new(shotExists, shotReady, shotCode, master, dna, prp, masterSha, dnaSha, prpSha,
                crp, crpStatus, coverage, shot, intent, provider, capability,
                capability ? null : "PROVIDER_CAPABILITY_UNSUPPORTED",
                confirm, dup, character, requested, count);

        var request = new ImageGenerationExecutionRequest(
            "canonical production still",
            [
                new("FRONT", "image/png", [1], "aa", "FRONT"),
                new("THREE_QUARTER", "image/png", [1], "aa", "THREE_QUARTER"),
                new("SIDE", "image/png", [1], "aa", "SIDE"),
                new("FULL_BODY", "image/png", [1], "aa", "FULL_BODY"),
            ],
            "16:9",
            null);

        async Task<(FirstRealProductionV2Application.Outcome Outcome, MockCharacterReferenceProvider Mock)> Attempt(
            FirstRealProductionV2Rules.GateInput input, bool succeed = true)
        {
            var mock = new MockCharacterReferenceProvider { Succeed = succeed };
            var outcome = await FirstRealProductionV2Application.ExecuteOnceAsync(input, mock, request);
            return (outcome, mock);
        }

        var case1 = Attempt(Ready(crp: false)).GetAwaiter().GetResult();
        Ok(case1.Outcome.Gate.Code == "CRP_NOT_READY" && !case1.Outcome.GeminiCalled && case1.Mock.CallCount == 0,
            "Case 1 CRP canUse=false → BLOCK Gemini called=false");

        var case2 = Attempt(Ready(masterSha: false)).GetAwaiter().GetResult();
        Ok(case2.Outcome.Gate.Code == "MASTER_MISMATCH" && !case2.Outcome.GeminiCalled && case2.Mock.CallCount == 0,
            "Case 2 Master mismatch → BLOCK Gemini called=false");

        var case3 = Attempt(Ready(dnaSha: false)).GetAwaiter().GetResult();
        Ok(case3.Outcome.Gate.Code == "DNA_MISMATCH" && !case3.Outcome.GeminiCalled && case3.Mock.CallCount == 0,
            "Case 3 DNA mismatch → BLOCK Gemini called=false");

        var case4 = Attempt(Ready(prpSha: false)).GetAwaiter().GetResult();
        Ok(case4.Outcome.Gate.Code == "PRP_MISMATCH" && !case4.Outcome.GeminiCalled && case4.Mock.CallCount == 0,
            "Case 4 PRP mismatch → BLOCK Gemini called=false");

        var case5 = Attempt(Ready(shotExists: false)).GetAwaiter().GetResult();
        Ok(case5.Outcome.Gate.Code == "SHOT_NOT_FOUND" && !case5.Outcome.GeminiCalled && case5.Mock.CallCount == 0,
            "Case 5 Shot missing → BLOCK Gemini called=false");

        var case6 = Attempt(Ready(intent: false)).GetAwaiter().GetResult();
        Ok(case6.Outcome.Gate.Code == "INTENT_INVALID" && !case6.Outcome.GeminiCalled && case6.Mock.CallCount == 0,
            "Case 6 Intent invalid → BLOCK Gemini called=false");

        var case7 = Attempt(Ready(provider: null)).GetAwaiter().GetResult();
        Ok(case7.Outcome.Gate.Status == "NEEDS_PROVIDER_SELECTION" && !case7.Outcome.GeminiCalled && case7.Mock.CallCount == 0,
            "Case 7 Provider not selected → NEEDS_PROVIDER_SELECTION Gemini called=false");

        var case8 = Attempt(Ready(provider: "RUNWAY", capability: false)).GetAwaiter().GetResult();
        Ok(case8.Outcome.Gate.Status == "BLOCKED" && !case8.Outcome.GeminiCalled && case8.Mock.CallCount == 0
            && !FirstRealProductionV2Rules.AllowsRunway() && !FirstRealProductionV2Rules.AllowsVeo(),
            "Case 8 Runway IMAGE_GENERATION unsupported → BLOCK Gemini/Runway called=false");

        var case9 = Attempt(Ready(confirm: false)).GetAwaiter().GetResult();
        Ok(case9.Outcome.Gate.Code == "CONFIRM_REQUIRED" && !case9.Outcome.GeminiCalled && case9.Mock.CallCount == 0,
            "Case 9 Confirmation false → BLOCK Gemini called=false");

        var case10 = Attempt(Ready()).GetAwaiter().GetResult();
        Ok(case10.Outcome.Gate.Status == "READY" && case10.Outcome.GeminiCalled && case10.Outcome.Generation
            && case10.Outcome.ArtifactCreated && !case10.Outcome.ArtifactApproved && case10.Mock.CallCount == 1,
            "Case 10 All valid → READY Gemini called=true artifact created approved=false");

        var case11 = Attempt(Ready(dup: true)).GetAwaiter().GetResult();
        Ok(case11.Outcome.Gate.Code == "BLOCK_DUPLICATE" && !case11.Outcome.GeminiCalled && case11.Mock.CallCount == 0,
            "Case 11 Duplicate fingerprint → BLOCK_DUPLICATE Gemini called=false");

        var case12 = Attempt(Ready(), succeed: false).GetAwaiter().GetResult();
        Ok(case12.Outcome.Gate.Status == "FAILED" && case12.Outcome.Generation && !case12.Outcome.ArtifactCreated
            && case12.Mock.CallCount == 1 && !FirstRealProductionV2Rules.AutoRetry(),
            "Case 12 Gemini failure → generation=FAILED no automatic retry");

        Ok(typeof(IImageGenerationProvider).IsAssignableFrom(typeof(MockCharacterReferenceProvider)),
            "Application → IImageGenerationProvider → Mock Gemini (no HTTP)");
        Ok(!FirstRealProductionV2Rules.CrpReadyForProduction("VALIDATED", true, 4)
            && FirstRealProductionV2Rules.CrpReadyForProduction("LOCKED", true, 4)
            && !FirstRealProductionV2Rules.CrpReadyForProduction("LOCKED", true, 3),
            "CRP production requires LOCKED + canUse + 4/4");
        Ok(FirstRealProductionV2Rules.IsShot001("CHAR-001-MINH-ERA01-SHOT-001")
            && !FirstRealProductionV2Rules.IsShot001("SHOT-002")
            && !FirstRealProductionV2Rules.IsShot001("SHOT-011"),
            "SHOT-001 only");
        Ok(FirstRealProductionV2Rules.HasRequiredCrpRefs(request.References)
            && !FirstRealProductionV2Rules.HasRequiredCrpRefs([request.References[0]]),
            "CRP refs FRONT/THREE_QUARTER/SIDE/FULL_BODY");
        Ok(!FirstRealProductionV2Rules.AllowedCrpPath(
                "CHAR-001", "still.jpg", CharacterReferenceCompletionRules.HistoricalStillId, "CHAR-001", "FULL_BODY")
            && !FirstRealProductionV2Rules.AllowedCrpPath(
                "CHAR-001", "CHAR-003-FRONT.jpg", null, "CHAR-003", "FRONT")
            && !FirstRealProductionV2Rules.AllowedCrpPath(
                "CHAR-001", "CHAR-001-MASTER.jpg", null, "CHAR-001", "FULL_BODY"),
            "no historical still / other character / Master-as-FULL_BODY");
        Ok(!FirstRealProductionV2Rules.AutoApprove() && !FirstRealProductionV2Rules.AutoLock()
            && !FirstRealProductionV2Rules.AutoSelectProvider() && !FirstRealProductionV2Rules.AllowsVideo()
            && !FirstRealProductionV2Rules.AllowsBatch() && !FirstRealProductionV2Rules.OpensShot002(),
            "no auto approve/lock/select/video/batch/shot-002");
        Ok(FirstRealProductionV2Rules.PendingReview == "READY_FOR_DIRECTOR",
            "artifact pending director review");
        Ok(FirstRealProductionV2Rules.IsHistoricalStill(CharacterReferenceCompletionRules.HistoricalStillId)
            && !FirstRealProductionV2Rules.IsHistoricalStill("artifact-1"),
            "historical still 7ed003d7 is not First Real V2");

        var sha = new string('a', 64);
        var payload = JsonSerializer.SerializeToElement(new
        {
            story = new { action = "Minh đang đọc tờ giấy" },
            scene = new { location = "phòng khách", summary = "Phòng khách buổi tối" },
            character = new { expression = "concerned" },
            composition = new { framing = "medium", angle = "eye_level", movement = "hold" },
            lighting = new { style = "warm_window" },
            motion = new { action = "hold" },
            timing = new { durationSeconds = 5 },
        });
        var intent = FirstRealProductionRules.TryResolveIntent("CHAR-001", payload, sha, sha, sha, sha);
        Ok(intent is not null, "intent resolves from existing contract");
        var sha1 = ProductionOsRules.IntentSha(intent!);
        Ok(sha1 == ProductionOsRules.IntentSha(intent!) && sha1.Length == 64, "IntentSha unchanged when Intent unchanged");
        var canonical = ProductionOsRules.Canonical(intent!);
        Ok(canonical.IntentSha256 == sha1 && !ProductionOsRules.ContainsProvider(canonical.Text),
            "canonical provider-neutral");

        return fail;
    }
}
