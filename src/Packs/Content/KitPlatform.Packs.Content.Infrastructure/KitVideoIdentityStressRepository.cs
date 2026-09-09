using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoIdentityStressRepository
{
    private readonly IDbConnectionFactory _db;
    public KitVideoIdentityStressRepository(IDbConnectionFactory db) => _db = db;

    public sealed class TestRow
    {
        public Guid Id { get; set; }
        public string ProjectCode { get; set; } = "FAMIXA";
        public string CharacterId { get; set; } = "CHAR-001";
        public string EraId { get; set; } = "ERA-01";
        public Guid CandidateId { get; set; }
        public string CandidateCode { get; set; } = "";
        public Guid? IdentityTestId { get; set; }
        public string SourceSha256 { get; set; } = "";
        public string SourceFingerprint { get; set; } = "";
        public string DnaVersion { get; set; } = "V1";
        public string IdentityVersion { get; set; } = "V1";
        public string Status { get; set; } = "PENDING";
        public string DocumentId { get; set; } = KitVideoIdentityStressRules.DocumentId;
        public string ExtraJson { get; set; } = "{}";
    }

    public sealed class AttemptRow
    {
        public Guid Id { get; set; }
        public Guid TestId { get; set; }
        public string TestGroup { get; set; } = "";
        public string TestCase { get; set; } = "";
        public int Attempt { get; set; }
        public string ArtifactPath { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public string Fingerprint { get; set; } = "";
        public string ImageType { get; set; } = "IDENTITY_STRESS";
        public string QaStatus { get; set; } = "PENDING";
        public string QaJson { get; set; } = "{}";
        public string Provider { get; set; } = "";
        public string Model { get; set; } = "";
    }

    public async Task InsertTestAsync(TestRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_identity_stress_test (
                id, project_code, character_id, era_id, candidate_id, candidate_code, identity_test_id,
                source_sha256, source_fingerprint, dna_version, identity_version, status, document_id, extra_json
            ) VALUES (
                @Id, @ProjectCode, @CharacterId, @EraId, @CandidateId, @CandidateCode, @IdentityTestId,
                @SourceSha256, @SourceFingerprint, @DnaVersion, @IdentityVersion, @Status, @DocumentId, @ExtraJson::jsonb
            );
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, row);
    }

    public async Task<TestRow?> GetTestAsync(Guid id, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, project_code AS ProjectCode, character_id AS CharacterId, era_id AS EraId,
                   candidate_id AS CandidateId, candidate_code AS CandidateCode, identity_test_id AS IdentityTestId,
                   source_sha256 AS SourceSha256, source_fingerprint AS SourceFingerprint,
                   dna_version AS DnaVersion, identity_version AS IdentityVersion, status AS Status,
                   document_id AS DocumentId, extra_json::text AS ExtraJson
            FROM pack_content.video_identity_stress_test WHERE id = @Id LIMIT 1;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<TestRow>(sql, new { Id = id });
    }

    public async Task<IReadOnlyList<TestRow>> ListTestsAsync(Guid? candidateId, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, project_code AS ProjectCode, character_id AS CharacterId, era_id AS EraId,
                   candidate_id AS CandidateId, candidate_code AS CandidateCode, identity_test_id AS IdentityTestId,
                   source_sha256 AS SourceSha256, source_fingerprint AS SourceFingerprint,
                   dna_version AS DnaVersion, identity_version AS IdentityVersion, status AS Status,
                   document_id AS DocumentId, extra_json::text AS ExtraJson
            FROM pack_content.video_identity_stress_test
            WHERE (@CandidateId IS NULL OR candidate_id = @CandidateId)
            ORDER BY created_at DESC;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<TestRow>(sql, new { CandidateId = candidateId })).ToList();
    }

    public async Task SetStatusAsync(Guid id, string status, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.video_identity_stress_test
            SET status = @Status, updated_at = NOW()
            WHERE id = @Id;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { Id = id, Status = status });
    }

    public async Task InsertAttemptAsync(AttemptRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_identity_stress_attempt (
                id, test_id, test_group, test_case, attempt, artifact_path, sha256, fingerprint,
                image_type, qa_status, qa_json, provider, model
            ) VALUES (
                @Id, @TestId, @TestGroup, @TestCase, @Attempt, @ArtifactPath, @Sha256, @Fingerprint,
                @ImageType, @QaStatus, @QaJson::jsonb, @Provider, @Model
            );
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, row);
    }

    public async Task<IReadOnlyList<AttemptRow>> ListAttemptsAsync(Guid testId, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, test_id AS TestId, test_group AS TestGroup, test_case AS TestCase,
                   attempt AS Attempt, artifact_path AS ArtifactPath, sha256 AS Sha256, fingerprint AS Fingerprint,
                   image_type AS ImageType, qa_status AS QaStatus, qa_json::text AS QaJson,
                   provider AS Provider, model AS Model
            FROM pack_content.video_identity_stress_attempt
            WHERE test_id = @Id
            ORDER BY test_case, attempt;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<AttemptRow>(sql, new { Id = testId })).ToList();
    }

    public async Task<AttemptRow?> GetAttemptAsync(Guid id, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, test_id AS TestId, test_group AS TestGroup, test_case AS TestCase,
                   attempt AS Attempt, artifact_path AS ArtifactPath, sha256 AS Sha256, fingerprint AS Fingerprint,
                   image_type AS ImageType, qa_status AS QaStatus, qa_json::text AS QaJson,
                   provider AS Provider, model AS Model
            FROM pack_content.video_identity_stress_attempt
            WHERE id = @Id LIMIT 1;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<AttemptRow>(sql, new { Id = id });
    }

    public async Task<int> NextAttemptAsync(Guid testId, string testCase, CancellationToken ct)
    {
        const string sql = """
            SELECT COALESCE(MAX(attempt), 0) + 1
            FROM pack_content.video_identity_stress_attempt
            WHERE test_id = @TestId AND test_case = @Case;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(sql, new { TestId = testId, Case = testCase });
    }

    public async Task UpdateAttemptQaAsync(Guid id, string qaStatus, string qaJson, string? imageType, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.video_identity_stress_attempt
            SET qa_status = @QaStatus, qa_json = @QaJson::jsonb, image_type = COALESCE(@ImageType, image_type)
            WHERE id = @Id;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { Id = id, QaStatus = qaStatus, QaJson = qaJson, ImageType = imageType });
    }

    public async Task UpsertResultAsync(
        Guid testId, string environment, string lighting, string camera, string emotion, string wardrobe,
        string identity, string age, string hair, string face, int score,
        string p0, string p1, string p2, string diagnosis, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_identity_stress_result (
                id, test_id, environment, lighting, camera, emotion, wardrobe,
                identity_stability, age_stability, hair_stability, face_stability,
                score, p0, p1, p2, diagnosis
            ) VALUES (
                @Id, @TestId, @Environment, @Lighting, @Camera, @Emotion, @Wardrobe,
                @Identity, @Age, @Hair, @Face, @Score, @P0::jsonb, @P1::jsonb, @P2::jsonb, @Diagnosis::jsonb
            )
            ON CONFLICT (test_id) DO UPDATE SET
                environment = EXCLUDED.environment,
                lighting = EXCLUDED.lighting,
                camera = EXCLUDED.camera,
                emotion = EXCLUDED.emotion,
                wardrobe = EXCLUDED.wardrobe,
                identity_stability = EXCLUDED.identity_stability,
                age_stability = EXCLUDED.age_stability,
                hair_stability = EXCLUDED.hair_stability,
                face_stability = EXCLUDED.face_stability,
                score = EXCLUDED.score,
                p0 = EXCLUDED.p0,
                p1 = EXCLUDED.p1,
                p2 = EXCLUDED.p2,
                diagnosis = EXCLUDED.diagnosis;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            Id = Guid.NewGuid(),
            TestId = testId,
            Environment = environment,
            Lighting = lighting,
            Camera = camera,
            Emotion = emotion,
            Wardrobe = wardrobe,
            Identity = identity,
            Age = age,
            Hair = hair,
            Face = face,
            Score = score,
            P0 = p0,
            P1 = p1,
            P2 = p2,
            Diagnosis = diagnosis,
        });
    }

    public sealed class ResultRow
    {
        public string Environment { get; set; } = "PENDING";
        public string Lighting { get; set; } = "PENDING";
        public string Camera { get; set; } = "PENDING";
        public string Emotion { get; set; } = "PENDING";
        public string Wardrobe { get; set; } = "PENDING";
        public string Identity { get; set; } = "PENDING";
        public string Age { get; set; } = "PENDING";
        public string Hair { get; set; } = "PENDING";
        public string Face { get; set; } = "PENDING";
        public int Score { get; set; }
        public string P0 { get; set; } = "[]";
        public string P1 { get; set; } = "[]";
        public string P2 { get; set; } = "[]";
    }

    public async Task<ResultRow?> GetResultAsync(Guid testId, CancellationToken ct)
    {
        const string sql = """
            SELECT environment AS Environment, lighting AS Lighting, camera AS Camera,
                   emotion AS Emotion, wardrobe AS Wardrobe, identity_stability AS Identity,
                   age_stability AS Age, hair_stability AS Hair, face_stability AS Face,
                   score AS Score, p0::text AS P0, p1::text AS P1, p2::text AS P2
            FROM pack_content.video_identity_stress_result WHERE test_id = @Id LIMIT 1;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ResultRow>(sql, new { Id = testId });
    }

    public async Task InsertDecisionAsync(Guid testId, Guid candidateId, string decision, string reason, string actor, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_identity_stress_decision (
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

    public async Task InsertEventAsync(Guid testId, string eventType, string? testCase, string actor, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_identity_stress_event (
                id, test_id, event_type, test_case, actor
            ) VALUES (
                @Id, @TestId, @EventType, @TestCase, @Actor
            );
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            Id = Guid.NewGuid(),
            TestId = testId,
            EventType = eventType,
            TestCase = testCase,
            Actor = actor,
        });
    }

    public async Task<string?> LatestDecisionAsync(Guid testId, CancellationToken ct)
    {
        const string sql = """
            SELECT decision FROM pack_content.video_identity_stress_decision
            WHERE test_id = @Id ORDER BY created_at DESC LIMIT 1;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<string?>(sql, new { Id = testId });
    }
}
