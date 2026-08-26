namespace KitPlatform.Application.Success;

public static class GrowthOpportunityBuckets
{
    public const string RefillDue = "refill_due";
    public const string RefillOverdue = "refill_overdue";
    public const string SnoozedExpiring = "snoozed_expiring";
}

public sealed record GrowthOpportunityItemDto(
    Guid SuggestionId,
    Guid CustomerId,
    string CustomerName,
    string? CustomerPhone,
    string? OrderLabel,
    string OrderNumber,
    DateOnly? SuggestedForDate,
    DateTimeOffset? OrderDate,
    int? DaysOverdue,
    string Bucket,
    string Status);

public sealed record GrowthOpportunitiesTodayDto(
    DateOnly BusinessDate,
    int TotalCount,
    IReadOnlyList<GrowthOpportunityItemDto> RefillDue,
    IReadOnlyList<GrowthOpportunityItemDto> RefillOverdue,
    IReadOnlyList<GrowthOpportunityItemDto> SnoozedExpiring);

public sealed record GrowthCareNowResultDto(
    Guid SuggestionId,
    Guid DraftOrderId,
    string DraftNumber,
    Guid CustomerId,
    Guid CareActionId,
    bool AlreadyHadOpenDraft);

public sealed record GrowthWeeklyRefillReportDto(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    int DueCount,
    int NotifiedCount,
    int ConvertedCount,
    decimal AttributedRevenue);

public sealed record DormantBuyerItemDto(
    Guid CustomerId,
    string CustomerName,
    string? CustomerPhone,
    Guid LastOrderId,
    string LastOrderNumber,
    DateTimeOffset LastOrderDate,
    int DaysSinceLastBuy,
    decimal LastOrderTotal,
    Guid? WarehouseId);

public sealed record DormantBuyersDto(
    DateOnly BusinessDate,
    int DormantDays,
    int TotalCount,
    IReadOnlyList<DormantBuyerItemDto> Items);

public interface IGrowthDeskService
{
    Task<GrowthOpportunitiesTodayDto> GetOpportunitiesTodayAsync(
        CancellationToken cancellationToken = default);

    Task<int> CountOpportunitiesTodayAsync(
        CancellationToken cancellationToken = default);

    Task<GrowthCareNowResultDto> CareNowAsync(
        Guid suggestionId,
        CancellationToken cancellationToken = default);

    Task<GrowthWeeklyRefillReportDto> GetWeeklyRefillReportAsync(
        DateOnly? weekStart = null,
        CancellationToken cancellationToken = default);

    Task<DormantBuyersDto> GetDormantBuyersAsync(
        int dormantDays = 30,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<GrowthCareNowResultDto> CareDormantBuyerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);
}
