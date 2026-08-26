using KitPlatform.Application.Dashboard;

namespace KitPlatform.Application.Success;

public sealed record OwnerCockpitDto(
    DashboardOverviewDto Overview,
    OwnerCockpitSalesExtrasDto SalesExtras,
    OwnerCockpitInventoryExtrasDto InventoryExtras,
    OwnerCockpitCustomerExtrasDto Customers,
    OwnerCockpitAssessmentSnapshotDto? LatestAssessment,
    OwnerCockpitRiskStripDto? RiskStrip = null,
    OwnerCockpitPeakHoursDto? PeakHours = null);

public sealed record OwnerCockpitSalesExtrasDto(
    decimal MonthNetTotal,
    int WeekOrderCount,
    int MonthOrderCount);

public sealed record OwnerCockpitInventoryExtrasDto(
    int NearExpirySkuCount,
    decimal NearExpiryStockValue,
    int UrgentNearExpirySkuCount,
    int UrgentExpiryDays);

public sealed record OwnerCockpitCustomerExtrasDto(
    int NewCustomers7d,
    int ReturningCustomers7d,
    int DormantBuyerCount,
    int DormantDays,
    int ActiveBuyerCount);

public sealed record OwnerCockpitAssessmentSnapshotDto(
    Guid SubmissionId,
    decimal? OverallScore,
    DateTime? CompletedAt,
    string Status);

/// <summary>Hour-of-day sales in VN timezone (peak staffing hint).</summary>
public sealed record OwnerCockpitPeakHoursDto(
    int WindowDays,
    int? PeakHour,
    int PeakOrderCount,
    decimal PeakRevenue,
    IReadOnlyList<OwnerCockpitHourBucketDto> Hours);

public sealed record OwnerCockpitHourBucketDto(int Hour, int OrderCount, decimal Revenue);
