using System.Reflection;
using KitPlatform.Api.Authorization;
using KitPlatform.Api.Controllers.Success;
using KitPlatform.Application.Identity;
using KitPlatform.Application.Learning;
using KitPlatform.Application.Success;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace KitPlatform.Tests;

public class OperationalTaskOverdueTests
{
    // 2026-11-04 10:00 in Vietnam.
    private static readonly DateTime Now = new(2026, 11, 4, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Due_yesterday_open_is_overdue()
    {
        var due = new DateTime(2026, 11, 3, 9, 0, 0, DateTimeKind.Utc);
        Assert.True(OperationalTaskRules.IsOverdue(due, "open", Now));
        Assert.True(OperationalTaskRules.IsOverdue(due, "in_progress", Now));
    }

    [Fact]
    public void Due_earlier_today_is_not_overdue_yet()
    {
        var due = new DateTime(2026, 11, 4, 1, 0, 0, DateTimeKind.Utc);
        Assert.False(OperationalTaskRules.IsOverdue(due, "open", Now));
    }

    [Fact]
    public void Vietnam_day_boundary_is_used_not_utc()
    {
        // 2026-11-03 17:30 UTC = 2026-11-04 00:30 VN, so it is due "today" in VN.
        var dueVnToday = new DateTime(2026, 11, 3, 17, 30, 0, DateTimeKind.Utc);
        Assert.False(OperationalTaskRules.IsOverdue(dueVnToday, "open", Now));

        // 2026-11-03 16:59 UTC = 2026-11-03 23:59 VN, so it was due yesterday in VN.
        var dueVnYesterday = new DateTime(2026, 11, 3, 16, 59, 0, DateTimeKind.Utc);
        Assert.True(OperationalTaskRules.IsOverdue(dueVnYesterday, "open", Now));
    }

    [Theory]
    [InlineData("done")]
    [InlineData("cancelled")]
    [InlineData("DONE")]
    public void Done_or_cancelled_is_never_overdue(string status)
    {
        var due = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.False(OperationalTaskRules.IsOverdue(due, status, Now));
    }

    [Fact]
    public void No_due_date_is_never_overdue()
    {
        Assert.False(OperationalTaskRules.IsOverdue(null, "open", Now));
    }

    [Fact]
    public void ApplyOverdue_marks_sorts_overdue_first_and_filters()
    {
        var onTime = MakeTask("A", new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc), "open");
        var late = MakeTask("B", new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), "open");
        var doneLate = MakeTask("C", new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), "done");

        var all = OperationalTaskRules.ApplyOverdue([onTime, late, doneLate], Now, overdueOnly: false);
        Assert.Equal(["B", "A", "C"], all.Select(t => t.Title));
        Assert.True(all[0].IsOverdue);
        Assert.False(all[2].IsOverdue);

        var onlyOverdue = OperationalTaskRules.ApplyOverdue([onTime, late, doneLate], Now, overdueOnly: true);
        Assert.Equal(["B"], onlyOverdue.Select(t => t.Title));
    }

    private static OperationalTaskDto MakeTask(string title, DateTime? dueAt, string status) =>
        new(Guid.NewGuid(), null, null, Guid.NewGuid(), "NV", title, dueAt, status, null, Guid.NewGuid(), null, Now);
}

public class CashVarianceAccountabilityTests
{
    [Fact]
    public void Groups_by_closer_with_absolute_sum_and_count()
    {
        var lan = Guid.NewGuid();
        var minh = Guid.NewGuid();
        var rows = CashVarianceAccountability.GroupByCloser(
        [
            new CashVarianceShiftSample(lan, "Lan", -20_000m),
            new CashVarianceShiftSample(lan, "Lan", 5_000m),
            new CashVarianceShiftSample(minh, "Minh", 10_000m),
            new CashVarianceShiftSample(minh, "Minh", null),
        ]);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Lan", rows[0].CloserName);
        Assert.Equal(2, rows[0].ShiftCount);
        Assert.Equal(25_000m, rows[0].AbsVarianceTotal);
        Assert.Equal(-15_000m, rows[0].NetVarianceTotal);
        Assert.Equal("Minh", rows[1].CloserName);
        Assert.Equal(1, rows[1].ShiftCount);
        Assert.Equal(10_000m, rows[1].AbsVarianceTotal);
    }

    [Fact]
    public void Unknown_closer_gets_plain_label()
    {
        var rows = CashVarianceAccountability.GroupByCloser([new CashVarianceShiftSample(null, "", 1_000m)]);
        Assert.Equal(CashVarianceAccountability.UnknownCloserName, rows.Single().CloserName);
    }
}

