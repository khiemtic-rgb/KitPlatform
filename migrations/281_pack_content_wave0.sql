-- KitPlatform 281: Pack Content (KIT Content Park) — Wave 0
-- Independent content ops park. DO NOT merge into migration-files.prod.txt.
-- Manifest: deploy/ubuntu/migration-files.content.txt
-- Layer: Pack:Content

CREATE SCHEMA IF NOT EXISTS pack_content;

COMMENT ON SCHEMA pack_content IS
    'KIT Content Park — multi-brand content factory, budget ceilings, publish targets. Isolated from Pharmacy ERP / Family OS.';

-- Platform catalog only (opt-in). Controllers use ADMIN role for org-level ops.
INSERT INTO platform_module_registry (module_code, module_name, description, verticals, sort_order)
SELECT v.code, v.name, v.description, v.verticals, v.sort_order
FROM (
    VALUES
        (
            'kit_content',
            'KIT Content Park',
            'Multi-brand content management, budget ceilings, publish targets (Astro/WP/social)',
            ARRAY['pharmacy', 'hybrid', 'clinic', 'family'],
            90
        )
) AS v(code, name, description, verticals, sort_order)
WHERE NOT EXISTS (
    SELECT 1 FROM platform_module_registry m WHERE m.module_code = v.code
);

INSERT INTO permissions (permission_code, permission_name, module_name)
VALUES
    ('content.read', 'Content — xem', 'content'),
    ('content.write', 'Content — sửa', 'content'),
    ('content.publish', 'Content — xuất bản', 'content'),
    ('content.budget.manage', 'Content — trần ngân sách', 'content')
ON CONFLICT (permission_code) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code = 'ADMIN'
  AND p.permission_code LIKE 'content.%'
  AND NOT EXISTS (
      SELECT 1 FROM role_permissions rp WHERE rp.role_id = r.id AND rp.permission_id = p.id
  );

-- =============================================================================
-- Org settings (singleton) — all knobs dynamic / JSONB
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_content.org_settings (
    id                              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    monthly_ceiling_usd             NUMERIC(12, 2) NOT NULL DEFAULT 120,
    max_image_candidates_per_item   INT NOT NULL DEFAULT 3,
    regen_multiplier                NUMERIC(6, 3) NOT NULL DEFAULT 1.200,
    default_image_tier              VARCHAR(32) NOT NULL DEFAULT 'balanced',
    -- Dynamic catalogs (extend without ALTER)
    image_rate_usd_json             JSONB NOT NULL DEFAULT '{"lean":0.02,"balanced":0.05,"premium":0.14}'::jsonb,
    text_pack_estimate_usd          NUMERIC(12, 4) NOT NULL DEFAULT 0.0800,
    variant_kinds_json              JSONB NOT NULL DEFAULT '["web_long","fb_page","fb_short","linkedin","instagram","group_suggested","seo_meta"]'::jsonb,
    connector_types_json            JSONB NOT NULL DEFAULT '["astro_git","wordpress_rest","facebook_page","buffer","manual"]'::jsonb,
    channel_types_json              JSONB NOT NULL DEFAULT '["facebook_page","instagram","linkedin","threads","other"]'::jsonb,
    extra_json                      JSONB NOT NULL DEFAULT '{}'::jsonb,
    updated_at                      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by                      UUID,
    CONSTRAINT ck_content_org_image_tier CHECK (default_image_tier IN ('lean', 'balanced', 'premium')),
    CONSTRAINT ck_content_org_candidates CHECK (max_image_candidates_per_item BETWEEN 1 AND 10),
    CONSTRAINT ck_content_org_ceiling CHECK (monthly_ceiling_usd >= 0)
);

COMMENT ON TABLE pack_content.org_settings IS
    'Singleton org knobs for Content Park — budget ceiling, rates, dynamic enums.';

INSERT INTO pack_content.org_settings (id)
SELECT 'a0000000-0000-7000-8000-000000000001'::uuid
WHERE NOT EXISTS (SELECT 1 FROM pack_content.org_settings);

