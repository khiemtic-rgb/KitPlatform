-- KitPlatform 339: KIT Video Engine Phase 05.1 — production integrity
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Do not call Runway. Artifact hash + image type + revalidate.

ALTER TABLE pack_content.video_keyframe_attempt
    ADD COLUMN IF NOT EXISTS artifact_sha256 VARCHAR(64) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS qa_artifact_sha256 VARCHAR(64) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS approved_artifact_sha256 VARCHAR(64) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS image_type VARCHAR(40) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS persist_status VARCHAR(32) NOT NULL DEFAULT 'OK';

ALTER TABLE pack_content.video_keyframe_attempt
    DROP CONSTRAINT IF EXISTS ck_video_keyframe_attempt_status;

ALTER TABLE pack_content.video_keyframe_attempt
    ADD CONSTRAINT ck_video_keyframe_attempt_status CHECK (status IN (
        'QUEUED', 'SUBMITTED', 'PROCESSING', 'ARTIFACT_READY', 'VISION_PROCESSING',
        'VISION_PASS', 'VISION_FAIL', 'REVIEW_REQUIRED', 'APPROVED', 'FAILED',
        'PERSISTENCE_FAILED', 'NOT_READY',
        'GENERATED', 'QA_PASS', 'QA_FAIL', 'REJECTED'
    ));

COMMENT ON COLUMN pack_content.video_keyframe_attempt.artifact_sha256 IS
    'SHA256 of durable JPEG. QA and Approve hashes must match this file.';
COMMENT ON COLUMN pack_content.video_keyframe_attempt.image_type IS
    'PRODUCTION_STILL | CHARACTER_SHEET | COLLAGE | MULTI_PANEL | REFERENCE_BOARD | TEXT_HEAVY_IMAGE | INVALID_COMPOSITION';
