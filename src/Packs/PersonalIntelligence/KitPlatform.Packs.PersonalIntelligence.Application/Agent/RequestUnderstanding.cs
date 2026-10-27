namespace KitPlatform.Packs.PersonalIntelligence.Agent;

/// <summary>
/// A structured completion condition. The doneWhen sentence is not parsed.
/// </summary>
public sealed class CompletionAsk
{
    public string Id { get; set; } = "";
    public string Capability { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Relation { get; set; } = "";
    public string Target { get; set; } = "";
}

/// <summary>
/// One real capability a domain has registered. The caller cannot add a tool here.
/// </summary>
public sealed record CapabilityDefinition(
    string Id,
    string ToolName,
    string Permission,
    ToolEffect Effect,
    bool RequiresApproval,
    string DataSource,
    string Scope,
    IReadOnlySet<string> Subjects,
    IReadOnlySet<string> Relations);

public enum UnderstandingState
{
    NotUnderstood,
    MissingData,
    MissingCapability,
    ReadyToPlan,
}

/// <summary>
/// What the server can say about a request before it reads or changes anything.
/// </summary>
public sealed record UnderstoodRequest(
    UnderstandingState State,
    string Goal,
    string Scope,
    string Reason,
    IReadOnlyList<string> RequiredData,
    IReadOnlyList<string> AllowedSources,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> Missing,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Gaps,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> Permissions,
    bool ApprovalRequired,
    IReadOnlyList<AgentStepSpec> Plan);

public static class RequestUnderstanding
{
    public static string Code(UnderstandingState state) => state switch
    {
        UnderstandingState.MissingData => "missing-data",
        UnderstandingState.MissingCapability => "missing-capability",
        UnderstandingState.ReadyToPlan => "ready-to-plan",
        _ => "not-understood",
    };

    public static string Canonical(IReadOnlyList<CompletionAsk>? asks)
    {
        if (asks is null || asks.Count == 0)
            return "";
        return string.Join(
            "\n",
            asks.Select(ask => string.Join("|", Text(ask.Id), Text(ask.Capability), Text(ask.Subject), Text(ask.Relation), Text(ask.Target)))
                .OrderBy(line => line, StringComparer.Ordinal));
    }

    public static UnderstoodRequest Analyze(
        string objective,
        string doneWhen,
        IReadOnlyList<CompletionAsk>? asks,
        IReadOnlyList<CapabilityDefinition> catalog,
        string assessTool)
    {
        var goal = OneLine(objective, "the objective");
        var sentence = OneLine(doneWhen, "the completion text");
        var assumptions = new List<string> { "The doneWhen sentence is stored and is not parsed as a condition." };
        var risks = new List<string>
        {
            "The server owns the plan. A capability that is not in the catalog is not turned into a tool.",
            "A read does not require approval and cannot become a live effect.",
        };
        if (asks is null || asks.Count == 0)
        {
            return Closed(
                UnderstandingState.NotUnderstood,
                goal,
                "The request has no structured completion condition. Free-form text is not interpreted.",
                assumptions,
                [sentence],
                [],
                [],
                risks,
                [],
                assessTool,
                $"Record that this request is not understood: {sentence}");
        }

        var missing = new List<string>();
        var gaps = new List<string>();
        var capabilities = new List<string>();
        var required = new List<string>();
        var sources = new List<string>();
        var permissions = new List<string>();
        var definitions = new List<CapabilityDefinition>();
        var kinds = new SortedSet<string>(StringComparer.Ordinal);
        var approval = false;
        var state = UnderstandingState.ReadyToPlan;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ask in asks)
        {
            var id = Text(ask.Id);
            var capability = Text(ask.Capability);
            var subject = Text(ask.Subject);
            var relation = Text(ask.Relation);
            var target = Text(ask.Target);
            if (!ValidId(id) || !seen.Add(id))
            {
                state = Worse(state, UnderstandingState.NotUnderstood);
                missing.Add($"condition {id} is not a usable id");
                continue;
            }

            var definition = catalog.FirstOrDefault(item => string.Equals(item.Id, capability, StringComparison.Ordinal));
            if (definition is null)
            {
                state = Worse(state, UnderstandingState.MissingCapability);
                gaps.Add(capability.Length == 0 ? id : capability);
                missing.Add($"No registered tool answers {capability}.");
                continue;
            }

            if (!definition.Subjects.Contains(subject))
            {
                state = Worse(state, UnderstandingState.MissingData);
                missing.Add($"subject {subject} is not a stored kind {definition.Id} reads");
                continue;
            }

            if (!definition.Relations.Contains(relation))
            {
                state = Worse(state, UnderstandingState.NotUnderstood);
                missing.Add($"relation {relation} is not a comparison {definition.Id} can judge");
                continue;
            }

            if (relation == "atLeast")
            {
                if (!int.TryParse(target, out var threshold) || threshold < 1 || target.Length > 7 || target != threshold.ToString())
                {
                    state = Worse(state, UnderstandingState.MissingData);
                    missing.Add($"target {target} is not a positive count");
                    continue;
                }
            }
            else if (!definition.Subjects.Contains(target) || string.Equals(subject, target, StringComparison.Ordinal))
            {
                state = Worse(state, UnderstandingState.MissingData);
                missing.Add($"target {target} is not a different stored kind");
                continue;
            }

            capabilities.Add(definition.Id);
            definitions.Add(definition);
            kinds.Add(subject);
            if (relation == "greaterThan")
                kinds.Add(target);
            required.Add($"{definition.Id} {subject} {relation} {target} from {definition.DataSource}");
            Add(sources, definition.DataSource);
            Add(permissions, definition.Permission);
            approval |= definition.RequiresApproval;
        }

