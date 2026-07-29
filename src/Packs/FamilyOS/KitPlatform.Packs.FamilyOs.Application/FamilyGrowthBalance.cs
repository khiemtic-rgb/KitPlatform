namespace KitPlatform.Packs.FamilyOs;

/// <summary>
/// Growth Balance™ — personalize "value of caring" and avoid 3 drifts:
/// tự ti · thiếu phấn đấu · dễ hư.
/// SoT: docs/novixa/03-solution/famixa-growth-balance-v1.md
/// </summary>
public static class FamilyGrowthBalance
{
    public static class ResourceBands
    {
        public const string Tight = "tight";
        public const string Moderate = "moderate";
        public const string Abundant = "abundant";
        public const string Unknown = "unknown";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            Tight, Moderate, Abundant, Unknown,
        };

        public static string LabelVi(string? code) =>
            (code ?? "").Trim().ToLowerInvariant() switch
            {
                Tight => "Ít thời gian / điều kiện eo hẹp",
                Moderate => "Trung bình — đủ để giữ nhịp nhẹ",
                Abundant => "Dư thời gian / điều kiện hơn",
                _ => "Chưa rõ",
            };
    }

    public static class WorryCodes
    {
        public const string TuTi = "tu_ti";
        public const string ThieuPhanDau = "thieu_phan_dau";
        public const string DeHu = "de_hu";
        public const string BalanceOk = "balance_ok";
        public const string Unknown = "unknown";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            TuTi, ThieuPhanDau, DeHu, BalanceOk, Unknown,
        };

        public static string LabelVi(string? code) =>
            (code ?? "").Trim().ToLowerInvariant() switch
            {
                TuTi => "Lo con tự ti / sợ thử",
                ThieuPhanDau => "Lo con thiếu phấn đấu / ảo giác khá",
                DeHu => "Lo chiều quá / con dễ hư",
                BalanceOk => "Đang giữ cân bằng",
                _ => "Chưa chọn",
            };
    }

    public sealed record Signals(
        string ResourceBand,
        string PrimaryWorry,
        string? CalibrationPhase = null);

    public sealed record Guidance(
        string WorryCode,
        string WorryLabelVi,
        string CareValueVi,
        string NextStepVi,
        string CoachTipVi,
        string AvoidVi);

    public static string Normalize(string? value, HashSet<string> allowed, string fallback)
    {
        var v = (value ?? "").Trim().ToLowerInvariant();
        return allowed.Contains(v) ? v : fallback;
    }

    /// <summary>Infer worry from self-calibration phase when parent has not captured yet.</summary>
    public static string InferWorryFromCalibration(string? phase) =>
        (phase ?? "").Trim().ToLowerInvariant() switch
        {
            FamilySelfCalibration.Phases.PeerShock => WorryCodes.TuTi,
            FamilySelfCalibration.Phases.Rebuild => WorryCodes.TuTi,
            FamilySelfCalibration.Phases.BubbleRisk => WorryCodes.ThieuPhanDau,
            _ => WorryCodes.Unknown,
        };

    public static Guidance BuildGuidance(Signals signals, string? childShortName = null)
    {
        var band = Normalize(signals.ResourceBand, ResourceBands.All, ResourceBands.Unknown);
        var worry = Normalize(signals.PrimaryWorry, WorryCodes.All, WorryCodes.Unknown);
        if (worry is WorryCodes.Unknown)
            worry = InferWorryFromCalibration(signals.CalibrationPhase);

        var who = string.IsNullOrWhiteSpace(childShortName) ? "con" : childShortName.Trim();
        var care = CareValue(band, worry);
        var label = WorryCodes.LabelVi(worry);

        return worry switch
        {
            WorryCodes.TuTi => new Guidance(
                worry,
                label,
                care,
                band is ResourceBands.Tight
                    ? $"Hôm nay chỉ cần 1 việc nhỏ {who} làm được — bạn khen đúng việc đó (30 giây)."
                    : $"Chuỗi 3 ngày: mỗi ngày {who} một việc vừa sức + hỏi “con làm thế nào?” — không hỏi xếp hạng.",
                "Tự tin đến từ việc làm được, không từ lời ‘con giỏi hơn bạn’. Quan tâm lúc này là đứng cạnh, không đè.",
                "Tránh so sánh / mắng năng lực / nhồi đề để ‘bù’."),

            WorryCodes.ThieuPhanDau => new Guidance(
                worry,
                label,
                care,
                $"Hôm nay hỏi {who} giải thích lại 1 ý vừa học — khen khi chịu khó làm rõ, không khen ‘giỏi nhất’.",
                "Thiếu phấn đấu hay bắt đầu từ ảo giác khá. Quan tâm = giữ tiêu chuẩn vừa sức + bằng chứng, không phải mắng lười.",
                "Tránh khen sáo hoặc buông hết chuẩn vì sợ con buồn."),

            WorryCodes.DeHu => new Guidance(
                worry,
                label,
                care,
                $"Chọn 1 thỏa thuận nhỏ với {who} hôm nay (giờ / việc / màn hình) — thương nhưng giữ khung; bạn chỉ 👍 khi đúng.",
                "Thương con không phải chiều mọi ý. Quan tâm có giá trị khi vừa ấm vừa có giới hạn nhà mình.",
                "Tránh chỉ chiều cảm xúc hoặc chỉ siết không giải thích."),

            WorryCodes.BalanceOk => new Guidance(
                worry,
                label,
                care,
                $"Giữ nhịp: hiện diện ngắn + 1 tiêu chuẩn vừa với {who} — không thêm checklist.",
                "Nhà bạn đang gần điểm cân bằng. Quan tâm tiếp tục bằng hiện diện đều, không phải thêm áp lực.",
                "Đừng nới thành nuông hoặc siết thành đè chỉ vì một ngày lệch."),

            _ => new Guidance(
                WorryCodes.Unknown,
                label,
                care,
                "Chọn lo lớn nhất của nhà (tự ti / thiếu phấn đấu / dễ hư) — Famixa gợi ý bước vừa nhà bạn.",
                "Quan tâm có giá trị khi đúng nhà bạn: không công thức chung, không xếp hạng.",
                "Đừng bắt đầu bằng so sánh với nhà khác."),
        };
    }

    public static string CareValue(string resourceBand, string worry)
    {
        var band = Normalize(resourceBand, ResourceBands.All, ResourceBands.Unknown);
        var baseLine = band switch
        {
            ResourceBands.Tight =>
                "Nhà ít thời gian vẫn quan tâm được: 1 phút hiện diện đúng việc đáng hơn 1 giờ mắng.",
            ResourceBands.Abundant =>
                "Nhiều điều kiện không thay được hiện diện có khung — quan tâm là tiêu chuẩn rõ + thương thật.",
            ResourceBands.Moderate =>
                "Quan tâm có giá trị khi vừa ấm vừa có khung nhẹ — đúng nhịp nhà bạn, không theo nhà khác.",
            _ =>
                "Mỗi nhà quan tâm một kiểu. Famixa giúp bạn quan tâm có phương pháp — không phải quản lý con như checklist.",
        };

        return worry switch
        {
            WorryCodes.TuTi => baseLine + " Ưu tiên dựng lại tự tin bằng việc làm được.",
            WorryCodes.ThieuPhanDau => baseLine + " Ưu tiên bằng chứng học thật, không ảo giác khá.",
            WorryCodes.DeHu => baseLine + " Ưu tiên thương kèm thỏa thuận rõ.",
            _ => baseLine,
        };
    }
}
