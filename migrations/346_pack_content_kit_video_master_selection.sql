-- KitPlatform 346: Master Reference selection (compare / Director / audit).
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Reuses video_asset / video_asset_candidate / video_asset_reference.
-- Does not generate images. Does not LOCK CHAR-001. Does not touch Golden.

ALTER TABLE pack_content.video_asset_candidate
    DROP CONSTRAINT IF EXISTS ck_video_asset_candidate_image;

ALTER TABLE pack_content.video_asset_candidate
    ADD CONSTRAINT ck_video_asset_candidate_image CHECK (image_type IN (
        'UNKNOWN', 'PRODUCTION_STILL', 'CHARACTER_CANDIDATE',
        'CHARACTER_SHEET', 'COLLAGE', 'MULTI_PANEL', 'REFERENCE_BOARD'
    ));

CREATE TABLE IF NOT EXISTS pack_content.video_asset_event (
    id                   UUID PRIMARY KEY,
    asset_id             UUID NOT NULL REFERENCES pack_content.video_asset (id) ON DELETE RESTRICT,
    version_id           UUID NOT NULL REFERENCES pack_content.video_asset_version (id) ON DELETE RESTRICT,
    candidate_id         UUID,
    event_type           VARCHAR(40) NOT NULL,
    actor                VARCHAR(120),
    artifact_path        TEXT NOT NULL DEFAULT '',
    sha256               VARCHAR(64) NOT NULL DEFAULT '',
    visual_dna_version   VARCHAR(16) NOT NULL DEFAULT 'V1',
    payload              JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_video_asset_event_asset
    ON pack_content.video_asset_event (asset_id, created_at DESC);

COMMENT ON TABLE pack_content.video_asset_event IS
    'Master Reference selection audit. AI may recommend; only Director SELECT becomes Canon.';

UPDATE pack_content.video_asset
SET extra_json = jsonb_set(
      COALESCE(extra_json, '{}'::jsonb),
      '{masterReference,selection}',
      '{"documentId":"CHAR-001_MINH_MASTER_REFERENCE_SELECTION_V1","autoSelect":false,"status":"DRAFT"}'::jsonb,
      true
    ),
    updated_at = NOW()
WHERE asset_code = 'CHAR-001'
  AND COALESCE(extra_json->'masterReference'->>'status', '') NOT IN ('LOCKED');

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.video_asset_event TO %I', r);
    END IF;
  END LOOP;
END $$;
