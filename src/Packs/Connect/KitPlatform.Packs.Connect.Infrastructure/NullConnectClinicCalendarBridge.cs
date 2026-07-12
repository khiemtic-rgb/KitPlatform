namespace KitPlatform.Packs.Connect.Infrastructure;

internal sealed class NullConnectClinicCalendarBridge : IConnectClinicCalendarBridge
{
    public Task OnBookingConfirmedAsync(
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
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task OnBookingCancelledAsync(Guid bookingId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task OnBookingNoShowAsync(Guid bookingId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task OnBookingCompletedAsync(Guid bookingId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
