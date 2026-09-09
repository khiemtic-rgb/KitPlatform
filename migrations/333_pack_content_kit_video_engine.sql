-- KitPlatform 333: KIT Video Engine V1 — Checkpoint 1 (Project / Universe / Production / Shot state)
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Engine ≠ Project. Famixa is the first consumer. Do not overwrite series_pilot / series_build / character Canon.

CREATE TABLE IF NOT EXISTS pack_content.video_project (
    id            UUID PRIMARY KEY,
    project_code  VARCHAR(32) NOT NULL,
    name          VARCHAR(120) NOT NULL,
    brand_code    VARCHAR(64) NOT NULL DEFAULT '',
    status        VARCHAR(24) NOT NULL DEFAULT 'draft',
    visual_json   JSONB NOT NULL DEFAULT '{}'::jsonb,
    rules_json    JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_video_project_code UNIQUE (project_code),
    CONSTRAINT ck_video_project_code CHECK (project_code ~ '^[A-Z][A-Z0-9_]{1,30}$'),
    CONSTRAINT ck_video_project_status CHECK (status IN ('draft', 'active', 'archived'))
);

CREATE TABLE IF NOT EXISTS pack_content.video_universe (
    id             UUID PRIMARY KEY,
    project_id     UUID NOT NULL REFERENCES pack_content.video_project (id) ON DELETE RESTRICT,
    universe_code  VARCHAR(48) NOT NULL,
    name           VARCHAR(120) NOT NULL,
    status         VARCHAR(24) NOT NULL DEFAULT 'draft',
    world_json     JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_video_universe UNIQUE (project_id, universe_code),
    CONSTRAINT ck_video_universe_code CHECK (universe_code ~ '^[A-Z][A-Z0-9_]{1,46}$'),
    CONSTRAINT ck_video_universe_status CHECK (status IN ('draft', 'active', 'archived'))
);

CREATE TABLE IF NOT EXISTS pack_content.video_production (
    id               UUID PRIMARY KEY,
    project_id       UUID NOT NULL REFERENCES pack_content.video_project (id) ON DELETE RESTRICT,
    universe_id      UUID NOT NULL REFERENCES pack_content.video_universe (id) ON DELETE RESTRICT,
    production_code  VARCHAR(64) NOT NULL,
    title            VARCHAR(240) NOT NULL DEFAULT '',
    state            VARCHAR(32) NOT NULL DEFAULT 'DRAFT',
    series_build_id  UUID,
    extra_json       JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_video_production UNIQUE (project_id, production_code),
    CONSTRAINT ck_video_production_state CHECK (state IN (
        'DRAFT', 'SCRIPT_APPROVED', 'SHOT_PLANNED', 'SCENE_MASTER_READY',
        'KEYFRAME_GENERATION', 'KEYFRAME_REVIEW', 'KEYFRAME_APPROVED',
        'VIDEO_GENERATION', 'VIDEO_READY', 'LIPSYNC', 'LIPSYNC_READY',
        'EDITING', 'FINAL_QA', 'FINAL'
    ))
);

CREATE INDEX IF NOT EXISTS ix_video_production_project
    ON pack_content.video_production (project_id, updated_at DESC);

CREATE TABLE IF NOT EXISTS pack_content.video_production_event (
    id             UUID PRIMARY KEY,
    production_id  UUID NOT NULL REFERENCES pack_content.video_production (id) ON DELETE RESTRICT,
    from_state     VARCHAR(32),
    to_state       VARCHAR(32) NOT NULL,
    actor          VARCHAR(120),
    reason         TEXT,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS pack_content.video_shot_state (
    id             UUID PRIMARY KEY,
    production_id  UUID NOT NULL REFERENCES pack_content.video_production (id) ON DELETE RESTRICT,
    shot_code      VARCHAR(32) NOT NULL,
    state          VARCHAR(32) NOT NULL DEFAULT 'DRAFT',
    failed         BOOLEAN NOT NULL DEFAULT FALSE,
    extra_json     JSONB NOT NULL DEFAULT '{}'::jsonb,
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_video_shot_state UNIQUE (production_id, shot_code),
    CONSTRAINT ck_video_shot_state CHECK (state IN (
        'DRAFT', 'HOLD', 'READY', 'KF_GENERATING', 'KF_REVIEW', 'KF_APPROVED',
        'I2V_READY', 'I2V_GENERATING', 'VIDEO_READY', 'LIPSYNC_READY',
        'FINAL_SELECTED', 'FAILED'
    ))
);

COMMENT ON TABLE pack_content.video_project IS
    'KIT Video Engine project. Brand/universe/style live here — not in provider adapters.';
COMMENT ON TABLE pack_content.video_production IS
    'One production (episode/campaign). series_build_id is an optional Famixa adapter hook.';

INSERT INTO pack_content.video_project (id, project_code, name, brand_code, status, visual_json, rules_json)
VALUES
(
    '019f3330-0001-7000-8000-000000000001',
    'FAMIXA',
    'Famixa',
    'famixa',
    'active',
    '{"look":"stylized-human cinematic family drama","not":["cartoon","anime","photoreal TikTok"]}'::jsonb,
    '{"noInvention":true,"confirmBeforeCredit":true,"http200IsNotSuccess":true,"compliance":[]}'::jsonb
),
(
    '019f3330-0001-7000-8000-000000000002',
    'NOVIXA',
    'Novixa',
    'novixa',
    'draft',
    '{}'::jsonb,
    '{"noInvention":true,"confirmBeforeCredit":true}'::jsonb
),
(
    '019f3330-0001-7000-8000-000000000003',
    'PHARMACY',
    'Dược / Nhà thuốc',
    'pharmacy',
    'draft',
    '{}'::jsonb,
    '{"noInvention":true,"confirmBeforeCredit":true,"compliance":["prohibited_claims","disclaimer"]}'::jsonb
),
(
    '019f3330-0001-7000-8000-000000000004',
    'MARKETING',
    'KIT Marketing',
    'kit_mkt',
    'draft',
    '{}'::jsonb,
    '{"noInvention":true,"confirmBeforeCredit":true}'::jsonb
),
(
    '019f3330-0001-7000-8000-000000000005',
    'VAN_DINH_TRA',
    'Vân Đỉnh Trà',
    'van_dinh_tra',
    'draft',
    '{}'::jsonb,
    '{"noInvention":true,"confirmBeforeCredit":true}'::jsonb
)
ON CONFLICT (project_code) DO NOTHING;

INSERT INTO pack_content.video_universe (id, project_id, universe_code, name, status, world_json)
VALUES
(
    '019f3330-1001-7000-8000-000000000001',
    '019f3330-0001-7000-8000-000000000001',
    'FAMILY_A',
    'Famixa Family A',
    'active',
    '{"characters":["CHAR-001","CHAR-002","CHAR-003","CHAR-004","CHAR-VO"],"note":"An mention-only. Ông/Bà/Em gái have no ID yet."}'::jsonb
),
(
    '019f3330-1001-7000-8000-000000000002',
    '019f3330-0001-7000-8000-000000000003',
    'PHARMACY',
    'Pharmacy / Clinic / Customer',
    'draft',
    '{"note":"Activate when Pharmacy video uses the engine. Compliance stays on project rules_json."}'::jsonb
)
ON CONFLICT (project_id, universe_code) DO NOTHING;

DO $$
DECLARE
  r text;
  t text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      FOREACH t IN ARRAY ARRAY[
        'video_project', 'video_universe', 'video_production',
        'video_production_event', 'video_shot_state'
      ] LOOP
        EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.%I TO %I', t, r);
      END LOOP;
    END IF;
  END LOOP;
END $$;
