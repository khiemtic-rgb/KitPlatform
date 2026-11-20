using Dapper;
using Npgsql;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentPlatformPostStore
{
    private readonly IDbConnectionFactory _db;

    public ContentPlatformPostStore(IDbConnectionFactory db) => _db = db;

    public static bool IsMissingTable(Exception ex)
    {
        for (Exception? cur = ex; cur is not null; cur = cur.InnerException)
        {
            if (cur is PostgresException pg && pg.SqlState == "42P01") return true;
        }
        return false;
    }

    public async Task<IReadOnlyList<ContentPlatformPostSnapshot>> ListSnapshotsAsync(Guid brandId, CancellationToken ct)
    {
        const string sql = """
            SELECT p.channel_id AS ChannelId, p.connector AS Connector, p.kind AS Kind,
                   p.status AS Status, p.publish_at AS PublishAt, p.matched_topic_id AS MatchedTopicId
            FROM pack_content.platform_post p
            INNER JOIN pack_content.channel_target c ON c.id = p.channel_id
            WHERE c.brand_id = @BrandId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = (await conn.QueryAsync<SnapshotRow>(sql, new { BrandId = brandId })).ToList();
        return rows.Select(r => new ContentPlatformPostSnapshot(
            r.ChannelId, r.Connector, r.Kind, r.Status, r.PublishAt, r.MatchedTopicId)).ToList();
    }

    public async Task<bool> TopicOnChannelAsync(Guid topicId, Guid channelId, CancellationToken ct)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM pack_content.platform_post
                WHERE channel_id = @ChannelId
                  AND matched_topic_id = @TopicId
                  AND status IN ('scheduled', 'published')
            )
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(sql, new { ChannelId = channelId, TopicId = topicId });
    }

    public async Task<IReadOnlyList<ContentPlatformPostDto>> ListAsync(Guid brandId, bool unmatchedOnly, CancellationToken ct)
    {
        const string sql = """
            SELECT p.id AS Id, c.brand_id AS BrandId, p.channel_id AS ChannelId, c.name AS ChannelName,
                   p.connector AS Connector, p.external_id AS ExternalId, p.kind AS Kind,
                   p.title AS Title, p.caption AS Caption, p.permalink AS Permalink,
                   p.status AS Status, p.publish_at AS PublishAt,
                   p.matched_topic_id AS MatchedTopicId, t.title AS MatchedTopicTitle,
                   p.synced_at AS SyncedAt
            FROM pack_content.platform_post p
            INNER JOIN pack_content.channel_target c ON c.id = p.channel_id
            LEFT JOIN pack_content.topic t ON t.id = p.matched_topic_id
            WHERE c.brand_id = @BrandId
              AND (@Unmatched = FALSE OR p.matched_topic_id IS NULL)
            ORDER BY p.publish_at NULLS LAST, p.synced_at DESC
            LIMIT 300
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = (await conn.QueryAsync<PostRow>(sql, new { BrandId = brandId, Unmatched = unmatchedOnly })).ToList();
        return rows.Select(r => r.ToDto()).ToList();
    }

    public async Task<ContentPlatformPostDto?> GetAsync(Guid id, CancellationToken ct)
    {
        const string sql = """
            SELECT p.id AS Id, c.brand_id AS BrandId, p.channel_id AS ChannelId, c.name AS ChannelName,
                   p.connector AS Connector, p.external_id AS ExternalId, p.kind AS Kind,
                   p.title AS Title, p.caption AS Caption, p.permalink AS Permalink,
                   p.status AS Status, p.publish_at AS PublishAt,
                   p.matched_topic_id AS MatchedTopicId, t.title AS MatchedTopicTitle,
                   p.synced_at AS SyncedAt
            FROM pack_content.platform_post p
            INNER JOIN pack_content.channel_target c ON c.id = p.channel_id
            LEFT JOIN pack_content.topic t ON t.id = p.matched_topic_id
            WHERE p.id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<PostRow>(sql, new { Id = id });
        return row?.ToDto();
    }

    public async Task<ContentPlatformPostDto> MarkOutsideAsync(
        Guid topicId,
        Guid channelId,
        DateTimeOffset publishAt,
        string? kind,
        CancellationToken ct)
    {
        const string lookup = """
            SELECT t.title AS Title, t.brand_id AS TopicBrandId,
                   c.brand_id AS ChannelBrandId, c.channel_type AS ChannelType
            FROM pack_content.topic t
            CROSS JOIN pack_content.channel_target c
            WHERE t.id = @TopicId AND c.id = @ChannelId
            """;
        const string upsert = """
            INSERT INTO pack_content.platform_post (
                channel_id, connector, external_id, kind, title,
                status, publish_at, matched_topic_id, match_source, synced_at, updated_at
            ) VALUES (
                @ChannelId, @Connector, @ExternalId, @Kind, @Title,
                'scheduled', @PublishAt, @TopicId, 'manual', NOW(), NOW()
            )
            ON CONFLICT (channel_id, external_id) DO UPDATE SET
                connector = EXCLUDED.connector,
                kind = EXCLUDED.kind,
                title = EXCLUDED.title,
                status = 'scheduled',
                publish_at = EXCLUDED.publish_at,
                matched_topic_id = EXCLUDED.matched_topic_id,
                match_source = 'manual',
                updated_at = NOW()
            RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var src = await conn.QuerySingleOrDefaultAsync<OutsideRow>(lookup, new { TopicId = topicId, ChannelId = channelId });
        if (src is null) throw new InvalidOperationException("Không thấy bài viết hoặc kênh.");
        if (src.TopicBrandId != src.ChannelBrandId)
            throw new InvalidOperationException("Bài viết không thuộc thương hiệu của kênh này.");
        if (src.ChannelType is not ("facebook_page" or "youtube_channel"))
            throw new InvalidOperationException("Chỉ đánh dấu lịch Fanpage hoặc YouTube.");
        var resolved = string.IsNullOrWhiteSpace(kind)
            ? src.ChannelType == "youtube_channel" ? "short" : "reel"
            : kind.Trim();
        if (resolved is not ("post" or "reel" or "video" or "short"))
            throw new InvalidOperationException("Loại bài phải là post, reel, video hoặc short.");
        var id = await conn.ExecuteScalarAsync<Guid>(upsert, new
        {
            ChannelId = channelId,
            Connector = src.ChannelType,
            ExternalId = "manual:" + topicId.ToString("D"),
            Kind = resolved,
            Title = Clip(src.Title, 300),
            PublishAt = publishAt,
            TopicId = topicId,
        });
        return await GetAsync(id, ct) ?? throw new InvalidOperationException("Không thấy bài nền tảng vừa ghi.");
    }

    public async Task SetMatchAsync(Guid id, Guid? topicId, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.platform_post
            SET matched_topic_id = @TopicId,
                match_source = CASE WHEN @TopicId IS NULL THEN NULL ELSE 'manual' END,
                updated_at = NOW()
            WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var n = await conn.ExecuteAsync(sql, new { Id = id, TopicId = topicId });
        if (n == 0) throw new InvalidOperationException("Không thấy bài nền tảng.");
    }

    public async Task<IReadOnlyList<ContentPlatformMatchCandidate>> LoadCandidatesAsync(
        Guid brandId,
        Guid channelId,
        CancellationToken ct)
    {
        const string topicsSql = """
            SELECT id AS TopicId, title AS Title
            FROM pack_content.topic
            WHERE brand_id = @BrandId
            ORDER BY updated_at DESC
            LIMIT 1000
            """;
        const string filesSql = """
            SELECT topic_id AS TopicId, file_name AS Value
            FROM pack_content.asset
            WHERE kind = 'video' AND topic_id = ANY(@Ids)
            """;
        const string captionsSql = """
            SELECT topic_id AS TopicId, left(body_markdown, 400) AS Value
            FROM pack_content.variant
            WHERE topic_id = ANY(@Ids)
              AND kind IN ('fb_page', 'fb_short', 'social_caption', 'youtube', 'video_caption')
            """;
        const string jobsSql = """
            SELECT topic_id AS TopicId, external_ref AS Value
            FROM pack_content.publish_job
            WHERE brand_id = @BrandId
              AND channel_target_id = @ChannelId
              AND external_ref IS NOT NULL
              AND btrim(external_ref) <> ''
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var topics = (await conn.QueryAsync<TopicSeed>(topicsSql, new { BrandId = brandId })).ToList();
        if (topics.Count == 0) return [];
        var ids = topics.Select(t => t.TopicId).ToArray();
        var files = (await conn.QueryAsync<TopicValue>(filesSql, new { Ids = ids })).ToList();
        var captions = (await conn.QueryAsync<TopicValue>(captionsSql, new { Ids = ids })).ToList();
        var jobs = (await conn.QueryAsync<TopicValue>(jobsSql, new { BrandId = brandId, ChannelId = channelId })).ToList();
        return topics.Select(t => new ContentPlatformMatchCandidate(
            t.TopicId,
            t.Title,
            files.Where(f => f.TopicId == t.TopicId).Select(f => f.Value).ToList(),
            captions.Where(c => c.TopicId == t.TopicId).Select(c => c.Value).ToList(),
            jobs.Where(j => j.TopicId == t.TopicId).Select(j => j.Value).ToList())).ToList();
    }

    public async Task<(int Total, int Matched)> ReplaceChannelAsync(
        Guid channelId,
        string connector,
        IReadOnlyList<ContentPlatformIncoming> items,
        IReadOnlyDictionary<string, Guid> matchedByExternalId,
        bool deleteMissing,
        CancellationToken ct)
    {
        const string upsert = """
            INSERT INTO pack_content.platform_post (
                channel_id, connector, external_id, kind, title, caption, permalink,
                status, publish_at, matched_topic_id, match_source, synced_at, updated_at
            ) VALUES (
                @ChannelId, @Connector, @ExternalId, @Kind, @Title, @Caption, @Permalink,
                @Status, @PublishAt, @MatchedTopicId, @MatchSource, NOW(), NOW()
            )
            ON CONFLICT (channel_id, external_id) DO UPDATE SET
                connector = EXCLUDED.connector,
                kind = EXCLUDED.kind,
                title = EXCLUDED.title,
                caption = EXCLUDED.caption,
                permalink = EXCLUDED.permalink,
                status = EXCLUDED.status,
                publish_at = EXCLUDED.publish_at,
                synced_at = NOW(),
                updated_at = NOW(),
                matched_topic_id = CASE
                    WHEN platform_post.match_source = 'manual' THEN platform_post.matched_topic_id
                    ELSE EXCLUDED.matched_topic_id
                END,
                match_source = CASE
                    WHEN platform_post.match_source = 'manual' THEN platform_post.match_source
                    ELSE EXCLUDED.match_source
                END
            """;
        const string deleteSql = """
            DELETE FROM pack_content.platform_post
            WHERE channel_id = @ChannelId
              AND match_source IS DISTINCT FROM 'manual'
              AND NOT (external_id = ANY(@Ids))
            """;
        const string countSql = """
            SELECT COUNT(*)::int AS Total, COUNT(matched_topic_id)::int AS Matched
            FROM pack_content.platform_post
            WHERE channel_id = @ChannelId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.ExternalId)) continue;
            var matched = matchedByExternalId.TryGetValue(item.ExternalId, out var topicId) ? topicId : (Guid?)null;
            await conn.ExecuteAsync(upsert, new
            {
                ChannelId = channelId,
                Connector = connector,
                ExternalId = item.ExternalId.Trim(),
                Kind = item.Kind,
                Title = Clip(item.Title, 300),
                Caption = Clip(item.Caption, 500),
                Permalink = Clip(item.Permalink, 500),
                Status = item.Status,
                PublishAt = item.PublishAt,
                MatchedTopicId = matched,
                MatchSource = matched is null ? null : "auto",
            }, tx);
        }
        if (deleteMissing)
        {
            var ids = items.Select(i => i.ExternalId.Trim()).Where(s => s.Length > 0).Distinct().ToArray();
            await conn.ExecuteAsync(deleteSql, new { ChannelId = channelId, Ids = ids }, tx);
        }
        var count = await conn.QuerySingleAsync<CountRow>(countSql, new { ChannelId = channelId }, tx);
        await tx.CommitAsync(ct);
        return (count.Total, count.Matched);
    }

    public async Task<IReadOnlyList<ContentCalendarItemDto>> ListCalendarAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        Guid? brandId,
        CancellationToken ct)
    {
        const string sql = """
            SELECT p.publish_at AS At,
                   c.brand_id AS BrandId,
                   b.code AS BrandCode,
                   b.name AS BrandName,
                   COALESCE(NULLIF(btrim(p.title), ''), NULLIF(btrim(left(p.caption, 120)), ''), '(không tiêu đề)') AS Title,
                   p.connector AS Channel,
                   p.status AS Status,
                   p.matched_topic_id AS TopicId
            FROM pack_content.platform_post p
            INNER JOIN pack_content.channel_target c ON c.id = p.channel_id
            INNER JOIN pack_content.brand b ON b.id = c.brand_id
            WHERE p.publish_at IS NOT NULL
              AND p.publish_at >= @FromUtc
              AND p.publish_at < @ToUtc
              AND (@BrandId IS NULL OR c.brand_id = @BrandId)
            ORDER BY p.publish_at
            LIMIT 400
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = (await conn.QueryAsync<CalRow>(sql, new { FromUtc = fromUtc, ToUtc = toUtc, BrandId = brandId })).ToList();
        return rows.Select(r => new ContentCalendarItemDto(
            r.At,
            "platform",
            null,
            r.TopicId,
            null,
            r.BrandId,
            r.BrandCode,
            r.BrandName,
            r.Title,
            r.Channel,
            r.Status,
            ContentPlatformRules.SourceLabel(r.Channel))).ToList();
    }

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var t = value.Trim();
        return t.Length <= max ? t : t[..max];
    }

    private sealed class OutsideRow
    {
        public string? Title { get; set; }
        public Guid TopicBrandId { get; set; }
        public Guid ChannelBrandId { get; set; }
        public string ChannelType { get; set; } = "";
    }

    private sealed class SnapshotRow
    {
        public Guid ChannelId { get; set; }
        public string Connector { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTimeOffset? PublishAt { get; set; }
        public Guid? MatchedTopicId { get; set; }
    }

    private sealed class TopicSeed
    {
        public Guid TopicId { get; set; }
        public string? Title { get; set; }
    }

    private sealed class TopicValue
    {
        public Guid TopicId { get; set; }
        public string Value { get; set; } = "";
    }

    private sealed class CountRow
    {
        public int Total { get; set; }
        public int Matched { get; set; }
    }

    private sealed class CalRow
    {
        public DateTimeOffset At { get; set; }
        public Guid BrandId { get; set; }
        public string BrandCode { get; set; } = "";
        public string BrandName { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Channel { get; set; }
        public string Status { get; set; } = "";
        public Guid? TopicId { get; set; }
    }

    private sealed class PostRow
    {
        public Guid Id { get; set; }
        public Guid BrandId { get; set; }
        public Guid ChannelId { get; set; }
        public string ChannelName { get; set; } = "";
        public string Connector { get; set; } = "";
        public string ExternalId { get; set; } = "";
        public string Kind { get; set; } = "";
        public string? Title { get; set; }
        public string? Caption { get; set; }
        public string? Permalink { get; set; }
        public string Status { get; set; } = "";
        public DateTimeOffset? PublishAt { get; set; }
        public Guid? MatchedTopicId { get; set; }
        public string? MatchedTopicTitle { get; set; }
        public DateTimeOffset SyncedAt { get; set; }

        public ContentPlatformPostDto ToDto() => new(
            Id, BrandId, ChannelId, ChannelName, Connector, ExternalId, Kind, Title, Caption, Permalink,
            Status, PublishAt, MatchedTopicId, MatchedTopicTitle, SyncedAt,
            ContentPlatformRules.SourceLabel(Connector));
    }
}
