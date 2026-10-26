using KitPlatform.Packs.PersonalIntelligence;

namespace KitPlatform.Packs.PersonalIntelligence.Trading;

/// <summary>
/// An imported fill is an observation. It is not a decision.
/// Idempotency key matches migration 385: trading account + external trade id.
/// </summary>
public sealed record ObservedTrade(
    Guid TradingAccountId,
    string ExternalTradeId,
    string Symbol,
    string Side,
    decimal Quantity,
    decimal Price,
    DateTimeOffset ExecutedAt,
    decimal? FeeAmount,
    string? FeeCurrency,
    EpistemicKind Kind);

public sealed record TradeImportResult(
    IReadOnlyList<ObservedTrade> Accepted,
    IReadOnlyList<string> DuplicateExternalIds);

public static class HistoricalTradeImport
{
    public static TradeImportResult ImportCsv(
        Guid tradingAccountId,
        string csv,
        IReadOnlySet<string> alreadyStored)
    {
        if (tradingAccountId == Guid.Empty)
            throw new InvalidOperationException("Import requires a trading account.");

        var accepted = new List<ObservedTrade>();
        var duplicates = new List<string>();
        var seen = new HashSet<string>(alreadyStored, StringComparer.Ordinal);
        var lines = csv.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = lines.Length > 0 && lines[0].StartsWith("external_trade_id", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        for (var i = start; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0)
                continue;

            var parts = line.Split(',');
            if (parts.Length < 6)
                throw new InvalidOperationException($"Import row {i + 1} does not have the required columns.");

            var externalId = parts[0].Trim();
            if (!seen.Add(externalId))
            {
                duplicates.Add(externalId);
                continue;
            }

            accepted.Add(new ObservedTrade(
                tradingAccountId,
                externalId,
                parts[1].Trim(),
                parts[2].Trim().ToUpperInvariant(),
                decimal.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture),
                decimal.Parse(parts[4], System.Globalization.CultureInfo.InvariantCulture),
                DateTimeOffset.Parse(parts[5], System.Globalization.CultureInfo.InvariantCulture),
                parts.Length > 6 && parts[6].Length > 0
                    ? decimal.Parse(parts[6], System.Globalization.CultureInfo.InvariantCulture)
                    : null,
                parts.Length > 7 && parts[7].Length > 0 ? parts[7].Trim() : null,
                EpistemicKind.Observation));
        }

        return new TradeImportResult(accepted, duplicates);
    }
}
