using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KitPlatform.Packs.PersonalIntelligence.Trading;

public sealed record ResearchCandle(
    string Source,
    string Symbol,
    string Interval,
    DateTimeOffset OpenTime,
    DateTimeOffset CloseTime,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    string RawPayload);

public sealed record CandleIntake(
    IReadOnlyList<ResearchCandle> Accepted,
    int Duplicates,
    int Stale,
    int FutureOrIncomplete,
    int Gaps,
    IReadOnlyList<string> Problems);

/// <summary>
/// Reads Binance public kline JSON. A candle is usable only after it has closed
/// at or before asOf, and only when it is not older than maxStale.
/// </summary>
public static class ResearchCandles
{
    public const string BinanceSource = "binance-public-klines";
    public const string Interval = "1h";
    public static readonly TimeSpan Bar = TimeSpan.FromHours(1);

    public static CandleIntake ParseBinance(string json, string symbol, DateTimeOffset asOf, TimeSpan maxStale)
    {
        if (maxStale <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maxStale), "maxStale must be positive.");
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Kline payload must be a JSON array.");

        var parsed = new List<ResearchCandle>();
        var malformed = new List<string>();
        foreach (var row in document.RootElement.EnumerateArray())
        {
            if (!TryParseRow(row, symbol, out var candle, out var problem))
            {
                malformed.Add(problem);
                continue;
            }

            parsed.Add(candle);
        }

        return Classify(parsed, asOf, maxStale, malformed);
    }

    public static CandleIntake Classify(IReadOnlyList<ResearchCandle> candles, DateTimeOffset asOf, TimeSpan maxStale)
        => Classify(candles, asOf, maxStale, []);

    private static CandleIntake Classify(
        IReadOnlyList<ResearchCandle> candles,
        DateTimeOffset asOf,
        TimeSpan maxStale,
        IReadOnlyList<string> malformed)
    {
        var ordered = candles.OrderBy(candle => candle.OpenTime).ToArray();
        var duplicates = 0;
        var stale = 0;
        var future = 0;
        var accepted = new List<ResearchCandle>();
        var seen = new HashSet<long>();
        var problems = new List<string>(malformed);
        foreach (var candle in ordered)
        {
            var openMs = candle.OpenTime.ToUnixTimeMilliseconds();
            if (!seen.Add(openMs))
            {
                duplicates++;
                continue;
            }

            if (candle.OpenTime > asOf || candle.CloseTime > asOf)
            {
                future++;
                continue;
            }

            if (candle.CloseTime < asOf - maxStale)
            {
                stale++;
                continue;
            }

            accepted.Add(candle);
        }

        var gaps = 0;
        for (var i = 1; i < accepted.Count; i++)
        {
            var delta = accepted[i].OpenTime - accepted[i - 1].OpenTime;
            if (delta != Bar)
            {
                gaps++;
                problems.Add($"gap:{accepted[i - 1].OpenTime:O}->{accepted[i].OpenTime:O}");
            }
        }

        return new CandleIntake(accepted, duplicates, stale, future, gaps, problems);
    }

    private static bool TryParseRow(JsonElement row, string symbol, out ResearchCandle candle, out string problem)
    {
        candle = null!;
        problem = "";
        if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 7)
        {
            problem = "Kline row is too short.";
            return false;
        }

        var openMs = row[0].GetInt64();
        var closeMs = row[6].GetInt64();
        if (closeMs - openMs != (long)Bar.TotalMilliseconds - 1)
        {
            problem = "Kline close time is not one 1h bar minus 1ms.";
            return false;
        }

        if (!TryPrice(row[1], out var open)
            || !TryPrice(row[2], out var high)
            || !TryPrice(row[3], out var low)
            || !TryPrice(row[4], out var close))
        {
            problem = "Kline price is not a positive number.";
            return false;
        }

        if (high < open || high < close || low > open || low > close || low > high)
        {
            problem = "Kline high/low does not contain the open and close.";
            return false;
        }

        var openTime = DateTimeOffset.FromUnixTimeMilliseconds(openMs);
        candle = new ResearchCandle(
            BinanceSource,
            symbol,
            Interval,
            openTime,
            DateTimeOffset.FromUnixTimeMilliseconds(closeMs),
            open,
            high,
            low,
            close,
            row.GetRawText());
        return true;
    }

    private static bool TryPrice(JsonElement element, out decimal price)
    {
        var text = element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out price) && price > 0m;
    }
}

public static class PublicMarketRequest
{
    public const string HostName = "api.binance.com";
    public const string KlinesPath = "/api/v3/klines";
    public const int MaxLimit = 500;

    public static Uri Klines(string symbol, string interval, int limit)
        => Klines(symbol, interval, limit, null);

    public static Uri Klines(string symbol, string interval, int limit, long? endTime)
    {
        if (!string.Equals(interval, ResearchCandles.Interval, StringComparison.Ordinal))
            throw new InvalidOperationException("Trading Research V1 reads 1h klines only.");
        if (limit < 1 || limit > MaxLimit)
            throw new InvalidOperationException("Kline limit must be from 1 to 500.");
        if (symbol.Length is 0 or > 32 || symbol.Any(ch => !char.IsAsciiLetterOrDigit(ch)))
            throw new InvalidOperationException("Symbol is invalid.");
        if (endTime is < 0)
            throw new InvalidOperationException("Kline end time is invalid.");

        var query = $"symbol={symbol}&interval=1h&limit={limit.ToString(CultureInfo.InvariantCulture)}";
        if (endTime is not null)
            query += "&endTime=" + endTime.Value.ToString(CultureInfo.InvariantCulture);
        var uri = new Uri($"https://{HostName}{KlinesPath}?{query}");
        EnsureReadOnly("GET", uri);
        return uri;
    }

    public static void EnsureReadOnly(string method, Uri uri)
    {
        if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Research market data is GET only.");
        if (!string.Equals(uri.Host, HostName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Research V1 refuses this market host.");
        if (!string.Equals(uri.AbsolutePath, KlinesPath, StringComparison.Ordinal))
            throw new InvalidOperationException("Research V1 refuses this market path.");
        if (uri.AbsoluteUri.Contains("testnet", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Research V1 does not call testnet.");
    }
}

public static class ResearchRunGuard
{
    public static void Ensure(int candles, int maxCandles, int httpRequests, int maxHttpRequests, TimeSpan elapsed, TimeSpan maxRuntime)
    {
        if (candles > maxCandles)
            throw new InvalidOperationException("Candle count exceeds the research run limit.");
        if (httpRequests > maxHttpRequests)
            throw new InvalidOperationException("HTTP request count exceeds the research run limit.");
        if (elapsed > maxRuntime)
            throw new InvalidOperationException("Research run exceeded its time limit.");
    }
}

public static class ResearchHash
{
    public static string Sha256(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
