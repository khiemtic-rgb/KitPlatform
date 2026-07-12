namespace KitPlatform.Application.Customers;

public sealed record CustomerAdminListItemDto(
    Guid Id,
    string CustomerCode,
    string FullName,
    string Phone,
    string? Email,
    short Status,
    DateTimeOffset CreatedAt);

public sealed record PagedCustomersResult(
    IReadOnlyList<CustomerAdminListItemDto> Items,
    int Total,
    int Page,
    int PageSize);

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
    string? ClinicalNotes = null);

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
    string? ClinicalNotes = null);

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
    string? ClinicalNotes = null);

public sealed record NextCustomerCodeDto(string CustomerCode);
