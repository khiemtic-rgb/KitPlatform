using System.Net;
using System.Text;
using Xunit;
using KitPlatform.Packs.PersonalIntelligence.ResearchHost;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class ResearchStrategyExpansionTests
{
    [Fact]
    public void Baseline_sma_matches_the_original_signal_keys()
    {
        var candles = Hours(40, hour => hour == 20 ? 200m : hour > 20 ? 1m : 100m);
        var signals = ResearchStrategies.Signals(ResearchStrategies.Resolve(ResearchStrategies.Sma, null), candles);

        Assert.Equal(SmaCrossStrategy.Signals(candles).Select(signal => signal.SignalKey), signals.Select(signal => signal.SignalKey));
        Assert.Contains(signals, signal => signal.Status == "ready" && signal.FillPrice != CloseAt(candles, signal));
    }

    [Theory]
    [InlineData(ResearchStrategies.Sma, null)]
    [InlineData(ResearchStrategies.Ema, null)]
    [InlineData(ResearchStrategies.Rsi, null)]
    [InlineData(ResearchStrategies.Breakout, "lookback=5")]
    public void Ready_signal_uses_the_next_open_and_a_later_bar_does_not_rewrite_it(string id, string? canonical)
    {
        var candles = id switch
        {
            ResearchStrategies.Ema => Trend(),
            ResearchStrategies.Rsi => RsiSeries(),
            ResearchStrategies.Breakout => BreakoutSeries(),
            _ => Hours(40, hour => hour == 20 ? 200m : hour > 20 ? 1m : 100m),
        };
        var spec = ResearchStrategies.Resolve(id, canonical);
        var first = ResearchStrategies.Signals(spec, candles);
        var ready = first.Where(signal => signal.Status == "ready").ToArray();
        Assert.NotEmpty(ready);
        Assert.All(ready, signal =>
        {
            var index = IndexOf(candles, signal);
            Assert.Equal(candles[index + 1].Open, signal.FillPrice);
        });

        var second = ResearchStrategies.Signals(spec, candles.Append(Bar(candles.Length, 1000m)).ToArray());
        foreach (var signal in ready)
        {
            var match = Assert.Single(second, row => row.SignalKey == signal.SignalKey);
            Assert.Equal(signal.Side, match.Side);
            Assert.Equal(signal.FillPrice, match.FillPrice);
        }
    }

    [Fact]
    public void Breakout_and_rsi_read_only_bars_already_closed()
    {
        var candles = BreakoutSeries();
        var signal = Assert.Single(ResearchStrategies.Signals(ResearchStrategies.Resolve(ResearchStrategies.Breakout, "lookback=5"), candles), row => row.Side == "BUY");
        Assert.Equal(candles[6].Open, signal.FillPrice);

        var closes = new decimal[] { 10m, 9m, 8m, 7m, 7.2m, 9m };
        var early = ResearchStrategies.RsiValue(closes, 3, 4);
        Assert.True(early < 30m);
        Assert.True(ResearchStrategies.RsiValue(closes, 3, 5) >= 30m);
        Assert.Equal(early, ResearchStrategies.RsiValue(closes.Take(5).ToArray(), 3, 4));
    }

    [Fact]
    public void Invalid_duplicate_and_gapped_candles_are_not_replaced()
    {
        var open = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var json = "[" + Kline(open, 100m) + "," + Kline(open, 100m) + "," + Kline(open.AddHours(2), 100m) + "]";
        var intake = ResearchCandles.ParseBinance(json, "BTCUSDT", open.AddHours(4), TimeSpan.FromHours(96));

        Assert.Equal(1, intake.Duplicates);
        Assert.Equal(1, intake.Gaps);
        Assert.Equal(2, intake.Accepted.Count);
        Assert.Contains(intake.Problems, problem => problem.StartsWith("gap:", StringComparison.Ordinal));
    }

    [Fact]
    public void Malformed_ohlc_is_rejected()
    {
        var open = DateTimeOffset.Parse("2026-01-01T00:00:00Z").ToUnixTimeMilliseconds();
        var json = $"[[{open},\"100\",\"90\",\"100\",\"100\",\"0\",{open + 3_599_999},\"0\",0,\"0\",\"0\",\"0\"]]";
        var intake = ResearchCandles.ParseBinance(json, "BTCUSDT", DateTimeOffset.Parse("2026-01-02T00:00:00Z"), TimeSpan.FromHours(96));

        Assert.Empty(intake.Accepted);
        Assert.Contains(intake.Problems, problem => problem.Contains("high/low", StringComparison.Ordinal));
    }

    [Fact]
    public void Out_of_sample_follows_entry_time()
    {
        var opens = Enumerable.Range(0, 10).Select(hour => DateTimeOffset.Parse("2026-01-01T00:00:00Z").AddHours(hour)).ToArray();
        var split = ResearchRobustness.Split(opens, [Trade(opens[8], -2m), Trade(opens[1], 1m)]);

        Assert.True(split.Known);
        Assert.Equal(opens[7], split.SplitAt);
        Assert.Equal(1, split.InSample.ClosedTrades);
        Assert.Equal(1m, split.InSample.Performance.NetPnl);
        Assert.Equal(1, split.OutOfSample.ClosedTrades);
        Assert.Equal(-2m, split.OutOfSample.Performance.NetPnl);
        Assert.NotEqual(split.InSample.Performance.NetPnl, split.OutOfSample.Performance.NetPnl);
    }

    [Fact]
    public void Different_parameters_do_not_share_the_baseline_hash()
    {
        var intake = ResearchCandles.Classify(Hours(5, _ => 100m), DateTimeOffset.Parse("2026-01-03T00:00:00Z"), TimeSpan.FromDays(30));
        var config = new ResearchConfig(0.001m, 0.8m, 10000m, new PaperSessionConfig(10m, 0.001m, 0m, 1m, new RiskLimits(1000m, 1m, 0.5m), 5000m, false));
        var baseline = ResearchSession.Hash(intake, config, ResearchStrategies.Resolve(ResearchStrategies.Sma, null));
        var variant = ResearchSession.Hash(intake, config, ResearchStrategies.Resolve(ResearchStrategies.Sma, "fast=8,slow=21"));

        Assert.Equal(ResearchSession.Hash(intake, config), baseline);
        Assert.NotEqual(baseline, variant);
    }

    [Fact]
    public async Task History_pages_backward_without_inventing_missing_bars()
    {
        var first = DateTimeOffset.Parse("2026-06-01T00:00:00Z");
        var handler = new PageHandler(first);
        using var client = new BinancePublicKlineClient(handler);
        var page = await client.ReadHistoryAsync("BTCUSDT", 600, 2, CancellationToken.None);
        var intake = ResearchCandles.ParseBinance(page.Body, "BTCUSDT", first.AddHours(700), TimeSpan.FromDays(40));

        Assert.Equal(2, page.Requests);
        Assert.Contains("endTime=", handler.Requests[1].RequestUri!.Query, StringComparison.Ordinal);
        Assert.Equal(600, intake.Accepted.Count);
        Assert.Equal(0, intake.Gaps);
        Assert.Equal(first, intake.Accepted[0].OpenTime);
    }

    static int IndexOf(IReadOnlyList<ResearchCandle> candles, ResearchSignal signal)
        => candles.Select((candle, index) => (candle, index)).Single(row => row.candle.CloseTime == signal.SignalTime).index;

    static decimal CloseAt(IReadOnlyList<ResearchCandle> candles, ResearchSignal signal)
        => candles[IndexOf(candles, signal)].Close;

    static ResearchCandle[] Hours(int count, Func<int, decimal> close)
        => Enumerable.Range(0, count).Select(hour => Bar(hour, close(hour))).ToArray();

    static ResearchCandle[] Trend()
    {
        var values = new List<decimal>();
        for (var hour = 0; hour < 30; hour++)
            values.Add(100m);
        for (var hour = 0; hour < 30; hour++)
            values.Add(100m + hour);
        for (var hour = 0; hour < 30; hour++)
            values.Add(129m - (hour * 3m));
        return values.Select((close, hour) => Bar(hour, close)).ToArray();
    }

    static ResearchCandle[] RsiSeries()
    {
        var closes = new List<decimal>();
        for (var hour = 0; hour < 30; hour++)
            closes.Add(100m - hour);
        closes.AddRange([71m, 80m, 90m, 96m, 99m, 70m, 55m, 40m, 30m, 20m, 35m, 55m, 75m]);
        return closes.Select((close, hour) => Bar(hour, close)).ToArray();
    }

    static ResearchCandle[] BreakoutSeries()
    {
        var candles = new List<ResearchCandle>();
        for (var hour = 0; hour < 5; hour++)
            candles.Add(Bar(hour, 10m));
        candles.Add(Bar(5, 12m));
        candles.Add(Bar(6, 11m));
        return candles.ToArray();
    }

    static ResearchCandle Bar(int hour, decimal close)
    {
        var openTime = DateTimeOffset.Parse("2026-01-01T00:00:00Z").AddHours(hour);
        var open = close == 12m ? 10m : close;
        var high = Math.Max(open, close);
        var low = Math.Min(open, close);
        return new ResearchCandle("binance-public-klines", "BTCUSDT", "1h", openTime, openTime.AddHours(1).AddMilliseconds(-1), open, high, low, close, "{}");
    }

    static string Kline(DateTimeOffset open, decimal close)
    {
        var openMs = open.ToUnixTimeMilliseconds();
        var text = close.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return $"[{openMs},\"{text}\",\"{text}\",\"{text}\",\"{text}\",\"0\",{openMs + 3_599_999},\"0\",0,\"0\",\"0\",\"0\"]";
    }

    static LedgerTrade Trade(DateTimeOffset opened, decimal net) => new(
        "trade-" + opened.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
        ResearchTradeLedger.Closed, ResearchTradeLedger.Long, "BTCUSDT", "1h", ResearchStrategies.Sma,
        opened, opened.AddHours(2), 100m, 101m, 0.001m, net, true, 0.01m, true, 0.01m, true, net, true,
        PaperSession.Realized, ResearchTradeLedger.CloseReasonSmaCrossBelow, ["a"]);

    sealed class PageHandler : HttpMessageHandler
    {
        readonly DateTimeOffset _first;
        public List<HttpRequestMessage> Requests { get; } = [];

        public PageHandler(DateTimeOffset first) => _first = first;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var query = request.RequestUri!.Query;
            var limit = int.Parse(System.Text.RegularExpressions.Regex.Match(query, "limit=(\\d+)").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            var start = query.Contains("endTime=", StringComparison.Ordinal) ? _first : _first.AddHours(100);
            var rows = Enumerable.Range(0, limit).Select(hour => Kline(start.AddHours(hour), 100m));
            var body = "[" + string.Join(',', rows) + "]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
