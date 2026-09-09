using System.Text.Json;
using Dapper;
using KitPlatform.Infrastructure.Data;
using Npgsql;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoJobRepository
{
    private readonly IDbConnectionFactory _db;

    public KitVideoJobRepository(IDbConnectionFactory db) => _db = db;

    public sealed class JobRow
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid ProductionId { get; set; }
        public string SceneCode { get; set; } = "";
        public string ShotCode { get; set; } = "";
        public string Provider { get; set; } = "";
        public string Operation { get; set; } = "";
        public string Status { get; set; } = "";
        public string IdempotencyKey { get; set; } = "";
        public bool Confirmed { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
    }

    public sealed class AttemptRow
    {
        public Guid Id { get; set; }
        public Guid JobId { get; set; }
        public int AttemptNo { get; set; }
        public string Status { get; set; } = "";
        public string? Error { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public sealed class ProviderTaskRow
    {
        public Guid Id { get; set; }
        public Guid AttemptId { get; set; }
        public string Provider { get; set; } = "";
        public string ProviderTaskId { get; set; } = "";
        public string ProviderStatus { get; set; } = "";
        public string? FailureCode { get; set; }
        public string? OutputUrl { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public async Task<JobRow?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, project_id AS ProjectId, production_id AS ProductionId,
                   scene_code AS SceneCode, shot_code AS ShotCode, provider AS Provider,
                   operation AS Operation, status AS Status, idempotency_key AS IdempotencyKey,
                   confirmed AS Confirmed, created_at AS CreatedAt, started_at AS StartedAt,
                   completed_at AS CompletedAt
            FROM pack_content.video_generation_job
            WHERE id = @Id;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<JobRow>(sql, new { Id = id });
    }

    public async Task<JobRow?> GetByKeyAsync(string key, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, project_id AS ProjectId, production_id AS ProductionId,
                   scene_code AS SceneCode, shot_code AS ShotCode, provider AS Provider,
                   operation AS Operation, status AS Status, idempotency_key AS IdempotencyKey,
                   confirmed AS Confirmed, created_at AS CreatedAt, started_at AS StartedAt,
                   completed_at AS CompletedAt
            FROM pack_content.video_generation_job
            WHERE idempotency_key = @Key;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<JobRow>(sql, new { Key = key });
    }

    public async Task<IReadOnlyList<JobRow>> ListByProductionAsync(Guid productionId, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, project_id AS ProjectId, production_id AS ProductionId,
                   scene_code AS SceneCode, shot_code AS ShotCode, provider AS Provider,
                   operation AS Operation, status AS Status, idempotency_key AS IdempotencyKey,
                   confirmed AS Confirmed, created_at AS CreatedAt, started_at AS StartedAt,
                   completed_at AS CompletedAt
            FROM pack_content.video_generation_job
            WHERE production_id = @Id
            ORDER BY created_at DESC;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<JobRow>(sql, new { Id = productionId })).ToList();
    }

    public async Task<JobRow> InsertAsync(JobRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_generation_job (
                id, project_id, production_id, scene_code, shot_code, provider, operation,
                status, idempotency_key, confirmed, created_at, started_at, completed_at)
            VALUES (
                @Id, @ProjectId, @ProductionId, @SceneCode, @ShotCode, @Provider, @Operation,
                @Status, @IdempotencyKey, @Confirmed, @CreatedAt, @StartedAt, @CompletedAt);
            """;
        try
        {
            await using var conn = await _db.CreateOpenConnectionAsync(ct);
            await conn.ExecuteAsync(sql, row);
            return row;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return (await GetByKeyAsync(row.IdempotencyKey, ct))
                ?? throw new InvalidOperationException("IDEMPOTENCY: job đã tồn tại.");
        }
    }

    public async Task UpdateJobAsync(
        Guid id,
        string status,
        bool confirmed,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt,
        CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.video_generation_job
            SET status = @Status, confirmed = @Confirmed, started_at = @StartedAt, completed_at = @CompletedAt
            WHERE id = @Id;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            Id = id,
            Status = status,
            Confirmed = confirmed,
            StartedAt = startedAt,
            CompletedAt = completedAt,
        });
    }

    public async Task<IReadOnlyList<AttemptRow>> ListAttemptsAsync(Guid jobId, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, job_id AS JobId, attempt_no AS AttemptNo, status AS Status,
                   error AS Error, created_at AS CreatedAt
            FROM pack_content.video_generation_attempt
            WHERE job_id = @Id
            ORDER BY attempt_no;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<AttemptRow>(sql, new { Id = jobId })).ToList();
    }

    public async Task<AttemptRow> InsertAttemptAsync(AttemptRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_generation_attempt (id, job_id, attempt_no, status, error, created_at)
            VALUES (@Id, @JobId, @AttemptNo, @Status, @Error, @CreatedAt);
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, row);
        return row;
    }

    public async Task UpdateAttemptAsync(Guid id, string status, string? error, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.video_generation_attempt
            SET status = @Status, error = @Error
            WHERE id = @Id;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { Id = id, Status = status, Error = error });
    }

    public async Task<IReadOnlyList<ProviderTaskRow>> ListTasksAsync(IReadOnlyList<Guid> attemptIds, CancellationToken ct)
    {
        if (attemptIds.Count == 0) return [];
        const string sql = """
            SELECT id AS Id, attempt_id AS AttemptId, provider AS Provider,
                   provider_task_id AS ProviderTaskId, provider_status AS ProviderStatus,
                   failure_code AS FailureCode, output_url AS OutputUrl, created_at AS CreatedAt
            FROM pack_content.video_provider_task
            WHERE attempt_id = ANY(@Ids)
            ORDER BY created_at;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<ProviderTaskRow>(sql, new { Ids = attemptIds.ToArray() })).ToList();
    }

    public async Task InsertProviderTaskAsync(ProviderTaskRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_provider_task (
                id, attempt_id, provider, provider_task_id, provider_status,
                failure_code, output_url, created_at)
            VALUES (
                @Id, @AttemptId, @Provider, @ProviderTaskId, @ProviderStatus,
                @FailureCode, @OutputUrl, @CreatedAt);
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, row);
    }

    public static string ShotExtra(string? provider, string? failure) =>
        JsonSerializer.Serialize(new { lastProvider = provider ?? "", lastFailureCode = failure ?? "" });
}
