namespace KitPlatform.Packs.Survey.Infrastructure;

internal static class KapVietnameseLabels
{
    public const string PlatformTitle = "NỀN TẢNG ĐÁNH GIÁ KAP";
    public const string ReportTitle = "BÁO CÁO TƯ VẤN NĂNG LỰC";
    public const string FooterLine = "Báo cáo KAP · Novixa";

    public static string MaturityLevel(int level, string? name = null) =>
        string.IsNullOrWhiteSpace(name) ? $"Cấp {level}" : $"Cấp {level} — {name}";

    public static string RiskLevel(string level) => level.Trim().ToLowerInvariant() switch
    {
        "high" => "Cao",
        "medium" => "Trung bình",
        "low" => "Thấp",
        _ => level,
    };

    public static string Stars(int count) =>
        count <= 0 ? "—" : new string('★', Math.Clamp(count, 1, 5)) + new string('☆', Math.Clamp(5 - count, 0, 5));

    public static string QualitativeTag(string tag) => tag.Trim().ToLowerInvariant() switch
    {
        "pain_attract" => "Thu hút khách hàng",
        "pain_retention" => "Giữ chân khách hàng",
        "pain_inventory" => "Quản lý kho",
        "pain_staff" => "Quản lý nhân sự",
        "pain_revenue" => "Tăng doanh thu",
        "need_revenue" => "Tăng doanh thu",
        "need_inventory" => "Giảm tồn kho",
        "need_crm" => "Giữ chân khách hàng",
        "need_ops" => "Chuẩn hóa vận hành",
        "need_time" => "Tiết kiệm thời gian quản lý",
        "need_telemed" => "Kết nối bác sĩ khám online",
        _ => tag.Replace('_', ' '),
    };

    public static string ReportKindTitle(KapReportPdfKind kind) => kind switch
    {
        KapReportPdfKind.Executive => "BÁO CÁO ĐIỀU HÀNH",
        KapReportPdfKind.Consulting => "BÁO CÁO TƯ VẤN CHUYỂN ĐỔI SỐ",
        KapReportPdfKind.Appendix => "PHỤ LỤC KỸ THUẬT",
        KapReportPdfKind.Owner => "ĐÁNH GIÁ NHANH NHÀ THUỐC",
        _ => ReportTitle,
    };
}
