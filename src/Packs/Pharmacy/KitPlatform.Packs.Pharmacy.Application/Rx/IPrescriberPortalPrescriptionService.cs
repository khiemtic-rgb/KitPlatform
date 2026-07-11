namespace KitPlatform.Packs.Pharmacy.Rx;

public interface IPrescriberPortalPrescriptionService
{
    Task<IReadOnlyList<PortalCustomerSearchItemDto>> SearchCustomersAsync(
        Guid prescriberId,
        Guid tenantId,
        string? query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PortalProductSearchItemDto>> SearchProductsAsync(
        Guid prescriberId,
        Guid tenantId,
        string? query,
        CancellationToken cancellationToken = default);

    Task<PortalPrescriptionDetailDto> CreateSignedPrescriptionAsync(
        Guid prescriberId,
        PortalCreatePrescriptionRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PortalPrescriptionSummaryDto>> ListMyPrescriptionsAsync(
        Guid prescriberId,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    Task<PortalPrescriptionDetailDto?> GetPrescriptionAsync(
        Guid prescriberId,
        Guid prescriptionId,
        CancellationToken cancellationToken = default);

    Task<PortalPrescriptionDetailDto> AmendSignedPrescriptionAsync(
        Guid prescriberId,
        Guid prescriptionId,
        PortalAmendPrescriptionRequest request,
        CancellationToken cancellationToken = default);

    Task<PortalPrescriptionDetailDto?> CancelPrescriptionAsync(
        Guid prescriberId,
        Guid prescriptionId,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task<PortalPrescriberDashboardDto> GetDashboardAsync(
        Guid prescriberId,
        CancellationToken cancellationToken = default);

    Task<PortalPrescriptionShareDto?> GetPrescriptionShareAsync(
        Guid prescriberId,
        Guid prescriptionId,
        CancellationToken cancellationToken = default);
}
