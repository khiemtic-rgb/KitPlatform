using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentWorkRepository
{
    private readonly IDbConnectionFactory _db;

    public ContentWorkRepository(IDbConnectionFactory db) => _db = db;

    public sealed class WorkJobRow
    {
        public Guid Id { get; set; }
        public string Kind { get; set; } = "";
        public string Status { get; set; } = ContentWorkStatuses.Queued;
        public Guid? BrandId { get; set; }
        public string? BrandCode { get; set; }
        public string? BrandName { get; set; }
        public Guid? TopicId { get; set; }
        public Guid? PackageId { get; set; }
        public Guid? VideoJobId { get; set; }
        public string? Title { get; set; }
        public string PayloadJson { get; set; } = "{}";
        public string ResultJson { get; set; } = "{}";
        public string? ErrorMessage { get; set; }
        public int RetryCount { get; set; }
        public int MaxRetries { get; set; } = 3;
        public DateTimeOffset AvailableAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
    }

    public sealed class StatusCountRow
    {
        public string Status { get; set; } = "";
        public int Cnt { get; set; }
    }

    public sealed class BrandOpsRow
    {
        public Guid BrandId { get; set; }
        public string BrandCode { get; set; } = "";
        public string BrandName { get; set; } = "";
        public int ReviewCount { get; set; }
        public int ScheduledCount { get; set; }
        public int PublishedMonthCount { get; set; }
        public decimal SpendUsd { get; set; }
    }

    public sealed class CalendarRow
    {
        public DateTimeOffset At { get; set; }
        public string Kind { get; set; } = "";
        public Guid? PackageId { get; set; }
        public Guid? TopicId { get; set; }
        public Guid? PublishJobId { get; set; }
        public Guid BrandId { get; set; }
        public string BrandCode { get; set; } = "";
        public string BrandName { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Channel { get; set; }
        public string Status { get; set; } = "";
    }

    public async Task<Guid> InsertAsync(WorkJobRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.work_job (
                kind, status, brand_id, topic_id, package_id, video_job_id, title,
                payload_json, retry_count, max_retries, available_at
            ) VALUES (
                @Kind, @Status, @BrandId, @TopicId, @PackageId, @VideoJobId, @Title,
                @PayloadJson::jsonb, @RetryCount, @MaxRetries, @AvailableAt
            ) RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<Guid>(sql, row);
    }

    public async Task<WorkJobRow?> GetAsync(Guid id, CancellationToken ct)
    {
        const string sql = WorkSelect + " AND w.id = @Id";
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<WorkJobRow>(sql, new { Id = id });
    }

    public async Task<WorkJobRow?> FindActiveAsync(
        string kind,
        Guid? topicId,
        Guid? packageId,
        Guid? videoJobId,
        CancellationToken ct)
    {
        const string sql = WorkSelect + """
             AND w.kind = @Kind
              AND w.status IN ('Queued', 'Running')
              AND w.topic_id IS NOT DISTINCT FROM @TopicId
              AND w.package_id IS NOT DISTINCT FROM @PackageId
              AND w.video_job_id IS NOT DISTINCT FROM @VideoJobId
            ORDER BY w.created_at DESC
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<WorkJobRow>(sql, new
        {
            Kind = kind,
            TopicId = topicId,
            PackageId = packageId,
            VideoJobId = videoJobId,
        });
    }

    public async Task<IReadOnlyList<WorkJobRow>> ListActiveAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT
                w.id AS Id,
                w.kind AS Kind,
                w.status AS Status,
                w.brand_id AS BrandId,
                b.code AS BrandCode,
                b.name AS BrandName,
                w.topic_id AS TopicId,
                w.package_id AS PackageId,
                w.video_job_id AS VideoJobId,
                w.title AS Title,
                CAST(w.payload_json AS text) AS PayloadJson,
                CAST(w.result_json AS text) AS ResultJson,
                w.error_message AS ErrorMessage,
                w.retry_count AS RetryCount,
                w.max_retries AS MaxRetries,
                w.available_at AS AvailableAt,
                w.created_at AS CreatedAt,
                w.started_at AS StartedAt,
                w.completed_at AS CompletedAt
            FROM pack_content.work_job AS w
            LEFT JOIN pack_content.brand AS b ON b.id = w.brand_id
            WHERE w.status IN ('Queued', 'Running')
            ORDER BY w.available_at, w.created_at
            LIMIT 50
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<WorkJobRow>(sql)).ToList();
    }

    public async Task<IReadOnlyList<WorkJobRow>> ListFailedAsync(int limit, CancellationToken ct)
    {
        const string sql = """
            SELECT
                w.id AS Id,
                w.kind AS Kind,
                w.status AS Status,
                w.brand_id AS BrandId,
                b.code AS BrandCode,
                b.name AS BrandName,
                w.topic_id AS TopicId,
                w.package_id AS PackageId,
                w.video_job_id AS VideoJobId,
                w.title AS Title,
                CAST(w.payload_json AS text) AS PayloadJson,
                CAST(w.result_json AS text) AS ResultJson,
                w.error_message AS ErrorMessage,
                w.retry_count AS RetryCount,
                w.max_retries AS MaxRetries,
                w.available_at AS AvailableAt,
                w.created_at AS CreatedAt,
                w.started_at AS StartedAt,
                w.completed_at AS CompletedAt
            FROM pack_content.work_job AS w
            LEFT JOIN pack_content.brand AS b ON b.id = w.brand_id
            WHERE w.status = 'Failed'
            ORDER BY COALESCE(w.completed_at, w.updated_at) DESC
            LIMIT @Limit
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<WorkJobRow>(sql, new { Limit = limit })).ToList();
    }

    public async Task<int> RequeueOrphanedRunningAsync(TimeSpan olderThan, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.work_job
            SET status = 'Queued',
                error_message = COALESCE(error_message, 'Worker restart — nhận lại job'),
                updated_at = NOW()
            WHERE status = 'Running'
              AND COALESCE(started_at, updated_at) < NOW() - (@Seconds * INTERVAL '1 second')
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteAsync(sql, new { Seconds = Math.Max(30, (int)olderThan.TotalSeconds) });
    }

    public async Task<WorkJobRow?> ClaimNextAsync(CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.work_job AS w
            SET status = 'Running',
                started_at = COALESCE(w.started_at, NOW()),
                updated_at = NOW()
            FROM (
                SELECT id
                FROM pack_content.work_job
                WHERE status = 'Queued'
                  AND available_at <= NOW()
                ORDER BY available_at, created_at
                LIMIT 1
                FOR UPDATE SKIP LOCKED
            ) pick
            WHERE w.id = pick.id
            RETURNING w.id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var id = await conn.ExecuteScalarAsync<Guid?>(sql);
        if (id is null) return null;
        return await GetAsync(id.Value, ct);
    }

    public async Task MarkSucceededAsync(Guid id, string resultJson, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.work_job SET
                status = 'Succeeded',
                result_json = @ResultJson::jsonb,
                error_message = NULL,
                completed_at = NOW(),
                updated_at = NOW()
            WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { Id = id, ResultJson = resultJson });
    }

    public async Task MarkRetryOrFailAsync(
        Guid id,
        string error,
        int retryCount,
        int maxRetries,
        DateTimeOffset? nextAvailableAt,
        CancellationToken ct)
    {
        var failed = retryCount >= maxRetries;
        const string sql = """
            UPDATE pack_content.work_job SET
                status = @Status,
                error_message = @Error,
                retry_count = @RetryCount,
                available_at = COALESCE(@NextAvailableAt, available_at),
                completed_at = CASE WHEN @Failed THEN NOW() ELSE NULL END,
                updated_at = NOW()
            WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            Id = id,
            Status = failed ? ContentWorkStatuses.Failed : ContentWorkStatuses.Queued,
            Error = error.Length > 2000 ? error[..2000] : error,
            RetryCount = retryCount,
            NextAvailableAt = nextAvailableAt,
            Failed = failed,
        });
    }

    public async Task<IReadOnlyList<StatusCountRow>> CountTopicStatusAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT status AS Status, COUNT(*)::int AS Cnt
            FROM pack_content.topic
            GROUP BY status
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<StatusCountRow>(sql)).ToList();
    }

    public async Task<int> CountPublishedSinceAsync(DateTimeOffset fromUtc, CancellationToken ct)
    {
        const string sql = """
            SELECT COUNT(*)::int
            FROM pack_content.topic
            WHERE status = 'Published'
              AND updated_at >= @FromUtc
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(sql, new { FromUtc = fromUtc });
    }

    public async Task<int> CountWorkErrorsAsync(DateTimeOffset fromUtc, CancellationToken ct)
    {
        const string sql = """
            SELECT (
                (SELECT COUNT(*) FROM pack_content.work_job
                 WHERE status = 'Failed' AND COALESCE(completed_at, updated_at) >= @FromUtc)
              + (SELECT COUNT(*) FROM pack_content.publish_job
                 WHERE status = 'Failed' AND updated_at >= @FromUtc)
              + (SELECT COUNT(*) FROM pack_content.video_job
                 WHERE status = 'Failed' AND updated_at >= @FromUtc)
            )::int
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(sql, new { FromUtc = fromUtc });
    }

    public async Task<int> CountActiveWorkAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT COUNT(*)::int
            FROM pack_content.work_job
            WHERE status IN ('Queued', 'Running')
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(sql);
    }

    public async Task<IReadOnlyList<BrandOpsRow>> ListBrandOpsAsync(
        DateTimeOffset monthStartUtc,
        CancellationToken ct)
    {
        const string sql = """
            SELECT
                b.id AS BrandId,
                b.code AS BrandCode,
                b.name AS BrandName,
                (SELECT COUNT(*)::int FROM pack_content.topic t
                 WHERE t.brand_id = b.id AND t.status = 'Review') AS ReviewCount,
                (SELECT COUNT(*)::int FROM pack_content.topic t
                 WHERE t.brand_id = b.id AND t.status = 'Scheduled') AS ScheduledCount,
                (SELECT COUNT(*)::int FROM pack_content.topic t
                 WHERE t.brand_id = b.id AND t.status = 'Published' AND t.updated_at >= @MonthStart) AS PublishedMonthCount,
                (SELECT COALESCE(SUM(u.estimate_usd), 0)
                 FROM pack_content.usage_ledger u
                 WHERE u.brand_id = b.id AND u.created_at >= @MonthStart) AS SpendUsd
            FROM pack_content.brand b
            WHERE b.is_active
            ORDER BY b.sort_order, b.name
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<BrandOpsRow>(sql, new { MonthStart = monthStartUtc })).ToList();
    }

    public async Task<IReadOnlyList<CalendarRow>> ListCalendarAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        Guid? brandId,
        CancellationToken ct)
    {
        const string sql = """
            SELECT * FROM (
                SELECT
                    COALESCE(t.display_at, t.updated_at) AS At,
                    'topic' AS Kind,
                    p.id AS PackageId,
                    t.id AS TopicId,
                    CAST(NULL AS uuid) AS PublishJobId,
                    t.brand_id AS BrandId,
                    b.code AS BrandCode,
                    b.name AS BrandName,
                    t.title AS Title,
                    CAST(NULL AS text) AS Channel,
                    t.status AS Status
                FROM pack_content.topic t
                INNER JOIN pack_content.brand b ON b.id = t.brand_id
                LEFT JOIN pack_content.content_package p ON p.topic_id = t.id
                WHERE COALESCE(t.display_at, t.updated_at) >= @FromUtc
                  AND COALESCE(t.display_at, t.updated_at) < @ToUtc
                  AND (@BrandId IS NULL OR t.brand_id = @BrandId)
                  AND t.status NOT IN ('Draft', 'Generating', 'Rejected', 'BudgetBlocked')
                  AND (p.id IS NULL OR p.source_package_id IS NOT NULL)

                UNION ALL

                SELECT
                    COALESCE(pj.publish_at, pj.created_at) AS At,
                    'publish' AS Kind,
                    p.id AS PackageId,
                    pj.topic_id AS TopicId,
                    pj.id AS PublishJobId,
                    pj.brand_id AS BrandId,
                    b.code AS BrandCode,
                    b.name AS BrandName,
                    t.title AS Title,
                    pj.connector_type AS Channel,
                    pj.status AS Status
                FROM pack_content.publish_job pj
                INNER JOIN pack_content.brand b ON b.id = pj.brand_id
                INNER JOIN pack_content.topic t ON t.id = pj.topic_id
                LEFT JOIN pack_content.content_package p ON p.topic_id = t.id
                WHERE COALESCE(pj.publish_at, pj.created_at) >= @FromUtc
                  AND COALESCE(pj.publish_at, pj.created_at) < @ToUtc
                  AND (@BrandId IS NULL OR pj.brand_id = @BrandId)
            ) x
            ORDER BY At, BrandName, Title
            LIMIT 800
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<CalendarRow>(sql, new
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            BrandId = brandId,
        })).ToList();
    }

    public async Task<IReadOnlyList<Guid>> ListDuePublishJobIdsAsync(int limit, CancellationToken ct)
    {
        const string sql = """
            SELECT id
            FROM pack_content.publish_job
            WHERE status = 'Queued'
              AND (publish_at IS NULL OR publish_at <= NOW())
            ORDER BY created_at
            LIMIT @Limit
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<Guid>(sql, new { Limit = limit })).ToList();
    }

    private const string WorkSelect = """
        SELECT
            w.id AS Id,
            w.kind AS Kind,
            w.status AS Status,
            w.brand_id AS BrandId,
            b.code AS BrandCode,
            b.name AS BrandName,
            w.topic_id AS TopicId,
            w.package_id AS PackageId,
            w.video_job_id AS VideoJobId,
            w.title AS Title,
            CAST(w.payload_json AS text) AS PayloadJson,
            CAST(w.result_json AS text) AS ResultJson,
            w.error_message AS ErrorMessage,
            w.retry_count AS RetryCount,
            w.max_retries AS MaxRetries,
            w.available_at AS AvailableAt,
            w.created_at AS CreatedAt,
            w.started_at AS StartedAt,
            w.completed_at AS CompletedAt
        FROM pack_content.work_job AS w
        LEFT JOIN pack_content.brand AS b ON b.id = w.brand_id
        WHERE 1 = 1
        """;
}
