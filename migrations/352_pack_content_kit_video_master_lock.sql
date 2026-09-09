-- KitPlatform 352: CHAR-001 Minh Master Reference Lock (after Director PASS).
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Does not generate images. Does not touch Golden SH01-01 / Runway / Phase 01–06.
-- AI must not auto LOCK. One active Canon per Character + Era.

ALTER TABLE pack_content.video_asset_candidate
  DROP CONSTRAINT IF EXISTS ck_video_asset_candidate_status;
ALTER TABLE pack_content.video_asset_candidate
  ADD CONSTRAINT ck_video_asset_candidate_status CHECK (status IN (
    'DRAFT', 'CREATING', 'REVIEW', 'APPROVED', 'REJECTED', 'LOCKED', 'MASTER_REFERENCE_SOURCE'
  ));

CREATE TABLE IF NOT EXISTS pack_content.video_master_reference (
    id                      UUID PRIMARY KEY,
    master_code             VARCHAR(80) NOT NULL,
    project_code            VARCHAR(32) NOT NULL DEFAULT 'FAMIXA',
    character_id            VARCHAR(32) NOT NULL DEFAULT 'CHAR-001',
    character_code          VARCHAR(32) NOT NULL DEFAULT 'CHAR-001',
    character_name          VARCHAR(32) NOT NULL DEFAULT 'MINH',
    era_id                  VARCHAR(16) NOT NULL DEFAULT 'ERA-01',
    source_candidate_id     UUID NOT NULL REFERENCES pack_content.video_asset_candidate (id) ON DELETE RESTRICT,
    source_candidate_code   VARCHAR(64) NOT NULL,
    source_variation        VARCHAR(16) NOT NULL DEFAULT '004-D',
    artifact_id             UUID NOT NULL,
    artifact_path           TEXT NOT NULL DEFAULT '',
    sha256                  VARCHAR(64) NOT NULL,
    vision_fingerprint      VARCHAR(64) NOT NULL DEFAULT '',
    dna_version             VARCHAR(32) NOT NULL DEFAULT 'V1',
    identity_test_result    VARCHAR(32) NOT NULL DEFAULT '',
    stress_test_result      VARCHAR(32) NOT NULL DEFAULT '',
    master_review_result    VARCHAR(32) NOT NULL DEFAULT 'PASS',
    review_id               UUID REFERENCES pack_content.video_master_review (id) ON DELETE RESTRICT,
    locked_by               VARCHAR(120) NOT NULL,
    locked_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    lock_reason             TEXT NOT NULL DEFAULT '',
    version                 VARCHAR(16) NOT NULL DEFAULT 'V1',
    status                  VARCHAR(32) NOT NULL DEFAULT 'MASTER_REFERENCE_LOCKED',
    gate_json               JSONB NOT NULL DEFAULT '{}'::jsonb,
    extra_json              JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_video_master_reference_code UNIQUE (master_code),
    CONSTRAINT uq_video_master_reference_version UNIQUE (character_id, era_id, version),
    CONSTRAINT ck_video_master_reference_status CHECK (status = 'MASTER_REFERENCE_LOCKED'),
    CONSTRAINT ck_video_master_reference_scope CHECK (
        project_code = 'FAMIXA' AND character_id = 'CHAR-001' AND era_id = 'ERA-01'
    )
);

CREATE TABLE IF NOT EXISTS pack_content.video_master_canon_pointer (
    id                      UUID PRIMARY KEY,
    character_id            VARCHAR(32) NOT NULL,
    era_id                  VARCHAR(16) NOT NULL,
    master_reference_id     UUID NOT NULL REFERENCES pack_content.video_master_reference (id) ON DELETE RESTRICT,
    master_code             VARCHAR(80) NOT NULL,
    is_active               BOOLEAN NOT NULL DEFAULT TRUE,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_video_master_canon_active
    ON pack_content.video_master_canon_pointer (character_id, era_id)
    WHERE is_active;

CREATE TABLE IF NOT EXISTS pack_content.video_master_reference_event (
    id                      UUID PRIMARY KEY,
    master_reference_id     UUID NOT NULL REFERENCES pack_content.video_master_reference (id) ON DELETE RESTRICT,
    event_type              VARCHAR(40) NOT NULL,
    payload_json            JSONB NOT NULL DEFAULT '{}'::jsonb,
    actor                   VARCHAR(120),
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_video_master_reference_event_master
    ON pack_content.video_master_reference_event (master_reference_id, created_at DESC);

CREATE OR REPLACE FUNCTION pack_content.fn_video_master_reference_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN
    RAISE EXCEPTION 'MASTER_LOCKED: cannot delete MASTER_REFERENCE_LOCKED';
  END IF;
  RAISE EXCEPTION 'MASTER_LOCKED: cannot update MASTER_REFERENCE_LOCKED';
END;
$$;

DROP TRIGGER IF EXISTS trg_video_master_reference_immutable ON pack_content.video_master_reference;
CREATE TRIGGER trg_video_master_reference_immutable
  BEFORE UPDATE OR DELETE ON pack_content.video_master_reference
  FOR EACH ROW EXECUTE FUNCTION pack_content.fn_video_master_reference_immutable();

CREATE OR REPLACE FUNCTION pack_content.fn_video_master_reference_event_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'MASTER_LOCKED: audit event is immutable';
END;
$$;

DROP TRIGGER IF EXISTS trg_video_master_reference_event_immutable ON pack_content.video_master_reference_event;
CREATE TRIGGER trg_video_master_reference_event_immutable
  BEFORE UPDATE OR DELETE ON pack_content.video_master_reference_event
  FOR EACH ROW EXECUTE FUNCTION pack_content.fn_video_master_reference_event_immutable();

COMMENT ON TABLE pack_content.video_master_reference IS
    'CHAR-001 ERA-01 Master Reference V1. Immutable identity anchor. Not a production still. No new pixels.';

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.video_master_reference TO %I', r);
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.video_master_canon_pointer TO %I', r);
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.video_master_reference_event TO %I', r);
    END IF;
  END LOOP;
END $$;
