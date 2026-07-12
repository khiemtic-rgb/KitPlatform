namespace KitPlatform.Packs.Connect;

public static class ConnectBookingStatuses
{
    public const string Proposed = "proposed";
    public const string Confirmed = "confirmed";
    public const string Cancelled = "cancelled";
    public const string Completed = "completed";
    public const string NoShow = "no_show";
}

/// <summary>Aligned with ClinicEncounterModalities — remote_video reserved for CL3-B.</summary>
public static class ConnectEncounterModalities
{
    public const string InPerson = "in_person";
    public const string RemoteAsync = "remote_async";
    public const string RemoteVideo = "remote_video";

    public static bool IsKnown(string? value) =>
        value is InPerson or RemoteAsync or RemoteVideo;

    public static string Normalize(string? value) =>
        IsKnown(value) ? value! : InPerson;
}

public sealed record ConnectBookingDto(
    Guid Id,
    Guid ClinicTenantId,
    string ClinicTenantCode,
    string ClinicTenantName,
    Guid? PharmacyTenantId,
    string? PharmacyTenantCode,
    string? PharmacyTenantName,
    Guid? ReferralId,
    Guid? DoctorId,
    string? DoctorFullName,
    string PatientDisplayName,
    string? PatientPhone,
    Guid? PharmacyCustomerId,
    DateTime ScheduledAt,
    int DurationMinutes,
    string BookingStatus,
    string EncounterModality,
    string? Notes,
    DateTime? NotifiedAt,
    DateTime CreatedAt);

public sealed record CreateConnectBookingRequest(
    DateTime ScheduledAt,
    string PatientDisplayName,
    string? PatientPhone = null,
    Guid? ReferralId = null,
    Guid? PharmacyTenantId = null,
    Guid? DoctorId = null,
    int DurationMinutes = 30,
    string? Notes = null,
    string EncounterModality = "in_person");

public interface IConnectNotifyService
{
    Task NotifyBookingProposedAsync(
        ConnectBookingDto booking,
        CancellationToken cancellationToken = default);

    Task NotifyBookingConfirmedAsync(
        ConnectBookingDto booking,
        CancellationToken cancellationToken = default);

    Task NotifyReadyToDispenseAsync(
        ConnectStatusEventDto statusEvent,
        CancellationToken cancellationToken = default);
}

public interface IConnectBookingService
{
    Task<IReadOnlyList<ConnectBookingDto>> ListAsync(
        string? status = null,
        CancellationToken cancellationToken = default);

    Task<ConnectBookingDto> CreateAsync(
        CreateConnectBookingRequest request,
        CancellationToken cancellationToken = default);

    Task<ConnectBookingDto?> ConfirmAsync(Guid bookingId, CancellationToken cancellationToken = default);

    Task<ConnectBookingDto?> CancelAsync(Guid bookingId, CancellationToken cancellationToken = default);

    Task<ConnectBookingDto?> CompleteAsync(Guid bookingId, CancellationToken cancellationToken = default);

    Task<ConnectBookingDto?> MarkNoShowAsync(Guid bookingId, CancellationToken cancellationToken = default);
}
