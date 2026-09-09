-- KitPlatform 347: Master Reference selection refinement (gate / FRONT_RUNNER / variation family).
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Does not LOCK CHAR-001. Does not touch Golden / Phase 01–06 / Runway.

ALTER TABLE pack_content.video_asset_candidate
    ADD COLUMN IF NOT EXISTS parent_candidate_id UUID REFERENCES pack_content.video_asset_candidate (id) ON DELETE RESTRICT;

CREATE INDEX IF NOT EXISTS ix_video_asset_candidate_parent
    ON pack_content.video_asset_candidate (parent_candidate_id)
    WHERE parent_candidate_id IS NOT NULL;

COMMENT ON COLUMN pack_content.video_asset_candidate.parent_candidate_id IS
    'Variation family parent. Child does not overwrite parent. AI must not auto-SELECT / APPROVE / LOCK a variation.';

UPDATE pack_content.video_asset
SET extra_json = jsonb_set(
      COALESCE(extra_json, '{}'::jsonb),
      '{masterReference,selection,refinementId}',
      '"CHAR-001_MASTER_REFERENCE_SELECTION_REFINEMENT_V1"'::jsonb,
      true
    ),
    updated_at = NOW()
WHERE asset_code = 'CHAR-001'
  AND COALESCE(extra_json->'masterReference'->>'status', '') NOT IN ('LOCKED');
