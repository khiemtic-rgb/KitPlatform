using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

public static class BybitSigner
{
    public const string RecvWindow = "5000";

    public static string Payload(string timestamp, string apiKey, string recvWindow, string query) =>
        timestamp + apiKey + recvWindow + query;

    public static string Sign(string secret, string timestamp, string apiKey, string recvWindow, string query)
    {
        var payload = Payload(timestamp, apiKey, recvWindow, query);
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public static class BybitPermissionGate
{
    public static void EnsureReadable(JsonElement result)
    {
        if (!result.TryGetProperty("readOnly", out var readOnly) || readOnly.ValueKind != JsonValueKind.Number)
            throw new ExchangeConnectionException(400, "unverified", "API Bybit không trả readOnly. Không đánh dấu kết nối chỉ đọc và không lưu credential.");
        if (!result.TryGetProperty("permissions", out var permissions) || permissions.ValueKind != JsonValueKind.Object || !permissions.EnumerateObject().Any())
            throw new ExchangeConnectionException(400, "unverified", "API Bybit không trả permissions. Không đánh dấu kết nối chỉ đọc và không lưu credential.");
        foreach (var group in permissions.EnumerateObject())
        {
            if (group.Value.ValueKind != JsonValueKind.Array)
                throw new ExchangeConnectionException(400, "unverified", "Metadata quyền Bybit không đúng dạng danh sách. Không đánh dấu kết nối chỉ đọc.");
            foreach (var item in group.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
                    throw new ExchangeConnectionException(400, "unverified", "Metadata quyền Bybit có mục không đọc được. Không đánh dấu kết nối chỉ đọc.");
            }
        }

        if (ContainsToken(permissions, "Withdraw"))
            throw new ExchangeConnectionException(400, "withdrawal", "API key có quyền Withdraw. Không lưu key có quyền rút tiền.");
        if (ContainsToken(permissions, "Transfer"))
            throw new ExchangeConnectionException(400, "transfer", "API key có quyền chuyển tiền. Không lưu key này.");
        if (ContainsToken(permissions, "Trade") || ContainsToken(permissions, "Order"))
            throw new ExchangeConnectionException(400, "not_read_only", "Metadata Bybit còn quyền đặt lệnh hoặc giao dịch. readOnly=1 không đủ khi permissions mâu thuẫn. Không lưu key này.");
        if (readOnly.GetInt32() != 1)
            throw new ExchangeConnectionException(400, "not_read_only", "Bybit đánh dấu readOnly khác 1. Hãy tạo API key chỉ đọc, không bật đặt lệnh, chuyển tiền hoặc rút tiền.");
        if (IsExpired(result))
            throw new ExchangeConnectionException(400, "expired", "API key đã hết hạn theo metadata của sàn.");
    }

    public static List<string> PermissionNames(JsonElement result)
    {
        var names = new List<string>();
        if (!result.TryGetProperty("permissions", out var permissions) || permissions.ValueKind != JsonValueKind.Object)
            return names;
        foreach (var group in permissions.EnumerateObject())
        {
            if (group.Value.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var item in group.Value.EnumerateArray())
            {
                var name = item.GetString();
                if (!string.IsNullOrWhiteSpace(name))
                    names.Add(group.Name + ":" + name);
            }
        }

        return names;
    }

    public static int? Uta(JsonElement result) =>
        result.TryGetProperty("uta", out var uta) && uta.ValueKind == JsonValueKind.Number ? uta.GetInt32() : null;

    public static string? ExternalUserId(JsonElement result)
    {
        if (!result.TryGetProperty("userID", out var userId))
            return null;
        if (userId.ValueKind == JsonValueKind.String)
            return userId.GetString();
        return userId.ValueKind == JsonValueKind.Number ? userId.GetRawText() : null;
    }

    static bool ContainsToken(JsonElement permissions, string token)
    {
        foreach (var group in permissions.EnumerateObject())
        {
            if (group.Value.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var item in group.Value.EnumerateArray())
            {
                var name = item.GetString();
                if (name is not null && name.Contains(token, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    static bool IsExpired(JsonElement result)
    {
        if (result.TryGetProperty("deadlineDay", out var days)
            && days.ValueKind == JsonValueKind.Number
            && days.TryGetInt32(out var remaining)
            && remaining == 0)
            return true;
        if (result.TryGetProperty("expiredAt", out var expired)
            && expired.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(expired.GetString(), out var when))
            return when.Year >= 2000 && when < DateTimeOffset.UtcNow;
        return false;
    }
}

public sealed class BybitExchangeConnector : IExchangeConnector, IExchangeAdapter
{
    public const string Mainnet = "https://api.bybit.com";
    public const string Testnet = "https://api-testnet.bybit.com";
    readonly HttpClient _http;
    readonly IExchangeConnectorLog _log;

    public BybitExchangeConnector(HttpMessageHandler handler, IExchangeConnectorLog log)
    {
        _http = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(10) };
        _log = log;
    }

    public string ExchangeCode => "BYBIT";
    public bool RequiresPassphrase => false;

    public static Uri BaseAddress(string environment) => environment switch
    {
        "MAINNET" => new Uri(Mainnet),
        "TESTNET" => new Uri(Testnet),
        _ => throw new ExchangeConnectionException(400, "unsupported", "Môi trường không được hỗ trợ. Chỉ MAINNET hoặc TESTNET."),
    };

    public Task<ExchangeProbeDto> ProbeAsync(ExchangeCredential credential, CancellationToken cancellationToken) =>
        ReadAsync(credential.Environment, credential.ApiKey, credential.ApiSecret, cancellationToken);

    public async Task<ExchangeHistoryPage> ReadHistoryAsync(ExchangeCredential credential, ExchangeHistoryQuery query, CancellationToken cancellationToken)
    {
        var page = await ListExecutionsAsync(
            credential.Environment,
            credential.ApiKey,
            credential.ApiSecret,
            query.ProductSubtype,
            query.WindowStartMs,
            query.WindowEndMs,
            query.Limit,
            query.Cursor,
            cancellationToken);
        long? oldest = null;
        foreach (var item in page.Items)
        {
            item.SourceEndpoint ??= BybitExecutionMapper.Endpoint;
            if (item.ExecutedAt is DateTimeOffset when)
            {
                var ms = when.ToUnixTimeMilliseconds();
                if (oldest is null || ms < oldest)
                    oldest = ms;
            }
        }

        var cursor = string.IsNullOrWhiteSpace(page.NextPageCursor) ? null : page.NextPageCursor;
        return new ExchangeHistoryPage
        {
            Items = page.Items,
            NextCursor = cursor,
            PageFull = cursor is null && page.Items.Count >= query.Limit,
            OldestMs = oldest,
        };
    }

    public async Task<ExchangeProbeDto> ReadAsync(string environment, string apiKey, string apiSecret, CancellationToken cancellationToken)
    {
        var root = BaseAddress(environment);
        using var query = await SendAsync(root, environment, "/v5/user/query-api", "", apiKey, apiSecret, cancellationToken);
        var queryResult = RequireResult(query.RootElement, "/v5/user/query-api");
        BybitPermissionGate.EnsureReadable(queryResult);
        var uta = BybitPermissionGate.Uta(queryResult);
        var permissions = BybitPermissionGate.PermissionNames(queryResult);

        using var account = await SendAsync(root, environment, "/v5/account/info", "", apiKey, apiSecret, cancellationToken);
        var accountResult = RequireResult(account.RootElement, "/v5/account/info");
        var endpoints = "GET /v5/user/query-api; GET /v5/account/info";
        string? accountType = null;
        string? totalEquity = null;
        string? totalWallet = null;
        string? totalAvailable = null;
        string? balanceError = null;
        var assets = new List<ExchangeAssetDto>();

        if (uta == 1)
        {
            endpoints += "; GET /v5/account/wallet-balance?accountType=UNIFIED";
            using var wallet = await SendAsync(root, environment, "/v5/account/wallet-balance", "accountType=UNIFIED", apiKey, apiSecret, cancellationToken);
            if (RetCode(wallet.RootElement) != 0)
            {
                balanceError = Map(RetCode(wallet.RootElement), "/v5/account/wallet-balance").Message;
            }
            else
            {
                MapWallet(wallet.RootElement, ref accountType, ref totalEquity, ref totalWallet, ref totalAvailable, ref balanceError, assets);
            }
        }
        else if (uta == 0)
        {
            balanceError = "Tài khoản chưa là Unified Trading Account (uta=0). Không gọi wallet-balance với loại tài khoản khác và không điền số dư.";
        }
        else
        {
            balanceError = uta is null
                ? "API không trả trường uta. Không gọi wallet-balance và không điền số dư."
                : $"uta={uta}. Chỉ đọc wallet-balance UNIFIED khi uta=1. Không điền số dư.";
        }

        return new ExchangeProbeDto
        {
            Accepted = true,
            ExchangeCode = ExchangeCode,
            Environment = environment,
            Message = balanceError is null
                ? "Đã xác minh readOnly=1 và không có quyền Withdraw."
                : "Credential chỉ đọc đã được xác minh. Số dư Unified chưa có đủ từ sàn.",
            ReadOnlyVerified = true,
            PermissionNames = permissions,
            ExternalUserId = BybitPermissionGate.ExternalUserId(queryResult),
            MarginMode = Text(accountResult, "marginMode"),
            UnifiedMarginStatus = IntOrNull(accountResult, "unifiedMarginStatus"),
            Uta = uta,
            AccountType = accountType,
            TotalEquity = totalEquity,
            TotalWalletBalance = totalWallet,
            TotalAvailableBalance = totalAvailable,
            BalanceError = balanceError,
            SourceEndpoints = endpoints,
            Assets = assets,
            CheckedAt = DateTimeOffset.UtcNow,
        };
    }

    public async Task EnsureReadOnlyAsync(string environment, string apiKey, string apiSecret, CancellationToken cancellationToken)
    {
        var root = BaseAddress(environment);
        using var query = await SendAsync(root, environment, "/v5/user/query-api", "", apiKey, apiSecret, cancellationToken);
        var result = RequireResult(query.RootElement, "/v5/user/query-api");
        BybitPermissionGate.EnsureReadable(result);
    }

    public async Task<ExchangeExecutionPageDto> ListExecutionsAsync(
        string environment,
        string apiKey,
        string apiSecret,
        string category,
        long startTimeMs,
        long endTimeMs,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var query = BybitExecutionMapper.Query(category, startTimeMs, endTimeMs, limit, cursor);
        var root = BaseAddress(environment);
        using var document = await SendAsync(root, environment, "/v5/execution/list", query, apiKey, apiSecret, cancellationToken);
        return BybitExecutionMapper.ReadPage(document.RootElement, environment, category);
    }

    async Task<JsonDocument> SendAsync(
        Uri root, string environment, string path, string query, string apiKey, string apiSecret, CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var sign = BybitSigner.Sign(apiSecret, timestamp, apiKey, BybitSigner.RecvWindow, query);
        var uri = string.IsNullOrEmpty(query) ? new Uri(root, path) : new Uri(root, path + "?" + query);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("X-BAPI-API-KEY", apiKey);
        request.Headers.TryAddWithoutValidation("X-BAPI-TIMESTAMP", timestamp);
        request.Headers.TryAddWithoutValidation("X-BAPI-RECV-WINDOW", BybitSigner.RecvWindow);
        request.Headers.TryAddWithoutValidation("X-BAPI-SIGN", sign);
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when ((ex is HttpRequestException || ex is TaskCanceledException) && !cancellationToken.IsCancellationRequested)
        {
            _log.Info($"Bybit {environment} {path} network");
            throw new ExchangeConnectionException(502, "network", "Không kết nối được tới sàn. Không có dữ liệu tài khoản.");
        }

        using (response)
        {
            if ((int)response.StatusCode == 429)
            {
                _log.Info($"Bybit {environment} {path} rate_limit");
                throw new ExchangeConnectionException(429, "rate_limit", "Sàn giới hạn tần suất. Thử lại sau.");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _log.Info($"Bybit {environment} {path} http {(int)response.StatusCode}");
                throw new ExchangeConnectionException(502, "exchange", "Sàn trả lỗi HTTP. Không có dữ liệu tài khoản.");
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(body);
            }
            catch (JsonException)
            {
                _log.Info($"Bybit {environment} {path} invalid_json");
                throw new ExchangeConnectionException(502, "exchange", "Phản hồi của sàn không phải JSON hợp lệ.");
            }

            var code = RetCode(document.RootElement);
            _log.Info($"Bybit {environment} {path} retCode {code}");
            if (path != "/v5/account/wallet-balance" && code != 0)
            {
                document.Dispose();
                throw Map(code, path);
            }

            return document;
        }
    }

    static void MapWallet(
        JsonElement root,
        ref string? accountType,
        ref string? totalEquity,
        ref string? totalWallet,
        ref string? totalAvailable,
        ref string? balanceError,
        List<ExchangeAssetDto> assets)
    {
        if (!root.TryGetProperty("result", out var result)
            || !result.TryGetProperty("list", out var list)
            || list.ValueKind != JsonValueKind.Array)
        {
            balanceError = "Phản hồi wallet-balance không có danh sách ví. Không điền số dư.";
            return;
        }

        JsonElement? match = null;
        foreach (var item in list.EnumerateArray())
        {
            if (string.Equals(Text(item, "accountType"), "UNIFIED", StringComparison.Ordinal))
            {
                match = item;
                break;
            }
        }

        if (match is null)
        {
            balanceError = "Phản hồi không có ví UNIFIED. Không dùng loại tài khoản khác để điền số dư.";
            return;
        }

        var wallet = match.Value;
        accountType = Text(wallet, "accountType");
        totalEquity = Text(wallet, "totalEquity");
        totalWallet = Text(wallet, "totalWalletBalance");
        totalAvailable = Text(wallet, "totalAvailableBalance");
        if (!wallet.TryGetProperty("coin", out var coins) || coins.ValueKind != JsonValueKind.Array)
        {
            balanceError = "Sàn không trả danh sách coin.";
            return;
        }

        foreach (var coin in coins.EnumerateArray())
        {
            var name = Text(coin, "coin");
            if (string.IsNullOrWhiteSpace(name))
                continue;
            assets.Add(new ExchangeAssetDto
            {
                Coin = name,
                WalletBalance = Text(coin, "walletBalance"),
                Equity = Text(coin, "equity"),
                UsdValue = Text(coin, "usdValue"),
                AvailableToWithdraw = Text(coin, "availableToWithdraw"),
            });
        }
    }

    static JsonElement RequireResult(JsonElement root, string path)
    {
        if (!root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object)
            throw new ExchangeConnectionException(502, "exchange", $"Bybit không trả result cho {path}.");
        return result;
    }

    static int RetCode(JsonElement root)
    {
        if (!root.TryGetProperty("retCode", out var code) || code.ValueKind != JsonValueKind.Number || !code.TryGetInt32(out var value))
            throw new ExchangeConnectionException(502, "exchange", "Bybit không trả retCode.");
        return value;
    }

    static ExchangeConnectionException Map(int retCode, string path) => retCode switch
    {
        10002 => new(400, "timestamp", "Timestamp không được sàn chấp nhận."),
        10003 => new(400, "invalid_key", "API key không hợp lệ."),
        10004 => new(400, "signature", "Chữ ký không được sàn chấp nhận."),
        10005 => new(403, "permission", "API key không đủ quyền đọc."),
        10006 => new(429, "rate_limit", "Sàn giới hạn tần suất. Thử lại sau."),
        10010 => new(400, "ip", "IP của máy chủ không nằm trong danh sách IP của API key."),
        33004 => new(400, "expired", "API key đã hết hạn hoặc bị thu hồi."),
        _ => new(502, "exchange", $"Bybit từ chối {path} (retCode {retCode})."),
    };

    static string? Text(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        return value.GetRawText();
    }

    static int? IntOrNull(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : null;
}
