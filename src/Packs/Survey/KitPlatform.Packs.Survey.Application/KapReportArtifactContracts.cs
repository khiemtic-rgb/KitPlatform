namespace KitPlatform.Packs.Survey;

/// <summary>
/// Canonical KAP report artifact (schema v1.0) — single source for web, PDF, CRM.
/// Phases populate sections incrementally; unset sections remain null/empty.
/// </summary>
public static class KapReportArtifactSchema
{
    public const string Version = "1.4";
    public const string PipelineVersion = "1.4";
}

public enum KapReportPdfKind
{
    Executive,
    Consulting,
    Appendix,
    /// <summary>Short owner-facing pack (plain language, pain-led, Pilot hinge).</summary>
    Owner,
}

public sealed record KapReportArtifactDto(
    string SchemaVersion,
    KapReportMetaDto Meta,
    KapReportValidationDto? Validation,
    KapReportScoresDto Scores,
    KapMaturityAssessmentDto? Maturity,
    KapSwotAnalysisDto? Swot,
    IReadOnlyList<KapRootCauseDto> RootCauses,
    KapBenchmarkAnalysisDto? Benchmark,
    KapTrendAnalysisDto? Trend,
    IReadOnlyList<KapRiskItemDto> Risks,
    IReadOnlyList<KapOpportunityItemDto> Opportunities,
    IReadOnlyList<AssessmentInsightDto> Insights,
    IReadOnlyList<AssessmentRecommendationDto> Recommendations,
    KapRoadmapDto? Roadmap,
    IReadOnlyList<KapKpiRecommendationDto> Kpis,
    KapPriorityMatrixDto? PriorityMatrix,
    KapExecutiveSummaryDto? ExecutiveSummary,
    KapConsultingBriefDto? ConsultingBrief,
    KapAiNarrativeDto? AiNarrative,
    KapExecutiveDashboardDto? ExecutiveDashboard,
    KapNovixaReadinessDto? NovixaReadiness,
    KapGapAnalysisDto? GapAnalysis,
    IReadOnlyList<KapRoiMetricDto>? RoiMetrics,
    IReadOnlyList<KapModuleStarDto>? ModuleRecommendations,
    IReadOnlyList<KapInvestmentPhaseDto>? InvestmentPhases,
    IReadOnlyList<KapBusinessImpactForecastDto>? BusinessImpactForecast,
    IReadOnlyList<KapModuleMappingItemDto>? ModuleMappings,
    KapTransformationRoadmapDto? TransformationRoadmap,
    KapCostBenefitAnalysisDto? CostBenefit,
    KapImplementationTimelineDto? ImplementationTimeline,
    KapRiskRegisterDto? RiskRegister,
    KapTransformationReadinessDto? TransformationReadiness,
    KapCrossCategoryInsightDto? CrossCategoryInsight,
    KapNarrativeCascadeDto? InactionCascade,
    KapNarrativeCascadeDto? ImplementationJourney,
    KapWhyNovixaDto? WhyNovixa,
    KapReportAppendixDto? Appendix,
    KapPipelineStatusDto Pipeline);

public sealed record KapReportMetaDto(
    Guid SubmissionId,
    string TemplateCode,
    string TemplateVersion,
    string? OrgName,
    string? RespondentName,
    string? VerticalCode,
    string? OrgScale,
    DateTimeOffset? CompletedAt,
    DateTimeOffset GeneratedAt);

public sealed record KapReportValidationDto(
    bool IsComplete,
    int MissingRequiredCount,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Contradictions);

public sealed record KapReportScoresDto(
    decimal OverallScore,
    decimal OverallPct,
    IReadOnlyList<AssessmentCategoryScoreDto> Categories,
    IReadOnlyList<AssessmentDimensionScoreDto> Dimensions,
    AssessmentQualitativeTagsDto Qualitative);

public sealed record KapMaturityAssessmentDto(
    int Level,
    string Code,
    string Name,
    string Description,
    string Scope);