        if (state != UnderstandingState.ReadyToPlan)
        {
            var reason = state switch
            {
                UnderstandingState.MissingCapability => "The request names a capability that is not registered.",
                UnderstandingState.MissingData => "The request is understood only far enough to see that the subject or target is not available.",
                _ => "The request does not identify a completion condition this catalog can judge.",
            };
            return Closed(state, goal, reason, assumptions, missing, capabilities, gaps, risks, permissions, assessTool, $"Record the {Code(state)} gap for: {goal}");
        }

        var scope = string.Join("; ", definitions.Select(item => item.Scope).Distinct(StringComparer.Ordinal));
        var plan = new List<AgentStepSpec>();
        string? previous = null;
        foreach (var kind in kinds)
        {
            var stepId = "collect-" + kind;
            var purpose = previous is null
                ? $"Read {kind} rows for: {goal}. Requires {permissions[0]}. Source {sources[0]}."
                : $"Read {kind} rows for: {goal} after {previous} succeeds. Requires {permissions[0]}. Source {sources[0]}.";
            plan.Add(new AgentStepSpec(stepId, definitions[0].ToolName, previous, purpose));
            previous = stepId;
        }

        plan.Add(new AgentStepSpec("assess", assessTool, previous, $"Judge the collected counts for: {goal}. Requires {permissions[0]}."));
        return new UnderstoodRequest(
            UnderstandingState.ReadyToPlan,
            goal,
            scope,
            "The structured conditions match registered read capabilities.",
            required,
            sources,
            assumptions,
            [],
            capabilities.Distinct(StringComparer.Ordinal).ToArray(),
            [],
            risks,
            permissions,
            approval,
            plan);
    }

    private static UnderstoodRequest Closed(
        UnderstandingState state,
        string goal,
        string reason,
        IReadOnlyList<string> assumptions,
        IReadOnlyList<string> missing,
        IReadOnlyList<string> capabilities,
        IReadOnlyList<string> gaps,
        IReadOnlyList<string> risks,
        IReadOnlyList<string> permissions,
        string assessTool,
        string purpose)
        => new(
            state,
            goal,
            "unspecified",
            reason,
            [],
            [],
            assumptions,
            missing,
            capabilities,
            gaps,
            risks,
            permissions,
            false,
            [new AgentStepSpec("assess", assessTool, null, purpose)]);

    private static UnderstandingState Worse(UnderstandingState current, UnderstandingState next)
    {
        if (next == UnderstandingState.NotUnderstood || current == UnderstandingState.NotUnderstood)
            return UnderstandingState.NotUnderstood;
        if (next == UnderstandingState.MissingCapability || current == UnderstandingState.MissingCapability)
            return UnderstandingState.MissingCapability;
        if (next == UnderstandingState.MissingData || current == UnderstandingState.MissingData)
            return UnderstandingState.MissingData;
        return UnderstandingState.ReadyToPlan;
    }

    private static bool ValidId(string id)
    {
        if (id.Length is < 1 or > 40 || id[0] == '-')
            return false;
        foreach (var character in id)
        {
            var ok = character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-';
            if (!ok)
                return false;
        }

        return true;
    }

    private static void Add(List<string> target, string value)
    {
        if (!target.Contains(value, StringComparer.Ordinal))
            target.Add(value);
    }

    private static string Text(string? value) => value?.Trim() ?? "";

    private static string OneLine(string text, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(text) ? fallback : text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return value.Length <= 180 ? value : value[..180];
    }
}
