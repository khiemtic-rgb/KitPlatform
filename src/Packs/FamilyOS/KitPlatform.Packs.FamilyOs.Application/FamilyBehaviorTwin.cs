namespace KitPlatform.Packs.FamilyOs;

/// <summary>Behavior OS Wave 4 — risk bands (never fake precision %).</summary>
public static class FamilyPredictionBands
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";

    public static string LabelVi(string? band) =>
        (band ?? "").Trim().ToLowerInvariant() switch
        {
            High => "Cao",
            Medium => "Vừa",
            Low => "Thấp",
            _ => "Chưa rõ",
        };
}

public static class FamilyTwinDimensions
{
    public const string Autonomy = "autonomy";
    public const string Consistency = "consistency";
    public const string LearningDepth = "learning_depth";
    public const string Reflection = "reflection";
    public const string Persistence = "persistence";
    public const string SelfStart = "self_start";
    public const string Peace = "peace";

    public static string LabelVi(string? code) =>
        (code ?? "").Trim().ToLowerInvariant() switch
        {
            Autonomy => "Tự chủ",
            Consistency => "Ổn định làm việc",
            LearningDepth => "Độ sâu học",
            Reflection => "Phản tư",
            Persistence => "Bền bỉ",
            SelfStart => "Tự bắt đầu",
            Peace => "Ít cần nhắc",
            _ => code ?? "",
        };
}

/// <summary>
/// Wave 4 lite — Child Behavior Twin + evening quit prediction (rule/signal only).
/// Copy contract: mô hình hóa tín hiệu, không đánh giá nhân cách / bệnh lý.
/// </summary>
public static class FamilyBehaviorTwin
{
    public const string DisclaimerVi =
        "Đây là mô hình tín hiệu hành vi 7 ngày gần đây — không phải đánh giá tính cách hay năng lực của con.";

    public sealed record WindowSignals(
        int OpenCommitments,
        int DoneCount,
        int SkippedCount,
        int SelfStartCount,
        int ReflectionCount,
        int RetrievalCheckCount,
        int ParentNudgeCount,
        int OverdueDoneCount,
        int EveningOpenCount,
        int EveningSkipCount,
        int MaxHabitStreak,
        bool AnyAutonomousHabit,
        double AvgConfidenceWhenDone);

    public sealed record DimensionScore(
        string Code,
        string LabelVi,
        int Score,
        string WhyVi);

    public sealed record TwinResult(
        IReadOnlyList<DimensionScore> Dimensions,
        int OverallScore,
        string OverallLabelVi,
        string DisclaimerVi);

    public sealed record EveningPrediction(
        string RiskBand,
        string RiskLabelVi,
        IReadOnlyList<string> ReasonsVi,
        string SuggestedActionVi);

    public static TwinResult Score(WindowSignals s)
    {
        var total = Math.Max(1, s.OpenCommitments + s.DoneCount + s.SkippedCount);
        var doneRate = (double)s.DoneCount / total;
        var skipRate = (double)s.SkippedCount / total;

        var dims = new List<DimensionScore>
        {
            Dim(FamilyTwinDimensions.Autonomy,
                Clamp01(s.AnyAutonomousHabit ? 0.72 : 0.35 + s.MaxHabitStreak / 40.0),
                s.AnyAutonomousHabit
                    ? "Có thói quen đã tốt nghiệp nhắc."
                    : s.MaxHabitStreak >= 7
                        ? $"Chuỗi tối đa {s.MaxHabitStreak} ngày — đang hướng tới tự chủ."
                        : "Chưa thấy thói quen tự chủ ổn định."),

            Dim(FamilyTwinDimensions.Consistency,
                Clamp01(doneRate),
                $"Hoàn thành ~{Percent(doneRate)} việc trong cửa sổ 7 ngày."),

            Dim(FamilyTwinDimensions.LearningDepth,
                Clamp01(
                    0.25
                    + (s.RetrievalCheckCount > 0 ? 0.35 : 0)
                    + Math.Min(0.4, s.AvgConfidenceWhenDone / 100.0 * 0.4)),
                s.RetrievalCheckCount > 0
                    ? "Có kiểm tra nhớ / tín hiệu học sâu."
                    : s.AvgConfidenceWhenDone >= 60
                        ? "Độ tin cậy hoàn thành khá — vẫn nên thêm kiểm tra nhớ khi học."
                        : "Ít tín hiệu độ sâu học (reflection/quiz/ảnh)."),

            Dim(FamilyTwinDimensions.Reflection,
                Clamp01(s.DoneCount == 0 ? 0.4 : (double)s.ReflectionCount / Math.Max(1, s.DoneCount)),
                s.DoneCount == 0
                    ? "Chưa có lần hoàn thành để đo phản tư."
                    : $"Reflection sau done: {s.ReflectionCount}/{s.DoneCount}."),

            Dim(FamilyTwinDimensions.Persistence,
                Clamp01(1.0 - skipRate * 0.7 - (s.EveningSkipCount > 2 ? 0.15 : 0)),
                s.SkippedCount == 0
                    ? "Ít bỏ cuộc trong tuần."
                    : $"Có {s.SkippedCount} lần skip — bền bỉ còn dao động."),

            Dim(FamilyTwinDimensions.SelfStart,
                Clamp01(s.SelfStartCount == 0 ? 0.28 : Math.Min(1.0, 0.4 + s.SelfStartCount * 0.12)),
                s.SelfStartCount > 0
                    ? $"Tự bắt đầu {s.SelfStartCount} lần (tín hiệu autonomy)."
                    : "Chưa ghi nhận self-start — có thể vẫn chờ nhắc."),

            Dim(FamilyTwinDimensions.Peace,
                Clamp01(1.0 - Math.Min(1.0, s.ParentNudgeCount / 8.0)),
                s.ParentNudgeCount <= 1
                    ? "Ít cần bố mẹ nhắc trong tuần."
                    : $"Parent nudge ~{s.ParentNudgeCount} lần — Intervention Index đang cao."),
        };

        var overall = (int)Math.Round(dims.Average(d => d.Score));
        return new TwinResult(
            dims,
            overall,
            OverallLabel(overall),
            DisclaimerVi);
    }

