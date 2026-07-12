namespace KitPlatform.Application.CustomerApp;

/// <summary>
/// Localized title/body for customer in-app notifications and Web Push (P11b-S2).
/// </summary>
public interface ICustomerNotificationTextService
{
    Task<string> ResolveLocaleAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<string> FormatAsync(
        Guid tenantId,
        string locale,
        string translationKey,
        IReadOnlyDictionary<string, string>? args = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Stable keys — seeded in <c>tenant_string_translations</c> (migration 065).</summary>
public static class CustomerNotificationTextKeys
{
    public const string MedicationReminderTitle = "customer.notify.medication_reminder.title";
    public const string MedicationReminderFamilyTitle = "customer.notify.medication_reminder.family_title";
    public const string MedicationReminderBody = "customer.notify.medication_reminder.body";
    public const string MedicationReminderBodyWithNote = "customer.notify.medication_reminder.body_with_note";

    public const string CareAdvanceTitle = "customer.notify.care_reminder.advance.title";
    public const string CareAdvanceFamilyTitle = "customer.notify.care_reminder.advance.family_title";
    public const string CareDueTitle = "customer.notify.care_reminder.due.title";
    public const string CareDueFamilyTitle = "customer.notify.care_reminder.due.family_title";
    public const string CareAdvanceBody = "customer.notify.care_reminder.advance.body";
    public const string CareDueBody = "customer.notify.care_reminder.due.body";
    public const string CareScheduledTitle = "customer.notify.care_reminder.scheduled.title";
    public const string CareScheduledBody = "customer.notify.care_reminder.scheduled.body";

    public const string RepurchaseTitle = "customer.notify.repurchase.title";
    public const string RepurchaseBody = "customer.notify.repurchase.body";
    public const string DateToday = "customer.notify.date.today";

    public const string AdherenceMissedTitle = "customer.notify.adherence_missed.title";
    public const string AdherenceMissedBody = "customer.notify.adherence_missed.body";

    public const string ChatStaffReplyTitle = "customer.notify.chat.staff_reply.title";
    public const string ChatStaffReplyNamedTitle = "customer.notify.chat.staff_reply.named_title";

    public const string DraftOrderTitle = "customer.notify.draft_order.title";
    public const string DraftOrderBody = "customer.notify.draft_order.body";

    public const string ConnectRxReadyTitle = "customer.notify.connect.rx_ready.title";
    public const string ConnectRxReadyBody = "customer.notify.connect.rx_ready.body";
}
