using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

/// <summary>Một bài đọc từ Fanpage hoặc YouTube, trước khi ghi bảng.</summary>
public sealed record ContentPlatformIncoming(
    string ExternalId,
    string Kind,
    string? Title,
    string? Caption,
    string? Permalink,
    string Status,
    DateTimeOffset? PublishAt);

/// <summary>Dữ liệu của một bài trong Kit để ghép với bài trên nền tảng.</summary>
public sealed record ContentPlatformMatchCandidate(
    Guid TopicId,
    string? Title,
    IReadOnlyList<string> FileNames,
    IReadOnlyList<string> Captions,
    IReadOnlyList<string> ExternalIds);

/// <summary>Hàng platform_post vừa đủ để Advisor chặn lịch.</summary>
public sealed record ContentPlatformPostSnapshot(
    Guid ChannelId,
    string Connector,
    string Kind,
    string Status,
    DateTimeOffset? PublishAt,
    Guid? MatchedTopicId);

public sealed record ContentPlatformPostDto(
    Guid Id,
    Guid BrandId,
    Guid ChannelId,
    string ChannelName,
    string Connector,
    string ExternalId,
    string Kind,
    string? Title,
    string? Caption,
    string? Permalink,
    string Status,
    DateTimeOffset? PublishAt,
    Guid? MatchedTopicId,
    string? MatchedTopicTitle,
    DateTimeOffset SyncedAt,
    string SourceLabel);

public sealed record ContentPlatformSyncChannelDto(
    string ChannelName,
    string Connector,
    int Upserted,
    int Matched,
    int Unmatched,
    string? Warning);

public sealed record ContentPlatformSyncReportDto(
    int Channels,
    int Upserted,
    int Matched,
    int Unmatched,
    IReadOnlyList<ContentPlatformSyncChannelDto> ByChannel,
    IReadOnlyList<string> Warnings,
    bool MigrationRequired);

public sealed record ContentPlatformMatchRequest(Guid? TopicId);

public interface IContentPlatformSyncService
{
    Task<ContentPlatformSyncReportDto> SyncBrandAsync(Guid brandId, CancellationToken cancellationToken = default);

    Task<ContentPlatformSyncReportDto> SyncAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentPlatformPostDto>> ListAsync(
        Guid brandId,
        bool unmatchedOnly,
        CancellationToken cancellationToken = default);

    Task<ContentPlatformPostDto> MatchAsync(Guid id, Guid? topicId, CancellationToken cancellationToken = default);

    /// <summary>Ghi một lịch hẹn ngoài Graph (Business Suite không trả bài). match_source = manual.</summary>
    Task<ContentPlatformPostDto> MarkOutsideAsync(
        Guid topicId,
        Guid channelId,
        DateTimeOffset publishAt,
        string? kind,
        CancellationToken cancellationToken = default);
}

public sealed record ContentPlatformManualScheduleRequest(
    Guid TopicId,
    Guid ChannelId,
    DateTimeOffset PublishAt,
    string? Kind);

/// <summary>Ghép bài nền tảng với topic, và quyết định huỷ job đăng khi bài đã có trên kênh.</summary>
public static class ContentPlatformRules
{
    public const string MigrationHint =
        "Chưa có bảng pack_content.platform_post. Cần migration 442 trước khi đồng bộ lịch nền tảng.";

    public const string CancelReason = "Đã có trên nền tảng";

    private static readonly Regex Hashtag = new(@"#[\p{L}\p{N}_]+", RegexOptions.Compiled);

    public static bool Applies(string? connector) =>
        connector is "facebook_page" or "youtube_channel";

    public static bool ShouldCancel(string? connector, Guid? channelId, bool topicAlreadyOnChannel) =>
        channelId is not null && topicAlreadyOnChannel && Applies(connector);

    public static string SourceLabel(string? connector) =>
        connector == "youtube_channel" ? "YouTube Studio" : "Business Suite";

    /// <summary>Bỏ dấu, hashtag và mọi ký tự không phải chữ hoặc số.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var s = Hashtag.Replace(text.Trim().ToLowerInvariant(), " ").Replace('đ', 'd');
        var form = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(form.Length);
        foreach (var ch in form)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>80 ký tự đầu của caption, rồi chuẩn hoá.</summary>
    public static string CaptionKey(string? caption)
    {
        if (string.IsNullOrWhiteSpace(caption)) return "";
        var slice = caption.Trim();
        if (slice.Length > 80) slice = slice[..80];
        return Normalize(slice);
    }

