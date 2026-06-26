namespace PharmaCore.Application.CustomerApp;

public sealed record CustomerAddressDto(
    Guid Id,
    string Label,
    string? RecipientName,
    string? Phone,
    string AddressLine,
    string? Ward,
    string? District,
    string? Province,
    bool IsDefault);

public sealed record CustomerAddressListResult(IReadOnlyList<CustomerAddressDto> Items);

public sealed record UpsertCustomerAddressRequest(
    string Label,
    string? RecipientName,
    string? Phone,
    string AddressLine,
    string? Ward,
    string? District,
    string? Province,
    bool IsDefault = false);

public interface ICustomerAddressService
{
    Task<CustomerAddressListResult> ListAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<CustomerAddressDto> CreateAsync(
        Guid tenantId,
        Guid customerId,
        UpsertCustomerAddressRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerAddressDto> UpdateAsync(
        Guid tenantId,
        Guid customerId,
        Guid addressId,
        UpsertCustomerAddressRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid tenantId,
        Guid customerId,
        Guid addressId,
        CancellationToken cancellationToken = default);
}
