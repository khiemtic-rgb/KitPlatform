using System.Linq;
using System.Text.Json;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;
using Npgsql;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoCharacterDnaRepository
{
    private readonly IDbConnectionFactory _db;
    public KitVideoCharacterDnaRepository(IDbConnectionFactory db) => _db = db;

    public sealed class DnaRow
    {
        public Guid Id { get; set; }
        public string DnaCode { get; set; } = KitVideoCharacterDnaRules.DnaCode;
        public string DocumentId { get; set; } = KitVideoCharacterDnaRules.DocumentId;
        public string CharacterId { get; set; } = KitVideoCharacterDnaRules.CharacterId;
        public string CharacterName { get; set; } = KitVideoCharacterDnaRules.CharacterName;
        public string EraId { get; set; } = KitVideoCharacterDnaRules.EraId;
        public Guid MasterReferenceId { get; set; }
        public string MasterCode { get; set; } = KitVideoCharacterDnaRules.MasterCode;
        public string MasterSha256 { get; set; } = "";
        public string DnaVersion { get; set; } = KitVideoCharacterDnaRules.Version;
        public string Status { get; set; } = "DRAFT";
        public string SpecJson { get; set; } = "{}";
        public string Note { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ApprovedAt { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTimeOffset? LockedAt { get; set; }
        public string? LockedBy { get; set; }
    }

    private const string SelectSql = """
        SELECT id AS Id, dna_code AS DnaCode, document_id AS DocumentId,
               character_id AS CharacterId, character_name AS CharacterName, era_id AS EraId,
               master_reference_id AS MasterReferenceId, master_code AS MasterCode,
               master_sha256 AS MasterSha256, dna_version AS DnaVersion, status AS Status,
               spec_json::text AS SpecJson, note AS Note, created_at AS CreatedAt,
               approved_at AS ApprovedAt, approved_by AS ApprovedBy,
               locked_at AS LockedAt, locked_by AS LockedBy
        FROM pack_content.video_character_dna
        """;

    public async Task<IReadOnlyList<DnaRow>> ListLatestAsync(string eraId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<DnaRow>(
            SelectSql + " WHERE era_id = @Era ORDER BY character_id, dna_version DESC;",
            new { Era = eraId });
        return rows.GroupBy(r => r.CharacterId, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
    }

    public async Task<DnaRow?> GetByVersionAsync(string characterId, string eraId, string version, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<DnaRow>(
            SelectSql + " WHERE character_id = @Character AND era_id = @Era AND dna_version = @Version LIMIT 1;",
            new { Character = characterId, Era = eraId, Version = version });
    }

    public async Task<DnaRow?> GetByCodeAsync(string dnaCode, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<DnaRow>(
            SelectSql + " WHERE dna_code = @Code LIMIT 1;",
            new { Code = dnaCode });
    }

    public async Task<DnaRow> InsertDraftAsync(DnaRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_character_dna (
                id, dna_code, document_id, project_code, character_id, character_name, era_id,
                master_reference_id, master_code, master_sha256, dna_version, status, spec_json, note
            ) VALUES (
                @Id, @DnaCode, @DocumentId, 'FAMIXA', @CharacterId, @CharacterName, @EraId,
                @MasterReferenceId, @MasterCode, @MasterSha256, @DnaVersion, 'DRAFT', @SpecJson::jsonb, @Note
            );
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        try
        {
            await conn.ExecuteAsync(sql, row);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return await GetByVersionAsync(row.CharacterId, row.EraId, row.DnaVersion, ct)
                ?? throw new InvalidOperationException("DNA_GATE_NOT_SATISFIED: duplicate V1.");
        }
        return row;
    }

    public async Task<DnaRow> ApproveAndLockAsync(Guid id, string actor, string note, string dnaSha256, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            var current = await conn.QuerySingleOrDefaultAsync<DnaRow>(
                SelectSql + " WHERE id = @Id FOR UPDATE;",
                new { Id = id }, tx);
            if (current is null)
                throw new InvalidOperationException("Character DNA không tồn tại.");
            if (current.Status == "LOCKED")
            {
                await tx.CommitAsync(ct);
                return current;
            }
            var extra = JsonSerializer.Serialize(new
            {
                dna_sha256 = dnaSha256,
                master_sha256 = current.MasterSha256,
                master_id = current.MasterReferenceId,
                approved_by = actor,
            });
            await conn.ExecuteAsync("""
                UPDATE pack_content.video_character_dna
                SET status = 'LOCKED',
                    note = COALESCE(NULLIF(@Note, ''), note),
                    approved_at = NOW(),
                    approved_by = @Actor,
                    locked_at = NOW(),
                    locked_by = @Actor,
                    extra_json = COALESCE(extra_json, '{}'::jsonb) || @Extra::jsonb
                WHERE id = @Id AND status IN ('DRAFT', 'REJECTED', 'APPROVED');
                """, new { Id = id, Actor = actor, Note = note, Extra = extra }, tx);
            var next = await conn.QuerySingleAsync<DnaRow>(SelectSql + " WHERE id = @Id;", new { Id = id }, tx);
            await tx.CommitAsync(ct);
            return next;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<DnaRow> UpdateSpecAsync(Guid id, string specJson, string note, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var n = await conn.ExecuteAsync("""
            UPDATE pack_content.video_character_dna
            SET spec_json = @Spec::jsonb, note = COALESCE(NULLIF(@Note, ''), note)
            WHERE id = @Id AND status IN ('DRAFT', 'REJECTED');
            """, new { Id = id, Spec = specJson, Note = note });
        if (n == 0)
            throw new InvalidOperationException("DNA_LOCKED: V1 không overwrite. Dùng CHAR-001-MINH-ERA01-DNA-V2.");
        return await GetByIdAsync(id, ct) ?? throw new InvalidOperationException("Character DNA không tồn tại.");
    }

    public async Task<DnaRow> SetStatusAsync(Guid id, string status, string note, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync("""
            UPDATE pack_content.video_character_dna
            SET status = @Status, note = COALESCE(NULLIF(@Note, ''), note)
            WHERE id = @Id AND status <> 'LOCKED';
            """, new { Id = id, Status = status, Note = note });
        return await GetByIdAsync(id, ct) ?? throw new InvalidOperationException("Character DNA không tồn tại.");
    }

    public async Task<DnaRow> RejectAsync(Guid id, string note, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync("""
            UPDATE pack_content.video_character_dna
            SET status = 'REJECTED', note = COALESCE(NULLIF(@Note, ''), note)
            WHERE id = @Id AND status <> 'LOCKED';
            """, new { Id = id, Note = note });
        return await GetByIdAsync(id, ct) ?? throw new InvalidOperationException("Character DNA không tồn tại.");
    }

    public async Task<DnaRow?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<DnaRow>(SelectSql + " WHERE id = @Id;", new { Id = id });
    }

    public async Task InsertEventAsync(Guid dnaId, string eventType, object payload, string? actor, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync("""
            INSERT INTO pack_content.video_character_dna_event (id, dna_id, event_type, payload_json, actor)
            VALUES (@Id, @DnaId, @EventType, @Payload::jsonb, @Actor);
            """, new
        {
            Id = Guid.NewGuid(),
            DnaId = dnaId,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload),
            Actor = actor,
        });
    }
}
