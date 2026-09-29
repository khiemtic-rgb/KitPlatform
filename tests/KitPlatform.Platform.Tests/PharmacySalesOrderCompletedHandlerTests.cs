using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using KitPlatform.Application.Platform.Events;
using KitPlatform.Packs.Pharmacy;
using KitPlatform.Packs.Pharmacy.Infrastructure.Catalog.CsdlDuoc;
using KitPlatform.Packs.Pharmacy.Infrastructure.Events;
using Moq;
using Xunit;

namespace KitPlatform.Platform.Tests;

public sealed class PharmacySalesOrderCompletedHandlerTests
{
    [Fact]
    public async Task Handler_accepts_pharmacy_pack_source_and_completes()
    {
        var handler = new PharmacySalesOrderCompletedHandler(
            Mock.Of<ICsdlDuocStockOutSyncService>(),
            NullLogger<PharmacySalesOrderCompletedHandler>.Instance);
        var data = JsonSerializer.SerializeToElement(new { orderId = Guid.NewGuid(), orderNumber = "SO-TEST-001" });

        await handler.HandleAsync(new PlatformEventEnvelope(
            Guid.NewGuid(),
            PlatformEventTypes.SalesOrderCompleted,
            1,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            PharmacyPackDefinition.EventSource,
            PlatformEventAggregateTypes.SalesOrder,
            Guid.NewGuid(),
            null,
            null,
            data));
    }

    [Fact]
    public async Task Handler_ignores_non_pharmacy_source()
    {
        var handler = new PharmacySalesOrderCompletedHandler(
            Mock.Of<ICsdlDuocStockOutSyncService>(),
            NullLogger<PharmacySalesOrderCompletedHandler>.Instance);

        await handler.HandleAsync(new PlatformEventEnvelope(
            Guid.NewGuid(),
            PlatformEventTypes.SalesOrderCompleted,
            1,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "pack:other",
            PlatformEventAggregateTypes.SalesOrder,
            Guid.NewGuid(),
            null,
            null,
            null));
    }
}
