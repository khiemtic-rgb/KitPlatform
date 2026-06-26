using PharmaCore.Application.CustomerApp;

namespace PharmaCore.Infrastructure.CustomerApp;

internal sealed class CustomerAddressService : ICustomerAddressService
{
    private readonly CustomerAddressRepository _repo;

    public CustomerAddressService(CustomerAddressRepository repo) => _repo = repo;

    public async Task<CustomerAddressListResult> ListAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var items = await _repo.ListAsync(tenantId, customerId, cancellationToken);
        return new CustomerAddressListResult(items);
    }

    public async Task<CustomerAddressDto> CreateAsync(
        Guid tenantId,
        Guid customerId,
        UpsertCustomerAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        return await _repo.CreateAsync(tenantId, customerId, request, cancellationToken);
    }

    public async Task<CustomerAddressDto> UpdateAsync(
        Guid tenantId,
        Guid customerId,
        Guid addressId,
        UpsertCustomerAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        return await _repo.UpdateAsync(tenantId, customerId, addressId, request, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy địa chỉ.");
    }

    public async Task DeleteAsync(
        Guid tenantId,
        Guid customerId,
        Guid addressId,
        CancellationToken cancellationToken = default)
    {
        if (!await _repo.DeleteAsync(tenantId, customerId, addressId, cancellationToken))
            throw new InvalidOperationException("Không xóa được địa chỉ.");
    }

    private static void ValidateRequest(UpsertCustomerAddressRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Label))
            throw new InvalidOperationException("Nhập nhãn địa chỉ (vd: Nhà, Cơ quan).");
        if (string.IsNullOrWhiteSpace(request.AddressLine))
            throw new InvalidOperationException("Nhập địa chỉ chi tiết.");
    }
}
