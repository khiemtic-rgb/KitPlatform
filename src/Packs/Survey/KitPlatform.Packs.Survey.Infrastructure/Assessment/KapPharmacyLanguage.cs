namespace KitPlatform.Packs.Survey.Infrastructure;

/// <summary>Ngôn ngữ dễ hiểu cho chủ nhà thuốc — tránh thuật ngữ IT/phần mềm.</summary>
internal static class KapPharmacyLanguage
{
    private static readonly Dictionary<string, string> SimpleNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CUSTOMER"] = "Chăm sóc khách hàng",
        ["OPERATIONS"] = "Vận hành hàng ngày",
        ["INVENTORY"] = "Quản lý hàng & kho",
        ["BUSINESS"] = "Kinh doanh & lợi nhuận",
        ["TECH"] = "Ghi chép & theo dõi số liệu",
        ["GROWTH"] = "Mở rộng & phát triển",
    };

    private static readonly Dictionary<string, (string Situation, string Action)> CategoryAdvice =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["CUSTOMER"] = (
                "Khách quay lại chưa được ghi nhận và chăm sóc đều — dễ mất khách quen sang cửa hàng khác.",
                "Ghi nhận số điện thoại khi bán, nhắc tái mua thuốc và chăm sóc khách hay mua định kỳ."),
            ["OPERATIONS"] = (
                "Mỗi nhân viên bán và giao ca theo cách riêng — chủ khó kiểm soát tiền, thuốc và sai sót.",
                "Viết quy trình bán hàng, giao ca và đối soát cuối ngày — ai làm cũng giống nhau."),
            ["INVENTORY"] = (
                "Hàng cận hạn, tồn kho và nhập hàng chủ yếu dựa vào kinh nghiệm — dễ tồn đọng hoặc thiếu hàng bán chạy.",
                "Theo dõi hạn dùng, kiểm kê định kỳ và nhập hàng theo mức bán thực tế 2–4 tuần gần nhất."),
            ["BUSINESS"] = (
                "Chưa biết rõ nhóm thuốc nào thực sự có lãi — có thể bán nhiều mà lợi nhuận không tăng.",
                "Theo dõi doanh thu và lãi gộp theo nhóm sản phẩm; điều chỉnh tồn và khuyến mại theo số liệu."),
            ["TECH"] = (
                "Sổ sách, Excel hoặc nhiều công cụ rời nhau — xem tình hình kinh doanh chậm và dễ sai.",
                "Gom bán hàng, kho và khách vào một nơi; xem doanh thu và tồn từ điện thoại mỗi ngày."),
            ["GROWTH"] = (
                "Muốn mở rộng nhưng quy trình chưa đủ chuẩn để nhân bản sang cơ sở mới.",
                "Chuẩn hóa vận hành tại cửa hiện hiện tại trước khi mở thêm điểm bán."),
        };

    public static string SimpleName(string code, string? fallbackName = null) =>
        SimpleNames.TryGetValue(code, out var n)
            ? n
            : KapVietnameseText.Display(fallbackName);

    public static string BandLabelTen(decimal scoreOutOf4) => ToTen(scoreOutOf4) switch
    {
        >= 7.5m => "mức khá — duy trì và tinh chỉnh dần",
        >= 5.5m => "mức trung bình — nên cải thiện trong 1–2 tháng",
        >= 3.5m => "còn khoảng trống — nên ưu tiên xử lý sớm",
        _ => "cần củng cố nền tảng — ảnh hưởng tới lợi nhuận và an toàn thuốc",
    };

    public static string AssessmentLine(string code, string? name, decimal scoreOutOf4)
    {
        var letter = KapPharmacyScoreDisplay.Letter(code);
        var simple = SimpleName(code, name);
        var ten = KapPharmacyScoreDisplay.Format(scoreOutOf4);
        var band = BandLabelTen(scoreOutOf4);
        return $"{letter}. {simple} đạt {ten} — {band}.";
    }

    public static string Recommendation(string code)
    {
        if (!CategoryAdvice.TryGetValue(code, out var advice))
            return "Rà soát quy trình liên quan, giao một người phụ trách và theo dõi kết quả hàng tuần.";
        return advice.Action;
    }

    public static string Situation(string code) =>
        CategoryAdvice.TryGetValue(code, out var advice) ? advice.Situation : "";

    private static decimal ToTen(decimal scoreOutOf4) => KapPharmacyScoreDisplay.ToTen(scoreOutOf4);

    public static string OverallSummary(decimal overallScoreOutOf4) =>
        $"Tổng thể nhà thuốc đạt {KapPharmacyScoreDisplay.Format(overallScoreOutOf4)} trên thang 10 "
        + $"— {BandLabelTen(overallScoreOutOf4)}.";
}
