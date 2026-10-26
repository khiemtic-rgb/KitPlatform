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

    public static readonly string[] RequiredBooleanFields =
    [
        "enableReading",
        "enableWithdrawals",
        "enableInternalTransfer",
        "permitsUniversalTransfer",
        "enableSpotAndMarginTrading",
        "enableFutures",
        "enableMargin",
        "enableVanillaOptions",
    ];

    public static bool ReadOnly(JsonElement root)
    {
        try
        {
            EnsureReadOnly(root);
            return true;
        }
        catch (ExchangeConnectionException)
        {
            return false;
        }
    }

    public static void EnsureReadOnly(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !RequiredBooleans(root))
            throw new ExchangeConnectionException(400, "unverified", "apiRestrictions thiếu field hoặc không phải boolean. Không kích hoạt kết nối.");
        RejectUnknownFields(root);
        if (Flag(root, "enableWithdrawals"))
            throw new ExchangeConnectionException(400, "withdrawal", "API key Binance đang bật quyền rút tiền. Không lưu key này.");
        if (Flag(root, "enableInternalTransfer") || Flag(root, "permitsUniversalTransfer"))
            throw new ExchangeConnectionException(400, "transfer", "API key Binance đang bật quyền chuyển tiền. Không lưu key này.");
        if (Flag(root, "enableSpotAndMarginTrading") || Flag(root, "enableFutures") || Flag(root, "enableMargin") || Flag(root, "enableVanillaOptions"))
            throw new ExchangeConnectionException(400, "not_read_only", "API key Binance đang bật quyền giao dịch. Không kích hoạt kết nối.");
        if (!Flag(root, "enableReading"))
            throw new ExchangeConnectionException(403, "permission", "API key Binance chưa bật quyền đọc.");
    }

    static void RejectUnknownFields(JsonElement root)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (IsRequired(property.Name))
                continue;
            if (property.NameEquals("ipRestrict"))
            {
                if (property.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    throw new ExchangeConnectionException(400, "unverified", "apiRestrictions có field chưa được nhận diện. Không kích hoạt kết nối.");
                continue;
            }

            if (property.NameEquals("createTime") || property.NameEquals("tradingAuthorityExpirationTime"))
            {
                if (property.Value.ValueKind != JsonValueKind.Number)
                    throw new ExchangeConnectionException(400, "unverified", "apiRestrictions có field chưa được nhận diện. Không kích hoạt kết nối.");
                continue;
            }

            // An unrecognized boolean false grants no capability. The eight required flags still decide read-only.
            if (property.Value.ValueKind == JsonValueKind.False)
                continue;
            throw new ExchangeConnectionException(400, "unverified", "apiRestrictions có field chưa được nhận diện. Không kích hoạt kết nối.");
        }
    }

    static bool IsRequired(string name)
    {
        foreach (var field in RequiredBooleanFields)
        {
            if (field == name)
                return true;
        }

        return false;
    }

    static bool RequiredBooleans(JsonElement root)
    {
        foreach (var field in RequiredBooleanFields)
        {
            if (!root.TryGetProperty(field, out var value))
                return false;
            if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                return false;
        }

        return true;
    }

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
    public const string RestrictionsPath = "/sapi/v1/account/apiRestrictions";
    public const string AccountPath = "/api/v3/account";

    readonly HttpClient _http;
    readonly IExchangeConnectorLog _log;

    public BinanceExchangeConnector(HttpMessageHandler handler, IExchangeConnectorLog log)
    {
        DisableRedirects(handler);
        _http = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(15) };
        _log = log;
    }

    public static void DisableRedirects(HttpMessageHandler handler)
    {
        for (var current = handler; current is not null; current = current is DelegatingHandler delegating ? delegating.InnerHandler : null)
        {
            switch (current)
            {
                case SocketsHttpHandler sockets:
                    sockets.AllowAutoRedirect = false;
                    return;
                case HttpClientHandler http:
                    http.AllowAutoRedirect = false;
                    return;
            }
        }
    }

    public string ExchangeCode => "BINANCE";
    public bool RequiresPassphrase => false;

    public static Uri SpotRoot(string environment) => environment switch
    {
        "MAINNET" => new Uri(SpotMainnet),
        "TESTNET" => new Uri(SpotTestnet),
        _ => throw new ExchangeConnectionException(400, "unsupported", "Môi trường không được hỗ trợ. Chỉ MAINNET hoặc TESTNET."),
    };

    public static void EnsureAllowed(string method, string path)
    {
        if (!string.Equals(method, "GET", StringComparison.Ordinal)
            || path is not (RestrictionsPath or AccountPath))
            throw new ExchangeConnectionException(400, "not_allowlisted", "Endpoint Binance không nằm trong allowlist chỉ đọc.");
    }

    public async Task<ExchangeProbeDto> ProbeAsync(ExchangeCredential credential, CancellationToken cancellationToken)
    {
        var root = SpotRoot(credential.Environment);
        using var restrictions = await SendAsync(root, credential, RestrictionsPath, "", cancellationToken);
        BinancePermissionGate.EnsureReadOnly(restrictions.RootElement);
        var names = BinancePermissionGate.Names(restrictions.RootElement);
        names.Add("futures-blocked");
        names.Add("spot-read");
        var message = BinancePermissionGate.Describe(restrictions.RootElement, true);

        using var account = await SendAsync(root, credential, AccountPath, "", cancellationToken);
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
            ReadOnlyVerified = true,
            PermissionNames = names,
            ExternalUserId = ExchangeParse.Text(account.RootElement, "uid"),
            AccountType = "SPOT",
            BalanceError = "Không gọi ví futures. Enable Futures cho phép đặt lệnh, và Binance không có quyền chỉ đọc futures tách biệt.",
            SourceEndpoints = "GET /sapi/v1/account/apiRestrictions; GET /api/v3/account",
            Assets = assets,
            CheckedAt = DateTimeOffset.UtcNow,
        };
    }

    public Task<ExchangeHistoryPage> ReadHistoryAsync(ExchangeCredential credential, ExchangeHistoryQuery query, CancellationToken cancellationToken)
    {
        _ = credential;
        _ = query;
        _ = cancellationToken;
        throw new ExchangeConnectionException(400, "not_allowlisted", "Binance phase 2 chỉ cho phép GET /sapi/v1/account/apiRestrictions và GET /api/v3/account.");
    }

    async Task<JsonDocument> SendAsync(Uri root, ExchangeCredential credential, string path, string queryWithoutSign, CancellationToken cancellationToken)
    {
        EnsureAllowed("GET", path);
        if (root.Host is not ("api.binance.com" or "testnet.binance.vision"))
            throw new ExchangeConnectionException(400, "unsupported", "Host Binance không được phép.");
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
            if ((int)response.StatusCode is >= 300 and <= 399)
            {
                _log.Info($"Binance {credential.Environment} {path} redirect");
                throw new ExchangeConnectionException(502, "redirect", "Binance trả redirect. Không theo redirect và không gửi credential tới URL khác.");
            }

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
