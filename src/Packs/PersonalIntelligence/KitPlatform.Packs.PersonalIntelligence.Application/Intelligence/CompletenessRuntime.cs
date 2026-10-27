using KitPlatform.Packs.PersonalIntelligence.Agent;

namespace KitPlatform.Packs.PersonalIntelligence;

public sealed record DecisionSupportCoverage(int Decisions, int DecisionsWithEvidence, int EvidenceRows, int EvidenceWithContent);

public interface IDecisionSupportLedger
{
    DecisionSupportCoverage Read(Guid tenantId);
}

/// <summary>
/// Executes a decision-support completeness request. The plan comes from <see cref="BusinessUnderstanding"/>.
/// </summary>
public sealed class CompletenessRuntime
{
    public CompletenessResult Execute(AgentRequest request, IDecisionSupportLedger ledger, IAgentRunLog log)
    {
        var reads = 0;
        var interpreted = BusinessUnderstanding.Interpret(request.Objective, request.DoneWhen, request.Business);
        var understood = interpreted.Understanding;
        var registry = new ToolRegistry();
        registry.Register(
            new ToolContract(BusinessUnderstanding.DiscoverTool, EvidenceReviewRuntime.ReadPermission, ToolEffect.Read, false),
            _ => Discover(understood));
        registry.Register(
            new ToolContract(BusinessUnderstanding.CollectTool, EvidenceReviewRuntime.ReadPermission, ToolEffect.Read, false),
            call =>
            {
                if (understood.State != UnderstandingState.ReadyToPlan || !understood.Plan.Any(step => step.StepId == call.StepId))
                    return Stop(understood);
                reads++;
                var row = ledger.Read(request.TenantId);
                if (row.Decisions < 0 || row.DecisionsWithEvidence < 0 || row.EvidenceRows < 0 || row.EvidenceWithContent < 0 || row.DecisionsWithEvidence > row.Decisions)
                    return ToolResponse.Stop(ToolStepStatus.Failed, "The decision-support coverage was not usable.");
                return ToolResponse.Succeeded(
                    "Decision and evidence coverage was read for this tenant.",
                    new Dictionary<string, string>
                    {
                        ["verified"] = "true",
                        ["scope"] = understood.Scope,
                        ["understanding"] = RequestUnderstanding.Code(UnderstandingState.ReadyToPlan),
                        ["decisions"] = row.Decisions.ToString(),
                        ["linked"] = row.DecisionsWithEvidence.ToString(),
                        ["evidenceRows"] = row.EvidenceRows.ToString(),
                        ["content"] = row.EvidenceWithContent.ToString(),
                        ["fact"] = Facts(request.TenantId, row),
                    });
            });
        registry.Register(
            new ToolContract(BusinessUnderstanding.JudgeTool, EvidenceReviewRuntime.ReadPermission, ToolEffect.Read, false),
            call => Judge(request, interpreted, call));
        var receipt = new AgentRuntime(registry).Execute(request with { Plan = understood.Plan }, log);
        return new CompletenessResult(receipt, reads);
    }

    private static ToolResponse Discover(UnderstoodRequest understood)
    {
        if (understood.State != UnderstandingState.ReadyToPlan)
            return Stop(understood);
        return ToolResponse.Succeeded(
            "The infeasible thesis comparison was excluded before the coverage read.",
            new Dictionary<string, string>
            {
                ["verified"] = "true",
                ["scope"] = understood.Scope,
                ["understanding"] = RequestUnderstanding.Code(UnderstandingState.ReadyToPlan),
                ["limit"] = "Comparing a decision thesis with evidence payload content is not registered and is not executed.",
                ["assumption"] = string.Join('\n', understood.Assumptions),
            });
    }

