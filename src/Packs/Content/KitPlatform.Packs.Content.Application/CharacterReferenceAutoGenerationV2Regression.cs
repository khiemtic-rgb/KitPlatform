namespace KitPlatform.Packs.Content;

public static class CharacterReferenceAutoGenerationV2Regression
{
    public const string SuiteId = CharacterReferenceAutoGenerationV2Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var sha = new string('a', 64);
        CharacterReferenceAutoGenerationV2Rules.GateInput Ready(
            bool character = true,
            bool master = true,
            bool masterLocked = true,
            bool dna = true,
            bool dnaLocked = true,
            bool prp = true,
            bool prpLocked = true,
            bool masterSha = true,
            bool dnaSha = true,
            bool prpSha = true,
            bool dnaMaster = true,
            bool prpMaster = true,
            bool prpDna = true,
            bool crpLocked = false,
            string? provider = "GEMINI",
            bool capability = true,
            bool confirm = true,
            bool dup = false,
            bool historical = false) =>
            new(character, master, masterLocked, dna, dnaLocked, prp, prpLocked,
                masterSha, dnaSha, prpSha, dnaMaster, prpMaster, prpDna, crpLocked,
                provider, capability, capability ? null : "PROVIDER_CAPABILITY_UNSUPPORTED",
                confirm, dup, historical);

        var views = CharacterReferenceAutoGenerationV2Rules.RequiredTypes
            .Select(t => new CharacterReferenceViewRequest(t, $"canon {t}", t == "FULL_BODY" ? "3:4" : "1:1", []))
            .ToList();
        var request = new CharacterReferenceSetRequest("CHAR-002", "ERA-01", sha, sha, sha, views);

        async Task<(CharacterReferenceAutoGenerationV2Application.Outcome Outcome, MockCharacterReferenceSetProvider Mock)> Attempt(
            CharacterReferenceAutoGenerationV2Rules.GateInput input,
            bool succeed = true,
            int viewsToReturn = 4)
        {
            var mock = new MockCharacterReferenceSetProvider { Succeed = succeed, ViewsToReturn = viewsToReturn };
            var outcome = await CharacterReferenceAutoGenerationV2Application.ExecuteOnceAsync(input, mock, request);
            return (outcome, mock);
        }

        var case1 = Attempt(Ready()).GetAwaiter().GetResult();
        Ok(case1.Outcome.Gate.Status == "READY" && case1.Outcome.Generation && case1.Outcome.ProviderCalled
            && case1.Outcome.ReferenceCount == 4 && case1.Outcome.Coverage == 4
            && case1.Outcome.DirectorReview == "PENDING" && !case1.Outcome.Approved && !case1.Outcome.Locked
            && !case1.Outcome.CanUse && case1.Mock.CallCount == 1,
            "Case 1 Nam valid → generation=true 4/4 director=PENDING approved=false locked=false");

        var case2 = Attempt(Ready(confirm: false)).GetAwaiter().GetResult();
        Ok(case2.Outcome.Gate.Code == "CONFIRMATION_REQUIRED" && !case2.Outcome.ProviderCalled && !case2.Outcome.Generation
            && case2.Mock.CallCount == 0,
            "Case 2 confirm=false → CONFIRMATION_REQUIRED providerCalled=false");

        var case3 = Attempt(Ready(master: false)).GetAwaiter().GetResult();
        Ok(case3.Outcome.Gate.Code == "MASTER_NOT_READY" && !case3.Outcome.ProviderCalled && case3.Mock.CallCount == 0,
            "Case 3 Master missing → MASTER_NOT_READY providerCalled=false");

        var case4 = Attempt(Ready(dna: false)).GetAwaiter().GetResult();
        Ok(case4.Outcome.Gate.Code == "DNA_NOT_READY" && !case4.Outcome.ProviderCalled && case4.Mock.CallCount == 0,
            "Case 4 DNA missing → DNA_NOT_READY providerCalled=false");

        var case5 = Attempt(Ready(dnaMaster: false)).GetAwaiter().GetResult();
        Ok(case5.Outcome.Gate.Code == "AUTHORITY_SHA_MISMATCH" && !case5.Outcome.ProviderCalled && case5.Mock.CallCount == 0,
            "Case 5 SHA mismatch → AUTHORITY_SHA_MISMATCH providerCalled=false");

        var case6 = Attempt(Ready(provider: "RUNWAY", capability: false)).GetAwaiter().GetResult();
        Ok(case6.Outcome.Gate.Code == "PROVIDER_CAPABILITY_UNSUPPORTED" && !case6.Outcome.ProviderCalled
            && case6.Mock.CallCount == 0 && !CharacterReferenceAutoGenerationV2Rules.AllowsRunway(),
            "Case 6 Runway → PROVIDER_CAPABILITY_UNSUPPORTED providerCalled=false");

