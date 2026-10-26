using System.Globalization;
using System.Text.Json;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Npgsql;

namespace KitPlatform.Packs.PersonalIntelligence.PaperHost;

public static class PaperHostProgram
{
    public static int Main(string[] args) => Run(args);

    public static int Run(string[] args)
    {
        try
        {
            return RunCore(args);
        }
        catch (PaperTapeException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 4;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 4;
        }
    }

    private static int RunCore(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help", StringComparer.Ordinal) || args.Contains("-h", StringComparer.Ordinal))
        {
            Console.WriteLine(Help);
            return args.Length == 0 ? 4 : 0;
        }

        var tapePath = Required(args, "--tape");
        var summaryPath = Required(args, "--summary");
        var receiptPath = Optional(args, "--receipt");
        var connectionFile = Optional(args, "--connection-file");
        var killFile = Optional(args, "--kill-file");
        RefuseProductionPath(tapePath);
        RefuseProductionPath(summaryPath);
        if (receiptPath is not null)
            RefuseProductionPath(receiptPath);
        if (connectionFile is not null)
            RefuseProductionPath(connectionFile);
        if (killFile is not null)
            RefuseProductionPath(killFile);
        if (receiptPath is null && connectionFile is null)
            throw new InvalidOperationException("Pass --receipt or --connection-file. The host does not read the production environment.");

        var config = new PaperSessionConfig(
            ParseDecimal(Optional(args, "--slippage-bps") ?? "0"),
            ParseDecimal(Optional(args, "--fee-rate") ?? "0"),
            ParseDecimal(Optional(args, "--minimum-fee") ?? "0"),
            ParseDecimal(Optional(args, "--leverage") ?? "1"),
            new RiskLimits(
                ParseDecimal(Required(args, "--max-notional")),
                ParseDecimal(Required(args, "--max-leverage")),
                ParseDecimal(Required(args, "--min-confidence"))),
            ParseDecimal(Required(args, "--session-notional")),
            Has(args, "--live"));

        var tape = PaperTape.Read(File.ReadAllText(tapePath));
        IPaperOperationLog log;
        SqlPaperOperationLog? sql = null;
        if (connectionFile is not null)
        {
            sql = new SqlPaperOperationLog(File.ReadAllText(connectionFile).Trim());
            log = sql;
        }
        else
        {
            log = FilePaperOperationLog.Load(receiptPath!);
        }

        try
        {
            var report = PaperSession.Run(tape, config, log, killFile is null ? null : () => File.Exists(killFile));
            if (log is FilePaperOperationLog file)
                file.Save();
            WriteSummary(summaryPath, report);
            return report.Status switch
            {
                PaperSession.Killed => 2,
                PaperSession.LiveBlocked => 3,
                _ => 0,
            };
        }
        finally
        {
            sql?.Dispose();
        }
    }

    private static void WriteSummary(string path, PaperSessionReport report)
    {
        var text = JsonSerializer.Serialize(report, JsonOptions);
        File.WriteAllText(path, text + Environment.NewLine);
        Console.WriteLine($"status: {report.Status}");
        Console.WriteLine($"records_read: {report.RecordsRead}");
        Console.WriteLine($"proposals: {report.Proposals}");
        Console.WriteLine($"risk_refused: {report.RiskRefused}");
        Console.WriteLine($"session_notional_refused: {report.SessionNotionalRefused}");
        Console.WriteLine($"paper_filled: {report.PaperFilled}");
        Console.WriteLine($"replayed: {report.Replayed}");
        Console.WriteLine($"conflicts: {report.Conflicts}");
        Console.WriteLine($"unprocessed: {report.Unprocessed}");
        Console.WriteLine($"fees: {report.Fees.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"slippage_quote: {report.SlippageQuote.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"realized_pnl: {(report.RealizedPnl is null ? "null" : report.RealizedPnl.Value.ToString(CultureInfo.InvariantCulture))}");
        Console.WriteLine($"pnl_status: {report.PnlStatus}");
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static decimal ParseDecimal(string text)
    {
        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            throw new InvalidOperationException($"Cannot parse decimal '{text}'.");
        return value;
    }

    private static bool Has(string[] args, string name)
        => args.Any(arg => string.Equals(arg, name, StringComparison.Ordinal));

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

    private static void RefuseProductionPath(string path)
    {
        var full = Path.GetFullPath(path).Replace('\\', '/');
        if (full.Contains("/etc/kit-platform/api.env", StringComparison.OrdinalIgnoreCase)
            || full.EndsWith("/api.env", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The paper host does not read the production environment.");
    }

    private const string Help =
        """
        Paper Trading Host V1
        Reads an internal tape and writes a paper session. It does not open a port, call an exchange, or read /etc/kit-platform/api.env.

        Tape header:
          operation_key,recorded_at,symbol,side,quantity,price,confidence

        Formulas:
          BUY execution price  = tape * (1 + slippageBps / 10000)
          SELL execution price = tape * (1 - slippageBps / 10000)
          fee = max(minimumFee, executionPrice * quantity * feeRate)
          BUY net cash  = -(execution notional + fee)
          SELL net cash = execution notional - fee
          Realized PnL is reported only when a sell closes quantity bought earlier in the same new fills.
          Open positions are not marked. A sell without an entry does not produce a PnL number.

        dotnet run --project src/Packs/PersonalIntelligence/KitPlatform.Packs.PersonalIntelligence.PaperHost -- ^
          --tape tape.csv --summary summary.json --receipt receipt.json ^
          --slippage-bps 100 --fee-rate 0.01 --minimum-fee 0 ^
          --max-notional 100000 --max-leverage 2 --min-confidence 0.5 --session-notional 1000000

        Add --connection-file connection.txt to store receipts in pack_personal_intelligence.paper_host_receipt.
        The connection file is one Npgsql connection string. Only database trading_agent is accepted.
        Apply migrations/394_pack_personal_intelligence_paper_host_receipt_v1.sql to that database first.
        Add --kill-file stop.flag to stop before the next record. Exit 2 means KILLED. Exit 3 means LIVE_BLOCKED.
        --live refuses every fill through the existing risk engine.
        """;
}
