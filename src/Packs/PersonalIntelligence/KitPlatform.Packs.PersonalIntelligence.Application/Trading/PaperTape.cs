using System.Globalization;

namespace KitPlatform.Packs.PersonalIntelligence.Trading;

public sealed class PaperTapeException : Exception
{
    public PaperTapeException(string message) : base(message)
    {
    }
}

/// <summary>
/// One internal tape row. The host does not invent a side, size, or price.
/// </summary>
public sealed record PaperTapeRow(
    string OperationKey,
    DateTimeOffset RecordedAt,
    string Symbol,
    string Side,
    decimal Quantity,
    decimal Price,
    decimal Confidence);

/// <summary>
/// CSV schema: operation_key,recorded_at,symbol,side,quantity,price,confidence.
/// recorded_at must be an explicit UTC or offset timestamp and must not go backwards.
/// </summary>
public static class PaperTape
{
    public const string Header = "operation_key,recorded_at,symbol,side,quantity,price,confidence";

    public static IReadOnlyList<PaperTapeRow> Read(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            throw new PaperTapeException("Tape is empty.");

        var lines = csv.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var start = 0;
        while (start < lines.Length && lines[start].Length == 0)
            start++;
        if (start >= lines.Length)
            throw new PaperTapeException("Tape is empty.");
        if (!string.Equals(lines[start], Header, StringComparison.Ordinal))
            throw new PaperTapeException($"Tape header must be {Header}.");

        var rows = new List<PaperTapeRow>();
        DateTimeOffset? previous = null;
        for (var i = start + 1; i < lines.Length; i++)
        {
            if (lines[i].Length == 0)
                continue;
            rows.Add(ParseRow(lines[i], i + 1, ref previous));
        }

        if (rows.Count == 0)
            throw new PaperTapeException("Tape has a header and no records.");
        return rows;
    }

    private static PaperTapeRow ParseRow(string line, int lineNumber, ref DateTimeOffset? previous)
    {
        if (line.Contains('"'))
            throw new PaperTapeException($"Line {lineNumber} contains quotes. The paper tape has no quoted fields.");

        var parts = line.Split(',');
        if (parts.Length != 7)
            throw new PaperTapeException($"Line {lineNumber} must have 7 columns.");

        var key = parts[0];
        if (key.Length is 0 or > 200 || key.Any(ch => !char.IsAsciiLetterOrDigit(ch) && ch is not ('.' or '_' or ':' or '-')))
            throw new PaperTapeException($"Line {lineNumber} has an invalid operation_key.");

        var rawTime = parts[1];
        if (!rawTime.EndsWith('Z') && !rawTime.Contains('+', StringComparison.Ordinal) && rawTime.LastIndexOf('-') <= 9)
            throw new PaperTapeException($"Line {lineNumber} recorded_at must include Z or an explicit offset.");
        if (!DateTimeOffset.TryParse(rawTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var recorded))
            throw new PaperTapeException($"Line {lineNumber} has an invalid recorded_at.");
        if (previous is not null && recorded < previous.Value)
            throw new PaperTapeException($"Line {lineNumber} is earlier than the previous record.");
        previous = recorded;

        var symbol = parts[2];
        if (symbol.Length is 0 or > 64 || symbol.Any(ch => !char.IsAsciiLetterOrDigit(ch)))
            throw new PaperTapeException($"Line {lineNumber} has an invalid symbol.");

        var side = parts[3];
        if (side is not ("BUY" or "SELL"))
            throw new PaperTapeException($"Line {lineNumber} side must be BUY or SELL.");

        if (!TryPositive(parts[4], out var quantity))
            throw new PaperTapeException($"Line {lineNumber} quantity must be positive.");
        if (!TryPositive(parts[5], out var price))
            throw new PaperTapeException($"Line {lineNumber} price must be positive.");
        if (!decimal.TryParse(parts[6], NumberStyles.Number, CultureInfo.InvariantCulture, out var confidence)
            || confidence < 0m
            || confidence > 1m)
            throw new PaperTapeException($"Line {lineNumber} confidence must be from 0 to 1.");

        return new PaperTapeRow(key, recorded, symbol, side, quantity, price, confidence);
    }

    private static bool TryPositive(string text, out decimal value)
    {
        var ok = decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        return ok && value > 0m;
    }
}
