using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;
using Npgsql;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ProductionShotContractRepository
{
    private readonly IDbConnectionFactory _db;
    public ProductionShotContractRepository(IDbConnectionFactory db) => _db = db;

    public sealed class ContractRow
    {
        public Guid Id { get; set; }
        public Guid ShotId { get; set; }
        public string SeriesId { get; set; } = "FAMIXA";
        public string CharacterId { get; set; } = "";
        public string EraId { get; set; } = "ERA-01";
        public string ContractVersion { get; set; } = "V1";
        public string ContractStatus { get; set; } = "DRAFT";
        public string DocumentId { get; set; } = ProductionShotContractRules.DocumentId;
        public string PayloadJson { get; set; } = "{}";
        public string CanonicalJson { get; set; } = "";
        public string ContractSha256 { get; set; } = "";
        public Guid? MasterId { get; set; }
        public string MasterSha256 { get; set; } = "";
        public Guid? DnaId { get; set; }
        public string DnaSha256 { get; set; } = "";
        public Guid? PrpId { get; set; }
        public string PrpSha256 { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTimeOffset? ValidatedAt { get; set; }
        public string? ValidatedBy { get; set; }
        public DateTimeOffset? ApprovedAt { get; set; }
        public string? ApprovedBy { get; set; }
        public Guid? SupersededBy { get; set; }
    }

    private const string SelectSql = """
        SELECT id AS Id, shot_id AS ShotId, series_id AS SeriesId, character_id AS CharacterId, era_id AS EraId,
               contract_version AS ContractVersion, contract_status AS ContractStatus, document_id AS DocumentId,
               payload_json::text AS PayloadJson, canonical_json AS CanonicalJson, contract_sha256 AS ContractSha256,
               master_id AS MasterId, master_sha256 AS MasterSha256, dna_id AS DnaId, dna_sha256 AS DnaSha256,
               prp_id AS PrpId, prp_sha256 AS PrpSha256, created_at AS CreatedAt, created_by AS CreatedBy,
               updated_at AS UpdatedAt, updated_by AS UpdatedBy, validated_at AS ValidatedAt, validated_by AS ValidatedBy,
               approved_at AS ApprovedAt, approved_by AS ApprovedBy, superseded_by AS SupersededBy
        FROM pack_content.video_production_shot_contract
        """;

    public async Task<ContractRow?> GetLatestAsync(Guid shotId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ContractRow>(
            SelectSql + " WHERE shot_id = @ShotId ORDER BY created_at DESC LIMIT 1;",
            new { ShotId = shotId });
    }

    public async Task<ContractRow> InsertAsync(ContractRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_production_shot_contract (
                id, shot_id, series_id, character_id, era_id, contract_version, contract_status, document_id,
                payload_json, canonical_json, contract_sha256, master_id, master_sha256, dna_id, dna_sha256,
                prp_id, prp_sha256, created_by, updated_by, validated_at, validated_by
            ) VALUES (
                @Id, @ShotId, @SeriesId, @CharacterId, @EraId, @ContractVersion, @ContractStatus, @DocumentId,
                @PayloadJson::jsonb, @CanonicalJson, @ContractSha256, @MasterId, @MasterSha256, @DnaId, @DnaSha256,
                @PrpId, @PrpSha256, @CreatedBy, @UpdatedBy, @ValidatedAt, @ValidatedBy
            );
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        try
        {
            await conn.ExecuteAsync(sql, row);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new ProductionShotContractException("SHOT_CONTRACT_INVALID", "Duplicate contract version.");
        }
        return row;
    }

    public async Task<ContractRow> UpdateDraftAsync(ContractRow row, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var n = await conn.ExecuteAsync("""
            UPDATE pack_content.video_production_shot_contract
            SET payload_json = @PayloadJson::jsonb,
                canonical_json = @CanonicalJson,
                contract_sha256 = @ContractSha256,
                contract_status = @ContractStatus,
                series_id = @SeriesId,
                character_id = @CharacterId,
                era_id = @EraId,
                master_id = @MasterId,
                master_sha256 = @MasterSha256,
                dna_id = @DnaId,
                dna_sha256 = @DnaSha256,
                prp_id = @PrpId,
                prp_sha256 = @PrpSha256,
                updated_at = NOW(),
                updated_by = @UpdatedBy,
                validated_at = @ValidatedAt,
                validated_by = @ValidatedBy,
                rejected_at = NULL,
                rejected_by = NULL
            WHERE id = @Id AND contract_status IN ('DRAFT', 'VALIDATED', 'REJECTED');
            """, row);
        if (n == 0)
            throw new ProductionShotContractException("SHOT_CONTRACT_LOCKED", "SHOT_CONTRACT_LOCKED: V1 không overwrite. Dùng V2.");
        return await GetByIdAsync(row.Id, ct) ?? row;
    }

    public async Task<ContractRow?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ContractRow>(SelectSql + " WHERE id = @Id;", new { Id = id });
    }

    public async Task MarkSupersededAsync(Guid id, Guid nextId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync("""
            UPDATE pack_content.video_production_shot_contract
            SET contract_status = 'SUPERSEDED', superseded_by = @NextId, updated_at = NOW()
            WHERE id = @Id AND contract_status = 'DIRECTOR_APPROVED';
            """, new { Id = id, NextId = nextId });
    }

    public async Task ApproveAsync(Guid id, string actor, string sha, string canonical, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var n = await conn.ExecuteAsync("""
            UPDATE pack_content.video_production_shot_contract
            SET contract_status = 'DIRECTOR_APPROVED',
                contract_sha256 = @Sha,
                canonical_json = @Canonical,
                approved_at = NOW(),
                approved_by = @Actor,
                updated_at = NOW(),
                updated_by = @Actor
            WHERE id = @Id AND contract_status IN ('VALIDATED', 'DRAFT');
            """, new { Id = id, Actor = actor, Sha = sha, Canonical = canonical });
        if (n == 0)
            throw new ProductionShotContractException("SHOT_CONTRACT_LOCKED", "SHOT_CONTRACT_LOCKED: không approve được.");
    }

    public async Task RejectAsync(Guid id, string actor, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var n = await conn.ExecuteAsync("""
            UPDATE pack_content.video_production_shot_contract
            SET contract_status = 'REJECTED', rejected_at = NOW(), rejected_by = @Actor,
                updated_at = NOW(), updated_by = @Actor
            WHERE id = @Id AND contract_status <> 'DIRECTOR_APPROVED';
            """, new { Id = id, Actor = actor });
        if (n == 0)
            throw new ProductionShotContractException("SHOT_CONTRACT_LOCKED", "SHOT_CONTRACT_LOCKED: V1 không overwrite. Dùng V2.");
    }

    public async Task InsertEventAsync(Guid contractId, Guid shotId, string eventType, object payload, string actor, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync("""
            INSERT INTO pack_content.video_production_shot_contract_event (id, contract_id, shot_id, event_type, payload_json, actor)
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
