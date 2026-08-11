namespace KitPlatform.Application.Customers;

public sealed record CustomerAdminListItemDto(
    Guid Id,
    string CustomerCode,
    string FullName,
    string Phone,
    string? Email,
    short Status,
    DateTimeOffset CreatedAt,
    Guid? CustomerGroupId = null,
    string? CustomerGroupName = null,
    decimal GroupDiscountPercent = 0,
    bool HasAppAccount = false,
    DateTimeOffset? AppLastLoginAt = null,
    string AcquisitionSource = CustomerAcquisitionSources.Counter,
    string PharmacyRelation = CustomerPharmacyRelations.Member);

public sealed record PagedCustomersResult(
    IReadOnlyList<CustomerAdminListItemDto> Items,
    int Total,
    int Page,
    int PageSize);

/// <summary>CRM + Mode A phone readiness for the current tenant.</summary>
public sealed record CustomerPharmacyRelationSummaryDto(
    int Prospect,
    int Member,
    int Revoked,
    int Total,
    int HasAppAccount,
    int ValidVnMobile = 0,
    int PhoneNeedsFix = 0,
    int DuplicatePhoneGroups = 0,
    int CustomersInDuplicateGroups = 0,
    int ModeAReady = 0);

public sealed record SimilarCustomerMemberDto(
    Guid Id,
    string CustomerCode,
    string FullName,
    string Phone,
    string? Email,
    short Status,
    DateTimeOffset CreatedAt,
    int OrderCount = 0);

public sealed record SimilarCustomerClusterDto(
    string ClusterKey,
    string MatchKind,
    string DisplayLabel,
    double? MaxSimilarity,
    IReadOnlyList<SimilarCustomerMemberDto> Customers);

public sealed record SimilarCustomerClustersResult(
    IReadOnlyList<SimilarCustomerClusterDto> Clusters,
    int ClusterCount,
    int CustomerCount,
    double SimilarityThreshold);

public sealed record SimilarCustomerNameDto(
    Guid Id,
    string CustomerCode,
    string FullName,
    string Phone,
    double SimilarityScore);

public sealed record SimilarCustomerNamesResult(
    IReadOnlyList<SimilarCustomerNameDto> Matches,
    bool HasExactNormalizedMatch);

public sealed record CustomerDetailDto(
    Guid Id,
    string CustomerCode,
    string FullName,
    string Phone,
    string? Email,
    DateOnly? DateOfBirth,
    short? Gender,
    short Status,
    DateTimeOffset CreatedAt,
    bool HasAppAccount,
    bool? AppVerified,
    DateTimeOffset? AppLastLoginAt,
    bool AllowCredit = false,
    decimal? CreditLimit = null,
    string? AddressLine = null,
    string? IdNumber = null,
    string? EmergencyContactName = null,
    string? EmergencyContactPhone = null,
    string? ClinicalNotes = null,
    Guid? CustomerGroupId = null,
    string? CustomerGroupName = null,
    decimal GroupDiscountPercent = 0,
    string AcquisitionSource = CustomerAcquisitionSources.Counter,
    string PharmacyRelation = CustomerPharmacyRelations.Member,
    DateTimeOffset? PharmacyVerifiedAt = null,
    string? PharmacyVerifiedVia = null);

public sealed record CustomerOrderListItemDto(
    Guid Id,
    string OrderNumber,
    short Status,
    DateTimeOffset OrderDate,
    decimal TotalAmount,
    int ItemCount);

public sealed record PagedCustomerOrdersResult(
    IReadOnlyList<CustomerOrderListItemDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record CreateCustomerRequest(
    string FullName,
    string Phone,
    string? CustomerCode = null,
    string? Email = null,
    DateOnly? DateOfBirth = null,
    short? Gender = null,
    string? AddressLine = null,
    string? IdNumber = null,
    string? EmergencyContactName = null,
    string? EmergencyContactPhone = null,
    string? ClinicalNotes = null,
    string? AcquisitionSource = null,
    Guid? CustomerGroupId = null);

public sealed record UpdateCustomerRequest(
    string FullName,
    string Phone,
    string CustomerCode,
    string? Email = null,
    DateOnly? DateOfBirth = null,
    short? Gender = null,
    short Status = 1,
    bool AllowCredit = false,
    decimal? CreditLimit = null,
    string? AddressLine = null,
    string? IdNumber = null,
    string? EmergencyContactName = null,
    string? EmergencyContactPhone = null,
    string? ClinicalNotes = null,
    Guid? CustomerGroupId = null);

public sealed record MarkPharmacyMemberRequest(string? VerifiedVia = null);

public sealed record NextCustomerCodeDto(string CustomerCode);
