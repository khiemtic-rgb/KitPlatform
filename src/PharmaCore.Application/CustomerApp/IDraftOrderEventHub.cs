namespace PharmaCore.Application.CustomerApp;

public static class DraftOrderEventTypes
{
    public const string Sent = "sent";
    public const string Confirmed = "confirmed";
}

public sealed record DraftOrderEventPayload(
    Guid TenantId,
    Guid CustomerId,
    Guid DraftOrderId,
    string EventType,
    string DraftNumber);

public interface IDraftOrderEventHub
{
    IAsyncEnumerable<DraftOrderEventPayload> WatchCustomerAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken);

    IAsyncEnumerable<DraftOrderEventPayload> WatchStaffAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    void NotifySent(Guid tenantId, Guid customerId, Guid draftOrderId, string draftNumber);

    void NotifyConfirmed(Guid tenantId, Guid customerId, Guid draftOrderId, string draftNumber);
}
