-- KitPlatform 344: generic Master Reference candidates on video_asset.
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Reuses video_asset / version / reference. Not famixa_master_reference.
-- Does not generate images. Does not LOCK CHAR-001. Does not touch Golden / Phase 01–06.

CREATE TABLE IF NOT EXISTS pack_content.video_asset_candidate (
    id                     UUID PRIMARY KEY,
    version_id             UUID NOT NULL REFERENCES pack_content.video_asset_version (id) ON DELETE RESTRICT,
    candidate_code         VARCHAR(64) NOT NULL,
    role                   VARCHAR(32) NOT NULL,
    generation_attempt     INT NOT NULL DEFAULT 1,
    artifact_path          TEXT NOT NULL DEFAULT '',
    sha256                 VARCHAR(64) NOT NULL DEFAULT '',
    visual_style_version   VARCHAR(32) NOT NULL DEFAULT 'V1',
    character_id           VARCHAR(32) NOT NULL DEFAULT '',
    era_id                 VARCHAR(16) NOT NULL DEFAULT '',
    image_type             VARCHAR(32) NOT NULL DEFAULT 'UNKNOWN',
    status                 VARCHAR(24) NOT NULL DEFAULT 'DRAFT',
    qa_status              VARCHAR(16) NOT NULL DEFAULT 'PENDING',
    qa_json                JSONB NOT NULL DEFAULT '{}'::jsonb,
    extra_json             JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at             TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_video_asset_candidate UNIQUE (version_id, candidate_code),
    CONSTRAINT ck_video_asset_candidate_role CHECK (role ~ '^[A-Z0-9_]{2,32}$'),
    CONSTRAINT ck_video_asset_candidate_status CHECK (status IN (
        'DRAFT', 'CREATING', 'REVIEW', 'APPROVED', 'REJECTED', 'LOCKED'
    )),
    CONSTRAINT ck_video_asset_candidate_qa CHECK (qa_status IN (
        'PENDING', 'PASS', 'FAIL', 'BLOCK', 'DIAGNOSE'
    )),
    CONSTRAINT ck_video_asset_candidate_image CHECK (image_type IN (
        'UNKNOWN', 'PRODUCTION_STILL', 'CHARACTER_SHEET', 'COLLAGE', 'MULTI_PANEL', 'REFERENCE_BOARD'
    ))
);

CREATE INDEX IF NOT EXISTS ix_video_asset_candidate_version
    ON pack_content.video_asset_candidate (version_id, character_id, era_id);

COMMENT ON TABLE pack_content.video_asset_candidate IS
    'Append-only Master Reference candidates. Never overwrite a candidate_code. Generic — Famixa CHAR-001 is one consumer.';

UPDATE pack_content.video_asset
SET extra_json = extra_json || $json${
  "masterReference": {
    "documentId": "CHAR-001_MINH_MASTER_REFERENCE_SPEC_V1",
    "status": "DRAFT",
    "characterId": "CHAR-001",
    "era": "ERA-01",
    "age": 11,
    "version": "V1",
    "legacySheetsAreNotMaster": true,
    "generateEnabled": false
  }
}$json$::jsonb,
    updated_at = NOW()
WHERE asset_code = 'CHAR-001'
  AND lifecycle <> 'LOCKED'
  AND COALESCE(extra_json->'masterReference'->>'status', '') NOT IN ('APPROVED', 'LOCKED');

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.video_asset_candidate TO %I', r);
    END IF;
  END LOOP;
END $$;
