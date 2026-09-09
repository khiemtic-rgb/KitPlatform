using Dapper;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoVisionRepository
{
    private readonly IDbConnectionFactory _db;
    public KitVideoVisionRepository(IDbConnectionFactory db) => _db = db;

    public sealed class AttemptRow
    {
        public Guid Id { get; set; }
        public Guid ProductionId { get; set; }
        public string ShotCode { get; set; } = "";
        public int AttemptNo { get; set; }
        public string Status { get; set; } = "";
        public string Fingerprint { get; set; } = "";
        public string QaJson { get; set; } = "{}";
        public string RepairJson { get; set; } = "{}";
        public string? ImagePath { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string JobState { get; set; } = "QUEUED";
        public string Provider { get; set; } = "";
        public string Model { get; set; } = "";
        public string GenerationId { get; set; } = "";
        public string? IdempotencyKey { get; set; }
        public string VisionJson { get; set; } = "{}";
        public string ArtifactJson { get; set; } = "{}";
        public string FailureClass { get; set; } = "";
        public string MetadataJson { get; set; } = "{}";
        public bool CostUnknown { get; set; } = true;
        public string ArtifactSha256 { get; set; } = "";
        public string QaArtifactSha256 { get; set; } = "";
        public string ApprovedArtifactSha256 { get; set; } = "";
        public string ImageType { get; set; } = "";
        public string PersistStatus { get; set; } = "OK";
    }

    public async Task UpsertContractAsync(Guid productionId, string shot, string contract, string prompt, string fingerprint, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_visual_contract
                (id, production_id, shot_code, contract_json, prompt_json, fingerprint, updated_at)
            VALUES (@Id, @ProductionId, @Shot, @Contract::jsonb, @Prompt::jsonb, @Fingerprint, NOW())
            ON CONFLICT (production_id, shot_code) DO UPDATE
            SET contract_json = EXCLUDED.contract_json, prompt_json = EXCLUDED.prompt_json,
                fingerprint = EXCLUDED.fingerprint, updated_at = NOW();
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            Id = Guid.NewGuid(),
            ProductionId = productionId,
            Shot = shot,
            Contract = contract,
            Prompt = prompt,
            Fingerprint = fingerprint,
        });
    }

    public async Task<IReadOnlyList<AttemptRow>> ListAttemptsAsync(Guid productionId, string shot, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, production_id AS ProductionId, shot_code AS ShotCode, attempt_no AS AttemptNo,
                   status AS Status, fingerprint AS Fingerprint, qa_json::text AS QaJson,
                   repair_json::text AS RepairJson, image_path AS ImagePath, created_at AS CreatedAt,
                   job_state AS JobState, provider AS Provider, model AS Model, generation_id AS GenerationId,
                   idempotency_key AS IdempotencyKey, vision_json::text AS VisionJson,
                   artifact_json::text AS ArtifactJson, failure_class AS FailureClass,
                   metadata_json::text AS MetadataJson, cost_unknown AS CostUnknown,
                   artifact_sha256 AS ArtifactSha256, qa_artifact_sha256 AS QaArtifactSha256,
                   approved_artifact_sha256 AS ApprovedArtifactSha256, image_type AS ImageType,
                   persist_status AS PersistStatus
            FROM pack_content.video_keyframe_attempt
            WHERE production_id = @ProductionId AND shot_code = @Shot
            ORDER BY attempt_no;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<AttemptRow>(sql, new { ProductionId = productionId, Shot = shot });
        return rows.ToList();
    }

    public async Task<AttemptRow> InsertAttemptAsync(AttemptRow row, string promptJson, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_keyframe_attempt
                (id, production_id, shot_code, attempt_no, status, fingerprint, prompt_json, qa_json, repair_json, image_path, created_at,
                 job_state, provider, model, generation_id, idempotency_key, vision_json, artifact_json, failure_class, metadata_json, cost_unknown,
                 artifact_sha256, qa_artifact_sha256, approved_artifact_sha256, image_type, persist_status)
            VALUES (@Id, @ProductionId, @ShotCode, @AttemptNo, @Status, @Fingerprint, @Prompt::jsonb, @Qa::jsonb, @Repair::jsonb, @ImagePath, NOW(),
                    @JobState, @Provider, @Model, @GenerationId, @IdempotencyKey, @Vision::jsonb, @Artifact::jsonb, @FailureClass, @Metadata::jsonb, @CostUnknown,
                    @ArtifactSha256, @QaArtifactSha256, @ApprovedArtifactSha256, @ImageType, @PersistStatus);
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            row.Id,
            row.ProductionId,
            row.ShotCode,
            row.AttemptNo,
            row.Status,
            row.Fingerprint,
            Prompt = promptJson,
            Qa = row.QaJson,
            Repair = row.RepairJson,
            row.ImagePath,
            row.JobState,
            row.Provider,
            row.Model,
            row.GenerationId,
            row.IdempotencyKey,
            Vision = row.VisionJson,
            Artifact = row.ArtifactJson,
            row.FailureClass,
            Metadata = row.MetadataJson,
            row.CostUnknown,
            row.ArtifactSha256,
            row.QaArtifactSha256,
            row.ApprovedArtifactSha256,
            row.ImageType,
            row.PersistStatus,
        });
        return row;
    }

    public async Task<AttemptRow?> GetByIdempotencyAsync(string key, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, production_id AS ProductionId, shot_code AS ShotCode, attempt_no AS AttemptNo,
                   status AS Status, fingerprint AS Fingerprint, qa_json::text AS QaJson,
                   repair_json::text AS RepairJson, image_path AS ImagePath, created_at AS CreatedAt,
                   job_state AS JobState, provider AS Provider, model AS Model, generation_id AS GenerationId,
                   idempotency_key AS IdempotencyKey, vision_json::text AS VisionJson,
                   artifact_json::text AS ArtifactJson, failure_class AS FailureClass,
                   metadata_json::text AS MetadataJson, cost_unknown AS CostUnknown,
                   artifact_sha256 AS ArtifactSha256, qa_artifact_sha256 AS QaArtifactSha256,
                   approved_artifact_sha256 AS ApprovedArtifactSha256, image_type AS ImageType,
                   persist_status AS PersistStatus
            FROM pack_content.video_keyframe_attempt
            WHERE idempotency_key = @Key
            LIMIT 1;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<AttemptRow>(sql, new { Key = key });
    }

    public async Task UpdateIntegrityAsync(AttemptRow row, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.video_keyframe_attempt
            SET status = @Status, job_state = @JobState, qa_json = @Qa::jsonb, repair_json = @Repair::jsonb,
                vision_json = @Vision::jsonb, artifact_json = @Artifact::jsonb, failure_class = @FailureClass,
                metadata_json = @Metadata::jsonb, artifact_sha256 = @ArtifactSha256,
                qa_artifact_sha256 = @QaArtifactSha256, approved_artifact_sha256 = @ApprovedArtifactSha256,
                image_type = @ImageType, persist_status = @PersistStatus
            WHERE id = @Id;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            row.Id,
            row.Status,
            row.JobState,
            Qa = row.QaJson,
            Repair = row.RepairJson,
            Vision = row.VisionJson,
            Artifact = row.ArtifactJson,
            row.FailureClass,
            Metadata = row.MetadataJson,
            row.ArtifactSha256,
            row.QaArtifactSha256,
            row.ApprovedArtifactSha256,
            row.ImageType,
            row.PersistStatus,
        });
    }

    public async Task SetAttemptStatusAsync(Guid id, string status, string qaJson, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.video_keyframe_attempt
            SET status = @Status, job_state = @Status, qa_json = @Qa::jsonb
            WHERE id = @Id;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { Id = id, Status = status, Qa = qaJson });
    }

    public async Task<AttemptRow?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, production_id AS ProductionId, shot_code AS ShotCode, attempt_no AS AttemptNo,
                   status AS Status, fingerprint AS Fingerprint, qa_json::text AS QaJson,
                   repair_json::text AS RepairJson, image_path AS ImagePath, created_at AS CreatedAt,
                   job_state AS JobState, provider AS Provider, model AS Model, generation_id AS GenerationId,
                   idempotency_key AS IdempotencyKey, vision_json::text AS VisionJson,
                   artifact_json::text AS ArtifactJson, failure_class AS FailureClass,
                   metadata_json::text AS MetadataJson, cost_unknown AS CostUnknown,
                   artifact_sha256 AS ArtifactSha256, qa_artifact_sha256 AS QaArtifactSha256,
                   approved_artifact_sha256 AS ApprovedArtifactSha256, image_type AS ImageType,
                   persist_status AS PersistStatus
            FROM pack_content.video_keyframe_attempt
            WHERE id = @Id
            LIMIT 1;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<AttemptRow>(sql, new { Id = id });
    }
}
