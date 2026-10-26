using System.Globalization;
using System.Text.Json;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

public static class BybitExecutionMapper
{
    public static readonly string[] Categories = ["spot", "linear", "inverse", "option"];
    public const long MaxWindowMs = 7L * 24 * 60 * 60 * 1000;
    public const int PageSize = 100;
    public const int MaxPages = 10;
    public const int MaxRangeDays = 31;
    public const string Endpoint = "GET /v5/execution/list";

    static readonly HashSet<string> FillTypes = new(StringComparer.Ordinal)
    {
        "Trade", "AdlTrade", "BustTrade", "BlockTrade",
    };

    public static string NormalizeCategory(string? category)
    {
        var value = (category ?? "").Trim().ToLowerInvariant();
        if (!Categories.Contains(value))
        {
            throw new ExchangeConnectionException(
                400,
                "unsupported_category",
                $"Loại sản phẩm không được hỗ trợ: '{category}'. Hỗ trợ spot, linear, inverse, option. Loại không hỗ trợ không bị bỏ qua âm thầm.");
        }

        return value;
    }

    public static void ValidateCallWindow(long startMs, long endMs)
    {
        if (endMs <= startMs)
            throw new ExchangeConnectionException(400, "window", "Mốc kết thúc phải sau mốc bắt đầu.");
        if (endMs - startMs > MaxWindowMs)
            throw new ExchangeConnectionException(400, "window", "Một lần gọi GET /v5/execution/list chỉ nhận khoảng tối đa 7 ngày.");
    }

    public static string Query(string category, long startMs, long endMs, int limit, string? cursor)
    {
        category = NormalizeCategory(category);
        ValidateCallWindow(startMs, endMs);
        if (limit is < 1 or > PageSize)
            throw new ExchangeConnectionException(400, "limit", "Giới hạn trang Bybit phải từ 1 đến 100.");
        var query = FormattableString.Invariant($"category={category}&startTime={startMs}&endTime={endMs}&limit={limit}");
        if (!string.IsNullOrEmpty(cursor))
            query += "&cursor=" + Uri.EscapeDataString(cursor);
        return query;
    }

    public static List<(long Start, long End)> Windows(DateTimeOffset start, DateTimeOffset end)
    {
        var startMs = start.ToUnixTimeMilliseconds();
        var endMs = end.ToUnixTimeMilliseconds();
        if (endMs <= startMs)
            throw new ExchangeConnectionException(400, "window", "Mốc kết thúc phải sau mốc bắt đầu.");
        var maxMs = MaxRangeDays * 24L * 60 * 60 * 1000;
        if (endMs - startMs > maxMs)
        {
            throw new ExchangeConnectionException(
                400,
                "window",
                $"Khoảng đồng bộ tối đa {MaxRangeDays} ngày. Bybit chỉ trả tối đa 7 ngày mỗi lần gọi.");
        }

        var windows = new List<(long Start, long End)>();
        var cursor = startMs;
        while (cursor < endMs)
        {
            var windowEnd = Math.Min(cursor + MaxWindowMs, endMs);
            windows.Add((cursor, windowEnd));
            if (windowEnd >= endMs)
                break;
            cursor = windowEnd;
        }

        windows.Reverse();
        return windows;
    }

    public static string ExternalTradeId(string environment, string category, string execId) =>
        $"BYBIT:{environment}:{category}:{execId}";

    public static string ExternalAccountId(string environment, string? externalUserId, Guid connectionId)
    {
        var user = new string((externalUserId ?? "").Trim().Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray());
        if (user.Length > 80)
            user = user[..80];
        var id = user.Length == 0
            ? $"BYBIT:{environment}:connection:{connectionId:N}"
            : $"BYBIT:{environment}:{user}";
        return id.Length <= 200 ? id : $"BYBIT:{environment}:connection:{connectionId:N}";
    }

