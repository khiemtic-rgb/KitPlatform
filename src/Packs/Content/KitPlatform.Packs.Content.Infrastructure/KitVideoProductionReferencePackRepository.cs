using System.Linq;
using System.Text.Json;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;
using Npgsql;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoProductionReferencePackRepository
{
    private readonly IDbConnectionFactory _db;
    public KitVideoProductionReferencePackRepository(IDbConnectionFactory db) => _db = db;

    public sealed class PackRow
    {
        public Guid Id { get; set; }
        public string PackCode { get; set; } = KitVideoProductionReferencePackRules.PackCode;
        public string DocumentId { get; set; } = KitVideoProductionReferencePackRules.DocumentId;
        public string CharacterId { get; set; } = KitVideoProductionReferencePackRules.CharacterId;
        public string CharacterName { get; set; } = KitVideoProductionReferencePackRules.CharacterName;
        public string EraId { get; set; } = KitVideoProductionReferencePackRules.EraId;
        public Guid MasterReferenceId { get; set; }
        public string MasterCode { get; set; } = KitVideoCharacterDnaRules.MasterCode;
        public string MasterSha256 { get; set; } = "";
        public Guid CharacterDnaId { get; set; }
        public string DnaCode { get; set; } = KitVideoCharacterDnaRules.DnaCode;
        public string DnaSha256 { get; set; } = "";
        public string PackVersion { get; set; } = KitVideoProductionReferencePackRules.Version;
        public string Status { get; set; } = "DRAFT";
        public string SpecJson { get; set; } = "{}";
        public string Note { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ApprovedAt { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTimeOffset? LockedAt { get; set; }
        public string? LockedBy { get; set; }
        public string ExtraJson { get; set; } = "{}";
        public string? PrpSha256 { get; set; }
    }

    private const string SelectSql = """
        SELECT id AS Id, pack_code AS PackCode, document_id AS DocumentId,
               character_id AS CharacterId, character_name AS CharacterName, era_id AS EraId,
               master_reference_id AS MasterReferenceId, master_code AS MasterCode,
               master_sha256 AS MasterSha256, character_dna_id AS CharacterDnaId,
               dna_code AS DnaCode, dna_sha256 AS DnaSha256, pack_version AS PackVersion,
               status AS Status, spec_json::text AS SpecJson, note AS Note,
               created_at AS CreatedAt, approved_at AS ApprovedAt, approved_by AS ApprovedBy,
               locked_at AS LockedAt, locked_by AS LockedBy, extra_json::text AS ExtraJson
        FROM pack_content.video_production_reference_pack
        """;

    public async Task<IReadOnlyList<PackRow>> ListLatestAsync(string eraId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<PackRow>(
            SelectSql + " WHERE era_id = @Era ORDER BY character_id, pack_version DESC;",
            new { Era = eraId });
        return rows.GroupBy(r => r.CharacterId, StringComparer.OrdinalIgnoreCase)
            .Select(g => AttachSha(g.First())!)
            .ToList();
    }

    public async Task<PackRow?> GetByVersionAsync(string characterId, string eraId, string version, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return AttachSha(await conn.QuerySingleOrDefaultAsync<PackRow>(
            SelectSql + " WHERE character_id = @Character AND era_id = @Era AND pack_version = @Version LIMIT 1;",
            new { Character = characterId, Era = eraId, Version = version }));
    }

    public async Task<PackRow?> GetByCodeAsync(string packCode, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return AttachSha(await conn.QuerySingleOrDefaultAsync<PackRow>(
            SelectSql + " WHERE pack_code = @Code LIMIT 1;",
            new { Code = packCode }));
    }

    public async Task<PackRow?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return AttachSha(await conn.QuerySingleOrDefaultAsync<PackRow>(SelectSql + " WHERE id = @Id;", new { Id = id }));
    }

    public async Task<PackRow> InsertDraftAsync(PackRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_production_reference_pack (
                id, pack_code, document_id, project_code, character_id, character_name, era_id,
                master_reference_id, master_code, master_sha256, character_dna_id, dna_code, dna_sha256,
                pack_version, status, spec_json, note
            ) VALUES (
                @Id, @PackCode, @DocumentId, 'FAMIXA', @CharacterId, @CharacterName, @EraId,
                @MasterReferenceId, @MasterCode, @MasterSha256, @CharacterDnaId, @DnaCode, @DnaSha256,
                @PackVersion, 'DRAFT', @SpecJson::jsonb, @Note
            );
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        try
        {
            await conn.ExecuteAsync(sql, row);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return await GetByVersionAsync(row.CharacterId, row.EraId, row.PackVersion, ct)
                ?? throw new InvalidOperationException("PRP_GATE_NOT_SATISFIED: duplicate V1.");
        }
        return row;
    }

    public async Task<PackRow> ApproveAndLockAsync(Guid id, string actor, string note, string prpSha256, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            var current = await conn.QuerySingleOrDefaultAsync<PackRow>(
                SelectSql + " WHERE id = @Id FOR UPDATE;",
                new { Id = id }, tx);
            if (current is null)
                throw new InvalidOperationException("Production Reference Pack không tồn tại.");
            if (current.Status == "LOCKED")
            {
                await tx.CommitAsync(ct);
                return AttachSha(current)!;
            }
            var extra = JsonSerializer.Serialize(new
            {
                prp_sha256 = prpSha256,
                master_sha256 = current.MasterSha256,
                dna_sha256 = current.DnaSha256,
                master_id = current.MasterReferenceId,
                dna_id = current.CharacterDnaId,
                approved_by = actor,
                approved_at = DateTimeOffset.UtcNow,
            });
            await conn.ExecuteAsync("""
                UPDATE pack_content.video_production_reference_pack
                SET status = 'LOCKED',
                    note = COALESCE(NULLIF(@Note, ''), note),
                    approved_at = NOW(),
                    approved_by = @Actor,
                    locked_at = NOW(),
                    locked_by = @Actor,
                    extra_json = COALESCE(extra_json, '{}'::jsonb) || @Extra::jsonb
                WHERE id = @Id AND status IN ('DRAFT', 'REJECTED', 'APPROVED');
                """, new { Id = id, Actor = actor, Note = note, Extra = extra }, tx);
            var next = await conn.QuerySingleAsync<PackRow>(SelectSql + " WHERE id = @Id;", new { Id = id }, tx);
            await tx.CommitAsync(ct);
            return AttachSha(next)!;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<PackRow> UpdateSpecAsync(Guid id, string specJson, string note, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var n = await conn.ExecuteAsync("""
            UPDATE pack_content.video_production_reference_pack
            SET spec_json = @Spec::jsonb, note = COALESCE(NULLIF(@Note, ''), note)
            WHERE id = @Id AND status IN ('DRAFT', 'REJECTED');
            """, new { Id = id, Spec = specJson, Note = note });
        if (n == 0)
            throw new InvalidOperationException("PRP_LOCKED: V1 không overwrite. Dùng CHAR-001-MINH-ERA01-PROD-REF-V2.");
        return await GetByIdAsync(id, ct) ?? throw new InvalidOperationException("Production Reference Pack không tồn tại.");
    }

    public async Task<PackRow> SetStatusAsync(Guid id, string status, string note, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync("""
            UPDATE pack_content.video_production_reference_pack
            SET status = @Status, note = COALESCE(NULLIF(@Note, ''), note)
            WHERE id = @Id AND status <> 'LOCKED';
            """, new { Id = id, Status = status, Note = note });
        return await GetByIdAsync(id, ct) ?? throw new InvalidOperationException("Production Reference Pack không tồn tại.");
    }

    public async Task<PackRow> RejectAsync(Guid id, string note, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync("""
            UPDATE pack_content.video_production_reference_pack
            SET status = 'REJECTED', note = COALESCE(NULLIF(@Note, ''), note)
            WHERE id = @Id AND status <> 'LOCKED';
            """, new { Id = id, Note = note });
        return await GetByIdAsync(id, ct) ?? throw new InvalidOperationException("Production Reference Pack không tồn tại.");
    }

    private static PackRow? AttachSha(PackRow? row)
    {
        if (row is null) return null;
        row.PrpSha256 = ReadPrpSha(row.ExtraJson);
        return row;
    }

    private static string? ReadPrpSha(string? extraJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(extraJson) ? "{}" : extraJson);
            return doc.RootElement.TryGetProperty("prp_sha256", out var n) && n.ValueKind == JsonValueKind.String
                ? n.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task InsertEventAsync(Guid packId, string eventType, object payload, string? actor, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync("""
            INSERT INTO pack_content.video_production_reference_pack_event (id, pack_id, event_type, payload_json, actor)
            VALUES (@Id, @PackId, @EventType, @Payload::jsonb, @Actor);
            """, new
        {
            Id = Guid.NewGuid(),
            PackId = packId,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload),
            Actor = actor,
        });
    }
}
