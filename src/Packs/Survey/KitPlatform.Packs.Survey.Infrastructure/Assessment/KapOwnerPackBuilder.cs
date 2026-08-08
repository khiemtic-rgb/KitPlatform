using KitPlatform.Packs.Survey;

namespace KitPlatform.Packs.Survey.Infrastructure;

/// <summary>
/// Builds owner-facing PHC summary from existing scores/insights — no rescoring.
/// Language aimed at pharmacy owners (non-technical).
/// </summary>
internal static class KapOwnerPackBuilder
{
    public static AssessmentOwnerPackDto Build(
        AssessmentFullReportDto report,
        AssessmentReportIntelligenceDto intel)
    {
        var cats = report.CategoryScores
            .OrderByDescending(c => c.Score)
            .ToList();

        var strengths = cats
            .Take(3)
            .Select(c => new AssessmentOwnerPointDto(
                KapPharmacyLanguage.SimpleName(c.Code, c.Name),
                SoftStrengthBody(c.Code, c.Score),
                c.Code))
            .ToList();

        var pains = BuildPains(report, intel, cats);

        var oneThing = pains.Count > 0
            ? $"Trong 30 ngày tới, ưu tiên xử lý: {pains[0].Title}."
            : "Trong 30 ngày tới, chọn một quy trình đang tốn nhiều thời gian nhất của chủ để chuẩn hóa và đo lại.";

        var actions = BuildActions(intel, pains);

        var pilot = BuildPilotHinge(pains);

        var maturityName = intel.Maturity is not null
            ? KapVietnameseText.Display(intel.Maturity.Name)
            : MaturityFromPct(report.OverallPct);

        var headline = KapPharmacyLanguage.OverallSummary(report.OverallScore);
        if (intel.ConsultingBrief is not null
            && !string.IsNullOrWhiteSpace(intel.ConsultingBrief.DiagnosisHeadline))
        {
            var d = KapVietnameseText.Display(intel.ConsultingBrief.DiagnosisHeadline);
            if (d.Length is > 12 and < 160)
                headline = d;
        }

        return new AssessmentOwnerPackDto(
            OverallHeadline: headline,
            MaturityLabel: maturityName,
            OverallScorePct: report.OverallPct,
            Strengths: strengths,
            Pains: pains,
            OneThingFirst: oneThing,
            Actions30Days: actions,
            PilotHinge: pilot,
            NextStepCta: "Đặt lịch trao đổi 20 phút về kết quả — chọn 1–2 việc ưu tiên cho Pilot 30 ngày.");
    }

    private static List<AssessmentOwnerPainDto> BuildPains(
        AssessmentFullReportDto report,
        AssessmentReportIntelligenceDto intel,
        List<AssessmentCategoryScoreDto> catsStrongFirst)
    {
        var weak = catsStrongFirst.OrderBy(c => c.Score).Take(3).ToList();
        var fromCats = weak
            .Select(c => new AssessmentOwnerPainDto(
                Title: KapPharmacyLanguage.SimpleName(c.Code, c.Name),
                BusinessConsequence: SoftPainConsequence(c.Code),
                AreaCode: c.Code,
                SourceHint: $"Nhóm điểm {KapPharmacyScoreDisplay.Format(c.Score)}"))
            .ToList();

        // Prefer opportunity titles when they map to weak areas (still plain language).
        if (intel.Opportunities.Count > 0)
        {
            var merged = new List<AssessmentOwnerPainDto>();
            foreach (var opp in intel.Opportunities.Take(5))
            {
                var title = KapVietnameseText.Display(opp.Title);
                var body = KapVietnameseText.Display(opp.Body);
                var areaCode = MapArea(opp.Area);
                var consequence = ResolveConsequence(opp.ImpactHint, body, areaCode);
                if (string.IsNullOrWhiteSpace(title) || LooksLikeRawCode(title))
                    continue;
                merged.Add(new AssessmentOwnerPainDto(
                    Title: Truncate(HumanizeTitle(title), 80),
                    BusinessConsequence: consequence,
                    AreaCode: areaCode,
                    SourceHint: "Từ phân tích cơ hội"));
                if (merged.Count >= 3) break;
            }

            if (merged.Count >= 2)
                return merged;
        }

        if (intel.ConsultingBrief?.CostOfInaction is { Length: > 20 } cost
            && fromCats.Count > 0)
        {
            fromCats[0] = fromCats[0] with
            {
                BusinessConsequence = SoftenConsequence(
                    Truncate(KapVietnameseText.Display(cost), 180)),
            };
        }

        return fromCats;
    }