    private static ToolResponse Judge(AgentRequest request, BusinessInterpretation interpreted, ToolCall call)
    {
        var understood = interpreted.Understanding;
        if (understood.State != UnderstandingState.ReadyToPlan)
            return Stop(understood);
        if (!call.Inputs.TryGetValue("decisions", out var decisionsText)
            || !call.Inputs.TryGetValue("linked", out var linkedText)
            || !call.Inputs.TryGetValue("evidenceRows", out var evidenceText)
            || !call.Inputs.TryGetValue("content", out var contentText)
            || !int.TryParse(decisionsText, out var decisions)
            || !int.TryParse(linkedText, out var linked)
            || !int.TryParse(evidenceText, out var evidenceRows)
            || !int.TryParse(contentText, out var content))
            return ToolResponse.Stop(ToolStepStatus.Failed, "The decision-support coverage was not collected.");

        var missing = new List<string>();
        if (interpreted.FeasibleNeeds.Contains(BusinessUnderstanding.Decisions) && decisions < 1)
            missing.Add("recorded decisions");
        if (interpreted.FeasibleNeeds.Contains(BusinessUnderstanding.Linked) && (decisions < 1 || linked != decisions))
            missing.Add("decisions without evidence on the same context");
        if (interpreted.FeasibleNeeds.Contains(BusinessUnderstanding.Content) && content < 1)
            missing.Add("evidence rows with a non-empty payload");
        foreach (var unverifiable in interpreted.UnverifiableNeeds)
            missing.Add($"{unverifiable} is not a registered capability.");

        var lines = new Dictionary<string, string>
        {
            ["verified"] = "true",
            ["scope"] = understood.Scope,
            ["fact"] = Facts(request.TenantId, new DecisionSupportCoverage(decisions, linked, evidenceRows, content)),
            ["inference"] = "A shared context id is not a reading of decision text or payload content.",
            ["assumption"] = "An empty JSON payload is not evidence content.\n" + string.Join('\n', understood.Assumptions),
            ["limit"] = "Comparing a decision thesis with evidence payload content is not registered and was not executed.\nOther tenants are not read.\n" + string.Join('\n', understood.Risks),
        };
        if (missing.Count > 0)
        {
            lines["goalMet"] = "false";
            lines["understanding"] = GoalTaskRuntime.ExecutedUnproven;
            lines["goalReason"] = "The feasible coverage was read and does not prove every extracted criterion.";
            lines["missing"] = string.Join('\n', missing);
            return ToolResponse.Succeeded("The coverage does not prove the request.", lines);
        }

        lines["goalMet"] = "true";
        lines["understanding"] = GoalTaskRuntime.Proven;
        lines["goalReason"] = "Every extracted criterion holds for the coverage that was read.";
        return ToolResponse.Succeeded("The coverage proves the extracted criteria.", lines);
    }

    private static ToolResponse Stop(UnderstoodRequest understood)
        => ToolResponse.Stop(
            ToolStepStatus.Failed,
            understood.Reason,
            new Dictionary<string, string>
            {
                ["goalMet"] = "false",
                ["understanding"] = RequestUnderstanding.Code(understood.State),
                ["goalReason"] = understood.Reason,
                ["scope"] = understood.Scope,
                ["missing"] = string.Join('\n', understood.Missing),
                ["assumption"] = string.Join('\n', understood.Assumptions),
                ["limit"] = string.Join('\n', understood.Risks),
            });

    private static string Facts(Guid tenantId, DecisionSupportCoverage row)
        => string.Join(
            '\n',
            $"Tenant {tenantId:D} has {row.Decisions} decisions in pack_personal_intelligence.pi_decision.",
            $"Tenant {tenantId:D} has {row.DecisionsWithEvidence} decisions with evidence on the same context.",
            $"Tenant {tenantId:D} has {row.EvidenceRows} evidence rows and {row.EvidenceWithContent} with a non-empty payload.");
}

public sealed record CompletenessResult(AgentTaskReceipt Task, int ReadCalls);

public sealed class RefusingDecisionSupportLedger : IDecisionSupportLedger
{
    public DecisionSupportCoverage Read(Guid tenantId)
        => throw new InvalidOperationException("This request does not have a plan that reads decision support.");
}
