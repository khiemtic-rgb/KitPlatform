namespace KitPlatform.Application.CustomerApp;

public interface ICustomerPushService
{
    string? GetPublicKey();

    Task<PushSubscriptionStatusDto> GetStatusAsync(
        Guid tenantId,
        Guid customerAccountId,
        CancellationToken cancellationToken = default);

    Task RegisterSubscriptionAsync(
        Guid tenantId,
        Guid customerAccountId,
        RegisterPushSubscriptionRequest request,
        CancellationToken cancellationToken = default);

    Task UnregisterSubscriptionAsync(
        Guid tenantId,
        Guid customerAccountId,
        string endpoint,
        CancellationToken cancellationToken = default);

    Task<int> DispatchDueRemindersAsync(CancellationToken cancellationToken = default);

    Task<int> DispatchEngagementNotificationsAsync(CancellationToken cancellationToken = default);

    Task SendStaffChatReplyPushAsync(
        Guid tenantId,
        Guid customerId,
        string? staffName,
        string messageBody,
        CancellationToken cancellationToken = default);

    Task SendDraftOrderPushAsync(
        Guid tenantId,
        Guid customerId,
        string draftNumber,
        decimal totalAmount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// In-app (+ Web Push if subscribed) when Connect marks clinic Rx ready at pharmacy.
    /// </summary>
    Task NotifyConnectRxReadyAsync(
        Guid pharmacyTenantId,
        Guid customerId,
        string pharmacyDisplayName,
        string? summary,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends Web Push only (no legacy/customer_notifications insert). Used by <c>NotifyQueueWorker</c>.
    /// </summary>
    Task<bool> TryDispatchPushToCustomerAsync(
        Guid tenantId,
        Guid customerId,
        string title,
        string body,
        string category,
        string? href,
        CancellationToken cancellationToken = default);
}
