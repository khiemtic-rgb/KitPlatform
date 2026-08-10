-- KitPlatform 282: Content Park — variants, assets, publish jobs
-- Manifest: deploy/ubuntu/migration-files.content.txt only

CREATE TABLE IF NOT EXISTS pack_content.variant (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    topic_id        UUID NOT NULL REFERENCES pack_content.topic(id) ON DELETE CASCADE,
    kind            VARCHAR(64) NOT NULL,
    title           VARCHAR(500),
    body_markdown   TEXT NOT NULL DEFAULT '',
    meta_json       JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_content_variant_topic_kind UNIQUE (topic_id, kind)
);

CREATE INDEX IF NOT EXISTS ix_content_variant_topic ON pack_content.variant (topic_id);

CREATE TABLE IF NOT EXISTS pack_content.asset (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    topic_id        UUID NOT NULL REFERENCES pack_content.topic(id) ON DELETE CASCADE,
    kind            VARCHAR(32) NOT NULL DEFAULT 'image',
    file_name       VARCHAR(260) NOT NULL,
    content_type    VARCHAR(120) NOT NULL DEFAULT 'image/png',
    storage_path    TEXT NOT NULL,
    prompt          TEXT,
    model           VARCHAR(120),
    image_tier      VARCHAR(32),
    estimate_usd    NUMERIC(12, 4) NOT NULL DEFAULT 0,
    is_selected     BOOLEAN NOT NULL DEFAULT FALSE,
    meta_json       JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_content_asset_topic ON pack_content.asset (topic_id, is_selected);

CREATE TABLE IF NOT EXISTS pack_content.publish_job (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    topic_id            UUID NOT NULL REFERENCES pack_content.topic(id) ON DELETE CASCADE,
    brand_id            UUID NOT NULL REFERENCES pack_content.brand(id),
    target_kind         VARCHAR(32) NOT NULL,
    site_target_id      UUID REFERENCES pack_content.site_target(id) ON DELETE SET NULL,
    channel_target_id   UUID REFERENCES pack_content.channel_target(id) ON DELETE SET NULL,
    connector_type      VARCHAR(64) NOT NULL,
    status              VARCHAR(32) NOT NULL DEFAULT 'Queued',
    publish_at          TIMESTAMPTZ,
    external_ref        TEXT,
    last_error          TEXT,
    result_json         JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_content_publish_target CHECK (target_kind IN ('site', 'channel', 'manual')),
    CONSTRAINT ck_content_publish_status CHECK (status IN (
        'Queued', 'Running', 'Succeeded', 'Failed', 'Cancelled'
    ))
);

CREATE INDEX IF NOT EXISTS ix_content_publish_topic ON pack_content.publish_job (topic_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_content_publish_status ON pack_content.publish_job (status, publish_at);

CREATE TABLE IF NOT EXISTS pack_content.publish_log (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    job_id          UUID NOT NULL REFERENCES pack_content.publish_job(id) ON DELETE CASCADE,
    level           VARCHAR(16) NOT NULL DEFAULT 'info',
    message         TEXT NOT NULL,
    detail_json     JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_content_publish_log_job ON pack_content.publish_log (job_id, created_at);
