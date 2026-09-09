using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_CHARACTER_REFERENCE_REGENERATION_V1 — Director REJECT then regenerate one 4-view set.
/// Does not mutate Master/DNA/PRP, auto-approve, auto-lock, or open production.
/// </summary>
public static class CharacterReferenceRegenerationV1Rules
{
    public const string DocumentId = "FAMIXA_CHARACTER_REFERENCE_REGENERATION_V1";
    public const string SuiteId = "FAMIXA_CHARACTER_REFERENCE_REGENERATION_V1_REGRESSION";
    public const string Version = "CHARACTER_REFERENCE_REGENERATION_V1";
    public const string Intent = "CHARACTER_REFERENCE_SET_REGENERATION";
    public const string PendingReview = "READY_FOR_DIRECTOR";
    public const string Rejected = "REJECTED";
    public const string Regenerating = "REGENERATING";
    public const string Locked = "LOCKED";
    public const string Approved = "DIRECTOR_APPROVED";

    public static readonly string[] RequiredTypes = CharacterReferencePackRules.RequiredTypes;
    public static readonly string[] RejectReasonCodes =
        ["FACE", "HAIR", "PROPORTION", "AGE", "WARDROBE", "INCONSISTENT", "FULL_BODY", "QUALITY", "OTHER"];

    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool AutoSelectProvider() => false;
    public static bool AutoRetry() => false;
    public static bool AutoFallbackProvider() => false;
    public static bool OpensFirstRealProduction() => false;
    public static bool GeneratesShot() => false;
    public static bool GeneratesVideo() => false;
    public static bool MutatesMaster() => false;
    public static bool MutatesDna() => false;
    public static bool MutatesPrp() => false;
    public static bool MutatesIdentity() => false;
    public static bool OverwritesRejectedSet() => false;
    public static bool OverwritesLockedCrp() => false;
    public static bool AllowsPerSlotGenerate() => false;
    public static bool RejectIncrementsVersion() => false;
    public static bool RejectCreatesHistoryVersion() => false;

    public static string StaffReject => "Không đạt";
    public static string StaffRejectTitle => "Đánh giá bộ ảnh";
    public static string StaffRejectReason => "Lý do không đạt";
    public static string StaffRejectConfirm => "Xác nhận không đạt";
    public static string StaffRegenerate => "Tạo lại bộ ảnh";
    public static string StaffStart => "Bắt đầu tạo lại";
    public static string StaffCancel => "Hủy";
    public static string StaffRejected => "Không đạt";
    public static string StaffPending => "Đang chờ duyệt";
    public static string StaffNeedReason => "Cần nhập lý do không đạt.";
    public static string StaffMaster => "Master chưa khóa.";
    public static string StaffDna => "DNA chưa khóa.";
    public static string StaffPrp => "PRP chưa khóa.";
    public static string StaffMissing => "Chưa có bộ ảnh chuẩn.";
    public static string StaffNotRejected => "Cần đánh giá Không đạt trước khi tạo lại.";
    public static string StaffLocked => "Bộ ảnh đã khóa. Không tạo lại.";
    public static string StaffGenerating => "Bộ ảnh đang được tạo. Không gọi thêm.";
    public static string StaffProvider => "Vui lòng chọn nhà cung cấp.";
    public static string StaffCapability => "Nhà cung cấp này chưa hỗ trợ.";
    public static string StaffConfirm => "Cần xác nhận trước khi tạo lại.";
    public static string StaffDuplicate => "Bộ ảnh này đã được tạo trước đó.";
    public static string StaffFail => "Không tạo được bộ ảnh. Không tự gọi lại.";
    public static string StaffIncomplete => "Bộ ảnh chưa đủ 4 góc. Không dùng được.";
    public static string StaffCharacter => "Không tìm thấy nhân vật.";
    public static string StaffApproved => "Bộ ảnh đã duyệt. Không tạo lại từ trạng thái này.";
    public static string StaffDraft => "Bộ ảnh chưa sẵn sàng để tạo lại.";
    public static string RejectedSetLabel(int ready, int total = 4) =>
        $"Bộ ảnh chuẩn: {ready}/{total} · {StaffRejected}";

