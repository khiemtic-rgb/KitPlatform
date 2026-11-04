namespace KitPlatform.Packs.Pharmacy.Knowledge;

public sealed record PharmacyKnowledgeApprovalRequest(
    string Status,
    string? ReferenceSource,
    string? Version = null,
    bool AddsKeywords = false,
    bool AddsCategories = false);

public sealed record PharmacyKnowledgePolicyRequest(
    bool ProductMatchingEnabled,
    string[]? ExtraExcludeKeywords,
    int? PriorityOverride,
    bool AddsKeywords = false,
    bool AddsCategories = false);

public sealed record PharmacyKnowledgeRuleApprovalDto(
    string RuleCode,
    string ApprovalStatus,
    string? ApprovedBy,
    DateTime? ApprovedAt,
    string? ReferenceSource,
    string? Version = null);

public sealed record PharmacyKnowledgePolicyDto(
    string RuleCode,
    string RuleVersion,
    bool ProductMatchingEnabled,
    string[] ExtraExcludeKeywords,
    int? PriorityOverride);

public sealed record PharmacyKnowledgeApprovalEventDto(
    string RuleCode,
    string? Version,
    string FromStatus,
    string ToStatus,
    string ActorUserId,
    string? ReferenceSource,
    DateTime CreatedAt);

public interface IPharmacyKnowledgeApprovalService
{
    Task<PharmacyKnowledgeRuleApprovalDto?> TransitionAsync(
        string ruleCode,
        PharmacyKnowledgeApprovalRequest request,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<PharmacyKnowledgePolicyDto> SavePolicyAsync(
        string ruleCode,
        PharmacyKnowledgePolicyRequest request,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PharmacyKnowledgeApprovalEventDto>> ListHistoryAsync(
        string ruleCode,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
