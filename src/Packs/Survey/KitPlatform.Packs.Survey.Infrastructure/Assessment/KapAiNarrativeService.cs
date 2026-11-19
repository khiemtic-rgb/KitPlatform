using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Survey;

namespace KitPlatform.Packs.Survey.Infrastructure;

internal sealed record KapAiNarrativeResult(
    KapConsultingBriefDto Brief,
    KapExecutiveSummaryDto Executive,
    KapAiNarrativeDto Meta);

internal sealed class KapAiNarrativeService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly AssessmentSettings _settings;
    private readonly ILogger<KapAiNarrativeService> _logger;

    public KapAiNarrativeService(
        HttpClient http,
        IOptions<AssessmentSettings> settings,
        ILogger<KapAiNarrativeService> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<KapAiNarrativeResult?> TryEnrichAsync(
        KapAiNarrativeContext context,
        KapConsultingBriefDto brief,
        KapExecutiveSummaryDto executive,
        CancellationToken cancellationToken)
    {
        var ai = _settings.AiNarrative;
        if (!ai.Enabled)
            return null;

        if (!string.IsNullOrWhiteSpace(ai.ApiKey))
        {
            try
            {
                var llm = await CallLlmAsync(context, brief, executive, cancellationToken);
                if (llm is not null)
                    return llm;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "KAP AI narrative LLM failed for submission {SubmissionId} — dùng ROI theo quy mô", context.SubmissionId);
            }
        }
        else
        {
            _logger.LogInformation("KAP AI ApiKey chưa cấu hình — dùng cá nhân hóa + ROI theo quy mô cho {SubmissionId}", context.SubmissionId);
        }

        if (!ai.FallbackPersonalization)
            return null;

        return BuildPersonalizedFallback(context, brief, executive);
    }

    private async Task<KapAiNarrativeResult?> CallLlmAsync(
        KapAiNarrativeContext context,
        KapConsultingBriefDto brief,
        KapExecutiveSummaryDto executive,
        CancellationToken cancellationToken)
    {
        var ai = _settings.AiNarrative;
        var userPayload = JsonSerializer.Serialize(context, JsonOptions);
        var deterministic = JsonSerializer.Serialize(new
        {
            brief.DiagnosisHeadline,
            brief.CostOfInaction,
            brief.BusinessImpacts,
            brief.ModuleFits,
            brief.RoiStory,
            brief.UrgencyStatement,
            brief.NextStepCta,
            executive.Headline,
            executive.Paragraphs,
        }, JsonOptions);

        var requestBody = new
        {
            model = ai.Model,
            temperature = 0.65,
            max_tokens = ai.MaxTokens,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new
                {
                    role = "user",
                    content = $"""
                        Dữ liệu khảo sát (JSON):
                        {userPayload}

                        Bản nháp quy tắc (JSON):
                        {deterministic}

                        Viết lại báo cáo tư vấn thuyết phục — suy luận chéo nhóm, có hồn, ít quảng cáo Novixa.
                        Trả về JSON đúng cấu trúc đã mô tả.
                        """,
                },
            },
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{ai.BaseUrl.TrimEnd('/')}/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ai.ApiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(ai.TimeoutSeconds));

        using var res = await _http.SendAsync(req, cts.Token);
        var body = await res.Content.ReadAsStringAsync(cts.Token);
        if (!res.IsSuccessStatusCode)
        {
            _logger.LogWarning("KAP AI HTTP {Status}: {Body}", (int)res.StatusCode, body.Length > 300 ? body[..300] : body);
            return null;
        }

        using var doc = JsonDocument.Parse(body);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
            return null;

        var parsed = JsonSerializer.Deserialize<LlmNarrativeResponse>(content, JsonOptions);
        if (parsed is null)
            return null;

        return MapLlmResult(parsed, ai.Model, brief, executive);
    }

    private static KapAiNarrativeResult MapLlmResult(
        LlmNarrativeResponse parsed,
        string model,
        KapConsultingBriefDto brief,
        KapExecutiveSummaryDto executive)
    {
        var enrichedBrief = brief with
        {
            DiagnosisHeadline = Coalesce(parsed.DiagnosisHeadline, brief.DiagnosisHeadline),
            CostOfInaction = Coalesce(parsed.CostOfInaction, brief.CostOfInaction),
            BusinessImpacts = MapImpacts(parsed.BusinessImpacts, brief.BusinessImpacts),
            ModuleFits = MapModules(parsed.ModuleFits, brief.ModuleFits),
            RoiStory = parsed.RoiStory is null
                ? brief.RoiStory
                : brief.RoiStory with
                {
                    Summary = Coalesce(parsed.RoiStory.Summary, brief.RoiStory.Summary),
                    BeforeState = parsed.RoiStory.BeforeState?.Count > 0 ? parsed.RoiStory.BeforeState : brief.RoiStory.BeforeState,
                    AfterState = parsed.RoiStory.AfterState?.Count > 0 ? parsed.RoiStory.AfterState : brief.RoiStory.AfterState,
                },
            UrgencyStatement = Coalesce(parsed.UrgencyStatement, brief.UrgencyStatement),
            NextStepCta = Coalesce(parsed.NextStepCta, brief.NextStepCta),
        };

        var enrichedExec = executive with
        {
            Headline = Coalesce(parsed.ExecutiveHeadline, executive.Headline),
            Paragraphs = parsed.ExecutiveParagraphs?.Count > 0 ? parsed.ExecutiveParagraphs : executive.Paragraphs,
            Source = "ai_enriched_v1",
            OpeningContext = Coalesce(parsed.OpeningContext, executive.OpeningContext),
            Analysis = Coalesce(parsed.Analysis, executive.Analysis),
            Assessment = Coalesce(parsed.Assessment, executive.Assessment),
            Conclusion = Coalesce(parsed.Conclusion, executive.Conclusion),
            Recommendations = Coalesce(parsed.Recommendations, executive.Recommendations),
        };

        var meta = new KapAiNarrativeDto("openai_v1", model, DateTimeOffset.UtcNow,
            parsed.PersonalizedInsights?.Select(KapVietnameseText.Polish).ToList() ?? [],
            KapVietnameseText.Polish(parsed.AiConclusion));

        return new KapAiNarrativeResult(enrichedBrief, enrichedExec, meta);
    }

    private static KapAiNarrativeResult BuildPersonalizedFallback(
        KapAiNarrativeContext context,
        KapConsultingBriefDto brief,
        KapExecutiveSummaryDto executive)
    {
        var insights = new List<string>();
        foreach (var q in context.WeakAnswers.Take(4))
        {
            insights.Add(
                $"«{q.QuestionTitle}»: Bạn trả lời «{q.AnswerLabel}» — "
                + $"cho thấy {KapVietnameseText.Display(q.CategoryName)} cần cải thiện, không chỉ làm thủ công.");
        }

        if (!string.IsNullOrWhiteSpace(context.PainPointLabel))
            insights.Insert(0, $"Bạn xác nhận trở ngại lớn nhất là «{context.PainPointLabel}» — mọi giải pháp cần xoay quanh vấn đề này trước.");

        var headline = insights.Count > 0
            ? $"{context.OrgName}: {insights[0].Split('—')[0].Trim()}"
            : brief.DiagnosisHeadline;

        var extraParagraph = insights.Count > 1
            ? string.Join(" ", insights.Skip(1).Take(2))
            : null;

        var paragraphs = new List<string>(executive.Paragraphs);
        if (!string.IsNullOrWhiteSpace(extraParagraph))
            paragraphs.Insert(1, extraParagraph);

        var enrichedBrief = brief with
        {
            DiagnosisHeadline = headline.Length > 20 ? headline : brief.DiagnosisHeadline,
        };

        var categories = context.Categories
            .Select(c => new AssessmentCategoryScoreDto(c.Code, c.Name, c.Score, c.ScorePct))
            .ToList();
        if (categories.Count > 0)
        {
            var scores = new KapReportScoresDto(
                context.OverallScore,
                context.OverallPct,
                categories,
                [],
                new AssessmentQualitativeTagsDto(null, null));
            var weak = categories.OrderBy(c => c.Score).Take(3).ToList();
            enrichedBrief = KapScaleBasedRoiBuilder.ApplyScaleRoi(
                enrichedBrief, context.OrgName, context.OrgScale, scores, null, weak, enrichedBrief.ModuleFits);
        }

        var enrichedExec = executive with
        {
            Paragraphs = paragraphs,
            Source = "personalized_v1",
        };

        var meta = new KapAiNarrativeDto("personalized_v1", null, DateTimeOffset.UtcNow, insights);
        return new KapAiNarrativeResult(enrichedBrief, enrichedExec, meta);
    }

    private static IReadOnlyList<KapBusinessImpactDto> MapImpacts(
        List<LlmImpact>? llm,
        IReadOnlyList<KapBusinessImpactDto> fallback) =>
        llm?.Count > 0
            ? llm.Select(i => new KapBusinessImpactDto(
                i.Area ?? "GENERAL",
                i.Title ?? "",
                i.ImpactStatement ?? "",
                i.CostHint ?? "")).ToList()
            : fallback;

    private static IReadOnlyList<KapSoftwareModuleFitDto> MapModules(
        List<LlmModule>? llm,
        IReadOnlyList<KapSoftwareModuleFitDto> fallback) =>
        llm?.Count > 0
            ? llm.Select((m, idx) => new KapSoftwareModuleFitDto(
                m.ModuleName ?? "",
                m.PainResolved ?? "",
                m.Outcome30Days ?? "",
                m.Outcome90Days ?? "",
                90 - idx * 10)).ToList()
            : fallback;

    private static string Coalesce(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private const string SystemPrompt = """
        Bạn là chuyên gia tư vấn quản trị & chuyển đổi số cho nhà thuốc Việt Nam — văn phong Deloitte/PwC, không phải chatbot.
        Viết cho CHỦ NHÀ THUỐC và ban lãnh đạo.

        TƯ DUY BẮT BUỘC (AI REASONING):
        - SUY LUẬN, không mô tả: liên kết ít nhất 2 nhóm điểm yếu/mạnh, giải thích nguyên nhân gốc và hệ quả kinh doanh.
        - Ví dụ ĐÚNG: «Điểm Vận hành thấp kết hợp Dữ liệu thấp cho thấy phụ thuộc kinh nghiệm cá nhân — nếu chủ vắng 3 ngày, sai lệch tồn tăng.»
        - Ví dụ SAI: «Nhà thuốc chưa dùng AI.» (chỉ mô tả, không phân tích)
        - Trích ≥2 câu trả lời cụ thể từ weakAnswers; dùng benchmarkSummary và crossCategoryHints nếu có.

        TƯ VẤN (CONSULTING TONE):
        - Mở đầu kiểu: «Sau khi phân tích toàn bộ dữ liệu khảo sát, AI nhận thấy [tên NT] không gặp khó khăn về bán hàng mà gặp khó khăn về [mở rộng/quản trị]…»
        - Kết luận aiConclusion: 2–3 đoạn (~400–600 từ), lập luận có chiều sâu, đọc như báo cáo tư vấn — không lặp costOfInaction.
        - Ít nhắc thương hiệu Novixa trực tiếp: trước hết nêu nhu cầu nền tảng quản trị, sau đó mới gợi ý Novixa trong hệ sinh thái KIT Technology.

        QUY TẮC KHÁC:
        - Tiếng Việt có dấu; KPI ước lượng ghi «ước tính».
        - KHÔNG nêu giá phần mềm, hoàn vốn, thu hồi chi phí.
        - Không lặp cụm từ; tránh «Chuẩn hóa ↓ Chuẩn hóa», «ROI (ROI)», «POS (POS)».
        - costOfInaction: dạng cascade thời gian (3 tháng → 6 tháng → 12 tháng), không copy executive conclusion.
        - Kết thúc mời liên hệ tư vấn lộ trình — không ép báo giá.

        Trả JSON duy nhất:
        {
          "diagnosisHeadline": "string — insight, không phải điểm số",
          "costOfInaction": "string — kịch bản nếu không hành động",
          "executiveHeadline": "string",
          "openingContext": "1.1 Mở vấn đề — có hồn, cá nhân hóa, không liệt kê điểm đầu",
          "analysis": "1.2 Suy luận chéo nhóm — nguyên nhân gốc, hệ quả",
          "assessment": "1.3 Trưởng thành, sẵn sàng CĐS, mở rộng",
          "conclusion": "1.4 Kết luận — không lặp costOfInaction",
          "recommendations": "1.5 Việc ngay / sau / định hướng CĐS",
          "executiveParagraphs": ["string"],
          "businessImpacts": [{"area","title","impactStatement","costHint"}],
          "moduleFits": [{"moduleName","painResolved","outcome30Days","outcome90Days"}],
          "roiStory": {"summary","beforeState":["string"],"afterState":["string"]},
          "urgencyStatement": "string",
          "nextStepCta": "string — khách quan, không quảng cáo",
          "personalizedInsights": ["string — suy luận, không mô tả template"],
          "aiConclusion": "2–3 đoạn kết luận điều hành kiểu Big4 — tổng kết, rủi ro, cơ hội, khuyến nghị triển khai"
        }
        """;

    private sealed class LlmNarrativeResponse
    {
        public string? DiagnosisHeadline { get; set; }
        public string? CostOfInaction { get; set; }
        public string? ExecutiveHeadline { get; set; }
        public string? OpeningContext { get; set; }
        public string? Analysis { get; set; }
        public string? Assessment { get; set; }
        public string? Conclusion { get; set; }
        public string? Recommendations { get; set; }
        public List<string>? ExecutiveParagraphs { get; set; }
        public List<LlmImpact>? BusinessImpacts { get; set; }
        public List<LlmModule>? ModuleFits { get; set; }
        public LlmRoi? RoiStory { get; set; }
        public string? UrgencyStatement { get; set; }
        public string? NextStepCta { get; set; }
        public List<string>? PersonalizedInsights { get; set; }
        public string? AiConclusion { get; set; }
    }

    private sealed class LlmImpact
    {
        public string? Area { get; set; }
        public string? Title { get; set; }
        public string? ImpactStatement { get; set; }
        public string? CostHint { get; set; }
    }

    private sealed class LlmModule
    {
        public string? ModuleName { get; set; }
        public string? PainResolved { get; set; }
        public string? Outcome30Days { get; set; }
        public string? Outcome90Days { get; set; }
    }

    private sealed class LlmRoi
    {
        public string? Summary { get; set; }
        public List<string>? BeforeState { get; set; }
        public List<string>? AfterState { get; set; }
    }
}

