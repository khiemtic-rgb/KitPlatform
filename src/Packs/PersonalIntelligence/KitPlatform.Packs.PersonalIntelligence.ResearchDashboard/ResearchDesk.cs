using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KitPlatform.Packs.PersonalIntelligence.ResearchHost;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.ResearchDashboard;

public sealed class ResearchDeskRequest
{
    public string? Symbol { get; set; }
    public string? Timeframe { get; set; }
    public string? Strategy { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? Confidence { get; set; }
    public decimal? SlippageBps { get; set; }
    public decimal? FeeRate { get; set; }
    public decimal? MaxNotional { get; set; }
    public decimal? MaxLeverage { get; set; }
    public decimal? MinConfidence { get; set; }
    public decimal? SessionNotional { get; set; }
    public string? DataSource { get; set; }
    public string? StrategyParams { get; set; }
    public bool? Live { get; set; }
}

public sealed record ResearchDeskConfig(
    string Symbol,
    string Timeframe,
    string Strategy,
    decimal Quantity,
    decimal Confidence,
    decimal SlippageBps,
    decimal FeeRate,
    decimal MaxNotional,
    decimal MaxLeverage,
    decimal MinConfidence,
    decimal SessionNotional,
    string DataSource,
    string StrategyParams = "");

public sealed record ResearchDeskTask(
    string TaskId,
    string ParameterKey,
    string Status,
    string DataSource,
    string NarrativeKind,
    DateTimeOffset RequestedAt,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    string? Error,
    bool Replayed,
    int NewFills,
    ResearchDeskConfig Config,
    string ReportText,
    DashboardDocument? Document);

public sealed class ResearchDeskException(string message) : InvalidOperationException(message);

public static class ResearchDesk
{
    public const string NarrativeFromExecution = "derived_from_execution";
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);

    public static ResearchDeskConfig Normalize(ResearchDeskRequest request)
    {
        if (request.Live == true)
            throw new ResearchDeskException("Trading Agent V1 does not accept live execution.");
        var symbol = (request.Symbol ?? "BTCUSDT").Trim().ToUpperInvariant();
        var timeframe = (request.Timeframe ?? "1h").Trim().ToLowerInvariant();
        var strategy = (request.Strategy ?? "sma-cross-v1").Trim().ToLowerInvariant();
        var source = (request.DataSource ?? "fixture").Trim().ToLowerInvariant();
        var quantity = request.Quantity ?? 0.001m;
        var confidence = request.Confidence ?? 0.80m;
        var slippage = request.SlippageBps ?? 10m;
        var fee = request.FeeRate ?? 0.001m;
        var maxNotional = request.MaxNotional ?? 1000m;
        var maxLeverage = request.MaxLeverage ?? 1m;
        var minConfidence = request.MinConfidence ?? 0.50m;
        var sessionNotional = request.SessionNotional ?? 5000m;
        if (symbol != "BTCUSDT")
            throw new ResearchDeskException("The research desk only accepts BTCUSDT.");
        if (timeframe != "1h")
            throw new ResearchDeskException("The research desk only accepts the 1h timeframe.");
        StrategySpec spec;
        try
        {
            spec = ResearchStrategies.Resolve(strategy, request.StrategyParams);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new ResearchDeskException(ex.Message);
        }
        if (source is not ("fixture" or "public_market"))
            throw new ResearchDeskException("Data source must be fixture or public_market.");
        if (quantity <= 0m || quantity > 0.01m)
            throw new ResearchDeskException("Quantity must be greater than 0 and at most 0.01.");
        if (confidence is < 0.5m or > 1m || minConfidence is < 0.5m or > 1m || minConfidence > confidence)
            throw new ResearchDeskException("Confidence must stay between 0.5 and 1, and at least the minimum confidence.");
        if (slippage is < 0m or > 50m)
            throw new ResearchDeskException("Slippage must stay between 0 and 50 bps.");
        if (fee is < 0m or > 0.01m)
            throw new ResearchDeskException("Fee rate must stay between 0 and 0.01.");
        if (maxNotional <= 0m || maxNotional > 1000m)
            throw new ResearchDeskException("Max notional must be greater than 0 and at most 1000.");
        if (maxLeverage != 1m)
            throw new ResearchDeskException("Max leverage is fixed at 1.");
        if (sessionNotional <= 0m || sessionNotional > 5000m || sessionNotional < maxNotional)
            throw new ResearchDeskException("Session notional must cover the max notional and stay at most 5000.");
        return new ResearchDeskConfig(symbol, timeframe, spec.Id, quantity, confidence, slippage, fee, maxNotional, maxLeverage, minConfidence, sessionNotional, source, spec.Canonical);
    }

