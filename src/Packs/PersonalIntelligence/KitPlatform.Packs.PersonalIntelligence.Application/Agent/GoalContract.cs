namespace KitPlatform.Packs.PersonalIntelligence.Agent;

/// <summary>
/// A completion condition a domain can verify. Matching is exact. Free-form text is not interpreted.
/// </summary>
public sealed record TaskCriterion(string Id, string Statement);

public static class GoalContract
{
    public static bool TryMatch(string doneWhen, IReadOnlyList<TaskCriterion> criteria, out TaskCriterion? match)
    {
        var text = doneWhen.Trim();
        foreach (var criterion in criteria)
        {
            if (string.Equals(criterion.Statement, text, StringComparison.Ordinal)
                || string.Equals(criterion.Id, text, StringComparison.Ordinal))
            {
                match = criterion;
                return true;
            }
        }

        match = null;
        return false;
    }
}
