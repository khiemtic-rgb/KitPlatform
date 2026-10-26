namespace KitPlatform.Packs.PersonalIntelligence.Trading;

public sealed record StrategySpec(
    string Id,
    string Family,
    string Canonical,
    int Fast,
    int Slow,
    int RsiPeriod,
    decimal RsiEntry,
    decimal RsiExit,
    int Lookback)
{
    public bool IsBaselineSma
        => Id == ResearchStrategies.Sma && Fast == SmaCrossStrategy.FastPeriod && Slow == SmaCrossStrategy.SlowPeriod;
}

/// <summary>
/// Closed-bar strategies. A signal at bar t uses that bar and older bars only.
/// A fill uses the next bar open. The signal bar close is not a fill price.
/// </summary>
public static class ResearchStrategies
{
    public const string EngineVersion = "research-engine-v2";
    public const string Sma = "sma-cross-v1";
    public const string Ema = "ema-cross-v1";
    public const string Rsi = "rsi-reversion-v1";
    public const string Breakout = "breakout-v1";
    public const string Ready = "ready";
    public const string AwaitingNextOpen = "awaiting_next_open";

    public static IReadOnlyList<string> Ids { get; } = [Sma, Ema, Rsi, Breakout];

    public static StrategySpec Resolve(string? id, string? canonical)
    {
        var strategy = string.IsNullOrWhiteSpace(id) ? Sma : id.Trim().ToLowerInvariant();
        if (!Ids.Contains(strategy))
            throw new ArgumentOutOfRangeException(nameof(id), "Unknown research strategy.");
        var pairs = Parse(string.IsNullOrWhiteSpace(canonical) ? DefaultCanonical(strategy) : canonical);
        return strategy switch
        {
            Sma => MovingAverage(Sma, "trend", pairs, 5, 20),
            Ema => MovingAverage(Ema, "trend", pairs, 12, 26),
            Rsi => RsiSpec(pairs),
            Breakout => BreakoutSpec(pairs),
            _ => throw new ArgumentOutOfRangeException(nameof(id), "Unknown research strategy."),
        };
    }

    public static string DefaultCanonical(string id) => id switch
    {
        Sma => "fast=5,slow=20",
        Ema => "fast=12,slow=26",
        Rsi => "entry=30,exit=50,period=14",
        Breakout => "lookback=20",
        _ => throw new ArgumentOutOfRangeException(nameof(id), "Unknown research strategy."),
    };

    public static string? CloseReason(string? strategyId) => strategyId switch
    {
        Sma => ResearchTradeLedger.CloseReasonSmaCrossBelow,
        Ema => "ema_cross_below",
        Rsi => "rsi_reversion_exit",
        Breakout => "breakout_range_exit",
        _ => null,
    };

