using System.Text.RegularExpressions;

namespace KitPlatform.Packs.PersonalIntelligence.Agent;

/// <summary>
/// Turns a new sentence into structured conditions by reading the capability catalog.
/// Relation phrases are grammar. The sentence itself is not a registered template.
/// </summary>
public static partial class CatalogGrounding
{
    private static readonly Dictionary<string, string[]> RelationPhrases = new(StringComparer.Ordinal)
    {
        ["greaterThan"] = ["greater than", "more than", "nhiều hơn"],
        ["atLeast"] = ["at least", "ít nhất"],
    };

    public static IReadOnlyList<CompletionAsk>? Match(string objective, string doneWhen, IReadOnlyList<CapabilityDefinition> catalog)
    {
        var text = $"{objective}\n{doneWhen}";
        var tokens = DottedTokens().Matches(text).Select(match => match.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var unknown = tokens.Where(token => !catalog.Any(item => Same(item.Id, token) || Same(item.ToolName, token))).ToArray();
        if (unknown.Length > 0)
            return [Ask("grounded-unknown", unknown[0], "OBSERVATION", "atLeast", "1")];

        var approval = catalog.FirstOrDefault(item => item.RequiresApproval && tokens.Any(token => Same(item.Id, token) || Same(item.ToolName, token)));
        if (approval is not null)
            return [Ask("grounded-approval", approval.Id, "OBSERVATION", "atLeast", "1")];

        var readable = catalog.Where(item => !item.RequiresApproval).ToArray();
        var subjects = readable
            .SelectMany(item => item.Subjects)
            .Distinct(StringComparer.Ordinal)
            .Select(subject => (Subject: subject, At: WordAt(text, subject)))
            .Where(item => item.At >= 0)
            .OrderBy(item => item.At)
            .Select(item => item.Subject)
            .ToArray();
        if (HasPhrase(text, "greaterThan") && subjects.Length >= 2)
        {
            var definition = readable.FirstOrDefault(item =>
                item.Relations.Contains("greaterThan")
                && item.Subjects.Contains(subjects[0])
                && item.Subjects.Contains(subjects[1]));
            if (definition is not null)
                return [Ask("grounded-compare", definition.Id, subjects[0], "greaterThan", subjects[1])];
        }

        if (HasPhrase(text, "atLeast") && subjects.Length >= 1 && TryCount(text, out var count))
        {
            var definition = readable.FirstOrDefault(item => item.Relations.Contains("atLeast") && item.Subjects.Contains(subjects[0]));
            if (definition is not null)
                return [Ask("grounded-count", definition.Id, subjects[0], "atLeast", count.ToString())];
        }

        if (HasPhrase(text, "greaterThan") || HasPhrase(text, "atLeast"))
        {
            var definition = readable.FirstOrDefault() ?? catalog[0];
            return [Ask("grounded-gap", definition.Id, "UNSPECIFIED", "atLeast", "1")];
        }

        return null;
    }

    private static bool HasPhrase(string text, string relation)
        => RelationPhrases.TryGetValue(relation, out var phrases) && phrases.Any(phrase => text.Contains(phrase, StringComparison.OrdinalIgnoreCase));

    private static bool TryCount(string text, out int count)
    {
        count = 0;
        var phraseAt = -1;
        foreach (var phrase in RelationPhrases["atLeast"])
        {
            var at = text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
            if (at >= 0 && (phraseAt < 0 || at < phraseAt))
                phraseAt = at + phrase.Length;
        }

        if (phraseAt < 0)
            return false;
        var match = CountToken().Match(text, phraseAt);
        return match.Success && int.TryParse(match.Groups[1].Value, out count) && count >= 1;
    }

    private static int WordAt(string text, string word)
    {
        var match = Regex.Match(text, $@"(?<![A-Za-z0-9]){Regex.Escape(word)}(?![A-Za-z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Index : -1;
    }

    private static bool Same(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static CompletionAsk Ask(string id, string capability, string subject, string relation, string target)
        => new()
        {
            Id = id,
            Capability = capability,
            Subject = subject,
            Relation = relation,
            Target = target,
        };

    [GeneratedRegex(@"[A-Za-z][A-Za-z0-9]*\.[A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z][A-Za-z0-9_]*)*", RegexOptions.CultureInvariant)]
    private static partial Regex DottedTokens();

    [GeneratedRegex(@"([1-9][0-9]{0,6})", RegexOptions.CultureInvariant)]
    private static partial Regex CountToken();
}
