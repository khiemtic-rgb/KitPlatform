namespace KitPlatform.Packs.Content;

public static class CharacterAuthorityInitializationV1Regression
{
    public const string SuiteId = CharacterAuthorityInitializationV1Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        CharacterAuthorityInitializationV1Rules.GateInput Ready(
            bool character = true,
            bool identity = true,
            bool locked = false,
            string stage = CharacterAuthorityInitializationV1Rules.StageMaster,
            string action = CharacterAuthorityInitializationV1Rules.ActionExecute,
            string master = CharacterAuthorityInitializationV1Rules.Missing,
            string dna = CharacterAuthorityInitializationV1Rules.Missing,
            string prp = CharacterAuthorityInitializationV1Rules.Missing,
            string? provider = "GEMINI",
            bool capability = true,
            bool confirm = true,
            bool dup = false,
            bool historical = false) =>
            new(character, identity, locked, stage, action, master, dna, prp,
                provider, capability, capability ? null : "PROVIDER_CAPABILITY_UNSUPPORTED",
                confirm, dup, historical);

        var request = new CharacterAuthorityGenerationRequest(
            "CHAR-002", "ERA-01", "MASTER",
            CharacterAuthorityInitializationV1Rules.CanonicalMasterBrief(
                "CHAR-002", "Nam", "Bố", "ERA-01", "male", "38", null),
            "3:4", "V1");

        async Task<(CharacterAuthorityInitializationV1Application.Outcome Outcome, MockCharacterAuthorityGenerationProvider Mock)> Master(
            CharacterAuthorityInitializationV1Rules.GateInput input,
            bool succeed = true)
        {
            var mock = new MockCharacterAuthorityGenerationProvider { Succeed = succeed };
            var outcome = await CharacterAuthorityInitializationV1Application.ExecuteMasterOnceAsync(input, mock, request);
            return (outcome, mock);
        }

        var case1 = CharacterAuthorityInitializationV1Rules.Evaluate(Ready(character: false));
        Ok(case1.Code == "CHARACTER_NOT_FOUND" && !case1.MayCallProvider, "Case 1 Character missing → CHARACTER_NOT_FOUND");

        var case2 = CharacterAuthorityInitializationV1Rules.Evaluate(Ready(identity: false));
        Ok(case2.Code == "CHARACTER_IDENTITY_NOT_READY" && !case2.MayCallProvider,
            "Case 2 Character Identity missing → CHARACTER_IDENTITY_NOT_READY");

        var case3 = Master(Ready(confirm: false)).GetAwaiter().GetResult();
        Ok(case3.Outcome.Gate.Code == "CONFIRMATION_REQUIRED" && !case3.Outcome.ProviderCalled && case3.Mock.CallCount == 0,
            "Case 3 Master generation chưa confirm → blocked");

        var case4 = Master(Ready()).GetAwaiter().GetResult();
        Ok(case4.Outcome.ProviderCalled && case4.Mock.CallCount == 1
            && case4.Outcome.MasterStatus == CharacterAuthorityInitializationV1Rules.ReadyForDirector,
            "Case 4 confirm + mock provider → called exactly once");

        var case5 = Master(Ready(), succeed: false).GetAwaiter().GetResult();
        Ok(case5.Outcome.Gate.Code == "GENERATION_FAILED" && case5.Mock.CallCount == 1
            && !CharacterAuthorityInitializationV1Rules.AutoRetry()
            && case5.Outcome.MasterStatus == CharacterAuthorityInitializationV1Rules.Failed,
            "Case 5 Provider failure → failed, no retry");

        Ok(case4.Outcome.MasterStatus == CharacterAuthorityInitializationV1Rules.ReadyForDirector
            && !case4.Outcome.Approved && !case4.Outcome.Locked,
            "Case 6 Master generated → READY_FOR_DIRECTOR");

        var case7 = CharacterAuthorityInitializationV1Application.CompileDna(
            Ready(stage: CharacterAuthorityInitializationV1Rules.StageDna,
                master: CharacterAuthorityInitializationV1Rules.ReadyForDirector));
        Ok(case7.Gate.Code == "MASTER_NOT_LOCKED" && !case7.ProviderCalled,
            "Case 7 Master chưa approve → DNA blocked");

        var case8 = CharacterAuthorityInitializationV1Application.CompileDna(
            Ready(stage: CharacterAuthorityInitializationV1Rules.StageDna,
                master: CharacterAuthorityInitializationV1Rules.Approved));
        Ok(case8.Gate.Code == "MASTER_NOT_LOCKED" && !case8.ProviderCalled,
            "Case 8 Master approved nhưng chưa lock → DNA blocked");

        var case9 = CharacterAuthorityInitializationV1Application.CompileDna(
            Ready(stage: CharacterAuthorityInitializationV1Rules.StageDna,
                master: CharacterAuthorityInitializationV1Rules.Locked, confirm: true));
        Ok(case9.Gate.Status == "READY" && case9.DnaStatus == CharacterAuthorityInitializationV1Rules.ReadyForDirector
            && !case9.ProviderCalled,
            "Case 9 Master locked → DNA generation allowed");

        var case10 = CharacterAuthorityInitializationV1Application.CompilePrp(
            Ready(stage: CharacterAuthorityInitializationV1Rules.StagePrp,
                master: CharacterAuthorityInitializationV1Rules.Locked,
                dna: CharacterAuthorityInitializationV1Rules.ReadyForDirector));
        Ok(case10.Gate.Code == "DNA_NOT_LOCKED" && !case10.ProviderCalled,
            "Case 10 DNA chưa locked → PRP blocked");

