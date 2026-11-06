namespace KitPlatform.Packs.Sales;

public static class SalesCohort
{
    public const int NovixaQualificationMaturityDays = 7;

    public static readonly IReadOnlyDictionary<string, int> NovixaTransitionMaturityDays =
        new Dictionary<string, int>
        {
            ["research>phc"] = 14,
            ["phc>demo"] = 14,
            ["demo>pilot"] = 14,
            ["pilot>paid"] = 30,
        };

    public static bool InEligibleWindow(
        DateTimeOffset createdAt,
        DateTimeOffset periodStart,
        DateTimeOffset asOf,
        int maturityDays) =>
        createdAt >= periodStart && createdAt <= asOf.AddDays(-maturityDays);

    public static decimal? Rate(int cohort, int reached, DateTimeOffset cohortEnd, DateTimeOffset asOf, int maturityDays)
    {
        if (cohort <= 0)
            return null;
        if (asOf < cohortEnd.AddDays(maturityDays))
            return null;
        return Math.Round((decimal)reached / cohort, 4);
    }

    public static decimal? Flow(int entered, int reachedInPeriod) =>
        entered <= 0 ? null : Math.Round((decimal)reachedInPeriod / entered, 4);
}

public readonly record struct StageVisitEvent(string ToStatus, DateTimeOffset At);

public static class SalesDwell
{
    public static (IReadOnlyDictionary<string, TimeSpan> Closed, TimeSpan Open, string? OpenStatus) Compute(
        IReadOnlyList<StageVisitEvent> events,
        DateTimeOffset asOf)
    {
        var closed = new Dictionary<string, TimeSpan>(StringComparer.Ordinal);
        string? open = null;
        DateTimeOffset openAt = default;
        foreach (var ev in events.OrderBy(e => e.At))
        {
            if (open is not null && string.Equals(open, ev.ToStatus, StringComparison.Ordinal))
                continue;
            if (open is not null)
            {
                var span = ev.At - openAt;
                if (span < TimeSpan.Zero)
                    span = TimeSpan.Zero;
                closed[open] = closed.GetValueOrDefault(open) + span;
            }

            if (ev.ToStatus == "lost")
            {
                open = null;
                continue;
            }

            open = ev.ToStatus;
            openAt = ev.At;
        }

        var openSpan = open is null ? TimeSpan.Zero : (asOf > openAt ? asOf - openAt : TimeSpan.Zero);
        return (closed, openSpan, open);
    }
}
