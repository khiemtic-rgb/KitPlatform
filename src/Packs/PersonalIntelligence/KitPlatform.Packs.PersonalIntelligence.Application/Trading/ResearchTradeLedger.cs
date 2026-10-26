namespace KitPlatform.Packs.PersonalIntelligence.Trading;

public sealed record LedgerFill(
    string OperationKey,
    string Side,
    decimal ExecutionPrice,
    decimal Quantity,
    decimal Fee,
    decimal SlippageQuote,
    decimal NetCash,
    string? Symbol,
    string? Timeframe,
    string? Strategy,
    DateTimeOffset? FillTime);

public sealed record LedgerTrade(
    string TradeId,
    string Status,
    string? Direction,
    string? Symbol,
    string? Timeframe,
    string? Strategy,
    DateTimeOffset? OpenedAt,
    DateTimeOffset? ClosedAt,
    decimal? OpenPrice,
    decimal? ClosePrice,
    decimal? Quantity,
    decimal? GrossPnl,
    bool GrossKnown,
    decimal? Fees,
    bool FeesKnown,
    decimal? Slippage,
    bool SlippageKnown,
    decimal? NetPnl,
    bool NetKnown,
    string NetStatus,
    string? CloseReason,
    IReadOnlyList<string> FillKeys);

public sealed record LedgerPosition(
    string Symbol,
    string Direction,
    decimal Quantity,
    decimal EntryPrice,
    decimal? UnrealizedPnl,
    bool UnrealizedKnown,
    string UnrealizedStatus,
    IReadOnlyList<string> FillKeys);

public sealed record ResearchLedger(
    IReadOnlyList<LedgerTrade> Trades,
    IReadOnlyList<LedgerPosition> Positions,
    bool Incomplete);

/// <summary>
/// Rebuilds the long-only paper book from recorded fills.
/// A fill is not a closed trade. Realized PnL uses the same average-cost rule as <see cref="PaperSession"/>.
/// Unrealized PnL is never marked here.
/// </summary>
public static class ResearchTradeLedger
{
    public const string Open = "Open";
    public const string Closed = "Closed";
    public const string Partial = "Partial";
    public const string Unmatched = "Unmatched";
    public const string Long = "Long";
    public const string CloseReasonSmaCrossBelow = "sma_cross_below";
    public const string Unmarked = "unmarked";

    public static ResearchLedger Build(IReadOnlyList<LedgerFill> fills)
    {
        ArgumentNullException.ThrowIfNull(fills);
        var trades = new List<LedgerTrade>();
        var books = new Dictionary<string, Book>(StringComparer.Ordinal);
        var incomplete = false;
        var ordered = fills
            .Where(fill => fill.FillTime is not null && fill.Symbol is not null)
            .OrderBy(fill => fill.FillTime)
            .ThenBy(fill => fill.OperationKey, StringComparer.Ordinal)
            .ToArray();
        var consumed = new HashSet<string>(ordered.Select(fill => fill.OperationKey), StringComparer.Ordinal);

        foreach (var fill in fills)
        {
            if (!consumed.Contains(fill.OperationKey))
            {
                incomplete = true;
                trades.Add(UnmatchedTrade(fill));
            }
        }

        foreach (var fill in ordered)
        {
            var symbol = fill.Symbol!;
            books.TryGetValue(symbol, out var book);
            if (fill.Side == "BUY")
            {
                book ??= new Book(symbol, fill);
                book.Add(fill);
                books[symbol] = book;
                continue;
            }

            if (fill.Side != "SELL" || book is null || book.Quantity <= 0m || fill.Quantity > book.Quantity)
            {
                incomplete = true;
                trades.Add(UnmatchedTrade(fill));
                continue;
            }

            trades.Add(book.Close(fill));
            if (book.Quantity == 0m)
                books.Remove(symbol);
        }

        foreach (var book in books.Values)
            trades.Add(book.OpenTrade());

        var positions = books.Values.Select(book => book.Position()).ToArray();
        return new ResearchLedger(trades, positions, incomplete);
    }

    public static string? TimeframeOf(string? signalKey)
    {
        if (string.IsNullOrWhiteSpace(signalKey))
            return null;
        var parts = signalKey.Split(':');
        return parts.Length >= 3 && parts[2].Length > 0 ? parts[2] : null;
    }

