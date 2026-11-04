using KitPlatform.Application.Learning;

namespace KitPlatform.Infrastructure.Learning;

internal sealed record LearningQuizProgressDecision(
    string Status,
    int ScorePct,
    bool FirstPass,
    bool FirstPerfect);

internal sealed record LearningSuggestionInput(
    int ModulesPassed,
    int ModulesTotal,
    bool HasPosBasic,
    bool HasTone,
    bool HasGrn,
    bool HasShiftClose,
    int CloseDaysThisMonth,
    int DaysElapsedThisMonth,
    int CloseStreakDays);

internal sealed record LearningSuggestionResult(
    int Knowledge,
    int Attitude,
    int Care,
    int Stock,
    int Discipline,
    IReadOnlyList<LearningSuggestedScoreBasisDto> Basis);

internal static class LearningScoringRules
{
    private static readonly TimeZoneInfo VnZone = ResolveVnZone();

    /// <summary>
    /// Bài đã đạt thì lần làm sau (kể cả trượt) không hạ trạng thái và giữ điểm cao nhất.
    /// </summary>
    public static LearningQuizProgressDecision DecideQuizProgress(
        string? currentStatus,
        int? currentScorePct,
        bool passedNow,
        int scoreNow)
    {
        var wasPassed = string.Equals(currentStatus, "passed", StringComparison.OrdinalIgnoreCase);
        var hadPerfect = wasPassed && (currentScorePct ?? 0) >= 100;
        if (wasPassed)
        {
            var best = Math.Max(currentScorePct ?? 0, scoreNow);
            return new LearningQuizProgressDecision(
                "passed", best, FirstPass: false, FirstPerfect: passedNow && scoreNow >= 100 && !hadPerfect);
        }

        return new LearningQuizProgressDecision(
            passedNow ? "passed" : "failed",
            scoreNow,
            FirstPass: passedNow,
            FirstPerfect: passedNow && scoreNow >= 100);
    }

    /// <summary>Điểm ôn tháng = số câu đúng / tổng câu của đề (câu bỏ trống tính sai).</summary>
    public static int DrillScorePct(int correctCount, int questionCount) =>
        questionCount <= 0
            ? 0
            : (int)Math.Round(100.0 * Math.Clamp(correctCount, 0, questionCount) / questionCount);

    public static DateOnly VnToday(DateTime utcNow) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), VnZone));

    public static DateOnly VnMonthStart(DateTime utcNow)
    {
        var today = VnToday(utcNow);
        return new DateOnly(today.Year, today.Month, 1);
    }

    /// <summary>Gợi ý điểm theo luật cố định từ dữ liệu học + checklist (không phải AI).</summary>
    public static LearningSuggestionResult SuggestScores(LearningSuggestionInput i)
    {
        var trainPct = i.ModulesTotal > 0
            ? (int)Math.Round(100.0 * i.ModulesPassed / i.ModulesTotal)
            : 0;
        var expectedDays = Math.Max(1, i.DaysElapsedThisMonth);
        var closePct = (int)Math.Round(100.0 * i.CloseDaysThisMonth / expectedDays);

        var knowledge = i.HasPosBasic ? Math.Clamp(70 + trainPct / 5, 60, 95) : 55;
        var attitude = i.HasTone ? 82 : 68;
        var care = i.HasTone || i.HasPosBasic ? 80 : 65;
        var stock = i.HasGrn ? 82 : (trainPct >= 50 ? 72 : 60);
        var discipline = i.HasShiftClose
            ? Math.Clamp(70 + closePct / 5, 60, 95)
            : Math.Clamp(55 + closePct / 4, 50, 85);
        if (i.CloseStreakDays >= 7)
            discipline = Math.Max(discipline, 85);

        var learned = $"đã học {i.ModulesPassed}/{i.ModulesTotal} bài ({trainPct}%)";
        var basis = new List<LearningSuggestedScoreBasisDto>
        {
            new("knowledge", knowledge, i.HasPosBasic
                ? $"Có chứng nhận Bán hàng tại quầy cơ bản; {learned}."
                : $"Chưa có chứng nhận Bán hàng tại quầy cơ bản; {learned}."),
            new("attitude", attitude, i.HasTone
                ? "Có chứng nhận Thái độ phục vụ."
                : "Chưa có chứng nhận Thái độ phục vụ — mức mặc định, cần quản lý quan sát thêm."),
            new("care", care, i.HasTone || i.HasPosBasic
                ? "Có chứng nhận Thái độ phục vụ hoặc Bán hàng tại quầy cơ bản."
                : "Chưa có chứng nhận liên quan chăm sóc khách."),
            new("stock", stock, i.HasGrn
                ? "Có chứng nhận Nhận hàng nhập kho."
                : $"Chưa có chứng nhận Nhận hàng nhập kho; {learned}."),
            new("discipline", discipline,
                $"Đóng ca đủ mục bắt buộc {i.CloseDaysThisMonth}/{expectedDays} ngày tháng này, chuỗi {i.CloseStreakDays} ngày"
                + (i.HasShiftClose ? "; có chứng nhận Đóng ca đúng quy trình." : ".")),
        };

        return new LearningSuggestionResult(knowledge, attitude, care, stock, discipline, basis);
    }

    private static TimeZoneInfo ResolveVnZone()
    {
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "ICT", "ICT");
    }
}
