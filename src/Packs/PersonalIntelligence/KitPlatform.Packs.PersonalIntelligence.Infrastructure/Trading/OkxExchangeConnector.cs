using System.Net.Http;
using System.Text.Json;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

public static class OkxPermissionGate
{
    static readonly HashSet<string> Known = new(StringComparer.Ordinal) { "read_only", "trade", "withdraw" };

    public static (bool ReadOnlyVerified, List<string> Names, string Message) Evaluate(JsonElement configRow)
    {
        if (!configRow.TryGetProperty("perm", out var perm) || perm.ValueKind != JsonValueKind.String)
            return (false, ["unverified"], "OKX không trả perm dạng chuỗi. Quyền chưa xác minh. Số dư đọc được không chứng minh key chỉ đọc.");
        var parts = (perm.GetString() ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (parts.Count == 0)
            return (false, ["unverified"], "OKX trả perm rỗng. Quyền chưa xác minh. Số dư đọc được không chứng minh key chỉ đọc.");
        if (parts.Any(part => !Known.Contains(part)))
            return (false, ["unverified"], "OKX trả perm không thuộc read_only, trade hoặc withdraw. Quyền chưa xác minh.");
        if (parts.Contains("withdraw"))
            throw new ExchangeConnectionException(400, "withdrawal", "API key OKX có quyền withdraw. Không lưu key này.");
        if (parts.Contains("trade"))
            throw new ExchangeConnectionException(400, "not_read_only", "API key OKX có quyền trade. Quyền này đặt lệnh và chuyển tiền. Không lưu key này.");
        if (parts.Count != 1 || parts[0] != "read_only")
            return (false, ["unverified"], "OKX không xác nhận perm chỉ có read_only. Quyền chưa xác minh.");
        return (true, ["read_only"], "OKX perm chỉ có read_only. Không có trade hoặc withdraw.");
    }

    public static string? HistoryBlock(IReadOnlyCollection<string> names, string subtype) =>
        names.Contains("read_only")
            ? null
            : "OKX chưa xác minh perm chỉ có read_only. Không đồng bộ lịch sử " + subtype + ". Số dư đọc được không chứng minh quyền chỉ đọc.";
}

public sealed class OkxExchangeConnector : IExchangeAdapter
{
    public const string Host = "https://www.okx.com";
    readonly HttpClient _http;
    readonly IExchangeConnectorLog _log;

    public OkxExchangeConnector(HttpMessageHandler handler, IExchangeConnectorLog log)
    {
        _http = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(15) };
        _log = log;
    }

    public string ExchangeCode => "OKX";
    public bool RequiresPassphrase => true;

