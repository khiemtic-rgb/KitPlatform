using System.Text.Json;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

public static class OkxExecutionMapper
{
    public static string InstType(string subtype) => subtype switch
    {
        "spot" => "SPOT",
        "swap" => "SWAP",
        "expiry" => "FUTURES",
        _ => throw new ExchangeConnectionException(400, "unsupported_category", $"OKX chưa hỗ trợ subtype {subtype}."),
    };

    public static string ExternalTradeId(string environment, string subtype, string instId, string tradeId) =>
        $"OKX:{environment}:{subtype}:{instId}:{tradeId}";

    public static ExchangeHistoryPage ReadPage(JsonElement data, string environment, string subtype, int limit)
    {
        var expected = InstType(subtype);
        var page = new ExchangeHistoryPage();
        if (data.ValueKind != JsonValueKind.Array)
            throw new ExchangeConnectionException(502, "exchange", "OKX không trả data dạng danh sách cho fills-history.");
        foreach (var item in data.EnumerateArray())
        {
            var actual = ExchangeParse.Text(item, "instType");
            if (actual is not null && !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new ExchangeConnectionException(502, "category_mismatch", $"OKX trả instType {actual} trong khi yêu cầu {expected}. Không ghi lẫn loại sản phẩm.");
            }

            var mapped = MapItem(item, environment, subtype);
            page.Items.Add(mapped);
            if (mapped.ExecutedAt is DateTimeOffset when)
            {
                var ms = when.ToUnixTimeMilliseconds();
                if (page.OldestMs is null || ms < page.OldestMs)
                    page.OldestMs = ms;
            }
        }

        page.PageFull = limit > 0 && page.Items.Count >= limit;
        page.NextCursor = page.PageFull ? page.Items[^1].ExecId : null;
        return page;
    }

    public static ExchangeExecutionItemDto MapItem(JsonElement item, string environment, string subtype)
    {
        var (market, product) = ExchangeMarket.ForSubtype("OKX", subtype);
        var reasons = new List<string>();
        var missing = new List<string>();
        var instType = ExchangeParse.Text(item, "instType");
        if (instType is "MARGIN" or "OPTION")
            reasons.Add($"instType {instType} không thuộc SPOT hoặc FUTURES đã hỗ trợ. Không ghi vào sổ.");
        var instId = ExchangeParse.Text(item, "instId");
        var tradeId = ExchangeParse.Text(item, "tradeId");
        var orderId = ExchangeParse.Text(item, "ordId");
        var priceText = ExchangeParse.Text(item, "fillPx");
        var qtyText = ExchangeParse.Text(item, "fillSz");
        var feeText = ExchangeParse.Text(item, "fee");
        var feeCurrency = ExchangeParse.Text(item, "feeCcy");
        var side = ExchangeParse.Text(item, "side")?.ToUpperInvariant() switch
        {
            "BUY" => "BUY",
            "SELL" => "SELL",
            _ => null,
        };
        if (tradeId is null)
            reasons.Add("Thiếu tradeId.");
        if (instId is null || instId.Length > 64)
            reasons.Add("Thiếu instId hoặc instId dài hơn VARCHAR(64).");
        if (side is null)
            reasons.Add("side của OKX không phải buy hoặc sell.");
        var price = ReadPositive(priceText, "fillPx", reasons);
        var quantity = ReadPositive(qtyText, "fillSz", reasons);
        var executedAt = ExchangeParse.UnixMs(item, "ts");
        if (executedAt is null)
            reasons.Add("Thiếu ts.");
        decimal? fee = null;
        if (feeText is null)
            missing.Add("fee");
        else if (!ExchangeParse.TryDecimal(feeText, out var parsedFee) || !ExchangeParse.Fits(parsedFee, 16, 8))
            missing.Add("fee");
        else
            fee = ExchangeParse.Round(parsedFee, 8);
        if (fee is not null && feeCurrency is null)
            missing.Add("feeCcy");
        decimal? total = null;
        var basis = "none";
        if (price is not null && quantity is not null)
        {
            var productValue = price.Value * quantity.Value;
            if (productValue >= 0 && ExchangeParse.Fits(productValue, 20, 4))
            {
                total = ExchangeParse.Round(productValue, 4);
                basis = "fillSz*fillPx";
            }
        }

        var external = tradeId is null || instId is null ? "" : ExternalTradeId(environment, subtype, instId, tradeId);
        if (external.Length > 200)
            reasons.Add("Mã khớp lệnh dài hơn external_trade_id VARCHAR(200).");
        var pnl = ExchangeParse.Text(item, "fillPnl") ?? ExchangeParse.Text(item, "pnl");
        return new ExchangeExecutionItemDto
        {
            Mappable = reasons.Count == 0,
            ExecId = tradeId,
            OrderId = orderId,
            Symbol = instId,
            Side = side,
            Quantity = quantity,
            Price = price,
            TotalValue = total,
            TotalValueBasis = basis,
            FeeAmount = fee,
            FeeCurrency = feeCurrency,
            ExecutedAt = executedAt,
            ExecType = ExchangeParse.Text(item, "execType") ?? "Trade",
            ExternalTradeId = external.Length <= 200 ? external : "",
            Reason = string.Join(" ", reasons),
            MissingFields = missing,
            ExecPriceText = ExchangeParse.Bound(priceText),
            ExecQtyText = ExchangeParse.Bound(qtyText),
            ExecFeeText = ExchangeParse.Bound(feeText),
            MarketType = market,
            ProductSubtype = product,
            RealizedPnl = pnl,
            PositionSide = ExchangeParse.Text(item, "posSide"),
            QuantityUnit = "fillSz",
            SourceEndpoint = "GET /api/v5/trade/fills-history",
        };
    }

    static decimal? ReadPositive(string? text, string name, List<string> reasons)
    {
        if (text is null)
        {
            reasons.Add($"Thiếu {name}.");
            return null;
        }

        if (!ExchangeParse.TryDecimal(text, out var value))
        {
            reasons.Add($"{name} không phải số.");
            return null;
        }

        if (value <= 0)
        {
            reasons.Add($"{name} không dương nên không ghi vào sổ.");
            return null;
        }

        if (!ExchangeParse.Fits(value, 12, 12))
        {
            reasons.Add($"{name} vượt numeric(24,12).");
            return null;
        }

        return ExchangeParse.Round(value, 12);
    }
}
