using System.Globalization;
using System.Text.Json;

namespace KitPlatform.Packs.PersonalIntelligence.Trading;

public sealed record ResearchConfig(
    decimal Quantity,
    decimal Confidence,
    decimal StartingEquity,
    PaperSessionConfig Paper);

public sealed record StoredFill(
    string OperationKey,
    string Side,
    decimal TapePrice,
    decimal ExecutionPrice,
    decimal Quantity,
    decimal Fee,
    decimal SlippageQuote,
    decimal NetCash,
    string? SignalKey = null);

public sealed record ResearchRiskDecision(string OperationKey, bool Allowed, string Reasons, string? SignalKey = null);

public sealed record StoredSignal(
    string SignalKey,
    string Symbol,
    string Side,
    DateTimeOffset SignalTime,
    DateTimeOffset? FillTime,
    decimal? FillPrice,
    string Status,
    string? StrategyId = null,
    string? StrategyVersion = null,
    string? Timeframe = null,
    DateTimeOffset? CandleOpenTime = null);

public sealed record ResearchSnapshot(
    string Status,
    int CandlesAccepted,
    int Duplicates,
    int Stale,
    int FutureOrIncomplete,
    int Gaps,
    int Signals,
    int AwaitingNextOpen,
    int RiskRefused,
    int PaperFilled,
    decimal Fees,
    decimal SlippageQuote,
    decimal? NetRealizedPnl,
    string PnlStatus,
    decimal? CashMaxDrawdown,
    decimal? CashMaxDrawdownFraction,
    string DrawdownStatus,
    IReadOnlyList<StoredSignal> SignalsRecorded,
    IReadOnlyList<StoredFill> Fills,
    IReadOnlyList<ResearchRiskDecision> RiskDecisions,
    ExecutionReceipt? Receipt = null);

public sealed record ResearchReport(
    string Status,
    bool Replayed,
    int NewPaperFills,
    int CandlesInsertedThisRun,
    int DuplicateCandlesThisRun,
    string SessionKey,
    ResearchSnapshot Snapshot);

public sealed record ResearchStoredSession(string SessionKey, string PayloadHash, string Status, string Result);

public interface IResearchLog
{
    int StoreCandles(IReadOnlyList<ResearchCandle> candles);

    ResearchStoredSession? Find(string sessionKey);

    bool TryAdd(ResearchStoredSession session);

    /// <summary>
    /// Replaces the stored snapshot JSON when the session key and payload hash still match.
    /// It does not insert a session, a fill, or a new hash.
    /// </summary>
    bool TryUpdateResult(string sessionKey, string payloadHash, string result);
}

public sealed class MemoryResearchLog : IResearchLog
{
    private readonly Dictionary<string, ResearchCandle> _candles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ResearchStoredSession> _sessions = new(StringComparer.Ordinal);

    public int CandleCount => _candles.Count;

    public int SessionCount => _sessions.Count;

    public int StoreCandles(IReadOnlyList<ResearchCandle> candles)
    {
        var inserted = 0;
        foreach (var candle in candles)
        {
            var key = CandleKey(candle);
            if (_candles.ContainsKey(key))
                continue;
            _candles.Add(key, candle);
            inserted++;
        }

        return inserted;
    }

    public ResearchStoredSession? Find(string sessionKey)
        => _sessions.TryGetValue(sessionKey, out var session) ? session : null;

    public bool TryAdd(ResearchStoredSession session)
        => _sessions.TryAdd(session.SessionKey, session);

    public bool TryUpdateResult(string sessionKey, string payloadHash, string result)
    {
        if (!_sessions.TryGetValue(sessionKey, out var session))
            return false;
        if (!string.Equals(session.PayloadHash, payloadHash, StringComparison.Ordinal))
            return false;
        _sessions[sessionKey] = session with { Result = result };
        return true;
    }

