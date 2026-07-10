using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.Pharmacy.Rx;

namespace KitPlatform.Packs.Pharmacy.Infrastructure;

internal sealed class PrescriptionService : IPrescriptionService
{
    private readonly PrescriptionRepository _repository;
    private readonly ITenantContext _tenant;
    private readonly IBranchAccessService _branchAccess;

    public PrescriptionService(
        PrescriptionRepository repository,
        ITenantContext tenant,
        IBranchAccessService branchAccess)
    {
        _repository = repository;
        _tenant = tenant;
        _branchAccess = branchAccess;
    }

    public Task<IReadOnlyList<LinkedPrescriberDto>> GetPrescribersAsync(
        string? search = null,
        bool activeOnly = false,
        CancellationToken cancellationToken = default) =>
        _repository.GetPrescribersAsync(search, activeOnly, cancellationToken);

    public Task<LinkedPrescriberDto?> GetPrescriberAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetPrescriberAsync(id, cancellationToken);

    public async Task<LinkedPrescriberDto> CreatePrescriberAsync(
        CreateLinkedPrescriberRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new InvalidOperationException("Tên bác sĩ không được để trống.");
        if (string.IsNullOrWhiteSpace(request.LicenseNumber))
            throw new InvalidOperationException("Số CCHN (chứng chỉ hành nghề) là bắt buộc.");

        var id = await _repository.CreatePrescriberAsync(request, cancellationToken);
        return (await _repository.GetPrescriberAsync(id, cancellationToken))!;
    }

    public async Task<LinkedPrescriberDto?> UpdatePrescriberAsync(
        Guid id,
        UpdateLinkedPrescriberRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new InvalidOperationException("Tên bác sĩ không được để trống.");
        if (string.IsNullOrWhiteSpace(request.LicenseNumber))
            throw new InvalidOperationException("Số CCHN (chứng chỉ hành nghề) là bắt buộc.");

        var updated = await _repository.UpdatePrescriberAsync(id, request, cancellationToken);
        return updated ? await _repository.GetPrescriberAsync(id, cancellationToken) : null;
    }

    public Task<bool> DeletePrescriberAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.DeletePrescriberAsync(id, cancellationToken);

    public async Task<PrescriptionPagedListResult> GetPrescriptionsAsync(
        PrescriptionListFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new PrescriptionListFilter();
        var scope = await _branchAccess.GetScopeAsync(cancellationToken);
        var allowed = scope.Unrestricted ? null : scope.BranchIds.ToArray();
        return await _repository.GetPrescriptionsAsync(filter, allowed, cancellationToken);
    }

    public async Task<PrescriptionDetailDto?> GetPrescriptionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetPrescriptionAsync(id, cancellationToken);
        if (item?.BranchId is Guid branchId)
            await _branchAccess.EnsureBranchAccessAsync(branchId, cancellationToken);
        return item;
    }

    public async Task<PrescriptionDetailDto> CreatePrescriptionAsync(
        CreatePrescriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.BranchId is Guid branchId)
            await _branchAccess.EnsureBranchAccessAsync(branchId, cancellationToken);

        var id = await _repository.CreatePrescriptionAsync(request, _tenant.UserId, cancellationToken);
        return (await GetPrescriptionAsync(id, cancellationToken))!;
    }

    public async Task<PrescriptionDetailDto?> UpdatePrescriptionAsync(
        Guid id,
        UpdatePrescriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.BranchId is Guid branchId)
            await _branchAccess.EnsureBranchAccessAsync(branchId, cancellationToken);

        var updated = await _repository.UpdatePrescriptionAsync(id, request, _tenant.UserId, cancellationToken);
        if (!updated)
            return null;
        return await GetPrescriptionAsync(id, cancellationToken);
    }

    public async Task<PrescriptionDetailDto?> SubmitPrescriptionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await GetPrescriptionAsync(id, cancellationToken);
        if (existing is null)
            return null;

        var submitted = await _repository.SubmitPrescriptionAsync(id, _tenant.UserId, cancellationToken);
        return submitted ? await GetPrescriptionAsync(id, cancellationToken) : null;
    }

    public async Task<PrescriptionDetailDto?> VerifyPrescriptionAsync(
        Guid id,
        VerifyPrescriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetPrescriptionAsync(id, cancellationToken);
        if (existing is null)
            return null;

        var ok = await _repository.VerifyPrescriptionAsync(id, request, _tenant.UserId, cancellationToken);
        return ok ? await GetPrescriptionAsync(id, cancellationToken) : null;
    }

    public async Task<PrescriptionDetailDto?> CancelPrescriptionAsync(
        Guid id,
        CancelPrescriptionRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetPrescriptionAsync(id, cancellationToken);
        if (existing is null)
            return null;

        var ok = await _repository.CancelPrescriptionAsync(id, request?.Reason, _tenant.UserId, cancellationToken);
        return ok ? await GetPrescriptionAsync(id, cancellationToken) : null;
    }

    public async Task<PrescriptionAttachmentDto> AddAttachmentAsync(
        Guid id,
        AddPrescriptionAttachmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FileUrl))
            throw new InvalidOperationException("Đường dẫn file không được để trống.");

        var existing = await GetPrescriptionAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Đơn thuốc không tồn tại.");

        return await _repository.AddAttachmentAsync(existing.Id, request, _tenant.UserId, cancellationToken);
    }

    public async Task<PrescriptionPosLoadDto?> GetPosLoadAsync(
        Guid id,
        Guid warehouseId,
        short priceType,
        CancellationToken cancellationToken = default)
    {
        await _branchAccess.EnsureWarehouseAccessAsync(warehouseId, cancellationToken);
        var item = await _repository.GetPosLoadAsync(id, warehouseId, priceType, cancellationToken);
        if (item?.BranchId is Guid branchId)
            await _branchAccess.EnsureBranchAccessAsync(branchId, cancellationToken);
        return item;
    }
}
