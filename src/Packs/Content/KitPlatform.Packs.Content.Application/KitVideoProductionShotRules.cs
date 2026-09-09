using System.Linq;
using System.Text.Json;

namespace KitPlatform.Packs.Content;

public static class KitVideoProductionShotRules
{
    public const string DocumentId = "CHAR-001_MINH_PRODUCTION_SHOT_SPEC_V1";
    public const string ShotPrefix = "CHAR-001-MINH-ERA01-SHOT";
    public const string Version = "V1";
    public const string CharacterId = "CHAR-001";
    public const string EraId = "ERA-01";
    public const string CharacterName = "MINH";

    public static readonly string[] RequiredSections =
    [
        "source", "scene", "camera", "framing", "pose", "expression", "lighting", "wardrobe",
        "environment", "interaction", "continuity", "identityRisk", "forbiddenConditions",
        "identity", "identityCheck", "directorDecision",
    ];

    public static readonly string[] IdentityKeys =
    [
        "identity", "faceReference", "eyesReference", "hairReference", "ageProportion",
        "expressionBaseline", "style", "masterSha256", "dnaSha256", "prpSha256", "character_id",
    ];

    public static readonly string[] AllowedVariation =
    [
        "camera angle", "camera distance", "framing", "body pose", "body orientation", "gaze",
        "expression intensity", "lighting", "environment", "props", "wardrobe within PRP",
        "interaction", "composition",
    ];

    public static readonly string[] ForbiddenConditions =
    [
        "generic AI child", "different child identity", "face redesign", "face replacement",
        "face blending", "identity averaging", "age change", "hair redesign", "anime",
        "photorealistic conversion", "character-sheet presentation", "collage",
        "text embedded in image", "unapproved character reference", "unapproved identity source",
        "identity drift", "face drift", "age drift", "hair identity drift", "proportion drift", "style drift",
    ];

