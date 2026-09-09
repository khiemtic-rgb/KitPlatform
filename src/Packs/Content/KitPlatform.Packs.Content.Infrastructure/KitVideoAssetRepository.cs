using Dapper;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoAssetRepository
{
    private readonly IDbConnectionFactory _db;
    public KitVideoAssetRepository(IDbConnectionFactory db) => _db = db;

    public sealed class AssetRow
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string ProjectCode { get; set; } = "";
        public string AssetCode { get; set; } = "";
        public string AssetKind { get; set; } = "";
        public string Name { get; set; } = "";
        public string Lifecycle { get; set; } = "";
        public string CurrentVersion { get; set; } = "V1";
        public string CurrentEra { get; set; } = "";
        public Guid? VersionId { get; set; }
        public string Version { get; set; } = "V1";
        public string Era { get; set; } = "";
        public string Status { get; set; } = "";
        public string CanonJson { get; set; } = "{}";
    }

    public sealed class RefRow
    {
        public Guid VersionId { get; set; }
        public string Kind { get; set; } = "";
        public string Path { get; set; } = "";
        public bool IsPrimary { get; set; }
        public bool IsSecondary { get; set; }
        public string QaStatus { get; set; } = "PENDING";
    }

    public sealed class EraRow
    {
        public Guid AssetId { get; set; }
        public string Era { get; set; } = "";
        public string Status { get; set; } = "";
        public string CanonJson { get; set; } = "{}";
    }

    public sealed class UsageRow
    {
        public string ShotCode { get; set; } = "";
        public string SceneCode { get; set; } = "";
    }

    private const string AssetSelect = """
        SELECT a.id AS Id, a.project_id AS ProjectId, p.project_code AS ProjectCode,
               a.asset_code AS AssetCode, a.asset_kind AS AssetKind, a.name AS Name,
               a.lifecycle AS Lifecycle, a.current_version AS CurrentVersion, a.current_era AS CurrentEra,
               v.id AS VersionId, v.version AS Version, v.era AS Era, v.status AS Status,
               v.canon_json::text AS CanonJson
        FROM pack_content.video_asset a
        JOIN pack_content.video_project p ON p.id = a.project_id
        LEFT JOIN pack_content.video_asset_version v ON v.asset_id = a.id AND v.is_current
        """;

    public async Task<IReadOnlyList<AssetRow>> ListAsync(string? projectCode, CancellationToken ct)
    {
        var sql = AssetSelect;
        if (!string.IsNullOrWhiteSpace(projectCode))
            sql += " WHERE p.project_code = @Code";
        sql += " ORDER BY a.asset_kind, a.asset_code";
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<AssetRow>(sql, new { Code = projectCode })).ToList();
    }

    public async Task<AssetRow?> GetAsync(string projectCode, string assetCode, CancellationToken ct)
    {
        var sql = AssetSelect + " WHERE p.project_code = @Project AND a.asset_code = @Code";
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<AssetRow>(sql, new { Project = projectCode, Code = assetCode });
    }

    public async Task<IReadOnlyList<RefRow>> ListRefsAsync(IReadOnlyList<Guid> versionIds, CancellationToken ct)
    {
        if (versionIds.Count == 0) return [];
        const string sql = """
            SELECT version_id AS VersionId, kind AS Kind, path AS Path,
                   is_primary AS IsPrimary, is_secondary AS IsSecondary, qa_status AS QaStatus
            FROM pack_content.video_asset_reference
            WHERE version_id = ANY(@Ids);
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<RefRow>(sql, new { Ids = versionIds.ToArray() })).ToList();
    }

    public async Task<IReadOnlyList<EraRow>> ListErasAsync(Guid assetId, CancellationToken ct)
    {
        const string sql = """
            SELECT asset_id AS AssetId, era AS Era, status AS Status, canon_json::text AS CanonJson
            FROM pack_content.video_asset_version
            WHERE asset_id = @Id
            ORDER BY era, version;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<EraRow>(sql, new { Id = assetId })).ToList();
    }

    public async Task UpdateCanonAsync(Guid versionId, string canonJson, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.video_asset_version SET canon_json = @Canon::jsonb WHERE id = @Id;
            UPDATE pack_content.video_asset SET updated_at = NOW()
            WHERE id = (SELECT asset_id FROM pack_content.video_asset_version WHERE id = @Id);
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { Id = versionId, Canon = canonJson });
    }

    public async Task SetLifecycleAsync(Guid assetId, Guid versionId, string life, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.video_asset SET lifecycle = @Life, updated_at = NOW() WHERE id = @AssetId;
            UPDATE pack_content.video_asset_version SET status = @Life WHERE id = @VersionId;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { AssetId = assetId, VersionId = versionId, Life = life });
    }

    public async Task InsertVersionAsync(
        Guid id, Guid assetId, string version, string era, string status, string canonJson, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.video_asset_version SET is_current = FALSE WHERE asset_id = @AssetId;
            INSERT INTO pack_content.video_asset_version (id, asset_id, version, era, status, is_current, canon_json)
            VALUES (@Id, @AssetId, @Version, @Era, @Status, TRUE, @Canon::jsonb);
            UPDATE pack_content.video_asset
            SET current_version = @Version, lifecycle = 'DRAFT', updated_at = NOW()
            WHERE id = @AssetId;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            Id = id,
            AssetId = assetId,
            Version = version,
            Era = era,
            Status = status,
            Canon = canonJson,
        });
    }

    public async Task<IReadOnlyList<UsageRow>> ListUsageAsync(Guid assetId, CancellationToken ct)
    {
        const string sql = """
            SELECT shot_code AS ShotCode, scene_code AS SceneCode
            FROM pack_content.video_asset_usage
            WHERE asset_id = @Id
            ORDER BY shot_code;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<UsageRow>(sql, new { Id = assetId })).ToList();
    }

    public async Task UpsertUsageAsync(Guid assetId, Guid productionId, string scene, string shot, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_asset_usage (id, asset_id, production_id, scene_code, shot_code)
            VALUES (@Id, @AssetId, @ProductionId, @Scene, @Shot)
            ON CONFLICT (asset_id, production_id, shot_code) DO NOTHING;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            Id = Guid.NewGuid(),
            AssetId = assetId,
            ProductionId = productionId,
            Scene = scene,
            Shot = shot,
        });
    }

    public async Task UpsertSceneMasterAsync(
        Guid productionId, string scene, string location, string status, string canon, string presence, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_scene_master (
                id, production_id, scene_code, location_code, status, canon_json, presence_json, updated_at)
            VALUES (@Id, @ProductionId, @Scene, @Location, @Status, @Canon::jsonb, @Presence::jsonb, NOW())
            ON CONFLICT (production_id, scene_code) DO UPDATE
            SET location_code = EXCLUDED.location_code, status = EXCLUDED.status,
                canon_json = EXCLUDED.canon_json, presence_json = EXCLUDED.presence_json, updated_at = NOW();
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            Id = Guid.NewGuid(),
            ProductionId = productionId,
            Scene = scene,
            Location = location,
            Status = status,
            Canon = canon,
            Presence = presence,
        });
    }

    public async Task InsertSnapshotAsync(Guid productionId, string shot, string snapshot, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_keyframe_snapshot (id, production_id, shot_code, snapshot_json, approved_at)
            VALUES (@Id, @ProductionId, @Shot, @Snap::jsonb, NOW())
            ON CONFLICT (production_id, shot_code) DO NOTHING;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            Id = Guid.NewGuid(),
            ProductionId = productionId,
            Shot = shot,
            Snap = snapshot,
        });
    }

    public async Task UpsertPackageAsync(Guid productionId, string shot, string scene, string status, string json, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_shot_package (
                id, production_id, shot_code, scene_code, status, package_json, updated_at)
            VALUES (@Id, @ProductionId, @Shot, @Scene, @Status, @Json::jsonb, NOW())
            ON CONFLICT (production_id, shot_code) DO UPDATE
            SET scene_code = EXCLUDED.scene_code, status = EXCLUDED.status,
                package_json = EXCLUDED.package_json, updated_at = NOW();
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            Id = Guid.NewGuid(),
            ProductionId = productionId,
            Shot = shot,
            Scene = scene,
            Status = status,
            Json = json,
        });
    }
}
