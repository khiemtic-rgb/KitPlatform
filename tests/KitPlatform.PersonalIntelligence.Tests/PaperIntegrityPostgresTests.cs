using KitPlatform.Packs.PersonalIntelligence.ResearchHost;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Npgsql;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class PaperIntegrityPostgresTests : IAsyncLifetime
{
    private const string HistoricalKey = "integrity-002-hist:BTCUSDT:1h:1:BUY";
    private string _connection = "";

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("KIT_PI_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("KIT_PI_TEST_CONNECTION is required. PostgreSQL integration is NOT VERIFIED without the isolated database.");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (builder.Database is "kitplatform" or "kitplatform_test" or "pharmacore" or "postgres" or "trading_research" or "novixa_prod")
            throw new InvalidOperationException("Refusing a shared or operating database.");
        if (builder.Port == 5432)
            throw new InvalidOperationException("Refusing the shared PostgreSQL port.");
        // KIT_PI_TEST_CONNECTION names the Personal Intelligence database. The research store accepts only trading_agent.
        builder.Database = "trading_agent";

        await using var conn = new NpgsqlConnection(builder.ConnectionString);
        await conn.OpenAsync();
        var database = await Scalar<string>(conn, "SELECT current_database()");
        var port = Convert.ToInt32(await Scalar<object>(conn, "SELECT inet_server_port()"));
        if (!string.Equals(database, "trading_agent", StringComparison.Ordinal) || port != 5433)
            throw new InvalidOperationException("Connected database does not match the isolated trading_agent database.");
        var forbidden = Convert.ToInt32(await Scalar<object>(conn, "SELECT COUNT(*) FROM pg_database WHERE datname IN ('trading_research', 'novixa_prod')"));
        if (forbidden != 0)
            throw new InvalidOperationException("Refusing a cluster that contains an operating database.");
        _connection = builder.ConnectionString;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void Round_trip_is_stored_once_and_replay_does_not_add_a_fill()
    {
        var stamp = DateTimeOffset.Parse("2031-01-01T00:00:00Z").AddMinutes(Random.Shared.Next(0, 200_000));
        using var store = new SqlResearchStore(_connection);
        var first = ResearchSession.Run(Intake(Series(stamp)), Config(), store, store);
        store.WriteDetails(first.SessionKey, first.Snapshot);
        var completed = ResearchRunRecord.From(first, DateTimeOffset.UtcNow);
        store.WriteRun(completed);
        var before = Count("SELECT COUNT(*) FROM research_trading.paper_fill WHERE signal_key = ANY(@keys)", Keys(first));

        var second = ResearchSession.Run(Intake(Series(stamp)), Config(), store, store);
        store.WriteDetails(second.SessionKey, second.Snapshot);
        var replay = ResearchRunRecord.From(second, DateTimeOffset.UtcNow);
        store.WriteRun(replay);
        var after = Count("SELECT COUNT(*) FROM research_trading.paper_fill WHERE signal_key = ANY(@keys)", Keys(first));

        Assert.Equal(2, first.NewPaperFills);
        Assert.Equal(-0.0004m, first.Snapshot.NetRealizedPnl);
        Assert.Equal(PaperSession.Realized, first.Snapshot.PnlStatus);
        Assert.True(second.Replayed);
        Assert.Equal(0, second.NewPaperFills);
        Assert.Equal(before, after);
        Assert.Equal(2, after);

        var chain = ReadChain(Keys(first));
        Assert.Equal(2, chain.Count);
        Assert.Contains(chain, row => row.Side == "BUY" && row.EffectReady && row.StrategyId == SmaCrossStrategy.Id && row.StrategyVersion == SmaCrossStrategy.Version);
        Assert.Contains(chain, row => row.Side == "SELL" && row.EffectReady && row.OperationSignal == row.SignalKey);
        Assert.Equal(ResearchRunRecord.Completed, StatusOf(completed.RunId));
        Assert.True(Recomputed(completed.RunId));
        Assert.Equal(ResearchRunRecord.ReplayedStatus, StatusOf(replay.RunId));
        Assert.False(Recomputed(replay.RunId));
    }

    [Fact]
    public void Partial_sell_is_stored_without_realized_pnl()
    {
        var buy = "integrity-002-partial:" + Guid.NewGuid().ToString("N") + ":BUY";
        var sell = "integrity-002-partial:" + Guid.NewGuid().ToString("N") + ":SELL";
        var paper = PaperSession.Run(PaperTape.Read(
            PaperTape.Header + "\n" +
            $"{buy},2026-02-01T00:00:00Z,ETHUSDT,BUY,1,100,0.9\n" +
            $"{sell},2026-02-01T01:00:00Z,ETHUSDT,SELL,0.4,100,0.9\n"), Wide(), new MemoryPaperOperationLog());
        Assert.Equal(PaperSession.PartialClose, paper.PnlStatus);
        Assert.Null(paper.RealizedPnl);

        var signals = paper.NewFills.Select(fill => new StoredSignal(fill.OperationKey, fill.Symbol, fill.Side, DateTimeOffset.Parse("2026-02-01T00:00:00Z"), DateTimeOffset.Parse("2026-02-01T01:00:00Z"), fill.TapePrice, SmaCrossStrategy.Ready, SmaCrossStrategy.Id, SmaCrossStrategy.Version, "1h", DateTimeOffset.Parse("2026-02-01T00:00:00Z"))).ToArray();
        var fills = paper.NewFills.Select(fill => new StoredFill(fill.OperationKey, fill.Side, fill.TapePrice, fill.ExecutionPrice, fill.Quantity, fill.Fee, fill.SlippageQuote, fill.NetCash, fill.OperationKey)).ToArray();
        var decisions = paper.NewFills.Select(fill => new ResearchRiskDecision(fill.OperationKey, true, "", fill.OperationKey)).ToArray();
        var snapshot = Snapshot(paper.PnlStatus, null, signals, fills, decisions);
        using var store = new SqlResearchStore(_connection);
        foreach (var fill in paper.NewFills)
            ((IPaperOperationLog)store).TryAdd(new PaperOperationReceipt(fill.OperationKey, "partial-hash", "paper_filled", "partial"));
        store.WriteDetails("integrity-002-partial-session", snapshot);
        var run = new ResearchRunRecord(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, ResearchRunRecord.Completed, null, 0, 0, 2, 2, 2, 2, false, false);
        store.WriteRun(run);

        var chain = ReadChain([buy, sell]);
        Assert.Equal(2, chain.Count);
        Assert.Contains(chain, row => row.Side == "SELL" && row.Quantity == 0.4m);
        Assert.False(Recomputed(run.RunId));
        Assert.Equal(ResearchRunRecord.Completed, StatusOf(run.RunId));
    }

    [Fact]
    public void Missing_signal_key_stays_unlinked_and_historical_version_stays_null()
    {
        SeedHistoricalRow();
        var orphan = "integrity-002-orphan:" + Guid.NewGuid().ToString("N");
        var snapshot = Snapshot(
            PaperSession.NoClosedPosition,
            null,
            [],
            [new StoredFill(orphan, "SELL", 100m, 99.9m, 0.001m, 0.0000999m, 0.0001m, 0.0998001m, null)],
            []);
        using var store = new SqlResearchStore(_connection);
        ((IPaperOperationLog)store).TryAdd(new PaperOperationReceipt(orphan, "orphan-hash", "paper_filled", "orphan"));
        store.WriteDetails("integrity-002-orphan-session", snapshot);
        store.WriteDetails("integrity-002-orphan-session", snapshot);

        Assert.Equal(1, Count("SELECT COUNT(*) FROM research_trading.paper_fill WHERE operation_key = @key", orphan));
        Assert.True(Flag("SELECT signal_key IS NULL FROM research_trading.paper_fill WHERE operation_key = @key", orphan));
        Assert.True(Flag("SELECT signal_key IS NULL FROM research_trading.paper_operation WHERE operation_key = @key", orphan));
        Assert.Equal(0, Count("SELECT COUNT(*) FROM research_trading.signal s JOIN research_trading.paper_fill f ON f.signal_key = s.signal_key WHERE f.operation_key = @key", orphan));

        Assert.True(Flag("SELECT strategy_id IS NULL AND strategy_version IS NULL FROM research_trading.signal WHERE signal_key = @key", HistoricalKey));
        Assert.True(Flag("SELECT signal_key IS NULL FROM research_trading.risk_decision WHERE operation_key = @key", HistoricalKey));
        Assert.True(Flag("SELECT signal_key IS NULL FROM research_trading.paper_fill WHERE operation_key = @key", HistoricalKey));
        Assert.True(Flag("SELECT signal_key IS NULL FROM research_trading.paper_operation WHERE operation_key = @key", HistoricalKey));
        Assert.Equal("awaiting_next_open", Text("SELECT status FROM research_trading.signal WHERE signal_key = @key", HistoricalKey));
    }

    private List<ChainRow> ReadChain(IReadOnlyList<string> keys)
    {
        using var conn = new NpgsqlConnection(_connection);
        conn.Open();
        using var command = new NpgsqlCommand(
            """
            SELECT s.signal_key, s.side, s.strategy_id, s.strategy_version, s.status,
                   f.quantity, r.signal_key, o.signal_key
            FROM research_trading.signal s
            JOIN research_trading.risk_decision r ON r.signal_key = s.signal_key AND r.operation_key = s.signal_key
            JOIN research_trading.paper_fill f ON f.signal_key = s.signal_key AND f.operation_key = s.signal_key
            JOIN research_trading.paper_operation o ON o.signal_key = s.signal_key AND o.operation_key = s.signal_key
            WHERE s.signal_key = ANY(@keys)
            """,
            conn);
        command.Parameters.AddWithValue("keys", keys.ToArray());
        using var reader = command.ExecuteReader();
        var rows = new List<ChainRow>();
        while (reader.Read())
        {
            rows.Add(new ChainRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4) == SmaCrossStrategy.Ready,
                reader.GetDecimal(5),
                reader.GetString(7)));
        }

        return rows;
    }

    private void SeedHistoricalRow()
    {
        using var conn = new NpgsqlConnection(_connection);
        conn.Open();
        using var command = new NpgsqlCommand(
            """
            INSERT INTO research_trading.signal (signal_key, session_key, symbol, side, signal_time, status)
            VALUES (@key, 'integrity-002-hist', 'BTCUSDT', 'BUY', TIMESTAMPTZ '2026-01-01T00:00:00Z', 'awaiting_next_open')
            ON CONFLICT (signal_key) DO NOTHING;

            INSERT INTO research_trading.risk_decision (operation_key, session_key, allowed, reasons)
            VALUES (@key, 'integrity-002-hist', TRUE, '')
            ON CONFLICT (operation_key) DO NOTHING;

            INSERT INTO research_trading.paper_fill (
                operation_key, session_key, side, tape_price, execution_price, quantity, fee, slippage_quote, net_cash)
            VALUES (@key, 'integrity-002-hist', 'BUY', 100, 100, 0.001, 0, 0, -0.1)
            ON CONFLICT (operation_key) DO NOTHING;

            INSERT INTO research_trading.paper_operation (operation_key, payload_hash, status, result)
            VALUES (@key, 'hist', 'paper_filled', 'historical')
            ON CONFLICT (operation_key) DO NOTHING;
            """,
            conn);
        command.Parameters.AddWithValue("key", HistoricalKey);
        command.ExecuteNonQuery();
    }

    private int Count(string sql, IReadOnlyList<string> keys)
    {
        using var conn = new NpgsqlConnection(_connection);
        conn.Open();
        using var command = new NpgsqlCommand(sql, conn);
        command.Parameters.AddWithValue("keys", keys.ToArray());
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private int Count(string sql, string key) => Convert.ToInt32(Scalar(sql, key));

    private bool Flag(string sql, string key)
    {
        var value = Scalar(sql, key);
        return value is bool flag && flag;
    }

    private string Text(string sql, string key) => (string)Scalar(sql, key)!;

    private string StatusOf(string runId) => (string)Scalar("SELECT status FROM research_trading.run_history WHERE run_id = @key", runId)!;

    private bool Recomputed(string runId) => (bool)Scalar("SELECT pnl_recomputed FROM research_trading.run_history WHERE run_id = @key", runId)!;

    private object? Scalar(string sql, string key)
    {
        using var conn = new NpgsqlConnection(_connection);
        conn.Open();
        using var command = new NpgsqlCommand(sql, conn);
        command.Parameters.AddWithValue("key", key);
        return command.ExecuteScalar();
    }

    private static async Task<T> Scalar<T>(NpgsqlConnection conn, string sql)
    {
        await using var command = new NpgsqlCommand(sql, conn);
        var value = await command.ExecuteScalarAsync();
        return (T)value!;
    }

    private static ResearchSnapshot Snapshot(
        string pnlStatus,
        decimal? pnl,
        IReadOnlyList<StoredSignal> signals,
        IReadOnlyList<StoredFill> fills,
        IReadOnlyList<ResearchRiskDecision> decisions)
        => new(
            ResearchSession.Completed,
            0, 0, 0, 0, 0,
            signals.Count,
            0,
            0,
            fills.Count,
            0m,
            0m,
            pnl,
            pnlStatus,
            null,
            null,
            ResearchSession.DrawdownUnavailable,
            signals,
            fills,
            decisions);

    private static IReadOnlyList<string> Keys(ResearchReport report) => report.Snapshot.Fills.Select(fill => fill.OperationKey).ToArray();

    private static ResearchConfig Config()
        => new(0.001m, 0.8m, 10_000m, new PaperSessionConfig(10m, 0.001m, 0m, 1m, new RiskLimits(1_000m, 1m, 0.5m), 5_000m, false));

    private static PaperSessionConfig Wide()
        => new(100m, 0.01m, 0m, 1m, new RiskLimits(100_000m, 2m, 0.5m), 1_000_000m, false);

    private static CandleIntake Intake(IReadOnlyList<ResearchCandle> candles)
        => ResearchCandles.Classify(candles, candles[^1].CloseTime, TimeSpan.FromDays(30));

    private static List<ResearchCandle> Series(DateTimeOffset start)
    {
        var bars = new List<ResearchCandle>();
        for (var hour = 0; hour < 30; hour++)
        {
            var close = hour == 20 ? 200m : hour > 20 ? 1m : 100m;
            var openTime = start.AddHours(hour);
            bars.Add(new ResearchCandle("integrity-002", "BTCUSDT", "1h", openTime, openTime.AddHours(1).AddMilliseconds(-1), 100m, Math.Max(100m, close), Math.Min(100m, close), close, $"integrity-002:{hour}:{close}"));
        }

        return bars;
    }

    private sealed record ChainRow(string SignalKey, string Side, string StrategyId, string StrategyVersion, bool EffectReady, decimal Quantity, string OperationSignal);
}
