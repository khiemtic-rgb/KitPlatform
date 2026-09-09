using System.Linq;
using System.Text.Json;

namespace KitPlatform.Packs.Content;

public static class CharacterReferenceApprovalLockV1Regression
{
    public const string SuiteId = CharacterReferenceApprovalLockRules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var master = "be439c39e067aa6c7727255e9643ac78cb7c6285917af60dda38bf14a32518f1";
        var dna = "75ececad8899211ce31107232fe0288c11a9e113c5bc0e7c0a6c9f749d72f4dc";
        var prp = "5e61ad240aaebaa13dcd91463a41ef9f9c0498fabefe86b7e8b1a1ad973a9444";
        var other = new string('c', 64);
        var dnaSpec = JsonSerializer.SerializeToElement(new
        {
            face = "round child face",
            eyes = "dark brown",
            hair = "short black",
            age = "11",
            expression = "neutral",
            proportion = "child proportion",
            style = "stylized cinematic",
        });
        JsonElement Meta(string characterId) => JsonSerializer.SerializeToElement(new
        {
            characterId,
            face = "round child face",
            eyes = "dark brown",
            hair = "short black",
            age = "11",
            proportion = "child proportion",
            style = "stylized cinematic",
            masterSha256 = master,
            dnaSha256 = dna,
            prpSha256 = prp,
        });
        var good = Required("CHAR-001-FRONT.jpg", master, Meta("CHAR-001"));
        var auth = CharacterReferencePackRules.EvaluateAuthorityGate(true, true, true, true, true, true, true, true, true, true);
        var coverage = CharacterReferencePackRules.EvaluateCoverage(good);
        var artifacts = CharacterReferencePackRules.EvaluateArtifacts(good);
        var identity = CharacterReferencePackRules.EvaluateIdentity(dnaSpec, good);

        Ok(CharacterReferenceApprovalLockRules.ApproveBlock("DRAFT", auth, coverage, artifacts, identity, "CHAR-001", good) is null, "01 4/4 ready DRAFT may approve");
        Ok(CharacterReferenceApprovalLockRules.ShouldWriteApprove("DRAFT")
            && CharacterReferenceApprovalLockRules.ApprovedStatus == "DIRECTOR_APPROVED", "02 DRAFT → DIRECTOR_APPROVED");
        Ok(CharacterReferenceApprovalLockRules.MayLockFrom("DIRECTOR_APPROVED")
            && CharacterReferenceApprovalLockRules.LockBlock("DIRECTOR_APPROVED", auth, coverage, artifacts, identity, "CHAR-001", good) is null, "03 DIRECTOR_APPROVED → LOCKED");
        Ok(CharacterReferenceApprovalLockRules.LockBlock("DRAFT", auth, coverage, artifacts, identity, "CHAR-001", good)
            == CharacterReferenceApprovalLockRules.DirectorRequired, "04 DRAFT → LOCK BLOCK");

        var three = good.Where(e => e.Type != "FULL_BODY").ToList();
        Ok(CharacterReferenceApprovalLockRules.ApproveBlock("DRAFT", auth,
            CharacterReferencePackRules.EvaluateCoverage(three),
            CharacterReferencePackRules.EvaluateArtifacts(three), identity, "CHAR-001", three)
            == CharacterReferenceApprovalLockRules.PackIncomplete, "05 3/4 BLOCK");
        Ok(CharacterReferenceApprovalLockRules.ApproveBlock("DRAFT", auth,
            CharacterReferencePackRules.EvaluateCoverage(three),
            CharacterReferencePackRules.EvaluateArtifacts(three), identity, "CHAR-001", three)
            == CharacterReferenceApprovalLockRules.PackIncomplete, "06 missing FULL_BODY BLOCK");

