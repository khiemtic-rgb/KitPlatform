using System.Linq;
using System.Text.Json;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;
using Npgsql;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoMasterLockRepository
{
    private readonly IDbConnectionFactory _db;
    public KitVideoMasterLockRepository(IDbConnectionFactory db) => _db = db;

    public sealed class MasterRow
    {
        public Guid Id { get; set; }
        public string MasterCode { get; set; } = "";
        public string CharacterId { get; set; } = "CHAR-001";
        public string CharacterName { get; set; } = "MINH";
        public string EraId { get; set; } = "ERA-01";
        public Guid SourceCandidateId { get; set; }
        public string SourceCandidateCode { get; set; } = "";
        public string SourceVariation { get; set; } = "004-D";
        public string ArtifactPath { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public string VisionFingerprint { get; set; } = "";
        public string DnaVersion { get; set; } = "V1";
        public string IdentityTestResult { get; set; } = "";
        public string StressTestResult { get; set; } = "";
        public string MasterReviewResult { get; set; } = "PASS";
        public Guid? ReviewId { get; set; }
        public string LockedBy { get; set; } = "";
        public DateTimeOffset LockedAt { get; set; }
        public string LockReason { get; set; } = "";
        public string Version { get; set; } = "V1";
        public string Status { get; set; } = KitVideoMasterLockRules.LockedStatus;
        public Guid? CanonPointerId { get; set; }
    }

    public async Task<IReadOnlyList<MasterRow>> ListLatestAsync(string eraId, CancellationToken ct)
    {
        const string sql = """
            SELECT DISTINCT ON (m.character_id)
                   m.id AS Id, m.master_code AS MasterCode, m.character_id AS CharacterId,
                   m.character_name AS CharacterName, m.era_id AS EraId,
                   m.source_candidate_id AS SourceCandidateId, m.source_candidate_code AS SourceCandidateCode,
                   m.source_variation AS SourceVariation, m.artifact_path AS ArtifactPath, m.sha256 AS Sha256,
                   m.vision_fingerprint AS VisionFingerprint, m.dna_version AS DnaVersion,
                   m.identity_test_result AS IdentityTestResult, m.stress_test_result AS StressTestResult,
                   m.master_review_result AS MasterReviewResult, m.review_id AS ReviewId,
                   m.locked_by AS LockedBy, m.locked_at AS LockedAt, m.lock_reason AS LockReason,
                   m.version AS Version, m.status AS Status, p.id AS CanonPointerId
            FROM pack_content.video_master_reference m
            LEFT JOIN pack_content.video_master_canon_pointer p
              ON p.master_reference_id = m.id AND p.is_active
            WHERE m.era_id = @Era
            ORDER BY m.character_id, m.version DESC;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<MasterRow>(sql, new { Era = eraId })).ToList();
    }

    public async Task<MasterRow?> GetByVersionAsync(string characterId, string eraId, string version, CancellationToken ct)
    {
        const string sql = """
            SELECT m.id AS Id, m.master_code AS MasterCode, m.character_id AS CharacterId,
                   m.character_name AS CharacterName, m.era_id AS EraId,
                   m.source_candidate_id AS SourceCandidateId, m.source_candidate_code AS SourceCandidateCode,
                   m.source_variation AS SourceVariation, m.artifact_path AS ArtifactPath, m.sha256 AS Sha256,
                   m.vision_fingerprint AS VisionFingerprint, m.dna_version AS DnaVersion,
                   m.identity_test_result AS IdentityTestResult, m.stress_test_result AS StressTestResult,
                   m.master_review_result AS MasterReviewResult, m.review_id AS ReviewId,
                   m.locked_by AS LockedBy, m.locked_at AS LockedAt, m.lock_reason AS LockReason,
                   m.version AS Version, m.status AS Status, p.id AS CanonPointerId
            FROM pack_content.video_master_reference m
            LEFT JOIN pack_content.video_master_canon_pointer p
              ON p.master_reference_id = m.id AND p.is_active
            WHERE m.character_id = @Character AND m.era_id = @Era AND m.version = @Version
            LIMIT 1;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<MasterRow>(sql, new { Character = characterId, Era = eraId, Version = version });
    }

    public async Task<MasterRow?> GetActiveCanonAsync(string characterId, string eraId, CancellationToken ct)
    {
        const string sql = """
            SELECT m.id AS Id, m.master_code AS MasterCode, m.character_id AS CharacterId,
                   m.character_name AS CharacterName, m.era_id AS EraId,
                   m.source_candidate_id AS SourceCandidateId, m.source_candidate_code AS SourceCandidateCode,
                   m.source_variation AS SourceVariation, m.artifact_path AS ArtifactPath, m.sha256 AS Sha256,
                   m.vision_fingerprint AS VisionFingerprint, m.dna_version AS DnaVersion,
                   m.identity_test_result AS IdentityTestResult, m.stress_test_result AS StressTestResult,
                   m.master_review_result AS MasterReviewResult, m.review_id AS ReviewId,
                   m.locked_by AS LockedBy, m.locked_at AS LockedAt, m.lock_reason AS LockReason,
                   m.version AS Version, m.status AS Status, p.id AS CanonPointerId
            FROM pack_content.video_master_canon_pointer p
            JOIN pack_content.video_master_reference m ON m.id = p.master_reference_id
            WHERE p.character_id = @Character AND p.era_id = @Era AND p.is_active
            LIMIT 1;
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<MasterRow>(sql, new { Character = characterId, Era = eraId });
    }

    public sealed record LockWrite(
        Guid MasterId,
        Guid PointerId,
        Guid ReviewId,
        Guid CandidateId,
        Guid? ParentCandidateId,
        Guid? IdentityTestId,
        Guid? StressTestId,
        Guid AssetId,
        Guid VersionId,
        string CandidateCode,
        string ArtifactPath,
        string Sha256,
        string Fingerprint,
        string DnaVersion,
        string IdentityResult,
        string StressResult,
        string GateJson,
        string Actor,
        string Reason);

    public async Task<MasterRow> LockAtomicAsync(LockWrite w, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            var existing = await conn.QuerySingleOrDefaultAsync<MasterRow>("""
                SELECT m.id AS Id, m.master_code AS MasterCode, m.character_id AS CharacterId,
                       m.character_name AS CharacterName, m.era_id AS EraId,
                       m.source_candidate_id AS SourceCandidateId, m.source_candidate_code AS SourceCandidateCode,
                       m.source_variation AS SourceVariation, m.artifact_path AS ArtifactPath, m.sha256 AS Sha256,
                       m.vision_fingerprint AS VisionFingerprint, m.dna_version AS DnaVersion,
                       m.identity_test_result AS IdentityTestResult, m.stress_test_result AS StressTestResult,
                       m.master_review_result AS MasterReviewResult, m.review_id AS ReviewId,
                       m.locked_by AS LockedBy, m.locked_at AS LockedAt, m.lock_reason AS LockReason,
                       m.version AS Version, m.status AS Status, p.id AS CanonPointerId
                FROM pack_content.video_master_reference m
                LEFT JOIN pack_content.video_master_canon_pointer p
                  ON p.master_reference_id = m.id AND p.is_active
                WHERE m.character_id = 'CHAR-001' AND m.era_id = 'ERA-01' AND m.version = 'V1'
                FOR UPDATE OF m
                """, transaction: tx);
            if (existing is not null)
            {
                if (!KitVideoMasterLockRules.SameSource(existing.SourceCandidateCode, w.CandidateCode))
                    throw new InvalidOperationException("MASTER_LOCK_CONFLICT: Canon active với source khác. Không overwrite V1.");
                await tx.CommitAsync(ct);
                return existing;
            }

            await conn.ExecuteAsync("""
                INSERT INTO pack_content.video_master_reference (
                    id, master_code, project_code, character_id, character_code, character_name, era_id,
                    source_candidate_id, source_candidate_code, source_variation, artifact_id, artifact_path,
                    sha256, vision_fingerprint, dna_version, identity_test_result, stress_test_result,
                    master_review_result, review_id, locked_by, approved_by, lock_reason, version, status, gate_json,
                    parent_candidate_id, identity_test_id, stress_test_id
                ) VALUES (
                    @MasterId, @MasterCode, 'FAMIXA', 'CHAR-001', 'CHAR-001', 'MINH', 'ERA-01',
                    @CandidateId, @CandidateCode, '004-D', @CandidateId, @ArtifactPath,
                    @Sha256, @Fingerprint, @DnaVersion, @IdentityResult, @StressResult,
                    'PASS', @ReviewId, @Actor, @Actor, @Reason, 'V1', 'MASTER_REFERENCE_LOCKED', @GateJson::jsonb,
                    @ParentCandidateId, @IdentityTestId, @StressTestId
                );
                """, new
            {
                w.MasterId,
                MasterCode = KitVideoMasterLockRules.MasterCode,
                w.CandidateId,
                w.CandidateCode,
                w.ArtifactPath,
                w.Sha256,
                w.Fingerprint,
                w.DnaVersion,
                w.IdentityResult,
                w.StressResult,
                w.ReviewId,
                w.Actor,
                w.Reason,
                w.GateJson,
                w.ParentCandidateId,
                w.IdentityTestId,
                w.StressTestId,
            }, tx);

            await conn.ExecuteAsync("""
                INSERT INTO pack_content.video_master_canon_pointer (
                    id, character_id, era_id, master_reference_id, master_code, is_active
                ) VALUES (
                    @PointerId, 'CHAR-001', 'ERA-01', @MasterId, @MasterCode, TRUE
                );
                """, new { w.PointerId, w.MasterId, MasterCode = KitVideoMasterLockRules.MasterCode }, tx);

            var payload = JsonSerializer.Serialize(new
            {
                eventType = "MASTER_REFERENCE_LOCKED",
                character_id = "CHAR-001",
                candidate_id = w.CandidateId,
                master_id = w.MasterId,
                user = w.Actor,
                timestamp = DateTimeOffset.UtcNow,
                source_sha256 = w.Sha256,
                characterCode = "CHAR-001",
                eraCode = "ERA-01",
                masterReferenceId = w.MasterId,
                sourceCandidateId = w.CandidateId,
                sourceVariationId = "004-D",
                artifactId = w.CandidateId,
                sha256 = w.Sha256,
                director = w.Actor,
                masterCode = KitVideoMasterLockRules.MasterCode,
            });
            await conn.ExecuteAsync("""
                INSERT INTO pack_content.video_master_reference_event (id, master_reference_id, event_type, payload_json, actor)
                VALUES (@Id, @MasterId, 'MASTER_REFERENCE_LOCKED', @Payload::jsonb || @Gate::jsonb, @Actor);
                """, new
            {
                Id = Guid.NewGuid(),
                w.MasterId,
                Payload = payload,
                Gate = w.GateJson,
                w.Actor,
            }, tx);

            await conn.ExecuteAsync("""
                UPDATE pack_content.video_asset_candidate
                SET status = 'SELECTED_AS_MASTER',
                    extra_json = COALESCE(extra_json, '{}'::jsonb) || @Extra::jsonb
                WHERE id = @CandidateId
                  AND status NOT IN ('LOCKED', 'MASTER_REFERENCE_SOURCE', 'SELECTED_AS_MASTER', 'MASTER', 'REJECTED');
                """, new
            {
                w.CandidateId,
                Extra = JsonSerializer.Serialize(new
                {
                    masterReview = new { status = "LOCKED", autoSelected = false, selectedAsMaster = true },
                    selection = new { lifecycle = "MASTER_REFERENCE", eligible = true, selectedAsMaster = true },
                }),
            }, tx);

            await conn.ExecuteAsync("""
                UPDATE pack_content.video_master_review
                SET status = 'LOCKED', updated_at = NOW()
                WHERE id = @ReviewId AND status <> 'LOCKED';
                """, new { w.ReviewId }, tx);

            await conn.ExecuteAsync("""
                INSERT INTO pack_content.video_master_review_decision (
                    id, review_id, candidate_id, decision, reason, actor
                ) VALUES
                    (@IdPass, @ReviewId, @CandidateId, 'PASS', @Reason, @Actor),
                    (@IdLock, @ReviewId, @CandidateId, 'LOCK', @Reason, @Actor);
                """, new { IdPass = Guid.NewGuid(), IdLock = Guid.NewGuid(), w.ReviewId, w.CandidateId, w.Reason, w.Actor }, tx);

            await conn.ExecuteAsync("""
                INSERT INTO pack_content.video_master_review_event (id, review_id, event_type, payload_json, actor)
                VALUES (@Id1, @ReviewId, 'DIRECTOR_APPROVED', @Payload::jsonb, @Actor),
                       (@Id2, @ReviewId, 'MASTER_REFERENCE_CREATED', @Payload::jsonb, @Actor),
                       (@Id3, @ReviewId, 'MASTER_REFERENCE_LOCKED', @Payload::jsonb, @Actor),
                       (@Id4, @ReviewId, 'MASTER_LOCKED', @Payload::jsonb, @Actor);
                """, new
            {
                Id1 = Guid.NewGuid(),
                Id2 = Guid.NewGuid(),
                Id3 = Guid.NewGuid(),
                Id4 = Guid.NewGuid(),
                w.ReviewId,
                Payload = payload,
                w.Actor,
            }, tx);

            await conn.ExecuteAsync("""
                INSERT INTO pack_content.video_asset_reference (id, version_id, kind, path, is_primary, qa_status, qa_json)
                VALUES (@Id, @VersionId, 'MASTER_REFERENCE', @Path, TRUE, 'PASS', @Qa::jsonb)
                ON CONFLICT (version_id, kind) DO UPDATE
                SET path = EXCLUDED.path, qa_status = 'PASS', qa_json = EXCLUDED.qa_json
                WHERE pack_content.video_asset_reference.qa_json->>'locked' IS DISTINCT FROM 'true';
                """, new
            {
                Id = Guid.NewGuid(),
                w.VersionId,
                Path = w.ArtifactPath,
                Qa = $"{{\"sha256\":\"{w.Sha256}\",\"sourceCandidate\":\"{w.CandidateCode}\",\"locked\":true,\"copiedBytes\":false}}",
            }, tx);

            await conn.ExecuteAsync("""
                UPDATE pack_content.video_asset
                SET extra_json = jsonb_set(
                      COALESCE(extra_json, '{}'::jsonb),
                      '{masterReference}',
                      COALESCE(extra_json->'masterReference', '{}'::jsonb) || @Patch::jsonb,
                      true
                    ),
                    updated_at = NOW()
                WHERE id = @AssetId
                  AND COALESCE(extra_json->'masterReference'->>'status', '') <> 'LOCKED';
                """, new
            {
                w.AssetId,
                Patch = JsonSerializer.Serialize(new
                {
                    status = "LOCKED",
                    documentId = KitVideoMasterLockRules.DocumentId,
                    masterCode = KitVideoMasterLockRules.MasterCode,
                    masterReferenceId = w.MasterId,
                    sourceCandidate = w.CandidateCode,
                    canonPointerId = w.PointerId,
                }),
            }, tx);

            await conn.ExecuteAsync("""
                INSERT INTO pack_content.video_asset_event (
                    id, asset_id, version_id, candidate_id, event_type, actor, artifact_path, sha256, payload
                ) VALUES (
                    @Id, @AssetId, @VersionId, @CandidateId, 'MASTER_REFERENCE_LOCKED', @Actor, @Path, @Sha, @Payload::jsonb
                );
                """, new
            {
                Id = Guid.NewGuid(),
                w.AssetId,
                w.VersionId,
                w.CandidateId,
                w.Actor,
                Path = w.ArtifactPath,
                Sha = w.Sha256,
                Payload = payload,
            }, tx);

            await tx.CommitAsync(ct);
            return new MasterRow
            {
                Id = w.MasterId,
                MasterCode = KitVideoMasterLockRules.MasterCode,
                SourceCandidateId = w.CandidateId,
                SourceCandidateCode = w.CandidateCode,
                ArtifactPath = w.ArtifactPath,
                Sha256 = w.Sha256,
                VisionFingerprint = w.Fingerprint,
                DnaVersion = w.DnaVersion,
                IdentityTestResult = w.IdentityResult,
                StressTestResult = w.StressResult,
                ReviewId = w.ReviewId,
                LockedBy = w.Actor,
                LockedAt = DateTimeOffset.UtcNow,
                LockReason = w.Reason,
                CanonPointerId = w.PointerId,
            };
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            await tx.RollbackAsync(ct);
            return await GetByVersionAsync("CHAR-001", "ERA-01", "V1", ct)
                ?? throw new InvalidOperationException("MASTER_LOCK_CONFLICT: duplicate V1.");
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
