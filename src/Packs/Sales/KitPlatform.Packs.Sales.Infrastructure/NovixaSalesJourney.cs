using KitPlatform.Packs.Sales;

namespace KitPlatform.Packs.Sales.Infrastructure;

/// <summary>
/// Outbound Facebook sequence + quiet-window clock (VN pharmacy).
/// Copy stays inside NVX-SALES-004, product_profile, founding FAQ.
/// </summary>
internal static class NovixaSalesJourney
{
    public const string PhcPlaceholder = "{phc}";

    public static readonly string[] Steps = ["hook", "phc_ask", "report_ask", "pilot_ask"];

    public static IReadOnlyList<KitSalesJourneyPainDto> Pains =>
        NovixaPainCatalog.All.Select(p => new KitSalesJourneyPainDto(
            p.Code,
            p.Name,
            "pain-library-v1",
            p.Category,
            NovixaPainCatalog.CategoryLabel(p.Category))).ToList();

    public static bool IsJourneyCode(string? code)
    {
        var step = Parse(code).Step;
        return Steps.Contains(step);
    }

    public static (string Step, string Pain) Parse(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return ("hook", NovixaPainCatalog.All[0].Code);
        var parts = code.Split(':', 2);
        var step = Steps.Contains(parts[0]) ? parts[0] : "hook";
        var pain = parts.Length > 1 ? NovixaPainCatalog.Canonical(parts[1]) : NovixaPainCatalog.All[0].Code;
        return (step, pain);
    }

    public static string Encode(string step, string pain)
    {
        var s = Steps.Contains(step) ? step : "hook";
        return $"{s}:{NovixaPainCatalog.Canonical(pain)}";
    }

    public static string NextStep(string step)
    {
        var i = Array.IndexOf(Steps, step);
        if (i < 0 || i >= Steps.Length - 1)
            return Steps[^1];
        return Steps[i + 1];
    }

    public static KitSalesJourneyPainDto Pain(string code)
    {
        var p = NovixaPainCatalog.Get(code);
        return new(p.Code, p.Name, "pain-library-v1", p.Category, NovixaPainCatalog.CategoryLabel(p.Category));
    }

    public static string Draft(string step, string pain, string phcUrl)
    {
        var body = step switch
        {
            "phc_ask" =>
                "Anh/chị làm giúp em Pharmacy Health Check được không ạ — khoảng 20–30 phút, hiện không thu phí. "
                + "Xong em gửi báo cáo tóm tắt (mạnh chỗ nào, còn khoảng trống chỗ nào). "
                + PhcPlaceholder,
            "report_ask" =>
                "Anh/chị đã xem phần Kết quả tóm tắt em gửi chưa ạ? "
                + "Phần nào thấy đúng nhất với nhà thuốc hiện tại?",
            "pilot_ask" =>
                "Em không đề nghị anh/chị chuyển toàn bộ ngay. "
                + "Mình có thể làm Pilot 30 ngày tại một cơ sở, chỉ 1–2 mục tiêu trên báo cáo. "
                + "Phí Pilot: 299.000đ/cơ sở/30 ngày, hoặc 598.000đ nếu kèm lớp giữ khách/App. "
                + "Nếu tiếp tục dùng Novixa, phí Pilot được tính vào tháng đầu.",
            _ => HookFromLibrary(pain),
        };
        return body.Replace(PhcPlaceholder, phcUrl, StringComparison.Ordinal);
    }

    public static IReadOnlyList<string> Citations(string step, string pain) =>
        step switch
        {
            "phc_ask" => ["nvx-sales-004 §2", "novixa.vn/vi/health-check"],
            "report_ask" => ["nvx-sales-004 §3.1"],
            "pilot_ask" => ["nvx-sales-004 §3.5"],
            _ => ["pain-library-v1", "nvx-sales-004 §2 — hỏi trước, giải pháp sau"],
        };

    public static DateTimeOffset NextQuietWindow(DateTimeOffset fromUtc)
    {
        var tz = VnTz();
        var local = TimeZoneInfo.ConvertTime(fromUtc, tz);
        var slots = new[] { new TimeSpan(14, 30, 0), new TimeSpan(20, 0, 0) };
        var preferred = new[] { DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday };

        for (var day = 0; day < 16; day++)
        {
            var date = local.Date.AddDays(day);
            if (!preferred.Contains(date.DayOfWeek))
                continue;
            foreach (var slot in slots)
            {
                var candidate = date + slot;
                if (candidate <= local.AddMinutes(40))
                    continue;
                return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(candidate, DateTimeKind.Unspecified), tz);
            }
        }

        var fallback = local.Date.AddDays(1).AddHours(14).AddMinutes(30);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(fallback, DateTimeKind.Unspecified), tz);
    }

    public static string WindowLabel(DateTimeOffset utc)
    {
        var local = TimeZoneInfo.ConvertTime(utc, VnTz());
        var day = local.ToString("dddd dd/MM", new System.Globalization.CultureInfo("vi-VN"));
        return $"{day} {local:HH:mm} (giờ VN — chiều/tối giữa tuần)";
    }

    public const string WhyNow =
        "Tránh đầu giờ sáng (mở cửa, giao hàng, chủ đang căng). "
        + "Chọn Thứ 3–5, 14:30 hoặc 20:00 — lúc quầy thường vắng, chủ hay lo khách trong ngày nên dễ nói chuyện hơn.";

    private static string HookFromLibrary(string pain)
    {
        var item = NovixaPainCatalog.Get(pain);
        return item.Outreach + "\n\n" + item.Question;
    }

    private static TimeZoneInfo VnTz()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }
}
