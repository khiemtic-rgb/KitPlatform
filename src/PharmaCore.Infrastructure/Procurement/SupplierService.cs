using PharmaCore.Application.Procurement;

namespace PharmaCore.Infrastructure.Procurement;

internal sealed class SupplierService : ISupplierService
{
    private readonly ProcurementRepository _repository;

    public SupplierService(ProcurementRepository repository) => _repository = repository;

    public Task<IReadOnlyList<SupplierDto>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default) =>
        _repository.GetSuppliersAsync(activeOnly, cancellationToken);

    public Task<SupplierDto?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetSupplierAsync(id, cancellationToken);

    public async Task<SupplierDto> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SupplierCode))
            throw new InvalidOperationException("Mã NCC không được để trống.");
        if (string.IsNullOrWhiteSpace(request.SupplierName))
            throw new InvalidOperationException("Tên NCC không được để trống.");

        var id = await _repository.CreateSupplierAsync(request, cancellationToken);
        return (await _repository.GetSupplierAsync(id, cancellationToken))!;
    }

    public async Task<SupplierDto?> UpdateAsync(Guid id, UpdateSupplierRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SupplierName))
            throw new InvalidOperationException("Tên NCC không được để trống.");

        var updated = await _repository.UpdateSupplierAsync(id, request, cancellationToken);
        return updated ? await _repository.GetSupplierAsync(id, cancellationToken) : null;
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deleted = await _repository.SoftDeleteSupplierAsync(id, cancellationToken);
        return deleted ? (true, null) : (false, "NCC không tồn tại.");
    }
}