    public static ExchangeExecutionPageDto ReadPage(JsonElement root, string environment, string category)
    {
        category = NormalizeCategory(category);
        if (environment is not ("MAINNET" or "TESTNET"))
            throw new ExchangeConnectionException(400, "unsupported", "Môi trường không được hỗ trợ. Chỉ MAINNET hoặc TESTNET.");
        if (!root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object)
            throw new ExchangeConnectionException(502, "exchange", "Bybit không trả result cho /v5/execution/list.");
        var responseCategory = Text(result, "category");
        if (responseCategory is not null && !string.Equals(responseCategory, category, StringComparison.OrdinalIgnoreCase))
        {
            throw new ExchangeConnectionException(
                502,
                "category_mismatch",
                $"Bybit trả category {responseCategory} trong khi yêu cầu {category}. Không ghi lẫn loại sản phẩm.");
        }

        if (!result.TryGetProperty("list", out var list) || list.ValueKind != JsonValueKind.Array)
            throw new ExchangeConnectionException(502, "exchange", "Bybit không trả result.list cho /v5/execution/list.");

        var page = new ExchangeExecutionPageDto { NextPageCursor = Text(result, "nextPageCursor") };
        foreach (var item in list.EnumerateArray())
            page.Items.Add(MapItem(item, environment, category));
        return page;
    }

