namespace KitPlatform.Packs.Pharmacy.Knowledge;

public sealed record PharmacyKnowledgeVersionDto(
    string RuleCode,
    string Version,
    string LifecycleStatus,
    string Source,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    string? SymptomCode,
    string[] CategoryCodes,
    string[] Keywords,
    string[] ExcludeKeywords,
    string ReasonVi,
    int Priority,
    string SourceVerificationStatus,
    string? ReferenceSource,
    string? SourceIdentity,
    string? SourceUrl,
    string? DocumentRef,
    string? IssuingOrganization,
    DateTime? SourcePublishedOn,
    string? EvidenceNote,
    string? VerifiedBy,
    DateTime? VerifiedAt);

public sealed record PharmacyKnowledgeSourceVerificationRequest(
    string Status,
    string? ReferenceSource,
    string? SourceIdentity,
    string? SourceUrl,
    string? DocumentRef,
    string? IssuingOrganization,
    DateTime? SourcePublishedOn,
    string? EvidenceNote);

public sealed record PharmacyKnowledgeContentUpdateRequest(
    string ReasonVi,
    string[]? Keywords,
    string[]? ExcludeKeywords,
    string[]? CategoryCodes,
    Guid? SymptomId);

public sealed record PharmacyKnowledgeAdoptionViewDto(
    string RuleCode,
    string RuleVersion,
    string Status,
    string? ReferenceSource,
    bool ProductMatchingEnabled,
    string[] ExtraExcludeKeywords,
    int? PriorityOverride);

public sealed record PharmacyTenantKnowledgeCatalogDto(
    IReadOnlyList<PharmacyKnowledgeVersionDto> PublishedVersions,
    IReadOnlyList<PharmacyKnowledgeAdoptionViewDto> Adoptions);

public sealed record PharmacyKnowledgePublicationEventDto(
    string RuleCode,
    string Version,
    string FromStatus,
    string ToStatus,
    string ActorUserId,
    DateTime CreatedAt);

public interface IPharmacyKnowledgePublicationService
{
    Task<IReadOnlyList<PharmacyKnowledgeVersionDto>> ListVersionsAsync(
        CancellationToken cancellationToken = default);

    Task<PharmacyKnowledgeVersionDto?> GetVersionAsync(
        string ruleCode,
        string version,
        CancellationToken cancellationToken = default);

    Task<PharmacyTenantKnowledgeCatalogDto> ListForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<PharmacyKnowledgeVersionDto?> GetVisibleToTenantAsync(
        Guid tenantId,
        string ruleCode,
        string version,
        CancellationToken cancellationToken = default);

    Task<PharmacyKnowledgeVersionDto> PublishAsync(
        string ruleCode,
        string version,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<PharmacyKnowledgeVersionDto> RetireAsync(
        string ruleCode,
        string version,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<PharmacyKnowledgeVersionDto> SubmitReviewAsync(
        string ruleCode,
        string version,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<PharmacyKnowledgeVersionDto> ReturnToDraftAsync(
        string ruleCode,
        string version,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<PharmacyKnowledgeVersionDto> VerifySourceAsync(
        string ruleCode,
        string version,
        Guid userId,
        PharmacyKnowledgeSourceVerificationRequest request,
        CancellationToken cancellationToken = default);

    Task<PharmacyKnowledgeVersionDto> UpdateUnpublishedContentAsync(
        string ruleCode,
        string version,
        PharmacyKnowledgeContentUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PharmacyKnowledgePublicationEventDto>> ListHistoryAsync(
        string? ruleCode,
        CancellationToken cancellationToken = default);
}
