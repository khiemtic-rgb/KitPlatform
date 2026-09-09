using Dapper;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoMotionRepository
{
    private readonly IDbConnectionFactory _db;
    public KitVideoMotionRepository(IDbConnectionFactory db) => _db = db;

    public sealed class TakeRow
    {
        public Guid Id { get; set; }
        public Guid ProductionId { get; set; }
        public string ShotCode { get; set; } = "";
        public Guid KeyframeAttemptId { get; set; }
        public int AttemptNo { get; set; }
        public string Status { get; set; } = "READY";
        public string Model { get; set; } = KitVideoMotionRules.Model;
        public int DurationSec { get; set; } = 5;
        public string Ratio { get; set; } = KitVideoMotionRules.Ratio;
        public string MotionPrompt { get; set; } = "";
        public string Fingerprint { get; set; } = "";
        public string SourceArtifactSha256 { get; set; } = "";
        public string VideoSha256 { get; set; } = "";
        public string RunwayTaskId { get; set; } = "";
        public string? OutputUrl { get; set; }
        public string? VideoPath { get; set; }
        public string FailureClass { get; set; } = "";
        public string FailureCode { get; set; } = "";
        public string RetryReason { get; set; } = "";
        public string CreditState { get; set; } = "NONE";
        public string PersistStatus { get; set; } = "OK";
        public string? IdempotencyKey { get; set; }
        public string QaJson { get; set; } = "{}";
        public string MotionJson { get; set; } = "{}";
        public string ProviderJson { get; set; } = "{}";
        public string MetadataJson { get; set; } = "{}";
        public bool Confirmed { get; set; }
    }

    private const string Select = """
        SELECT id AS Id, production_id AS ProductionId, shot_code AS ShotCode,
               keyframe_attempt_id AS KeyframeAttemptId, attempt_no AS AttemptNo,
               status AS Status, model AS Model, duration_sec AS DurationSec, ratio AS Ratio,
               motion_prompt AS MotionPrompt, fingerprint AS Fingerprint,
               source_artifact_sha256 AS SourceArtifactSha256, video_sha256 AS VideoSha256,
               runway_task_id AS RunwayTaskId, output_url AS OutputUrl, video_path AS VideoPath,
               failure_class AS FailureClass, failure_code AS FailureCode, retry_reason AS RetryReason,
               credit_state AS CreditState, persist_status AS PersistStatus,
               idempotency_key AS IdempotencyKey, qa_json::text AS QaJson,
               motion_json::text AS MotionJson, provider_json::text AS ProviderJson,
               metadata_json::text AS MetadataJson, confirmed AS Confirmed
        FROM pack_content.video_runway_take
        """;

    public async Task<TakeRow?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<TakeRow>(Select + " WHERE id = @Id LIMIT 1;", new { Id = id });
    }

    public async Task<TakeRow?> GetByIdempotencyAsync(string key, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<TakeRow>(Select + " WHERE idempotency_key = @Key LIMIT 1;", new { Key = key });
    }

    public async Task<IReadOnlyList<TakeRow>> ListByKeyframeAsync(Guid keyframeAttemptId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<TakeRow>(
            Select + " WHERE keyframe_attempt_id = @Id ORDER BY attempt_no;",
            new { Id = keyframeAttemptId });
        return rows.ToList();
    }

    public async Task InsertAsync(TakeRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_runway_take
                (id, production_id, shot_code, keyframe_attempt_id, attempt_no, status, model, duration_sec, ratio,
                 motion_prompt, fingerprint, source_artifact_sha256, video_sha256, runway_task_id, output_url, video_path,
                 failure_class, failure_code, retry_reason, credit_state, persist_status, idempotency_key,
                 qa_json, motion_json, provider_json, metadata_json, confirmed)
            VALUES (@Id, @ProductionId, @ShotCode, @KeyframeAttemptId, @AttemptNo, @Status, @Model, @DurationSec, @Ratio,
                    @MotionPrompt, @Fingerprint, @SourceArtifactSha256, @VideoSha256, @RunwayTaskId, @OutputUrl, @VideoPath,
                    @FailureClass, @FailureCode, @RetryReason, @CreditState, @PersistStatus, @IdempotencyKey,
                    @Qa::jsonb, @Motion::jsonb, @Provider::jsonb, @Metadata::jsonb, @Confirmed);
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, Params(row));
    }

    public async Task UpdateAsync(TakeRow row, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.video_runway_take
            SET status = @Status, runway_task_id = @RunwayTaskId, output_url = @OutputUrl, video_path = @VideoPath,
                video_sha256 = @VideoSha256, failure_class = @FailureClass, failure_code = @FailureCode,
                credit_state = @CreditState, persist_status = @PersistStatus, qa_json = @Qa::jsonb,
                provider_json = @Provider::jsonb, metadata_json = @Metadata::jsonb, confirmed = @Confirmed,
                updated_at = NOW()
            WHERE id = @Id;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, Params(row));
    }

    public async Task InsertProviderTaskAsync(
        Guid takeId,
        string taskId,
        string fingerprint,
        string sourceHash,
        string status,
        string failureCode,
        string failureMessage,
        string? outputUrl,
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_runway_provider_task
                (id, take_id, provider, provider_task_id, request_fingerprint, source_artifact_hash,
                 motion_contract_ver, submitted_at, completed_at, provider_status, failure_code, failure_message, output_url)
            VALUES (@Id, @TakeId, 'RUNWAY', @TaskId, @Fingerprint, @SourceHash,
                    'KIT-VIDEO-MOTION-V1', NOW(),
                    CASE WHEN @Status IN ('SUCCEEDED','FAILED','CANCELLED') THEN NOW() ELSE NULL END,
                    @Status, @FailureCode, @FailureMessage, @OutputUrl);
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            Id = Guid.NewGuid(),
            TakeId = takeId,
            TaskId = taskId,
            Fingerprint = fingerprint,
            SourceHash = sourceHash,
            Status = status,
            FailureCode = failureCode,
            FailureMessage = failureMessage,
            OutputUrl = outputUrl,
        });
    }

    private static object Params(TakeRow row) => new
    {
        row.Id,
        row.ProductionId,
        row.ShotCode,
        row.KeyframeAttemptId,
        row.AttemptNo,
        row.Status,
        row.Model,
        row.DurationSec,
        row.Ratio,
        row.MotionPrompt,
        row.Fingerprint,
        row.SourceArtifactSha256,
        row.VideoSha256,
        row.RunwayTaskId,
        row.OutputUrl,
        row.VideoPath,
        row.FailureClass,
        row.FailureCode,
        row.RetryReason,
        row.CreditState,
        row.PersistStatus,
        row.IdempotencyKey,
        Qa = row.QaJson,
        Motion = row.MotionJson,
        Provider = row.ProviderJson,
        Metadata = row.MetadataJson,
        row.Confirmed,
    };
}
