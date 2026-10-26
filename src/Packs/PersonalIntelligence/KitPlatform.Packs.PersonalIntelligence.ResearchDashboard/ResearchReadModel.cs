using System.Globalization;
using System.Text.Json;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.ResearchDashboard;

public sealed record CandleInput(
    string Symbol,
    string Interval,
    DateTimeOffset OpenTime,
    DateTimeOffset CloseTime,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    DateTimeOffset IngestedAt);

public sealed record SignalInput(
    string SignalKey,
    string Symbol,
    string Side,
    DateTimeOffset SignalTime,
    DateTimeOffset? FillTime,
    decimal? FillPrice,
    string Status);

public sealed record DecisionInput(string OperationKey, bool Allowed, string Reasons);

public sealed record FillInput(
    string OperationKey,
    string Side,
    decimal TapePrice,
    decimal ExecutionPrice,
    decimal Quantity,
    decimal Fee,
    decimal SlippageQuote,
    decimal NetCash);

public sealed record RunInput(long Id, DateTimeOffset RecordedAt, string Status, string Detail);

public sealed record SessionInput(DateTimeOffset CreatedAt, string Status, decimal? NetRealizedPnl, string PnlStatus);

public sealed record ResearchReadBundle(
    int CandleCount,
    IReadOnlyList<CandleInput> Candles,
    IReadOnlyList<SignalInput> Signals,
    IReadOnlyList<DecisionInput> Decisions,
    IReadOnlyList<FillInput> Fills,
    IReadOnlyList<RunInput> Runs,
    IReadOnlyList<SessionInput> Sessions);

public sealed record PnlView(string? Amount, string Status, bool Known);

public sealed record OverviewView(
    string Status,
    string? LastRunAt,
    int Candles,
    int Signals,
    int PaperFills,
    int Replays,
    string Fees,
    string Slippage,
    string CashFromFills,
    PnlView RealizedPnl);

public sealed record CandleView(
    string Symbol,
    string Interval,
    string OpenTime,
    string CloseTime,
    string Open,
    string High,
    string Low,
    string Close,
    bool Closed,
    string IngestedAt);

public sealed record SignalView(
    string Strategy,
    string Symbol,
    string Side,
    string SignalTime,
    string Status,
    string? FillTime,
    string? FillPrice,
    bool? RiskAllowed,
    string RiskReason);

public sealed record FillView(
    string OperationKey,
    string Side,
    string Quantity,
    string TapePrice,
    string ExecutionPrice,
    string Fee,
    string SlippageQuote,
    string NetCash);

public sealed record RunView(
    long Id,
    string RecordedAt,
    string Status,
    int? Inserted,
    int? Duplicates,
    int? NewFills,
    bool? Replayed,
    string? HttpRequests,
    string Detail);

public sealed record TradeLedgerView(
    string TradeId,
    string Status,
    string? Direction,
    string? Symbol,
    string? Timeframe,
    string? Strategy,
    string? OpenedAt,
    string? ClosedAt,
    string? OpenPrice,
    string? ClosePrice,
    string? Quantity,
    PnlView GrossPnl,
    PnlView Fees,
    PnlView Slippage,
    PnlView NetPnl,
    string? CloseReason,
    IReadOnlyList<string> FillKeys);

public sealed record PositionLedgerView(
    string Symbol,
    string Direction,
    string Quantity,
    string EntryPrice,
    PnlView UnrealizedPnl,
    IReadOnlyList<string> FillKeys);

public sealed record PerformanceView(
    int ClosedTrades,
    PnlView WinRate,
    PnlView ProfitFactor,
    PnlView Expectancy,
    PnlView NetPnl,
    PnlView MaxDrawdown);

public sealed record SampleWindowView(
    int ClosedTrades,
    PnlView NetPnl,
    string? AverageHoldHours,
    string? From,
    string? To);

public sealed record SampleSplitView(
    bool Known,
    string? SplitAt,
    SampleWindowView InSample,
    SampleWindowView OutOfSample,
    string Note);

public sealed record DashboardDocument(
    string GeneratedAt,
    string Source,
    OverviewView Overview,
    IReadOnlyList<CandleView> Candles,
    IReadOnlyList<SignalView> Signals,
    IReadOnlyList<FillView> Fills,
    IReadOnlyList<RunView> Runs,
    IReadOnlyList<TradeLedgerView> Trades,
    IReadOnlyList<PositionLedgerView> Positions,
    PerformanceView Performance,
    SampleSplitView Sample);

