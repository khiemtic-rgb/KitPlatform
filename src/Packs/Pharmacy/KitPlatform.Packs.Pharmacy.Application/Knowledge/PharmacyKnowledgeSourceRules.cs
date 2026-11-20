namespace KitPlatform.Packs.Pharmacy.Knowledge;

public static class PharmacyKnowledgeSourceRules
{
    public const string Unverified = "UNVERIFIED";
    public const string Accepted = "ACCEPTED";
    public const string Rejected = "REJECTED";
    public const string SystemSourceLabel = "novixa";

    public static string NormalizeStatus(string? status)
    {
        var value = status?.Trim().ToUpperInvariant();
        return value is Unverified or Accepted or Rejected
            ? value
            : throw new InvalidOperationException("Trạng thái nguồn không hợp lệ.");
    }

    public static void EnsureEditable(string? lifecycleStatus)
    {
        var status = PharmacyKnowledgeApproval.NormalizeLifecycle(lifecycleStatus);
        if (status is not (PharmacyKnowledgeApproval.Draft or PharmacyKnowledgeApproval.InReview))
            throw new InvalidOperationException("Chỉ sửa phiên bản còn nháp hoặc đang xét.");
    }

    /// <summary>
    /// IN_REVIEW plus an accepted citation. Does not write a row.
    /// </summary>
    public static string EnsureMayPublish(
        string? ruleCode,
        string? lifecycleStatus,
        string? verificationStatus,
        string? referenceSource,
        string? sourceIdentity,
        string? sourceUrl,
        string? documentRef,
        string? issuingOrganization,
        string? evidenceNote,
        string? verifiedBy,
        DateTime? verifiedAt)
    {
        var next = PharmacyKnowledgeApproval.Transition(lifecycleStatus ?? "", PharmacyKnowledgeApproval.Published);
        if (string.Equals(ruleCode?.Trim(), PharmacyKnowledgeCopilotQuery.OperationallyExcludedRuleCode, StringComparison.Ordinal))
            throw new InvalidOperationException("KR_OTHER không được xuất bản.");
        if (!string.Equals(NormalizeStatus(verificationStatus), Accepted, StringComparison.Ordinal))
            throw new InvalidOperationException("Chỉ xuất bản phiên bản có nguồn đã được chấp nhận.");
        RequireAcceptedCitation(
            referenceSource,
            sourceIdentity,
            sourceUrl,
            documentRef,
            issuingOrganization,
            evidenceNote,
            verifiedBy,
            verifiedAt);
        return next;
    }

    public static void EnsureVerificationRequest(
        string? status,
        string? referenceSource,
        string? sourceIdentity,
        string? sourceUrl,
        string? documentRef,
        string? issuingOrganization,
        string? evidenceNote)
    {
        var normalized = NormalizeStatus(status);
        if (normalized == Unverified)
            throw new InvalidOperationException("Chỉ ghi nhận ACCEPTED hoặc REJECTED.");
        if (normalized == Rejected)
            return;
        RequireAcceptedCitation(
            referenceSource,
            sourceIdentity,
            sourceUrl,
            documentRef,
            issuingOrganization,
            evidenceNote,
            verifiedBy: "reviewer",
            verifiedAt: DateTime.UtcNow);
    }

    /// <summary>
    /// A content edit that does not send a symptom keeps the symptom already stored.
    /// Sending a different id still counts as a content change.
    /// </summary>
    public static Guid? SymptomIdForContentEdit(Guid? requested, Guid? current) =>
        requested ?? current;

    public static bool ClearsAcceptance(
        string? reasonVi,
        string? nextReasonVi,
        Guid? symptomId,
        Guid? nextSymptomId,
        IEnumerable<string>? keywords,
        IEnumerable<string>? nextKeywords,
        IEnumerable<string>? excludeKeywords,
        IEnumerable<string>? nextExcludeKeywords,
        IEnumerable<string>? categoryCodes,
        IEnumerable<string>? nextCategoryCodes) =>
        !string.Equals(reasonVi ?? "", nextReasonVi ?? "", StringComparison.Ordinal)
        || symptomId != nextSymptomId
        || !Same(keywords, nextKeywords)
        || !Same(excludeKeywords, nextExcludeKeywords)
        || !Same(categoryCodes, nextCategoryCodes);

    private static void RequireAcceptedCitation(
        string? referenceSource,
        string? sourceIdentity,
        string? sourceUrl,
        string? documentRef,
        string? issuingOrganization,
        string? evidenceNote,
        string? verifiedBy,
        DateTime? verifiedAt)
    {
        RequireText(referenceSource, "Câu trích dẫn");
        RequireText(sourceIdentity, "Định danh nguồn");
        RequireText(issuingOrganization, "Cơ quan ban hành");
        RequireText(evidenceNote, "Vị trí bằng chứng");
        if (IsSystemLabel(referenceSource) || IsSystemLabel(sourceIdentity))
            throw new InvalidOperationException("Nhãn novixa không phải nguồn chuyên môn.");
        if (string.IsNullOrWhiteSpace(sourceUrl) && string.IsNullOrWhiteSpace(documentRef))
            throw new InvalidOperationException("Cần URL hoặc mã hồ sơ của nguồn.");
        if (string.IsNullOrWhiteSpace(verifiedBy) || verifiedAt is null)
            throw new InvalidOperationException("Thiếu người hoặc thời điểm kiểm chứng nguồn.");
    }

    private static void RequireText(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Thiếu {label}.");
    }

    private static bool IsSystemLabel(string? value) =>
        string.Equals(value?.Trim(), SystemSourceLabel, StringComparison.OrdinalIgnoreCase);

    private static bool Same(IEnumerable<string>? left, IEnumerable<string>? right) =>
        (left ?? []).SequenceEqual(right ?? [], StringComparer.Ordinal);
}
