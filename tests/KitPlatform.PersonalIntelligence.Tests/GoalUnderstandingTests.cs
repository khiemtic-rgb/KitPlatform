using KitPlatform.Packs.PersonalIntelligence;
using KitPlatform.Packs.PersonalIntelligence.Agent;
using KitPlatform.Packs.PersonalIntelligence.ResearchHost;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class GoalUnderstandingTests
{
    private static readonly Guid Tenant = Guid.Parse("a1111111-1111-1111-1111-111111111111");

    [Fact]
    public void A_structured_comparison_is_planned_from_the_count_capability()
    {
        var sentence = "Observation rows outnumber fact rows.";
        Assert.False(GoalContract.TryMatch(sentence, EvidenceReviewRuntime.Criteria, out _));
        Assert.False(GoalContract.TryMatch(sentence, TradingResearchRuntime.Criteria, out _));
        var understood = GoalTaskRuntime.Understand("Compare stored evidence.", sentence, [Compare()]);

        Assert.Equal(UnderstandingState.ReadyToPlan, understood.State);
        Assert.Equal("ready-to-plan", RequestUnderstanding.Code(understood.State));
        Assert.False(understood.ApprovalRequired);
        Assert.Contains(EvidenceReviewRuntime.ReadPermission, understood.Permissions);
        Assert.DoesNotContain("personal_intelligence.trading.run", understood.Permissions);
        Assert.Equal(["collect-FACT", "collect-OBSERVATION", "assess"], understood.Plan.Select(step => step.StepId).ToArray());
        Assert.Equal("collect-FACT", understood.Plan[1].DependsOn);
        Assert.Equal("collect-OBSERVATION", understood.Plan[2].DependsOn);
        Assert.All(understood.Plan, step => Assert.Contains(EvidenceReviewRuntime.ReadPermission, step.Purpose));
        Assert.Contains(understood.Plan, step => step.ToolName == EvidenceReviewRuntime.CollectTool);
        Assert.DoesNotContain(understood.Plan, step => step.ToolName is "trading.paper_execute" or "evidence.payload");
        Assert.Contains("pack_personal_intelligence.pi_evidence", understood.AllowedSources);
    }

    [Fact]
    public void An_unstructured_sentence_is_not_understood_and_does_not_read()
    {
        var understood = GoalTaskRuntime.Understand("Improve the tenant.", "Make it good.", null);
        var ledger = new MapLedger(("OBSERVATION", 2));
        var result = new GoalTaskRuntime().Execute(Request("Make it good.", null, understood.Plan), ledger, new MemoryAgentRunLog());

        Assert.Equal(UnderstandingState.NotUnderstood, understood.State);
        Assert.DoesNotContain(understood.Plan, step => step.StepId.StartsWith("collect", StringComparison.Ordinal));
        Assert.Equal(AgentTaskStatus.Failed, result.Task.Status);
        Assert.False(result.Task.Goal!.Met);
        Assert.Equal("not-understood", result.Task.Goal.Understanding);
        Assert.Contains("Make it good.", result.Task.Goal.Missing);
        Assert.Equal(0, result.ReadCalls);
        Assert.Equal(0, ledger.Calls);
    }

    [Fact]
    public void An_unknown_capability_is_a_gap_and_a_missing_subject_is_not_a_read()
    {
        var payload = GoalTaskRuntime.Understand("Read the payload.", "Summarize the evidence payload.", [Ask("payload", "evidence.payload", "OBSERVATION", "atLeast", "1")]);
        var opinion = GoalTaskRuntime.Understand("Count opinions.", "Opinions are present.", [Ask("opinion", "evidence.count", "OPINION", "atLeast", "1")]);
        var mixed = GoalTaskRuntime.Understand("Compare and invent.", "Do both.", [Compare(), Ask("payload", "evidence.payload", "OBSERVATION", "atLeast", "1")]);
        var ledger = new MapLedger(("OBSERVATION", 2), ("FACT", 0));

        Assert.Equal(UnderstandingState.MissingCapability, payload.State);
        Assert.Contains("evidence.payload", payload.Gaps);
        Assert.Contains("No registered tool answers evidence.payload.", payload.Missing);
        Assert.DoesNotContain(payload.Plan, step => step.ToolName == "evidence.payload");
        Assert.Equal(UnderstandingState.MissingData, opinion.State);
        Assert.Contains(opinion.Missing, item => item.Contains("OPINION", StringComparison.Ordinal));
        Assert.Equal(UnderstandingState.MissingCapability, mixed.State);
        Assert.DoesNotContain(mixed.Plan, step => step.StepId.StartsWith("collect", StringComparison.Ordinal));
        var executed = new GoalTaskRuntime().Execute(Request("Summarize the evidence payload.", [Ask("payload", "evidence.payload", "OBSERVATION", "atLeast", "1")], payload.Plan), ledger, new MemoryAgentRunLog());
        Assert.Equal("missing-capability", executed.Task.Goal!.Understanding);
        Assert.False(executed.Task.Goal.Met);
        Assert.Equal(0, executed.ReadCalls);
        Assert.Equal(0, ledger.Calls);
    }

    [Fact]
    public void A_read_comparison_meets_only_the_counts_and_a_client_plan_is_ignored()
    {
        var asks = new[] { Compare() };
        var ledger = new MapLedger(("OBSERVATION", 2), ("FACT", 0));
        var log = new MemoryAgentRunLog();
        var request = Request("Observation rows outnumber fact rows.", asks, [new AgentStepSpec("execute", "trading.paper_execute", null, "Do not run this.")]);
        var runtime = new GoalTaskRuntime();
        var first = runtime.Execute(request, ledger, log);
        var second = runtime.Execute(request, ledger, log);

        Assert.Equal(AgentTaskStatus.Completed, first.Task.Status);
        Assert.True(first.Task.Goal!.Met);
        Assert.Equal(GoalTaskRuntime.Proven, first.Task.Goal.Understanding);
        Assert.Contains(first.Task.Goal.Facts, fact => fact.Contains("2 OBSERVATION", StringComparison.Ordinal) && fact.Contains("pi_evidence", StringComparison.Ordinal));
        Assert.Contains(first.Task.Goal.Facts, fact => fact.Contains("0 FACT", StringComparison.Ordinal));
        Assert.Contains("OBSERVATION-source", first.Task.EvidenceRefs);
        Assert.DoesNotContain(first.Task.Plan!, step => step.ToolName == "trading.paper_execute");
        Assert.Equal(2, first.ReadCalls);
        Assert.Equal(2, ledger.Calls);
        Assert.True(second.Task.Replayed);
        Assert.Equal(0, second.ReadCalls);
        Assert.Equal(2, ledger.Calls);
        Assert.False(first.Task.LiveAccepted);
    }

    [Fact]
    public void A_missing_fact_count_is_executed_and_not_marked_met()
    {
        var asks = new[] { Ask("facts", "evidence.count", "FACT", "atLeast", "1"), Ask("observations", "evidence.count", "OBSERVATION", "atLeast", "1") };
        var ledger = new MapLedger(("OBSERVATION", 2), ("FACT", 0));
        var result = new GoalTaskRuntime().Execute(Request("Both kinds are present.", asks, GoalTaskRuntime.Understand("Both kinds are present.", "Both kinds are present.", asks).Plan), ledger, new MemoryAgentRunLog());

        Assert.Equal(AgentTaskStatus.Failed, result.Task.Status);
        Assert.False(result.Task.Goal!.Met);
        Assert.Equal(GoalTaskRuntime.ExecutedUnproven, result.Task.Goal.Understanding);
        Assert.Contains("FACT evidence rows", result.Task.Goal.Missing);
        Assert.Contains(result.Task.Steps, step => step.StepId == "collect-FACT" && step.Status == ToolStepStatus.Succeeded);
        Assert.Contains(result.Task.Steps, step => step.StepId == "collect-OBSERVATION" && step.Status == ToolStepStatus.Succeeded);
        Assert.Contains(result.Task.Steps, step => step.StepId == "assess" && step.Status == ToolStepStatus.Succeeded);
        Assert.Equal(2, ledger.Calls);
    }

    [Fact]
    public void Trading_permission_does_not_authorize_an_evidence_read()
    {
        var ledger = new MapLedger(("OBSERVATION", 2), ("FACT", 0));
        var understood = GoalTaskRuntime.Understand("Compare stored evidence.", "Observation rows outnumber fact rows.", [Compare()]);
        var request = new AgentRequest(
            "goal-denied",
            "goal-denied",
            Tenant,
            "Compare stored evidence.",
            "Observation rows outnumber fact rows.",
            ["personal_intelligence.trading.run"],
            understood.Plan,
            null,
            VerifyGoal: true,
            Asks: [Compare()]);
        var result = new GoalTaskRuntime().Execute(request, ledger, new MemoryAgentRunLog());

        Assert.Equal(AgentTaskStatus.Failed, result.Task.Status);
        Assert.Contains(result.Task.Steps, step => step.Status == ToolStepStatus.Denied);
        Assert.True(result.Task.Goal is null || !result.Task.Goal.Met);
        Assert.Equal(0, result.ReadCalls);
        Assert.Equal(0, ledger.Calls);
        Assert.False(result.Task.LiveAccepted);
    }

    private static CompletionAsk Compare()
        => Ask("obs-over-fact", "evidence.count", "OBSERVATION", "greaterThan", "FACT");

    private static CompletionAsk Ask(string id, string capability, string subject, string relation, string target)
        => new() { Id = id, Capability = capability, Subject = subject, Relation = relation, Target = target };

    private static AgentRequest Request(string doneWhen, IReadOnlyList<CompletionAsk>? asks, IReadOnlyList<AgentStepSpec> plan)
        => new(
            "goal-task",
            "goal-task",
            Tenant,
            "Compare stored evidence.",
            doneWhen,
            [EvidenceReviewRuntime.ReadPermission],
            plan,
            null,
            VerifyGoal: true,
            Asks: asks);

    private sealed class MapLedger : IEvidenceLedger
    {
        private readonly Dictionary<string, int> _counts;
        public int Calls { get; private set; }

        public MapLedger(params (string Kind, int Count)[] rows)
            => _counts = rows.ToDictionary(row => row.Kind, row => row.Count, StringComparer.Ordinal);

        public EvidenceObservation Read(Guid tenantId, string epistemicKind)
        {
            Calls++;
            var count = _counts.GetValueOrDefault(epistemicKind);
            return new EvidenceObservation(epistemicKind, count, count > 0 ? epistemicKind + "-source" : null);
        }
    }
}
