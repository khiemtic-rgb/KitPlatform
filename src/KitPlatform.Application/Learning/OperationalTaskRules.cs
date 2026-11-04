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

    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    /// <summary>
    /// Overdue when the due day (Asia/Ho_Chi_Minh, fixed UTC+7, no DST) is before today
    /// and the task is still open or in progress. A task due today is not overdue yet.
    /// </summary>
    public static bool IsOverdue(DateTime? dueAtUtc, string? status, DateTime utcNow)
    {
        if (dueAtUtc is not DateTime due)
            return false;
        var current = status?.Trim().ToLowerInvariant();
        if (current is not (Open or InProgress))
            return false;
        var dueUtc = due.Kind == DateTimeKind.Local ? due.ToUniversalTime() : due;
        var dueDay = DateOnly.FromDateTime(dueUtc.Add(VietnamOffset));
        var today = DateOnly.FromDateTime(utcNow.Add(VietnamOffset));
        return dueDay < today;
    }

    public static IReadOnlyList<OperationalTaskDto> ApplyOverdue(
        IEnumerable<OperationalTaskDto> tasks,
        DateTime utcNow,
        bool overdueOnly)
    {
        var marked = tasks.Select(t => t with { IsOverdue = IsOverdue(t.DueAt, t.Status, utcNow) });
        if (overdueOnly)
            marked = marked.Where(t => t.IsOverdue);
        return marked.OrderByDescending(t => t.IsOverdue).ToList();
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
    DateTime CreatedAt,
    bool IsOverdue = false);

public sealed record CreateOperationalTaskRequest(
    Guid AssigneeEmployeeId,
    string Title,
    Guid? BranchId = null,
    Guid? SalesShiftId = null,
    DateTime? DueAt = null);

public sealed record UpdateOperationalTaskStatusRequest(
    string Status,
    string? EvidenceNote = null);
