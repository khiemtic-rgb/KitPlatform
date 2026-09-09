using System.Linq;
using System.Text.Json;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_FIRST_REAL_PRODUCTION_V1 — one shot, one image, Director-selected Gemini.
/// Does not generate, approve, lock, or pick a provider by itself.
/// </summary>
public static class FirstRealProductionRules
{
    public const string DocumentId = "FAMIXA_FIRST_REAL_PRODUCTION_V1";
    public const string SuiteId = "FAMIXA_FIRST_REAL_PRODUCTION_V1_REGRESSION";
    public const string AllowedProvider = "GEMINI";
    public const string AllowedGeneration = "IMAGE_GENERATION";
    public const string StaffCrpBlock = "Bộ ảnh chuẩn của nhân vật chưa hoàn tất. Không thể tạo hình production.";

    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool AutoSelectProvider() => false;
    public static bool AutoFix() => false;
    public static bool AllowsRunway() => false;
    public static bool AllowsVeo() => false;
    public static bool AllowsBatch() => false;
    public static bool CreatesPixels(string? action) =>
        action is "GENERATE" or "EXECUTE" or "GEMINI";

    public sealed record GateInput(
        bool MasterLocked,
        bool DnaLocked,
        bool PrpLocked,
        bool CrpUsable,
        string? CrpStatus,
        bool ShotContractValid,
        bool IntentValid,
        string? DirectorProvider,
        bool CapabilityReady,
        string? CapabilityCode,
        bool Confirm,
        bool DuplicateFingerprint,
        string? ShotCharacterId,
        string? RequestedCharacterId);

    public sealed record Gate(
        string Status,
        string? Code,
        string StaffMessage,
        bool AuthorityValid,
        bool CrpUsable,
        bool IntentValid,
        bool ProviderSelected,
        bool CapabilityReady,
        bool GenerationAllowed,
        bool ExecutionNotDuplicate,
        bool MayCallProvider);

