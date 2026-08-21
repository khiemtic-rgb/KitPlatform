using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Gemini Brand Fit JSON often returns outline/reason as a string[] instead of a string.
/// Strict STJ then kills the whole job and Idea Pool stays «Chưa chấm».
/// </summary>
internal sealed class JsonStringOrListConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetDouble(out var n) ? n.ToString("G", System.Globalization.CultureInfo.InvariantCulture) : reader.GetInt64().ToString(),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.StartArray => ReadArray(ref reader),
            JsonTokenType.StartObject => SkipObject(ref reader),
            _ => null,
        };
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value);
    }

    private static string? ReadArray(ref Utf8JsonReader reader)
    {
        var parts = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    if (reader.GetString() is { Length: > 0 } s) parts.Add(s);
                    break;
                case JsonTokenType.Number:
                    parts.Add(reader.TryGetDouble(out var n)
                        ? n.ToString("G", System.Globalization.CultureInfo.InvariantCulture)
                        : reader.GetInt64().ToString());
                    break;
                case JsonTokenType.True:
                case JsonTokenType.False:
                    parts.Add(reader.GetBoolean() ? "true" : "false");
                    break;
                case JsonTokenType.StartArray:
                case JsonTokenType.StartObject:
                    using (var doc = JsonDocument.ParseValue(ref reader))
                    {
                        var text = FlattenElement(doc.RootElement);
                        if (!string.IsNullOrWhiteSpace(text)) parts.Add(text);
                    }
                    break;
            }
        }

        return parts.Count == 0 ? null : string.Join("\n", parts);
    }

    private static string? SkipObject(ref Utf8JsonReader reader)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return FlattenElement(doc.RootElement);
    }

    private static string FlattenElement(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.String) return el.GetString() ?? "";
        if (el.ValueKind == JsonValueKind.Number) return el.GetRawText();
        if (el.ValueKind is JsonValueKind.True or JsonValueKind.False) return el.GetRawText();
        if (el.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var item in el.EnumerateArray())
            {
                var t = FlattenElement(item);
                if (string.IsNullOrWhiteSpace(t)) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(t);
            }
            return sb.ToString();
        }

        if (el.ValueKind == JsonValueKind.Object
            && el.TryGetProperty("text", out var text)
            && text.ValueKind == JsonValueKind.String)
        {
            return text.GetString() ?? "";
        }

        return "";
    }
}

internal sealed class JsonFlexibleStringListConverter : JsonConverter<List<string>?>
{
    public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString()?.Trim();
            return string.IsNullOrWhiteSpace(s) ? [] : [s];
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return [];
        }

        var list = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var item = reader.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(item)) list.Add(item);
            }
            else if (reader.TokenType is JsonTokenType.StartArray or JsonTokenType.StartObject)
            {
                using var doc = JsonDocument.ParseValue(ref reader);
            }
            else
            {
                reader.Skip();
            }
        }

        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<string>? value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        if (value is not null)
        {
            foreach (var item in value) writer.WriteStringValue(item);
        }
        writer.WriteEndArray();
    }
}

internal sealed class JsonFlexibleIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt32(out var i)) return Math.Clamp(i, 0, 100);
            if (reader.TryGetDouble(out var d)) return Math.Clamp((int)Math.Round(d), 0, 100);
        }

        if (reader.TokenType == JsonTokenType.String
            && int.TryParse(reader.GetString(), out var parsed))
        {
            return Math.Clamp(parsed, 0, 100);
        }

        reader.Skip();
        return 0;
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}

internal sealed record ContentAdaptFitRow(
    string? BrandCode,
    string? Verdict,
    int Score,
    string? Reason,
    string? Title,
    string? Angle,
    string? Audience,
    string? Cta,
    string? Outline);

internal sealed record ContentAdaptParsed(
    string? Insight,
    string? Problem,
    string? CoreMessage,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<ContentAdaptFitRow> Fits);

/// <summary>Walk Gemini Brand Fit JSON with JsonDocument — STJ property converters do not run on this payload.</summary>
internal static class ContentAdaptJson
{
    public static ContentAdaptParsed Parse(string raw)
    {
        var json = StripFence(raw);
        using var doc = JsonDocument.Parse(json);
        return FromRoot(doc.RootElement);
    }

    public static ContentAdaptParsed ParseLenient(string raw)
    {
        try
        {
            return Parse(raw);
        }
        catch (JsonException)
        {
            var repaired = RepairTruncatedJson(StripFence(raw));
            try
            {
                return Parse(repaired);
            }
            catch (JsonException)
            {
                var partial = ExtractPartialFits(repaired);
                if (partial.Count == 0)
                    throw new InvalidOperationException(
                        "AI cắt JSON Brand Fit giữa chừng. Chấm lại — mỗi lần ít brand hơn nếu vẫn lỗi.");
                return new ContentAdaptParsed(null, null, null, [], partial);
            }
        }
    }

    private static ContentAdaptParsed FromRoot(JsonElement root)
    {
        string? insight = null, problem = null, coreMessage = null;
        var keywords = new List<string>();
        if (TryProp(root, out var coreEl, "coreIdea", "core_idea"))
        {
            insight = ReadFlexible(coreEl, "insight");
            problem = ReadFlexible(coreEl, "problem");
            coreMessage = ReadFlexible(coreEl, "coreMessage", "core_message");
            keywords = ReadList(coreEl, "keywords");
        }

        var fits = new List<ContentAdaptFitRow>();
        if (TryProp(root, out var fitsEl, "fits") && fitsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in fitsEl.EnumerateArray())
            {
                var mapped = MapFit(row);
                if (mapped is not null) fits.Add(mapped);
            }
        }

