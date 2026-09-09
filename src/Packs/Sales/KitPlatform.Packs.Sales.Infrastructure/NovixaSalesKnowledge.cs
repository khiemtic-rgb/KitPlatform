using System.Globalization;
using System.Text;

namespace KitPlatform.Packs.Sales.Infrastructure;

/// <summary>
/// Grounded Novixa replies only. Sources: novixa-site founding FAQ,
/// NVX-SALES-004, founding-program-terms-v1 §7, product_profile novixa-sales-v1.
/// Unknown → escalate. Do not invent features, prices, or promises.
/// </summary>
internal static class NovixaSalesKnowledge
{
    public const string PhcPlaceholder = "{phc}";

    public sealed record Match(string Intent, string Reply, bool Escalate, IReadOnlyList<string> Citations);

    public static Match Resolve(string raw, string phcUrl)
    {
        var text = Fold(raw);
        if (string.IsNullOrWhiteSpace(text))
            return Fill(Unknown, phcUrl);

        foreach (var rule in Rules)
        {
            if (rule.Any.Any(key => text.Contains(key, StringComparison.Ordinal)))
                return Fill(rule, phcUrl);
        }

        return Fill(Unknown, phcUrl);
    }

    private static Match Fill(Rule rule, string phcUrl) =>
        new(rule.Intent, rule.Reply.Replace(PhcPlaceholder, phcUrl, StringComparison.Ordinal), rule.Escalate, rule.Citations);

    private sealed record Rule(
        string Intent,
        string[] Any,
        string Reply,
        bool Escalate,
        IReadOnlyList<string> Citations);

    private static readonly Rule Unknown = new(
        "unknown",
        [],
        "Em chưa có câu trả lời đó trong tài liệu Novixa đã công bố, nên em không dám nói thêm cho chắc. "
        + "Anh/chị để lại câu hỏi — người phụ trách sẽ trả lời đúng phạm vi sản phẩm. "
        + "Nếu muốn, anh/chị có thể làm Pharmacy Health Check (không thu phí) tại " + PhcPlaceholder,
        true,
        ["escalate-if-not-in-sot"]);