    /// <summary>
    /// Khớp chắc khi đúng một topic. external_id của job ưu tiên trước;
    /// không có thì so tên file, tiêu đề, hoặc 80 ký tự đầu caption.
    /// </summary>
    public static Guid? Match(
        string? externalId,
        string? title,
        string? caption,
        IReadOnlyList<ContentPlatformMatchCandidate> candidates)
    {
        var ext = (externalId ?? "").Trim();
        if (ext.Length > 0)
        {
            var byExt = candidates
                .Where(c => c.ExternalIds.Any(id => SameExternal(ext, id)))
                .Select(c => c.TopicId)
                .Distinct()
                .ToList();
            if (byExt.Count == 1) return byExt[0];
            if (byExt.Count > 1) return null;
        }

        var incoming = Keys(title, caption, [], []).ToHashSet(StringComparer.Ordinal);
        if (incoming.Count == 0) return null;
        var hits = new List<Guid>();
        foreach (var c in candidates)
        {
            if (Keys(c.Title, null, c.FileNames, c.Captions).Any(incoming.Contains))
                hits.Add(c.TopicId);
        }
        var distinct = hits.Distinct().ToList();
        if (distinct.Count == 1) return distinct[0];
        if (distinct.Count > 1) return null;

        var heads = new List<string>();
        var titleHead = CaptionKey(title);
        if (titleHead.Length >= 16) heads.Add(titleHead);
        var captionHead = CaptionKey(caption);
        if (captionHead.Length >= 16 && !heads.Contains(captionHead, StringComparer.Ordinal))
            heads.Add(captionHead);
        var incomingBlob = Normalize(title) + captionHead;
        var contained = new List<Guid>();
        foreach (var c in candidates)
        {
            var blob = Normalize(c.Title);
            foreach (var text in c.Captions) blob += Normalize(text);
            var fileHit = false;
            foreach (var file in c.FileNames)
            {
                var stem = Normalize(Path.GetFileNameWithoutExtension(file ?? ""));
                if (stem.Length >= 8)
                {
                    blob += stem;
                    if (incomingBlob.Contains(stem, StringComparison.Ordinal)) fileHit = true;
                }
            }
            if (fileHit || heads.Any(h => blob.Contains(h, StringComparison.Ordinal)))
                contained.Add(c.TopicId);
        }
        var containedDistinct = contained.Distinct().ToList();
        return containedDistinct.Count == 1 ? containedDistinct[0] : null;
    }

    public static ContentScheduleExistingJob ToBlockingJob(ContentPlatformPostSnapshot row)
    {
        var scheduled = string.Equals(row.Status, "scheduled", StringComparison.OrdinalIgnoreCase);
        var format = row.Kind switch
        {
            "short" or "reel" => "short",
            "video" => "long",
            _ => null,
        };
        return new ContentScheduleExistingJob(
            row.MatchedTopicId ?? Guid.Empty,
            null,
            row.ChannelId,
            scheduled ? "Queued" : "Succeeded",
            row.PublishAt,
            format,
            row.Connector == "youtube_channel" ? "YouTube" : "Fanpage",
            row.PublishAt is not null);
    }

    private static bool SameExternal(string platformId, string jobId)
    {
        var a = platformId.Trim();
        var b = jobId.Trim();
        if (a.Length == 0 || b.Length == 0) return false;
        if (string.Equals(a, b, StringComparison.Ordinal)) return true;
        return EndsWithId(a, b) || EndsWithId(b, a);
    }

    private static bool EndsWithId(string full, string id) =>
        id.Length >= 5
        && full.Length > id.Length + 1
        && full[full.Length - id.Length - 1] == '_'
        && full.EndsWith(id, StringComparison.Ordinal);

    private static IEnumerable<string> Keys(
        string? title,
        string? caption,
        IReadOnlyList<string> files,
        IReadOnlyList<string> captions)
    {
        var titleKey = Normalize(title);
        if (titleKey.Length >= 8) yield return titleKey;
        var cap = CaptionKey(caption);
        if (cap.Length >= 8) yield return cap;
        foreach (var file in files)
        {
            var stem = Normalize(Path.GetFileNameWithoutExtension(file ?? ""));
            if (stem.Length >= 8) yield return stem;
        }
        foreach (var text in captions)
        {
            var key = CaptionKey(text);
            if (key.Length >= 8) yield return key;
        }
    }
}

