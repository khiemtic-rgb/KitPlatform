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
    DateTime? ClosedAt,
    string? OpenedByName = null,
    string? ClosedByName = null);

/// <summary>EP01 additive risk strip — optional for older clients if null.</summary>
public sealed record OwnerCockpitRiskStripDto(
    decimal CashVarianceThreshold,
    int ClosedShiftCountToday,
    int OpenShiftCountToday,
    int CashVarianceAlertCount,
    decimal MaxAbsCashVarianceToday,
    string? TopAlertShiftNumber,
    /// <summary>AC3 — not_done | in_progress | done | has_variance</summary>
    string CycleCountStatusToday = "not_done",
    Guid? CycleCountAdjustmentId = null,
    string? CycleCountAdjustmentNumber = null);

/// <summary>
/// AC4 — three by-employee reports. Attribution uses as-built proxies only
/// (no cancelled_by / discount_applied_by columns).
/// </summary>
public sealed record LossEmployeeReportsDto(
    DateTime FromUtc,
    DateTime ToUtc,
    Guid? BranchId,
    string AttributionNotes,
    IReadOnlyList<LossEmployeeCancelRowDto> Cancellations,
    IReadOnlyList<LossEmployeeDiscountRowDto> Discounts,
    IReadOnlyList<LossEmployeeAdjustmentRowDto> Adjustments,
    IReadOnlyList<LossCashVarianceByCloserRowDto>? CashVarianceByCloser = null);

public sealed record LossEmployeeCancelRowDto(
    Guid? EmployeeId,
    string EmployeeName,
    int CancelCount,
    decimal CancelValue);

public sealed record LossEmployeeDiscountRowDto(
    Guid? EmployeeId,
    string EmployeeName,
    int OrderCount,
    decimal OrderDiscountAmount,
    decimal LineDiscountAmount,
    decimal TotalPosDiscount);

public sealed record LossEmployeeAdjustmentRowDto(
    Guid? EmployeeId,
    string EmployeeName,
    int AdjustmentCount,
    decimal AbsVarianceValue);

/// <summary>
/// AC1 — Loss audit feed composed from kit_audit.activity_log / audit_logs (no success_loss_event table).
/// </summary>
public sealed record LossAuditFeedDto(
    DateTime FromUtc,
    DateTime ToUtc,
    Guid? BranchId,
    Guid? UserId,
    string? EventType,
    string AttributionNotes,
    int Total,
    int Page,
    int PageSize,
    IReadOnlyList<LossAuditFeedItemDto> Items);

public sealed record LossAuditFeedItemDto(
    Guid Id,
    DateTime OccurredAt,
    string EventType,
    Guid? ActorUserId,
    string? ActorUsername,
    string Summary,
    string EntityType,
    Guid? EntityId,
    string? DocumentNumber,
    string? DocumentHref,
    Guid? BranchId,
    string? BranchName);

/// <summary>AC3 — cycle count compose over inventory counting sessions (reason tag [cycle_count]).</summary>
public sealed record LossCycleCountSuggestionDto(
    Guid ProductId,
    string Sku,
    string ProductName,
    string Source,
    decimal? OnHandQty,
    decimal? MinStock);

public sealed record LossCycleCountSuggestionsDto(
    Guid WarehouseId,
    string WarehouseName,
    Guid BranchId,
    string BranchName,
    IReadOnlyList<LossCycleCountSuggestionDto> Items);

public sealed record LossCycleCountSessionDto(
    Guid AdjustmentId,
    string AdjustmentNumber,
    Guid WarehouseId,
    string WarehouseName,
    string Reason,
    string CountHref,
    IReadOnlyList<LossCycleCountSuggestionDto> Suggestions);

public sealed record LossCycleCountStatusDto(
    DateOnly BusinessDate,
    string Status,
    Guid? AdjustmentId,
    string? AdjustmentNumber,
    string? CountHref,
    int VarianceSkuCount);

public sealed record LossCycleCountVarianceRowDto(
    DateOnly BusinessDate,
    Guid ProductId,
    string Sku,
    string ProductName,
    Guid AdjustmentId,
    string AdjustmentNumber,
    decimal SystemQuantity,
    decimal ActualQuantity,
    decimal DifferenceQuantity,
    string CountHref);

public sealed record LossCycleCountVarianceReportDto(
    DateTime FromUtc,
    DateTime ToUtc,
    Guid? BranchId,
    IReadOnlyList<LossCycleCountVarianceRowDto> Items);
