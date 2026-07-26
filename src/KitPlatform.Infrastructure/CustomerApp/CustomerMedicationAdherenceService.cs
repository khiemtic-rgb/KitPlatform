using KitPlatform.Application.CustomerApp;

namespace KitPlatform.Infrastructure.CustomerApp;

internal sealed class CustomerMedicationAdherenceService : ICustomerMedicationAdherenceService
{
    private const int DefaultSnoozeMinutes = 15;
    private const int MissedAlertThresholdDays = 3;

    private static readonly HashSet<string> AllowedSkipReasons = new(StringComparer.OrdinalIgnoreCase)
    {
        "forgot", "away", "unwell", "out_of_stock", "other",
    };

    private readonly CustomerReminderRepository _repo;

    public CustomerMedicationAdherenceService(CustomerReminderRepository repo) => _repo = repo;

    public async Task<MedicationAdherenceSummaryDto> GetSummaryAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetAdherenceSummaryAsync(
            tenantId,
            customerId,
            DateTimeOffset.UtcNow,
            cancellationToken);

        return new MedicationAdherenceSummaryDto(
            row.DueCount,
            row.TakenToday,
            row.SkippedToday,
            row.ScheduledToday,
            row.MissedStreakDays,
            row.MissedStreakDays >= MissedAlertThresholdDays);
    }

    public async Task<MedicationReminderListResult> ListDueAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListDueAsync(
            tenantId,
            customerId,
            DateTimeOffset.UtcNow,
            cancellationToken);
        return new MedicationReminderListResult(rows.Select(MapRow).ToList());
    }

    public async Task<MedicationReminderListResult> ListFamilyDueAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListFamilyDueAsync(
            tenantId,
            customerId,
            DateTimeOffset.UtcNow,
            cancellationToken);
        return new MedicationReminderListResult(rows.Select(MapRow).ToList());
    }

    public async Task<MedicationReminderDto?> RespondAsync(
        Guid tenantId,
        Guid customerId,
        Guid reminderId,
        RespondMedicationReminderRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repo.GetByIdAsync(tenantId, customerId, reminderId, cancellationToken);
        if (existing is null)
            return null;

        var action = request.Action.Trim().ToLowerInvariant();
        var utcNow = DateTimeOffset.UtcNow;
        var scheduledAt = existing.NextRemindAt.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(existing.NextRemindAt.Value, DateTimeKind.Utc))
            : utcNow;

        DateTimeOffset? nextRemindAt;
        int? snoozeMinutes = null;

        switch (action)
        {
            case "taken":
                await _repo.InsertAdherenceEventAsync(
                    tenantId,
                    customerId,
                    reminderId,
                    existing.ProductId,
                    existing.FamilyMemberId,
                    scheduledAt,
                    action,
                    null,
                    cancellationToken);
                nextRemindAt = ReminderScheduleHelper.ComputeNextRemindAt(
                    existing.RemindTime.ToTimeSpan(),
                    existing.DaysOfWeek.Select(d => (int)d).ToArray(),
                    utcNow);
                break;

            case "skipped":
            {
                var skipReason = NormalizeSkipReason(request.SkipReason);
                await _repo.InsertAdherenceEventAsync(
                    tenantId,
                    customerId,
                    reminderId,
                    existing.ProductId,
                    existing.FamilyMemberId,
                    scheduledAt,
                    action,
                    null,
                    cancellationToken,
                    skipReason);
                nextRemindAt = ReminderScheduleHelper.ComputeNextRemindAt(
                    existing.RemindTime.ToTimeSpan(),
                    existing.DaysOfWeek.Select(d => (int)d).ToArray(),
                    utcNow);
                break;
            }

            case "snooze":
                snoozeMinutes = request.SnoozeMinutes is > 0 and <= 120
                    ? request.SnoozeMinutes.Value
                    : DefaultSnoozeMinutes;
                await _repo.InsertAdherenceEventAsync(
                    tenantId,
                    customerId,
                    reminderId,
                    existing.ProductId,
                    existing.FamilyMemberId,
                    scheduledAt,
                    "snoozed",
                    snoozeMinutes,
                    cancellationToken);
                nextRemindAt = utcNow.AddMinutes(snoozeMinutes.Value);
                break;

            default:
                throw new InvalidOperationException("action phải là taken, skipped hoặc snooze.");
        }

        var updated = await _repo.UpdateAsync(
            tenantId,
            customerId,
            reminderId,
            existing.ProductId,
            existing.FamilyMemberId,
            existing.DosageNote,
            existing.RemindTime.ToTimeSpan(),
            existing.DaysOfWeek.Select(d => (int)d).ToArray(),
            nextRemindAt,
            existing.IsActive,
            cancellationToken);

        if (!updated)
            return null;

        var row = await _repo.GetByIdAsync(tenantId, customerId, reminderId, cancellationToken);
        return row is null ? null : MapRow(row);
    }

    private static MedicationReminderDto MapRow(MedicationReminderRow row) =>
        new(
            row.Id,
            row.ProductId,
            row.FamilyMemberId,
            row.ProductCode,
            row.ProductName,
            row.DosageNote,
            ReminderScheduleHelper.FormatRemindTime(row.RemindTime),
            row.DaysOfWeek.Select(d => (int)d).ToArray(),
            row.NextRemindAt.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(row.NextRemindAt.Value, DateTimeKind.Utc))
                : null,
            row.IsActive,
            new DateTimeOffset(DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(row.UpdatedAt, DateTimeKind.Utc)));

    private static string? NormalizeSkipReason(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var code = raw.Trim().ToLowerInvariant();
        if (!AllowedSkipReasons.Contains(code))
            throw new InvalidOperationException(
                "skipReason phải là forgot, away, unwell, out_of_stock hoặc other.");
        return code;
    }
}
