using System.Text.Json;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Npgsql;

namespace KitPlatform.Packs.PersonalIntelligence.ResearchHost;

public sealed record PublicKlineResponse(string Body, string? UsedWeight1m);

public sealed record PublicKlinePage(string Body, string? UsedWeight1m, int Requests);

public sealed class BinancePublicKlineClient : IDisposable
{
    private readonly HttpClient _http;

    public BinancePublicKlineClient()
        : this(new HttpClientHandler { AllowAutoRedirect = false })
    {
    }

    public BinancePublicKlineClient(HttpMessageHandler handler)
    {
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
    }

    public Task<PublicKlineResponse> ReadAsync(string symbol, int limit, CancellationToken cancellationToken)
        => ReadPageAsync(symbol, limit, null, cancellationToken);

    public async Task<PublicKlinePage> ReadHistoryAsync(string symbol, int bars, int maxRequests, CancellationToken cancellationToken)
    {
        if (bars < 1 || bars > 1500)
            throw new InvalidOperationException("Historical bar count must be from 1 to 1500.");
        if (maxRequests < 1 || maxRequests > 3)
            throw new InvalidOperationException("Historical page count must be from 1 to 3.");
        var rows = new Dictionary<long, JsonElement>();
        string? weight = null;
        var requests = 0;
        long? endTime = null;
        var remaining = bars;
        while (remaining > 0 && requests < maxRequests)
        {
            var limit = Math.Min(PublicMarketRequest.MaxLimit, remaining);
            requests++;
            var page = await ReadPageAsync(symbol, limit, endTime, cancellationToken);
            weight = page.UsedWeight1m ?? weight;
            using var document = JsonDocument.Parse(page.Body);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Kline payload must be a JSON array.");
            long? oldest = null;
            var count = 0;
            foreach (var row in document.RootElement.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 1)
                    continue;
                var open = row[0].GetInt64();
                rows[open] = row.Clone();
                oldest = oldest is null || open < oldest ? open : oldest;
                count++;
            }

            if (count < limit || oldest is null)
                break;
            remaining -= count;
            endTime = oldest.Value - 1;
        }

        var merged = rows.OrderBy(row => row.Key).Select(row => row.Value.GetRawText());
        return new PublicKlinePage("[" + string.Join(',', merged) + "]", weight, requests);
    }

    async Task<PublicKlineResponse> ReadPageAsync(string symbol, int limit, long? endTime, CancellationToken cancellationToken)
    {
        try
        {
            return await SendAsync(symbol, limit, endTime, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return await SendAsync(symbol, limit, endTime, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return await SendAsync(symbol, limit, endTime, cancellationToken);
        }
    }

    async Task<PublicKlineResponse> SendAsync(string symbol, int limit, long? endTime, CancellationToken cancellationToken)
    {
        var uri = PublicMarketRequest.Klines(symbol, ResearchCandles.Interval, limit, endTime);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await _http.SendAsync(request, cancellationToken);
        if ((int)response.StatusCode is 418 or 429)
            throw new HttpRequestException($"Public kline request was rate limited with HTTP {(int)response.StatusCode}.");
        if ((int)response.StatusCode is < 200 or > 299)
            throw new InvalidOperationException($"Public kline request failed with HTTP {(int)response.StatusCode}.");
        var weight = response.Headers.TryGetValues("x-mbx-used-weight-1m", out var values)
            ? values.FirstOrDefault()
            : null;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (body.Length > 2_000_000)
            throw new InvalidOperationException("Public kline payload exceeds the research size limit.");
        return new PublicKlineResponse(body, weight);
    }

    public void Dispose() => _http.Dispose();
}

public sealed class FileResearchState : IResearchLog, IPaperOperationLog
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;
    private readonly Dictionary<string, ResearchCandle> _candles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ResearchStoredSession> _sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PaperOperationReceipt> _operations = new(StringComparer.Ordinal);

    private FileResearchState(string path) => _path = path;

    public static FileResearchState Load(string path)
    {
        var state = new FileResearchState(path);
        if (!File.Exists(path))
            return state;
        var document = JsonSerializer.Deserialize<FileBody>(File.ReadAllText(path), JsonOptions) ?? new FileBody();
        foreach (var candle in document.Candles)
            state._candles[MemoryResearchLog.CandleKey(candle)] = candle;
        foreach (var session in document.Sessions)
            state._sessions[session.SessionKey] = session;
        foreach (var operation in document.Operations)
            state._operations[operation.OperationKey] = operation;
        return state;
    }

    public int StoreCandles(IReadOnlyList<ResearchCandle> candles)
    {
        var inserted = 0;
        foreach (var candle in candles)
        {
            if (_candles.TryAdd(MemoryResearchLog.CandleKey(candle), candle))
                inserted++;
        }

        return inserted;
    }

    ResearchStoredSession? IResearchLog.Find(string sessionKey)
        => _sessions.TryGetValue(sessionKey, out var session) ? session : null;

    bool IResearchLog.TryAdd(ResearchStoredSession session)
        => _sessions.TryAdd(session.SessionKey, session);

    PaperOperationReceipt? IPaperOperationLog.Find(string operationKey)
        => _operations.TryGetValue(operationKey, out var receipt) ? receipt : null;

    bool IPaperOperationLog.TryAdd(PaperOperationReceipt receipt)
        => _operations.TryAdd(receipt.OperationKey, receipt);

    public void Save()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        var body = new FileBody
        {
            Candles = _candles.Values.ToList(),
            Sessions = _sessions.Values.ToList(),
            Operations = _operations.Values.ToList(),
        };
        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(body, JsonOptions) + Environment.NewLine);
        File.Move(temporary, _path, overwrite: true);
    }

    private sealed class FileBody
    {
        public List<ResearchCandle> Candles { get; set; } = [];
        public List<ResearchStoredSession> Sessions { get; set; } = [];
        public List<PaperOperationReceipt> Operations { get; set; } = [];
    }
}