public sealed record KapSwotAnalysisDto(
    IReadOnlyList<KapSwotItemDto> Strengths,
    IReadOnlyList<KapSwotItemDto> Weaknesses,
    IReadOnlyList<KapSwotItemDto> Opportunities,
    IReadOnlyList<KapSwotItemDto> Threats);

public sealed record KapSwotItemDto(string Title, string Body, string? Area);

public sealed record KapRootCauseDto(
    string Code,
    string Area,
    string Title,
    string Body,
    IReadOnlyList<string> Evidence);

public sealed record KapBenchmarkAnalysisDto(
    string CohortCode,
    int SampleSize,
    decimal? OverallVsMean,
    IReadOnlyList<KapBenchmarkCategoryDto> Categories,
    string? Narrative,
    decimal? EstimatedPercentile = null,
    string? PercentileLabel = null,
    IReadOnlyList<KapBenchmarkTierDto>? Tiers = null);

public sealed record KapBenchmarkTierDto(
    string Label,
    decimal ScorePct,
    string? Note = null);

public sealed record KapBenchmarkCategoryDto(
    string Code,
    string Name,
    decimal Score,
    decimal? CohortMean,
    decimal? Delta);

public sealed record KapTrendAnalysisDto(
    bool HasPriorSubmission,
    Guid? PriorSubmissionId,
    decimal? OverallDelta,
    string TrendLabel,
    IReadOnlyList<KapTrendCategoryDto> Categories);

public sealed record KapTrendCategoryDto(string Code, decimal Delta, string Label);

public sealed record KapRiskItemDto(
    string Area,
    string Level,
    string Title,
    string Body);

public sealed record KapOpportunityItemDto(
    string Area,
    string Title,
    string Body,
    string? ImpactHint);

public sealed record KapRoadmapDto(
    IReadOnlyList<KapRoadmapItemDto> Days30,
    IReadOnlyList<KapRoadmapItemDto> Days60,
    IReadOnlyList<KapRoadmapItemDto> Days90,
    IReadOnlyList<KapRoadmapItemDto> Days180);

public sealed record KapRoadmapItemDto(int HorizonDays, string Title, string Body);

public sealed record KapKpiRecommendationDto(
    string Name,
    string Target,
    int DeadlineDays,
    string? Area);

public sealed record KapPriorityMatrixDto(
    IReadOnlyList<KapPriorityItemDto> HighImpactHighPriority,
    IReadOnlyList<KapPriorityItemDto> QuickWins,
    IReadOnlyList<KapPriorityItemDto> LongTerm,
    IReadOnlyList<KapPriorityItemDto> Optional);

public sealed record KapPriorityItemDto(
    string Title,
    string Body,
    string Quadrant,
    int Priority);

public sealed record KapExecutiveSummaryDto(
    string Headline,
    IReadOnlyList<string> Paragraphs,
    string Source,
    string? OpeningContext = null,
    string? Analysis = null,
    string? Assessment = null,
    string? Conclusion = null,
    string? Recommendations = null);

public sealed record KapActionPlanItemDto(
    string Title,
    string Body,
    string Priority,
    string Owner,
    string Timeline,
    string ExpectedOutcome);

public sealed record KapActionPlanDto(
    string Narrative,
    IReadOnlyList<KapActionPlanItemDto> Items);

public sealed record KapConsultingBriefDto(
    string DiagnosisHeadline,
    string CostOfInaction,
    IReadOnlyList<KapBusinessImpactDto> BusinessImpacts,
    IReadOnlyList<KapSoftwareModuleFitDto> ModuleFits,
    KapRoiStoryDto RoiStory,
    string UrgencyStatement,
    string NextStepCta);

public sealed record KapBusinessImpactDto(
    string Area,
    string Title,
    string ImpactStatement,
    string CostHint);

