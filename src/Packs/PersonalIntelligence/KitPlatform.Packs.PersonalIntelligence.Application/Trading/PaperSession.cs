using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace KitPlatform.Packs.PersonalIntelligence.Trading;

public sealed record PaperSessionConfig(
    decimal SlippageBps,
    decimal FeeRate,
    decimal MinimumFee,
    decimal Leverage,
    RiskLimits Limits,
    decimal SessionNotionalCap,
    bool Live);

public sealed record PaperOperationReceipt(string OperationKey, string PayloadHash, string Status, string Result);

public interface IPaperOperationLog
{
    PaperOperationReceipt? Find(string operationKey);

    bool TryAdd(PaperOperationReceipt receipt);
}

public sealed class MemoryPaperOperationLog : IPaperOperationLog
{
    private readonly Dictionary<string, PaperOperationReceipt> _rows = new(StringComparer.Ordinal);

    public PaperOperationReceipt? Find(string operationKey)
        => _rows.TryGetValue(operationKey, out var row) ? row : null;

    public bool TryAdd(PaperOperationReceipt receipt)
        => _rows.TryAdd(receipt.OperationKey, receipt);

    public int Count => _rows.Count;
}

public sealed record PaperFillRecord(
    string OperationKey,
    string Symbol,
    string Side,
    decimal TapePrice,
    decimal ExecutionPrice,
    decimal Quantity,
    decimal Fee,
    decimal SlippageQuote,
    decimal NetCash);

public sealed record PaperSessionReport(
    string Status,
    int RecordsRead,
    int Proposals,
    int RiskRefused,
    int SessionNotionalRefused,
    int PaperFilled,
    int Replayed,
    int Conflicts,
    int Unprocessed,
    decimal Fees,
    decimal SlippageQuote,
    decimal? RealizedPnl,
    string PnlStatus,
    IReadOnlyList<PaperFillRecord> NewFills);

/// <summary>
/// Reads an already validated tape, asks the existing risk engine, then the paper matcher.
/// A veto, a live proposal, a session-notional breach, or a repeated operation key does not add a fill.
/// </summary>
public static class PaperSession
{
    public const string Completed = "COMPLETED";
    public const string Killed = "KILLED";
    public const string LiveBlocked = "LIVE_BLOCKED";
    public const string Realized = "realized";
    public const string NoClosedPosition = "no_closed_position";
    public const string PartialClose = "partial_close";
    public const string PnlUnavailable = "unavailable";
    public const string ReplayNotRecomputed = "not_recomputed_on_replay";

    public static PaperSessionReport Run(
        IReadOnlyList<PaperTapeRow> tape,
        PaperSessionConfig config,
        IPaperOperationLog log,
        Func<bool>? killSwitch = null)
    {
        ArgumentNullException.ThrowIfNull(tape);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(log);
        if (config.SlippageBps < 0m || config.FeeRate < 0m || config.MinimumFee < 0m || config.SessionNotionalCap < 0m)
            throw new ArgumentOutOfRangeException(nameof(config), "Slippage, fee, and the session notional cap must be non-negative.");
        if (config.Leverage <= 0m)
            throw new ArgumentOutOfRangeException(nameof(config), "Leverage must be positive.");

        var proposals = 0;
        var riskRefused = 0;
        var notionalRefused = 0;
        var filled = 0;
        var replayed = 0;
        var conflicts = 0;
        var fees = 0m;
        var slippage = 0m;
        var sessionNotional = 0m;
        var fills = new List<PaperFillRecord>();
        var books = new Dictionary<string, PositionBook>(StringComparer.Ordinal);
        var pnlIncomplete = false;
        var reduced = false;

        for (var index = 0; index < tape.Count; index++)
        {
            if (killSwitch?.Invoke() == true)
            {
                return Report(Killed, tape.Count, proposals, riskRefused, notionalRefused, filled, replayed, conflicts, tape.Count - index, fees, slippage, fills, books, pnlIncomplete, reduced, replayed > 0 && filled == 0);
            }

            var row = tape[index];
            var hash = Hash(row);
            var existing = log.Find(row.OperationKey);
            if (existing is not null)
            {
                if (!string.Equals(existing.PayloadHash, hash, StringComparison.Ordinal))
                    conflicts++;
                else
                    replayed++;
                continue;
            }

            var mode = config.Live ? ExecutionMode.Live : ExecutionMode.Paper;
            var proposal = new TradeProposal(row.Symbol, row.Side, row.Quantity, row.Price, config.Leverage, row.Confidence, mode);
            proposals++;
            var verdict = RiskEngine.Evaluate(proposal, config.Limits);
            if (!verdict.Allowed)
            {
                riskRefused++;
                log.TryAdd(new PaperOperationReceipt(row.OperationKey, hash, "risk_refused", string.Join("; ", verdict.Reasons)));
                continue;
            }

            if (!PaperExecutionMatcher.TryMatch(proposal, config.SlippageBps, config.FeeRate, config.MinimumFee, out var match, out var matchRefusal))
            {
                riskRefused++;
                log.TryAdd(new PaperOperationReceipt(row.OperationKey, hash, "risk_refused", matchRefusal));
                continue;
            }

            if (sessionNotional + match.Notional > config.SessionNotionalCap)
            {
                notionalRefused++;
                log.TryAdd(new PaperOperationReceipt(row.OperationKey, hash, "session_notional_refused", "Session notional cap."));
                continue;
            }

            if (!log.TryAdd(new PaperOperationReceipt(row.OperationKey, hash, "paper_filled", FormatFill(row, match))))
            {
                replayed++;
                continue;
            }

            sessionNotional += match.Notional;
            fees += match.Fee;
            slippage += match.SlippageQuote;
            filled++;
            fills.Add(new PaperFillRecord(row.OperationKey, row.Symbol, row.Side, match.TapePrice, match.ExecutionPrice, match.Quantity, match.Fee, match.SlippageQuote, match.NetCash));
            ApplyFill(books, row, match, ref pnlIncomplete, ref reduced);
        }

        var status = config.Live ? LiveBlocked : Completed;
        return Report(status, tape.Count, proposals, riskRefused, notionalRefused, filled, replayed, conflicts, 0, fees, slippage, fills, books, pnlIncomplete, reduced, replayed > 0 && filled == 0);
    }