public sealed class SqlResearchStore : IResearchLog, IPaperOperationLog, IDisposable
{
    private readonly NpgsqlConnection _connection;

    public SqlResearchStore(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("The connection file is empty.");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.Database, "trading_agent", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"The research host accepts only database trading_agent and refuses database {builder.Database}.");
        _connection = new NpgsqlConnection(builder.ConnectionString);
        _connection.Open();
    }

    public int StoreCandles(IReadOnlyList<ResearchCandle> candles)
    {
        var inserted = 0;
        foreach (var candle in candles)
        {
            using var command = new NpgsqlCommand(
                """
                INSERT INTO research_trading.candle
                    (source, symbol, interval_code, open_time, close_time, open_price, high_price, low_price, close_price, raw_payload, payload_hash)
                VALUES
                    (@source, @symbol, @interval, @open_time, @close_time, @open_price, @high_price, @low_price, @close_price, @raw_payload, @payload_hash)
                ON CONFLICT (source, symbol, interval_code, open_time) DO NOTHING
                """,
                _connection);
            command.Parameters.AddWithValue("source", candle.Source);
            command.Parameters.AddWithValue("symbol", candle.Symbol);
            command.Parameters.AddWithValue("interval", candle.Interval);
            command.Parameters.AddWithValue("open_time", candle.OpenTime);
            command.Parameters.AddWithValue("close_time", candle.CloseTime);
            command.Parameters.AddWithValue("open_price", candle.Open);
            command.Parameters.AddWithValue("high_price", candle.High);
            command.Parameters.AddWithValue("low_price", candle.Low);
            command.Parameters.AddWithValue("close_price", candle.Close);
            command.Parameters.AddWithValue("raw_payload", candle.RawPayload);
            command.Parameters.AddWithValue("payload_hash", ResearchHash.Sha256(candle.RawPayload));
            inserted += command.ExecuteNonQuery();
        }

        return inserted;
    }

    ResearchStoredSession? IResearchLog.Find(string sessionKey)
    {
        using var command = new NpgsqlCommand(
            """
            SELECT payload_hash, status, result
            FROM research_trading.session_result
            WHERE session_key = @key
            """,
            _connection);
        command.Parameters.AddWithValue("key", sessionKey);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;
        return new ResearchStoredSession(sessionKey, reader.GetString(0), reader.GetString(1), reader.GetString(2));
    }

    bool IResearchLog.TryAdd(ResearchStoredSession session)
    {
        using var command = new NpgsqlCommand(
            """
            INSERT INTO research_trading.session_result (session_key, payload_hash, status, result)
            VALUES (@key, @hash, @status, @result)
            ON CONFLICT (session_key) DO NOTHING
            """,
            _connection);
        command.Parameters.AddWithValue("key", session.SessionKey);
        command.Parameters.AddWithValue("hash", session.PayloadHash);
        command.Parameters.AddWithValue("status", session.Status);
        command.Parameters.AddWithValue("result", session.Result);
        return command.ExecuteNonQuery() == 1;
    }

    PaperOperationReceipt? IPaperOperationLog.Find(string operationKey)
    {
        using var command = new NpgsqlCommand(
            """
            SELECT payload_hash, status, result
            FROM research_trading.paper_operation
            WHERE operation_key = @key
            """,
            _connection);
        command.Parameters.AddWithValue("key", operationKey);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;
        return new PaperOperationReceipt(operationKey, reader.GetString(0), reader.GetString(1), reader.GetString(2));
    }

    bool IPaperOperationLog.TryAdd(PaperOperationReceipt receipt)
    {
        using var command = new NpgsqlCommand(
            """
            INSERT INTO research_trading.paper_operation (operation_key, payload_hash, status, result)
            VALUES (@key, @hash, @status, @result)
            ON CONFLICT (operation_key) DO NOTHING
            """,
            _connection);
        command.Parameters.AddWithValue("key", receipt.OperationKey);
        command.Parameters.AddWithValue("hash", receipt.PayloadHash);
        command.Parameters.AddWithValue("status", receipt.Status);
        command.Parameters.AddWithValue("result", receipt.Result);
        return command.ExecuteNonQuery() == 1;
    }

    public void WriteDetails(string sessionKey, ResearchSnapshot snapshot)
    {
        foreach (var signal in snapshot.SignalsRecorded)
        {
            using var command = new NpgsqlCommand(ResearchTradingSql.InsertSignal, _connection);
            command.Parameters.AddWithValue("key", signal.SignalKey);
            command.Parameters.AddWithValue("session", sessionKey);
            command.Parameters.AddWithValue("symbol", signal.Symbol);
            command.Parameters.AddWithValue("side", signal.Side);
            command.Parameters.AddWithValue("signal_time", signal.SignalTime);
            command.Parameters.AddWithValue("fill_time", (object?)signal.FillTime ?? DBNull.Value);
            command.Parameters.AddWithValue("fill_price", (object?)signal.FillPrice ?? DBNull.Value);
            command.Parameters.AddWithValue("status", signal.Status);
            command.Parameters.AddWithValue("strategy_id", (object?)signal.StrategyId ?? DBNull.Value);
            command.Parameters.AddWithValue("strategy_version", (object?)signal.StrategyVersion ?? DBNull.Value);
            command.Parameters.AddWithValue("timeframe", (object?)signal.Timeframe ?? DBNull.Value);
            command.Parameters.AddWithValue("candle_open", (object?)signal.CandleOpenTime ?? DBNull.Value);
            command.ExecuteNonQuery();
        }

        foreach (var decision in snapshot.RiskDecisions)
        {
            using var command = new NpgsqlCommand(ResearchTradingSql.InsertRisk, _connection);
            command.Parameters.AddWithValue("key", decision.OperationKey);
            command.Parameters.AddWithValue("session", sessionKey);
            command.Parameters.AddWithValue("allowed", decision.Allowed);
            command.Parameters.AddWithValue("reasons", decision.Reasons);
            command.Parameters.AddWithValue("signal", (object?)ResearchTradingSql.LinkedSignalKey(snapshot, decision.SignalKey) ?? DBNull.Value);
            command.ExecuteNonQuery();
        }

        foreach (var fill in snapshot.Fills)
        {
            var signalKey = ResearchTradingSql.LinkedSignalKey(snapshot, fill.SignalKey);
            using var command = new NpgsqlCommand(ResearchTradingSql.InsertFill, _connection);
            command.Parameters.AddWithValue("key", fill.OperationKey);
            command.Parameters.AddWithValue("session", sessionKey);
            command.Parameters.AddWithValue("side", fill.Side);
            command.Parameters.AddWithValue("tape", fill.TapePrice);
            command.Parameters.AddWithValue("execution", fill.ExecutionPrice);
            command.Parameters.AddWithValue("quantity", fill.Quantity);
            command.Parameters.AddWithValue("fee", fill.Fee);
            command.Parameters.AddWithValue("slippage", fill.SlippageQuote);
            command.Parameters.AddWithValue("net", fill.NetCash);
            command.Parameters.AddWithValue("signal", (object?)signalKey ?? DBNull.Value);
            command.ExecuteNonQuery();
            if (signalKey is null)
                continue;
            using var link = new NpgsqlCommand(ResearchTradingSql.LinkOperation, _connection);
            link.Parameters.AddWithValue("key", fill.OperationKey);
            link.Parameters.AddWithValue("signal", signalKey);
            link.ExecuteNonQuery();
        }
    }

    public void WriteRun(ResearchRunRecord run)
    {
        using var command = new NpgsqlCommand(ResearchTradingSql.InsertRun, _connection);
        command.Parameters.AddWithValue("run_id", run.RunId);
        command.Parameters.AddWithValue("started", run.StartedAt);
        command.Parameters.AddWithValue("ended", run.EndedAt);
        command.Parameters.AddWithValue("status", run.Status);
        command.Parameters.AddWithValue("error", (object?)run.Error ?? DBNull.Value);
        command.Parameters.AddWithValue("candles_inserted", run.CandlesInserted);
        command.Parameters.AddWithValue("candles_skipped", run.CandlesSkipped);
        command.Parameters.AddWithValue("signals", run.Signals);
        command.Parameters.AddWithValue("risk_decisions", run.RiskDecisions);
        command.Parameters.AddWithValue("operations", run.Operations);
        command.Parameters.AddWithValue("fills", run.Fills);
        command.Parameters.AddWithValue("replayed", run.Replayed);
        command.Parameters.AddWithValue("pnl_recomputed", run.PnlRecomputed);
        command.ExecuteNonQuery();
    }

    public void WriteHealth(string status, string detail)
    {
        using var command = new NpgsqlCommand(
            """
            INSERT INTO research_trading.health (status, detail)
            VALUES (@status, @detail)
            """,
            _connection);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("detail", detail);
        command.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();
}

public static class ResearchPaths
{
    public static void RefuseProductionPath(string path)
    {
        var full = Path.GetFullPath(path).Replace('\\', '/');
        if (full.Contains("/etc/kit-platform/api.env", StringComparison.OrdinalIgnoreCase)
            || full.EndsWith("/api.env", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The research host does not read the production environment.");
    }
}
