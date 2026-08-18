-- KitPlatform 291: Content Park — background work queue (generate / publish / video)
-- Manifest: deploy/ubuntu/migration-files.content.txt only
-- Local: apply against kitplatform; do not merge into Pharmacy prod manifest.

CREATE TABLE IF NOT EXISTS pack_content.work_job (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    kind                VARCHAR(32) NOT NULL,
    status              VARCHAR(32) NOT NULL DEFAULT 'Queued',
    brand_id            UUID REFERENCES pack_content.brand(id) ON DELETE SET NULL,
    topic_id            UUID REFERENCES pack_content.topic(id) ON DELETE SET NULL,
    package_id          UUID REFERENCES pack_content.content_package(id) ON DELETE SET NULL,
    video_job_id        UUID REFERENCES pack_content.video_job(id) ON DELETE SET NULL,
    title               VARCHAR(500),
    payload_json        JSONB NOT NULL DEFAULT '{}'::jsonb,
    result_json         JSONB NOT NULL DEFAULT '{}'::jsonb,
    error_message       TEXT,
    retry_count         INT NOT NULL DEFAULT 0,
    max_retries         INT NOT NULL DEFAULT 3,
    available_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at          TIMESTAMPTZ,
    completed_at        TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_content_work_kind CHECK (kind IN (
        'generate_topic', 'generate_package', 'publish_topic', 'video_mvp', 'video_render'
    )),
    CONSTRAINT ck_content_work_status CHECK (status IN (
        'Queued', 'Running', 'Succeeded', 'Failed', 'Cancelled'
    )),
    CONSTRAINT ck_content_work_retries CHECK (retry_count >= 0 AND max_retries BETWEEN 0 AND 10)
);

CREATE INDEX IF NOT EXISTS ix_content_work_due
    ON pack_content.work_job (status, available_at, created_at)
    WHERE status = 'Queued';

CREATE INDEX IF NOT EXISTS ix_content_work_active
    ON pack_content.work_job (status, created_at DESC)
    WHERE status IN ('Queued', 'Running');

CREATE INDEX IF NOT EXISTS ix_content_work_topic
    ON pack_content.work_job (topic_id, created_at DESC)
    WHERE topic_id IS NOT NULL;

COMMENT ON TABLE pack_content.work_job IS
    'Content Factory queue — HTTP enqueues; ContentWorkWorker claims with SKIP LOCKED.';
