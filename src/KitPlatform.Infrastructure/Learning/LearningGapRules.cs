namespace KitPlatform.Infrastructure.Learning;

internal sealed record LearningOutcomeWindow(
    DateOnly BeforeFrom,
    DateOnly PassedOn,
    DateOnly AfterTo,
    int DaysBefore,
    int DaysAfter);

/// <summary>Luật cố định cho báo cáo khoảng trống đào tạo (không phải AI).</summary>
internal static class LearningGapRules
{
    public const int DefaultDays = 90;
    public const int MaxDays = 365;
    public const int MinAnswersForWeakQuestion = 3;
    public const int WeakQuestionWrongPct = 40;
    public const int OutcomeWindowDays = 30;
    public const int MinDaysAfterForOutcome = 7;

    public static int ClampDays(int? days) =>
        days is null or <= 0 ? DefaultDays : Math.Min(days.Value, MaxDays);

    public static int WrongPct(int answered, int wrong) =>
        answered <= 0 ? 0 : (int)Math.Round(100.0 * Math.Clamp(wrong, 0, answered) / answered);

    public static bool IsWeakQuestion(int answered, int wrong) =>
        answered >= MinAnswersForWeakQuestion && WrongPct(answered, wrong) >= WeakQuestionWrongPct;

    /// <summary>
    /// Trước = 30 ngày trước ngày đạt; sau = từ ngày đạt tới tối đa 30 ngày, không quá hôm qua.
    /// Null khi chưa đủ 7 ngày sau để so sánh.
    /// </summary>
    public static LearningOutcomeWindow? OutcomeWindow(DateOnly passedOn, DateOnly today)
    {
        var lastFullDay = today.AddDays(-1);
        var afterTo = passedOn.AddDays(OutcomeWindowDays - 1);
        if (afterTo > lastFullDay)
            afterTo = lastFullDay;
        var daysAfter = afterTo.DayNumber - passedOn.DayNumber + 1;
        if (daysAfter < MinDaysAfterForOutcome)
            return null;
        var beforeFrom = passedOn.AddDays(-OutcomeWindowDays);
        return new LearningOutcomeWindow(beforeFrom, passedOn, afterTo, OutcomeWindowDays, daysAfter);
    }

    public static decimal PerDay(int count, int days) =>
        days <= 0 ? 0m : Math.Round((decimal)count / days, 1, MidpointRounding.AwayFromZero);
}
