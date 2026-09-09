-- KitPlatform 353: CHAR-001 Minh Master Reference Approval (Director PASS = LOCK V1).
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Does not generate images. Does not touch Golden SH01-01 / Runway / Phase 01–06.

ALTER TABLE pack_content.video_asset_candidate
  DROP CONSTRAINT IF EXISTS ck_video_asset_candidate_status;
ALTER TABLE pack_content.video_asset_candidate
  ADD CONSTRAINT ck_video_asset_candidate_status CHECK (status IN (
    'DRAFT', 'CREATING', 'REVIEW', 'APPROVED', 'REJECTED', 'LOCKED',
    'MASTER_REFERENCE_SOURCE', 'SELECTED_AS_MASTER', 'MASTER'
  ));

ALTER TABLE pack_content.video_master_reference
  ADD COLUMN IF NOT EXISTS parent_candidate_id UUID;
ALTER TABLE pack_content.video_master_reference
  ADD COLUMN IF NOT EXISTS approved_by VARCHAR(120);
ALTER TABLE pack_content.video_master_reference
  ADD COLUMN IF NOT EXISTS identity_test_id UUID;
ALTER TABLE pack_content.video_master_reference
  ADD COLUMN IF NOT EXISTS stress_test_id UUID;

COMMENT ON TABLE pack_content.video_master_reference IS
    'CHAR-001 ERA-01 Master Reference V1. Director PASS creates and locks. Immutable. Not a production still.';
