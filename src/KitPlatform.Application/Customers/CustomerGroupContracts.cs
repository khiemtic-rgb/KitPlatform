namespace KitPlatform.Application.Customers;

public sealed record CustomerGroupDto(
    Guid Id,
    string GroupCode,
    string GroupName,
    decimal DiscountPercent,
    short Status);

public sealed record CreateCustomerGroupRequest(
    string GroupCode,
    string GroupName,
    decimal DiscountPercent = 0);

public sealed record UpdateCustomerGroupRequest(
    string GroupName,
    decimal DiscountPercent,
    short Status = 1);

public interface ICustomerGroupService
{
    Task<IReadOnlyList<CustomerGroupDto>> ListAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<CustomerGroupDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CustomerGroupDto> CreateAsync(CreateCustomerGroupRequest request, CancellationToken cancellationToken = default);
    Task<CustomerGroupDto?> UpdateAsync(Guid id, UpdateCustomerGroupRequest request, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
