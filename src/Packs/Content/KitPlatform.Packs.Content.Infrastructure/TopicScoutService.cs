using System.Text.Json;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;
using Microsoft.Extensions.Configuration;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class TopicScoutService : ITopicScoutService
{
    private const string Api = "https://www.googleapis.com/youtube/v3";
    private static readonly string[] Regions = ["VN", "US", "GB", "AU"];
    private static readonly string[] Categories = ["24", "1"];

    private readonly IDbConnectionFactory _db;
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _cfg;
    private readonly IContentPackageService _packages;

    public TopicScoutService(
        IDbConnectionFactory db,
        IHttpClientFactory http,
        IConfiguration cfg,
        IContentPackageService packages)
    {
        _db = db;
        _http = http;
        _cfg = cfg;
        _packages = packages;
    }

    public async Task<IReadOnlyList<TopicScoutProposalDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id AS Id, lane AS Lane, title AS Title, brief AS Brief, status AS Status,
                   package_id AS PackageId, scanned_at AS ScannedAt
            FROM pack_content.topic_scout_proposal
            WHERE status IN ('pending', 'accepted')
            ORDER BY CASE status WHEN 'pending' THEN 0 ELSE 1 END, scanned_at DESC
            LIMIT 40
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<Row>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.Select(Map).ToList();
    }

    public async Task<TopicScoutRunDto> RunAsync(CancellationToken cancellationToken = default)
    {
        var key = Key();
        if (key is null)
            throw new InvalidOperationException(
                "Chưa có khóa YouTube Data API. Đặt Content:YoutubeDataApiKey hoặc biến YOUTUBE_DATA_API_KEY. Không dùng khóa tải video lên.");

        var clips = new List<TopicScoutClip>();
        foreach (var region in Regions)
        {
            foreach (var category in Categories)
                clips.AddRange(await ChartAsync(key, region, category, cancellationToken));
        }

        var channels = clips.Select(c => c.ChannelId).Distinct(StringComparer.Ordinal).ToList();
        var subs = await SubscribersAsync(key, channels, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var kept = clips
            .Select(clip => subs.TryGetValue(clip.ChannelId, out var count) ? clip with { Subscribers = count } : clip)
            .Where(clip => TopicScoutRules.Keep(clip, now))
            .GroupBy(clip => clip.VideoId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

        var saved = new Dictionary<string, int> { ["vi"] = 0, ["en"] = 0 };
        var keys = new Dictionary<string, List<string>> { ["vi"] = [], ["en"] = [] };
        foreach (var lane in new[] { "vi", "en" })
        {
            var clusters = TopicScoutRules.Cluster(kept.Where(clip => clip.Lane == lane).ToList());
            foreach (var cluster in clusters)
            {
                var (title, brief) = TopicScoutRules.Brief(cluster);
                await UpsertAsync(cluster, title, brief, cancellationToken);
                saved[lane]++;
                keys[lane].Add(cluster.Key);
            }
            await DismissStaleAsync(lane, keys[lane], cancellationToken);
        }

        return new TopicScoutRunDto(
            saved["vi"],
            saved["en"],
            $"Phần Việt: {saved["vi"]} phiếu. Phần Anh: {saved["en"]} phiếu. Chỉ video ngắn của kênh dưới 100.000 người đăng ký, trong 90 ngày, từ 50.000 lượt xem.");
    }

    public async Task<TopicScoutProposalDto> AcceptAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await RequirePendingAsync(id, cancellationToken);
        var created = await _packages.CreatePoolAsync(
            new CreatePoolIdeasRequest(null,
            [
                new PoolIdeaDraft(
                    row.Title,
                    row.Brief,
                    null,
                    row.Title,
                    row.Title,
                    row.Lane == "vi" ? "Người xem tiếng Việt, video ngắn" : "English-speaking short-video viewers",
                    "traffic",
                    "youtube",
                    FirstUrl(row.EvidenceJson),
                    "topic_scout",
                    row.Brief,
                    "fact"),
            ]),
            cancellationToken);
        var packageId = created.Packages[0].Id;
        const string sql = """
            UPDATE pack_content.topic_scout_proposal
            SET status = 'accepted', package_id = @PackageId
            WHERE id = @Id AND status = 'pending'
            RETURNING id AS Id, lane AS Lane, title AS Title, brief AS Brief, status AS Status,
                      package_id AS PackageId, scanned_at AS ScannedAt
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var updated = await conn.QuerySingleAsync<Row>(
            new CommandDefinition(sql, new { Id = id, PackageId = packageId }, cancellationToken: cancellationToken));
        return Map(updated);
    }

    public async Task<TopicScoutProposalDto> DismissAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE pack_content.topic_scout_proposal
            SET status = 'dismissed'
            WHERE id = @Id AND status = 'pending'
            RETURNING id AS Id, lane AS Lane, title AS Title, brief AS Brief, status AS Status,
                      package_id AS PackageId, scanned_at AS ScannedAt
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<Row>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        if (row is null)
            throw new InvalidOperationException("Phiếu không còn ở trạng thái chờ.");
        return Map(row);
    }

    private async Task<Row> RequirePendingAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, lane AS Lane, title AS Title, brief AS Brief, status AS Status,
                   package_id AS PackageId, scanned_at AS ScannedAt, evidence_json::text AS EvidenceJson
            FROM pack_content.topic_scout_proposal
            WHERE id = @Id AND status = 'pending'
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Row>(
                   new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
               ?? throw new InvalidOperationException("Phiếu không còn ở trạng thái chờ.");
    }

    private async Task UpsertAsync(TopicScoutCluster cluster, string title, string brief, CancellationToken cancellationToken)
    {
        var evidence = JsonSerializer.Serialize(cluster.Clips.Select(clip => new
        {
            clip.VideoId,
            clip.Title,
            clip.ChannelTitle,
            clip.Views,
            clip.Subscribers,
            clip.DurationSeconds,
            publishedAt = clip.PublishedAt,
        }));
        const string sql = """
            INSERT INTO pack_content.topic_scout_proposal (lane, cluster_key, title, brief, evidence_json, status)
            VALUES (@Lane, @Key, @Title, @Brief, @Evidence::jsonb, 'pending')
            ON CONFLICT (lane, cluster_key) DO UPDATE
            SET title = EXCLUDED.title,
                brief = EXCLUDED.brief,
                evidence_json = EXCLUDED.evidence_json,
                scanned_at = NOW()
            WHERE pack_content.topic_scout_proposal.status = 'pending'
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            cluster.Lane,
            Key = cluster.Key,
            Title = title.Length <= 200 ? title : title[..200],
            Brief = brief,
            Evidence = evidence,
        }, cancellationToken: cancellationToken));
    }

    private async Task DismissStaleAsync(string lane, IReadOnlyList<string> keys, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE pack_content.topic_scout_proposal
            SET status = 'dismissed'
            WHERE lane = @Lane AND status = 'pending'
              AND NOT (cluster_key = ANY(@Keys))
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(new CommandDefinition(
            sql, new { Lane = lane, Keys = keys.ToArray() }, cancellationToken: cancellationToken));
    }

    private async Task<IReadOnlyList<TopicScoutClip>> ChartAsync(
        string key, string region, string category, CancellationToken cancellationToken)
    {
        var url = $"{Api}/videos?part=snippet,statistics,contentDetails&chart=mostPopular"
                  + $"&regionCode={region}&videoCategoryId={category}&maxResults=25&key={Uri.EscapeDataString(key)}";
        var json = await GetAsync(url, cancellationToken);
        return TopicScoutChartParser.Videos(json, region);
    }

    private async Task<Dictionary<string, long>> SubscribersAsync(
        string key, IReadOnlyList<string> channelIds, CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, long>(StringComparer.Ordinal);
        for (var i = 0; i < channelIds.Count; i += 50)
        {
            var batch = string.Join(',', channelIds.Skip(i).Take(50));
            var url = $"{Api}/channels?part=statistics&id={Uri.EscapeDataString(batch)}&key={Uri.EscapeDataString(key)}";
            foreach (var pair in TopicScoutChartParser.Subscribers(await GetAsync(url, cancellationToken)))
                map[pair.Key] = pair.Value;
        }
        return map;
    }

    private async Task<string> GetAsync(string url, CancellationToken cancellationToken)
    {
        var client = _http.CreateClient("content-topic-scout");
        using var response = await client.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("YouTube từ chối lần quét (" + (int)response.StatusCode + ").");
        return body;
    }

    private string? Key()
    {
        var configured = _cfg["Content:YoutubeDataApiKey"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.Trim();
        var env = Environment.GetEnvironmentVariable("YOUTUBE_DATA_API_KEY");
        return string.IsNullOrWhiteSpace(env) ? null : env.Trim();
    }

    private static string? FirstUrl(string? evidenceJson)
    {
        if (string.IsNullOrWhiteSpace(evidenceJson))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(evidenceJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return null;
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.TryGetProperty("VideoId", out var id) || item.TryGetProperty("videoId", out id))
                {
                    var value = id.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        return "https://www.youtube.com/watch?v=" + value;
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }
        return null;
    }

    private static TopicScoutProposalDto Map(Row row) =>
        new(row.Id, row.Lane, row.Title, row.Brief, row.Status, row.PackageId,
            new DateTimeOffset(DateTime.SpecifyKind(row.ScannedAt, DateTimeKind.Utc)));

    private sealed class Row
    {
        public Guid Id { get; init; }
        public string Lane { get; init; } = "";
        public string Title { get; init; } = "";
        public string Brief { get; init; } = "";
        public string Status { get; init; } = "";
        public Guid? PackageId { get; init; }
        public DateTime ScannedAt { get; init; }
        public string? EvidenceJson { get; init; }
    }
}
