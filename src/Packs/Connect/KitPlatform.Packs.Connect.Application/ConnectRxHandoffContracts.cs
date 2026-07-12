namespace KitPlatform.Packs.Connect;

public static class ConnectRxHandoffStatuses
{
    public const string PendingPharmacy = "pending_pharmacy";
    public const string Consumed = "consumed";
    public const string Dismissed = "dismissed";
}

public sealed record ConnectRxHandoffLineDto(
    string DrugName,
    string? Strength,
    decimal Quantity,
    string? Unit,
    string? DosageInstruction,
    int SortOrder);

public sealed record ConnectRxHandoffDto(
    Guid Id,
    Guid ClinicTenantId,
    string ClinicTenantCode,
    string ClinicTenantName,
    Guid PharmacyTenantId,
    string PharmacyTenantCode,
    string PharmacyTenantName,
    Guid ClinicPrescriptionId,
    string PrescriptionCode,
    string? PatientDisplayName,
    string? PatientPhone,
    string? ProviderDisplayName,
    string? DiagnosisText,
    string? Notes,
    string? PdfSha256,
    string HandoffStatus,
    Guid? StatusEventId,
    DateTime CreatedAt,
    DateTime? ConsumedAt,
    IReadOnlyList<ConnectRxHandoffLineDto> Lines);

public sealed record CreateConnectRxHandoffRequest(
    Guid PharmacyTenantId,
    Guid ClinicPrescriptionId,
    string PrescriptionCode,
    string? PatientDisplayName,
    string? PatientPhone,
    string? ProviderDisplayName,
    string? DiagnosisText,
    string? Notes,
    string? PdfSha256,
    IReadOnlyList<ConnectRxHandoffLineDto> Lines);

public interface IConnectRxHandoffService
{
    Task<ConnectRxHandoffDto> CreateFromClinicAsync(
        CreateConnectRxHandoffRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConnectRxHandoffDto>> ListAsync(
        string? status = null,
        CancellationToken cancellationToken = default);

    Task<ConnectRxHandoffDto?> GetAsync(Guid handoffId, CancellationToken cancellationToken = default);

    Task MarkFromStatusEventAsync(
        Guid statusEventId,
        string handoffStatus,
        CancellationToken cancellationToken = default);
}