    public static bool Accepts(string? characterId, string? eraId) =>
        string.Equals(characterId, CharacterId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(eraId, EraId, StringComparison.OrdinalIgnoreCase);

    public static bool SameSha(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    public static bool ShaExists(string? sha) =>
        !string.IsNullOrWhiteSpace(sha) && sha.Trim().Length >= 32;

    public static string ShotCode(int seq) => $"{ShotPrefix}-{seq:000}";

    public static void EnsureDirector(string? actor)
    {
        var a = (actor ?? "").Trim();
        if (a.Length == 0 || a.Equals("anonymous", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SHOT_GATE_NOT_SATISFIED: chỉ Director được duyệt Production Shot.");
    }

    public static bool CreatesPixels(string? action) =>
        action is "GENERATE" or "REGENERATE" or "GEMINI" or "RUNWAY";

    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;

    public sealed record CheckItem(string Code, string Label, bool Pass, string? Reason);

    public static IReadOnlyList<CheckItem> EvaluateSourceGate(
        bool masterLocked, bool dnaLocked, bool prpLocked,
        bool masterShaValid, bool dnaShaValid, bool prpShaValid,
        bool identityPass, bool stressPass)
    {
        return
        [
            Check("master_locked", "Master LOCKED", masterLocked, "Master chưa LOCKED"),
            Check("dna_locked", "DNA LOCKED", dnaLocked, "DNA chưa LOCKED"),
            Check("prp_locked", "PRP LOCKED", prpLocked, "PRP chưa LOCKED"),
            Check("master_sha", "Master SHA MATCH", masterShaValid, "Master SHA256 không khớp"),
            Check("dna_sha", "DNA SHA MATCH", dnaShaValid, "DNA SHA256 không khớp"),
            Check("prp_sha", "PRP SHA MATCH", prpShaValid, "PRP SHA256 không khớp"),
            Check("identity_pass", "Identity PASS", identityPass, "Identity Test chưa PASS"),
            Check("stress_pass", "Stress PASS", stressPass, "Identity Stress Test chưa PASS"),
        ];
    }

    public static bool GatePass(IReadOnlyList<CheckItem> items) => items.All(x => x.Pass);

    public static void EnsureCanCreate(IReadOnlyList<CheckItem> gate)
    {
        FailFirst(gate);
        if (CreatesPixels("CREATE"))
            throw new InvalidOperationException("SHOT_GATE_NOT_SATISFIED: không tạo ảnh / video.");
    }

    public static IReadOnlyList<CheckItem> EvaluateShotChecks(JsonElement spec, JsonElement dnaSpec, bool identityCheckPass)
    {
        var inheritOk = true;
        try { EnsureInheritance(spec, dnaSpec); }
        catch (InvalidOperationException) { inheritOk = false; }
        var risk = ReadRisk(spec);
        var high = risk == "HIGH";
        var items = new List<CheckItem>
        {
            Check("identity_inherit", "Identity inheritance (Face/Eyes/Hair/Age/Expression/Proportion/Style)", inheritOk, "Shot conflict với DNA. DNA thắng. Không tự sửa."),
            Check("continuity", "Continuity PASS", HasObject(spec, "continuity"), "thiếu Continuity reference"),
            Check("forbidden", "Forbidden conditions PASS", HasList(spec, "forbiddenConditions", 1), "thiếu Forbidden conditions"),
            Check("identity_risk", "Identity risk assessed", risk is "LOW" or "MEDIUM" or "HIGH", "Identity risk chưa đánh giá"),
            Check("identity_check", high ? "Identity Check REQUIRED (HIGH)" : "Identity Check", identityCheckPass, high ? "HIGH RISK bắt buộc Identity Check PASS" : "Identity Check chưa PASS"),
            Check("multi_person", "Multi-person policy", MultiPersonOk(spec), "blend / swap / thiếu identity anchor"),
            Check("no_pixels", "Không tạo ảnh / video / Gemini / Runway", !CreatesPixels(ReadString(spec, "engineAction")), "không gọi production engine"),
        };
        var validation = inheritOk && items.All(x => x.Pass) && IsComplete(spec);
        items.Add(Check("shot_validation", "Shot validation", validation, "SHOT validation FAIL"));
        return items;
    }

    public static IReadOnlyList<CheckItem> EvaluateDirectorReview(
        IReadOnlyList<CheckItem> source, JsonElement spec, JsonElement dnaSpec, bool identityCheckPass)
    {
        var items = source.ToList();
        items.AddRange(EvaluateShotChecks(spec, dnaSpec, identityCheckPass));
        return items;
    }

    public static void EnsureCanApprove(
        string? status, JsonElement spec, JsonElement dnaSpec, IReadOnlyList<CheckItem> source, bool identityCheckPass)
    {
        if (string.Equals(status, "LOCKED", StringComparison.OrdinalIgnoreCase))
            return;
        if (status is not ("DIRECTOR_REVIEW" or "IDENTITY_CHECK" or "APPROVED"))
            throw new InvalidOperationException("SHOT_GATE_NOT_SATISFIED: không bỏ qua gate. Chạy Identity Check rồi Director Review.");
        var review = EvaluateDirectorReview(source, spec, dnaSpec, identityCheckPass);
        FailFirst(review);
    }

    public static void EnsureInheritance(JsonElement shot, JsonElement dna)
    {
        if (dna.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("SHOT_GATE_NOT_SATISFIED: DNA spec không đọc được.");
        if (!shot.TryGetProperty("identity", out var ident) || ident.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("SHOT_GATE_NOT_SATISFIED: Shot không được định nghĩa lại Identity — thiếu identity inherit.");
        SameNode(ident, "face", dna, "face", "Face");
        SameNode(ident, "eyes", dna, "eyes", "Eyes");
        SameNode(ident, "hair", dna, "hair", "Hair");
        SameNode(ident, "age", dna, "age", "Age");
        SameNode(ident, "expression", dna, "expression", "Expression");
        SameNode(ident, "body", dna, "body", "Proportion");
        if (dna.TryGetProperty("style", out _))
            SameNode(ident, "style", dna, "style", "Style");
    }

    public static void EnsureNoIdentityMutation(JsonElement original, JsonElement edited)
    {
        foreach (var key in IdentityKeys)
        {
            original.TryGetProperty(key, out var a);
            edited.TryGetProperty(key, out var b);
            if (!JsonEquals(a, b))
                throw new InvalidOperationException("SHOT_GATE_NOT_SATISFIED: forbidden identity mutation. Shot không được đổi identity.");
        }
    }

    public static void EnsureImmutable(string? action, string? status)
    {
        if (!string.Equals(status, "LOCKED", StringComparison.OrdinalIgnoreCase))
            return;
        var a = (action ?? "").Trim().ToUpperInvariant();
        if (a is "UPDATE" or "DELETE" or "EDIT" or "OVERWRITE" or "CHANGE_MASTER" or "CHANGE_SHA256" or "REGENERATE" or "CHANGE_DNA" or "CHANGE_PRP")
            throw new InvalidOperationException("SHOT_LOCKED: V1 không overwrite. Dùng SHOT-V2.");
    }

    public static string AssessRisk(JsonElement spec)
    {
        var camera = ReadNested(spec, "camera", "angle") + " " + ReadNested(spec, "camera", "distance");
        var framing = ReadNested(spec, "framing", "type");
        var lighting = ReadNested(spec, "lighting", "style");
        var interaction = ReadNested(spec, "interaction", "kind");
        var pose = ReadNested(spec, "pose", "body");
        var people = ReadInt(spec, "interaction", "peopleCount");
        var text = $"{camera} {framing} {lighting} {interaction} {pose}".ToLowerInvariant();
        if (people > 1 || text.Contains("profile extreme") || text.Contains("heavy occlusion")
            || text.Contains("extreme lighting") || text.Contains("fast motion") || text.Contains("unusual camera")
            || text.Contains("small face") || text.Contains("multiple"))
            return "HIGH";
        if (text.Contains("strong angle") || text.Contains("motion") || text.Contains("dramatic")
            || text.Contains("partial occlusion") || text.Contains("unusual expression"))
            return "MEDIUM";
        return "LOW";
    }

    public static bool IdentityCheckRecorded(JsonElement spec) =>
        spec.TryGetProperty("identityCheck", out var n)
        && n.ValueKind == JsonValueKind.Object
        && n.TryGetProperty("pass", out var p)
        && p.ValueKind == JsonValueKind.True;

    public static JsonElement BuildDraftSpec(
        int seq,
        string masterSha256,
        string dnaSha256,
        string prpSha256,
        Guid masterId,
        Guid dnaId,
        Guid prpId,
        JsonElement dnaSpec,
        string? previousShotId)
    {
        object Clone(string key) =>
            dnaSpec.TryGetProperty(key, out var n)
                ? JsonSerializer.Deserialize<JsonElement>(n.GetRawText())
                : JsonSerializer.SerializeToElement(new { });

        var spec = new Dictionary<string, object?>
        {
            ["shot_id"] = ShotCode(seq),
            ["character_id"] = CharacterId,
            ["era_id"] = EraId,
            ["master_id"] = masterId,
            ["master_sha256"] = masterSha256,
            ["dna_id"] = dnaId,
            ["dna_sha256"] = dnaSha256,
            ["prp_id"] = prpId,
            ["prp_sha256"] = prpSha256,
            ["shot_status"] = "DRAFT",
            ["source"] = new Dictionary<string, object?>
            {
                ["master"] = KitVideoCharacterDnaRules.MasterCode,
                ["dna"] = KitVideoCharacterDnaRules.DnaCode,
                ["prp"] = KitVideoProductionReferencePackRules.PackCode,
                ["master_sha256"] = masterSha256,
                ["dna_sha256"] = dnaSha256,
                ["prp_sha256"] = prpSha256,
                ["notANewIdentity"] = true,
                ["notAnImage"] = true,
            },
            ["scene"] = new Dictionary<string, object?> { ["scene_id"] = "SC-01", ["shot_type"] = "MEDIUM" },
            ["camera"] = new Dictionary<string, object?>
            {
                ["angle"] = "eye-level 3/4",
                ["distance"] = "medium",
                ["lens_intent"] = "50mm cinematic still",
            },
            ["framing"] = new Dictionary<string, object?> { ["type"] = "medium", ["face"] = "clear", ["composition"] = "single subject Minh" },
            ["pose"] = new Dictionary<string, object?> { ["body"] = "natural standing", ["orientation"] = "3/4" },
            ["expression"] = new Dictionary<string, object?> { ["baseline"] = "NEUTRAL", ["intensity"] = "low", ["gaze"] = "slightly off-camera" },
            ["lighting"] = new Dictionary<string, object?> { ["style"] = "normal soft", ["direction"] = "front-side" },
            ["wardrobe"] = new Dictionary<string, object?> { ["withinPrp"] = true, ["wardrobeIsNotIdentity"] = true },
            ["environment"] = new Dictionary<string, object?> { ["kind"] = "interior era-01", ["allowed"] = true },
            ["interaction"] = new Dictionary<string, object?>
            {
                ["kind"] = "none",
                ["peopleCount"] = 1,
                ["otherCharacters"] = Array.Empty<string>(),
                ["minhDistinct"] = true,
                ["noBlend"] = true,
                ["noSwap"] = true,
                ["noAverage"] = true,
            },
            ["continuity"] = new Dictionary<string, object?>
            {
                ["character_id"] = CharacterId,
                ["master_id"] = masterId,
                ["master_sha256"] = masterSha256,
                ["dna_id"] = dnaId,
                ["dna_sha256"] = dnaSha256,
                ["prp_id"] = prpId,
                ["prp_sha256"] = prpSha256,
                ["scene_id"] = "SC-01",
                ["previous_shot_id"] = previousShotId,
                ["next_shot_id"] = null,
            },
            ["identityRisk"] = new Dictionary<string, object?>
            {
                ["level"] = "LOW",
                ["reason"] = "front / 3/4 · normal lighting · clear face · single person · normal pose",
                ["identityCheckRequired"] = false,
            },
            ["forbiddenConditions"] = ForbiddenConditions,
            ["allowedVariation"] = AllowedVariation,
            ["identity"] = new Dictionary<string, object?>
            {
                ["face"] = Clone("face"),
                ["eyes"] = Clone("eyes"),
                ["hair"] = Clone("hair"),
                ["age"] = Clone("age"),
                ["expression"] = Clone("expression"),
                ["body"] = Clone("body"),
                ["style"] = Clone("style"),
                ["notRedefined"] = true,
            },
            ["faceReference"] = Clone("face"),
            ["eyesReference"] = Clone("eyes"),
            ["hairReference"] = Clone("hair"),
            ["ageProportion"] = new Dictionary<string, object?> { ["age"] = Clone("age"), ["body"] = Clone("body") },
            ["expressionBaseline"] = Clone("expression"),
            ["style"] = Clone("style"),
            ["identityCheck"] = new Dictionary<string, object?> { ["pass"] = false, ["run"] = false, ["required"] = false },
            ["directorDecision"] = new Dictionary<string, object?> { ["status"] = "PENDING", ["noAutoApprove"] = true, ["noAutoLock"] = true },
            ["production_notes"] = "Specification only. Không Gemini. Không Runway. Không Production Still.",
            ["engineAction"] = "SPEC",
        };
        return JsonSerializer.SerializeToElement(spec);
    }

    public static JsonElement MarkIdentityCheck(JsonElement spec, bool pass, string risk)
    {
        var map = spec.ValueKind == JsonValueKind.Object
            ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(spec.GetRawText()) ?? new()
            : new Dictionary<string, JsonElement>();
        map["identityCheck"] = JsonSerializer.SerializeToElement(new
        {
            pass,
            run = true,
            required = risk == "HIGH",
            risk,
            at = DateTimeOffset.UtcNow,
        });
        map["identityRisk"] = JsonSerializer.SerializeToElement(new
        {
            level = risk,
            identityCheckRequired = risk == "HIGH",
        });
        map["directorDecision"] = JsonSerializer.SerializeToElement(new
        {
            status = pass ? "READY" : "BLOCKED",
            noAutoApprove = true,
            noAutoLock = true,
        });
        return JsonSerializer.SerializeToElement(map);
    }

    public static bool IsComplete(JsonElement spec)
    {
        if (spec.ValueKind != JsonValueKind.Object) return false;
        foreach (var key in RequiredSections)
        {
            if (!spec.TryGetProperty(key, out var n) || n.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                return false;
        }
        return ShaExists(ReadString(spec, "master_sha256"))
            && ShaExists(ReadString(spec, "dna_sha256"))
            && ShaExists(ReadString(spec, "prp_sha256"));
    }

    private static void FailFirst(IReadOnlyList<CheckItem> items)
    {
        var fail = items.FirstOrDefault(x => !x.Pass);
        if (fail is null) return;
        var prefix = fail.Code is "master_sha" or "dna_sha" or "prp_sha" ? "SHOT_INVALID" : "SHOT_GATE_NOT_SATISFIED";
        throw new InvalidOperationException($"{prefix}: {fail.Reason}");
    }

    private static CheckItem Check(string code, string label, bool pass, string reason) =>
        new(code, label, pass, pass ? null : reason);

    private static string ReadRisk(JsonElement spec)
    {
        if (spec.TryGetProperty("identityRisk", out var n) && n.ValueKind == JsonValueKind.Object
            && n.TryGetProperty("level", out var l) && l.ValueKind == JsonValueKind.String)
            return (l.GetString() ?? "LOW").ToUpperInvariant();
        return AssessRisk(spec);
    }

    private static bool MultiPersonOk(JsonElement spec)
    {
        if (!spec.TryGetProperty("interaction", out var n) || n.ValueKind != JsonValueKind.Object)
            return true;
        var people = n.TryGetProperty("peopleCount", out var p) && p.TryGetInt32(out var c) ? c : 1;
        if (people <= 1) return true;
        var distinct = n.TryGetProperty("minhDistinct", out var d) && d.ValueKind == JsonValueKind.True;
        var noBlend = n.TryGetProperty("noBlend", out var b) && b.ValueKind == JsonValueKind.True;
        var noSwap = n.TryGetProperty("noSwap", out var s) && s.ValueKind == JsonValueKind.True;
        var hasAnchor = spec.TryGetProperty("identity", out var ident) && ident.ValueKind == JsonValueKind.Object;
        return distinct && noBlend && noSwap && hasAnchor;
    }

    private static bool HasObject(JsonElement spec, string name) =>
        spec.ValueKind == JsonValueKind.Object
        && spec.TryGetProperty(name, out var n)
        && n.ValueKind == JsonValueKind.Object
        && n.EnumerateObject().Any();

    private static bool HasList(JsonElement spec, string name, int min) =>
        spec.ValueKind == JsonValueKind.Object
        && spec.TryGetProperty(name, out var n)
        && n.ValueKind == JsonValueKind.Array
        && n.GetArrayLength() >= min;

    private static string ReadString(JsonElement spec, string name) =>
        spec.TryGetProperty(name, out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() ?? "" : "";

    private static string ReadNested(JsonElement spec, string parent, string child)
    {
        if (!spec.TryGetProperty(parent, out var p) || p.ValueKind != JsonValueKind.Object) return "";
        return p.TryGetProperty(child, out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() ?? "" : "";
    }

    private static int ReadInt(JsonElement spec, string parent, string child)
    {
        if (!spec.TryGetProperty(parent, out var p) || p.ValueKind != JsonValueKind.Object) return 1;
        return p.TryGetProperty(child, out var c) && c.TryGetInt32(out var n) ? n : 1;
    }

    private static void SameNode(JsonElement left, string leftKey, JsonElement right, string rightKey, string label)
    {
        if (!left.TryGetProperty(leftKey, out var a) || !right.TryGetProperty(rightKey, out var b) || !JsonEquals(a, b))
            throw new InvalidOperationException($"SHOT_GATE_NOT_SATISFIED: {label} không kế thừa nguyên trạng từ DNA.");
    }

    private static bool JsonEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind == JsonValueKind.Undefined && b.ValueKind == JsonValueKind.Undefined) return true;
        if (a.ValueKind != b.ValueKind) return false;
        return string.Equals(Canonical(a), Canonical(b), StringComparison.Ordinal);
    }

    private static string Canonical(JsonElement node) =>
        JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(node.GetRawText()));
}