        var masterFail = CharacterReferencePackRules.EvaluateAuthorityGate(true, true, false, true, true, true, true, true, true, true);
        Ok(CharacterReferenceApprovalLockRules.ApproveBlock("DRAFT", masterFail, coverage, artifacts, identity, "CHAR-001", good)
            == CharacterReferenceApprovalLockRules.MasterMismatch, "07 Master mismatch BLOCK");
        var dnaFail = CharacterReferencePackRules.EvaluateAuthorityGate(true, true, true, true, true, false, true, true, true, true);
        Ok(CharacterReferenceApprovalLockRules.ApproveBlock("DRAFT", dnaFail, coverage, artifacts, identity, "CHAR-001", good)
            == CharacterReferenceApprovalLockRules.DnaMismatch, "08 DNA mismatch BLOCK");
        var prpFail = CharacterReferencePackRules.EvaluateAuthorityGate(true, true, true, true, true, true, true, true, true, false);
        Ok(CharacterReferenceApprovalLockRules.ApproveBlock("DRAFT", prpFail, coverage, artifacts, identity, "CHAR-001", good)
            == CharacterReferenceApprovalLockRules.PrpMismatch, "09 PRP mismatch BLOCK");

        var foreign = Required("CHAR-002-FRONT.jpg", master, Meta("CHAR-002"));
        Ok(CharacterReferenceApprovalLockRules.ApproveBlock("DRAFT", auth, coverage, artifacts, identity, "CHAR-001", foreign)
            == CharacterReferenceApprovalLockRules.IdentityMismatch, "10 wrong character asset BLOCK");
        var pathOwned = Required("CHAR-001-FRONT.jpg", master, JsonSerializer.SerializeToElement(new { }));
        Ok(CharacterReferenceApprovalLockRules.ApproveBlock("DRAFT", auth, coverage, artifacts, identity, "CHAR-001", pathOwned) is null, "10b path ownership when metadata empty");

        var lockedEdit = false;
        try { CharacterReferenceApprovalLockRules.EnsureMutable("EDIT", "LOCKED"); }
        catch (InvalidOperationException ex) { lockedEdit = ex.Message.StartsWith(CharacterReferenceApprovalLockRules.AlreadyLocked, StringComparison.Ordinal); }
        Ok(lockedEdit, "11 LOCKED → edit BLOCK");
        var lockedRegen = false;
        try { CharacterReferenceApprovalLockRules.EnsureMutable("REGENERATE", "LOCKED"); }
        catch (InvalidOperationException ex) { lockedRegen = ex.Message.StartsWith(CharacterReferenceApprovalLockRules.AlreadyLocked, StringComparison.Ordinal); }
        Ok(lockedRegen && !CharacterReferenceApprovalLockRules.CreatesPixels("LOCK"), "12 LOCKED → regenerate BLOCK");
        var lockedDelete = false;
        try { CharacterReferenceApprovalLockRules.EnsureMutable("DELETE", "LOCKED"); }
        catch (InvalidOperationException ex) { lockedDelete = ex.Message.StartsWith(CharacterReferenceApprovalLockRules.AlreadyLocked, StringComparison.Ordinal); }
        Ok(lockedDelete, "13 LOCKED → delete BLOCK");

        Ok(!CharacterReferenceApprovalLockRules.ShouldWriteApprove("DIRECTOR_APPROVED")
            && !CharacterReferenceApprovalLockRules.ShouldWriteApprove("APPROVED"), "14 approve twice no duplicate");
        Ok(!CharacterReferenceApprovalLockRules.ShouldWriteLock("LOCKED"), "15 lock twice no duplicate");

        Ok(!CharacterReferenceApprovalLockRules.AutoApprove()
            && !CharacterReferenceApprovalLockRules.AutoLock()
            && !CharacterReferenceApprovalLockRules.AllowsProvider("GEMINI")
            && !CharacterReferenceApprovalLockRules.CreatesPixels("APPROVE")
            && !CharacterReferenceApprovalLockRules.CreatesPixels("LOCK"), "16 provider safety generation=false");
        Ok(!CharacterReferenceApprovalLockRules.CreatesPixels("GEMINI") == false
            && CharacterReferenceApprovalLockRules.CreatesPixels("RUNWAY")
            && CharacterReferenceApprovalLockRules.CreatesPixels("VEO"), "17 Gemini/Runway/Veo flagged");

