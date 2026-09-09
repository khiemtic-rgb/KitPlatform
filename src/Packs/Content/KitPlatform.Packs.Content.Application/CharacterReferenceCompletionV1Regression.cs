using System.Text.Json;

namespace KitPlatform.Packs.Content;

public static class CharacterReferenceCompletionV1Regression
{
    public const string SuiteId = CharacterReferenceCompletionRules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var sha = new string('a', 64);
        var meta = JsonSerializer.SerializeToElement(new { characterId = "CHAR-001" });
        CharacterReferencePackRules.RefEntry Slot(string type, string path) =>
            new(type, path, sha, sha, meta, true);
        var three = CharacterReferencePackRules.RequiredTypes
            .Where(t => t != "FULL_BODY")
            .Select(t => Slot(t, $"CHAR-001-{t}.jpg"))
            .ToList();
        var four = three.Concat([Slot("FULL_BODY", "CHAR-001-FULL_BODY.jpg")]).ToList();

        Ok(CharacterReferencePackRules.NormalizeCharacterId("CHAR-001") == "CHAR-001", "01 CHAR-001 tồn tại");
        Ok(CharacterReferencePackRules.MasterLocked("MASTER_REFERENCE_LOCKED")
            || CharacterReferencePackRules.MasterLocked("LOCKED"), "02 Master tồn tại");
        Ok(CharacterReferencePackRules.DnaLocked("LOCKED"), "03 DNA tồn tại");
        Ok(CharacterReferencePackRules.EvaluateAuthorityGate(true, true, true, true, true, true, true, true, true, true)
            .Any(x => x.Code == "prp_exists" && x.Pass), "04 PRP tồn tại");
        Ok(CharacterReferencePackRules.PackCode("CHAR-001", "ERA-01", "V1") == "CHAR-001-ERA-01-REF-V1", "05 CRP V1 tồn tại");
        Ok(CharacterReferencePackRules.MissingTypes(CharacterReferencePackRules.EvaluateCoverage(three)).Contains("FULL_BODY"),
            "06 CRP ban đầu thiếu FULL_BODY");

        var valid = new CharacterReferenceCompletionRules.Candidate(
            "asset-full", "CHAR-001", "ERA-01", "FULL_BODY", @"E:\kit\CHAR-001-FULL_BODY.jpg");
        var found = CharacterReferenceCompletionRules.SelectFullBody("CHAR-001", [valid]);
        Ok(found is not null && found.AssetId == "asset-full", "07 Existing FULL_BODY lookup hoạt động");

        var wrong = new CharacterReferenceCompletionRules.Candidate(
            "asset-linh", "CHAR-003", "ERA-01", "FULL_BODY", @"E:\kit\CHAR-003-FULL_BODY.jpg");
        Ok(CharacterReferenceCompletionRules.SelectFullBody("CHAR-001", [wrong]) is null
            && !CharacterReferenceCompletionRules.AcceptFullBody("CHAR-001", wrong), "08 Wrong-character asset bị block");

        Ok(CharacterReferenceCompletionRules.SelectFullBody("CHAR-001", []) is null
            && !CharacterReferencePackRules.PathMatchesView("", "FULL_BODY"), "09 Missing asset bị block");

        Ok(CharacterReferenceCompletionRules.AcceptFullBody("CHAR-001", valid)
            && CharacterReferencePackRules.EvaluateCoverage(four).All(x => x.Pass), "10 Attach FULL_BODY thành công khi asset hợp lệ");

        Ok(CharacterReferenceCompletionRules.SameAttachment(@"E:\kit\CHAR-001-FULL_BODY.jpg", sha, @"E:\kit\CHAR-001-FULL_BODY.jpg", sha)
            && CharacterReferenceCompletionRules.SelectFullBody("CHAR-001", [valid, valid])?.AssetId == "asset-full",
            "11 Attach idempotent");

