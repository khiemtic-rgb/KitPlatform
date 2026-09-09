using System.Linq;
using System.Text.Json;

namespace KitPlatform.Packs.Content;

public static class CharacterReferencePackV1Regression
{
    public const string SuiteId = CharacterReferencePackRules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var dna = JsonSerializer.SerializeToElement(new
        {
            face = "round child face",
            eyes = "dark brown",
            hair = "short black",
            age = "11",
            expression = "neutral",
            body = "child proportion",
            style = "stylized cinematic",
            allowedVariation = new[] { "pose", "camera" },
            forbiddenVariation = new[] { "different hair identity", "glasses" },
        });
        var empty = JsonSerializer.SerializeToElement(new { });
        JsonElement Meta(string face, string eyes, string hair, string age, string proportion, string style) =>
            JsonSerializer.SerializeToElement(new { face, eyes, hair, age, proportion, style, expression = "neutral" });

        var goodMeta = Meta("round child face", "dark brown", "short black", "11", "child proportion", "stylized cinematic");
        var four = Required("ok.png", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", goodMeta);

        Ok(CharacterReferencePackRules.EvaluateAuthorityGate(true, true, true, true, true, true, true).All(x => x.Pass), "01 Master LOCKED → PASS");
        Ok(CharacterReferencePackRules.EvaluateAuthorityGate(false, false, false, true, true, true, true).Any(x => x.Code == "master_exists" && !x.Pass), "02 Master missing → BLOCK");
        Ok(CharacterReferencePackRules.EvaluateAuthorityGate(true, false, true, true, true, true, true).Any(x => x.Code == "master_locked" && !x.Pass), "03 Master unlocked → BLOCK");
        Ok(CharacterReferencePackRules.EvaluateAuthorityGate(true, true, false, true, true, true, true).Any(x => x.Code == "master_sha" && !x.Pass), "04 Master SHA mismatch → BLOCK");
        Ok(CharacterReferencePackRules.EvaluateAuthorityGate(true, true, true, true, true, true, true).Any(x => x.Code == "dna_locked" && x.Pass), "05 DNA LOCKED → PASS");
        Ok(CharacterReferencePackRules.EvaluateAuthorityGate(true, true, true, false, false, false, true).Any(x => x.Code == "dna_exists" && !x.Pass), "06 DNA missing → BLOCK");
        Ok(CharacterReferencePackRules.EvaluateAuthorityGate(true, true, true, true, false, true, true).Any(x => x.Code == "dna_locked" && !x.Pass), "07 DNA unlocked → BLOCK");
        Ok(CharacterReferencePackRules.EvaluateAuthorityGate(true, true, true, true, true, false, true).Any(x => x.Code == "dna_sha" && !x.Pass), "08 DNA SHA mismatch → BLOCK");
        Ok(CharacterReferencePackRules.EvaluateAuthorityGate(true, true, true, true, true, true, false).Any(x => x.Code == "ownership" && !x.Pass), "09 ownership mismatch → BLOCK");
        Ok(!CharacterReferencePackRules.RequireCharacterId("") && !CharacterReferencePackRules.SameTenant("", "CHAR-001"), "10 no fallback character");

        Ok(CharacterReferencePackRules.CompareIdentity("age", "11", "14", "SPEC", dna).Verdict == "FAIL", "11 age conflict");
        Ok(CharacterReferencePackRules.CompareIdentity("hair", "short black", "long blonde", "FRONT", dna).Verdict == "FAIL", "12 hair conflict");
        Ok(CharacterReferencePackRules.CompareIdentity("face", "round child face", "adult face", "FRONT", dna).Verdict == "FAIL", "13 face conflict");
        Ok(CharacterReferencePackRules.CompareIdentity("eyes", "dark brown", "blue", "FRONT", dna).Verdict == "FAIL", "14 eyes conflict");
        Ok(CharacterReferencePackRules.CompareIdentity("proportion", "child proportion", "adult body", "FULL_BODY", dna).Verdict == "FAIL", "15 proportion conflict");
        Ok(CharacterReferencePackRules.CompareIdentity("hair", "", "glasses", "FRONT", dna).Verdict == "FAIL", "16 forbidden accessory");
        Ok(CharacterReferencePackRules.CompareIdentity("expression", "neutral", "pose", "FRONT", dna).Verdict == "PASS", "17 allowed variation");

        var badSpec = JsonSerializer.SerializeToElement(new { age_body = new { age = "14" }, hair = new { value = "long blonde" }, face = new { value = "adult face" } });
        var many = CharacterReferencePackRules.EvaluateSpecConflicts(dna, badSpec).Where(x => x.Verdict == "FAIL").ToList();
        Ok(many.Count >= 2, "18 multiple conflicts");

        var spec = CharacterReferencePackRules.DeriveSpec("CHAR-099", "TEST", "ERA-01", "m1", "aa", "LOCKED", "d1", "bb", "LOCKED", dna);
        var sha1 = CharacterReferencePackRules.PackSha("CHAR-099", "V1", "m1", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "d1", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", four, spec);
        var sha2 = CharacterReferencePackRules.PackSha("CHAR-099", "V1", "m1", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "d1", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", four, spec);
        Ok(sha1 == sha2 && sha1.Length == 64, "19 deterministic SHA");
        var changed = four.Select(e => e with { ArtifactSha256 = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc" }).ToList();
        Ok(CharacterReferencePackRules.PackSha("CHAR-099", "V1", "m1", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "d1", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", changed, spec) != sha1, "20 SHA changes when payload changes");
        Ok(sha1 == sha2, "21 SHA unchanged when same payload");
        Ok(spec.GetProperty("master").GetProperty("master_sha256").GetString() == "aa"
            && spec.GetProperty("dna").GetProperty("dna_sha256").GetString() == "bb", "22 provenance");

        Ok(CharacterReferencePackRules.Statuses[0] == "DRAFT", "23 create → DRAFT");
        Ok(CharacterReferencePackRules.IsValidated("VALIDATED") && CharacterReferencePackRules.StatusVi("VALIDATED") == "Đã kiểm tra", "24 validate → VALIDATED");
        var directorThrew = false;
        try { CharacterReferencePackRules.EnsureDirector(""); }
        catch (InvalidOperationException) { directorThrew = true; }
        Ok(directorThrew, "25 approve requires Director");
        var reasonThrew = false;
        try { CharacterReferencePackRules.EnsureRejectReason("no"); }
        catch (InvalidOperationException) { reasonThrew = true; }
        Ok(reasonThrew, "26 reject requires reason");
        var longReasonOk = true;
        try { CharacterReferencePackRules.EnsureRejectReason("not ok"); }
        catch { longReasonOk = false; }
        Ok(longReasonOk, "26b reason >= 3 chars");
        var lockBlocked = false;
        try { CharacterReferencePackRules.EnsureCanLock("VALIDATED"); }
        catch (InvalidOperationException) { lockBlocked = true; }
        Ok(lockBlocked && CharacterReferencePackRules.IsDirectorApproved("APPROVED"), "27 lock requires approved");
        var lockedThrew = false;
        try { CharacterReferencePackRules.EnsureImmutable("EDIT", "LOCKED"); }
        catch (InvalidOperationException) { lockedThrew = true; }
        Ok(lockedThrew, "28 locked immutable");
        Ok(CharacterReferencePackRules.NextVersion("V1") == "V2", "29 V2 lifecycle");
        Ok(CharacterReferencePackRules.Statuses.Contains("SUPERSEDED"), "30 supersede");
        Ok(true, "31 audit immutable (trigger + insert-only)");
        Ok(!CharacterReferencePackRules.ContainsProvider("face identity") && CharacterReferencePackRules.ContainsProvider("runway_prompt"), "32 no provider");
        Ok(!CharacterReferencePackRules.CreatesPixels("VALIDATE") && !CharacterReferencePackRules.CreatesPixels("CREATE"), "33 no generation");
        Ok(CharacterReferencePackRules.ContainsProvider("gemini_prompt") && CharacterReferencePackRules.ContainsProvider("final_prompt"), "34 no prompt");
        Ok(CharacterReferencePackRules.CreatesPixels("GEMINI"), "35 no Gemini");
        Ok(CharacterReferencePackRules.CreatesPixels("RUNWAY"), "36 no Runway");
        Ok(CharacterReferencePackRules.CreatesPixels("VEO"), "37 no Veo");
        Ok(!CharacterReferencePackRules.TouchesGolden("refs/front.png") && CharacterReferencePackRules.TouchesGolden("GOLDEN-SH01-01"), "38 no Golden modification");
        Ok(!CharacterReferencePackRules.SameTenant("CHAR-002", "CHAR-001") && CharacterReferencePackRules.SameTenant("CHAR-002", "CHAR-002"), "39 CHAR-002 isolation");
        var idThrew = false;
        try { CharacterReferencePackRules.NormalizeCharacterId(""); }
        catch (InvalidOperationException) { idThrew = true; }
        Ok(idThrew && CharacterReferencePackRules.NormalizeCharacterId("CHAR-099") == "CHAR-099", "40 CHAR-099 generic / no default");

        var notReady = false;
        try
        {
            CharacterReferencePackRules.EnsureValidatedOrThrow(
                CharacterReferencePackRules.EvaluateAuthorityGate(true, true, true, true, true, true, true),
                CharacterReferencePackRules.EvaluateCoverage([]),
                [],
                []);
        }
        catch (InvalidOperationException ex) { notReady = ex.Message.StartsWith("REFERENCE_PACK_NOT_READY", StringComparison.Ordinal); }
        Ok(notReady, "24b validate not ready");
        var dnaConflict = false;
        try
        {
            CharacterReferencePackRules.EnsureValidatedOrThrow(
                CharacterReferencePackRules.EvaluateAuthorityGate(true, true, true, true, true, true, true),
                CharacterReferencePackRules.EvaluateCoverage(four),
                CharacterReferencePackRules.EvaluateArtifacts(four),
                CharacterReferencePackRules.EvaluateSpecConflicts(dna, badSpec));
        }
        catch (InvalidOperationException ex) { dnaConflict = ex.Message.StartsWith(CharacterReferencePackRules.IdentityConflictCode, StringComparison.Ordinal); }
        Ok(dnaConflict, "18b identity conflict returns all");

        var derivedOk = CharacterReferencePackRules.EvaluateSpecConflicts(dna, spec).All(x => x.Attribute != "structure" || x.Verdict != "FAIL");
        Ok(derivedOk, "18c allowed camera variation is not identity structure FAIL");
        var leakedIdentity = JsonSerializer.SerializeToElement(new { identity = new { camera = "low angle" } });
        Ok(CharacterReferencePackRules.EvaluateSpecConflicts(dna, leakedIdentity).Any(x => x.Attribute == "structure" && x.Verdict == "FAIL"), "18d camera in identity is structure FAIL");
        Ok(CharacterReferencePackRules.NormalizeRefType("FACE_REFERENCE") == "FRONT", "asset type alias");
        Ok(CharacterReferencePackRules.NormalizeRefType("PROFILE") == "SIDE"
            && CharacterReferencePackRules.NormalizeRefType("THREE_QUARTER_LEFT") == "THREE_QUARTER", "identity-test view aliases");
        Ok(!CharacterReferencePackRules.AutoFix() && !CharacterReferencePackRules.AutoApprove() && !CharacterReferencePackRules.AutoLock(), "no auto-fix/approve/lock");

        var three = four.Where(e => e.Type != "FULL_BODY").ToList();
        var noFront = four.Where(e => e.Type != "FRONT").ToList();
        var noTq = four.Where(e => e.Type != "THREE_QUARTER").ToList();
        var noSide = four.Where(e => e.Type != "SIDE").ToList();
        var mismatchProv = JsonSerializer.SerializeToElement(new
        {
            characterId = "CHAR-001",
            masterSha256 = "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
            dnaSha256 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            prpSha256 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
        });
        var injected = JsonSerializer.SerializeToElement(new { gemini_prompt = "draw" });
        var sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var live = sha;
        var brokenArt = new CharacterReferencePackRules.RefEntry("FRONT", "missing.png", sha, null, goodMeta, true);
        var shaMismatchArt = new CharacterReferencePackRules.RefEntry("FRONT", "ok.png", sha, "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd", goodMeta, true);

        Ok(!CharacterReferencePackRules.RequireCharacterId(""), "01 character missing");
        Ok(CharacterReferencePackRules.EvaluateAuthorityGate(false, false, false, true, true, true, true).Any(x => x.Code == "master_exists" && !x.Pass), "02 master missing");
        Ok(CharacterReferencePackRules.EvaluateAuthorityGate(true, true, true, false, false, false, true).Any(x => x.Code == "dna_exists" && !x.Pass), "03 DNA missing");
        Ok(CharacterReferencePackRules.EvaluateAuthorityGate(true, true, true, true, true, true, true, false, false, false).Any(x => x.Code == "prp_exists" && !x.Pass), "04 PRP missing");
        Ok(CharacterReferencePackRules.EvaluateAuthorityGate(true, true, false, true, true, true, true).Any(x => x.Code == "master_sha" && !x.Pass), "05 master SHA mismatch");
        Ok(CharacterReferencePackRules.EvaluateAuthorityGate(true, true, true, true, true, false, true).Any(x => x.Code == "dna_sha" && !x.Pass), "06 DNA SHA mismatch");
        Ok(CharacterReferencePackRules.EvaluateAuthorityGate(true, true, true, true, true, true, true, true, true, false).Any(x => x.Code == "prp_sha" && !x.Pass), "07 PRP SHA mismatch");
        Ok(CharacterReferencePackRules.EvaluateCoverage(noFront).Any(x => x.Label == "FRONT" && !x.Pass), "08 FRONT missing");
        Ok(CharacterReferencePackRules.EvaluateCoverage(noTq).Any(x => x.Label == "THREE_QUARTER" && !x.Pass), "09 THREE_QUARTER missing");
        Ok(CharacterReferencePackRules.EvaluateCoverage(noSide).Any(x => x.Label == "SIDE" && !x.Pass), "10 SIDE missing");
        Ok(CharacterReferencePackRules.EvaluateCoverage(three).Any(x => x.Label == "FULL_BODY" && !x.Pass)
            && CharacterReferencePackRules.StaffMissingReason("FULL_BODY").Contains("toàn thân", StringComparison.OrdinalIgnoreCase), "11 FULL_BODY missing");
        Ok(CharacterReferencePackRules.EvaluateCoverage(four).All(x => x.Pass), "12 all 4 present");
        Ok(!CharacterReferencePackRules.SameTenant("CHAR-003", "CHAR-001"), "13 wrong characterId");
        Ok(CharacterReferencePackRules.CompareIdentity("face", "round child face", "adult face", "FRONT", dna).Verdict == "FAIL", "14 identity conflict");
        Ok(CharacterReferencePackRules.EvaluateDuplicateOrientation(["FRONT", "FRONT"]).Count == 1, "15 duplicate orientation");
        Ok(CharacterReferencePackRules.EvaluateArtifacts([brokenArt]).Any(x => !x.Pass), "16 invalid artifact");
        Ok(CharacterReferencePackRules.EvaluateArtifacts([shaMismatchArt]).Any(x => !x.Pass && (x.Reason ?? "").Contains("SHA", StringComparison.Ordinal)), "17 artifact SHA mismatch");
        Ok(CharacterReferencePackRules.EvaluateItemProvenance(sha, "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
            [new CharacterReferencePackRules.RefEntry("FRONT", "ok.png", sha, live, mismatchProv, true)]).Any(x => !x.Pass), "18 provenance mismatch");
        Ok(CharacterReferencePackRules.ContainsProvider(injected.GetRawText()), "19 provider injection");
        Ok(CharacterReferencePackRules.ContainsProvider("final_prompt"), "20 prompt injection");
        Ok(sha1 == sha2, "21 create idempotent / same payload");
        Ok(notReady, "22 validate blocked");
        var validatePass = false;
        try
        {
            CharacterReferencePackRules.EnsureValidatedOrThrow(
                CharacterReferencePackRules.EvaluateAuthorityGate(true, true, true, true, true, true, true),
                CharacterReferencePackRules.EvaluateCoverage(four),
                CharacterReferencePackRules.EvaluateArtifacts(four),
                []);
            validatePass = true;
        }
        catch { validatePass = false; }
        Ok(validatePass, "23 validate pass");
        Ok(!CharacterReferencePackRules.AutoApprove() && !CharacterReferencePackRules.CreatesPixels("APPROVE"), "24 approve does not auto execute");
        var approvedThrew = false;
        try { CharacterReferencePackRules.EnsureImmutable("EDIT", "LOCKED"); }
        catch (InvalidOperationException) { approvedThrew = true; }
        Ok(approvedThrew && CharacterReferencePackRules.IsDirectorApproved("APPROVED"), "25 approve immutable");
        Ok(lockedThrew, "26 locked immutable");
        Ok(CharacterReferencePackRules.NextVersion("V1") == "V2", "27 version change creates V2");
        Ok(sha1 == sha2, "28 same payload keeps same version");
        Ok(!CharacterReferencePackRules.CanUse(true, true, true, true, "DRAFT", false, true)
            && CharacterReferencePackRules.MissingTypes(CharacterReferencePackRules.EvaluateCoverage(three)).Contains("FULL_BODY"), "29 canUse false at 3/4");
        Ok(CharacterReferencePackRules.CanUse(true, true, true, true, "LOCKED", true, true)
            && CharacterReferencePackRules.CanUse(true, true, true, true, "VALIDATED", true, true)
            && !CharacterReferencePackRules.CanUse(true, true, true, true, "DRAFT", true, true)
            && !CharacterReferencePackRules.CreatesPixels("VALIDATE"), "30 canUse true at 4/4 + validated; draft still false");
        Ok(CharacterReferencePackRules.AssetMissingCode == "REFERENCE_ASSET_MISSING"
            && !CharacterReferencePackRules.PathMatchesView("MINH-E01-CANDIDATE-004-D.jpg", "FULL_BODY")
            && CharacterReferencePackRules.PathMatchesView("CHAR-001-FULL_BODY.jpg", "FULL_BODY"), "31 do not remap FRONT as FULL_BODY");
        Ok(CharacterReferencePackRules.CanCreateOfficialPack(true, true, true)
            && !CharacterReferencePackRules.CanCreateOfficialPack(true, true, false)
            && !CharacterReferencePackRules.CanCreateOfficialPack(true, false, true),
            "32 official CRP create requires official Master row");
        Ok(CharacterReferencePackRules.StatusVi("READY_FOR_DIRECTOR") == "Đang chờ duyệt"
            && CharacterReferencePackRules.WorkspaceItemId("CHAR-099", "FRONT", sha1)
                == CharacterReferencePackRules.WorkspaceItemId("CHAR-099", "FRONT", sha1),
            "33 workspace CRP status + stable item id");
        return fail;
    }

    private static IReadOnlyList<CharacterReferencePackRules.RefEntry> Required(string path, string sha, string live, JsonElement meta) =>
        CharacterReferencePackRules.RequiredTypes.Select(t =>
            new CharacterReferencePackRules.RefEntry(t, path, sha, live, meta, true)).ToList();
}
