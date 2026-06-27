using PharmaCore.Application.Procurement;

namespace PharmaCore.Infrastructure.Procurement;

internal sealed class ProcurementVatTreatmentService : IProcurementVatTreatmentService
{
    private readonly ProcurementRepository _repository;

    public ProcurementVatTreatmentService(ProcurementRepository repository) => _repository = repository;

    public Task<IReadOnlyList<ProcurementVatTreatmentDto>> GetAllAsync(
        bool activeOnly = true,
        CancellationToken cancellationToken = default) =>
        _repository.GetVatTreatmentsAsync(activeOnly, cancellationToken);

    public Task<ProcurementVatTreatmentDto?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetVatTreatmentAsync(id, cancellationToken);

    public async Task<ProcurementVatTreatmentDto> CreateAsync(
        CreateProcurementVatTreatmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = request.TreatmentCode.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Mã loại thuế không hợp lệ.");

        if (await _repository.VatTreatmentCodeExistsAsync(code, cancellationToken))
            throw new InvalidOperationException($"Mã thuế «{code}» đã tồn tại. Chọn mã khác hoặc sửa dòng hiện có.");

        try
        {
            var id = await _repository.CreateVatTreatmentAsync(
                request with { TreatmentCode = code },
                cancellationToken);
            return (await _repository.GetVatTreatmentAsync(id, cancellationToken))!;
        }
        catch (Exception ex) when (IsUniqueVatTreatmentCodeViolation(ex))
        {
            throw new InvalidOperationException($"Mã thuế «{code}» đã tồn tại. Chọn mã khác hoặc sửa dòng hiện có.");
        }
    }

    private static bool IsUniqueVatTreatmentCodeViolation(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            var type = current.GetType();
            if (type.Name is "PostgresException" && type.GetProperty("SqlState")?.GetValue(current) is "23505")
                return true;
        }

        return false;
    }

    public async Task<ProcurementVatTreatmentDto?> UpdateAsync(
        Guid id,
        UpdateProcurementVatTreatmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await _repository.UpdateVatTreatmentAsync(id, request, cancellationToken);
        return updated ? await _repository.GetVatTreatmentAsync(id, cancellationToken) : null;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetVatTreatmentAsync(id, cancellationToken);
        if (item is null)
            return false;

        if (!item.CanDelete)
        {
            if (ProcurementVatTreatmentDefaults.IsBuiltIn(item.TreatmentCode))
                throw new InvalidOperationException("Không thể xóa mức thuế mặc định của hệ thống.");

            throw new InvalidOperationException(
                "Mức thuế đang được dùng trên đơn đặt hàng — bấm Sửa và chọn «Ngừng dùng» thay vì xóa.");
        }

        return await _repository.DeleteVatTreatmentAsync(id, cancellationToken);
    }
}
