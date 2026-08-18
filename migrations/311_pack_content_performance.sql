-- KitPlatform 311: manual content performance ingest (views/clicks/UTM)
-- Manifest: deploy/ubuntu/migration-files.content.txt only
-- Do not reuse 298–310 (Local OS). Content park continues after 297.

CREATE TABLE IF NOT EXISTS pack_content.content_performance (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    package_id      UUID NOT NULL REFERENCES pack_content.content_package (id) ON DELETE CASCADE,
    topic_id        UUID NOT NULL REFERENCES pack_content.topic (id) ON DELETE CASCADE,
    brand_id        UUID NOT NULL REFERENCES pack_content.brand (id) ON DELETE CASCADE,
    channel         VARCHAR(64) NOT NULL,
    metric_date     DATE NOT NULL,
    impressions     INT,
    views           INT,
    clicks          INT,
    engagements     INT,
    comments        INT,
    shares          INT,
    utm_campaign    VARCHAR(200),
    utm_source      VARCHAR(120),
    utm_medium      VARCHAR(120),
    notes           TEXT,
    extra_json      JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_content_performance_channel CHECK (channel <> ''),
    CONSTRAINT ck_content_performance_nonneg CHECK (
        (impressions IS NULL OR impressions >= 0)
        AND (views IS NULL OR views >= 0)
        AND (clicks IS NULL OR clicks >= 0)
        AND (engagements IS NULL OR engagements >= 0)
        AND (comments IS NULL OR comments >= 0)
        AND (shares IS NULL OR shares >= 0)
    )
);

CREATE INDEX IF NOT EXISTS ix_content_performance_package_date
    ON pack_content.content_performance (package_id, metric_date DESC, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_content_performance_brand_date
    ON pack_content.content_performance (brand_id, metric_date DESC);

COMMENT ON TABLE pack_content.content_performance IS
    'Manual metrics per package/channel/day. No auto-pull from Meta/GA in this wave.';
