namespace KitPlatform.Packs.PersonalIntelligence.ResearchDashboard;

public static class ResearchReadSql
{
    public const string CandleCount = "SELECT COUNT(*) FROM research_trading.candle";

    public const string Candles =
        """
        SELECT symbol, interval_code, open_time, close_time, open_price, high_price, low_price, close_price, ingested_at
        FROM research_trading.candle
        ORDER BY open_time DESC
        LIMIT 200
        """;

    public const string Signals =
        """
        SELECT signal_key, symbol, side, signal_time, fill_time, fill_price, status
        FROM research_trading.signal
        ORDER BY signal_time DESC
        """;

    public const string Decisions =
        """
        SELECT operation_key, allowed, reasons
        FROM research_trading.risk_decision
        ORDER BY operation_key
        """;

    public const string Fills =
        """
        SELECT operation_key, side, tape_price, execution_price, quantity, fee, slippage_quote, net_cash
        FROM research_trading.paper_fill
        ORDER BY operation_key
        """;

    public const string Runs =
        """
        SELECT health_id, recorded_at, status, detail
        FROM research_trading.health
        ORDER BY recorded_at DESC, health_id DESC
        LIMIT 100
        """;

    public const string Sessions =
        """
        SELECT created_at, status, result
        FROM research_trading.session_result
        ORDER BY created_at DESC
        LIMIT 20
        """;

    public static IReadOnlyList<string> Statements { get; } =
    [
        CandleCount,
        Candles,
        Signals,
        Decisions,
        Fills,
        Runs,
        Sessions,
    ];
}
