namespace KitPlatform.Packs.Content;

public static class CharacterAuthorityPipelineV1Regression
{
    public const string SuiteId = CharacterAuthorityPipelineV1Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        CharacterAuthorityPipelineV1Rules.Snapshot Snap(
            bool exists = true,
            bool profile = true,
            bool workspace = true,
            bool locked = false,
            string master = CharacterAuthorityInitializationV1Rules.Missing,
            string dna = CharacterAuthorityInitializationV1Rules.Missing,
            string prp = CharacterAuthorityInitializationV1Rules.Missing,
            string? crp = null,
            int coverage = 0,
            bool consistency = false,
            bool crpApproved = false,
            bool crpLocked = false,
            bool canUse = false,
            bool photo = false,
            bool historical = false,
            bool wrong = false,
            string? provider = null,
            bool confirm = false,
            bool dup = false) =>
            new(exists, profile, workspace, locked, master, dna, prp, crp, coverage, consistency,
                crpApproved, crpLocked, canUse, photo, historical, wrong, provider, confirm, dup);

        var profile = Snap(workspace: false);
        var g1 = CharacterAuthorityPipelineV1Rules.Evaluate(profile);
        Ok(profile.ProfileReady && g1.WorkspaceCreated && g1.PipelineState == CharacterAuthorityPipelineV1Rules.ProfileCreated,
            "CASE 01 New character profile valid → authority workspace created");

        var g2 = CharacterAuthorityPipelineV1Rules.Evaluate(Snap(profile: false), "EXECUTE_MASTER");
        Ok(g2.Code is "PROFILE_NOT_READY" or "MASTER_NOT_READY" && !g2.MayGenerateMaster,
            "CASE 02 Profile missing → MASTER_NOT_READY / PROFILE_NOT_READY");

        var g3 = CharacterAuthorityPipelineV1Rules.Evaluate(
            Snap(provider: "GEMINI", confirm: true), "EXECUTE_MASTER");
        Ok(g3.MayGenerateMaster && CharacterAuthorityPipelineV1Rules.MayCallProvider(g3, "EXECUTE_MASTER"),
            "CASE 03 Generate Master → exactly 1 provider execution allowed");

        var pending = Snap(master: CharacterAuthorityInitializationV1Rules.ReadyForDirector);
        Ok(CharacterAuthorityPipelineV1Rules.ResolveState(pending) == CharacterAuthorityPipelineV1Rules.MasterPendingReview
            && !CharacterAuthorityPipelineV1Rules.AutoApprove(),
            "CASE 04 Master generated → PENDING review");

        var noAppr = CharacterAuthorityPipelineV1Application.AfterMasterLock(
            Snap(master: CharacterAuthorityInitializationV1Rules.ReadyForDirector));
        Ok(noAppr.Gate.Code == "MASTER_NOT_APPROVED" && !noAppr.DnaBuilt,
            "CASE 05 No approval → DNA blocked");

        var canLock = CharacterAuthorityPipelineV1Rules.Evaluate(
            Snap(master: CharacterAuthorityInitializationV1Rules.Approved), "LOCK_MASTER");
        Ok(canLock.MayLockMaster, "CASE 06 Master approved → Master can lock");

        var afterLock = CharacterAuthorityPipelineV1Application.AfterMasterLock(
            Snap(master: CharacterAuthorityInitializationV1Rules.Approved));
        Ok(afterLock.DnaBuilt && afterLock.Locked,
            "CASE 07 Master locked → DNA auto build");

        Ok(afterLock.PrpBuilt && afterLock.PipelineState == CharacterAuthorityPipelineV1Rules.PrpReady,
            "CASE 08 DNA ready → PRP auto build");

        var crpAllowed = CharacterAuthorityPipelineV1Rules.Evaluate(
            Snap(master: CharacterAuthorityInitializationV1Rules.Locked,
                dna: CharacterAuthorityInitializationV1Rules.Locked,
                prp: CharacterAuthorityInitializationV1Rules.Locked), "GENERATE_CRP");
        Ok(crpAllowed.MayGenerateCrpSet, "CASE 09 PRP ready → CRP set generation allowed");