    private static List<AssessmentOwnerActionDto> BuildActions(
        AssessmentReportIntelligenceDto intel,
        IReadOnlyList<AssessmentOwnerPainDto> pains)
    {
        var list = new List<AssessmentOwnerActionDto>();

        if (intel.ActionPlan?.Items.Count > 0)
        {
            foreach (var item in intel.ActionPlan.Items.Take(3))
            {
                list.Add(new AssessmentOwnerActionDto(
                    Title: Truncate(KapVietnameseText.Display(item.Title), 100),
                    Who: string.IsNullOrWhiteSpace(item.Owner) ? "Chủ / quản lý ca" : KapVietnameseText.Display(item.Owner),
                    When: string.IsNullOrWhiteSpace(item.Timeline) ? "Trong 30 ngày" : KapVietnameseText.Display(item.Timeline),
                    DoneWhen: string.IsNullOrWhiteSpace(item.ExpectedOutcome)
                        ? "Có quy trình rõ và đã chạy ít nhất 1 tuần"
                        : Truncate(KapVietnameseText.Display(item.ExpectedOutcome), 120)));
            }
        }
        else if (intel.Roadmap?.Days30.Count > 0)
        {
            foreach (var item in intel.Roadmap.Days30.Take(3))
            {
                list.Add(new AssessmentOwnerActionDto(
                    Title: Truncate(KapVietnameseText.Display(item.Title), 100),
                    Who: "Chủ / quản lý ca",
                    When: "Trong 30 ngày",
                    DoneWhen: Truncate(KapVietnameseText.Display(item.Body), 120)));
            }
        }

        if (list.Count == 0 && pains.Count > 0)
        {
            list.Add(new AssessmentOwnerActionDto(
                Title: $"Bắt đầu xử lý: {pains[0].Title}",
                Who: "Chủ nhà thuốc",
                When: "Tuần 1–4",
                DoneWhen: "Đã có người phụ trách và checklist hàng tuần"));
        }

        return list.Take(3).ToList();
    }

