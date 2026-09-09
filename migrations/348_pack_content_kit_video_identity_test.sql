-- KitPlatform 348: CHAR-001 Identity Test (casting validation before Master).
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Does not LOCK CHAR-001. Does not create Canon. Does not touch Golden / Runway / Phase 01–06.

CREATE TABLE IF NOT EXISTS pack_content.video_identity_test (
    id                   UUID PRIMARY KEY,
    project_code         VARCHAR(32) NOT NULL DEFAULT 'FAMIXA',
    character_id         VARCHAR(32) NOT NULL DEFAULT 'CHAR-001',
    era_id               VARCHAR(16) NOT NULL DEFAULT 'ERA-01',
    candidate_id         UUID NOT NULL REFERENCES pack_content.video_asset_candidate (id) ON DELETE RESTRICT,
    candidate_code       VARCHAR(64) NOT NULL,
    source_sha256        VARCHAR(64) NOT NULL DEFAULT '',
    source_fingerprint   VARCHAR(64) NOT NULL DEFAULT '',
    dna_version          VARCHAR(16) NOT NULL DEFAULT 'V1',
    status               VARCHAR(24) NOT NULL DEFAULT 'PENDING',
    document_id          VARCHAR(80) NOT NULL DEFAULT 'CHAR-001_MINH_IDENTITY_TEST_SPEC_V1',
    extra_json           JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_video_identity_test_status CHECK (status IN (
        'PENDING', 'RUNNING', 'PASS', 'CONDITIONAL', 'FAIL', 'BLOCKED', 'INCOMPLETE'
    )),
    CONSTRAINT ck_video_identity_test_scope CHECK (
        project_code = 'FAMIXA' AND character_id = 'CHAR-001' AND era_id = 'ERA-01'
    )
);

CREATE INDEX IF NOT EXISTS ix_video_identity_test_candidate
    ON pack_content.video_identity_test (candidate_id, created_at DESC);

CREATE TABLE IF NOT EXISTS pack_content.video_identity_test_artifact (
    id                   UUID PRIMARY KEY,
    test_id              UUID NOT NULL REFERENCES pack_content.video_identity_test (id) ON DELETE RESTRICT,
    test_type            VARCHAR(16) NOT NULL,
    test_variant         VARCHAR(32) NOT NULL,
    attempt              INT NOT NULL DEFAULT 1,
    artifact_path        TEXT NOT NULL DEFAULT '',
    sha256               VARCHAR(64) NOT NULL DEFAULT '',
    fingerprint          VARCHAR(64) NOT NULL DEFAULT '',
    image_type           VARCHAR(32) NOT NULL DEFAULT 'IDENTITY_TEST',
    qa_status            VARCHAR(16) NOT NULL DEFAULT 'PENDING',
    qa_json              JSONB NOT NULL DEFAULT '{}'::jsonb,
    provider             VARCHAR(64) NOT NULL DEFAULT '',
    model                VARCHAR(80) NOT NULL DEFAULT '',
    extra_json           JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_video_identity_test_artifact UNIQUE (test_id, test_variant, attempt),
    CONSTRAINT ck_video_identity_test_type CHECK (test_type IN ('VIEW', 'EMOTION')),
    CONSTRAINT ck_video_identity_test_image CHECK (image_type IN (
        'IDENTITY_TEST', 'UNKNOWN', 'CHARACTER_SHEET', 'COLLAGE', 'MULTI_PANEL'
    )),
    CONSTRAINT ck_video_identity_test_art_qa CHECK (qa_status IN (
        'PENDING', 'PASS', 'FAIL', 'BLOCK', 'GENERATION_FAILED', 'VISION_FAIL', 'ARTIFACT_FAILED'
    ))
);

CREATE INDEX IF NOT EXISTS ix_video_identity_test_artifact_test
    ON pack_content.video_identity_test_artifact (test_id, test_variant, attempt);

CREATE TABLE IF NOT EXISTS pack_content.video_identity_test_result (
    id                   UUID PRIMARY KEY,
    test_id              UUID NOT NULL REFERENCES pack_content.video_identity_test (id) ON DELETE RESTRICT,
    view_stability       VARCHAR(16) NOT NULL DEFAULT 'PENDING',
    emotion_stability    VARCHAR(16) NOT NULL DEFAULT 'PENDING',
    identity_stability   VARCHAR(16) NOT NULL DEFAULT 'PENDING',
    score                INT NOT NULL DEFAULT 0,
    p0                   JSONB NOT NULL DEFAULT '[]'::jsonb,
    diagnosis            JSONB NOT NULL DEFAULT '[]'::jsonb,
    extra_json           JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_video_identity_test_result UNIQUE (test_id)
);

CREATE TABLE IF NOT EXISTS pack_content.video_identity_test_decision (
    id                   UUID PRIMARY KEY,
    test_id              UUID NOT NULL REFERENCES pack_content.video_identity_test (id) ON DELETE RESTRICT,
    candidate_id         UUID NOT NULL,
    decision             VARCHAR(32) NOT NULL,
    reason               TEXT NOT NULL DEFAULT '',
    actor                VARCHAR(120),
    extra_json           JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_video_identity_test_decision CHECK (decision IN (
        'PASS', 'CONDITIONAL', 'FAIL', 'REJECT', 'PROMOTE_TO_MASTER_REVIEW'
    ))
);

CREATE INDEX IF NOT EXISTS ix_video_identity_test_decision_test
    ON pack_content.video_identity_test_decision (test_id, created_at DESC);

COMMENT ON TABLE pack_content.video_identity_test IS
    'CHAR-001 Identity Test. Casting only. Does not create Canon / Master / LOCK. No Runway.';

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.video_identity_test TO %I', r);
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.video_identity_test_artifact TO %I', r);
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.video_identity_test_result TO %I', r);
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.video_identity_test_decision TO %I', r);
    END IF;
  END LOOP;
END $$;
