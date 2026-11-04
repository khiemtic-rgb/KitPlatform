namespace KitPlatform.Application.Learning;

public static class OperationalTaskRules
{
    public const string Open = "open";
    public const string InProgress = "in_progress";
    public const string Done = "done";
    public const string Cancelled = "cancelled";

    public static bool CanAssign(bool hasLearningWrite) => hasLearningWrite;

    public static bool CanView(bool canManage, bool isAssignee) => canManage || isAssignee;

    public static bool CanTransition(string? from, string? to, bool canManage = true)
    {
        var current = from?.Trim().ToLowerInvariant();
        var target = to?.Trim().ToLowerInvariant();
        return (current, target) switch
        {
            (Open, InProgress) => true,
            (Open, Done) => true,
            (Open, Cancelled) => canManage,
            (InProgress, Done) => true,
            (InProgress, Cancelled) => canManage,
            (InProgress, Open) => true,
            _ => false,
        };
    }
}

public sealed record OperationalShiftOptionDto(
    Guid Id,
    string ShiftNumber,
    string WarehouseName,
    Guid BranchId);

public sealed record OperationalTaskDto(
    Guid Id,
    Guid? BranchId,
    Guid? SalesShiftId,
    Guid AssigneeEmployeeId,
    string AssigneeName,
    string Title,
    DateTime? DueAt,
    string Status,
    string? EvidenceNote,
    Guid CreatedByUserId,
    DateTime? CompletedAt,
    DateTime CreatedAt);

public sealed record CreateOperationalTaskRequest(
    Guid AssigneeEmployeeId,
    string Title,
    Guid? BranchId = null,
    Guid? SalesShiftId = null,
    DateTime? DueAt = null);

public sealed record UpdateOperationalTaskStatusRequest(
    string Status,
    string? EvidenceNote = null);
