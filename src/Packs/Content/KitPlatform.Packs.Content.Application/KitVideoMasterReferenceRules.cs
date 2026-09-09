using System.Text.Json;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

public static class KitVideoMasterReferenceRules
{
    public const string Version = "KIT-VIDEO-MASTER-REFERENCE-V1";
    public const string MinhDocumentId = "CHAR-001_MINH_MASTER_REFERENCE_SPEC_V1";
    public const string FamixaProject = "FAMIXA";
    public const string MinhAsset = "CHAR-001";
    public const string Era01 = "ERA-01";

    public static void EnsureProjectAsset(string? projectCode, string? assetCode)
    {
        if (!string.Equals((projectCode ?? "").Trim(), FamixaProject, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("MASTER_REF: Phase này chỉ seed Project = FAMIXA.");
        if (!string.Equals((assetCode ?? "").Trim(), MinhAsset, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("MASTER_REF: Phase này chỉ mở CHAR-001. Không tự thêm CHAR-002+.");
    }

    public static bool IsLegacyOrGolden(string? path) =>
        Regex.IsMatch(path ?? "", @"famixa/canon/CHAR-001|take-01|SH01-01|Golden|golden-sh01", RegexOptions.IgnoreCase);

    public static string NextCandidateCode(IReadOnlyList<string> existing, string era = Era01)
    {
        var prefix = era == Era01 ? "MINH-E01-CANDIDATE-" : $"MINH-{era}-CANDIDATE-";
        var max = 0;
        foreach (var id in existing)
        {
            var m = Regex.Match(id ?? "", @"CANDIDATE-(\d+)$", RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var n)) max = Math.Max(max, n);
        }
        return $"{prefix}{(max + 1):000}";
    }

    public static void EnsureNotOverwrite(string status, string version, string nextVersion)
    {
        if (status.Equals("LOCKED", StringComparison.OrdinalIgnoreCase) && version == nextVersion)
            throw new InvalidOperationException("MASTER_LOCKED: V1 không overwrite. Tạo MASTER-REF-V2.");
    }

    public static IReadOnlyList<string> EvaluateQa(KitVideoMasterQaInput obs)
    {
        var p0 = new List<string>();
        if (obs.ArtifactExists == false) p0.Add("ARTIFACT_MISSING");
        if (!string.IsNullOrWhiteSpace(obs.Sha256)
            && !string.IsNullOrWhiteSpace(obs.PersistedSha256)
            && !obs.Sha256.Equals(obs.PersistedSha256, StringComparison.OrdinalIgnoreCase))
            p0.Add("HASH_MISMATCH");
        if (!string.IsNullOrWhiteSpace(obs.ExpectedCharacterId)
            && !string.IsNullOrWhiteSpace(obs.CharacterId)
            && !obs.CharacterId.Equals(obs.ExpectedCharacterId, StringComparison.OrdinalIgnoreCase))
            p0.Add("IDENTITY_FAIL");
        if (obs.IdentityMatch == false) p0.Add("IDENTITY_FAIL");
        if (obs.ExpectedAge is { } exp && obs.Age is { } age && exp != age) p0.Add("AGE_FAIL");
        if (obs.StyleMatch == false || obs.Photoreal == true || obs.Anime == true) p0.Add("STYLE_FAIL");
        if (obs.Corrupt == true) p0.Add("CORRUPT");
        if (obs.Watermark == true) p0.Add("WATERMARK");
        if (IsSheet(obs.ImageType)) p0.Add("SHEET_NOT_PRODUCTION_STILL");
        if (obs.AngleIdentityFail == true) p0.Add("ANGLE_INCONSISTENT");
        if (obs.ExpressionIdentityFail == true) p0.Add("EXPRESSION_INCONSISTENT");
        return p0;
    }

    public static string QaStatus(IReadOnlyList<string> p0)
    {
        if (p0.Contains("ARTIFACT_MISSING") || p0.Contains("HASH_MISMATCH")) return "BLOCK";
        return p0.Count == 0 ? "PASS" : "FAIL";
    }

    public static bool IsSheet(string? imageType) =>
        imageType is "CHARACTER_SHEET" or "COLLAGE" or "MULTI_PANEL" or "REFERENCE_BOARD";

    public static bool CanPromoteToProductionStill(string? imageType) =>
        string.Equals(imageType, "PRODUCTION_STILL", StringComparison.OrdinalIgnoreCase);

    public static bool SameFingerprint(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) && string.Equals(a, b, StringComparison.Ordinal);

    public static string RefuseGenerate(bool controlledTest) =>
        controlledTest
            ? "GEMINI_NOT_CALLED: controlled test must not become Canon."
            : "GEMINI_NOT_CALLED: Master generation is off. Compile only.";

    public static bool IsProviderIndependent(JsonElement rules)
    {
        var raw = rules.ValueKind == JsonValueKind.Undefined ? "{}" : rules.GetRawText();
        return !Regex.IsMatch(raw, @"gemini|runway|elevenlabs|\bfal\b", RegexOptions.IgnoreCase);
    }
}

public sealed record KitVideoMasterQaInput(
    bool? ArtifactExists = null,
    string? Sha256 = null,
    string? PersistedSha256 = null,
    string? CharacterId = null,
    string? ExpectedCharacterId = null,
    int? Age = null,
    int? ExpectedAge = null,
    bool? IdentityMatch = null,
    bool? StyleMatch = null,
    bool? Photoreal = null,
    bool? Anime = null,
    bool? Corrupt = null,
    bool? Watermark = null,
    string? ImageType = null,
    bool? AngleIdentityFail = null,
    bool? ExpressionIdentityFail = null);
