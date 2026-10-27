using KitPlatform.Packs.PersonalIntelligence;

namespace KitPlatform.Packs.PersonalIntelligence.Agent;

public sealed record MultiCriterionInterpretation(
    UnderstoodRequest Understanding,
    IReadOnlyList<string> FeasibleNeeds);

/// <summary>
/// Plans one request across two registered read capabilities. It does not interpret open language.
/// </summary>
public static class MultiCriterionPlanning
{
    public const string Intent = "plan-criteria";
    public const string Subject = "tenant-evidence";
    public const string Comparison = "observation-over-fact";
    public const string Coverage = "coverage-complete";
    public const string ClassifyTool = "multi.classify";
    public const string JudgeTool = "multi.judge";

    private static readonly HashSet<string> Feasible = new(StringComparer.Ordinal) { Comparison, Coverage };

    public static string Canonical(BusinessRequest? request)
    {
        if (request is null)
            return "";
        var needs = request.Needs.Select(item => item.Trim()).Where(item => item.Length > 0).OrderBy(item => item, StringComparer.Ordinal);
        return string.Join("|", request.Intent.Trim(), request.Subject.Trim(), string.Join(",", needs));
    }

    public static MultiCriterionInterpretation? Interpret(string objective, string doneWhen, BusinessRequest? request)
    {
        var supplied = request is null ? null : Normalize(request);
        if (supplied is not null && !string.Equals(supplied.Intent, Intent, StringComparison.Ordinal))
            return null;
        var selected = supplied ?? FromLexicon(objective, doneWhen);
        if (selected is null)
            return null;
        if (!string.Equals(selected.Subject, Subject, StringComparison.Ordinal))
        {
            var subject = selected.Subject.Length == 0 ? "the subject was not stated" : $"subject {selected.Subject} is not tenant-evidence";
            return new MultiCriterionInterpretation(
                Closed(UnderstandingState.MissingData, "The request does not name both criteria.", [subject]),
                []);
        }

        if (selected.Needs.Count == 0)
        {
            return new MultiCriterionInterpretation(
                Closed(UnderstandingState.MissingData, "The request does not name both criteria.", Gaps(objective, doneWhen)),
                []);
        }

        var feasible = new List<string>();
        var unknown = new List<string>();
        foreach (var need in selected.Needs.Distinct(StringComparer.Ordinal))
        {
            if (Feasible.Contains(need))
                feasible.Add(need);
            else
                unknown.Add(need);
        }

        if (unknown.Count > 0)
        {
            return new MultiCriterionInterpretation(
                Closed(UnderstandingState.MissingCapability, "A required criterion has no registered capability, so the plan is not feasible.", unknown.Select(item => $"No registered capability answers {item}.").ToArray()),
                []);
        }

        return new MultiCriterionInterpretation(Ready(objective, feasible), feasible);
    }

    private static BusinessRequest? FromLexicon(string objective, string doneWhen)
    {
        var text = $"{objective}\n{doneWhen}";
        var marker = Contains(text, "both") || Contains(text, "đồng thời");
        if (!marker)
            return null;
        var observation = Contains(text, "observation") || Contains(text, "quan sát");
        var fact = Contains(text, "fact") || Contains(text, "sự kiện");
        var evidence = Contains(text, "evidence") || Contains(text, "bằng chứng");
        var decision = Contains(text, "decision") || Contains(text, "quyết định");
        var complete = Contains(text, "complete") || Contains(text, "sufficient") || Contains(text, "đầy đủ");
        if (observation && fact && evidence && decision && complete)
        {
            return new BusinessRequest
            {
                Intent = Intent,
                Subject = Subject,
                Needs = [Comparison, Coverage],
            };
        }

        return new BusinessRequest
        {
            Intent = Intent,
            Subject = Subject,
            Needs = [],
        };
    }

    private static IReadOnlyList<string> Gaps(string objective, string doneWhen)
    {
        var text = $"{objective}\n{doneWhen}";
        var missing = new List<string>();
        if (!(Contains(text, "observation") || Contains(text, "quan sát")) || !(Contains(text, "fact") || Contains(text, "sự kiện")))
            missing.Add("an observation and fact comparison");
        if (!(Contains(text, "complete") || Contains(text, "sufficient") || Contains(text, "đầy đủ"))
            || !(Contains(text, "evidence") || Contains(text, "bằng chứng"))
            || !(Contains(text, "decision") || Contains(text, "quyết định")))
            missing.Add("a coverage criterion for the tenant's decisions");
        return missing.Count == 0 ? ["completion criterion"] : missing;
    }

