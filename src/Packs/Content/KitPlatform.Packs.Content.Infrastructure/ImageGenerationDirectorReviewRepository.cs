using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ImageGenerationDirectorReviewRepository
{
    private readonly IDbConnectionFactory _db;
    public ImageGenerationDirectorReviewRepository(IDbConnectionFactory db) => _db = db;

    public sealed class Row
    {
        public Guid Id { get; set; }
        public Guid ShotId { get; set; }
        public Guid ExecutionId { get; set; }
        public string ReviewVersion { get; set; } = "V1";
        public string ReviewStatus { get; set; } = "PENDING";
        public string DocumentId { get; set; } = ImageGenerationDirectorReviewRules.DocumentId;
        public string SeriesId { get; set; } = "FAMIXA";
        public string CharacterId { get; set; } = "";
        public string EraId { get; set; } = "ERA-01";
        public string MasterSha256 { get; set; } = "";
        public string DnaSha256 { get; set; } = "";
        public string PrpSha256 { get; set; } = "";
        public string ShotContractSha256 { get; set; } = "";
        public string PromptSha256 { get; set; } = "";
        public string ImageGenerationContractSha256 { get; set; } = "";
        public string ArtifactSha256 { get; set; } = "";
        public string DirectorApproval { get; set; } = "PENDING";
        public string? DirectorId { get; set; }
        public DateTimeOffset? DirectorAt { get; set; }
        public string? RejectionReason { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }

    private const string SelectSql = """
        SELECT id AS Id, shot_id AS ShotId, execution_id AS ExecutionId, review_version AS ReviewVersion,
               review_status AS ReviewStatus, document_id AS DocumentId, series_id AS SeriesId,
               character_id AS CharacterId, era_id AS EraId, master_sha256 AS MasterSha256,
               dna_sha256 AS DnaSha256, prp_sha256 AS PrpSha256, shot_contract_sha256 AS ShotContractSha256,
               prompt_sha256 AS PromptSha256, image_generation_contract_sha256 AS ImageGenerationContractSha256,
               artifact_sha256 AS ArtifactSha256, director_approval AS DirectorApproval, director_id AS DirectorId,
               director_at AS DirectorAt, rejection_reason AS RejectionReason, created_at AS CreatedAt,
               created_by AS CreatedBy, updated_at AS UpdatedAt, updated_by AS UpdatedBy
        FROM pack_content.video_image_director_review
        """;

    public async Task<Row?> GetLatestAsync(Guid shotId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Row>(
            SelectSql + " WHERE shot_id = @ShotId ORDER BY created_at DESC LIMIT 1;",
            new { ShotId = shotId });
    }

    public async Task<Row?> GetByExecutionAsync(Guid executionId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Row>(
            SelectSql + " WHERE execution_id = @ExecutionId ORDER BY created_at DESC LIMIT 1;",
            new { ExecutionId = executionId });
    }

    public async Task<Row> InsertAsync(Row row, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync("""
            INSERT INTO pack_content.video_image_director_review (
                id, shot_id, execution_id, review_version, review_status, document_id, series_id,
                character_id, era_id, master_sha256, dna_sha256, prp_sha256, shot_contract_sha256,
                prompt_sha256, image_generation_contract_sha256, artifact_sha256, director_approval,
                created_by, updated_by
            ) VALUES (
                @Id, @ShotId, @ExecutionId, @ReviewVersion, @ReviewStatus, @DocumentId, @SeriesId,
                @CharacterId, @EraId, @MasterSha256, @DnaSha256, @PrpSha256, @ShotContractSha256,
                @PromptSha256, @ImageGenerationContractSha256, @ArtifactSha256, @DirectorApproval,
                @CreatedBy, @UpdatedBy
            );
            """, row);
        return await GetByExecutionAsync(row.ExecutionId, ct) ?? row;
    }

    public async Task<Row> DecideAsync(Guid id, string status, string approval, string actor, string? reason, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var n = await conn.ExecuteAsync("""
            UPDATE pack_content.video_image_director_review
            SET review_status = @Status, director_approval = @Approval, director_id = @Actor,
                director_at = NOW(), rejection_reason = @Reason, updated_at = NOW(), updated_by = @Actor
            WHERE id = @Id AND review_status = 'PENDING';
            """, new { Id = id, Status = status, Approval = approval, Actor = actor, Reason = reason });
        if (n == 0)
            throw new ImageGenerationDirectorReviewException("IMAGE_DIRECTOR_REVIEW_LOCKED", "Review không còn PENDING.");
        await using var conn2 = await _db.CreateOpenConnectionAsync(ct);
        return await conn2.QuerySingleAsync<Row>(SelectSql + " WHERE id = @Id;", new { Id = id });
    }

    public async Task InsertEventAsync(Guid? reviewId, Guid executionId, Guid shotId, string eventType, object payload, string actor, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync("""
            INSERT INTO pack_content.video_image_director_review_event
                (id, review_id, execution_id, shot_id, event_type, payload_json, actor)
            VALUES (@Id, @ReviewId, @ExecutionId, @ShotId, @Type, @Payload::jsonb, @Actor);
            """, new
        {
            Id = Guid.NewGuid(),
            ReviewId = reviewId,
            ExecutionId = executionId,
            ShotId = shotId,
            Type = eventType,
            Payload = System.Text.Json.JsonSerializer.Serialize(payload),
            Actor = actor,
        });
    }
}
