using System.Text.Json;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Survey;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.Survey.Infrastructure;

internal static class KapAnalysisPhases
{
    public const string Validation = "validation";
    public const string Scoring = "scoring";
    public const string Maturity = "maturity";
    public const string RootCause = "root_cause";
    public const string Swot = "swot";
    public const string Benchmark = "benchmark";
    public const string Trend = "trend";
    public const string Risk = "risk";
    public const string Opportunity = "opportunity";
    public const string Recommendations = "recommendations";
    public const string Roadmap = "roadmap";
    public const string Kpi = "kpi";
    public const string PriorityMatrix = "priority_matrix";
    public const string ExecutiveSummary = "executive_summary";
    public const string ConsultingNarrative = "consulting_narrative";
    public const string ConsultingExtensions = "consulting_extensions";
    public const string AiNarrative = "ai_narrative";
    public const string Appendix = "appendix";

    public static IReadOnlyList<string> ForLevel(int level) => level switch
    {
        >= 3 => Level3(),
        >= 2 => Level2(),
        _ => Level1(),
    };

    private static IReadOnlyList<string> Level1() =>
    [
        Validation, Scoring, Maturity, RootCause, Swot, Risk, Opportunity,
        Recommendations, Roadmap, Kpi, PriorityMatrix, ConsultingNarrative, ConsultingExtensions, ExecutiveSummary, Appendix,
    ];

    private static IReadOnlyList<string> Level2() =>
        Level1().Concat([Benchmark, Trend]).ToList();

    private static IReadOnlyList<string> Level3() =>
        Level2().Concat([AiNarrative]).ToList();
}

