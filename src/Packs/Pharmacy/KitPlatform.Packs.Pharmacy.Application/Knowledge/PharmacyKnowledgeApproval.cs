namespace KitPlatform.Packs.Pharmacy.Knowledge;

public static class PharmacyKnowledgeApprovalAccess
{
    /// <summary>
    /// Pin trong tenant của token ERP. Claim tường minh vẫn đủ.
    /// Vai trò ADMIN của đúng tenant đó cũng đủ, và không mở review, xác minh nguồn hay xuất bản.
    /// </summary>
    public static bool CanApprove(bool isErpPrincipal, bool hasApprovePermission, bool isTenantAdmin = false) =>
        isErpPrincipal && (hasApprovePermission || isTenantAdmin);

    /// <summary>
    /// Xuất bản catalog chung. Không nằm trong picker nhà thuốc và không được migration gán.
    /// </summary>
    public const string PlatformPublishPermission = "platform.knowledge.publish";

    /// <summary>Content review. Not seeded and not in the pharmacy permission picker.</summary>
    public const string ReviewPermission = "pharmacy.knowledge.review";

    /// <summary>Professional source acceptance. Not seeded and not a substitute for publish.</summary>
    public const string VerifySourcePermission = "pharmacy.knowledge.verify-source";

    public static bool CanPublish(bool isErpPrincipal, bool hasPlatformPublishPermission) =>
        isErpPrincipal && hasPlatformPublishPermission;

    public static bool CanReview(bool isErpPrincipal, bool hasReviewPermission) =>
        isErpPrincipal && hasReviewPermission;

    public static bool CanVerifySource(bool isErpPrincipal, bool hasVerifySourcePermission) =>
        isErpPrincipal && hasVerifySourcePermission;

    public static bool CanReadCatalog(
        bool isErpPrincipal,
        bool hasPublishPermission,
        bool hasReviewPermission,
        bool hasVerifySourcePermission) =>
        CanPublish(isErpPrincipal, hasPublishPermission)
        || CanReview(isErpPrincipal, hasReviewPermission)
        || CanVerifySource(isErpPrincipal, hasVerifySourcePermission);
}

public static class PharmacyKnowledgeCopilotQuery
{
    public const string TenantPublishedPin =
        "a.tenant_id = @TenantId AND a.status = 'APPROVED' AND v.lifecycle_status = 'PUBLISHED'";

    /// <summary>
    /// Catch-all seed rule. It has no keywords. Copilot must not run it.
    /// </summary>
    public const string OperationallyExcludedRuleCode = "KR_OTHER";

    public const string ExcludeUnverifiedCatchAll =
        "v.rule_code <> '" + OperationallyExcludedRuleCode + "'";
}

public static class PharmacyKnowledgeApproval
{
    public const string Draft = "DRAFT";
    public const string InReview = "IN_REVIEW";
    public const string Published = "PUBLISHED";
    public const string Retired = "RETIRED";
    public const string Approved = "APPROVED";
    public const string PendingReview = "PENDING_REVIEW";
    public const string None = "NONE";

    public static bool IsUsableByCopilot(string? adoptionStatus, string? lifecycleStatus) =>
        string.Equals(adoptionStatus, Approved, StringComparison.Ordinal)
        && string.Equals(lifecycleStatus, Published, StringComparison.Ordinal);

    public static string? PreserveReference(string? existing, string? incoming) =>
        string.IsNullOrWhiteSpace(existing) ? incoming : existing;

    public const string PreserveReferenceSql =
        "reference_source = COALESCE(reference_source, @ReferenceSource)";

    public static string NormalizeLifecycle(string? status)
    {
        var value = status?.Trim().ToUpperInvariant();
        return value is Draft or InReview or Published or Retired
            ? value
            : throw new InvalidOperationException("Trạng thái phiên bản không hợp lệ.");
    }

    public static string NormalizeAdoption(string? status)
    {
        var value = status?.Trim().ToUpperInvariant();
        return value is Approved or Retired
            ? value
            : throw new InvalidOperationException("Trạng thái pin không hợp lệ.");
    }

    /// <summary>Global content lifecycle. PUBLISHED does not turn Copilot on.</summary>
    public static string Transition(string current, string target)
    {
        var from = NormalizeLifecycle(current);
        var to = NormalizeLifecycle(target);
        var allowed = (from, to) switch
        {
            (Draft, InReview) => true,
            (InReview, Published) => true,
            (InReview, Draft) => true,
            (Published, Retired) => true,
            (Draft, Retired) => true,
            (InReview, Retired) => true,
            _ => false,
        };
        if (!allowed || from == to)
            throw new InvalidOperationException($"Không chuyển phiên bản từ {from} sang {to}.");
        return to;
    }

    public static void EnsureVersionMayBePinned(
        string? ruleCode,
        string? lifecycleStatus,
        DateTime? effectiveFrom,
        DateTime? effectiveTo,
        DateTime today,
        string? sourceVerificationStatus)
    {
        if (string.Equals(ruleCode?.Trim(), PharmacyKnowledgeCopilotQuery.OperationallyExcludedRuleCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("KR_OTHER không được ghim.");
        if (!string.Equals(lifecycleStatus, Published, StringComparison.Ordinal))
            throw new InvalidOperationException("Chỉ được ghim phiên bản đã xuất bản.");
        if (effectiveFrom is DateTime from && from.Date > today.Date)
            throw new InvalidOperationException("Phiên bản chưa tới ngày hiệu lực.");
        if (effectiveTo is DateTime to && to.Date < today.Date)
            throw new InvalidOperationException("Phiên bản đã hết hiệu lực.");
        if (!string.Equals(sourceVerificationStatus?.Trim(), PharmacyKnowledgeSourceRules.Accepted, StringComparison.Ordinal))
            throw new InvalidOperationException("Chỉ được ghim phiên bản có nguồn ACCEPTED.");
    }

    public static void RejectPolicyExpansion(
        bool addsKeywords,
        bool addsCategories,
        int versionPriority,
        int? priorityOverride)
    {
        if (addsKeywords || addsCategories)
            throw new InvalidOperationException("Chính sách nhà thuốc không được mở rộng nội dung quy tắc.");
        if (priorityOverride is int value && value < versionPriority)
            throw new InvalidOperationException("Chỉ được giảm độ ưu tiên của quy tắc đã duyệt.");
    }
}
