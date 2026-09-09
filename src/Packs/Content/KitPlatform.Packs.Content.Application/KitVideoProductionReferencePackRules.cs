using System.Linq;
using System.Text.Json;

namespace KitPlatform.Packs.Content;

public static class KitVideoProductionReferencePackRules
{
    public const string DocumentId = "CHAR-001_MINH_PRODUCTION_REFERENCE_PACK_V1";
    public const string PackCode = "CHAR-001-MINH-ERA01-PROD-REF-V1";
    public const string Version = "V1";
    public const string CharacterId = "CHAR-001";
    public const string EraId = "ERA-01";
    public const string CharacterName = "MINH";

    public static readonly string[] RequiredSections =
    [
        "masterReference", "characterDna", "identityAnchor", "faceReference", "eyesReference",
        "hairReference", "ageProportion", "expressionBaseline", "allowedVariation", "forbiddenVariation",
        "continuityRules", "identityConstraints", "productionRules", "shotRules", "cameraRules",
        "expressionRules", "wardrobeRules", "environmentRules", "productionContinuity",
        "forbiddenProduction", "stressTestSummary", "regressionStatus", "lockMetadata",
    ];

    public static readonly string[] ReviewOnlySections =
    [
        "identityConstraints", "productionRules", "shotRules", "cameraRules",
        "expressionRules", "wardrobeRules", "environmentRules", "productionContinuity", "forbiddenProduction",
    ];

    public static readonly string[] ProductionAllowed =
    [
        "camera distance", "camera angle", "framing", "body pose", "hand position", "gaze direction",
        "facial expression within DNA", "lighting", "environment", "wardrobe within DNA",
        "props", "interaction", "scene composition",
    ];

    public static readonly string[] ProductionForbidden =
    [
        "đổi khuôn mặt", "đổi shape mắt", "đổi tỷ lệ khuôn mặt", "đổi kiểu tóc đặc trưng",
        "đổi tuổi biểu kiến", "đổi proportions", "generic AI boy", "anime", "photorealistic",
        "character redesign", "face blending", "face averaging", "identity swap", "identity drift",
        "face mutation", "tạo một Minh khác", "sử dụng character reference không được approve",
    ];

    public static readonly string[] IdentityKeys =
    [
        "identityAnchor", "faceReference", "eyesReference", "hairReference", "ageProportion",
        "expressionBaseline", "allowedVariation", "forbiddenVariation", "continuityRules",
        "masterSha256", "dnaSha256",
    ];