    public static IReadOnlyList<ResearchSignal> Signals(StrategySpec spec, IReadOnlyList<ResearchCandle> candles)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(candles);
        if (spec.IsBaselineSma)
            return SmaCrossStrategy.Signals(candles);
        return spec.Id switch
        {
            Sma or Ema => MovingAverageSignals(spec, candles),
            Rsi => RsiSignals(spec, candles),
            Breakout => BreakoutSignals(spec, candles),
            _ => throw new ArgumentOutOfRangeException(nameof(spec), "Unknown research strategy."),
        };
    }

    public static decimal RsiValue(IReadOnlyList<decimal> closes, int period, int endInclusive)
    {
        if (period < 2 || endInclusive < period || endInclusive >= closes.Count)
            throw new ArgumentOutOfRangeException(nameof(endInclusive), "RSI would read outside the closed bars.");
        decimal gain = 0m;
        decimal loss = 0m;
        for (var index = 1; index <= period; index++)
        {
            var change = closes[index] - closes[index - 1];
            if (change >= 0m)
                gain += change;
            else
                loss -= change;
        }

        gain /= period;
        loss /= period;
        for (var index = period + 1; index <= endInclusive; index++)
        {
            var change = closes[index] - closes[index - 1];
            var up = change > 0m ? change : 0m;
            var down = change < 0m ? -change : 0m;
            gain = ((gain * (period - 1)) + up) / period;
            loss = ((loss * (period - 1)) + down) / period;
        }

        if (loss == 0m)
            return 100m;
        var rs = gain / loss;
        return 100m - (100m / (1m + rs));
    }

    static StrategySpec MovingAverage(string id, string family, Dictionary<string, decimal> pairs, int fastDefault, int slowDefault)
    {
        RequireKeys(pairs, "fast", "slow");
        var fast = Integer(pairs, "fast", fastDefault, 2, 30);
        var slow = Integer(pairs, "slow", slowDefault, fast + 1, 80);
        return new StrategySpec(id, family, Canonical(pairs), fast, slow, 14, 30m, 50m, 20);
    }

    static StrategySpec RsiSpec(Dictionary<string, decimal> pairs)
    {
        RequireKeys(pairs, "period", "entry", "exit");
        var period = Integer(pairs, "period", 14, 2, 30);
        var entry = pairs["entry"];
        var exit = pairs["exit"];
        if (entry is < 5m or > 40m || exit is < 20m or > 90m || exit < entry + 5m)
            throw new ArgumentOutOfRangeException(nameof(pairs), "RSI entry and exit are outside the research bounds.");
        return new StrategySpec(Rsi, "mean-reversion", Canonical(pairs), 5, 20, period, entry, exit, 20);
    }

    static StrategySpec BreakoutSpec(Dictionary<string, decimal> pairs)
    {
        RequireKeys(pairs, "lookback");
        var lookback = Integer(pairs, "lookback", 20, 5, 48);
        return new StrategySpec(Breakout, "breakout", Canonical(pairs), 5, 20, 14, 30m, 50m, lookback);
    }

    static IReadOnlyList<ResearchSignal> MovingAverageSignals(StrategySpec spec, IReadOnlyList<ResearchCandle> candles)
    {
        var signals = new List<ResearchSignal>();
        var position = 0;
        for (var index = spec.Slow; index < candles.Count; index++)
        {
            var previousFast = spec.Id == Ema ? EmaValue(candles, index - 1, spec.Fast) : SmaValue(candles, index - 1, spec.Fast);
            var previousSlow = spec.Id == Ema ? EmaValue(candles, index - 1, spec.Slow) : SmaValue(candles, index - 1, spec.Slow);
            var fast = spec.Id == Ema ? EmaValue(candles, index, spec.Fast) : SmaValue(candles, index, spec.Fast);
            var slow = spec.Id == Ema ? EmaValue(candles, index, spec.Slow) : SmaValue(candles, index, spec.Slow);
            string? side = null;
            if (position == 0 && previousFast <= previousSlow && fast > slow)
                side = "BUY";
            else if (position == 1 && previousFast >= previousSlow && fast < slow)
                side = "SELL";
            if (side is null)
                continue;
            position = side == "BUY" ? 1 : 0;
            signals.Add(Emit(spec, candles, index, side));
        }

        return signals;
    }

    static IReadOnlyList<ResearchSignal> RsiSignals(StrategySpec spec, IReadOnlyList<ResearchCandle> candles)
    {
        var closes = candles.Select(candle => candle.Close).ToArray();
        var signals = new List<ResearchSignal>();
        var position = 0;
        for (var index = spec.RsiPeriod + 1; index < candles.Count; index++)
        {
            var previous = RsiValue(closes, spec.RsiPeriod, index - 1);
            var current = RsiValue(closes, spec.RsiPeriod, index);
            string? side = null;
            if (position == 0 && previous < spec.RsiEntry && current >= spec.RsiEntry)
                side = "BUY";
            else if (position == 1 && previous < spec.RsiExit && current >= spec.RsiExit)
                side = "SELL";
            if (side is null)
                continue;
            position = side == "BUY" ? 1 : 0;
            signals.Add(Emit(spec, candles, index, side));
        }

        return signals;
    }

    static IReadOnlyList<ResearchSignal> BreakoutSignals(StrategySpec spec, IReadOnlyList<ResearchCandle> candles)
    {
        var signals = new List<ResearchSignal>();
        var position = 0;
        for (var index = spec.Lookback; index < candles.Count; index++)
        {
            decimal highest = decimal.MinValue;
            decimal lowest = decimal.MaxValue;
            for (var cursor = index - spec.Lookback; cursor < index; cursor++)
            {
                if (candles[cursor].High > highest)
                    highest = candles[cursor].High;
                if (candles[cursor].Low < lowest)
                    lowest = candles[cursor].Low;
            }

            string? side = null;
            if (position == 0 && candles[index].Close > highest)
                side = "BUY";
            else if (position == 1 && candles[index].Close < lowest)
                side = "SELL";
            if (side is null)
                continue;
            position = side == "BUY" ? 1 : 0;
            signals.Add(Emit(spec, candles, index, side));
        }

        return signals;
    }

    static ResearchSignal Emit(StrategySpec spec, IReadOnlyList<ResearchCandle> candles, int index, string side)
    {
        var bar = candles[index];
        var suffix = spec.Canonical == DefaultCanonical(spec.Id) ? "" : ":" + spec.Canonical;
        var key = $"{spec.Id}:{bar.Symbol}:1h:{bar.OpenTime.ToUnixTimeMilliseconds()}:{side}{suffix}";
        if (index + 1 >= candles.Count)
            return new ResearchSignal(key, bar.Symbol, side, bar.CloseTime, null, null, AwaitingNextOpen, spec.Id, spec.Canonical, "1h", bar.OpenTime);
        var next = candles[index + 1];
        return new ResearchSignal(key, bar.Symbol, side, bar.CloseTime, next.OpenTime, next.Open, Ready, spec.Id, spec.Canonical, "1h", bar.OpenTime);
    }

    static decimal SmaValue(IReadOnlyList<ResearchCandle> candles, int endInclusive, int period)
    {
        var start = endInclusive - period + 1;
        if (start < 0)
            throw new InvalidOperationException("SMA window would read before the first candle.");
        decimal sum = 0m;
        for (var index = start; index <= endInclusive; index++)
            sum += candles[index].Close;
        return sum / period;
    }

    static decimal EmaValue(IReadOnlyList<ResearchCandle> candles, int endInclusive, int period)
    {
        if (endInclusive < period - 1)
            throw new InvalidOperationException("EMA window would read before the first candle.");
        decimal seed = 0m;
        for (var index = 0; index < period; index++)
            seed += candles[index].Close;
        var ema = seed / period;
        var weight = 2m / (period + 1);
        for (var index = period; index <= endInclusive; index++)
            ema = (candles[index].Close * weight) + (ema * (1m - weight));
        return ema;
    }

    static Dictionary<string, decimal> Parse(string canonical)
    {
        var pairs = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var part in canonical.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var split = part.Split('=', 2);
            if (split.Length != 2 || !decimal.TryParse(split[1], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var value))
                throw new ArgumentOutOfRangeException(nameof(canonical), "Strategy parameters must look like fast=5,slow=20.");
            pairs[split[0]] = value;
        }

        return pairs;
    }

    static void RequireKeys(Dictionary<string, decimal> pairs, params string[] keys)
    {
        if (pairs.Count != keys.Length || keys.Any(key => !pairs.ContainsKey(key)))
            throw new ArgumentOutOfRangeException(nameof(pairs), "Strategy parameters do not match this strategy.");
    }

    static int Integer(Dictionary<string, decimal> pairs, string key, int fallback, int min, int max)
    {
        var value = pairs.TryGetValue(key, out var parsed) ? parsed : fallback;
        if (value != decimal.Truncate(value) || value < min || value > max)
            throw new ArgumentOutOfRangeException(key, "Strategy parameter is outside the research bounds.");
        return (int)value;
    }

    static string Canonical(Dictionary<string, decimal> pairs)
        => string.Join(',', pairs.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
}