internal sealed class KapAiNarrativeContext
{
    public Guid SubmissionId { get; init; }
    public string OrgName { get; init; } = "";
    public string? OrgScale { get; init; }
    public decimal OverallScore { get; init; }
    public decimal OverallPct { get; init; }
    public string? MaturityName { get; init; }
    public string? PainPointLabel { get; init; }
    public string? PriorityNeedLabel { get; init; }
    public IReadOnlyList<KapAiWeakAnswer> WeakAnswers { get; init; } = [];
    public IReadOnlyList<KapAiCategorySnapshot> Categories { get; init; } = [];
    public IReadOnlyList<KapAiRootCauseSnapshot> RootCauses { get; init; } = [];
    public string? BenchmarkSummary { get; init; }
    public IReadOnlyList<string> RiskTitles { get; init; } = [];
    public IReadOnlyList<string> OpportunityTitles { get; init; } = [];
    public IReadOnlyList<string> CrossCategoryHints { get; init; } = [];
}

internal sealed record KapAiWeakAnswer(
    string QuestionCode,
    string QuestionTitle,
    string AnswerLabel,
    string CategoryName,
    decimal Score);

internal sealed record KapAiCategorySnapshot(string Code, string Name, decimal Score, decimal ScorePct);

internal sealed record KapAiRootCauseSnapshot(string Title, string Body);