public sealed record KapSoftwareModuleFitDto(
    string ModuleName,
    string PainResolved,
    string Outcome30Days,
    string Outcome90Days,
    int Priority);

public sealed record KapRoiStoryDto(
    string Summary,
    IReadOnlyList<string> BeforeState,
    IReadOnlyList<string> AfterState);

public sealed record KapExecutiveDashboardDto(
    IReadOnlyList<string> TopProblems,
    IReadOnlyList<string> TopRisks,
    IReadOnlyList<string> TopOpportunities,
    decimal DigitalReadinessPct,
    decimal NovixaFitPct,
    string AiAssessmentLine);

public sealed record KapNovixaReadinessDto(
    decimal OverallPct,
    string StatusLabel,
    IReadOnlyList<KapReadinessDimensionDto> Dimensions);

public sealed record KapReadinessDimensionDto(
    string Code,
    string Name,
    decimal ScorePct);

public sealed record KapGapItemDto(
    string CurrentState,
    string TargetState,
    string NovixaModule,
    string? FeatureHint);

public sealed record KapGapAnalysisDto(
    string Narrative,
    IReadOnlyList<KapGapItemDto> Items);

public sealed record KapRoiMetricDto(
    string Label,
    string Range,
    string Description);

public sealed record KapModuleStarDto(
    string ModuleCode,
    string ModuleName,
    int Stars,
    string Rationale);

public sealed record KapInvestmentPhaseDto(
    int PhaseNumber,
    string Title,
    string ModuleFocus,
    string Description,
    string TimingHint);

public sealed record KapBusinessImpactForecastDto(
    string Metric,
    string ChangeRange,
    string Direction);

public sealed record KapModuleMappingItemDto(
    string Problem,
    string Module,
    string FeatureChain);

public sealed record KapTransformationPhaseDto(
    int Phase,
    string Title,
    string Description,
    string Module);

public sealed record KapTransformationRoadmapDto(
    IReadOnlyList<KapTransformationPhaseDto> Phases,
    string Narrative);

public sealed record KapCostBenefitItemDto(
    string Category,
    string InvestmentHint,
    string BenefitRange,
    string PaybackHint);

public sealed record KapCostBenefitAnalysisDto(
    string Summary,
    IReadOnlyList<KapCostBenefitItemDto> Items,
    string NetAssessment);

public sealed record KapTimelineMilestoneDto(
    string PeriodLabel,
    string Activity,
    string Deliverable);

public sealed record KapImplementationTimelineDto(
    string Narrative,
    IReadOnlyList<KapTimelineMilestoneDto> Milestones);

public sealed record KapRiskRegisterItemDto(
    string Code,
    string Title,
    string Severity,
    string Impact,
    string Mitigation,
    string OwnerHint);

public sealed record KapRiskRegisterDto(
    string Summary,
    IReadOnlyList<KapRiskRegisterItemDto> Items);

public sealed record KapAiNarrativeDto(
    string Source,
    string? Model,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<string> PersonalizedInsights,
    string? AiConclusion = null);

public sealed record KapReadinessBarDto(string Label, int Pct);

public sealed record KapTransformationReadinessDto(
    IReadOnlyList<KapReadinessBarDto> Bars,
    string Narrative);

public sealed record KapNarrativeStepDto(string Horizon, string Outcome);

public sealed record KapNarrativeCascadeDto(
    string Summary,
    IReadOnlyList<KapNarrativeStepDto> Steps);

public sealed record KapCrossCategoryInsightDto(
    string Headline,
    string Analysis,
    IReadOnlyList<string> Implications);

public sealed record KapWhyNovixaRowDto(
    string Problem,
    string Module,
    string Benefit,
    string KpiTarget);

public sealed record KapWhyNovixaDto(
    string Intro,
    IReadOnlyList<KapWhyNovixaRowDto> Rows);

public sealed record KapReportAppendixDto(
    IReadOnlyList<KapAppendixQuestionDto> Questions);

