using KitPlatform.Infrastructure.Learning;
using Xunit;

namespace KitPlatform.Tests;

public sealed class LearningScoringRulesTests
{
    [Fact]
    public void Failed_retake_after_pass_keeps_passed_status_and_best_score()
    {
        var d = LearningScoringRules.DecideQuizProgress("passed", 90, passedNow: false, scoreNow: 40);

        Assert.Equal("passed", d.Status);
        Assert.Equal(90, d.ScorePct);
        Assert.False(d.FirstPass);
        Assert.False(d.FirstPerfect);
    }

    [Fact]
    public void Passed_retake_with_lower_score_keeps_best_and_is_not_first_pass()
    {
        var d = LearningScoringRules.DecideQuizProgress("passed", 100, passedNow: true, scoreNow: 83);

        Assert.Equal("passed", d.Status);
        Assert.Equal(100, d.ScorePct);
        Assert.False(d.FirstPass);
        Assert.False(d.FirstPerfect);
    }

    [Fact]
    public void Passed_retake_reaching_100_for_first_time_flags_first_perfect()
    {
        var d = LearningScoringRules.DecideQuizProgress("passed", 83, passedNow: true, scoreNow: 100);

        Assert.Equal(100, d.ScorePct);
        Assert.False(d.FirstPass);
        Assert.True(d.FirstPerfect);
    }

    [Theory]
    [InlineData("in_progress", true, 80, "passed", true)]
    [InlineData("failed", true, 100, "passed", true)]
    [InlineData("failed", false, 50, "failed", false)]
    [InlineData(null, false, 0, "failed", false)]
    public void Not_yet_passed_module_takes_the_attempt_result(
        string? current, bool passedNow, int score, string expectedStatus, bool firstPass)
    {
        var d = LearningScoringRules.DecideQuizProgress(current, 30, passedNow, score);

        Assert.Equal(expectedStatus, d.Status);
        Assert.Equal(score, d.ScorePct);
        Assert.Equal(firstPass, d.FirstPass);
    }

    [Theory]
    [InlineData(1, 5, 20)]
    [InlineData(5, 5, 100)]
    [InlineData(3, 5, 60)]
    [InlineData(0, 0, 0)]
    public void Drill_score_uses_total_questions_in_drill(int correct, int total, int expected) =>
        Assert.Equal(expected, LearningScoringRules.DrillScorePct(correct, total));

    [Fact]
    public void Drill_sending_one_correct_answer_out_of_five_is_not_100()
    {
        var answered = 1;
        var questionCount = Math.Max(answered, Math.Min(5, 12));

        Assert.Equal(20, LearningScoringRules.DrillScorePct(1, questionCount));
    }

    [Theory]
    [InlineData("2026-10-31T16:59:59Z", 2026, 10, 31)]
    [InlineData("2026-10-31T17:00:00Z", 2026, 11, 1)]
    [InlineData("2026-11-30T20:00:00Z", 2026, 12, 1)]
    public void Vn_today_uses_utc_plus_7(string utc, int y, int m, int d)
    {
        var now = DateTime.Parse(utc, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

        Assert.Equal(new DateOnly(y, m, d), LearningScoringRules.VnToday(now));
        Assert.Equal(new DateOnly(y, m, 1), LearningScoringRules.VnMonthStart(now));
    }

    [Fact]
    public void Close_streak_counts_back_from_yesterday_when_today_not_closed()
    {
        var today = new DateOnly(2026, 11, 4);
        var dates = new HashSet<DateOnly> { today.AddDays(-1), today.AddDays(-2), today.AddDays(-4) };

        Assert.Equal(2, LearningRepository.CloseStreak(dates, today));
        dates.Add(today);
        Assert.Equal(3, LearningRepository.CloseStreak(dates, today));
    }

    [Fact]
    public void Suggested_scores_return_a_basis_for_every_criterion()
    {
        var r = LearningScoringRules.SuggestScores(new LearningSuggestionInput(
            ModulesPassed: 3,
            ModulesTotal: 6,
            HasPosBasic: true,
            HasTone: false,
            HasGrn: false,
            HasShiftClose: false,
            CloseDaysThisMonth: 2,
            DaysElapsedThisMonth: 4,
            CloseStreakDays: 1));

        Assert.Equal(
            ["knowledge", "attitude", "care", "stock", "discipline"],
            r.Basis.Select(b => b.Criterion).ToArray());
        Assert.Equal(80, r.Knowledge);
        Assert.Equal(68, r.Attitude);
        Assert.Equal(r.Knowledge, r.Basis[0].Score);
        Assert.Equal(r.Discipline, r.Basis[4].Score);
        Assert.All(r.Basis, b => Assert.False(string.IsNullOrWhiteSpace(b.Reason)));
        Assert.All(r.Basis, b => Assert.DoesNotContain("pos_basic", b.Reason));
        Assert.Contains("2/4", r.Basis[4].Reason);
    }

    [Fact]
    public void Observation_below_80_percent_is_scored_as_not_passed_instead_of_rejected()
    {
        var keys = LearningObservationRules.KeysForLevel("L1");
        var criteria = keys.Select((k, i) => (k, i < 5)).ToDictionary(x => x.k, x => x.Item2);

        var score = LearningObservationRules.Evaluate("L1", criteria);

        Assert.False(score.Passed);
        Assert.Equal(5, score.Met);
        Assert.Equal(keys.Count, score.Total);
        Assert.Equal(56, score.Pct);
    }

    [Fact]
    public void Observation_at_80_percent_passes()
    {
        var keys = LearningObservationRules.KeysForLevel("L0");
        var criteria = keys.Select((k, i) => (k, i < 5)).ToDictionary(x => x.k, x => x.Item2);

        Assert.True(LearningObservationRules.Evaluate("L0", criteria).Passed);
    }
}
