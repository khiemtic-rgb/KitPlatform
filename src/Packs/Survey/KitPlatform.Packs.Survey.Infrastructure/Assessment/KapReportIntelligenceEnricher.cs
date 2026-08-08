using KitPlatform.Packs.Survey;

namespace KitPlatform.Packs.Survey.Infrastructure;

/// <summary>
/// Bổ sung consulting extensions khi artifact DB cũ thiếu các trường v1.1 (NovixaReadiness, ROI, …).
/// </summary>
internal static class KapReportIntelligenceEnricher
{
    public static AssessmentReportIntelligenceDto Enrich(
        AssessmentFullReportDto report,
        AssessmentReportIntelligenceDto intel,
        string orgName,
        string? orgScale = null)
    {
        var scores = new KapReportScoresDto(
            report.OverallScore,
            report.OverallPct,
            report.CategoryScores,
            report.DimensionScores,
            report.QualitativeTags);

        var brief = intel.ConsultingBrief;
        if (brief is not null && !HasAiEnrichment(intel))
        {
            var weak = report.CategoryScores.OrderBy(c => c.Score).Take(3).ToList();
            brief = KapScaleBasedRoiBuilder.ApplyScaleRoi(
                brief, orgName, orgScale, scores, intel.Maturity, weak, brief.ModuleFits);
        }

        if (brief is not null && (!HasConsultingExtensions(intel) || !HasAiEnrichment(intel)))
        {
            var ext = KapConsultingExtensionsBuilder.Build(
                orgName, orgScale, scores,
                intel.Maturity, intel.RootCauses, intel.Risks, intel.Opportunities, brief);

            var actionPlan = KapActionPlanBuilder.Build(report.Recommendations, intel.Roadmap, brief);

            var bundle = KapConsultingIntelligenceBuilder.Build(
                orgName, orgScale, scores,
                intel.Maturity, intel.RootCauses, intel.Risks, intel.Opportunities,
                intel.Benchmark, brief, ext.NovixaReadiness, ext.GapAnalysis, intel.PriorityMatrix);

            return AttachOwnerPack(report, Normalize(new AssessmentReportIntelligenceDto(
                KapReportArtifactSchema.Version,
                intel.Maturity, intel.Swot, intel.RootCauses,
                intel.Benchmark is not null
                    ? intel.Benchmark with { Tiers = bundle.BenchmarkTiers }
                    : intel.Benchmark,
                intel.Trend,
                intel.Risks, intel.Opportunities, intel.Roadmap, intel.Kpis, bundle.PriorityMatrix,
                intel.ExecutiveSummary, brief, intel.AiNarrative,
                ext.ExecutiveDashboard, ext.NovixaReadiness, ext.GapAnalysis, ext.RoiMetrics,
                ext.ModuleRecommendations, ext.InvestmentPhases, ext.BusinessImpactForecast,
                ext.ModuleMappings, ext.TransformationRoadmap, ext.CostBenefit,
                ext.ImplementationTimeline, ext.RiskRegister, intel.Appendix, intel.Pipeline,
                actionPlan,
                bundle.TransformationReadiness,
                bundle.CrossCategoryInsight,
                bundle.InactionCascade,
                bundle.ImplementationJourney,
                bundle.WhyNovixa)));
        }

        if (brief != intel.ConsultingBrief)
        {
            var actionPlan = KapActionPlanBuilder.Build(report.Recommendations, intel.Roadmap, brief);
            return AttachOwnerPack(report, Normalize(intel with { ConsultingBrief = brief, ActionPlan = actionPlan }));
        }

        if (intel.ActionPlan is null && brief is not null)
        {
            var actionPlan = KapActionPlanBuilder.Build(report.Recommendations, intel.Roadmap, brief);
            intel = intel with { ActionPlan = actionPlan };
        }

        intel = EnsureConsultingIntelligence(intel, orgName, orgScale, scores);
        return AttachOwnerPack(report, Normalize(intel));
    }

    private static AssessmentReportIntelligenceDto AttachOwnerPack(
        AssessmentFullReportDto report,
        AssessmentReportIntelligenceDto intel) =>
        intel with { OwnerPack = KapOwnerPackBuilder.Build(report, intel) };