    public async Task<ExchangeProbeDto> ProbeAsync(ExchangeCredential credential, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(credential.Passphrase))
            throw new ExchangeConnectionException(400, "passphrase_required", "OKX yêu cầu passphrase. Không lưu credential khi thiếu passphrase.");
        if (credential.Environment is not ("MAINNET" or "TESTNET"))
            throw new ExchangeConnectionException(400, "unsupported", "Môi trường không được hỗ trợ. Chỉ MAINNET hoặc TESTNET.");
        using var config = await SendAsync(credential, "/api/v5/account/config", "", cancellationToken);
        var configRow = First(config.RootElement, "/api/v5/account/config");
        var (readOnly, names, message) = OkxPermissionGate.Evaluate(configRow);
        using var balance = await SendAsync(credential, "/api/v5/account/balance", "", cancellationToken);
        var balanceRow = First(balance.RootElement, "/api/v5/account/balance");
        var assets = new List<ExchangeAssetDto>();
        if (balanceRow.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in details.EnumerateArray())
            {
                var coin = ExchangeParse.Text(row, "ccy");
                if (coin is null)
                    continue;
                assets.Add(new ExchangeAssetDto
                {
                    Coin = coin,
                    WalletBalance = ExchangeParse.Text(row, "cashBal"),
                    Equity = ExchangeParse.Text(row, "eq"),
                    UsdValue = ExchangeParse.Text(row, "eqUsd"),
                    AvailableToWithdraw = ExchangeParse.Text(row, "availBal"),
                });
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
            ExternalUserId = ExchangeParse.Text(configRow, "uid"),
            AccountType = ExchangeParse.Text(configRow, "acctLv"),
            MarginMode = ExchangeParse.Text(configRow, "posMode"),
            TotalEquity = ExchangeParse.Text(balanceRow, "totalEq"),
            SourceEndpoints = "GET /api/v5/account/config; GET /api/v5/account/balance",
            Assets = assets,
            CheckedAt = DateTimeOffset.UtcNow,
        };
    }

    public async Task<ExchangeHistoryPage> ReadHistoryAsync(ExchangeCredential credential, ExchangeHistoryQuery query, CancellationToken cancellationToken)
    {
        var instType = OkxExecutionMapper.InstType(query.ProductSubtype);
        var raw = FormattableString.Invariant($"instType={instType}&begin={query.WindowStartMs}&end={query.WindowEndMs}&limit={query.Limit}");
        if (!string.IsNullOrEmpty(query.Cursor))
            raw += "&after=" + Uri.EscapeDataString(query.Cursor);
        using var document = await SendAsync(credential, "/api/v5/trade/fills-history", raw, cancellationToken);
        if (!document.RootElement.TryGetProperty("data", out var data))
            throw new ExchangeConnectionException(502, "exchange", "OKX không trả data cho fills-history.");
        return OkxExecutionMapper.ReadPage(data, credential.Environment, query.ProductSubtype, query.Limit);
    }

    async Task<JsonDocument> SendAsync(ExchangeCredential credential, string path, string query, CancellationToken cancellationToken)
    {
        var requestPath = string.IsNullOrEmpty(query) ? path : path + "?" + query;
        var timestamp = OkxSigner.Timestamp(DateTimeOffset.UtcNow);
        var sign = OkxSigner.Sign(credential.ApiSecret, timestamp, "GET", requestPath);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(Host), requestPath));
        request.Headers.TryAddWithoutValidation("OK-ACCESS-KEY", credential.ApiKey);
        request.Headers.TryAddWithoutValidation("OK-ACCESS-SIGN", sign);
        request.Headers.TryAddWithoutValidation("OK-ACCESS-TIMESTAMP", timestamp);
        request.Headers.TryAddWithoutValidation("OK-ACCESS-PASSPHRASE", credential.Passphrase ?? "");
        if (credential.Environment == "TESTNET")
            request.Headers.TryAddWithoutValidation("x-simulated-trading", "1");
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when ((ex is HttpRequestException || ex is TaskCanceledException) && !cancellationToken.IsCancellationRequested)
        {
            _log.Info($"OKX {credential.Environment} {path} network");
            throw new ExchangeConnectionException(502, "network", "Không kết nối được tới OKX. Không có dữ liệu tài khoản.");
        }

        using (response)
        {
            if ((int)response.StatusCode == 429)
            {
                _log.Info($"OKX {credential.Environment} {path} rate_limit");
                throw new ExchangeConnectionException(429, "rate_limit", "OKX giới hạn tần suất. Thử lại sau.");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(body);
            }
            catch (JsonException)
            {
                _log.Info($"OKX {credential.Environment} {path} invalid_json");
                throw new ExchangeConnectionException(502, "exchange", "Phản hồi của OKX không phải JSON hợp lệ.");
            }

            var code = document.RootElement.TryGetProperty("code", out var codeValue) ? codeValue.GetString() : null;
            _log.Info($"OKX {credential.Environment} {path} code {code ?? "none"}");
            if (!string.Equals(code, "0", StringComparison.Ordinal))
            {
                document.Dispose();
                throw Map(code, path);
            }

            if (!response.IsSuccessStatusCode)
            {
                document.Dispose();
                throw new ExchangeConnectionException(502, "exchange", "OKX trả lỗi HTTP. Không có dữ liệu.");
            }

            return document;
        }
    }

    static JsonElement First(JsonElement root, string path)
    {
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
            throw new ExchangeConnectionException(502, "exchange", $"OKX không trả data cho {path}.");
        return data[0];
    }

    static ExchangeConnectionException Map(string? code, string path) => code switch
    {
        "50102" => new(400, "timestamp", "Timestamp không được OKX chấp nhận."),
        "50111" => new(400, "invalid_key", "API key OKX không hợp lệ."),
        "50113" => new(400, "signature", "Chữ ký không được OKX chấp nhận."),
        "50105" or "50114" => new(400, "passphrase", "Passphrase OKX không đúng."),
        "50110" => new(400, "ip", "IP của máy chủ không nằm trong danh sách IP của API key OKX."),
        "50011" => new(429, "rate_limit", "OKX giới hạn tần suất. Thử lại sau."),
        _ => new(502, "exchange", $"OKX từ chối {path} (code {code ?? "none"})."),
    };
}