    /// <summary>Evening quit / abandon risk for a mission (band only).</summary>
    public static EveningPrediction PredictEveningQuit(
        TimeOnly? windowStart,
        TimeOnly? windowEnd,
        string? habitStage,
        bool isLearningMission,
        WindowSignals? memberSignals,
        TimeOnly localNow)
    {
        var reasons = new List<string>();
        var score = 0; // internal 0–10 → band; never expose as %

        var end = windowEnd ?? windowStart;
        var isEvening = end is TimeOnly t && t.Hour >= 17;
        if (!isEvening)
        {
            return new EveningPrediction(
                FamilyPredictionBands.Low,
                FamilyPredictionBands.LabelVi(FamilyPredictionBands.Low),
                ["Không phải khung buổi tối."],
                "Theo dõi bình thường.");
        }

        score += 2;
        reasons.Add("Nhiệm vụ rơi vào buổi tối (sau 17:00).");

        if (localNow.Hour >= 20)
        {
            score += 2;
            reasons.Add("Đã muộn trong ngày — năng lượng thường giảm.");
        }

        var stage = (habitStage ?? "").Trim().ToLowerInvariant();
        if (stage is FamilyHabitStages.New or FamilyHabitStages.Guided)
        {
            score += 2;
            reasons.Add("Thói quen còn mới — dễ bỏ khi mệt.");
        }
        else if (stage is FamilyHabitStages.Autonomous or FamilyHabitStages.Maintained)
        {
            score -= 2;
            reasons.Add("Thói quen đã tự chủ — rủi ro bỏ cuộc thấp hơn.");
        }

        if (isLearningMission)
        {
            score += 1;
            reasons.Add("Việc học buổi tối dễ «ảo giác đã học» / bỏ dở.");
        }

        if (memberSignals is not null)
        {
            if (memberSignals.EveningSkipCount >= 2)
            {
                score += 2;
                reasons.Add($"Đã skip buổi tối {memberSignals.EveningSkipCount} lần trong 7 ngày.");
            }

            if (memberSignals.SelfStartCount == 0 && memberSignals.ParentNudgeCount >= 3)
            {
                score += 1;
                reasons.Add("Ít tự bắt đầu + nhiều nhắc — dễ phụ thuộc nhắc buổi tối.");
            }
        }

        score = Math.Clamp(score, 0, 10);
        var band = score >= 6
            ? FamilyPredictionBands.High
            : score >= 3
                ? FamilyPredictionBands.Medium
                : FamilyPredictionBands.Low;

        var action = band switch
        {
            FamilyPredictionBands.High =>
                "Trước giờ bắt đầu 15 phút: gợi ý self-cue ngắn, tránh parent push sớm.",
            FamilyPredictionBands.Medium =>
                "Để con tự bắt đầu; chỉ soft-nudge nếu quá giờ.",
            _ => "Quan sát — không can thiệp sớm.",
        };

        return new EveningPrediction(
            band,
            FamilyPredictionBands.LabelVi(band),
            reasons,
            action);
    }

    private static DimensionScore Dim(string code, double unit, string why) =>
        new(code, FamilyTwinDimensions.LabelVi(code), (int)Math.Round(Clamp01(unit) * 100), why);

    private static double Clamp01(double v) => Math.Clamp(v, 0, 1);

    private static int Percent(double unit) => (int)Math.Round(Clamp01(unit) * 100);

    private static string OverallLabel(int score) =>
        score switch
        {
            >= 75 => "Tín hiệu tích cực",
            >= 55 => "Đang ổn định",
            >= 40 => "Cần theo dõi nhẹ",
            _ => "Cần hỗ trợ cấu trúc",
        };
}