    public static string CandleKey(ResearchCandle candle)
        => string.Join('|', candle.Source, candle.Symbol, candle.Interval, candle.OpenTime.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
}

/// <summary>
/// Turns a closed-candle intake into signals, then calls <see cref="PaperSession"/>.
/// Fee, slippage, and risk vetoes stay in the paper host path.
/// </summary>
public static class ResearchSession
{
    public const string Completed = "COMPLETED";
    public const string DataGap = "DATA_GAP";
    public const string NoCandles = "NO_CANDLES";
    public const string Killed = "KILLED";
    public const string LiveBlocked = "LIVE_BLOCKED";
    public const string Replayed = "REPLAYED";
    public const string CashDrawdown = "cash_from_fills_open_position_unmarked";
    public const string DrawdownUnavailable = "unavailable";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public static string Serialize(ResearchSnapshot snapshot)
        => JsonSerializer.Serialize(snapshot, JsonOptions);

    public static ResearchSnapshot Deserialize(string json)
        => JsonSerializer.Deserialize<ResearchSnapshot>(json, JsonOptions)
           ?? throw new InvalidOperationException("Stored research session result is empty.");

    public static void SaveReceipt(IResearchLog log, string sessionKey, string payloadHash, ResearchSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Receipt is null)
            throw new InvalidOperationException("A receipt save requires a receipt.");
        if (!string.Equals(sessionKey, payloadHash, StringComparison.Ordinal))
            throw new InvalidOperationException("Session key and payload hash must match.");
        if (!string.Equals(snapshot.Receipt.SessionKey, sessionKey, StringComparison.Ordinal)
            || !string.Equals(snapshot.Receipt.PayloadHash, payloadHash, StringComparison.Ordinal))
            throw new InvalidOperationException("The receipt does not belong to this session.");
        var stored = log.Find(sessionKey) ?? throw new InvalidOperationException("The research session is not stored.");
        if (!string.Equals(stored.PayloadHash, payloadHash, StringComparison.Ordinal))
            throw new InvalidOperationException("Stored research session hash does not match its key.");
        if (!log.TryUpdateResult(sessionKey, payloadHash, Serialize(snapshot)))
            throw new InvalidOperationException("The stored research session result was not updated.");
    }

    public static ResearchReport Run(
        CandleIntake intake,
        ResearchConfig config,
        IPaperOperationLog paperLog,
        IResearchLog researchLog,
        Func<bool>? killSwitch = null,
        StrategySpec? strategy = null,
        IResearchRunLog? runs = null)
    {
        ArgumentNullException.ThrowIfNull(intake);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(paperLog);
        ArgumentNullException.ThrowIfNull(researchLog);
        var started = DateTimeOffset.UtcNow;
        try
        {
            return RunRecorded(intake, config, paperLog, researchLog, killSwitch, strategy, started, runs);
        }
        catch (Exception ex)
        {
            runs?.Record(ResearchRunRecord.Failure(started, ex.Message));
            throw;
        }
    }