    private static BusinessRequest Normalize(BusinessRequest request)
        => new()
        {
            Intent = request.Intent.Trim(),
            Subject = request.Subject.Trim(),
            Needs = (request.Needs ?? []).Select(item => (item ?? "").Trim()).Where(item => item.Length > 0).Distinct(StringComparer.Ordinal).ToList(),
        };

    private static UnderstoodRequest Ready(string objective, IReadOnlyList<string> feasible)
    {
        var needs = string.Join(", ", feasible);
        var plan = new List<AgentStepSpec>
        {
            new(
                "discover",
                ClassifyTool,
                null,
                $"Classify the request before any read. Input: {Intent} for {Subject}. Capability: {ClassifyTool}. Expected evidence: required criteria {needs}. A shared context is not a support verdict."),
        };
        string? countStep = null;
        if (feasible.Contains(Comparison))
        {
            plan.Add(new AgentStepSpec(
                "collect-OBSERVATION",
                EvidenceReviewRuntime.CollectTool,
                "discover",
                "Read the OBSERVATION count after discover succeeds. Input: caller tenant and OBSERVATION. Capability: evidence.count. Expected evidence: the observation row count. This step does not read coverage."));
            plan.Add(new AgentStepSpec(
                "collect-FACT",
                EvidenceReviewRuntime.CollectTool,
                "collect-OBSERVATION",
                "Read the FACT count after the observation count is available. Input: caller tenant and FACT. Capability: evidence.count. Expected evidence: both kind counts for the comparison."));
            countStep = "collect-FACT";
        }

        string? coverageStep = null;
        if (feasible.Contains(Coverage))
        {
            plan.Add(new AgentStepSpec(
                "coverage",
                BusinessUnderstanding.CollectTool,
                "discover",
                "Read decision-support coverage after discover succeeds. Input: caller tenant. Capability: decision.support-coverage. Expected evidence: decision count, shared-context count, evidence row count, and non-empty payload count. This step does not use the kind counts."));
            coverageStep = "coverage";
        }

        var depends = string.Join(",", new[] { countStep, coverageStep }.Where(item => item is not null));
        plan.Add(new AgentStepSpec(
            "assess",
            JudgeTool,
            depends,
            $"Judge the required criteria only after {depends} succeed. Input: evidence.count and decision.support-coverage. Capability: {JudgeTool}. Expected evidence: proven or unmet for each required criterion. The overall goal requires every required criterion."));
        return new UnderstoodRequest(
            UnderstandingState.ReadyToPlan,
            OneLine(objective, "judge the required evidence criteria together"),
            "multi-criteria; pack_personal_intelligence.pi_evidence; pack_personal_intelligence.pi_decision; caller tenant only",
            "The request names criteria from evidence.count and decision.support-coverage.",
            feasible.Select(item => item == Comparison ? "OBSERVATION and FACT counts from pi_evidence" : "decision and evidence coverage from pi_decision and pi_evidence").ToArray(),
            ["pack_personal_intelligence.pi_evidence", "pack_personal_intelligence.pi_decision"],
            ["The sentence was classified by a closed lexicon or an explicit business request. It was not parsed as open language."],
            [],
            feasible.Select(item => item == Comparison ? "evidence.count" : "decision.support-coverage").ToArray(),
            [],
            [
                "The server owns the plan. A client step list is not executed.",
                "This plan does not judge whether evidence supports a decision.",
                "A read does not require approval and cannot become a live effect.",
            ],
            ["personal_intelligence.evidence.read"],
            false,
            plan);
    }

    private static UnderstoodRequest Closed(UnderstandingState state, string reason, IReadOnlyList<string> missing)
        => new(
            state,
            "multi-criteria",
            "unspecified",
            reason,
            [],
            [],
            ["The sentence was classified by a closed lexicon or an explicit business request. It was not parsed as open language."],
            missing,
            [],
            state == UnderstandingState.MissingCapability ? missing : [],
            ["No evidence row or decision row is read for this request."],
            [],
            false,
            [new AgentStepSpec("assess", JudgeTool, null, $"Record the {RequestUnderstanding.Code(state)} result without reading stored rows.")]);

    private static bool Contains(string text, string term)
        => text.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static string OneLine(string text, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(text) ? fallback : text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return value.Length <= 180 ? value : value[..180];
    }
}
