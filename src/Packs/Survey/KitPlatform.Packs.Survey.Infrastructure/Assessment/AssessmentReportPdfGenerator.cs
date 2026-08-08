using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using KitPlatform.Packs.Survey;

namespace KitPlatform.Packs.Survey.Infrastructure;

internal static class AssessmentReportPdfGenerator
{
    private static readonly Color Brand = Colors.Teal.Darken2;
    private static readonly Color BrandLight = Colors.Teal.Lighten4;
    private static readonly Color Accent = Colors.Orange.Medium;
    private static readonly Color TextMuted = Colors.Grey.Darken1;

    /// <summary>Scale PDF typography for print/mobile readability.</summary>
    private static float Fs(float pt) => pt * 1.25f;

    static AssessmentReportPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(
        AssessmentFullReportDto report,
        string orgName,
        KapReportPdfKind kind = KapReportPdfKind.Consulting) =>
        report.Intelligence is not null
            ? GenerateIntelligenceReport(report, report.Intelligence, orgName, kind)
            : GenerateLegacy(report, orgName);

    private static byte[] GenerateIntelligenceReport(
        AssessmentFullReportDto report,
        AssessmentReportIntelligenceDto intel,
        string orgName,
        KapReportPdfKind kind)
    {
        intel = KapReportIntelligenceEnricher.Enrich(report, intel, orgName, report.OrgScale);

        return Document.Create(container =>
        {
            switch (kind)
            {
                case KapReportPdfKind.Executive:
                    BuildExecutivePdf(container, report, intel, orgName);
                    break;
                case KapReportPdfKind.Appendix:
                    BuildAppendixPdf(container, report, intel, orgName);
                    break;
                case KapReportPdfKind.Owner:
                    BuildOwnerPdf(container, report, intel, orgName);
                    break;
                default:
                    BuildConsultingPdf(container, report, intel, orgName);
                    break;
            }
        }).GeneratePdf();
    }

    private static void BuildExecutivePdf(
        IDocumentContainer container,
        AssessmentFullReportDto report,
        AssessmentReportIntelligenceDto intel,
        string orgName)
    {
        // Trang 1: Bìa điều hành
        AddPage(container, orgName, report, KapReportPdfKind.Executive, col =>
        {
            col.Item().PaddingTop(40).Text(orgName).Bold().FontSize(Fs(22)).FontColor(Brand);
            col.Item().PaddingTop(8).Text("BÁO CÁO ĐIỀU HÀNH — QUYẾT ĐỊNH ĐẦU TƯ").Bold().FontSize(Fs(14));
            col.Item().PaddingTop(4).Text($"Ngày: {report.CompletedAt?.ToString("dd/MM/yyyy") ?? "—"}").FontSize(Fs(10)).FontColor(TextMuted);
            col.Item().PaddingTop(16).Text("Dành cho ban lãnh đạo — tóm tắt kết luận, rủi ro, lợi ích đầu tư và đề xuất hành động.")
                .FontSize(Fs(9)).FontColor(TextMuted);
        });

        AddSectionPage(container, orgName, report, KapReportPdfKind.Executive, "1. TÓM TẮT ĐIỀU HÀNH",
            col => RenderExecutiveDashboard(col, intel, report, includeAiLine: true));

        AddSectionPage(container, orgName, report, KapReportPdfKind.Executive, "2. CHẨN ĐOÁN & PHÂN TÍCH CHUYÊN SÂU",
            col => RenderDeepDiagnosis(col, report, intel, maxInsights: 4));

        AddSectionPageIf(intel.NovixaReadiness is not null, container, orgName, report, KapReportPdfKind.Executive,
            "3. MỨC SẴN SÀNG TRIỂN KHAI NOVIXA",
            col => RenderNovixaReadiness(col, intel.NovixaReadiness));

        AddSectionPageIf(HasRoiContent(intel), container, orgName, report, KapReportPdfKind.Executive,
            "4. PHÂN TÍCH LỢI ÍCH KHI TRIỂN KHAI",
            col =>
            {
                RenderRoiMetrics(col, intel.RoiMetrics ?? []);
                RenderRoiBeforeAfter(col, intel.ConsultingBrief?.RoiStory);
                RenderCostBenefit(col, intel.CostBenefit, compact: true);
            });

        AddSectionPageIf(HasImpactOrInvestment(intel), container, orgName, report, KapReportPdfKind.Executive,
            "5. DỰ BÁO TÁC ĐỘNG & ĐỀ XUẤT ĐẦU TƯ",
            col =>
            {
                RenderBusinessImpactForecast(col, intel.BusinessImpactForecast ?? []);
                RenderInvestmentPhases(col, intel.InvestmentPhases ?? []);
            });

        AddSectionPage(container, orgName, report, KapReportPdfKind.Executive, "6. RỦI RO & HÀNH ĐỘNG TIẾP THEO",
            col =>
            {
                RenderRiskRegister(col, intel.RiskRegister, maxItems: 5);
                RenderClosingCta(col, intel);
            });
    }

    private static void BuildOwnerPdf(
        IDocumentContainer container,
        AssessmentFullReportDto report,
        AssessmentReportIntelligenceDto intel,
        string orgName)
    {
        var pack = intel.OwnerPack ?? KapOwnerPackBuilder.Build(report, intel);

        AddPage(container, orgName, report, KapReportPdfKind.Owner, col =>
        {
            col.Item().PaddingTop(36).Text("ĐÁNH GIÁ NHANH NHÀ THUỐC").Bold().FontSize(Fs(18)).FontColor(Brand);
            col.Item().PaddingTop(6).Text(orgName).Bold().FontSize(Fs(16));
            col.Item().PaddingTop(6)
                .Text($"Ngày: {report.CompletedAt?.ToString("dd/MM/yyyy") ?? "—"} · Mã: {report.SubmissionId.ToString()[..8].ToUpperInvariant()}")
                .FontSize(Fs(9)).FontColor(TextMuted);
            col.Item().PaddingTop(14)
                .Text("Báo cáo giúp anh/chị nhìn rõ điểm mạnh và việc nên ưu tiên — không phải tài liệu kỹ thuật.")
                .FontSize(Fs(10)).FontColor(TextMuted).LineHeight(1.35f);
        });

        AddSectionPage(container, orgName, report, KapReportPdfKind.Owner, "KẾT QUẢ TÓM TẮT", col =>
        {
            col.Item().Background(BrandLight).Padding(12).Column(box =>
            {
                box.Item().Text($"Mức tổng: {Math.Round(pack.OverallScorePct):0}/100 · {pack.MaturityLabel}")
                    .Bold().FontSize(Fs(12)).FontColor(Brand);
                box.Item().PaddingTop(4).Text(V(pack.OverallHeadline)).FontSize(Fs(10)).LineHeight(1.35f);
            });

            col.Item().PaddingTop(12).Text("ĐANG LÀM TỐT").Bold().FontSize(Fs(11)).FontColor(Brand);
            foreach (var s in pack.Strengths.Take(3))
            {
                col.Item().PaddingTop(4).Text($"✓ {V(s.Title)}").Bold().FontSize(Fs(10));
                col.Item().PaddingTop(1).Text(V(s.Body)).FontSize(Fs(9)).FontColor(TextMuted).LineHeight(1.3f);
            }

            col.Item().PaddingTop(12).Text("NỖI ĐAU / CƠ HỘI LỚN").Bold().FontSize(Fs(11)).FontColor(Accent);
            var i = 1;
            foreach (var p in pack.Pains.Take(3))
            {
                col.Item().PaddingTop(6).Text($"{i:00}. {V(p.Title)}").Bold().FontSize(Fs(10));
                col.Item().PaddingTop(1).Text($"→ {V(p.BusinessConsequence)}").FontSize(Fs(9)).LineHeight(1.35f);
                i++;
            }

            col.Item().PaddingTop(12).Background(Colors.Orange.Lighten4).Padding(10).Column(box =>
            {
                box.Item().Text("NÊN LÀM TRƯỚC (30 ngày)").Bold().FontSize(Fs(10));
                box.Item().PaddingTop(3).Text(V(pack.OneThingFirst)).FontSize(Fs(10)).LineHeight(1.35f);
            });
        });

        AddSectionPage(container, orgName, report, KapReportPdfKind.Owner, "SÁU GÓC NHÌN ĐƠN GIẢN", col =>
        {
            col.Item().Text("Mỗi dòng là một mặt hoạt động — không cần biết thuật ngữ kỹ thuật.")
                .FontSize(Fs(9)).FontColor(TextMuted);
            RenderPharmacyScoreBars(col, report.CategoryScores);
        });

        AddSectionPage(container, orgName, report, KapReportPdfKind.Owner, "VIỆC LÀM TRONG 30 NGÀY", col =>
        {
            if (pack.Actions30Days.Count == 0)
            {
                col.Item().Text(V(pack.OneThingFirst)).FontSize(Fs(10));
                return;
            }

            foreach (var a in pack.Actions30Days)
            {
                col.Item().PaddingTop(8).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                {
                    c.Item().Text(V(a.Title)).Bold().FontSize(Fs(10));
                    c.Item().PaddingTop(3).Text($"Ai làm: {V(a.Who)}").FontSize(Fs(9));
                    c.Item().Text($"Khi nào: {V(a.When)}").FontSize(Fs(9));
                    c.Item().Text($"Xong khi: {V(a.DoneWhen)}").FontSize(Fs(9)).FontColor(TextMuted);
                });
            }
        });

