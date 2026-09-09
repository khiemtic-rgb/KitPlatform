using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;
using Npgsql;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ProductionPromptCompilerRepository
{
    private readonly IDbConnectionFactory _db;
    public ProductionPromptCompilerRepository(IDbConnectionFactory db) => _db = db;

    public sealed class PromptRow
    {
        public Guid Id { get; set; }
        public Guid ShotId { get; set; }
        public Guid ContractId { get; set; }
        public string SeriesId { get; set; } = "FAMIXA";
        public string CharacterId { get; set; } = "";
        public string EraId { get; set; } = "ERA-01";
        public string ContractVersion { get; set; } = "V1";
        public string PromptVersion { get; set; } = "V1";
        public string PromptStatus { get; set; } = "NOT_READY";
        public string DocumentId { get; set; } = ProductionPromptCompilerRules.DocumentId;
        public string PromptText { get; set; } = "";
        public string NegativeJson { get; set; } = "[]";
        public string PromptSha256 { get; set; } = "";
        public string ContractSha256 { get; set; } = "";
        public Guid? MasterId { get; set; }
        public string MasterSha256 { get; set; } = "";
        public Guid? DnaId { get; set; }
        public string DnaSha256 { get; set; } = "";
        public Guid? PrpId { get; set; }
        public string PrpSha256 { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public Guid? SupersededBy { get; set; }
    }

    private const string SelectSql = """
        SELECT id AS Id, shot_id AS ShotId, contract_id AS ContractId, series_id AS SeriesId, character_id AS CharacterId,
               era_id AS EraId, contract_version AS ContractVersion, prompt_version AS PromptVersion,
               prompt_status AS PromptStatus, document_id AS DocumentId, prompt_text AS PromptText,
               negative_json::text AS NegativeJson, prompt_sha256 AS PromptSha256, contract_sha256 AS ContractSha256,
               master_id AS MasterId, master_sha256 AS MasterSha256, dna_id AS DnaId, dna_sha256 AS DnaSha256,
               prp_id AS PrpId, prp_sha256 AS PrpSha256, created_at AS CreatedAt, created_by AS CreatedBy,
               superseded_by AS SupersededBy
        FROM pack_content.video_production_prompt
        """;

    public async Task<PromptRow?> GetLatestAsync(Guid shotId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<PromptRow>(
            SelectSql + " WHERE shot_id = @ShotId ORDER BY created_at DESC LIMIT 1;",
            new { ShotId = shotId });
    }

    public async Task<PromptRow?> GetCompiledByContractShaAsync(Guid shotId, string contractSha, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<PromptRow>(
            SelectSql + " WHERE shot_id = @ShotId AND contract_sha256 = @Sha AND prompt_status = 'COMPILED' LIMIT 1;",
            new { ShotId = shotId, Sha = contractSha });
    }

    public async Task<PromptRow> InsertAsync(PromptRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_production_prompt (
                id, shot_id, contract_id, series_id, character_id, era_id, contract_version, prompt_version,
                prompt_status, document_id, prompt_text, negative_json, prompt_sha256, contract_sha256,
                master_id, master_sha256, dna_id, dna_sha256, prp_id, prp_sha256, created_by
            ) VALUES (
                @Id, @ShotId, @ContractId, @SeriesId, @CharacterId, @EraId, @ContractVersion, @PromptVersion,
                @PromptStatus, @DocumentId, @PromptText, @NegativeJson::jsonb, @PromptSha256, @ContractSha256,
                @MasterId, @MasterSha256, @DnaId, @DnaSha256, @PrpId, @PrpSha256, @CreatedBy
            );
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        try
        {
            await conn.ExecuteAsync(sql, row);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new ProductionPromptCompilerException("PROMPT_COMPILER_BLOCKED", "Duplicate prompt version.");
        }
        return await GetByIdAsync(row.Id, ct) ?? row;
    }

    public async Task<PromptRow?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<PromptRow>(SelectSql + " WHERE id = @Id;", new { Id = id });
    }

    public async Task MarkSupersededAsync(Guid id, Guid nextId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync("""
            UPDATE pack_content.video_production_prompt
            SET prompt_status = 'SUPERSEDED', superseded_by = @NextId
            WHERE id = @Id AND prompt_status = 'COMPILED';
            """, new { Id = id, NextId = nextId });
    }

    public async Task InsertEventAsync(
        Guid? promptId, Guid shotId, Guid? contractId, string eventType, object payload, string actor, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync("""
            INSERT INTO pack_content.video_production_prompt_event
                (id, prompt_id, shot_id, contract_id, event_type, payload_json, actor)
            VALUES (@Id, @PromptId, @ShotId, @ContractId, @Type, @Payload::jsonb, @Actor);
            """, new
        {
            Id = Guid.NewGuid(),
            PromptId = promptId,
            ShotId = shotId,
            ContractId = contractId,
            Type = eventType,
            Payload = System.Text.Json.JsonSerializer.Serialize(payload),
            Actor = actor,
        });
    }
}
