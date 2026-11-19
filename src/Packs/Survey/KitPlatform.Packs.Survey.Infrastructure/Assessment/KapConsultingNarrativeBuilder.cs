namespace KitPlatform.Packs.Survey.Infrastructure;

internal static class KapConsultingNarrativeBuilder
{
    private sealed record CategoryPlaybook(
        string DisplayName,
        string WeakDiagnosis,
        string BusinessImpact,
        string CostRange,
        string ModuleName,
        string ModulePain,
        string Outcome30,
        string Outcome90);

    private static readonly Dictionary<string, CategoryPlaybook> Playbooks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["INVENTORY"] = new(
            "Quản lý kho & hàng hóa",
            "Kho đang vận hành chủ yếu bằng kinh nghiệm và Excel — khó biết chính xác tồn, hạn dùng và hàng cần nhập.",
            "Tồn đọng và hàng cận date là «lỗ hổng âm thầm»: vốn bị kẹt, lợi nhuận bị bào mòn mỗi tháng dù doanh thu vẫn ổn.",
            "3–8% lợi nhuận/tháng",
            "Theo dõi hàng cận hạn & kiểm kê",
            "Ghi nhận lô, hạn dùng khi nhập; cảnh báo trước 90/60/30 ngày; nhập theo tốc độ bán.",
            "Kiểm kê số hóa + bật cảnh báo HSD — biết ngay hàng nào cần xử lý trong tuần.",
            "Giảm tồn chậm 20–30%, hạn chế thất thoát, đạt chuẩn GPP về quản lý hạn dùng."),
        ["CUSTOMER"] = new(
            "Khách hàng & giữ chân",
            "Chưa có hồ sơ khách tập trung — mỗi lần khách quay lại là «khách mới», không tận dụng được lịch sử mua và chăm sóc.",
            "Chi phí thu hút khách mới cao gấp 5–7 lần giữ chân khách cũ. Mất dữ liệu KH = mất doanh thu tái mua định kỳ.",
            "10–20% doanh thu lặp lại bị bỏ lỡ",
            "Giữ chân khách quen",
            "Ghi nhận khách, nhắc tái mua thuốc và chăm sóc khách mua định kỳ.",
            "Ghi nhận SĐT khi bán + gửi nhắc uống thuốc — tăng tỷ lệ quay lại trong 30 ngày.",
            "Tăng 15–25% doanh thu từ khách quay lại, giảm phụ thuộc vào khách vãng lai."),
        ["TECH"] = new(
            "Dữ liệu & công nghệ",
            "Dữ liệu bán hàng, kho và khách nằm rời rạc — chủ nhà thuốc không xem được tình hình thực khi không có mặt tại quầy.",
            "Quyết định nhập hàng và điều tiết giá bán bị chậm 1–2 tuần so với thực tế thị trường — cơ hội bị đối thủ nắm trước.",
            "2–4 giờ/ngày cho việc đối chiếu thủ công",
            "Theo dõi kinh doanh tập trung",
            "Bán hàng, kho và khách trong một sổ sách/hệ thống — xem doanh thu và tồn từ điện thoại.",
            "Đồng bộ dữ liệu bán hàng theo thời gian thực — báo cáo cuối ngày tự động, không cần gõ Excel.",
            "Ra quyết định nhập hàng dựa trên số liệu 30 ngày — giảm 50% thời gian quản lý."),
        ["OPERATIONS"] = new(
            "Vận hành & quy trình",
            "Quy trình bán hàng, giao ca và kiểm soát thu chi chưa chuẩn hóa — phụ thuộc vào nhân viên chủ chốt.",
            "Sai sót khi đổi ca, thiếu kiểm soát tiền mặt và thuốc kiểm soát đặc biệt là rủi ro pháp lý và uy tín.",
            "Rủi ro kiểm tra GPP & thất thoát nhân viên",
            "Quy trình bán hàng & giao ca",
            "Bán hàng đúng quy định GPP, giao ca có đối soát tiền và thuốc.",
            "Mở ca/đóng ca có đối soát — biết chính xác doanh thu và chênh lệch từng ca.",
            "Giảm 80% sai sót vận hành, chủ NT yên tâm khi vắng mặt."),
        ["BUSINESS"] = new(
            "Kinh doanh & tài chính",
            "Chưa theo dõi lãi gộp theo mặt hàng và xu hướng doanh thu — khó biết sản phẩm nào thực sự sinh lời.",
            "Bán nhiều nhưng lãi không tăng tương ứng — thường do chiết khấu, tồn kho và mix sản phẩm chưa tối ưu.",
            "5–12% biên lợi nhuận bị «ăn mòn»",
            "Theo dõi lãi gộp",
            "Biết nhóm thuốc nào có lãi, doanh thu theo tuần — ra quyết định nhập hàng và khuyến mại theo số.",
            "Báo cáo lãi gộp theo tuần — nhận diện mặt hàng kém hiệu quả.",
            "Tăng 8–15% biên lợi nhuận nhờ điều chỉnh mix và giá bán có căn cứ."),
        ["GROWTH"] = new(
            "Phát triển & mở rộng",
            "Có tầm nhìn phát triển nhưng thiếu nền tảng dữ liệu để nhân bản mô hình sang chi nhánh mới.",
            "Mở rộng khi vận hành chưa chuẩn hóa sẽ nhân đôi rủi ro và chi phí quản lý.",
            "Chi phí mở chi nhánh tăng 30–50% nếu không có quy trình chuẩn",
            "Chuẩn bị mở rộng",
            "Quy trình thống nhất, báo cáo tập trung khi có nhiều điểm bán.",
            "Chuẩn hóa quy trình tại cơ sở hiện tại — sẵn sàng checklist mở rộng.",
            "Mở chi nhánh thứ 2 với cùng bộ quy trình — giảm 40% thời gian vận hành ổn định."),
    };

    private static readonly Dictionary<string, string> PainTagLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pain_attract"] = "thu hút khách hàng",
        ["pain_retention"] = "giữ chân khách hàng",
        ["pain_inventory"] = "quản lý kho",
        ["pain_staff"] = "quản lý nhân sự",
        ["pain_revenue"] = "tăng doanh thu",
        ["need_revenue"] = "tăng doanh thu",
        ["need_inventory"] = "giảm tồn kho",
        ["need_crm"] = "giữ chân khách hàng",
        ["need_ops"] = "chuẩn hóa vận hành",
        ["need_time"] = "tiết kiệm thời gian quản lý",
        ["need_telemed"] = "kết nối bác sĩ khám online",
        // PHARMACY_V2
        ["pain_numbers"] = "nắm rõ lãi lỗ",
        ["pain_compliance"] = "sổ sách, sẵn sàng khi kiểm tra",
        ["pain_growth"] = "chuẩn bị mở thêm cơ sở",
        ["need_profit"] = "nắm rõ lãi lỗ",
        ["need_compliance"] = "sổ sách, sẵn sàng khi kiểm tra",
        ["need_other"] = "việc khác",
    };

    private const string TelemedModuleName = "Mạng bác sĩ & khám online";

    public static (KapConsultingBriefDto Brief, KapExecutiveSummaryDto Executive) Build(
        string orgName,
        string? orgScale,
        KapReportScoresDto scores,
        KapMaturityAssessmentDto? maturity,
        IReadOnlyList<KapRootCauseDto> rootCauses,
        IReadOnlyList<KapRiskItemDto> risks,
        KapBenchmarkAnalysisDto? benchmark,
        AssessmentQualitativeTagsDto qualitative,
        IReadOnlyDictionary<string, decimal>? questionScores = null)
    {
        var scaleLabel = ScaleLabel(orgScale);
        var weakCategories = scores.Categories
            .OrderBy(c => c.Score)
            .Take(3)
            .ToList();
        var topWeak = weakCategories.FirstOrDefault();
        var playbook = topWeak is not null && Playbooks.TryGetValue(topWeak.Code, out var pb) ? pb : null;

        var painLabel = TagLabel(qualitative.PainPoint);
        var needLabel = TagLabel(qualitative.PriorityNeed);

        var diagnosisHeadline = playbook is not null
            ? $"{orgName}: Điểm nghẽn chính là «{playbook.DisplayName}» — đây là lý do doanh thu khó tăng dù bạn đang cố gắng"
            : $"{orgName}: Cần chuẩn hóa vận hành trước khi đầu tư marketing — nền tảng hiện tại chưa đủ để mở rộng";

        var impacts = BuildImpacts(weakCategories, rootCauses, risks, painLabel);
        var modules = BuildModuleFits(weakCategories, needLabel, qualitative, questionScores);
        var roi = BuildRoiStory(orgName, orgScale, scores, maturity, weakCategories, modules);
        var costOfInaction = BuildCostOfInaction(scaleLabel, topWeak, playbook, benchmark);
        var urgency = BuildUrgency(scaleLabel, benchmark, maturity);
        var cta = BuildCta(orgName, needLabel, modules);

        var brief = new KapConsultingBriefDto(
            diagnosisHeadline,
            costOfInaction,
            impacts,
            modules,
            roi,
            urgency,
            cta);

        var executive = BuildExecutiveSummary(orgName, scaleLabel, scores, maturity, brief, benchmark, rootCauses);
        return (brief, executive);
    }

    private static List<KapBusinessImpactDto> BuildImpacts(
        IReadOnlyList<AssessmentCategoryScoreDto> weakCategories,
        IReadOnlyList<KapRootCauseDto> rootCauses,
        IReadOnlyList<KapRiskItemDto> risks,
        string? painLabel)
    {
        var impacts = new List<KapBusinessImpactDto>();

        foreach (var cat in weakCategories)
        {
            if (!Playbooks.TryGetValue(cat.Code, out var pb))
                continue;
            impacts.Add(new KapBusinessImpactDto(
                cat.Code,
                $"Hệ quả: {pb.DisplayName} yếu",
                $"{pb.WeakDiagnosis} {pb.BusinessImpact}",
                pb.CostRange));
        }

        foreach (var rc in rootCauses.Take(2))
        {
            impacts.Add(new KapBusinessImpactDto(
                rc.Area,
                rc.Title,
                rc.Body,
                "Ảnh hưởng trực tiếp đến hiệu quả kinh doanh"));
        }

        foreach (var risk in risks.Where(r => string.Equals(r.Level, "high", StringComparison.OrdinalIgnoreCase)).Take(2))
        {
            impacts.Add(new KapBusinessImpactDto(
                risk.Area,
                $"Rủi ro: {risk.Title}",
                risk.Body,
                "Rủi ro pháp lý & uy tín"));
        }

        if (!string.IsNullOrWhiteSpace(painLabel))
        {
            impacts.Insert(0, new KapBusinessImpactDto(
                "QUALITATIVE",
                $"Trở ngại bạn tự nhận diện: {painLabel}",
                $"Đây là điểm nghẽn bạn tự xác nhận trong khảo sát — nếu không xử lý trong 90 ngày, chi phí cơ hội sẽ tích lũy thêm mỗi tháng.",
                "Chi phí cơ hội tăng dần"));
        }

        return impacts.DistinctBy(i => i.Title).Take(5).ToList();
    }

    private static List<KapSoftwareModuleFitDto> BuildModuleFits(
        IReadOnlyList<AssessmentCategoryScoreDto> weakCategories,
        string? needLabel,
        AssessmentQualitativeTagsDto? qualitative = null,
        IReadOnlyDictionary<string, decimal>? questionScores = null)
    {
        var modules = new List<KapSoftwareModuleFitDto>();
        var priority = 95;

        foreach (var cat in weakCategories)
        {
            if (!Playbooks.TryGetValue(cat.Code, out var pb))
                continue;
            modules.Add(new KapSoftwareModuleFitDto(
                pb.ModuleName,
                pb.ModulePain,
                pb.Outcome30,
                pb.Outcome90,
                priority));
            priority -= 10;
        }

        if (WantsTelemedNetwork(qualitative, questionScores)
            && modules.All(m => !m.ModuleName.Contains("bác sĩ", StringComparison.OrdinalIgnoreCase)))
        {
            modules.Insert(0, new KapSoftwareModuleFitDto(
                TelemedModuleName,
                "Chưa liên kết bác sĩ — mất cơ hội tư vấn online và đơn điện tử từ khách.",
                "Trong 30 ngày: danh sách bác sĩ liên kết và quy trình nhận đơn cơ bản.",
                "Trong 90 ngày: khách tư vấn online → đơn về quầy — tăng doanh thu dịch vụ.",
                93));
        }

        if (!string.IsNullOrWhiteSpace(needLabel) && modules.Count > 0)
        {
            var top = modules[0] with
            {
                PainResolved = $"{modules[0].PainResolved} (Phù hợp nhu cầu ưu tiên của bạn: {needLabel})"
            };
            modules[0] = top;
        }

        if (modules.Count == 0)
        {
            modules.Add(new KapSoftwareModuleFitDto(
                "Chuẩn hóa bán hàng & kho",
                "Thay sổ tay và Excel bằng cách ghi chép tập trung — giảm sai sót cuối ngày.",
                "Trong 30 ngày: thống nhất quy trình bán và kiểm kê cơ bản.",
                "Trong 90 ngày: theo dõi doanh thu và tồn theo tuần, sẵn sàng mở rộng.",
                90));
        }

        return modules;
    }

    private static KapRoiStoryDto BuildRoiStory(
        string orgName,
        string? orgScale,
        KapReportScoresDto scores,
        KapMaturityAssessmentDto? maturity,
        IReadOnlyList<AssessmentCategoryScoreDto> weakCategories,
        IReadOnlyList<KapSoftwareModuleFitDto> modules) =>
        KapScaleBasedRoiBuilder.BuildStory(orgName, orgScale, scores, maturity, weakCategories, modules);

    private static string BuildCostOfInaction(
        string scaleLabel,
        AssessmentCategoryScoreDto? topWeak,
        CategoryPlaybook? playbook,
        KapBenchmarkAnalysisDto? benchmark)
    {
        var gap = "";
        if (benchmark?.OverallVsMean is decimal d && d < -0.15m)
            gap = $" Bạn đang chậm hơn nhóm tham chiếu {Math.Abs(d):F1} điểm.";
        var cost = playbook?.CostRange ?? "3–10% hiệu quả kinh doanh/tháng";
        return $"Nếu không hành động trong 90 ngày tới, {scaleLabel} như bạn thường tiếp tục mất khoảng {cost} "
            + $"do {(topWeak?.Name ?? "vận hành")} chưa được số hóa.{gap} "
            + "Đối thủ đã dùng phần mềm sẽ có lợi thế tích lũy — càng trì hoãn, chi phí chuyển đổi càng cao.";
    }

    private static string BuildUrgency(
        string scaleLabel,
        KapBenchmarkAnalysisDto? benchmark,
        KapMaturityAssessmentDto? maturity)
    {
        if (maturity?.Level <= 2)
            return $"Với {KapVietnameseLabels.MaturityLevel(maturity.Level, maturity.Name)}, {scaleLabel} đang ở giai đoạn «dễ chuyển đổi nhất» — "
                + "đầu tư công nghệ bây giờ rẻ hơn sửa hệ thống sau 1–2 năm vận hành sai.";

        if (benchmark?.OverallVsMean is < 0)
            return "Khoảng cách với nhóm tham chiếu đang mở rộng — 6 tháng nữa sẽ khó bắt kịp hơn. "
                + "Khuyến nghị: chạy thử 30 ngày với phân hệ ưu tiên cao nhất, đo KPI trước khi triển khai toàn bộ.";

        return "Thời điểm tốt để chuyển đổi: bạn đã có nền tảng, chỉ cần «khóa» quy trình bằng hệ thống. "
            + "Triển khai trong quý này để kịp mùa cao điểm và tối ưu tồn kho trước cuối năm.";
    }

    private static string BuildCta(string orgName, string? needLabel, IReadOnlyList<KapSoftwareModuleFitDto> modules)
    {
        var module = modules.FirstOrDefault()?.ModuleName ?? "Novixa";
        var need = !string.IsNullOrWhiteSpace(needLabel) ? $" (tập trung {needLabel})" : "";
        return $"Liên hệ đội tư vấn Novixa để nhận gợi ý giải pháp phù hợp với {orgName}{need} "
            + "— đặt lịch demo 30 phút qua hotline hoặc novixa.vn/lien-he, không áp gói sẵn.";
    }

    private static KapExecutiveSummaryDto BuildExecutiveSummary(
        string orgName,
        string scaleLabel,
        KapReportScoresDto scores,
        KapMaturityAssessmentDto? maturity,
        KapConsultingBriefDto brief,
        KapBenchmarkAnalysisDto? benchmark,
        IReadOnlyList<KapRootCauseDto> rootCauses)
    {
        var maturityLabel = maturity is null
            ? "đang hình thành nền tảng vận hành"
            : $"{KapVietnameseLabels.MaturityLevel(maturity.Level, maturity.Name)}";

        var weak = scores.Categories.OrderBy(c => c.Score).Take(2).ToList();
        var strong = scores.Categories.OrderByDescending(c => c.Score).Take(2).ToList();

        var opening = $"Sau khi phân tích toàn bộ dữ liệu khảo sát, AI nhận thấy {orgName} "
            + $"{(scores.OverallPct >= 55 ? "đã có nền tảng kinh doanh khá ổn định" : "đang vận hành ổn định ở quy mô hiện tại")} "
            + $"nhưng {(weak.Any(c => c.Code.Equals("GROWTH", StringComparison.OrdinalIgnoreCase)) || scores.OverallPct < 55 ? "đang thiếu nền tảng quản trị để phát triển bền vững và mở rộng" : "cần củng cố quản trị trước khi mở rộng quy mô")}. "
            + brief.DiagnosisHeadline;

        var analysis = "Phân tích nguyên nhân: ";
        if (weak.Count >= 2)
        {
            analysis += $"Điểm {KapPharmacyLanguage.SimpleName(weak[0].Code, weak[0].Name)} ({weak[0].ScorePct:F0}%) kết hợp với "
                + $"{KapPharmacyLanguage.SimpleName(weak[1].Code, weak[1].Name)} ({weak[1].ScorePct:F0}%) cho thấy doanh nghiệp phụ thuộc nhiều vào kinh nghiệm cá nhân — "
                + "nếu chủ nhà thuốc vắng mặt trên 3 ngày, sai lệch tồn và chậm xử lý đơn hàng sẽ tăng";
        }
        else if (weak.Count > 0)
            analysis += $"điểm thấp ở {KapPharmacyLanguage.SimpleName(weak[0].Code, weak[0].Name)} "
                + "thường do thiếu quy trình và công cụ theo dõi tập trung";
        if (strong.Count > 0)
            analysis += $". Điểm cao ở {KapPharmacyLanguage.SimpleName(strong[0].Code, strong[0].Name)} là lợi thế có thể khai thác khi triển khai số hóa";
        if (rootCauses.Count > 0)
            analysis += $". Nguyên nhân gốc: {KapVietnameseText.Display(rootCauses[0].Title)} — {KapVietnameseText.Display(rootCauses[0].Body)}";
        if (benchmark?.OverallVsMean is decimal delta)
            analysis += delta < 0
                ? $". So với nhóm tham chiếu, khoảng cách {Math.Abs(delta):F1} điểm cho thấy cần ưu tiên đóng gap vận hành"
                : ". So với nhóm tham chiếu, bạn đang ở nhóm trên trung bình — cơ hội tối ưu để dẫn đầu";

        var novixaFit = Math.Clamp(scores.OverallPct + (maturity?.Level <= 2 ? 12 : 8), 35, 95);
        var assessment = $"Đánh giá: mức trưởng thành {maturityLabel}; "
            + $"sẵn sàng chuyển đổi số khoảng {scores.OverallPct:F0}%; "
            + $"mức phù hợp triển khai Novixa giai đoạn 1 khoảng {novixaFit:F0}%. "
            + $"Khả năng mở rộng {(scores.OverallPct >= 55 ? "khả thi nếu chuẩn hóa quy trình trước" : "cần củng cố nền tảng 60–90 ngày")}.";

        var conclusion = $"Qua phân tích, {orgName} "
            + $"{(scores.OverallPct >= 55 ? "có nền tảng kinh doanh tốt nhưng thiếu hệ thống quản trị để tăng trưởng bền vững" : "cần ưu tiên chuẩn hóa vận hành trước khi đầu tư mở rộng")}. "
            + $"Nhận định tổng thể: nên hành động trong 90 ngày tới — trì hoãn sẽ làm khoảng cách với nhóm dẫn đầu ngành mở rộng.";

        var primaryAction = brief.ModuleFits.FirstOrDefault()?.ModuleName ?? "chuẩn hóa vận hành";
        var recommendations = $"Khuyến nghị: (1) Làm ngay — tập trung «{primaryAction}» trong 30 ngày đầu; "
            + $"(2) Làm sau 60–90 ngày — củng cố {KapPharmacyLanguage.SimpleName(weak.FirstOrDefault()?.Code ?? "OPERATIONS")} và đo KPI; "
            + "(3) Định hướng chuyển đổi số — triển khai theo giai đoạn, không dồn một lần; "
            + "liên hệ tư vấn để nhận lộ trình chi tiết phù hợp quy mô.";

        var paragraphs = new List<string> { opening, analysis, assessment, conclusion, recommendations };

        var headline = $"{orgName} — Báo cáo tư vấn chuyển đổi số";
        return new KapExecutiveSummaryDto(
            headline, paragraphs, "consulting_v3",
            opening, analysis, assessment, conclusion, recommendations);
    }

    private static string ScaleLabel(string? orgScale) => orgScale?.Trim().ToLowerInvariant() switch
    {
        "micro" => "Quầy thuốc cá nhân",
        "small" => "Nhà thuốc nhỏ",
        "medium" => "Nhà thuốc vừa",
        "large" => "Nhà thuốc lớn",
        "chain" => "Chuỗi nhà thuốc",
        _ => "Nhà thuốc",
    };

    private static string? TagLabel(string? tag) =>
        tag is null ? null : PainTagLabels.GetValueOrDefault(tag, tag.Replace("pain_", "").Replace("need_", "").Replace('_', ' '));

    private static bool WantsTelemedNetwork(
        AssessmentQualitativeTagsDto? qualitative,
        IReadOnlyDictionary<string, decimal>? questionScores) =>
        string.Equals(qualitative?.PriorityNeed, "need_telemed", StringComparison.OrdinalIgnoreCase)
        || (questionScores?.GetValueOrDefault("C6", 4m) ?? 4m) <= 2m;
}
