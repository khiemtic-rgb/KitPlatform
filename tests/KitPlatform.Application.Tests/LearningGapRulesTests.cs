using KitPlatform.Infrastructure.Learning;
using Xunit;

namespace KitPlatform.Tests;

public sealed class LearningGapRulesTests
{
    [Theory]
    [InlineData(null, 90)]
    [InlineData(0, 90)]
    [InlineData(-5, 90)]
    [InlineData(30, 30)]
    [InlineData(10000, 365)]
    public void Days_are_clamped(int? input, int expected) =>
        Assert.Equal(expected, LearningGapRules.ClampDays(input));

    [Theory]
    [InlineData(2, 2, false)]
    [InlineData(3, 1, false)]
    [InlineData(5, 2, true)]
    [InlineData(10, 3, false)]
    [InlineData(10, 4, true)]
    public void Weak_question_needs_enough_answers_and_wrong_rate(int answered, int wrong, bool weak) =>
        Assert.Equal(weak, LearningGapRules.IsWeakQuestion(answered, wrong));

    [Fact]
    public void Wrong_pct_handles_zero_and_overflow()
    {
        Assert.Equal(0, LearningGapRules.WrongPct(0, 3));
        Assert.Equal(100, LearningGapRules.WrongPct(4, 9));
        Assert.Equal(33, LearningGapRules.WrongPct(3, 1));
    }

    [Fact]
    public void Outcome_window_needs_seven_full_days_after_pass()
    {
        var passed = new DateOnly(2026, 10, 1);

        Assert.Null(LearningGapRules.OutcomeWindow(passed, new DateOnly(2026, 10, 7)));

        var w = LearningGapRules.OutcomeWindow(passed, new DateOnly(2026, 10, 8));
        Assert.NotNull(w);
        Assert.Equal(7, w!.DaysAfter);
        Assert.Equal(new DateOnly(2026, 10, 7), w.AfterTo);
        Assert.Equal(new DateOnly(2026, 9, 1), w.BeforeFrom);
        Assert.Equal(30, w.DaysBefore);
    }

    [Fact]
    public void Outcome_window_caps_after_period_at_thirty_days()
    {
        var w = LearningGapRules.OutcomeWindow(new DateOnly(2026, 9, 1), new DateOnly(2026, 11, 4));

        Assert.NotNull(w);
        Assert.Equal(30, w!.DaysAfter);
        Assert.Equal(new DateOnly(2026, 9, 30), w.AfterTo);
    }

    [Fact]
    public void Per_day_rounds_to_one_decimal()
    {
        Assert.Equal(1.5m, LearningGapRules.PerDay(45, 30));
        Assert.Equal(0.3m, LearningGapRules.PerDay(2, 7));
        Assert.Equal(0m, LearningGapRules.PerDay(5, 0));
    }
}
