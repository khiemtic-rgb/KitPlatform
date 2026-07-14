using KitPlatform.Application.Customers;

namespace KitPlatform.Infrastructure.Customers;

internal sealed class CustomerGroupService : ICustomerGroupService
{
    private readonly CustomerGroupRepository _repository;

    public CustomerGroupService(CustomerGroupRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<CustomerGroupDto>> ListAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var items = await _repository.ListAsync(cancellationToken);
        return activeOnly ? items.Where(g => g.Status == 1).ToList() : items;
    }

    public Task<CustomerGroupDto?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetAsync(id, cancellationToken);

    public async Task<CustomerGroupDto> CreateAsync(
        CreateCustomerGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(request.GroupCode);
        var name = NormalizeName(request.GroupName);
        var discount = NormalizeDiscount(request.DiscountPercent);

        try
        {
            var id = await _repository.CreateAsync(code, name, discount, cancellationToken);
            return (await _repository.GetAsync(id, cancellationToken))!;
        }
        catch (Exception ex) when (ex.Message.Contains("uq_customer_groups_tenant_code", StringComparison.OrdinalIgnoreCase)
                                   || ex.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Mã nhóm «{code}» đã tồn tại.");
        }
    }

    public async Task<CustomerGroupDto?> UpdateAsync(
        Guid id,
        UpdateCustomerGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await _repository.GetAsync(id, cancellationToken) is null)
            return null;

        var name = NormalizeName(request.GroupName);
        var discount = NormalizeDiscount(request.DiscountPercent);
        var updated = await _repository.UpdateAsync(id, name, discount, request.Status, cancellationToken);
        return updated ? await _repository.GetAsync(id, cancellationToken) : null;
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await _repository.GetAsync(id, cancellationToken) is null)
            return (false, "Nhóm khách không tồn tại.");

        await _repository.ClearCustomerGroupRefsAsync(id, cancellationToken);
        var ok = await _repository.SoftDeleteAsync(id, cancellationToken);
        return ok ? (true, null) : (false, "Không xóa được nhóm khách.");
    }

    private static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Mã nhóm không được để trống.");
        return code.Trim().ToUpperInvariant();
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Tên nhóm không được để trống.");
        return name.Trim();
    }

    private static decimal NormalizeDiscount(decimal discount)
    {
        if (discount < 0 || discount > 100)
            throw new InvalidOperationException("Chiết khấu nhóm phải từ 0 đến 100%.");
        return Math.Round(discount, 2);
    }
}
