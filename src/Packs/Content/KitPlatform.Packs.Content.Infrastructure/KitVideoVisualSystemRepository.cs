using System.Linq;
using Dapper;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoVisualSystemRepository
{
    private readonly IDbConnectionFactory _db;
    public KitVideoVisualSystemRepository(IDbConnectionFactory db) => _db = db;

    public sealed class Row
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string ProjectCode { get; set; } = "";
        public string SystemCode { get; set; } = "";
        public string Version { get; set; } = "V1";
        public string Status { get; set; } = "draft";
        public string RulesJson { get; set; } = "{}";
        public DateTimeOffset? LockedAt { get; set; }
        public string? LockedBy { get; set; }
    }

    private const string Select = """
        SELECT s.id AS Id, s.project_id AS ProjectId, p.project_code AS ProjectCode,
               s.system_code AS SystemCode, s.version AS Version, s.status AS Status,
               s.rules_json::text AS RulesJson, s.locked_at AS LockedAt, s.locked_by AS LockedBy
        FROM pack_content.video_visual_system s
        JOIN pack_content.video_project p ON p.id = s.project_id
        """;

    public async Task<Row?> GetAsync(string projectCode, string systemCode, string version, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Row>(
            Select + " WHERE p.project_code = @Project AND s.system_code = @Code AND s.version = @Version LIMIT 1;",
            new { Project = projectCode, Code = systemCode, Version = version });
    }

    public async Task<Row?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Row>(Select + " WHERE s.id = @Id LIMIT 1;", new { Id = id });
    }

    public async Task LockAsync(Guid id, string? actor, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(
            """
            UPDATE pack_content.video_visual_system
            SET status = 'locked', locked_at = NOW(), locked_by = @Actor, updated_at = NOW()
            WHERE id = @Id AND status <> 'locked';
            """,
            new { Id = id, Actor = actor ?? "" });
    }

    public async Task<Guid?> GetProjectIdAsync(string projectCode, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Guid?>(
            "SELECT id FROM pack_content.video_project WHERE project_code = @Project LIMIT 1;",
            new { Project = projectCode });
    }

    public async Task<IReadOnlyList<Row>> ListBySystemAsync(string projectCode, string systemCode, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<Row>(
            Select + " WHERE p.project_code = @Project AND s.system_code = @Code ORDER BY s.version;",
            new { Project = projectCode, Code = systemCode });
        return rows.ToList();
    }

    public async Task<Row?> GetInForceAsync(string projectCode, string systemCode, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Row>(
            Select + " WHERE p.project_code = @Project AND s.system_code = @Code AND s.status IN ('proposed', 'locked') ORDER BY s.version DESC LIMIT 1;",
            new { Project = projectCode, Code = systemCode });
    }

    public async Task InsertAsync(
        Guid id, Guid projectId, string systemCode, string version, string status, string rulesJson, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_content.video_visual_system
                (id, project_id, system_code, version, status, rules_json)
            VALUES
                (@Id, @ProjectId, @Code, @Version, @Status, @Rules::jsonb);
            """,
            new { Id = id, ProjectId = projectId, Code = systemCode, Version = version, Status = status, Rules = rulesJson });
    }

    public async Task UpdateRulesJsonAsync(Guid id, string rulesJson, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(
            """
            UPDATE pack_content.video_visual_system
            SET rules_json = @Rules::jsonb, updated_at = NOW()
            WHERE id = @Id;
            """,
            new { Id = id, Rules = rulesJson });
    }

    public async Task SupersedeAsync(Guid id, Guid? nextId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(
            """
            UPDATE pack_content.video_visual_system
            SET status = 'superseded', superseded_by = @Next, updated_at = NOW()
            WHERE id = @Id AND status <> 'locked';
            """,
            new { Id = id, Next = nextId });
    }
}
