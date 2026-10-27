using System.Globalization;
using System.Text.Json;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

static class ExchangeParse
{
    public static bool TryDecimal(string? text, out decimal value)
    {
        value = 0;
        return text is not null && decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    public static bool Fits(decimal value, int integerDigits, int scale)
    {
        var rounded = decimal.Round(Math.Abs(value), scale, MidpointRounding.AwayFromZero);
        decimal limit = 1;
        for (var i = 0; i < integerDigits; i++)
            limit *= 10;
        return rounded < limit;
    }

    public static decimal? Round(decimal value, int scale) =>
        decimal.Round(value, scale, MidpointRounding.AwayFromZero);

    public static string? Text(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    public static bool? Bool(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.True)
            return true;
        if (value.ValueKind == JsonValueKind.False)
            return false;
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase))
            return false;
        return null;
    }

    public static DateTimeOffset? UnixMs(JsonElement obj, string name)
    {
        var text = Text(obj, name);
        if (text is null || !long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var ms))
            return null;
        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(ms);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    public static string? Bound(string? value) =>
        value is null ? null : value.Length <= 64 ? value : value[..64];
}
