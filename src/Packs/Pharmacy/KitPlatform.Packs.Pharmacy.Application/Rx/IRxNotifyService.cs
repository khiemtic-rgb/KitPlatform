namespace KitPlatform.Packs.Pharmacy.Rx;

/// <summary>Rx notify: lời mời BS + đơn mới (SMS / log stub).</summary>
public interface IRxNotifyService
{
    Task NotifyPrescriberInviteAsync(
        string phone,
        string tenantCode,
        string tenantName,
        CancellationToken cancellationToken = default);

    Task NotifyPrescriberLinkApprovedAsync(
        string phone,
        string tenantCode,
        string tenantName,
        CancellationToken cancellationToken = default);

    Task NotifyPharmacyLinkRequestAsync(
        string tenantCode,
        string tenantName,
        string prescriberName,
        string? prescriberPhone,
        CancellationToken cancellationToken = default);

    Task NotifyPharmacyNewPrescriptionAsync(
        string tenantCode,
        string tenantName,
        string prescriptionCode,
        Guid prescriptionId,
        string? patientName,
        string? prescriberName,
        CancellationToken cancellationToken = default);
}
