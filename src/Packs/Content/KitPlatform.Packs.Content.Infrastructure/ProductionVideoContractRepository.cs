using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;
using Npgsql;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ProductionVideoContractRepository
{
    private readonly IDbConnectionFactory _db;
    public ProductionVideoContractRepository(IDbConnectionFactory db) => _db = db;

    public sealed class Row
    {
        public Guid Id { get; set; }
        public Guid ShotId { get; set; }
        public Guid ShotContractId { get; set; }
        public Guid StillExecutionId { get; set; }
        public string SeriesId { get; set; } = "FAMIXA";
        public string CharacterId { get; set; } = "";
        public string EraId { get; set; } = "ERA-01";
        public string ContractVersion { get; set; } = "V1";
        public string ContractStatus { get; set; } = "DRAFT";
        public string DocumentId { get; set; } = ProductionVideoContractRules.DocumentId;
        public string PayloadJson { get; set; } = "{}";
        public string CanonicalJson { get; set; } = "";
        public string ContractSha256 { get; set; } = "";
        public Guid? MasterId { get; set; }
        public string MasterSha256 { get; set; } = "";
        public Guid? DnaId { get; set; }
        public string DnaSha256 { get; set; } = "";
        public Guid? PrpId { get; set; }
        public string PrpSha256 { get; set; } = "";
        public string ShotContractSha256 { get; set; } = "";
        public string StillArtifactSha256 { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTimeOffset? ValidatedAt { get; set; }
        public string? ValidatedBy { get; set; }
        public DateTimeOffset? ApprovedAt { get; set; }
        public string? ApprovedBy { get; set; }
        public string? RejectionReason { get; set; }
        public Guid? SupersededBy { get; set; }
    }

    private const string SelectSql = """
        SELECT id AS Id, shot_id AS ShotId, shot_contract_id AS ShotContractId, still_execution_id AS StillExecutionId,
               series_id AS SeriesId, character_id AS CharacterId, era_id AS EraId,
               contract_version AS ContractVersion, contract_status AS ContractStatus, document_id AS DocumentId,
               payload_json::text AS PayloadJson, canonical_json AS CanonicalJson, contract_sha256 AS ContractSha256,
               master_id AS MasterId, master_sha256 AS MasterSha256, dna_id AS DnaId, dna_sha256 AS DnaSha256,
               prp_id AS PrpId, prp_sha256 AS PrpSha256, shot_contract_sha256 AS ShotContractSha256,
               still_artifact_sha256 AS StillArtifactSha256, created_at AS CreatedAt, created_by AS CreatedBy,
               updated_at AS UpdatedAt, updated_by AS UpdatedBy, validated_at AS ValidatedAt, validated_by AS ValidatedBy,
               approved_at AS ApprovedAt, approved_by AS ApprovedBy, rejection_reason AS RejectionReason,
               superseded_by AS SupersededBy
        FROM pack_content.video_production_video_contract
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

    public async Task<Row> InsertAsync(Row row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_production_video_contract (
                id, shot_id, shot_contract_id, still_execution_id, series_id, character_id, era_id,
                contract_version, contract_status, document_id, payload_json, canonical_json, contract_sha256,
                master_id, master_sha256, dna_id, dna_sha256, prp_id, prp_sha256,
                shot_contract_sha256, still_artifact_sha256, created_by, updated_by, validated_at, validated_by
            ) VALUES (
                @Id, @ShotId, @ShotContractId, @StillExecutionId, @SeriesId, @CharacterId, @EraId,
                @ContractVersion, @ContractStatus, @DocumentId, @PayloadJson::jsonb, @CanonicalJson, @ContractSha256,
                @MasterId, @MasterSha256, @DnaId, @DnaSha256, @PrpId, @PrpSha256,
                @ShotContractSha256, @StillArtifactSha256, @CreatedBy, @UpdatedBy, @ValidatedAt, @ValidatedBy
            );
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        try { await conn.ExecuteAsync(sql, row); }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new ProductionVideoContractException("VIDEO_CONTRACT_NOT_READY", "Duplicate contract version.");
        }
        return await GetByIdAsync(row.Id, ct) ?? row;
    }

    public async Task<Row> UpdateDraftAsync(Row row, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var n = await conn.ExecuteAsync("""
            UPDATE pack_content.video_production_video_contract
            SET payload_json = @PayloadJson::jsonb, canonical_json = @CanonicalJson, contract_sha256 = @ContractSha256,
                contract_status = @ContractStatus, shot_contract_id = @ShotContractId, still_execution_id = @StillExecutionId,
                master_id = @MasterId, master_sha256 = @MasterSha256, dna_id = @DnaId, dna_sha256 = @DnaSha256,
                prp_id = @PrpId, prp_sha256 = @PrpSha256, shot_contract_sha256 = @ShotContractSha256,
                still_artifact_sha256 = @StillArtifactSha256, updated_at = NOW(), updated_by = @UpdatedBy,
                validated_at = @ValidatedAt, validated_by = @ValidatedBy, rejected_at = NULL, rejected_by = NULL,
                rejection_reason = NULL
            WHERE id = @Id AND contract_status IN ('DRAFT', 'VALIDATED', 'REJECTED');
            """, row);
        if (n == 0)
            throw new ProductionVideoContractException("VIDEO_CONTRACT_LOCKED", "VIDEO_CONTRACT_LOCKED: V1 không overwrite. Dùng V2.");
        return await GetByIdAsync(row.Id, ct) ?? row;
    }

    public async Task MarkSupersededAsync(Guid id, Guid nextId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync("""
            UPDATE pack_content.video_production_video_contract
            SET contract_status = 'SUPERSEDED', superseded_by = @NextId, updated_at = NOW()
            WHERE id = @Id AND contract_status = 'DIRECTOR_APPROVED';
            """, new { Id = id, NextId = nextId });
    }

    public async Task ApproveAsync(Guid id, string actor, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var n = await conn.ExecuteAsync("""
            UPDATE pack_content.video_production_video_contract
            SET contract_status = 'DIRECTOR_APPROVED', approved_at = NOW(), approved_by = @Actor,
                updated_at = NOW(), updated_by = @Actor
            WHERE id = @Id AND contract_status = 'VALIDATED';
            """, new { Id = id, Actor = actor });
        if (n == 0)
            throw new ProductionVideoContractException("VIDEO_CONTRACT_NOT_READY", "Chỉ VALIDATED được Director APPROVE.");
    }

    public async Task RejectAsync(Guid id, string actor, string reason, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var n = await conn.ExecuteAsync("""
            UPDATE pack_content.video_production_video_contract
            SET contract_status = 'REJECTED', rejected_at = NOW(), rejected_by = @Actor,
                rejection_reason = @Reason, updated_at = NOW(), updated_by = @Actor
            WHERE id = @Id AND contract_status <> 'DIRECTOR_APPROVED';
            """, new { Id = id, Actor = actor, Reason = reason });
        if (n == 0)
            throw new ProductionVideoContractException("VIDEO_CONTRACT_LOCKED", "VIDEO_CONTRACT_LOCKED: V1 không overwrite. Dùng V2.");
    }

    public async Task InsertEventAsync(Guid? contractId, Guid shotId, string eventType, object payload, string actor, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync("""
            INSERT INTO pack_content.video_production_video_contract_event
                (id, contract_id, shot_id, event_type, payload_json, actor)
            VALUES (@Id, @ContractId, @ShotId, @Type, @Payload::jsonb, @Actor);
            """, new
        {
            Id = Guid.NewGuid(),
            ContractId = contractId,
            ShotId = shotId,
            Type = eventType,
            Payload = System.Text.Json.JsonSerializer.Serialize(payload),
            Actor = actor,
        });
    }
}
