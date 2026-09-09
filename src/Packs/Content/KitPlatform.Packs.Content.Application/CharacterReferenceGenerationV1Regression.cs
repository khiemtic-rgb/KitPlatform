using System.Text.Json;

namespace KitPlatform.Packs.Content;

public static class CharacterReferenceGenerationV1Regression
{
    public const string SuiteId = CharacterReferenceGenerationRules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var sha = new string('a', 64);
        var other = new string('b', 64);
        CharacterReferenceGenerationRules.GateInput Ready(
            bool character = true, bool identity = true, bool master = true, bool masterLocked = true,
            bool dna = true, bool dnaLocked = true, bool dnaMatch = true,
            bool prp = true, bool prpOk = true, bool type = true, bool intent = true, bool canonical = true,
            string? provider = "GEMINI", bool capability = true, bool confirm = true, bool dup = false) =>
            new(character, identity, master, masterLocked, dna, dnaLocked, dnaMatch,
                prp, prpOk, type, intent, canonical, provider, capability,
                capability ? null : "PROVIDER_CAPABILITY_UNSUPPORTED", confirm, dup);

        var mock = new MockCharacterReferenceProvider();
        var blocked = false;
        var called = false;
        void Attempt(CharacterReferenceGenerationRules.GateInput input)
        {
            var gate = CharacterReferenceGenerationRules.Evaluate(input);
            called = CharacterReferenceGenerationRules.MayCallProvider(gate);
            if (called)
                mock.GenerateAsync(new ImageGenerationExecutionRequest("canon", [], "3:4", null), CancellationToken.None)
                    .GetAwaiter().GetResult();
            blocked = !called;
        }

        var before = mock.CallCount;
        Attempt(Ready());
        Ok(!blocked && called && mock.CallCount == before + 1, "01 valid character → allowed");

        Attempt(Ready(character: false));
        Ok(blocked && !called && CharacterReferenceGenerationRules.Evaluate(Ready(character: false)).Code == "CHARACTER_MISSING",
            "02 missing character → BLOCKED");

        Attempt(Ready(master: false));
        Ok(blocked && !called && CharacterReferenceGenerationRules.Evaluate(Ready(master: false)).StaffMessage
            == CharacterReferenceGenerationRules.StaffMasterBlock, "03 missing Master → BLOCKED");

        Attempt(Ready(masterLocked: false));
        Ok(blocked && !called, "04 unlocked Master → BLOCKED");

        Attempt(Ready(dna: false));
        Ok(blocked && !called, "05 missing DNA → BLOCKED");

        Attempt(Ready(dnaMatch: false, identity: false));
        Ok(blocked && !called && CharacterReferenceGenerationRules.Evaluate(Ready(dnaMatch: false, identity: false)).Code == "DNA_INVALID",
            "06 DNA/Master mismatch → BLOCKED");

        Attempt(Ready(type: false));
        Ok(blocked && !called && CharacterReferenceGenerationRules.Evaluate(Ready(type: false)).Code == "INVALID_REFERENCE_TYPE",
            "07 invalid reference type → BLOCKED");

        Attempt(Ready(provider: null));
        Ok(blocked && !called
            && CharacterReferenceGenerationRules.Evaluate(Ready(provider: null)).Status == "NEEDS_PROVIDER_SELECTION",
            "08 provider missing → NEEDS_PROVIDER_SELECTION");

        Attempt(Ready(provider: "RUNWAY", capability: false));
        Ok(blocked && !called && !CharacterReferenceGenerationRules.AllowsRunway()
            && CharacterReferenceGenerationRules.CapabilityUnsupported(ProductionOsRules.RunwayVideoProfile),
            "09 unsupported capability → BLOCKED");

        Attempt(Ready(dup: true));
        Ok(blocked && !called && CharacterReferenceGenerationRules.Evaluate(Ready(dup: true)).Code == "BLOCK_DUPLICATE",
            "10 duplicate fingerprint → BLOCK_DUPLICATE");

        var confirmCalls = mock.CallCount;
        Attempt(Ready(confirm: false));
        Ok(blocked && !called && mock.CallCount == confirmCalls, "11 confirm=false → provider not called");

        Attempt(Ready());
        Ok(!blocked && called, "12 confirm=true + valid → provider called");

        mock.Succeed = false;
        var failResult = mock.GenerateAsync(new ImageGenerationExecutionRequest("canon", [], "3:4", null), CancellationToken.None)
            .GetAwaiter().GetResult();
        Ok(!failResult.Succeeded && !CharacterReferenceGenerationRules.AutoApprove(),
            "13 provider failure → no approval");
        mock.Succeed = true;

        Ok(CharacterReferenceGenerationRules.CandidateAfterGenerate() == "READY_FOR_DIRECTOR",
            "14 generated artifact → READY_FOR_DIRECTOR");

        Ok(!CharacterReferenceGenerationRules.RejectReasonValid("")
            && !CharacterReferenceGenerationRules.RejectReasonValid("ab")
            && CharacterReferenceGenerationRules.RejectReasonValid("sai mặt"),
            "15 reject requires reason");