    public static string Hash(PaperTapeRow row)
    {
        var text = string.Join('|',
            row.OperationKey,
            row.RecordedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            row.Symbol,
            row.Side,
            row.Quantity.ToString(CultureInfo.InvariantCulture),
            row.Price.ToString(CultureInfo.InvariantCulture),
            row.Confidence.ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    private static void ApplyFill(Dictionary<string, PositionBook> books, PaperTapeRow row, PaperMatch match, ref bool pnlIncomplete, ref bool reduced)
    {
        if (!books.TryGetValue(row.Symbol, out var book))
            book = new PositionBook();

        if (row.Side == "BUY")
        {
            book.Quantity += match.Quantity;
            book.Cost += match.Notional + match.Fee;
        }
        else if (book.Quantity <= 0m || match.Quantity > book.Quantity)
        {
            pnlIncomplete = true;
        }
        else
        {
            var average = book.Cost / book.Quantity;
            var proceeds = match.Notional - match.Fee;
            book.Realized += proceeds - (average * match.Quantity);
            book.Cost -= average * match.Quantity;
            book.Quantity -= match.Quantity;
            reduced = true;
        }

        books[row.Symbol] = book;
    }

    private static PaperSessionReport Report(
        string status,
        int records,
        int proposals,
        int riskRefused,
        int notionalRefused,
        int filled,
        int replayed,
        int conflicts,
        int unprocessed,
        decimal fees,
        decimal slippage,
        List<PaperFillRecord> fills,
        Dictionary<string, PositionBook> books,
        bool pnlIncomplete,
        bool reduced,
        bool replayOnly)
    {
        decimal? pnl = null;
        var pnlStatus = NoClosedPosition;
        var flat = books.Values.All(book => book.Quantity == 0m);
        if (replayOnly)
            pnlStatus = ReplayNotRecomputed;
        else if (pnlIncomplete)
            pnlStatus = PnlUnavailable;
        else if (reduced && flat)
        {
            pnl = books.Values.Sum(book => book.Realized);
            pnlStatus = Realized;
        }
        else if (reduced)
            pnlStatus = PartialClose;

        return new PaperSessionReport(status, records, proposals, riskRefused, notionalRefused, filled, replayed, conflicts, unprocessed, fees, slippage, pnl, pnlStatus, fills);
    }

    private static string FormatFill(PaperTapeRow row, PaperMatch match)
        => string.Join('|',
            row.Symbol,
            row.Side,
            match.ExecutionPrice.ToString(CultureInfo.InvariantCulture),
            match.Fee.ToString(CultureInfo.InvariantCulture),
            match.SlippageQuote.ToString(CultureInfo.InvariantCulture),
            match.NetCash.ToString(CultureInfo.InvariantCulture));

    private struct PositionBook
    {
        public decimal Quantity;
        public decimal Cost;
        public decimal Realized;
    }
}
