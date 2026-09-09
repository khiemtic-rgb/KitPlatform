using System.Linq;
using System.Text.Json;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;
using Npgsql;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class CharacterReferencePackRepository
{
    private readonly IDbConnectionFactory _db;
    public CharacterReferencePackRepository(IDbConnectionFactory db) => _db = db;

    public sealed class PackRow
    {
        public Guid Id { get; set; }
        public string PackCode { get; set; } = "";
        public string DocumentId { get; set; } = CharacterReferencePackRules.DocumentId;
        public string CharacterId { get; set; } = "";
        public string CharacterName { get; set; } = "";
        public string EraId { get; set; } = "ERA-01";
        public string PackVersion { get; set; } = "V1";
        public Guid MasterReferenceId { get; set; }
        public string MasterSha256 { get; set; } = "";
        public Guid CharacterDnaId { get; set; }
        public string DnaSha256 { get; set; } = "";
        public string Status { get; set; } = "DRAFT";
        public string Notes { get; set; } = "";
        public string? CreatedBy { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTimeOffset? ApprovedAt { get; set; }
        public DateTimeOffset? LockedAt { get; set; }
        public string? LockedBy { get; set; }
        public string? PackSha256 { get; set; }
        public Guid? SupersedesId { get; set; }
        public string ExtraJson { get; set; } = "{}";
    }

    public sealed class ItemRow
    {
        public Guid Id { get; set; }
        public Guid PackId { get; set; }
        public string RefType { get; set; } = "";
        public bool Required { get; set; }
        public string ArtifactPath { get; set; } = "";
        public string ArtifactSha256 { get; set; } = "";
        public string Status { get; set; } = "MISSING";
        public string MetadataJson { get; set; } = "{}";
        public DateTimeOffset CreatedAt { get; set; }
    }

    private const string SelectPack = """
        SELECT id AS Id, pack_code AS PackCode, document_id AS DocumentId,
               character_id AS CharacterId, character_name AS CharacterName, era_id AS EraId,
               pack_version AS PackVersion, master_reference_id AS MasterReferenceId,
               master_sha256 AS MasterSha256, character_dna_id AS CharacterDnaId,
               dna_sha256 AS DnaSha256, status AS Status, notes AS Notes, created_by AS CreatedBy,
               created_at AS CreatedAt, updated_at AS UpdatedAt, approved_by AS ApprovedBy,
               approved_at AS ApprovedAt, locked_at AS LockedAt, locked_by AS LockedBy,
               pack_sha256 AS PackSha256, supersedes_id AS SupersedesId, extra_json::text AS ExtraJson
        FROM pack_content.video_character_reference_pack
        """;

    private const string SelectItem = """
        SELECT id AS Id, pack_id AS PackId, ref_type AS RefType, required AS Required,
               artifact_path AS ArtifactPath, artifact_sha256 AS ArtifactSha256,
               status AS Status, metadata_json::text AS MetadataJson, created_at AS CreatedAt
        FROM pack_content.video_character_reference_item
        """;

    public async Task<PackRow?> GetByVersionAsync(string characterId, string eraId, string version, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<PackRow>(
            SelectPack + " WHERE character_id = @Character AND era_id = @Era AND pack_version = @Version LIMIT 1;",
            new { Character = characterId, Era = eraId, Version = version });
    }

    public async Task<PackRow?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<PackRow>(SelectPack + " WHERE id = @Id;", new { Id = id });
    }

    public async Task<IReadOnlyList<PackRow>> ListByCharacterAsync(string characterId, string eraId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<PackRow>(
            SelectPack + " WHERE character_id = @Character AND era_id = @Era ORDER BY pack_version;",
            new { Character = characterId, Era = eraId });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<PackRow>> ListLatestAsync(string eraId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<PackRow>(
            SelectPack + """
             WHERE era_id = @Era AND status <> 'SUPERSEDED'
             ORDER BY character_id, pack_version DESC, created_at DESC
            """,
            new { Era = eraId });
        return rows
            .GroupBy(r => r.CharacterId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    public async Task<IReadOnlyList<ItemRow>> ListItemsForPacksAsync(IReadOnlyCollection<Guid> packIds, CancellationToken ct)
    {
        if (packIds.Count == 0) return [];
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<ItemRow>(
            SelectItem + " WHERE pack_id = ANY(@Ids) ORDER BY ref_type;",
            new { Ids = packIds.ToArray() });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ItemRow>> ListItemsAsync(Guid packId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<ItemRow>(SelectItem + " WHERE pack_id = @PackId ORDER BY ref_type;", new { PackId = packId });
        return rows.ToList();
    }

    public async Task<PackRow> InsertDraftAsync(PackRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_character_reference_pack (
                id, pack_code, document_id, project_code, character_id, character_name, era_id,
                pack_version, master_reference_id, master_sha256, character_dna_id, dna_sha256,
                status, notes, created_by, supersedes_id, extra_json
            ) VALUES (
                @Id, @PackCode, @DocumentId, 'FAMIXA', @CharacterId, @CharacterName, @EraId,
                @PackVersion, @MasterReferenceId, @MasterSha256, @CharacterDnaId, @DnaSha256,
                'DRAFT', @Notes, @CreatedBy, @SupersedesId, COALESCE(@ExtraJson, '{}')::jsonb
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
                ?? throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: duplicate version.");
        }
        return row;
    }

    public async Task<ItemRow> UpsertItemAsync(ItemRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_character_reference_item (
                id, pack_id, ref_type, required, artifact_path, artifact_sha256, status, metadata_json
            ) VALUES (
                @Id, @PackId, @RefType, @Required, @ArtifactPath, @ArtifactSha256, @Status, @MetadataJson::jsonb
            )
            ON CONFLICT (pack_id, ref_type) DO UPDATE SET
                artifact_path = EXCLUDED.artifact_path,
                artifact_sha256 = EXCLUDED.artifact_sha256,
                status = EXCLUDED.status,
                metadata_json = EXCLUDED.metadata_json;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, row);
        return row;
    }

    public async Task<PackRow> SetStatusAsync(Guid id, string status, string? actor, string notes, string? packSha, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var current = await conn.QuerySingleOrDefaultAsync<PackRow>(SelectPack + " WHERE id = @Id FOR UPDATE;", new { Id = id }, tx)
            ?? throw new InvalidOperationException("Character Reference Pack không tồn tại.");
        if (current.Status == "LOCKED")
        {
            await tx.CommitAsync(ct);
            return current;
        }
        await conn.ExecuteAsync("""
            UPDATE pack_content.video_character_reference_pack
            SET status = @Status, notes = @Notes, updated_at = NOW(),
                approved_by = CASE WHEN @Status IN ('APPROVED', 'DIRECTOR_APPROVED', 'LOCKED') THEN COALESCE(@Actor, approved_by) ELSE approved_by END,
                approved_at = CASE WHEN @Status IN ('APPROVED', 'DIRECTOR_APPROVED', 'LOCKED') THEN COALESCE(approved_at, NOW()) ELSE approved_at END,
                locked_by = CASE WHEN @Status = 'LOCKED' THEN @Actor ELSE locked_by END,
                locked_at = CASE WHEN @Status = 'LOCKED' THEN NOW() ELSE locked_at END,
                pack_sha256 = COALESCE(@PackSha, pack_sha256)
            WHERE id = @Id;
            """, new { Id = id, Status = status, Notes = notes, Actor = actor, PackSha = packSha }, tx);
        await tx.CommitAsync(ct);
        return await GetByIdAsync(id, ct) ?? current;
    }

    public async Task SetExtraJsonAsync(Guid id, string extraJson, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync("""
            UPDATE pack_content.video_character_reference_pack
            SET extra_json = @Extra::jsonb, updated_at = NOW()
            WHERE id = @Id AND status IN ('DRAFT', 'REVIEW', 'VALIDATED', 'REJECTED');
            """, new { Id = id, Extra = extraJson });
    }

    public async Task<PackRow> MarkSupersededAsync(Guid id, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync("""
            UPDATE pack_content.video_character_reference_pack
            SET status = 'SUPERSEDED', updated_at = NOW()
            WHERE id = @Id AND status = 'LOCKED';
            """, new { Id = id });
        return await GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("Character Reference Pack không tồn tại.");
    }

    public async Task InsertEventAsync(Guid packId, string eventType, object payload, string? actor, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync("""
            INSERT INTO pack_content.video_character_reference_pack_event (id, pack_id, event_type, payload_json, actor)
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
