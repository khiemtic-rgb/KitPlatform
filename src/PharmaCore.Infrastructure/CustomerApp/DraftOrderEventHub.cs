using System.Runtime.CompilerServices;
using System.Threading.Channels;
using PharmaCore.Application.CustomerApp;

namespace PharmaCore.Infrastructure.CustomerApp;

internal sealed class DraftOrderEventHub : IDraftOrderEventHub
{
    private sealed class Subscription
    {
        public required Func<DraftOrderEventPayload, bool> Filter { get; init; }
        public required ChannelWriter<DraftOrderEventPayload> Writer { get; init; }
    }

    private readonly object _gate = new();
    private readonly List<Subscription> _subscriptions = [];

    public async IAsyncEnumerable<DraftOrderEventPayload> WatchCustomerAsync(
        Guid tenantId,
        Guid customerId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var payload in WatchAsync(
                           evt => evt.TenantId == tenantId && evt.CustomerId == customerId,
                           cancellationToken))
        {
            yield return payload;
        }
    }

    public async IAsyncEnumerable<DraftOrderEventPayload> WatchStaffAsync(
        Guid tenantId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var payload in WatchAsync(evt => evt.TenantId == tenantId, cancellationToken))
        {
            yield return payload;
        }
    }

    public void NotifySent(Guid tenantId, Guid customerId, Guid draftOrderId, string draftNumber) =>
        Publish(new DraftOrderEventPayload(
            tenantId,
            customerId,
            draftOrderId,
            DraftOrderEventTypes.Sent,
            draftNumber));

    public void NotifyConfirmed(Guid tenantId, Guid customerId, Guid draftOrderId, string draftNumber) =>
        Publish(new DraftOrderEventPayload(
            tenantId,
            customerId,
            draftOrderId,
            DraftOrderEventTypes.Confirmed,
            draftNumber));

    private async IAsyncEnumerable<DraftOrderEventPayload> WatchAsync(
        Func<DraftOrderEventPayload, bool> filter,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<DraftOrderEventPayload>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        var subscription = new Subscription { Filter = filter, Writer = channel.Writer };
        lock (_gate)
        {
            _subscriptions.Add(subscription);
        }

        try
        {
            await foreach (var payload in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return payload;
            }
        }
        finally
        {
            lock (_gate)
            {
                _subscriptions.Remove(subscription);
            }

            channel.Writer.TryComplete();
        }
    }

    private void Publish(DraftOrderEventPayload payload)
    {
        lock (_gate)
        {
            foreach (var subscription in _subscriptions)
            {
                if (!subscription.Filter(payload))
                    continue;

                subscription.Writer.TryWrite(payload);
            }
        }
    }
}