public sealed record KapAppendixQuestionDto(
    string Code,
    string Title,
    string CategoryCode,
    string? AnswerLabel,
    decimal? Score,
    bool Scorable);

public sealed record KapPipelineStatusDto(
    string PipelineVersion,
    string EngineMode,
    int PipelineLevel,
    IReadOnlyList<string> PhasesCompleted,
    IReadOnlyList<string> PhasesPending);

public sealed record AssessmentReportIntelligenceDto(
    string SchemaVersion,
    KapMaturityAssessmentDto? Maturity,
    KapSwotAnalysisDto? Swot,
    IReadOnlyList<KapRootCauseDto> RootCauses,
    KapBenchmarkAnalysisDto? Benchmark,
    KapTrendAnalysisDto? Trend,
    IReadOnlyList<KapRiskItemDto> Risks,
    IReadOnlyList<KapOpportunityItemDto> Opportunities,
    KapRoadmapDto? Roadmap,
    IReadOnlyList<KapKpiRecommendationDto> Kpis,
    KapPriorityMatrixDto? PriorityMatrix,
    KapExecutiveSummaryDto? ExecutiveSummary,
    KapConsultingBriefDto? ConsultingBrief,
    KapAiNarrativeDto? AiNarrative,
    KapExecutiveDashboardDto? ExecutiveDashboard,
    KapNovixaReadinessDto? NovixaReadiness,
    KapGapAnalysisDto? GapAnalysis,
    IReadOnlyList<KapRoiMetricDto>? RoiMetrics,
    IReadOnlyList<KapModuleStarDto>? ModuleRecommendations,
    IReadOnlyList<KapInvestmentPhaseDto>? InvestmentPhases,
    IReadOnlyList<KapBusinessImpactForecastDto>? BusinessImpactForecast,
    IReadOnlyList<KapModuleMappingItemDto>? ModuleMappings,
    KapTransformationRoadmapDto? TransformationRoadmap,
    KapCostBenefitAnalysisDto? CostBenefit,
    KapImplementationTimelineDto? ImplementationTimeline,
    KapRiskRegisterDto? RiskRegister,
    KapReportAppendixDto? Appendix,
    KapPipelineStatusDto Pipeline,
    KapActionPlanDto? ActionPlan = null,
    KapTransformationReadinessDto? TransformationReadiness = null,
    KapCrossCategoryInsightDto? CrossCategoryInsight = null,
    KapNarrativeCascadeDto? InactionCascade = null,
    KapNarrativeCascadeDto? ImplementationJourney = null,
    KapWhyNovixaDto? WhyNovixa = null,
    /// <summary>Owner-facing summary (presentation layer; not persisted in artifact).</summary>
    AssessmentOwnerPackDto? OwnerPack = null);

/// <summary>Báo cáo ngắn cho chủ nhà thuốc — dễ đọc, tập trung nỗi đau & bước Pilot.</summary>
public sealed record AssessmentOwnerPackDto(
    string OverallHeadline,
    string MaturityLabel,
    decimal OverallScorePct,
    IReadOnlyList<AssessmentOwnerPointDto> Strengths,
    IReadOnlyList<AssessmentOwnerPainDto> Pains,
    string OneThingFirst,
    IReadOnlyList<AssessmentOwnerActionDto> Actions30Days,
    AssessmentOwnerPilotHingeDto PilotHinge,
    string NextStepCta);

public sealed record AssessmentOwnerPointDto(
    string Title,
    string Body,
    string? AreaCode = null);

public sealed record AssessmentOwnerPainDto(
    string Title,
    string BusinessConsequence,
    string? AreaCode = null,
    string? SourceHint = null);

public sealed record AssessmentOwnerActionDto(
    string Title,
    string Who,
    string When,
    string DoneWhen);

public sealed record AssessmentOwnerPilotHingeDto(
    string RecommendedFocus,
    IReadOnlyList<string> FocusOptions,
    string HowToTalk);