    private static AssessmentReportIntelligenceDto EnsureConsultingIntelligence(
        AssessmentReportIntelligenceDto intel,
        string orgName,
        string? orgScale,
        KapReportScoresDto scores)
    {
        if (intel.ConsultingBrief is null)
            return intel;

        var benchmark = intel.Benchmark;
        if (benchmark is not null && (benchmark.Tiers is null || benchmark.Tiers.Count == 0))
        {
            benchmark = benchmark with
            {
                Tiers = KapConsultingIntelligenceBuilder.BuildBenchmarkTiers(
                    scores.OverallPct, orgScale, benchmark),
            };
        }

        if (intel.TransformationReadiness is not null
            && intel.CrossCategoryInsight is not null
            && intel.InactionCascade is not null
            && intel.WhyNovixa is not null)
        {
            var refreshedCb = RefreshCostBenefitIfStale(
                intel.CostBenefit, scores, orgName, orgScale, intel.ConsultingBrief);
            if (benchmark == intel.Benchmark && refreshedCb == intel.CostBenefit)
                return intel;
            return intel with { Benchmark = benchmark, CostBenefit = refreshedCb };
        }

        var bundle = KapConsultingIntelligenceBuilder.Build(
            orgName,
            orgScale,
            scores,
            intel.Maturity,
            intel.RootCauses,
            intel.Risks,
            intel.Opportunities,
            benchmark,
            intel.ConsultingBrief,
            intel.NovixaReadiness,
            intel.GapAnalysis,
            intel.PriorityMatrix);

        return intel with
        {
            Benchmark = benchmark,
            PriorityMatrix = bundle.PriorityMatrix,
            CostBenefit = RefreshCostBenefitIfStale(intel.CostBenefit, scores, orgName, orgScale, intel.ConsultingBrief),
            TransformationReadiness = bundle.TransformationReadiness,
            CrossCategoryInsight = bundle.CrossCategoryInsight,
            InactionCascade = bundle.InactionCascade,
            ImplementationJourney = bundle.ImplementationJourney,
            WhyNovixa = bundle.WhyNovixa,
        };
    }

    private static KapCostBenefitAnalysisDto? RefreshCostBenefitIfStale(
        KapCostBenefitAnalysisDto? current,
        KapReportScoresDto scores,
        string orgName,
        string? orgScale,
        KapConsultingBriefDto? brief)
    {
        if (brief is null) return current;
        if (current is not null && !HasStaleCostBenefit(current))
            return current;

        var roiMetrics = KapScaleBasedRoiBuilder.BuildMetrics(orgScale, scores, null);
        return KapScaleBasedRoiBuilder.BuildCostBenefit(orgName, orgScale, scores, brief, roiMetrics);
    }

    private static bool HasStaleCostBenefit(KapCostBenefitAnalysisDto cb) =>
        cb.Items.Any(i =>
            KapVietnameseText.ContainsPricingLanguage(i.InvestmentHint)
            || KapVietnameseText.ContainsPricingLanguage(i.BenefitRange)
            || KapVietnameseText.ContainsPricingLanguage(i.PaybackHint)
            || i.Category.Contains("Phần mềm Novixa", StringComparison.OrdinalIgnoreCase))
        || KapVietnameseText.ContainsPricingLanguage(cb.Summary)
        || KapVietnameseText.ContainsPricingLanguage(cb.NetAssessment)
        || cb.NetAssessment.Contains("ROI", StringComparison.OrdinalIgnoreCase);