    public static string? StrategyOf(string? signalKey)
    {
        if (string.IsNullOrWhiteSpace(signalKey))
            return null;
        var index = signalKey.IndexOf(':');
        return index > 0 ? signalKey[..index] : signalKey;
    }

    private static LedgerTrade UnmatchedTrade(LedgerFill fill)
        => new(
            "unmatched:" + fill.OperationKey,
            Unmatched,
            null,
            fill.Symbol,
            fill.Timeframe,
            fill.Strategy,
            fill.FillTime,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            false,
            null,
            false,
            null,
            false,
            PaperSession.PnlUnavailable,
            null,
            [fill.OperationKey]);

    private sealed class Book
    {
        public Book(string symbol, LedgerFill first)
        {
            Symbol = symbol;
            Strategy = first.Strategy;
            Timeframe = first.Timeframe;
            OpenedAt = first.FillTime;
            EntryKey = first.OperationKey;
        }

        public string Symbol { get; }
        public string? Strategy { get; private set; }
        public string? Timeframe { get; private set; }
        public DateTimeOffset? OpenedAt { get; private set; }
        public string EntryKey { get; private set; }
        public decimal Quantity { get; private set; }
        public decimal Cost { get; private set; }
        public decimal ExecNotional { get; private set; }
        public decimal Fees { get; private set; }
        public decimal Slippage { get; private set; }
        public List<string> FillKeys { get; } = [];

        public void Add(LedgerFill fill)
        {
            if (Quantity == 0m)
            {
                OpenedAt = fill.FillTime;
                EntryKey = fill.OperationKey;
                Strategy = fill.Strategy;
                Timeframe = fill.Timeframe;
                FillKeys.Clear();
            }

            Quantity += fill.Quantity;
            Cost += (-fill.NetCash);
            ExecNotional += fill.ExecutionPrice * fill.Quantity;
            Fees += fill.Fee;
            Slippage += fill.SlippageQuote;
            FillKeys.Add(fill.OperationKey);
        }

        public LedgerTrade Close(LedgerFill fill)
        {
            var averageCost = Cost / Quantity;
            var averageExec = ExecNotional / Quantity;
            var averageFee = Fees / Quantity;
            var averageSlippage = Slippage / Quantity;
            var closedQty = fill.Quantity;
            var gross = (fill.ExecutionPrice - averageExec) * closedQty;
            var fees = (averageFee * closedQty) + fill.Fee;
            var slippage = (averageSlippage * closedQty) + fill.SlippageQuote;
            var exitNotional = fill.ExecutionPrice * closedQty;
            var net = (exitNotional - fill.Fee) - (averageCost * closedQty);
            var keys = FillKeys.Append(fill.OperationKey).ToArray();
            var reason = ResearchStrategies.CloseReason(fill.Strategy);

            Quantity -= closedQty;
            Cost -= averageCost * closedQty;
            ExecNotional -= averageExec * closedQty;
            Fees -= averageFee * closedQty;
            Slippage -= averageSlippage * closedQty;

            return new LedgerTrade(
                "closed:" + EntryKey + ":" + fill.OperationKey,
                Quantity == 0m ? Closed : Partial,
                Long,
                Symbol,
                Timeframe,
                Strategy,
                OpenedAt,
                fill.FillTime,
                averageExec,
                fill.ExecutionPrice,
                closedQty,
                gross,
                true,
                fees,
                true,
                slippage,
                true,
                net,
                true,
                PaperSession.Realized,
                reason,
                keys);
        }

        public LedgerTrade OpenTrade()
            => new(
                "open:" + EntryKey,
                Open,
                Long,
                Symbol,
                Timeframe,
                Strategy,
                OpenedAt,
                null,
                Quantity == 0m ? null : ExecNotional / Quantity,
                null,
                Quantity,
                null,
                false,
                Fees,
                true,
                Slippage,
                true,
                null,
                false,
                PaperSession.NoClosedPosition,
                null,
                FillKeys.ToArray());

        public LedgerPosition Position()
            => new(Symbol, Long, Quantity, ExecNotional / Quantity, null, false, Unmarked, FillKeys.ToArray());
    }
}