    public static async Task<ResearchDeskTask> ExecuteAsync(
        string deskRoot,
        ResearchDeskRequest request,
        string? fixtureBody = null,
        CancellationToken cancellationToken = default)
    {
        var config = Normalize(request);
        var root = PrepareRoot(deskRoot);
        var gate = Gates.GetOrAdd(root, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var parameterKey = ParameterKey(config);
            var work = Path.Combine(root, "work", parameterKey);
            Directory.CreateDirectory(work);
            var summary = Path.Combine(work, "summary.json");
            var health = Path.Combine(work, "health.json");
            var state = Path.Combine(work, "state.json");
            var requested = DateTimeOffset.UtcNow;
            var history = config.DataSource == "public_market";
            var args = new List<string>
            {
                "--summary", summary,
                "--health", health,
                "--state", state,
                "--symbol", config.Symbol,
                "--limit", history ? "600" : "48",
                "--max-stale-hours", history ? "800" : "96",
                "--quantity", Text(config.Quantity),
                "--confidence", config.Confidence.ToString("0.00", CultureInfo.InvariantCulture),
                "--starting-equity", "10000",
                "--slippage-bps", Text(config.SlippageBps),
                "--fee-rate", Text(config.FeeRate),
                "--minimum-fee", "0",
                "--leverage", "1",
                "--max-notional", Text(config.MaxNotional),
                "--max-leverage", Text(config.MaxLeverage),
                "--min-confidence", config.MinConfidence.ToString("0.00", CultureInfo.InvariantCulture),
                "--session-notional", Text(config.SessionNotional),
                "--max-http", history ? "2" : "1",
                "--max-candles", history ? "600" : "500",
                "--max-runtime-seconds", history ? "45" : "30",
                "--strategy", config.Strategy,
                "--strategy-params", config.StrategyParams,
            };
            if (config.DataSource == "fixture")
            {
                var fixture = Path.Combine(work, "klines.json");
                await File.WriteAllTextAsync(fixture, fixtureBody ?? StandardFixture(), cancellationToken);
                args.Add("--fixture");
                args.Add(fixture);
                args.Add("--as-of");
                args.Add("2026-01-04T00:00:00Z");
            }

            var started = DateTimeOffset.UtcNow;
            var code = await ResearchHostProgram.RunAsync(args.ToArray());
            var finished = DateTimeOffset.UtcNow;
            var task = ReadOutcome(parameterKey, config, requested, started, finished, code, summary, health, state);
            Save(root, task);
            return task;
        }
        finally
        {
            gate.Release();
        }
    }

    public static async Task<IReadOnlyList<ResearchDeskTask>> ExecutePlanAsync(
        string deskRoot,
        ResearchDeskRequest request,
        CancellationToken cancellationToken = default)
    {
        var seed = Normalize(request);
        var tasks = new List<ResearchDeskTask>();
        foreach (var strategy in ResearchStrategies.Ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            tasks.Add(await ExecuteAsync(deskRoot, new ResearchDeskRequest
            {
                Symbol = seed.Symbol,
                Timeframe = seed.Timeframe,
                Strategy = strategy,
                StrategyParams = ResearchStrategies.DefaultCanonical(strategy),
                Quantity = seed.Quantity,
                Confidence = seed.Confidence,
                SlippageBps = seed.SlippageBps,
                FeeRate = seed.FeeRate,
                MaxNotional = seed.MaxNotional,
                MaxLeverage = seed.MaxLeverage,
                MinConfidence = seed.MinConfidence,
                SessionNotional = seed.SessionNotional,
                DataSource = seed.DataSource,
            }, cancellationToken: cancellationToken));
        }

        return tasks;
    }

    public static IReadOnlyList<ResearchDeskTask> List(string deskRoot)
    {
        var path = TasksPath(PrepareRoot(deskRoot));
        if (!File.Exists(path))
            return [];
        var tasks = JsonSerializer.Deserialize<List<ResearchDeskTask>>(File.ReadAllText(path), Json) ?? [];
        return tasks.OrderByDescending(task => task.RequestedAt).ToArray();
    }

    public static ResearchDeskTask? Find(string deskRoot, string taskId)
    {
        if (taskId.Length is 0 or > 80 || taskId.Any(ch => !char.IsAsciiLetterOrDigit(ch) && ch is not '-' and not '_'))
            return null;
        return List(deskRoot).FirstOrDefault(task => task.TaskId == taskId);
    }

