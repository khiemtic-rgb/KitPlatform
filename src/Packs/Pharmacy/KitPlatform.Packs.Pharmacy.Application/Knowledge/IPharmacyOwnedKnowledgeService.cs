namespace KitPlatform.Packs.Pharmacy.Knowledge;

public interface IPharmacyOwnedKnowledgeService
{
    Task<IReadOnlyList<PharmacyOwnedKnowledgeSummaryDto>> ListAsync(
        Guid tenantId,
        string? kind,
        CancellationToken cancellationToken = default,
        string? query = null,
        string? lifecycleStatus = null,
        string? classification = null);

    Task<IReadOnlyList<PharmacyOwnedKnowledgeInUseDto>> ListInUseAsync(
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        string? kind,
        string? query,
        string? classification,
        CancellationToken cancellationToken = default);

    Task<PharmacyOwnedKnowledgeInUseDto?> GetInUseAsync(
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        Guid knowledgeId,
        CancellationToken cancellationToken = default);

    Task<PharmacyOwnedKnowledgeDetailDto?> GetAsync(
        Guid tenantId,
        Guid knowledgeId,
        CancellationToken cancellationToken = default);

    Task<PharmacyOwnedKnowledgeDetailDto> CreateAsync(
        PharmacyOwnedKnowledgeWriteRequest request,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default,
        bool isTenantAdmin = true);

    Task<PharmacyOwnedKnowledgeDetailDto> UpdateAsync(
        Guid knowledgeId,
        PharmacyOwnedKnowledgeWriteRequest request,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default,
        bool isTenantAdmin = true);

    Task<PharmacyOwnedKnowledgeDetailDto> SubmitAsync(
        Guid knowledgeId,
        Guid versionId,
        DateTime expectedUpdatedAt,
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        CancellationToken cancellationToken = default);

    Task<PharmacyOwnedKnowledgeDetailDto> ReturnAsync(
        Guid knowledgeId,
        Guid versionId,
        DateTime expectedUpdatedAt,
        string? note,
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        CancellationToken cancellationToken = default);

    Task<PharmacyOwnedKnowledgeDetailDto> ApproveAsync(
        Guid knowledgeId,
        Guid versionId,
        DateTime expectedUpdatedAt,
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        CancellationToken cancellationToken = default,
        DateTime? reviewDueOn = null);

    Task<PharmacyOwnedKnowledgeDetailDto> StartNextDraftAsync(
        Guid knowledgeId,
        Guid sourceVersionId,
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        CancellationToken cancellationToken = default);

    Task<PharmacyOwnedKnowledgeDetailDto> ActivateAsync(
        Guid knowledgeId,
        Guid versionId,
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        CancellationToken cancellationToken = default,
        DateTime? reviewDueOn = null);

    Task<PharmacyOwnedKnowledgeDetailDto> StopAsync(
        Guid knowledgeId,
        Guid versionId,
        string? note,
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        CancellationToken cancellationToken = default);

    Task<PharmacyOwnedKnowledgeDetailDto> ResumeAsync(
        Guid knowledgeId,
        Guid versionId,
        string? note,
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        CancellationToken cancellationToken = default);

    Task RecordFeedbackAsync(
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        PharmacyKnowledgeFeedbackRequest request,
        CancellationToken cancellationToken = default);

    Task RecordOpenAsync(
        Guid tenantId,
        Guid userId,
        Guid knowledgeId,
        CancellationToken cancellationToken = default);

    Task<PharmacyKnowledgeFeedbackSummaryDto> GetFeedbackSummaryAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task GrantAsync(
        Guid tenantId,
        Guid actorUserId,
        bool isTenantAdmin,
        Guid granteeUserId,
        string? action,
        string? kind,
        CancellationToken cancellationToken = default);
}
