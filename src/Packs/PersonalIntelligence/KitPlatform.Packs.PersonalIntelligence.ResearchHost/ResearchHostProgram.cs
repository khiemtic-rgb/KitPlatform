using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.ResearchHost;

public static class ResearchHostProgram
{
    public static Task<int> Main(string[] args) => RunAsync(args);

    public static async Task<int> RunAsync(string[] args)
    {
        string? healthPath = null;
        SqlResearchStore? sql = null;
        var started = DateTimeOffset.UtcNow;
        try
        {
            if (args.Length == 0 || args.Contains("--help", StringComparer.Ordinal) || args.Contains("-h", StringComparer.Ordinal))
            {
                Console.WriteLine(Help);
                return args.Length == 0 ? 4 : 0;
            }

            if (args.Contains("--live", StringComparer.Ordinal))
                throw new InvalidOperationException("The research host does not run live orders.");

            var summaryPath = Required(args, "--summary");
            var requestedHealth = Required(args, "--health");
            var statePath = Optional(args, "--state");
            var connectionFile = Optional(args, "--connection-file");
            var fixturePath = Optional(args, "--fixture");
            var killFile = Optional(args, "--kill-file");
            foreach (var path in new[] { summaryPath, requestedHealth, statePath, connectionFile, fixturePath, killFile })
            {
                if (path is not null)
                    ResearchPaths.RefuseProductionPath(path);
            }

            healthPath = requestedHealth;

            if (statePath is null && connectionFile is null)
                throw new InvalidOperationException("Pass --state or --connection-file. The host does not read the production environment.");

            var symbol = Optional(args, "--symbol") ?? "BTCUSDT";
            var limit = int.Parse(Optional(args, "--limit") ?? "48", CultureInfo.InvariantCulture);
            var maxStale = TimeSpan.FromHours((double)ParseDecimal(Optional(args, "--max-stale-hours") ?? "48"));
            var asOf = Optional(args, "--as-of") is { } rawAsOf
                ? DateTimeOffset.Parse(rawAsOf, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                : DateTimeOffset.UtcNow;
            var maxCandles = int.Parse(Optional(args, "--max-candles") ?? "500", CultureInfo.InvariantCulture);
            var maxHttp = int.Parse(Optional(args, "--max-http") ?? "1", CultureInfo.InvariantCulture);
            var maxRuntime = TimeSpan.FromSeconds(int.Parse(Optional(args, "--max-runtime-seconds") ?? "30", CultureInfo.InvariantCulture));
            var config = new ResearchConfig(
                ParseDecimal(Optional(args, "--quantity") ?? "0.001"),
                ParseDecimal(Optional(args, "--confidence") ?? "0.80"),
                ParseDecimal(Optional(args, "--starting-equity") ?? "10000"),
                new PaperSessionConfig(
                    ParseDecimal(Optional(args, "--slippage-bps") ?? "10"),
                    ParseDecimal(Optional(args, "--fee-rate") ?? "0.001"),
                    ParseDecimal(Optional(args, "--minimum-fee") ?? "0"),
                    ParseDecimal(Optional(args, "--leverage") ?? "1"),
                    new RiskLimits(
                        ParseDecimal(Required(args, "--max-notional")),
                        ParseDecimal(Required(args, "--max-leverage")),
                        ParseDecimal(Required(args, "--min-confidence"))),
                    ParseDecimal(Required(args, "--session-notional")),
                    false));
            var strategy = ResearchStrategies.Resolve(Optional(args, "--strategy"), Optional(args, "--strategy-params"));

            if (killFile is not null && File.Exists(killFile))
            {
                WriteOutputs(summaryPath, healthPath, KilledReport(), 0, null, sql);
                return 2;
            }

            var clock = Stopwatch.StartNew();
            string body;
            string? weight = null;
            var httpRequests = 0;
            if (fixturePath is not null)
            {
                body = File.ReadAllText(fixturePath);
            }
            else if (maxHttp == 1)
            {
                httpRequests = 1;
                ResearchRunGuard.Ensure(0, maxCandles, httpRequests, maxHttp, clock.Elapsed, maxRuntime);
                using var client = new BinancePublicKlineClient();
                var response = await client.ReadAsync(symbol, limit, CancellationToken.None);
                body = response.Body;
                weight = response.UsedWeight1m;
            }
            else
            {
                ResearchRunGuard.Ensure(0, maxCandles, 1, maxHttp, clock.Elapsed, maxRuntime);
                using var client = new BinancePublicKlineClient();
                var response = await client.ReadHistoryAsync(symbol, limit, maxHttp, CancellationToken.None);
                body = response.Body;
                weight = response.UsedWeight1m;
                httpRequests = response.Requests;
            }

            var intake = ResearchCandles.ParseBinance(body, symbol, asOf, maxStale);
            ResearchRunGuard.Ensure(intake.Accepted.Count, maxCandles, httpRequests, maxHttp, clock.Elapsed, maxRuntime);

            FileResearchState? file = null;
            IResearchLog researchLog;
            IPaperOperationLog paperLog;
            if (connectionFile is not null)
            {
                sql = new SqlResearchStore(File.ReadAllText(connectionFile).Trim());
                researchLog = sql;
                paperLog = sql;
            }
            else
            {
                file = FileResearchState.Load(statePath!);
                researchLog = file;
                paperLog = file;
            }

            var report = ResearchSession.Run(
                intake,
                config,
                paperLog,
                researchLog,
                killFile is null ? null : () => File.Exists(killFile),
                strategy);
            if (sql is not null && report.SessionKey.Length > 0)
                sql.WriteDetails(report.SessionKey, report.Snapshot);
            sql?.WriteRun(ResearchRunRecord.From(report, started));
            file?.Save();
            WriteOutputs(summaryPath, healthPath, report, httpRequests, weight, sql);
            return report.Status switch
            {
                ResearchSession.Killed => 2,
                _ => 0,
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or OverflowException or JsonException or ArgumentOutOfRangeException or HttpRequestException)
        {
            Console.Error.WriteLine(ex.Message);
            try
            {
                sql?.WriteRun(ResearchRunRecord.Failure(started, ex.Message));
            }
            catch (Exception recordError)
            {
                Console.Error.WriteLine(recordError.Message);
            }
            if (healthPath is not null)
                WriteHealthFile(healthPath, "ERROR", ex.Message, 0, null);
            return 4;
        }
        finally
        {
            sql?.Dispose();
        }
    }

    private static ResearchReport KilledReport()
        => new(ResearchSession.Killed, false, 0, 0, 0, "", new ResearchSnapshot(
            ResearchSession.Killed, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0m, 0m, null,
            PaperSession.NoClosedPosition, null, null, ResearchSession.DrawdownUnavailable, [], [], []));

    private static void WriteOutputs(
        string summaryPath,
        string healthPath,
        ResearchReport report,
        int httpRequests,
        string? weight,
        SqlResearchStore? sql)
    {
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(summaryPath))!);
        File.WriteAllText(summaryPath, json + Environment.NewLine);
        var detail = $"http={httpRequests};weight1m={weight ?? "none"};inserted={report.CandlesInsertedThisRun};duplicates={report.DuplicateCandlesThisRun};new_fills={report.NewPaperFills};replayed={report.Replayed}";
        WriteHealthFile(healthPath, report.Status, detail, httpRequests, weight);
        sql?.WriteHealth(report.Status, detail);
        Console.WriteLine($"status: {report.Status}");
        Console.WriteLine($"replayed: {report.Replayed}");
        Console.WriteLine($"candles_inserted: {report.CandlesInsertedThisRun}");
        Console.WriteLine($"duplicate_candles: {report.DuplicateCandlesThisRun}");
        Console.WriteLine($"signals: {report.Snapshot.Signals}");
        Console.WriteLine($"risk_refused: {report.Snapshot.RiskRefused}");
        Console.WriteLine($"paper_filled: {report.Snapshot.PaperFilled}");
        Console.WriteLine($"new_paper_fills: {report.NewPaperFills}");
        Console.WriteLine($"fees: {report.Snapshot.Fees.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"slippage_quote: {report.Snapshot.SlippageQuote.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"net_realized_pnl: {(report.Snapshot.NetRealizedPnl is null ? "null" : report.Snapshot.NetRealizedPnl.Value.ToString(CultureInfo.InvariantCulture))}");
        Console.WriteLine($"pnl_status: {report.Snapshot.PnlStatus}");
        Console.WriteLine($"cash_max_drawdown: {(report.Snapshot.CashMaxDrawdown is null ? "null" : report.Snapshot.CashMaxDrawdown.Value.ToString(CultureInfo.InvariantCulture))}");
        Console.WriteLine($"drawdown_status: {report.Snapshot.DrawdownStatus}");
        Console.WriteLine($"http_requests: {httpRequests}");
    }

    private static void WriteHealthFile(string path, string status, string detail, int httpRequests, string? weight)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        var payload = JsonSerializer.Serialize(new
        {
            status,
            recordedAt = DateTimeOffset.UtcNow,
            detail,
            httpRequests,
            usedWeight1m = weight,
        });
        File.WriteAllText(path, payload + Environment.NewLine);
    }

