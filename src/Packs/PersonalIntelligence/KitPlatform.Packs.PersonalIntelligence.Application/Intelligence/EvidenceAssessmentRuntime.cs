using KitPlatform.Packs.PersonalIntelligence.Agent;

namespace KitPlatform.Packs.PersonalIntelligence;

public sealed record EvidenceQuality(
    int Decisions,
    int DecisionsWithoutContext,
    int DecisionsWithContextEvidence,
    int DecisionsWithContextAndNoEvidence,
    int EvidenceRows,
    int EmptyPayloadRows,
    int ObservationStatementRows,
    int PayloadStatementRows);

public interface IEvidenceAssessmentLedger
{
    EvidenceQuality Read(Guid tenantId);
}

/// <summary>
/// Reads evidence quality for one tenant and refuses a support verdict the rows cannot prove.
/// </summary>
public sealed class EvidenceAssessmentRuntime
{
    public const string UnableToAssess = "unable-to-assess";

    public EvidenceAssessmentResult Execute(AgentRequest request, IEvidenceAssessmentLedger ledger, IAgentRunLog log)
    {
        var reads = 0;
        var interpreted = EvidenceValidation.Interpret(request.Objective, request.DoneWhen, request.Business)
            ?? throw new InvalidOperationException("This request is not an evidence-validation request.");
        var understood = interpreted.Understanding;
        var registry = new ToolRegistry();
        registry.Register(
            new ToolContract(EvidenceValidation.ClassifyTool, EvidenceReviewRuntime.ReadPermission, ToolEffect.Read, false),
            _ => Discover(understood));
        registry.Register(
            new ToolContract(EvidenceValidation.CollectTool, EvidenceReviewRuntime.ReadPermission, ToolEffect.Read, false),
            call =>
            {
                if (understood.State != UnderstandingState.ReadyToPlan || !understood.Plan.Any(step => step.StepId == call.StepId))
                    return Stop(understood);
                reads++;
                var row = ledger.Read(request.TenantId);
                if (!Usable(row))
                    return ToolResponse.Stop(ToolStepStatus.Failed, "The evidence quality was not usable.");
                return ToolResponse.Succeeded(
                    "Evidence quality was read for this tenant.",
                    new Dictionary<string, string>
                    {
                        ["verified"] = "true",
                        ["scope"] = understood.Scope,
                        ["understanding"] = RequestUnderstanding.Code(UnderstandingState.ReadyToPlan),
                        ["decisions"] = row.Decisions.ToString(),
                        ["unlinked"] = row.DecisionsWithoutContext.ToString(),
                        ["colocated"] = row.DecisionsWithContextEvidence.ToString(),
                        ["contextGap"] = row.DecisionsWithContextAndNoEvidence.ToString(),
                        ["evidenceRows"] = row.EvidenceRows.ToString(),
                        ["emptyPayload"] = row.EmptyPayloadRows.ToString(),
                        ["observation"] = row.ObservationStatementRows.ToString(),
                        ["statements"] = row.PayloadStatementRows.ToString(),
                        ["fact"] = Facts(request.TenantId, row),
                    });
            });
        registry.Register(
            new ToolContract(EvidenceValidation.JudgeTool, EvidenceReviewRuntime.ReadPermission, ToolEffect.Read, false),
            call => Judge(request, interpreted, call));
        var receipt = new AgentRuntime(registry).Execute(request with { Plan = understood.Plan }, log);
        return new EvidenceAssessmentResult(receipt, reads);
    }

    private static bool Usable(EvidenceQuality row)
    {
        if (row.Decisions < 0 || row.DecisionsWithoutContext < 0 || row.DecisionsWithContextEvidence < 0 || row.DecisionsWithContextAndNoEvidence < 0)
            return false;
        if (row.EvidenceRows < 0 || row.EmptyPayloadRows < 0 || row.ObservationStatementRows < 0 || row.PayloadStatementRows < 0)
            return false;
        if (row.DecisionsWithoutContext + row.DecisionsWithContextEvidence + row.DecisionsWithContextAndNoEvidence != row.Decisions)
            return false;
        if (row.EmptyPayloadRows > row.EvidenceRows || row.ObservationStatementRows > row.EvidenceRows || row.PayloadStatementRows > row.EvidenceRows)
            return false;
        if (row.PayloadStatementRows + row.EmptyPayloadRows > row.EvidenceRows)
            return false;
        return true;
    }

    private static ToolResponse Discover(UnderstoodRequest understood)
    {
        if (understood.State != UnderstandingState.ReadyToPlan)
            return Stop(understood);
        return ToolResponse.Succeeded(
            "The support verdict was excluded before the quality read.",
            new Dictionary<string, string>
            {
                ["verified"] = "true",
                ["scope"] = understood.Scope,
                ["understanding"] = RequestUnderstanding.Code(UnderstandingState.ReadyToPlan),
                ["limit"] = "A shared context and an empty payload cannot prove that evidence supports a decision. Thesis text and source_ref are not read.",
                ["assumption"] = string.Join('\n', understood.Assumptions),
            });
    }

