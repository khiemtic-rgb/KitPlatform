using System.Text.Json;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

public static class KitVideoMasterSelectionRules
{
    public const string DocumentId = "CHAR-001_MINH_MASTER_REFERENCE_SELECTION_V1";
    public const string RefinementId = "CHAR-001_MASTER_REFERENCE_SELECTION_REFINEMENT_V1";
    public const string MasterKind = "MASTER_REFERENCE";
    public const string SelectableImageType = "PRODUCTION_STILL";
    public const int MaxFrontRunners = 3;
    public const int MaxVariations = 5;

    public static bool Accepts(string? project, string? character, string? era) =>
        string.Equals(project, "FAMIXA", StringComparison.OrdinalIgnoreCase)
        && string.Equals(character, "CHAR-001", StringComparison.OrdinalIgnoreCase)
        && string.Equals(era, "ERA-01", StringComparison.OrdinalIgnoreCase);

    public static string NormalizeImageType(string? imageType)
    {
        var t = (imageType ?? "").Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(t) ? "UNKNOWN" : t;
    }

    public static bool IsBlockedType(string? imageType)
    {
        var t = NormalizeImageType(imageType);
        return t is "CHARACTER_SHEET" or "COLLAGE" or "REFERENCE_BOARD" or "STORYBOARD"
            or "MULTI_PANEL" or "VIDEO_FRAME" or "VIDEO_SCREENSHOT" or "WATERMARKED_IMAGE"
            or "TEXT_HEAVY_IMAGE";
    }

    public static bool IsBlockedSelectLifecycle(string? lifecycle)
    {
        var t = (lifecycle ?? "DRAFT").Trim().ToUpperInvariant();
        return t is "DRAFT" or "REVIEW" or "INELIGIBLE" or "NOT_ELIGIBLE";
    }

    public static IReadOnlyList<string> EvaluateP0(
        bool artifactOk, string? sha, string? liveSha, string? qaSha, string? imageType, int? characterCount,
        bool faceOk, bool golden, bool controlledTest)
    {
        var p0 = new List<string>();
        if (!artifactOk) p0.Add("P0-01_ARTIFACT");
        if (!string.IsNullOrWhiteSpace(sha) && !string.IsNullOrWhiteSpace(liveSha)
            && !sha.Equals(liveSha, StringComparison.OrdinalIgnoreCase))
            p0.Add("P0-01_HASH");
        if (!string.IsNullOrWhiteSpace(sha) && !string.IsNullOrWhiteSpace(qaSha)
            && !sha.Equals(qaSha, StringComparison.OrdinalIgnoreCase))
            p0.Add("P0-01_HASH");
        if (!string.IsNullOrWhiteSpace(liveSha) && !string.IsNullOrWhiteSpace(qaSha)
            && !liveSha.Equals(qaSha, StringComparison.OrdinalIgnoreCase))
            p0.Add("P0-01_HASH");
        if (IsBlockedType(imageType) || NormalizeImageType(imageType) != SelectableImageType)
            p0.Add("P0-02_IMAGE_TYPE");
        if (characterCount is { } n && n != 1) p0.Add("P0-03_CHARACTER_COUNT");
        if (golden) p0.Add("P0_GOLDEN_NOT_MASTER");
        if (!faceOk) p0.Add("P0-04_IDENTITY_MISSING");
        if (controlledTest) p0.Add("CONTROLLED_TEST_NOT_CANON");
        return p0;
    }

    public static int Score(int face, int dna, int age, int hair, int eyes, int distinct, int stability) =>
        (int)Math.Round(face * 0.25 + dna * 0.2 + age * 0.15 + hair * 0.1 + eyes * 0.1 + distinct * 0.1 + stability * 0.1);

    public static string Recommend(bool eligible, int score) =>
        !eligible ? "INELIGIBLE" : score >= 80 ? "RECOMMENDED" : score >= 70 ? "ALTERNATIVE" : "NOT_RECOMMENDED";

    public static string Lifecycle(bool eligible, IReadOnlyList<string> p0) =>
        eligible ? "ELIGIBLE" : p0.Any(x => x.Contains("ARTIFACT") || x.Contains("HASH")) ? "FAILED" : "NOT_ELIGIBLE";

