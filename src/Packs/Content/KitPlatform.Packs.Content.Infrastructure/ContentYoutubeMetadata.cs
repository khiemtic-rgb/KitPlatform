using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed record ContentYoutubeChapter(int StartSeconds, string Title);

/// <summary>Description, tags, chapters and the AI disclosure flag for a YouTube upload. No network calls.</summary>
internal static class ContentYoutubeMetadata
{
    public const int DescriptionMax = 4900;
    public const int TagsMax = 500;

    static readonly Regex Hashtag = new(@"(?<![\p{L}\p{N}_#])#([\p{L}\p{N}_]+)", RegexOptions.Compiled);

    /// <summary>
    /// Plain-text body, then chapters before the trailing hashtag lines, then the CTA and #Shorts.
    /// </summary>
    public static string BuildDescription(string plainBody, string? ctaUrl, bool shorts, string? chapterBlock)
    {
        var description = InsertChapters(plainBody.Trim(), chapterBlock);
        if (!string.IsNullOrWhiteSpace(ctaUrl) && !description.Contains(ctaUrl, StringComparison.OrdinalIgnoreCase))
            description = $"{description.Trim()}\n\n{ctaUrl}";
        if (shorts && !description.Contains("#Shorts", StringComparison.OrdinalIgnoreCase))
            description = $"{description.Trim()}\n\n#Shorts";
        return description.Length > DescriptionMax ? description[..DescriptionMax] : description;
    }

    static string InsertChapters(string body, string? chapterBlock)
    {
        if (string.IsNullOrWhiteSpace(chapterBlock)) return body;
        if (Regex.IsMatch(body, @"(?m)^0:00\s")) return body;
        var lines = body.Split('\n').ToList();
        var at = lines.Count;
        while (at > 0 && (string.IsNullOrWhiteSpace(lines[at - 1]) || IsHashtagLine(lines[at - 1])))
            at--;
        if (at == lines.Count)
            return $"{body}\n\n{chapterBlock}";
        var head = string.Join('\n', lines.Take(at)).TrimEnd();
        var tail = string.Join('\n', lines.Skip(at)).Trim();
        return $"{head}\n\n{chapterBlock}\n\n{tail}";
    }

    static bool IsHashtagLine(string line)
    {
        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length > 0 && words.All(w => w.StartsWith('#') && w.Length > 1);
    }

    public static IEnumerable<string> Hashtags(string text) =>
        Hashtag.Matches(text ?? "").Select(m => m.Groups[1].Value);

    /// <summary>Distinct tags without "#", kept in order while the YouTube 500-character budget allows.</summary>
    public static IReadOnlyList<string> BuildTags(IEnumerable<string?> candidates)
    {
        var tags = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var used = 0;
        foreach (var raw in candidates)
        {
            var tag = Clean(raw);
            if (tag.Length == 0 || string.Equals(tag, "Shorts", StringComparison.OrdinalIgnoreCase) || !seen.Add(tag))
                continue;
            // YouTube counts a comma between tags and quotes around a tag that has a space.
            var cost = tag.Length + (tag.Contains(' ') ? 2 : 0) + (tags.Count > 0 ? 1 : 0);
            if (used + cost > TagsMax) continue;
            tags.Add(tag);
            used += cost;
        }
        return tags;
    }

    static string Clean(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var s = raw.Trim().TrimStart('#').Replace("<", "", StringComparison.Ordinal).Replace(">", "", StringComparison.Ordinal)
            .Replace(",", " ", StringComparison.Ordinal);
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s.Length > 100 ? s[..100].Trim() : s;
    }

