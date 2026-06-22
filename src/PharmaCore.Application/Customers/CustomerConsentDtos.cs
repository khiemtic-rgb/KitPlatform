namespace PharmaCore.Application.Customers;

public sealed record CustomerConsentDto(
    Guid Id,
    Guid CustomerId,
    short Channel,
    short Purpose,
    bool Granted,
    DateTime? GrantedAt,
    DateTime? RevokedAt,
    short Source,
    string? Notes);

public sealed record UpsertCustomerConsentRequest(
    short Channel,
    short Purpose,
    bool Granted,
    short Source = CustomerConsentSources.Admin,
    string? Notes = null);

public sealed record UpsertCustomerConsentsRequest(
    IReadOnlyList<UpsertCustomerConsentRequest> Items);
