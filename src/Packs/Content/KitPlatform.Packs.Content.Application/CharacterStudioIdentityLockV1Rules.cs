using System.Linq;
using System.Text.Json;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_CHARACTER_STUDIO_IDENTITY_LOCK_V1 — A studio turntable + no authority SHA in
/// CRP prompts, B FRONT-first then Master+FRONT, C Vision vs Master before PASS.
/// Does not auto-approve, auto-lock, generate video, or write official Minh tables.
/// </summary>
public static class CharacterStudioIdentityLockV1Rules
{
    public const string DocumentId = "FAMIXA_CHARACTER_STUDIO_IDENTITY_LOCK_V1";
    public const string SuiteId = "FAMIXA_CHARACTER_STUDIO_IDENTITY_LOCK_V1_REGRESSION";

    public const string VerdictPass = "PASS";
    public const string VerdictFail = "FAIL";
    public const string VerdictPending = "NOT_EVALUATED";

    public const string StudioLock =
        "Turntable character reference. Seamless neutral studio cyclorama. Identical lighting and lens on every view. No furniture, no plants, no courtyard, no living room, no props, no location storytelling.";

    public static readonly string[] GenerationOrder = ["FRONT", "THREE_QUARTER", "SIDE", "FULL_BODY"];

    public static readonly string[] ScoreKeys =
        ["face", "hair", "age", "gender", "skin", "structure", "proportion", "clothing", "style", "crossView"];

    public static string ViewCamera(string view) => (view ?? "").Trim().ToUpperInvariant() switch
    {
        "FRONT" => "neutral front portrait, head and shoulders, 0 degrees, looking at camera, same crop as the turntable grid",
        "THREE_QUARTER" => "neutral three-quarter view, approximately 35 degrees, same head-and-shoulders crop as FRONT",
        "SIDE" => "true 90-degree side profile, ear and nose bridge visible, not looking at camera, not three-quarter, same crop as FRONT",
        "FULL_BODY" => "neutral full-body standing reference, head to toe visible, arms at sides, same person and wardrobe as FRONT",
        _ => "neutral character reference on the studio turntable",
    };

    public static IReadOnlyList<string> ViewRefs(string view) =>
        string.Equals((view ?? "").Trim(), "FRONT", StringComparison.OrdinalIgnoreCase)
            ? ["MASTER"]
            : ["MASTER", "FRONT"];