    private static ResearchReport RunRecorded(
        CandleIntake intake,
        ResearchConfig config,
        IPaperOperationLog paperLog,
        IResearchLog researchLog,
        Func<bool>? killSwitch,
        StrategySpec? strategy,
        DateTimeOffset started,
        IResearchRunLog? runs)
    {
        ResearchReport Finish(ResearchReport report)
        {
            runs?.Record(ResearchRunRecord.From(report, started));
            return report;
        }

        if (config.Quantity <= 0m || config.StartingEquity <= 0m)
            throw new ArgumentOutOfRangeException(nameof(config), "Quantity and starting equity must be positive.");
        if (config.Confidence < 0m || config.Confidence > 1m)
            throw new ArgumentOutOfRangeException(nameof(config), "Confidence must be from 0 to 1.");

        if (killSwitch?.Invoke() == true)
            return Finish(new ResearchReport(Killed, false, 0, 0, 0, "", Empty(Killed, intake)));

        var inserted = researchLog.StoreCandles(intake.Accepted);
        var duplicates = intake.Duplicates + (intake.Accepted.Count - inserted);
        var spec = strategy ?? ResearchStrategies.Resolve(null, null);
        var hash = Hash(intake, config, spec);
        var existing = researchLog.Find(hash);
        if (existing is not null)
        {
            if (!string.Equals(existing.PayloadHash, hash, StringComparison.Ordinal))
                throw new InvalidOperationException("Stored research session hash does not match its key.");
            var stored = Deserialize(existing.Result);
            return Finish(new ResearchReport(Replayed, true, 0, inserted, duplicates, hash, stored));
        }

        if (intake.Accepted.Count == 0)
            return Finish(Store(researchLog, hash, Empty(NoCandles, intake), inserted, duplicates));

        if (intake.Gaps > 0)
            return Finish(Store(researchLog, hash, Empty(DataGap, intake), inserted, duplicates));

        var signals = ResearchStrategies.Signals(spec, intake.Accepted);
        var ready = signals.Where(signal => signal.Status == ResearchStrategies.Ready).ToArray();
        var tape = ready.Select(signal => new PaperTapeRow(
            signal.SignalKey,
            signal.FillTime!.Value,
            signal.Symbol,
            signal.Side,
            config.Quantity,
            signal.FillPrice!.Value,
            config.Confidence)).ToArray();

        var paper = PaperSession.Run(tape, config.Paper, paperLog, killSwitch);
        var decisions = new List<ResearchRiskDecision>();
        foreach (var row in tape)
        {
            var receipt = paperLog.Find(row.OperationKey);
            if (receipt is null)
                continue;
            var allowed = string.Equals(receipt.Status, "paper_filled", StringComparison.Ordinal);
            decisions.Add(new ResearchRiskDecision(row.OperationKey, allowed, allowed ? "" : receipt.Result, row.OperationKey));
        }

        if (paper.Status == PaperSession.Killed)
            return Finish(new ResearchReport(Killed, false, paper.PaperFilled, inserted, duplicates, hash, Snapshot(Killed, intake, signals, paper, config.StartingEquity, decisions)));

        var status = paper.Status == PaperSession.LiveBlocked ? LiveBlocked : Completed;
        return Finish(Store(researchLog, hash, Snapshot(status, intake, signals, paper, config.StartingEquity, decisions), inserted, duplicates, paper.PaperFilled));
    }

    public static string Hash(CandleIntake intake, ResearchConfig config)
        => Hash(intake, config, ResearchStrategies.Resolve(null, null));