    private static AssessmentOwnerPilotHingeDto BuildPilotHinge(IReadOnlyList<AssessmentOwnerPainDto> pains)
    {
        var options = new[]
        {
            "An tâm vận hành & kho (bán thống nhất, hạn dùng, tồn)",
            "Giữ khách & nhắc mua lại",
            "Chủ theo dõi từ xa / nhiều cơ sở",
        };

        var focus = "An tâm vận hành & kho (bán thống nhất, hạn dùng, tồn)";
        var codes = pains.Select(p => p.AreaCode).Where(c => !string.IsNullOrWhiteSpace(c)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (codes.Contains("CUSTOMER") || codes.Contains("GROWTH"))
            focus = options[1];
        else if (codes.Contains("INVENTORY") || codes.Contains("OPERATIONS") || codes.Contains("BUSINESS"))
            focus = options[0];
        else if (codes.Contains("TECH"))
            focus = options[2];

        var talk = pains.Count > 0
            ? $"Anh/chị đang gặp: «{pains[0].Title}». Pilot 30 ngày chỉ tập trung 1–2 việc này — không đổi hết hệ thống một lúc."
            : "Pilot 30 ngày chỉ chọn 1–2 mục tiêu đo được — không yêu cầu chuyển đổi toàn bộ ngay.";

        return new AssessmentOwnerPilotHingeDto(focus, options, talk);
    }

    private static string SoftStrengthBody(string code, decimal score) =>
        $"{KapPharmacyLanguage.SimpleName(code)} đang ở mức {KapPharmacyScoreDisplay.Format(score)} — nên giữ vững và tinh chỉnh dần, không cần đập đi làm lại.";

    private static string ResolveConsequence(string? impactHint, string body, string? areaCode)
    {
        var hint = KapVietnameseText.Display(impactHint);
        if (!string.IsNullOrWhiteSpace(hint) && !LooksLikeRawCode(hint))
            return SoftenConsequence(Truncate(hint, 180));

        if (!string.IsNullOrWhiteSpace(body) && !LooksLikeRawCode(body))
            return SoftenConsequence(Truncate(body, 160));

        if (!string.IsNullOrWhiteSpace(areaCode))
            return SoftPainConsequence(areaCode);

        return SoftGenericConsequence();
    }

    private static bool LooksLikeRawCode(string text)
    {
        var t = text.Trim();
        if (t.Length == 0) return true;
        // impact_high, IMPACT-HIGH, need_crm, etc.
        if (!t.Contains(' ') && t.Contains('_')) return true;
        if (!t.Contains(' ') && t.Contains('-') && t.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            return true;
        if (t.StartsWith("impact_", StringComparison.OrdinalIgnoreCase)) return true;
        if (t.StartsWith("priority_", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string HumanizeTitle(string title)
    {
        var t = title.Trim();
        // Strip noisy prefixes from generated opportunities
        foreach (var prefix in new[] { "Cơ hội: ", "Co hoi: ", "Opportunity: " })
        {
            if (t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                t = t[prefix.Length..].Trim();
        }
        return SoftenConsequence(t);
    }

    private static string SoftenConsequence(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return SoftGenericConsequence();
        if (LooksLikeRawCode(text))
        {
            return text.Trim().ToLowerInvariant() switch
            {
                "impact_high" or "high" => "Ảnh hưởng rõ tới doanh thu, vốn hoặc thời gian quản lý của chủ.",
                "impact_medium" or "medium" => "Ảnh hưởng vừa phải — nên xử lý trong 30–60 ngày.",
                "impact_low" or "low" => "Ảnh hưởng nhẹ — theo dõi và xử lý khi đã xong việc ưu tiên hơn.",
                _ => SoftGenericConsequence(),
            };
        }

        var t = text.Trim();
        t = t.Replace("yếu kém", "còn dư địa", StringComparison.OrdinalIgnoreCase);
        t = t.Replace("nhà thuốc yếu", "nhà thuốc còn khoảng trống", StringComparison.OrdinalIgnoreCase);
        t = t.Replace("mức yếu", "còn khoảng trống", StringComparison.OrdinalIgnoreCase);
        return t;
    }

    private static string SoftGenericConsequence() =>
        "Tốn thời gian quản lý hơn mức cần thiết và khó giữ khách/vốn ổn định lâu dài.";

    private static string SoftPainConsequence(string code) => code.ToUpperInvariant() switch
    {
        "CUSTOMER" => "Dễ mất doanh thu từ khách quen vì chưa nhắc đúng lúc — khách sang cửa khác mà chủ chưa kịp biết.",
        "INVENTORY" => "Vốn dễ nằm im trong hàng chậm/cận hạn; thiếu hàng bán chạy thì mất đơn ngay tại quầy.",
        "OPERATIONS" => "Chủ phải hỏi nhân viên mới yên tâm; giao ca lệch dễ lệch tiền/thuốc.",
        "BUSINESS" => "Bán nhiều chưa chắc lãi — khó biết nhóm hàng nào đang kéo lợi nhuận.",
        "TECH" => "Xem tình hình muộn, phụ thuộc sổ/Excel rời — khó quyết định khi không đứng quầy.",
        "GROWTH" => "Muốn mở rộng nhưng nền tảng chưa đủ chuẩn — mở thêm điểm dễ nhân bản cả lỗi cũ.",
        _ => SoftGenericConsequence(),
    };

    private static string? MapArea(string? area)
    {
        if (string.IsNullOrWhiteSpace(area)) return null;
        var a = area.Trim().ToUpperInvariant();
        if (a.Contains("CUST") || a.Contains("KHÁCH") || a.Contains("KHACH")) return "CUSTOMER";
        if (a.Contains("INV") || a.Contains("KHO")) return "INVENTORY";
        if (a.Contains("OPS") || a.Contains("VẬN") || a.Contains("VAN")) return "OPERATIONS";
        if (a.Contains("TECH") || a.Contains("DATA") || a.Contains("DỮ")) return "TECH";
        if (a.Contains("GROW") || a.Contains("PHÁT")) return "GROWTH";
        if (a.Contains("BUS") || a.Contains("KINH")) return "BUSINESS";
        return null;
    }

    private static string MaturityFromPct(decimal pct) => pct switch
    {
        >= 81 => "Trưởng thành",
        >= 61 => "Vận hành tốt",
        >= 41 => "Đang kiểm soát",
        >= 21 => "Đang hình thành",
        _ => "Khởi đầu",
    };

    private static string Truncate(string s, int max)
    {
        s = s.Trim();
        if (s.Length <= max) return s;
        return s[..(max - 1)].TrimEnd() + "…";
    }
}