    /// <summary>Keyword lists stored on variants: seo_meta keywords/focusKeyword, youtube tags/hashtags.</summary>
    public static IEnumerable<string> VariantKeywords(string? metaJson)
    {
        if (string.IsNullOrWhiteSpace(metaJson)) yield break;
        JsonDocument doc;
        try { doc = JsonDocument.Parse(metaJson); }
        catch (JsonException) { yield break; }
        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object) yield break;
            foreach (var key in new[] { "tags", "hashtags", "keywords", "focusKeyword", "focus_keyword" })
            {
                if (!doc.RootElement.TryGetProperty(key, out var v)) continue;
                if (v.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in v.EnumerateArray())
                        if (item.ValueKind == JsonValueKind.String) yield return item.GetString()!;
                }
                else if (v.ValueKind == JsonValueKind.String)
                {
                    foreach (var part in v.GetString()!.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        yield return part;
                }
            }
        }
    }

    /// <summary>Reads "chapters" (or "youtubeChapters"): [{ "start": "0:45" | 45, "title": "..." }].</summary>
    public static IReadOnlyList<ContentYoutubeChapter> ParseChapters(string? json)
    {
        var list = new List<ContentYoutubeChapter>();
        if (string.IsNullOrWhiteSpace(json)) return list;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return list;
            if (!doc.RootElement.TryGetProperty("chapters", out var arr) && !doc.RootElement.TryGetProperty("youtubeChapters", out arr))
                return list;
            if (arr.ValueKind != JsonValueKind.Array) return list;
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var title = item.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString()?.Trim() : null;
                JsonElement start = default;
                var hasStart = item.TryGetProperty("start", out start) || item.TryGetProperty("at", out start) || item.TryGetProperty("time", out start);
                var seconds = hasStart ? Seconds(start) : null;
                if (seconds is int s && !string.IsNullOrWhiteSpace(title))
                    list.Add(new ContentYoutubeChapter(s, title));
            }
        }
        catch (JsonException) { }
        return list;
    }

    static int? Seconds(JsonElement v)
    {
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) && d >= 0) return (int)Math.Floor(d);
        if (v.ValueKind != JsonValueKind.String) return null;
        var parts = v.GetString()!.Trim().Split(':');
        if (parts.Length is < 1 or > 3) return null;
        var total = 0;
        foreach (var p in parts)
        {
            if (!int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out var n)) return null;
            total = total * 60 + n;
        }
        return total;
    }

    /// <summary>
    /// "0:00 Title" lines when YouTube would accept them: first at 0:00, at least 3, each chapter at least 10 s
    /// (the last one too when the video length is known). Null otherwise.
    /// </summary>
    public static string? ChapterBlock(IReadOnlyList<ContentYoutubeChapter> chapters, double? durationSeconds)
    {
        if (chapters.Count < 3) return null;
        var ordered = chapters.OrderBy(c => c.StartSeconds).ToList();
        if (ordered[0].StartSeconds != 0) return null;
        for (var i = 1; i < ordered.Count; i++)
            if (ordered[i].StartSeconds - ordered[i - 1].StartSeconds < 10) return null;
        if (durationSeconds is double d && d > 0 && d - ordered[^1].StartSeconds < 10) return null;
        var hours = ordered[^1].StartSeconds >= 3600;
        var sb = new StringBuilder();
        foreach (var c in ordered)
            sb.Append(Stamp(c.StartSeconds, hours)).Append(' ').Append(c.Title.Replace('\n', ' ')).Append('\n');
        return sb.ToString().TrimEnd();
    }

    static string Stamp(int s, bool hours) =>
        hours ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";

    /// <summary>
    /// status.containsSyntheticMedia: the topic decides (containsSyntheticMedia, aiVoice, aiVisual), else the channel
    /// default youtube.containsSyntheticMedia. Null leaves the field unset.
    /// </summary>
    public static bool? SyntheticMedia(string? topicExtraJson, string? channelYoutubeValue)
    {
        if (!string.IsNullOrWhiteSpace(topicExtraJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(topicExtraJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("containsSyntheticMedia", out var flag)
                        && flag.ValueKind is JsonValueKind.True or JsonValueKind.False)
                        return flag.GetBoolean();
                    foreach (var key in new[] { "aiVoice", "aiVisual" })
                        if (doc.RootElement.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True)
                            return true;
                }
            }
            catch (JsonException) { }
        }
        return bool.TryParse(channelYoutubeValue, out var channelDefault) ? channelDefault : null;
    }
}
