using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoIdentityTestRepository
{
    private readonly IDbConnectionFactory _db;
    public KitVideoIdentityTestRepository(IDbConnectionFactory db) => _db = db;

    public sealed class TestRow
    {
        public Guid Id { get; set; }
        public string ProjectCode { get; set; } = "FAMIXA";
        public string CharacterId { get; set; } = "CHAR-001";
        public string EraId { get; set; } = "ERA-01";
        public Guid CandidateId { get; set; }
        public string CandidateCode { get; set; } = "";
        public string SourceSha256 { get; set; } = "";
        public string SourceFingerprint { get; set; } = "";
        public string DnaVersion { get; set; } = "V1";
        public string Status { get; set; } = "PENDING";
        public string DocumentId { get; set; } = KitVideoIdentityTestRules.DocumentId;
        public string ExtraJson { get; set; } = "{}";
    }

    public sealed class ArtifactRow
    {
        public Guid Id { get; set; }
        public Guid TestId { get; set; }
        public string TestType { get; set; } = "";
        public string TestVariant { get; set; } = "";
        public int Attempt { get; set; }
        public string ArtifactPath { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public string Fingerprint { get; set; } = "";
        public string ImageType { get; set; } = "IDENTITY_TEST";
        public string QaStatus { get; set; } = "PENDING";
        public string QaJson { get; set; } = "{}";
        public string Provider { get; set; } = "";
        public string Model { get; set; } = "";
    }

    public async Task InsertTestAsync(TestRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_identity_test (
                id, project_code, character_id, era_id, candidate_id, candidate_code,
                source_sha256, source_fingerprint, dna_version, status, document_id, extra_json
            ) VALUES (
                @Id, @ProjectCode, @CharacterId, @EraId, @CandidateId, @CandidateCode,
                @SourceSha256, @SourceFingerprint, @DnaVersion, @Status, @DocumentId, @ExtraJson::jsonb
            );
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, row);
    }

    public async Task<TestRow?> GetTestAsync(Guid id, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, project_code AS ProjectCode, character_id AS CharacterId, era_id AS EraId,
                   candidate_id AS CandidateId, candidate_code AS CandidateCode, source_sha256 AS SourceSha256,
                   source_fingerprint AS SourceFingerprint, dna_version AS DnaVersion, status AS Status,
                   document_id AS DocumentId, extra_json::text AS ExtraJson
            FROM pack_content.video_identity_test WHERE id = @Id LIMIT 1;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<TestRow>(sql, new { Id = id });
    }

    public async Task<IReadOnlyList<TestRow>> ListTestsAsync(Guid? candidateId, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, project_code AS ProjectCode, character_id AS CharacterId, era_id AS EraId,
                   candidate_id AS CandidateId, candidate_code AS CandidateCode, source_sha256 AS SourceSha256,
                   source_fingerprint AS SourceFingerprint, dna_version AS DnaVersion, status AS Status,
                   document_id AS DocumentId, extra_json::text AS ExtraJson
            FROM pack_content.video_identity_test
            WHERE (@CandidateId IS NULL OR candidate_id = @CandidateId)
            ORDER BY created_at DESC;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<TestRow>(sql, new { CandidateId = candidateId })).ToList();
    }

    public async Task SetStatusAsync(Guid id, string status, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.video_identity_test
            SET status = @Status, updated_at = NOW()
            WHERE id = @Id;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { Id = id, Status = status });
    }

    public async Task InsertArtifactAsync(ArtifactRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_identity_test_artifact (
                id, test_id, test_type, test_variant, attempt, artifact_path, sha256, fingerprint,
                image_type, qa_status, qa_json, provider, model
            ) VALUES (
                @Id, @TestId, @TestType, @TestVariant, @Attempt, @ArtifactPath, @Sha256, @Fingerprint,
                @ImageType, @QaStatus, @QaJson::jsonb, @Provider, @Model
            );
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, row);
    }

    public async Task<IReadOnlyList<ArtifactRow>> ListArtifactsAsync(Guid testId, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, test_id AS TestId, test_type AS TestType, test_variant AS TestVariant,
                   attempt AS Attempt, artifact_path AS ArtifactPath, sha256 AS Sha256, fingerprint AS Fingerprint,
                   image_type AS ImageType, qa_status AS QaStatus, qa_json::text AS QaJson,
                   provider AS Provider, model AS Model
            FROM pack_content.video_identity_test_artifact
            WHERE test_id = @Id
            ORDER BY test_variant, attempt;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<ArtifactRow>(sql, new { Id = testId })).ToList();
    }

    public async Task<ArtifactRow?> GetArtifactAsync(Guid id, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, test_id AS TestId, test_type AS TestType, test_variant AS TestVariant,
                   attempt AS Attempt, artifact_path AS ArtifactPath, sha256 AS Sha256, fingerprint AS Fingerprint,
                   image_type AS ImageType, qa_status AS QaStatus, qa_json::text AS QaJson,
                   provider AS Provider, model AS Model
            FROM pack_content.video_identity_test_artifact
            WHERE id = @Id LIMIT 1;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ArtifactRow>(sql, new { Id = id });
    }

    public async Task<int> NextAttemptAsync(Guid testId, string variant, CancellationToken ct)
    {
        const string sql = """
            SELECT COALESCE(MAX(attempt), 0) + 1
            FROM pack_content.video_identity_test_artifact
            WHERE test_id = @TestId AND test_variant = @Variant;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(sql, new { TestId = testId, Variant = variant });
    }

    public async Task UpdateArtifactQaAsync(Guid id, string qaStatus, string qaJson, string? imageType, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.video_identity_test_artifact
            SET qa_status = @QaStatus, qa_json = @QaJson::jsonb, image_type = COALESCE(@ImageType, image_type)
            WHERE id = @Id;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { Id = id, QaStatus = qaStatus, QaJson = qaJson, ImageType = imageType });
    }

    public async Task UpsertResultAsync(Guid testId, string view, string emotion, string identity, int score, string p0, string diagnosis, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_identity_test_result (
                id, test_id, view_stability, emotion_stability, identity_stability, score, p0, diagnosis
            ) VALUES (
                @Id, @TestId, @View, @Emotion, @Identity, @Score, @P0::jsonb, @Diagnosis::jsonb
            )
            ON CONFLICT (test_id) DO UPDATE SET
                view_stability = EXCLUDED.view_stability,
                emotion_stability = EXCLUDED.emotion_stability,
                identity_stability = EXCLUDED.identity_stability,
                score = EXCLUDED.score,
                p0 = EXCLUDED.p0,
                diagnosis = EXCLUDED.diagnosis;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            Id = Guid.NewGuid(),
            TestId = testId,
            View = view,
            Emotion = emotion,
            Identity = identity,
            Score = score,
            P0 = p0,
            Diagnosis = diagnosis,
        });
    }

    public sealed class ResultRow
    {
        public string View { get; set; } = "PENDING";
        public string Emotion { get; set; } = "PENDING";
        public string Identity { get; set; } = "PENDING";
        public int Score { get; set; }
    }

    public async Task<ResultRow?> GetResultAsync(Guid testId, CancellationToken ct)
    {
        const string sql = """
            SELECT view_stability AS View, emotion_stability AS Emotion,
                   identity_stability AS Identity, score AS Score
            FROM pack_content.video_identity_test_result WHERE test_id = @Id LIMIT 1;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ResultRow>(sql, new { Id = testId });
    }

    public async Task InsertDecisionAsync(Guid testId, Guid candidateId, string decision, string reason, string actor, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_identity_test_decision (
                id, test_id, candidate_id, decision, reason, actor
            ) VALUES (
                @Id, @TestId, @CandidateId, @Decision, @Reason, @Actor
            );
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            Id = Guid.NewGuid(),
            TestId = testId,
            CandidateId = candidateId,
            Decision = decision,
            Reason = reason,
            Actor = actor,
        });
    }

    public async Task<bool> HasCompleteAsync(Guid candidateId, CancellationToken ct)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM pack_content.video_identity_test t
                WHERE t.candidate_id = @Id
                  AND (
                    t.status IN ('PASS', 'CONDITIONAL')
                    OR (
                        SELECT COUNT(DISTINCT a.test_variant)
                        FROM pack_content.video_identity_test_artifact a
                        WHERE a.test_id = t.id
                          AND length(a.sha256) > 8
                          AND a.qa_status NOT IN ('PENDING', 'GENERATION_FAILED', 'ARTIFACT_FAILED')
                    ) >= 7
                  )
            );
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(sql, new { Id = candidateId });
    }

    public async Task<Guid?> LatestCompleteIdAsync(Guid candidateId, CancellationToken ct)
    {
        const string sql = """
            SELECT t.id
            FROM pack_content.video_identity_test t
            WHERE t.candidate_id = @Id
              AND (
                t.status IN ('PASS', 'CONDITIONAL')
                OR (
                    SELECT COUNT(DISTINCT a.test_variant)
                    FROM pack_content.video_identity_test_artifact a
                    WHERE a.test_id = t.id
                      AND length(a.sha256) > 8
                      AND a.qa_status NOT IN ('PENDING', 'GENERATION_FAILED', 'ARTIFACT_FAILED')
                ) >= 7
              )
            ORDER BY t.created_at DESC
            LIMIT 1;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<Guid?>(sql, new { Id = candidateId });
    }

    public async Task<string?> LatestDecisionAsync(Guid testId, CancellationToken ct)
    {
        const string sql = """
            SELECT decision FROM pack_content.video_identity_test_decision
            WHERE test_id = @Id ORDER BY created_at DESC LIMIT 1;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<string?>(sql, new { Id = testId });
    }
}
