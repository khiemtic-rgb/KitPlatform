namespace KitPlatform.Packs.Pharmacy.Knowledge;

public static class PharmacyCustomerKnowledgeRules
{
    public static readonly string[] Kinds =
    [
        "CHRONIC",
        "ALLERGY_DRUG",
        "ALLERGY_OTHER",
        "ADVERSE_REACTION",
        "MEDICATION_USE",
        "FOLLOW_UP",
    ];

    public static readonly string[] Sources =
    [
        "SELF_REPORT",
        "PRESCRIPTION",
        "TRANSACTION",
        "PROFESSIONAL",
        "UNVERIFIED",
    ];

    public static string NormalizeKind(string? kind)
    {
        var value = kind?.Trim().ToUpperInvariant();
        if (value is null || !Kinds.Contains(value, StringComparer.Ordinal))
            throw new InvalidOperationException("Loại ghi nhận không hợp lệ.");
        return value;
    }

    public static string NormalizeSource(string? source)
    {
        var value = source?.Trim().ToUpperInvariant();
        if (value is null || !Sources.Contains(value, StringComparer.Ordinal))
            throw new InvalidOperationException("Nguồn ghi nhận không hợp lệ.");
        return value;
    }

    public static string RequireBody(string? body)
    {
        var value = body?.Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Thiếu nội dung ghi nhận.");
        if (value.Length > 2000)
            throw new InvalidOperationException("Nội dung ghi nhận vượt quá 2000 ký tự.");
        return value;
    }
}

public sealed record CustomerKnowledgeEntryDto(
    Guid Id,
    string Kind,
    string Body,
    string SourceKind,
    bool NeedsVerification,
    string RecordedBy,
    string? RecordedByName,
    DateTimeOffset RecordedAt);

public sealed record CustomerKnowledgeWriteRequest(
    Guid CustomerId,
    string Kind,
    string Body,
    string SourceKind,
    bool NeedsVerification);

public sealed record CustomerKnowledgeListResult(
    IReadOnlyList<CustomerKnowledgeEntryDto> Entries,
    bool StoreReady);

public interface IPharmacyCustomerKnowledgeService
{
    Task<CustomerKnowledgeListResult> ListAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<CustomerKnowledgeEntryDto> CreateAsync(
        Guid tenantId,
        Guid userId,
        CustomerKnowledgeWriteRequest request,
        CancellationToken cancellationToken = default);

    Task RevokeAsync(
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        Guid entryId,
        CancellationToken cancellationToken = default);
}
