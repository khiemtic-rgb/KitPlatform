using System.Text.Json;
using Microsoft.Extensions.Logging;
using KitPlatform.Application.Platform.Events;
using KitPlatform.Packs.Pharmacy;
using KitPlatform.Packs.Pharmacy.Infrastructure.Catalog.CsdlDuoc;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Events;

/// <summary>
/// Pack consumer for <see cref="PlatformEventTypes.SalesOrderCompleted"/> —
/// đẩy phiếu xuất bán lẻ lên CSDL dược khi EnableStockOutSync.
/// </summary>
internal sealed class PharmacySalesOrderCompletedHandler : IPlatformEventHandler
{
    private readonly ICsdlDuocStockOutSyncService _csdlSync;
    private readonly ILogger<PharmacySalesOrderCompletedHandler> _logger;

    public PharmacySalesOrderCompletedHandler(
        ICsdlDuocStockOutSyncService csdlSync,
        ILogger<PharmacySalesOrderCompletedHandler> logger)
    {
        _csdlSync = csdlSync;
        _logger = logger;
    }

    public IReadOnlySet<string> EventTypes { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { PlatformEventTypes.SalesOrderCompleted };

    public async Task HandleAsync(PlatformEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(envelope.Source, PharmacyPackDefinition.EventSource, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(envelope.Source, PlatformEventSources.PharmacyPack, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var orderNumber = TryReadString(envelope.Data, "orderNumber");
        var orderId = TryReadGuid(envelope.Data, "orderId") ?? envelope.AggregateId;

        _logger.LogInformation(
            "Pharmacy pack handled {EventType} order={OrderNumber} orderId={OrderId} tenant={TenantId}",
            envelope.EventType,
            orderNumber ?? "(unknown)",
            orderId,
            envelope.TenantId);

        if (orderId == Guid.Empty)
            return;

        try
        {
            await _csdlSync.SyncSalesOrderAsync(envelope.TenantId, orderId, orderNumber, cancellationToken);
        }
        catch (Exception ex)
        {
            // Never fail platform event dispatch because of CSDL sync.
            _logger.LogError(ex, "CSDL stock-out sync threw for order {OrderId}", orderId);
        }
    }

    private static string? TryReadString(object? data, string propertyName)
    {
        if (data is not JsonElement element || element.ValueKind != JsonValueKind.Object)
            return null;
        if (!element.TryGetProperty(propertyName, out var prop))
            return null;
        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
    }

    private static Guid? TryReadGuid(object? data, string propertyName)
    {
        if (data is not JsonElement element || element.ValueKind != JsonValueKind.Object)
            return null;
        if (!element.TryGetProperty(propertyName, out var prop))
            return null;
        return prop.ValueKind == JsonValueKind.String && Guid.TryParse(prop.GetString(), out var id) ? id : null;
    }
}
