using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Pharmacy.Catalog;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Catalog.CsdlDuoc;

public interface ICsdlDuocStockOutSyncService
{
    Task SyncSalesOrderAsync(Guid tenantId, Guid salesOrderId, string? orderNumber, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CsdlDuocSyncLogDto>> ListRecentAsync(Guid tenantId, int limit = 50, CancellationToken cancellationToken = default);
}

public sealed record CsdlDuocSyncLogDto(
    Guid Id,
    Guid SalesOrderId,
    string? OrderNumber,
    string Direction,
    string Status,
    string? RemoteTransactionId,
    string? RemoteStatus,
    int LineCount,
    int SkippedLineCount,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime UpdatedAt);

internal sealed class CsdlDuocStockOutSyncService : ICsdlDuocStockOutSyncService
{
    private static readonly ConcurrentDictionary<string, string> UnitIdCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeZoneInfo VietnamTz = ResolveVietnamTz();

    private readonly CsdlDuocSyncLogRepository _log;
    private readonly CsdlDuocTransactionClient _client;
    private readonly ICsdlDuocCredentialResolver _credentials;
    private readonly IOptionsMonitor<NationalDrugCatalogSettings> _options;
    private readonly ILogger<CsdlDuocStockOutSyncService> _logger;

    public CsdlDuocStockOutSyncService(
        CsdlDuocSyncLogRepository log,
        CsdlDuocTransactionClient client,
        ICsdlDuocCredentialResolver credentials,
        IOptionsMonitor<NationalDrugCatalogSettings> options,
        ILogger<CsdlDuocStockOutSyncService> logger)
    {
        _log = log;
        _client = client;
        _credentials = credentials;
        _options = options;
        _logger = logger;
    }

    public async Task SyncSalesOrderAsync(
        Guid tenantId,
        Guid salesOrderId,
        string? orderNumber,
        CancellationToken cancellationToken = default)
    {
        var creds = await _credentials.ResolveAsync(tenantId, cancellationToken);
        if (!creds.CanSyncStockOut)
        {
            _logger.LogDebug(
                "CSDL stock-out sync disabled — skip order {OrderId} source={Source} status={Status}",
                salesOrderId, creds.Source, creds.LinkStatus);
            return;
        }

        // Prefer tenant-linked sync; platform EnableStockOutSync still allows sandbox UAT.
        if (creds.IsTenantLinked
            && !string.Equals(creds.LinkStatus, "Connected", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("CSDL tenant link not Connected — skip order {OrderId}", salesOrderId);
            return;
        }

        var claimed = await _log.TryBeginAsync(tenantId, salesOrderId, orderNumber, cancellationToken);
        if (!claimed)
        {
            _logger.LogInformation("CSDL stock-out already logged for order {OrderId} — skip", salesOrderId);
            return;
        }

        var settings = _options.CurrentValue;
        try
        {
            var lines = await _log.LoadSaleLinesAsync(tenantId, salesOrderId, cancellationToken);
            if (lines.Count == 0)
            {
                await _log.UpdateAsync(
                    tenantId, salesOrderId, "skipped", null, null, 0, 0, null, null,
                    "Không tìm thấy dòng bán hoàn tất.", cancellationToken);
                return;
            }

            var header = lines[0];
            var items = new List<CsdlDuocStockOutItem>();
            var skipped = 0;
            var fallbackUnit = string.IsNullOrWhiteSpace(settings.DefaultCsdlUnitId)
                ? "U31"
                : settings.DefaultCsdlUnitId.Trim();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line.NationalDrugId)
                    || string.IsNullOrWhiteSpace(line.BatchNumber)
                    || line.ExpiryDate is null)
                {
                    skipped++;
                    continue;
                }

                var qty = (int)Math.Round(line.Quantity, MidpointRounding.AwayFromZero);
                if (qty <= 0)
                {
                    skipped++;
                    continue;
                }

                var unitId = await ResolveUnitIdCachedAsync(creds, line.NationalDrugId!, fallbackUnit, cancellationToken);
                items.Add(new CsdlDuocStockOutItem
                {
                    DrugId = line.NationalDrugId!.Trim(),
                    UnitId = unitId,
                    Quantity = qty,
                    BatchNo = line.BatchNumber!.Trim(),
                    PackagingSpecifications = string.IsNullOrWhiteSpace(line.Packaging) ? null : line.Packaging.Trim(),
                    ExpiryDate = line.ExpiryDate.Value.ToString("yyyy-MM-dd"),
                    Manufacturer = string.IsNullOrWhiteSpace(line.ManufacturerName)
                        ? null
                        : new CsdlDuocStockOutManufacturer
                        {
                            Name = line.ManufacturerName.Trim(),
                            Country = string.IsNullOrWhiteSpace(line.CountryCode) ? null : line.CountryCode.Trim(),
                        },
                    Price = Math.Round(line.UnitPrice, 2),
                });
            }

