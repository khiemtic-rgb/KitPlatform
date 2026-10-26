namespace KitPlatform.Packs.PersonalIntelligence.Trading;

public sealed record SampleWindow(
    int ClosedTrades,
    ResearchPerformanceReport Performance,
    decimal? AverageHoldHours,
    DateTimeOffset? From,
    DateTimeOffset? To);

public sealed record SampleSplit(
    bool Known,
    DateTimeOffset? SplitAt,
    SampleWindow InSample,
    SampleWindow OutOfSample,
    string Note);

/// <summary>
/// Time-ordered 70/30 split. Trades are classified by entry time.
/// The later window is not shuffled and is not used to choose the earlier signals.
/// </summary>
public static class ResearchRobustness
{
    public const string Note = "time_ordered_entry_split";

    public static SampleSplit Split(IReadOnlyList<DateTimeOffset> candleOpens, IReadOnlyList<LedgerTrade> trades)
    {
        ArgumentNullException.ThrowIfNull(candleOpens);
        ArgumentNullException.ThrowIfNull(trades);
        var ordered = candleOpens.OrderBy(time => time).ToArray();
        if (ordered.Length < 10)
            return Unknown();
        var index = (int)Math.Floor(ordered.Length * 0.7m);
        if (index <= 0 || index >= ordered.Length)
            return Unknown();
        var splitAt = ordered[index];
        var closed = trades.Where(trade => trade.Status == ResearchTradeLedger.Closed).ToArray();
        var sample = closed.Where(trade => trade.OpenedAt is not null && trade.OpenedAt < splitAt).ToArray();
        var later = closed.Where(trade => trade.OpenedAt is not null && trade.OpenedAt >= splitAt).ToArray();
        return new SampleSplit(true, splitAt, Window(sample), Window(later), Note);
    }

    static SampleSplit Unknown()
        => new(false, null, Window([]), Window([]), "unavailable");

    static SampleWindow Window(IReadOnlyList<LedgerTrade> trades)
    {
        var hold = trades.Where(trade => trade.OpenedAt is not null && trade.ClosedAt is not null).ToArray();
        decimal? average = hold.Length == 0 || hold.Length != trades.Count
            ? null
            : hold.Average(trade => (decimal)(trade.ClosedAt!.Value - trade.OpenedAt!.Value).TotalHours);
        return new SampleWindow(
            trades.Count,
            ResearchPerformance.FromClosedTrades(trades),
            average,
            trades.MinBy(trade => trade.OpenedAt)?.OpenedAt,
            trades.MaxBy(trade => trade.ClosedAt)?.ClosedAt);
    }
}