    private static ResearchDeskTask ReadOutcome(
        string parameterKey,
        ResearchDeskConfig config,
        DateTimeOffset requested,
        DateTimeOffset started,
        DateTimeOffset finished,
        int code,
        string summaryPath,
        string healthPath,
        string statePath)
    {
        if (code != 0 || !File.Exists(summaryPath))
        {
            var error = File.Exists(healthPath) ? ReadHealthDetail(healthPath) : "The research host did not produce a summary.";
            return new ResearchDeskTask(NewId(), parameterKey, code == 2 ? "KILLED" : "FAILED", config.DataSource, NarrativeFromExecution, requested, started, finished, error, false, 0, config, ExplainFailure(error), null);
        }

        var report = JsonSerializer.Deserialize<ResearchReport>(File.ReadAllText(summaryPath))
            ?? throw new ResearchDeskException("The research summary was empty.");
        var healthStatus = report.Replayed ? ResearchSession.Replayed : report.Status;
        var healthDetail = $"inserted={report.CandlesInsertedThisRun};duplicates={report.DuplicateCandlesThisRun};new_fills={report.NewPaperFills};replayed={report.Replayed}";
        if (File.Exists(healthPath))
        {
            using var health = JsonDocument.Parse(File.ReadAllText(healthPath));
            healthStatus = health.RootElement.TryGetProperty("status", out var statusValue) ? statusValue.GetString() ?? healthStatus : healthStatus;
            healthDetail = health.RootElement.TryGetProperty("detail", out var detailValue) ? detailValue.GetString() ?? healthDetail : healthDetail;
        }
        var candles = ReadCandles(statePath);
        var runs = new List<RunInput>
        {
            new(1, finished, healthStatus, healthDetail),
        };
        var bundle = new ResearchReadBundle(
            candles.Count,
            candles,
            report.Snapshot.SignalsRecorded.Select(row => new SignalInput(row.SignalKey, row.Symbol, row.Side, row.SignalTime, row.FillTime, row.FillPrice, row.Status)).ToArray(),
            report.Snapshot.RiskDecisions.Select(row => new DecisionInput(row.OperationKey, row.Allowed, row.Reasons)).ToArray(),
            report.Snapshot.Fills.Select(row => new FillInput(row.OperationKey, row.Side, row.TapePrice, row.ExecutionPrice, row.Quantity, row.Fee, row.SlippageQuote, row.NetCash)).ToArray(),
            runs,
            [new SessionInput(finished, report.Snapshot.Status, report.Snapshot.NetRealizedPnl, report.Snapshot.PnlStatus)]);
        var document = ResearchReadModel.Build(bundle, finished, config.DataSource);
        var status = report.Replayed ? ResearchSession.Replayed : report.Status;
        return new ResearchDeskTask(NewId(), parameterKey, status, config.DataSource, NarrativeFromExecution, requested, started, finished, null, report.Replayed, report.NewPaperFills, config, Explain(config, report), document);
    }

    private static string Explain(ResearchDeskConfig config, ResearchReport report)
    {
        var pnl = report.Snapshot.NetRealizedPnl is null
            ? $"Realized PnL chưa xác định ({report.Snapshot.PnlStatus}). Giá trị này không phải 0."
            : $"Realized PnL {Text(report.Snapshot.NetRealizedPnl.Value)} ({report.Snapshot.PnlStatus}).";
        var execution = report.Replayed
            ? "Lần chạy này là REPLAYED: không tạo paper fill mới. Đây không phải một lần thực thi tạo dữ liệu mới."
            : $"Lần chạy này tạo {report.NewPaperFills} paper fill mới và ghi {report.CandlesInsertedThisRun} nến mới.";
        var next = report.Replayed
            ? "Bước nghiên cứu tiếp theo là đợi một nến 1h đã đóng, hoặc đổi tham số trong giới hạn cho phép, trước khi chạy lại."
            : report.Snapshot.PnlStatus == "no_closed_position"
                ? "Vị thế còn mở. Bước tiếp theo là theo dõi tín hiệu thoát; không đánh dấu vị thế theo giá thị trường."
                : "Bước tiếp theo là đối chiếu phí, slippage và lý do risk trước khi đặt một nhiệm vụ nghiên cứu khác.";
        return $"Chiến lược {config.Strategy} trên {config.Symbol} {config.Timeframe}. Nguồn dữ liệu {config.DataSource}. {execution} Tín hiệu {report.Snapshot.Signals}, risk từ chối {report.Snapshot.RiskRefused}, phí {Text(report.Snapshot.Fees)}, slippage {Text(report.Snapshot.SlippageQuote)}. {pnl} {next}";
    }

