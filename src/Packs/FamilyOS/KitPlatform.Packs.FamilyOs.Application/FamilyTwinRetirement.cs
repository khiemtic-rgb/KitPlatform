namespace KitPlatform.Packs.FamilyOs;

/// <summary>Behavior OS Wave 5 — AI Retirement runtime stages (Autonomy Gradient).</summary>
public static class FamilyRetirementStages
{
    public const string FullSupport = "full_support";
    public const string Assisted = "assisted";
    public const string Soft = "soft";
    public const string Observe = "observe";
    public const string Retired = "retired";

    public static string LabelVi(string? code) =>
        (code ?? "").Trim().ToLowerInvariant() switch
        {
            FullSupport => "AI hỗ trợ đầy đủ",
            Assisted => "AI hỗ trợ có chọn lọc",
            Soft => "AI nhắc nhẹ / tự cue",
            Observe => "Observe-only — AI quan sát",
            Retired => "AI nghỉ hưu (mục tiêu)",
            _ => "Chưa xác định",
        };

    public static string AdviceVi(string? code) =>
        (code ?? "").Trim().ToLowerInvariant() switch
        {
            Retired => "Gia đình đang gần mục tiêu: ít nhắc, nhiều tự chủ.",
            Observe => "Ưu tiên quan sát — tắt parent push trừ khi bố mẹ chủ động bật lại.",
            Soft => "Giữ self-cue; hạn chế parent nudge.",
            Assisted => "Nhắc khi quá giờ; tránh nhắc sớm.",
            _ => "Giai đoạn xây nền — vẫn cần khung rõ, nhưng đo Autonomy mỗi tuần.",
        };
}

public static class FamilySiblingBalance
{
    public const string Even = "even";
    public const string MildSkew = "mild_skew";
    public const string ClearSkew = "clear_skew";

    public static string LabelVi(string? code) =>
        (code ?? "").Trim().ToLowerInvariant() switch
        {
            Even => "Đều nhau",
            MildSkew => "Lệch nhẹ",
            ClearSkew => "Lệch rõ",
            _ => "Chưa đủ dữ liệu",
        };
}

/// <summary>
/// Wave 5 lite — Family Twin + Retirement policy (rule/signal).
/// Sibling fairness: so sánh công bằng, không xếp hạng tổn thương.
/// </summary>
public static class FamilyTwinRetirement
{
    public const string DisclaimerVi =
        "Family Twin mô hình hóa tín hiệu nhà — không xếp hạng con, không đánh giá tính cách.";

    public sealed record ChildSignal(
        Guid MemberId,
        string MemberName,
        int OverallScore,
        int AutonomyScore,
        int PeaceScore,
        int SelfStartScore,
        int DoneCount,
        int SkippedCount,
        int ParentNudgeCount);

    public sealed record FamilyTwinResult(
        int FamilyPeaceIndex,
        int FamilyAutonomyIndex,
        int ParentalInterventionIndex,
        string RetirementStage,
        string RetirementLabelVi,
        string RetirementAdviceVi,
        string SiblingBalance,
        string SiblingBalanceLabelVi,
        string SiblingAdviceVi,
        bool DependenceWarning,
        string? DependenceWarningVi,
        bool RecommendObserveOnly,
        string DisclaimerVi);

