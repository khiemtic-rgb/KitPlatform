using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Pharmacy.Catalog;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Catalog.CsdlDuoc;

public interface ICsdlDuocStockInSyncService
{
    Task SyncGoodsReceiptAsync(
        Guid tenantId,
        Guid goodsReceiptId,
        string? grnNumber,
        CancellationToken cancellationToken = default);
}

internal sealed class CsdlDuocStockInSyncService : ICsdlDuocStockInSyncService
{
    private const string Direction = "stock-in";
    private static readonly ConcurrentDictionary<string, string> UnitIdCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeZoneInfo VietnamTz = ResolveVietnamTz();

    private readonly CsdlDuocSyncLogRepository _log;
    private readonly CsdlDuocTransactionClient _client;
    private readonly ICsdlDuocCredentialResolver _credentials;
    private readonly IOptionsMonitor<NationalDrugCatalogSettings> _options;
    private readonly ILogger<CsdlDuocStockInSyncService> _logger;

    public CsdlDuocStockInSyncService(
        CsdlDuocSyncLogRepository log,
        CsdlDuocTransactionClient client,
        ICsdlDuocCredentialResolver credentials,
        IOptionsMonitor<NationalDrugCatalogSettings> options,
        ILogger<CsdlDuocStockInSyncService> logger)
    {
        _log = log;
        _client = client;
        _credentials = credentials;
        _options = options;
        _logger = logger;
    }

    public async Task SyncGoodsReceiptAsync(
        Guid tenantId,
        Guid goodsReceiptId,
        string? grnNumber,
        CancellationToken cancellationToken = default)
    {
        var creds = await _credentials.ResolveAsync(tenantId, cancellationToken);
        if (!creds.CanSyncStockIn)
        {
            _logger.LogDebug(
                "CSDL stock-in sync disabled — skip GRN {GrnId} source={Source} status={Status}",
                goodsReceiptId, creds.Source, creds.LinkStatus);
            return;
        }

        if (creds.IsTenantLinked
            && !string.Equals(creds.LinkStatus, "Connected", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("CSDL tenant link not Connected — skip GRN {GrnId}", goodsReceiptId);
            return;
        }

        var claimed = await _log.TryBeginAsync(
            tenantId, goodsReceiptId, grnNumber, cancellationToken, Direction);
        if (!claimed)
        {
            _logger.LogInformation("CSDL stock-in already logged for GRN {GrnId} — skip", goodsReceiptId);
            return;
        }

        var settings = _options.CurrentValue;
        try
        {
            var lines = await _log.LoadGrnLinesAsync(tenantId, goodsReceiptId, cancellationToken);
            if (lines.Count == 0)
            {
                await _log.UpdateAsync(
                    tenantId, goodsReceiptId, "skipped", null, null, 0, 0, null, null,
                    "Không tìm thấy dòng phiếu nhập hoàn tất.", cancellationToken, Direction);
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
                    Price = Math.Round(line.UnitCost, 2),
                });
            }

            if (items.Count == 0)
            {
                await _log.UpdateAsync(
                    tenantId, goodsReceiptId, "skipped", null, null, 0, skipped, null, null,
                    "Không có dòng đủ national_drug_id + lô + HSD để đẩy CSDL dược.",
                    cancellationToken, Direction);
                return;
            }

            var receiptUtc = header.ReceiptDate.Kind switch
            {
                DateTimeKind.Utc => header.ReceiptDate,
                DateTimeKind.Local => header.ReceiptDate.ToUniversalTime(),
                _ => DateTime.SpecifyKind(header.ReceiptDate, DateTimeKind.Utc),
            };
            var txnDate = TimeZoneInfo.ConvertTimeFromUtc(receiptUtc, VietnamTz);
            var practice = !string.IsNullOrWhiteSpace(creds.PracticeLicenseCode)
                ? creds.PracticeLicenseCode.Trim()
                : (string.IsNullOrWhiteSpace(settings.PracticeLicenseCode) ? null : settings.PracticeLicenseCode.Trim());

            var request = new CsdlDuocStockInRequest
            {
                TransactionDate = txnDate.ToString("yyyy-MM-dd'T'HH:mm:ss"),
                Reason = "purchase",
                ReferenceNumber = header.GrnNumber.Length <= 50
                    ? header.GrnNumber
                    : header.GrnNumber[..50],
                PracticeLicenseCode = practice,
                Items = items,
            };

            var (body, statusCode, raw) = await _client.PostStockInAsync(creds, request, cancellationToken);
            if (statusCode is < 200 or >= 300 || string.IsNullOrWhiteSpace(body?.TransactionId))
            {
                await _log.UpdateAsync(
                    tenantId, goodsReceiptId, "error", null, null, items.Count, skipped, request, raw,
                    $"HTTP {statusCode}: {Truncate(raw, 800)}",
                    cancellationToken, Direction);
                return;
            }

            var remoteId = body.TransactionId!;
            string? remoteStatus = "submitted";
            try
            {
                var (st, stCode, _) = await _client.GetStockInStatusAsync(creds, remoteId, cancellationToken);
                if (stCode is >= 200 and < 300)
                    remoteStatus = st?.Status ?? "submitted";
                else
                    remoteStatus = $"status_http_{stCode}";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CSDL stock-in status poll failed for {RemoteId}", remoteId);
            }

            await _log.UpdateAsync(
                tenantId, goodsReceiptId, "submitted", remoteId, remoteStatus, items.Count, skipped, request,
                new { transaction_id = remoteId, http = statusCode },
                null,
                cancellationToken, Direction);

            _logger.LogInformation(
                "CSDL stock-in submitted grn={GrnNumber} remote={RemoteId} lines={Lines} skipped={Skipped}",
                header.GrnNumber, remoteId, items.Count, skipped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CSDL stock-in sync failed for GRN {GrnId}", goodsReceiptId);
            try
            {
                await _log.UpdateAsync(
                    tenantId, goodsReceiptId, "error", null, null, 0, 0, null, null, ex.Message,
                    cancellationToken, Direction);
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "Failed to persist CSDL stock-in error for {GrnId}", goodsReceiptId);
            }
        }
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

internal sealed class NoOpCsdlDuocStockInSyncService : ICsdlDuocStockInSyncService
{
    public Task SyncGoodsReceiptAsync(
        Guid tenantId,
        Guid goodsReceiptId,
        string? grnNumber,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
