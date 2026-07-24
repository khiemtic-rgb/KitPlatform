using System.Collections.Generic;

namespace KitPlatform.Infrastructure.Learning;

/// <summary>Rubric quan sát tại quầy theo level (soft, ≥80%).</summary>
internal static class LearningObservationRules
{
    public const int PassPct = 80;

    private static readonly string[] GenericKeys =
    [
        "applied", "attitude", "process", "ask_help", "on_shift", "boundaries"
    ];

    private static readonly string[] L0Keys =
    [
        "login", "dashboard", "checklist_open", "notify", "checklist_done", "logout"
    ];

    private static readonly string[] L1Keys =
    [
        "greet", "listen", "needs", "counsel", "pos", "fefo", "pay", "usage", "thanks"
    ];

    private static readonly string[] L2Keys =
    [
        "greet", "lookup", "crm", "points", "program", "no_push", "explain", "remind"
    ];

    private static readonly string[] L3Keys =
    [
        "lot", "expiry", "fefo", "receive", "damage", "count", "process"
    ];

    private static readonly string[] L4Keys =
    [
        "ontime", "login", "open_checklist", "dashboard", "tasks", "mid_checklist",
        "handover", "close_checklist", "logout"
    ];

    private static readonly string[] L5Keys =
    [
        "greet", "listen", "ask", "advise", "usage", "no_push", "crm", "aftercare"
    ];

    private static readonly string[] L6Keys =
    [
        "assign", "coordinate", "coach", "incidents", "dashboard", "checklist", "quality", "report"
    ];

    public static IReadOnlyList<string> KeysForLevel(string? levelCode)
    {
        var level = levelCode?.Trim() ?? "";
        if (string.Equals(level, "L0", StringComparison.OrdinalIgnoreCase))
            return L0Keys;
        if (string.Equals(level, "L1", StringComparison.OrdinalIgnoreCase))
            return L1Keys;
        if (string.Equals(level, "L2", StringComparison.OrdinalIgnoreCase))
            return L2Keys;
        if (string.Equals(level, "L3", StringComparison.OrdinalIgnoreCase))
            return L3Keys;
        if (string.Equals(level, "L4", StringComparison.OrdinalIgnoreCase))
            return L4Keys;
        if (string.Equals(level, "L5", StringComparison.OrdinalIgnoreCase))
            return L5Keys;
        if (string.Equals(level, "L6", StringComparison.OrdinalIgnoreCase))
            return L6Keys;
        return GenericKeys;
    }

    /// <summary>Trả lỗi nếu chưa đạt ngưỡng; null nếu OK.</summary>
    public static string? ValidateOrError(
        string? levelCode,
        IReadOnlyDictionary<string, bool> criteria)
    {
        var keys = KeysForLevel(levelCode);
        var passed = 0;
        foreach (var key in keys)
        {
            if (criteria.TryGetValue(key, out var ok) && ok)
                passed++;
        }

        var pct = keys.Count == 0 ? 0 : (int)Math.Round(100.0 * passed / keys.Count);
        if (pct < PassPct)
        {
            return $"Cần đạt ≥ {PassPct}% tiêu chí quan sát (hiện {passed}/{keys.Count} = {pct}%).";
        }

        return null;
    }
}
