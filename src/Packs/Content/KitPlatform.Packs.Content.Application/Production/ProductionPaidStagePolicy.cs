namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// At most three provider API calls for one stage of one shot, including the first.
/// Calls before <see cref="CountFrom"/> are history and are not counted.
/// An imported file or a fixture is not an API call.
/// A later call requires a classified failure and a changed input or remedy.
/// The episode ceiling does not buy a fourth call, except owner grants:
/// one extra SH01 video, one extra SH03 still, and one approved SH03 video retry.
/// </summary>
public static class ProductionPaidStagePolicy
{
    public const int MaxPaidAttempts = 3;

    /// <summary>Owner authorized one extra SH01 video after Runway returned no file.</summary>
    public const string OwnerQualityTestShot = "EP01-SC01-SH01";
    public const string OwnerQualityTestAction = "CREATE_VIDEO";
    public const int OwnerQualityTestAttempts = 4;

    /// <summary>Owner authorized one extra SH03 still, then one approved video retry.</summary>
    public const string OwnerRunwayRepairShot = "EP01-SC01-SH03";
    public const int OwnerRunwayRepairAttempts = 4;
    public const int OwnerRunwayVideoRetryAttempts = 5;

    public static int Cap(string? shotCode, string? action)
    {
        if (string.Equals(shotCode, OwnerQualityTestShot, StringComparison.Ordinal)
            && string.Equals(action, OwnerQualityTestAction, StringComparison.Ordinal))
            return OwnerQualityTestAttempts;
        if (string.Equals(shotCode, OwnerRunwayRepairShot, StringComparison.Ordinal)
            && string.Equals(action, "CREATE_VIDEO", StringComparison.Ordinal))
            return OwnerRunwayVideoRetryAttempts;
        if (string.Equals(shotCode, OwnerRunwayRepairShot, StringComparison.Ordinal)
            && string.Equals(action, "CREATE_IMAGE", StringComparison.Ordinal))
            return OwnerRunwayRepairAttempts;
        return MaxPaidAttempts;
    }

    /// <summary>2026-10-19 21:13 +07. The owner restarted the attempt window at this instant.</summary>
    public static readonly DateTimeOffset CountFrom = new(2026, 10, 19, 14, 13, 0, TimeSpan.Zero);
    public const decimal EpisodeCeilingUsd = 14m;
    public const string Allow = "ALLOW";
    public const string Retry = "RETRY";
    public const string Blocked = "BLOCKED";

    public static string Decide(
        int completedAttempts,
        bool failureClassified,
        bool inputChanged,
        decimal spentUsd,
        decimal nextUsd,
        int maxAttempts = MaxPaidAttempts)
    {
        if (completedAttempts < 0 || spentUsd < 0 || nextUsd < 0 || maxAttempts < 1)
            return Blocked;
        if (spentUsd + nextUsd > EpisodeCeilingUsd)
            return Blocked;
        if (completedAttempts == 0)
            return Allow;
        if (completedAttempts >= maxAttempts)
            return Blocked;
        if (!failureClassified || !inputChanged)
            return Blocked;
        return Retry;
    }
}