    private static AssessmentReportIntelligenceDto Normalize(AssessmentReportIntelligenceDto intel) =>
        intel with
        {
            Maturity = intel.Maturity is null ? null : intel.Maturity with
            {
                Name = KapVietnameseText.Display(intel.Maturity.Name),
                Description = KapVietnameseText.Display(intel.Maturity.Description),
            },
            Swot = NormalizeSwot(intel.Swot),
            RootCauses = intel.RootCauses.Select(rc => rc with
            {
                Title = KapVietnameseText.Display(rc.Title),
                Body = KapVietnameseText.Display(rc.Body),
                Evidence = rc.Evidence.Select(KapVietnameseText.Display).ToList(),
            }).ToList(),
            Benchmark = intel.Benchmark is null ? null : intel.Benchmark with
            {
                Narrative = KapVietnameseText.Polish(intel.Benchmark.Narrative),
                PercentileLabel = KapVietnameseText.Polish(intel.Benchmark.PercentileLabel),
                Categories = intel.Benchmark.Categories.Select(c => c with
                {
                    Name = KapVietnameseText.Category(c.Code, c.Name),
                }).ToList(),
                Tiers = intel.Benchmark.Tiers?.Select(t => t with
                {
                    Label = KapVietnameseText.Polish(t.Label),
                    Note = KapVietnameseText.Polish(t.Note),
                }).ToList(),
            },
            Opportunities = intel.Opportunities.Select(o => o with
            {
                Title = KapVietnameseText.Display(o.Title),
                Body = KapVietnameseText.Display(o.Body),
                ImpactHint = KapVietnameseText.Display(o.ImpactHint),
            }).ToList(),
            Risks = intel.Risks.Select(r => r with
            {
                Title = KapVietnameseText.Display(r.Title),
                Body = KapVietnameseText.Display(r.Body),
            }).ToList(),
            Roadmap = intel.Roadmap is null ? null : intel.Roadmap with
            {
                Days30 = NormalizeRoadmap(intel.Roadmap.Days30),
                Days60 = NormalizeRoadmap(intel.Roadmap.Days60),
                Days90 = NormalizeRoadmap(intel.Roadmap.Days90),
                Days180 = NormalizeRoadmap(intel.Roadmap.Days180),
            },
            Kpis = intel.Kpis.Select(k => k with
            {
                Name = KapVietnameseText.Display(k.Name),
                Target = KapVietnameseText.Display(k.Target),
            }).ToList(),
            PriorityMatrix = NormalizePriorityMatrix(intel.PriorityMatrix),
            ExecutiveSummary = NormalizeExecutiveSummary(intel.ExecutiveSummary),
            ConsultingBrief = NormalizeBrief(intel.ConsultingBrief),
            AiNarrative = intel.AiNarrative is null ? null : intel.AiNarrative with
            {
                PersonalizedInsights = intel.AiNarrative.PersonalizedInsights.Select(KapVietnameseText.Display).ToList(),
                AiConclusion = KapVietnameseText.Polish(intel.AiNarrative.AiConclusion),
            },
            ExecutiveDashboard = intel.ExecutiveDashboard is null ? null : intel.ExecutiveDashboard with
            {
                TopProblems = intel.ExecutiveDashboard.TopProblems.Select(KapVietnameseText.Display).ToList(),
                TopRisks = intel.ExecutiveDashboard.TopRisks.Select(KapVietnameseText.Display).ToList(),
                TopOpportunities = intel.ExecutiveDashboard.TopOpportunities.Select(KapVietnameseText.Display).ToList(),
                AiAssessmentLine = KapVietnameseText.Display(intel.ExecutiveDashboard.AiAssessmentLine),
            },
            NovixaReadiness = intel.NovixaReadiness is null ? null : intel.NovixaReadiness with
            {
                StatusLabel = KapVietnameseText.Display(intel.NovixaReadiness.StatusLabel),
                Dimensions = intel.NovixaReadiness.Dimensions.Select(d => d with
                {
                    Name = KapVietnameseText.Display(d.Name),
                }).ToList(),
            },
            GapAnalysis = intel.GapAnalysis is null ? null : intel.GapAnalysis with
            {
                Narrative = KapVietnameseText.Display(intel.GapAnalysis.Narrative),
                Items = intel.GapAnalysis.Items.Select(i => i with
                {
                    CurrentState = KapVietnameseText.Display(i.CurrentState),
                    TargetState = KapVietnameseText.Display(i.TargetState),
                    NovixaModule = KapVietnameseText.Display(i.NovixaModule),
                    FeatureHint = KapVietnameseText.Display(i.FeatureHint),
                }).ToList(),
            },
            RoiMetrics = intel.RoiMetrics?
                .Where(m => !KapVietnameseText.ContainsPricingLanguage(m.Label))
                .Select(m => m with
                {
                    Label = KapVietnameseText.Display(m.Label),
                    Description = KapVietnameseText.Display(m.Description),
                }).ToList(),
            ModuleRecommendations = intel.ModuleRecommendations?.Select(m => m with
            {
                ModuleName = KapVietnameseText.Display(m.ModuleName),
                Rationale = KapVietnameseText.Display(m.Rationale),
            }).ToList(),
            InvestmentPhases = intel.InvestmentPhases?.Select(p => p with
            {
                Title = KapVietnameseText.Display(p.Title),
                Description = KapVietnameseText.Display(p.Description),
                ModuleFocus = KapVietnameseText.Display(p.ModuleFocus),
            }).ToList(),
            BusinessImpactForecast = intel.BusinessImpactForecast?.Select(f => f with
            {
                Metric = KapVietnameseText.Display(f.Metric),
            }).ToList(),
            ModuleMappings = intel.ModuleMappings?.Select(m => m with
            {
                Problem = KapVietnameseText.Display(m.Problem),
                Module = KapVietnameseText.Display(m.Module),
                FeatureChain = KapVietnameseText.Display(m.FeatureChain),
            }).ToList(),
            TransformationRoadmap = intel.TransformationRoadmap is null ? null : intel.TransformationRoadmap with
            {
                Narrative = KapVietnameseText.Display(intel.TransformationRoadmap.Narrative),
                Phases = intel.TransformationRoadmap.Phases.Select(p => p with
                {
                    Title = KapVietnameseText.Display(p.Title),
                    Description = KapVietnameseText.Display(p.Description),
                    Module = KapVietnameseText.Display(p.Module),
                }).ToList(),
            },
            CostBenefit = intel.CostBenefit is null ? null : intel.CostBenefit with
            {
                Summary = KapVietnameseText.StripPricingMentions(intel.CostBenefit.Summary),
                NetAssessment = KapVietnameseText.StripPricingMentions(intel.CostBenefit.NetAssessment),
                Items = intel.CostBenefit.Items
                    .Where(i => !i.Category.Contains("Phần mềm Novixa", StringComparison.OrdinalIgnoreCase)
                        && !KapVietnameseText.ContainsPricingLanguage(i.InvestmentHint)
                        && !KapVietnameseText.ContainsPricingLanguage(i.BenefitRange)
                        && !KapVietnameseText.ContainsPricingLanguage(i.PaybackHint))
                    .Select(i => i with
                    {
                        Category = KapVietnameseText.Display(i.Category),
                        InvestmentHint = KapVietnameseText.Display(i.InvestmentHint),
                        PaybackHint = KapVietnameseText.Display(i.PaybackHint),
                    }).ToList(),
            },
            ImplementationTimeline = intel.ImplementationTimeline is null ? null : intel.ImplementationTimeline with
            {
                Narrative = KapVietnameseText.Display(intel.ImplementationTimeline.Narrative),
                Milestones = intel.ImplementationTimeline.Milestones.Select(m => m with
                {
                    Activity = KapVietnameseText.Display(m.Activity),
                    Deliverable = KapVietnameseText.Display(m.Deliverable),
                }).ToList(),
            },
            RiskRegister = intel.RiskRegister is null ? null : intel.RiskRegister with
            {
                Summary = KapVietnameseText.Display(intel.RiskRegister.Summary),
                Items = intel.RiskRegister.Items.Select(i => i with
                {
                    Title = KapVietnameseText.Display(i.Title),
                    Impact = KapVietnameseText.Display(i.Impact),
                    Mitigation = KapVietnameseText.Display(i.Mitigation),
                }).ToList(),
            },
            ActionPlan = intel.ActionPlan is null ? null : intel.ActionPlan with
            {
                Narrative = KapVietnameseText.Polish(intel.ActionPlan.Narrative),
                Items = intel.ActionPlan.Items.Select(i => i with
                {
                    Title = KapVietnameseText.Polish(i.Title),
                    Body = KapVietnameseText.Polish(i.Body),
                    ExpectedOutcome = KapVietnameseText.Polish(i.ExpectedOutcome),
                }).ToList(),
            },
            TransformationReadiness = NormalizeTransformationReadiness(intel.TransformationReadiness),
            CrossCategoryInsight = NormalizeCrossCategory(intel.CrossCategoryInsight),
            InactionCascade = NormalizeCascade(intel.InactionCascade),
            ImplementationJourney = NormalizeCascade(intel.ImplementationJourney),
            WhyNovixa = NormalizeWhyNovixa(intel.WhyNovixa),
        };