    private static string ExplainFailure(string error)
        => "Research host không hoàn thành nhiệm vụ. Không có paper fill mới được suy ra từ lỗi này. " + error;

    private static string ReadHealthDetail(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.TryGetProperty("detail", out var detail) ? detail.GetString() ?? "Research host error." : "Research host error.";
    }

    private static List<CandleInput> ReadCandles(string path)
    {
        if (!File.Exists(path))
            return [];
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("Candles", out var candles))
            return [];
        return candles.EnumerateArray().Select(row => new CandleInput(
            row.GetProperty("Symbol").GetString() ?? "",
            row.GetProperty("Interval").GetString() ?? "",
            row.GetProperty("OpenTime").GetDateTimeOffset(),
            row.GetProperty("CloseTime").GetDateTimeOffset(),
            row.GetProperty("Open").GetDecimal(),
            row.GetProperty("High").GetDecimal(),
            row.GetProperty("Low").GetDecimal(),
            row.GetProperty("Close").GetDecimal(),
            row.GetProperty("CloseTime").GetDateTimeOffset())).ToList();
    }

    private static void Save(string root, ResearchDeskTask task)
    {
        var path = TasksPath(root);
        var tasks = File.Exists(path)
            ? JsonSerializer.Deserialize<List<ResearchDeskTask>>(File.ReadAllText(path), Json) ?? []
            : [];
        tasks.Add(task);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(tasks, Json) + Environment.NewLine);
        File.Move(temporary, path, overwrite: true);
    }

    private static string PrepareRoot(string deskRoot)
    {
        if (string.IsNullOrWhiteSpace(deskRoot))
            throw new ResearchDeskException("The research desk directory is not configured.");
        var root = Path.GetFullPath(deskRoot);
        ResearchPaths.RefuseProductionPath(root);
        if (root.Contains("novixa_prod", StringComparison.OrdinalIgnoreCase))
            throw new ResearchDeskException("The research desk refuses novixa_prod.");
        Directory.CreateDirectory(root);
        return root;
    }

    private static string TasksPath(string root) => Path.Combine(root, "tasks.json");

    private static string ParameterKey(ResearchDeskConfig config)
    {
        var raw = string.Join('|', config.Symbol, config.Timeframe, config.Strategy, Text(config.Quantity), config.Confidence.ToString("0.00", CultureInfo.InvariantCulture), Text(config.SlippageBps), Text(config.FeeRate), Text(config.MaxNotional), Text(config.MaxLeverage), config.MinConfidence.ToString("0.00", CultureInfo.InvariantCulture), Text(config.SessionNotional), config.DataSource);
        if (config.StrategyParams.Length > 0 && config.StrategyParams != ResearchStrategies.DefaultCanonical(config.Strategy))
            raw += "|" + config.StrategyParams;
        if (config.DataSource == "public_market")
            raw += "|history=600";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant()[..16];
    }

    private static string NewId() => Guid.NewGuid().ToString("N");

    private static string Text(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    public static string StandardFixture()
    {
        var rows = new List<string>();
        for (var hour = 0; hour < 30; hour++)
        {
            var close = hour == 20 ? "200" : hour > 20 ? "1" : "100";
            var openMs = DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture).AddHours(hour).ToUnixTimeMilliseconds();
            var high = string.Compare(close, "100", StringComparison.Ordinal) > 0 ? close : "100";
            var low = string.Compare(close, "100", StringComparison.Ordinal) < 0 ? close : "100";
            rows.Add($"[{openMs},\"100\",\"{high}\",\"{low}\",\"{close}\",\"0\",{openMs + 3_600_000 - 1},\"0\",0,\"0\",\"0\",\"0\"]");
        }

        return "[" + string.Join(',', rows) + "]";
    }

    public static string OpenPositionFixture()
    {
        var rows = new List<string>();
        for (var hour = 0; hour < 22; hour++)
        {
            var close = hour == 20 ? "200" : "100";
            var openMs = DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture).AddHours(hour).ToUnixTimeMilliseconds();
            rows.Add($"[{openMs},\"100\",\"{close}\",\"100\",\"{close}\",\"0\",{openMs + 3_600_000 - 1},\"0\",0,\"0\",\"0\",\"0\"]");
        }

        return "[" + string.Join(',', rows) + "]";
    }
}
