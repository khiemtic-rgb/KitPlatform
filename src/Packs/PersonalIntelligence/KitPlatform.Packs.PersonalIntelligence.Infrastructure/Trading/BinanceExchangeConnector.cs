using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

public static class BinancePermissionGate
{
    public static void EnsureNotTransfer(JsonElement root)
    {
        if (Flag(root, "enableWithdrawals"))
            throw new ExchangeConnectionException(400, "withdrawal", "API key Binance đang bật quyền rút tiền. Không lưu key này.");
        if (Flag(root, "enableInternalTransfer") || Flag(root, "permitsUniversalTransfer"))
            throw new ExchangeConnectionException(400, "transfer", "API key Binance đang bật quyền chuyển tiền. Không lưu key này.");
        if (!Flag(root, "enableReading"))
            throw new ExchangeConnectionException(403, "permission", "API key Binance chưa bật quyền đọc.");
    }

    public static bool ReadOnly(JsonElement root) =>
        Flag(root, "enableReading")
        && !Flag(root, "enableWithdrawals")
        && !Flag(root, "enableInternalTransfer")
        && !Flag(root, "permitsUniversalTransfer")
        && !Flag(root, "enableSpotAndMarginTrading")
        && !Flag(root, "enableFutures")
        && !Flag(root, "enableMargin")
        && !Flag(root, "enableVanillaOptions");

    public static bool FuturesEnabled(JsonElement root) => Flag(root, "enableFutures");

    public static bool SpotTradeEnabled(JsonElement root) =>
        Flag(root, "enableSpotAndMarginTrading") || Flag(root, "enableMargin");

    public static string FuturesHistoryBlock(IReadOnlyCollection<string> names)
    {
        if (names.Contains("restrictions-unverified"))
            return "Không đọc được apiRestrictions. Không gọi lịch sử USD-M hoặc COIN-M khi quyền chưa xác minh.";
        if (names.Contains("futures") || names.Contains("futures-unsafe"))
            return "Key Binance bật Enable Futures. Quyền này cho phép đặt lệnh hợp đồng. Không gọi USD-M hoặc COIN-M và không yêu cầu bật quyền này.";
        return "Binance không có quyền chỉ đọc futures tách khỏi quyền đặt lệnh. Luồng USD-M và COIN-M không được gọi. Không bật Enable Futures để lấy lịch sử.";
    }

    public static string? SpotHistoryBlock(IReadOnlyCollection<string> names)
    {
        if (names.Contains("restrictions-unverified"))
            return "Không đọc được apiRestrictions. Không đồng bộ lịch sử Spot khi quyền chưa xác minh.";
        if (names.Contains("spotTrading") || names.Contains("margin") || names.Contains("spot-trade"))
            return "Key Binance bật giao dịch Spot hoặc Margin. Không đồng bộ lịch sử Spot vì key có thể đặt lệnh.";
        return null;
    }

    public static string Describe(JsonElement root, bool readOnly)
    {
        if (readOnly)
            return "Đã đọc apiRestrictions. Spot chỉ có quyền đọc. USD-M và COIN-M không được đồng bộ vì Binance không có quyền futures chỉ đọc. Không bật Enable Futures.";
        if (Flag(root, "enableFutures"))
            return "Key bật Enable Futures nên có thể đặt lệnh hợp đồng. Không xác minh là chỉ đọc. Futures không được đồng bộ. Không bật thêm quyền.";
        if (SpotTradeEnabled(root))
            return "Key bật giao dịch Spot hoặc Margin nên có thể đặt lệnh. Không xác minh là chỉ đọc. Lịch sử Spot không được đồng bộ.";
        return "apiRestrictions chưa chứng minh key chỉ đọc. Lịch sử không được đồng bộ cho đến khi quyền được xác minh.";
    }

    public static List<string> Names(JsonElement root)
    {
        var names = new List<string>();
        if (Flag(root, "enableReading")) names.Add("reading");
        names.Add(Flag(root, "ipRestrict") ? "ipRestrict" : "ipOpen");
        if (Flag(root, "enableSpotAndMarginTrading")) names.Add("spotTrading");
        if (Flag(root, "enableFutures")) names.Add("futures");
        if (Flag(root, "enableMargin")) names.Add("margin");
        if (Flag(root, "enableVanillaOptions")) names.Add("options");
        return names;
    }

    static bool Flag(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}

public sealed class BinanceExchangeConnector : IExchangeAdapter
{
    public const string SpotMainnet = "https://api.binance.com";
    public const string SpotTestnet = "https://testnet.binance.vision";
    public const string UsdMainnet = "https://fapi.binance.com";
    public const string CoinMainnet = "https://dapi.binance.com";
    public const string FuturesTestnet = "https://testnet.binancefuture.com";

    readonly HttpClient _http;
    readonly IExchangeConnectorLog _log;

    public BinanceExchangeConnector(HttpMessageHandler handler, IExchangeConnectorLog log)
    {
        _http = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(15) };
        _log = log;
    }

    public string ExchangeCode => "BINANCE";
    public bool RequiresPassphrase => false;

