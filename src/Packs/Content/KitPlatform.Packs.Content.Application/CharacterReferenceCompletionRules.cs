using System.Linq;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_CHARACTER_REFERENCE_COMPLETION_V1 — attach an existing FULL_BODY, then VALIDATE.
/// Does not generate, approve, or lock.
/// </summary>
public static class CharacterReferenceCompletionRules
{
    public const string DocumentId = "FAMIXA_CHARACTER_REFERENCE_COMPLETION_V1";
    public const string SuiteId = "FAMIXA_CHARACTER_REFERENCE_COMPLETION_V1_REGRESSION";
    public const string HistoricalStillId = "7ed003d7-789a-41ef-bcdf-a88637ff14d2";
    public const string StaffMissingFullBody = "Thiếu ảnh toàn thân.";
    public const string StaffComplete = "Bộ ảnh chuẩn đã hoàn tất.";
    public const string StaffNext = "Có thể chuyển sang bước tạo hình.";

    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool CreatesPixels(string? action) => CharacterReferencePackRules.CreatesPixels(action);
    public static bool AllowsProvider(string? provider) => false;

    public sealed record Candidate(
        string AssetId,
        string CharacterId,
        string EraId,
        string ReferenceType,
        string Path,
        string? ImageType = null);

    public static bool IsHistoricalStill(string? assetId) =>
        string.Equals(assetId?.Trim(), HistoricalStillId, StringComparison.OrdinalIgnoreCase);

    public static bool AcceptFullBody(string characterId, Candidate asset)
    {
        var id = CharacterReferencePackRules.NormalizeCharacterId(characterId);
        if (IsHistoricalStill(asset.AssetId)
            && CharacterReferencePackRules.NormalizeRefType(asset.ReferenceType) != "FULL_BODY"
            && CharacterReferencePackRules.NormalizeRefType(asset.ImageType) != "FULL_BODY")
            return false;
        if (!CharacterReferencePackRules.SameTenant(asset.CharacterId, id)) return false;
        var type = CharacterReferencePackRules.NormalizeRefType(asset.ReferenceType);
        var image = CharacterReferencePackRules.NormalizeRefType(asset.ImageType);
        if (type != "FULL_BODY" && image != "FULL_BODY") return false;
        if (!CharacterReferencePackRules.PathMatchesView(asset.Path, "FULL_BODY")) return false;
        if (CharacterReferencePackRules.TouchesGolden(asset.Path)) return false;
        return true;
    }

    public static Candidate? SelectFullBody(string characterId, IEnumerable<Candidate> assets)
    {
        var hits = assets
            .Where(a => AcceptFullBody(characterId, a))
            .OrderBy(a => a.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return hits.Count == 0 ? null : hits[0];
    }

    public static bool SameAttachment(string? existingPath, string? existingSha, string incomingPath, string incomingSha)
    {
        if (string.IsNullOrWhiteSpace(existingPath) || string.IsNullOrWhiteSpace(incomingPath)) return false;
        return CharacterReferencePackRules.SameSha(existingSha, incomingSha)
            || string.Equals(Path.GetFullPath(existingPath), Path.GetFullPath(incomingPath), StringComparison.OrdinalIgnoreCase);
    }

    public static string StaffCoverage(int ready, int total, IEnumerable<string>? missing)
    {
        var miss = (missing ?? []).Select(CharacterReferencePackRules.StaffViewLabel).Where(x => x.Length > 0).ToList();
        if (ready < total && miss.Count > 0) return $"Thiếu: {string.Join(", ", miss)}";
        if (ready >= total && total > 0) return StaffComplete;
        return CharacterReferencePackRules.StaffMissingReason("FULL_BODY");
    }
}
