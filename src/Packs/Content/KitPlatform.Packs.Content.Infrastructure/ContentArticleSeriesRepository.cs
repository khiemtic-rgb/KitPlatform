using System.Text.Json;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentArticleSeriesRepository
{
    private readonly IDbConnectionFactory _db;

    public ContentArticleSeriesRepository(IDbConnectionFactory db) => _db = db;

    public sealed class SeriesRow
    {
        public Guid Id { get; set; }
        public Guid BrandId { get; set; }
        public string BrandCode { get; set; } = "";
        public string BrandName { get; set; } = "";
        public Guid SourcePackageId { get; set; }
        public string SourcePackageTitle { get; set; } = "";
        public Guid CorePackageId { get; set; }
        public string CoreIdeaTitle { get; set; } = "";
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? Objective { get; set; }
        public string? Audience { get; set; }
        public string? CoreMessage { get; set; }
        public string Status { get; set; } = "DRAFT";
        public int EpisodeCount { get; set; }
        public int CurrentEpisodeNo { get; set; }
        public int PlannedEpisodeRows { get; set; }
        public int PublishedCount { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string BlueprintJson { get; set; } = "{}";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public sealed class EpisodeRow
    {
        public Guid Id { get; set; }
        public Guid SeriesId { get; set; }
        public int EpisodeNo { get; set; }
        public string Code { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Objective { get; set; }
        public string? Angle { get; set; }
        public string? KeyMessage { get; set; }
        public string Status { get; set; } = "PLANNED";
        public DateTimeOffset? PlannedAt { get; set; }
        public DateTimeOffset? PublishedAt { get; set; }
        public Guid? PreviousEpisodeId { get; set; }
        public Guid? NextEpisodeId { get; set; }
        public string ContinuityJson { get; set; } = "{}";
        public Guid? ContentTopicId { get; set; }
        public string? TopicStatus { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    private const string SeriesSelect = """
        SELECT s.id AS Id, s.brand_id AS BrandId, b.code AS BrandCode, b.name AS BrandName,
               s.source_package_id AS SourcePackageId, p.title AS SourcePackageTitle,
               COALESCE(p.source_package_id, p.id) AS CorePackageId,
               COALESCE(src.title, p.title) AS CoreIdeaTitle,
               s.code AS Code, s.name AS Name, s.description AS Description,
               s.objective AS Objective, s.audience AS Audience, s.core_message AS CoreMessage,
               s.status AS Status, s.episode_count AS EpisodeCount, s.current_episode_no AS CurrentEpisodeNo,
               (SELECT COUNT(*)::int FROM pack_content.content_episode e
                 WHERE e.series_id = s.id AND e.status <> 'CANCELLED') AS PlannedEpisodeRows,
               (SELECT COUNT(*)::int FROM pack_content.content_episode e
                 WHERE e.series_id = s.id AND e.status IN ('PUBLISHED', 'ANALYZED')) AS PublishedCount,
               s.start_date AS StartDate, s.end_date AS EndDate,
               CAST(s.blueprint_json AS text) AS BlueprintJson,
               s.created_at AS CreatedAt, s.updated_at AS UpdatedAt
        FROM pack_content.content_series s
        INNER JOIN pack_content.brand b ON b.id = s.brand_id
        INNER JOIN pack_content.content_package p ON p.id = s.source_package_id
        LEFT JOIN pack_content.content_package src ON src.id = p.source_package_id
        """;

    private const string EpisodeSelect = """
        SELECT e.id AS Id, e.series_id AS SeriesId, e.episode_no AS EpisodeNo, e.code AS Code,
               e.title AS Title, e.objective AS Objective, e.angle AS Angle, e.key_message AS KeyMessage,
               e.status AS Status, e.planned_at AS PlannedAt, e.published_at AS PublishedAt,
               e.previous_episode_id AS PreviousEpisodeId, e.next_episode_id AS NextEpisodeId,
               CAST(e.continuity_json AS text) AS ContinuityJson,
               e.content_topic_id AS ContentTopicId, t.status AS TopicStatus,
               e.created_at AS CreatedAt, e.updated_at AS UpdatedAt
        FROM pack_content.content_episode e
        LEFT JOIN pack_content.topic t ON t.id = e.content_topic_id
        """;

    public async Task<IReadOnlyList<SeriesRow>> ListAsync(
        Guid? brandId,
        string? status,
        Guid? corePackageId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct)
    {
        var sql = SeriesSelect + "\n" + """
            WHERE (@BrandId IS NULL OR s.brand_id = @BrandId)
              AND (@Status IS NULL OR s.status = @Status)
              AND (@CorePackageId IS NULL OR COALESCE(p.source_package_id, p.id) = @CorePackageId)
              AND (@FromUtc IS NULL OR s.updated_at >= @FromUtc)
              AND (@ToUtc IS NULL OR s.updated_at <= @ToUtc)
            ORDER BY s.updated_at DESC
            LIMIT 300
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<SeriesRow>(sql, new
        {
            BrandId = brandId,
            Status = status,
            CorePackageId = corePackageId,
            FromUtc = from,
            ToUtc = to,
        })).ToList();
    }

    public async Task<SeriesRow?> GetAsync(Guid id, CancellationToken ct)
    {
        var sql = SeriesSelect + " WHERE s.id = @Id";
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<SeriesRow>(sql, new { Id = id });
    }

    public async Task<int> NextSeriesSequenceAsync(Guid brandId, string prefix, CancellationToken ct)
    {
        const string sql = """
            SELECT COALESCE(MAX(
                CASE WHEN s.code ~ ('^' || @Prefix || '-S[0-9]{3}$')
                     THEN substring(s.code from '[0-9]{3}$')::int
                     ELSE 0 END
            ), 0) + 1
            FROM pack_content.content_series s
            WHERE s.brand_id = @BrandId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(sql, new { BrandId = brandId, Prefix = prefix });
    }

    public async Task<Guid> InsertAsync(
        Guid brandId,
        Guid sourcePackageId,
        string code,
        string name,
        string? description,
        string? objective,
        string? audience,
        string? coreMessage,
        string status,
        int episodeCount,
        DateOnly? startDate,
        DateOnly? endDate,
        string blueprintJson,
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.content_series (
                brand_id, source_package_id, code, name, description, objective, audience, core_message,
                status, episode_count, current_episode_no, start_date, end_date, blueprint_json
            ) VALUES (
                @BrandId, @SourcePackageId, @Code, @Name, @Description, @Objective, @Audience, @CoreMessage,
                @Status, @EpisodeCount, 1, @StartDate, @EndDate, CAST(@BlueprintJson AS jsonb)
            ) RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            BrandId = brandId,
            SourcePackageId = sourcePackageId,
            Code = code,
            Name = name,
            Description = description,
            Objective = objective,
            Audience = audience,
            CoreMessage = coreMessage,
            Status = status,
            EpisodeCount = episodeCount,
            StartDate = startDate,
            EndDate = endDate,
            BlueprintJson = string.IsNullOrWhiteSpace(blueprintJson) ? "{}" : blueprintJson,
        });
    }

    public async Task UpdateAsync(
        Guid id,
        string name,
        string? description,
        string? objective,
        string? audience,
        string? coreMessage,
        string status,
        int episodeCount,
        int currentEpisodeNo,
        DateOnly? startDate,
        DateOnly? endDate,
        string blueprintJson,
        CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.content_series SET
                name = @Name,
                description = @Description,
                objective = @Objective,
                audience = @Audience,
                core_message = @CoreMessage,
                status = @Status,
                episode_count = @EpisodeCount,
                current_episode_no = @CurrentEpisodeNo,
                start_date = @StartDate,
                end_date = @EndDate,
                blueprint_json = CAST(@BlueprintJson AS jsonb),
                updated_at = NOW()
            WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            Id = id,
            Name = name,
            Description = description,
            Objective = objective,
            Audience = audience,
            CoreMessage = coreMessage,
            Status = status,
            EpisodeCount = episodeCount,
            CurrentEpisodeNo = currentEpisodeNo,
            StartDate = startDate,
            EndDate = endDate,
            BlueprintJson = string.IsNullOrWhiteSpace(blueprintJson) ? "{}" : blueprintJson,
        });
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        const string sql = "DELETE FROM pack_content.content_series WHERE id = @Id";
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteAsync(sql, new { Id = id }) > 0;
    }

    public async Task<IReadOnlyList<EpisodeRow>> ListEpisodesAsync(Guid seriesId, CancellationToken ct)
    {
        var sql = EpisodeSelect + "\n" + """
            WHERE e.series_id = @SeriesId
            ORDER BY e.episode_no
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<EpisodeRow>(sql, new { SeriesId = seriesId })).ToList();
    }

    public async Task<EpisodeRow?> GetEpisodeAsync(Guid seriesId, Guid episodeId, CancellationToken ct)
    {
        var sql = EpisodeSelect + " WHERE e.series_id = @SeriesId AND e.id = @Id";
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<EpisodeRow>(sql, new { SeriesId = seriesId, Id = episodeId });
    }

    public async Task<EpisodeRow?> FindEpisodeByNoAsync(Guid seriesId, int episodeNo, CancellationToken ct)
    {
        var sql = EpisodeSelect + " WHERE e.series_id = @SeriesId AND e.episode_no = @No";
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<EpisodeRow>(sql, new { SeriesId = seriesId, No = episodeNo });
    }

    public async Task<Guid> InsertOrReuseEpisodeAsync(
        Guid seriesId,
        int episodeNo,
        string code,
        string title,
        string? objective,
        string? angle,
        string? keyMessage,
        string status,
        string continuityJson,
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.content_episode (
                series_id, episode_no, code, title, objective, angle, key_message, status, continuity_json
            ) VALUES (
                @SeriesId, @EpisodeNo, @Code, @Title, @Objective, @Angle, @KeyMessage, @Status,
                CAST(@ContinuityJson AS jsonb)
            )
            ON CONFLICT (series_id, episode_no) DO UPDATE SET
                title = EXCLUDED.title,
                objective = COALESCE(EXCLUDED.objective, pack_content.content_episode.objective),
                angle = COALESCE(EXCLUDED.angle, pack_content.content_episode.angle),
                key_message = COALESCE(EXCLUDED.key_message, pack_content.content_episode.key_message),
                continuity_json = CASE
                    WHEN EXCLUDED.continuity_json = '{}'::jsonb THEN pack_content.content_episode.continuity_json
                    ELSE EXCLUDED.continuity_json END,
                updated_at = NOW()
            WHERE pack_content.content_episode.status NOT IN ('PUBLISHED', 'ANALYZED', 'CANCELLED')
              AND pack_content.content_episode.content_topic_id IS NULL
            RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var id = await conn.ExecuteScalarAsync<Guid?>(sql, new
        {
            SeriesId = seriesId,
            EpisodeNo = episodeNo,
            Code = code,
            Title = title,
            Objective = objective,
            Angle = angle,
            KeyMessage = keyMessage,
            Status = status,
            ContinuityJson = string.IsNullOrWhiteSpace(continuityJson) ? "{}" : continuityJson,
        });
        if (id is Guid created) return created;

        const string existing = "SELECT id FROM pack_content.content_episode WHERE series_id = @SeriesId AND episode_no = @EpisodeNo";
        return await conn.ExecuteScalarAsync<Guid>(existing, new { SeriesId = seriesId, EpisodeNo = episodeNo });
    }

    public async Task UpdateEpisodeAsync(
        Guid id,
        int episodeNo,
        string code,
        string title,
        string? objective,
        string? angle,
        string? keyMessage,
        string status,
        DateTimeOffset? plannedAt,
        DateTimeOffset? publishedAt,
        Guid? previousEpisodeId,
        Guid? nextEpisodeId,
        string continuityJson,
        Guid? contentTopicId,
        CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.content_episode SET
                episode_no = @EpisodeNo,
                code = @Code,
                title = @Title,
                objective = @Objective,
                angle = @Angle,
                key_message = @KeyMessage,
                status = @Status,
                planned_at = @PlannedAt,
                published_at = @PublishedAt,
                previous_episode_id = @PreviousEpisodeId,
                next_episode_id = @NextEpisodeId,
                continuity_json = CAST(@ContinuityJson AS jsonb),
                content_topic_id = @ContentTopicId,
                updated_at = NOW()
            WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            Id = id,
            EpisodeNo = episodeNo,
            Code = code,
            Title = title,
            Objective = objective,
            Angle = angle,
            KeyMessage = keyMessage,
            Status = status,
            PlannedAt = plannedAt,
            PublishedAt = publishedAt,
            PreviousEpisodeId = previousEpisodeId,
            NextEpisodeId = nextEpisodeId,
            ContinuityJson = string.IsNullOrWhiteSpace(continuityJson) ? "{}" : continuityJson,
            ContentTopicId = contentTopicId,
        });
    }

    public async Task<bool> DeleteEpisodeAsync(Guid seriesId, Guid episodeId, CancellationToken ct)
    {
        const string sql = """
            DELETE FROM pack_content.content_episode
            WHERE series_id = @SeriesId AND id = @Id
              AND status NOT IN ('PUBLISHED', 'ANALYZED')
              AND content_topic_id IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteAsync(sql, new { SeriesId = seriesId, Id = episodeId }) > 0;
    }

    public async Task DeletePlannedWithoutTopicAsync(Guid seriesId, CancellationToken ct)
    {
        const string sql = """
            DELETE FROM pack_content.content_episode
            WHERE series_id = @SeriesId
              AND content_topic_id IS NULL
              AND status IN ('PLANNED', 'BRIEFING', 'READY')
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { SeriesId = seriesId });
    }

    public async Task RelinkAsync(Guid seriesId, CancellationToken ct)
    {
        const string sql = """
            WITH ordered AS (
                SELECT id, episode_no,
                       lag(id) OVER (ORDER BY episode_no) AS prev_id,
                       lead(id) OVER (ORDER BY episode_no) AS next_id
                FROM pack_content.content_episode
                WHERE series_id = @SeriesId AND status <> 'CANCELLED'
            )
            UPDATE pack_content.content_episode e
            SET previous_episode_id = o.prev_id,
                next_episode_id = o.next_id,
                updated_at = NOW()
            FROM ordered o
            WHERE e.id = o.id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { SeriesId = seriesId });
    }

    public async Task SyncFromTopicAsync(Guid topicId, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.content_episode e
            SET status = CASE t.status
                    WHEN 'Generating' THEN 'GENERATING'
                    WHEN 'Review' THEN 'REVIEW'
                    WHEN 'Approved' THEN 'APPROVED'
                    WHEN 'Scheduled' THEN 'SCHEDULED'
                    WHEN 'Published' THEN 'PUBLISHED'
                    WHEN 'BudgetBlocked' THEN 'PAUSED'
                    WHEN 'Rejected' THEN 'REVIEW'
                    ELSE e.status
                END,
                published_at = CASE
                    WHEN t.status = 'Published' THEN COALESCE(e.published_at, NOW())
                    ELSE e.published_at END,
                updated_at = NOW()
            FROM pack_content.topic t
            WHERE e.content_topic_id = t.id
              AND t.id = @TopicId
              AND e.status NOT IN ('CANCELLED')
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { TopicId = topicId });
    }

    public static ContentArticleSeriesDto MapSeries(SeriesRow row) =>
        new(
            row.Id,
            row.BrandId,
            row.BrandCode,
            row.BrandName,
            row.SourcePackageId,
            row.SourcePackageTitle,
            row.CorePackageId,
            row.CoreIdeaTitle,
            row.Code,
            row.Name,
            row.Description,
            row.Objective,
            row.Audience,
            row.CoreMessage,
            row.Status,
            row.EpisodeCount,
            row.CurrentEpisodeNo,
            row.PlannedEpisodeRows,
            row.PublishedCount,
            row.StartDate is { } sd ? DateOnly.FromDateTime(DateTime.SpecifyKind(sd, DateTimeKind.Unspecified)) : null,
            row.EndDate is { } ed ? DateOnly.FromDateTime(DateTime.SpecifyKind(ed, DateTimeKind.Unspecified)) : null,
            ParseJson(row.BlueprintJson),
            row.CreatedAt,
            row.UpdatedAt);

    public static ContentArticleEpisodeDto MapEpisode(EpisodeRow row)
    {
        var status = row.ContentTopicId is not null && !string.IsNullOrWhiteSpace(row.TopicStatus)
            ? ContentArticleSeriesRules.MapTopicToEpisodeStatus(row.TopicStatus)
            : row.Status;
        return new ContentArticleEpisodeDto(
            row.Id,
            row.SeriesId,
            row.EpisodeNo,
            row.Code,
            row.Title,
            row.Objective,
            row.Angle,
            row.KeyMessage,
            status,
            row.PlannedAt,
            row.PublishedAt,
            row.PreviousEpisodeId,
            row.NextEpisodeId,
            ParseJson(row.ContinuityJson),
            row.ContentTopicId,
            row.TopicStatus,
            row.CreatedAt,
            row.UpdatedAt);
    }

    private static JsonElement ParseJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return JsonDocument.Parse("{}").RootElement.Clone();
        try
        {
            return JsonDocument.Parse(raw).RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonDocument.Parse("{}").RootElement.Clone();
        }
    }
}