    public static string RejectReasonLabel(string? code) => (code ?? "").Trim().ToUpperInvariant() switch
    {
        "FACE" => "Không giống khuôn mặt",
        "HAIR" => "Sai kiểu tóc",
        "PROPORTION" => "Sai tỷ lệ cơ thể",
        "AGE" => "Sai tuổi",
        "WARDROBE" => "Sai trang phục",
        "INCONSISTENT" => "Các góc không đồng nhất",
        "FULL_BODY" => "Toàn thân không đúng",
        "QUALITY" => "Hình ảnh không đạt chất lượng",
        _ => "Khác",
    };

    public static bool RejectReasonValid(string? text) => (text ?? "").Trim().Length >= 3;

    public static bool IsPendingReview(string? status) =>
        status is "READY_FOR_DIRECTOR" or "CRP_PENDING_REVIEW" or "PENDING_REVIEW" or "VALIDATED" or "REVIEW";

    public static bool IsRejected(string? status) =>
        status is "REJECTED" or "CRP_REJECTED";

    public static bool IsLocked(string? status) =>
        string.Equals(status, Locked, StringComparison.OrdinalIgnoreCase);

    public static bool IsApproved(string? status) =>
        status is "APPROVED" or "DIRECTOR_APPROVED" or "CRP_APPROVED";

    public static bool IsGenerating(string? status) =>
        status is "REGENERATING" or "GENERATING" or "CRP_GENERATING" or "CRP_REGENERATING";

    public static bool IsDraft(string? status) =>
        status is "DRAFT" or "CRP_DRAFT" or "INCOMPLETE" or null or "";

    public static bool MayReject(string? status) => IsPendingReview(status);

    public static bool MayRegenerate(string? status) => IsRejected(status);

    public static string NextVersion(string? current)
    {
        var n = CharacterReferencePackRules.VersionNumber(string.IsNullOrWhiteSpace(current) ? "V1" : current);
        return CharacterReferencePackRules.NextVersion(n <= 0 ? "V1" : $"V{n}");
    }