-- =============================================================================
-- Brand profile — add brands without schema change
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_content.brand (
    id                      UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    code                    VARCHAR(64) NOT NULL,
    name                    VARCHAR(200) NOT NULL,
    tone_json               JSONB NOT NULL DEFAULT '{}'::jsonb,
    visual_kit_json         JSONB NOT NULL DEFAULT '{}'::jsonb,
    default_cta_url         TEXT,
    default_cta_label       VARCHAR(200),
    monthly_ceiling_usd     NUMERIC(12, 2),
    image_tier              VARCHAR(32),
    pause_when_exceeded     BOOLEAN NOT NULL DEFAULT TRUE,
    is_active               BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order              INT NOT NULL DEFAULT 100,
    extra_json              JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_content_brand_code UNIQUE (code),
    CONSTRAINT ck_content_brand_tier CHECK (image_tier IS NULL OR image_tier IN ('lean', 'balanced', 'premium'))
);

CREATE INDEX IF NOT EXISTS ix_content_brand_active ON pack_content.brand (is_active, sort_order);

-- =============================================================================
-- Site targets (Astro / WordPress / …) — dynamic connector_type
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_content.site_target (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    brand_id            UUID NOT NULL REFERENCES pack_content.brand(id) ON DELETE CASCADE,
    code                VARCHAR(64) NOT NULL,
    name                VARCHAR(200) NOT NULL,
    connector_type      VARCHAR(64) NOT NULL,
    base_url            TEXT,
    config_json         JSONB NOT NULL DEFAULT '{}'::jsonb,
    secret_ref          VARCHAR(200),
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order          INT NOT NULL DEFAULT 100,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_content_site_brand_code UNIQUE (brand_id, code)
);

CREATE INDEX IF NOT EXISTS ix_content_site_brand ON pack_content.site_target (brand_id, is_active);

-- =============================================================================
-- Channel targets (FB Page / IG / …) — dynamic channel_type
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_content.channel_target (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    brand_id            UUID NOT NULL REFERENCES pack_content.brand(id) ON DELETE CASCADE,
    code                VARCHAR(64) NOT NULL,
    name                VARCHAR(200) NOT NULL,
    channel_type        VARCHAR(64) NOT NULL,
    external_id         VARCHAR(200),
    config_json         JSONB NOT NULL DEFAULT '{}'::jsonb,
    secret_ref          VARCHAR(200),
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order          INT NOT NULL DEFAULT 100,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_content_channel_brand_code UNIQUE (brand_id, code)
);

CREATE INDEX IF NOT EXISTS ix_content_channel_brand ON pack_content.channel_target (brand_id, is_active);

-- =============================================================================
-- Topics — editorial unit
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_content.topic (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    brand_id            UUID NOT NULL REFERENCES pack_content.brand(id) ON DELETE CASCADE,
    title               VARCHAR(500) NOT NULL,
    pillar              VARCHAR(120),
    goal                VARCHAR(64) NOT NULL DEFAULT 'traffic',
    cta_url             TEXT,
    utm_campaign        VARCHAR(200),
    priority            VARCHAR(8) NOT NULL DEFAULT 'P1',
    status              VARCHAR(32) NOT NULL DEFAULT 'Draft',
    body_outline        TEXT,
    extra_json          JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_content_topic_priority CHECK (priority IN ('P0', 'P1', 'P2')),
    CONSTRAINT ck_content_topic_status CHECK (status IN (
        'Draft', 'Generating', 'Review', 'Approved', 'Scheduled',
        'Published', 'BudgetBlocked', 'Rejected'
    ))
);

CREATE INDEX IF NOT EXISTS ix_content_topic_brand_status
    ON pack_content.topic (brand_id, status, priority, created_at DESC);

-- =============================================================================
-- Usage ledger — budget enforcement
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_content.usage_ledger (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    brand_id            UUID REFERENCES pack_content.brand(id) ON DELETE SET NULL,
    topic_id            UUID REFERENCES pack_content.topic(id) ON DELETE SET NULL,
    kind                VARCHAR(64) NOT NULL,
    image_tier          VARCHAR(32),
    quantity            INT NOT NULL DEFAULT 1,
    estimate_usd        NUMERIC(12, 4) NOT NULL DEFAULT 0,
    meta_json           JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_content_usage_month
    ON pack_content.usage_ledger (created_at DESC, brand_id);

COMMENT ON TABLE pack_content.usage_ledger IS
    'Estimated AI/image spend; used to enforce monthly ceilings without vendor invoices.';