        Ok(CharacterReferenceApprovalLockRules.AuthorityUnchanged(master, master)
            && CharacterReferenceApprovalLockRules.AuthorityUnchanged(dna, dna)
            && CharacterReferenceApprovalLockRules.AuthorityUnchanged(prp, prp)
            && !CharacterReferenceApprovalLockRules.AuthorityUnchanged(master, other), "18 authority SHA unchanged");
        Ok(CharacterReferenceApprovalLockRules.NextVersion("V1") == "V2"
            && !CharacterReferenceApprovalLockRules.MutatesMaster()
            && !CharacterReferenceApprovalLockRules.MutatesDna()
            && !CharacterReferenceApprovalLockRules.MutatesPrp(), "19 versioning + upstream immutable");

        var machine = false;
        try { CharacterReferenceApprovalLockRules.EnsureDirector("SYSTEM"); }
        catch (InvalidOperationException) { machine = true; }
        Ok(machine, "20 SYSTEM is not Director");
        var directorOk = true;
        try { CharacterReferenceApprovalLockRules.EnsureDirector("director"); }
        catch { directorOk = false; }
        Ok(directorOk, "21 director actor allowed");

        var approvedToDraft = false;
        try { CharacterReferenceApprovalLockRules.EnsureMutable("EDIT", "DIRECTOR_APPROVED"); }
        catch (InvalidOperationException ex) { approvedToDraft = ex.Message.StartsWith(CharacterReferenceApprovalLockRules.InvalidState, StringComparison.Ordinal); }
        Ok(approvedToDraft, "22 DIRECTOR_APPROVED → DRAFT BLOCK");
        var lockedToApproved = false;
        try { CharacterReferenceApprovalLockRules.EnsureCanApprove("LOCKED"); }
        catch (InvalidOperationException ex) { lockedToApproved = ex.Message.StartsWith(CharacterReferenceApprovalLockRules.AlreadyLocked, StringComparison.Ordinal); }
        Ok(lockedToApproved, "23 LOCKED → DIRECTOR_APPROVED BLOCK");

        Ok(CharacterReferenceApprovalLockRules.ConsistencyVerdict("NEEDS_REVIEW")
            == CharacterReferenceApprovalLockRules.NeedsDirectorReview, "24 uncertain → NEEDS_DIRECTOR_REVIEW");
        Ok(!CharacterReferenceApprovalLockRules.OpensFirstRealProduction()
            && !CharacterReferenceApprovalLockRules.AutoReplace()
            && !CharacterReferenceApprovalLockRules.AutoSelect(), "25 no First Real / auto replace");
        Ok(CharacterReferenceApprovalLockRules.StaffLocked.Contains("Sẵn sàng", StringComparison.Ordinal)
            && CharacterReferenceApprovalLockRules.StaffApprove == "Duyệt bộ ảnh", "26 staff copy");
        Ok(CharacterReferenceApprovalLockRules.MayApproveFrom("READY_FOR_DIRECTOR")
            && !CharacterReferenceApprovalLockRules.MayApproveFrom("LOCKED"),
            "27 READY_FOR_DIRECTOR may Director approve");

        return fail;
    }

    private static IReadOnlyList<CharacterReferencePackRules.RefEntry> Required(string path, string sha, JsonElement meta) =>
        CharacterReferencePackRules.RequiredTypes.Select(t =>
            new CharacterReferencePackRules.RefEntry(
                t,
                path.Replace("FRONT", t, StringComparison.Ordinal),
                sha,
                sha,
                meta,
                true)).ToList();
}
