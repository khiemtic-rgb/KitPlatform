using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ProductionScopeFoundationRepository : IProductionScopeFoundationStore
{
    readonly IDbConnectionFactory _db;

    public ProductionScopeFoundationRepository(IDbConnectionFactory db) => _db = db;

    public async Task InsertAnchorAsync(ProductionScopeAnchor anchor, CancellationToken ct = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO pack_content.production_scope_anchor (scope_id, kind, series_build_id, test_run_id, created_at)
            VALUES (@ScopeId, @Kind, @SeriesBuildId, @TestRunId, @CreatedAt)
            """, new
        {
            anchor.ScopeId,
            anchor.Kind,
            anchor.SeriesBuildId,
            anchor.TestRunId,
            anchor.CreatedAt,
        }, cancellationToken: ct));
    }

    public async Task<ProductionScopeAnchor?> GetAnchorAsync(Guid scopeId, CancellationToken ct = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<AnchorRow>(new CommandDefinition("""
            SELECT scope_id AS ScopeId, kind AS Kind, series_build_id AS SeriesBuildId,
                   test_run_id AS TestRunId, created_at AS CreatedAt
            FROM pack_content.production_scope_anchor
            WHERE scope_id = @ScopeId
            """, new { ScopeId = scopeId }, cancellationToken: ct));
        return row is null ? null : new ProductionScopeAnchor(row.ScopeId, row.Kind, row.SeriesBuildId, row.TestRunId, row.CreatedAt);
    }

    public async Task InsertTestRunAsync(ProductionTestRun run, CancellationToken ct = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO pack_content.production_test_run (test_run_id, name, status, created_at, created_by, metadata)
            VALUES (@TestRunId, @Name, @Status, @CreatedAt, @CreatedBy, CAST(@MetadataJson AS jsonb))
            """, run, cancellationToken: ct));
    }

    public async Task<ProductionTestRun?> GetTestRunAsync(Guid testRunId, CancellationToken ct = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<RunRow>(new CommandDefinition("""
            SELECT test_run_id AS TestRunId, name AS Name, status AS Status,
                   created_at AS CreatedAt, created_by AS CreatedBy, metadata::text AS MetadataJson
            FROM pack_content.production_test_run
            WHERE test_run_id = @TestRunId
            """, new { TestRunId = testRunId }, cancellationToken: ct));
        return row is null ? null : new ProductionTestRun(row.TestRunId, row.Name, row.Status, row.CreatedAt, row.CreatedBy, row.MetadataJson ?? "{}");
    }

    public async Task InsertTestScopeAsync(ProductionTestScope scope, CancellationToken ct = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO pack_content.production_test_scope (scope_id, test_run_id, episode_code, scene_code, created_at, metadata)
            VALUES (@ScopeId, @TestRunId, @EpisodeCode, @SceneCode, @CreatedAt, CAST(@MetadataJson AS jsonb))
            """, scope, cancellationToken: ct));
    }

    public async Task<ProductionTestScope?> GetTestScopeAsync(Guid scopeId, CancellationToken ct = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<ScopeRow>(new CommandDefinition("""
            SELECT scope_id AS ScopeId, test_run_id AS TestRunId, episode_code AS EpisodeCode,
                   scene_code AS SceneCode, created_at AS CreatedAt, metadata::text AS MetadataJson
            FROM pack_content.production_test_scope
            WHERE scope_id = @ScopeId
            """, new { ScopeId = scopeId }, cancellationToken: ct));
        return row is null ? null : new ProductionTestScope(row.ScopeId, row.TestRunId, row.EpisodeCode, row.SceneCode, row.CreatedAt, row.MetadataJson ?? "{}");
    }

    public async Task<ProductionTestScope?> GetTestScopeByRunAsync(Guid testRunId, CancellationToken ct = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<ScopeRow>(new CommandDefinition("""
            SELECT scope_id AS ScopeId, test_run_id AS TestRunId, episode_code AS EpisodeCode,
                   scene_code AS SceneCode, created_at AS CreatedAt, metadata::text AS MetadataJson
            FROM pack_content.production_test_scope
            WHERE test_run_id = @TestRunId
            ORDER BY created_at
            LIMIT 1
            """, new { TestRunId = testRunId }, cancellationToken: ct));
        return row is null ? null : new ProductionTestScope(row.ScopeId, row.TestRunId, row.EpisodeCode, row.SceneCode, row.CreatedAt, row.MetadataJson ?? "{}");
    }

    public async Task InsertTestShotAsync(ProductionTestShot shot, CancellationToken ct = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO pack_content.production_test_shot (
                test_shot_id, scope_id, shot_id, episode_code, scene_code, shot_code, created_at, metadata)
            VALUES (
                @TestShotId, @ScopeId, @ShotId, @EpisodeCode, @SceneCode, @ShotCode, @CreatedAt, CAST(@MetadataJson AS jsonb))
            """, shot, cancellationToken: ct));
    }

    public async Task<ProductionTestShot?> GetTestShotAsync(Guid scopeId, string shotCode, CancellationToken ct = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<ShotRow>(new CommandDefinition("""
            SELECT test_shot_id AS TestShotId, scope_id AS ScopeId, shot_id AS ShotId,
                   episode_code AS EpisodeCode, scene_code AS SceneCode, shot_code AS ShotCode,
                   created_at AS CreatedAt, metadata::text AS MetadataJson
            FROM pack_content.production_test_shot
            WHERE scope_id = @ScopeId AND shot_code = @ShotCode
            """, new { ScopeId = scopeId, ShotCode = shotCode }, cancellationToken: ct));
        return row is null ? null : new ProductionTestShot(row.TestShotId, row.ScopeId, row.ShotId, row.EpisodeCode, row.SceneCode, row.ShotCode, row.CreatedAt, row.MetadataJson ?? "{}");
    }

    sealed class AnchorRow
    {
        public Guid ScopeId { get; set; }
        public string Kind { get; set; } = "";
        public Guid? SeriesBuildId { get; set; }
        public Guid? TestRunId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    sealed class RunRow
    {
        public Guid TestRunId { get; set; }
        public string Name { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public string CreatedBy { get; set; } = "";
        public string? MetadataJson { get; set; }
    }

    sealed class ScopeRow
    {
        public Guid ScopeId { get; set; }
        public Guid TestRunId { get; set; }
        public string EpisodeCode { get; set; } = "";
        public string SceneCode { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public string? MetadataJson { get; set; }
    }

    sealed class ShotRow
    {
        public Guid TestShotId { get; set; }
        public Guid ScopeId { get; set; }
        public string ShotId { get; set; } = "";
        public string EpisodeCode { get; set; } = "";
        public string SceneCode { get; set; } = "";
        public string ShotCode { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public string? MetadataJson { get; set; }
    }
}
