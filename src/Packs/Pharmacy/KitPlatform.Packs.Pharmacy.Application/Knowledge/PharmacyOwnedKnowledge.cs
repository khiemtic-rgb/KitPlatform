namespace KitPlatform.Packs.Pharmacy.Knowledge;

public static class PharmacyKnowledgeTenantScope
{
    /// <summary>
    /// Loại hình lấy từ settings.platform.vertical của đúng tenant.
    /// Không đọc mã tenant. Marketing, clinic, family và local không vào đây.
    /// Hybrid là tenant hỗn hợp còn vận hành nhà thuốc; KIT_MKT dùng vertical marketing.
    /// </summary>
    public static bool AllowsPharmacyKnowledge(string? settingsVertical)
    {
        var value = settingsVertical?.Trim();
        if (string.IsNullOrEmpty(value))
            return false;
        return value.Equals("pharmacy", StringComparison.OrdinalIgnoreCase)
            || value.Equals("pharmacy_chain", StringComparison.OrdinalIgnoreCase)
            || value.Equals("supplement_store", StringComparison.OrdinalIgnoreCase)
            || value.Equals("medical_equipment_store", StringComparison.OrdinalIgnoreCase)
            || value.Equals("hybrid", StringComparison.OrdinalIgnoreCase);
    }
}

public static class PharmacyOwnedKnowledgeAccess
{
    public const string DraftAction = "draft";
    public const string SubmitAction = "submit";
    public const string ApproveAction = "approve";
    public const string ActivateAction = "activate";
    public const string ReadAction = "read";

    /// <summary>
    /// Soạn nháp của đúng tenant. Chỉ vai trò ADMIN trên token ERP.
    /// Không suy ra quyền xuất bản, kiểm chứng nguồn, hoặc pin.
    /// </summary>
    public static bool CanManage(bool isErpPrincipal, bool isTenantAdmin) =>
        isErpPrincipal && isTenantAdmin;

    public static bool Allows(
        bool isTenantAdmin,
        string action,
        string kind,
        IEnumerable<(string Action, string Kind)> grants)
    {
        if (string.Equals(kind, PharmacyOwnedKnowledgeRules.ClinicalKnowledge, StringComparison.Ordinal)
            && string.Equals(action, ActivateAction, StringComparison.Ordinal))
            return false;
        if (isTenantAdmin)
            return true;
        return grants.Any(grant =>
            string.Equals(grant.Action, action, StringComparison.Ordinal)
            && string.Equals(grant.Kind, kind, StringComparison.Ordinal));
    }
}

public static class PharmacyOwnedKnowledgeRules
{
    public const string Draft = "DRAFT";
    public const string ExactSnapshot = "EXACT";
    public const string LegacyUnavailable = "LEGACY_UNAVAILABLE";
    public const string Guidance = "GUIDANCE";
    public const string OperatingProcedure = "OPERATING_PROCEDURE";
    public const string OperationalPolicy = "OPERATIONAL_POLICY";
    public const string ProductKnowledge = "PRODUCT_KNOWLEDGE";
    public const string ClinicalKnowledge = "CLINICAL_KNOWLEDGE";

    public static readonly string[] Kinds =
    [
        Guidance,
        OperatingProcedure,
        OperationalPolicy,
        ProductKnowledge,
        ClinicalKnowledge,
    ];

    public static string NormalizeKind(string? kind)
    {
        var value = kind?.Trim().ToUpperInvariant();
        if (value is null || !Kinds.Contains(value, StringComparer.Ordinal))
            throw new InvalidOperationException("Loại tri thức không hợp lệ.");
        return value;
    }

    public static string RequireTitle(string? title)
    {
        var value = title?.Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Thiếu tiêu đề.");
        if (value.Length > 200)
            throw new InvalidOperationException("Tiêu đề vượt quá 200 ký tự.");
        return value;
    }

    public static string RequireBody(string? body)
    {
        var value = body?.Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Thiếu nội dung.");
        if (value.Length > 20000)
            throw new InvalidOperationException("Nội dung vượt quá 20000 ký tự.");
        return value;
    }

    public static string? NormalizeClassification(string? classification)
    {
        var value = classification?.Trim();
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (value.Length > 200)
            throw new InvalidOperationException("Phân loại vượt quá 200 ký tự.");
        return value;
    }

    public static void EnsureDraft(string? lifecycleStatus)
    {
        if (!string.Equals(lifecycleStatus, Draft, StringComparison.Ordinal))
            throw new InvalidOperationException("Chỉ sửa được bản nháp.");
    }
}

