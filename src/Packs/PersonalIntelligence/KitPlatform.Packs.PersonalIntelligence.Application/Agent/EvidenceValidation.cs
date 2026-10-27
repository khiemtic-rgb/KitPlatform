namespace KitPlatform.Packs.PersonalIntelligence.Agent;

public sealed record EvidenceInterpretation(
    UnderstoodRequest Understanding,
    IReadOnlyList<string> FeasibleNeeds,
    IReadOnlyList<string> UnverifiableNeeds);

/// <summary>
/// Classifies one evidence-validation intent. A shared context or an empty payload is not a support verdict.
/// </summary>
public static class EvidenceValidation
{
    public const string Intent = "validate-evidence";
    public const string Subject = "decision-support";
    public const string Relation = "context-relation";
    public const string Payload = "payload-statement";
    public const string Observation = "observation-statement";
    public const string Support = "supports-decision";
    public const string ClassifyTool = "evidence.classify";
    public const string CollectTool = "evidence.decision-quality";
    public const string JudgeTool = "evidence.judge";

    private static readonly HashSet<string> Feasible = new(StringComparer.Ordinal) { Relation, Payload, Observation };

    public static string Canonical(BusinessRequest? request)
    {
        if (request is null)
            return "";
        var needs = request.Needs.Select(item => item.Trim()).Where(item => item.Length > 0).OrderBy(item => item, StringComparer.Ordinal);
        return string.Join("|", request.Intent.Trim(), request.Subject.Trim(), string.Join(",", needs));
    }

    public static EvidenceInterpretation? Interpret(string objective, string doneWhen, BusinessRequest? request)
    {
        var supplied = request is null ? null : Normalize(request);
        if (supplied is not null && !string.Equals(supplied.Intent, Intent, StringComparison.Ordinal))
            return null;
        var selected = supplied ?? FromLexicon(objective, doneWhen);
        if (selected is null)
            return null;
        if (!string.Equals(selected.Subject, Subject, StringComparison.Ordinal))
        {
            var subject = selected.Subject.Length == 0 ? "the subject was not stated" : $"subject {selected.Subject} is not decision-support";
            return new EvidenceInterpretation(
                Closed(UnderstandingState.MissingData, "The request does not name the decision being assessed.", [subject]),
                [],
                []);
        }

        if (selected.Needs.Count == 0)
        {
            return new EvidenceInterpretation(
                Closed(UnderstandingState.MissingData, "The request names no evidence criterion.", ["completion criterion"]),
                [],
                []);
        }

        var feasible = new List<string>();
        var unverifiable = new List<string>();
        var unknown = new List<string>();
        foreach (var need in selected.Needs.Distinct(StringComparer.Ordinal))
        {
            if (Feasible.Contains(need))
                feasible.Add(need);
            else if (string.Equals(need, Support, StringComparison.Ordinal))
                unverifiable.Add(need);
            else
                unknown.Add(need);
        }

        if (unknown.Count > 0)
        {
            return new EvidenceInterpretation(
                Closed(UnderstandingState.MissingCapability, "The request names a criterion that is not in the evidence-validation catalog.", unknown.Select(item => $"No registered capability answers {item}.").ToArray()),
                [],
                []);
        }

        if (feasible.Count == 0)
        {
            return new EvidenceInterpretation(
                Closed(UnderstandingState.MissingCapability, "The requested criterion has no registered read.", unverifiable.Select(item => $"{item} is not a registered capability.").ToArray()),
                [],
                unverifiable);
        }

        return new EvidenceInterpretation(Ready(objective, feasible, unverifiable), feasible, unverifiable);
    }

    private static BusinessRequest? FromLexicon(string objective, string doneWhen)
    {
        var text = $"{objective}\n{doneWhen}";
        var marker = Contains(text, "validate") || Contains(text, "đánh giá") || Contains(text, "căn cứ");
        if (!marker)
            return null;
        var evidence = Contains(text, "evidence") || Contains(text, "bằng chứng");
        var decision = Contains(text, "decision") || Contains(text, "quyết định");
        if (evidence && decision)
        {
            return new BusinessRequest
            {
                Intent = Intent,
                Subject = Subject,
                Needs = [Relation, Payload, Observation, Support],
            };
        }

        return new BusinessRequest
        {
            Intent = Intent,
            Subject = "",
            Needs = [],
        };
    }

    private static BusinessRequest Normalize(BusinessRequest request)
        => new()
        {
            Intent = request.Intent.Trim(),
            Subject = request.Subject.Trim(),
            Needs = (request.Needs ?? []).Select(item => (item ?? "").Trim()).Where(item => item.Length > 0).Distinct(StringComparer.Ordinal).ToList(),
        };

    private static UnderstoodRequest Ready(string objective, IReadOnlyList<string> feasible, IReadOnlyList<string> unverifiable)
    {
        var needs = string.Join(", ", feasible);
        var plan = new List<AgentStepSpec>
        {
            new(
                "discover",
                ClassifyTool,
                null,
                $"Classify the request before any read. Input: {Intent} for {Subject}. Capability: {ClassifyTool}. Expected evidence: registered needs {needs}. A shared context is not a support verdict, and thesis text is not read."),
            new(
                "collect",
                CollectTool,
                "discover",
                $"Read evidence quality after discover succeeds. Input: caller tenant. Capability: {CollectTool}. Expected evidence: decision context counts, empty payload count, observation-statement count, and payload-statement count."),
            new(
                "assess",
                JudgeTool,
                "collect",
                $"Judge {needs} from the collected rows. Input: evidence.decision-quality. Capability: {JudgeTool}. Expected evidence: met, unmet, or unable to assess for each extracted criterion."),
        };
        var gaps = unverifiable.Select(item => $"{item} is not a registered capability.").ToList();
        return new UnderstoodRequest(
            UnderstandingState.ReadyToPlan,
            OneLine(objective, "validate evidence for a decision"),
            "evidence-validation; pack_personal_intelligence.pi_decision; pack_personal_intelligence.pi_evidence; pack_personal_intelligence.pi_context; caller tenant only",
            "The request matches the evidence-validation intent.",
            feasible.Select(item => $"{item} from pi_decision, pi_evidence, and pi_context").ToArray(),
            ["pack_personal_intelligence.pi_decision", "pack_personal_intelligence.pi_evidence", "pack_personal_intelligence.pi_context"],
            ["The sentence was classified by a closed lexicon or an explicit business request. It was not parsed as open language."],
            gaps,
            [CollectTool],
            gaps,
            [
                "The server owns the plan. Thesis text and source_ref are not read.",
                "A shared context id is not a decision link.",
                "A read does not require approval and cannot become a live effect.",
            ],
            ["personal_intelligence.evidence.read"],
            false,
            plan);
    }

    private static UnderstoodRequest Closed(UnderstandingState state, string reason, IReadOnlyList<string> missing)
        => new(
            state,
            "evidence-validation",
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
