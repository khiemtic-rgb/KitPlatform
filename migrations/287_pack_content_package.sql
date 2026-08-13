-- KitPlatform 287: Content Package (Campaign / 1 idea → multi-variant pack)
-- Sprint 1 B1–B3. Manifest: deploy/ubuntu/migration-files.content.txt
-- Depends on: 282 (variant), 286 (marketing park tenant)

CREATE TABLE IF NOT EXISTS pack_content.content_package (
    id                      UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    brand_id                UUID NOT NULL REFERENCES pack_content.brand(id) ON DELETE CASCADE,
    topic_id                UUID NOT NULL REFERENCES pack_content.topic(id) ON DELETE CASCADE,
    title                   VARCHAR(500) NOT NULL,
    angle                   TEXT,
    audience                VARCHAR(200),
    content_type            VARCHAR(64) NOT NULL DEFAULT 'educational',
    pillar                  VARCHAR(120),
    goal                    VARCHAR(64) NOT NULL DEFAULT 'traffic',
    priority                VARCHAR(8) NOT NULL DEFAULT 'P1',
    status                  VARCHAR(32) NOT NULL DEFAULT 'Draft',
    source_package_id       UUID REFERENCES pack_content.content_package(id) ON DELETE SET NULL,
    extra_json              JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_content_package_topic UNIQUE (topic_id),
    CONSTRAINT ck_content_package_priority CHECK (priority IN ('P0', 'P1', 'P2')),
    CONSTRAINT ck_content_package_status CHECK (status IN (
        'Draft', 'Generating', 'Review', 'Approved', 'Scheduled',
        'Published', 'BudgetBlocked', 'Rejected'
    ))
);

CREATE INDEX IF NOT EXISTS ix_content_package_brand_status
    ON pack_content.content_package (brand_id, status, priority, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_content_package_source
    ON pack_content.content_package (source_package_id)
    WHERE source_package_id IS NOT NULL;

COMMENT ON TABLE pack_content.content_package IS
    'Content Package — one idea + brand; Generate All upserts topic variants; Adapt clones to another brand.';

-- Expand org variant catalog for TikTok script + caption/hashtag pack
UPDATE pack_content.org_settings
SET
    variant_kinds_json = (
        SELECT COALESCE(jsonb_agg(to_jsonb(k)), '[]'::jsonb)
        FROM (
            SELECT DISTINCT jsonb_array_elements_text(
                COALESCE(variant_kinds_json, '[]'::jsonb)
                || '["tiktok_script","social_caption"]'::jsonb
            ) AS k
        ) s
    ),
    updated_at = NOW();
