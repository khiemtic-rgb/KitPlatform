using System.Text.Json;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class CharacterIdentityGovernanceRepository
{
    private readonly IDbConnectionFactory _db;
    public CharacterIdentityGovernanceRepository(IDbConnectionFactory db) => _db = db;

    public sealed class AuditRow
    {
        public Guid Id { get; set; }
        public string CharacterId { get; set; } = "";
        public string EraId { get; set; } = "ERA-01";
        public Guid? MasterId { get; set; }
        public Guid? DnaId { get; set; }
        public Guid? PrpId { get; set; }
        public Guid? ShotId { get; set; }
        public string Gate { get; set; } = "";
        public string Result { get; set; } = "";
        public string? Code { get; set; }
        public string? Source { get; set; }
        public string? Attribute { get; set; }
        public string? RequestedValue { get; set; }
        public string? AuthoritativeValue { get; set; }
        public string Reason { get; set; } = "";
        public string? Actor { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public async Task InsertManyAsync(IReadOnlyList<AuditRow> rows, object payload, CancellationToken ct)
    {
        foreach (var row in rows)
            await InsertAsync(row, payload, ct);
    }

    public async Task InsertAsync(AuditRow row, object payload, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync("""
            INSERT INTO pack_content.video_identity_governance_audit (
                id, project_code, character_id, era_id, master_id, dna_id, prp_id, shot_id,
                gate, result, code, source, attribute, requested_value, authoritative_value,
                reason, actor, payload_json
            ) VALUES (
                @Id, 'FAMIXA', @CharacterId, @EraId, @MasterId, @DnaId, @PrpId, @ShotId,
                @Gate, @Result, @Code, @Source, @Attribute, @RequestedValue, @AuthoritativeValue,
                @Reason, @Actor, @Payload::jsonb
            );
            """, new
        {
            row.Id,
            row.CharacterId,
            row.EraId,
            row.MasterId,
            row.DnaId,
            row.PrpId,
            row.ShotId,
            row.Gate,
            row.Result,
            row.Code,
            row.Source,
            row.Attribute,
            row.RequestedValue,
            row.AuthoritativeValue,
            row.Reason,
            row.Actor,
            Payload = JsonSerializer.Serialize(payload),
        });
    }

    public async Task<IReadOnlyList<AuditRow>> ListAsync(string characterId, string eraId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<AuditRow>("""
            SELECT id AS Id, character_id AS CharacterId, era_id AS EraId,
                   master_id AS MasterId, dna_id AS DnaId, prp_id AS PrpId, shot_id AS ShotId,
                   gate AS Gate, result AS Result, code AS Code, source AS Source,
                   attribute AS Attribute, requested_value AS RequestedValue,
                   authoritative_value AS AuthoritativeValue, reason AS Reason,
                   actor AS Actor, created_at AS CreatedAt
            FROM pack_content.video_identity_governance_audit
            WHERE character_id = @Character AND era_id = @Era
            ORDER BY created_at DESC
            LIMIT 100;
            """, new { Character = characterId, Era = eraId });
        return rows.ToList();
    }
}
