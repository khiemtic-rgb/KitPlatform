-- KitPlatform 341: Phase 06 director + immutable provider task + credit states
-- Manifest: deploy/ubuntu/migration-files.content.txt only.

ALTER TABLE pack_content.video_runway_take
    DROP CONSTRAINT IF EXISTS ck_video_runway_take_status;

ALTER TABLE pack_content.video_runway_take
    ADD CONSTRAINT ck_video_runway_take_status CHECK (status IN (
        'READY', 'BLOCKED', 'SUBMITTING', 'SUBMITTED', 'RUNWAY_ACCEPTED', 'PROCESSING',
        'SUCCEEDED', 'DOWNLOAD', 'DOWNLOADING', 'VERIFY', 'ARTIFACT_VERIFY', 'VIDEO_QA',
        'VIDEO_READY', 'READY_FOR_DIRECTOR', 'VIDEO_QA_FAIL', 'APPROVED_TAKE',
        'REJECTED', 'INVALIDATED', 'FAILED', 'DIAGNOSE'
    ));

ALTER TABLE pack_content.video_runway_take
    DROP CONSTRAINT IF EXISTS ck_video_runway_take_credit;

ALTER TABLE pack_content.video_runway_take
    ADD CONSTRAINT ck_video_runway_take_credit CHECK (credit_state IN (
        'NONE', 'PENDING', 'ESTIMATED', 'ACTUAL', 'CHARGED', 'REFUND_PENDING', 'REFUNDED', 'UNKNOWN'
    ));

ALTER TABLE pack_content.video_runway_take
    ADD COLUMN IF NOT EXISTS estimated_credit VARCHAR(24) NOT NULL DEFAULT 'UNKNOWN',
    ADD COLUMN IF NOT EXISTS actual_credit VARCHAR(24) NOT NULL DEFAULT 'UNKNOWN',
    ADD COLUMN IF NOT EXISTS currency VARCHAR(8) NOT NULL DEFAULT 'USD';

CREATE TABLE IF NOT EXISTS pack_content.video_runway_provider_task (
    id                    UUID PRIMARY KEY,
    take_id               UUID NOT NULL REFERENCES pack_content.video_runway_take (id) ON DELETE RESTRICT,
    provider              VARCHAR(32) NOT NULL DEFAULT 'RUNWAY',
    provider_task_id      VARCHAR(160) NOT NULL,
    request_fingerprint   VARCHAR(64) NOT NULL DEFAULT '',
    source_artifact_hash  VARCHAR(64) NOT NULL DEFAULT '',
    motion_contract_ver   VARCHAR(40) NOT NULL DEFAULT 'KIT-VIDEO-MOTION-V1',
    submitted_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at          TIMESTAMPTZ,
    provider_status       VARCHAR(48) NOT NULL,
    failure_code          VARCHAR(80) NOT NULL DEFAULT '',
    failure_message       TEXT NOT NULL DEFAULT '',
    output_url            TEXT
);

CREATE INDEX IF NOT EXISTS ix_video_runway_provider_task_take
    ON pack_content.video_runway_provider_task (take_id, submitted_at);

COMMENT ON TABLE pack_content.video_runway_provider_task IS
    'Immutable Runway snapshot. Never overwrite. New poll row = new insert.';
