using KitPlatform.Packs.FamilyOs;
using Xunit;

namespace KitPlatform.Platform.Tests;

public sealed class FamilySchoolQuietInterventionTests
{
    private static FamilyMotivationIntervention.Input HotOverdue(bool schoolQuiet) =>
        new(
            Status: FamilyCommitmentStatuses.Pending,
            ReminderState: FamilyReminderStates.Overdue,
            HabitStage: FamilyHabitStages.New,
            ReminderSuppressed: false,
            HabitStreakDays: 0,
            IsLearningMission: false,
            SkipReason: null,
            ParentNudgesUsedToday: 0,
            ParentNudgeBudget: 3,
            Title: "Đánh răng sáng",
            WindowEnd: new TimeOnly(7, 30),
            SchoolQuiet: schoolQuiet);

    [Fact]
    public void School_quiet_blocks_parent_push_and_child_chime()
    {
        var d = FamilyMotivationIntervention.Decide(HotOverdue(schoolQuiet: true));
        Assert.False(d.AllowParentPush);
        Assert.False(d.AllowChildChime);
        Assert.Equal(FamilyInterventionLevels.ObserveOnly, d.InterventionLevel);
        Assert.Contains("giờ học", d.ParentAdviceVi, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Outside_quiet_overdue_can_allow_parent_push()
    {
        var d = FamilyMotivationIntervention.Decide(HotOverdue(schoolQuiet: false));
        Assert.True(d.AllowParentPush);
    }

    [Fact]
    public void Quiet_takes_priority_before_other_gates()
    {
        var d = FamilyMotivationIntervention.Decide(
            HotOverdue(schoolQuiet: true) with { FamilyObserveOnly = true });
        Assert.False(d.AllowParentPush);
        Assert.False(d.AllowChildChime);
        Assert.Contains("giờ học", d.ParentAdviceVi, StringComparison.OrdinalIgnoreCase);
    }
}
