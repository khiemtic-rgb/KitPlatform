using System.Globalization;
using System.Text;
using System.Text.Json;

namespace KitPlatform.Packs.Content;

public sealed record TopicScoutClip(
    string Lane,
    string Region,
    string VideoId,
    string Title,
    string ChannelId,
    string ChannelTitle,
    long Views,
    long Subscribers,
    int DurationSeconds,
    DateTimeOffset PublishedAt);

public sealed record TopicScoutCluster(
    string Lane,
    string Label,
    string Key,
    IReadOnlyList<TopicScoutClip> Clips);

public sealed record TopicScoutProposalDto(
    Guid Id,
    string Lane,
    string Title,
    string Brief,
    string Status,
    Guid? PackageId,
    DateTimeOffset ScannedAt);

public sealed record TopicScoutRunDto(
    int Vietnamese,
    int English,
    string Message);

public interface ITopicScoutService
{
    Task<IReadOnlyList<TopicScoutProposalDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<TopicScoutRunDto> RunAsync(CancellationToken cancellationToken = default);
    Task<TopicScoutProposalDto> AcceptAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TopicScoutProposalDto> DismissAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>Quét bảng thịnh hành theo lịch. Mặc định tắt. Chỉ bật trên host content.</summary>
public sealed class TopicScoutOptions
{
    public const string SectionName = "Content:TopicScout";

    /// <summary>Env Content__TopicScout__WorkerEnabled. Không đưa phiếu vào pool.</summary>
    public bool WorkerEnabled { get; set; }

    /// <summary>Khoảng cách giữa hai lần quét. Giới hạn 1–24 giờ.</summary>
    public int IntervalHours { get; set; } = 6;
}

/// <summary>
/// Chọn video ngắn từ bảng thịnh hành rồi gom thành phiếu. Không nhận từ khóa.
/// Phần Việt chỉ lấy video ở Việt Nam có chữ Việt. Phần Anh chỉ lấy Mỹ, Anh, Úc và tiêu đề không có chữ Việt.
/// </summary>
public static class TopicScoutRules
{
    public const int MaxPerLane = 4;
    public const long MinViews = 50_000;
    public const long MaxSubscribers = 100_000;
    public const int MaxDurationSeconds = 60;
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(90);

    private static readonly string[] EnglishRegions = ["US", "GB", "AU"];

    private static readonly HashSet<string> Stop = new(StringComparer.Ordinal)
    {
        "the", "and", "you", "your", "this", "that", "with", "from", "have", "what", "when", "where",
        "how", "why", "official", "video", "shorts", "short", "watch", "new", "like", "just", "about",
        "they", "them", "she", "her", "his", "him", "was", "are", "for", "not", "but", "all", "can",
        "out", "get", "got", "its", "dont", "did", "does", "will", "would", "could", "should", "than",
        "then", "into", "over", "after", "before", "really", "people", "channel", "subscribe", "please",
        "follow", "part", "episode", "cua", "va", "la", "mot", "nhung", "trong", "cho", "voi", "khong",
        "duoc", "nay", "cac", "khi", "tren", "da", "co", "bi", "thi", "lai", "cung", "nhu", "ve", "tu",
        "den", "hay", "hoac", "tap", "phan", "moi", "xem", "ngay", "day", "nha", "nhe", "minh", "ban",
        "anh", "chi", "em",
    };

    public static string? LaneOf(string regionCode, string title)
    {
        var region = (regionCode ?? "").Trim().ToUpperInvariant();
        var vietnamese = HasVietnamese(title);
        if (region == "VN" && vietnamese)
            return "vi";
        if (EnglishRegions.Contains(region) && !vietnamese && HasLatin(title))
            return "en";
        return null;
    }

    public static bool Keep(TopicScoutClip clip, DateTimeOffset now) =>
        clip.DurationSeconds is > 0 and <= MaxDurationSeconds
        && clip.Views >= MinViews
        && clip.Subscribers >= 0
        && clip.Subscribers < MaxSubscribers
        && now - clip.PublishedAt <= MaxAge
        && now >= clip.PublishedAt;