    public static bool PromptHasAuthoritySha(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return false;
        return prompt.Contains("MasterSha256:", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains("DnaSha256:", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains("PrpSha256:", StringComparison.OrdinalIgnoreCase);
    }

    public static bool PromptHasStudioLock(string? prompt) =>
        !string.IsNullOrWhiteSpace(prompt)
        && prompt.Contains(StudioLock, StringComparison.Ordinal);

    public static bool IsPass(string? verdict) =>
        string.Equals(verdict, VerdictPass, StringComparison.OrdinalIgnoreCase);

    public static bool IsFail(string? verdict) =>
        string.Equals(verdict, VerdictFail, StringComparison.OrdinalIgnoreCase);

    public static bool IsUnevaluated(string? verdict) =>
        string.Equals(verdict, VerdictPending, StringComparison.OrdinalIgnoreCase)
        || string.IsNullOrWhiteSpace(verdict);

    public const string GateVisionNotReady = "IDENTITY_VISION_NOT_READY";
    public const string GateScoreNotReady = "IDENTITY_SCORE_NOT_READY";
    public const string GateScoreFailed = "IDENTITY_SCORE_FAILED";
    public const string StaffScoreExisting = "Chấm với Master";
    public const string StaffVisionPending =
        "Bộ 4 ảnh chưa được chấm với Master. Bấm Chấm với Master — không tạo ảnh mới.";
    public const string StaffVisionFail =
        "Có góc không khớp Master. Tạo lại bộ ảnh hoặc đánh Không đạt.";
    public const string StaffScoreFailed =
        "Không chấm được với Master. Thử lại Chấm với Master — không cần tạo lại ảnh.";
    public const string StaffScoreConfirm =
        "Chấm 4 ảnh hiện tại với Master. Không vẽ ảnh mới. Không duyệt và không khóa tự động.";

    public static bool SlotsReadyForDirector(IEnumerable<CharacterStudioV1Rules.SlotScore> slots) =>
        CharacterStudioV1Rules.RequiredViews.All(t =>
            slots.Any(s =>
                string.Equals(s.Type, t, StringComparison.OrdinalIgnoreCase)
                && IsPass(s.Verdict)));

    public static bool SlotsNeedVision(IEnumerable<CharacterStudioV1Rules.SlotScore> slots) =>
        slots.Any(s => IsUnevaluated(s.Verdict));

    public static bool MayApproveAfterVision(bool officialLocked, IEnumerable<CharacterStudioV1Rules.SlotScore> slots) =>
        officialLocked || SlotsReadyForDirector(slots);

    public static bool MayScoreExisting(
        bool officialLocked, int coverage, IEnumerable<CharacterStudioV1Rules.SlotScore> slots) =>
        !officialLocked && coverage >= 4 && SlotsNeedVision(slots);

    public static bool GeneratesPixelsOnScore() => false;

    public static IReadOnlyDictionary<string, int> DeclaredPassScores(string type) =>
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["face"] = 96,
            ["hair"] = 96,
            ["age"] = 96,
            ["gender"] = 96,
            ["skin"] = 96,
            ["structure"] = 96,
            ["proportion"] = string.Equals(type, "FULL_BODY", StringComparison.OrdinalIgnoreCase) ? 95 : 96,
            ["clothing"] = 96,
            ["style"] = 96,
            ["crossView"] = 96,
        };

    public static CharacterStudioV1Rules.SlotScore UnevaluatedSlot(string type) =>
        new(type, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, VerdictPending);

    public static IReadOnlyDictionary<string, int>? ParseScores(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in ScoreKeys)
            {
                if (root.TryGetProperty(key, out var n) && n.TryGetInt32(out var v))
                    map[key] = Math.Clamp(v, 0, 100);
            }
            return map.Count == 0 ? null : map;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string VisionUserPrompt(string view, int ageMin, int ageMax) =>
        string.Join(" ",
            $"Judge CRP view {view} against the MASTER reference image only.",
            $"Target appearance age {ageMin}–{ageMax}.",
            "Score 0-100 integers for face, hair, age, gender, skin, structure, proportion, clothing, style, crossView.",
            "face = same person as MASTER. age = apparent age inside the target band.",
            "clothing = same wardrobe as MASTER. style = same 3D stylized realism.",
            "crossView = same identity as MASTER, not a redesign.",
            "SIDE must be a true 90-degree profile (FAIL if three-quarter).",
            "FULL_BODY must show head-to-toe of the same person, not a crop of MASTER.",
            "Location storytelling, courtyard, living room, or a second person: lower style and crossView.",
            "Return ONLY JSON with those keys. Do not approve. Do not invent a name.");

    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool CallsGemini() => false;
    public static bool GeneratesVideo() => false;
    public static bool UsesCharacterName() => false;
    public static bool WritesOfficialMinhTables() => false;
}

public interface ICharacterStudioIdentityJudge
{
    Task<IReadOnlyDictionary<string, int>?> ScoreViewAsync(
        string view,
        byte[] candidate,
        byte[] master,
        int ageMin,
        int ageMax,
        CancellationToken cancellationToken);
}

public sealed class MockCharacterStudioIdentityJudge : ICharacterStudioIdentityJudge
{
    public IReadOnlyDictionary<string, int>? Result { get; init; }
    public int Calls { get; private set; }

    public Task<IReadOnlyDictionary<string, int>?> ScoreViewAsync(
        string view, byte[] candidate, byte[] master, int ageMin, int ageMax,
        CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult<IReadOnlyDictionary<string, int>?>(
            Result ?? CharacterStudioIdentityLockV1Rules.DeclaredPassScores(view));
    }
}