    private static decimal ParseDecimal(string text)
    {
        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            throw new InvalidOperationException($"Cannot parse decimal '{text}'.");
        return value;
    }

    private static string Required(string[] args, string name)
        => Optional(args, name) ?? throw new InvalidOperationException($"Missing {name}.");

    private static string? Optional(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.Ordinal))
                continue;
            if (i + 1 >= args.Length || args[i + 1].StartsWith('-'))
                throw new InvalidOperationException($"Missing the value for {name}.");
            return args[i + 1];
        }

        return null;
    }

    private const string Help =
        """
        Trading Research Host V1
        One-shot research process. It does not open a port, send an order, read /etc/kit-platform/api.env, or use an exchange trading adapter.

        Strategy families, all long-only, one position, next-bar open fills:
          sma-cross-v1 fast=5,slow=20. Entry when the fast average crosses above the slow average.
          ema-cross-v1 fast=12,slow=26. Same cross rule on exponential averages.
          rsi-reversion-v1 entry=30,exit=50,period=14. Entry when RSI crosses up through 30. Exit when it crosses up through 50.
          breakout-v1 lookback=20. Entry when the close breaks the prior window high. Exit when it breaks the prior window low.
          Pass --strategy and optional --strategy-params. The default is sma-cross-v1 with the baseline parameters.
          Averages, RSI, and breakout windows never read a later bar. A signal without a next bar is not filled.

        Market data, when --fixture is omitted:
          GET https://api.binance.com/api/v3/klines?symbol=SYMBOL&interval=1h&limit=N
          One page is 1..500 bars. --max-http 2 or 3 pages backward with endTime, at most 1500 bars.
          No API key. Redirects are disabled. Missing bars stay missing.

        dotnet run --project src/Packs/PersonalIntelligence/KitPlatform.Packs.PersonalIntelligence.ResearchHost -- ^
          --fixture candles.json --summary summary.json --health health.json --state state.json ^
          --symbol BTCUSDT --as-of 2026-01-03T00:00:00Z --max-stale-hours 96 ^
          --max-notional 100000 --max-leverage 2 --min-confidence 0.5 --session-notional 1000000

        --connection-file accepts only database trading_agent.
        Apply migrations/395_research_trading_v1.sql first.
        --kill-file stops the process before a new session. Exit 2 means KILLED.
        """;
}
