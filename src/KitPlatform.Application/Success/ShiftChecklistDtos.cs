namespace KitPlatform.Application.Success;

public sealed record ShiftChecklistOpenShiftDto(Guid Id, string ShiftNumber, string WarehouseName);

public sealed record ShiftChecklistTodayDto(
    DateOnly BusinessDate,
    Guid? BranchId,
    IReadOnlyList<ShiftChecklistBranchOptionDto> Branches,
    ShiftChecklistKindStatusDto Open,
    ShiftChecklistKindStatusDto Close,
    IReadOnlyList<ShiftChecklistOpenShiftDto>? OpenShifts = null);

public sealed record ShiftChecklistBranchOptionDto(Guid Id, string Name, string? Code);

public sealed record ShiftChecklistKindStatusDto(
    string Kind,
    string Status,
    Guid? RunId,
    int CheckedCount,
    int TotalCount,
    int RequiredMissingCount,
    DateTime? CompletedAt,
    Guid? SalesShiftId = null);

public sealed record ShiftChecklistRunDto(
    Guid Id,
    Guid BranchId,
    string BranchName,
    string Kind,
    DateOnly BusinessDate,
    string Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    IReadOnlyList<ShiftChecklistRunItemDto> Items);

public sealed record ShiftChecklistRunItemDto(
    Guid Id,
    string Label,
    bool IsRequired,
    bool IsChecked,
    DateTime? CheckedAt,
    Guid? CheckedByUserId = null);

public sealed record StartShiftChecklistRequest(Guid BranchId, string Kind, Guid? SalesShiftId = null);

public sealed record SetShiftChecklistItemRequest(bool Checked);