    public static IReadOnlyList<TopicScoutCluster> Cluster(IReadOnlyList<TopicScoutClip> clips)
    {
        var lane = clips.Select(c => c.Lane).Distinct().ToList();
        if (lane.Count != 1)
            return [];
        var tokens = new Dictionary<string, List<TopicScoutClip>>(StringComparer.Ordinal);
        foreach (var clip in clips)
        {
            foreach (var token in Tokens(clip.Title))
            {
                if (!tokens.TryGetValue(token, out var list))
                {
                    list = [];
                    tokens[token] = list;
                }
                if (list.All(item => item.VideoId != clip.VideoId))
                    list.Add(clip);
            }
        }

        var ranked = tokens
            .Select(pair => (
                Token: pair.Key,
                Clips: pair.Value,
                Channels: pair.Value.Select(c => c.ChannelId).Distinct(StringComparer.Ordinal).Count(),
                Views: pair.Value.Sum(c => c.Views)))
            .Where(row => row.Clips.Count >= 3 && row.Channels >= 2)
            .OrderByDescending(row => row.Clips.Count)
            .ThenByDescending(row => row.Views)
            .ToList();

        var used = new HashSet<string>(StringComparer.Ordinal);
        var clusters = new List<TopicScoutCluster>();
        foreach (var row in ranked)
        {
            if (clusters.Count >= MaxPerLane)
                break;
            var take = row.Clips
                .Where(clip => used.Add(clip.VideoId))
                .OrderByDescending(clip => clip.Views)
                .Take(3)
                .ToList();
            if (take.Count < 3)
            {
                foreach (var clip in take)
                    used.Remove(clip.VideoId);
                continue;
            }
            var label = char.ToUpper(row.Token[0], CultureInfo.InvariantCulture) + row.Token[1..];
            clusters.Add(new TopicScoutCluster(lane[0], label, lane[0] + ":" + row.Token, take));
        }
        return clusters;
    }

    public static (string Title, string Brief) Brief(TopicScoutCluster cluster)
    {
        var top = cluster.Clips[0];
        var evidence = new StringBuilder();
        var n = 1;
        foreach (var clip in cluster.Clips)
        {
            evidence.Append(n++).Append(". ").Append(clip.ChannelTitle)
                .Append(" · ").Append(clip.Views.ToString("N0", CultureInfo.InvariantCulture)).Append(cluster.Lane == "vi" ? " lượt xem" : " views")
                .Append(" · ").Append(clip.Subscribers.ToString("N0", CultureInfo.InvariantCulture))
                .Append(cluster.Lane == "vi" ? " người đăng ký" : " subscribers")
                .Append(" · ").Append(clip.DurationSeconds).Append("s · ")
                .Append(clip.PublishedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Append(" · https://www.youtube.com/watch?v=").AppendLine(clip.VideoId);
        }

        if (cluster.Lane == "vi")
        {
            var title = "Tình huống: " + cluster.Label;
            var body = $"""
                Chủ đề. {title}.

                Người xem. Người xem tiếng Việt đang dừng ở video ngắn cùng tình huống «{cluster.Label}».

                Giây đầu. Câu trên màn hình lấy từ video được xem nhiều nhất trong cụm: «{top.Title}».

                Cách dựng. Một mặt, một phòng, một vật. Ảnh đứng rồi chuyển động ngắn. Không có cảnh đi bộ.

                Vì sao có cơ hội.
                {evidence.ToString().TrimEnd()}

                Cách làm tập này.
                - Cảnh 1: hiện đúng tình huống «{cluster.Label}».
                - Cảnh 2: người trong cuộc nhìn thấy.
                - Cảnh 3: một câu nói ngược với hình.
                - Cảnh 4: vật trên tay bị xoay ra.
                - Cảnh 5: dừng ở mặt, chưa giải quyết.

                Cách dừng. Sau 14 ngày, nếu người xem không xem quá nửa thời lượng thì dừng. Không dựng tập sau trước ngưỡng đó.
                """;
            return (title, body.Trim());
        }

        var englishTitle = "Situation: " + cluster.Label;
        var english = $"""
            Topic. {englishTitle}.

            Viewer. English-speaking viewers who stop on a short video of «{cluster.Label}».

            First second. On-screen line taken from the most-viewed video in this cluster: «{top.Title}».

            How to shoot. One face, one room, one object. A still, then a short move. No walking shot.

            Why this can work.
            {evidence.ToString().TrimEnd()}

            How this episode is made.
            - Shot 1: show «{cluster.Label}».
            - Shot 2: the other person sees it.
            - Shot 3: one spoken line that contradicts the picture.
            - Shot 4: the object is turned toward them.
            - Shot 5: hold on the face. No apology yet.

            When to stop. After 14 days, stop if viewers do not watch past half the runtime. Do not shoot the next episode before that.
            """;
        return (englishTitle, english.Trim());
    }

    public static int DurationSeconds(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso) || !iso.StartsWith("PT", StringComparison.Ordinal))
            return 0;
        var hours = 0;
        var minutes = 0;
        var seconds = 0;
        var number = 0;
        var any = false;
        foreach (var character in iso.AsSpan(2))
        {
            if (char.IsDigit(character))
            {
                number = number * 10 + (character - '0');
                any = true;
                continue;
            }
            if (character == 'H') hours = number;
            else if (character == 'M') minutes = number;
            else if (character == 'S') seconds = number;
            number = 0;
            any = false;
        }
        if (any)
            return 0;
        return hours * 3600 + minutes * 60 + seconds;
    }

