namespace KitPlatform.Packs.FamilyOs;

/// <summary>
/// Self-calibration playbook (school bubble → overconfidence → peer shock → rebuild).
/// SoT labels/rules: docs/novixa/03-solution/famixa-self-calibration-playbook-v1.md
/// </summary>
public static class FamilySelfCalibration
{
    public static class SchoolCodes
    {
        public const string BubbleEasy = "bubble_easy";
        public const string Mixed = "mixed";
        public const string Competitive = "competitive";
        public const string Unknown = "unknown";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            BubbleEasy, Mixed, Competitive, Unknown,
        };

        public static string LabelVi(string? code) =>
            (code ?? "").Trim().ToLowerInvariant() switch
            {
                BubbleEasy => "Ít đối chiếu ngoài (bubble)",
                Mixed => "Có đối chiếu vừa phải",
                Competitive => "Môi trường cạnh tranh / nhiều đối chiếu",
                _ => "Chưa rõ",
            };
    }

    public static class SelfViewCodes
    {
        public const string Overestimates = "overestimates";
        public const string Calibrated = "calibrated";
        public const string Underestimates = "underestimates";
        public const string Unknown = "unknown";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            Overestimates, Calibrated, Underestimates, Unknown,
        };

        public static string LabelVi(string? code) =>
            (code ?? "").Trim().ToLowerInvariant() switch
            {
                Overestimates => "Con thường tự đánh giá cao hơn thực lực",
                Calibrated => "Con đánh giá khá sát",
                Underestimates => "Con thường tự đánh giá thấp",
                _ => "Chưa rõ",
            };
    }

    public static class PeerShockCodes
    {
        public const string None = "none";
        public const string Mild = "mild";
        public const string Sharp = "sharp";
        public const string Unknown = "unknown";

        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            None, Mild, Sharp, Unknown,
        };

        public static string LabelVi(string? code) =>
            (code ?? "").Trim().ToLowerInvariant() switch
            {
                None => "Chưa thấy cú sốc",
                Mild => "Có chạnh lòng nhẹ khi gặp bạn ngoài",
                Sharp => "Sụp tự tin rõ sau khi đối chiếu ngoài",
                _ => "Chưa rõ",
            };
    }

    public static class Phases
    {
        public const string NeedsCapture = "needs_capture";
        public const string BubbleRisk = "bubble_risk";
        public const string PeerShock = "peer_shock";
        public const string Rebuild = "rebuild";
        public const string Steady = "steady";

        public static string LabelVi(string? code) =>
            (code ?? "").Trim().ToLowerInvariant() switch
            {
                NeedsCapture => "Cần hiểu thêm nhà bạn",
                BubbleRisk => "Rủi ro ảo giác khá (bubble)",
                PeerShock => "Đang qua cú sốc đối chiếu",
                Rebuild => "Dựng lại tự tin bằng bằng chứng",
                Steady => "Tự đánh giá ổn định",
                _ => "Chưa xác định",
            };
    }

    public sealed record Signals(
        string SchoolCode,
        string SelfView,
        string PeerShock,
        int IllusionHits7d);

    public sealed record Guidance(
        string PhaseCode,
        string PhaseLabelVi,
        string NextStepVi,
        string CoachTipVi,
        string AvoidVi);

    public static string Normalize(string? value, HashSet<string> allowed, string fallback)
    {
        var v = (value ?? "").Trim().ToLowerInvariant();
        return allowed.Contains(v) ? v : fallback;
    }

    public static string ResolvePhase(Signals s)
    {
        var school = Normalize(s.SchoolCode, SchoolCodes.All, SchoolCodes.Unknown);
        var view = Normalize(s.SelfView, SelfViewCodes.All, SelfViewCodes.Unknown);
        var shock = Normalize(s.PeerShock, PeerShockCodes.All, PeerShockCodes.Unknown);

        if (school is SchoolCodes.Unknown || view is SelfViewCodes.Unknown)
            return Phases.NeedsCapture;

        if (shock is PeerShockCodes.Mild or PeerShockCodes.Sharp)
            return Phases.PeerShock;

        if (view is SelfViewCodes.Underestimates)
            return Phases.Rebuild;

        if (school is SchoolCodes.BubbleEasy
            && view is SelfViewCodes.Overestimates)
            return Phases.BubbleRisk;

        if (s.IllusionHits7d >= 2 && view is SelfViewCodes.Overestimates)
            return Phases.BubbleRisk;

        if (view is SelfViewCodes.Overestimates && shock is PeerShockCodes.None)
            return Phases.BubbleRisk;

        return Phases.Steady;
    }

    public static Guidance BuildGuidance(Signals signals, string? childShortName = null)
    {
        var who = string.IsNullOrWhiteSpace(childShortName) ? "con" : childShortName.Trim();
        var phase = ResolvePhase(signals);

        return phase switch
        {
            Phases.NeedsCapture => new Guidance(
                phase,
                Phases.LabelVi(phase),
                "Trả lời 2 câu ngắn để Famixa hiểu môi trường học và cách con tự đánh giá.",
                "Chưa cần sửa gì lớn — chỉ cần thêm 2 tín hiệu để đề xuất đúng nhà bạn.",
                "Đừng bắt đầu bằng ‘con yếu’ hay so sánh trường."),

            Phases.BubbleRisk => new Guidance(
                phase,
                Phases.LabelVi(phase),
                $"Hôm nay hỏi {who} giải thích lại 1 ý vừa học — không hỏi ‘con giỏi chứ?’.",
                "Trong môi trường ít đối chiếu, khen ‘giỏi nhất’ dễ nuôi ảo giác. Hãy khen bằng chứng cụ thể.",
                "Tránh so sánh với bạn trường khác khi chưa chuẩn bị cảm xúc."),

            Phases.PeerShock => new Guidance(
                phase,
                Phases.LabelVi(phase),
                $"48 giờ tới: giữ {who} an toàn cảm xúc — 1 việc nhỏ làm được + khen nỗ lực, chưa ép ‘phải bắt kịp’.",
                "Cú sốc đối chiếu là tín hiệu môi trường, không phải bản án năng lực. Tách giá trị khỏi xếp hạng.",
                "Không nói ‘trường mình kém’ / ‘bạn kia giỏi hơn con’."),

            Phases.Rebuild => new Guidance(
                phase,
                Phases.LabelVi(phase),
                $"Chuỗi 3 ngày: mỗi ngày {who} hoàn thành 1 việc học có kiểm tra nhớ (retrieval) — ghi nhận tiến bộ nhỏ.",
                "Tự tin bền đến từ chuỗi thắng nhỏ có bằng chứng, không từ lời động viên chung.",
                "Tránh nhồi đề khó để ‘bù’ — dễ sập lại."),

            _ => new Guidance(
                Phases.Steady,
                Phases.LabelVi(Phases.Steady),
                $"Giữ nhịp: hỏi {who} học được gì hôm nay — thay vì hỏi điểm / xếp hạng.",
                "Nhà bạn đang hiệu chỉnh ổn. Duy trì bằng chứng nhẹ, không nới lỏng thành checklist.",
                "Đừng biến mọi buổi thành thi đua."),
        };
    }
}
