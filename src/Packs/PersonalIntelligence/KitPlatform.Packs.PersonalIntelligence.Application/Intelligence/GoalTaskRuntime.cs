using KitPlatform.Packs.PersonalIntelligence.Agent;

namespace KitPlatform.Packs.PersonalIntelligence;

/// <summary>
/// Plans a goal from structured conditions and the evidence count capability.
/// It does not parse free-form text and it does not register a tool the catalog does not have.
/// </summary>
public sealed class GoalTaskRuntime
{
    public const string Kind = "goal";
    public const string Proven = "proven";
    public const string ExecutedUnproven = "executed-unproven";

    public static CapabilityDefinition EvidenceCount { get; } = new(
        "evidence.count",
        EvidenceReviewRuntime.CollectTool,
        EvidenceReviewRuntime.ReadPermission,
        ToolEffect.Read,
        false,
        "pack_personal_intelligence.pi_evidence",
        EvidenceReviewRuntime.Scope,
        new HashSet<string>(StringComparer.Ordinal) { "OBSERVATION", "FACT" },
        new HashSet<string>(StringComparer.Ordinal) { "atLeast", "greaterThan" });

    public static CapabilityDefinition PaperDecision { get; } = new(
        "trading.paper_execute",
        "trading.paper_execute",
        "personal_intelligence.trading.run",
        ToolEffect.Paper,
        true,
        "research_trading.paper_operation",
        "Naming the paper action does not execute it. A person has to decide through the trading approval gate.",
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal));

    public static IReadOnlyList<CapabilityDefinition> Catalog { get; } = [EvidenceCount, PaperDecision];

    public static UnderstoodRequest Understand(string objective, string doneWhen, IReadOnlyList<CompletionAsk>? asks)
        => RequestUnderstanding.Analyze(objective, doneWhen, asks, Catalog, EvidenceReviewRuntime.AssessTool);

    public GoalTaskResult Execute(AgentRequest request, IEvidenceLedger ledger, IAgentRunLog log)
    {
        var reads = 0;
        var understood = Understand(request.Objective, request.DoneWhen, request.Asks);
        var registry = new ToolRegistry();
        registry.Register(
            new ToolContract(EvidenceReviewRuntime.CollectTool, EvidenceReviewRuntime.ReadPermission, ToolEffect.Read, false),
            call =>
            {
                if (understood.State != UnderstandingState.ReadyToPlan)
                    return Gap(understood);
                var kind = KindOf(call.StepId);
                if (kind is null || !understood.Plan.Any(step => step.StepId == call.StepId))
                    return Gap(understood);
                reads++;
                var row = ledger.Read(request.TenantId, kind);
                if (!string.Equals(row.EpistemicKind, kind, StringComparison.Ordinal))
                    return ToolResponse.Stop(ToolStepStatus.Failed, "The evidence ledger returned a different kind.");
                var outputs = new Dictionary<string, string>
                {
                    ["verified"] = "true",
                    ["scope"] = EvidenceReviewRuntime.Scope,
                    ["understanding"] = RequestUnderstanding.Code(UnderstandingState.ReadyToPlan),
                    ["count." + kind] = row.Count.ToString(),
                    ["fact"] = Fact(request.TenantId, row),
                    ["evidence"] = string.IsNullOrWhiteSpace(row.SampleSourceRef) ? "none" : row.SampleSourceRef,
                };
                foreach (var pair in call.Inputs)
                {
                    if (pair.Key.StartsWith("count.", StringComparison.Ordinal))
                        outputs[pair.Key] = pair.Value;
                }

                outputs["count." + kind] = row.Count.ToString();
                if (call.Inputs.TryGetValue("fact", out var prior) && !string.IsNullOrWhiteSpace(prior))
                    outputs["fact"] = prior + "\n" + outputs["fact"];
                return ToolResponse.Succeeded($"Read {row.Count} {kind} rows for this tenant.", outputs);
            });
        registry.Register(
            new ToolContract(EvidenceReviewRuntime.AssessTool, EvidenceReviewRuntime.ReadPermission, ToolEffect.Read, false),
            call => Assess(request, understood, call));
        var receipt = new AgentRuntime(registry).Execute(request with { Plan = understood.Plan }, log);
        return new GoalTaskResult(receipt, reads);
    }

    private static ToolResponse Assess(AgentRequest request, UnderstoodRequest understood, ToolCall call)
    {
        if (understood.State != UnderstandingState.ReadyToPlan)
            return Gap(understood);
        var unmet = new List<string>();
        var facts = new List<string>();
        foreach (var ask in request.Asks ?? [])
        {
            var subject = ask.Subject.Trim();
            if (!call.Inputs.TryGetValue("count." + subject, out var leftText) || !int.TryParse(leftText, out var left))
                return ToolResponse.Stop(ToolStepStatus.Failed, "The evidence count was not collected.");
            facts.Add(FactLine(request.TenantId, subject, left));
            if (ask.Relation.Trim() == "atLeast")
            {
                var target = int.Parse(ask.Target.Trim());
                if (left < target)
                    unmet.Add($"{subject} evidence rows");
            }
            else
            {
                var other = ask.Target.Trim();
                if (!call.Inputs.TryGetValue("count." + other, out var rightText) || !int.TryParse(rightText, out var right))
                    return ToolResponse.Stop(ToolStepStatus.Failed, "The evidence count was not collected.");
                facts.Add(FactLine(request.TenantId, other, right));
                if (left <= right)
                    unmet.Add($"{subject} greater than {other}");
            }
        }

        var lines = Report(request, understood);
        lines["fact"] = string.Join('\n', facts.Distinct(StringComparer.Ordinal));
        lines["verified"] = "true";
        if (unmet.Count > 0)
        {
            lines["goalMet"] = "false";
            lines["understanding"] = ExecutedUnproven;
            lines["goalReason"] = "The counts were read and do not meet every completion condition.";
            lines["missing"] = string.Join('\n', unmet);
            return ToolResponse.Succeeded("The evidence does not meet the completion conditions.", lines);
        }

        lines["goalMet"] = "true";
        lines["understanding"] = Proven;
        lines["goalReason"] = "Every structured condition holds for the counts that were read.";
        return ToolResponse.Succeeded("The evidence meets the structured conditions.", lines);
    }

    private static ToolResponse Gap(UnderstoodRequest understood)
    {
        var lines = new Dictionary<string, string>
        {
            ["goalMet"] = "false",
            ["understanding"] = RequestUnderstanding.Code(understood.State),
            ["goalReason"] = understood.Reason,
            ["scope"] = understood.Scope,
            ["missing"] = string.Join('\n', understood.Missing),
            ["assumption"] = string.Join('\n', understood.Assumptions),
            ["limit"] = string.Join('\n', understood.Risks.Concat(["No evidence row was read for this request."])),
        };
        return ToolResponse.Stop(ToolStepStatus.Failed, understood.Reason, lines);
    }

    private static Dictionary<string, string> Report(AgentRequest request, UnderstoodRequest understood)
        => new()
        {
            ["scope"] = understood.Scope,
            ["assumption"] = string.Join('\n', understood.Assumptions),
            ["inference"] = "The row count is an observation of stored rows. It is not a reading of payload content.",
            ["limit"] = string.Join('\n', understood.Risks.Concat(
            [
                "The plan was accepted before the rows were read. A succeeded step is not the goal.",
                "Only OBSERVATION and FACT counts for the caller tenant are available.",
                $"Objective recorded: {OneLine(request.Objective)}",
            ])),
        };

    private static string? KindOf(string stepId)
    {
        const string prefix = "collect-";
        if (!stepId.StartsWith(prefix, StringComparison.Ordinal))
            return null;
        var kind = stepId[prefix.Length..];
        return EvidenceCount.Subjects.Contains(kind) ? kind : null;
    }

    private static string Fact(Guid tenantId, EvidenceObservation row)
        => FactLine(tenantId, row.EpistemicKind, row.Count) + (row.Count > 0 && !string.IsNullOrWhiteSpace(row.SampleSourceRef) ? $" Sample source_ref {row.SampleSourceRef}." : "");

    private static string FactLine(Guid tenantId, string kind, int count)
        => $"Tenant {tenantId:D} has {count} {kind} rows in pack_personal_intelligence.pi_evidence.";

    private static string OneLine(string text)
    {
        var value = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return value.Length <= 180 ? value : value[..180];
    }
}

public sealed record GoalTaskResult(AgentTaskReceipt Task, int ReadCalls);

public sealed class RefusingEvidenceLedger : IEvidenceLedger
{
    public EvidenceObservation Read(Guid tenantId, string epistemicKind)
        => throw new InvalidOperationException("This request does not have a plan that reads evidence.");
}
