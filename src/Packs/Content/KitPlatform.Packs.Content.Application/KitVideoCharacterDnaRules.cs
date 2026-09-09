using System.Linq;
using System.Text.Json;

namespace KitPlatform.Packs.Content;

public static class KitVideoCharacterDnaRules
{
    public const string DocumentId = "CHAR-001_MINH_CHARACTER_DNA_V1";
    public const string DnaCode = "CHAR-001-MINH-ERA01-DNA-V1";
    public const string Version = "V1";
    public const string MasterCode = "CHAR-001-MINH-ERA01-MASTER-V1";
    public const string CharacterId = "CHAR-001";
    public const string EraId = "ERA-01";
    public const string CharacterName = "MINH";

    public static readonly string[] AllowedVariation =
    [
        "clothing", "background", "environment", "lighting", "time_of_day",
        "camera_distance", "camera_angle", "pose", "gaze_direction", "expression",
        "body_position", "scene_context",
    ];

    public static readonly string[] ForbiddenVariation =
    [
        "different face identity", "different eye structure", "different facial proportions",
        "different age", "different hair identity", "different recognizable facial features",
        "generic AI face", "model-like face", "adult appearance", "younger-child appearance",
        "anime transformation", "photorealistic transformation", "character-sheet transformation",
        "identity blending", "identity averaging", "face swap", "identity merge",
    ];

    public static readonly string[] CriticalInvariants =
    [
        "age appearance", "face identity", "overall facial structure", "eye identity", "major facial proportions",
    ];

    public static readonly string[] HighInvariants =
    [
        "hairstyle identity", "eyebrow character", "nose character", "mouth character", "ear character",
    ];

    public static readonly string[] VariableInvariants =
    [
        "expression", "gaze", "pose", "clothing", "lighting", "background", "scene",
    ];

