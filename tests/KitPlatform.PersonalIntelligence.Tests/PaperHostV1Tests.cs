using KitPlatform.Packs.PersonalIntelligence.PaperHost;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public class PaperHostV1Tests
{
    private static string FindPackRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Packs", "PersonalIntelligence");
            if (Directory.Exists(Path.Combine(candidate, "KitPlatform.Packs.PersonalIntelligence.PaperHost")))
                return candidate;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the Personal Intelligence pack from the test output directory.");
    }

    private static PaperSessionConfig Config(bool live = false, decimal sessionNotional = 1_000m, decimal minConfidence = 0.5m)
        => new(100m, 0.01m, 0m, 1m, new RiskLimits(100_000m, 2m, minConfidence), sessionNotional, live);

    private static IReadOnlyList<PaperTapeRow> Tape(string body)
        => PaperTape.Read("operation_key,recorded_at,symbol,side,quantity,price,confidence\n" + body);

    [Fact]
    public void Buy_fill_adds_slippage_and_fee_to_cost()
    {
        var report = PaperSession.Run(Tape("b1,2026-01-01T00:00:00Z,BTCUSDT,BUY,1,100,0.9\n"), Config(), new MemoryPaperOperationLog());
        var fill = Assert.Single(report.NewFills);

        Assert.Equal(101m, fill.ExecutionPrice);
        Assert.Equal(1.01m, fill.Fee);
        Assert.Equal(1m, fill.SlippageQuote);
        Assert.Equal(-102.01m, fill.NetCash);
        Assert.Equal(PaperSession.NoClosedPosition, report.PnlStatus);
        Assert.Null(report.RealizedPnl);
    }

    [Fact]
    public void Sell_fill_subtracts_slippage_and_fee_from_proceeds()
    {
        var report = PaperSession.Run(Tape("s1,2026-01-01T00:00:00Z,BTCUSDT,SELL,1,100,0.9\n"), Config(), new MemoryPaperOperationLog());
        var fill = Assert.Single(report.NewFills);

        Assert.Equal(99m, fill.ExecutionPrice);
        Assert.Equal(0.99m, fill.Fee);
        Assert.Equal(1m, fill.SlippageQuote);
        Assert.Equal(98.01m, fill.NetCash);
        Assert.Equal(PaperSession.PnlUnavailable, report.PnlStatus);
        Assert.Null(report.RealizedPnl);
    }

    [Fact]
    public void Round_trip_reports_realized_pnl_from_entry_cost()
    {
        var tape = Tape("""
            b1,2026-01-01T00:00:00Z,BTCUSDT,BUY,1,100,0.9
            s1,2026-01-01T00:01:00Z,BTCUSDT,SELL,1,100,0.9
            """);
        var report = PaperSession.Run(tape, Config(), new MemoryPaperOperationLog());

        Assert.Equal(2, report.PaperFilled);
        Assert.Equal(2.00m, report.Fees);
        Assert.Equal(2m, report.SlippageQuote);
        Assert.Equal(-4.00m, report.RealizedPnl);
        Assert.Equal(PaperSession.Realized, report.PnlStatus);
    }

    [Fact]
    public void Risk_veto_does_not_create_a_fill()
    {
        var log = new MemoryPaperOperationLog();
        var report = PaperSession.Run(Tape("b1,2026-01-01T00:00:00Z,BTCUSDT,BUY,1,100,0.2\n"), Config(), log);

        Assert.Equal(0, report.PaperFilled);
        Assert.Equal(1, report.RiskRefused);
        Assert.Empty(report.NewFills);
        Assert.Equal("risk_refused", log.Find("b1")!.Status);
        Assert.Contains("Confidence is below the configured minimum.", log.Find("b1")!.Result, StringComparison.Ordinal);
    }

    [Fact]
    public void Session_notional_cap_refuses_the_fill_that_would_exceed_it()
    {
        var tape = Tape("""
            b1,2026-01-01T00:00:00Z,BTCUSDT,BUY,1,100,0.9
            b2,2026-01-01T00:01:00Z,BTCUSDT,BUY,1,100,0.9
            """);
        var report = PaperSession.Run(tape, Config(sessionNotional: 150m), new MemoryPaperOperationLog());

        Assert.Equal(1, report.PaperFilled);
        Assert.Equal(1, report.SessionNotionalRefused);
        Assert.Equal("b1", Assert.Single(report.NewFills).OperationKey);
    }

    [Fact]
    public void Kill_switch_stops_before_the_next_operation()
    {
        var seen = 0;
        var tape = Tape("""
            b1,2026-01-01T00:00:00Z,BTCUSDT,BUY,1,100,0.9
            b2,2026-01-01T00:01:00Z,BTCUSDT,BUY,1,100,0.9
            """);
        var report = PaperSession.Run(tape, Config(), new MemoryPaperOperationLog(), () => ++seen > 1);

        Assert.Equal(PaperSession.Killed, report.Status);
        Assert.Equal(1, report.PaperFilled);
        Assert.Equal(1, report.Unprocessed);
        Assert.Equal(2, report.RecordsRead);
    }

    [Fact]
    public void Same_operation_key_does_not_create_a_second_fill()
    {
        var log = new MemoryPaperOperationLog();
        var tape = Tape("b1,2026-01-01T00:00:00Z,BTCUSDT,BUY,1,100,0.9\n");
        var first = PaperSession.Run(tape, Config(), log);
        var second = PaperSession.Run(tape, Config(), log);

        Assert.Equal(1, first.PaperFilled);
        Assert.Equal(0, second.PaperFilled);
        Assert.Equal(1, second.Replayed);
        Assert.Equal(1, log.Count);
        Assert.Equal(PaperSession.ReplayNotRecomputed, second.PnlStatus);
        Assert.Null(second.RealizedPnl);
    }

    [Fact]
    public void Different_payload_for_the_same_key_is_a_conflict_and_not_a_fill()
    {
        var log = new MemoryPaperOperationLog();
        PaperSession.Run(Tape("b1,2026-01-01T00:00:00Z,BTCUSDT,BUY,1,100,0.9\n"), Config(), log);
        var second = PaperSession.Run(Tape("b1,2026-01-01T00:00:00Z,BTCUSDT,BUY,2,100,0.9\n"), Config(), log);

        Assert.Equal(0, second.PaperFilled);
        Assert.Equal(1, second.Conflicts);
        Assert.Equal(1, log.Count);
    }

    [Fact]
    public void Live_mode_does_not_create_a_fill()
    {
        var log = new MemoryPaperOperationLog();
        var report = PaperSession.Run(Tape("b1,2026-01-01T00:00:00Z,BTCUSDT,BUY,1,100,0.9\n"), Config(live: true), log);

        Assert.Equal(PaperSession.LiveBlocked, report.Status);
        Assert.Equal(0, report.PaperFilled);
        Assert.Equal(1, report.RiskRefused);
        Assert.Contains("Live execution is outside Trading Agent V1.", log.Find("b1")!.Result, StringComparison.Ordinal);
    }

    [Fact]
    public void Tape_rejects_a_record_that_moves_backwards()
    {
        var ex = Assert.Throws<PaperTapeException>(() => Tape("""
            b1,2026-01-02T00:00:00Z,BTCUSDT,BUY,1,100,0.9
            b2,2026-01-01T00:00:00Z,BTCUSDT,BUY,1,100,0.9
            """));
        Assert.Contains("earlier", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Paper_session_assembly_does_not_reference_http_or_the_exchange_adapters()
    {
        var names = typeof(PaperSession).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name!).ToArray();

        Assert.DoesNotContain(names, name =>
            name.Contains("Http", StringComparison.Ordinal)
            || name.Contains("AspNetCore", StringComparison.Ordinal)
            || name.Contains("Infrastructure", StringComparison.Ordinal)
            || name.Contains("Exchange", StringComparison.Ordinal));
    }

    [Fact]
    public void Paper_host_does_not_reference_the_api_infrastructure_or_exchange_adapters()
    {
        var names = typeof(PaperHostProgram).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name!).ToArray();
        var root = FindPackRoot();
        var files = Directory.GetFiles(Path.Combine(root, "KitPlatform.Packs.PersonalIntelligence.PaperHost"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(root, "KitPlatform.Packs.PersonalIntelligence.Application", "Trading"), "Paper*.cs"));
        var source = string.Join('\n', files.Select(File.ReadAllText));

        Assert.Contains("KitPlatform.Packs.PersonalIntelligence.Application", names);
        Assert.DoesNotContain(names, name =>
            name.Contains("Infrastructure", StringComparison.Ordinal)
            || name.Contains("PersonalIntelligence.Api", StringComparison.Ordinal)
            || name.Contains("AspNetCore", StringComparison.Ordinal)
            || name.Contains("Exchange", StringComparison.Ordinal));
        Assert.DoesNotContain("HttpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpListener", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WebApplication", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IExchangeAdapter", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExchangeBackfill", source, StringComparison.Ordinal);
        Assert.DoesNotContain("api.bybit", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api.binance", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("okx.com", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Host_refuses_the_production_database_and_the_production_environment_file()
    {
        var database = Assert.Throws<InvalidOperationException>(() =>
            new SqlPaperOperationLog("Host=127.0.0.1;Port=1;Database=novixa_prod;Username=paper;Password=not-used"));
        Assert.Contains("novixa_prod", database.Message, StringComparison.Ordinal);
        var collector = Assert.Throws<InvalidOperationException>(() =>
            new SqlPaperOperationLog("Host=127.0.0.1;Port=1;Database=TRADING_RESEARCH;Username=paper;Password=not-used"));
        Assert.Contains("trading_research", collector.Message, StringComparison.OrdinalIgnoreCase);
        var other = Assert.Throws<InvalidOperationException>(() =>
            new SqlPaperOperationLog("Host=127.0.0.1;Port=1;Database=kit_other;Username=paper;Password=not-used"));
        Assert.Contains("kit_other", other.Message, StringComparison.OrdinalIgnoreCase);
        var accepted = Record.Exception(() =>
            new SqlPaperOperationLog("Host=127.0.0.1;Port=1;Database=TRADING_AGENT;Username=paper;Password=not-used"));
        Assert.NotNull(accepted);
        Assert.IsNotType<InvalidOperationException>(accepted);

        var directory = Directory.CreateTempSubdirectory("paper-host-v1-");
        try
        {
            var code = PaperHostProgram.Run(
            [
                "--tape", Path.Combine(directory.FullName, "api.env"),
                "--summary", Path.Combine(directory.FullName, "summary.json"),
                "--receipt", Path.Combine(directory.FullName, "receipt.json"),
                "--max-notional", "1000",
                "--max-leverage", "2",
                "--min-confidence", "0.5",
                "--session-notional", "1000",
            ]);
            Assert.Equal(4, code);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void Host_replay_and_kill_file_do_not_open_a_listener()
    {
        var directory = Directory.CreateTempSubdirectory("paper-host-v1-");
        try
        {
            var tape = Path.Combine(directory.FullName, "tape.csv");
            var summary = Path.Combine(directory.FullName, "summary.json");
            var receipt = Path.Combine(directory.FullName, "receipt.json");
            File.WriteAllText(tape, """
                operation_key,recorded_at,symbol,side,quantity,price,confidence
                b1,2026-01-01T00:00:00Z,BTCUSDT,BUY,1,100,0.9
                b2,2026-01-01T00:01:00Z,BTCUSDT,BUY,1,100,0.9
                """);
            var args = new[]
            {
                "--tape", tape,
                "--summary", summary,
                "--receipt", receipt,
                "--slippage-bps", "100",
                "--fee-rate", "0.01",
                "--max-notional", "100000",
                "--max-leverage", "2",
                "--min-confidence", "0.5",
                "--session-notional", "100000",
            };

            Assert.Equal(0, PaperHostProgram.Run(args));
            Assert.Equal(0, PaperHostProgram.Run(args));
            Assert.Contains("\"PaperFilled\": 0", File.ReadAllText(summary), StringComparison.Ordinal);
            Assert.Contains("\"Replayed\": 2", File.ReadAllText(summary), StringComparison.Ordinal);

            File.WriteAllText(Path.Combine(directory.FullName, "stop.flag"), "stop");
            var killed = PaperHostProgram.Run(args.Concat(["--kill-file", Path.Combine(directory.FullName, "stop.flag")]).ToArray());
            Assert.Equal(2, killed);
            Assert.Contains("\"Status\": \"KILLED\"", File.ReadAllText(summary), StringComparison.Ordinal);
            Assert.Contains("\"Unprocessed\": 2", File.ReadAllText(summary), StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