        var sha = new string('a', 64);
        var views = CharacterReferenceAutoGenerationV2Rules.RequiredTypes
            .Select(t => new CharacterReferenceViewRequest(t, t, "1:1", []))
            .ToList();
        var request = new CharacterReferenceSetRequest("CHAR-002", "ERA-01", sha, sha, sha, views);
        var mock = new MockCharacterReferenceSetProvider();
        var set = CharacterAuthorityPipelineV1Application.GenerateCrpSetAsync(
            Snap(master: CharacterAuthorityInitializationV1Rules.Locked,
                dna: CharacterAuthorityInitializationV1Rules.Locked,
                prp: CharacterAuthorityInitializationV1Rules.Locked),
            mock, request).GetAwaiter().GetResult();
        Ok(set.ProviderCalled && mock.CallCount == 1 && set.ReferenceCount == 4,
            "CASE 10 CRP generation → exactly 1 set / 4 references");

        var missingBody = Snap(master: CharacterAuthorityInitializationV1Rules.Locked,
            dna: CharacterAuthorityInitializationV1Rules.Locked,
            prp: CharacterAuthorityInitializationV1Rules.Locked,
            coverage: 3, consistency: false);
        Ok(CharacterAuthorityPipelineV1Rules.ResolveState(missingBody) == CharacterAuthorityPipelineV1Rules.CrpGenerating
            && !CharacterAuthorityPipelineV1Rules.CharacterAuthorityReady(missingBody),
            "CASE 11 CRP missing FULL_BODY → validation FAIL");

        var four = Snap(master: CharacterAuthorityInitializationV1Rules.Locked,
            dna: CharacterAuthorityInitializationV1Rules.Locked,
            prp: CharacterAuthorityInitializationV1Rules.Locked,
            crp: "DRAFT", coverage: 4, consistency: true);
        Ok(CharacterAuthorityPipelineV1Rules.ResolveState(four) == CharacterAuthorityPipelineV1Rules.CrpPendingReview
            && !CharacterAuthorityPipelineV1Rules.AutoApprove(),
            "CASE 12 CRP 4/4 + consistency PASS → PENDING Director");

        var noCrpAppr = CharacterAuthorityPipelineV1Rules.Evaluate(four, "LOCK_CRP");
        Ok(noCrpAppr.Code == "CRP_NOT_APPROVED" && !noCrpAppr.MayLockCrp,
            "CASE 13 No CRP approval → cannot lock");

        var approved = four with { CrpApproved = true };
        Ok(CharacterAuthorityPipelineV1Rules.Evaluate(approved, "LOCK_CRP").MayLockCrp,
            "CASE 14 CRP approved → can lock");

        var locked = approved with { CrpLocked = true, CanUse = true, CrpStatus = "LOCKED" };
        Ok(CharacterAuthorityPipelineV1Rules.CharacterAuthorityReady(locked) && locked.CanUse,
            "CASE 15 CRP locked → canUse=true");

        Ok(CharacterAuthorityPipelineV1Rules.ResolveState(locked) == CharacterAuthorityPipelineV1Rules.CharacterReady
            && CharacterAuthorityPipelineV1Rules.ProductionAllowed(CharacterAuthorityPipelineV1Rules.CharacterReady)
            && CharacterAuthorityPipelineV1Rules.SameAuthorityForAllShots()
            && !CharacterAuthorityPipelineV1Rules.CreatesAuthorityPerShot(),
            "CASE 16 Character Authority ready → production gate READY");

        var dup = CharacterAuthorityPipelineV1Rules.Evaluate(
            Snap(provider: "GEMINI", confirm: true, dup: true), "EXECUTE_MASTER");
        Ok(dup.Code == "BLOCK_DUPLICATE" && !dup.MayGenerateMaster,
            "CASE 17 Same fingerprint → BLOCK_DUPLICATE");

        var wrong = CharacterAuthorityPipelineV1Rules.Evaluate(Snap(wrong: true), "EXECUTE_MASTER");
        Ok(wrong.Code == "WRONG_CHARACTER", "CASE 18 Wrong character reference → BLOCK");

