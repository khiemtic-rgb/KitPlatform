namespace KitPlatform.Packs.PersonalIntelligence.Trading;

public sealed record ResearchSignal(
    string SignalKey,
    string Symbol,
    string Side,
    DateTimeOffset SignalTime,
    DateTimeOffset? FillTime,
    decimal? FillPrice,
    string Status,
    string? StrategyId = null,
    string? StrategyVersion = null,
    string? Timeframe = null,
    DateTimeOffset? CandleOpenTime = null);

/// <summary>
/// sma-cross-v1. Timeframe is one hour. Long only, one position.
/// A decision at bar t uses closes of bars at or before t. It never reads a later close.
/// Entry: fast SMA(5) crosses above slow SMA(20). The previous bar had fast &lt;= slow.
/// Exit: fast SMA crosses below slow SMA while a long position is open.
/// The first eligible bar is index 20, so both averages exist on that bar and the previous bar.
/// A fill, when the next bar exists, uses that next bar's open. Its close is not an input.
/// If the next bar does not exist yet, the signal stays awaiting_next_open and is not a fill.
/// </summary>
public static class SmaCrossStrategy
{
    public const string Id = "sma-cross-v1";
    public const string Version = "fast=5,slow=20";
    public const int FastPeriod = 5;
    public const int SlowPeriod = 20;
    public const string Timeframe = "1h";
    public const string Ready = "ready";
    public const string AwaitingNextOpen = "awaiting_next_open";

    public static IReadOnlyList<ResearchSignal> Signals(IReadOnlyList<ResearchCandle> candles)
    {
        var signals = new List<ResearchSignal>();
        var position = 0;
        for (var index = SlowPeriod; index < candles.Count; index++)
        {
            var previousFast = Sma(candles, index - 1, FastPeriod);
            var previousSlow = Sma(candles, index - 1, SlowPeriod);
            var fast = Sma(candles, index, FastPeriod);
            var slow = Sma(candles, index, SlowPeriod);
            string? side = null;
            if (position == 0 && previousFast <= previousSlow && fast > slow)
                side = "BUY";
            else if (position == 1 && previousFast >= previousSlow && fast < slow)
                side = "SELL";
            if (side is null)
                continue;

            position = side == "BUY" ? 1 : 0;
            var bar = candles[index];
            var key = $"{Id}:{bar.Symbol}:{Timeframe}:{bar.OpenTime.ToUnixTimeMilliseconds()}:{side}";
            var next = index + 1 < candles.Count ? candles[index + 1] : null;
            signals.Add(new ResearchSignal(
                key,
                bar.Symbol,
                side,
                bar.CloseTime,
                next?.OpenTime,
                next?.Open,
                next is null ? AwaitingNextOpen : Ready,
                Id,
                Version,
                Timeframe,
                bar.OpenTime));
        }

        return signals;
    }

    private static decimal Sma(IReadOnlyList<ResearchCandle> candles, int endInclusive, int period)
    {
        var start = endInclusive - period + 1;
        if (start < 0)
            throw new InvalidOperationException("SMA window would read before the first candle.");
        decimal sum = 0m;
        for (var index = start; index <= endInclusive; index++)
            sum += candles[index].Close;
        return sum / period;
    }
}
