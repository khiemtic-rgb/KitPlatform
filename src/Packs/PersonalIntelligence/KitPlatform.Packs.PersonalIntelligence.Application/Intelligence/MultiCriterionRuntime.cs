using KitPlatform.Packs.PersonalIntelligence.Agent;

namespace KitPlatform.Packs.PersonalIntelligence;

/// <summary>
/// Executes one plan that calls evidence.count and decision.support-coverage, then judges every required criterion.
/// </summary>
public sealed class MultiCriterionRuntime
{
    public MultiCriterionResult Execute(
        AgentRequest request,
        IEvidenceLedger evidence,
        IDecisionSupportLedger coverage,
        IAgentRunLog log)
    {
        var evidenceReads = 0;
        var coverageReads = 0;
        var interpreted = MultiCriterionPlanning.Interpret(request.Objective, request.DoneWhen, request.Business)
            ?? throw new InvalidOperationException("This request is not a multi-criteria plan.");
        var understood = interpreted.Understanding;
        var registry = new ToolRegistry();
        registry.Register(
            new ToolContract(MultiCriterionPlanning.ClassifyTool, EvidenceReviewRuntime.ReadPermission, ToolEffect.Read, false),
            _ => Discover(understood));
        registry.Register(
            new ToolContract(EvidenceReviewRuntime.CollectTool, EvidenceReviewRuntime.ReadPermission, ToolEffect.Read, false),
            call =>
            {
                if (understood.State != UnderstandingState.ReadyToPlan || !understood.Plan.Any(step => step.StepId == call.StepId))
                    return Stop(understood);
                var kind = KindOf(call.StepId);
                if (kind is null)
                    return ToolResponse.Stop(ToolStepStatus.Failed, "The evidence count step is not registered.");
                evidenceReads++;
                EvidenceObservation row;
                try
                {
                    row = evidence.Read(request.TenantId, kind);
                }
                catch (Exception ex)
                {
                    return FailedRead(understood, $"{kind} count", ex.Message);
                }

                if (!string.Equals(row.EpistemicKind, kind, StringComparison.Ordinal) || row.Count < 0)
                    return FailedRead(understood, $"{kind} count", "The evidence count was not usable.");
                var outputs = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["verified"] = "true",
                    ["scope"] = understood.Scope,
                    ["count." + kind] = row.Count.ToString(),
                    ["fact"] = $"Tenant {request.TenantId:D} has {row.Count} {kind} rows in pack_personal_intelligence.pi_evidence.",
                };
                foreach (var pair in call.Inputs)
                {
                    if (pair.Key.StartsWith("count.", StringComparison.Ordinal))
                        outputs[pair.Key] = pair.Value;
                }

                outputs["count." + kind] = row.Count.ToString();
                return ToolResponse.Succeeded($"Read {row.Count} {kind} rows for this tenant.", outputs);
            });
        registry.Register(
            new ToolContract(BusinessUnderstanding.CollectTool, EvidenceReviewRuntime.ReadPermission, ToolEffect.Read, false),
            call =>
            {
                if (understood.State != UnderstandingState.ReadyToPlan || !understood.Plan.Any(step => step.StepId == call.StepId))
                    return Stop(understood);
                coverageReads++;
                DecisionSupportCoverage row;
                try
                {
                    row = coverage.Read(request.TenantId);
                }
                catch (Exception ex)
                {
                    return FailedRead(understood, "decision-support coverage", ex.Message);
                }

                if (row.Decisions < 0 || row.DecisionsWithEvidence < 0 || row.EvidenceRows < 0 || row.EvidenceWithContent < 0 || row.DecisionsWithEvidence > row.Decisions)
                    return FailedRead(understood, "decision-support coverage", "The decision-support coverage was not usable.");
                return ToolResponse.Succeeded(
                    "Decision and evidence coverage was read for this tenant.",
                    new Dictionary<string, string>
                    {
                        ["verified"] = "true",
                        ["scope"] = understood.Scope,
                        ["decisions"] = row.Decisions.ToString(),
                        ["linked"] = row.DecisionsWithEvidence.ToString(),
                        ["evidenceRows"] = row.EvidenceRows.ToString(),
                        ["content"] = row.EvidenceWithContent.ToString(),
                        ["fact"] = $"Tenant {request.TenantId:D} has {row.Decisions} decisions in pack_personal_intelligence.pi_decision.\nTenant {request.TenantId:D} has {row.DecisionsWithEvidence} decisions with evidence on the same context.\nTenant {request.TenantId:D} has {row.EvidenceRows} evidence rows and {row.EvidenceWithContent} with a non-empty payload.",
                    });
            });
        registry.Register(
            new ToolContract(MultiCriterionPlanning.JudgeTool, EvidenceReviewRuntime.ReadPermission, ToolEffect.Read, false),
            call => Judge(request, interpreted, call));
        var receipt = new AgentRuntime(registry).Execute(request with { Plan = understood.Plan }, log);
        return new MultiCriterionResult(receipt, evidenceReads, coverageReads);
    }

    private static string? KindOf(string stepId)
        => stepId switch
        {
            "collect-OBSERVATION" => "OBSERVATION",
            "collect-FACT" => "FACT",
            _ => null,
        };

    private static ToolResponse Discover(UnderstoodRequest understood)
    {
        if (understood.State != UnderstandingState.ReadyToPlan)
            return Stop(understood);
        return ToolResponse.Succeeded(
            "The required capabilities were selected before any read.",
            new Dictionary<string, string>
            {
                ["verified"] = "true",
                ["scope"] = understood.Scope,
                ["understanding"] = RequestUnderstanding.Code(UnderstandingState.ReadyToPlan),
                ["limit"] = "A kind count and a coverage count do not show that evidence supports a decision.",
                ["assumption"] = string.Join('\n', understood.Assumptions),
            });
    }

    private static ToolResponse Judge(AgentRequest request, MultiCriterionInterpretation interpreted, ToolCall call)
    {
        var understood = interpreted.Understanding;
        if (understood.State != UnderstandingState.ReadyToPlan)
            return Stop(understood);
        var facts = new List<string>();
        var missing = new List<string>();
        if (interpreted.FeasibleNeeds.Contains(MultiCriterionPlanning.Comparison))
        {
            if (!call.Inputs.TryGetValue("count.OBSERVATION", out var observationText)
                || !call.Inputs.TryGetValue("count.FACT", out var factText)
                || !int.TryParse(observationText, out var observation)
                || !int.TryParse(factText, out var fact))
                return ToolResponse.Stop(ToolStepStatus.Failed, "The evidence counts were not collected.");
            facts.Add($"Tenant {request.TenantId:D} has {observation} OBSERVATION rows in pack_personal_intelligence.pi_evidence.");
            facts.Add($"Tenant {request.TenantId:D} has {fact} FACT rows in pack_personal_intelligence.pi_evidence.");
            var holds = observation > fact;
            facts.Add(holds
                ? "Criterion observation-over-fact is proven."
                : "Criterion observation-over-fact is executed-unproven.");
            if (!holds)
                missing.Add("OBSERVATION greater than FACT");
        }

        if (interpreted.FeasibleNeeds.Contains(MultiCriterionPlanning.Coverage))
        {
            if (!call.Inputs.TryGetValue("decisions", out var decisionsText)
                || !call.Inputs.TryGetValue("linked", out var linkedText)
                || !call.Inputs.TryGetValue("evidenceRows", out var evidenceText)
                || !call.Inputs.TryGetValue("content", out var contentText)
                || !int.TryParse(decisionsText, out var decisions)
                || !int.TryParse(linkedText, out var linked)
                || !int.TryParse(evidenceText, out var evidenceRows)
                || !int.TryParse(contentText, out var content))
                return ToolResponse.Stop(ToolStepStatus.Failed, "The decision-support coverage was not collected.");
            facts.Add($"Tenant {request.TenantId:D} has {decisions} decisions in pack_personal_intelligence.pi_decision.");
            facts.Add($"Tenant {request.TenantId:D} has {linked} decisions with evidence on the same context.");
            facts.Add($"Tenant {request.TenantId:D} has {evidenceRows} evidence rows and {content} with a non-empty payload.");
            var gaps = new List<string>();
            if (decisions < 1)
                gaps.Add("recorded decisions");
            if (decisions < 1 || linked != decisions)
                gaps.Add("decisions without evidence on the same context");
            if (content < 1)
                gaps.Add("evidence rows with a non-empty payload");
            facts.Add(gaps.Count == 0
                ? "Criterion coverage-complete is proven."
                : "Criterion coverage-complete is executed-unproven.");
            missing.AddRange(gaps);
        }

        var lines = new Dictionary<string, string>
        {
            ["verified"] = "true",
            ["scope"] = understood.Scope,
            ["fact"] = string.Join('\n', facts),
            ["inference"] = "An observation count greater than a fact count is not coverage and is not a support verdict.",
            ["assumption"] = "An empty JSON payload is not evidence content.\n" + string.Join('\n', understood.Assumptions),
            ["limit"] = "A kind count and a coverage count do not show that evidence supports a decision.\npi_evidence has no decision_id. Thesis text and source_ref are not read.\nOther tenants are not read.\n" + string.Join('\n', understood.Risks),
        };
        if (missing.Count > 0)
        {
            lines["goalMet"] = "false";
            lines["understanding"] = GoalTaskRuntime.ExecutedUnproven;
            lines["goalReason"] = "The required capabilities were read. Not every required criterion holds.";
            lines["missing"] = string.Join('\n', missing);
            return ToolResponse.Succeeded("The required criteria are not all proven.", lines);
        }

        lines["goalMet"] = "true";
        lines["understanding"] = GoalTaskRuntime.Proven;
        lines["goalReason"] = "Every required criterion holds for the rows that were read.";
        return ToolResponse.Succeeded("Every required criterion holds.", lines);
    }

    private static ToolResponse FailedRead(UnderstoodRequest understood, string missing, string reason)
        => ToolResponse.Stop(
            ToolStepStatus.Failed,
            reason,
            new Dictionary<string, string>
            {
                ["goalMet"] = "false",
                ["understanding"] = GoalTaskRuntime.ExecutedUnproven,
                ["goalReason"] = reason,
                ["scope"] = understood.Scope,
                ["missing"] = missing,
                ["limit"] = "The overall goal was not judged because a required capability failed.\nA kind count and a coverage count do not show that evidence supports a decision.",
            });

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
}

public sealed record MultiCriterionResult(AgentTaskReceipt Task, int EvidenceReads, int CoverageReads);
