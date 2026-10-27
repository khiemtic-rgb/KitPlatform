using KitPlatform.Packs.PersonalIntelligence;
using KitPlatform.Packs.PersonalIntelligence.Agent;
using KitPlatform.Packs.PersonalIntelligence.ResearchHost;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class EvidenceValidationTests
{
    private static readonly Guid Tenant = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    private const string Sentence = "Validate whether the stored evidence supports a specific decision.";

    [Fact]
    public void A_validation_sentence_is_not_a_registered_statement_or_a_completeness_route()
    {
        Assert.False(GoalContract.TryMatch(Sentence, EvidenceReviewRuntime.Criteria, out _));
        Assert.False(GoalContract.TryMatch(Sentence, TradingResearchRuntime.Criteria, out _));
        Assert.Null(EvidenceValidation.Interpret("Assess decision support.", "Is the stored evidence complete enough to support the tenant's decisions?", null));
        var interpreted = EvidenceValidation.Interpret("Assess the rows.", Sentence, null);

        Assert.NotNull(interpreted);
        Assert.Equal(UnderstandingState.ReadyToPlan, interpreted.Understanding.State);
        Assert.Equal(
            [EvidenceValidation.Relation, EvidenceValidation.Payload, EvidenceValidation.Observation],
            interpreted.FeasibleNeeds.ToArray());
        Assert.Equal([EvidenceValidation.Support], interpreted.UnverifiableNeeds.ToArray());
        Assert.Equal(["discover", "collect", "assess"], interpreted.Understanding.Plan.Select(step => step.StepId).ToArray());
        Assert.Equal(EvidenceValidation.CollectTool, interpreted.Understanding.Plan[1].ToolName);
        Assert.DoesNotContain(interpreted.Understanding.Plan, step => step.ToolName is "trading.paper_execute" or "decision.support-coverage" or "thesis.read");
        Assert.Contains(interpreted.Understanding.Plan, step => step.Purpose!.Contains("before any read", StringComparison.Ordinal));
        var vietnamese = EvidenceValidation.Interpret("Xem lại.", "Hãy đánh giá bằng chứng cho quyết định này.", null);
        Assert.Equal(UnderstandingState.ReadyToPlan, vietnamese!.Understanding.State);
    }

    [Fact]
    public void A_partial_validation_names_the_missing_subject_and_an_unknown_need_does_not_read()
    {
        var partial = EvidenceValidation.Interpret("Review the request.", "Validate the evidence.", null);
        Assert.Equal(UnderstandingState.MissingData, partial!.Understanding.State);
        Assert.Contains(partial.Understanding.Missing, item => item.Contains("subject", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(partial.Understanding.Plan, step => step.StepId == "collect");

        var ledger = new QualityLedger(Sample(statements: 1));
        var unknown = new EvidenceAssessmentRuntime().Execute(
            Request(Sentence, Business("live-order"), []),
            ledger,
            new MemoryAgentRunLog());
        Assert.Equal(UnderstandingState.MissingCapability, EvidenceValidation.Interpret(Sentence, Sentence, Business("live-order"))!.Understanding.State);
        Assert.Equal(0, unknown.ReadCalls);
        Assert.Equal("missing-capability", unknown.Task.Goal!.Understanding);
    }

    [Fact]
    public void Support_stays_unassessed_when_the_rows_show_existence_and_a_matching_statement()
    {
        var row = new EvidenceQuality(4, 1, 3, 0, 5, 5, 2, 0);
        var ledger = new QualityLedger(row);
        var log = new MemoryAgentRunLog();
        var runtime = new EvidenceAssessmentRuntime();
        var request = Request(Sentence, null, [new AgentStepSpec("execute", "trading.paper_execute", null, "Do not run this.")]);
        var first = runtime.Execute(request, ledger, log);
        var second = runtime.Execute(request, ledger, log);

        Assert.Equal(AgentTaskStatus.Failed, first.Task.Status);
        Assert.False(first.Task.Goal!.Met);
        Assert.Equal(EvidenceAssessmentRuntime.UnableToAssess, first.Task.Goal.Understanding);
        Assert.Contains(first.Task.Goal.Facts, fact => fact.Contains("5 evidence rows", StringComparison.Ordinal));
        Assert.Contains(first.Task.Goal.Facts, fact => fact.Contains("2 evidence rows whose context summary", StringComparison.Ordinal));
        Assert.Contains(first.Task.Goal.Facts, fact => fact.Contains("0 evidence rows whose payload contains a statement", StringComparison.Ordinal));
        Assert.Contains(first.Task.Goal.Missing, item => item == "evidence rows with a payload statement");
        Assert.Contains(first.Task.Goal.Missing, item => item == "decisions with no context link while evidence exists");
        Assert.Contains(first.Task.Goal.Missing, item => item.Contains("supports-decision", StringComparison.Ordinal));
        Assert.Contains(first.Task.Goal.Inferences, item => item.Contains("shared context", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(first.Task.Plan!, step => step.ToolName == "trading.paper_execute");
        Assert.Equal(1, first.ReadCalls);
        Assert.True(second.Task.Replayed);
        Assert.Equal(0, second.ReadCalls);
    }

    [Fact]
    public void An_explicit_observation_can_hold_without_becoming_a_support_verdict()
    {
        var ledger = new QualityLedger(Sample(observation: 2, statements: 0, unlinked: 1, colocated: 3));
        var result = new EvidenceAssessmentRuntime().Execute(
            Request(Sentence, Business(EvidenceValidation.Observation), []),
            ledger,
            new MemoryAgentRunLog());

        Assert.Equal(AgentTaskStatus.Completed, result.Task.Status);
        Assert.True(result.Task.Goal!.Met);
        Assert.Equal(GoalTaskRuntime.Proven, result.Task.Goal.Understanding);
        Assert.Contains(result.Task.Goal.Facts, fact => fact.Contains("2 evidence rows whose context summary", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Task.Goal.Missing, item => item.Contains("payload", StringComparison.Ordinal));
        Assert.Contains(result.Task.Goal.Limits, item => item.Contains("not a support verdict", StringComparison.Ordinal));
        Assert.Equal(1, result.ReadCalls);
    }

    [Fact]
    public void A_payload_statement_and_a_context_relation_fail_separately_from_support()
    {
        var payload = new EvidenceAssessmentRuntime().Execute(
            Request(Sentence, Business(EvidenceValidation.Payload), []),
            new QualityLedger(Sample(statements: 0, evidence: 5, empty: 5)),
            new MemoryAgentRunLog());
        var relation = new EvidenceAssessmentRuntime().Execute(
            Request(Sentence, Business(EvidenceValidation.Relation), []),
            new QualityLedger(Sample(unlinked: 2, evidence: 5, colocated: 2, decisions: 4)),
            new MemoryAgentRunLog());
        var satisfied = new EvidenceAssessmentRuntime().Execute(
            Request(Sentence, Business(EvidenceValidation.Payload), []),
            new QualityLedger(Sample(statements: 1, evidence: 4, empty: 3)),
            new MemoryAgentRunLog());

        Assert.Equal(GoalTaskRuntime.ExecutedUnproven, payload.Task.Goal!.Understanding);
        Assert.False(payload.Task.Goal.Met);
        Assert.Contains(payload.Task.Goal.Missing, item => item == "evidence rows with a payload statement");
        Assert.DoesNotContain(payload.Task.Goal.Missing, item => item.Contains("context link", StringComparison.Ordinal));
        Assert.Equal(GoalTaskRuntime.ExecutedUnproven, relation.Task.Goal!.Understanding);
        Assert.Contains(relation.Task.Goal.Missing, item => item == "decisions with no context link while evidence exists");
        Assert.DoesNotContain(relation.Task.Goal.Missing, item => item.Contains("payload", StringComparison.Ordinal));
        Assert.Equal(GoalTaskRuntime.Proven, satisfied.Task.Goal!.Understanding);
        Assert.True(satisfied.Task.Goal.Met);
        Assert.Contains(satisfied.Task.Goal.Facts, fact => fact.Contains("1 evidence rows whose payload contains a statement", StringComparison.Ordinal));
    }

    [Fact]
    public void Trading_permission_does_not_read_evidence_quality()
    {
        var ledger = new QualityLedger(Sample());
        var request = new AgentRequest(
            "validation-task",
            "validation-task",
            Tenant,
            "Assess the rows.",
            Sentence,
            ["personal_intelligence.trading.run"],
            EvidenceValidation.Interpret("Assess the rows.", Sentence, null)!.Understanding.Plan,
            null,
            VerifyGoal: true);
        var result = new EvidenceAssessmentRuntime().Execute(request, ledger, new MemoryAgentRunLog());

        Assert.Equal(AgentTaskStatus.Failed, result.Task.Status);
        Assert.Contains(result.Task.Steps, step => step.Status == ToolStepStatus.Denied);
        Assert.True(result.Task.Goal is null || !result.Task.Goal.Met);
        Assert.Equal(0, result.ReadCalls);
        Assert.Equal(0, ledger.Calls);
    }

    private static BusinessRequest Business(params string[] needs)
        => new()
        {
            Intent = EvidenceValidation.Intent,
            Subject = EvidenceValidation.Subject,
            Needs = needs.ToList(),
        };

    private static EvidenceQuality Sample(
        int decisions = 4,
        int unlinked = 0,
        int colocated = 4,
        int gap = 0,
        int evidence = 4,
        int empty = 4,
        int observation = 1,
        int statements = 0)
        => new(decisions, unlinked, colocated, gap, evidence, empty, observation, statements);

    private static AgentRequest Request(string doneWhen, BusinessRequest? business, IReadOnlyList<AgentStepSpec> plan)
        => new(
            "validation-task",
            "validation-task",
            Tenant,
            "Assess the rows.",
            doneWhen,
            [EvidenceReviewRuntime.ReadPermission],
            plan,
            null,
            VerifyGoal: true,
            Business: business);

    private sealed class QualityLedger(EvidenceQuality row) : IEvidenceAssessmentLedger
    {
        public int Calls { get; private set; }

        public EvidenceQuality Read(Guid tenantId)
        {
            Calls++;
            return row;
        }
    }
}