public static class ResearchReadModel
{
    public static DashboardDocument Build(ResearchReadBundle bundle, DateTimeOffset generatedAt, string source)
    {
        var runs = bundle.Runs.OrderByDescending(row => row.RecordedAt).ThenByDescending(row => row.Id).ToArray();
        var latest = runs.FirstOrDefault();
        var sessions = bundle.Sessions.OrderByDescending(row => row.CreatedAt).ToArray();
        var pnl = ResolvePnl(sessions);
        var decisions = bundle.Decisions.ToDictionary(row => row.OperationKey, StringComparer.Ordinal);
        var ledger = ResearchTradeLedger.Build(LedgerFills(bundle));
        return new DashboardDocument(
            generatedAt.ToString("O", CultureInfo.InvariantCulture),
            source,
            new OverviewView(
                latest?.Status ?? "NO_DATA",
                latest?.RecordedAt.ToString("O", CultureInfo.InvariantCulture),
                bundle.CandleCount,
                bundle.Signals.Count,
                bundle.Fills.Count,
                runs.Count(row => string.Equals(row.Status, "REPLAYED", StringComparison.OrdinalIgnoreCase)),
                Sum(bundle.Fills.Select(row => row.Fee)),
                Sum(bundle.Fills.Select(row => row.SlippageQuote)),
                Sum(bundle.Fills.Select(row => row.NetCash)),
                pnl),
            bundle.Candles.Select(row => new CandleView(
                row.Symbol,
                row.Interval,
                row.OpenTime.ToString("O", CultureInfo.InvariantCulture),
                row.CloseTime.ToString("O", CultureInfo.InvariantCulture),
                Text(row.Open),
                Text(row.High),
                Text(row.Low),
                Text(row.Close),
                row.CloseTime <= generatedAt,
                row.IngestedAt.ToString("O", CultureInfo.InvariantCulture))).ToArray(),
            bundle.Signals.Select(row =>
            {
                decisions.TryGetValue(row.SignalKey, out var decision);
                return new SignalView(
                    StrategyOf(row.SignalKey),
                    row.Symbol,
                    row.Side,
                    row.SignalTime.ToString("O", CultureInfo.InvariantCulture),
                    row.Status,
                    row.FillTime?.ToString("O", CultureInfo.InvariantCulture),
                    row.FillPrice is null ? null : Text(row.FillPrice.Value),
                    decision is null ? null : decision.Allowed,
                    decision is null ? "chưa có quyết định risk" : decision.Allowed ? "" : (string.IsNullOrWhiteSpace(decision.Reasons) ? "không có lý do được lưu" : decision.Reasons));
            }).ToArray(),
            bundle.Fills.Select(row => new FillView(
                row.OperationKey,
                row.Side,
                Text(row.Quantity),
                Text(row.TapePrice),
                Text(row.ExecutionPrice),
                Text(row.Fee),
                Text(row.SlippageQuote),
                Text(row.NetCash))).ToArray(),
            runs.Select(row =>
            {
                var parsed = ParseDetail(row.Detail);
                return new RunView(row.Id, row.RecordedAt.ToString("O", CultureInfo.InvariantCulture), row.Status, parsed.Inserted, parsed.Duplicates, parsed.NewFills, parsed.Replayed, parsed.HttpRequests, row.Detail);
            }).ToArray(),
            ledger.Trades.Select(MapTrade).ToArray(),
            ledger.Positions.Select(MapPosition).ToArray(),
            MapPerformance(ResearchPerformance.FromClosedTrades(ledger.Trades)),
            MapSample(ResearchRobustness.Split(bundle.Candles.Select(row => row.OpenTime).ToArray(), ledger.Trades)));
    }

    public static ResearchLedger Ledger(ResearchReadBundle bundle)
        => ResearchTradeLedger.Build(LedgerFills(bundle));