/// <summary>Đọc JSON Graph và YouTube Data API. Không gọi mạng.</summary>
public static class ContentPlatformReaders
{
    public static IReadOnlyList<ContentPlatformIncoming> ParseFacebookEdge(
        string edge,
        string json,
        DateTimeOffset now,
        DateTimeOffset since)
    {
        var list = new List<ContentPlatformIncoming>();
        using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != System.Text.Json.JsonValueKind.Array)
            return list;
        foreach (var item in data.EnumerateArray())
        {
            var id = Str(item, "id");
            if (string.IsNullOrWhiteSpace(id)) continue;
            var created = ReadTime(item, "created_time");
            var scheduledAt = ReadTime(item, "scheduled_publish_time");
            var unpublished = ReadBool(item, "is_published") == false || ReadBool(item, "published") == false;
            var scheduled = edge == "scheduled_posts"
                || unpublished
                || (scheduledAt is { } at && at > now);
            if (!scheduled && created is { } made && made < since) continue;
            if (!scheduled && created is null && edge is "videos" or "video_reels") continue;

            var kind = edge switch
            {
                "video_reels" => "reel",
                "videos" => LengthSeconds(item) is > 0 and <= 60 ? "short" : "video",
                _ => "post",
            };
            var caption = Str(item, "message") ?? Str(item, "description");
            list.Add(new ContentPlatformIncoming(
                id.Trim(),
                kind,
                Clip(Str(item, "title"), 300),
                caption,
                Str(item, "permalink_url"),
                scheduled ? "scheduled" : "published",
                scheduled ? scheduledAt : created ?? scheduledAt));
        }
        return list;
    }

    public static IReadOnlyList<ContentPlatformIncoming> Merge(IEnumerable<ContentPlatformIncoming> items)
    {
        var map = new Dictionary<string, ContentPlatformIncoming>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.ExternalId)) continue;
            if (!map.TryGetValue(item.ExternalId, out var prev))
            {
                map[item.ExternalId] = item;
                continue;
            }
            map[item.ExternalId] = Prefer(prev, item);
        }
        return map.Values.ToList();
    }

    public static string? NextGraphPage(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        if (!doc.RootElement.TryGetProperty("paging", out var paging)
            || !paging.TryGetProperty("next", out var next)
            || next.ValueKind != System.Text.Json.JsonValueKind.String)
            return null;
        var url = next.GetString();
        if (string.IsNullOrWhiteSpace(url)) return null;
        return url.StartsWith("https://graph.facebook.com/", StringComparison.Ordinal) ? url : null;
    }

    public static string? ParseUploadsPlaylistId(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        if (!doc.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
            return null;
        var it = items[0];
        if (!it.TryGetProperty("contentDetails", out var details)
            || !details.TryGetProperty("relatedPlaylists", out var lists)
            || !lists.TryGetProperty("uploads", out var uploads))
            return null;
        var id = uploads.GetString();
        return string.IsNullOrWhiteSpace(id) ? null : id.Trim();
    }

    public static IReadOnlyList<string> ParsePlaylistVideoIds(string json)
    {
        var ids = new List<string>();
        using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != System.Text.Json.JsonValueKind.Array)
            return ids;
        foreach (var item in items.EnumerateArray())
        {
            string? id = null;
            if (item.TryGetProperty("contentDetails", out var details) && details.TryGetProperty("videoId", out var vid))
                id = vid.GetString();
            if (string.IsNullOrWhiteSpace(id)) continue;
            ids.Add(id.Trim());
        }
        return ids;
    }

    public static string? NextPageToken(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        if (!doc.RootElement.TryGetProperty("nextPageToken", out var token)
            || token.ValueKind != System.Text.Json.JsonValueKind.String)
            return null;
        var value = token.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static IReadOnlyList<ContentPlatformIncoming> ParseYoutubeVideos(
        string json,
        DateTimeOffset now,
        DateTimeOffset since)
    {
        var list = new List<ContentPlatformIncoming>();
        using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != System.Text.Json.JsonValueKind.Array)
            return list;
        foreach (var item in items.EnumerateArray())
        {
            var id = Str(item, "id");
            if (string.IsNullOrWhiteSpace(id)) continue;
            var snippet = item.TryGetProperty("snippet", out var sn) ? sn : default;
            var status = item.TryGetProperty("status", out var st) ? st : default;
            var title = snippet.ValueKind == System.Text.Json.JsonValueKind.Object ? Str(snippet, "title") : null;
            var description = snippet.ValueKind == System.Text.Json.JsonValueKind.Object ? Str(snippet, "description") : null;
            var uploaded = snippet.ValueKind == System.Text.Json.JsonValueKind.Object ? ReadTime(snippet, "publishedAt") : null;
            var publishAt = status.ValueKind == System.Text.Json.JsonValueKind.Object ? ReadTime(status, "publishAt") : null;
            var scheduled = publishAt is { } at && at > now;
            var when = scheduled ? publishAt : uploaded ?? publishAt;
            if (!scheduled && (when is null || when < since)) continue;
            var blob = (title ?? "") + "\n" + (description ?? "");
            var kind = blob.Contains("#shorts", StringComparison.OrdinalIgnoreCase) ? "short" : "video";
            list.Add(new ContentPlatformIncoming(
                id.Trim(),
                kind,
                Clip(title, 300),
                description,
                kind == "short" ? $"https://www.youtube.com/shorts/{id.Trim()}" : $"https://www.youtube.com/watch?v={id.Trim()}",
                scheduled ? "scheduled" : "published",
                when));
        }
        return list;
    }

    private static ContentPlatformIncoming Prefer(ContentPlatformIncoming a, ContentPlatformIncoming b)
    {
        var kind = Rank(b.Kind) > Rank(a.Kind) ? b.Kind : a.Kind;
        var scheduled = a.Status == "scheduled" || b.Status == "scheduled";
        DateTimeOffset? at = scheduled
            ? (a.Status == "scheduled" ? a.PublishAt : null) ?? (b.Status == "scheduled" ? b.PublishAt : null) ?? a.PublishAt ?? b.PublishAt
            : a.PublishAt ?? b.PublishAt;
        return new ContentPlatformIncoming(
            a.ExternalId,
            kind,
            First(a.Title, b.Title),
            First(a.Caption, b.Caption),
            First(a.Permalink, b.Permalink),
            scheduled ? "scheduled" : "published",
            at);
    }

    private static int Rank(string kind) => kind switch { "reel" => 4, "short" => 3, "video" => 2, _ => 1 };

    private static string? First(string? a, string? b) => string.IsNullOrWhiteSpace(a) ? b : a;

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var t = value.Trim();
        return t.Length <= max ? t : t[..max];
    }

    private static string? Str(System.Text.Json.JsonElement el, string name)
    {
        if (el.ValueKind != System.Text.Json.JsonValueKind.Object || !el.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == System.Text.Json.JsonValueKind.String) return v.GetString();
        return null;
    }

    private static bool? ReadBool(System.Text.Json.JsonElement el, string name)
    {
        if (el.ValueKind != System.Text.Json.JsonValueKind.Object || !el.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == System.Text.Json.JsonValueKind.True) return true;
        if (v.ValueKind == System.Text.Json.JsonValueKind.False) return false;
        if (v.ValueKind == System.Text.Json.JsonValueKind.String && bool.TryParse(v.GetString(), out var parsed))
            return parsed;
        return null;
    }

    private static double? LengthSeconds(System.Text.Json.JsonElement el)
    {
        if (!el.TryGetProperty("length", out var v) || v.ValueKind != System.Text.Json.JsonValueKind.Number) return null;
        return v.TryGetDouble(out var n) ? n : null;
    }

    private static DateTimeOffset? ReadTime(System.Text.Json.JsonElement el, string name)
    {
        if (el.ValueKind != System.Text.Json.JsonValueKind.Object || !el.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == System.Text.Json.JsonValueKind.Number && v.TryGetInt64(out var unix))
            return DateTimeOffset.FromUnixTimeSeconds(unix);
        if (v.ValueKind != System.Text.Json.JsonValueKind.String) return null;
        var s = v.GetString();
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (long.TryParse(s, out var unixText) && unixText > 1_000_000_000)
            return DateTimeOffset.FromUnixTimeSeconds(unixText);
        return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto)
            ? dto
            : null;
    }
}