        var hist = CharacterAuthorityPipelineV1Rules.Evaluate(Snap(historical: true), "EXECUTE_MASTER");
        Ok(hist.Code == "HISTORICAL_STILL"
            && CharacterAuthorityPipelineV1Rules.RejectHistoricalStill(
                CharacterAuthorityInitializationV1Rules.HistoricalStillId),
            "CASE 19 Historical still → BLOCK");

        Ok(CharacterAuthorityInitializationV1Rules.MinhShaUnchanged(
                CharacterAuthorityInitializationV1Rules.ProtectedMasterSha,
                CharacterAuthorityInitializationV1Rules.ProtectedDnaSha,
                CharacterAuthorityInitializationV1Rules.ProtectedPrpSha,
                CharacterAuthorityInitializationV1Rules.ProtectedCrpSha)
            && !CharacterAuthorityPipelineV1Rules.MutatesMinh(),
            "CASE 20 Locked Minh → absolutely unchanged");

        var photo = CharacterAuthorityPipelineV1Rules.Evaluate(
            Snap(photo: true, provider: "GEMINI", confirm: true), "EXECUTE_MASTER");
        Ok(photo.Code == "INVALID_REFERENCE"
            && CharacterAuthorityPipelineV1Rules.RejectPhotorealisticExternal(
                "/content/famixa/canon/CHAR-002-nam-master.png"),
            "CASE 21 Photorealistic external-like asset → cannot become Master automatically");

        var missingProv = CharacterAuthorityPipelineV1Rules.Evaluate(Snap(confirm: true), "EXECUTE_MASTER");
        Ok(missingProv.Code == "PROVIDER_MISSING", "CASE 22 Provider missing → BLOCK");

        var runway = CharacterAuthorityPipelineV1Rules.Evaluate(
            Snap(provider: "RUNWAY", confirm: true), "EXECUTE_MASTER");
        Ok(runway.Code == "PROVIDER_CAPABILITY_UNSUPPORTED", "CASE 23 Unsupported provider → BLOCK");

        var app = typeof(CharacterAuthorityPipelineV1Rules).Assembly;
        Ok(!app.GetTypes().Any(t =>
                t.Name.Contains("ContentGemini", StringComparison.Ordinal)
                || t.FullName?.Contains("Google.GenAI", StringComparison.Ordinal) == true)
            && typeof(ICharacterReferenceGenerationProvider).IsAssignableFrom(typeof(MockCharacterReferenceSetProvider))
            && typeof(ICharacterAuthorityGenerationProvider).IsAssignableFrom(typeof(MockCharacterAuthorityGenerationProvider)),
            "CASE 24 Application references Gemini SDK → FAIL architecture scan");

        var crpEarly = CharacterAuthorityPipelineV1Rules.Evaluate(
            Snap(master: CharacterAuthorityInitializationV1Rules.ReadyForDirector), "GENERATE_CRP");
        Ok(crpEarly.Code == "MASTER_NOT_LOCKED" && !crpEarly.MayGenerateCrpSet,
            "CASE 25 CRP generation with Master not locked → BLOCK");

        Ok(!CharacterAuthorityPipelineV1Rules.ProductionAllowed(CharacterAuthorityPipelineV1Rules.ProfileCreated)
            && !CharacterAuthorityPipelineV1Rules.ProductionAllowed(CharacterAuthorityPipelineV1Rules.PrpReady)
            && CharacterAuthorityPipelineV1Rules.ProductionAllowed(CharacterAuthorityPipelineV1Rules.CharacterReady)
            && !CharacterAuthorityPipelineV1Rules.OpensFirstRealProduction(),
            "CASE 26 Production tries to use character with Authority not READY → BLOCK");

        Ok(!CharacterAuthorityPipelineV1Rules.AutoRunFullPipeline()
            && !CharacterAuthorityPipelineV1Rules.AutoApprove()
            && !CharacterAuthorityPipelineV1Rules.AutoLock(),
            "no auto full pipeline / approve / lock");

        return fail;
    }
}
