using System.Linq;
using System.Text.Json;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;
using Npgsql;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoProductionShotRepository
{
    private readonly IDbConnectionFactory _db;
    public KitVideoProductionShotRepository(IDbConnectionFactory db) => _db = db;

    public sealed class ShotRow
    {
        public Guid Id { get; set; }
        public string ShotCode { get; set; } = "";
        public string DocumentId { get; set; } = KitVideoProductionShotRules.DocumentId;
        public string CharacterId { get; set; } = KitVideoProductionShotRules.CharacterId;
        public string CharacterName { get; set; } = KitVideoProductionShotRules.CharacterName;
        public string EraId { get; set; } = KitVideoProductionShotRules.EraId;
        public int ShotSeq { get; set; } = 1;
        public string ShotVersion { get; set; } = KitVideoProductionShotRules.Version;
        public string ShotStatus { get; set; } = "DRAFT";
        public Guid MasterReferenceId { get; set; }
        public string MasterCode { get; set; } = "";
        public string MasterSha256 { get; set; } = "";
        public Guid CharacterDnaId { get; set; }
        public string DnaCode { get; set; } = "";
        public string DnaSha256 { get; set; } = "";
        public Guid ProductionPackId { get; set; }
        public string PrpCode { get; set; } = "";
        public string PrpSha256 { get; set; } = "";
        public string SpecJson { get; set; } = "{}";
        public string Note { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTimeOffset? ApprovedAt { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTimeOffset? LockedAt { get; set; }
        public string? LockedBy { get; set; }
        public string ExtraJson { get; set; } = "{}";
        public string? ShotSha256 { get; set; }
    }

    private const string SelectSql = """
        SELECT id AS Id, shot_code AS ShotCode, document_id AS DocumentId,
               character_id AS CharacterId, character_name AS CharacterName, era_id AS EraId,
               shot_seq AS ShotSeq, shot_version AS ShotVersion, shot_status AS ShotStatus,
               master_reference_id AS MasterReferenceId, master_code AS MasterCode,
               master_sha256 AS MasterSha256, character_dna_id AS CharacterDnaId,
               dna_code AS DnaCode, dna_sha256 AS DnaSha256, production_pack_id AS ProductionPackId,
               prp_code AS PrpCode, prp_sha256 AS PrpSha256, spec_json::text AS SpecJson, note AS Note,
               created_at AS CreatedAt, created_by AS CreatedBy, approved_at AS ApprovedAt,
               approved_by AS ApprovedBy, locked_at AS LockedAt, locked_by AS LockedBy,
               extra_json::text AS ExtraJson
        FROM pack_content.video_production_shot
        """;

    public async Task<IReadOnlyDictionary<string, int>> CountByCharacterAsync(CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<CharacterCountRow>(
            "SELECT character_id AS CharacterId, COUNT(*)::int AS Count FROM pack_content.video_production_shot GROUP BY character_id;");
        return rows.ToDictionary(r => r.CharacterId, r => r.Count, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class CharacterCountRow
    {
        public string CharacterId { get; set; } = "";
        public int Count { get; set; }
    }

    public async Task<IReadOnlyList<ShotRow>> ListAsync(string characterId, string eraId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = (await conn.QueryAsync<ShotRow>(
            SelectSql + " WHERE character_id = @Character AND era_id = @Era ORDER BY shot_seq, shot_version;",
            new { Character = characterId, Era = eraId })).ToList();
        return rows.Select(r => AttachSha(r)!).ToList();
    }

    public async Task<ShotRow?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return AttachSha(await conn.QuerySingleOrDefaultAsync<ShotRow>(SelectSql + " WHERE id = @Id;", new { Id = id }));
    }

    public async Task<int> NextSeqAsync(string characterId, string eraId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var max = await conn.ExecuteScalarAsync<int?>(
            "SELECT MAX(shot_seq) FROM pack_content.video_production_shot WHERE character_id = @Character AND era_id = @Era;",
            new { Character = characterId, Era = eraId });
        return (max ?? 0) + 1;
    }

    public async Task<ShotRow> InsertDraftAsync(ShotRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_production_shot (
                id, shot_code, document_id, project_code, character_id, character_name, era_id,
                shot_seq, shot_version, shot_status, master_reference_id, master_code, master_sha256,
                character_dna_id, dna_code, dna_sha256, production_pack_id, prp_code, prp_sha256,
                spec_json, note, created_by
            ) VALUES (
                @Id, @ShotCode, @DocumentId, 'FAMIXA', @CharacterId, @CharacterName, @EraId,
                @ShotSeq, @ShotVersion, 'DRAFT', @MasterReferenceId, @MasterCode, @MasterSha256,
                @CharacterDnaId, @DnaCode, @DnaSha256, @ProductionPackId, @PrpCode, @PrpSha256,
                @SpecJson::jsonb, @Note, @CreatedBy
            );
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        try
        {
            await conn.ExecuteAsync(sql, row);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new InvalidOperationException("SHOT_GATE_NOT_SATISFIED: duplicate shot code.");
        }
        return row;
    }

    public async Task<ShotRow> UpdateSpecAsync(Guid id, string specJson, string status, string note, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var n = await conn.ExecuteAsync("""
            UPDATE pack_content.video_production_shot
            SET spec_json = @Spec::jsonb,
                shot_status = @Status,
                note = COALESCE(NULLIF(@Note, ''), note)
            WHERE id = @Id AND shot_status <> 'LOCKED';
            """, new { Id = id, Spec = specJson, Status = status, Note = note });
        if (n == 0)
            throw new InvalidOperationException("SHOT_LOCKED: V1 không overwrite. Dùng SHOT-V2.");
        return await GetByIdAsync(id, ct) ?? throw new InvalidOperationException("Production Shot không tồn tại.");
    }

    public async Task<ShotRow> SetStatusAsync(Guid id, string status, string note, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var n = await conn.ExecuteAsync("""
            UPDATE pack_content.video_production_shot
            SET shot_status = @Status, note = COALESCE(NULLIF(@Note, ''), note)
            WHERE id = @Id AND shot_status <> 'LOCKED';
            """, new { Id = id, Status = status, Note = note });
        if (n == 0)
            throw new InvalidOperationException("SHOT_LOCKED: V1 không overwrite. Dùng SHOT-V2.");
        return await GetByIdAsync(id, ct) ?? throw new InvalidOperationException("Production Shot không tồn tại.");
    }

    public async Task<ShotRow> ApproveAndLockAsync(Guid id, string actor, string note, string shotSha256, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            var current = await conn.QuerySingleOrDefaultAsync<ShotRow>(
                SelectSql + " WHERE id = @Id FOR UPDATE;",
                new { Id = id }, tx);
            if (current is null)
                throw new InvalidOperationException("Production Shot không tồn tại.");
            if (current.ShotStatus == "LOCKED")
            {
                await tx.CommitAsync(ct);
                return AttachSha(current)!;
            }
            var extra = JsonSerializer.Serialize(new
            {
                shot_sha256 = shotSha256,
                master_id = current.MasterReferenceId,
                master_sha256 = current.MasterSha256,
                dna_id = current.CharacterDnaId,
                dna_sha256 = current.DnaSha256,
                prp_id = current.ProductionPackId,
                prp_sha256 = current.PrpSha256,
                approved_by = actor,
                approved_at = DateTimeOffset.UtcNow,
            });
            await conn.ExecuteAsync("""
                UPDATE pack_content.video_production_shot
                SET shot_status = 'LOCKED',
                    note = COALESCE(NULLIF(@Note, ''), note),
                    approved_at = NOW(),
                    approved_by = @Actor,
                    locked_at = NOW(),
                    locked_by = @Actor,
                    extra_json = COALESCE(extra_json, '{}'::jsonb) || @Extra::jsonb
                WHERE id = @Id AND shot_status IN ('DRAFT', 'IDENTITY_CHECK', 'DIRECTOR_REVIEW', 'APPROVED', 'REJECTED');
                """, new { Id = id, Actor = actor, Note = note, Extra = extra }, tx);
            var next = await conn.QuerySingleAsync<ShotRow>(SelectSql + " WHERE id = @Id;", new { Id = id }, tx);
            await tx.CommitAsync(ct);
            return AttachSha(next)!;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task InsertEventAsync(Guid shotId, string eventType, object payload, string? actor, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync("""
            INSERT INTO pack_content.video_production_shot_event (id, shot_id, event_type, payload_json, actor)
            VALUES (@Id, @ShotId, @EventType, @Payload::jsonb, @Actor);
            """, new
        {
            Id = Guid.NewGuid(),
            ShotId = shotId,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload),
            Actor = actor,
        });
    }

    private static ShotRow? AttachSha(ShotRow? row)
    {
        if (row is null) return null;
        row.ShotSha256 = ReadShotSha(row.ExtraJson);
        return row;
    }

    private static string? ReadShotSha(string? extraJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(extraJson) ? "{}" : extraJson);
            return doc.RootElement.TryGetProperty("shot_sha256", out var n) && n.ValueKind == JsonValueKind.String
                ? n.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
