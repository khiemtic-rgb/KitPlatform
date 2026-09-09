using Dapper;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoContinuityRepository
{
    private readonly IDbConnectionFactory _db;
    public KitVideoContinuityRepository(IDbConnectionFactory db) => _db = db;

    public sealed class GraphRow
    {
        public Guid ProductionId { get; set; }
        public string ScriptText { get; set; } = "";
        public string ScriptHash { get; set; } = "";
        public string GraphJson { get; set; } = "{}";
    }

    public sealed class OverrideRow
    {
        public string ShotId { get; set; } = "";
        public string Reason { get; set; } = "";
        public string ApprovedBy { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
    }

    public async Task<GraphRow?> GetAsync(Guid productionId, CancellationToken ct)
    {
        const string sql = """
            SELECT production_id AS ProductionId, script_text AS ScriptText,
                   script_hash AS ScriptHash, graph_json::text AS GraphJson
            FROM pack_content.video_story_graph
            WHERE production_id = @Id;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<GraphRow>(sql, new { Id = productionId });
    }

    public async Task UpsertAsync(Guid productionId, string script, string hash, string graph, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_story_graph (production_id, script_text, script_hash, graph_json, updated_at)
            VALUES (@Id, @Script, @Hash, @Graph::jsonb, NOW())
            ON CONFLICT (production_id) DO UPDATE
            SET script_text = EXCLUDED.script_text, script_hash = EXCLUDED.script_hash,
                graph_json = EXCLUDED.graph_json, updated_at = NOW();
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { Id = productionId, Script = script, Hash = hash, Graph = graph });
    }

    public async Task<IReadOnlyList<OverrideRow>> ListOverridesAsync(Guid productionId, CancellationToken ct)
    {
        const string sql = """
            SELECT shot_id AS ShotId, reason AS Reason, approved_by AS ApprovedBy, created_at AS CreatedAt
            FROM pack_content.video_continuity_override
            WHERE production_id = @Id
            ORDER BY created_at DESC;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<OverrideRow>(sql, new { Id = productionId });
        return rows.ToList();
    }

    public async Task InsertOverrideAsync(Guid productionId, string shotId, string reason, string approvedBy, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_continuity_override (id, production_id, shot_id, reason, approved_by, created_at)
            VALUES (@Id, @ProductionId, @ShotId, @Reason, @ApprovedBy, NOW());
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            Id = Guid.NewGuid(),
            ProductionId = productionId,
            ShotId = shotId,
            Reason = reason,
            ApprovedBy = approvedBy,
        });
    }
}
