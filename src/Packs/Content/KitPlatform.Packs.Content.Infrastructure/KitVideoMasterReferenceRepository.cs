using Dapper;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoMasterReferenceRepository
{
    private readonly IDbConnectionFactory _db;
    public KitVideoMasterReferenceRepository(IDbConnectionFactory db) => _db = db;

    public sealed class AssetRow
    {
        public Guid AssetId { get; set; }
        public Guid VersionId { get; set; }
        public string ProjectCode { get; set; } = "";
        public string AssetCode { get; set; } = "";
        public string Lifecycle { get; set; } = "";
        public string ExtraJson { get; set; } = "{}";
        public string Era { get; set; } = "";
        public string Version { get; set; } = "V1";
        public string Status { get; set; } = "DRAFT";
    }

    public sealed class CandidateRow
    {
        public Guid Id { get; set; }
        public Guid VersionId { get; set; }
        public string CandidateCode { get; set; } = "";
        public string Role { get; set; } = "";
        public int GenerationAttempt { get; set; }
        public string ArtifactPath { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public string VisualStyleVersion { get; set; } = "V1";
        public string CharacterId { get; set; } = "";
        public string EraId { get; set; } = "";
        public string ImageType { get; set; } = "UNKNOWN";
        public string Status { get; set; } = "DRAFT";
        public string QaStatus { get; set; } = "PENDING";
        public string QaJson { get; set; } = "{}";
        public string ExtraJson { get; set; } = "{}";
        public string Fingerprint { get; set; } = "";
        public Guid? ParentCandidateId { get; set; }
    }

    public async Task<AssetRow> EnsureCharacterAssetAsync(
        string projectCode, string assetCode, string name, string era, CancellationToken ct)
    {
        var existing = await GetAssetAsync(projectCode, assetCode, ct);
        if (existing is not null) return existing;
        var assetId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        const string insert = """
            INSERT INTO pack_content.video_asset
                (id, project_id, asset_code, asset_kind, name, lifecycle, current_version, current_era, extra_json)
            SELECT @Id, p.id, @Code, 'CHARACTER', @Name, 'DRAFT', 'V1', @Era,
                   jsonb_build_object('source', 'character_studio', 'characterCode', @Code)
            FROM pack_content.video_project p
            WHERE p.project_code = @Project
            ON CONFLICT (project_id, asset_code) DO NOTHING;

            INSERT INTO pack_content.video_asset_version
                (id, asset_id, version, era, status, is_current, canon_json)
            SELECT @VersionId, a.id, 'V1', @Era, 'DRAFT', TRUE, '{}'::jsonb
            FROM pack_content.video_asset a
            JOIN pack_content.video_project p ON p.id = a.project_id
            WHERE p.project_code = @Project AND a.asset_code = @Code
              AND NOT EXISTS (
                  SELECT 1 FROM pack_content.video_asset_version v
                  WHERE v.asset_id = a.id AND v.is_current);
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(insert, new
        {
            Id = assetId,
            VersionId = versionId,
            Project = projectCode,
            Code = assetCode,
            Name = name,
            Era = era,
        });
        return await GetAssetAsync(projectCode, assetCode, ct)
            ?? throw new InvalidOperationException("CHARACTER_IDENTITY_NOT_READY: chưa có video asset.");
    }

    public async Task<AssetRow?> GetAssetAsync(string projectCode, string assetCode, CancellationToken ct)
    {
        const string sql = """
            SELECT a.id AS AssetId, v.id AS VersionId, p.project_code AS ProjectCode,
                   a.asset_code AS AssetCode, a.lifecycle AS Lifecycle,
                   a.extra_json::text AS ExtraJson, v.era AS Era, v.version AS Version, v.status AS Status
            FROM pack_content.video_asset a
            JOIN pack_content.video_project p ON p.id = a.project_id
            JOIN pack_content.video_asset_version v ON v.asset_id = a.id AND v.is_current
            WHERE p.project_code = @Project AND a.asset_code = @Code
            LIMIT 1;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<AssetRow>(sql, new { Project = projectCode, Code = assetCode });
    }

    public async Task<IReadOnlyList<CandidateRow>> ListCandidatesAsync(Guid versionId, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, version_id AS VersionId, candidate_code AS CandidateCode, role AS Role,
                   generation_attempt AS GenerationAttempt, artifact_path AS ArtifactPath, sha256 AS Sha256,
                   visual_style_version AS VisualStyleVersion, character_id AS CharacterId, era_id AS EraId,
                   image_type AS ImageType, status AS Status, qa_status AS QaStatus, qa_json::text AS QaJson,
                   extra_json::text AS ExtraJson, COALESCE(fingerprint, '') AS Fingerprint,
                   parent_candidate_id AS ParentCandidateId
            FROM pack_content.video_asset_candidate
            WHERE version_id = @Id
            ORDER BY created_at, candidate_code;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<CandidateRow>(sql, new { Id = versionId })).ToList();
    }

    public async Task<CandidateRow?> GetCandidateAsync(Guid id, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, version_id AS VersionId, candidate_code AS CandidateCode, role AS Role,
                   generation_attempt AS GenerationAttempt, artifact_path AS ArtifactPath, sha256 AS Sha256,
                   visual_style_version AS VisualStyleVersion, character_id AS CharacterId, era_id AS EraId,
                   image_type AS ImageType, status AS Status, qa_status AS QaStatus, qa_json::text AS QaJson,
                   extra_json::text AS ExtraJson, COALESCE(fingerprint, '') AS Fingerprint,
                   parent_candidate_id AS ParentCandidateId
            FROM pack_content.video_asset_candidate
            WHERE id = @Id
            LIMIT 1;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<CandidateRow>(sql, new { Id = id });
    }

    public async Task InsertCandidateAsync(CandidateRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_asset_candidate (
                id, version_id, candidate_code, role, generation_attempt, artifact_path, sha256,
                visual_style_version, character_id, era_id, image_type, status, qa_status, qa_json,
                extra_json, fingerprint, parent_candidate_id
            ) VALUES (
                @Id, @VersionId, @CandidateCode, @Role, @GenerationAttempt, @ArtifactPath, @Sha256,
                @VisualStyleVersion, @CharacterId, @EraId, @ImageType, @Status, @QaStatus, @QaJson::jsonb,
                @ExtraJson::jsonb, @Fingerprint, @ParentCandidateId
            );
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, row);
    }

    public async Task UpdateCandidateQaAsync(Guid id, string qaStatus, string qaJson, CancellationToken ct, string? imageType = null)
    {
        const string sql = """
            UPDATE pack_content.video_asset_candidate
            SET qa_status = @QaStatus,
                qa_json = @QaJson::jsonb,
                image_type = COALESCE(@ImageType, image_type),
                extra_json = COALESCE(@ExtraJson::jsonb, extra_json)
            WHERE id = @Id AND status NOT IN ('LOCKED', 'MASTER_REFERENCE_SOURCE', 'SELECTED_AS_MASTER', 'MASTER');
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { Id = id, QaStatus = qaStatus, QaJson = qaJson, ExtraJson = (string?)null, ImageType = imageType });
    }

    public async Task<int> CountFingerprintAsync(Guid versionId, string fingerprint, CancellationToken ct)
    {
        const string sql = """
            SELECT COUNT(*) FROM pack_content.video_asset_candidate
            WHERE version_id = @VersionId AND fingerprint = @Fingerprint;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(sql, new { VersionId = versionId, Fingerprint = fingerprint });
    }

    public async Task<string?> MinhDnaStatusAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT v.canon_json->'visualDna'->>'status'
            FROM pack_content.famixa_character c
            JOIN pack_content.famixa_character_version v ON v.character_id = c.id AND v.is_current_canon
            WHERE c.character_code = 'CHAR-001'
            LIMIT 1;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<string?>(sql);
    }

    public async Task<string> MinhDnaVersionAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT COALESCE(v.canon_json->'visualDna'->>'version', 'V1')
            FROM pack_content.famixa_character c
            JOIN pack_content.famixa_character_version v ON v.character_id = c.id AND v.is_current_canon
            WHERE c.character_code = 'CHAR-001'
            LIMIT 1;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<string?>(sql) ?? "V1";
    }

    public async Task SetCandidateStatusAsync(Guid id, string status, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.video_asset_candidate SET status = @Status
            WHERE id = @Id AND status NOT IN ('LOCKED', 'MASTER_REFERENCE_SOURCE', 'SELECTED_AS_MASTER', 'MASTER');
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { Id = id, Status = status });
    }

    public async Task MergeCandidateExtraAsync(Guid id, string extraJson, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.video_asset_candidate
            SET extra_json = COALESCE(extra_json, '{}'::jsonb) || @Extra::jsonb
            WHERE id = @Id AND status NOT IN ('LOCKED', 'MASTER_REFERENCE_SOURCE', 'SELECTED_AS_MASTER', 'MASTER');
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { Id = id, Extra = extraJson });
    }

    public async Task InsertEventAsync(
        Guid assetId, Guid versionId, Guid? candidateId, string eventType, string? actor,
        string artifactPath, string sha256, string payload, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_asset_event (
                id, asset_id, version_id, candidate_id, event_type, actor, artifact_path, sha256, payload
            ) VALUES (
                @Id, @AssetId, @VersionId, @CandidateId, @EventType, @Actor, @Path, @Sha, @Payload::jsonb
            );
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            Id = Guid.NewGuid(),
            AssetId = assetId,
            VersionId = versionId,
            CandidateId = candidateId,
            EventType = eventType,
            Actor = actor,
            Path = artifactPath,
            Sha = sha256,
            Payload = string.IsNullOrWhiteSpace(payload) ? "{}" : payload,
        });
    }

    public async Task<IReadOnlyList<EventRow>> ListEventsAsync(Guid assetId, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, event_type AS EventType, actor AS Actor, candidate_id AS CandidateId,
                   artifact_path AS ArtifactPath, sha256 AS Sha256, visual_dna_version AS VisualDnaVersion,
                   created_at AS CreatedAt
            FROM pack_content.video_asset_event
            WHERE asset_id = @Id
            ORDER BY created_at;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<EventRow>(sql, new { Id = assetId })).ToList();
    }

    public async Task UpsertMasterReferenceAsync(Guid versionId, string path, string sha256, string candidateCode, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_asset_reference (id, version_id, kind, path, is_primary, qa_status, qa_json)
            VALUES (@Id, @VersionId, 'MASTER_REFERENCE', @Path, TRUE, 'PASS', @Qa::jsonb)
            ON CONFLICT (version_id, kind) DO UPDATE
            SET path = EXCLUDED.path, qa_status = 'PASS', qa_json = EXCLUDED.qa_json
            WHERE pack_content.video_asset_reference.qa_json->>'locked' IS DISTINCT FROM 'true';
            """;
        var qa = $"{{\"sha256\":\"{sha256}\",\"sourceCandidate\":\"{candidateCode}\",\"copiedBytes\":false}}";
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { Id = Guid.NewGuid(), VersionId = versionId, Path = path, Qa = qa });
    }

    public sealed class EventRow
    {
        public Guid Id { get; set; }
        public string EventType { get; set; } = "";
        public string? Actor { get; set; }
        public Guid? CandidateId { get; set; }
        public string ArtifactPath { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public string VisualDnaVersion { get; set; } = "V1";
        public DateTimeOffset CreatedAt { get; set; }
    }

    public async Task MergeAssetExtraAsync(Guid assetId, string extraJson, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.video_asset
            SET extra_json = COALESCE(extra_json, '{}'::jsonb) || @Extra::jsonb,
                updated_at = NOW()
            WHERE id = @Id;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { Id = assetId, Extra = extraJson });
    }

    public async Task<int> CountSuccessfulFingerprintAsync(Guid versionId, string fingerprint, CancellationToken ct)
    {
        const string sql = """
            SELECT COUNT(*) FROM pack_content.video_asset_candidate
            WHERE version_id = @VersionId AND fingerprint = @Fingerprint
              AND status IN ('REVIEW', 'APPROVED', 'LOCKED', 'MASTER', 'MASTER_REFERENCE_SOURCE', 'SELECTED_AS_MASTER');
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(sql, new { VersionId = versionId, Fingerprint = fingerprint });
    }

    public async Task SetMasterStatusAsync(Guid assetId, string status, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.video_asset
            SET extra_json = jsonb_set(
                  COALESCE(extra_json, '{}'::jsonb),
                  '{masterReference,status}',
                  to_jsonb(@Status::text),
                  true
                ),
                updated_at = NOW()
            WHERE id = @Id
              AND COALESCE(extra_json->'masterReference'->>'status', '') <> 'LOCKED';
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { Id = assetId, Status = status });
    }
}