internal static class KapAiNarrativeContextBuilder
{
    private static readonly Dictionary<string, string> TagLabels = new(StringComparer.OrdinalIgnoreCase)
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

    public static KapAiNarrativeContext Build(
        Guid submissionId,
        string orgName,
        string? orgScale,
        KapReportScoresDto scores,
        KapMaturityAssessmentDto? maturity,
        IReadOnlyList<KapRootCauseDto> rootCauses,
        IReadOnlyList<KapRiskItemDto> risks,
        IReadOnlyList<KapOpportunityItemDto> opportunities,
        KapBenchmarkAnalysisDto? benchmark,
        IReadOnlyList<AssessmentScoringRow> scoringRows)
    {
        var weakAnswers = scoringRows
            .Where(r => r.Scorable && r.OptionScore.HasValue && r.OptionScore <= 2)
            .GroupBy(r => r.QuestionCode)
            .Select(g =>
            {
                var r = g.First();
                return new KapAiWeakAnswer(
                    r.QuestionCode,
                    r.QuestionTitle,
                    r.OptionLabel ?? r.ResponseTextValue ?? "—",
                    r.CategoryName,
                    r.OptionScore ?? 0);
            })
            .OrderBy(a => a.Score)
            .Take(8)
            .ToList();

        var weak = scores.Categories.OrderBy(c => c.Score).Take(2).ToList();
        var hints = new List<string>();
        if (weak.Count >= 2)
        {
            hints.Add(
                $"Liên kết {weak[0].Name} ({weak[0].ScorePct:F0}%) + {weak[1].Name} ({weak[1].ScorePct:F0}%): phụ thuộc kinh nghiệm cá nhân, rủi ro khi chủ vắng mặt.");
        }

        var benchSummary = benchmark is null
            ? null
            : $"{benchmark.Narrative} · {benchmark.PercentileLabel} · chênh lệch tổng {benchmark.OverallVsMean:+#.##;-#.##;0} điểm";

        return new KapAiNarrativeContext
        {
            SubmissionId = submissionId,
            OrgName = orgName,
            OrgScale = orgScale,
            OverallScore = scores.OverallScore,
            OverallPct = scores.OverallPct,
            MaturityName = maturity?.Name,
            PainPointLabel = TagLabel(scores.Qualitative.PainPoint),
            PriorityNeedLabel = TagLabel(scores.Qualitative.PriorityNeed),
            WeakAnswers = weakAnswers,
            Categories = scores.Categories
                .Select(c => new KapAiCategorySnapshot(c.Code, c.Name, c.Score, c.ScorePct))
                .ToList(),
            RootCauses = rootCauses
                .Select(r => new KapAiRootCauseSnapshot(r.Title, r.Body))
                .ToList(),
            BenchmarkSummary = benchSummary,
            RiskTitles = risks.Select(r => r.Title).Take(4).ToList(),
            OpportunityTitles = opportunities.Select(o => o.Title).Take(4).ToList(),
            CrossCategoryHints = hints,
        };
    }

    private static string? TagLabel(string? tag) =>
        tag is null ? null : TagLabels.GetValueOrDefault(tag, tag.Replace('_', ' '));
}
