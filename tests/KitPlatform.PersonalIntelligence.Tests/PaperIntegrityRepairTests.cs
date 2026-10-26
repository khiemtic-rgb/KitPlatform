using KitPlatform.Packs.PersonalIntelligence.Trading;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class PaperIntegrityRepairTests
{
    [Fact]
    public void Buy_then_sell_closes_and_realizes_fee_and_slippage()
    {
        var report = PaperSession.Run(Tape("""
            sma-cross-v1:BTCUSDT:1h:1:BUY,2026-01-01T00:00:00Z,BTCUSDT,BUY,0.001,100,0.80
            sma-cross-v1:BTCUSDT:1h:2:SELL,2026-01-01T01:00:00Z,BTCUSDT,SELL,0.001,100,0.80
            """), LiveConfig(), new MemoryPaperOperationLog());
        var buy = report.NewFills[0];
        var sell = report.NewFills[1];

        Assert.Equal(100.1m, buy.ExecutionPrice);
        Assert.Equal(0.0001001m, buy.Fee);
        Assert.Equal(0.0001m, buy.SlippageQuote);
        Assert.Equal(-0.1002001m, buy.NetCash);
        Assert.Equal(99.9m, sell.ExecutionPrice);
        Assert.Equal(0.0000999m, sell.Fee);
        Assert.Equal(0.0001m, sell.SlippageQuote);
        Assert.Equal(0.0998001m, sell.NetCash);
        Assert.Equal(-0.0004m, report.RealizedPnl);
        Assert.Equal(PaperSession.Realized, report.PnlStatus);

        var chain = PaperIntegrity.Evaluate(Signals(report), Decisions(report), Stored(report));
        Assert.Equal("opened", chain.Fills[0].PositionEffect);
        Assert.Equal("closed", chain.Fills[1].PositionEffect);
        Assert.True(chain.FullyClosed);
        Assert.Equal(-0.0004m, chain.RealizedPnl);
        Assert.Equal(ResearchSession.CashDrawdown, chain.DrawdownStatus);
        Assert.Equal(SmaCrossStrategy.Id, chain.Fills[0].StrategyId);
        Assert.Equal(SmaCrossStrategy.Version, chain.Fills[0].StrategyVersion);
    }

    [Fact]
    public void Awaiting_sell_is_not_a_closed_trade()
    {
        var waiting = new StoredSignal("sma-cross-v1:BTCUSDT:1h:2:SELL", "BTCUSDT", "SELL", DateTimeOffset.Parse("2026-01-01T02:00:00Z"), null, null, SmaCrossStrategy.AwaitingNextOpen, SmaCrossStrategy.Id, SmaCrossStrategy.Version, "1h", DateTimeOffset.Parse("2026-01-01T01:00:00Z"));
        var empty = PaperIntegrity.Evaluate([waiting], [], []);
        Assert.False(empty.FullyClosed);
        Assert.Null(empty.RealizedPnl);
        Assert.Equal(PaperSession.NoClosedPosition, empty.PnlStatus);

        var fill = new StoredFill("sma-cross-v1:BTCUSDT:1h:2:SELL", "SELL", 100m, 99.9m, 0.001m, 0.0000999m, 0.0001m, 0.0998001m, waiting.SignalKey);
        var decision = new ResearchRiskDecision(waiting.SignalKey, true, "", waiting.SignalKey);
        var attached = PaperIntegrity.Evaluate([waiting], [decision], [fill]);
        Assert.Equal(PaperIntegrity.Awaiting, Assert.Single(attached.Fills).LinkStatus);
        Assert.Equal("not_a_fill", Assert.Single(attached.Fills).PositionEffect);
        Assert.False(attached.FullyClosed);
        Assert.Null(attached.RealizedPnl);
    }

    [Fact]
    public void Fill_without_signal_and_risk_link_is_rejected()
    {
        var fill = new StoredFill("orphan", "SELL", 100m, 99.9m, 0.001m, 0.0000999m, 0.0001m, 0.0998001m, null);
        var chain = PaperIntegrity.Evaluate([], [], [fill]);

        var row = Assert.Single(chain.Fills);
        Assert.Equal(PaperIntegrity.Unlinked, row.LinkStatus);
        Assert.Equal("rejected", row.PositionEffect);
        Assert.False(chain.FullyClosed);
        Assert.Null(chain.RealizedPnl);
        Assert.Equal(PaperSession.NoClosedPosition, chain.PnlStatus);
        Assert.Equal(ResearchSession.DrawdownUnavailable, chain.DrawdownStatus);
    }

    [Fact]
    public void Replay_does_not_duplicate_fills_or_recompute_pnl()
    {
        var paper = new MemoryPaperOperationLog();
        var research = new MemoryResearchLog();
        var runs = new MemoryResearchRunLog();
        var intake = Intake(CrossSeries());
        var first = ResearchSession.Run(intake, SessionConfig(), paper, research, runs: runs);
        var second = ResearchSession.Run(intake, SessionConfig(), paper, research, runs: runs);

        Assert.Equal(2, first.NewPaperFills);
        Assert.Equal(-4m, first.Snapshot.NetRealizedPnl);
        Assert.True(second.Replayed);
        Assert.Equal(0, second.NewPaperFills);
        Assert.Equal(first.Snapshot.Fills.Count, second.Snapshot.Fills.Count);
        Assert.Equal(2, paper.Count);
        Assert.Equal(ResearchRunRecord.Completed, runs.Runs[0].Status);
        Assert.True(runs.Runs[0].PnlRecomputed);
        Assert.Equal(ResearchRunRecord.ReplayedStatus, runs.Runs[1].Status);
        Assert.True(runs.Runs[1].Replayed);
        Assert.False(runs.Runs[1].PnlRecomputed);
        Assert.Equal(0, runs.Runs[1].Fills);
    }

    [Fact]
    public void Partial_close_stays_open_and_does_not_publish_realized_pnl()
    {
        var report = PaperSession.Run(Tape("""
            b1,2026-01-01T00:00:00Z,BTCUSDT,BUY,1,100,0.9
            s1,2026-01-01T01:00:00Z,BTCUSDT,SELL,0.4,100,0.9
            """), WideConfig(), new MemoryPaperOperationLog());

        Assert.Equal(PaperSession.PartialClose, report.PnlStatus);
        Assert.Null(report.RealizedPnl);
        Assert.Equal(2, report.PaperFilled);

        var opened = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var ledger = ResearchTradeLedger.Build(report.NewFills.Select((fill, index) => new LedgerFill(
            fill.OperationKey,
            fill.Side,
            fill.ExecutionPrice,
            fill.Quantity,
            fill.Fee,
            fill.SlippageQuote,
            fill.NetCash,
            fill.Symbol,
            "1h",
            SmaCrossStrategy.Id,
            opened.AddHours(index))).ToArray());
        Assert.Contains(ledger.Trades, trade => trade.Status == ResearchTradeLedger.Partial);
        Assert.DoesNotContain(ledger.Trades, trade => trade.Status == ResearchTradeLedger.Closed);
        var position = Assert.Single(ledger.Positions);
        Assert.Equal(0.6m, position.Quantity);
    }

    [Fact]
    public void Open_position_has_no_pnl_and_no_drawdown()
    {
        var report = ResearchSession.Run(Intake(CrossSeries().Take(22).ToArray()), SessionConfig(), new MemoryPaperOperationLog(), new MemoryResearchLog());

        Assert.Equal(1, report.NewPaperFills);
        Assert.Equal("BUY", Assert.Single(report.Snapshot.Fills).Side);
        Assert.Null(report.Snapshot.NetRealizedPnl);
        Assert.Equal(PaperSession.NoClosedPosition, report.Snapshot.PnlStatus);
        Assert.Null(report.Snapshot.CashMaxDrawdown);
        Assert.Equal(ResearchSession.DrawdownUnavailable, report.Snapshot.DrawdownStatus);
    }

    [Fact]
    public void New_signal_stores_strategy_id_and_version()
    {
        var signal = Assert.Single(SmaCrossStrategy.Signals(CrossSeries().Take(22).ToArray()));

        Assert.Equal(SmaCrossStrategy.Id, signal.StrategyId);
        Assert.Equal(SmaCrossStrategy.Version, signal.StrategyVersion);
        Assert.Equal("BTCUSDT", signal.Symbol);
        Assert.Equal("1h", signal.Timeframe);
        Assert.Equal("BUY", signal.Side);
        Assert.NotNull(signal.CandleOpenTime);
        Assert.StartsWith("sma-cross-v1:BTCUSDT:1h:", signal.SignalKey, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_history_records_completion_and_failure()
    {
        var runs = new MemoryResearchRunLog();
        var completed = ResearchSession.Run(Intake(CrossSeries().Take(22).ToArray()), SessionConfig(), new MemoryPaperOperationLog(), new MemoryResearchLog(), runs: runs);
        Assert.Equal(ResearchSession.Completed, completed.Status);
        Assert.Equal(ResearchRunRecord.Completed, Assert.Single(runs.Runs).Status);
        Assert.Null(runs.Runs[0].Error);
        Assert.True(runs.Runs[0].Signals >= 1);
        Assert.Equal(1, runs.Runs[0].Fills);

        var failed = new MemoryResearchRunLog();
        var intake = new CandleIntake([], 0, 0, 0, 0, []);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ResearchSession.Run(intake, new ResearchConfig(0m, 0.8m, 10_000m, WideConfig()), new MemoryPaperOperationLog(), new MemoryResearchLog(), runs: failed));
        var failure = Assert.Single(failed.Runs);
        Assert.Equal(ResearchRunRecord.Failed, failure.Status);
        Assert.False(string.IsNullOrWhiteSpace(failure.Error));
        Assert.False(failure.PnlRecomputed);
        Assert.NotEqual(failure.StartedAt, default);
        Assert.True(failure.EndedAt >= failure.StartedAt);
    }

    [Fact]
    public void Historical_rows_without_version_or_link_stay_unidentified()
    {
        var historical = new StoredSignal("sma-cross-v1:BTCUSDT:1h:1:BUY", "BTCUSDT", "BUY", DateTimeOffset.Parse("2026-01-01T01:00:00Z"), null, null, SmaCrossStrategy.AwaitingNextOpen);
        var incoming = historical with { Status = SmaCrossStrategy.Ready, FillPrice = 100m, StrategyId = SmaCrossStrategy.Id, StrategyVersion = SmaCrossStrategy.Version };
        var promoted = PaperIntegrity.Promote(historical, incoming);

        Assert.Equal(SmaCrossStrategy.Ready, promoted.Status);
        Assert.Equal(100m, promoted.FillPrice);
        Assert.Null(promoted.StrategyId);
        Assert.Null(promoted.StrategyVersion);
        Assert.Equal(PaperIntegrity.Unidentified, PaperIntegrity.StrategyLabel(promoted.StrategyId, promoted.StrategyVersion));

        var fill = new StoredFill(historical.SignalKey, "BUY", 100m, 100.1m, 0.001m, 0.0001001m, 0.0001m, -0.1002001m, null);
        var decision = new ResearchRiskDecision(historical.SignalKey, true, "");
        var chain = PaperIntegrity.Evaluate([historical], [decision], [fill]);
        Assert.Equal(PaperIntegrity.Unlinked, Assert.Single(chain.Fills).LinkStatus);
        Assert.Null(chain.RealizedPnl);
    }

    [Fact]
    public void Migration_is_present_and_not_on_the_production_manifest()
    {
        var root = FindRepoRoot();
        var migration = File.ReadAllText(Path.Combine(root, "migrations", "398_research_trading_paper_integrity_v1.sql"));
        var researchManifest = File.ReadAllText(Path.Combine(root, "deploy", "ubuntu", "migration-files.trading-research.txt"));
        var productionManifest = File.ReadAllText(Path.Combine(root, "deploy", "ubuntu", "migration-files.prod.txt"));

        Assert.Contains("research_trading.run_history", migration, StringComparison.Ordinal);
        Assert.Contains("strategy_id", migration, StringComparison.Ordinal);
        Assert.Contains("current_database() IN ('novixa_prod', 'trading_research')", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE research_trading.signal", migration, StringComparison.Ordinal);
        Assert.Contains("398_research_trading_paper_integrity_v1.sql", researchManifest, StringComparison.Ordinal);
        Assert.DoesNotContain("398_research_trading_paper_integrity_v1.sql", productionManifest, StringComparison.Ordinal);
        Assert.Contains("awaiting_next_open", ResearchTradingSql.InsertSignal, StringComparison.Ordinal);
        Assert.DoesNotContain("strategy_id = EXCLUDED.strategy_id", ResearchTradingSql.InsertSignal, StringComparison.Ordinal);
        Assert.Contains("signal_key", ResearchTradingSql.InsertFill, StringComparison.Ordinal);
        Assert.Contains("ON CONFLICT (operation_key) DO NOTHING", ResearchTradingSql.InsertFill, StringComparison.Ordinal);
    }

    private static IReadOnlyList<StoredSignal> Signals(PaperSessionReport report)
        => report.NewFills.Select(fill => new StoredSignal(
            fill.OperationKey,
            fill.Symbol,
            fill.Side,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-01-01T01:00:00Z"),
            fill.TapePrice,
            SmaCrossStrategy.Ready,
            SmaCrossStrategy.Id,
            SmaCrossStrategy.Version,
            "1h",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"))).ToArray();

    private static IReadOnlyList<ResearchRiskDecision> Decisions(PaperSessionReport report)
        => report.NewFills.Select(fill => new ResearchRiskDecision(fill.OperationKey, true, "", fill.OperationKey)).ToArray();

    private static IReadOnlyList<StoredFill> Stored(PaperSessionReport report)
        => report.NewFills.Select(fill => new StoredFill(
            fill.OperationKey,
            fill.Side,
            fill.TapePrice,
            fill.ExecutionPrice,
            fill.Quantity,
            fill.Fee,
            fill.SlippageQuote,
            fill.NetCash,
            fill.OperationKey)).ToArray();

    private static PaperSessionConfig LiveConfig()
        => new(10m, 0.001m, 0m, 1m, new RiskLimits(1_000m, 1m, 0.5m), 5_000m, false);

    private static PaperSessionConfig WideConfig()
        => new(100m, 0.01m, 0m, 1m, new RiskLimits(100_000m, 2m, 0.5m), 1_000_000m, false);

    private static ResearchConfig SessionConfig()
        => new(1m, 0.9m, 10_000m, WideConfig());

    private static IReadOnlyList<PaperTapeRow> Tape(string csv)
        => PaperTape.Read(PaperTape.Header + "\n" + csv);

    private static CandleIntake Intake(IReadOnlyList<ResearchCandle> candles)
        => ResearchCandles.Classify(candles, candles[^1].CloseTime, TimeSpan.FromDays(10));

    private static List<ResearchCandle> CrossSeries()
    {
        var bars = new List<ResearchCandle>();
        for (var hour = 0; hour < 30; hour++)
        {
            var close = hour == 20 ? 200m : hour > 20 ? 1m : 100m;
            var openTime = DateTimeOffset.Parse("2026-01-01T00:00:00Z").AddHours(hour);
            bars.Add(new ResearchCandle("fixture", "BTCUSDT", "1h", openTime, openTime.AddHours(1).AddMilliseconds(-1), 100m, Math.Max(100m, close), Math.Min(100m, close), close, $"{hour}:{close}"));
        }

        return bars;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "migrations", "398_research_trading_paper_integrity_v1.sql")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the research repo root from the test output directory.");
    }
}
