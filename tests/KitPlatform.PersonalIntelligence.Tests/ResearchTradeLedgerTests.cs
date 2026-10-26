using Xunit;
using KitPlatform.Packs.PersonalIntelligence.ResearchDashboard;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class ResearchTradeLedgerTests
{
    private static readonly DateTimeOffset Opened = new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ClosedAt = new(2026, 1, 2, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Buy_opens_a_position_and_does_not_create_a_closed_trade()
    {
        var ledger = ResearchTradeLedger.Build([Buy("entry")]);

        Assert.DoesNotContain(ledger.Trades, row => row.Status == ResearchTradeLedger.Closed);
        var open = Assert.Single(ledger.Trades);
        Assert.Equal(ResearchTradeLedger.Open, open.Status);
        Assert.Equal(ResearchTradeLedger.Long, open.Direction);
        Assert.False(open.NetKnown);
        Assert.Null(open.NetPnl);
        Assert.Equal(PaperSession.NoClosedPosition, open.NetStatus);
        var position = Assert.Single(ledger.Positions);
        Assert.Equal(0.001m, position.Quantity);
        Assert.False(position.UnrealizedKnown);
        Assert.Null(position.UnrealizedPnl);
        Assert.Equal(ResearchTradeLedger.Unmarked, position.UnrealizedStatus);
    }

    [Fact]
    public void Buy_then_sell_is_one_closed_trade_and_matches_the_session()
    {
        var paper = Session(BuyRow("entry", Opened), SellRow("exit", ClosedAt));
        var ledger = ResearchTradeLedger.Build(paper.NewFills.Select(fill => new LedgerFill(
            fill.OperationKey,
            fill.Side,
            fill.ExecutionPrice,
            fill.Quantity,
            fill.Fee,
            fill.SlippageQuote,
            fill.NetCash,
            "BTCUSDT",
            "1h",
            SmaCrossStrategy.Id,
            fill.OperationKey == "entry" ? Opened : ClosedAt)).ToArray());

        Assert.Equal(2, paper.NewFills.Count);
        var trade = Assert.Single(ledger.Trades);
        Assert.Equal(ResearchTradeLedger.Closed, trade.Status);
        Assert.Equal(ResearchTradeLedger.Long, trade.Direction);
        Assert.Equal(["entry", "exit"], trade.FillKeys);
        Assert.True(trade.GrossKnown);
        Assert.True(trade.FeesKnown);
        Assert.True(trade.SlippageKnown);
        Assert.True(trade.NetKnown);
        Assert.Equal(paper.RealizedPnl, trade.NetPnl);
        Assert.Equal(trade.GrossPnl - trade.Fees, trade.NetPnl);
        Assert.Equal(paper.Fees, trade.Fees);
        Assert.Equal(paper.SlippageQuote, trade.Slippage);
        Assert.Equal(ResearchTradeLedger.CloseReasonSmaCrossBelow, trade.CloseReason);
        Assert.Empty(ledger.Positions);
        Assert.NotEqual(0m, trade.NetPnl);
    }

    [Fact]
    public void Open_position_realized_pnl_is_not_zero()
    {
        var paper = Session(BuyRow("entry", Opened));
        var ledger = ResearchTradeLedger.Build([Buy("entry")]);

        Assert.Equal(PaperSession.NoClosedPosition, paper.PnlStatus);
        Assert.Null(paper.RealizedPnl);
        Assert.False(Assert.Single(ledger.Trades).NetKnown);
        Assert.NotEqual(0m, paper.Fees);
    }

    [Fact]
    public void Missing_signal_time_stays_unmatched_and_does_not_become_zero_pnl()
    {
        var document = ResearchReadModel.Build(
            new ResearchReadBundle(
                0, [], [], [],
                [new FillInput("orphan", "BUY", 100m, 100.1m, 0.001m, 0.0001m, 0.0001m, -0.1002m)],
                [],
                [new SessionInput(Opened, "COMPLETED", null, "unavailable")]),
            Opened,
            "fixture");

        var trade = Assert.Single(document.Trades);
        Assert.Equal(ResearchTradeLedger.Unmatched, trade.Status);
        Assert.False(trade.NetPnl.Known);
        Assert.Null(trade.NetPnl.Amount);
        Assert.NotEqual("0", trade.NetPnl.Status);
        Assert.Empty(document.Positions);
    }

    [Fact]
    public void Partial_sell_closes_one_slice_and_leaves_the_position_open()
    {
        var paper = Session(
            new PaperTapeRow("entry", Opened, "BTCUSDT", "BUY", 0.002m, 100m, 0.8m),
            new PaperTapeRow("exit", ClosedAt, "BTCUSDT", "SELL", 0.001m, 100m, 0.8m));
        var ledger = ResearchTradeLedger.Build(paper.NewFills.Select(fill => new LedgerFill(
            fill.OperationKey, fill.Side, fill.ExecutionPrice, fill.Quantity, fill.Fee, fill.SlippageQuote, fill.NetCash,
            "BTCUSDT", "1h", SmaCrossStrategy.Id, fill.OperationKey == "entry" ? Opened : ClosedAt)).ToArray());

        var slice = Assert.Single(ledger.Trades, row => row.Status == ResearchTradeLedger.Partial);
        var open = Assert.Single(ledger.Trades, row => row.Status == ResearchTradeLedger.Open);
        Assert.Equal(PaperSession.PartialClose, paper.PnlStatus);
        Assert.Null(paper.RealizedPnl);
        Assert.True(slice.NetKnown);
        Assert.NotEqual(ResearchTradeLedger.Closed, slice.Status);
        Assert.Equal(0.001m, slice.Quantity);
        Assert.Equal(0.001m, open.Quantity);
        Assert.False(open.NetKnown);
        Assert.Equal(0.001m, Assert.Single(ledger.Positions).Quantity);
    }

    [Fact]
    public void Oversized_sell_does_not_invent_a_closed_trade()
    {
        var ledger = ResearchTradeLedger.Build([Buy("entry"), Sell("exit", ClosedAt, 1m)]);

        Assert.True(ledger.Incomplete);
        Assert.DoesNotContain(ledger.Trades, row => row.Status == ResearchTradeLedger.Closed);
        Assert.Contains(ledger.Trades, row => row.TradeId == "unmatched:exit" && !row.NetKnown);
        Assert.Equal(0.001m, Assert.Single(ledger.Positions).Quantity);
    }

    [Fact]
    public async Task Replay_keeps_the_same_closed_trade_and_a_different_parameter_key_is_isolated()
    {
        var root = Directory.CreateTempSubdirectory("research-ledger-");
        try
        {
            var first = await ResearchDesk.ExecuteAsync(root.FullName, new ResearchDeskRequest { DataSource = "fixture", Quantity = 0.001m });
            var replay = await ResearchDesk.ExecuteAsync(root.FullName, new ResearchDeskRequest { DataSource = "fixture", Quantity = 0.001m });
            var other = await ResearchDesk.ExecuteAsync(root.FullName, new ResearchDeskRequest { DataSource = "fixture", Quantity = 0.002m });

            var firstTrade = Assert.Single(first.Document!.Trades, row => row.Status == ResearchTradeLedger.Closed);
            var replayTrade = Assert.Single(replay.Document!.Trades, row => row.Status == ResearchTradeLedger.Closed);
            var otherTrade = Assert.Single(other.Document!.Trades, row => row.Status == ResearchTradeLedger.Closed);
            Assert.Equal(firstTrade.TradeId, replayTrade.TradeId);
            Assert.Equal(firstTrade.NetPnl.Amount, replayTrade.NetPnl.Amount);
            Assert.Equal(first.Document.Overview.RealizedPnl.Amount, firstTrade.NetPnl.Amount);
            Assert.Equal(0, replay.NewFills);
            Assert.Empty(first.Document.Positions);
            Assert.NotEqual(first.ParameterKey, other.ParameterKey);
            Assert.NotEqual(firstTrade.NetPnl.Amount, otherTrade.NetPnl.Amount);
            Assert.True(File.Exists(Path.Combine(root.FullName, "work", first.ParameterKey, "state.json")));
            Assert.True(File.Exists(Path.Combine(root.FullName, "work", other.ParameterKey, "state.json")));
            Assert.Equal(2, first.Document.Fills.Count);
            Assert.Single(first.Document.Trades, row => row.Status == ResearchTradeLedger.Closed);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void Ledger_source_does_not_read_tenant_trades()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Packs", "PersonalIntelligence",
            "KitPlatform.Packs.PersonalIntelligence.Application", "Trading", "ResearchTradeLedger.cs"));
        var page = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "client", "admin", "src", "modules", "second-brain", "TradingResearchDeskPage.tsx"));
        var ledger = File.ReadAllText(path);
        var ui = File.ReadAllText(page);
        Assert.DoesNotContain("pack_personal_intelligence", ledger);
        Assert.DoesNotContain("trading_order", ledger);
        Assert.DoesNotContain("from './trading.api'", ui);
        Assert.Contains("Trade Ledger", ui);
        Assert.Contains("Chưa có giao dịch đã ghép.", ui);
        Assert.Contains("Đang tải nhiệm vụ research.", ui);
        Assert.Contains("Không kết nối được Trading Research desk", ui);
    }

    private static PaperSessionReport Session(params PaperTapeRow[] rows)
        => PaperSession.Run(rows, new PaperSessionConfig(10m, 0.001m, 0m, 1m, new RiskLimits(1000m, 1m, 0.5m), 5000m, false), new MemoryPaperOperationLog());

    private static PaperTapeRow BuyRow(string key, DateTimeOffset time)
        => new(key, time, "BTCUSDT", "BUY", 0.001m, 100m, 0.8m);

    private static PaperTapeRow SellRow(string key, DateTimeOffset time)
        => new(key, time, "BTCUSDT", "SELL", 0.001m, 100m, 0.8m);

    private static LedgerFill Buy(string key, DateTimeOffset? time = null)
        => Fill(key, "BUY", 100.1m, 0.001m, 0.00010010m, 0.000100m, -0.10020010m, time ?? Opened);

    private static LedgerFill Sell(string key, DateTimeOffset time, decimal quantity = 0.001m)
    {
        var execution = 99.9m;
        var notional = execution * quantity;
        var fee = decimal.Round(notional * 0.001m, 8, MidpointRounding.AwayFromZero);
        return Fill(key, "SELL", execution, quantity, fee, Math.Abs(execution - 100m) * quantity, notional - fee, time);
    }

    private static LedgerFill Fill(string key, string side, decimal execution, decimal quantity, decimal fee, decimal slippage, decimal netCash, DateTimeOffset time)
        => new(key, side, execution, quantity, fee, slippage, netCash, "BTCUSDT", "1h", SmaCrossStrategy.Id, time);
}
