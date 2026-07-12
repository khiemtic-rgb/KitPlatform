namespace KitPlatform.Packs.Connect;

/// <summary>
/// Optional Clinic calendar bridge — implemented by Clinic pack when clinic_appointments is enabled.
/// Connect calls this on booking confirm/cancel/no-show; no-op if Clinic pack not registered.
/// </summary>
public interface IConnectClinicCalendarBridge
{
    Task OnBookingConfirmedAsync(
        Guid bookingId,
        string patientDisplayName,
        string? patientPhone,
        DateTime scheduledAt,
        int durationMinutes,
        string? notes,
        Guid? pharmacyCustomerId = null,
        Guid? clinicCustomerId = null,
        string encounterModality = "in_person",
        Guid? pharmacyTenantId = null,
        Guid? referralId = null,
        CancellationToken cancellationToken = default);

    Task OnBookingCancelledAsync(Guid bookingId, CancellationToken cancellationToken = default);

    Task OnBookingNoShowAsync(Guid bookingId, CancellationToken cancellationToken = default);

    Task OnBookingCompletedAsync(Guid bookingId, CancellationToken cancellationToken = default);
}
