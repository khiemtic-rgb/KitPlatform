-- KitPlatform 345: Master Reference creation (fingerprint + generateEnabled).
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Does not LOCK CHAR-001. Does not touch Golden / Phase 01–06 / Runway.

ALTER TABLE pack_content.video_asset_candidate
    ADD COLUMN IF NOT EXISTS fingerprint VARCHAR(64) NOT NULL DEFAULT '';

CREATE INDEX IF NOT EXISTS ix_video_asset_candidate_fingerprint
    ON pack_content.video_asset_candidate (version_id, fingerprint);

UPDATE pack_content.video_asset
SET extra_json = jsonb_set(
      COALESCE(extra_json, '{}'::jsonb),
      '{masterReference,generateEnabled}',
      'true'::jsonb,
      true
    ),
    updated_at = NOW()
WHERE asset_code = 'CHAR-001'
  AND COALESCE(extra_json->'masterReference'->>'status', '') NOT IN ('LOCKED');