    public static string Hash(CandleIntake intake, ResearchConfig config, StrategySpec strategy)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var candle in intake.Accepted)
        {
            builder.Append(candle.OpenTime.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)).Append('|');
            builder.Append(candle.Close.ToString(CultureInfo.InvariantCulture)).Append('|');
            builder.Append(candle.Open.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        builder.Append(intake.Gaps.ToString(CultureInfo.InvariantCulture)).Append('|');
        builder.Append(config.Quantity.ToString(CultureInfo.InvariantCulture)).Append('|');
        builder.Append(config.Confidence.ToString(CultureInfo.InvariantCulture)).Append('|');
        builder.Append(config.StartingEquity.ToString(CultureInfo.InvariantCulture)).Append('|');
        builder.Append(config.Paper.SlippageBps.ToString(CultureInfo.InvariantCulture)).Append('|');
        builder.Append(config.Paper.FeeRate.ToString(CultureInfo.InvariantCulture)).Append('|');
        builder.Append(config.Paper.MinimumFee.ToString(CultureInfo.InvariantCulture)).Append('|');
        builder.Append(config.Paper.SessionNotionalCap.ToString(CultureInfo.InvariantCulture)).Append('|');
        builder.Append(config.Paper.Leverage.ToString(CultureInfo.InvariantCulture)).Append('|');
        builder.Append(config.Paper.Limits.MaxNotional.ToString(CultureInfo.InvariantCulture)).Append('|');
        builder.Append(config.Paper.Limits.MaxLeverage.ToString(CultureInfo.InvariantCulture)).Append('|');
        builder.Append(config.Paper.Limits.MinConfidence.ToString(CultureInfo.InvariantCulture)).Append('|');
        builder.Append(config.Paper.Live ? "live" : "paper");
        if (!strategy.IsBaselineSma)
            builder.Append('|').Append(strategy.Id).Append('|').Append(strategy.Canonical);
        return ResearchHash.Sha256(builder.ToString());
    }

    private static ResearchReport Store(
        IResearchLog researchLog,
        string hash,
        ResearchSnapshot snapshot,
        int inserted,
        int duplicates,
        int newFills = 0)
    {
        var result = JsonSerializer.Serialize(snapshot, JsonOptions);
        researchLog.TryAdd(new ResearchStoredSession(hash, hash, snapshot.Status, result));
        return new ResearchReport(snapshot.Status, false, newFills, inserted, duplicates, hash, snapshot);
    }

    private static ResearchSnapshot Empty(string status, CandleIntake intake)
        => new(
            status,
            intake.Accepted.Count,
            intake.Duplicates,
            intake.Stale,
            intake.FutureOrIncomplete,
            intake.Gaps,
            0,
            0,
            0,
            0,
            0m,
            0m,
            null,
            PaperSession.NoClosedPosition,
            null,
            null,
            DrawdownUnavailable,
            [],
            [],
            []);

    private static ResearchSnapshot Snapshot(
        string status,
        CandleIntake intake,
        IReadOnlyList<ResearchSignal> signals,
        PaperSessionReport paper,
        decimal startingEquity,
        IReadOnlyList<ResearchRiskDecision> decisions)
    {
        var fills = paper.NewFills.Select(fill => new StoredFill(
            fill.OperationKey,
            fill.Side,
            fill.TapePrice,
            fill.ExecutionPrice,
            fill.Quantity,
            fill.Fee,
            fill.SlippageQuote,
            fill.NetCash,
            fill.OperationKey)).ToArray();
        decimal equity = startingEquity;
        decimal peak = startingEquity;
        decimal maxDrawdown = 0m;
        foreach (var fill in fills)
        {
            equity += fill.NetCash;
            if (equity > peak)
                peak = equity;
            var drawdown = peak - equity;
            if (drawdown > maxDrawdown)
                maxDrawdown = drawdown;
        }

        var closedBook = paper.PnlStatus == PaperSession.Realized && fills.Length > 0;
        decimal? drawdownValue = closedBook ? maxDrawdown : null;
        decimal? fraction = drawdownValue is null || peak <= 0m ? null : drawdownValue.Value / peak;
        var net = paper.PnlStatus == PaperSession.Realized ? paper.RealizedPnl : null;
        return new ResearchSnapshot(
            status,
            intake.Accepted.Count,
            intake.Duplicates,
            intake.Stale,
            intake.FutureOrIncomplete,
            intake.Gaps,
            signals.Count,
            signals.Count(signal => signal.Status == ResearchStrategies.AwaitingNextOpen),
            paper.RiskRefused,
            paper.PaperFilled,
            paper.Fees,
            paper.SlippageQuote,
            net,
            paper.PnlStatus,
            drawdownValue,
            fraction,
            closedBook ? CashDrawdown : DrawdownUnavailable,
            signals.Select(signal => new StoredSignal(
                signal.SignalKey,
                signal.Symbol,
                signal.Side,
                signal.SignalTime,
                signal.FillTime,
                signal.FillPrice,
                signal.Status,
                signal.StrategyId,
                signal.StrategyVersion,
                signal.Timeframe,
                signal.CandleOpenTime)).ToArray(),
            fills,
            decisions);
    }
}