    public static Uri SpotRoot(string environment) => environment switch
    {
        "MAINNET" => new Uri(SpotMainnet),
        "TESTNET" => new Uri(SpotTestnet),
        _ => throw new ExchangeConnectionException(400, "unsupported", "Môi trường không được hỗ trợ. Chỉ MAINNET hoặc TESTNET."),
    };

    public static Uri FuturesRoot(string environment, string subtype) => environment switch
    {
        "MAINNET" => new Uri(subtype == "coin_m" ? CoinMainnet : UsdMainnet),
        "TESTNET" => new Uri(FuturesTestnet),
        _ => throw new ExchangeConnectionException(400, "unsupported", "Môi trường không được hỗ trợ. Chỉ MAINNET hoặc TESTNET."),
    };

    public async Task<ExchangeProbeDto> ProbeAsync(ExchangeCredential credential, CancellationToken cancellationToken)
    {
        var root = SpotRoot(credential.Environment);
        var endpoints = "GET /api/v3/account";
        bool readOnly;
        string message;
        var names = new List<string>();
        var futures = false;
        try
        {
            using var restrictions = await SendAsync(root, credential, "/sapi/v1/account/apiRestrictions", "", cancellationToken);
            BinancePermissionGate.EnsureNotTransfer(restrictions.RootElement);
            readOnly = BinancePermissionGate.ReadOnly(restrictions.RootElement);
            futures = BinancePermissionGate.FuturesEnabled(restrictions.RootElement);
            endpoints = "GET /sapi/v1/account/apiRestrictions; GET /api/v3/account";
            names = BinancePermissionGate.Names(restrictions.RootElement);
            names.Add("futures-blocked");
            if (futures)
                names.Add("futures-unsafe");
            if (BinancePermissionGate.SpotTradeEnabled(restrictions.RootElement))
                names.Add("spot-trade");
            else if (names.Contains("reading"))
                names.Add("spot-read");
            message = BinancePermissionGate.Describe(restrictions.RootElement, readOnly);
        }
        catch (ExchangeConnectionException ex) when (ex.Code == "exchange")
        {
            readOnly = false;
            names = ["restrictions-unverified"];
            message = "Không đọc được apiRestrictions. Quyền chưa xác minh. Không đồng bộ lịch sử và không suy ra quyền chỉ đọc từ số dư.";
        }

        using var account = await SendAsync(root, credential, "/api/v3/account", "", cancellationToken);
        var assets = new List<ExchangeAssetDto>();
        if (account.RootElement.TryGetProperty("balances", out var balances) && balances.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in balances.EnumerateArray())
            {
                var coin = ExchangeParse.Text(row, "asset");
                var free = ExchangeParse.Text(row, "free");
                var locked = ExchangeParse.Text(row, "locked");
                if (coin is null)
                    continue;
                if (!HasAmount(free) && !HasAmount(locked))
                    continue;
                assets.Add(new ExchangeAssetDto { Coin = coin, WalletBalance = free, AvailableToWithdraw = free, Equity = locked });
            }
        }