    private static readonly Rule[] Rules =
    [
        new(
            "no_revenue_promise",
            ["tang doanh thu", "tang %", "cam ket doanh thu", "chac chan tang", "bao tang"],
            "Novixa không cam kết tăng X% doanh thu. Buổi đầu chỉ đánh giá hiện trạng; "
            + "nếu có chỗ Novixa giải quyết được thì mình mới trao đổi tiếp.",
            false,
            ["nvx-sales-004 §6 cấm"]),
        new(
            "no_unlimited_trial",
            ["dung thu mien phi", "trial", "14 ngay", "dung thu vo thoi han"],
            "Founding Early Access không phải bản demo hay dùng thử 14 ngày. "
            + "Là giai đoạn chạy thật tại quầy, 4 tháng đầu 299.000đ/tháng, hợp đồng tối thiểu 6 tháng.",
            false,
            ["novixa-site foundingFaq", "nvx-sales-004 §6 cấm"]),
        new(
            "not_in_phase1",
            ["hoa don dien tu", "hddt", "hdđt", "ke toan thue", "bao cao thue", "csdl duoc", "duoc quoc gia"],
            "Các phần đó chưa có trong Phase 1: HĐĐT tích hợp, báo cáo thuế/kế toán chuyên sâu, "
            + "và danh mục thuốc quốc gia live đang là roadmap / tham khảo — em không hứa là đã có sẵn.",
            false,
            ["founding-program-terms-v1 §7"]),
        new(
            "price",
            ["bao nhieu", "bao gia", "299", "founding", "chi phi", "gia thang", "phi founding"],
            "Trên website: Founding Early Access 299.000đ/tháng trong 4 tháng đầu, tối thiểu 6 tháng. "
            + "Tháng 5 chốt gói theo mức thực sự dùng — Novixa không tự động nâng giá. "
            + "Bảng giá đầy đủ theo quy mô trao đổi khi demo và ghi rõ khi ký. "
            + "Kịch bản nội bộ còn có Pilot 30 ngày 299.000đ/cơ sở (598.000đ nếu kèm lớp giữ khách/App); "
            + "phí Pilot được tính vào tháng đầu nếu tiếp tục dùng.",
            false,
            ["novixa-site foundingFaq", "nvx-sales-004 §3.5"]),
        new(
            "phc",
            ["health check", "phc", "danh gia", "kiem tra nha thuoc", "bao cao"],
            "Novixa Pharmacy Health Check: khoảng 20–30 phút đánh giá vận hành, kho, khách hàng và chăm sóc sau bán. "
            + "Chương trình hiện không thu phí. Sau đó gửi báo cáo ngắn — mạnh chỗ nào, còn khoảng trống chỗ nào. "
            + "Link: " + PhcPlaceholder,
            false,
            ["nvx-sales-004 §2", "novixa.vn/vi/health-check"]),
        new(
            "is_sales",
            ["ban phan mem", "co phai ban", "chao hang", "ban hang"],
            "Novixa là nền tảng bên em phát triển, nhưng buổi đầu chỉ đánh giá hiện trạng. "
            + "Nếu sau đó anh/chị thấy vấn đề nào Novixa giải quyết được thì mình mới trao đổi tiếp. Không phù hợp cũng không sao.",
            false,
            ["nvx-sales-004 §2"]),
        new(
            "app_optional",
            ["bat buoc app", "khong can app", "khong muon app", "ep app"],
            "App khách không bắt buộc. Có thể Pilot kho/vận hành hoặc giữ khách trước — không ép App khi nền khách/chăm sóc còn trống.",
            false,
            ["nvx-sales-004 §4 + §6"]),
        new(
            "switch_software",
            ["dang dung phan mem", "doi phan mem", "phan mem khac", "dang dung pos", "phan mem roi", "dang dung roi"],
            "Đúng ạ — mình xem phần mềm hiện tại đã đủ phần khách / sau bán chưa, không bắt đầu từ “thay POS”. "
            + "Vì đổi phần mềm phiền nên có Pilot một cơ sở, không yêu cầu chuyển hết ngay.",
            false,
            ["nvx-sales-004 §4"]),
        new(
            "what_is_novixa",
            ["novixa la gi", "lam gi", "tinh nang", "co gi"],
            "Novixa (Smart Pharmacy Solutions) là nền tảng vận hành nhà thuốc: POS, tồn theo lô/HSD (FEFO), "
            + "mua hàng, CRM, báo cáo, và app khách / loyalty / O2O theo gói. "
            + "Phù hợp nhà thuốc GPP muốn gom quầy, lô và chăm sóc khách trên một nền — "
            + "đặc biệt chuỗi 2–10 cửa hoặc đơn lẻ đang dùng Excel kèm POS. "
            + "Nếu chỉ cần thu ngân, không quan tâm lô/HSD, POS bán lẻ phổ biến có thể đủ.",
            false,
            ["novixa-site founding + foundingFaq", "product_profile novixa-sales-v1"]),
        new(
            "greeting",
            ["xin chao", "chao anh", "chao chi", "hello", "alo em", "alo"],
            "Em chào anh/chị, em là Novixa — phần mềm vận hành nhà thuốc (POS, kho lô, khách). "
            + "Em không chào phần mềm ngay. Hiện có Pharmacy Health Check khoảng 20–30 phút, không thu phí, "
            + "sau đó gửi báo cáo ngắn. Anh/chị làm tại " + PhcPlaceholder + " hoặc hỏi em đúng phạm vi Novixa đã công bố.",
            false,
            ["nvx-sales-004 §2"]),
        new(
            "next_step",
            ["dang ky", "muon thu", "buoc tiep", "lam sao", "bat dau", "lien he"],
            "Bước đầu tiên Novixa đang làm là Pharmacy Health Check (không thu phí): " + PhcPlaceholder + " "
            + "Sau báo cáo mình mới nói Pilot / Founding nếu khớp. Em không mở lịch dùng thử vô thời hạn.",
            false,
            ["nvx-sales-004 §2", "novixa.vn/vi/health-check"]),
    ];

    private static string Fold(string value)
    {
        var form = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(form.Length);
        foreach (var ch in form)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(ch);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