    public static string ExecutionFingerprint(
        string characterId, string eraId, string masterSha, string dnaSha, string prpSha,
        string version, string? provider)
    {
        var payload = new
        {
            character_id = characterId.Trim().ToUpperInvariant(),
            era_id = string.IsNullOrWhiteSpace(eraId) ? "ERA-01" : eraId.Trim().ToUpperInvariant(),
            master_sha256 = (masterSha ?? "").Trim().ToLowerInvariant(),
            dna_sha256 = (dnaSha ?? "").Trim().ToLowerInvariant(),
            prp_sha256 = (prpSha ?? "").Trim().ToLowerInvariant(),
            reference_set_version = string.IsNullOrWhiteSpace(version) ? "V1" : version.Trim().ToUpperInvariant(),
            intent = Intent,
            provider = (provider ?? "").Trim().ToUpperInvariant(),
            slots = RequiredTypes,
        };
        return KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, Canonical)));
    }

    public static string DuplicatePolicy(string fingerprint, string? existingSuccess) =>
        !string.IsNullOrWhiteSpace(existingSuccess)
        && string.Equals(fingerprint, existingSuccess, StringComparison.OrdinalIgnoreCase)
            ? "BLOCK_DUPLICATE"
            : "NEW";

    public static bool CanUseProduction(string? status, bool canUse, int coverage) =>
        FirstRealProductionV2Rules.CrpReadyForProduction(status, canUse, coverage);

    public static bool CanUseAfterReject() => false;
    public static bool CanUseAfterRegenerate() => false;
    public static bool CanUseWhilePending() => false;
    public static bool CanUseWhileApprovedUnlocked() => false;

    public sealed record GateInput(
        bool CharacterExists,
        bool MasterLocked,
        bool DnaLocked,
        bool PrpLocked,
        bool CrpExists,
        string? CrpStatus,
        string? DirectorProvider,
        bool Confirm,
        bool DuplicateSuccess,
        bool RejectReasonOk,
        string Action);

    public sealed record Gate(
        string Status,
        string? Code,
        string StaffMessage,
        bool MayReject,
        bool MayRegenerate,
        bool MayCallProvider,
        bool GenerationAllowed);

    public static Gate Evaluate(GateInput input)
    {
        if (!input.CharacterExists)
            return Block("INVALID_CHARACTER", StaffCharacter);
        if (!input.MasterLocked)
            return Block("MASTER_NOT_READY", StaffMaster);
        if (!input.DnaLocked)
            return Block("DNA_NOT_READY", StaffDna);
        if (!input.PrpLocked)
            return Block("PRP_NOT_READY", StaffPrp);
        if (!input.CrpExists)
            return Block("CRP_NOT_FOUND", StaffMissing);
        if (IsLocked(input.CrpStatus))
            return Block("CRP_LOCKED", StaffLocked);
        if (IsGenerating(input.CrpStatus) && input.Action != "REJECT")
            return Block("CRP_ALREADY_GENERATING", StaffGenerating);

        if (input.Action == "REJECT")
        {
            if (!MayReject(input.CrpStatus))
            {
                if (IsApproved(input.CrpStatus)) return Block("CRP_NOT_REJECTED", StaffApproved);
                if (IsDraft(input.CrpStatus)) return Block("CRP_NOT_REJECTED", StaffDraft);
                return Block("CRP_NOT_REJECTED", StaffNotRejected);
            }
            if (!input.RejectReasonOk)
                return Block("CRP_GATE_NOT_SATISFIED", StaffNeedReason);
            return new Gate(Rejected, null, StaffRejected, true, false, false, false);
        }

        if (input.Action is "REGENERATE" or "EXECUTE")
        {
            if (!MayRegenerate(input.CrpStatus))
                return Block("CRP_NOT_REJECTED", StaffNotRejected);
            if (string.IsNullOrWhiteSpace(input.DirectorProvider))
                return new Gate("NEEDS_PROVIDER", "PROVIDER_REQUIRED", StaffProvider, false, true, false, false);
            if (CharacterReferenceAutoGenerationV2Rules.SetCapability(input.DirectorProvider) != "SUPPORTED")
                return Block("PROVIDER_CAPABILITY_UNSUPPORTED", StaffCapability);
            if (input.DuplicateSuccess)
                return Block("BLOCK_DUPLICATE", StaffDuplicate);
            if (!input.Confirm)
                return new Gate("BLOCKED", "CONFIRMATION_REQUIRED", StaffConfirm, false, true, false, false);
            return new Gate(Regenerating, null, StaffRegenerate, false, true, true, true);
        }

        return new Gate(input.CrpStatus ?? "DRAFT", null, StaffOf(input.CrpStatus), MayReject(input.CrpStatus), MayRegenerate(input.CrpStatus), false, false);
    }

    public static bool MayCallProvider(Gate gate) =>
        gate.MayCallProvider && gate.GenerationAllowed && !AutoSelectProvider() && !AutoRetry() && !AutoApprove();

    public static string StaffOf(string? status) =>
        IsRejected(status) ? StaffRejected
        : IsPendingReview(status) ? StaffPending
        : IsLocked(status) ? CharacterReferenceApprovalLockRules.StaffLocked
        : StaffRegenerate;

    public static string ConfirmBody(string providerLabel) =>
        "Bộ ảnh tham chiếu hiện tại đã bị đánh giá Không đạt.\n\n"
        + "Famixa sẽ tạo một bộ ảnh tham chiếu mới gồm 4 góc:\n"
        + "• Trước mặt\n• 3/4\n• Nghiêng\n• Toàn thân\n\n"
        + "Nhân vật sẽ được giữ theo Master + DNA + PRP hiện tại.\n\n"
        + $"Provider:\n{providerLabel}\n\n"
        + "Bộ ảnh cũ sẽ được giữ lại trong lịch sử và không bị ghi đè.\n\n"
        + "Bạn có muốn tạo lại không?";

    private static Gate Block(string code, string staff) =>
        new("BLOCKED", code, staff, false, false, false, false);

    private static readonly JsonSerializerOptions Canonical = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

