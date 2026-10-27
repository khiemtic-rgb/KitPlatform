using KitPlatform.Packs.PersonalIntelligence.Agent;

namespace KitPlatform.Packs.PersonalIntelligence;

public sealed record EvidenceObservation(string EpistemicKind, int Count, string? SampleSourceRef);

public interface IEvidenceLedger
{
    EvidenceObservation Read(Guid tenantId, string epistemicKind);
}

public sealed record EvidenceReviewResult(AgentTaskReceipt Task, int CollectCalls);

/// <summary>
/// Read-only evidence review. It does not use the trading plan or write a paper operation.
/// </summary>
public sealed class EvidenceReviewRuntime
{
    public const string Kind = "evidence-review";
    public const string CollectTool = "evidence.collect";
    public const string AssessTool = "evidence.assess";
    public const string ReadPermission = "personal_intelligence.evidence.read";
    public const string Scope = "evidence-review; pack_personal_intelligence.pi_evidence; caller tenant only";
    public const string ObservationStatement = "The tenant has at least one observation evidence row.";
    public const string FactStatement = "The tenant has at least one fact evidence row.";

    public static readonly IReadOnlyList<TaskCriterion> Criteria =
    [
        new("observation-present", ObservationStatement),
        new("fact-present", FactStatement),
    ];

    public static IReadOnlyList<AgentStepSpec> PlanFor(string objective, string doneWhen)
    {
        var goal = OneLine(objective, "the evidence objective");
        if (!GoalContract.TryMatch(doneWhen, Criteria, out var criterion) || criterion is null)
        {
            return
            [
                new("assess", AssessTool, null, $"Record that this completion condition is not verifiable: {OneLine(doneWhen, "the completion condition")}"),
            ];
        }

        return
        [
            new("collect", CollectTool, null, $"Read {criterion.Id} rows for: {goal}"),
            new("assess", AssessTool, "collect", $"Decide whether the stored rows meet: {criterion.Statement}"),
        ];
    }

    public EvidenceReviewResult Execute(AgentRequest request, IEvidenceLedger ledger, IAgentRunLog log)
    {
        var calls = 0;
        var state = new State();
        var registry = new ToolRegistry();
        registry.Register(
            new ToolContract(CollectTool, ReadPermission, ToolEffect.Read, false),
            _ =>
            {
                calls++;
                return Collect(request, ledger, state);
            });
        registry.Register(
            new ToolContract(AssessTool, ReadPermission, ToolEffect.Read, false),
            call => Assess(request, state, call));
        var receipt = new AgentRuntime(registry).Execute(request, log);
        return new EvidenceReviewResult(receipt, calls);
    }

    private static ToolResponse Collect(AgentRequest request, IEvidenceLedger ledger, State state)
    {
        if (!GoalContract.TryMatch(request.DoneWhen, Criteria, out var criterion) || criterion is null)
            return Unverified(request.DoneWhen);
        var kind = KindOf(criterion);
        var row = ledger.Read(request.TenantId, kind);
        if (!string.Equals(row.EpistemicKind, kind, StringComparison.Ordinal))
            return ToolResponse.Stop(ToolStepStatus.Failed, "The evidence ledger returned a different kind.");
        state.Row = row;
        var fact = $"Tenant {request.TenantId:D} has {row.Count} {row.EpistemicKind} rows in pack_personal_intelligence.pi_evidence.";
        if (row.Count > 0 && !string.IsNullOrWhiteSpace(row.SampleSourceRef))
            fact += $"\nSample source_ref {row.SampleSourceRef}.";
        return ToolResponse.Succeeded(
            "Evidence rows were read for this tenant.",
            new Dictionary<string, string>
            {
                ["verified"] = "true",
                ["scope"] = Scope,
                ["fact"] = fact,
                ["count"] = row.Count.ToString(),
                ["kind"] = row.EpistemicKind,
                ["evidence"] = row.SampleSourceRef ?? "none",
            });
    }

    private static ToolResponse Assess(AgentRequest request, State state, ToolCall call)
    {
        if (!GoalContract.TryMatch(request.DoneWhen, Criteria, out var criterion) || criterion is null)
            return Unverified(request.DoneWhen);
        var row = state.Row;
        if (row is null
            && call.Inputs.TryGetValue("count", out var countText)
            && call.Inputs.TryGetValue("kind", out var kind)
            && int.TryParse(countText, out var count))
        {
            call.Inputs.TryGetValue("evidence", out var sample);
            row = new EvidenceObservation(kind, count, sample == "none" ? null : sample);
        }

        if (row is null)
            return ToolResponse.Stop(ToolStepStatus.Failed, "The evidence count was not collected.");
        var lines = Report(request, row);
        if (row.Count < 1)
        {
            lines["verified"] = "true";
            lines["goalMet"] = "false";
            lines["goalReason"] = $"The stored {row.EpistemicKind} count is 0, so the completion condition is not met.";
            lines["missing"] = $"{row.EpistemicKind} evidence rows";
            return ToolResponse.Succeeded("The evidence does not meet the completion condition.", lines);
        }

        lines["verified"] = "true";
        lines["goalMet"] = "true";
        lines["goalReason"] = $"The tenant has {row.Count} {row.EpistemicKind} evidence rows, which meets the declared condition.";
        lines["evidence"] = row.SampleSourceRef ?? "none";
        return ToolResponse.Succeeded("The evidence meets the declared condition.", lines);
    }

    private static Dictionary<string, string> Report(AgentRequest request, EvidenceObservation row)
        => new()
        {
            ["scope"] = Scope,
            ["fact"] = $"Tenant {request.TenantId:D} has {row.Count} {row.EpistemicKind} rows in pack_personal_intelligence.pi_evidence.",
            ["inference"] = "The row count is an observation of stored rows. It is not a reading of payload content.",
            ["assumption"] = "epistemic_kind is compared as the stored text.",
            ["limit"] = "Other tenants are not read. An empty payload is not treated as a fact. Free-form business language is not interpreted.",
        };

    private static ToolResponse Unverified(string doneWhen)
        => ToolResponse.Stop(
            ToolStepStatus.Failed,
            "The completion condition is not one this domain can verify.",
            new Dictionary<string, string>
            {
                ["goalMet"] = "false",
                ["goalReason"] = "The completion condition is not one this domain can verify.",
                ["missing"] = doneWhen,
                ["scope"] = Scope,
                ["limit"] = "Only a declared evidence criterion can be verified. Free-form business language is not interpreted.",
            });

    private static string KindOf(TaskCriterion criterion)
        => criterion.Id == "fact-present" ? "FACT" : "OBSERVATION";

    private static string OneLine(string text, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(text) ? fallback : text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return value.Length <= 180 ? value : value[..180];
    }

    private sealed class State
    {
        public EvidenceObservation? Row { get; set; }
    }
}