    public static ExchangeExecutionItemDto MapItem(JsonElement item, string environment, string category)
    {
        category = NormalizeCategory(category);
        var missing = new List<string>();
        var reasons = new List<string>();
        var execType = Text(item, "execType");
        var execId = Text(item, "execId");
        var orderId = Text(item, "orderId");
        var symbol = Text(item, "symbol");
        var priceText = Text(item, "execPrice");
        var qtyText = Text(item, "execQty");
        var feeText = Text(item, "execFee");
        var valueText = Text(item, "execValue");
        var feeCurrency = Text(item, "feeCurrency");

        if (execType is null)
        {
            missing.Add("execType");
            reasons.Add("Thiếu execType. Không suy đoán đây là một lần khớp.");
        }
        else if (string.Equals(execType, "Funding", StringComparison.Ordinal))
        {
            reasons.Add("execType Funding không ghi vào sổ giao dịch và không cộng vào phí. Funding cần endpoint khác nếu muốn đối soát.");
        }
        else if (!FillTypes.Contains(execType))
        {
            reasons.Add($"execType {execType} không ánh xạ vào giao dịch đã khớp. Không ghi vào sổ.");
        }

        if (category == "option")
            reasons.Add("category option không thuộc SPOT hoặc FUTURES. Không ghi vào hai nhóm này. Option chưa có mô hình đồng bộ riêng.");

        if (execId is null)
            reasons.Add("Thiếu execId. Không thể định danh execution.");
        if (orderId is null)
            missing.Add("orderId");
        if (symbol is null)
            reasons.Add("Thiếu symbol.");
        else if (symbol.Length > 64)
            reasons.Add("symbol dài hơn cột VARCHAR(64).");

        var sideRaw = Text(item, "side");
        string? side = sideRaw?.ToUpperInvariant() switch
        {
            "BUY" => "BUY",
            "SELL" => "SELL",
            _ => null,
        };
        if (side is null)
            reasons.Add(sideRaw is null ? "Thiếu side." : $"side '{sideRaw}' không tương thích với BUY/SELL.");

        decimal? price = null;
        if (priceText is null)
            reasons.Add("Thiếu execPrice.");
        else if (!TryDecimal(priceText, out var parsedPrice))
            reasons.Add("execPrice không phải số.");
        else if (parsedPrice <= 0)
            reasons.Add("Giá khớp không dương nên không ghi vào trade.");
        else if (!Fits(parsedPrice, 12, 12))
            reasons.Add("Giá vượt numeric(24,12) của sổ.");
        else
            price = decimal.Round(parsedPrice, 12, MidpointRounding.AwayFromZero);

        decimal? quantity = null;
        if (qtyText is null)
            reasons.Add("Thiếu execQty.");
        else if (!TryDecimal(qtyText, out var parsedQty))
            reasons.Add("execQty không phải số.");
        else if (parsedQty <= 0)
            reasons.Add("Số lượng khớp không dương nên không ghi vào trade.");
        else if (!Fits(parsedQty, 12, 12))
            reasons.Add("Số lượng vượt numeric(24,12) của sổ.");
        else
            quantity = decimal.Round(parsedQty, 12, MidpointRounding.AwayFromZero);

        if (!TryTime(item, out var executedAt))
            reasons.Add("Thiếu hoặc không đọc được execTime.");

        decimal? fee = null;
        if (feeText is null)
            missing.Add("execFee");
        else if (!TryDecimal(feeText, out var parsedFee))
            missing.Add("execFee");
        else if (!Fits(parsedFee, 16, 8))
            missing.Add("execFee");
        else
            fee = decimal.Round(parsedFee, 8, MidpointRounding.AwayFromZero);

        if (fee is not null && feeCurrency is null)
            missing.Add("feeCurrency");
        if (feeCurrency is not null && feeCurrency.Length > 32)
        {
            missing.Add("feeCurrency");
            feeCurrency = null;
        }

        decimal? total = null;
        var basis = "none";
        if (valueText is not null && TryDecimal(valueText, out var parsedValue) && parsedValue >= 0 && Fits(parsedValue, 20, 4))
        {
            total = decimal.Round(parsedValue, 4, MidpointRounding.AwayFromZero);
            basis = "execValue";
        }
        else if (price is not null && quantity is not null)
        {
            var product = price.Value * quantity.Value;
            if (product >= 0 && Fits(product, 20, 4))
            {
                total = decimal.Round(product, 4, MidpointRounding.AwayFromZero);
                basis = "quantity*price";
                if (valueText is null)
                    missing.Add("execValue");
            }
        }
        else if (valueText is null)
        {
            missing.Add("execValue");
        }

        string external = "";
        if (execId is not null)
        {
            external = ExternalTradeId(environment, category, execId);
            if (external.Length > 200)
                reasons.Add("execId dài hơn external_trade_id VARCHAR(200).");
        }

        var mapped = reasons.Count == 0;
        return new ExchangeExecutionItemDto
        {
            Mappable = mapped,
            ExecId = execId,
            OrderId = orderId,
            Symbol = symbol,
            Side = side,
            Quantity = quantity,
            Price = price,
            TotalValue = total,
            TotalValueBasis = basis,
            FeeAmount = fee,
            FeeCurrency = feeCurrency,
            ExecutedAt = executedAt == default ? null : executedAt,
            ExecType = execType,
            IsMaker = BoolText(item, "isMaker"),
            FeeRate = Text(item, "feeRate"),
            ClosedSize = Text(item, "closedSize"),
            ExternalTradeId = external.Length <= 200 ? external : "",
            Reason = string.Join(" ", reasons),
            MissingFields = missing,
            ExecPriceText = Bound(priceText),
            ExecQtyText = Bound(qtyText),
            ExecFeeText = Bound(feeText),
            ExecValueText = Bound(valueText),
            MarketType = category switch
            {
                "spot" => ExchangeMarket.Spot,
                "linear" or "inverse" => ExchangeMarket.Futures,
                _ => null,
            },
            ProductSubtype = category,
            QuantityUnit = "execQty",
            SourceEndpoint = Endpoint,
        };
    }

    static bool Fits(decimal value, int integerDigits, int scale)
    {
        var rounded = decimal.Round(Math.Abs(value), scale, MidpointRounding.AwayFromZero);
        decimal limit = 1;
        for (var i = 0; i < integerDigits; i++)
            limit *= 10;
        return rounded < limit;
    }

    static bool TryDecimal(string text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);

    static bool TryTime(JsonElement item, out DateTimeOffset when)
    {
        when = default;
        if (!item.TryGetProperty("execTime", out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return false;
        var text = value.ValueKind == JsonValueKind.Number ? value.GetRawText() : value.GetString();
        if (string.IsNullOrWhiteSpace(text) || !long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var ms))
            return false;
        try
        {
            when = DateTimeOffset.FromUnixTimeMilliseconds(ms);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    static string? BoolText(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.True)
            return "true";
        if (value.ValueKind == JsonValueKind.False)
            return "false";
        return Text(item, name);
    }

    static string? Text(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    static string? Bound(string? value) =>
        value is null ? null : value.Length <= 64 ? value : value[..64];
}
