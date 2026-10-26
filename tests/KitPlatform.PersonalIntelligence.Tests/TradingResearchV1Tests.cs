using System.Net;
using System.Text;
using KitPlatform.Packs.PersonalIntelligence.ResearchHost;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public class TradingResearchV1Tests
{
    [Fact]
    public void Public_market_request_allows_only_spot_klines_get()
    {
        var uri = PublicMarketRequest.Klines("BTCUSDT", "1h", 2);
        PublicMarketRequest.EnsureReadOnly("GET", uri);

        Assert.Equal("api.binance.com", uri.Host);
        Assert.Equal("/api/v3/klines", uri.AbsolutePath);
        Assert.Throws<InvalidOperationException>(() => PublicMarketRequest.Klines("BTCUSDT", "1m", 2));
        Assert.Throws<InvalidOperationException>(() => PublicMarketRequest.Klines("BTCUSDT", "1h", 501));
        Assert.Throws<InvalidOperationException>(() => PublicMarketRequest.EnsureReadOnly("POST", uri));
        Assert.Throws<InvalidOperationException>(() =>
            PublicMarketRequest.EnsureReadOnly("GET", new Uri("https://api.binance.com/api/v3/order")));
        Assert.Throws<InvalidOperationException>(() =>
            PublicMarketRequest.EnsureReadOnly("GET", new Uri("https://testnet.binance.vision/api/v3/klines")));
    }

    [Fact]
    public async Task Kline_client_sends_one_get_and_no_order_request()
    {
        var handler = new RecordingHandler("""
            [[1792731600000,"100","110","90","105","1",1792735199999,"0",1,"0","0","0"]]
            """);
        using var client = new BinancePublicKlineClient(handler);
        var response = await client.ReadAsync("BTCUSDT", 2, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v3/klines", request.RequestUri!.AbsolutePath);
        Assert.DoesNotContain("order", request.RequestUri.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("105", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Duplicate_open_time_is_kept_once()
    {
        var open = DateTimeOffset.Parse("2026-01-01T00:00:00Z").ToUnixTimeMilliseconds();
        var row = Kline(open, "100", "100", "100", "100");
        var intake = ResearchCandles.ParseBinance($"[{row},{row}]", "BTCUSDT", DateTimeOffset.Parse("2026-01-02T00:00:00Z"), TimeSpan.FromDays(3));

        Assert.Equal(1, intake.Duplicates);
        Assert.Single(intake.Accepted);
    }

    [Fact]
    public void Stale_and_unclosed_candles_are_excluded()
    {
        var asOf = DateTimeOffset.Parse("2026-01-02T00:00:00Z");
        var stale = Kline(DateTimeOffset.Parse("2026-01-01T00:00:00Z").ToUnixTimeMilliseconds(), "100", "100", "100", "100");
        var fresh = Kline(DateTimeOffset.Parse("2026-01-01T23:00:00Z").ToUnixTimeMilliseconds(), "100", "100", "100", "100");
        var future = Kline(asOf.ToUnixTimeMilliseconds(), "100", "100", "100", "100");
        var intake = ResearchCandles.ParseBinance($"[{stale},{fresh},{future}]", "BTCUSDT", asOf, TimeSpan.FromHours(2));

        Assert.Equal(1, intake.Stale);
        Assert.Equal(1, intake.FutureOrIncomplete);
        var accepted = Assert.Single(intake.Accepted);
        Assert.Equal(DateTimeOffset.Parse("2026-01-01T23:00:00Z"), accepted.OpenTime);
    }

    [Fact]
    public void A_gap_blocks_signals()
    {
        var first = Bar(0, 100m, 100m);
        var third = Bar(3, 100m, 100m);
        var intake = ResearchCandles.Classify([first, third], third.CloseTime, TimeSpan.FromDays(2));
        var report = ResearchSession.Run(intake, Config(), new MemoryPaperOperationLog(), new MemoryResearchLog());

        Assert.Equal(1, intake.Gaps);
        Assert.Equal(ResearchSession.DataGap, report.Status);
        Assert.Equal(0, report.Snapshot.Signals);
        Assert.Empty(report.Snapshot.Fills);
    }

    [Fact]
    public void Sma_cross_fills_the_next_open_and_ignores_a_later_close()
    {
        var bars = CrossSeries();
        var signals = SmaCrossStrategy.Signals(bars);
        var buy = signals[0];
        var sell = signals[1];

        Assert.Equal("BUY", buy.Side);
        Assert.Equal(bars[20].CloseTime, buy.SignalTime);
        Assert.Equal(100m, buy.FillPrice);
        Assert.Equal("SELL", sell.Side);
        Assert.Equal(100m, sell.FillPrice);
        Assert.True(sell.SignalTime > buy.SignalTime);

        bars[21] = Bar(21, 100m, 9_999m);
        var afterCloseChange = SmaCrossStrategy.Signals(bars);
        Assert.Equal(buy.SignalKey, afterCloseChange[0].SignalKey);
        Assert.Equal(100m, afterCloseChange[0].FillPrice);
    }

    [Fact]
    public void Round_trip_applies_fee_slippage_and_cash_drawdown()
    {
        var report = ResearchSession.Run(Intake(CrossSeries()), Config(), new MemoryPaperOperationLog(), new MemoryResearchLog());
        var buy = report.Snapshot.Fills[0];
        var sell = report.Snapshot.Fills[1];

        Assert.Equal(2, report.NewPaperFills);
        Assert.Equal(101m, buy.ExecutionPrice);
        Assert.Equal(1.01m, buy.Fee);
        Assert.Equal(-102.01m, buy.NetCash);
        Assert.Equal(99m, sell.ExecutionPrice);
        Assert.Equal(0.99m, sell.Fee);
        Assert.Equal(98.01m, sell.NetCash);
        Assert.Equal(2m, report.Snapshot.Fees);
        Assert.Equal(2m, report.Snapshot.SlippageQuote);
        Assert.Equal(-4m, report.Snapshot.NetRealizedPnl);
        Assert.Equal(PaperSession.Realized, report.Snapshot.PnlStatus);
        Assert.Equal(102.01m, report.Snapshot.CashMaxDrawdown);
        Assert.Equal(0.010201m, report.Snapshot.CashMaxDrawdownFraction);
        Assert.Equal(ResearchSession.CashDrawdown, report.Snapshot.DrawdownStatus);
    }

    [Fact]
    public void Risk_veto_and_live_mode_do_not_fill()
    {
        var vetoLog = new MemoryPaperOperationLog();
        var veto = ResearchSession.Run(Intake(CrossSeries()), Config(confidence: 0.2m), vetoLog, new MemoryResearchLog());
        Assert.Equal(0, veto.NewPaperFills);
        Assert.Equal(0, veto.Snapshot.PaperFilled);
        Assert.Contains(veto.Snapshot.RiskDecisions, decision => !decision.Allowed && decision.Reasons.Contains("Confidence", StringComparison.Ordinal));

        var live = ResearchSession.Run(Intake(CrossSeries()), Config(live: true), new MemoryPaperOperationLog(), new MemoryResearchLog());
        Assert.Equal(ResearchSession.LiveBlocked, live.Status);
        Assert.Empty(live.Snapshot.Fills);
    }

    [Fact]
    public void The_same_candles_replay_without_a_second_fill()
    {
        var paper = new MemoryPaperOperationLog();
        var research = new MemoryResearchLog();
        var intake = Intake(CrossSeries());
        var first = ResearchSession.Run(intake, Config(), paper, research);
        var operations = paper.Count;
        var second = ResearchSession.Run(intake, Config(), paper, research);

        Assert.Equal(2, first.NewPaperFills);
        Assert.True(second.Replayed);
        Assert.Equal(0, second.NewPaperFills);
        Assert.Equal(first.Snapshot.NetRealizedPnl, second.Snapshot.NetRealizedPnl);
        Assert.Equal(first.Snapshot.Fills.Count, second.Snapshot.Fills.Count);
        Assert.Equal(operations, paper.Count);
        Assert.Equal(1, research.SessionCount);
        Assert.Equal(first.SessionKey, second.SessionKey);
    }

    [Fact]
    public void A_signal_without_the_next_candle_waits_and_then_fills_once()
    {
        var paper = new MemoryPaperOperationLog();
        var research = new MemoryResearchLog();
        var partial = CrossSeries().Take(21).ToArray();
        var waiting = ResearchSession.Run(Intake(partial), Config(), paper, research);
        Assert.Equal(0, waiting.NewPaperFills);
        Assert.Equal(1, waiting.Snapshot.AwaitingNextOpen);

        var once = CrossSeries().Take(22).ToArray();
        var completed = ResearchSession.Run(Intake(once), Config(), paper, research);
        var replay = ResearchSession.Run(Intake(once), Config(), paper, research);

        Assert.Equal(1, completed.NewPaperFills);
        Assert.Equal(101m, Assert.Single(completed.Snapshot.Fills).ExecutionPrice);
        Assert.True(replay.Replayed);
        Assert.Equal(0, replay.NewPaperFills);
        Assert.Equal(1, paper.Count);
    }

    [Fact]
    public void Run_guard_stops_an_oversized_collect()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ResearchRunGuard.Ensure(501, 500, 1, 1, TimeSpan.Zero, TimeSpan.FromSeconds(30)));
        Assert.Throws<InvalidOperationException>(() =>
            ResearchRunGuard.Ensure(1, 500, 2, 1, TimeSpan.Zero, TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void Research_host_does_not_reference_exchange_adapters_or_order_paths()
    {
        var names = typeof(ResearchHostProgram).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name!).ToArray();
        var root = FindPackRoot();
        var files = Directory.GetFiles(Path.Combine(root, "KitPlatform.Packs.PersonalIntelligence.ResearchHost"), "*.cs")
            .Concat(Directory.GetFiles(Path.Combine(root, "KitPlatform.Packs.PersonalIntelligence.Application", "Trading"), "Research*.cs"))
            .Append(Path.Combine(root, "KitPlatform.Packs.PersonalIntelligence.Application", "Trading", "SmaCrossStrategy.cs"));
        var source = string.Join('\n', files.Select(File.ReadAllText));

        Assert.DoesNotContain(names, name =>
            name.Contains("Infrastructure", StringComparison.Ordinal)
            || name.Contains("PersonalIntelligence.Api", StringComparison.Ordinal)
            || name.Contains("AspNetCore", StringComparison.Ordinal)
            || name.Contains("Exchange", StringComparison.Ordinal));
        Assert.DoesNotContain("/api/v3/order", source, StringComparison.Ordinal);
        Assert.DoesNotContain("testnet.binance", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api-testnet", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IExchangeAdapter", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExchangeBackfill", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpListener", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WebApplication", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Research_store_refuses_the_production_database_and_environment()
    {
        var database = Assert.Throws<InvalidOperationException>(() =>
            new SqlResearchStore("Host=127.0.0.1;Port=1;Database=novixa_prod;Username=research;Password=not-used"));
        Assert.Contains("novixa_prod", database.Message, StringComparison.Ordinal);
        var collector = Assert.Throws<InvalidOperationException>(() =>
            new SqlResearchStore("Host=127.0.0.1;Port=1;Database=trading_research;Username=research;Password=not-used"));
        Assert.Contains("trading_research", collector.Message, StringComparison.Ordinal);
        var other = Assert.Throws<InvalidOperationException>(() =>
            new SqlResearchStore("Host=127.0.0.1;Port=1;Database=kit_other;Username=research;Password=not-used"));
        Assert.Contains("kit_other", other.Message, StringComparison.OrdinalIgnoreCase);
        var accepted = Record.Exception(() =>
            new SqlResearchStore("Host=127.0.0.1;Port=1;Database=TRADING_AGENT;Username=research;Password=not-used"));
        Assert.NotNull(accepted);
        Assert.IsNotType<InvalidOperationException>(accepted);

        var directory = Directory.CreateTempSubdirectory("trading-research-v1-");
        try
        {
            var code = await ResearchHostProgram.RunAsync(
            [
                "--summary", Path.Combine(directory.FullName, "api.env"),
                "--health", Path.Combine(directory.FullName, "health.json"),
                "--state", Path.Combine(directory.FullName, "state.json"),
                "--fixture", Path.Combine(directory.FullName, "candles.json"),
                "--max-notional", "1000",
                "--max-leverage", "2",
                "--min-confidence", "0.5",
                "--session-notional", "1000",
            ]);
            Assert.Equal(4, code);
            Assert.False(File.Exists(Path.Combine(directory.FullName, "api.env")));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public async Task Host_fixture_replays_and_kill_file_stops_before_collect()
    {
        var directory = Directory.CreateTempSubdirectory("trading-research-v1-");
        try
        {
            var fixture = Path.Combine(directory.FullName, "candles.json");
            var summary = Path.Combine(directory.FullName, "summary.json");
            var health = Path.Combine(directory.FullName, "health.json");
            var state = Path.Combine(directory.FullName, "state.json");
            File.WriteAllText(fixture, FixtureJson());
            var args = new[]
            {
                "--fixture", fixture,
                "--summary", summary,
                "--health", health,
                "--state", state,
                "--symbol", "BTCUSDT",
                "--as-of", "2026-01-04T00:00:00Z",
                "--max-stale-hours", "96",
                "--quantity", "1",
                "--confidence", "0.9",
                "--starting-equity", "10000",
                "--slippage-bps", "100",
                "--fee-rate", "0.01",
                "--max-notional", "100000",
                "--max-leverage", "2",
                "--min-confidence", "0.5",
                "--session-notional", "100000",
            };

            Assert.Equal(0, await ResearchHostProgram.RunAsync(args));
            Assert.Equal(0, await ResearchHostProgram.RunAsync(args));
            var second = File.ReadAllText(summary);
            Assert.Contains("\"Replayed\": true", second, StringComparison.Ordinal);
            Assert.Contains("\"NewPaperFills\": 0", second, StringComparison.Ordinal);
            Assert.Contains("\"PnlStatus\": \"realized\"", second, StringComparison.Ordinal);
            Assert.Contains("\"NetRealizedPnl\": -4", second, StringComparison.Ordinal);

            File.WriteAllText(Path.Combine(directory.FullName, "stop.flag"), "stop");
            var killed = await ResearchHostProgram.RunAsync(args.Concat(["--kill-file", Path.Combine(directory.FullName, "stop.flag")]).ToArray());
            Assert.Equal(2, killed);
            Assert.Contains("\"Status\": \"KILLED\"", File.ReadAllText(summary), StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    private static ResearchConfig Config(decimal confidence = 0.9m, bool live = false)
        => new(1m, confidence, 10_000m, new PaperSessionConfig(100m, 0.01m, 0m, 1m, new RiskLimits(100_000m, 2m, 0.5m), 1_000_000m, live));

    private static CandleIntake Intake(IReadOnlyList<ResearchCandle> candles)
        => ResearchCandles.Classify(candles, candles[^1].CloseTime, TimeSpan.FromDays(10));

    private static List<ResearchCandle> CrossSeries()
    {
        var bars = new List<ResearchCandle>();
        for (var hour = 0; hour < 30; hour++)
        {
            var close = hour == 20 ? 200m : hour > 20 ? 1m : 100m;
            bars.Add(Bar(hour, 100m, close));
        }

        return bars;
    }

    private static ResearchCandle Bar(int hour, decimal open, decimal close)
    {
        var openTime = DateTimeOffset.Parse("2026-01-01T00:00:00Z").AddHours(hour);
        return new ResearchCandle(
            "fixture",
            "BTCUSDT",
            "1h",
            openTime,
            openTime.AddHours(1).AddMilliseconds(-1),
            open,
            Math.Max(open, close),
            Math.Min(open, close),
            close,
            $"{hour}:{open}:{close}");
    }

    private static string FixtureJson()
    {
        var rows = new List<string>();
        for (var hour = 0; hour < 30; hour++)
        {
            var close = hour == 20 ? "200" : hour > 20 ? "1" : "100";
            var openMs = DateTimeOffset.Parse("2026-01-01T00:00:00Z").AddHours(hour).ToUnixTimeMilliseconds();
            var high = string.Compare(close, "100", StringComparison.Ordinal) > 0 ? close : "100";
            var low = string.Compare(close, "100", StringComparison.Ordinal) < 0 ? close : "100";
            rows.Add(Kline(openMs, "100", high, low, close));
        }

        return "[" + string.Join(',', rows) + "]";
    }

    private static string Kline(long openMs, string open, string high, string low, string close)
        => $"[{openMs},\"{open}\",\"{high}\",\"{low}\",\"{close}\",\"0\",{openMs + 3_600_000 - 1},\"0\",0,\"0\",\"0\",\"0\"]";

    private static string FindPackRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Packs", "PersonalIntelligence");
            if (Directory.Exists(Path.Combine(candidate, "KitPlatform.Packs.PersonalIntelligence.ResearchHost")))
                return candidate;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the Personal Intelligence pack from the test output directory.");
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly string _body;

        public RecordingHandler(string body) => _body = body;

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (!string.Equals(request.Method.Method, "GET", StringComparison.Ordinal)
                || !string.Equals(request.RequestUri?.AbsolutePath, "/api/v3/klines", StringComparison.Ordinal))
                throw new InvalidOperationException("The research client attempted a non-kline request.");

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }
}
