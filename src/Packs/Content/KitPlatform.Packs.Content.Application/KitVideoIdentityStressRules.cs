using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KitPlatform.Packs.Content;

public static class KitVideoIdentityStressRules
{
    public const string DocumentId = "CHAR-001_MINH_IDENTITY_STRESS_TEST_IMPLEMENTATION_V1";
    public const string Kind = "IDENTITY_STRESS";
    public const string AllowedCandidate = "MINH-E01-CANDIDATE-004-D";
    public const int RequiredCount = 10;
    public const int ScoreThreshold = 80;
    public const string PromptVersion = "V1";

    public static readonly (string Code, string Group, string Key, string Label, string Prompt)[] Cases =
    [
        ("ST-01", "CAMERA", "EXTREME_3_4", "Extreme 3/4", "Strong three-quarter view. Same Minh. Face structure must stay the same boy."),
        ("ST-02", "CAMERA", "HIGH_ANGLE", "High angle", "Camera above eye level. Same eyes nose mouth head proportion. No warp."),
        ("ST-03", "CAMERA", "LOW_ANGLE", "Low angle", "Camera below eye level. Keep jaw nose eye spacing hair silhouette. Same Minh 11."),
        ("ST-04", "CAMERA", "LOOKING_UP_DOWN", "Looking up / down", "Minh looking up or down. Same face. Not a new boy."),
        ("ST-05", "EMOTION", "STRONG_EMOTION", "Strong emotion", "Strong but not extreme emotion: worried or surprised or sad or happy. Identity must not change."),
        ("ST-06", "POSE", "MOTION_POSE", "Motion pose", "Minh in motion: walking fast or turning. Not a portrait pose. Same boy."),
        ("ST-07", "LIGHTING", "LIGHTING_CHANGE", "Lighting change", "Changed light: indoor soft or warm or cool or outdoor. Lighting must not change identity."),
        ("ST-08", "OCCLUSION", "PARTIAL_OCCLUSION", "Partial occlusion", "Partial occlusion by hair or hand or foreground object. Face still readable as Minh."),
        ("ST-09", "SCENE", "SCENE_CONTEXT", "Scene context", "Minh sitting at a simple study desk. Single scene. Not a sheet. Same boy."),
        ("ST-10", "SCENE", "MULTI_PERSON", "Multi person", "Minh with one other person. Identify Minh correctly. No face merge. No duplicate Minh. Not a Master."),
    ];

    public static readonly string[] RequiredCases = Cases.Select(c => c.Code).ToArray();

    public static bool Accepts(string? project, string? character, string? era) =>
        string.Equals(project, "FAMIXA", StringComparison.OrdinalIgnoreCase)
        && string.Equals(character, "CHAR-001", StringComparison.OrdinalIgnoreCase)
        && string.Equals(era, "ERA-01", StringComparison.OrdinalIgnoreCase);

    public static bool DnaAllows(string? status) =>
        string.Equals(status, "APPROVED", StringComparison.OrdinalIgnoreCase);

    public static bool IsAllowedCandidate(string? code) =>
        string.Equals(code, AllowedCandidate, StringComparison.OrdinalIgnoreCase);

