namespace PharmaCore.Application.Customers;

public interface ICustomerConsentService
{
    Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerConsentDto>> GetConsentsAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerConsentDto>> UpsertConsentsAsync(
        Guid customerId,
        UpsertCustomerConsentsRequest request,
        CancellationToken cancellationToken = default);
}