internal sealed class AssessmentAnalysisPipeline
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly AssessmentRepository _repo;
    private readonly AssessmentIntelligenceRepository _intel;
    private readonly IDbConnectionFactory _db;
    private readonly AssessmentSettings _settings;
    private readonly KapAiNarrativeService _aiNarrative;

    public AssessmentAnalysisPipeline(
        AssessmentRepository repo,
        AssessmentIntelligenceRepository intel,
        IDbConnectionFactory db,
        IOptions<AssessmentSettings> settings,
        KapAiNarrativeService aiNarrative)
    {
        _repo = repo;
        _intel = intel;
        _db = db;
        _settings = settings.Value;
        _aiNarrative = aiNarrative;
    }

    public async Task<KapReportArtifactDto> RunAsync(
        Guid submissionId,
        string triggerEvent,
        string? orgScale = null,
        CancellationToken cancellationToken = default)
    {
        var submission = await _repo.GetSubmissionAsync(submissionId, cancellationToken)
            ?? throw new AssessmentException(AssessmentErrorCodes.NotFound, "Không tìm thấy submission.", 404);

        var scoringRows = await _repo.GetScoringDataAsync(submissionId, cancellationToken);
        var scoring = AssessmentScoringEngine.Compute(scoringRows);
        var rules = await _repo.GetActiveRulesAsync(submission.TemplateId, cancellationToken);
        var responseCodes = BuildResponseCodes(scoringRows);
        var questionScores = BuildQuestionScores(scoringRows);

        var matches = AssessmentRuleEngine.Evaluate(
            rules,
            scoring.OverallScore,
            scoring.CategoryScoreByCode,
            scoring.DimensionScoreByCode,
            responseCodes,
            questionScores);

        var pipelineLevel = Math.Clamp(_settings.AnalysisPipelineLevel, 1, 3);
        var phasesRequested = KapAnalysisPhases.ForLevel(pipelineLevel);
        var phasesCompleted = new List<string>();

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var runId = await _intel.StartAnalysisRunAsync(conn, tx, submissionId, triggerEvent, pipelineLevel, phasesRequested);

        try
        {
            Guid? priorId = null;
            if (!string.IsNullOrWhiteSpace(submission.RespondentPhone))
            {
                priorId = await _intel.FindPriorSubmissionIdAsync(
                    submission.RespondentPhone!,
                    submission.TemplateId,
                    submissionId,
                    cancellationToken);
            }

            await _intel.UpsertSubmissionContextAsync(conn, tx, submissionId, "pharmacy", priorId, orgScale);

            var validation = BuildValidation(scoringRows);
            phasesCompleted.Add(KapAnalysisPhases.Validation);

            // Chỉ giữ nhóm đã được chấm. GetValueOrDefault(..., 0) biến nhóm hồ sơ
            // "Bắt đầu" (không có câu tính điểm) thành nhóm yếu nhất của báo cáo.
            var categoryScores = scoringRows
                .GroupBy(r => r.CategoryCode)
                .Where(g => scoring.CategoryScoreByCode.ContainsKey(g.Key))
                .Select(g =>
                {
                    var first = g.First();
                    var score = scoring.CategoryScoreByCode[first.CategoryCode];
                    return new AssessmentCategoryScoreDto(
                        first.CategoryCode,
                        first.CategoryName,
                        score,
                        AssessmentScoringEngine.ScoreToPct(score));
                })
                .OrderBy(c => c.Code)
                .ToList();

            var dimensionScores = scoringRows
                .GroupBy(r => r.DimensionCode)
                .Where(g => scoring.DimensionScoreByCode.ContainsKey(g.Key))
                .Select(g =>
                {
                    var first = g.First();
                    var score = scoring.DimensionScoreByCode[first.DimensionCode];
                    return new AssessmentDimensionScoreDto(
                        first.DimensionCode,
                        first.DimensionName,
                        first.CategoryCode,
                        score,
                        AssessmentScoringEngine.ScoreToPct(score));
                })
                .ToList();

            var scores = new KapReportScoresDto(
                scoring.OverallScore,
                scoring.OverallPct,
                categoryScores,
                dimensionScores,
                new AssessmentQualitativeTagsDto(
                    scoring.Qualitative.PainPointTag,
                    scoring.Qualitative.PriorityNeedTag));
            phasesCompleted.Add(KapAnalysisPhases.Scoring);

            var maturityLevels = await _intel.GetMaturityLevelsAsync(submission.TemplateId, "pharmacy", cancellationToken);
            var maturity = MapMaturity(maturityLevels, scoring.OverallScore);
            phasesCompleted.Add(KapAnalysisPhases.Maturity);

            var rootCauseKb = await _intel.GetRootCauseKbAsync(submission.TemplateId, cancellationToken);
            var rootCauses = BuildRootCauses(
                rootCauseKb,
                scoring.OverallScore,
                scoring.CategoryScoreByCode,
                scoring.DimensionScoreByCode,
                responseCodes,
                questionScores);
            phasesCompleted.Add(KapAnalysisPhases.RootCause);

            var swot = BuildSwot(matches, categoryScores);
            phasesCompleted.Add(KapAnalysisPhases.Swot);

            var risks = ExtractRisks(matches);
            phasesCompleted.Add(KapAnalysisPhases.Risk);

            var opportunities = BuildOpportunities(matches, scoring.Qualitative.PriorityNeedTag, categoryScores);
            phasesCompleted.Add(KapAnalysisPhases.Opportunity);

            var insights = ExtractInsights(matches);
            var recommendations = ExtractRecommendations(matches);
            phasesCompleted.Add(KapAnalysisPhases.Recommendations);

            var roadmap = BuildRoadmap(matches);
            phasesCompleted.Add(KapAnalysisPhases.Roadmap);

            var kpis = ExtractKpis(matches);
            phasesCompleted.Add(KapAnalysisPhases.Kpi);

            var priorityMatrix = BuildPriorityMatrix(recommendations, roadmap);
            phasesCompleted.Add(KapAnalysisPhases.PriorityMatrix);

            KapBenchmarkAnalysisDto? benchmark = null;
            KapTrendAnalysisDto? trend = null;
            if (pipelineLevel >= 2)
            {
                benchmark = await BuildBenchmarkAsync(
                    submission.TemplateId,
                    scoring.OverallScore,
                    categoryScores,
                    orgScale,
                    cancellationToken);
                if (benchmark is not null)
                    phasesCompleted.Add(KapAnalysisPhases.Benchmark);

                trend = await BuildTrendAsync(priorId, scoring.OverallScore, categoryScores, cancellationToken);
                if (trend is not null)
                    phasesCompleted.Add(KapAnalysisPhases.Trend);
            }

            var (consultingBrief, executive) = KapConsultingNarrativeBuilder.Build(
                submission.RespondentOrgName ?? submission.RespondentName ?? "Doanh nghiệp",
                orgScale,
                scores,
                maturity,
                rootCauses,
                risks,
                benchmark,
                scores.Qualitative,
                questionScores);
            phasesCompleted.Add(KapAnalysisPhases.ConsultingNarrative);

            var extensions = KapConsultingExtensionsBuilder.Build(
                submission.RespondentOrgName ?? submission.RespondentName ?? "Doanh nghiệp",
                orgScale,
                scores,
                maturity,
                rootCauses,
                risks,
                opportunities,
                consultingBrief);
            phasesCompleted.Add(KapAnalysisPhases.ConsultingExtensions);
            phasesCompleted.Add(KapAnalysisPhases.ExecutiveSummary);

            var orgName = submission.RespondentOrgName ?? submission.RespondentName ?? "Doanh nghiệp";

            KapAiNarrativeDto? aiNarrative = null;
            var engineMode = pipelineLevel >= 3 ? "hybrid" : "deterministic";
            if (pipelineLevel >= 3)
            {
                var aiContext = KapAiNarrativeContextBuilder.Build(
                    submissionId,
                    orgName,
                    orgScale,
                    scores,
                    maturity,
                    rootCauses,
                    risks,
                    opportunities,
                    benchmark,
                    scoringRows);

                var enriched = await _aiNarrative.TryEnrichAsync(
                    aiContext,
                    consultingBrief,
                    executive,
                    cancellationToken);

                if (enriched is not null)
                {
                    consultingBrief = enriched.Brief;
                    executive = enriched.Executive;
                    aiNarrative = enriched.Meta;
                    phasesCompleted.Add(KapAnalysisPhases.AiNarrative);
                    engineMode = enriched.Meta.Source.StartsWith("openai", StringComparison.OrdinalIgnoreCase)
                        ? "ai_full"
                        : "hybrid";
                }
            }

            var appendix = BuildAppendix(scoringRows);
            phasesCompleted.Add(KapAnalysisPhases.Appendix);

            if (benchmark is not null)
            {
                benchmark = benchmark with
                {
                    Tiers = KapConsultingIntelligenceBuilder.BuildBenchmarkTiers(
                        scores.OverallPct, orgScale, benchmark),
                };
            }

            var intelligence = KapConsultingIntelligenceBuilder.Build(
                orgName,
                orgScale,
                scores,
                maturity,
                rootCauses,
                risks,
                opportunities,
                benchmark,
                consultingBrief,
                extensions.NovixaReadiness,
                extensions.GapAnalysis,
                priorityMatrix);
            priorityMatrix = intelligence.PriorityMatrix;

            var pending = phasesRequested.Except(phasesCompleted).ToList();

            var artifact = new KapReportArtifactDto(
                KapReportArtifactSchema.Version,
                new KapReportMetaDto(
                    submissionId,
                    submission.TemplateCode,
                    submission.TemplateVersion,
                    submission.RespondentOrgName,
                    submission.RespondentName,
                    "pharmacy",
                    null,
                    submission.CompletedAt,
                    DateTimeOffset.UtcNow),
                validation,
                scores,
                maturity,
                swot,
                rootCauses,
                benchmark,
                trend,
                risks,
                opportunities,
                insights,
                recommendations,
                roadmap,
                kpis,
                priorityMatrix,
                executive,
                consultingBrief,
                aiNarrative,
                extensions.ExecutiveDashboard,
                extensions.NovixaReadiness,
                extensions.GapAnalysis,
                extensions.RoiMetrics,
                extensions.ModuleRecommendations,
                extensions.InvestmentPhases,
                extensions.BusinessImpactForecast,
                extensions.ModuleMappings,
                extensions.TransformationRoadmap,
                extensions.CostBenefit,
                extensions.ImplementationTimeline,
                extensions.RiskRegister,
                intelligence.TransformationReadiness,
                intelligence.CrossCategoryInsight,
                intelligence.InactionCascade,
                intelligence.ImplementationJourney,
                intelligence.WhyNovixa,
                appendix,
                new KapPipelineStatusDto(
                    KapReportArtifactSchema.PipelineVersion,
                    engineMode,
                    pipelineLevel,
                    phasesCompleted,
                    pending));

            var json = JsonSerializer.Serialize(artifact, JsonOptions);
            var artifactId = await _intel.SaveArtifactAsync(
                conn,
                tx,
                submissionId,
                json,
                KapReportArtifactSchema.PipelineVersion,
                engineMode,
                phasesCompleted,
                cancellationToken);

            await _intel.CompleteAnalysisRunAsync(
                conn,
                tx,
                runId,
                artifactId,
                phasesCompleted,
                pending.Count == 0 ? "completed" : "partial");

            await tx.CommitAsync(cancellationToken);
            return artifact;
        }
        catch (Exception ex)
        {
            await _intel.FailAnalysisRunAsync(conn, tx, runId, ex.Message);
            await tx.CommitAsync(cancellationToken);
            throw;
        }
    }

    public static KapReportArtifactDto? DeserializeArtifact(string json) =>
        JsonSerializer.Deserialize<KapReportArtifactDto>(json, JsonOptions);

    public static AssessmentReportIntelligenceDto ToIntelligenceDto(KapReportArtifactDto artifact) =>
        new(
            artifact.SchemaVersion,
            artifact.Maturity,
            artifact.Swot,
            artifact.RootCauses,
            artifact.Benchmark,
            artifact.Trend,
            artifact.Risks,
            artifact.Opportunities,
            artifact.Roadmap,
            artifact.Kpis,
            artifact.PriorityMatrix,
            artifact.ExecutiveSummary,
            artifact.ConsultingBrief,
            artifact.AiNarrative,
            artifact.ExecutiveDashboard,
            artifact.NovixaReadiness,
            artifact.GapAnalysis,
            artifact.RoiMetrics ?? [],
            artifact.ModuleRecommendations ?? [],
            artifact.InvestmentPhases ?? [],
            artifact.BusinessImpactForecast ?? [],
            artifact.ModuleMappings ?? [],
            artifact.TransformationRoadmap,
            artifact.CostBenefit,
            artifact.ImplementationTimeline,
            artifact.RiskRegister,
            artifact.Appendix,
            artifact.Pipeline,
            null,
            artifact.TransformationReadiness,
            artifact.CrossCategoryInsight,
            artifact.InactionCascade,
            artifact.ImplementationJourney,
            artifact.WhyNovixa);

    private static Dictionary<string, string> BuildResponseCodes(IReadOnlyList<AssessmentScoringRow> rows) =>
        rows
            .Where(r => !string.IsNullOrWhiteSpace(r.OptionCode))
            .GroupBy(r => r.QuestionCode)
            .ToDictionary(g => g.Key, g => g.First().OptionCode!);

    private static Dictionary<string, decimal> BuildQuestionScores(IReadOnlyList<AssessmentScoringRow> rows) =>
        rows
            .Where(r => r.Scorable && r.OptionScore.HasValue)
            .GroupBy(r => r.QuestionCode)
            .ToDictionary(g => g.Key, g => (decimal)g.First().OptionScore!.Value);

    private static KapReportValidationDto BuildValidation(IReadOnlyList<AssessmentScoringRow> rows)
    {
        var required = rows.Where(r => r.Required).GroupBy(r => r.QuestionCode).ToList();
        var missing = required.Count(g => g.All(r => !r.ResponseOptionId.HasValue && string.IsNullOrWhiteSpace(r.ResponseTextValue)));
        var warnings = new List<string>();
        if (missing > 0)
            warnings.Add($"Thiếu {missing} câu bắt buộc.");

        return new KapReportValidationDto(missing == 0, missing, warnings, []);
    }

    private static KapMaturityAssessmentDto? MapMaturity(
        IReadOnlyList<AssessmentMaturityLevelRow> levels,
        decimal overallScore)
    {
        var match = levels.FirstOrDefault(l => overallScore >= l.ScoreMin && overallScore <= l.ScoreMax)
            ?? levels.OrderByDescending(l => l.Level).FirstOrDefault();
        if (match is null)
            return null;

        return new KapMaturityAssessmentDto(
            match.Level,
            match.Code,
            match.Name,
            match.Description ?? "",
            "overall");
    }

    private static IReadOnlyList<KapRootCauseDto> BuildRootCauses(
        IReadOnlyList<AssessmentRootCauseKbRow> kb,
        decimal overallScore,
        IReadOnlyDictionary<string, decimal> categoryScores,
        IReadOnlyDictionary<string, decimal> dimensionScores,
        IReadOnlyDictionary<string, string> responseCodes,
        IReadOnlyDictionary<string, decimal> questionScores)
    {
        var results = new List<KapRootCauseDto>();
        foreach (var item in kb)
        {
            if (!AssessmentExpressionEvaluator.Evaluate(
                    item.TriggerExpression,
                    overallScore,
                    categoryScores,
                    dimensionScores,
                    responseCodes,
                    questionScores))
                continue;

            var evidence = new List<string>();
            if (!string.IsNullOrWhiteSpace(item.EvidenceHint))
                evidence.Add(item.EvidenceHint);
            if (!string.IsNullOrWhiteSpace(item.QuestionCode))
                evidence.Add($"Mã câu: {item.QuestionCode}");

            results.Add(new KapRootCauseDto(
                item.CauseCode,
                item.CategoryCode ?? "",
                item.CauseTitle,
                item.CauseBody,
                evidence));
        }

        return results;
    }

    private static KapSwotAnalysisDto BuildSwot(
        IReadOnlyList<AssessmentRuleEngine.Match> matches,
        IReadOnlyList<AssessmentCategoryScoreDto> categories)
    {
        var strengths = matches
            .Where(m => string.Equals(m.Rule.ActionType, "swot_strength", StringComparison.OrdinalIgnoreCase))
            .Select(m => PayloadSwot(m, null))
            .ToList();

        var weaknesses = matches
            .Where(m => string.Equals(m.Rule.ActionType, "swot_weakness", StringComparison.OrdinalIgnoreCase))
            .Select(m => PayloadSwot(m, null))
            .ToList();

        if (strengths.Count == 0)
        {
            foreach (var cat in categories.OrderByDescending(c => c.Score).Take(2).Where(c => c.Score >= 3m))
                strengths.Add(new KapSwotItemDto($"Điểm mạnh: {cat.Name}", $"Nhóm {cat.Name} đạt {cat.Score:F2}/4 ({cat.ScorePct:F1}%).", cat.Code));
        }

        if (weaknesses.Count == 0)
        {
            foreach (var cat in categories.OrderBy(c => c.Score).Take(2).Where(c => c.Score < 2.8m))
            {
                weaknesses.Add(new KapSwotItemDto(
                    $"Điểm yếu: {cat.Name}",
                    $"Nhóm {cat.Name} ({cat.ScorePct:F0}%) đang kéo hiệu quả tổng thể xuống — cần số hóa quy trình, không chỉ tăng cường nhân sự.",
                    cat.Code));
            }
        }

        var opportunities = matches
            .Where(m => string.Equals(m.Rule.ActionType, "swot_opportunity", StringComparison.OrdinalIgnoreCase))
            .Select(m => PayloadSwot(m, null))
            .ToList();

        var threats = matches
            .Where(m => string.Equals(m.Rule.ActionType, "swot_threat", StringComparison.OrdinalIgnoreCase))
            .Select(m => PayloadSwot(m, null))
            .ToList();

        if (threats.Count == 0)
        {
            foreach (var cat in categories.OrderBy(c => c.Score).Take(1).Where(c => c.Score < 2.5m))
            {
                threats.Add(new KapSwotItemDto(
                    "Áp lực cạnh tranh",
                    $"Đối thủ cùng khu vực đã số hóa — khách hàng kỳ vọng tra cứu tồn nhanh, nhắc thuốc và tích điểm. {cat.Name} yếu sẽ lộ ra khi khách so sánh.",
                    cat.Code));
            }
        }

        return new KapSwotAnalysisDto(strengths, weaknesses, opportunities, threats);
    }

    private static KapSwotItemDto PayloadSwot(AssessmentRuleEngine.Match match, string? area)
    {
        var root = match.Payload.RootElement;
        var title = root.TryGetProperty("title", out var t) ? t.GetString() ?? match.Rule.Code : match.Rule.Code;
        var body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
        var a = root.TryGetProperty("area", out var ar) ? ar.GetString() : area;
        return new KapSwotItemDto(title, body, a);
    }

    private static IReadOnlyList<KapRiskItemDto> ExtractRisks(IReadOnlyList<AssessmentRuleEngine.Match> matches) =>
        matches
            .Where(m => string.Equals(m.Rule.ActionType, "risk", StringComparison.OrdinalIgnoreCase))
            .Select(m =>
            {
                var root = m.Payload.RootElement;
                return new KapRiskItemDto(
                    root.TryGetProperty("area", out var a) ? a.GetString() ?? "" : "",
                    root.TryGetProperty("level", out var l) ? l.GetString() ?? "medium" : "medium",
                    root.TryGetProperty("title", out var t) ? t.GetString() ?? m.Rule.Code : m.Rule.Code,
                    root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "");
            })
            .ToList();

    private static IReadOnlyList<KapOpportunityItemDto> BuildOpportunities(
        IReadOnlyList<AssessmentRuleEngine.Match> matches,
        string? priorityNeedTag,
        IReadOnlyList<AssessmentCategoryScoreDto> categories)
    {
        var list = matches
            .Where(m => string.Equals(m.Rule.ActionType, "opportunity", StringComparison.OrdinalIgnoreCase))
            .Select(m =>
            {
                var root = m.Payload.RootElement;
                return new KapOpportunityItemDto(
                    root.TryGetProperty("area", out var a) ? a.GetString() ?? "" : "",
                    root.TryGetProperty("title", out var t) ? t.GetString() ?? m.Rule.Code : m.Rule.Code,
                    root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "",
                    root.TryGetProperty("impactHint", out var h) ? h.GetString() : null);
            })
            .ToList();

        if (!string.IsNullOrWhiteSpace(priorityNeedTag))
        {
            list.Add(new KapOpportunityItemDto(
                "GROWTH",
                "Ưu tiên cải thiện theo phản hồi",
                $"Khách hàng ưu tiên: {priorityNeedTag}",
                "impact_high"));
        }

        foreach (var cat in categories.Where(c => c.Score >= 2.5m && c.Score < 3.5m).Take(2))
        {
            list.Add(new KapOpportunityItemDto(
                cat.Code,
                $"Thắng nhanh: {cat.Name}",
                $"Nhóm {cat.Name} gần đạt chuẩn ({cat.ScorePct:F0}%) — triển khai 1 module Novixa có thể đẩy lên mức tốt trong 30–45 ngày.",
                "quick_gain"));
        }

        return list;
    }

    private static IReadOnlyList<AssessmentInsightDto> ExtractInsights(IReadOnlyList<AssessmentRuleEngine.Match> matches) =>
        matches
            .Where(m => string.Equals(m.Rule.ActionType, "insight", StringComparison.OrdinalIgnoreCase))
            .Select(m =>
            {
                var root = m.Payload.RootElement;
                return new AssessmentInsightDto(
                    root.TryGetProperty("title", out var t) ? t.GetString() ?? m.Rule.Code : m.Rule.Code,
                    root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "",
                    root.TryGetProperty("severity", out var s) ? s.GetString() ?? "info" : "info");
            })
            .ToList();

    private static IReadOnlyList<AssessmentRecommendationDto> ExtractRecommendations(
        IReadOnlyList<AssessmentRuleEngine.Match> matches) =>
        matches
            .Where(m => string.Equals(m.Rule.ActionType, "recommendation", StringComparison.OrdinalIgnoreCase))
            .Select((m, i) =>
            {
                var root = m.Payload.RootElement;
                return new AssessmentRecommendationDto(
                    root.TryGetProperty("title", out var t) ? t.GetString() ?? m.Rule.Code : m.Rule.Code,
                    root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "",
                    m.Rule.Priority,
                    root.TryGetProperty("productArea", out var p) ? p.GetString() : null,
                    root.TryGetProperty("estimateHint", out var h) ? h.GetString() : null);
            })
            .ToList();

    private static KapRoadmapDto BuildRoadmap(IReadOnlyList<AssessmentRuleEngine.Match> matches)
    {
        var items = matches
            .Where(m => string.Equals(m.Rule.ActionType, "roadmap_item", StringComparison.OrdinalIgnoreCase))
            .Select(m =>
            {
                var root = m.Payload.RootElement;
                var days = root.TryGetProperty("horizonDays", out var d) && d.TryGetInt32(out var v) ? v : 30;
                return new KapRoadmapItemDto(
                    days,
                    root.TryGetProperty("title", out var t) ? t.GetString() ?? m.Rule.Code : m.Rule.Code,
                    root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "");
            })
            .ToList();

        return new KapRoadmapDto(
            items.Where(i => i.HorizonDays <= 30).ToList(),
            items.Where(i => i.HorizonDays is > 30 and <= 60).ToList(),
            items.Where(i => i.HorizonDays is > 60 and <= 90).ToList(),
            items.Where(i => i.HorizonDays > 90).ToList());
    }

    private static IReadOnlyList<KapKpiRecommendationDto> ExtractKpis(IReadOnlyList<AssessmentRuleEngine.Match> matches) =>
        matches
            .Where(m => string.Equals(m.Rule.ActionType, "kpi", StringComparison.OrdinalIgnoreCase))
            .Select(m =>
            {
                var root = m.Payload.RootElement;
                var deadline = root.TryGetProperty("deadlineDays", out var d) && d.TryGetInt32(out var v) ? v : 90;
                return new KapKpiRecommendationDto(
                    root.TryGetProperty("name", out var n) ? n.GetString() ?? m.Rule.Code : m.Rule.Code,
                    root.TryGetProperty("target", out var t) ? t.GetString() ?? "" : "",
                    deadline,
                    root.TryGetProperty("area", out var a) ? a.GetString() : null);
            })
            .ToList();

    private static KapPriorityMatrixDto BuildPriorityMatrix(
        IReadOnlyList<AssessmentRecommendationDto> recommendations,
        KapRoadmapDto? roadmap)
    {
        var high = recommendations
            .Where(r => r.Priority >= 75)
            .Select(r => new KapPriorityItemDto(r.Title, r.Body, "high_impact_high_priority", r.Priority))
            .ToList();

        var quick = (roadmap?.Days30 ?? [])
            .Select(r => new KapPriorityItemDto(r.Title, r.Body, "quick_win", 70))
            .ToList();

        var longTerm = (roadmap?.Days90 ?? [])
            .Concat(roadmap?.Days180 ?? [])
            .Select(r => new KapPriorityItemDto(r.Title, r.Body, "long_term", 50))
            .ToList();

        var optional = recommendations
            .Where(r => r.Priority < 60)
            .Select(r => new KapPriorityItemDto(r.Title, r.Body, "optional", r.Priority))
            .ToList();

        return new KapPriorityMatrixDto(high, quick, longTerm, optional);
    }

    private async Task<KapBenchmarkAnalysisDto?> BuildBenchmarkAsync(
        Guid templateId,
        decimal overallScore,
        IReadOnlyList<AssessmentCategoryScoreDto> categories,
        string? orgScale,
        CancellationToken cancellationToken)
    {
        var cohort = await _intel.GetBenchmarkCohortByScaleAsync(templateId, orgScale, cancellationToken);
        if (cohort is null)
            return null;

        using var doc = JsonDocument.Parse(cohort.StatsJson);
        var root = doc.RootElement;
        decimal? overallMean = null;
        if (root.TryGetProperty("overall", out var overall) && overall.TryGetProperty("mean", out var mean))
            overallMean = mean.GetDecimal();

        var catStats = new List<KapBenchmarkCategoryDto>();
        if (root.TryGetProperty("categories", out var cats))
        {
            foreach (var cat in categories)
            {
                if (!cats.TryGetProperty(cat.Code, out var stat))
                    continue;
                var cohortMean = stat.TryGetProperty("mean", out var m) ? m.GetDecimal() : (decimal?)null;
                catStats.Add(new KapBenchmarkCategoryDto(
                    cat.Code,
                    cat.Name,
                    cat.Score,
                    cohortMean,
                    cohortMean.HasValue ? cat.Score - cohortMean.Value : null));
            }
        }

        var narrative = cohort.SampleSize >= 100
            ? $"So sánh với {cohort.SampleSize} nhà thuốc cùng nhóm (dữ liệu ẩn danh)."
            : cohort.SampleSize >= _settings.BenchmarkAggregateMinSampleSize
                ? $"So sánh với {cohort.SampleSize} mẫu tham chiếu Novixa (đang mở rộng nhóm tham chiếu)."
                : "So sánh với ngưỡng tham chiếu Novixa (nhóm tham chiếu đang tích lũy dữ liệu).";

        var delta = overallMean.HasValue ? overallScore - overallMean.Value : (decimal?)null;
        var (percentile, percentileLabel) = EstimatePercentile(delta);

        return new KapBenchmarkAnalysisDto(
            cohort.CohortCode,
            cohort.SampleSize,
            delta,
            catStats,
            narrative,
            percentile,
            percentileLabel);
    }

    private static (decimal? Percentile, string? Label) EstimatePercentile(decimal? delta)
    {
        if (delta is null) return (null, null);
        return delta.Value switch
        {
            >= 0.5m => (90m, "Top 10% — nhóm dẫn đầu"),
            >= 0.25m => (75m, "Top 25% — trên trung bình ngành"),
            >= 0m => (60m, "Trên trung bình nhóm tham chiếu"),
            >= -0.25m => (40m, "Dưới trung bình — cần cải thiện"),
            _ => (20m, "Bottom 25% — ưu tiên hành động ngay"),
        };
    }

    private async Task<KapTrendAnalysisDto?> BuildTrendAsync(
        Guid? priorSubmissionId,
        decimal overallScore,
        IReadOnlyList<AssessmentCategoryScoreDto> categories,
        CancellationToken cancellationToken)
    {
        if (!priorSubmissionId.HasValue)
            return new KapTrendAnalysisDto(false, null, null, "no_prior", []);

        var priorScores = await _repo.GetCategoryScoresAsync(priorSubmissionId.Value, cancellationToken);
        var priorSubmission = await _repo.GetSubmissionAsync(priorSubmissionId.Value, cancellationToken);
        var priorOverall = priorSubmission?.OverallScore;
        var delta = priorOverall.HasValue ? overallScore - priorOverall.Value : (decimal?)null;
        var label = delta switch
        {
            null => "unknown",
            > 0.15m => "improving",
            < -0.15m => "declining",
            _ => "stable",
        };

        var trendCats = categories
            .Select(c =>
            {
                var prior = priorScores.FirstOrDefault(p => p.Code == c.Code);
                var d = prior is null ? 0m : c.Score - prior.Score;
                return new KapTrendCategoryDto(c.Code, d, d > 0 ? "up" : d < 0 ? "down" : "flat");
            })
            .ToList();

        return new KapTrendAnalysisDto(true, priorSubmissionId, delta, label, trendCats);
    }

    private static KapReportAppendixDto BuildAppendix(IReadOnlyList<AssessmentScoringRow> rows) =>
        new(rows
            .GroupBy(r => r.QuestionCode)
            .Select(g =>
            {
                var first = g.First();
                return new KapAppendixQuestionDto(
                    first.QuestionCode,
                    first.QuestionTitle,
                    first.CategoryCode,
                    first.OptionLabel,
                    first.Scorable && first.OptionScore.HasValue ? first.OptionScore : null,
                    first.Scorable);
            })
            .OrderBy(q => q.Code)
            .ToList());
}
