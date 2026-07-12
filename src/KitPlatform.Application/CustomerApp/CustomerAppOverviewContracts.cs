namespace KitPlatform.Application.CustomerApp;

public sealed record CustomerHomeSummaryDto(
    CustomerLoyaltySummaryDto? Loyalty,
    CustomerDraftOrderListResult DraftOrders,
    CustomerRepurchaseSuggestionListResult RepurchaseSuggestions,
    MedicationAdherenceSummaryDto Adherence,
    CustomerConnectInboxDto? ConnectInbox = null);

public sealed record CustomerOrdersOverviewDto(
    CustomerDraftOrderListResult DraftOrders,
    CustomerPurchaseListResult Purchases,
    CustomerReservationListResult? Reservations);

public sealed record CustomerRemindersOverviewDto(
    MedicationReminderListResult Reminders,
    MedicationAdherenceSummaryDto Adherence,
    MedicationReminderListResult Due,
    CustomerRepurchaseSuggestionListResult RepurchaseSuggestions,
    CustomerFamilyMemberListResult Family);

public sealed record CustomerChatOverviewDto(
    IReadOnlyList<Customers.CustomerConsentDto> Consents,
    CustomerChatMessageListResult Messages,
    CustomerChatThreadDto Thread);

public interface ICustomerAppOverviewService
{
    Task<CustomerHomeSummaryDto> GetHomeSummaryAsync(
        Guid tenantId,
        Guid customerId,
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<CustomerOrdersOverviewDto> GetOrdersOverviewAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<CustomerRemindersOverviewDto> GetRemindersOverviewAsync(
        Guid tenantId,
        Guid customerId,
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<CustomerChatOverviewDto> GetChatOverviewAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default);
}