    public static void EnsureCanSelect(SelectionGate gate)
    {
        if (gate.Locked) throw new InvalidOperationException("MASTER_LOCKED: V1 không overwrite.");
        if (IsBlockedSelectLifecycle(gate.Lifecycle))
            throw new InvalidOperationException("SELECTION_GATE: DRAFT / REVIEW / INELIGIBLE / NOT_ELIGIBLE không được SELECT.");
        var life = (gate.Lifecycle ?? "").Trim().ToUpperInvariant();
        if (life is not ("ELIGIBLE" or "FRONT_RUNNER"))
            throw new InvalidOperationException("SELECTION_GATE: candidate chưa ELIGIBLE.");
        if (NormalizeImageType(gate.ImageType) != SelectableImageType)
            throw new InvalidOperationException("SELECTION_GATE: imageType phải là PRODUCTION_STILL.");
        if (!gate.ArtifactExists || !gate.Readable)
            throw new InvalidOperationException("SELECTION_GATE: artifact thiếu hoặc không đọc được.");
        if (!HashesMatch(gate.Sha256, gate.LiveSha256, gate.QaSha256))
            throw new InvalidOperationException("SELECTION_GATE: SHA256 / Vision QA không cùng một artifact.");
        if (!gate.VisionPass)
            throw new InvalidOperationException("SELECTION_GATE: Vision chưa PASS.");
        if (gate.P0.Count > 0 || !gate.Eligible)
            throw new InvalidOperationException("MASTER_NOT_ELIGIBLE: P0 / Vision / artifact. Score không thắng gate.");
        if (!gate.CanonEligible)
            throw new InvalidOperationException("MASTER_NOT_ELIGIBLE: ảnh thử kiểm chứng không thành Canon.");
        if (!gate.DnaApproved)
            throw new InvalidOperationException("DNA_NOT_APPROVED: Director duyệt Visual DNA trước khi chọn Canon.");
    }

    public static void EnsureCanMarkFrontRunner(bool locked, string? lifecycle, string? recommendation, bool canonEligible, bool controlledTest, int frontRunnerCount, bool already)
    {
        if (locked) throw new InvalidOperationException("MASTER_LOCKED");
        var life = (lifecycle ?? "").Trim().ToUpperInvariant();
        if (life is "REJECTED" or "FAILED")
            throw new InvalidOperationException("FRONT_RUNNER: ảnh đã loại không vào chung kết.");
        if (controlledTest || !canonEligible)
            throw new InvalidOperationException("FRONT_RUNNER: ảnh thử kiểm chứng không vào chung kết.");
        if (!already && frontRunnerCount >= MaxFrontRunners)
            throw new InvalidOperationException("FRONT_RUNNER_FULL: tối đa 3 ứng viên dẫn đầu.");
    }

    public static void EnsureCanVary(bool locked, string? parentLifecycle, string? parentRecommendation, int existingVariations)
    {
        if (locked) throw new InvalidOperationException("MASTER_LOCKED");
        var life = (parentLifecycle ?? "").Trim().ToUpperInvariant();
        if (life is "REJECTED" or "FAILED")
            throw new InvalidOperationException("VARIATION: không vẽ từ ứng viên loại.");
        if (existingVariations >= MaxVariations)
            throw new InvalidOperationException("VARIATION_FULL: tối đa 5 (A–E).");
    }

    public static string NextVariationCode(string parentCode, IReadOnlyList<string> existing)
    {
        var have = new HashSet<string>(existing.Select(x => (x ?? "").ToUpperInvariant()));
        foreach (var letter in new[] { "A", "B", "C", "D", "E" })
        {
            var code = $"{parentCode}-{letter}";
            if (!have.Contains(code.ToUpperInvariant())) return code;
        }
        throw new InvalidOperationException("VARIATION_FULL: tối đa 5 (A–E).");
    }

    public static bool ProductionAccepts(string? status, string? path, string? sha, string? liveSha)
    {
        if (KitVideoMasterReferenceRules.IsLegacyOrGolden(path)) return false;
        if (!string.Equals(status, "LOCKED", StringComparison.OrdinalIgnoreCase)) return false;
        return !string.IsNullOrWhiteSpace(sha)
            && sha.Equals(liveSha ?? sha, StringComparison.OrdinalIgnoreCase);
    }

    public static JsonElement SelectionPayload(string lifecycle, int score, string recommendation, bool eligible, IReadOnlyList<string> p0, bool frontRunner = false)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            selection = new
            {
                documentId = DocumentId,
                refinementId = RefinementId,
                lifecycle,
                score,
                recommendation,
                eligible,
                p0,
                frontRunner,
                autoSelected = false,
                visualDnaVersion = "V1",
            }
        }));
        return doc.RootElement.Clone();
    }

    public static bool LooksLikeAuditType(string? type) =>
        Regex.IsMatch(type ?? "", "CANDIDATE_CREATED|CANDIDATE_ANALYZED|CANDIDATE_REJECTED|CANDIDATE_SELECTED|CANDIDATE_FRONT_RUNNER|CANDIDATE_VARIATION|MASTER_CREATED|MASTER_APPROVED|MASTER_LOCKED");

    private static bool HashesMatch(string? sha, string? live, string? qa)
    {
        if (string.IsNullOrWhiteSpace(sha) || string.IsNullOrWhiteSpace(live) || string.IsNullOrWhiteSpace(qa)) return false;
        return sha.Equals(live, StringComparison.OrdinalIgnoreCase)
            && sha.Equals(qa, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record SelectionGate(
    bool ArtifactExists,
    bool Readable,
    string? Sha256,
    string? LiveSha256,
    string? QaSha256,
    bool VisionPass,
    string? ImageType,
    IReadOnlyList<string> P0,
    bool Eligible,
    bool DnaApproved,
    string? Lifecycle,
    bool Locked,
    bool CanonEligible);
