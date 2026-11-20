using KitPlatform.Application.Learning;

namespace KitPlatform.Application.Success;

/// <summary>Owner "Nhân sự hôm nay" — staff accountability only, no revenue figures.</summary>
public sealed record OwnerStaffTodayDto(
    DateOnly BusinessDate,
    IReadOnlyList<StaffChecklistBranchDto> Checklists,
    IReadOnlyList<StaffTaskAssigneeDto> TasksByAssignee,
    IReadOnlyList<LossCashVarianceByCloserRowDto> CashVarianceByCloser7d,
    int? EmployeesWithoutCredentialCount,
    int ActiveEmployeeCount);

public sealed record StaffChecklistBranchDto(
    Guid BranchId,
    string BranchName,
    StaffChecklistKindDto Open,
    StaffChecklistKindDto Close);

/// <summary>Status: not_started | in_progress | completed.</summary>
public sealed record StaffChecklistKindDto(
    string Status,
    int RunCount,
    int CompletedRunCount,
    int MissingRequiredCount,
    IReadOnlyList<string> TickedByNames);

public sealed record StaffTaskAssigneeDto(
    Guid EmployeeId,
    string AssigneeName,
    int OpenCount,
    int OverdueCount);

/// <summary>One checklist item of a run started today.</summary>
public sealed record StaffChecklistItemSample(
    Guid BranchId,
    Guid RunId,
    string Kind,
    string RunStatus,
    bool IsRequired,
    bool IsChecked,
    string? CheckedByName);

public sealed record StaffOpenTaskSample(
    Guid EmployeeId,
    string AssigneeName,
    DateTime? DueAt,
    string Status);

public interface IOwnerStaffTodayService
{
    Task<OwnerStaffTodayDto> GetAsync(CancellationToken cancellationToken = default);
}

public static class OwnerStaffTodayRules
{
    public const string NotStarted = "not_started";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";

    public static StaffChecklistKindDto SummarizeKind(IEnumerable<StaffChecklistItemSample> items)
    {
        var list = items.ToList();
        var runs = list
            .GroupBy(i => i.RunId)
            .Select(g => g.First().RunStatus)
            .ToList();
        if (runs.Count == 0)
            return new StaffChecklistKindDto(NotStarted, 0, 0, 0, []);

        var completedRuns = runs.Count(s => string.Equals(s, Completed, StringComparison.OrdinalIgnoreCase));
        var status = completedRuns == runs.Count ? Completed : InProgress;
        var missing = list.Count(i => i.IsRequired && !i.IsChecked);
        var tickedBy = list
            .Where(i => i.IsChecked && !string.IsNullOrWhiteSpace(i.CheckedByName))
            .Select(i => i.CheckedByName!.Trim())
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(n => n, StringComparer.CurrentCulture)
            .ToList();
        return new StaffChecklistKindDto(status, runs.Count, completedRuns, missing, tickedBy);
    }

    public static IReadOnlyList<StaffChecklistBranchDto> SummarizeChecklists(
        IEnumerable<(Guid BranchId, string BranchName)> branches,
        IEnumerable<StaffChecklistItemSample> items)
    {
        var byBranch = items.ToLookup(i => i.BranchId);
        return branches
            .Select(b =>
            {
                var rows = byBranch[b.BranchId].ToList();
                return new StaffChecklistBranchDto(
                    b.BranchId,
                    b.BranchName,
                    SummarizeKind(rows.Where(r => r.Kind == "open")),
                    SummarizeKind(rows.Where(r => r.Kind == "close")));
            })
            .ToList();
    }

    /// <summary>Open/in-progress tasks per assignee; overdue first, then most open.</summary>
    public static IReadOnlyList<StaffTaskAssigneeDto> GroupTasksByAssignee(
        IEnumerable<StaffOpenTaskSample> tasks,
        DateTime utcNow)
    {
        return tasks
            .Where(t => t.Status is OperationalTaskRules.Open or OperationalTaskRules.InProgress)
            .GroupBy(t => t.EmployeeId)
            .Select(g => new StaffTaskAssigneeDto(
                g.Key,
                g.First().AssigneeName,
                g.Count(),
                g.Count(t => OperationalTaskRules.IsOverdue(t.DueAt, t.Status, utcNow))))
            .OrderByDescending(r => r.OverdueCount)
            .ThenByDescending(r => r.OpenCount)
            .ThenBy(r => r.AssigneeName, StringComparer.CurrentCulture)
            .ToList();
    }
}