    public static bool Accepts(string? characterId, string? eraId) =>
        string.Equals(characterId, CharacterId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(eraId, EraId, StringComparison.OrdinalIgnoreCase);

    public static bool IsLockedMaster(string? status, string? masterCode) =>
        string.Equals(masterCode, MasterCode, StringComparison.OrdinalIgnoreCase)
        && string.Equals(status, KitVideoMasterLockRules.LockedStatus, StringComparison.OrdinalIgnoreCase);

    public static bool SameSha(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    public static void EnsureDirector(string? actor)
    {
        var a = (actor ?? "").Trim();
        if (a.Length == 0 || a.Equals("anonymous", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("DNA_GATE_NOT_SATISFIED: chỉ Director được duyệt Character DNA.");
    }

    public static readonly string[] ReviewGroups = ["face", "eyes", "hair", "age", "expression", "body", "style"];
    public static readonly string[] ExtraSections =
        ["variationBoundaries", "continuityRules", "stressRules", "regressionRules", "lockMetadata"];

    public static void EnsureCanCreate(bool masterExists, bool masterLocked, string? masterCode, bool shaValid, bool lockedDna = false)
    {
        if (lockedDna)
            throw new InvalidOperationException("DNA_LOCKED: V1 đã khóa. Không tạo DNA-V2 từ nút V1.");
        if (!masterExists)
            throw new InvalidOperationException("DNA_GATE_NOT_SATISFIED: master exists thất bại.");
        if (!masterLocked || !IsLockedMaster(KitVideoMasterLockRules.LockedStatus, masterCode))
            throw new InvalidOperationException("DNA_GATE_NOT_SATISFIED: Master chưa LOCKED. Không tạo APPROVED DNA.");
        if (!shaValid)
            throw new InvalidOperationException("DNA_INVALID: Master SHA không khớp. DNA không tự cập nhật.");
    }

    public static bool ResultPass(string? result) =>
        !string.IsNullOrWhiteSpace(result)
        && result.Contains("PASS", StringComparison.OrdinalIgnoreCase)
        && !result.Contains("FAIL", StringComparison.OrdinalIgnoreCase);

    public static void EnsureProductionGate(bool identityPass, bool stressPass, int p0)
    {
        if (!identityPass)
            throw new InvalidOperationException("DNA_GATE_NOT_SATISFIED: Identity Test phải PASS.");
        if (!stressPass)
            throw new InvalidOperationException("DNA_GATE_NOT_SATISFIED: Stress Test phải PASS.");
        if (p0 > 0)
            throw new InvalidOperationException("DNA_GATE_NOT_SATISFIED: P0 phải = 0.");
    }

    public sealed record CheckItem(string Code, string Label, bool Pass, string? Reason);

    public static IReadOnlyList<CheckItem> EvaluateSpecChecks(JsonElement spec)
    {
        var items = new List<CheckItem>
        {
            Check("face_invariant", "Face invariant", GroupHas(spec, "face", "invariant"), "thiếu Face invariant"),
            Check("eyes_invariant", "Eyes invariant", GroupHas(spec, "eyes", "invariant"), "thiếu Eyes invariant"),
            Check("hair_invariant", "Hair invariant", GroupHas(spec, "hair", "invariant"), "thiếu Hair invariant"),
            Check("age_invariant", "Age invariant", GroupHas(spec, "age", "invariant"), "thiếu Age invariant"),
            Check("expression_rules", "Expression rules", GroupHas(spec, "expression", "invariant") || GroupHas(spec, "expression", "tested"), "thiếu Expression rules"),
            Check("proportion_invariant", "Proportion invariant", GroupHas(spec, "body", "invariant"), "thiếu Proportion invariant"),
            Check("style_invariant", "Style invariant", GroupHas(spec, "style", "invariant"), "thiếu Style invariant"),
            Check("allowed_variation", "Allowed variation", HasList(spec, "allowedVariation", 1), "thiếu Allowed variation"),
            Check("forbidden_variation", "Forbidden variation", HasList(spec, "forbiddenVariation", 1), "thiếu Forbidden variation"),
            Check("continuity_rules", "Continuity rules", HasObject(spec, "continuityRules"), "thiếu Continuity rules"),
            Check("stress_rules", "Stress rules", HasObject(spec, "stressRules"), "thiếu Stress rules"),
            Check("regression_rules", "Regression rules", HasObject(spec, "regressionRules"), "thiếu Regression rules"),
        };
        return items;
    }

    public static IReadOnlyList<CheckItem> EvaluateDirectorGate(
        JsonElement spec, bool masterExists, bool masterLocked, bool shaValid,
        bool identityPass, bool stressPass, int p0)
    {
        var items = new List<CheckItem>
        {
            Check("master_locked", "Master LOCKED", masterExists && masterLocked, "Master chưa LOCKED"),
            Check("master_sha", "Master SHA khớp", shaValid, "Master SHA không khớp"),
            Check("identity_pass", "Identity Test PASS", identityPass, "Identity Test chưa PASS"),
            Check("stress_pass", "Stress Test PASS", stressPass, "Stress Test chưa PASS"),
            Check("p0_zero", "P0 = 0", p0 == 0, "P0 phải = 0"),
        };
        var specChecks = EvaluateSpecChecks(spec);
        items.AddRange(specChecks);
        var validationOk = specChecks.All(x => x.Pass);
        try { EnsureComplete(spec); }
        catch (InvalidOperationException) { validationOk = false; }
        items.Add(Check("dna_validation", "DNA validation", validationOk, "DNA validation FAIL"));
        return items;
    }

    public static bool GatePass(IReadOnlyList<CheckItem> items) => items.All(x => x.Pass);

    public static void EnsureCanApprove(
        string? status, JsonElement spec, bool masterExists, bool masterLocked, bool shaValid,
        bool identityPass = true, bool stressPass = true, int p0 = 0)
    {
        if (string.Equals(status, "LOCKED", StringComparison.OrdinalIgnoreCase))
            return;
        var gate = EvaluateDirectorGate(spec, masterExists, masterLocked, shaValid, identityPass, stressPass, p0);
        var fail = gate.FirstOrDefault(x => !x.Pass);
        if (fail is null) return;
        var prefix = fail.Code == "master_sha" ? "DNA_INVALID" : "DNA_GATE_NOT_SATISFIED";
        throw new InvalidOperationException($"{prefix}: {fail.Reason}");
    }

    public static void EnsureComplete(JsonElement spec)
    {
        if (spec.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("DNA_GATE_NOT_SATISFIED: DNA complete thất bại.");
        foreach (var key in new[]
        {
            "identityCore", "face", "eyes", "eyebrows", "nose", "mouth", "ears",
            "hair", "skin", "age", "body", "expression", "style",
        })
        {
            if (!spec.TryGetProperty(key, out var node) || node.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException($"DNA_GATE_NOT_SATISFIED: DNA complete thất bại ({key}).");
        }
        foreach (var key in ReviewGroups)
        {
            if (!spec.TryGetProperty(key, out var node) || !HasList(node, "invariant", 1))
                throw new InvalidOperationException("DNA_GATE_NOT_SATISFIED: identity invariants present thất bại.");
            if (!HasList(node, "allowed", 1))
                throw new InvalidOperationException("DNA_GATE_NOT_SATISFIED: allowed variation present thất bại.");
            if (!HasList(node, "forbidden", 1))
                throw new InvalidOperationException("DNA_GATE_NOT_SATISFIED: forbidden variation present thất bại.");
        }
        foreach (var key in ExtraSections)
        {
            if (!spec.TryGetProperty(key, out var node) || node.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException($"DNA_GATE_NOT_SATISFIED: DNA complete thất bại ({key}).");
        }
        if (!HasList(spec, "allowedVariation", AllowedVariation.Length))
            throw new InvalidOperationException("DNA_GATE_NOT_SATISFIED: allowed variation present thất bại.");
        if (!HasList(spec, "forbiddenVariation", ForbiddenVariation.Length))
            throw new InvalidOperationException("DNA_GATE_NOT_SATISFIED: forbidden variation present thất bại.");
        if (!spec.TryGetProperty("identityInvariants", out var inv) || inv.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("DNA_GATE_NOT_SATISFIED: identity invariants present thất bại.");
        if (!HasList(inv, "CRITICAL", CriticalInvariants.Length)
            || !HasList(inv, "HIGH", HighInvariants.Length)
            || !HasList(inv, "VARIABLE", VariableInvariants.Length))
            throw new InvalidOperationException("DNA_GATE_NOT_SATISFIED: identity invariants present thất bại.");
    }

    public static void EnsureImmutable(string? action, string? status)
    {
        if (!string.Equals(status, "LOCKED", StringComparison.OrdinalIgnoreCase))
            return;
        var a = (action ?? "").Trim().ToUpperInvariant();
        if (a is "UPDATE" or "DELETE" or "EDIT" or "OVERWRITE" or "CHANGE_MASTER" or "CHANGE_SHA256" or "REGENERATE")
            throw new InvalidOperationException("DNA_LOCKED: V1 không overwrite. Dùng CHAR-001-MINH-ERA01-DNA-V2.");
    }

    public static bool AutoCreateApproved(bool masterLocked) => false;

    public static bool CreatesPixels(string? action) =>
        action is "GENERATE" or "REGENERATE";

    public static JsonElement BuildV1Spec(string masterSha256)
    {
        const string note = "Master V1 không cung cấp số đo pixel. Mô tả cấu trúc nhận diện.";
        object T(string value, string importance, string invariance) =>
            new { value, importance, invariance, notes = note };

        var spec = new Dictionary<string, object?>
        {
            ["derived_from_master"] = MasterCode,
            ["master_sha256"] = masterSha256,
            ["dna_version"] = Version,
            ["identityCore"] = new Dictionary<string, object?>
            {
                ["character"] = CharacterName,
                ["characterId"] = CharacterId,
                ["era"] = EraId,
                ["ageAppearance"] = "approximately 11 years old",
                ["visualIdentity"] = "Vietnamese boy",
                ["style"] = "Famixa illustration style",
                ["source"] = MasterCode,
            },
            ["face"] = new Dictionary<string, object?>
            {
                ["invariant"] = new[] { "face identity", "overall facial structure", "major facial proportions" },
                ["allowed"] = new[] { "camera angle", "lighting", "expression" },
                ["forbidden"] = new[] { "different face identity", "generic AI face", "face swap", "identity merge" },
                ["face_shape"] = T("soft slightly oval child face — not fully round, not teen-angular", "CRITICAL", "HIGH"),
                ["face_width"] = T("child width; wide-ish forehead relative to chin", "HIGH", "HIGH"),
                ["face_height"] = T("child vertical balance; small chin, natural 11yo cheeks", "HIGH", "HIGH"),
                ["forehead"] = T("relatively wide child forehead", "HIGH", "HIGH"),
                ["cheek_structure"] = T("natural 11-year-old fullness — not baby-face, not hollow teen", "HIGH", "HIGH"),
                ["jaw_structure"] = T("soft child jaw, not angular", "HIGH", "HIGH"),
                ["chin_structure"] = T("small soft chin", "HIGH", "HIGH"),
                ["facial_balance"] = T("intelligent, sensitive, inward — recognizable Minh across angles", "CRITICAL", "HIGH"),
            },
            ["eyes"] = new Dictionary<string, object?>
            {
                ["invariant"] = new[] { "eye identity", "eye structure" },
                ["allowed"] = new[] { "gaze direction", "expression", "camera angle", "lighting" },
                ["forbidden"] = new[] { "different eye structure", "anime transformation" },
                ["eye_shape"] = T("relatively large natural eyes, not anime, not doll-glass", "CRITICAL", "HIGH"),
                ["eye_size"] = T("larger than adult scale, still human child", "CRITICAL", "HIGH"),
                ["eye_spacing"] = T("balanced spacing", "CRITICAL", "HIGH"),
                ["eye_position"] = T("natural child placement; acting lives in the eyes", "CRITICAL", "HIGH"),
                ["iris_character"] = T("stylized-moderate iris; not glassy AI doll", "CRITICAL", "HIGH"),
                ["eyelid_character"] = T("natural lids", "CRITICAL", "HIGH"),
                ["gaze_character"] = T("clear gaze; direction may change, structure may not", "CRITICAL", "HIGH"),
            },
            ["eyebrows"] = new Dictionary<string, object?>
            {
                ["shape"] = T("natural, slightly soft, not thick, not sharp", "HIGH", "HIGH"),
                ["thickness"] = T("soft child thickness", "HIGH", "HIGH"),
                ["spacing"] = T("natural spacing above the eyes", "HIGH", "HIGH"),
                ["position"] = T("stable placement; emotion may lift/knit slightly", "HIGH", "HIGH"),
                ["natural_character"] = T("confused → worried → frustrated → hurt without cartoon brows", "HIGH", "HIGH"),
            },
            ["nose"] = new Dictionary<string, object?>
            {
                ["nose_shape"] = T("small soft child nose", "HIGH", "HIGH"),
                ["nose_length"] = T("child length — not adult nose", "HIGH", "HIGH"),
                ["nose_width"] = T("natural child width", "HIGH", "HIGH"),
                ["bridge_character"] = T("soft bridge, not sharp", "HIGH", "HIGH"),
                ["tip_character"] = T("soft tip; stable front / 3/4 / side", "HIGH", "HIGH"),
            },
            ["mouth"] = new Dictionary<string, object?>
            {
                ["mouth_shape"] = T("small-to-medium natural mouth", "HIGH", "HIGH"),
                ["lip_proportion"] = T("natural lips — not overly thick or thin", "HIGH", "HIGH"),
                ["mouth_width"] = T("child mouth width", "HIGH", "HIGH"),
                ["upper_lower_lip_relation"] = T("quiet default; no ad-smile rest", "HIGH", "HIGH"),
                ["resting_expression"] = T("IDENTITY: quiet / reserved. EXPRESSION: smile, sad, surprise, speaking are not new identities", "HIGH", "HIGH"),
            },
            ["ears"] = new Dictionary<string, object?>
            {
                ["ear_shape"] = T("natural child ears", "MEDIUM", "HIGH"),
                ["ear_size"] = T("child scale", "MEDIUM", "HIGH"),
                ["ear_position"] = T("natural placement", "MEDIUM", "HIGH"),
                ["ear_visibility"] = T("VARIABLE by camera, hair, occlusion — structure is not", "MEDIUM", "VARIABLE"),
            },
            ["hair"] = new Dictionary<string, object?>
            {
                ["invariant"] = new[] { "hairstyle identity", "hairline character", "hair silhouette" },
                ["allowed"] = new[] { "minor strand variation", "wind", "movement", "lighting", "slight styling" },
                ["forbidden"] = new[] { "different hair identity" },
                ["hair_color"] = T("black or dark-brown", "HIGH", "HIGH"),
                ["hair_length"] = T("modern child length; silhouette locked", "HIGH", "HIGH"),
                ["hair_style"] = T("modern child cut, neat but not perfect", "HIGH", "HIGH"),
                ["hair_direction"] = T("slight asymmetry allowed; identity silhouette stays", "HIGH", "HIGH"),
                ["hair_volume"] = T("natural volume", "HIGH", "HIGH"),
                ["hairline_character"] = T("hairline character is identity — not a restyle", "HIGH", "HIGH"),
            },
            ["skin"] = new Dictionary<string, object?>
            {
                ["skin_tone"] = T("natural soft stylized complexion", "HIGH", "HIGH"),
                ["skin_character"] = T("stylized skin — no photoreal pores, no plastic gloss", "HIGH", "HIGH"),
                ["facial_softness"] = T("child softness", "HIGH", "HIGH"),
                ["overall_complexion"] = T("lighting/exposure may shift; complexion identity may not", "HIGH", "HIGH"),
            },
            ["age"] = new Dictionary<string, object?>
            {
                ["invariant"] = new[] { "age appearance approximately 11 years old" },
                ["allowed"] = new[] { "camera angle", "pose", "expression", "lighting", "clothing" },
                ["forbidden"] = new[] { "different age", "adult appearance", "younger-child appearance" },
                ["age_appearance"] = T("approximately 11 years old", "CRITICAL", "HIGH"),
                ["not_younger_child"] = T("must not become a younger child", "CRITICAL", "HIGH"),
                ["not_teenager"] = T("must not become a teenager", "CRITICAL", "HIGH"),
                ["not_adult"] = T("must not become an adult", "CRITICAL", "HIGH"),
            },
            ["body"] = new Dictionary<string, object?>
            {
                ["invariant"] = new[] { "11-year-old proportion", "head-to-body relationship", "overall silhouette" },
                ["allowed"] = new[] { "pose", "body_position", "clothing", "camera_distance" },
                ["forbidden"] = new[] { "adult appearance", "younger-child appearance" },
                ["body_age_character"] = T("clear 11-year-old body character", "CRITICAL", "HIGH"),
                ["body_proportion"] = T("child proportion — larger head than adult scale, not chibi", "HIGH", "HIGH"),
                ["shoulder_proportion"] = T("small child shoulders", "HIGH", "HIGH"),
                ["head_to_body_relationship"] = T("child head-to-body; never adult body + child face", "CRITICAL", "HIGH"),
                ["overall_silhouette"] = T("same boy at 11 — readable without wardrobe", "HIGH", "HIGH"),
            },
            ["expression"] = new Dictionary<string, object?>
            {
                ["invariant"] = new[] { "same identity across NEUTRAL / SAD / LIGHT SMILE" },
                ["allowed"] = new[] { "NEUTRAL", "SAD", "LIGHT_SMILE", "happy", "surprised", "curious", "concerned", "laughing", "speaking" },
                ["forbidden"] = new[] { "treat expression as a new identity" },
                ["tested"] = new[] { "NEUTRAL", "SAD", "LIGHT_SMILE" },
                ["schema"] = new[] { "happy", "surprised", "curious", "concerned", "laughing", "speaking" },
                ["rule"] = "Expression may change. Identity must not. No new test images in DNA V1.",
            },
            ["style"] = new Dictionary<string, object?>
            {
                ["invariant"] = new[] { "FAMIXA_ILLUSTRATION" },
                ["allowed"] = new[] { "lighting motivated by scene" },
                ["forbidden"] = new[] { "photorealistic transformation", "anime transformation", "character-sheet transformation" },
                ["visual_style"] = "FAMIXA_ILLUSTRATION",
                ["not_photorealistic"] = true,
                ["not_anime"] = true,
                ["not_character_sheet"] = true,
                ["not_collage"] = true,
                ["no_text"] = true,
            },
            ["allowedVariation"] = AllowedVariation,
            ["forbiddenVariation"] = ForbiddenVariation,
            ["identityInvariants"] = new Dictionary<string, object?>
            {
                ["CRITICAL"] = CriticalInvariants,
                ["HIGH"] = HighInvariants,
                ["MEDIUM"] = Array.Empty<string>(),
                ["VARIABLE"] = VariableInvariants,
            },
            ["variationBoundaries"] = new Dictionary<string, object?>
            {
                ["mayChange"] = AllowedVariation,
                ["mustNotBreak"] = CriticalInvariants,
            },
            ["continuityRules"] = new Dictionary<string, object?>
            {
                ["sameIdentityAcrossShots"] = true,
                ["wardrobeIsNotIdentity"] = true,
                ["masterIsImmutable"] = true,
                ["noGoldenFaceLock"] = true,
            },
            ["stressRules"] = new Dictionary<string, object?>
            {
                ["identity7of7"] = true,
                ["stress10of10"] = true,
                ["st10MustPass"] = true,
                ["p0MustBeZero"] = true,
            },
            ["regressionRules"] = new Dictionary<string, object?>
            {
                ["noGenerate"] = true,
                ["noGemini"] = true,
                ["noRunway"] = true,
                ["goldenUntouched"] = true,
                ["masterImmutable"] = true,
                ["noAutoApprove"] = true,
                ["noAutoLock"] = true,
            },
            ["lockMetadata"] = new Dictionary<string, object?>
            {
                ["derived_from_master"] = MasterCode,
                ["master_sha256"] = masterSha256,
                ["dna_version"] = Version,
                ["immutableAfterLock"] = true,
            },
        };
        return JsonSerializer.SerializeToElement(spec);
    }

    private static CheckItem Check(string code, string label, bool pass, string reason) =>
        new(code, label, pass, pass ? null : reason);

    private static bool GroupHas(JsonElement spec, string group, string field)
    {
        if (spec.ValueKind != JsonValueKind.Object) return false;
        if (!spec.TryGetProperty(group, out var node) || node.ValueKind != JsonValueKind.Object) return false;
        return HasList(node, field, 1);
    }

    private static bool HasObject(JsonElement spec, string name) =>
        spec.ValueKind == JsonValueKind.Object
        && spec.TryGetProperty(name, out var node)
        && node.ValueKind == JsonValueKind.Object
        && node.EnumerateObject().Any();

    private static bool HasList(JsonElement obj, string name, int min)
    {
        if (obj.ValueKind != JsonValueKind.Object) return false;
        if (!obj.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return false;
        return arr.GetArrayLength() >= min;
    }
}
