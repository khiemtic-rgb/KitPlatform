-- KitPlatform 288: Video Factory mỏng (template + job từ Content Package)
-- Manifest: deploy/ubuntu/migration-files.content.txt
-- Depends on: 287 (content_package)

CREATE TABLE IF NOT EXISTS pack_content.video_template (
    id                      UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    code                    VARCHAR(64) NOT NULL,
    name                    VARCHAR(200) NOT NULL,
    provider                VARCHAR(32) NOT NULL DEFAULT 'storyboard_local',
    external_template_id    VARCHAR(200),
    aspect_ratio            VARCHAR(16) NOT NULL DEFAULT '9:16',
    duration_sec            INT NOT NULL DEFAULT 45,
    description             TEXT,
    config_json             JSONB NOT NULL DEFAULT '{}'::jsonb,
    is_active               BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order              INT NOT NULL DEFAULT 0,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_video_template_code UNIQUE (code),
    CONSTRAINT ck_video_template_provider CHECK (provider IN ('storyboard_local', 'creatomate')),
    CONSTRAINT ck_video_template_duration CHECK (duration_sec BETWEEN 5 AND 600)
);

CREATE TABLE IF NOT EXISTS pack_content.video_job (
    id                      UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    brand_id                UUID NOT NULL REFERENCES pack_content.brand(id) ON DELETE CASCADE,
    package_id              UUID REFERENCES pack_content.content_package(id) ON DELETE SET NULL,
    topic_id                UUID REFERENCES pack_content.topic(id) ON DELETE SET NULL,
    template_id             UUID NOT NULL REFERENCES pack_content.video_template(id),
    title                   VARCHAR(500) NOT NULL,
    script_body             TEXT NOT NULL DEFAULT '',
    status                  VARCHAR(32) NOT NULL DEFAULT 'Draft',
    provider                VARCHAR(32) NOT NULL DEFAULT 'storyboard_local',
    external_render_id      VARCHAR(200),
    preview_url             TEXT,
    output_url              TEXT,
    error_message           TEXT,
    storyboard_json         JSONB NOT NULL DEFAULT '[]'::jsonb,
    config_json             JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    rendered_at             TIMESTAMPTZ,
    CONSTRAINT ck_video_job_status CHECK (status IN (
        'Draft', 'Queued', 'Rendering', 'Ready', 'Failed', 'Approved'
    )),
    CONSTRAINT ck_video_job_provider CHECK (provider IN ('storyboard_local', 'creatomate'))
);

CREATE INDEX IF NOT EXISTS ix_video_job_brand_status
    ON pack_content.video_job (brand_id, status, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_video_job_package
    ON pack_content.video_job (package_id)
    WHERE package_id IS NOT NULL;

COMMENT ON TABLE pack_content.video_template IS
    'Video Factory — template storyboard local hoặc Creatomate (không auto-post TikTok).';
COMMENT ON TABLE pack_content.video_job IS
    'Video job từ Content Package (kéo tiktok_script) → storyboard / optional Creatomate render.';

-- Seed templates (idempotent by code)
INSERT INTO pack_content.video_template (
    code, name, provider, aspect_ratio, duration_sec, description, config_json, sort_order
)
SELECT v.code, v.name, v.provider, v.aspect_ratio, v.duration_sec, v.description, v.config_json::jsonb, v.sort_order
FROM (VALUES
    (
        'tiktok_45s_hooks',
        'TikTok / Reels 45s — Hook beats',
        'storyboard_local',
        '9:16',
        45,
        'Storyboard local: HOOK → PROBLEM → INSIGHT → SOLUTION → CTA. Xuất CapCut / Creatomate thủ công.',
        '{"beats":["HOOK","PROBLEM","INSIGHT","SOLUTION","CTA"],"platform":"tiktok"}',
        10
    ),
    (
        'reel_square_promo',
        'Feed / IG 30s — Promo',
        'storyboard_local',
        '1:1',
        30,
        'Góc ngắn promo sản phẩm/dịch vụ; ưu tiên social_caption + CTA.',
        '{"beats":["HOOK","OFFER","PROOF","CTA"],"platform":"instagram"}',
        20
    ),
    (
        'creatomate_brand_slot',
        'Creatomate — brand slot (khi có API key)',
        'creatomate',
        '9:16',
        45,
        'Cần Content:CreatomateApiKey + external_template_id. Không có key → chỉ chuẩn bị storyboard.',
        '{"modifications":{"Script":"{{script}}","Title":"{{title}}"}}',
        30
    )
) AS v(code, name, provider, aspect_ratio, duration_sec, description, config_json, sort_order)
WHERE NOT EXISTS (
    SELECT 1 FROM pack_content.video_template t WHERE t.code = v.code
);
