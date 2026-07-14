namespace KitPlatform.Application.Success;

/// <summary>AC2 — cash reconciliation composed from existing sales_shifts (no new cash tables).</summary>
public sealed record LossCashVarianceTodayDto(
    DateOnly BusinessDate,
    decimal Threshold,
    int ClosedShiftCount,
    int OpenShiftCount,
    int AlertCount,
    decimal MaxAbsVariance,
    IReadOnlyList<LossCashVarianceShiftDto> Shifts);

public sealed record LossCashVarianceShiftDto(
    Guid ShiftId,
    string ShiftNumber,
    Guid WarehouseId,
    string WarehouseName,
    Guid BranchId,
    string BranchName,
    string Status,
    decimal OpeningCash,
    decimal? ClosingCash,
    decimal? ExpectedCash,
    decimal? CashVariance,
    decimal AbsCashVariance,
    bool IsAlert,
    DateTime OpenedAt,
    DateTime? ClosedAt);

/// <summary>EP01 additive risk strip — optional for older clients if null.</summary>
public sealed record OwnerCockpitRiskStripDto(
    decimal CashVarianceThreshold,
    int ClosedShiftCountToday,
    int OpenShiftCountToday,
    int CashVarianceAlertCount,
    decimal MaxAbsCashVarianceToday,
    string? TopAlertShiftNumber);