    public static FamilyTwinResult Score(
        IReadOnlyList<ChildSignal> children,
        int parentNudgesThisWeek,
        int parentNudgesPrevWeek,
        bool observeOnlyForced)
    {
        if (children.Count == 0)
        {
            return new FamilyTwinResult(
                50, 50, parentNudgesThisWeek,
                observeOnlyForced ? FamilyRetirementStages.Observe : FamilyRetirementStages.FullSupport,
                FamilyRetirementStages.LabelVi(
                    observeOnlyForced ? FamilyRetirementStages.Observe : FamilyRetirementStages.FullSupport),
                FamilyRetirementStages.AdviceVi(
                    observeOnlyForced ? FamilyRetirementStages.Observe : FamilyRetirementStages.FullSupport),
                FamilySiblingBalance.Even,
                FamilySiblingBalance.LabelVi(FamilySiblingBalance.Even),
                "Chưa có đủ tín hiệu con để so sánh công bằng.",
                DependenceWarning: false,
                DependenceWarningVi: null,
                RecommendObserveOnly: observeOnlyForced,
                DisclaimerVi);
        }

        var autonomy = (int)Math.Round(children.Average(c => c.AutonomyScore));
        var peace = (int)Math.Round(children.Average(c => c.PeaceScore));
        var overall = (int)Math.Round(children.Average(c => c.OverallScore));
        var selfStart = (int)Math.Round(children.Average(c => c.SelfStartScore));

        // Intervention index lite: nudges / child / week (scaled 0–100 inverse peace feel)
        var intervention = Math.Clamp(parentNudgesThisWeek * 12, 0, 100);

        var stage = ResolveStage(autonomy, peace, intervention, overall, observeOnlyForced);
        var (sibCode, sibAdvice) = SiblingFairness(children);

        var dependence = parentNudgesThisWeek >= parentNudgesPrevWeek + 2
            && autonomy < 55
            && selfStart < 45;
        string? depMsg = dependence
            ? "Intervention đang tăng trong khi Autonomy chưa cải thiện — rủi ro AI nuôi phụ thuộc. Hãy giảm nhắc và tăng self-start."
            : null;

        var recommendObserve = observeOnlyForced
            || stage is FamilyRetirementStages.Observe or FamilyRetirementStages.Retired
            || (dependence && peace >= 50);

        return new FamilyTwinResult(
            FamilyPeaceIndex: peace,
            FamilyAutonomyIndex: autonomy,
            ParentalInterventionIndex: intervention,
            RetirementStage: stage,
            RetirementLabelVi: FamilyRetirementStages.LabelVi(stage),
            RetirementAdviceVi: FamilyRetirementStages.AdviceVi(stage),
            SiblingBalance: sibCode,
            SiblingBalanceLabelVi: FamilySiblingBalance.LabelVi(sibCode),
            SiblingAdviceVi: sibAdvice,
            DependenceWarning: dependence,
            DependenceWarningVi: depMsg,
            RecommendObserveOnly: recommendObserve,
            DisclaimerVi);
    }

    public static string ResolveStage(
        int autonomy,
        int peace,
        int intervention,
        int overall,
        bool observeOnlyForced)
    {
        if (observeOnlyForced)
            return autonomy >= 70 ? FamilyRetirementStages.Retired : FamilyRetirementStages.Observe;

        if (autonomy >= 75 && peace >= 70 && intervention <= 25)
            return FamilyRetirementStages.Retired;
        if (autonomy >= 65 && intervention <= 40)
            return FamilyRetirementStages.Observe;
        if (autonomy >= 50 && overall >= 55)
            return FamilyRetirementStages.Soft;
        if (autonomy >= 35)
            return FamilyRetirementStages.Assisted;
        return FamilyRetirementStages.FullSupport;
    }

    private static (string Code, string Advice) SiblingFairness(IReadOnlyList<ChildSignal> children)
    {
        if (children.Count < 2)
            return (FamilySiblingBalance.Even, "Một con — chưa áp dụng công bằng anh chị em.");

        var rates = children
            .Select(c =>
            {
                var total = Math.Max(1, c.DoneCount + c.SkippedCount);
                return (double)c.DoneCount / total;
            })
            .ToList();

        var max = rates.Max();
        var min = rates.Min();
        var gap = max - min;

        if (gap < 0.15)
            return (
                FamilySiblingBalance.Even,
                "Tín hiệu hoàn thành giữa các con khá đều — giữ khích lệ ngang nhau.");

        if (gap < 0.30)
            return (
                FamilySiblingBalance.MildSkew,
                "Có lệch nhẹ — hỗ trợ thêm cho con đang khó hơn, tránh so sánh công khai.");

        return (
            FamilySiblingBalance.ClearSkew,
            "Lệch rõ giữa các con — ưu tiên công bằng hỗ trợ (thời gian/giúp đỡ), không xếp hạng.");
    }
}
