namespace KitPlatform.Application.CustomerApp;

public sealed record MedicationAdherenceSummaryDto(
    int DueCount,
    int TakenToday,
    int SkippedToday,
    int ScheduledToday,
    int MissedStreakDays,
    bool ShowMissedAlert);

public sealed record RespondMedicationReminderRequest(
    string Action,
    int? SnoozeMinutes = null,
    /// <summary>Khi Action=skipped: forgot|away|unwell|out_of_stock|other.</summary>
    string? SkipReason = null);

public interface ICustomerMedicationAdherenceService
{
    Task<MedicationAdherenceSummaryDto> GetSummaryAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<MedicationReminderListResult> ListDueAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<MedicationReminderListResult> ListFamilyDueAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<MedicationReminderDto?> RespondAsync(
        Guid tenantId,
        Guid customerId,
        Guid reminderId,
        RespondMedicationReminderRequest request,
        CancellationToken cancellationToken = default);
}
