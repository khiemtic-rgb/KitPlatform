using Dapper;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoMasterReviewRepository
{
    private readonly IDbConnectionFactory _db;
    public KitVideoMasterReviewRepository(IDbConnectionFactory db) => _db = db;

    public sealed class ReviewRow
    {
        public Guid Id { get; set; }
        public string ProjectCode { get; set; } = "FAMIXA";
        public string CharacterId { get; set; } = "CHAR-001";
        public string EraId { get; set; } = "ERA-01";
        public Guid CandidateId { get; set; }
        public string CandidateCode { get; set; } = "";
        public Guid? IdentityTestId { get; set; }
        public Guid? StressTestId { get; set; }
        public string SourceSha256 { get; set; } = "";
        public string SourceFingerprint { get; set; } = "";
        public string Status { get; set; } = "PENDING";
        public string DocumentId { get; set; } = KitVideoMasterReviewRules.DocumentId;
        public string MasterRefCode { get; set; } = KitVideoMasterReviewRules.MasterRefCode;
        public string Note { get; set; } = "";
        public string ExtraJson { get; set; } = "{}";
    }

    public async Task InsertAsync(ReviewRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_master_review (
                id, project_code, character_id, era_id, candidate_id, candidate_code,
                identity_test_id, stress_test_id, source_sha256, source_fingerprint,
                status, document_id, master_ref_code, note, extra_json
            ) VALUES (
                @Id, @ProjectCode, @CharacterId, @EraId, @CandidateId, @CandidateCode,
                @IdentityTestId, @StressTestId, @SourceSha256, @SourceFingerprint,
                @Status, @DocumentId, @MasterRefCode, @Note, @ExtraJson::jsonb
            );
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, row);
    }

    public async Task<ReviewRow?> GetAsync(Guid id, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, project_code AS ProjectCode, character_id AS CharacterId, era_id AS EraId,
                   candidate_id AS CandidateId, candidate_code AS CandidateCode,
                   identity_test_id AS IdentityTestId, stress_test_id AS StressTestId,
                   source_sha256 AS SourceSha256, source_fingerprint AS SourceFingerprint,
                   status AS Status, document_id AS DocumentId, master_ref_code AS MasterRefCode,
                   note AS Note, extra_json::text AS ExtraJson
            FROM pack_content.video_master_review WHERE id = @Id LIMIT 1;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ReviewRow>(sql, new { Id = id });
    }

    public async Task<ReviewRow?> GetByCandidateAsync(Guid candidateId, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, project_code AS ProjectCode, character_id AS CharacterId, era_id AS EraId,
                   candidate_id AS CandidateId, candidate_code AS CandidateCode,
                   identity_test_id AS IdentityTestId, stress_test_id AS StressTestId,
                   source_sha256 AS SourceSha256, source_fingerprint AS SourceFingerprint,
                   status AS Status, document_id AS DocumentId, master_ref_code AS MasterRefCode,
                   note AS Note, extra_json::text AS ExtraJson
            FROM pack_content.video_master_review
            WHERE candidate_id = @Id
            ORDER BY created_at DESC
            LIMIT 1;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ReviewRow>(sql, new { Id = candidateId });
    }

    public async Task<IReadOnlyList<ReviewRow>> ListAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, project_code AS ProjectCode, character_id AS CharacterId, era_id AS EraId,
                   candidate_id AS CandidateId, candidate_code AS CandidateCode,
                   identity_test_id AS IdentityTestId, stress_test_id AS StressTestId,
                   source_sha256 AS SourceSha256, source_fingerprint AS SourceFingerprint,
                   status AS Status, document_id AS DocumentId, master_ref_code AS MasterRefCode,
                   note AS Note, extra_json::text AS ExtraJson
            FROM pack_content.video_master_review
            ORDER BY created_at DESC;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<ReviewRow>(sql)).ToList();
    }

    public async Task<bool> HasDirectorPassAsync(Guid candidateId, CancellationToken ct)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM pack_content.video_master_review
                WHERE candidate_id = @Id
                  AND status IN ('PASS', 'SELECTED', 'APPROVED', 'LOCKED')
            );
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(sql, new { Id = candidateId });
    }

    public async Task SetStatusAsync(Guid id, string status, string? note, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.video_master_review
            SET status = @Status, note = COALESCE(@Note, note), updated_at = NOW()
            WHERE id = @Id AND status <> 'LOCKED';
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { Id = id, Status = status, Note = note });
    }

    public async Task InsertDecisionAsync(Guid reviewId, Guid candidateId, string decision, string reason, string actor, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_master_review_decision (
                id, review_id, candidate_id, decision, reason, actor
            ) VALUES (
                @Id, @ReviewId, @CandidateId, @Decision, @Reason, @Actor
            );
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            Id = Guid.NewGuid(),
            ReviewId = reviewId,
            CandidateId = candidateId,
            Decision = decision,
            Reason = reason,
            Actor = actor,
        });
    }

    public async Task<string?> LatestDecisionAsync(Guid reviewId, CancellationToken ct)
    {
        const string sql = """
            SELECT decision FROM pack_content.video_master_review_decision
            WHERE review_id = @Id AND decision IN ('PASS', 'CONDITIONAL', 'REJECT')
            ORDER BY created_at DESC LIMIT 1;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<string?>(sql, new { Id = reviewId });
    }

    public async Task MarkEligibleOnPassAsync(Guid candidateId, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.video_asset_candidate
            SET status = CASE WHEN status = 'DRAFT' THEN 'REVIEW' ELSE status END,
                extra_json = CASE
                  WHEN COALESCE(extra_json->'selection'->>'lifecycle', '') IN ('FRONT_RUNNER', 'ELIGIBLE')
                  THEN extra_json
                  ELSE jsonb_set(COALESCE(extra_json, '{}'::jsonb), '{selection,lifecycle}', '"ELIGIBLE"', true)
                END
            WHERE id = @Id AND status NOT IN ('LOCKED', 'MASTER_REFERENCE_SOURCE', 'SELECTED_AS_MASTER', 'MASTER', 'REJECTED');
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { Id = candidateId });
    }

    public async Task InsertEventAsync(Guid reviewId, string eventType, string? payload, string actor, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_master_review_event (id, review_id, event_type, payload_json, actor)
            VALUES (@Id, @ReviewId, @EventType, @Payload::jsonb, @Actor);
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            Id = Guid.NewGuid(),
            ReviewId = reviewId,
            EventType = eventType,
            Payload = string.IsNullOrWhiteSpace(payload) ? "{}" : payload,
            Actor = actor,
        });
    }
}
