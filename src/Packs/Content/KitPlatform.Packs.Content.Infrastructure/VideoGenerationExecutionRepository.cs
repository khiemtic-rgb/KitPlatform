using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;
using Npgsql;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class VideoGenerationExecutionRepository
{
    private readonly IDbConnectionFactory _db;
    public VideoGenerationExecutionRepository(IDbConnectionFactory db) => _db = db;

    public sealed class Row
    {
        public Guid Id { get; set; }
        public Guid ShotId { get; set; }
        public Guid VideoContractId { get; set; }
        public Guid StillExecutionId { get; set; }
        public Guid ShotContractId { get; set; }
        public Guid PromptId { get; set; }
        public Guid IgcId { get; set; }
        public string SeriesId { get; set; } = "FAMIXA";
        public string CharacterId { get; set; } = "";
        public string EraId { get; set; } = "ERA-01";
        public string DocumentId { get; set; } = VideoGenerationExecutionRules.DocumentId;
        public string ExecutionStatus { get; set; } = "PREFLIGHT";
        public string Provider { get; set; } = "";
        public string ProviderStatus { get; set; } = "";
        public string? ProviderRequestId { get; set; }
        public string ProviderConfigVersion { get; set; } = VideoGenerationExecutionRules.ProviderConfigVersion;
        public string ExecutionFingerprint { get; set; } = "";
        public string IdempotencyKey { get; set; } = "";
        public string MasterSha256 { get; set; } = "";
        public string DnaSha256 { get; set; } = "";
        public string PrpSha256 { get; set; } = "";
        public string ShotContractSha256 { get; set; } = "";
        public string PromptSha256 { get; set; } = "";
        public string IgcSha256 { get; set; } = "";
        public string VideoContractSha256 { get; set; } = "";
        public string StillArtifactSha256 { get; set; } = "";
        public double DurationSeconds { get; set; }
        public string Resolution { get; set; } = "";
        public string Fps { get; set; } = "";
        public string AspectRatio { get; set; } = "";
        public string? ArtifactPath { get; set; }
        public string ArtifactSha256 { get; set; } = "";
        public string ArtifactMime { get; set; } = "";
        public string QaJson { get; set; } = "{}";
        public string CreditStatus { get; set; } = "UNKNOWN";
        public decimal? CreditValue { get; set; }
        public DateTimeOffset? RequestedAt { get; set; }
        public DateTimeOffset? AcceptedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTimeOffset? ApprovedAt { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTimeOffset? RejectedAt { get; set; }
        public string? RejectedBy { get; set; }
    }

    private const string SelectSql = """
        SELECT id AS Id, shot_id AS ShotId, video_contract_id AS VideoContractId, still_execution_id AS StillExecutionId,
               shot_contract_id AS ShotContractId, prompt_id AS PromptId, igc_id AS IgcId,
               series_id AS SeriesId, character_id AS CharacterId, era_id AS EraId, document_id AS DocumentId,
               execution_status AS ExecutionStatus, provider AS Provider, provider_status AS ProviderStatus,
               provider_request_id AS ProviderRequestId, provider_config_version AS ProviderConfigVersion,
               execution_fingerprint AS ExecutionFingerprint, idempotency_key AS IdempotencyKey,
               master_sha256 AS MasterSha256, dna_sha256 AS DnaSha256, prp_sha256 AS PrpSha256,
               shot_contract_sha256 AS ShotContractSha256, prompt_sha256 AS PromptSha256, igc_sha256 AS IgcSha256,
               video_contract_sha256 AS VideoContractSha256, still_artifact_sha256 AS StillArtifactSha256,
               duration_seconds AS DurationSeconds, resolution AS Resolution, fps AS Fps, aspect_ratio AS AspectRatio,
               artifact_path AS ArtifactPath, artifact_sha256 AS ArtifactSha256, artifact_mime AS ArtifactMime,
               qa_json::text AS QaJson, credit_status AS CreditStatus, credit_value AS CreditValue,
               requested_at AS RequestedAt, accepted_at AS AcceptedAt, completed_at AS CompletedAt,
               created_at AS CreatedAt, created_by AS CreatedBy, updated_at AS UpdatedAt, updated_by AS UpdatedBy,
               approved_at AS ApprovedAt, approved_by AS ApprovedBy, rejected_at AS RejectedAt, rejected_by AS RejectedBy
        FROM pack_content.video_generation_execution
        """;

    public async Task<Row?> GetLatestAsync(Guid shotId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Row>(
            SelectSql + " WHERE shot_id = @ShotId ORDER BY created_at DESC LIMIT 1;",
            new { ShotId = shotId });
    }

    public async Task<Row?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Row>(SelectSql + " WHERE id = @Id;", new { Id = id });
    }

    public async Task<Row?> GetByFingerprintAsync(string fingerprint, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Row>(
            SelectSql + " WHERE execution_fingerprint = @Fingerprint LIMIT 1;",
            new { Fingerprint = fingerprint });
    }

    public async Task<Row> InsertAsync(Row row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_generation_execution (
                id, shot_id, video_contract_id, still_execution_id, shot_contract_id, prompt_id, igc_id,
                series_id, character_id, era_id, document_id, execution_status, provider, provider_status,
                provider_request_id, provider_config_version, execution_fingerprint, idempotency_key,
                master_sha256, dna_sha256, prp_sha256, shot_contract_sha256, prompt_sha256, igc_sha256,
                video_contract_sha256, still_artifact_sha256, duration_seconds, resolution, fps, aspect_ratio,
                artifact_path, artifact_sha256, artifact_mime, qa_json, credit_status, credit_value,
                requested_at, created_by, updated_by
            ) VALUES (
                @Id, @ShotId, @VideoContractId, @StillExecutionId, @ShotContractId, @PromptId, @IgcId,
                @SeriesId, @CharacterId, @EraId, @DocumentId, @ExecutionStatus, @Provider, @ProviderStatus,
                @ProviderRequestId, @ProviderConfigVersion, @ExecutionFingerprint, @IdempotencyKey,
                @MasterSha256, @DnaSha256, @PrpSha256, @ShotContractSha256, @PromptSha256, @IgcSha256,
                @VideoContractSha256, @StillArtifactSha256, @DurationSeconds, @Resolution, @Fps, @AspectRatio,
                @ArtifactPath, @ArtifactSha256, @ArtifactMime, @QaJson::jsonb, @CreditStatus, @CreditValue,
                @RequestedAt, @CreatedBy, @UpdatedBy
            );
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        try { await conn.ExecuteAsync(sql, row); }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return await GetByFingerprintAsync(row.ExecutionFingerprint, ct)
                ?? throw new VideoGenerationExecutionException("VIDEO_GENERATION_EXECUTION_LOCKED", "Duplicate execution fingerprint. Không gọi provider lần 2.");
        }
        return await GetByIdAsync(row.Id, ct) ?? row;
    }

    public async Task<Row> UpdateAsync(Row row, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync("""
            UPDATE pack_content.video_generation_execution
            SET execution_status = @ExecutionStatus, provider = @Provider, provider_status = @ProviderStatus,
                provider_request_id = @ProviderRequestId, artifact_path = @ArtifactPath,
                artifact_sha256 = @ArtifactSha256, artifact_mime = @ArtifactMime, qa_json = @QaJson::jsonb,
                credit_status = @CreditStatus, credit_value = @CreditValue, accepted_at = @AcceptedAt,
                completed_at = @CompletedAt, approved_at = @ApprovedAt, approved_by = @ApprovedBy,
                rejected_at = @RejectedAt, rejected_by = @RejectedBy, updated_at = NOW(), updated_by = @UpdatedBy
            WHERE id = @Id;
            """, row);
        return await GetByIdAsync(row.Id, ct) ?? row;
    }

    public async Task InsertEventAsync(Guid? executionId, Guid shotId, string eventType, object payload, string actor, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync("""
            INSERT INTO pack_content.video_generation_execution_event
                (id, execution_id, shot_id, event_type, payload_json, actor)
            VALUES (@Id, @ExecutionId, @ShotId, @Type, @Payload::jsonb, @Actor);
            """, new
        {
            Id = Guid.NewGuid(),
            ExecutionId = executionId,
            ShotId = shotId,
            Type = eventType,
            Payload = System.Text.Json.JsonSerializer.Serialize(payload),
            Actor = actor,
        });
    }
}