    private static ToolResponse Judge(AgentRequest request, EvidenceInterpretation interpreted, ToolCall call)
    {
        var understood = interpreted.Understanding;
        if (understood.State != UnderstandingState.ReadyToPlan)
            return Stop(understood);
        if (!TryRow(call, out var row))
            return ToolResponse.Stop(ToolStepStatus.Failed, "The evidence quality was not collected.");

        var missing = new List<string>();
        if (row.EvidenceRows == 0)
            missing.Add("evidence rows");
        else if (interpreted.FeasibleNeeds.Contains(EvidenceValidation.Payload) && row.PayloadStatementRows < 1)
            missing.Add("evidence rows with a payload statement");
        if (interpreted.FeasibleNeeds.Contains(EvidenceValidation.Relation) && row.Decisions < 1)
            missing.Add("recorded decisions");
        if (row.EvidenceRows > 0 && row.DecisionsWithoutContext > 0 && interpreted.FeasibleNeeds.Contains(EvidenceValidation.Relation))
            missing.Add("decisions with no context link while evidence exists");
        if (row.DecisionsWithContextAndNoEvidence > 0 && interpreted.FeasibleNeeds.Contains(EvidenceValidation.Relation))
            missing.Add("evidence on a decision context");
        if (row.EvidenceRows > 0 && interpreted.FeasibleNeeds.Contains(EvidenceValidation.Observation) && row.ObservationStatementRows < 1)
            missing.Add("evidence rows whose context summary is the stored-trade observation statement");
        foreach (var unverifiable in interpreted.UnverifiableNeeds)
            missing.Add($"{unverifiable} is not a registered capability.");

        var lines = new Dictionary<string, string>
        {
            ["verified"] = "true",
            ["scope"] = understood.Scope,
            ["fact"] = Facts(request.TenantId, row),
            ["inference"] = "A shared context id is not evidence that the row supports the decision.\nThe stored-trade observation statement is context text, not an evidence payload.",
            ["assumption"] = "An empty JSON payload has no assessable statement.\n" + string.Join('\n', understood.Assumptions),
            ["limit"] = "pi_evidence has no decision_id. Thesis text and source_ref are not read.\nThe stored-trade observation statement is not a support verdict.\nOther tenants are not read.\n" + string.Join('\n', understood.Risks),
        };
        if (interpreted.UnverifiableNeeds.Contains(EvidenceValidation.Support))
        {
            lines["goalMet"] = "false";
            lines["understanding"] = UnableToAssess;
            lines["goalReason"] = "Support cannot be assessed from existence, co-location, or the stored observation statement.";
            lines["missing"] = string.Join('\n', missing);
            return ToolResponse.Succeeded("The support conclusion was not assessed.", lines);
        }

        if (missing.Count > 0)
        {
            lines["goalMet"] = "false";
            lines["understanding"] = GoalTaskRuntime.ExecutedUnproven;
            lines["goalReason"] = "The requested evidence criterion was assessed and does not hold.";
            lines["missing"] = string.Join('\n', missing);
            return ToolResponse.Succeeded("The evidence criterion does not hold.", lines);
        }

        lines["goalMet"] = "true";
        lines["understanding"] = GoalTaskRuntime.Proven;
        lines["goalReason"] = "Every requested evidence criterion holds for the rows that were read.";
        return ToolResponse.Succeeded("The requested evidence criteria hold.", lines);
    }

    private static bool TryRow(ToolCall call, out EvidenceQuality row)
    {
        row = new EvidenceQuality(0, 0, 0, 0, 0, 0, 0, 0);
        if (!call.Inputs.TryGetValue("decisions", out var decisionsText)
            || !call.Inputs.TryGetValue("unlinked", out var unlinkedText)
            || !call.Inputs.TryGetValue("colocated", out var colocatedText)
            || !call.Inputs.TryGetValue("contextGap", out var gapText)
            || !call.Inputs.TryGetValue("evidenceRows", out var evidenceText)
            || !call.Inputs.TryGetValue("emptyPayload", out var emptyText)
            || !call.Inputs.TryGetValue("observation", out var observationText)
            || !call.Inputs.TryGetValue("statements", out var statementText)
            || !int.TryParse(decisionsText, out var decisions)
            || !int.TryParse(unlinkedText, out var unlinked)
            || !int.TryParse(colocatedText, out var colocated)
            || !int.TryParse(gapText, out var gap)
            || !int.TryParse(evidenceText, out var evidence)
            || !int.TryParse(emptyText, out var empty)
            || !int.TryParse(observationText, out var observation)
            || !int.TryParse(statementText, out var statements))
            return false;
        row = new EvidenceQuality(decisions, unlinked, colocated, gap, evidence, empty, observation, statements);
        return Usable(row);
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

    private static string Facts(Guid tenantId, EvidenceQuality row)
        => string.Join(
            '\n',
            $"Tenant {tenantId:D} has {row.EvidenceRows} evidence rows and {row.Decisions} decisions.",
            $"Tenant {tenantId:D} has {row.DecisionsWithoutContext} decisions with no context.",
            $"Tenant {tenantId:D} has {row.DecisionsWithContextEvidence} decisions that share a context with evidence.",
            $"Tenant {tenantId:D} has {row.DecisionsWithContextAndNoEvidence} decisions that have a context and no evidence.",
            $"Tenant {tenantId:D} has {row.EmptyPayloadRows} evidence rows with an empty payload.",
            $"Tenant {tenantId:D} has {row.ObservationStatementRows} evidence rows whose context summary is the stored-trade observation statement.",
            $"Tenant {tenantId:D} has {row.PayloadStatementRows} evidence rows whose payload contains a statement.");
}

public sealed record EvidenceAssessmentResult(AgentTaskReceipt Task, int ReadCalls);

public sealed class RefusingEvidenceAssessmentLedger : IEvidenceAssessmentLedger
{
    public EvidenceQuality Read(Guid tenantId)
        => throw new InvalidOperationException("This request does not have a plan that reads evidence quality.");
}
