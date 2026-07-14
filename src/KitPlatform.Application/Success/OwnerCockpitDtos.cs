using KitPlatform.Application.Dashboard;

namespace KitPlatform.Application.Success;

public sealed record OwnerCockpitDto(
    DashboardOverviewDto Overview,
    OwnerCockpitSalesExtrasDto SalesExtras,
    OwnerCockpitInventoryExtrasDto InventoryExtras,
    OwnerCockpitCustomerExtrasDto Customers,
    OwnerCockpitAssessmentSnapshotDto? LatestAssessment,
    OwnerCockpitRiskStripDto? RiskStrip = null);

public sealed record OwnerCockpitSalesExtrasDto(
    decimal MonthNetTotal,
    int WeekOrderCount,
    int MonthOrderCount);

public sealed record OwnerCockpitInventoryExtrasDto(
    int NearExpirySkuCount,
    decimal NearExpiryStockValue);

public sealed record OwnerCockpitCustomerExtrasDto(
    int NewCustomers7d,
    int ReturningCustomers7d);

public sealed record OwnerCockpitAssessmentSnapshotDto(
    Guid SubmissionId,
    decimal? OverallScore,
    DateTime? CompletedAt,
    string Status);