        if (fits.Count == 0)
            throw new InvalidOperationException("AI không trả fits[] Brand Fit.");

        return new ContentAdaptParsed(insight, problem, coreMessage, keywords, fits);
    }

    private static ContentAdaptFitRow? MapFit(JsonElement row)
    {
        if (row.ValueKind != JsonValueKind.Object) return null;
        var code = ReadFlexible(row, "brandCode", "brand_code");
        if (string.IsNullOrWhiteSpace(code)) return null;
        return new ContentAdaptFitRow(
            code,
            ReadFlexible(row, "verdict"),
            ReadScore(row),
            ReadFlexible(row, "reason"),
            ReadFlexible(row, "title"),
            ReadFlexible(row, "angle"),
            ReadFlexible(row, "audience"),
            ReadFlexible(row, "cta"),
            ReadFlexible(row, "outline"));
    }

    /// <summary>Close an unterminated string / array / object when Gemini hits max tokens.</summary>
    internal static string RepairTruncatedJson(string raw)
    {
        var s = raw.Trim();
        var start = s.IndexOf('{');
        if (start < 0) return s;
        s = s[start..];

        var closer = new List<char>();
        var inString = false;
        var escape = false;
        foreach (var c in s)
        {
            if (inString)
            {
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                if (c == '"') inString = false;
                continue;
            }

            switch (c)
            {
                case '"':
                    inString = true;
                    break;
                case '{':
                    closer.Add('}');
                    break;
                case '[':
                    closer.Add(']');
                    break;
                case '}' or ']':
                    if (closer.Count > 0 && closer[^1] == c)
                        closer.RemoveAt(closer.Count - 1);
                    break;
            }
        }

        var sb = new StringBuilder(s);
        if (inString)
        {
            if (escape) sb.Append('n');
            sb.Append('"');
        }

        while (sb.Length > 0 && (char.IsWhiteSpace(sb[^1]) || sb[^1] == ','))
            sb.Length--;

        for (var i = closer.Count - 1; i >= 0; i--)
            sb.Append(closer[i]);
        return sb.ToString();
    }

    private static List<ContentAdaptFitRow> ExtractPartialFits(string json)
    {
        var fits = new List<ContentAdaptFitRow>();
        var s = json;
        var idx = 0;
        while (idx < s.Length)
        {
            var open = s.IndexOf('{', idx);
            if (open < 0) break;
            var depth = 0;
            var inString = false;
            var escape = false;
            var end = -1;
            for (var i = open; i < s.Length; i++)
            {
                var c = s[i];
                if (inString)
                {
                    if (escape) { escape = false; continue; }
                    if (c == '\\') { escape = true; continue; }
                    if (c == '"') inString = false;
                    continue;
                }

                if (c == '"') { inString = true; continue; }
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) { end = i; break; }
                }
            }

            if (end < 0) break;
            var slice = s[open..(end + 1)];
            idx = end + 1;
            try
            {
                using var doc = JsonDocument.Parse(slice);
                var mapped = MapFit(doc.RootElement);
                if (mapped is not null) fits.Add(mapped);
            }
            catch (JsonException)
            {
                // skip incomplete object
            }
        }

        return fits;
    }

    private static string StripFence(string raw)
    {
        var s = raw.Trim();
        if (s.StartsWith("```", StringComparison.Ordinal))
        {
            var nl = s.IndexOf('\n');
            if (nl >= 0)
            {
                s = s[(nl + 1)..];
                var fence = s.LastIndexOf("```", StringComparison.Ordinal);
                if (fence >= 0) s = s[..fence];
                s = s.Trim();
            }
        }

        var start = s.IndexOf('{');
        if (start < 0) return s;
        var end = s.LastIndexOf('}');
        return end > start ? s[start..(end + 1)] : s[start..];
    }

    private static bool TryProp(JsonElement obj, out JsonElement value, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out value))
                return true;
        }

        value = default;
        return false;
    }

    private static string? ReadFlexible(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var el))
                continue;
            var text = Flatten(el);
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }

        return null;
    }

    private static List<string> ReadList(JsonElement obj, params string[] names)
    {
        var list = new List<string>();
        foreach (var name in names)
        {
            if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var el))
                continue;
            if (el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
                return list;
            }

            if (el.ValueKind != JsonValueKind.Array) continue;
            foreach (var item in el.EnumerateArray())
            {
                var s = Flatten(item);
                if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
            }

            return list;
        }

        return list;
    }

    private static int ReadScore(JsonElement obj)
    {
        if (!TryProp(obj, out var el, "score")) return 0;
        if (el.ValueKind == JsonValueKind.Number)
        {
            if (el.TryGetInt32(out var i)) return Math.Clamp(i, 0, 100);
            if (el.TryGetDouble(out var d)) return Math.Clamp((int)Math.Round(d), 0, 100);
        }

        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var parsed))
            return Math.Clamp(parsed, 0, 100);
        return 0;
    }

    private static string Flatten(JsonElement el) =>
        el.ValueKind switch
        {
            JsonValueKind.String => el.GetString()?.Trim() ?? "",
            JsonValueKind.Number => el.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Array => string.Join(
                "\n",
                el.EnumerateArray().Select(Flatten).Where(s => !string.IsNullOrWhiteSpace(s))),
            JsonValueKind.Object when el.TryGetProperty("text", out var t) => Flatten(t),
            _ => "",
        };
}
