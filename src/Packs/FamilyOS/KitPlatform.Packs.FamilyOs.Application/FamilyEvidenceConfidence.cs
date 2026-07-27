namespace KitPlatform.Packs.FamilyOs;

/// <summary>Behavior OS Wave 2 — evidence ladder + completion confidence (pure).</summary>
public static class FamilyEvidenceLevels
{
    public const int SelfCheck = 0;
    public const int Reflection = 1;
    public const int Retrieval = 2;
    public const int Photo = 3;

    public static string LabelVi(int level) =>
        level switch
        {
            Photo => "Có ảnh",
            Retrieval => "Đã kiểm tra nhớ",
            Reflection => "Đã phản tư",
            _ => "Tự đánh dấu",
        };
}

public static class FamilyRetrievalAnswers
{
    public const string Skim = "skim";
    public const string Practice = "practice";
    public const string Retrieve = "retrieve";

    public const string CanExplain = "can_explain";
    public const string Vaguely = "vaguely";
    public const string NeedReview = "need_review";

    public static readonly HashSet<string> Methods = new(StringComparer.OrdinalIgnoreCase)
    {
        Skim, Practice, Retrieve,
    };

    public static readonly HashSet<string> Recalls = new(StringComparer.OrdinalIgnoreCase)
    {
        CanExplain, Vaguely, NeedReview,
    };

    public static string MethodLabelVi(string? code) =>
        (code ?? "").Trim().ToLowerInvariant() switch
        {
            Skim => "Đọc / lướt nhanh",
            Practice => "Làm bài / thực hành",
            Retrieve => "Nhớ lại / tự kiểm tra",
            _ => code ?? "",
        };

    public static string RecallLabelVi(string? code) =>
        (code ?? "").Trim().ToLowerInvariant() switch
        {
            CanExplain => "Giải thích được",
            Vaguely => "Nhớ đại khái",
            NeedReview => "Cần xem lại",
            _ => code ?? "",
        };
}

/// <summary>Heuristic: learning-like titles get a light retrieval check.</summary>
public static class FamilyLearningMission
{
    private static readonly string[] Keywords =
    [
        "học", "bài", "toán", "văn", "lý", "hóa", "sinh", "sử", "địa",
        "tiếng", "anh", "đọc", "viết", "homework", "study", "english",
        "math", "lesson", "ôn", "kiểm tra", "bài tập",
    ];

    public static bool IsLearningTitle(string? title)
    {
        var t = (title ?? "").Trim().ToLowerInvariant();
        if (t.Length == 0) return false;
        foreach (var k in Keywords)
        {
            if (t.Contains(k, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}

public static class FamilyEvidenceConfidence
{
    public sealed record Signals(
        bool IsDone,
        bool HasReflection,
        bool HasRetrievalCheck,
        string? MethodAnswer,
        string? RecallAnswer,
        bool HasPhotoEvidence,
        bool IsLearningMission);

    public sealed record ScoreResult(
        int EvidenceLevel,
        int ConfidenceScore,
        string ConfidenceLabelVi,
        bool IllusionRisk);

    public static ScoreResult Score(Signals s)
    {
        if (!s.IsDone)
        {
            return new ScoreResult(0, 0, "Chưa hoàn thành", IllusionRisk: false);
        }

        var level = FamilyEvidenceLevels.SelfCheck;
        if (s.HasReflection) level = Math.Max(level, FamilyEvidenceLevels.Reflection);
        if (s.HasRetrievalCheck) level = Math.Max(level, FamilyEvidenceLevels.Retrieval);
        if (s.HasPhotoEvidence) level = Math.Max(level, FamilyEvidenceLevels.Photo);

        var score = 42; // self tick only — “cần thêm dữ liệu”, không cáo buộc
        if (s.HasReflection) score += 22;

        var illusion = false;
        if (s.HasRetrievalCheck)
        {
            var method = (s.MethodAnswer ?? "").Trim().ToLowerInvariant();
            var recall = (s.RecallAnswer ?? "").Trim().ToLowerInvariant();

            // Illusion-of-learning: skim + claims can explain
            illusion = method == FamilyRetrievalAnswers.Skim
                && recall == FamilyRetrievalAnswers.CanExplain;

            var quizBoost = (method, recall) switch
            {
                (FamilyRetrievalAnswers.Retrieve, FamilyRetrievalAnswers.CanExplain) => 22,
                (FamilyRetrievalAnswers.Practice, FamilyRetrievalAnswers.CanExplain) => 18,
                (FamilyRetrievalAnswers.Retrieve, FamilyRetrievalAnswers.Vaguely) => 14,
                (FamilyRetrievalAnswers.Practice, FamilyRetrievalAnswers.Vaguely) => 12,
                (FamilyRetrievalAnswers.Skim, FamilyRetrievalAnswers.CanExplain) => 6,
                (_, FamilyRetrievalAnswers.NeedReview) => 5,
                _ => 10,
            };
            score += quizBoost;
        }
        else if (s.IsLearningMission)
        {
            // Learning mission without retrieval → slightly lower than generic tick
            score -= 4;
        }

        if (s.HasPhotoEvidence) score += 12;

        score = Math.Clamp(score, 28, 96);
        return new ScoreResult(level, score, LabelVi(score), illusion);
    }

    public static string LabelVi(int score) =>
        score switch
        {
            >= 80 => "Tin cậy cao",
            >= 60 => "Khá chắc",
            >= 45 => "Cần thêm dữ liệu",
            _ => "Ít tín hiệu",
        };
}