    private static KapExecutiveSummaryDto? NormalizeExecutiveSummary(KapExecutiveSummaryDto? exec) =>
        exec is null ? null : exec with
        {
            Headline = KapVietnameseText.Polish(exec.Headline),
            Paragraphs = exec.Paragraphs.Select(KapVietnameseText.Polish).ToList(),
            OpeningContext = KapVietnameseText.Polish(exec.OpeningContext),
            Analysis = KapVietnameseText.Polish(exec.Analysis),
            Assessment = KapVietnameseText.Polish(exec.Assessment),
            Conclusion = KapVietnameseText.Polish(exec.Conclusion),
            Recommendations = KapVietnameseText.Polish(exec.Recommendations),
        };

    private static KapConsultingBriefDto? NormalizeBrief(KapConsultingBriefDto? brief) =>
        brief is null ? null : brief with
        {
            DiagnosisHeadline = KapVietnameseText.Display(brief.DiagnosisHeadline),
            CostOfInaction = KapVietnameseText.Display(brief.CostOfInaction),
            UrgencyStatement = KapVietnameseText.Display(brief.UrgencyStatement),
            NextStepCta = KapVietnameseText.Display(brief.NextStepCta),
            BusinessImpacts = brief.BusinessImpacts.Select(i => i with
            {
                Title = KapVietnameseText.Display(i.Title),
                ImpactStatement = KapVietnameseText.Display(i.ImpactStatement),
                CostHint = KapVietnameseText.Display(i.CostHint),
            }).ToList(),
            ModuleFits = brief.ModuleFits.Select(m => m with
            {
                ModuleName = KapVietnameseText.Display(m.ModuleName),
                PainResolved = KapVietnameseText.Display(m.PainResolved),
                Outcome30Days = KapVietnameseText.Display(m.Outcome30Days),
                Outcome90Days = KapVietnameseText.Display(m.Outcome90Days),
            }).ToList(),
            RoiStory = brief.RoiStory with
            {
                Summary = KapVietnameseText.Display(KapVietnameseText.StripPricingMentions(brief.RoiStory.Summary)),
                BeforeState = brief.RoiStory.BeforeState
                    .Select(s => KapVietnameseText.Display(KapVietnameseText.StripPricingMentions(s)))
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList(),
                AfterState = brief.RoiStory.AfterState
                    .Select(s => KapVietnameseText.Display(KapVietnameseText.StripPricingMentions(s)))
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList(),
            },
        };