public class OwnerStaffTodayRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Branch_without_runs_is_not_started()
    {
        var branch = Guid.NewGuid();
        var result = OwnerStaffTodayRules.SummarizeChecklists([(branch, "CN1")], []);
        var row = Assert.Single(result);
        Assert.Equal(OwnerStaffTodayRules.NotStarted, row.Open.Status);
        Assert.Equal(OwnerStaffTodayRules.NotStarted, row.Close.Status);
        Assert.Equal(0, row.Open.MissingRequiredCount);
    }

    [Fact]
    public void Summarizes_status_missing_required_and_tickers()
    {
        var branch = Guid.NewGuid();
        var openRun = Guid.NewGuid();
        var closeRun = Guid.NewGuid();
        var items = new[]
        {
            new StaffChecklistItemSample(branch, openRun, "open", "completed", true, true, "Lan"),
            new StaffChecklistItemSample(branch, openRun, "open", "completed", false, true, "Minh"),
            new StaffChecklistItemSample(branch, closeRun, "close", "in_progress", true, false, null),
            new StaffChecklistItemSample(branch, closeRun, "close", "in_progress", true, false, null),
            new StaffChecklistItemSample(branch, closeRun, "close", "in_progress", false, false, null),
            new StaffChecklistItemSample(branch, closeRun, "close", "in_progress", true, true, "Lan"),
        };

        var row = Assert.Single(OwnerStaffTodayRules.SummarizeChecklists([(branch, "CN1")], items));
        Assert.Equal(OwnerStaffTodayRules.Completed, row.Open.Status);
        Assert.Equal(["Lan", "Minh"], row.Open.TickedByNames);
        Assert.Equal(0, row.Open.MissingRequiredCount);
        Assert.Equal(OwnerStaffTodayRules.InProgress, row.Close.Status);
        Assert.Equal(2, row.Close.MissingRequiredCount);
        Assert.Equal(["Lan"], row.Close.TickedByNames);
    }

    [Fact]
    public void One_unfinished_run_of_several_keeps_in_progress()
    {
        var branch = Guid.NewGuid();
        var items = new[]
        {
            new StaffChecklistItemSample(branch, Guid.NewGuid(), "open", "completed", true, true, "Lan"),
            new StaffChecklistItemSample(branch, Guid.NewGuid(), "open", "in_progress", true, false, null),
        };
        var row = Assert.Single(OwnerStaffTodayRules.SummarizeChecklists([(branch, "CN1")], items));
        Assert.Equal(OwnerStaffTodayRules.InProgress, row.Open.Status);
        Assert.Equal(2, row.Open.RunCount);
        Assert.Equal(1, row.Open.CompletedRunCount);
    }

    [Fact]
    public void Groups_tasks_by_assignee_overdue_first()
    {
        var lan = Guid.NewGuid();
        var minh = Guid.NewGuid();
        var yesterday = new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc);
        var tomorrow = new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc);
        var rows = OwnerStaffTodayRules.GroupTasksByAssignee(
        [
            new StaffOpenTaskSample(minh, "Minh", tomorrow, "open"),
            new StaffOpenTaskSample(minh, "Minh", null, "open"),
            new StaffOpenTaskSample(lan, "Lan", yesterday, "in_progress"),
            new StaffOpenTaskSample(lan, "Lan", yesterday, "done"),
        ], Now);

        Assert.Equal(["Lan", "Minh"], rows.Select(r => r.AssigneeName));
        Assert.Equal(1, rows[0].OpenCount);
        Assert.Equal(1, rows[0].OverdueCount);
        Assert.Equal(2, rows[1].OpenCount);
        Assert.Equal(0, rows[1].OverdueCount);
    }

    [Fact]
    public void Staff_today_endpoint_uses_owner_cockpit_policy()
    {
        var method = typeof(OwnerStaffTodayController).GetMethod(nameof(OwnerStaffTodayController.Get))!;
        var policies = method.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).ToList();
        Assert.Contains(SuccessPolicies.Owner, policies);
    }

    [Fact]
    public void Staff_today_dto_has_no_revenue_fields()
    {
        var names = typeof(OwnerStaffTodayDto).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain(names, n => n.Contains("Revenue", StringComparison.OrdinalIgnoreCase)
                                          || n.Contains("Sales", StringComparison.OrdinalIgnoreCase));
    }
}

public class EmployeeProfileRulesTests
{
    [Fact]
    public void Omitted_job_title_leaves_profile_untouched()
    {
        Assert.False(EmployeeProfileRules.ShouldApply(null));
        Assert.True(EmployeeProfileRules.ShouldApply(""));
    }

    [Fact]
    public void Blank_job_title_clears_and_text_is_trimmed()
    {
        Assert.Null(EmployeeProfileRules.NormalizeJobTitle("   "));
        Assert.Equal("Dược sĩ", EmployeeProfileRules.NormalizeJobTitle("  Dược sĩ "));
    }

    [Fact]
    public void Too_long_job_title_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(
            () => EmployeeProfileRules.NormalizeJobTitle(new string('a', EmployeeProfileRules.MaxJobTitleLength + 1)));
    }
}
