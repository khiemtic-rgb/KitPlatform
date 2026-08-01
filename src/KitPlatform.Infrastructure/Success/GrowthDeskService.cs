using KitPlatform.Application.Abstractions;
using KitPlatform.Application.CustomerApp;
using KitPlatform.Application.Success;

namespace KitPlatform.Infrastructure.Success;

internal sealed class GrowthDeskService : IGrowthDeskService
{
    private readonly GrowthDeskRepository _repo;
    private readonly ICustomerDraftOrderService _draftOrders;
    private readonly ITenantContext _tenant;

    public GrowthDeskService(
        GrowthDeskRepository repo,
        ICustomerDraftOrderService draftOrders,
        ITenantContext tenant)
    {
        _repo = repo;
        _draftOrders = draftOrders;
        _tenant = tenant;
    }

    public async Task<GrowthOpportunitiesTodayDto> GetOpportunitiesTodayAsync(
        CancellationToken cancellationToken = default)
    {
        var (businessDate, rows) = await _repo.ListOpportunitiesTodayAsync(
            _tenant.TenantId,
            cancellationToken);

        var items = rows.Select(MapItem).ToList();
        return new GrowthOpportunitiesTodayDto(
            businessDate,
            items.Count,
            items.Where(i => i.Bucket == GrowthOpportunityBuckets.RefillDue).ToList(),
            items.Where(i => i.Bucket == GrowthOpportunityBuckets.RefillOverdue).ToList(),
            items.Where(i => i.Bucket == GrowthOpportunityBuckets.SnoozedExpiring).ToList());
    }

    public Task<int> CountOpportunitiesTodayAsync(CancellationToken cancellationToken = default) =>
        _repo.CountOpportunitiesTodayAsync(_tenant.TenantId, cancellationToken);

    public async Task<GrowthCareNowResultDto> CareNowAsync(
        Guid suggestionId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenant.TenantId;
        var existing = await _repo.FindOpenCareDraftAsync(tenantId, suggestionId, cancellationToken);
        if (existing is not null)
        {
            return new GrowthCareNowResultDto(
                suggestionId,
                existing.DraftOrderId,
                existing.DraftNumber,
                existing.CustomerId,
                existing.CareActionId,
                AlreadyHadOpenDraft: true);
        }

        var suggestion = await _repo.GetSuggestionForCareAsync(tenantId, suggestionId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gợi ý tái mua.");

        var status = suggestion.Status.Trim().ToLowerInvariant();
        if (status is "converted")
            throw new InvalidOperationException("Gợi ý này đã chuyển thành đơn rồi.");
        if (status is "dismissed" or "expired")
            throw new InvalidOperationException("Gợi ý không còn hiệu lực để chăm sóc.");
        if (status is not ("pending" or "snoozed"))
            throw new InvalidOperationException("Không thể chăm sóc gợi ý này.");

        var lines = await _repo.ListOrderLinesForDraftAsync(
            tenantId,
            suggestion.SalesOrderId,
            cancellationToken);
        if (lines.Count == 0)
            throw new InvalidOperationException("Đơn gốc không còn dòng thuốc để tạo draft.");

        var label = string.IsNullOrWhiteSpace(suggestion.OrderLabel)
            ? $"#{suggestion.OrderNumber}"
            : suggestion.OrderLabel.Trim();
        var notes = $"Growth Desk · Chăm sóc ngay · tái mua {label}";

        var draft = await _draftOrders.CreateAsync(
            tenantId,
            _tenant.UserId,
            new UpsertCustomerDraftOrderRequest(
                suggestion.CustomerId,
                ChatThreadId: null,
                WarehouseId: suggestion.WarehouseId,
                PriceType: 1,
                Items: lines
                    .Select(l => new CustomerDraftOrderLineRequest(
                        l.ProductId,
                        l.ProductUnitId,
                        l.Quantity))
                    .ToList(),
                Notes: notes),
            cancellationToken);

        var careActionId = await _repo.InsertCareActionAsync(
            tenantId,
            suggestionId,
            suggestion.CustomerId,
            _tenant.UserId,
            draft.Id,
            notes,
            cancellationToken);

        return new GrowthCareNowResultDto(
            suggestionId,
            draft.Id,
            draft.DraftNumber,
            suggestion.CustomerId,
            careActionId,
            AlreadyHadOpenDraft: false);
    }

    public async Task<GrowthWeeklyRefillReportDto> GetWeeklyRefillReportAsync(
        DateOnly? weekStart = null,
        CancellationToken cancellationToken = default)
    {
        var today = await _repo.GetVnTodayAsync(cancellationToken);
        var start = weekStart ?? StartOfWeekMonday(today);
        var end = start.AddDays(6);
        var row = await _repo.GetWeeklyRefillAsync(_tenant.TenantId, start, end, cancellationToken);
        return new GrowthWeeklyRefillReportDto(
            start,
            end,
            row.DueCount,
            row.NotifiedCount,
            row.ConvertedCount,
            row.AttributedRevenue);
    }

    private static DateOnly StartOfWeekMonday(DateOnly day)
    {
        var offset = ((int)day.DayOfWeek + 6) % 7; // Monday = 0
        return day.AddDays(-offset);
    }

    private static GrowthOpportunityItemDto MapItem(GrowthDeskRepository.OpportunityRow row) =>
        new(
            row.SuggestionId,
            row.CustomerId,
            row.CustomerName,
            row.CustomerPhone,
            row.OrderLabel,
            row.OrderNumber,
            row.SuggestedForDate,
            row.OrderDate is null
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(row.OrderDate.Value, DateTimeKind.Utc)),
            row.DaysOverdue,
            row.Bucket ?? "",
            row.Status);
}
