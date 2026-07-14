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
    IReadOnlyList<LossEmployeeAdjustmentRowDto> Adjustments);

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