            if (items.Count == 0)
            {
                await _log.UpdateAsync(
                    tenantId, salesOrderId, "skipped", null, null, 0, skipped, null, null,
                    "Không có dòng đủ national_drug_id + lô + HSD để đẩy CSDL dược.",
                    cancellationToken);
                return;
            }

            var orderUtc = header.OrderDate.Kind switch
            {
                DateTimeKind.Utc => header.OrderDate,
                DateTimeKind.Local => header.OrderDate.ToUniversalTime(),
                _ => DateTime.SpecifyKind(header.OrderDate, DateTimeKind.Utc),
            };
            var txnDate = TimeZoneInfo.ConvertTimeFromUtc(orderUtc, VietnamTz);
            var practice = !string.IsNullOrWhiteSpace(creds.PracticeLicenseCode)
                ? creds.PracticeLicenseCode.Trim()
                : (string.IsNullOrWhiteSpace(settings.PracticeLicenseCode) ? null : settings.PracticeLicenseCode.Trim());

            var request = new CsdlDuocStockOutRequest
            {
                TransactionDate = txnDate.ToString("yyyy-MM-dd'T'HH:mm:ss"),
                Reason = "sale-retail",
                ReferenceNumber = header.OrderNumber.Length <= 50
                    ? header.OrderNumber
                    : header.OrderNumber[..50],
                PracticeLicenseCode = practice,
                Note = string.IsNullOrWhiteSpace(header.RetailFacilityCode)
                    ? null
                    : $"facility={header.RetailFacilityCode}",
                Items = items,
            };

            var (body, statusCode, raw) = await _client.PostStockOutAsync(creds, request, cancellationToken);
            if (statusCode is < 200 or >= 300 || string.IsNullOrWhiteSpace(body?.TransactionId))
            {
                await _log.UpdateAsync(
                    tenantId, salesOrderId, "error", null, null, items.Count, skipped, request, raw,
                    $"HTTP {statusCode}: {Truncate(raw, 800)}",
                    cancellationToken);
                return;
            }

            var remoteId = body.TransactionId!;
            string? remoteStatus = "submitted";
            try
            {
                var (st, stCode, _) = await _client.GetStockOutStatusAsync(creds, remoteId, cancellationToken);
                if (stCode is >= 200 and < 300)
                    remoteStatus = st?.Status ?? "submitted";
                else
                    remoteStatus = $"status_http_{stCode}";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CSDL stock-out status poll failed for {RemoteId}", remoteId);
            }

            await _log.UpdateAsync(
                tenantId, salesOrderId, "submitted", remoteId, remoteStatus, items.Count, skipped, request,
                new { transaction_id = remoteId, http = statusCode },
                null,
                cancellationToken);

            _logger.LogInformation(
                "CSDL stock-out submitted order={OrderNumber} remote={RemoteId} lines={Lines} skipped={Skipped}",
                header.OrderNumber, remoteId, items.Count, skipped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CSDL stock-out sync failed for order {OrderId}", salesOrderId);
            try
            {
                await _log.UpdateAsync(
                    tenantId, salesOrderId, "error", null, null, 0, 0, null, null, ex.Message, cancellationToken);
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "Failed to persist CSDL sync error for {OrderId}", salesOrderId);
            }
        }
    }

    public async Task<IReadOnlyList<CsdlDuocSyncLogDto>> ListRecentAsync(
        Guid tenantId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var rows = await _log.ListRecentAsync(tenantId, limit, cancellationToken);
        return rows.Select(r => new CsdlDuocSyncLogDto(
            r.Id, r.SalesOrderId, r.OrderNumber, r.Direction, r.Status,
            r.RemoteTransactionId, r.RemoteStatus, r.LineCount, r.SkippedLineCount,
            r.ErrorMessage, r.CreatedAt, r.UpdatedAt)).ToList();
    }

    private async Task<string> ResolveUnitIdCachedAsync(
        CsdlDuocEffectiveCredentials credentials,
        string drugId,
        string fallback,
        CancellationToken cancellationToken)
    {
        if (UnitIdCache.TryGetValue(drugId, out var cached))
            return cached;
        var unit = await _client.ResolveUnitIdAsync(credentials, drugId, fallback, cancellationToken) ?? fallback;
        UnitIdCache[drugId] = unit;
        return unit;
    }

    private static TimeZoneInfo ResolveVietnamTz()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
    }

    private static string Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? string.Empty
        : value.Length <= max ? value
        : value[..max] + "…";
}
