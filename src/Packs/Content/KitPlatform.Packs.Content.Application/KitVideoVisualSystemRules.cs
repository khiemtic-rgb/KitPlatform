using System.Text.Json;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

public static class KitVideoVisualSystemRules
{
    public const string Version = "KIT-VIDEO-VISUAL-SYSTEM-V1";
    public const string SystemCode = "VISUAL_STYLE_SYSTEM";
    public const string DocVersion = "V1";
    public const string FamixaProject = "FAMIXA";
    public const string FamixaDocumentId = "FAMIXA_VISUAL_STYLE_SYSTEM_V1";

    public static void EnsureProject(string? projectCode)
    {
        if (!string.Equals((projectCode ?? "").Trim(), FamixaProject, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("VISUAL_SYSTEM: Phase này chỉ seed Project = FAMIXA.");
    }

    public static void EnsureImmutable(string status, string version, string nextVersion)
    {
        if (status.Equals("locked", StringComparison.OrdinalIgnoreCase) && version == nextVersion)
            throw new InvalidOperationException("VISUAL_SYSTEM_LOCKED: V1 không sửa im lặng. Tạo V2.");
    }

    public static IReadOnlyList<string> PromptOverrideBlocks(string prompt, string? lockedHair, string? lockedLocation)
    {
        var blocked = new List<string>();
        if (!string.IsNullOrWhiteSpace(lockedHair) && Regex.IsMatch(prompt ?? "", @"blonde|nhuộm|đổi tóc|new hairstyle|long hair", RegexOptions.IgnoreCase))
            blocked.Add("PROMPT_OVERRIDE: locked Character Canon (hair)");
        if (!string.IsNullOrWhiteSpace(lockedLocation) && Regex.IsMatch(prompt ?? "", @"beach villa|đổi phòng|new living room|another house", RegexOptions.IgnoreCase))
            blocked.Add("PROMPT_OVERRIDE: locked Location Canon");
        return blocked;
    }

    public static bool IsProductionStill(string? imageType) =>
        string.Equals(imageType, "PRODUCTION_STILL", StringComparison.OrdinalIgnoreCase);

    public static bool EraBelongsToMinh(string? characterId, string? era)
    {
        var id = (characterId ?? "").Trim().ToUpperInvariant();
        var e = (era ?? "").Trim().ToUpperInvariant();
        return id == "CHAR-001" && e is "ERA-01" or "ERA-02" or "ERA-03" or "A11" or "A16" or "A23";
    }

    public static bool IsProviderIndependent(JsonElement rules)
    {
        var raw = rules.ValueKind == JsonValueKind.Undefined ? "{}" : rules.GetRawText();
        return !Regex.IsMatch(raw, @"gemini|runway|elevenlabs|\bfal\b", RegexOptions.IgnoreCase);
    }
}
