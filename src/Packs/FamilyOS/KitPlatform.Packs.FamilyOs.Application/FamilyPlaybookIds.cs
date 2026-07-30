namespace KitPlatform.Packs.FamilyOs;

/// <summary>Wave B starter playbook IDs — stable codes for CoachInsight / AFE (not a CMS).</summary>
public static class FamilyPlaybookIds
{
    public const string BrushForgot = "PB0001";
    public const string PackForgot = "PB0002";
    public const string SleepLate = "PB0003";
    public const string SkipReading = "PB0004";
    public const string TooManyNudges = "PB0005";
    public const string EveningOverdue = "PB0006";
    public const string AwaitingCheck = "PB0007";
    public const string PatternForgot = "PB0008";
    public const string SparseBlueprint = "PB0020";
    public const string SkipForgot = "PB0017";

    public static string? Resolve(
        string? proposalCode,
        string? focusTitle,
        int patternForgotCount,
        bool blueprintSparse = false)
    {
        var title = focusTitle ?? "";
        var brush = LooksBrush(title);
        var pack = LooksPack(title);
        var sleep = LooksSleep(title);
        var read = LooksRead(title);

        if (patternForgotCount >= 3)
        {
            if (brush) return BrushForgot;
            if (pack) return PackForgot;
            if (sleep) return SleepLate;
            if (read) return SkipReading;
            return PatternForgot;
        }

        if (string.Equals(proposalCode, FamilyCoachProposalCodes.SuggestMoveAfterDinner, StringComparison.OrdinalIgnoreCase)
            || string.Equals(proposalCode, FamilyCoachProposalCodes.SuggestMoveAfterSchool, StringComparison.OrdinalIgnoreCase))
        {
            if (brush) return BrushForgot;
            if (pack) return PackForgot;
            return PatternForgot;
        }

        if (string.Equals(proposalCode, FamilyCoachProposalCodes.SupportOverdue, StringComparison.OrdinalIgnoreCase))
            return EveningOverdue;

        if (brush) return BrushForgot;
        if (pack) return PackForgot;
        if (sleep) return SleepLate;
        if (read) return SkipReading;

        // No habit/pattern signal — sparse Blueprint asks for DNA first.
        if (blueprintSparse) return SparseBlueprint;

        return null;
    }

    private static bool LooksBrush(string title)
    {
        var t = title.ToLowerInvariant();
        return t.Contains("đánh răng", StringComparison.Ordinal)
            || t.Contains("danh rang", StringComparison.Ordinal)
            || t.Contains("brush", StringComparison.Ordinal);
    }

    private static bool LooksPack(string title)
    {
        var t = title.ToLowerInvariant();
        return t.Contains("cặp", StringComparison.Ordinal)
            || t.Contains("balo", StringComparison.Ordinal)
            || t.Contains("chuẩn bị", StringComparison.Ordinal)
            || t.Contains("chuan bi", StringComparison.Ordinal)
            || t.Contains("đồng phục", StringComparison.Ordinal);
    }

    private static bool LooksSleep(string title)
    {
        var t = title.ToLowerInvariant();
        return t.Contains("ngủ", StringComparison.Ordinal) || t.Contains("ngu ", StringComparison.Ordinal);
    }

    private static bool LooksRead(string title)
    {
        var t = title.ToLowerInvariant();
        return t.Contains("đọc", StringComparison.Ordinal) || t.Contains("sách", StringComparison.Ordinal);
    }
}