        var case11 = CharacterAuthorityInitializationV1Application.CompilePrp(
            Ready(stage: CharacterAuthorityInitializationV1Rules.StagePrp,
                master: CharacterAuthorityInitializationV1Rules.Locked,
                dna: CharacterAuthorityInitializationV1Rules.Locked));
        Ok(case11.Gate.Status == "READY" && case11.PrpStatus == CharacterAuthorityInitializationV1Rules.ReadyForDirector
            && !case11.ProviderCalled,
            "Case 11 DNA locked → PRP generation allowed");

        Ok(!CharacterAuthorityInitializationV1Rules.AuthorityReady(
                CharacterAuthorityInitializationV1Rules.Locked,
                CharacterAuthorityInitializationV1Rules.Locked,
                CharacterAuthorityInitializationV1Rules.Approved),
            "Case 12 PRP chưa locked → AUTHORITY_READY=false");

        Ok(CharacterAuthorityInitializationV1Rules.AuthorityReady(
                CharacterAuthorityInitializationV1Rules.Locked,
                CharacterAuthorityInitializationV1Rules.Locked,
                CharacterAuthorityInitializationV1Rules.Locked),
            "Case 13 Master + DNA + PRP locked → AUTHORITY_READY=true");

        var first = Master(Ready()).GetAwaiter().GetResult();
        var second = Master(Ready(dup: true)).GetAwaiter().GetResult();
        Ok(first.Outcome.ProviderCalled && first.Mock.CallCount == 1
            && second.Outcome.Gate.Code == "BLOCK_DUPLICATE" && second.Mock.CallCount == 0,
            "Case 14 Duplicate Master generation → BLOCK_DUPLICATE");

        var lockedAuth = Master(Ready(locked: true, character: true, identity: true)).GetAwaiter().GetResult();
        Ok(lockedAuth.Outcome.Gate.Code == "AUTHORITY_CONFLICT" && lockedAuth.Mock.CallCount == 0
            && CharacterAuthorityInitializationV1Rules.MinhShaUnchanged(
                CharacterAuthorityInitializationV1Rules.ProtectedMasterSha,
                CharacterAuthorityInitializationV1Rules.ProtectedDnaSha,
                CharacterAuthorityInitializationV1Rules.ProtectedPrpSha,
                CharacterAuthorityInitializationV1Rules.ProtectedCrpSha)
            && !CharacterAuthorityInitializationV1Rules.MutatesMinh(),
            "Case 15 Locked authority → no generate; golden SHA unchanged");

        var hist = CharacterAuthorityInitializationV1Rules.Evaluate(Ready(historical: true));
        Ok(hist.Code == "REJECTED" && !hist.MayCallProvider
            && CharacterAuthorityInitializationV1Rules.RejectHistoricalStill(
                CharacterAuthorityInitializationV1Rules.HistoricalStillId),
            "Case 16 Historical still → rejected");

        Ok(!CharacterAuthorityInitializationV1Rules.CanOpenCrpGeneration(false)
            && CharacterAuthorityInitializationV1Rules.CanOpenCrpGeneration(true)
            && !CharacterAuthorityInitializationV1Rules.GeneratesCrp(),
            "Case 17 CRP generation trước Authority → blocked");

        Ok(!CharacterAuthorityInitializationV1Rules.CanOpenProduction(false)
            && !CharacterAuthorityInitializationV1Rules.CanOpenProduction(true)
            && !CharacterAuthorityInitializationV1Rules.GeneratesProductionStill()
            && !CharacterAuthorityInitializationV1Rules.GeneratesShot(),
            "Case 18 Production generation trước Authority → blocked");

        var runway = CharacterAuthorityInitializationV1Rules.Evaluate(Ready(provider: "RUNWAY", capability: false));
        var veo = CharacterAuthorityInitializationV1Rules.Evaluate(Ready(provider: "VEO", capability: false));
        Ok(runway.Code == "PROVIDER_CAPABILITY_UNSUPPORTED" && veo.Code == "PROVIDER_CAPABILITY_UNSUPPORTED"
            && !CharacterAuthorityInitializationV1Rules.AllowsRunway()
            && !CharacterAuthorityInitializationV1Rules.AllowsVeo(),
            "Case 19 Runway/Veo unsupported → PROVIDER_CAPABILITY_UNSUPPORTED");

        var app = typeof(CharacterAuthorityInitializationV1Application).Assembly;
        Ok(typeof(ICharacterAuthorityGenerationProvider).IsAssignableFrom(typeof(MockCharacterAuthorityGenerationProvider))
            && !app.GetTypes().Any(t =>
                t.Name.Contains("ContentGemini", StringComparison.Ordinal)
                || t.Name.Contains("GeminiClient", StringComparison.Ordinal)
                || t.FullName?.Contains("Google.GenAI", StringComparison.Ordinal) == true)
            && !CharacterAuthorityInitializationV1Rules.AutoApprove()
            && !CharacterAuthorityInitializationV1Rules.AutoLock()
            && !CharacterAuthorityInitializationV1Rules.AutoSelectProvider(),
            "Case 20 Application architecture scan → no Gemini SDK/HTTP");

        var fp1 = CharacterAuthorityInitializationV1Rules.ExecutionFingerprint("CHAR-002", "ERA-01", "V1", "MASTER");
        var fp2 = CharacterAuthorityInitializationV1Rules.ExecutionFingerprint("CHAR-002", "ERA-01", "V1", "MASTER");
        var fp3 = CharacterAuthorityInitializationV1Rules.ExecutionFingerprint("CHAR-002", "ERA-01", "V2", "MASTER");
        Ok(fp1 == fp2 && fp1 != fp3 && fp1.Length == 64, "fingerprint deterministic, no timestamp");

        return fail;
    }
}