    private static IReadOnlyList<LedgerFill> LedgerFills(ResearchReadBundle bundle)
    {
        var signals = bundle.Signals.GroupBy(row => row.SignalKey, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        return bundle.Fills.Select(fill =>
        {
            signals.TryGetValue(fill.OperationKey, out var signal);
            return new LedgerFill(
                fill.OperationKey,
                fill.Side,
                fill.ExecutionPrice,
                fill.Quantity,
                fill.Fee,
                fill.SlippageQuote,
                fill.NetCash,
                signal?.Symbol,
                ResearchTradeLedger.TimeframeOf(signal?.SignalKey),
                ResearchTradeLedger.StrategyOf(signal?.SignalKey),
                signal?.Status == "ready" ? signal.FillTime : null);
        }).ToArray();
    }

    private static TradeLedgerView MapTrade(LedgerTrade trade)
        => new(
            trade.TradeId,
            trade.Status,
            trade.Direction,
            trade.Symbol,
            trade.Timeframe,
            trade.Strategy,
            trade.OpenedAt?.ToString("O", CultureInfo.InvariantCulture),
            trade.ClosedAt?.ToString("O", CultureInfo.InvariantCulture),
            trade.OpenPrice is null ? null : Text(trade.OpenPrice.Value),
            trade.ClosePrice is null ? null : Text(trade.ClosePrice.Value),
            trade.Quantity is null ? null : Text(trade.Quantity.Value),
            Money(trade.GrossPnl, trade.GrossKnown, trade.NetStatus),
            Money(trade.Fees, trade.FeesKnown, "recorded"),
            Money(trade.Slippage, trade.SlippageKnown, "recorded"),
            Money(trade.NetPnl, trade.NetKnown, trade.NetStatus),
            trade.CloseReason,
            trade.FillKeys);

    private static PositionLedgerView MapPosition(LedgerPosition position)
        => new(
            position.Symbol,
            position.Direction,
            Text(position.Quantity),
            Text(position.EntryPrice),
            new PnlView(null, position.UnrealizedStatus, false),
            position.FillKeys);

    private static SampleSplitView MapSample(SampleSplit split)
        => new(split.Known, split.SplitAt?.ToString("O", CultureInfo.InvariantCulture), MapWindow(split.InSample), MapWindow(split.OutOfSample), split.Note);

    private static SampleWindowView MapWindow(SampleWindow window)
        => new(
            window.ClosedTrades,
            Money(window.Performance.NetPnl, window.Performance.NetPnlKnown, window.Performance.NetPnlKnown ? "realized" : "unavailable"),
            window.AverageHoldHours is null ? null : Text(window.AverageHoldHours.Value),
            window.From?.ToString("O", CultureInfo.InvariantCulture),
            window.To?.ToString("O", CultureInfo.InvariantCulture));

    private static PerformanceView MapPerformance(ResearchPerformanceReport report)
        => new(
            report.ClosedTrades,
            Money(report.WinRate, report.WinRateKnown, "realized"),
            Money(report.ProfitFactor, report.ProfitFactorKnown, report.ProfitFactorStatus),
            Money(report.Expectancy, report.ExpectancyKnown, "realized"),
            Money(report.NetPnl, report.NetPnlKnown, "realized"),
            Money(report.MaxDrawdown, report.MaxDrawdownKnown, "realized"));

    private static PnlView Money(decimal? amount, bool known, string status)
        => known && amount is not null ? new PnlView(Text(amount.Value), status, true) : new PnlView(null, status, false);

    public static PnlView ResolvePnl(IReadOnlyList<SessionInput> sessions)
    {
        var latest = sessions.OrderByDescending(row => row.CreatedAt).FirstOrDefault();
        if (latest is null)
            return new PnlView(null, "unavailable", false);
        if (string.Equals(latest.PnlStatus, "realized", StringComparison.Ordinal) && latest.NetRealizedPnl is not null)
            return new PnlView(Text(latest.NetRealizedPnl.Value), "realized", true);
        return new PnlView(null, string.IsNullOrWhiteSpace(latest.PnlStatus) ? "unavailable" : latest.PnlStatus, false);
    }

    public static RunDetail ParseDetail(string detail)
    {
        int? inserted = null;
        int? duplicates = null;
        int? newFills = null;
        bool? replayed = null;
        string? http = null;
        foreach (var part in detail.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var split = part.Split('=', 2);
            if (split.Length != 2)
                continue;
            var key = split[0];
            var value = split[1];
            if (key == "inserted" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var insertedValue))
                inserted = insertedValue;
            else if (key == "duplicates" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var duplicateValue))
                duplicates = duplicateValue;
            else if (key == "new_fills" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fillValue))
                newFills = fillValue;
            else if (key == "replayed" && bool.TryParse(value, out var replayedValue))
                replayed = replayedValue;
            else if (key == "http")
                http = value;
        }

        return new RunDetail(inserted, duplicates, newFills, replayed, http);
    }

    public static string StrategyOf(string signalKey)
    {
        var index = signalKey.IndexOf(':');
        return index > 0 ? signalKey[..index] : signalKey;
    }

    public static decimal? ReadPnl(string sessionResult)
    {
        using var document = JsonDocument.Parse(sessionResult);
        if (!document.RootElement.TryGetProperty("NetRealizedPnl", out var value) || value.ValueKind is JsonValueKind.Null)
            return null;
        return value.GetDecimal();
    }

    public static string ReadPnlStatus(string sessionResult)
    {
        using var document = JsonDocument.Parse(sessionResult);
        return document.RootElement.TryGetProperty("PnlStatus", out var value) ? value.GetString() ?? "unavailable" : "unavailable";
    }

    private static string Sum(IEnumerable<decimal> values)
        => Text(values.Aggregate(0m, (total, value) => total + value));

    public static string Text(decimal value)
        => value.ToString(CultureInfo.InvariantCulture);
}

public sealed record RunDetail(int? Inserted, int? Duplicates, int? NewFills, bool? Replayed, string? HttpRequests);