        var cover3 = CharacterReferencePackRules.EvaluateCoverage(three);
        var cover4 = CharacterReferencePackRules.EvaluateCoverage(four);
        Ok(cover3.Count(x => x.Pass) == 3 && cover4.Count(x => x.Pass) == 4, "12 Coverage 3/4 → 4/4");

        var validatePass = false;
        try
        {
            CharacterReferencePackRules.EnsureValidatedOrThrow(
                CharacterReferencePackRules.EvaluateAuthorityGate(true, true, true, true, true, true, true, true, true, true),
                cover4,
                CharacterReferencePackRules.EvaluateArtifacts(four),
                []);
            validatePass = true;
        }
        catch { validatePass = false; }
        Ok(validatePass, "13 Validate thành công");

        Ok(!CharacterReferencePackRules.CanUse(true, true, true, true, "DRAFT", false, true)
            && CharacterReferencePackRules.CanUse(true, true, true, true, "VALIDATED", true, true),
            "14 canUse false → true");

        var master = "be439c39e067aa6c7727255e9643ac78cb7c6285917af60dda38bf14a32518f1";
        var dna = "75ececad8899211ce31107232fe0288c11a9e113c5bc0e7c0a6c9f749d72f4dc";
        var prp = "5e61ad240aaebaa13dcd91463a41ef9f9c0498fabefe86b7e8b1a1ad973a9444";
        Ok(CharacterReferencePackRules.SameSha(master, master)
            && CharacterReferencePackRules.SameSha(dna, dna)
            && CharacterReferencePackRules.SameSha(prp, prp), "15 Authority SHA unchanged");

        Ok(!CharacterReferencePackRules.AutoApprove() && !CharacterReferenceCompletionRules.AutoApprove(), "16 CRP không tự APPROVE");
        Ok(!CharacterReferencePackRules.AutoLock() && !CharacterReferenceCompletionRules.AutoLock(), "17 CRP không tự LOCK");
        Ok(!CharacterReferenceCompletionRules.AllowsProvider("GEMINI")
            && !CharacterReferencePackRules.CreatesPixels("VALIDATE"), "18 Gemini không gọi");
        Ok(!CharacterReferenceCompletionRules.AllowsProvider("RUNWAY"), "19 Runway không gọi");
        Ok(!CharacterReferenceCompletionRules.AllowsProvider("VEO"), "20 Veo không gọi");
        Ok(!CharacterReferenceCompletionRules.CreatesPixels("ATTACH")
            && !FirstRealProductionRules.CreatesPixels("VALIDATE"), "21 generation=false");
        Ok(!FirstRealProductionRules.MayCallProvider(FirstRealProductionRules.Evaluate(
            new FirstRealProductionRules.GateInput(true, true, true, true, "VALIDATED", true, true, null, true, null, false, false, "CHAR-001", "CHAR-001"))),
            "22 SHOT-001 không execute");
        Ok(!CharacterReferencePackRules.RequireCharacterId(""), "23 Không tạo shot mới");
        Ok(CharacterReferencePackRules.EvaluateDuplicateOrientation(["FULL_BODY", "FULL_BODY"]).Count == 1
            && CharacterReferencePackRules.EvaluateDuplicateOrientation(["FULL_BODY"]).Count == 0, "24 Không duplicate FULL_BODY");

        Ok(!CharacterReferenceCompletionRules.AcceptFullBody("CHAR-001",
                new("7ed003d7-789a-41ef-bcdf-a88637ff14d2", "CHAR-001", "ERA-01", "PRODUCTION_STILL", "still.jpg"))
            && !CharacterReferencePackRules.PathMatchesView("MINH-E01-CANDIDATE-004-D.jpg", "FULL_BODY"),
            "historical still / master not FULL_BODY");
        Ok(CharacterReferenceCompletionRules.StaffCoverage(3, 4, ["FULL_BODY"]).Contains("Toàn thân", StringComparison.Ordinal)
            && CharacterReferenceCompletionRules.StaffCoverage(4, 4, []) == CharacterReferenceCompletionRules.StaffComplete,
            "staff missing / complete language");
        return fail;
    }
}
