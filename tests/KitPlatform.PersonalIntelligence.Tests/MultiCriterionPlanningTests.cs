using KitPlatform.Packs.PersonalIntelligence;
using KitPlatform.Packs.PersonalIntelligence.Agent;
using KitPlatform.Packs.PersonalIntelligence.ResearchHost;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class MultiCriterionPlanningTests
{
    private static readonly Guid Tenant = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    private const string Sentence = "Confirm both that observation evidence outnumbers fact evidence and that the stored evidence is complete enough to support the tenant's decisions.";

    [Fact]
    public void A_two_criterion_sentence_plans_two_capabilities_and_leaves_existing_routes_alone()
    {
        Assert.False(GoalContract.TryMatch(Sentence, EvidenceReviewRuntime.Criteria, out _));
        Assert.False(GoalContract.TryMatch(Sentence, TradingResearchRuntime.Criteria, out _));
        Assert.Null(MultiCriterionPlanning.Interpret("Assess decision support.", "Is the stored evidence complete enough to support the tenant's decisions?", null));
        Assert.Null(MultiCriterionPlanning.Interpret("Assess the rows.", "Validate whether the stored evidence supports a specific decision.", null));
        var interpreted = MultiCriterionPlanning.Interpret("Review the tenant evidence.", Sentence, null);

        Assert.NotNull(interpreted);
        Assert.Equal(UnderstandingState.ReadyToPlan, interpreted.Understanding.State);
        Assert.Equal([MultiCriterionPlanning.Comparison, MultiCriterionPlanning.Coverage], interpreted.FeasibleNeeds.ToArray());
        Assert.Equal("discover", interpreted.Understanding.Plan.Single(step => step.StepId == "coverage").DependsOn);
        Assert.Equal("collect-FACT,coverage", interpreted.Understanding.Plan.Single(step => step.StepId == "assess").DependsOn);
        Assert.Contains(interpreted.Understanding.Plan, step => step.ToolName == "evidence.collect");
        Assert.Contains(interpreted.Understanding.Plan, step => step.ToolName == "decision.support-coverage");
        Assert.DoesNotContain(interpreted.Understanding.Plan, step => step.ToolName is "trading.paper_execute" or "thesis.read");
        Assert.Contains(interpreted.Understanding.Plan, step => step.Purpose!.Contains("before any read", StringComparison.Ordinal));
        Assert.Contains("evidence.count", interpreted.Understanding.Capabilities);
        Assert.Contains("decision.support-coverage", interpreted.Understanding.Capabilities);
    }

    [Fact]
    public void A_partial_sentence_names_the_gap_and_an_unknown_criterion_blocks_the_plan()
    {
        var partial = MultiCriterionPlanning.Interpret("Review the request.", "Confirm both the evidence.", null);
        Assert.Equal(UnderstandingState.MissingData, partial!.Understanding.State);
        Assert.Contains(partial.Understanding.Missing, item => item.Contains("comparison", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(partial.Understanding.Plan, step => step.StepId == "coverage");

        var evidence = new CountLedger(5, 1);
        var coverage = new CoverageLedger(new DecisionSupportCoverage(4, 4, 4, 1));
        var blocked = new MultiCriterionRuntime().Execute(
            Request(Sentence, Business(MultiCriterionPlanning.Comparison, MultiCriterionPlanning.Coverage, "live-order"), []),
            evidence,
            coverage,
            new MemoryAgentRunLog());

        Assert.Equal("missing-capability", blocked.Task.Goal!.Understanding);
        Assert.Equal(0, blocked.EvidenceReads);
        Assert.Equal(0, blocked.CoverageReads);
        Assert.Equal(0, evidence.Calls);
        Assert.Equal(0, coverage.Calls);
    }

    [Fact]
    public void One_proven_criterion_does_not_make_the_whole_request_proven()
    {
        var evidence = new CountLedger(5, 1, "hidden-source-ref");
        var coverage = new CoverageLedger(new DecisionSupportCoverage(4, 2, 5, 0));
        var request = Request(Sentence, null, [new AgentStepSpec("execute", "trading.paper_execute", null, "Do not run this.")]);
        var log = new MemoryAgentRunLog();
        var runtime = new MultiCriterionRuntime();
        var first = runtime.Execute(request, evidence, coverage, log);
        var second = runtime.Execute(request, evidence, coverage, log);

        Assert.Equal(AgentTaskStatus.Failed, first.Task.Status);
        Assert.False(first.Task.Goal!.Met);
        Assert.Equal(GoalTaskRuntime.ExecutedUnproven, first.Task.Goal.Understanding);
        Assert.Contains(first.Task.Goal.Facts, fact => fact == "Criterion observation-over-fact is proven.");
        Assert.Contains(first.Task.Goal.Facts, fact => fact == "Criterion coverage-complete is executed-unproven.");
        Assert.Contains(first.Task.Goal.Facts, fact => fact.Contains("5 OBSERVATION rows", StringComparison.Ordinal));
        Assert.Contains(first.Task.Goal.Facts, fact => fact.Contains("1 FACT rows", StringComparison.Ordinal));
        Assert.Contains(first.Task.Goal.Facts, fact => fact.Contains("2 decisions with evidence", StringComparison.Ordinal));
        Assert.Contains(first.Task.Goal.Missing, item => item == "evidence rows with a non-empty payload");
        Assert.Contains(first.Task.Goal.Missing, item => item == "decisions without evidence on the same context");
        Assert.DoesNotContain("hidden-source-ref", string.Join('\n', first.Task.Goal.Facts), StringComparison.Ordinal);
        Assert.DoesNotContain(first.Task.Plan!, step => step.ToolName == "trading.paper_execute");
        Assert.Equal(2, first.EvidenceReads);
        Assert.Equal(1, first.CoverageReads);
        Assert.True(second.Task.Replayed);
        Assert.Equal(2, evidence.Calls);
        Assert.Equal(1, coverage.Calls);
    }

    [Fact]
    public void The_overall_goal_is_proven_only_when_every_required_criterion_holds()
    {
        var result = new MultiCriterionRuntime().Execute(
            Request(Sentence, null, []),
            new CountLedger(4, 1),
            new CoverageLedger(new DecisionSupportCoverage(2, 2, 4, 1)),
            new MemoryAgentRunLog());

        Assert.Equal(AgentTaskStatus.Completed, result.Task.Status);
        Assert.True(result.Task.Goal!.Met);
        Assert.Equal(GoalTaskRuntime.Proven, result.Task.Goal.Understanding);
        Assert.Contains(result.Task.Goal.Facts, fact => fact == "Criterion observation-over-fact is proven.");
        Assert.Contains(result.Task.Goal.Facts, fact => fact == "Criterion coverage-complete is proven.");
        Assert.Contains(result.Task.Goal.Limits, item => item.Contains("do not show that evidence supports a decision", StringComparison.Ordinal));
    }

    [Fact]
    public void An_explicit_comparison_is_not_expanded_into_coverage()
    {
        var evidence = new CountLedger(5, 1);
        var coverage = new CoverageLedger(new DecisionSupportCoverage(4, 1, 4, 0));
        var result = new MultiCriterionRuntime().Execute(
            Request(Sentence, Business(MultiCriterionPlanning.Comparison), []),
            evidence,
            coverage,
            new MemoryAgentRunLog());

        Assert.Equal(AgentTaskStatus.Completed, result.Task.Status);
        Assert.True(result.Task.Goal!.Met);
        Assert.Equal(2, result.EvidenceReads);
        Assert.Equal(0, result.CoverageReads);
        Assert.Equal(0, coverage.Calls);
        Assert.DoesNotContain(result.Task.Plan!, step => step.ToolName == "decision.support-coverage");
        Assert.DoesNotContain(result.Task.Goal.Missing, item => item.Contains("payload", StringComparison.Ordinal));
    }

    [Fact]
    public void Trading_permission_does_not_read_either_capability()
    {
        var evidence = new CountLedger(5, 1);
        var coverage = new CoverageLedger(new DecisionSupportCoverage(4, 4, 4, 1));
        var request = new AgentRequest(
            "multi-task",
            "multi-task",
            Tenant,
            "Review the tenant evidence.",
            Sentence,
            ["personal_intelligence.trading.run"],
            MultiCriterionPlanning.Interpret("Review the tenant evidence.", Sentence, null)!.Understanding.Plan,
            null,
            VerifyGoal: true);
        var result = new MultiCriterionRuntime().Execute(request, evidence, coverage, new MemoryAgentRunLog());

        Assert.Equal(AgentTaskStatus.Failed, result.Task.Status);
        Assert.Contains(result.Task.Steps, step => step.Status == ToolStepStatus.Denied);
        Assert.True(result.Task.Goal is null || !result.Task.Goal.Met);
        Assert.Equal(0, result.EvidenceReads);
        Assert.Equal(0, result.CoverageReads);
    }

    private static BusinessRequest Business(params string[] needs)
        => new()
        {
            Intent = MultiCriterionPlanning.Intent,
            Subject = MultiCriterionPlanning.Subject,
            Needs = needs.ToList(),
        };

    private static AgentRequest Request(string doneWhen, BusinessRequest? business, IReadOnlyList<AgentStepSpec> plan)
        => new(
            "multi-task",
            "multi-task",
            Tenant,
            "Review the tenant evidence.",
            doneWhen,
            [EvidenceReviewRuntime.ReadPermission],
            plan,
            null,
            VerifyGoal: true,
            Business: business);

    private sealed class CountLedger(int observation, int fact, string sample = "hidden-source-ref") : IEvidenceLedger
    {
        public int Calls { get; private set; }

        public EvidenceObservation Read(Guid tenantId, string epistemicKind)
        {
            Calls++;
            var count = epistemicKind == "OBSERVATION" ? observation : fact;
            return new EvidenceObservation(epistemicKind, count, sample);
        }
    }

    private sealed class CoverageLedger(DecisionSupportCoverage row) : IDecisionSupportLedger
    {
        public int Calls { get; private set; }

        public DecisionSupportCoverage Read(Guid tenantId)
        {
            Calls++;
            return row;
        }
    }
}