    public static bool Accepts(string? characterId, string? eraId) =>
        string.Equals(characterId, CharacterId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(eraId, EraId, StringComparison.OrdinalIgnoreCase);

    public static bool SameSha(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    public static bool ShaExists(string? sha) =>
        !string.IsNullOrWhiteSpace(sha) && sha.Trim().Length >= 32;

    public static void EnsureDirector(string? actor)
    {
        var a = (actor ?? "").Trim();
        if (a.Length == 0 || a.Equals("anonymous", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("PRP_GATE_NOT_SATISFIED: chỉ Director được duyệt Production Reference Pack.");
    }

    public static bool CreatesPixels(string? action) =>
        action is "GENERATE" or "REGENERATE";

    public static bool AutoCreateApproved() => false;
    public static bool AutoLock() => false;

    public sealed record CheckItem(string Code, string Label, bool Pass, string? Reason);

    public static IReadOnlyList<CheckItem> EvaluateCreateGate(
        bool masterLocked, bool dnaLocked, bool masterShaValid, bool dnaShaValid,
        bool identityPass, bool stressPass, int p0, bool directorApproval)
    {
        return
        [
            Check("master_locked", "Master LOCKED", masterLocked, "Master chưa LOCKED"),
            Check("dna_locked", "Character DNA V1 LOCKED", dnaLocked, "Character DNA V1 chưa LOCKED"),
            Check("master_sha", "Master SHA256 khớp", masterShaValid, "Master SHA256 không khớp"),
            Check("dna_sha", "DNA SHA256 tồn tại và khớp", dnaShaValid, "DNA SHA256 thiếu hoặc không khớp"),
            Check("identity_pass", "Identity Test PASS", identityPass, "Identity Test chưa PASS"),
            Check("stress_pass", "Identity Stress Test PASS", stressPass, "Identity Stress Test chưa PASS"),
            Check("p0_zero", "P0 = 0", p0 == 0, "P0 phải = 0"),
            Check("director_approval", "Director Approval PASS", directorApproval, "Director Approval chưa PASS"),
        ];
    }

    public static bool GatePass(IReadOnlyList<CheckItem> items) => items.All(x => x.Pass);

    public static void EnsureCanCreate(IReadOnlyList<CheckItem> gate, bool lockedPack = false)
    {
        if (lockedPack)
            throw new InvalidOperationException("PRP_LOCKED: V1 đã khóa. Không tạo PROD-REF-V2 từ nút V1.");
        var fail = gate.FirstOrDefault(x => !x.Pass);
        if (fail is null) return;
        var prefix = fail.Code is "master_sha" or "dna_sha" ? "PRP_INVALID" : "PRP_GATE_NOT_SATISFIED";
        throw new InvalidOperationException($"{prefix}: {fail.Reason}");
    }

    public static IReadOnlyList<CheckItem> EvaluateReviewChecks(JsonElement spec, JsonElement dnaSpec)
    {
        var inheritOk = true;
        try { EnsureInheritance(spec, dnaSpec); }
        catch (InvalidOperationException) { inheritOk = false; }
        var items = new List<CheckItem>
        {
            Check("identity_inherit", "Identity inheritance (Face/Eyes/Hair/Age/Expression/Proportion/Style)", inheritOk, "PRP conflict với DNA. DNA thắng. Không tự sửa."),
            Check("identity_constraints", "Identity Constraints", HasObject(spec, "identityConstraints"), "thiếu Identity Constraints"),
            Check("production_rules", "Production Rules", HasObject(spec, "productionRules"), "thiếu Production Rules"),
            Check("shot_rules", "Shot Rules", HasObject(spec, "shotRules"), "thiếu Shot Rules"),
            Check("camera_rules", "Camera Rules", HasObject(spec, "cameraRules"), "thiếu Camera Rules"),
            Check("expression_rules", "Expression Rules", HasObject(spec, "expressionRules"), "thiếu Expression Rules"),
            Check("wardrobe_rules", "Wardrobe Rules", HasObject(spec, "wardrobeRules"), "thiếu Wardrobe Rules"),
            Check("environment_rules", "Environment Rules", HasObject(spec, "environmentRules"), "thiếu Environment Rules"),
            Check("continuity_rules", "Continuity Rules", HasObject(spec, "productionContinuity") || HasObject(spec, "continuityRules"), "thiếu Continuity Rules"),
            Check("forbidden_production", "Forbidden Production", HasList(spec, "forbiddenProduction", 1) || HasObject(spec, "forbiddenProduction") || HasList(spec, "forbiddenVariation", 1), "thiếu Forbidden Production"),
            Check("lock_metadata", "Regression / Lock Metadata", HasObject(spec, "lockMetadata") && HasObject(spec, "regressionStatus"), "thiếu Regression / Lock Metadata"),
        };
        var validation = inheritOk && items.All(x => x.Pass);
        try { EnsureComplete(spec); }
        catch (InvalidOperationException) { validation = false; }
        items.Add(Check("prp_validation", "PRP validation", validation, "PRP validation FAIL"));
        return items;
    }

    public static IReadOnlyList<CheckItem> EvaluateDirectorReview(
        IReadOnlyList<CheckItem> createGate, JsonElement spec, JsonElement dnaSpec)
    {
        var items = createGate.ToList();
        items.AddRange(EvaluateReviewChecks(spec, dnaSpec));
        return items;
    }

    public static void EnsureCanApprove(string? status, JsonElement spec, IReadOnlyList<CheckItem> gate, JsonElement dnaSpec)
    {
        if (string.Equals(status, "LOCKED", StringComparison.OrdinalIgnoreCase))
            return;
        var review = EvaluateDirectorReview(gate, spec, dnaSpec);
        var fail = review.FirstOrDefault(x => !x.Pass);
        if (fail is null) return;
        var prefix = fail.Code is "master_sha" or "dna_sha" ? "PRP_INVALID" : "PRP_GATE_NOT_SATISFIED";
        throw new InvalidOperationException($"{prefix}: {fail.Reason}");
    }

    public static void EnsureCanCreateShot(
        bool prpLocked, IReadOnlyList<CheckItem> gate, JsonElement spec, JsonElement dnaSpec)
    {
        if (!prpLocked)
            throw new InvalidOperationException("PRP_GATE_NOT_SATISFIED: PRP chưa LOCKED. Không tạo Production Shot.");
        EnsureCanApprove("DRAFT", spec, gate, dnaSpec);
        if (CreatesPixels("GENERATE"))
            throw new InvalidOperationException("PRP_GATE_NOT_SATISFIED: không tạo Production Still.");
    }

    public static void EnsureComplete(JsonElement spec)
    {
        if (spec.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("PRP_GATE_NOT_SATISFIED: pack complete thất bại.");
        foreach (var key in RequiredSections)
        {
            if (!spec.TryGetProperty(key, out var node) || node.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                throw new InvalidOperationException($"PRP_GATE_NOT_SATISFIED: thiếu {key}.");
        }
        if (!ShaExists(ReadString(spec, "masterSha256")))
            throw new InvalidOperationException("PRP_INVALID: MASTER SHA256 thiếu.");
        if (!ShaExists(ReadString(spec, "dnaSha256")))
            throw new InvalidOperationException("PRP_INVALID: DNA SHA256 thiếu.");
    }

    public static void EnsureInheritance(JsonElement pack, JsonElement dna)
    {
        if (dna.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("PRP_GATE_NOT_SATISFIED: DNA spec không đọc được.");
        SameNode(pack, "faceReference", dna, "face", "Face");
        SameNode(pack, "eyesReference", dna, "eyes", "Eyes");
        SameNode(pack, "hairReference", dna, "hair", "Hair");
        if (!pack.TryGetProperty("ageProportion", out var ageProp) || ageProp.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("PRP_GATE_NOT_SATISFIED: Age / Proportion không kế thừa.");
        SameNode(ageProp, "age", dna, "age", "Age");
        SameNode(ageProp, "body", dna, "body", "Proportion");
        SameNode(pack, "expressionBaseline", dna, "expression", "Expression baseline");
        SameNode(pack, "allowedVariation", dna, "allowedVariation", "Allowed Variation");
        SameNode(pack, "forbiddenVariation", dna, "forbiddenVariation", "Forbidden Variation");
        SameNode(pack, "continuityRules", dna, "continuityRules", "Continuity Rules");
        SameNode(pack, "identityInvariants", dna, "identityInvariants", "Identity Invariants");
        if (dna.TryGetProperty("style", out _))
            SameNode(pack, "style", dna, "style", "Style");
    }

    public static void EnsureNoIdentityMutation(JsonElement original, JsonElement edited)
    {
        foreach (var key in IdentityKeys)
        {
            original.TryGetProperty(key, out var a);
            edited.TryGetProperty(key, out var b);
            if (!JsonEquals(a, b))
                throw new InvalidOperationException("PRP_GATE_NOT_SATISFIED: forbidden identity mutation. Pack không được đổi identity.");
        }
    }

    public static void EnsureImmutable(string? action, string? status)
    {
        if (!string.Equals(status, "LOCKED", StringComparison.OrdinalIgnoreCase))
            return;
        var a = (action ?? "").Trim().ToUpperInvariant();
        if (a is "UPDATE" or "DELETE" or "EDIT" or "OVERWRITE" or "CHANGE_MASTER" or "CHANGE_SHA256" or "REGENERATE" or "CHANGE_DNA")
            throw new InvalidOperationException("PRP_LOCKED: V1 không overwrite. Dùng CHAR-001-MINH-ERA01-PROD-REF-V2.");
    }

    public static void EnsureDoesNotMutateSources()
    {
        // Pack never writes Master or DNA. Callers must not invoke Master/DNA mutate APIs from this path.
    }

    public static JsonElement BuildV1Spec(
        string masterSha256,
        string dnaSha256,
        Guid masterId,
        Guid dnaId,
        JsonElement dnaSpec,
        bool identityPass,
        bool stressPass,
        int p0)
    {
        object Clone(string key) =>
            dnaSpec.TryGetProperty(key, out var n)
                ? JsonSerializer.Deserialize<JsonElement>(n.GetRawText())
                : JsonSerializer.SerializeToElement(new { });

        var spec = new Dictionary<string, object?>
        {
            ["derived_from_master"] = KitVideoCharacterDnaRules.MasterCode,
            ["derived_from_dna"] = KitVideoCharacterDnaRules.DnaCode,
            ["masterSha256"] = masterSha256,
            ["dnaSha256"] = dnaSha256,
            ["masterReference"] = new Dictionary<string, object?>
            {
                ["masterCode"] = KitVideoCharacterDnaRules.MasterCode,
                ["masterId"] = masterId,
                ["status"] = "LOCKED",
                ["sha256"] = masterSha256,
            },
            ["characterDna"] = new Dictionary<string, object?>
            {
                ["dnaCode"] = KitVideoCharacterDnaRules.DnaCode,
                ["dnaId"] = dnaId,
                ["status"] = "LOCKED",
                ["sha256"] = dnaSha256,
            },
            ["identityAnchor"] = new Dictionary<string, object?>
            {
                ["character"] = CharacterName,
                ["characterId"] = CharacterId,
                ["era"] = EraId,
                ["sourceMaster"] = KitVideoCharacterDnaRules.MasterCode,
                ["sourceDna"] = KitVideoCharacterDnaRules.DnaCode,
                ["notANewIdentity"] = true,
            },
            ["faceReference"] = Clone("face"),
            ["eyesReference"] = Clone("eyes"),
            ["hairReference"] = Clone("hair"),
            ["ageProportion"] = new Dictionary<string, object?>
            {
                ["age"] = Clone("age"),
                ["body"] = Clone("body"),
            },
            ["expressionBaseline"] = Clone("expression"),
            ["allowedVariation"] = Clone("allowedVariation"),
            ["forbiddenVariation"] = Clone("forbiddenVariation"),
            ["continuityRules"] = Clone("continuityRules"),
            ["identityInvariants"] = Clone("identityInvariants"),
            ["style"] = Clone("style"),
            ["stressTestSummary"] = new Dictionary<string, object?>
            {
                ["identityPass"] = identityPass,
                ["stressPass"] = stressPass,
                ["p0"] = p0,
                ["identity7of7"] = identityPass,
                ["stress10of10"] = stressPass,
            },
            ["regressionStatus"] = new Dictionary<string, object?>
            {
                ["noGenerate"] = true,
                ["noGemini"] = true,
                ["noRunway"] = true,
                ["masterImmutable"] = true,
                ["dnaImmutable"] = true,
                ["noNewIdentity"] = true,
                ["noAutoApprove"] = true,
                ["noAutoLock"] = true,
            },
            ["lockMetadata"] = new Dictionary<string, object?>
            {
                ["packCode"] = PackCode,
                ["packVersion"] = Version,
                ["derived_from_master"] = KitVideoCharacterDnaRules.MasterCode,
                ["derived_from_dna"] = KitVideoCharacterDnaRules.DnaCode,
                ["master_sha256"] = masterSha256,
                ["dna_sha256"] = dnaSha256,
                ["immutableAfterLock"] = true,
                ["notANewIdentity"] = true,
            },
        };
        foreach (var kv in BuildProductionRules(masterSha256, dnaSha256, masterId, dnaId))
            spec[kv.Key] = kv.Value;
        return JsonSerializer.SerializeToElement(spec);
    }

    public static Dictionary<string, object?> BuildProductionRules(
        string masterSha256, string dnaSha256, Guid masterId, Guid dnaId) => new()
    {
        ["identityConstraints"] = new Dictionary<string, object?>
        {
            ["inherit"] = new[] { "FACE", "EYES", "HAIR", "AGE", "EXPRESSION", "PROPORTION", "STYLE" },
            ["dnaWinsOnConflict"] = true,
            ["masterWinsOnMasterConflict"] = true,
            ["noAutoFix"] = true,
            ["notANewIdentity"] = true,
        },
        ["productionRules"] = new Dictionary<string, object?>
        {
            ["uses"] = new[] { "video", "shot", "scene", "camera", "framing", "pose", "expression", "lighting", "wardrobe", "environment", "interaction", "continuity" },
            ["mustFollowDna"] = true,
            ["allowed"] = ProductionAllowed,
        },
        ["shotRules"] = new Dictionary<string, object?>
        {
            ["fields"] = new[] { "SHOT ID", "SHOT TYPE", "CAMERA", "FRAMING", "POSE", "EXPRESSION", "GAZE", "LIGHTING", "ENVIRONMENT", "WARDROBE", "CONTINUITY REQUIREMENT", "IDENTITY RISK" },
            ["risk"] = new[] { "LOW RISK", "MEDIUM RISK", "HIGH RISK" },
            ["highRiskRequiresIdentityCheck"] = true,
            ["noShotCreatedInReview"] = true,
        },
        ["cameraRules"] = new Dictionary<string, object?>
        {
            ["allowed"] = new[] { "camera distance", "camera angle", "framing" },
            ["mustNotChangeFaceIdentity"] = true,
        },
        ["expressionRules"] = new Dictionary<string, object?>
        {
            ["withinDna"] = new[] { "NEUTRAL", "SAD", "LIGHT SMILE" },
            ["expressionIsNotNewIdentity"] = true,
        },
        ["wardrobeRules"] = new Dictionary<string, object?>
        {
            ["wardrobeIsNotIdentity"] = true,
            ["allowedWithinDna"] = true,
        },
        ["environmentRules"] = new Dictionary<string, object?>
        {
            ["allowed"] = new[] { "environment", "lighting", "scene composition", "props" },
            ["mustNotBreakIdentity"] = true,
        },
        ["productionContinuity"] = new Dictionary<string, object?>
        {
            ["required"] = new[] { "character_id", "master_id", "master_sha256", "dna_id", "dna_sha256", "prp_id", "prp_version" },
            ["character_id"] = CharacterId,
            ["master_id"] = masterId,
            ["master_sha256"] = masterSha256,
            ["dna_id"] = dnaId,
            ["dna_sha256"] = dnaSha256,
            ["prp_version"] = Version,
            ["shaMismatchBlocksProduction"] = true,
        },
        ["forbiddenProduction"] = ProductionForbidden,
    };

    public static JsonElement AttachMissingProductionRules(JsonElement spec, string masterSha256, string dnaSha256, Guid masterId, Guid dnaId)
    {
        var map = spec.ValueKind == JsonValueKind.Object
            ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(spec.GetRawText()) ?? new()
            : new Dictionary<string, JsonElement>();
        var overlay = BuildProductionRules(masterSha256, dnaSha256, masterId, dnaId);
        foreach (var kv in overlay)
        {
            if (!map.ContainsKey(kv.Key) || map[kv.Key].ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                map[kv.Key] = JsonSerializer.SerializeToElement(kv.Value);
        }
        return JsonSerializer.SerializeToElement(map);
    }

    private static CheckItem Check(string code, string label, bool pass, string reason) =>
        new(code, label, pass, pass ? null : reason);

    private static string ReadString(JsonElement spec, string name) =>
        spec.TryGetProperty(name, out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() ?? "" : "";

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

    private static void SameNode(JsonElement left, string leftKey, JsonElement right, string rightKey, string label)
    {
        if (!left.TryGetProperty(leftKey, out var a) || !right.TryGetProperty(rightKey, out var b) || !JsonEquals(a, b))
            throw new InvalidOperationException($"PRP_GATE_NOT_SATISFIED: {label} không kế thừa nguyên trạng từ DNA.");
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