        Ok(!CharacterReferenceGenerationRules.AcceptLocks()
            && !CharacterReferenceGenerationRules.AcceptApproves(),
            "16 accept does not auto-lock");

        var three = CharacterReferencePackRules.RequiredTypes
            .Where(t => t != "FULL_BODY")
            .Select(t => new CharacterReferencePackRules.RefEntry(t, $"CHAR-099-{t}.jpg", sha, sha,
                JsonSerializer.SerializeToElement(new { characterId = "CHAR-099" }), true))
            .ToList();
        var four = three.Concat([
            new CharacterReferencePackRules.RefEntry("FULL_BODY", "CHAR-099-FULL_BODY.jpg", sha, sha,
                JsonSerializer.SerializeToElement(new { characterId = "CHAR-099" }), true),
        ]).ToList();
        Ok(CharacterReferencePackRules.EvaluateCoverage(three).Count(x => x.Pass) == 3
            && CharacterReferencePackRules.EvaluateCoverage(four).Count(x => x.Pass) == 4,
            "17 register updates CRP");

        Ok(!CharacterReferenceGenerationRules.CoverageApproves(4, 4)
            && !CharacterReferenceGenerationRules.RegisterApproves()
            && !CharacterReferencePackRules.CanUse(true, true, true, true, "DRAFT", true, true),
            "18 4/4 does not auto-approve");

        Ok(!CharacterReferenceGenerationRules.MutatesMaster(), "19 no Master mutation");
        Ok(!CharacterReferenceGenerationRules.MutatesDna(), "20 no DNA mutation");
        Ok(!CharacterReferenceGenerationRules.MutatesPrp(), "21 no PRP mutation");
        Ok(!CharacterReferenceGenerationRules.GeneratesShot(), "22 no Shot mutation");
        Ok(!CharacterReferenceGenerationRules.OpensFirstRealProduction(), "23 no First Real Production execution");
        Ok(!CharacterReferenceGenerationRules.AllowsRunway(), "24 no Runway");
        Ok(!CharacterReferenceGenerationRules.AllowsVeo()
            && CharacterReferenceGenerationRules.CapabilityUnsupported(ProductionOsRules.VeoVideoProfile),
            "25 no Veo");

        var intent = CharacterReferenceGenerationRules.BuildDefault("CHAR-099", "ERA-01", "FULL_BODY", sha, sha, sha);
        var same = CharacterReferenceGenerationRules.BuildDefault("CHAR-099", "ERA-01", "FULL_BODY", sha, sha, sha);
        var changed = CharacterReferenceGenerationRules.BuildDefault("CHAR-099", "ERA-01", "FULL_BODY", other, sha, sha);
        Ok(CharacterReferenceGenerationRules.IntentValid(intent)
            && CharacterReferenceGenerationRules.IntentSha(intent) == CharacterReferenceGenerationRules.IntentSha(same)
            && CharacterReferenceGenerationRules.IntentSha(intent) != CharacterReferenceGenerationRules.IntentSha(changed)
            && CharacterReferenceGenerationRules.ExecutionFingerprint(intent, "GEMINI", CharacterReferenceGenerationRules.GenerationConfigForAttempt(1))
                != CharacterReferenceGenerationRules.ExecutionFingerprint(intent, "GEMINI", CharacterReferenceGenerationRules.GenerationConfigForAttempt(2))
            && !CharacterReferenceGenerationRules.AutoRetry(),
            "intent fingerprint deterministic + Director regenerate is not blind retry");

        var canonical = CharacterReferenceGenerationRules.Canonical(intent);
        var compiled = CharacterReferenceGenerationRules.CompileProviderRequest(canonical, [], "FULL_BODY");
        Ok(compiled.Prompt == canonical.Text
            && !compiled.Prompt.Contains("gemini_prompt", StringComparison.OrdinalIgnoreCase)
            && mock.ProviderId == MockCharacterReferenceProvider.Id,
            "canonical compile + mock provider");

        Ok(CharacterReferenceGenerationRules.CapabilityAllowsDirector(ProductionOsRules.GeminiImageProfile)
            && CharacterReferenceGenerationRules.ReferenceCapability(ProductionOsRules.GeminiImageProfile) == "LIMITED"
            && !CharacterReferenceGenerationRules.AutoSelectProvider(),
            "Gemini CHARACTER_REFERENCE LIMITED — Director selects");

        Ok(!CharacterReferenceGenerationRules.MayUseAsIdentitySource(
                CharacterReferenceCompletionRules.HistoricalStillId, "still.jpg"),
            "historical still is not FULL_BODY source");

        Ok(CharacterReferenceGenerationRules.SlotState(false, "READY_FOR_DIRECTOR", "DRAFT", false) == "READY_FOR_DIRECTOR"
            && CharacterReferenceGenerationRules.SlotState(true, "ACCEPTED", "DRAFT", true) == "REGISTERED"
            && CharacterReferenceGenerationRules.SlotState(true, "REGISTERED", "VALIDATED", true) == "CRP_VALIDATED"
            && !CharacterReferenceGenerationRules.ValidateApproves(),
            "state machine no skip / no auto-approve");

        return fail;
    }
}
