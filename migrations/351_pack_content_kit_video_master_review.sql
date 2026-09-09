-- KitPlatform 351: CHAR-001 Minh Master Review (after Identity + Stress, before SELECT/APPROVE/LOCK).
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Does not generate images. Does not touch Golden SH01-01 / Runway / Phase 01–06.
-- AI must not auto SELECT / APPROVE / LOCK.

CREATE TABLE IF NOT EXISTS pack_content.video_master_review (
    id                   UUID PRIMARY KEY,
    project_code         VARCHAR(32) NOT NULL DEFAULT 'FAMIXA',
    character_id         VARCHAR(32) NOT NULL DEFAULT 'CHAR-001',
    era_id               VARCHAR(16) NOT NULL DEFAULT 'ERA-01',
    candidate_id         UUID NOT NULL REFERENCES pack_content.video_asset_candidate (id) ON DELETE RESTRICT,
    candidate_code       VARCHAR(64) NOT NULL,
    identity_test_id     UUID,
    stress_test_id       UUID,
    source_sha256        VARCHAR(64) NOT NULL DEFAULT '',
    source_fingerprint   VARCHAR(64) NOT NULL DEFAULT '',
    status               VARCHAR(24) NOT NULL DEFAULT 'PENDING',
    document_id          VARCHAR(80) NOT NULL DEFAULT 'CHAR-001_MINH_MASTER_REVIEW_SPEC_V1',
    master_ref_code      VARCHAR(64) NOT NULL DEFAULT 'CHAR-001_MASTER_REFERENCE_V1',
    note                 TEXT NOT NULL DEFAULT '',
    extra_json           JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_video_master_review_candidate UNIQUE (candidate_id),
    CONSTRAINT ck_video_master_review_status CHECK (status IN (
        'PENDING', 'PASS', 'CONDITIONAL', 'REJECTED', 'SELECTED', 'APPROVED', 'LOCKED', 'BLOCKED'
    )),
    CONSTRAINT ck_video_master_review_scope CHECK (
        project_code = 'FAMIXA' AND character_id = 'CHAR-001' AND era_id = 'ERA-01'
    )
);

CREATE INDEX IF NOT EXISTS ix_video_master_review_candidate
    ON pack_content.video_master_review (candidate_id, created_at DESC);

CREATE TABLE IF NOT EXISTS pack_content.video_master_review_decision (
    id                   UUID PRIMARY KEY,
    review_id            UUID NOT NULL REFERENCES pack_content.video_master_review (id) ON DELETE RESTRICT,
    candidate_id         UUID NOT NULL,
    decision             VARCHAR(32) NOT NULL,
    reason               TEXT NOT NULL DEFAULT '',
    actor                VARCHAR(120),
    created_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_video_master_review_decision CHECK (decision IN (
        'PASS', 'CONDITIONAL', 'REJECT', 'SELECT', 'APPROVE', 'LOCK', 'MASTER_CHANGE_REQUEST'
    ))
);

CREATE INDEX IF NOT EXISTS ix_video_master_review_decision_review
    ON pack_content.video_master_review_decision (review_id, created_at DESC);

CREATE TABLE IF NOT EXISTS pack_content.video_master_review_event (
    id                   UUID PRIMARY KEY,
    review_id            UUID NOT NULL REFERENCES pack_content.video_master_review (id) ON DELETE RESTRICT,
    event_type           VARCHAR(40) NOT NULL,
    payload_json         JSONB NOT NULL DEFAULT '{}'::jsonb,
    actor                VARCHAR(120),
    created_at           TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_video_master_review_event_review
    ON pack_content.video_master_review_event (review_id, created_at DESC);

COMMENT ON TABLE pack_content.video_master_review IS
    'CHAR-001 Master Review V1. Pointer to 004-D after Identity 7/7 + Stress 10/10. No new pixels. No Golden.';

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.video_master_review TO %I', r);
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.video_master_review_decision TO %I', r);
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.video_master_review_event TO %I', r);
    END IF;
  END LOOP;
END $$;
