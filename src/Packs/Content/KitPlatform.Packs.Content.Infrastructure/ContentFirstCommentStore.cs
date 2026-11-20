using Dapper;
using KitPlatform.Infrastructure.Data;
using Npgsql;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentFirstCommentRow
{
    public Guid Id { get; set; }
    public Guid PublishJobId { get; set; }
    public Guid ChannelTargetId { get; set; }
    public string Connector { get; set; } = "";
    public string ExternalId { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTimeOffset LiveAt { get; set; }
    public string Status { get; set; } = "pending";
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public string? PinNote { get; set; }
}

internal sealed class ContentFirstCommentStore
{
    private readonly IDbConnectionFactory _db;

    public ContentFirstCommentStore(IDbConnectionFactory db) => _db = db;

    public async Task InsertAsync(ContentFirstCommentRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.first_comment (
                publish_job_id, channel_target_id, connector, external_id, body, live_at, pin_note
            ) VALUES (
                @PublishJobId, @ChannelTargetId, @Connector, @ExternalId, @Body, @LiveAt, @PinNote
            )
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, row);
    }

    public async Task<IReadOnlyList<ContentFirstCommentRow>> ListDueAsync(DateTimeOffset now, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, publish_job_id AS PublishJobId, channel_target_id AS ChannelTargetId,
                   connector AS Connector, external_id AS ExternalId, body AS Body, live_at AS LiveAt,
                   status AS Status, attempts AS Attempts, next_attempt_at AS NextAttemptAt,
                   last_error AS LastError, pin_note AS PinNote
            FROM pack_content.first_comment
            WHERE status = 'pending'
              AND attempts < 6
              AND live_at <= @Now - interval '2 minutes'
              AND (next_attempt_at IS NULL OR next_attempt_at <= @Now)
            ORDER BY live_at
            LIMIT 20
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<ContentFirstCommentRow>(sql, new { Now = now });
        return rows.ToList();
    }

    public async Task SaveAsync(ContentFirstCommentRow row, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.first_comment SET
                status = @Status,
                attempts = @Attempts,
                next_attempt_at = @NextAttemptAt,
                last_error = @LastError,
                updated_at = NOW()
            WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, row);
    }

    public async Task<string?> ReadJobResultAsync(Guid jobId, CancellationToken ct)
    {
        const string sql = "SELECT result_json::text FROM pack_content.publish_job WHERE id = @Id";
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<string?>(sql, new { Id = jobId });
    }

    public async Task WriteJobResultAsync(Guid jobId, string resultJson, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.publish_job
            SET result_json = @ResultJson::jsonb, updated_at = NOW()
            WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { Id = jobId, ResultJson = resultJson });
    }

    public static bool IsMissingTable(Exception ex)
    {
        for (Exception? cur = ex; cur is not null; cur = cur.InnerException)
        {
            if (cur is PostgresException pg && pg.SqlState == "42P01") return true;
        }
        return false;
    }
}
