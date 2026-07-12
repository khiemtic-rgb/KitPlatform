using KitPlatform.Application.Configuration;
using KitPlatform.Application.Core;
using KitPlatform.Application.CustomerApp;

namespace KitPlatform.Infrastructure.CustomerApp;

internal sealed class CustomerAppOverviewService : ICustomerAppOverviewService
{
    private readonly ICustomerLoyaltyService _loyalty;
    private readonly ICustomerDraftOrderService _draftOrders;
    private readonly ICustomerRepurchaseService _repurchase;
    private readonly ICustomerMedicationAdherenceService _adherence;
    private readonly ICustomerPurchaseService _purchases;
    private readonly ICustomerReservationService _reservations;
    private readonly ICustomerReminderService _reminders;
    private readonly ICustomerFamilyService _family;
    private readonly ICustomerAppConsentService _consents;
    private readonly ICustomerChatService _chat;
    private readonly ITenantPlatformSettings _platform;
    private readonly ICustomerAppConnectService _connect;

    public CustomerAppOverviewService(
        ICustomerLoyaltyService loyalty,
        ICustomerDraftOrderService draftOrders,
        ICustomerRepurchaseService repurchase,
        ICustomerMedicationAdherenceService adherence,
        ICustomerPurchaseService purchases,
        ICustomerReservationService reservations,
        ICustomerReminderService reminders,
        ICustomerFamilyService family,
        ICustomerAppConsentService consents,
        ICustomerChatService chat,
        ITenantPlatformSettings platform,
        ICustomerAppConnectService connect)
    {
        _loyalty = loyalty;
        _draftOrders = draftOrders;
        _repurchase = repurchase;
        _adherence = adherence;
        _purchases = purchases;
        _reservations = reservations;
        _reminders = reminders;
        _family = family;
        _consents = consents;
        _chat = chat;
        _platform = platform;
        _connect = connect;
    }

    public async Task<CustomerHomeSummaryDto> GetHomeSummaryAsync(
        Guid tenantId,
        Guid customerId,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var loyaltyTask = _loyalty.GetSummaryAsync(tenantId, customerId, cancellationToken);
        var draftsTask = _draftOrders.ListForCustomerAsync(tenantId, customerId, cancellationToken);
        var repurchaseTask = _repurchase.ListAsync(tenantId, customerId, accountId, cancellationToken);
        var adherenceTask = _adherence.GetSummaryAsync(tenantId, customerId, cancellationToken);
        var connectTask = _connect.GetInboxAsync(tenantId, customerId, cancellationToken);

        await Task.WhenAll(loyaltyTask, draftsTask, repurchaseTask, adherenceTask, connectTask);

        return new CustomerHomeSummaryDto(
            await loyaltyTask,
            await draftsTask,
            await repurchaseTask,
            await adherenceTask,
            await connectTask);
    }

    public async Task<CustomerOrdersOverviewDto> GetOrdersOverviewAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var draftsTask = _draftOrders.ListForCustomerAsync(tenantId, customerId, cancellationToken);
        var purchasesTask = _purchases.ListForCustomerAsync(tenantId, customerId, cancellationToken);
        var reservationsTask = LoadReservationsIfEnabledAsync(tenantId, customerId, cancellationToken);

        await Task.WhenAll(draftsTask, purchasesTask, reservationsTask);

        return new CustomerOrdersOverviewDto(
            await draftsTask,
            await purchasesTask,
            await reservationsTask);
    }

    private async Task<CustomerReservationListResult?> LoadReservationsIfEnabledAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        if (!await _platform.IsModuleEnabledAsync(PlatformModuleCodes.Reservations, cancellationToken))
            return null;

        return await _reservations.ListForCustomerAsync(tenantId, customerId, cancellationToken);
    }

    public async Task<CustomerRemindersOverviewDto> GetRemindersOverviewAsync(
        Guid tenantId,
        Guid customerId,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var remindersTask = _reminders.ListAsync(tenantId, customerId, includeInactive: true, cancellationToken);
        var adherenceTask = _adherence.GetSummaryAsync(tenantId, customerId, cancellationToken);
        var dueTask = _adherence.ListDueAsync(tenantId, customerId, cancellationToken);
        var repurchaseTask = _repurchase.ListAsync(tenantId, customerId, accountId, cancellationToken);
        var familyTask = _family.ListAsync(tenantId, accountId, cancellationToken);

        await Task.WhenAll(remindersTask, adherenceTask, dueTask, repurchaseTask, familyTask);

        return new CustomerRemindersOverviewDto(
            await remindersTask,
            await adherenceTask,
            await dueTask,
            await repurchaseTask,
            await familyTask);
    }

    public async Task<CustomerChatOverviewDto> GetChatOverviewAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        const int messageLimit = 50;

        var consentsTask = _consents.GetConsentsAsync(tenantId, customerId, cancellationToken);
        var messagesTask = _chat.ListMessagesAsync(tenantId, customerId, beforeId: null, messageLimit, cancellationToken);
        var threadTask = _chat.GetThreadAsync(tenantId, customerId, cancellationToken);

        await Task.WhenAll(consentsTask, messagesTask, threadTask);

        await _chat.MarkCustomerReadAsync(tenantId, customerId, cancellationToken);

        var thread = await threadTask;
        var clearedThread = thread with { UnreadCount = 0 };

        return new CustomerChatOverviewDto(
            await consentsTask,
            await messagesTask,
            clearedThread);
    }
}