    private static KapSwotAnalysisDto? NormalizeSwot(KapSwotAnalysisDto? swot) =>
        swot is null ? null : swot with
        {
            Strengths = NormalizeSwotItems(swot.Strengths),
            Weaknesses = NormalizeSwotItems(swot.Weaknesses),
            Opportunities = NormalizeSwotItems(swot.Opportunities),
            Threats = NormalizeSwotItems(swot.Threats),
        };

    private static IReadOnlyList<KapSwotItemDto> NormalizeSwotItems(IReadOnlyList<KapSwotItemDto> items) =>
        items.Select(i => i with
        {
            Title = KapVietnameseText.Display(i.Title),
            Body = KapVietnameseText.Display(i.Body),
        }).ToList();

    private static KapPriorityMatrixDto? NormalizePriorityMatrix(KapPriorityMatrixDto? matrix) =>
        matrix is null ? null : matrix with
        {
            HighImpactHighPriority = NormalizePriorityItems(matrix.HighImpactHighPriority),
            QuickWins = NormalizePriorityItems(matrix.QuickWins),
            LongTerm = NormalizePriorityItems(matrix.LongTerm),
            Optional = NormalizePriorityItems(matrix.Optional),
        };

    private static IReadOnlyList<KapPriorityItemDto> NormalizePriorityItems(IReadOnlyList<KapPriorityItemDto> items) =>
        items.Select(i => i with
        {
            Title = KapVietnameseText.Display(i.Title),
            Body = KapVietnameseText.Display(i.Body),
        }).ToList();

