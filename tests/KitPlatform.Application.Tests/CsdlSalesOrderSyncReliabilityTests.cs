using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using KitPlatform.Application.Platform.Events;
using KitPlatform.Infrastructure.Platform.Events;
using KitPlatform.Packs.Pharmacy;
using KitPlatform.Packs.Pharmacy.Catalog;
using KitPlatform.Packs.Pharmacy.Infrastructure.Catalog.CsdlDuoc;
using KitPlatform.Packs.Pharmacy.Infrastructure.Events;
using Xunit;

namespace KitPlatform.Tests;

public sealed class CsdlSalesOrderSyncReliabilityTests
{
    [Fact]
    public async Task Successful_sync_lets_the_worker_mark_the_event_dispatched()
    {
        var sync = new RecordingStockOutSync();
        var dispatcher = Dispatcher(sync);
        var envelope = Envelope(Guid.NewGuid(), Guid.NewGuid());

        var dispatched = await WorkerWouldMarkDispatchedAsync(dispatcher, envelope);

        Assert.True(dispatched);
        Assert.Equal(1, sync.Calls);
        Assert.Equal(envelope.TenantId, sync.TenantId);
        Assert.Equal(envelope.AggregateId, sync.SalesOrderId);
    }

    [Fact]
    public async Task Failed_sync_is_not_marked_dispatched()
    {
        var sync = new RecordingStockOutSync { Failure = new InvalidOperationException("csdl down") };
        var dispatcher = Dispatcher(sync);

        var dispatched = await WorkerWouldMarkDispatchedAsync(dispatcher, Envelope(Guid.NewGuid(), Guid.NewGuid()));

        Assert.False(dispatched);
        Assert.Equal(1, sync.Calls);
    }

    [Fact]
    public async Task Disabled_sync_still_completes_and_can_be_dispatched()
    {
        var settings = new NationalDrugCatalogSettings();
        Assert.False(settings.EnableStockOutSync);
        Assert.False(settings.CanSyncStockOut);

        var credentials = new CsdlDuocEffectiveCredentials(
            Guid.NewGuid(), "platform", "sandbox", "https://api-sandbox.csdlduoc.com.vn/v2",
            "user", "secret", false, null, EnableStockOutSync: false, EnableStockInSync: false, LinkStatus: "Connected");
        Assert.False(credentials.CanSyncStockOut);

        var sync = new RecordingStockOutSync();
        var dispatched = await WorkerWouldMarkDispatchedAsync(Dispatcher(sync), Envelope(Guid.NewGuid(), Guid.NewGuid()));

        Assert.True(dispatched);
        Assert.Equal(1, sync.Calls);
    }

    [Fact]
    public async Task Existing_worker_retry_is_outside_the_handler()
    {
        var sync = new RecordingStockOutSync { Failure = new InvalidOperationException("csdl down") };
        var dispatcher = Dispatcher(sync);
        var envelope = Envelope(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(5, new PlatformEventOptions().MaxDispatchAttempts);
        Assert.False(await WorkerWouldMarkDispatchedAsync(dispatcher, envelope));
        Assert.False(await WorkerWouldMarkDispatchedAsync(dispatcher, envelope));
        Assert.Equal(2, sync.Calls);
    }

    [Fact]
    public async Task Event_tenant_is_the_tenant_passed_to_csdl_sync()
    {
        var tenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var sync = new RecordingStockOutSync();
        await WorkerWouldMarkDispatchedAsync(Dispatcher(sync), Envelope(tenantId, Guid.NewGuid()));

        Assert.Equal(tenantId, sync.TenantId);
    }

    [Fact]
    public async Task Successful_sale_completion_event_still_syncs_once()
    {
        var orderId = Guid.NewGuid();
        var sync = new RecordingStockOutSync();
        var data = JsonSerializer.SerializeToElement(new { orderId, orderNumber = "SO-1", status = "completed" });
        var envelope = new PlatformEventEnvelope(
            Guid.NewGuid(),
            PlatformEventTypes.SalesOrderCompleted,
            1,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            PharmacyPackDefinition.EventSource,
            PlatformEventAggregateTypes.SalesOrder,
            orderId,
            Guid.NewGuid(),
            null,
            data);

        Assert.True(await WorkerWouldMarkDispatchedAsync(Dispatcher(sync), envelope));
        Assert.Equal(1, sync.Calls);
        Assert.Equal("SO-1", sync.OrderNumber);
        Assert.Equal(orderId, sync.SalesOrderId);
    }

    [Fact]
    public void Handler_does_not_publish_platform_events_or_own_a_retry_loop()
    {
        var parameters = typeof(PharmacySalesOrderCompletedHandler)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single()
            .GetParameters();

        Assert.DoesNotContain(parameters, parameter =>
            parameter.ParameterType.Name.Contains("EventWriter", StringComparison.Ordinal)
            || parameter.ParameterType.Name.Contains("PlatformEventWorker", StringComparison.Ordinal));
        Assert.Contains(parameters, parameter => parameter.ParameterType == typeof(ICsdlDuocStockOutSyncService));
    }

    [Fact]
    public async Task Non_pharmacy_source_does_not_call_csdl_sync()
    {
        var sync = new RecordingStockOutSync();
        var envelope = Envelope(Guid.NewGuid(), Guid.NewGuid()) with { Source = "pack:other" };

        Assert.True(await WorkerWouldMarkDispatchedAsync(Dispatcher(sync), envelope));
        Assert.Equal(0, sync.Calls);
    }

    private static InProcessPlatformEventDispatcher Dispatcher(RecordingStockOutSync sync) =>
        new(
            [new PharmacySalesOrderCompletedHandler(sync, NullLogger<PharmacySalesOrderCompletedHandler>.Instance)],
            NullLogger<InProcessPlatformEventDispatcher>.Instance);

    private static PlatformEventEnvelope Envelope(Guid tenantId, Guid orderId) =>
        new(
            Guid.NewGuid(),
            PlatformEventTypes.SalesOrderCompleted,
            1,
            tenantId,
            DateTimeOffset.UtcNow,
            PlatformEventSources.PharmacyPack,
            PlatformEventAggregateTypes.SalesOrder,
            orderId,
            null,
            null,
            JsonSerializer.SerializeToElement(new { orderId, orderNumber = "SO-1" }));

    /// <summary>
    /// Same branch as <see cref="PlatformEventWorker"/>: dispatch success is the only path that sets dispatched.
    /// </summary>
    private static async Task<bool> WorkerWouldMarkDispatchedAsync(
        IPlatformEventDispatcher dispatcher,
        PlatformEventEnvelope envelope)
    {
        try
        {
            await dispatcher.DispatchAsync(envelope);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private sealed class RecordingStockOutSync : ICsdlDuocStockOutSyncService
    {
        public Exception? Failure { get; init; }
        public int Calls { get; private set; }
        public Guid? TenantId { get; private set; }
        public Guid? SalesOrderId { get; private set; }
        public string? OrderNumber { get; private set; }

        public Task SyncSalesOrderAsync(
            Guid tenantId,
            Guid salesOrderId,
            string? orderNumber,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            TenantId = tenantId;
            SalesOrderId = salesOrderId;
            OrderNumber = orderNumber;
            if (Failure is not null)
                throw Failure;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CsdlDuocSyncLogDto>> ListRecentAsync(
            Guid tenantId,
            int limit = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CsdlDuocSyncLogDto>>([]);
    }
}
