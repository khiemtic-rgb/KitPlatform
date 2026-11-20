using KitPlatform.Application.CustomerApp;
using KitPlatform.Packs.Pharmacy.CustomerApp;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.CustomerApp;

internal sealed class CustomerActiveMedicationService : ICustomerActiveMedicationService
{
    private readonly CustomerActiveMedicationRepository _repo;

    public CustomerActiveMedicationService(CustomerActiveMedicationRepository repo) => _repo = repo;

    public async Task<ActiveMedicationListResult> ListAsync(
        Guid tenantId,
        Guid customerId,
        Guid? familyMemberId = null,
        bool forSelfOnly = false,
        CancellationToken cancellationToken = default)
    {
        var orders = await _repo.ListLatestOrderLinesAsync(tenantId, customerId, cancellationToken);
        var reminders = await _repo.ListActiveRemindersAsync(tenantId, customerId, cancellationToken);
        var repurchases = await _repo.ListRepurchaseByProductAsync(tenantId, customerId, cancellationToken);

        var productIds = orders.Select(o => o.ProductId)
            .Concat(reminders.Select(r => r.ProductId))
            .Distinct()
            .ToList();

        var items = new List<ActiveMedicationDto>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var hasFamilyFilter = familyMemberId.HasValue || forSelfOnly;

        foreach (var productId in productIds)
        {
            var order = orders.FirstOrDefault(o => o.ProductId == productId);
            var productReminders = reminders.Where(r => r.ProductId == productId).ToList();
            var primaryReminder = PickPrimaryReminder(productReminders, familyMemberId, forSelfOnly);

            if (hasFamilyFilter && primaryReminder is null && !(forSelfOnly && order is not null))
                continue;

            var repurchaseRows = repurchases.Where(r => r.ProductId == productId).ToList();

            var supplyEnd = order?.SupplyEndDate;
            int? daysRemaining = supplyEnd is not null
                ? Math.Max(0, supplyEnd.Value.DayNumber - today.DayNumber)
                : null;

            var adherence = await _repo.ListAdherenceEventsAsync(
                tenantId,
                customerId,
                productId,
                cancellationToken);

            var timeline = BuildTimeline(order, primaryReminder, repurchaseRows, adherence);

            items.Add(new ActiveMedicationDto(
                productId,
                order?.ProductCode ?? primaryReminder?.ProductCode ?? "",
                order?.ProductName ?? primaryReminder?.ProductName ?? "",
                primaryReminder?.DosageNote,
                primaryReminder?.FamilyMemberId,
                primaryReminder?.Id,
                primaryReminder is not null
                    ? ReminderScheduleHelper.FormatRemindTime(primaryReminder.RemindTime)
                    : null,
                daysRemaining,
                supplyEnd,
                order?.OrderNumber,
                order is not null
                    ? new DateTimeOffset(DateTime.SpecifyKind(order.OrderDate, DateTimeKind.Utc))
                    : null,
                repurchaseRows.Count,
                timeline,
                order?.DaysSupply));
        }

        items.Sort((a, b) =>
        {
            var aDays = a.DaysRemaining ?? int.MaxValue;
            var bDays = b.DaysRemaining ?? int.MaxValue;
            return aDays.CompareTo(bDays);
        });

        return new ActiveMedicationListResult(items);
    }

    private static CustomerActiveMedicationRepository.ActiveMedicationReminderRow? PickPrimaryReminder(
        IReadOnlyList<CustomerActiveMedicationRepository.ActiveMedicationReminderRow> productReminders,
        Guid? familyMemberId,
        bool forSelfOnly)
    {
        if (familyMemberId.HasValue)
            return productReminders.FirstOrDefault(r => r.FamilyMemberId == familyMemberId);

        if (forSelfOnly)
            return productReminders.FirstOrDefault(r => r.FamilyMemberId is null);

        return productReminders.FirstOrDefault();
    }

    private static IReadOnlyList<ActiveMedicationTimelineEventDto> BuildTimeline(
        CustomerActiveMedicationRepository.ActiveMedicationOrderRow? order,
        CustomerActiveMedicationRepository.ActiveMedicationReminderRow? reminder,
        IReadOnlyList<CustomerActiveMedicationRepository.ActiveMedicationRepurchaseRow> repurchases,
        IReadOnlyList<CustomerActiveMedicationRepository.ActiveMedicationAdherenceRow> adherence)
    {
        var events = new List<ActiveMedicationTimelineEventDto>();

        if (order is not null)
        {
            events.Add(new ActiveMedicationTimelineEventDto(
                "purchased",
                new DateTimeOffset(DateTime.SpecifyKind(order.OrderDate, DateTimeKind.Utc)),
                $"Mua đơn {order.OrderNumber}"));

            events.Add(new ActiveMedicationTimelineEventDto(
                "supply_end",
                new DateTimeOffset(order.SupplyEndDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                $"Dự kiến hết thuốc ({order.DaysSupply} ngày)"));
        }

        if (reminder is not null)
        {
            events.Add(new ActiveMedicationTimelineEventDto(
                "reminder_on",
                new DateTimeOffset(DateTime.SpecifyKind(reminder.CreatedAt, DateTimeKind.Utc)),
                $"Bật nhắc uống lúc {ReminderScheduleHelper.FormatRemindTime(reminder.RemindTime)}"));
        }

        foreach (var row in repurchases.Where(r => r.SuggestedForDate is not null))
        {
            events.Add(new ActiveMedicationTimelineEventDto(
                "repurchase_due",
                new DateTimeOffset(row.SuggestedForDate!.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                $"Nhắc đặt lại (đơn {row.OrderNumber})"));
        }

        foreach (var row in adherence.Take(10))
        {
            var label = row.Response switch
            {
                "taken" => "Đã uống",
                "skipped" => "Bỏ qua",
                "snoozed" => "Hoãn nhắc",
                _ => row.Response,
            };
            events.Add(new ActiveMedicationTimelineEventDto(
                row.Response,
                new DateTimeOffset(DateTime.SpecifyKind(row.ScheduledAt, DateTimeKind.Utc)),
                label));
        }

        return events
            .OrderBy(e => e.OccurredAt)
            .ToList();
    }
}
