using PharmaCore.Application.CustomerApp;

namespace PharmaCore.Infrastructure.CustomerApp;

internal sealed class CustomerPurchaseService : ICustomerPurchaseService
{
    private readonly CustomerPurchaseRepository _repo;

    public CustomerPurchaseService(CustomerPurchaseRepository repo) => _repo = repo;

    public async Task<CustomerPurchaseListResult> ListForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var items = await _repo.ListAsync(tenantId, customerId, cancellationToken);
        return new CustomerPurchaseListResult(items);
    }

    public Task<CustomerPurchaseDetailDto?> GetForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        Guid salesOrderId,
        CancellationToken cancellationToken = default) =>
        _repo.GetAsync(tenantId, customerId, salesOrderId, cancellationToken);
}
