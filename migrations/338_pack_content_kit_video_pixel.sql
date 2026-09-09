-- KitPlatform 338: KIT Video Engine Phase 05 — real image artifact + pixel vision
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Keyframe only. Do not call Runway / LipSync / Final.

ALTER TABLE pack_content.video_keyframe_attempt
    ADD COLUMN IF NOT EXISTS job_state VARCHAR(32) NOT NULL DEFAULT 'QUEUED',
    ADD COLUMN IF NOT EXISTS provider VARCHAR(32) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS model VARCHAR(80) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS generation_id VARCHAR(80) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS idempotency_key VARCHAR(160),
    ADD COLUMN IF NOT EXISTS estimated_cost NUMERIC(12,4),
    ADD COLUMN IF NOT EXISTS actual_cost NUMERIC(12,4),
    ADD COLUMN IF NOT EXISTS cost_unknown BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS vision_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    ADD COLUMN IF NOT EXISTS artifact_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    ADD COLUMN IF NOT EXISTS failure_class VARCHAR(40) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb;

CREATE UNIQUE INDEX IF NOT EXISTS uq_video_keyframe_attempt_idem
    ON pack_content.video_keyframe_attempt (idempotency_key)
    WHERE idempotency_key IS NOT NULL AND length(idempotency_key) > 0;

ALTER TABLE pack_content.video_keyframe_attempt
    DROP CONSTRAINT IF EXISTS ck_video_keyframe_attempt_status;

ALTER TABLE pack_content.video_keyframe_attempt
    ADD CONSTRAINT ck_video_keyframe_attempt_status CHECK (status IN (
        'QUEUED', 'SUBMITTED', 'PROCESSING', 'ARTIFACT_READY', 'VISION_PROCESSING',
        'VISION_PASS', 'VISION_FAIL', 'REVIEW_REQUIRED', 'APPROVED', 'FAILED',
        'GENERATED', 'QA_PASS', 'QA_FAIL', 'REJECTED'
    ));

COMMENT ON COLUMN pack_content.video_keyframe_attempt.image_path IS
    'Durable local artifact. Do not keep provider-only expiring URLs.';
