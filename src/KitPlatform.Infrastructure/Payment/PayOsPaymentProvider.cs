using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Application.Payment;

namespace KitPlatform.Infrastructure.Payment;

/// <summary>PayOS VietQR adapter — first provider for Kit Payment Platform.</summary>
internal sealed class PayOsPaymentProvider : IPaymentProvider
{
    private const string CreateUrl = "https://api-merchant.payos.vn/v2/payment-requests";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PaymentPayOsOptions _options;
    private readonly ILogger<PayOsPaymentProvider> _logger;

    public PayOsPaymentProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<PaymentPayOsOptions> options,
        ILogger<PayOsPaymentProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderCode => PaymentProviderCodes.PayOs;

    public bool IsReady =>
        _options.Enabled
        && !string.IsNullOrWhiteSpace(_options.ClientId)
        && !string.IsNullOrWhiteSpace(_options.ApiKey)
        && !string.IsNullOrWhiteSpace(_options.ChecksumKey);

    public async Task<ProviderCheckoutResult> CreateCheckoutAsync(
        long orderCode,
        string publicCode,
        int amountVnd,
        string description,
        string returnUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default)
    {
        if (!IsReady)
            throw new InvalidOperationException("PayOS chưa cấu hình đủ ClientId/ApiKey/ChecksumKey.");

        // PayOS description max ~25 chars — prefer publicCode (FMX…) over raw bigint.
        var desc = string.IsNullOrWhiteSpace(description) ? publicCode : description.Trim();
        if (desc.Length > 25) desc = desc[..25];

        var signature = SignPaymentRequest(amountVnd, cancelUrl, desc, orderCode, returnUrl, _options.ChecksumKey);
        var body = new Dictionary<string, object?>
        {
            ["orderCode"] = orderCode,
            ["amount"] = amountVnd,
            ["description"] = desc,
            ["returnUrl"] = returnUrl,
            ["cancelUrl"] = cancelUrl,
            ["signature"] = signature,
        };

        var client = _httpClientFactory.CreateClient("KitPaymentPayOS");
        using var request = new HttpRequestMessage(HttpMethod.Post, CreateUrl)
        {
            Content = JsonContent.Create(body, options: JsonOpts),
        };
        request.Headers.TryAddWithoutValidation("x-client-id", _options.ClientId);
        request.Headers.TryAddWithoutValidation("x-api-key", _options.ApiKey);

        using var response = await client.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("PayOS create failed HTTP {Status}: {Body}", (int)response.StatusCode, raw);
            throw new InvalidOperationException("Không tạo được link thanh toán PayOS.");
        }

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        var code = root.TryGetProperty("code", out var codeEl) ? codeEl.GetString() : null;
        if (!string.Equals(code, "00", StringComparison.Ordinal))
        {
            var err = root.TryGetProperty("desc", out var descEl) ? descEl.GetString() : raw;
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(err) ? "PayOS từ chối tạo link thanh toán." : $"PayOS: {err}");
        }

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("PayOS không trả data.");

        DateTimeOffset? expiresAt = null;
        if (TryGetLong(data, "expiredAt", out var unix) && unix > 0)
            expiresAt = DateTimeOffset.FromUnixTimeSeconds(unix);

        return new ProviderCheckoutResult(
            data.TryGetProperty("checkoutUrl", out var urlEl) ? urlEl.GetString() : null,
            data.TryGetProperty("qrCode", out var qrEl) ? qrEl.GetString() : null,
            data.TryGetProperty("paymentLinkId", out var idEl) ? idEl.GetString() : null,
            expiresAt);
    }

    public ProviderWebhookResult ParseAndVerifyWebhook(string rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
            throw new InvalidOperationException("Webhook rỗng.");

        // Fail closed — never process unsigned webhooks.
        if (string.IsNullOrWhiteSpace(_options.ChecksumKey))
        {
            _logger.LogWarning("PayOS webhook rejected: checksum key is not configured.");
            throw new InvalidOperationException(
                "Webhook PayOS chưa cấu hình chữ ký (ChecksumKey) — từ chối để tránh kích hoạt giả mạo.");
        }

        using var doc = JsonDocument.Parse(rawBody);
        var root = doc.RootElement;

        if (!root.TryGetProperty("signature", out var sigEl))
            throw new InvalidOperationException("Webhook thiếu chữ ký.");
        var signature = sigEl.GetString() ?? "";
        if (!root.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Webhook thiếu data.");
        if (!VerifyPayOsObjectSignature(dataEl, signature, _options.ChecksumKey))
            throw new InvalidOperationException("Chữ ký webhook PayOS không hợp lệ.");

        var success = root.TryGetProperty("success", out var successEl) && successEl.ValueKind == JsonValueKind.True;
        var code = root.TryGetProperty("code", out var codeEl) ? codeEl.GetString() : null;
        var isSuccess = success || string.Equals(code, "00", StringComparison.Ordinal);

        if (!TryGetLong(dataEl, "orderCode", out var orderCode))
            throw new InvalidOperationException("Webhook thiếu orderCode.");

        var txnId = dataEl.TryGetProperty("reference", out var refEl) ? refEl.GetString()
            : dataEl.TryGetProperty("paymentLinkId", out var linkEl) ? linkEl.GetString()
            : null;

        return new ProviderWebhookResult(isSuccess, orderCode, txnId, rawBody);
    }

    private static string SignPaymentRequest(
        int amount,
        string cancelUrl,
        string description,
        long orderCode,
        string returnUrl,
        string checksumKey)
    {
        var data =
            $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}";
        return HmacSha256Hex(checksumKey, data);
    }

    private static bool VerifyPayOsObjectSignature(JsonElement dataObject, string signature, string checksumKey)
    {
        var pairs = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var prop in dataObject.EnumerateObject())
        {
            pairs[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.Null => "",
                JsonValueKind.String => prop.Value.GetString() ?? "",
                JsonValueKind.Number => prop.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => prop.Value.GetRawText(),
            };
        }

        var payload = string.Join("&", pairs.Select(kv => $"{kv.Key}={kv.Value}"));
        var expected = HmacSha256Hex(checksumKey, payload);
        var actual = signature.Trim().ToLowerInvariant();
        if (expected.Length != actual.Length) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(actual));
    }

    private static string HmacSha256Hex(string key, string data)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool TryGetLong(JsonElement obj, string name, out long value)
    {
        value = 0;
        if (!obj.TryGetProperty(name, out var el)) return false;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out value)) return true;
        if (el.ValueKind == JsonValueKind.String
            && long.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            return true;
        return false;
    }
}