        var case7 = Attempt(Ready(crpLocked: true)).GetAwaiter().GetResult();
        Ok(case7.Outcome.Gate.Code == "REFERENCE_ALREADY_COMPLETE" && !case7.Outcome.ProviderCalled && case7.Mock.CallCount == 0,
            "Case 7 CRP LOCKED 4/4 → REFERENCE_ALREADY_COMPLETE providerCalled=false");

        var first = Attempt(Ready()).GetAwaiter().GetResult();
        var second = Attempt(Ready(dup: true)).GetAwaiter().GetResult();
        Ok(first.Outcome.Generation && first.Mock.CallCount == 1
            && second.Outcome.Gate.Code == "BLOCK_DUPLICATE" && second.Mock.CallCount == 0,
            "Case 8 Duplicate → first success, second BLOCK_DUPLICATE, Gemini count=1");

        var case9 = Attempt(Ready(), succeed: false).GetAwaiter().GetResult();
        Ok(!case9.Outcome.Generation && case9.Outcome.ProviderCalled && case9.Mock.CallCount == 1
            && case9.Outcome.Gate.Code == "REFERENCE_GENERATION_FAILED"
            && !CharacterReferenceAutoGenerationV2Rules.AutoRetry(),
            "Case 9 Gemini failure → providerCalled=true generation=false no auto retry");

        var case10 = Attempt(Ready(), viewsToReturn: 3).GetAwaiter().GetResult();
        Ok(case10.Outcome.Coverage == 3 && case10.Outcome.MissingTypes.Contains("FULL_BODY")
            && !case10.Outcome.CanUse && case10.Outcome.Gate.Code == "REFERENCE_SET_INCOMPLETE",
            "Case 10 Partial 3/4 → missing FULL_BODY canUse=false");

        var case11 = Attempt(Ready(crpLocked: true)).GetAwaiter().GetResult();
        Ok(case11.Outcome.Gate.Code == "REFERENCE_ALREADY_COMPLETE" && !case11.Outcome.Generation
            && case11.Mock.CallCount == 0,
            "Case 11 Minh CRP LOCKED → no generation");

        var case12 = Attempt(Ready(historical: true)).GetAwaiter().GetResult();
        Ok(case12.Outcome.Gate.Code == "REJECTED" && !case12.Outcome.ProviderCalled
            && CharacterReferenceAutoGenerationV2Rules.RejectHistoricalStill(
                CharacterReferenceCompletionRules.HistoricalStillId),
            "Case 12 Historical still 7ed003d7 → REJECTED");

        Ok(typeof(ICharacterReferenceGenerationProvider).IsAssignableFrom(typeof(MockCharacterReferenceSetProvider)),
            "Application → ICharacterReferenceGenerationProvider → mock (no HTTP)");
        Ok(!CharacterReferenceAutoGenerationV2Rules.AutoApprove()
            && !CharacterReferenceAutoGenerationV2Rules.AutoLock()
            && !CharacterReferenceAutoGenerationV2Rules.AutoSelectProvider()
            && !CharacterReferenceAutoGenerationV2Rules.OpensFirstRealProduction()
            && !CharacterReferenceAutoGenerationV2Rules.GeneratesShot()
            && !CharacterReferenceAutoGenerationV2Rules.GeneratesVideo()
            && !CharacterReferenceAutoGenerationV2Rules.MutatesMaster()
            && !CharacterReferenceAutoGenerationV2Rules.FourOfFourMeansCanUse(),
            "no auto approve/lock/select/production/video/master");
        Ok(CharacterReferenceAutoGenerationV2Rules.SetCapability("GEMINI") == "SUPPORTED"
            && CharacterReferenceAutoGenerationV2Rules.SetCapability("RUNWAY") == "UNSUPPORTED"
            && CharacterReferenceAutoGenerationV2Rules.SetCapability("VEO") == "UNSUPPORTED",
            "Gemini CHARACTER_REFERENCE_GENERATION SUPPORTED; Runway/Veo UNSUPPORTED");
        Ok(CharacterReferenceAutoGenerationV2Rules.SetSha("CHAR-002", "ERA-01", sha, sha, sha)
            == CharacterReferenceAutoGenerationV2Rules.SetSha("CHAR-002", "ERA-01", sha, sha, sha)
            && CharacterReferenceAutoGenerationV2Rules.SetSha("CHAR-002", "ERA-01", sha, sha, sha)
                != CharacterReferenceAutoGenerationV2Rules.SetSha("CHAR-002", "ERA-01", new string('b', 64), sha, sha),
            "set SHA deterministic + provider-neutral");
        Ok(CharacterReferenceAutoGenerationV2Rules.CrpAfterGenerate == "DRAFT"
            && CharacterReferenceAutoGenerationV2Rules.PendingReview == "PENDING",
            "after generate CRP=DRAFT director=PENDING");

        return fail;
    }
}