        AddSectionPage(container, orgName, report, KapReportPdfKind.Owner, "NẾU MUỐN CÓ NGƯỜI ĐỒNG HÀNH", col =>
        {
            col.Item().Text(V(pack.PilotHinge.HowToTalk)).FontSize(Fs(10)).LineHeight(1.4f);
            col.Item().PaddingTop(10).Text("Hướng đồng hành phù hợp (Pilot 30 ngày):")
                .Bold().FontSize(Fs(10)).FontColor(Brand);
            col.Item().PaddingTop(4).Text(V(pack.PilotHinge.RecommendedFocus)).Bold().FontSize(Fs(11));

            col.Item().PaddingTop(8).Text("Có thể chọn một trong:").FontSize(Fs(9)).FontColor(TextMuted);
            foreach (var opt in pack.PilotHinge.FocusOptions)
                col.Item().PaddingTop(3).Text($"• {V(opt)}").FontSize(Fs(9));

            col.Item().PaddingTop(12).Background(BrandLight).Padding(10).Column(box =>
            {
                box.Item().Text("Pilot không phải dùng thử phần mềm vô thời hạn.")
                    .Bold().FontSize(Fs(9));
                box.Item().PaddingTop(3)
                    .Text("Pilot = kiểm chứng 1–2 việc trên trong 30 ngày. Phí Pilot thường tính vào tháng đầu nếu anh/chị tiếp tục.")
                    .FontSize(Fs(9)).LineHeight(1.35f);
            });

            col.Item().PaddingTop(14).AlignCenter().Text(V(pack.NextStepCta))
                .Bold().FontSize(Fs(11)).FontColor(Brand);
        });
    }

    private static void BuildConsultingPdf(
        IDocumentContainer container,
        AssessmentFullReportDto report,
        AssessmentReportIntelligenceDto intel,
        string orgName)
    {
        AddPage(container, orgName, report, KapReportPdfKind.Consulting, col =>
        {
            col.Item().PaddingTop(36).Text(orgName).Bold().FontSize(Fs(24)).FontColor(Brand);
            col.Item().PaddingTop(6).Text("BÁO CÁO TƯ VẤN CHUYỂN ĐỔI SỐ BẰNG AI").Bold().FontSize(Fs(16));
            col.Item().PaddingTop(4).Text("Phân tích chuyên sâu · Lộ trình triển khai · Đề xuất giải pháp Novixa")
                .FontSize(Fs(10)).FontColor(TextMuted);
            if (intel.Maturity is not null)
                col.Item().PaddingTop(16).Text(KapVietnameseLabels.MaturityLevel(intel.Maturity.Level, intel.Maturity.Name))
                    .FontSize(Fs(11)).FontColor(Brand);
            col.Item().PaddingTop(8).Text($"Ngày hoàn thành: {report.CompletedAt?.ToString("dd/MM/yyyy") ?? "—"}")
                .FontSize(Fs(9)).FontColor(TextMuted);
        });

        AddSectionPage(container, orgName, report, KapReportPdfKind.Consulting, "MỤC LỤC",
            col => RenderTableOfContents(col, fullSpecMode: true));

        AddSectionPage(container, orgName, report, KapReportPdfKind.Consulting, "1. TÓM TẮT ĐIỀU HÀNH",
            col => RenderStructuredExecutiveSummary(col, intel));

        AddSectionPage(container, orgName, report, KapReportPdfKind.Consulting, "2. BẢNG ĐIỀU HÀNH",
            col => RenderExecutiveDashboard(col, intel, report, includeAiLine: true));

        AddSectionPage(container, orgName, report, KapReportPdfKind.Consulting, "3. PHÂN TÍCH AI",
            col =>
            {
                RenderCrossCategoryInsight(col, intel.CrossCategoryInsight);
                RenderDeepDiagnosis(col, report, intel, maxInsights: 8);
            });

        AddSectionPage(container, orgName, report, KapReportPdfKind.Consulting, "4. ĐIỂM NĂNG LỰC & BIỂU ĐỒ",
            col =>
            {
                RenderMaturityOverview(col, report, intel);
                col.Item().PaddingTop(10);
                RenderPharmacyScoreBars(col, report.CategoryScores);
                col.Item().PaddingTop(10);
                RenderScoreHeatmap(col, report.CategoryScores);
                col.Item().PaddingTop(10);
                RenderQuestPdfScoreBars(col, report.CategoryScores, intel.Benchmark);
                RenderChartScoreTable(col, report.CategoryScores, intel.Benchmark);
            });

        AddSectionPageIf(intel.Benchmark?.Categories.Count > 0, container, orgName, report, KapReportPdfKind.Consulting,
            "5. SO SÁNH NGÀNH (BENCHMARK)",
            col =>
            {
                RenderBenchmark(col, intel.Benchmark);
                RenderBenchmarkTiers(col, intel.Benchmark);
            });

        AddSectionPageIf(intel.TransformationReadiness is not null, container, orgName, report, KapReportPdfKind.Consulting,
            "5B. ĐIỂM CHUYỂN ĐỔI",
            col => RenderTransformationReadiness(col, intel.TransformationReadiness));

        AddSectionPageIf(intel.NovixaReadiness is not null, container, orgName, report, KapReportPdfKind.Consulting,
            "6. MỨC SẴN SÀNG TRIỂN KHAI NOVIXA",
            col => RenderNovixaReadiness(col, intel.NovixaReadiness));

        AddSectionPageIf(HasRoiContent(intel), container, orgName, report, KapReportPdfKind.Consulting,
            "7. PHÂN TÍCH LỢI ÍCH KHI TRIỂN KHAI NOVIXA",
            col =>
            {
                RenderRoiMetrics(col, intel.RoiMetrics ?? []);
                RenderRoiBeforeAfter(col, intel.ConsultingBrief?.RoiStory);
                RenderCostBenefit(col, intel.CostBenefit, compact: false);
            });

        AddSectionPageIf(HasImpactContent(intel) || intel.Opportunities.Count > 0, container, orgName, report,
            KapReportPdfKind.Consulting, "8. TÁC ĐỘNG KINH DOANH & CƠ HỘI",
            col =>
            {
                RenderNarrativeCascade(col, intel.InactionCascade, "Nếu không hành động");
                if (intel.ConsultingBrief?.BusinessImpacts.Count > 0)
                    RenderConsultingBriefImpacts(col, intel.ConsultingBrief);
                else if (intel.BusinessImpactForecast?.Count > 0)
                    RenderBusinessImpactForecast(col, intel.BusinessImpactForecast);
                RenderOpportunities(col, intel.Opportunities);
            });

        AddSectionPageIf(HasModuleContent(intel), container, orgName, report, KapReportPdfKind.Consulting,
            "9. ĐỀ XUẤT PHÂN HỆ NOVIXA",
            col =>
            {
                RenderModuleStars(col, intel.ModuleRecommendations ?? []);
                if (intel.ConsultingBrief?.ModuleFits.Count > 0)
                    RenderModuleFits(col, intel.ConsultingBrief.ModuleFits);
            });

        AddSectionPageIf(intel.WhyNovixa?.Rows.Count > 0, container, orgName, report, KapReportPdfKind.Consulting,
            "9B. TẠI SAO NOVIXA?",
            col => RenderWhyNovixa(col, intel.WhyNovixa));

        AddSectionPageIf(HasGapOrMapping(intel), container, orgName, report, KapReportPdfKind.Consulting,
            "10. PHÂN TÍCH KHOẢNG CÁCH & GHÉP PHÂN HỆ",
            col =>
            {
                RenderGapAnalysis(col, intel.GapAnalysis);
                RenderModuleMappings(col, intel.ModuleMappings ?? []);
            });

        AddSectionPageIf(HasRootCauseOrRisk(intel), container, orgName, report, KapReportPdfKind.Consulting,
            "11. NGUYÊN NHÂN GỐC & RỦI RO",
            col =>
            {
                RenderRootCauses(col, intel.RootCauses);
                RenderRiskRegister(col, intel.RiskRegister, maxItems: 8);
                RenderRisks(col, intel.Risks);
            });

        AddSectionPageIf(intel.Swot is not null, container, orgName, report, KapReportPdfKind.Consulting,
            "12. ĐIỂM MẠNH — ĐIỂM YẾU — CƠ HỘI — RỦI RO",
            col => RenderSwot(col, intel.Swot));

        AddSectionPageIf(intel.PriorityMatrix is not null, container, orgName, report, KapReportPdfKind.Consulting,
            "13. MA TRẬN ƯU TIÊN",
            col =>
            {
                RenderPriorityMatrixVisual(col, intel.PriorityMatrix);
                RenderPriorityMatrix(col, intel.PriorityMatrix);
            });

        AddSectionPageIf(HasRoadmapContent(intel), container, orgName, report, KapReportPdfKind.Consulting,
            "14. LỘ TRÌNH CHUYỂN ĐỔI SỐ",
            col =>
            {
                RenderNarrativeCascade(col, intel.ImplementationJourney, "Nếu triển khai");
                RenderTransformationRoadmap(col, intel.TransformationRoadmap);
                RenderRoadmap(col, intel.Roadmap, include180: true);
                RenderInvestmentPhases(col, intel.InvestmentPhases ?? []);
                if (intel.ImplementationTimeline?.Milestones.Count > 0)
                    RenderImplementationTimeline(col, intel.ImplementationTimeline);
            });

        AddSectionPageIf(intel.ActionPlan?.Items.Count > 0 || report.Recommendations.Count > 0, container, orgName,
            report, KapReportPdfKind.Consulting, "15. KẾ HOẠCH HÀNH ĐỘNG",
            col => RenderActionPlan(col, intel.ActionPlan, report.Recommendations));

        AddSectionPageIf(intel.Kpis.Count > 0, container, orgName, report, KapReportPdfKind.Consulting,
            "16. KPI KHUYẾN NGHỊ",
            col => RenderKpis(col, intel.Kpis));

        AddSectionPage(container, orgName, report, KapReportPdfKind.Consulting, "17. KẾT LUẬN ĐIỀU HÀNH AI",
            col => RenderAiExecutiveConclusion(col, intel));

        AddSectionPage(container, orgName, report, KapReportPdfKind.Consulting, "18. LIÊN HỆ TƯ VẤN",
            col => RenderClosingSection(col, intel));
    }

    private static void AddSectionPage(
        IDocumentContainer container,
        string orgName,
        AssessmentFullReportDto report,
        KapReportPdfKind kind,
        string sectionTitle,
        Action<ColumnDescriptor> content)
    {
        AddPage(container, orgName, report, kind, col =>
        {
            col.Item().Text(sectionTitle).Bold().FontSize(Fs(14)).FontColor(Brand);
            col.Item().PaddingTop(8);
            content(col);
        });
    }

    private static void AddSectionPageIf(
        bool condition,
        IDocumentContainer container,
        string orgName,
        AssessmentFullReportDto report,
        KapReportPdfKind kind,
        string sectionTitle,
        Action<ColumnDescriptor> content)
    {
        if (!condition) return;
        AddSectionPage(container, orgName, report, kind, sectionTitle, content);
    }

    private static bool HasRoiContent(AssessmentReportIntelligenceDto intel) =>
        (intel.RoiMetrics?.Count ?? 0) > 0
        || intel.CostBenefit is not null
        || intel.ConsultingBrief?.RoiStory is not null;

    private static bool HasImpactContent(AssessmentReportIntelligenceDto intel) =>
        (intel.BusinessImpactForecast?.Count ?? 0) > 0
        || (intel.ConsultingBrief?.BusinessImpacts.Count ?? 0) > 0;

    private static bool HasImpactOrInvestment(AssessmentReportIntelligenceDto intel) =>
        HasImpactContent(intel) || (intel.InvestmentPhases?.Count ?? 0) > 0;

    private static bool HasModuleContent(AssessmentReportIntelligenceDto intel) =>
        (intel.ModuleRecommendations?.Count ?? 0) > 0
        || (intel.ConsultingBrief?.ModuleFits.Count ?? 0) > 0;

    private static bool HasGapOrMapping(AssessmentReportIntelligenceDto intel) =>
        intel.GapAnalysis?.Items.Count > 0
        || (intel.ModuleMappings?.Count ?? 0) > 0;

    private static bool HasRootCauseOrRisk(AssessmentReportIntelligenceDto intel) =>
        intel.RootCauses.Count > 0 || intel.RiskRegister?.Items.Count > 0;

    private static bool HasQualitative(AssessmentFullReportDto report) =>
        !string.IsNullOrWhiteSpace(report.QualitativeTags.PainPoint)
        || !string.IsNullOrWhiteSpace(report.QualitativeTags.PriorityNeed);

    private static bool HasRoadmapContent(AssessmentReportIntelligenceDto intel) =>
        intel.TransformationRoadmap?.Phases.Count > 0
        || intel.ImplementationTimeline?.Milestones.Count > 0
        || HasRoadmapItems(intel.Roadmap);

    private static bool HasRoadmapItems(KapRoadmapDto? roadmap) =>
        (roadmap?.Days30.Count ?? 0) > 0
        || (roadmap?.Days60.Count ?? 0) > 0
        || (roadmap?.Days90.Count ?? 0) > 0;

    private static bool HasPlanContent(AssessmentReportIntelligenceDto intel, AssessmentFullReportDto report) =>
        (intel.Roadmap?.Days180.Count ?? 0) > 0
        || intel.Kpis.Count > 0
        || report.Recommendations.Count > 0
        || (intel.InvestmentPhases?.Count ?? 0) > 0;

    private static void BuildAppendixPdf(
        IDocumentContainer container,
        AssessmentFullReportDto report,
        AssessmentReportIntelligenceDto intel,
        string orgName)
    {
        if (intel.Appendix?.Questions.Count > 0)
        {
            AddPage(container, orgName, report, KapReportPdfKind.Appendix, col =>
            {
                col.Item().Text("PHỤ LỤC — CHI TIẾT KHẢO SÁT").Bold().FontSize(Fs(14)).FontColor(Brand);
                col.Item().PaddingTop(4).Text(
                    "Toàn bộ câu hỏi, câu trả lời và điểm số — dùng để đối chiếu và làm căn cứ triển khai.")
                    .FontSize(Fs(9)).FontColor(TextMuted);
                col.Item().PaddingTop(8).Table(RenderAppendixTable(intel.Appendix));
            }, margin: 28, fontSize: 8);
        }
        else
        {
            AddPage(container, orgName, report, KapReportPdfKind.Appendix, col =>
            {
                col.Item().Text("Phụ lục chưa có dữ liệu khảo sát.").FontColor(TextMuted);
            });
        }

        AddPage(container, orgName, report, KapReportPdfKind.Appendix, col =>
        {
            col.Item().Text("ĐIỂM THEO NHÓM & CHIỀU").Bold().FontSize(Fs(12)).FontColor(Brand);
            RenderCategoryScores(col, report.CategoryScores);
            if (report.DimensionScores.Count > 0)
            {
                col.Item().PaddingTop(10).Text("Chi tiết theo chiều năng lực").Bold().FontSize(Fs(11));
                foreach (var dim in report.DimensionScores.OrderByDescending(d => d.Score))
                    col.Item().PaddingTop(2).Text($"• {dim.Name}: {dim.Score:F2}/4 ({dim.ScorePct:F1}%)");
            }
        });
    }

    private static void AddPage(
        IDocumentContainer container,
        string orgName,
        AssessmentFullReportDto report,
        KapReportPdfKind kind,
        Action<ColumnDescriptor> content,
        float margin = 36,
        float fontSize = 12)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(margin);
            page.DefaultTextStyle(x => x.FontSize(fontSize).FontColor(Colors.Grey.Darken3));
            page.Header().Element(c => RenderHeader(c, orgName, report, kind));
            page.Content().PaddingVertical(8).Column(content);
            page.Footer().AlignCenter().Text(t =>
            {
                t.Span($"{KapVietnameseLabels.FooterLine} · ").FontSize(Fs(8)).FontColor(Colors.Grey.Medium);
                t.Span(orgName).FontSize(Fs(8)).FontColor(Colors.Grey.Medium);
                t.Span(" · Trang ").FontSize(Fs(8)).FontColor(Colors.Grey.Medium);
                t.CurrentPageNumber().FontSize(Fs(8)).FontColor(Colors.Grey.Medium);
            });
        });
    }

    private static void RenderHeader(IContainer container, string orgName, AssessmentFullReportDto report, KapReportPdfKind kind)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(KapVietnameseLabels.PlatformTitle).FontSize(Fs(8)).Bold().FontColor(Brand);
                    left.Item().Text(KapVietnameseLabels.ReportKindTitle(kind)).FontSize(Fs(11)).Bold().FontColor(Brand);
                    left.Item().Text(orgName).FontSize(Fs(13)).Bold();
                });
                row.ConstantItem(140).AlignRight().Column(right =>
                {
                    right.Item().Text(report.TemplateCode).FontSize(Fs(8)).FontColor(TextMuted);
                    right.Item().Text(report.CompletedAt?.ToString("dd/MM/yyyy") ?? "—").FontSize(Fs(8)).FontColor(TextMuted);
                });
            });
            col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    private static void RenderTableOfContents(ColumnDescriptor col, bool extended = false, bool pharmacyMode = false, bool fullSpecMode = false)
    {
        string[] sections = fullSpecMode
            ?
            [
                "1. Tóm tắt điều hành", "2. Bảng điều hành", "3. Phân tích AI",
                "4. Điểm năng lực & biểu đồ", "5. So sánh ngành (benchmark)",
                "6. Mức sẵn sàng triển khai Novixa", "7. Phân tích lợi ích đầu tư (ước lượng AI)",
                "8. Tác động kinh doanh & cơ hội", "9. Đề xuất phân hệ Novixa",
                "10. Phân tích khoảng cách & ghép phân hệ", "11. Nguyên nhân gốc & rủi ro",
                "12. Điểm mạnh — điểm yếu — cơ hội — rủi ro", "13. Ma trận ưu tiên", "14. Lộ trình chuyển đổi số",
                "15. Kế hoạch hành động", "16. Chỉ số KPI khuyến nghị", "17. Kết luận điều hành AI",
                "18. Liên hệ tư vấn",
            ]
            : pharmacyMode
            ?
            [
                "1. Tóm tắt", "2. Nhận xét & gợi ý cải thiện", "3. Đánh giá nhanh — thang 10 điểm",
                "4. Hệ quả kinh doanh nếu không cải thiện", "5. Bảng điểm theo từng mặt",
                "6. Rủi ro cần lưu ý", "7. Điểm mạnh — điểm yếu & nhu cầu",
                "8. Lộ trình gợi ý 30–90 ngày", "9. Việc nên làm tiếp theo", "10. Kết luận & liên hệ tư vấn",
            ]
            : extended
            ?
            [
                "1. Tóm tắt điều hành", "2. Chẩn đoán chuyên sâu", "3. Điểm năng lực",
                "4. Mức sẵn sàng Novixa", "5. Lợi ích đầu tư & chi phí — lợi ích",
                "6. Tác động & hệ quả không hành động", "7. Đề xuất phân hệ",
                "8. Phân tích khoảng cách & ghép phân hệ", "9. Biểu đồ & bảng số liệu",
                "10. Nguyên nhân gốc & rủi ro", "11. Phân tích SWOT", "12. Ma trận ưu tiên",
                "13. Lộ trình triển khai", "14. Kế hoạch 180 ngày & khuyến nghị", "15. Kết luận",
            ]
            :
            [
                "1. Tóm tắt điều hành",
                "2. Chẩn đoán chuyên sâu",
                "3. Lợi ích đầu tư & tác động kinh doanh",
                "4. Đề xuất phân hệ & phân tích khoảng cách",
                "5. Biểu đồ & tham chiếu",
                "6. Rủi ro & phân tích SWOT",
                "7. Lộ trình & hành động tiếp theo",
            ];
        foreach (var s in sections)
            col.Item().PaddingTop(3).Text(s).FontSize(Fs(9));
    }

    private static void RenderMaturityOverview(
        ColumnDescriptor col,
        AssessmentFullReportDto report,
        AssessmentReportIntelligenceDto intel)
    {
        col.Item().Row(row =>
        {
            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(c =>
            {
                c.Item().Text("Điểm tổng").FontSize(Fs(9)).FontColor(TextMuted);
                c.Item().Text($"{report.OverallPct:F0}/100").Bold().FontSize(Fs(22)).FontColor(Brand);
                c.Item().Text($"{report.OverallScore:F2}/4").FontSize(Fs(9));
            });
            row.ConstantItem(12);
            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(c =>
            {
                if (intel.Maturity is null) return;
                c.Item().Text("Mức trưởng thành").FontSize(Fs(9)).FontColor(TextMuted);
                c.Item().Text(KapVietnameseLabels.MaturityLevel(intel.Maturity.Level, intel.Maturity.Name)).Bold().FontSize(Fs(11));
                c.Item().Text(intel.Maturity.Description).FontSize(Fs(8)).FontColor(TextMuted);
            });
        });
    }

    private static void RenderExecutiveDashboard(
        ColumnDescriptor col,
        AssessmentReportIntelligenceDto intel,
        AssessmentFullReportDto report,
        bool includeAiLine = false)
    {
        var dash = intel.ExecutiveDashboard;
        if (dash is null)
        {
            col.Item().PaddingTop(8).Text($"Điểm tổng: {report.OverallPct:F0}/100").Bold().FontSize(Fs(12));
            return;
        }

        if (includeAiLine)
        {
            col.Item().PaddingTop(4).Background(BrandLight).Padding(12).Column(box =>
            {
                if (intel.AiNarrative is not null)
                    box.Item().AlignRight().Text("Phân tích AI").FontSize(Fs(8)).Bold().FontColor("#4f46e5");
                box.Item().PaddingTop(4).Text(dash.AiAssessmentLine).LineHeight(1.35f).FontSize(Fs(10));
            });
        }

        col.Item().PaddingTop(10).Row(row =>
        {
            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
            {
                c.Item().Text("Sẵn sàng chuyển đổi số").FontSize(Fs(8)).FontColor(TextMuted);
                c.Item().Text($"{dash.DigitalReadinessPct:F0}%").Bold().FontSize(Fs(20)).FontColor(Brand);
            });
            row.ConstantItem(8);
            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
            {
                c.Item().Text("Phù hợp Novixa").FontSize(Fs(8)).FontColor(TextMuted);
                c.Item().Text($"{dash.NovixaFitPct:F0}%").Bold().FontSize(Fs(20)).FontColor(Brand);
            });
            row.ConstantItem(8);
            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
            {
                c.Item().Text("Điểm tổng").FontSize(Fs(8)).FontColor(TextMuted);
                c.Item().Text($"{report.OverallPct:F0}/100").Bold().FontSize(Fs(20)).FontColor(Brand);
                if (intel.Maturity is not null)
                    c.Item().Text(KapVietnameseLabels.MaturityLevel(intel.Maturity.Level, intel.Maturity.Name)).FontSize(Fs(8));
            });
        });

        RenderBulletBlock(col, "3 vấn đề lớn nhất", dash.TopProblems);
        RenderBulletBlock(col, "3 rủi ro lớn nhất", dash.TopRisks);
        RenderBulletBlock(col, "3 cơ hội lớn nhất", dash.TopOpportunities);
    }

    private static void RenderDeepDiagnosis(
        ColumnDescriptor col,
        AssessmentFullReportDto report,
        AssessmentReportIntelligenceDto intel,
        int maxInsights)
    {
        if (intel.ConsultingBrief is not null)
        {
            col.Item().Background(BrandLight).Padding(12).Column(box =>
            {
                box.Item().Text(V(intel.ConsultingBrief.DiagnosisHeadline)).Bold().FontSize(Fs(11)).FontColor(Colors.Red.Darken2);
                box.Item().PaddingTop(4).Text(V(intel.ConsultingBrief.CostOfInaction)).LineHeight(1.35f).FontSize(Fs(9));
            });
        }

        var insights = KapConsultingExtensionsBuilder.BuildCategoryInsights(report.CategoryScores, intel.Benchmark);
        if (insights.Count > 0)
        {
            col.Item().PaddingTop(10).Text("Nhận xét theo từng mặt hoạt động").Bold().FontSize(Fs(11)).FontColor(Brand);
            foreach (var insight in insights.Take(maxInsights))
            {
                col.Item().PaddingTop(6).BorderLeft(3).BorderColor(Brand).PaddingLeft(8).Column(c =>
                {
                    c.Item().Text($"{V(insight.CategoryName)} — {V(insight.ScoreLabel)}").Bold().FontSize(Fs(9));
                    c.Item().PaddingTop(2).Text(V(insight.Assessment)).FontSize(Fs(8)).LineHeight(1.35f);
                    c.Item().PaddingTop(2).Text(V(insight.Recommendation)).FontSize(Fs(8)).FontColor(TextMuted).LineHeight(1.35f);
                });
            }
        }

        var dimInsights = KapDimensionInsightsBuilder.Build(report.DimensionScores, report.CategoryScores);
        if (dimInsights.Count > 0)
        {
            col.Item().PaddingTop(10).Text("Chi tiết theo từng mặt").Bold().FontSize(Fs(11)).FontColor(Brand);
            foreach (var dim in dimInsights.Take(maxInsights + 2))
            {
                col.Item().PaddingTop(5).Column(c =>
                {
                    c.Item().Text($"{V(dim.DimensionName)} ({V(dim.CategoryName)}) — {V(dim.ScoreLabel)}").Bold().FontSize(Fs(8));
                    c.Item().PaddingTop(1).Text(V(dim.Assessment)).FontSize(Fs(8)).LineHeight(1.3f);
                    c.Item().Text(V(dim.Recommendation)).FontSize(Fs(7)).FontColor(TextMuted).LineHeight(1.3f);
                });
            }
        }

        if (intel.ExecutiveSummary?.Paragraphs.Count > 0)
        {
            col.Item().PaddingTop(10).Text("Tổng hợp chiến lược").Bold().FontSize(Fs(10)).FontColor(Brand);
            foreach (var p in intel.ExecutiveSummary.Paragraphs)
                col.Item().PaddingTop(3).Text(V(p)).FontSize(Fs(9)).LineHeight(1.35f);
        }

        if (intel.AiNarrative?.PersonalizedInsights.Count > 0)
        {
            col.Item().PaddingTop(8).Text("Nhận định cá nhân hóa (AI)").Bold().FontSize(Fs(10)).FontColor("#4f46e5");
            foreach (var insight in intel.AiNarrative.PersonalizedInsights.Take(maxInsights))
                col.Item().PaddingTop(3).Text($"• {V(insight)}").FontSize(Fs(9)).LineHeight(1.3f);
        }
    }

    private static void RenderClosingCta(ColumnDescriptor col, AssessmentReportIntelligenceDto intel)
    {
        if (intel.ConsultingBrief is not null)
        {
            col.Item().PaddingTop(10).Background(Accent).Padding(12).Column(c =>
            {
                c.Item().Text(V(intel.ConsultingBrief.UrgencyStatement)).FontSize(Fs(9)).FontColor(Colors.White);
                c.Item().PaddingTop(8).Text(V(intel.ConsultingBrief.NextStepCta)).Bold().FontSize(Fs(10)).FontColor(Colors.White);
            });
        }
    }

    private static void RenderClosingSection(ColumnDescriptor col, AssessmentReportIntelligenceDto intel)
    {
        if (intel.AiNarrative?.PersonalizedInsights.Count > 0)
        {
            col.Item().PaddingTop(4).Text("Kết luận").Bold().FontSize(Fs(11)).FontColor(Brand);
            col.Item().PaddingTop(4).Text(V(intel.AiNarrative.PersonalizedInsights[^1])).FontSize(Fs(9)).LineHeight(1.35f);
        }
        else if (intel.ExecutiveSummary?.Paragraphs.Count > 0)
        {
            col.Item().PaddingTop(4).Text("Kết luận").Bold().FontSize(Fs(11)).FontColor(Brand);
            col.Item().PaddingTop(4).Text(V(intel.ExecutiveSummary.Paragraphs[^1])).FontSize(Fs(9)).LineHeight(1.35f);
        }

        RenderClosingCta(col, intel);
        KapReportContactFooter.RenderPdf(col, Brand);
    }

    private static string V(string? text) => KapVietnameseText.Display(text);

    private static string VCat(string code, string? name) => KapVietnameseText.Category(code, name);

    private static void RenderQuestPdfScoreBars(
        ColumnDescriptor col,
        IReadOnlyList<AssessmentCategoryScoreDto> categories,
        KapBenchmarkAnalysisDto? benchmark)
    {
        if (categories.Count == 0) return;

        var benchMap = benchmark?.Categories.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, KapBenchmarkCategoryDto>(StringComparer.OrdinalIgnoreCase);

        col.Item().PaddingTop(4).Text("Biểu đồ điểm theo nhóm (thang 0–4)").Bold().FontSize(Fs(10)).FontColor(Brand);
        col.Item().PaddingTop(2).Text("■ Xanh lá: điểm của bạn  │  ■ Cam: mức tham chiếu trung bình  │  Số bên phải: điểm và %")
            .FontSize(Fs(7)).FontColor(TextMuted);

        const decimal maxScore = 4m;

        foreach (var cat in categories.OrderByDescending(c => c.Score))
        {
            benchMap.TryGetValue(cat.Code, out var benchRow);
            var label = VCat(cat.Code, cat.Name);
            var scoreWeight = Math.Max(1, (int)Math.Round((double)(cat.Score / maxScore * 100m)));
            var benchWeight = benchRow?.CohortMean is decimal mean
                ? Math.Max(0, (int)Math.Round((double)(mean / maxScore * 100m)) - scoreWeight)
                : 0;
            var restWeight = Math.Max(1, 100 - scoreWeight - benchWeight);

            col.Item().PaddingTop(6).Column(barCol =>
            {
                barCol.Item().Row(row =>
                {
                    row.ConstantItem(98).Text(label).FontSize(Fs(8));
                    row.RelativeItem().Height(16).Row(barRow =>
                    {
                        barRow.RelativeItem((uint)scoreWeight).Background(Brand);
                        if (benchWeight > 0)
                            barRow.RelativeItem((uint)benchWeight).Background(Colors.Orange.Lighten2);
                        barRow.RelativeItem((uint)restWeight);
                    });
                    row.ConstantItem(76).AlignRight().Text($"{cat.Score:F1}/4 ({cat.ScorePct:F0}%)").FontSize(Fs(7));
                });
                if (benchRow?.CohortMean is decimal cohortMean)
                    barCol.Item().PaddingLeft(98).Text($"Tham chiếu: {cohortMean:F2}/4").FontSize(Fs(6)).FontColor(TextMuted);
            });
        }
    }

    private static void RenderChartScoreTable(
        ColumnDescriptor col,
        IReadOnlyList<AssessmentCategoryScoreDto> categories,
        KapBenchmarkAnalysisDto? benchmark)
    {
        if (categories.Count == 0) return;

        var benchMap = benchmark?.Categories.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, KapBenchmarkCategoryDto>(StringComparer.OrdinalIgnoreCase);

        col.Item().PaddingTop(10).Text("Bảng số liệu chi tiết").Bold().FontSize(Fs(10)).FontColor(Brand);
        col.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(3);
                c.RelativeColumn();
                c.RelativeColumn();
                c.RelativeColumn();
                c.RelativeColumn();
            });
            table.Header(h =>
            {
                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Nhóm").Bold().FontSize(Fs(8));
                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Điểm/4").Bold().FontSize(Fs(8));
                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("%").Bold().FontSize(Fs(8));
                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("TB tham chiếu").Bold().FontSize(Fs(8));
                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Chênh lệch").Bold().FontSize(Fs(8));
            });
            foreach (var cat in categories.OrderByDescending(c => c.Score))
            {
                benchMap.TryGetValue(cat.Code, out var benchRow);
                table.Cell().Padding(4).Text(VCat(cat.Code, cat.Name)).FontSize(Fs(8));
                table.Cell().Padding(4).AlignRight().Text($"{cat.Score:F2}").FontSize(Fs(8));
                table.Cell().Padding(4).AlignRight().Text($"{cat.ScorePct:F0}%").FontSize(Fs(8));
                table.Cell().Padding(4).AlignRight().Text(benchRow?.CohortMean?.ToString("F2") ?? "—").FontSize(Fs(8));
                table.Cell().Padding(4).AlignRight().Text(benchRow?.Delta?.ToString("+0.00;-0.00") ?? "—").FontSize(Fs(8));
            }
        });
    }

    private static void RenderBulletBlock(ColumnDescriptor col, string title, IReadOnlyList<string> items)
    {
        if (items.Count == 0) return;
        col.Item().PaddingTop(8).Text(title).Bold().FontSize(Fs(10)).FontColor(Brand);
        foreach (var item in items)
            col.Item().PaddingTop(2).Text($"• {V(item)}").FontSize(Fs(9)).LineHeight(1.3f);
    }

    private static void RenderExecutivePage(
        ColumnDescriptor col,
        AssessmentFullReportDto report,
        AssessmentReportIntelligenceDto intel,
        bool fullSummary)
    {
        col.Item().Background(BrandLight).Padding(14).Column(box =>
        {
            var title = intel.ConsultingBrief is not null ? "CHẨN ĐOÁN KINH DOANH" : "TÓM TẮT ĐIỀU HÀNH";
            box.Item().Text(title).Bold().FontSize(Fs(12)).FontColor(Brand);

            if (intel.ConsultingBrief is not null)
            {
                box.Item().PaddingTop(6).Text(intel.ConsultingBrief.DiagnosisHeadline).Bold().FontSize(Fs(11)).FontColor(Colors.Red.Darken2);
                box.Item().PaddingTop(4).Text(intel.ConsultingBrief.CostOfInaction).LineHeight(1.35f);
            }

            if (intel.ExecutiveSummary is not null)
            {
                if (intel.ConsultingBrief is null)
                    box.Item().PaddingTop(6).Text(intel.ExecutiveSummary.Headline).Bold().FontSize(Fs(11));
                var limit = fullSummary ? int.MaxValue : (intel.ConsultingBrief is not null ? 2 : int.MaxValue);
                foreach (var p in intel.ExecutiveSummary.Paragraphs.Take(limit))
                    box.Item().PaddingTop(4).Text(p).LineHeight(1.35f);
            }

            if (intel.AiNarrative?.PersonalizedInsights.Count > 0)
            {
                box.Item().PaddingTop(8).Text("Nhận định cá nhân hóa").Bold().FontSize(Fs(10)).FontColor("#4f46e5");
                foreach (var insight in intel.AiNarrative.PersonalizedInsights.Take(fullSummary ? 8 : 4))
                    box.Item().PaddingTop(3).Text($"• {insight}").FontSize(Fs(9)).LineHeight(1.3f);
            }
        });
    }

    private static void RenderNovixaReadiness(ColumnDescriptor col, KapNovixaReadinessDto? readiness)
    {
        if (readiness is null) return;

        col.Item().PaddingTop(8).Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().Text("Mức sẵn sàng Novixa").Bold().FontSize(Fs(12)).FontColor(Brand);
                c.Item().PaddingTop(4).Text($"{readiness.OverallPct:F0}%").Bold().FontSize(Fs(28)).FontColor(Brand);
                c.Item().Text(readiness.StatusLabel).FontSize(Fs(10)).FontColor(TextMuted);
            });
        });

        col.Item().PaddingTop(8).Text(RenderProgressBar(readiness.OverallPct)).FontSize(Fs(10));

        col.Item().PaddingTop(10).Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); });
            table.Header(h =>
            {
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Chiều").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Điểm").Bold();
            });
            foreach (var dim in readiness.Dimensions)
            {
                table.Cell().Padding(5).Text(dim.Name);
                table.Cell().Padding(5).AlignRight().Text($"{dim.ScorePct:F0}%");
            }
        });
    }

    private static string RenderProgressBar(decimal pct)
    {
        var filled = (int)Math.Round(Math.Clamp(pct, 0, 100) / 10m);
        return new string('█', filled) + new string('░', 10 - filled);
    }

    private static void RenderRoiMetrics(ColumnDescriptor col, IReadOnlyList<KapRoiMetricDto> metrics)
    {
        if (metrics.Count == 0) return;
        col.Item().PaddingTop(8).Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(3); });
            table.Header(h =>
            {
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Chỉ số").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Mức").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Giải thích").Bold();
            });
            foreach (var m in metrics)
            {
                table.Cell().Padding(5).Text(m.Label);
                table.Cell().Padding(5).Text(m.Range).Bold();
                table.Cell().Padding(5).Text(m.Description).FontSize(Fs(8));
            }
        });
    }

    private static void RenderRoiBeforeAfter(ColumnDescriptor col, KapRoiStoryDto? roi)
    {
        if (roi is null) return;
        col.Item().PaddingTop(10).Text("Trước / sau triển khai").Bold().FontSize(Fs(11));
        col.Item().PaddingTop(4).Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().Text("Hiện trạng").Bold().FontSize(Fs(9)).FontColor(Colors.Red.Medium);
                foreach (var b in roi.BeforeState)
                    c.Item().PaddingTop(2).Text($"• {b}").FontSize(Fs(8));
            });
            row.ConstantItem(12);
            row.RelativeItem().Column(c =>
            {
                c.Item().Text("Sau triển khai").Bold().FontSize(Fs(9)).FontColor(Colors.Green.Darken2);
                foreach (var a in roi.AfterState)
                    c.Item().PaddingTop(2).Text($"• {a}").FontSize(Fs(8));
            });
        });
    }

    private static void RenderBusinessImpactForecast(ColumnDescriptor col, IReadOnlyList<KapBusinessImpactForecastDto> items)
    {
        if (items.Count == 0) return;
        col.Item().PaddingTop(8).Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); c.ConstantColumn(40); });
            table.Header(h =>
            {
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Chỉ số").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Dự báo (90 ngày)").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignCenter().Text("Xu hướng").Bold();
            });
            foreach (var item in items)
            {
                table.Cell().Padding(5).Text(item.Metric);
                table.Cell().Padding(5).Text(item.ChangeRange).Bold();
                table.Cell().Padding(5).AlignCenter().Text(item.Direction == "up" ? "↑" : "↓");
            }
        });
    }

    private static void RenderConsultingBriefImpacts(ColumnDescriptor col, KapConsultingBriefDto brief)
    {
        col.Item().PaddingTop(12).Text("Hệ quả nếu không hành động").Bold().FontSize(Fs(11)).FontColor(Colors.Red.Medium);
        foreach (var impact in brief.BusinessImpacts.Take(5))
        {
            col.Item().PaddingTop(6).Background(Colors.Red.Lighten5).Padding(10).Column(c =>
            {
                c.Item().Text(V(impact.Title)).Bold().FontSize(Fs(10));
                c.Item().PaddingTop(2).Text(V(impact.ImpactStatement)).FontSize(Fs(9)).LineHeight(1.3f);
                c.Item().PaddingTop(2).Text($"Ước tính: {V(impact.CostHint)}").FontSize(Fs(8)).FontColor(TextMuted);
            });
        }
    }

    private static void RenderModuleStars(ColumnDescriptor col, IReadOnlyList<KapModuleStarDto> modules)
    {
        if (modules.Count == 0) return;
        col.Item().PaddingTop(8).Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.ConstantColumn(70); c.RelativeColumn(3); });
            table.Header(h =>
            {
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Phân hệ").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Mức cần").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Lý do").Bold();
            });
            foreach (var mod in modules)
            {
                table.Cell().Padding(5).Text(V(mod.ModuleName)).Bold();
                table.Cell().Padding(5).Text(KapVietnameseLabels.Stars(mod.Stars));
                table.Cell().Padding(5).Text(V(mod.Rationale)).FontSize(Fs(8));
            }
        });
    }

    private static void RenderModuleFits(ColumnDescriptor col, IReadOnlyList<KapSoftwareModuleFitDto> modules)
    {
        col.Item().PaddingTop(10).Text("Chi tiết phù hợp").Bold().FontSize(Fs(11)).FontColor(Brand);
        foreach (var mod in modules)
        {
            col.Item().PaddingTop(6).Border(1).BorderColor(Brand).Padding(10).Column(c =>
            {
                c.Item().Text(V(mod.ModuleName)).Bold().FontSize(Fs(10)).FontColor(Brand);
                c.Item().PaddingTop(2).Text(V(mod.PainResolved)).FontSize(Fs(9));
                c.Item().PaddingTop(2).Text($"30 ngày: {V(mod.Outcome30Days)}").FontSize(Fs(8));
                c.Item().Text($"90 ngày: {V(mod.Outcome90Days)}").FontSize(Fs(8)).FontColor(TextMuted);
            });
        }
    }

    private static void RenderGapAnalysis(ColumnDescriptor col, KapGapAnalysisDto? gap)
    {
        if (gap is null || gap.Items.Count == 0) return;
        col.Item().PaddingTop(4).Text(V(gap.Narrative)).FontSize(Fs(9)).FontColor(TextMuted);
        col.Item().PaddingTop(8).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn();
                c.RelativeColumn();
                c.RelativeColumn();
                c.RelativeColumn();
            });
            table.Header(h =>
            {
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Hiện trạng").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Mục tiêu").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Phân hệ Novixa").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Tính năng").Bold();
            });
            foreach (var item in gap.Items)
            {
                table.Cell().Padding(5).Text(V(item.CurrentState));
                table.Cell().Padding(5).Text(V(item.TargetState));
                table.Cell().Padding(5).Text(V(item.NovixaModule));
                table.Cell().Padding(5).Text(V(item.FeatureHint) is { Length: > 0 } hint ? hint : "—").FontSize(Fs(8));
            }
        });
    }

    private static void RenderModuleMappings(ColumnDescriptor col, IReadOnlyList<KapModuleMappingItemDto> items)
    {
        if (items.Count == 0) return;
        foreach (var m in items)
        {
            col.Item().PaddingTop(6).Column(c =>
            {
                c.Item().Text(V(m.Problem)).Bold().FontSize(Fs(9));
                c.Item().Text($"→ {V(m.Module)} → {V(m.FeatureChain)}").FontSize(Fs(8)).FontColor(TextMuted);
            });
        }
    }

    private static void RenderInvestmentPhases(ColumnDescriptor col, IReadOnlyList<KapInvestmentPhaseDto> phases)
    {
        if (phases.Count == 0) return;
        col.Item().PaddingTop(10).Text("Đề xuất đầu tư theo giai đoạn").Bold().FontSize(Fs(11)).FontColor(Brand);
        foreach (var phase in phases)
        {
            col.Item().PaddingTop(6).BorderLeft(3).BorderColor(Brand).PaddingLeft(8).Column(c =>
            {
                c.Item().Text($"{V(phase.Title)} ({V(phase.TimingHint)})").Bold().FontSize(Fs(10));
                c.Item().Text($"Trọng tâm: {V(phase.ModuleFocus)}").FontSize(Fs(9));
                c.Item().Text(V(phase.Description)).FontSize(Fs(8)).FontColor(TextMuted);
            });
        }
    }

    private static void RenderTransformationRoadmap(ColumnDescriptor col, KapTransformationRoadmapDto? roadmap)
    {
        if (roadmap is null || roadmap.Phases.Count == 0) return;
        col.Item().PaddingTop(4).Text(V(roadmap.Narrative)).FontSize(Fs(9)).FontColor(TextMuted);
        foreach (var phase in roadmap.Phases)
        {
            col.Item().PaddingTop(8).Row(row =>
            {
                row.ConstantItem(28).Height(28).Background(Brand).AlignCenter().AlignMiddle()
                    .Text(phase.Phase.ToString()).Bold().FontColor(Colors.White);
                row.RelativeItem().PaddingLeft(8).Column(c =>
                {
                    c.Item().Text(V(phase.Title)).Bold().FontSize(Fs(10));
                    c.Item().Text(V(phase.Description)).FontSize(Fs(9));
                    c.Item().Text($"Phân hệ: {V(phase.Module)}").FontSize(Fs(8)).FontColor(TextMuted);
                });
            });
        }
    }

    private static void RenderPriorityMatrix(ColumnDescriptor col, KapPriorityMatrixDto? matrix)
    {
        if (matrix is null) return;
        RenderMatrixQuadrant(col, "Tác động cao — Ưu tiên cao", matrix.HighImpactHighPriority);
        RenderMatrixQuadrant(col, "Thắng nhanh (tác động cao, nỗ lực thấp)", matrix.QuickWins);
        RenderMatrixQuadrant(col, "Dài hạn", matrix.LongTerm);
        RenderMatrixQuadrant(col, "Tùy chọn", matrix.Optional);
    }

    private static void RenderMatrixQuadrant(ColumnDescriptor col, string title, IReadOnlyList<KapPriorityItemDto> items)
    {
        if (items.Count == 0) return;
        col.Item().PaddingTop(8).Text(title).Bold().FontSize(Fs(10)).FontColor(Brand);
        foreach (var item in items.Take(4))
            col.Item().PaddingTop(3).Text($"• {V(item.Title)}: {V(item.Body)}").FontSize(Fs(8)).LineHeight(1.3f);
    }

    private static void RenderPriorityMatrixVisual(ColumnDescriptor col, KapPriorityMatrixDto? matrix)
    {
        if (matrix is null) return;
        col.Item().PaddingTop(8).Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Red.Lighten4).Padding(8)
                .Text("Tác động cao\nNỗ lực cao\n→ Ưu tiên chiến lược").FontSize(Fs(8));
            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Green.Lighten4).Padding(8)
                .Text("Tác động cao\nNỗ lực thấp\n→ Thắng nhanh").FontSize(Fs(8)).Bold();
            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Orange.Lighten4).Padding(8)
                .Text("Tác động thấp\nNỗ lực cao\n→ Dài hạn").FontSize(Fs(8));
            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten3).Padding(8)
                .Text("Tác động thấp\nNỗ lực thấp\n→ Tùy chọn").FontSize(Fs(8));
        });
    }

    private static void RenderCostBenefit(ColumnDescriptor col, KapCostBenefitAnalysisDto? analysis, bool compact)
    {
        if (analysis is null) return;
        if (!compact)
            col.Item().PaddingTop(4).Text(V(analysis.Summary)).FontSize(Fs(9)).FontColor(TextMuted).LineHeight(1.35f);

        col.Item().PaddingTop(compact ? 8 : 10).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);
                c.RelativeColumn();
                c.RelativeColumn();
                if (!compact) c.RelativeColumn();
            });
            table.Header(h =>
            {
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Hạng mục").Bold();
                if (!compact)
                    h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Phạm vi triển khai").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Lợi ích").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Thời điểm hiệu quả").Bold();
            });
            foreach (var item in analysis.Items
                         .Where(i => !KapVietnameseText.ContainsPricingLanguage(i.InvestmentHint)
                             && !i.Category.Contains("Phần mềm Novixa", StringComparison.OrdinalIgnoreCase))
                         .Take(compact ? 3 : 10))
            {
                table.Cell().Padding(5).Text(V(item.Category));
                if (!compact)
                    table.Cell().Padding(5).Text(V(item.InvestmentHint)).FontSize(Fs(8));
                table.Cell().Padding(5).Text(V(item.BenefitRange)).Bold().FontSize(Fs(8));
                table.Cell().Padding(5).Text(V(item.PaybackHint)).FontSize(Fs(8));
            }
        });

        if (!compact)
        {
            var net = KapVietnameseText.Polish(KapVietnameseText.StripPricingMentions(analysis.NetAssessment));
            if (!string.IsNullOrWhiteSpace(net))
                col.Item().PaddingTop(10).Background(BrandLight).Padding(10)
                    .Text(V(net)).FontSize(Fs(9)).LineHeight(1.35f);
        }
    }

    private static void RenderImplementationTimeline(ColumnDescriptor col, KapImplementationTimelineDto? timeline)
    {
        if (timeline is null || timeline.Milestones.Count == 0) return;
        col.Item().PaddingTop(4).Text(V(timeline.Narrative)).FontSize(Fs(9)).FontColor(TextMuted);
        col.Item().PaddingTop(8).Table(table =>
        {
            table.ColumnsDefinition(c => { c.ConstantColumn(70); c.RelativeColumn(2); c.RelativeColumn(2); });
            table.Header(h =>
            {
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Giai đoạn").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Hoạt động").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Kết quả").Bold();
            });
            foreach (var m in timeline.Milestones)
            {
                table.Cell().Padding(5).Text(V(m.PeriodLabel)).Bold().FontSize(Fs(8));
                table.Cell().Padding(5).Text(V(m.Activity)).FontSize(Fs(8));
                table.Cell().Padding(5).Text(V(m.Deliverable)).FontSize(Fs(8));
            }
        });
    }

    private static void RenderRiskRegister(ColumnDescriptor col, KapRiskRegisterDto? register, int maxItems)
    {
        if (register is null) return;
        col.Item().PaddingTop(4).Text(V(register.Summary)).FontSize(Fs(9)).FontColor(TextMuted);
        col.Item().PaddingTop(8).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(28);
                c.RelativeColumn(2);
                c.ConstantColumn(52);
                c.RelativeColumn(2);
                if (maxItems > 4) c.RelativeColumn(2);
            });
            table.Header(h =>
            {
                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Mã").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Rủi ro").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Mức").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Tác động").Bold();
                if (maxItems > 4)
                    h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Giảm thiểu").Bold();
            });
            foreach (var r in register.Items.Take(maxItems))
            {
                table.Cell().Padding(4).Text(r.Code).FontSize(Fs(8));
                table.Cell().Padding(4).Text(V(r.Title)).Bold().FontSize(Fs(8));
                table.Cell().Padding(4).Text(KapVietnameseLabels.RiskLevel(r.Severity)).FontSize(Fs(8));
                table.Cell().Padding(4).Text(V(r.Impact)).FontSize(Fs(7));
                if (maxItems > 4)
                    table.Cell().Padding(4).Text(V(r.Mitigation)).FontSize(Fs(7));
            }
        });
    }

    private static void RenderTopRisksBrief(ColumnDescriptor col, IReadOnlyList<KapRiskItemDto> risks)
    {
        if (risks.Count == 0) return;
        col.Item().PaddingTop(10).Text("Rủi ro nổi bật").Bold().FontSize(Fs(10)).FontColor(Brand);
        foreach (var risk in risks.Take(3))
            col.Item().PaddingTop(4).Text($"• {risk.Title} ({KapVietnameseLabels.RiskLevel(risk.Level)})").FontSize(Fs(9));
    }

    private static void RenderAiConclusion(ColumnDescriptor col, AssessmentReportIntelligenceDto intel)
    {
        if (intel.ExecutiveSummary is not null)
        {
            col.Item().PaddingTop(8).Text(intel.ExecutiveSummary.Headline).Bold().FontSize(Fs(11));
            foreach (var p in intel.ExecutiveSummary.Paragraphs.TakeLast(2))
                col.Item().PaddingTop(4).Text(p).FontSize(Fs(9)).LineHeight(1.35f);
        }
        if (intel.AiNarrative?.PersonalizedInsights.Count > 0)
        {
            col.Item().PaddingTop(8).Text("Kết luận AI").Bold().FontSize(Fs(10)).FontColor("#4f46e5");
            col.Item().PaddingTop(4).Text(intel.AiNarrative.PersonalizedInsights[^1]).FontSize(Fs(9));
        }
    }

    private static void RenderBenchmark(ColumnDescriptor col, KapBenchmarkAnalysisDto? benchmark, bool includeTitle = true)
    {
        if (benchmark is null || benchmark.Categories.Count == 0) return;

        if (includeTitle)
        {
            col.Item().PaddingTop(16).Text("So sánh tham chiếu").Bold().FontSize(Fs(12)).FontColor(Brand);
            col.Item().Text(benchmark.Narrative ?? "").FontSize(Fs(8)).FontColor(TextMuted);
        }
        else if (!string.IsNullOrWhiteSpace(benchmark.Narrative))
        {
            col.Item().PaddingTop(8).Text(benchmark.Narrative).FontSize(Fs(8)).FontColor(TextMuted);
        }

        if (benchmark.OverallVsMean.HasValue)
        {
            var delta = benchmark.OverallVsMean.Value;
            var label = delta >= 0
                ? $"Trên ngưỡng tham chiếu {delta:+#.##;-#.##;0} điểm"
                : $"Dưới ngưỡng tham chiếu {delta:+#.##;-#.##;0} điểm";
            col.Item().PaddingTop(4).Text(label).Bold().FontColor(delta >= 0 ? Colors.Green.Darken2 : Colors.Red.Medium);
        }

        if (benchmark.EstimatedPercentile is decimal pct)
        {
            col.Item().PaddingTop(4).Text($"Phân vị ước tính: Top {100 - pct:F0}% · {V(benchmark.PercentileLabel)}")
                .FontSize(Fs(9)).FontColor(Brand);
        }

        col.Item().PaddingTop(6).Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
            table.Header(h =>
            {
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Nhóm").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Bạn").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Trung bình").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Chênh lệch").Bold();
            });
            foreach (var row in benchmark.Categories)
            {
                table.Cell().Padding(5).Text(VCat(row.Code, row.Name));
                table.Cell().Padding(5).AlignRight().Text($"{row.Score:F2}");
                table.Cell().Padding(5).AlignRight().Text(row.CohortMean?.ToString("F2") ?? "—");
                table.Cell().Padding(5).AlignRight().Text(row.Delta?.ToString("+0.00;-0.00") ?? "—");
            }
        });
    }

    private static void RenderCrossCategoryInsight(ColumnDescriptor col, KapCrossCategoryInsightDto? insight)
    {
        if (insight is null) return;
        col.Item().PaddingBottom(8).Background(BrandLight).Padding(10).Column(box =>
        {
            box.Item().Text(V(insight.Headline)).Bold().FontSize(Fs(10)).FontColor(Brand);
            box.Item().PaddingTop(4).Text(V(insight.Analysis)).FontSize(Fs(9)).LineHeight(1.4f);
            foreach (var imp in insight.Implications.Take(3))
                box.Item().PaddingTop(3).Text($"• {V(imp)}").FontSize(Fs(8));
        });
    }

    private static void RenderBenchmarkTiers(ColumnDescriptor col, KapBenchmarkAnalysisDto? benchmark)
    {
        if (benchmark?.Tiers is null || benchmark.Tiers.Count == 0) return;
        col.Item().PaddingTop(12).Text("So sánh đa tầng").Bold().FontSize(Fs(11)).FontColor(Brand);
        col.Item().PaddingTop(6).Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(3); });
            table.Header(h =>
            {
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Tầng").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Điểm %").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Ghi chú").Bold();
            });
            foreach (var tier in benchmark.Tiers)
            {
                table.Cell().Padding(5).Text(V(tier.Label)).Bold();
                table.Cell().Padding(5).AlignRight().Text($"{tier.ScorePct:F0}%");
                table.Cell().Padding(5).Text(V(tier.Note ?? "")).FontSize(Fs(8));
            }
        });
    }

    private static void RenderTransformationReadiness(ColumnDescriptor col, KapTransformationReadinessDto? readiness)
    {
        if (readiness is null || readiness.Bars.Count == 0) return;
        col.Item().PaddingTop(4).Text(V(readiness.Narrative)).FontSize(Fs(9)).FontColor(TextMuted).LineHeight(1.35f);
        foreach (var bar in readiness.Bars)
        {
            var filled = Math.Clamp(bar.Pct / 10, 0, 10);
            var barVisual = new string('█', filled) + new string('░', 10 - filled);
            col.Item().PaddingTop(8).Column(c =>
            {
                c.Item().Row(r =>
                {
                    r.RelativeItem().Text(V(bar.Label)).Bold().FontSize(Fs(9));
                    r.ConstantItem(40).AlignRight().Text($"{bar.Pct}%").Bold().FontColor(Brand);
                });
                c.Item().PaddingTop(2).Text(barVisual).FontSize(Fs(10)).FontColor(Brand);
            });
        }
    }

    private static void RenderNarrativeCascade(ColumnDescriptor col, KapNarrativeCascadeDto? cascade, string title)
    {
        if (cascade is null || cascade.Steps.Count == 0) return;
        col.Item().PaddingTop(10).Text(title).Bold().FontSize(Fs(11)).FontColor(Brand);
        col.Item().PaddingTop(4).Text(V(cascade.Summary)).FontSize(Fs(8)).FontColor(TextMuted).LineHeight(1.35f);
        foreach (var step in cascade.Steps)
        {
            col.Item().PaddingTop(6).Row(row =>
            {
                row.ConstantItem(72).Text(V(step.Horizon)).Bold().FontSize(Fs(8)).FontColor(Brand);
                row.RelativeItem().Text("↓").FontSize(Fs(8)).FontColor(TextMuted);
            });
            col.Item().PaddingLeft(8).Text(V(step.Outcome)).FontSize(Fs(9)).LineHeight(1.35f);
        }
    }

    private static void RenderWhyNovixa(ColumnDescriptor col, KapWhyNovixaDto? why)
    {
        if (why is null || why.Rows.Count == 0) return;
        col.Item().PaddingTop(4).Text(V(why.Intro)).FontSize(Fs(9)).LineHeight(1.4f);
        col.Item().PaddingTop(10).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);
                c.RelativeColumn();
                c.RelativeColumn(2);
                c.RelativeColumn();
            });
            table.Header(h =>
            {
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Vấn đề").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Module").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Lợi ích").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("KPI").Bold();
            });
            foreach (var row in why.Rows.Take(6))
            {
                table.Cell().Padding(5).Text(V(row.Problem)).FontSize(Fs(8));
                table.Cell().Padding(5).Text(V(row.Module)).FontSize(Fs(8));
                table.Cell().Padding(5).Text(V(row.Benefit)).FontSize(Fs(8));
                table.Cell().Padding(5).Text(V(row.KpiTarget)).Bold().FontSize(Fs(8));
            }
        });
    }

    private static void RenderCategoryScores(ColumnDescriptor col, IReadOnlyList<AssessmentCategoryScoreDto> categories)
    {
        if (categories.Count == 0) return;
        col.Item().PaddingTop(10).Text("Điểm theo nhóm năng lực").Bold().FontSize(Fs(11)).FontColor(Brand);
        col.Item().PaddingTop(6).Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(); c.RelativeColumn(); });
            table.Header(h =>
            {
                h.Cell().Background(Colors.Grey.Lighten3).Padding(6).Text("Nhóm").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(6).AlignRight().Text("Điểm").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(6).AlignRight().Text("%").Bold();
            });
            foreach (var cat in categories.OrderByDescending(c => c.Score))
            {
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(VCat(cat.Code, cat.Name));
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignRight().Text($"{cat.Score:F2}");
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignRight().Text($"{cat.ScorePct:F1}%");
            }
        });
    }

    private static void RenderRootCauses(ColumnDescriptor col, IReadOnlyList<KapRootCauseDto> items)
    {
        if (items.Count == 0) return;
        col.Item().PaddingTop(10).Text("Phân tích nguyên nhân gốc").Bold().FontSize(Fs(11));
        foreach (var rc in items.Take(5))
        {
            col.Item().PaddingTop(6).BorderLeft(3).BorderColor(Colors.Red.Medium).PaddingLeft(8).Column(c =>
            {
                c.Item().Text(V(rc.Title)).Bold();
                c.Item().Text(V(rc.Body)).LineHeight(1.3f);
                if (rc.Evidence.Count > 0)
                    c.Item().Text(string.Join(" · ", rc.Evidence.Select(V))).FontSize(Fs(8)).FontColor(TextMuted);
            });
        }
    }

    private static void RenderRisks(ColumnDescriptor col, IReadOnlyList<KapRiskItemDto> risks)
    {
        if (risks.Count == 0) return;
        col.Item().PaddingTop(10).Text("Rủi ro cần lưu ý").Bold().FontSize(Fs(11));
        foreach (var risk in risks)
        {
            var color = risk.Level.Equals("high", StringComparison.OrdinalIgnoreCase) ? Colors.Red.Medium : Accent;
            col.Item().PaddingTop(4).Background(Colors.Red.Lighten4).Padding(8).Column(c =>
            {
                c.Item().Text($"{V(risk.Title)} [{KapVietnameseLabels.RiskLevel(risk.Level)}]").Bold().FontColor(color);
                c.Item().Text(V(risk.Body));
            });
        }
    }

    private static void RenderSwot(ColumnDescriptor col, KapSwotAnalysisDto? swot)
    {
        if (swot is null) return;
        col.Item().PaddingTop(10).Text("Phân tích điểm mạnh — điểm yếu — cơ hội — rủi ro").Bold().FontSize(Fs(11));
        RenderSwotBlock(col, "Điểm mạnh (S)", swot.Strengths, Colors.Green.Darken2);
        RenderSwotBlock(col, "Điểm yếu (W)", swot.Weaknesses, Colors.Orange.Darken2);
        RenderSwotBlock(col, "Cơ hội (O)", swot.Opportunities, Brand);
        RenderSwotBlock(col, "Thách thức (T)", swot.Threats, Colors.Red.Medium);
    }

    private static void RenderSwotBlock(ColumnDescriptor col, string title, IReadOnlyList<KapSwotItemDto> items, string color)
    {
        if (items.Count == 0) return;
        col.Item().PaddingTop(4).Text(title).Bold().FontColor(color);
        foreach (var item in items.Take(3))
            col.Item().Text($"• {V(item.Title)}: {V(item.Body)}");
    }

    private static void RenderQualitative(ColumnDescriptor col, AssessmentQualitativeTagsDto tags)
    {
        if (string.IsNullOrWhiteSpace(tags.PainPoint) && string.IsNullOrWhiteSpace(tags.PriorityNeed))
            return;
        col.Item().PaddingTop(10).Text("Nhu cầu & trở ngại (khảo sát)").Bold().FontSize(Fs(11));
        if (!string.IsNullOrWhiteSpace(tags.PainPoint))
            col.Item().Text($"Trở ngại lớn nhất: {KapVietnameseLabels.QualitativeTag(tags.PainPoint)}");
        if (!string.IsNullOrWhiteSpace(tags.PriorityNeed))
            col.Item().Text($"Ưu tiên cải thiện: {KapVietnameseLabels.QualitativeTag(tags.PriorityNeed)}");
    }

    private static void RenderRecommendations(ColumnDescriptor col, IReadOnlyList<AssessmentRecommendationDto> items)
    {
        if (items.Count == 0) return;
        col.Item().PaddingTop(8).Text("Đề xuất ưu tiên").Bold().FontSize(Fs(11));
        var rank = 1;
        foreach (var rec in items.Take(8))
        {
            col.Item().PaddingTop(4).Column(c =>
            {
                c.Item().Text($"{rank}. {V(rec.Title)}").Bold();
                if (!string.IsNullOrWhiteSpace(rec.EstimateHint))
                    c.Item().Text(V(rec.EstimateHint)).FontSize(Fs(8)).FontColor(TextMuted);
                c.Item().Text(V(rec.Body));
            });
            rank++;
        }
    }

    private static void RenderRoadmap(ColumnDescriptor col, KapRoadmapDto? roadmap, bool include180 = false)
    {
        if (roadmap is null) return;
        RenderRoadmapBlock(col, "30 ngày tới", roadmap.Days30);
        RenderRoadmapBlock(col, "60 ngày", roadmap.Days60);
        RenderRoadmapBlock(col, "90 ngày", roadmap.Days90);
        if (include180)
            RenderRoadmapBlock(col, "180 ngày", roadmap.Days180);
    }

    private static void RenderRoadmapBlock(ColumnDescriptor col, string label, IReadOnlyList<KapRoadmapItemDto> items)
    {
        if (items.Count == 0) return;
        col.Item().PaddingTop(4).Text(label).Bold().FontColor(Brand);
        foreach (var item in items)
            col.Item().Text($"• {V(item.Title)}: {V(item.Body)}");
    }

    private static void RenderKpis(ColumnDescriptor col, IReadOnlyList<KapKpiRecommendationDto> kpis)
    {
        if (kpis.Count == 0) return;
        col.Item().PaddingTop(8).Text("Việc nên theo dõi sau khi cải thiện").Bold().FontSize(Fs(11));
        foreach (var kpi in kpis)
            col.Item().Text($"• {V(kpi.Name)} — Mục tiêu: {V(kpi.Target)} (trong {kpi.DeadlineDays} ngày)");
    }

    private static Action<TableDescriptor> RenderAppendixTable(KapReportAppendixDto appendix) => table =>
    {
        table.ColumnsDefinition(c =>
        {
            c.ConstantColumn(28);
            c.RelativeColumn(4);
            c.RelativeColumn(3);
            c.ConstantColumn(32);
        });
        table.Header(h =>
        {
            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Mã").Bold();
            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Câu hỏi").Bold();
            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Trả lời").Bold();
            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Điểm").Bold();
        });
        foreach (var q in appendix.Questions)
        {
            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(q.Code);
            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(q.Title);
            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(q.AnswerLabel ?? "—");
            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight()
                .Text(q.Score?.ToString() ?? "—");
        }
    };

    private static void RenderStructuredExecutiveSummary(
        ColumnDescriptor col,
        AssessmentReportIntelligenceDto intel)
    {
        var exec = intel.ExecutiveSummary;
        if (exec is null && intel.ConsultingBrief is null) return;

        if (intel.AiNarrative is not null)
            col.Item().AlignRight().Text("Phân tích AI").FontSize(Fs(8)).Bold().FontColor("#4f46e5");

        if (exec is not null)
        {
            col.Item().PaddingTop(6).Text(V(exec.Headline)).Bold().FontSize(Fs(12)).FontColor(Brand);

            RenderExecSection(col, "1.1 Mở vấn đề", exec.OpeningContext ?? exec.Paragraphs.FirstOrDefault());
            RenderExecSection(col, "1.2 Phân tích nguyên nhân", exec.Analysis ?? exec.Paragraphs.ElementAtOrDefault(1));
            RenderExecSection(col, "1.3 Đánh giá", exec.Assessment ?? exec.Paragraphs.ElementAtOrDefault(2));
            RenderExecSection(col, "1.4 Kết luận", exec.Conclusion ?? exec.Paragraphs.ElementAtOrDefault(3));
            RenderExecSection(col, "1.5 Khuyến nghị", exec.Recommendations ?? exec.Paragraphs.ElementAtOrDefault(4));
        }

        if (intel.ConsultingBrief is not null)
        {
            col.Item().PaddingTop(10).Background(BrandLight).Padding(10).Column(box =>
            {
                box.Item().Text(V(intel.ConsultingBrief.DiagnosisHeadline)).Bold().FontSize(Fs(10)).FontColor(Colors.Red.Darken2);
                box.Item().PaddingTop(4).Text(V(intel.ConsultingBrief.CostOfInaction)).FontSize(Fs(9)).LineHeight(1.35f);
            });
        }

        if (intel.AiNarrative?.PersonalizedInsights.Count > 0)
        {
            col.Item().PaddingTop(8).Text("Nhận định cá nhân hóa (AI)").Bold().FontSize(Fs(10)).FontColor("#4f46e5");
            foreach (var insight in intel.AiNarrative.PersonalizedInsights.Take(4))
                col.Item().PaddingTop(3).Text($"• {V(insight)}").FontSize(Fs(9)).LineHeight(1.3f);
        }
    }

    private static void RenderExecSection(ColumnDescriptor col, string title, string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return;
        col.Item().PaddingTop(8).Text(title).Bold().FontSize(Fs(10)).FontColor(Brand);
        col.Item().PaddingTop(3).Text(V(body)).FontSize(Fs(9)).LineHeight(1.4f);
    }

    private static void RenderScoreHeatmap(ColumnDescriptor col, IReadOnlyList<AssessmentCategoryScoreDto> categories)
    {
        if (categories.Count == 0) return;
        col.Item().Text("Bản đồ nhiệt điểm theo nhóm (màu đậm = điểm cao)").Bold().FontSize(Fs(9)).FontColor(Brand);
        col.Item().PaddingTop(6).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);
                foreach (var _ in categories) c.RelativeColumn();
            });
            table.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Nhóm").FontSize(Fs(7)).Bold();
            foreach (var cat in categories.OrderBy(c => KapPharmacyScoreDisplay.Letter(c.Code)))
                table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignCenter()
                    .Text(KapPharmacyScoreDisplay.Letter(cat.Code)).FontSize(Fs(7)).Bold();

            table.Cell().Padding(4).Text("Điểm /10").FontSize(Fs(7));
            foreach (var cat in categories.OrderBy(c => KapPharmacyScoreDisplay.Letter(c.Code)))
            {
                var ten = KapPharmacyScoreDisplay.ToTen(cat.Score);
                var bg = ten >= 7 ? Colors.Green.Lighten3
                    : ten >= 5 ? Colors.Orange.Lighten3
                    : Colors.Red.Lighten4;
                table.Cell().Background(bg).Padding(4).AlignCenter()
                    .Text(KapPharmacyScoreDisplay.Format(cat.Score)).FontSize(Fs(8)).Bold();
            }
        });
    }

    private static void RenderOpportunities(ColumnDescriptor col, IReadOnlyList<KapOpportunityItemDto> opportunities)
    {
        if (opportunities.Count == 0) return;
        col.Item().PaddingTop(10).Text("Phân tích cơ hội — nếu cải thiện").Bold().FontSize(Fs(11)).FontColor(Brand);
        foreach (var opp in opportunities.Take(6))
        {
            col.Item().PaddingTop(6).BorderLeft(3).BorderColor(Colors.Green.Medium).PaddingLeft(8).Column(c =>
            {
                c.Item().Text(V(opp.Title)).Bold().FontSize(Fs(9));
                c.Item().PaddingTop(2).Text(V(opp.Body)).FontSize(Fs(8)).LineHeight(1.35f);
                if (!string.IsNullOrWhiteSpace(opp.ImpactHint))
                    c.Item().Text($"Tác động ước tính: {V(opp.ImpactHint)}").FontSize(Fs(7)).FontColor(TextMuted);
            });
        }
    }

    private static void RenderActionPlan(
        ColumnDescriptor col,
        KapActionPlanDto? plan,
        IReadOnlyList<AssessmentRecommendationDto> recommendations)
    {
        if (plan is not null)
            col.Item().Text(V(plan.Narrative)).FontSize(Fs(9)).FontColor(TextMuted).LineHeight(1.35f);

        var items = plan?.Items ?? [];
        if (items.Count == 0 && recommendations.Count > 0)
        {
            items = KapActionPlanBuilder.Build(recommendations, null, null).Items;
        }

        if (items.Count == 0) return;

        col.Item().PaddingTop(8).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);
                c.ConstantColumn(50);
                c.ConstantColumn(70);
                c.ConstantColumn(60);
                c.RelativeColumn(2);
            });
            table.Header(h =>
            {
                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Công việc").Bold().FontSize(Fs(7));
                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Ưu tiên").Bold().FontSize(Fs(7));
                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Người TH").Bold().FontSize(Fs(7));
                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Thời gian").Bold().FontSize(Fs(7));
                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Kết quả mong đợi").Bold().FontSize(Fs(7));
            });
            foreach (var item in items.Take(8))
            {
                table.Cell().Padding(4).Text(V(item.Title)).FontSize(Fs(7));
                table.Cell().Padding(4).Text(V(item.Priority)).FontSize(Fs(7));
                table.Cell().Padding(4).Text(V(item.Owner)).FontSize(Fs(7));
                table.Cell().Padding(4).Text(V(item.Timeline)).FontSize(Fs(7));
                table.Cell().Padding(4).Text(V(item.ExpectedOutcome)).FontSize(Fs(7));
            }
        });
    }

    private static void RenderAiExecutiveConclusion(ColumnDescriptor col, AssessmentReportIntelligenceDto intel)
    {
        if (!string.IsNullOrWhiteSpace(intel.AiNarrative?.AiConclusion))
        {
            col.Item().Text(V(intel.AiNarrative.AiConclusion)).FontSize(Fs(10)).LineHeight(1.45f);
        }
        else if (intel.ExecutiveSummary is not null)
        {
            col.Item().Text(V(intel.ExecutiveSummary.Conclusion ?? intel.ExecutiveSummary.Headline))
                .Bold().FontSize(Fs(11)).LineHeight(1.45f);
            col.Item().PaddingTop(8).Text("Tổng kết chiến lược").Bold().FontSize(Fs(10)).FontColor(Brand);
            foreach (var p in intel.ExecutiveSummary.Paragraphs.Take(3))
                col.Item().PaddingTop(4).Text(V(p)).FontSize(Fs(9)).LineHeight(1.4f);
        }

        if (intel.ConsultingBrief is not null && string.IsNullOrWhiteSpace(intel.AiNarrative?.AiConclusion))
        {
            col.Item().PaddingTop(10).Text("Khuyến nghị triển khai").Bold().FontSize(Fs(10)).FontColor(Brand);
            col.Item().PaddingTop(4).Text(V(intel.ConsultingBrief.RoiStory.Summary)).FontSize(Fs(9)).LineHeight(1.35f);
            col.Item().PaddingTop(4).Text(V(intel.ConsultingBrief.UrgencyStatement)).FontSize(Fs(9));
        }

        if (intel.AiNarrative?.PersonalizedInsights.Count > 0)
        {
            col.Item().PaddingTop(8).Text("Rủi ro & cơ hội then chốt").Bold().FontSize(Fs(10)).FontColor(Brand);
            foreach (var insight in intel.AiNarrative.PersonalizedInsights.Take(5))
                col.Item().PaddingTop(3).Text($"• {V(insight)}").FontSize(Fs(9)).LineHeight(1.35f);
        }

        if (intel.Swot is not null)
        {
            if (intel.Swot.Threats.Count > 0)
            {
                col.Item().PaddingTop(8).Text("Thách thức cần theo dõi").Bold().FontSize(Fs(9));
                foreach (var t in intel.Swot.Threats.Take(2))
                    col.Item().PaddingTop(2).Text($"• {V(t.Title)}: {V(t.Body)}").FontSize(Fs(8));
            }
            if (intel.Swot.Opportunities.Count > 0)
            {
                col.Item().PaddingTop(6).Text("Cơ hội then chốt").Bold().FontSize(Fs(9));
                foreach (var o in intel.Swot.Opportunities.Take(2))
                    col.Item().PaddingTop(2).Text($"• {V(o.Title)}: {V(o.Body)}").FontSize(Fs(8));
            }
        }
    }

    private static void RenderPharmacyExecutiveSummary(
        ColumnDescriptor col,
        AssessmentReportIntelligenceDto intel,
        AssessmentFullReportDto report)
    {
        if (intel.ConsultingBrief is not null)
        {
            col.Item().Background(BrandLight).Padding(12).Column(box =>
            {
                box.Item().Text(V(intel.ConsultingBrief.DiagnosisHeadline)).Bold().FontSize(Fs(11)).FontColor(Colors.Red.Darken2);
                box.Item().PaddingTop(4).Text(V(intel.ConsultingBrief.CostOfInaction)).LineHeight(1.35f).FontSize(Fs(9));
            });
        }

        col.Item().PaddingTop(10).Text(KapPharmacyLanguage.OverallSummary(report.OverallScore)).Bold().FontSize(Fs(10));

        col.Item().PaddingTop(8).Text("6 mặt hoạt động").Bold().FontSize(Fs(9)).FontColor(Brand);
        foreach (var cat in report.CategoryScores.OrderBy(c => KapPharmacyScoreDisplay.Letter(c.Code)))
            col.Item().PaddingTop(3).Text(KapPharmacyScoreDisplay.LabeledLine(cat.Code, cat.Name, cat.Score)).FontSize(Fs(9));

        if (intel.ExecutiveSummary?.Paragraphs.Count > 0)
        {
            col.Item().PaddingTop(10).Text("Tóm lại").Bold().FontSize(Fs(10)).FontColor(Brand);
            foreach (var p in intel.ExecutiveSummary.Paragraphs.Take(3))
                col.Item().PaddingTop(3).Text(V(p)).FontSize(Fs(9)).LineHeight(1.35f);
        }

        if (intel.AiNarrative?.PersonalizedInsights.Count > 0)
        {
            col.Item().PaddingTop(8).Text("Gợi ý dành riêng cho bạn").Bold().FontSize(Fs(10)).FontColor("#4f46e5");
            foreach (var insight in intel.AiNarrative.PersonalizedInsights.Take(4))
                col.Item().PaddingTop(3).Text($"• {V(insight)}").FontSize(Fs(9)).LineHeight(1.3f);
        }
    }

    private static void RenderPharmacyCategoryScores(
        ColumnDescriptor col,
        IReadOnlyList<AssessmentCategoryScoreDto> categories)
    {
        if (categories.Count == 0) return;
        foreach (var cat in categories.OrderBy(c => KapPharmacyScoreDisplay.Letter(c.Code)))
        {
            col.Item().PaddingTop(6).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
            {
                c.Item().Text(KapPharmacyScoreDisplay.LabeledLine(cat.Code, cat.Name, cat.Score)).Bold().FontSize(Fs(10));
                c.Item().PaddingTop(2).Text(KapPharmacyLanguage.BandLabelTen(cat.Score)).FontSize(Fs(8)).FontColor(TextMuted);
                c.Item().PaddingTop(2).Text(KapPharmacyLanguage.Recommendation(cat.Code)).FontSize(Fs(8)).LineHeight(1.3f);
            });
        }
    }

    private static void RenderPharmacyScoreBars(
        ColumnDescriptor col,
        IReadOnlyList<AssessmentCategoryScoreDto> categories)
    {
        if (categories.Count == 0) return;

        col.Item().PaddingTop(4).Text("Thang 10 điểm — số bên phải mỗi dòng").FontSize(Fs(8)).FontColor(TextMuted);

        foreach (var cat in categories.OrderByDescending(c => c.Score))
        {
            var ten = KapPharmacyScoreDisplay.ToTen(cat.Score);
            var scoreWeight = Math.Max(1, (int)Math.Round((double)(ten / KapPharmacyScoreDisplay.ToTen(4m) * 100m)));
            var restWeight = Math.Max(1, 100 - scoreWeight);
            var label = KapPharmacyScoreDisplay.LabeledLine(cat.Code, cat.Name, cat.Score);

            col.Item().PaddingTop(6).Row(row =>
            {
                row.ConstantItem(130).Text(label).FontSize(Fs(8));
                row.RelativeItem().Height(14).Row(barRow =>
                {
                    barRow.RelativeItem((uint)scoreWeight).Background(Brand);
                    barRow.RelativeItem((uint)restWeight);
                });
                row.ConstantItem(48).AlignRight().Text(KapPharmacyScoreDisplay.Format(cat.Score)).FontSize(Fs(8)).Bold();
            });
        }
    }

    private static byte[] GenerateLegacy(AssessmentFullReportDto report, string orgName)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.Content().Column(col =>
                {
                    col.Item().Text($"Báo cáo — {orgName}").Bold().FontSize(Fs(16));
                    col.Item().PaddingTop(8).Text($"Điểm: {report.OverallScore:F2}/4 ({report.OverallPct:F1}%)");
                    col.Item().PaddingTop(8).Text(
                        "Báo cáo tư vấn đầy đủ đang được tạo. Vui lòng tải lại sau vài phút hoặc xem bản web.")
                        .FontSize(Fs(9)).FontColor(TextMuted);
                });
            });
        }).GeneratePdf();
    }
}