    public static (string Code, string Group, string Key, string Label, string Prompt)? CaseOf(string? code)
    {
        var row = Cases.FirstOrDefault(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrEmpty(row.Code) ? null : row;
    }

    public static string GroupOf(string code) => CaseOf(code)?.Group ?? "";

    public static void EnsureCanOpen(
        string? candidateCode,
        string? dnaStatus,
        bool identityEligible,
        bool identityComplete,
        bool artifactOk,
        bool artifactReadable,
        string? sha256,
        string? liveSha256,
        bool visionPass,
        string? lifecycle,
        bool lockedMaster)
    {
        if (!IsAllowedCandidate(candidateCode))
            throw new InvalidOperationException("STRESS_TEST_BLOCKED: chỉ MINH-E01-CANDIDATE-004-D.");
        if (!DnaAllows(dnaStatus))
            throw new InvalidOperationException("STRESS_TEST_BLOCKED: Visual DNA phải APPROVED.");
        if (lockedMaster)
            throw new InvalidOperationException("STRESS_TEST_BLOCKED: Master đã LOCKED.");
        var life = (lifecycle ?? "").ToUpperInvariant();
        if (life is "INELIGIBLE" or "NOT_ELIGIBLE" or "REJECTED")
            throw new InvalidOperationException("STRESS_TEST_BLOCKED: candidate không eligible.");
        if (!identityEligible)
            throw new InvalidOperationException("STRESS_TEST_BLOCKED: identity candidate chưa eligible.");
        if (!identityComplete)
            throw new InvalidOperationException("STRESS_TEST_BLOCKED: Identity Test 7/7 chưa COMPLETE.");
        if (!artifactOk || !artifactReadable || string.IsNullOrWhiteSpace(sha256))
            throw new InvalidOperationException("STRESS_TEST_BLOCKED: artifact / SHA256 chưa đủ.");
        if (!string.IsNullOrWhiteSpace(liveSha256) && !sha256.Equals(liveSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("STRESS_TEST_BLOCKED: source SHA mismatch.");
        if (!visionPass)
            throw new InvalidOperationException("STRESS_TEST_BLOCKED: Vision chưa PASS.");
    }

    public static void EnsureSourceStillValid(string storedSha, string liveSha, string storedFp, string liveFp)
    {
        if (!string.IsNullOrWhiteSpace(storedSha) && !string.IsNullOrWhiteSpace(liveSha)
            && !storedSha.Equals(liveSha, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("STRESS_TEST_INVALIDATED: source SHA changed.");
        if (!string.IsNullOrWhiteSpace(storedFp) && !string.IsNullOrWhiteSpace(liveFp) && storedFp != liveFp)
            throw new InvalidOperationException("STRESS_TEST_INVALIDATED: source fingerprint changed.");
    }

    public static string CompilePrompt(string caseCode, string candidateCode, string sourceSha, string? diagnosis = null)
    {
        var row = CaseOf(caseCode) ?? throw new InvalidOperationException("STRESS: case không hợp lệ.");
        var repair = KitVideoMasterCreationRules.SanitizeRepair(diagnosis);
        var people = row.Code == "ST-10" ? "two_people minh_plus_one" : "single_character single_subject";
        var lines = new List<string>
        {
            "[STYLE] Famixa stylized cinematic human. Not photoreal. Not cartoon. Not anime.",
            $"[CHARACTER DNA] Minh 11. Same boy as {AllowedCandidate}. Soft slightly oval child face. Hair silhouette Canon.",
            $"[STRESS] {row.Code} {row.Key} {row.Label}. {row.Prompt}",
            "[ERA] ERA-01 age 11.",
            $"[REFERENCE] Source {candidateCode} hash {Trunc(sourceSha)}. Visual DNA APPROVED. Identity Test V1. No Golden. No SH01-01.",
            $"[IMAGE CONTRACT] singleFrame singleScene singleComposition {people} no_panels no_collage no_storyboard no_watermark no_logo no_unrequested_text",
            "[FORBIDDEN] character sheet, collage, storyboard, multi-panel, watermark, logo, photoreal, anime, new face, teen, adult, face merge, duplicate Minh",
        };
        if (!string.IsNullOrWhiteSpace(repair))
            lines.Add($"[REPAIR] Keep 004-D face shape, eye spacing, hair silhouette, age 11. {repair}");
        var prompt = string.Join('\n', lines);
        if (prompt.Contains('{')) throw new InvalidOperationException("STRESS: prompt must not dump JSON.");
        return prompt;
    }

    public static string Fingerprint(string candidateId, string caseCode, string sourceFingerprint, string? diagnosis = null)
    {
        var raw = $"CHAR-001|{candidateId}|V1|{caseCode}|{(sourceFingerprint ?? "").ToLowerInvariant()}|{PromptVersion}|{KitVideoMasterCreationRules.SanitizeRepair(diagnosis)}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    public static IReadOnlyList<string> EvaluateP0(
        bool artifactOk, string? sha, string? liveSha, bool sheet,
        bool faceSame, bool ageOk, bool hairSame, bool proportionSame,
        bool distinctiveKept, bool differentPerson,
        bool missingSubject = false, bool faceDeformed = false, bool duplicateMinh = false, bool faceMerge = false)
    {
        var p0 = new List<string>();
        if (!artifactOk) p0.Add("ARTIFACT_FAILED");
        if (!string.IsNullOrWhiteSpace(sha) && !string.IsNullOrWhiteSpace(liveSha)
            && !sha.Equals(liveSha, StringComparison.OrdinalIgnoreCase))
            p0.Add("P0-01_HASH");
        if (sheet) p0.Add("IMAGE_TYPE_FAIL");
        if (missingSubject) p0.Add("MISSING_SUBJECT");
        if (!faceSame || differentPerson) p0.Add("IDENTITY_MISMATCH");
        if (faceDeformed) p0.Add("FACE_DEFORMATION");
        if (duplicateMinh) p0.Add("DUPLICATE_MINH");
        if (faceMerge) p0.Add("FACE_MERGE");
        if (!ageOk) p0.Add("AGE_DRIFT");
        if (!hairSame) p0.Add("HAIR_IDENTITY_BREAK");
        if (!proportionSame) p0.Add("FACIAL_PROPORTION_BREAK");
        if (!distinctiveKept) p0.Add("DISTINCTIVE_FEATURE_LOSS");
        return p0;
    }

    public static JsonElement VisionContract(string caseCode)
    {
        var two = caseCode.Equals("ST-10", StringComparison.OrdinalIgnoreCase);
        var json = two
            ? """
            {
              "purpose": "IDENTITY_STRESS",
              "testCase": "ST-10",
              "characters": [
                {"id":"CHAR-001","age":11,"era":"ERA-01","role":"primary"},
                {"id":"OTHER","role":"companion"}
              ],
              "expectedCharacters": 2,
              "identityTarget": "First image is the still. IDENTITY_SOURCE is MINH-E01-CANDIDATE-004-D. Identify Minh by face match to that reference. Score CHAR-001 only. Do not crop companion as identity. Do not fail solely because two people are present.",
              "imageContract": "single_scene two_people minh_plus_one",
              "requirements": [
                {"id":"CHAR-001"},
                {"id":"AGE_11"},
                {"id":"HAIR_IDENTITY"},
                {"id":"FACE_DEFORM"},
                {"id":"FACIAL_PROPORTION"},
                {"id":"DISTINCTIVE_FEATURES"}
              ],
              "forbidden": ["CHARACTER_SHEET","COLLAGE","MULTI_PANEL","WATERMARK"]
            }
            """
            : """
            {
              "purpose": "IDENTITY_STRESS",
              "testCase": "SINGLE",
              "characters": [{"id":"CHAR-001","age":11,"era":"ERA-01","role":"primary"}],
              "expectedCharacters": 1,
              "identityTarget": "Score CHAR-001 Minh against attached IDENTITY_SOURCE 004-D. Full frame. No Minh crop unless face is small.",
              "imageContract": "single_character single_scene",
              "requirements": [
                {"id":"CHAR-001"},
                {"id":"AGE_11"},
                {"id":"HAIR_IDENTITY"},
                {"id":"FACE_DEFORM"},
                {"id":"FACIAL_PROPORTION"},
                {"id":"DISTINCTIVE_FEATURES"}
              ],
              "forbidden": ["CHARACTER_SHEET","COLLAGE","MULTI_PANEL","WATERMARK"]
            }
            """;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    public static bool IsCollapsedCascade(IReadOnlyList<string> p0, int identityScore, string? vision)
    {
        if (identityScore != 40 || !string.Equals(vision, "FAIL", StringComparison.OrdinalIgnoreCase) || p0.Count != 6)
            return false;
        string[] exact =
        [
            "IDENTITY_MISMATCH", "FACE_DEFORMATION", "AGE_DRIFT",
            "HAIR_IDENTITY_BREAK", "FACIAL_PROPORTION_BREAK", "DISTINCTIVE_FEATURE_LOSS"
        ];
        return exact.All(p0.Contains);
    }

    public sealed record VisionSignals(
        string Overall,
        int Detected,
        int Expected,
        IReadOnlyList<string> Ids,
        IReadOnlyList<KitVideoVisionRequirementDto> Requirements,
        string ImageType,
        bool Sheet,
        string CaseCode);

    public static (List<string> P0, Dictionary<string, string> Provenance, int IdentityScore) EvaluateFromVision(VisionSignals s)
    {
        var minh = s.Ids.Contains("CHAR-001", StringComparer.OrdinalIgnoreCase)
            || s.Requirements.Any(r => r.Id.Equals("CHAR-001", StringComparison.OrdinalIgnoreCase)
                && r.Status.Equals("PASS", StringComparison.OrdinalIgnoreCase));
        var reqFail = (string key) => s.Requirements.Any(r =>
            (r.Id.Contains(key, StringComparison.OrdinalIgnoreCase) || (r.Reason ?? "").Contains(key, StringComparison.OrdinalIgnoreCase))
            && r.Status.Equals("FAIL", StringComparison.OrdinalIgnoreCase));
        var p0 = new List<string>();
        var why = new Dictionary<string, string>();
        if (s.Sheet)
        {
            p0.Add("IMAGE_TYPE_FAIL");
            why["IMAGE_TYPE_FAIL"] = $"imageType={s.ImageType}";
        }
        if (s.Detected == 0 || !minh)
        {
            p0.Add(s.Detected == 0 ? "MISSING_SUBJECT" : "IDENTITY_MISMATCH");
            why[p0[^1]] = $"minh={minh} detected={s.Detected} ids=[{string.Join(',', s.Ids)}]";
        }
        else
        {
            var charReq = s.Requirements.FirstOrDefault(r => r.Id.Equals("CHAR-001", StringComparison.OrdinalIgnoreCase));
            if (charReq is { Status: "FAIL" })
            {
                p0.Add("IDENTITY_MISMATCH");
                why["IDENTITY_MISMATCH"] = $"CHAR-001 requirement FAIL: {charReq.Reason}";
            }
        }
        if (reqFail("deform") || reqFail("FACE_DEFORM"))
        {
            p0.Add("FACE_DEFORMATION");
            why["FACE_DEFORMATION"] = "requirement FAIL deform";
        }
        if (reqFail("age") || reqFail("teen") || reqFail("adult") || reqFail("AGE_11"))
        {
            p0.Add("AGE_DRIFT");
            why["AGE_DRIFT"] = "requirement FAIL age";
        }
        if (reqFail("hair") || reqFail("HAIR_IDENTITY"))
        {
            p0.Add("HAIR_IDENTITY_BREAK");
            why["HAIR_IDENTITY_BREAK"] = "requirement FAIL hair";
        }
        if (reqFail("proportion") || reqFail("structure") || reqFail("FACIAL_PROPORTION"))
        {
            p0.Add("FACIAL_PROPORTION_BREAK");
            why["FACIAL_PROPORTION_BREAK"] = "requirement FAIL proportion";
        }
        if (reqFail("distinctive") || reqFail("DISTINCTIVE"))
        {
            p0.Add("DISTINCTIVE_FEATURE_LOSS");
            why["DISTINCTIVE_FEATURE_LOSS"] = "requirement FAIL distinctive";
        }
        var st10 = s.CaseCode.Equals("ST-10", StringComparison.OrdinalIgnoreCase);
        if (st10 && s.Ids.Count(i => i.Equals("CHAR-001", StringComparison.OrdinalIgnoreCase)) > 1)
        {
            p0.Add("DUPLICATE_MINH");
            why["DUPLICATE_MINH"] = "CHAR-001 listed more than once";
        }
        if (st10 && s.Detected > 2)
        {
            p0.Add("FACE_MERGE");
            why["FACE_MERGE"] = $"detected={s.Detected} expected=2";
        }
        if (!st10 && s.Detected > 1)
        {
            p0.Add("FACE_MERGE");
            why["FACE_MERGE"] = $"detected={s.Detected} expected=1";
        }
        if (s.Overall.Equals("FAIL", StringComparison.OrdinalIgnoreCase) && p0.Count == 0)
        {
            p0.Add("VISION_OVERALL_FAIL");
            why["VISION_OVERALL_FAIL"] = "Vision overall FAIL without a specific identity feature requirement. Do not cascade into 6 P0.";
        }
        var identity = !minh ? 30
            : s.Overall == "PASS" && p0.Count == 0 ? 92
            : p0.Contains("VISION_OVERALL_FAIL") ? 55
            : s.Overall == "UNCERTAIN" && minh && p0.Count == 0 ? 78
            : minh && p0.Count == 0 ? 84
            : 55;
        return (p0, why, identity);
    }

    public static (string Status, int Have, int P0, bool Complete) Rollup(
        IEnumerable<(string Code, string Sha256, IEnumerable<string> P0, string QaStatus, int IdentityScore)> cases)
    {
        var rows = cases.ToList();
        var have = rows.Count(c => !string.IsNullOrWhiteSpace(c.Sha256) && c.Sha256.Length > 8);
        var p0 = rows.Sum(c => c.P0.Count());
        var passed = rows.Count(c => c.QaStatus == "PASS" && !c.P0.Any() && c.IdentityScore >= ScoreThreshold);
        var complete = have == RequiredCount;
        var status = have == 0 ? "NOT_RUN"
            : !complete ? "RUNNING"
            : p0 > 0 || passed < RequiredCount ? "FAIL"
            : "PASS";
        return (status, have, p0, complete);
    }

    public static void EnsureNotCanonKind(string? kind)
    {
        var t = (kind ?? "").ToUpperInvariant();
        if (t is "MASTER_REFERENCE" or "CANON" or "PRODUCTION_STILL" or "LOCKED_REFERENCE")
            throw new InvalidOperationException("STRESS: artifact không phải Master / Canon / Production Still.");
    }

    public static bool IsDirectorDecision(string? decision) =>
        decision is "PASS" or "REJECT" or "REPAIR" or "PROMOTE_TO_MASTER_REVIEW";

    public static void EnsureDirectorApprove(string? stressStatus)
    {
        if (!string.Equals(stressStatus, "PASS", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("DIRECTOR: không approve khi Stress FAIL / chưa đủ 10 PASS.");
    }

    public static bool CanPromote(string? stressStatus) =>
        string.Equals(stressStatus, "PASS", StringComparison.OrdinalIgnoreCase);

    public static void EnsurePromote(string? identityStatus, string? stressStatus, bool directorPass, bool artifactOk, bool dnaApproved)
    {
        if (!string.Equals(identityStatus, "COMPLETE", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(identityStatus, "PASS", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(identityStatus, "CONDITIONAL", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("MASTER_GATE: Identity Test chưa PASS.");
        if (!CanPromote(stressStatus) || !directorPass)
            throw new InvalidOperationException("MASTER_GATE: Stress PASS + Director PASS. READY_FOR_MASTER_REVIEW only.");
        if (!artifactOk || !dnaApproved)
            throw new InvalidOperationException("MASTER_GATE: artifact + DNA APPROVED.");
    }

    private static string Trunc(string? sha) =>
        string.IsNullOrWhiteSpace(sha) ? "" : sha.Length <= 12 ? sha : sha[..12];
}