    private static IReadOnlyList<KapRoadmapItemDto> NormalizeRoadmap(IReadOnlyList<KapRoadmapItemDto> items) =>
        items.Select(i => i with
        {
            Title = KapVietnameseText.Display(i.Title),
            Body = KapVietnameseText.Display(i.Body),
        }).ToList();

    private static KapTransformationReadinessDto? NormalizeTransformationReadiness(KapTransformationReadinessDto? r) =>
        r is null ? null : r with { Narrative = KapVietnameseText.Polish(r.Narrative) };

    private static KapCrossCategoryInsightDto? NormalizeCrossCategory(KapCrossCategoryInsightDto? c) =>
        c is null ? null : c with
        {
            Headline = KapVietnameseText.Polish(c.Headline),
            Analysis = KapVietnameseText.Polish(c.Analysis),
            Implications = c.Implications.Select(KapVietnameseText.Polish).ToList(),
        };

    private static KapNarrativeCascadeDto? NormalizeCascade(KapNarrativeCascadeDto? c) =>
        c is null ? null : c with
        {
            Summary = KapVietnameseText.Polish(c.Summary),
            Steps = c.Steps.Select(s => s with
            {
                Horizon = KapVietnameseText.Polish(s.Horizon),
                Outcome = KapVietnameseText.Polish(s.Outcome),
            }).ToList(),
        };

    private static KapWhyNovixaDto? NormalizeWhyNovixa(KapWhyNovixaDto? w) =>
        w is null ? null : w with
        {
            Intro = KapVietnameseText.Polish(w.Intro),
            Rows = w.Rows.Select(r => r with
            {
                Problem = KapVietnameseText.Polish(r.Problem),
                Module = KapVietnameseText.Polish(r.Module),
                Benefit = KapVietnameseText.Polish(r.Benefit),
                KpiTarget = KapVietnameseText.Polish(r.KpiTarget),
            }).ToList(),
        };

    public static bool NeedsArtifactRebuild(KapReportArtifactDto artifact, AssessmentSettings? settings = null)
    {
        if (!string.Equals(artifact.SchemaVersion, KapReportArtifactSchema.Version, StringComparison.Ordinal))
            return true;

        if (artifact.CostBenefit?.Items.Any(i =>
                KapVietnameseText.ContainsPricingLanguage(i.InvestmentHint)
                || i.Category.Contains("Phần mềm Novixa", StringComparison.OrdinalIgnoreCase))
            == true)
            return true;

        if (artifact.RoiMetrics?.Any(m => KapVietnameseText.ContainsPricingLanguage(m.Label)) == true)
            return true;

        if (artifact.ConsultingBrief is null)
            return true;

        if (artifact.ExecutiveDashboard is null
            || artifact.NovixaReadiness is null
            || artifact.RoiMetrics is null || artifact.RoiMetrics.Count == 0
            || artifact.CostBenefit is null
            || artifact.BusinessImpactForecast is null || artifact.BusinessImpactForecast.Count == 0
            || artifact.TransformationReadiness is null
            || artifact.CrossCategoryInsight is null
            || artifact.WhyNovixa is null)
            return true;

        if (settings is { AnalysisPipelineLevel: >= 3, AiNarrative.Enabled: true }
            && !string.IsNullOrWhiteSpace(settings.AiNarrative.ApiKey))
        {
            if (artifact.AiNarrative is null
                || !artifact.AiNarrative.Source.StartsWith("openai", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool HasAiEnrichment(AssessmentReportIntelligenceDto intel) =>
        intel.AiNarrative?.Source.StartsWith("openai", StringComparison.OrdinalIgnoreCase) == true;

    private static bool HasConsultingExtensions(AssessmentReportIntelligenceDto intel) =>
        intel.ExecutiveDashboard is not null
        && intel.NovixaReadiness is not null
        && intel.RoiMetrics?.Count > 0
        && intel.CostBenefit is not null
        && intel.BusinessImpactForecast?.Count > 0;
}
