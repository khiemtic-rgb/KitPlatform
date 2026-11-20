using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Survey.Infrastructure;

/// <summary>Chuẩn hóa hiển thị tiếng Việt + mở rộng viết tắt trên PDF/báo cáo.</summary>
internal static class KapVietnameseText
{
    private static readonly Dictionary<string, string> CategoryByCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CUSTOMER"] = "Khách hàng",
        ["OPERATIONS"] = "Vận hành",
        ["INVENTORY"] = "Kho",
        ["BUSINESS"] = "Kinh doanh",
        ["TECH"] = "Dữ liệu & Công nghệ",
        ["GROWTH"] = "Phát triển",
        ["QUALITATIVE"] = "Khảo sát",
        ["GENERAL"] = "Tổng quan",
    };

    private static readonly Dictionary<string, string> ExactFixes = new(StringComparer.Ordinal)
    {
        ["Khach hang"] = "Khách hàng",
        ["Van hanh"] = "Vận hành",
        ["Du lieu & Cong nghe"] = "Dữ liệu & Công nghệ",
        ["Phat trien"] = "Phát triển",
        ["Khach hang (tong)"] = "Khách hàng (tổng)",
        ["Van hanh (tong)"] = "Vận hành (tổng)",
        ["Kho (tong)"] = "Kho (tổng)",
        ["Kinh doanh (tong)"] = "Kinh doanh (tổng)",
        ["Du lieu & Cong nghe (tong)"] = "Dữ liệu & Công nghệ (tổng)",
        ["Phat trien (tong)"] = "Phát triển (tổng)",
        ["Du lieu roi rac / Excel"] = "Dữ liệu rời rạc / Excel",
        ["Thieu SOP va phan quyen ro"] = "Thiếu quy trình chuẩn (SOP) và phân quyền rõ",
        ["Nhom Du lieu & Cong nghe diem thap"] = "Nhóm Dữ liệu & Công nghệ điểm thấp",
        ["Nhom Van hanh diem thap"] = "Nhóm Vận hành điểm thấp",
        ["Nhom Khach hang diem thap"] = "Nhóm Khách hàng điểm thấp",
        ["Chua theo doi hang can han"] = "Chưa theo dõi hàng cận hạn",
        ["Kiem ke thu cong, thieu dinh ky"] = "Kiểm kê thủ công, thiếu định kỳ",
        ["Nhap hang theo cam tinh"] = "Nhập hàng theo cảm tính",
        ["Thieu ho so khach hang tap trung"] = "Thiếu hồ sơ khách hàng tập trung",
        ["Rui ro ton kho & HSD"] = "Rủi ro tồn kho & hạn sử dụng",
        ["Rui ro du lieu roi rac"] = "Rủi ro dữ liệu rời rạc",
        ["Cong nghe chua dong bo"] = "Công nghệ chưa đồng bộ",
        ["Chuan hoa FEFO & canh bao HSD"] = "Chuẩn hóa xuất kho theo hạn dùng & cảnh báo hạn sử dụng",
        ["Ty le hang can han xu ly dung han"] = "Tỷ lệ hàng cận hạn xử lý đúng hạn",
    };

    // Cụm dài trước — tránh thay từng từ làm sai ngữ cảnh
    private static readonly (Regex Pattern, string Replacement)[] InlineFixes =
    [
        (new Regex(@"\bBan hang, kho va KH khong lien ket\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "Bán hàng, kho và khách hàng không liên kết"),
        (new Regex(@"\bQuy trinh ban hang va giao ca chua thong nhat\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "Quy trình bán hàng và giao ca chưa thống nhất"),
        (new Regex(@"\bphu thuoc nhan su chu chot\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "phụ thuộc nhân sự chủ chốt"),
        (new Regex(@"\bchu nha thuoc kho xem tinh hinh tu xa\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "chủ nhà thuốc khó xem tình hình từ xa"),
        (new Regex(@"\bra quyet dinh nhanh\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "ra quyết định nhanh"),
        (new Regex(@"\bNhom Du lieu & Cong nghe diem thap\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "Nhóm Dữ liệu & Công nghệ điểm thấp"),
        (new Regex(@"\bDu lieu roi rac\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Dữ liệu rời rạc"),
        (new Regex(@"\bdiem thap\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "điểm thấp"),
        (new Regex(@"\bDiem manh\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Điểm mạnh"),
        (new Regex(@"\bDiem yeu\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Điểm yếu"),
        (new Regex(@"\bBan hang\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Bán hàng"),
        (new Regex(@"\blien ket\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "liên kết"),
        (new Regex(@"\broi rac\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "rời rạc"),
        (new Regex(@"\btinh hinh\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "tình hình"),
        (new Regex(@"\btu xa\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "từ xa"),
        (new Regex(@"\bthong nhat\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "thống nhất"),
        (new Regex(@"\bphu thuoc\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "phụ thuộc"),
        (new Regex(@"\bnhan su\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "nhân sự"),
        (new Regex(@"\bchu chot\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "chủ chốt"),
        (new Regex(@"\bphan quyen\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "phân quyền"),
        (new Regex(@"\bgiao ca\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "giao ca"),
        (new Regex(@"\bChu nha thuoc\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Chủ nhà thuốc"),
        (new Regex(@"\bkho xem\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "khó xem"),
        (new Regex(@"\bNhom\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Nhóm"),
        (new Regex(@"\bCau\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Câu"),
        (new Regex(@"\btheo doi\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "theo dõi"),
        (new Regex(@"\bhang can han\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "hàng cận hạn"),
        (new Regex(@"\bho so\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "hồ sơ"),
        (new Regex(@"\btap trung\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "tập trung"),
        (new Regex(@"\bcam tinh\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "cảm tính"),
        (new Regex(@"\bdinh ky\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "định kỳ"),
        (new Regex(@"\bnguyen nhan\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "nguyên nhân"),
        (new Regex(@"\bquyet dinh\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "quyết định"),
        (new Regex(@"\bap dung\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "áp dụng"),
        (new Regex(@"\bcanh bao\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "cảnh báo"),
        (new Regex(@"\bChuan hoa\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Chuẩn hóa"),
        (new Regex(@"\bchi phi co hoi\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "chi phí cơ hội"),
        (new Regex(@"\bkhong hanh dong\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "không hành động"),
        (new Regex(@"\bkhong cai thien\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "không cải thiện"),
        (new Regex(@"\bkhong co\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "không có"),
        (new Regex(@"\bchua co\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "chưa có"),
        (new Regex(@"\bchua duoc\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "chưa được"),
        (new Regex(@"\bchua chuan hoa\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "chưa chuẩn hóa"),
        (new Regex(@"\bcan uu tien\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "cần ưu tiên"),
        (new Regex(@"\bcan cai thien\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "cần cải thiện"),
        (new Regex(@"\bcan hanh dong\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "cần hành động"),
        (new Regex(@"\btrong (\d+) ngay\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "trong $1 ngày"),
        (new Regex(@"\btrong (\d+) thang\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "trong $1 tháng"),
        (new Regex(@"\b(\d+) ngay dau\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "$1 ngày đầu"),
        (new Regex(@"\bChuyen doi so\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Chuyển đổi số"),
        (new Regex(@"\bSan sang\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Sẵn sàng"),
        (new Regex(@"\bDoi thu\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Đối thủ"),
        (new Regex(@"\bKhu vuc\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Khu vực"),
        (new Regex(@"\bThi truong\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Thị trường"),
        (new Regex(@"\bDoanh nghiep\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Doanh nghiệp"),
        (new Regex(@"\bTruong thanh\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Trưởng thành"),
        (new Regex(@"\bPhan tich\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Phân tích"),
        (new Regex(@"\bDanh gia\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Đánh giá"),
        (new Regex(@"\bKhuyen nghi\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Khuyến nghị"),
        (new Regex(@"\bKet luan\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Kết luận"),
        (new Regex(@"\bMo rong\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Mở rộng"),
        (new Regex(@"\bKha nang\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Khả năng"),
        (new Regex(@"\bKe hoach\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Kế hoạch"),
        (new Regex(@"\bHanh dong\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Hành động"),
        (new Regex(@"\bMuc tieu\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Mục tiêu"),
        (new Regex(@"\bKet qua\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Kết quả"),
        (new Regex(@"\bUu tien\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Ưu tiên"),
        (new Regex(@"\bTrung binh\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Trung bình"),
        (new Regex(@"\bSo sanh\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "So sánh"),
        (new Regex(@"\bHe qua\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Hệ quả"),
        (new Regex(@"\bCo hoi\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Cơ hội"),
        (new Regex(@"\bThach thuc\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Thách thức"),
        (new Regex(@"\bHieu qua\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Hiệu quả"),
        (new Regex(@"\bNang suat\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Năng suất"),
        (new Regex(@"\bPhu hop\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Phù hợp"),
        (new Regex(@"\bDau tu\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Đầu tư"),
        (new Regex(@"\bTrien khai\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Triển khai"),
        (new Regex(@"\bGiai doan\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Giai đoạn"),
        (new Regex(@"\bLoi nhuan\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Lợi nhuận"),
        (new Regex(@"\bChi phi\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Chi phí"),
        (new Regex(@"\bQuan ly\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Quản lý"),
        (new Regex(@"\bNhan vien\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Nhân viên"),
        (new Regex(@"\bLien he\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Liên hệ"),
        (new Regex(@"\bTu van\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Tư vấn"),
        (new Regex(@"\bCai thien\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Cải thiện"),
        (new Regex(@"\bHan che\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Hạn chế"),
        (new Regex(@"\bTang\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Tăng"),
        (new Regex(@"\bGiam\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Giảm"),
        (new Regex(@"\bTot\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Tốt"),
        (new Regex(@"\bYeu\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Yếu"),
        (new Regex(@"\bManh\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Mạnh"),
        (new Regex(@"\bKem\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Kém"),
        (new Regex(@"\bCham\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Chậm"),
        (new Regex(@"\bNen\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Nên"),
        (new Regex(@"\bCan\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Cần"),
        (new Regex(@"\bPhai\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Phải"),
        (new Regex(@"\bDuoc\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Được"),
        (new Regex(@"\bSe\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Sẽ"),
        (new Regex(@"\bDa\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Đã"),
        (new Regex(@"\bHeatmap\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Bản đồ nhiệt"),
        (new Regex(@"\bPercentile\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Phân vị"),
        (new Regex(@"\bGap Analysis\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Phân tích khoảng cách"),
        (new Regex(@"\bExecutive Summary\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Tóm tắt điều hành"),
        (new Regex(@"\bHealth Wallet\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Ví sức khỏe"),
        (new Regex(@"\bAI Copilot\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Trợ lý AI"),
        (new Regex(@"\bloyalty\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "khách hàng thân thiết"),
        (new Regex(@"(?<!\()\bROI\b(?!\))", RegexOptions.Compiled), "lợi ích vận hành"),
        (new Regex(@"(?<!\()\bKPI\b(?!\))", RegexOptions.Compiled), "chỉ số đo lường"),
        (new Regex(@"\b va \b", RegexOptions.Compiled | RegexOptions.IgnoreCase), " và "),
        (new Regex(@"\bKH\b", RegexOptions.Compiled), "khách hàng"),
        (new Regex(@"\bNCC\b", RegexOptions.Compiled), "nhà cung cấp"),
        (new Regex(@"\bHSD\b", RegexOptions.Compiled), "hạn sử dụng"),
        (new Regex(@"\bFEFO\b", RegexOptions.Compiled), "xuất kho theo hạn dùng (FEFO)"),
        (new Regex(@"\bGPP\b", RegexOptions.Compiled), "Thực hành tốt nhà thuốc (GPP)"),
        (new Regex(@"\bSOP\b", RegexOptions.Compiled), "quy trình chuẩn (SOP)"),
        (new Regex(@"\bCRM\b", RegexOptions.Compiled), "quản trị khách hàng"),
        (new Regex(@"\bPOS\b", RegexOptions.Compiled), "bán hàng (POS)"),
        (new Regex(@"\bDashboard\b", RegexOptions.Compiled), "bảng điều khiển"),
        (new Regex(@"\bTy le\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Tỷ lệ"),
        (new Regex(@"\bRui ro\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Rủi ro"),
        (new Regex(@"\bDu lieu\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Dữ liệu"),
        (new Regex(@"\bHe thong\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Hệ thống"),
        (new Regex(@"\bQuy trinh\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Quy trình"),
        (new Regex(@"\bNha thuoc\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Nhà thuốc"),
        (new Regex(@"\bKhach hang\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Khách hàng"),
        (new Regex(@"\bVan hanh\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Vận hành"),
        (new Regex(@"\bCong nghe\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Công nghệ"),
        (new Regex(@"\bKinh doanh\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Kinh doanh"),
        (new Regex(@"\bPhat trien\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Phát triển"),
        (new Regex(@"\bThieu\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Thiếu"),
        (new Regex(@"\bChua\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Chưa"),
        (new Regex(@"\bKhong\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Không"),
        (new Regex(@"\bThu cong\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Thủ công"),
        (new Regex(@"\bTu dong\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Tự động"),
        (new Regex(@"\bBao cao\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Báo cáo"),
        (new Regex(@"\bTon kho\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Tồn kho"),
        (new Regex(@"\bNhap hang\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Nhập hàng"),
        (new Regex(@"\bKiem ke\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Kiểm kê"),
        (new Regex(@"\bDiem\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "Điểm"),
        (new Regex(@"\b dong bo\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), " đồng bộ"),
    ];

    public static string Category(string code, string? name) =>
        CategoryByCode.TryGetValue(code, out var mapped) ? mapped : Display(name);

    public static string Display(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var t = text.Trim();
        if (ExactFixes.TryGetValue(t, out var exact))
            return exact;

        foreach (var (pattern, replacement) in InlineFixes)
            t = pattern.Replace(t, replacement);

        return t;
    }

    /// <summary>Chuẩn hóa + loại lặp ngoặc/abbreviation (POS (POS), ROI (ROI)…).</summary>
    public static string Polish(string? text)
    {
        var t = Display(text);
        if (string.IsNullOrWhiteSpace(t))
            return string.Empty;

        t = Regex.Replace(t, @"\(([^)]+)\)\s*\(\1\)", "($1)", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"(\blợi ích vận hành\s*){2,}", "lợi ích vận hành ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"(\blợi ích đầu tư\s*\(ROI\)\s*){2,}", "lợi ích vận hành ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"\blợi ích đầu tư\s*\(ROI\)\s*\(ROI\)", "lợi ích vận hành", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"\bKPI\s*\(KPI\)", "KPI", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"bán hàng\s*\(POS\)\s*\(POS\)", "bán hàng (POS)", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"\bPOS\s*\(POS\)", "POS", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"([^\s]+)\s*↓\s*\1", "$1", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"\s{2,}", " ").Trim();
        return t;
    }

    /// <summary>Loại câu/đoạn nhắc giá, hoàn vốn, thu hồi chi phí khỏi nội dung lợi ích.</summary>
    public static string StripPricingMentions(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var parts = text.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var kept = parts.Where(p => !ContainsPricingLanguage(p)).ToList();
        return kept.Count > 0 ? string.Join(". ", kept) + (text.TrimEnd().EndsWith('.') ? "." : "") : Display(text);
    }

    public static bool ContainsPricingLanguage(string? text) =>
        !string.IsNullOrWhiteSpace(text) && (
            text.Contains("triệu", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Thu hồi chi phí", StringComparison.OrdinalIgnoreCase)
            || text.Contains("hoàn vốn", StringComparison.OrdinalIgnoreCase)
            || text.Contains("chi phí phần mềm", StringComparison.OrdinalIgnoreCase)
            || text.Contains("ROI dương", StringComparison.OrdinalIgnoreCase));
}
