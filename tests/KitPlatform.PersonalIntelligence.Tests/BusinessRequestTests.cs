using KitPlatform.Packs.PersonalIntelligence;
using KitPlatform.Packs.PersonalIntelligence.Agent;
using KitPlatform.Packs.PersonalIntelligence.ResearchHost;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class BusinessRequestTests
{
    private static readonly Guid Tenant = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    private const string Sentence = "Is the stored evidence complete enough to support the tenant's decisions?";

    [Fact]
    public void A_completeness_sentence_is_planned_without_a_registered_statement()
    {
        Assert.False(GoalContract.TryMatch(Sentence, EvidenceReviewRuntime.Criteria, out _));
        Assert.False(GoalContract.TryMatch(Sentence, TradingResearchRuntime.Criteria, out _));
        var interpreted = BusinessUnderstanding.Interpret("Assess decision support.", Sentence, null);

        Assert.Equal(UnderstandingState.ReadyToPlan, interpreted.Understanding.State);
        Assert.Equal([BusinessUnderstanding.Decisions, BusinessUnderstanding.Linked, BusinessUnderstanding.Content], interpreted.FeasibleNeeds.ToArray());
        Assert.Empty(interpreted.UnverifiableNeeds);
        Assert.Equal(["discover", "collect", "assess"], interpreted.Understanding.Plan.Select(step => step.StepId).ToArray());
        Assert.Equal("discover", interpreted.Understanding.Plan[1].DependsOn);
        Assert.Equal("collect", interpreted.Understanding.Plan[2].DependsOn);
        Assert.Contains(BusinessUnderstanding.CollectTool, interpreted.Understanding.Plan.Select(step => step.ToolName));
        Assert.DoesNotContain(interpreted.Understanding.Plan, step => step.ToolName is "trading.paper_execute" or "thesis.read");
        Assert.Contains(interpreted.Understanding.Plan, step => step.Purpose!.Contains("Thesis comparison is not registered", StringComparison.Ordinal));
        Assert.Contains("personal_intelligence.evidence.read", interpreted.Understanding.Permissions);
    }

    [Fact]
    public void A_partial_sentence_names_the_missing_part_and_an_unknown_sentence_is_not_understood()
    {
        var partial = BusinessUnderstanding.Interpret("Look around.", "Check the evidence.", null);
        var unknown = BusinessUnderstanding.Interpret("Improve it.", "Make it good.", null);

        Assert.Equal(UnderstandingState.MissingData, partial.Understanding.State);
        Assert.Contains(partial.Understanding.Missing, item => item.Contains("subject", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(partial.Understanding.Plan, step => step.StepId == "collect");
        Assert.Equal(UnderstandingState.NotUnderstood, unknown.Understanding.State);
    }

    [Fact]
    public void Explicit_needs_are_not_expanded_and_only_a_read_count_can_prove_them()
    {
        var business = new BusinessRequest
        {
            Intent = BusinessUnderstanding.Intent,
            Subject = BusinessUnderstanding.Subject,
            Needs = [BusinessUnderstanding.Decisions],
        };
        var ledger = new CoverageLedger(new DecisionSupportCoverage(4, 1, 4, 0));
        var log = new MemoryAgentRunLog();
        var runtime = new CompletenessRuntime();
        var request = Request(Sentence, business, [new AgentStepSpec("execute", "trading.paper_execute", null, "Do not run this.")]);
        var first = runtime.Execute(request, ledger, log);
        var second = runtime.Execute(request, ledger, log);

        Assert.Equal(AgentTaskStatus.Completed, first.Task.Status);
        Assert.True(first.Task.Goal!.Met);
        Assert.Equal(GoalTaskRuntime.Proven, first.Task.Goal.Understanding);
        Assert.Contains(first.Task.Goal.Facts, fact => fact.Contains("4 decisions", StringComparison.Ordinal));
        Assert.DoesNotContain(first.Task.Goal.Missing, item => item.Contains("payload", StringComparison.Ordinal));
        Assert.DoesNotContain(first.Task.Plan!, step => step.ToolName == "trading.paper_execute");
        Assert.Equal(1, first.ReadCalls);
        Assert.True(second.Task.Replayed);
        Assert.Equal(0, second.ReadCalls);
        Assert.Equal(1, ledger.Calls);
    }

    [Fact]
    public void Incomplete_coverage_is_read_and_not_marked_sufficient()
    {
        var ledger = new CoverageLedger(new DecisionSupportCoverage(4, 2, 5, 0));
        var result = new CompletenessRuntime().Execute(Request(Sentence, null, []), ledger, new MemoryAgentRunLog());

        Assert.Equal(AgentTaskStatus.Failed, result.Task.Status);
        Assert.False(result.Task.Goal!.Met);
        Assert.Equal(GoalTaskRuntime.ExecutedUnproven, result.Task.Goal.Understanding);
        Assert.Contains("decisions without evidence on the same context", result.Task.Goal.Missing);
        Assert.Contains("evidence rows with a non-empty payload", result.Task.Goal.Missing);
        Assert.Contains(result.Task.Goal.Limits, limit => limit.Contains("thesis", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Task.Steps, step => step.StepId == "discover" && step.Status == ToolStepStatus.Succeeded);
        Assert.Contains(result.Task.Steps, step => step.StepId == "collect" && step.Status == ToolStepStatus.Succeeded);
        Assert.Contains(result.Task.Steps, step => step.StepId == "assess" && step.Status == ToolStepStatus.Succeeded);
        Assert.Equal(1, result.ReadCalls);
        Assert.False(result.Task.LiveAccepted);
    }

    [Fact]
    public void An_unverifiable_or_unknown_criterion_is_not_turned_into_a_read()
    {
        var thesis = new BusinessRequest
        {
            Intent = BusinessUnderstanding.Intent,
            Subject = BusinessUnderstanding.Subject,
            Needs = [BusinessUnderstanding.Thesis],
        };
        var mixed = new BusinessRequest
        {
            Intent = BusinessUnderstanding.Intent,
            Subject = BusinessUnderstanding.Subject,
            Needs = [BusinessUnderstanding.Decisions, BusinessUnderstanding.Thesis],
        };
        var unknown = new BusinessRequest
        {
            Intent = BusinessUnderstanding.Intent,
            Subject = BusinessUnderstanding.Subject,
            Needs = ["live-order"],
        };
        var ledger = new CoverageLedger(new DecisionSupportCoverage(4, 4, 4, 2));
        var thesisOnly = new CompletenessRuntime().Execute(Request("Compare the thesis.", thesis, []), ledger, new MemoryAgentRunLog());
        var callsAfterThesis = ledger.Calls;
        var withData = new CompletenessRuntime().Execute(Request("Decisions and thesis.", mixed, []), ledger, new MemoryAgentRunLog());
        var unknownResult = new CompletenessRuntime().Execute(Request("Place an order.", unknown, []), ledger, new MemoryAgentRunLog());

        Assert.Equal(UnderstandingState.MissingCapability, BusinessUnderstanding.Interpret("Compare the thesis.", "Compare the thesis.", thesis).Understanding.State);
        Assert.Equal(0, callsAfterThesis);
        Assert.False(thesisOnly.Task.Goal!.Met);
        Assert.Equal("missing-capability", thesisOnly.Task.Goal.Understanding);
        Assert.DoesNotContain(thesisOnly.Task.Steps, step => step.StepId == "collect");
        Assert.Equal(AgentTaskStatus.Failed, withData.Task.Status);
        Assert.False(withData.Task.Goal!.Met);
        Assert.Contains(withData.Task.Goal.Missing, item => item.Contains("thesis-consistent", StringComparison.Ordinal));
        Assert.Equal(1, withData.ReadCalls);
        Assert.Equal("missing-capability", unknownResult.Task.Goal!.Understanding);
        Assert.Equal(0, unknownResult.ReadCalls);
        Assert.Equal(1, ledger.Calls);
    }

    [Fact]
    public void Trading_permission_does_not_read_decision_support()
    {
        var ledger = new CoverageLedger(new DecisionSupportCoverage(4, 4, 4, 1));
        var interpreted = BusinessUnderstanding.Interpret("Assess decision support.", Sentence, null);
        var request = new AgentRequest(
            "completeness-denied",
            "completeness-denied",
            Tenant,
            "Assess decision support.",
            Sentence,
            ["personal_intelligence.trading.run"],
            interpreted.Understanding.Plan,
            null,
            VerifyGoal: true);
        var result = new CompletenessRuntime().Execute(request, ledger, new MemoryAgentRunLog());

        Assert.Equal(AgentTaskStatus.Failed, result.Task.Status);
        Assert.Contains(result.Task.Steps, step => step.Status == ToolStepStatus.Denied);
        Assert.True(result.Task.Goal is null || !result.Task.Goal.Met);
        Assert.Equal(0, result.ReadCalls);
        Assert.Equal(0, ledger.Calls);
    }

    private static AgentRequest Request(string doneWhen, BusinessRequest? business, IReadOnlyList<AgentStepSpec> plan)
        => new(
            "completeness-task",
            "completeness-task",
            Tenant,
            "Assess decision support.",
            doneWhen,
            [EvidenceReviewRuntime.ReadPermission],
            plan,
            null,
            VerifyGoal: true,
            Business: business);

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
