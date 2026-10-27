namespace KitPlatform.Packs.PersonalIntelligence.Agent;

/// <summary>
/// A business request. Needs are business criteria, not tool names.
/// </summary>
public sealed class BusinessRequest
{
    public string Intent { get; set; } = "";
    public string Subject { get; set; } = "";
    public List<string> Needs { get; set; } = [];
}

public sealed record BusinessInterpretation(
    UnderstoodRequest Understanding,
    IReadOnlyList<string> FeasibleNeeds,
    IReadOnlyList<string> UnverifiableNeeds);

/// <summary>
/// Classifies one closed business intent. It does not interpret open language and it does not call a model.
/// </summary>
public static class BusinessUnderstanding
{
    public const string Intent = "assess-completeness";
    public const string Subject = "decision-support";
    public const string Decisions = "decision-recorded";
    public const string Linked = "evidence-linked";
    public const string Content = "payload-content";
    public const string Thesis = "thesis-consistent";
    public const string DiscoverTool = "goal.discover";
    public const string CollectTool = "decision.support-coverage";
    public const string JudgeTool = "goal.judge";

    private static readonly HashSet<string> Feasible = new(StringComparer.Ordinal) { Decisions, Linked, Content };

    public static string Canonical(BusinessRequest? request)
    {
        if (request is null)
            return "";
        var needs = request.Needs.Select(item => item.Trim()).Where(item => item.Length > 0).OrderBy(item => item, StringComparer.Ordinal);
        return string.Join("|", request.Intent.Trim(), request.Subject.Trim(), string.Join(",", needs));
    }

    public static BusinessInterpretation Interpret(string objective, string doneWhen, BusinessRequest? request)
    {
        var supplied = request is null ? null : Normalize(request);
        var selected = supplied ?? FromLexicon(objective, doneWhen);
        if (selected is null)
            return new BusinessInterpretation(NotUnderstood(doneWhen), [], []);
        if (selected.Intent.Length == 0 || !string.Equals(selected.Intent, Intent, StringComparison.Ordinal))
            return new BusinessInterpretation(NotUnderstood(selected.Intent.Length == 0 ? doneWhen : selected.Intent), [], []);
        if (!string.Equals(selected.Subject, Subject, StringComparison.Ordinal))
        {
            var subject = selected.Subject.Length == 0 ? "the subject was not stated" : $"subject {selected.Subject} is not decision-support";
            return new BusinessInterpretation(
                Closed(UnderstandingState.MissingData, "The request does not name the decision-support subject.", [subject]),
                [],
                []);
        }

        if (selected.Needs.Count == 0)
        {
            return new BusinessInterpretation(
                Closed(UnderstandingState.MissingData, "The request names no completion criterion.", ["completion criterion"]),
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
            else if (string.Equals(need, Thesis, StringComparison.Ordinal))
                unverifiable.Add(need);
            else
                unknown.Add(need);
        }

        if (unknown.Count > 0)
        {
            return new BusinessInterpretation(
                Closed(UnderstandingState.MissingCapability, "The request names a criterion that is not in the decision-support catalog.", unknown.Select(item => $"No registered capability answers {item}.").ToArray()),
                [],
                []);
        }

        if (feasible.Count == 0)
        {
            return new BusinessInterpretation(
                Closed(UnderstandingState.MissingCapability, "The requested criterion has no registered read.", unverifiable.Select(item => $"{item} is not a registered capability.").ToArray()),
                [],
                unverifiable);
        }

        return new BusinessInterpretation(Ready(objective, feasible, unverifiable), feasible, unverifiable);
    }

    private static BusinessRequest? FromLexicon(string objective, string doneWhen)
    {
        var text = $"{objective}\n{doneWhen}";
        var evidence = Contains(text, "evidence") || Contains(text, "bằng chứng");
        var decision = Contains(text, "decision") || Contains(text, "quyết định");
        var complete = Contains(text, "complete") || Contains(text, "sufficient") || Contains(text, "đầy đủ") || Contains(text, "support");
        if (!evidence && !decision && !complete)
            return null;
        if (evidence && decision && complete)
        {
            return new BusinessRequest
            {
                Intent = Intent,
                Subject = Subject,
                Needs = [Decisions, Linked, Content],
            };
        }

        return new BusinessRequest
        {
            Intent = Intent,
            Subject = evidence && decision ? Subject : "",
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
        var goal = OneLine(objective, "assess decision-support completeness");
        var needs = string.Join(", ", feasible);
        var plan = new List<AgentStepSpec>
        {
            new(
                "discover",
                DiscoverTool,
                null,
                $"Classify the request before any read. Input: {Intent} for {Subject}. Capability: {DiscoverTool}. Expected evidence: registered needs {needs}. Thesis comparison is not registered and is not executed."),
            new(
                "collect",
                CollectTool,
                "discover",
                $"Read decision and evidence coverage after discover succeeds. Input: caller tenant. Capability: {CollectTool}. Expected evidence: decision count, linked-decision count, evidence row count, and non-empty payload count."),
            new(
                "assess",
                JudgeTool,
                "collect",
                $"Judge {needs} from the collected counts. Input: decision.support-coverage. Capability: {JudgeTool}. Expected evidence: met or unmet for each extracted criterion."),
        };
        var gaps = unverifiable.Count == 0
            ? new List<string>()
            : unverifiable.Select(item => $"{item} is not a registered capability.").ToList();
        return new UnderstoodRequest(
            UnderstandingState.ReadyToPlan,
            goal,
            "decision-support; pack_personal_intelligence.pi_decision; pack_personal_intelligence.pi_evidence; caller tenant only",
            "The request matches the decision-support completeness intent.",
            feasible.Select(item => $"{item} from pi_decision and pi_evidence").ToArray(),
            ["pack_personal_intelligence.pi_decision", "pack_personal_intelligence.pi_evidence"],
            ["The sentence was classified by a closed lexicon or an explicit business request. It was not parsed as open language."],
            gaps,
            [CollectTool],
            gaps,
            [
                "The server owns the plan. Thesis text is not read.",
                "A read does not require approval and cannot become a live effect.",
            ],
            ["personal_intelligence.evidence.read"],
            false,
            plan);
    }

    private static UnderstoodRequest NotUnderstood(string doneWhen)
        => Closed(UnderstandingState.NotUnderstood, "The request does not match a business intent this catalog can classify.", [OneLine(doneWhen, "the completion text")]);

    private static UnderstoodRequest Closed(UnderstandingState state, string reason, IReadOnlyList<string> missing)
        => new(
            state,
            "decision-support",
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