public sealed record PharmacyOwnedKnowledgeWriteRequest(
    string? Kind,
    string? Title,
    string? Body,
    string? Classification);

public sealed record PharmacyOwnedKnowledgeTransitionRequest(
    Guid VersionId,
    DateTime ExpectedUpdatedAt,
    string? Note,
    DateTime? ReviewDueOn = null);

public sealed record PharmacyOwnedKnowledgeGrantRequest(
    Guid UserId,
    string? Action,
    string? Kind);

public sealed record PharmacyOwnedKnowledgeSummaryDto(
    Guid Id,
    string Kind,
    string Title,
    string? Classification,
    int Version,
    string LifecycleStatus,
    DateTime UpdatedAt,
    string? ApplicationStatus,
    DateTime? ReviewDueOn = null,
    bool ReviewOverdue = false);

public sealed record PharmacyOwnedKnowledgeEventDto(
    Guid Id,
    string Action,
    int Version,
    string Kind,
    string Title,
    string? Body,
    string? Classification,
    string SnapshotFidelity,
    string ActorUserId,
    DateTime CreatedAt,
    string? Note);

public sealed record PharmacyOwnedKnowledgeDetailDto(
    Guid Id,
    string Kind,
    string Title,
    string Body,
    string? Classification,
    int Version,
    string LifecycleStatus,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<PharmacyOwnedKnowledgeEventDto> History,
    IReadOnlyList<PharmacyOwnedKnowledgeVersionBriefDto> Versions,
    IReadOnlyList<PharmacyOwnedKnowledgeActivationDto> Activations);

public sealed record PharmacyOwnedKnowledgeVersionBriefDto(
    Guid VersionId,
    int Version,
    string Kind,
    string Title,
    string LifecycleStatus,
    DateTime UpdatedAt,
    string? Body);

public sealed record PharmacyOwnedKnowledgeInUseDto(
    Guid Id,
    string Kind,
    string Title,
    string? Classification,
    int Version,
    string Status,
    string Body,
    DateTime UpdatedAt,
    DateTime? ReviewDueOn = null,
    bool ReviewOverdue = false);

public sealed record PharmacyOwnedKnowledgeActivationDto(
    Guid VersionId,
    int Version,
    string Status);

public static class PharmacyOwnedKnowledgeDocument
{
    public static IReadOnlyList<string> RequiredHeadings(string kind) => kind switch
    {
        PharmacyOwnedKnowledgeRules.Guidance => ["Mục đích", "Đối tượng áp dụng", "Nội dung chính", "Giới hạn"],
        PharmacyOwnedKnowledgeRules.OperatingProcedure => ["Mục đích", "Phạm vi áp dụng", "Các bước thực hiện", "Người chịu trách nhiệm"],
        PharmacyOwnedKnowledgeRules.OperationalPolicy => ["Mục đích", "Phạm vi áp dụng", "Điều kiện áp dụng", "Nội dung chính"],
        PharmacyOwnedKnowledgeRules.ProductKnowledge => ["Phạm vi áp dụng", "Nội dung chính", "Cảnh báo và giới hạn"],
        PharmacyOwnedKnowledgeRules.ClinicalKnowledge => ["Mục đích", "Nội dung chính", "Cảnh báo và giới hạn"],
        _ => throw new InvalidOperationException("Loại tri thức không hợp lệ."),
    };

    public static void EnsureSubmittable(string kind, string body)
    {
        var sections = Parse(body);
        foreach (var heading in RequiredHeadings(kind))
        {
            if (!sections.TryGetValue(heading, out var text) || string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException($"Thiếu mục \"{heading}\".");
        }
    }

    public static Dictionary<string, string> Parse(string? body)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(body))
            return map;
        string? heading = null;
        var buffer = new System.Text.StringBuilder();
        foreach (var raw in body.Replace("\r\n", "\n").Split('\n'))
        {
            if (raw.StartsWith("## ", StringComparison.Ordinal))
            {
                if (heading is not null)
                    map[heading] = buffer.ToString().Trim();
                heading = raw[3..].Trim();
                buffer.Clear();
            }
            else if (heading is not null)
            {
                if (buffer.Length > 0)
                    buffer.Append('\n');
                buffer.Append(raw);
            }
        }

        if (heading is not null)
            map[heading] = buffer.ToString().Trim();
        return map;
    }
}