public static class CharacterReferenceRegenerationV1Application
{
    public sealed record Outcome(
        CharacterReferenceRegenerationV1Rules.Gate Gate,
        string Status,
        string Version,
        bool ProviderCalled,
        int ProviderCalls,
        bool GeminiCalled,
        bool RunwayCalled,
        bool VeoCalled,
        bool Generation,
        int Coverage,
        IReadOnlyList<string> Types,
        bool Approved,
        bool Locked,
        bool CanUse,
        bool HistoryPreserved,
        bool OverwroteOld,
        string? Fingerprint,
        string? MasterSha,
        string? DnaSha,
        string? PrpSha);

    public static Outcome Reject(
        CharacterReferenceRegenerationV1Rules.GateInput input,
        string currentVersion,
        string masterSha,
        string dnaSha,
        string prpSha)
    {
        var gate = CharacterReferenceRegenerationV1Rules.Evaluate(input with { Action = "REJECT" });
        if (gate.Code is not null)
            return Empty(gate, currentVersion, masterSha, dnaSha, prpSha);
        return new Outcome(
            gate, CharacterReferenceRegenerationV1Rules.Rejected, currentVersion,
            false, 0, false, false, false, false, 4, CharacterReferenceRegenerationV1Rules.RequiredTypes,
            false, false, false, true, false, null, masterSha, dnaSha, prpSha);
    }

    public static async Task<Outcome> RegenerateAsync(
        CharacterReferenceRegenerationV1Rules.GateInput input,
        string currentVersion,
        string masterSha,
        string dnaSha,
        string prpSha,
        ICharacterReferenceGenerationProvider provider,
        CharacterReferenceSetRequest request,
        CancellationToken cancellationToken = default)
    {
        var nextVersion = CharacterReferenceRegenerationV1Rules.NextVersion(currentVersion);
        var fingerprint = CharacterReferenceRegenerationV1Rules.ExecutionFingerprint(
            request.CharacterId, request.EraId, masterSha, dnaSha, prpSha, nextVersion, input.DirectorProvider);
        var gate = CharacterReferenceRegenerationV1Rules.Evaluate(input with { Action = "EXECUTE" });
        if (!CharacterReferenceRegenerationV1Rules.MayCallProvider(gate))
            return Empty(gate, currentVersion, masterSha, dnaSha, prpSha, fingerprint);

        var result = await provider.GenerateSetAsync(request, cancellationToken);
        var calls = provider is MockCharacterReferenceSetProvider mock ? mock.CallCount : 1;
        var gemini = string.Equals(provider.ProviderId, "GEMINI", StringComparison.OrdinalIgnoreCase)
                     || provider is MockCharacterReferenceSetProvider;
        var types = result.Views.Where(v => v.Succeeded && v.Bytes is { Length: > 0 }).Select(v => v.ReferenceType).ToList();
        var coverage = CharacterReferenceRegenerationV1Rules.RequiredTypes.Count(t =>
            types.Contains(t, StringComparer.OrdinalIgnoreCase));
        var complete = coverage == 4 && result.Succeeded;
        if (!complete)
        {
            return new Outcome(
                gate with
                {
                    Status = "FAILED",
                    Code = coverage == 0 ? "REFERENCE_GENERATION_FAILED" : "REFERENCE_SET_INCOMPLETE",
                    StaffMessage = coverage == 0
                        ? CharacterReferenceRegenerationV1Rules.StaffFail
                        : CharacterReferenceRegenerationV1Rules.StaffIncomplete,
                    MayCallProvider = false,
                    GenerationAllowed = false,
                },
                CharacterReferenceRegenerationV1Rules.Rejected, currentVersion,
                true, calls, gemini, false, false, false, coverage, types,
                false, false, false, true, false, fingerprint, masterSha, dnaSha, prpSha);
        }

        return new Outcome(
            gate, CharacterReferenceRegenerationV1Rules.PendingReview, nextVersion,
            true, calls, gemini, false, false, true, 4, CharacterReferenceRegenerationV1Rules.RequiredTypes,
            false, false, false, true, false, fingerprint, masterSha, dnaSha, prpSha);
    }

    private static Outcome Empty(
        CharacterReferenceRegenerationV1Rules.Gate gate,
        string version,
        string masterSha,
        string dnaSha,
        string prpSha,
        string? fingerprint = null) =>
        new(gate, gate.Status, version, false, 0, false, false, false, false, 0, [],
            false, false, false, true, false, fingerprint, masterSha, dnaSha, prpSha);
}