    private static IEnumerable<string> Tokens(string title)
    {
        var folded = Fold(title);
        var buffer = new StringBuilder();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var character in folded + " ")
        {
            if (character is >= 'a' and <= 'z')
            {
                buffer.Append(character);
                continue;
            }
            if (buffer.Length >= 4)
            {
                var token = buffer.ToString();
                if (!Stop.Contains(token))
                    seen.Add(token);
            }
            buffer.Clear();
        }
        return seen;
    }

    private static string Fold(string value)
    {
        var replaced = (value ?? "").Replace('đ', 'd').Replace('Đ', 'D');
        var decomposed = replaced.Normalize(NormalizationForm.FormD);
        var buffer = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;
            buffer.Append(char.ToLowerInvariant(character));
        }
        return buffer.ToString();
    }

    private static bool HasVietnamese(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;
        const string marks = "ăâêôơưđĂÂÊÔƠƯĐáàảãạấầẩẫậắằẳẵặéèẻẽẹếềểễệíìỉĩịóòỏõọốồổỗộớờởỡợúùủũụứừửữựýỳỷỹỵ";
        return title.Any(marks.Contains);
    }

    private static bool HasLatin(string? title) =>
        (title ?? "").Any(character => character is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z'));
}

public static class TopicScoutChartParser
{
    public static IReadOnlyList<TopicScoutClip> Videos(string json, string regionCode)
    {
        var list = new List<TopicScoutClip>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var item in items.EnumerateArray())
        {
            var id = Text(item, "id");
            if (string.IsNullOrWhiteSpace(id))
                continue;
            if (!item.TryGetProperty("snippet", out var snippet))
                continue;
            var title = Text(snippet, "title");
            var lane = TopicScoutRules.LaneOf(regionCode, title ?? "");
            if (lane is null || string.IsNullOrWhiteSpace(title))
                continue;
            var channelId = Text(snippet, "channelId") ?? "";
            if (channelId.Length == 0)
                continue;
            var published = DateTimeOffset.TryParse(Text(snippet, "publishedAt"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)
                ? at
                : DateTimeOffset.MinValue;
            long views = 0;
            if (item.TryGetProperty("statistics", out var stats))
                long.TryParse(Text(stats, "viewCount"), NumberStyles.Integer, CultureInfo.InvariantCulture, out views);
            var duration = 0;
            if (item.TryGetProperty("contentDetails", out var details))
                duration = TopicScoutRules.DurationSeconds(Text(details, "duration"));
            list.Add(new TopicScoutClip(
                lane,
                regionCode.Trim().ToUpperInvariant(),
                id,
                title,
                channelId,
                Text(snippet, "channelTitle") ?? channelId,
                views,
                -1,
                duration,
                published));
        }
        return list;
    }

    public static IReadOnlyDictionary<string, long> Subscribers(string json)
    {
        var map = new Dictionary<string, long>(StringComparer.Ordinal);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return map;
        foreach (var item in items.EnumerateArray())
        {
            var id = Text(item, "id");
            if (string.IsNullOrWhiteSpace(id) || !item.TryGetProperty("statistics", out var stats))
                continue;
            if (stats.TryGetProperty("hiddenSubscriberCount", out var hidden)
                && hidden.ValueKind == JsonValueKind.True)
                continue;
            if (!long.TryParse(Text(stats, "subscriberCount"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
                continue;
            map[id] = count;
        }
        return map;
    }

    private static string? Text(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }
}
