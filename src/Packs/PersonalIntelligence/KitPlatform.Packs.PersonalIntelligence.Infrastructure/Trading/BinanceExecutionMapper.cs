using System.Globalization;
using System.Text.Json;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

public static class BinanceExecutionMapper
{
    public static readonly string[] Quotes = ["USDT", "USDC", "FDUSD", "BTC"];

    public static string ExternalTradeId(string environment, string subtype, string symbol, string tradeId) =>
        $"BINANCE:{environment}:{subtype}:{symbol}:{tradeId}";

    public static List<string> SymbolsFromBalances(JsonElement account)
    {
        var symbols = new List<string>();
        if (!account.TryGetProperty("balances", out var balances) || balances.ValueKind != JsonValueKind.Array)
            return symbols;
        foreach (var row in balances.EnumerateArray())
        {
            var asset = ExchangeParse.Text(row, "asset");
            if (string.IsNullOrWhiteSpace(asset) || Quotes.Contains(asset))
                continue;
            if (!Positive(ExchangeParse.Text(row, "free")) && !Positive(ExchangeParse.Text(row, "locked")))
                continue;
            foreach (var quote in Quotes)
            {
                if (string.Equals(asset, quote, StringComparison.Ordinal))
                    continue;
                symbols.Add(asset + quote);
            }
        }

        return symbols.Distinct(StringComparer.Ordinal).ToList();
    }

    public static List<string> SymbolsFromIncome(JsonElement income)
    {
        var symbols = new List<string>();
        if (income.ValueKind != JsonValueKind.Array)
            return symbols;
        foreach (var row in income.EnumerateArray())
        {
            var symbol = ExchangeParse.Text(row, "symbol");
            if (!string.IsNullOrWhiteSpace(symbol))
                symbols.Add(symbol);
        }

        return symbols.Distinct(StringComparer.Ordinal).ToList();
    }

    public static ExchangeExecutionItemDto MapSpot(JsonElement item, string environment)
    {
        var side = ExchangeParse.Bool(item, "isBuyer") switch
        {
            true => "BUY",
            false => "SELL",
            _ => null,
        };
        return Map(item, environment, "spot", "GET /api/v3/myTrades", side, "qty", null, null);
    }

    public static ExchangeExecutionItemDto MapFutures(JsonElement item, string environment, string subtype, string endpoint)
    {
        var side = ExchangeParse.Text(item, "side")?.ToUpperInvariant();
        if (side is not ("BUY" or "SELL"))
            side = null;
        return Map(item, environment, subtype, endpoint, side, "qty", ExchangeParse.Text(item, "realizedPnl"), ExchangeParse.Text(item, "positionSide"));
    }

    static ExchangeExecutionItemDto Map(
        JsonElement item, string environment, string subtype, string endpoint, string? side, string quantityUnit, string? realizedPnl, string? positionSide)
    {
        var (market, product) = ExchangeMarket.ForSubtype("BINANCE", subtype);
        var reasons = new List<string>();
        var missing = new List<string>();
        var symbol = ExchangeParse.Text(item, "symbol");
        var tradeId = ExchangeParse.Text(item, "id");
        var orderId = ExchangeParse.Text(item, "orderId");
        var priceText = ExchangeParse.Text(item, "price");
        var qtyText = ExchangeParse.Text(item, "qty");
        var quoteText = ExchangeParse.Text(item, "quoteQty");
        var feeText = ExchangeParse.Text(item, "commission");
        var feeCurrency = ExchangeParse.Text(item, "commissionAsset");
        if (tradeId is null)
            reasons.Add("Thiếu id khớp lệnh Binance.");
        if (symbol is null || symbol.Length > 64)
            reasons.Add("Thiếu symbol hoặc symbol dài hơn VARCHAR(64).");
        if (side is null)
            reasons.Add("Không đọc được chiều BUY/SELL từ sàn.");
        var price = ReadPositive(priceText, "price", reasons);
        var quantity = ReadPositive(qtyText, "qty", reasons);
        var executedAt = ExchangeParse.UnixMs(item, "time");
        if (executedAt is null)
            reasons.Add("Thiếu thời điểm khớp.");
        decimal? fee = null;
        if (feeText is null)
            missing.Add("commission");
        else if (!ExchangeParse.TryDecimal(feeText, out var parsedFee) || !ExchangeParse.Fits(parsedFee, 16, 8))
            missing.Add("commission");
        else
            fee = ExchangeParse.Round(parsedFee, 8);
        if (fee is not null && feeCurrency is null)
            missing.Add("commissionAsset");
        decimal? total = null;
        var basis = "none";
        if (quoteText is not null && ExchangeParse.TryDecimal(quoteText, out var quote) && quote >= 0 && ExchangeParse.Fits(quote, 20, 4))
        {
            total = ExchangeParse.Round(quote, 4);
            basis = "quoteQty";
        }
        else if (price is not null && quantity is not null)
        {
            var productValue = price.Value * quantity.Value;
            if (productValue >= 0 && ExchangeParse.Fits(productValue, 20, 4))
            {
                total = ExchangeParse.Round(productValue, 4);
                basis = "quantity*price";
            }
        }

        var external = tradeId is null || symbol is null ? "" : ExternalTradeId(environment, subtype, symbol, tradeId);
        if (external.Length > 200)
            reasons.Add("Mã khớp lệnh dài hơn external_trade_id VARCHAR(200).");
        return new ExchangeExecutionItemDto
        {
            Mappable = reasons.Count == 0,
            ExecId = tradeId,
            OrderId = orderId,
            Symbol = symbol,
            Side = side,
            Quantity = quantity,
            Price = price,
            TotalValue = total,
            TotalValueBasis = basis,
            FeeAmount = fee,
            FeeCurrency = feeCurrency,
            ExecutedAt = executedAt,
            ExecType = "Trade",
            IsMaker = ExchangeParse.Bool(item, "isMaker")?.ToString().ToLowerInvariant() ?? ExchangeParse.Bool(item, "maker")?.ToString().ToLowerInvariant(),
            ExternalTradeId = external.Length <= 200 ? external : "",
            Reason = string.Join(" ", reasons),
            MissingFields = missing,
            ExecPriceText = ExchangeParse.Bound(priceText),
            ExecQtyText = ExchangeParse.Bound(qtyText),
            ExecFeeText = ExchangeParse.Bound(feeText),
            ExecValueText = ExchangeParse.Bound(quoteText),
            MarketType = market,
            ProductSubtype = product,
            RealizedPnl = realizedPnl,
            PositionSide = positionSide,
            QuantityUnit = quantityUnit,
            SourceEndpoint = endpoint,
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

    static bool Positive(string? text) =>
        ExchangeParse.TryDecimal(text, out var value) && value > 0;
}
