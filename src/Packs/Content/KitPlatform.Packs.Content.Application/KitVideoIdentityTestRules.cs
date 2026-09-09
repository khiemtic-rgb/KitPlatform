using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

public static class KitVideoIdentityTestRules
{
    public const string DocumentId = "CHAR-001_MINH_IDENTITY_TEST_SPEC_V1";
    public const string Kind = "IDENTITY_TEST";
    public static readonly string[] ViewVariants = ["FRONT", "THREE_QUARTER_LEFT", "THREE_QUARTER_RIGHT", "PROFILE"];
    public static readonly string[] EmotionVariants = ["NEUTRAL", "SAD", "LIGHT_SMILE"];
    public static readonly string[] RequiredVariants = [.. ViewVariants, .. EmotionVariants];

    public static bool Accepts(string? project, string? character, string? era) =>
        string.Equals(project, "FAMIXA", StringComparison.OrdinalIgnoreCase)
        && string.Equals(character, "CHAR-001", StringComparison.OrdinalIgnoreCase)
        && string.Equals(era, "ERA-01", StringComparison.OrdinalIgnoreCase);

    public static bool DnaAllows(string? status) =>
        string.Equals(status, "APPROVED", StringComparison.OrdinalIgnoreCase);

    public static string TestTypeOf(string variant) =>
        EmotionVariants.Contains(variant.ToUpperInvariant()) ? "EMOTION" : "VIEW";

    public static void EnsureCanOpen(string? dnaStatus, string? candidateCode, bool frontRunner, bool designated)
    {
        if (!DnaAllows(dnaStatus))
            throw new InvalidOperationException("IDENTITY_TEST_BLOCKED: Visual DNA phải APPROVED.");
        var code = (candidateCode ?? "").ToUpperInvariant();
        if (Regex.IsMatch(code, @"CANDIDATE-001$") && !code.Contains("CANDIDATE-001-", StringComparison.Ordinal))
            throw new InvalidOperationException("IDENTITY_TEST: Candidate-001 không vào vòng test này.");
        if (!frontRunner && !designated)
            throw new InvalidOperationException("IDENTITY_TEST: chỉ FRONT_RUNNER hoặc Director chỉ định.");
    }

    public static string CompilePrompt(string variant, string candidateCode, string sourceSha)
    {
        var v = variant.Trim().ToUpperInvariant();
        var type = TestTypeOf(v);
        var view = type == "VIEW"
            ? $"[VIEW] {v}. Change viewpoint only. Same boy. Same age 11. Same hair. Same face."
            : "[VIEW] FRONT. Keep the same boy. Change expression only.";
        var emotion = type == "EMOTION"
            ? $"[EXPRESSION] {v}. Change emotion only. Identity must not change."
            : "[EXPRESSION] NEUTRAL. Subtle. Identity must not change.";
        var prompt = string.Join('\n',
            "[STYLE] Famixa stylized cinematic human. Not photoreal. Not cartoon. Not anime.",
            $"[CHARACTER DNA] Minh 11. Same boy as {candidateCode}. Soft slightly oval child face. Hair silhouette Canon.",
            "[BRIEF] Identity Test. Prove it is the same Minh. Not a new character.",
            "[ERA] ERA-01 age 11.",
            $"[REFERENCE] Source candidate {candidateCode}. Visual DNA APPROVED. No Golden. No SH01-01.",
            view,
            emotion,
            "[WARDROBE] HOME baseline T-shirt. Wardrobe is not identity.",
            "[IMAGE CONTRACT] single_character single_image single_subject single_composition no_panels no_collage no_storyboard no_watermark no_logo no_unrequested_text",
            "[FORBIDDEN] character sheet, collage, storyboard, multi-panel, watermark, logo, photoreal, anime, adult body, new face");
        if (prompt.Contains('{')) throw new InvalidOperationException("IDENTITY_TEST: prompt must not dump JSON.");
        return prompt;
    }

    public static string Fingerprint(string candidateId, string testType, string testVariant, string sourceSha, string dnaVersion = "V1")
    {
        var raw = $"{candidateId}|{testType}|{testVariant}|{dnaVersion}|{(sourceSha ?? "").ToLowerInvariant()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    public static IReadOnlyList<string> EvaluateP0(
        bool artifactOk, string? sha, string? liveSha, bool sheet, int? count, bool minhPresent,
        bool faceOk, bool identitySame, bool ageOk, string? requested, string? viewpoint)
    {
        var p0 = new List<string>();
        if (!artifactOk) p0.Add("ARTIFACT_FAILED");
        if (!string.IsNullOrWhiteSpace(sha) && !string.IsNullOrWhiteSpace(liveSha)
            && !sha.Equals(liveSha, StringComparison.OrdinalIgnoreCase))
            p0.Add("P0-01_HASH");
        if (sheet) p0.Add("IMAGE_TYPE_FAIL");
        if (count is { } n && n != 1) p0.Add("P0-03_CHARACTER_COUNT");
        if (!minhPresent) p0.Add("IDENTITY_MISSING");
        if (!faceOk) p0.Add("P0-04_IDENTITY_OBSTRUCTION");
        if (!identitySame) p0.Add("IDENTITY_DRIFT");
        if (!ageOk) p0.Add("AGE_DRIFT");
        var req = (requested ?? "").ToUpperInvariant();
        if (TestTypeOf(req) == "VIEW" && !string.IsNullOrWhiteSpace(viewpoint)
            && !viewpoint.Equals(req, StringComparison.OrdinalIgnoreCase))
            p0.Add("VIEWPOINT_FAILURE");
        return p0;
    }

    public static bool Completeness(IEnumerable<string> variantsWithVision) =>
        RequiredVariants.All(v => variantsWithVision.Contains(v, StringComparer.OrdinalIgnoreCase));

    public static void EnsureNotCanonKind(string? kind)
    {
        var t = (kind ?? "").ToUpperInvariant();
        if (t is "MASTER_REFERENCE" or "CANON" or "PRODUCTION_STILL" or "LOCKED_REFERENCE")
            throw new InvalidOperationException("IDENTITY_TEST: test artifact không phải Master / Canon / Production Still.");
    }

    public static bool IsDirectorDecision(string? decision) =>
        decision is "PASS" or "CONDITIONAL" or "FAIL" or "REJECT" or "PROMOTE_TO_MASTER_REVIEW";
}