        return new ExchangeProbeDto
        {
            Accepted = true,
            ExchangeCode = ExchangeCode,
            Environment = credential.Environment,
            Message = message,
            ReadOnlyVerified = readOnly,
            PermissionNames = names,
            ExternalUserId = ExchangeParse.Text(account.RootElement, "uid"),
            AccountType = "SPOT",
            BalanceError = "Không gọi ví futures. Enable Futures cho phép đặt lệnh, và Binance không có quyền chỉ đọc futures tách biệt.",
            SourceEndpoints = endpoints,
            Assets = assets,
            CheckedAt = DateTimeOffset.UtcNow,
        };
    }

    public async Task<ExchangeHistoryPage> ReadHistoryAsync(ExchangeCredential credential, ExchangeHistoryQuery query, CancellationToken cancellationToken)
    {
        if (query.Phase == "discover" && query.ProductSubtype == "spot")
        {
            using var account = await SendAsync(SpotRoot(credential.Environment), credential, "/api/v3/account", "", cancellationToken);
            return new ExchangeHistoryPage { DiscoveredSymbols = BinanceExecutionMapper.SymbolsFromBalances(account.RootElement) };
        }

        if (query.Phase == "discover")
        {
            var incomeRoot = FuturesRoot(credential.Environment, query.ProductSubtype);
            var path = query.ProductSubtype == "coin_m" ? "/dapi/v1/income" : "/fapi/v1/income";
            var incomeQuery = FormattableString.Invariant($"startTime={query.WindowStartMs}&endTime={query.WindowEndMs}&limit=1000");
            using var income = await SendAsync(incomeRoot, credential, path, incomeQuery, cancellationToken);
            var symbols = BinanceExecutionMapper.SymbolsFromIncome(income.RootElement);
            string? next = null;
            if (income.RootElement.ValueKind == JsonValueKind.Array && income.RootElement.GetArrayLength() >= 1000)
            {
                long last = 0;
                foreach (var row in income.RootElement.EnumerateArray())
                {
                    var when = ExchangeParse.UnixMs(row, "time");
                    if (when is not null && when.Value.ToUnixTimeMilliseconds() > last)
                        last = when.Value.ToUnixTimeMilliseconds();
                }

                if (last > query.WindowStartMs)
                    next = (last + 1).ToString(CultureInfo.InvariantCulture);
            }

            return new ExchangeHistoryPage { DiscoveredSymbols = symbols, NextCursor = next, PageFull = next is not null };
        }

        var signed = SignedTrades(credential.Environment, query);
        using var document = await SendAsync(signed.Root, credential, signed.Path, signed.Query, cancellationToken);
        var page = new ExchangeHistoryPage();
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new ExchangeConnectionException(502, "exchange", "Binance không trả danh sách khớp lệnh.");
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var mapped = query.ProductSubtype == "spot"
                ? BinanceExecutionMapper.MapSpot(item, credential.Environment)
                : BinanceExecutionMapper.MapFutures(item, credential.Environment, query.ProductSubtype, signed.Endpoint);
            page.Items.Add(mapped);
            if (mapped.ExecutedAt is DateTimeOffset when)
            {
                var ms = when.ToUnixTimeMilliseconds();
                if (page.OldestMs is null || ms < page.OldestMs)
                    page.OldestMs = ms;
            }
        }

        page.PageFull = page.Items.Count >= query.Limit;
        return page;
    }

    static (Uri Root, string Path, string Query, string Endpoint) SignedTrades(string environment, ExchangeHistoryQuery query)
    {
        var common = FormattableString.Invariant($"symbol={Uri.EscapeDataString(query.Symbol ?? "")}&startTime={query.WindowStartMs}&endTime={query.WindowEndMs}&limit={query.Limit}");
        return query.ProductSubtype switch
        {
            "spot" => (SpotRoot(environment), "/api/v3/myTrades", common, "GET /api/v3/myTrades"),
            "usd_m" => (FuturesRoot(environment, "usd_m"), "/fapi/v1/userTrades", common, "GET /fapi/v1/userTrades"),
            "coin_m" => (FuturesRoot(environment, "coin_m"), "/dapi/v1/userTrades", common, "GET /dapi/v1/userTrades"),
            _ => throw new ExchangeConnectionException(400, "unsupported_category", $"Binance chưa hỗ trợ subtype {query.ProductSubtype}."),
        };
    }

    async Task<JsonDocument> SendAsync(Uri root, ExchangeCredential credential, string path, string queryWithoutSign, CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        var payload = string.IsNullOrEmpty(queryWithoutSign)
            ? FormattableString.Invariant($"recvWindow={BinanceSigner.RecvWindow}&timestamp={timestamp}")
            : FormattableString.Invariant($"{queryWithoutSign}&recvWindow={BinanceSigner.RecvWindow}&timestamp={timestamp}");
        var sign = BinanceSigner.Sign(credential.ApiSecret, payload);
        var uri = new Uri(root, path + "?" + payload + "&signature=" + sign);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("X-MBX-APIKEY", credential.ApiKey);
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when ((ex is HttpRequestException || ex is TaskCanceledException) && !cancellationToken.IsCancellationRequested)
        {
            _log.Info($"Binance {credential.Environment} {path} network");
            throw new ExchangeConnectionException(502, "network", "Không kết nối được tới Binance. Không có dữ liệu tài khoản.");
        }

        using (response)
        {
            if ((int)response.StatusCode is 429 or 418)
            {
                _log.Info($"Binance {credential.Environment} {path} rate_limit");
                throw new ExchangeConnectionException(429, "rate_limit", "Binance giới hạn tần suất. Thử lại sau.");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(body);
            }
            catch (JsonException)
            {
                _log.Info($"Binance {credential.Environment} {path} invalid_json");
                throw new ExchangeConnectionException(502, "exchange", "Phản hồi của Binance không phải JSON hợp lệ.");
            }

            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("code", out var code)
                && code.TryGetInt32(out var number)
                && number < 0)
            {
                document.Dispose();
                _log.Info($"Binance {credential.Environment} {path} code {number}");
                throw Map(number, path);
            }

            if (!response.IsSuccessStatusCode)
            {
                document.Dispose();
                _log.Info($"Binance {credential.Environment} {path} http {(int)response.StatusCode}");
                throw new ExchangeConnectionException(502, "exchange", "Binance trả lỗi HTTP. Không có dữ liệu.");
            }

            return document;
        }
    }

    static ExchangeConnectionException Map(int code, string path) => code switch
    {
        -1021 => new(400, "timestamp", "Timestamp không được Binance chấp nhận."),
        -1022 => new(400, "signature", "Chữ ký không được Binance chấp nhận."),
        -2014 => new(400, "invalid_key", "API key Binance không hợp lệ."),
        -2015 => new(403, "permission", "API key, IP hoặc quyền không được Binance chấp nhận."),
        -1003 => new(429, "rate_limit", "Binance giới hạn tần suất. Thử lại sau."),
        -1121 => new(400, "unknown_symbol", "Binance không có symbol này."),
        _ => new(502, "exchange", $"Binance từ chối {path} (code {code})."),
    };

    static bool HasAmount(string? text) =>
        ExchangeParse.TryDecimal(text, out var value) && value > 0;
}
