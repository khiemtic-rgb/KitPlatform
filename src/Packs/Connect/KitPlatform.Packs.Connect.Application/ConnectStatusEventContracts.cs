namespace KitPlatform.Packs.Connect;

public static class ConnectStatusEventTypes
{
    public const string ReadyToDispense = "ready_to_dispense";
    public const string ReferralCompleted = "referral_completed";
    public const string BookingCompleted = "booking_completed";
}

public static class ConnectStatusSourceTypes
{
    public const string Referral = "referral";
    public const string Booking = "booking";
    public const string Manual = "manual";
    public const string ClinicRx = "clinic_rx";
}

public static class ConnectStatusEventStatuses
{
    public const string PendingPharmacy = "pending_pharmacy";
    public const string Consumed = "consumed";
    public const string Dismissed = "dismissed";
}

public sealed record ConnectStatusEventDto(
    Guid Id,
    Guid PharmacyTenantId,
    string PharmacyTenantCode,
    string PharmacyTenantName,
    Guid ClinicTenantId,
    string ClinicTenantCode,
    string ClinicTenantName,
    string EventType,
    string SourceType,
    Guid? SourceId,
    string? PatientDisplayName,
    string? PatientPhone,
    string? Summary,
    string EventStatus,
    DateTime CreatedAt,
    DateTime? ConsumedAt);

public sealed record CreateConnectStatusEventRequest(
    Guid PharmacyTenantId,
    string? PatientDisplayName = null,
    string? PatientPhone = null,
    string? Summary = null);

public interface IConnectStatusEventService
{
    Task<IReadOnlyList<ConnectStatusEventDto>> ListAsync(
        string? status = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConnectStatusEventDto>> ListPendingAsync(
        CancellationToken cancellationToken = default);

    Task<ConnectStatusEventDto> CreateManualReadyAsync(
        CreateConnectStatusEventRequest request,
        CancellationToken cancellationToken = default);

    Task<ConnectStatusEventDto?> ConsumeAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task<ConnectStatusEventDto?> DismissAsync(Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>Idempotent emit from booking/referral complete (internal).</summary>
    Task EmitReadyFromSourceAsync(
        string sourceType,
        Guid sourceId,
        Guid pharmacyTenantId,
        Guid clinicTenantId,
        string? patientDisplayName,
        string? patientPhone,
        string summary,
        CancellationToken cancellationToken = default);
}
