namespace KitPlatform.Packs.Pharmacy.Rx;

public interface IPrescriptionService
{
    Task<IReadOnlyList<LinkedPrescriberDto>> GetPrescribersAsync(
        string? search = null,
        bool activeOnly = false,
        CancellationToken cancellationToken = default);

    Task<LinkedPrescriberDto?> GetPrescriberAsync(Guid id, CancellationToken cancellationToken = default);

    Task<LinkedPrescriberDto> CreatePrescriberAsync(
        CreateLinkedPrescriberRequest request,
        CancellationToken cancellationToken = default);

    Task<LinkedPrescriberDto?> UpdatePrescriberAsync(
        Guid id,
        UpdateLinkedPrescriberRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeletePrescriberAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PrescriptionPagedListResult> GetPrescriptionsAsync(
        PrescriptionListFilter? filter = null,
        CancellationToken cancellationToken = default);

    Task<PrescriptionDetailDto?> GetPrescriptionAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PrescriptionDetailDto> CreatePrescriptionAsync(
        CreatePrescriptionRequest request,
        CancellationToken cancellationToken = default);

    Task<PrescriptionDetailDto?> UpdatePrescriptionAsync(
        Guid id,
        UpdatePrescriptionRequest request,
        CancellationToken cancellationToken = default);

    Task<PrescriptionDetailDto?> SubmitPrescriptionAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PrescriptionDetailDto?> VerifyPrescriptionAsync(
        Guid id,
        VerifyPrescriptionRequest request,
        CancellationToken cancellationToken = default);

    Task<PrescriptionDetailDto?> CancelPrescriptionAsync(
        Guid id,
        CancelPrescriptionRequest? request = null,
        CancellationToken cancellationToken = default);

    Task<PrescriptionAttachmentDto> AddAttachmentAsync(
        Guid id,
        AddPrescriptionAttachmentRequest request,
        CancellationToken cancellationToken = default);

    Task<PrescriptionPosLoadDto?> GetPosLoadAsync(
        Guid id,
        Guid warehouseId,
        short priceType,
        CancellationToken cancellationToken = default);

    Task<TenantRxDashboardDto> GetTenantDashboardAsync(CancellationToken cancellationToken = default);
}
