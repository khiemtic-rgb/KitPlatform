namespace KitPlatform.Packs.PersonalIntelligence.Trading;

public sealed record ResearchPerformanceReport(
    int ClosedTrades,
    decimal? WinRate,
    bool WinRateKnown,
    decimal? ProfitFactor,
    bool ProfitFactorKnown,
    string ProfitFactorStatus,
    decimal? Expectancy,
    bool ExpectancyKnown,
    decimal? NetPnl,
    bool NetPnlKnown,
    decimal? MaxDrawdown,
    bool MaxDrawdownKnown);

/// <summary>
/// Metrics from closed research trades only. Open positions and account balances are not inputs.
/// </summary>
public static class ResearchPerformance
{
    public static ResearchPerformanceReport FromClosedTrades(IReadOnlyList<LedgerTrade> trades)
    {
        ArgumentNullException.ThrowIfNull(trades);
        var closed = trades.Where(row => row.Status == ResearchTradeLedger.Closed).ToArray();
        if (closed.Length == 0 || closed.Any(row => !row.NetKnown || row.NetPnl is null))
            return Unknown(closed.Length);

        var ordered = closed
            .OrderBy(row => row.ClosedAt ?? DateTimeOffset.MinValue)
            .ThenBy(row => row.TradeId, StringComparer.Ordinal)
            .ToArray();
        var wins = ordered.Count(row => row.NetPnl > 0m);
        var gain = ordered.Where(row => row.NetPnl > 0m).Sum(row => row.NetPnl!.Value);
        var loss = ordered.Where(row => row.NetPnl < 0m).Sum(row => row.NetPnl!.Value);
        var net = ordered.Sum(row => row.NetPnl!.Value);
        decimal equity = 0m;
        decimal peak = 0m;
        decimal drawdown = 0m;
        foreach (var row in ordered)
        {
            equity += row.NetPnl!.Value;
            if (equity > peak)
                peak = equity;
            var gap = peak - equity;
            if (gap > drawdown)
                drawdown = gap;
        }

        var factorKnown = loss < 0m;
        return new ResearchPerformanceReport(
            ordered.Length,
            wins / (decimal)ordered.Length,
            true,
            factorKnown ? gain / decimal.Abs(loss) : null,
            factorKnown,
            factorKnown ? "realized" : "undefined",
            net / ordered.Length,
            true,
            net,
            true,
            drawdown,
            true);
    }

    static ResearchPerformanceReport Unknown(int closed) => new(
        closed, null, false, null, false, "unavailable", null, false, null, false, null, false);
}
