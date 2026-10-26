namespace KitPlatform.Packs.PersonalIntelligence.Trading;

public sealed record ResearchRunRecord(
    string RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    string Status,
    string? Error,
    int CandlesInserted,
    int CandlesSkipped,
    int Signals,
    int RiskDecisions,
    int Operations,
    int Fills,
    bool Replayed,
    bool PnlRecomputed)
{
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string ReplayedStatus = "replayed";
    public const string Unknown = "unknown";

    public static ResearchRunRecord From(ResearchReport report, DateTimeOffset started)
    {
        var status = report.Replayed
            ? ReplayedStatus
            : report.Status switch
            {
                ResearchSession.Completed => Completed,
                ResearchSession.Killed or ResearchSession.LiveBlocked => Failed,
                _ => Unknown,
            };
        return new ResearchRunRecord(
            Guid.NewGuid().ToString("N"),
            started,
            DateTimeOffset.UtcNow,
            status,
            null,
            report.CandlesInsertedThisRun,
            report.DuplicateCandlesThisRun,
            report.Snapshot.Signals,
            report.Snapshot.RiskDecisions.Count,
            report.Snapshot.Fills.Count + report.Snapshot.RiskDecisions.Count(decision => !decision.Allowed),
            report.NewPaperFills,
            report.Replayed,
            !report.Replayed && report.Snapshot.PnlStatus == PaperSession.Realized);
    }

    public static ResearchRunRecord Failure(DateTimeOffset started, string error)
    {
        var text = error.Length <= 500 ? error : error[..500];
        return new ResearchRunRecord(
            Guid.NewGuid().ToString("N"),
            started,
            DateTimeOffset.UtcNow,
            Failed,
            text,
            0,
            0,
            0,
            0,
            0,
            0,
            false,
            false);
    }
}

public interface IResearchRunLog
{
    void Record(ResearchRunRecord run);

    IReadOnlyList<ResearchRunRecord> Runs { get; }
}

public sealed class MemoryResearchRunLog : IResearchRunLog
{
    private readonly List<ResearchRunRecord> _runs = [];

    public void Record(ResearchRunRecord run) => _runs.Add(run);

    public IReadOnlyList<ResearchRunRecord> Runs => _runs;
}

public static class ResearchTradingSql
{
    public const string InsertSignal =
        """
        INSERT INTO research_trading.signal
            (signal_key, session_key, symbol, side, signal_time, fill_time, fill_price, status,
             strategy_id, strategy_version, timeframe, candle_open_time)
        VALUES
            (@key, @session, @symbol, @side, @signal_time, @fill_time, @fill_price, @status,
             @strategy_id, @strategy_version, @timeframe, @candle_open)
        ON CONFLICT (signal_key) DO UPDATE
        SET fill_time = EXCLUDED.fill_time,
            fill_price = EXCLUDED.fill_price,
            status = EXCLUDED.status
        WHERE research_trading.signal.status = 'awaiting_next_open'
          AND EXCLUDED.status = 'ready'
        """;

    public const string InsertRisk =
        """
        INSERT INTO research_trading.risk_decision
            (operation_key, session_key, allowed, reasons, signal_key)
        VALUES (@key, @session, @allowed, @reasons, @signal)
        ON CONFLICT (operation_key) DO NOTHING
        """;

    public const string InsertFill =
        """
        INSERT INTO research_trading.paper_fill
            (operation_key, session_key, side, tape_price, execution_price, quantity, fee, slippage_quote, net_cash, signal_key)
        VALUES
            (@key, @session, @side, @tape, @execution, @quantity, @fee, @slippage, @net, @signal)
        ON CONFLICT (operation_key) DO NOTHING
        """;

    public const string LinkOperation =
        """
        UPDATE research_trading.paper_operation
        SET signal_key = @signal
        WHERE operation_key = @key
          AND signal_key IS NULL
          AND operation_key = @signal
        """;

    public const string InsertRun =
        """
        INSERT INTO research_trading.run_history
            (run_id, started_at, ended_at, status, error, candles_inserted, candles_skipped,
             signals, risk_decisions, operations, fills, replayed, pnl_recomputed)
        VALUES
            (@run_id, @started, @ended, @status, @error, @candles_inserted, @candles_skipped,
             @signals, @risk_decisions, @operations, @fills, @replayed, @pnl_recomputed)
        ON CONFLICT (run_id) DO NOTHING
        """;

    public static string? LinkedSignalKey(ResearchSnapshot snapshot, string? operationKey)
    {
        if (string.IsNullOrWhiteSpace(operationKey))
            return null;
        return snapshot.SignalsRecorded.Any(signal => string.Equals(signal.SignalKey, operationKey, StringComparison.Ordinal))
            ? operationKey
            : null;
    }
}