    public static bool SameCharacter(string? shot, string? requested)
    {
        if (string.IsNullOrWhiteSpace(shot) || string.IsNullOrWhiteSpace(requested)) return false;
        return string.Equals(shot.Trim(), requested.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static bool ProviderSelected(string? provider) =>
        !string.IsNullOrWhiteSpace(provider);

    public static bool AllowsImageProvider(string? provider) =>
        string.Equals(provider, AllowedProvider, StringComparison.OrdinalIgnoreCase);

    public static bool ImageCapabilityReady(ProductionOsRules.ProviderCapabilityProfile profile) =>
        ProductionOsRules.CapabilityOf(profile, AllowedGeneration) == "SUPPORTED";

    public static Gate Evaluate(GateInput input)
    {
        if (!input.MasterLocked)
            return Block("MASTER_NOT_LOCKED", "Hồ sơ nhân vật chưa khóa. Không thể tạo hình production.", input);
        if (!input.DnaLocked)
            return Block("DNA_NOT_LOCKED", "Quy tắc nhận diện chưa khóa. Không thể tạo hình production.", input);
        if (!input.PrpLocked)
            return Block("PRP_NOT_LOCKED", "Ảnh tham chiếu sản xuất chưa khóa. Không thể tạo hình production.", input);
        if (!input.CrpUsable)
            return Block("CRP_NOT_READY", StaffCrpBlock, input);
        if (!SameCharacter(input.ShotCharacterId, input.RequestedCharacterId))
            return Block("CHARACTER_MISMATCH", "Shot này không được đổi sang nhân vật khác.", input);
        if (!input.ShotContractValid)
            return Block("SHOT_CONTRACT_INVALID", "Hợp đồng shot chưa hợp lệ. Không thể tạo hình production.", input);
        if (!input.IntentValid)
            return Block("INTENT_INVALID", "Nội dung shot chưa đủ để tạo hình.", input);
        if (!ProviderSelected(input.DirectorProvider))
            return new Gate("NEEDS_PROVIDER_SELECTION", "NEEDS_PROVIDER_SELECTION",
                "Chưa chọn nhà cung cấp AI.", true, input.CrpUsable, input.IntentValid,
                false, input.CapabilityReady, false, !input.DuplicateFingerprint, false);
        if (string.Equals(input.DirectorProvider, "RUNWAY", StringComparison.OrdinalIgnoreCase)
            || string.Equals(input.DirectorProvider, "VEO", StringComparison.OrdinalIgnoreCase)
            || !AllowsImageProvider(input.DirectorProvider)
            || !input.CapabilityReady)
            return Block(input.CapabilityCode ?? "PROVIDER_CAPABILITY_UNSUPPORTED",
                "Nhà cung cấp AI này chưa hỗ trợ tạo hình cho shot này.", input, providerSelected: true);
        if (input.DuplicateFingerprint)
            return Block("BLOCK_DUPLICATE", "Lần tạo hình này đã chạy. Không gọi lại nhà cung cấp.", input,
                providerSelected: true, capability: true, duplicate: true);
        if (!input.Confirm)
            return new Gate("BLOCKED", "CONFIRM_REQUIRED",
                "Cần xác nhận trước khi tạo hình.", true, true, true, true, true, false, true, false);
        return new Gate("READY", null, "Sẵn sàng tạo 01 hình.", true, true, true, true, true, true, true, true);
    }

    public static bool MayCallProvider(Gate gate) =>
        gate.MayCallProvider && gate.GenerationAllowed && !AutoSelectProvider() && !AutoApprove();

    public static ProductionOsRules.ProductionIntent? TryResolveIntent(
        string characterId,
        JsonElement payload,
        string masterSha,
        string dnaSha,
        string referenceSha,
        string contractSha)
    {
        if (string.IsNullOrWhiteSpace(characterId)
            || !CharacterIdentityGovernanceRules.ShaExists(masterSha)
            || !CharacterIdentityGovernanceRules.ShaExists(dnaSha)
            || !CharacterIdentityGovernanceRules.ShaExists(referenceSha)
            || !CharacterIdentityGovernanceRules.ShaExists(contractSha))
            return null;
        if (payload.ValueKind != JsonValueKind.Object) return null;
        var action = ReadNested(payload, "story", "action");
        if (action.Length == 0) return null;
        var location = ReadNested(payload, "scene", "location");
        if (location.Length == 0) location = ReadNested(payload, "scene", "summary");
        var state = ReadNested(payload, "character", "expression");
        if (state.Length == 0) state = "neutral";
        var shotSize = ReadNested(payload, "composition", "framing");
        if (shotSize.Length == 0) shotSize = "medium";
        var angle = ReadNested(payload, "composition", "angle");
        if (angle.Length == 0) angle = "eye_level";
        var movement = ReadNested(payload, "composition", "movement");
        if (movement.Length == 0) movement = ReadNested(payload, "motion", "camera");
        if (movement.Length == 0) movement = "hold";
        var lighting = ReadNested(payload, "lighting", "style");
        if (lighting.Length == 0) lighting = "warm_window";
        var motion = ReadNested(payload, "motion", "action");
        if (motion.Length == 0) motion = "hold";
        var duration = ReadNumber(payload, "timing", "durationSeconds");
        if (duration <= 0) duration = 5;
        var intent = new ProductionOsRules.ProductionIntent(
            characterId.Trim().ToUpperInvariant(),
            null,
            state,
            null,
            location,
            null,
            "read",
            action,
            shotSize,
            angle,
            movement,
            "center",
            "front",
            lighting,
            motion,
            "",
            duration,
            null,
            true,
            masterSha,
            dnaSha,
            referenceSha,
            contractSha);
        if (ProductionOsRules.ContainsForbiddenIntentKey(action) || ProductionOsRules.ContainsProvider(action))
            return null;
        return intent;
    }

    public static string StaffCharacterLine(string? name, string? characterId) =>
        string.IsNullOrWhiteSpace(name) ? (characterId ?? "Nhân vật") : name.Trim();

    private static Gate Block(
        string code, string staff, GateInput input,
        bool providerSelected = false, bool capability = false, bool duplicate = false) =>
        new("BLOCKED", code, staff,
            input.MasterLocked && input.DnaLocked && input.PrpLocked,
            input.CrpUsable, input.IntentValid, providerSelected, capability, false, !duplicate, false);

    private static string ReadNested(JsonElement payload, string obj, string name)
    {
        if (!payload.TryGetProperty(obj, out var o) || o.ValueKind != JsonValueKind.Object) return "";
        if (!o.TryGetProperty(name, out var n)) return "";
        return n.ValueKind == JsonValueKind.String ? n.GetString()?.Trim() ?? "" : "";
    }

    private static double ReadNumber(JsonElement payload, string obj, string name)
    {
        if (!payload.TryGetProperty(obj, out var o) || o.ValueKind != JsonValueKind.Object) return 0;
        if (!o.TryGetProperty(name, out var n)) return 0;
        return n.ValueKind == JsonValueKind.Number ? n.GetDouble() : 0;
    }
}
