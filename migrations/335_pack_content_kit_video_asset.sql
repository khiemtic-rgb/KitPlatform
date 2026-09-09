-- KitPlatform 335: KIT Video Engine Phase 02 — generic Asset / Canon / Reference
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Engine ≠ Project. Famixa consumes Project=FAMIXA. Do not overwrite famixa_character.

CREATE TABLE IF NOT EXISTS pack_content.video_asset (
    id                  UUID PRIMARY KEY,
    project_id          UUID NOT NULL REFERENCES pack_content.video_project (id) ON DELETE RESTRICT,
    asset_code          VARCHAR(48) NOT NULL,
    asset_kind          VARCHAR(24) NOT NULL,
    name                VARCHAR(160) NOT NULL,
    lifecycle           VARCHAR(24) NOT NULL DEFAULT 'DRAFT',
    current_version     VARCHAR(16) NOT NULL DEFAULT 'V1',
    current_era         VARCHAR(16) NOT NULL DEFAULT '',
    extra_json          JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_video_asset UNIQUE (project_id, asset_code),
    CONSTRAINT ck_video_asset_kind CHECK (asset_kind IN (
        'CHARACTER', 'LOCATION', 'PRODUCT', 'PROP', 'VEHICLE', 'ANIMAL', 'OTHER', 'WARDROBE'
    )),
    CONSTRAINT ck_video_asset_life CHECK (lifecycle IN (
        'DRAFT', 'CREATING', 'REVIEW', 'APPROVED', 'LOCKED', 'ARCHIVED'
    ))
);

CREATE INDEX IF NOT EXISTS ix_video_asset_project
    ON pack_content.video_asset (project_id, asset_kind, asset_code);

CREATE TABLE IF NOT EXISTS pack_content.video_asset_version (
    id           UUID PRIMARY KEY,
    asset_id     UUID NOT NULL REFERENCES pack_content.video_asset (id) ON DELETE RESTRICT,
    version      VARCHAR(16) NOT NULL,
    era          VARCHAR(16) NOT NULL DEFAULT '',
    status       VARCHAR(24) NOT NULL DEFAULT 'DRAFT',
    is_current   BOOLEAN NOT NULL DEFAULT FALSE,
    canon_json   JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_video_asset_version UNIQUE (asset_id, version, era),
    CONSTRAINT ck_video_asset_version_status CHECK (status IN (
        'DRAFT', 'CREATING', 'REVIEW', 'APPROVED', 'LOCKED', 'ARCHIVED'
    ))
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_video_asset_current_version
    ON pack_content.video_asset_version (asset_id) WHERE is_current;

CREATE TABLE IF NOT EXISTS pack_content.video_asset_reference (
    id            UUID PRIMARY KEY,
    version_id    UUID NOT NULL REFERENCES pack_content.video_asset_version (id) ON DELETE RESTRICT,
    kind          VARCHAR(32) NOT NULL,
    path          TEXT NOT NULL,
    is_primary    BOOLEAN NOT NULL DEFAULT FALSE,
    is_secondary  BOOLEAN NOT NULL DEFAULT FALSE,
    qa_status     VARCHAR(16) NOT NULL DEFAULT 'PENDING',
    qa_json       JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_video_asset_ref_qa CHECK (qa_status IN ('PENDING', 'PASS', 'FAIL', 'BLOCK')),
    CONSTRAINT uq_video_asset_ref UNIQUE (version_id, kind)
);

CREATE TABLE IF NOT EXISTS pack_content.video_asset_usage (
    id             UUID PRIMARY KEY,
    asset_id       UUID NOT NULL REFERENCES pack_content.video_asset (id) ON DELETE RESTRICT,
    production_id  UUID NOT NULL REFERENCES pack_content.video_production (id) ON DELETE RESTRICT,
    scene_code     VARCHAR(32) NOT NULL DEFAULT '',
    shot_code      VARCHAR(32) NOT NULL,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_video_asset_usage UNIQUE (asset_id, production_id, shot_code)
);

CREATE TABLE IF NOT EXISTS pack_content.video_scene_master (
    id              UUID PRIMARY KEY,
    production_id   UUID NOT NULL REFERENCES pack_content.video_production (id) ON DELETE RESTRICT,
    scene_code      VARCHAR(32) NOT NULL,
    location_code   VARCHAR(48) NOT NULL DEFAULT '',
    status          VARCHAR(16) NOT NULL DEFAULT 'DRAFT',
    canon_json      JSONB NOT NULL DEFAULT '{}'::jsonb,
    presence_json   JSONB NOT NULL DEFAULT '[]'::jsonb,
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_video_scene_master UNIQUE (production_id, scene_code),
    CONSTRAINT ck_video_scene_master_status CHECK (status IN ('DRAFT', 'LOCKED'))
);

CREATE TABLE IF NOT EXISTS pack_content.video_shot_package (
    id              UUID PRIMARY KEY,
    production_id   UUID NOT NULL REFERENCES pack_content.video_production (id) ON DELETE RESTRICT,
    shot_code       VARCHAR(32) NOT NULL,
    scene_code      VARCHAR(32) NOT NULL DEFAULT '',
    status          VARCHAR(16) NOT NULL DEFAULT 'DRAFT',
    package_json    JSONB NOT NULL DEFAULT '{}'::jsonb,
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_video_shot_package UNIQUE (production_id, shot_code),
    CONSTRAINT ck_video_shot_package_status CHECK (status IN ('DRAFT', 'READY', 'BLOCKED', 'APPROVED'))
);

CREATE TABLE IF NOT EXISTS pack_content.video_keyframe_snapshot (
    id              UUID PRIMARY KEY,
    production_id   UUID NOT NULL REFERENCES pack_content.video_production (id) ON DELETE RESTRICT,
    shot_code       VARCHAR(32) NOT NULL,
    snapshot_json   JSONB NOT NULL DEFAULT '{}'::jsonb,
    approved_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_video_keyframe_snapshot UNIQUE (production_id, shot_code)
);

COMMENT ON TABLE pack_content.video_asset IS
    'Engine asset (Character/Location/Prop/Wardrobe). Canon is structured JSONB — not a prompt blob. Famixa = project FAMIXA.';
COMMENT ON TABLE pack_content.video_asset_version IS
    'LOCKED versions are immutable. New look = new version. Eras share one identity.';

-- FAMIXA seed: same identity as famixa_character. Do not invent Ông/Bà. Do not lock Minh face.
INSERT INTO pack_content.video_asset (id, project_id, asset_code, asset_kind, name, lifecycle, current_version, current_era, extra_json)
SELECT v.id::uuid, '019f3330-0001-7000-8000-000000000001'::uuid, v.code, v.kind, v.name, 'APPROVED', 'V1', v.era, v.extra::jsonb
FROM (VALUES
    ('019f3350-0001-7000-8000-000000000001', 'CHAR-001', 'CHARACTER', 'Minh', 'ERA-01',
     '{"source":"famixa_character","characterCode":"CHAR-001"}'),
    ('019f3350-0001-7000-8000-000000000002', 'CHAR-002', 'CHARACTER', 'Nam', 'ERA-01',
     '{"source":"famixa_character","characterCode":"CHAR-002"}'),
    ('019f3350-0001-7000-8000-000000000003', 'CHAR-003', 'CHARACTER', 'Linh', 'ERA-01',
     '{"source":"famixa_character","characterCode":"CHAR-003"}'),
    ('019f3350-0001-7000-8000-000000000004', 'CHAR-004', 'CHARACTER', 'An', 'ERA-01',
     '{"source":"famixa_character","characterCode":"CHAR-004","visual":"mention"}'),
    ('019f3350-0001-7000-8000-000000000101', 'LOC-001', 'LOCATION', 'Minh home living room', '',
     '{"slug":"MINH-HOME-LIVING-ROOM"}'),
    ('019f3350-0001-7000-8000-000000000201', 'PROP-001', 'PROP', 'School test paper', '',
     '{"slug":"TEST-PAPER"}'),
    ('019f3350-0001-7000-8000-000000000202', 'PROP-002', 'PROP', 'Phone', '', '{}'),
    ('019f3350-0001-7000-8000-000000000203', 'PROP-003', 'PROP', 'Medicine box', '', '{}'),
    ('019f3350-0001-7000-8000-000000000301', 'WARDROBE-001', 'WARDROBE', 'School uniform', '', '{}'),
    ('019f3350-0001-7000-8000-000000000302', 'WARDROBE-002', 'WARDROBE', 'Home clothes', '', '{}')
) AS v(id, code, kind, name, era, extra)
ON CONFLICT (project_id, asset_code) DO NOTHING;

INSERT INTO pack_content.video_asset_version (id, asset_id, version, era, status, is_current, canon_json)
VALUES
(
    '019f3350-1001-7000-8000-000000000001',
    '019f3350-0001-7000-8000-000000000001',
    'V1', 'ERA-01', 'APPROVED', TRUE,
    $json${
      "identity":{"name":"Minh","gender":"male","age":11,"era":"ERA-01"},
      "face":{"shape":"round youthful","eyes":"large dark","distinctive":"boy 11"},
      "hair":{"style":"short black slightly messy","color":"black"},
      "body":{"proportion":"child","height":"short","build":"slim"},
      "skin":{"tone":"warm Vietnamese"},
      "wardrobe":{"defaultId":"WARDROBE-001"},
      "personality":["curious","sensitive"],
      "visualStyle":"FAMIXA_VISUAL_STYLE",
      "voice":{"ref":"CHAR-001"}
    }$json$::jsonb
),
(
    '019f3350-1001-7000-8000-000000001101',
    '019f3350-0001-7000-8000-000000000001',
    'V1', 'ERA-02', 'DRAFT', FALSE,
    $json${"identity":{"name":"Minh","gender":"male","age":16,"era":"ERA-02"},"face":{},"hair":{},"body":{},"skin":{},"wardrobe":{},"personality":[],"visualStyle":"FAMIXA_VISUAL_STYLE","voice":{}}$json$::jsonb
),
(
    '019f3350-1001-7000-8000-000000001201',
    '019f3350-0001-7000-8000-000000000001',
    'V1', 'ERA-03', 'DRAFT', FALSE,
    $json${"identity":{"name":"Minh","gender":"male","age":23,"era":"ERA-03"},"face":{},"hair":{},"body":{},"skin":{},"wardrobe":{},"personality":[],"visualStyle":"FAMIXA_VISUAL_STYLE","voice":{}}$json$::jsonb
),
(
    '019f3350-1001-7000-8000-000000000002',
    '019f3350-0001-7000-8000-000000000002',
    'V1', 'ERA-01', 'APPROVED', TRUE,
    $json${"identity":{"name":"Nam","gender":"male","age":40,"era":"ERA-01"},"face":{"shape":"adult father"},"hair":{"style":"short neat"},"body":{"proportion":"adult"},"skin":{},"wardrobe":{"defaultId":"WARDROBE-002"},"personality":[],"visualStyle":"FAMIXA_VISUAL_STYLE","voice":{}}$json$::jsonb
),
(
    '019f3350-1001-7000-8000-000000000003',
    '019f3350-0001-7000-8000-000000000003',
    'V1', 'ERA-01', 'APPROVED', TRUE,
    $json${"identity":{"name":"Linh","gender":"female","age":38,"era":"ERA-01"},"face":{"shape":"adult mother"},"hair":{"style":"shoulder dark"},"body":{"proportion":"adult"},"skin":{},"wardrobe":{"defaultId":"WARDROBE-002"},"personality":[],"visualStyle":"FAMIXA_VISUAL_STYLE","voice":{}}$json$::jsonb
),
(
    '019f3350-1001-7000-8000-000000000004',
    '019f3350-0001-7000-8000-000000000004',
    'V1', 'ERA-01', 'APPROVED', TRUE,
    $json${"identity":{"name":"An","gender":"female","age":11,"era":"ERA-01","visual":"mention"},"face":{},"hair":{},"body":{},"skin":{},"wardrobe":{},"personality":[],"visualStyle":"FAMIXA_VISUAL_STYLE","voice":{}}$json$::jsonb
),
(
    '019f3350-1001-7000-8000-000000000101',
    '019f3350-0001-7000-8000-000000000101',
    'V1', '', 'APPROVED', TRUE,
    $json${
      "architecture":"small Vietnamese living room",
      "layout":"sofa facing TV, doorway left, window right",
      "furniture":["sofa","TV cabinet","low table"],
      "doors":["living-room doorway"],
      "windows":["street window"],
      "wall":"warm cream",
      "floor":"tile",
      "lighting":"late afternoon indoor",
      "color":"warm amber",
      "objects":["school bag hook"],
      "spatial":["doorway left of sofa"]
    }$json$::jsonb
),
(
    '019f3350-1001-7000-8000-000000000201',
    '019f3350-0001-7000-8000-000000000201',
    'V1', '', 'APPROVED', TRUE,
    $json${"identity":{"name":"School test paper"},"look":"A4 paper red score","continuity":true}$json$::jsonb
),
(
    '019f3350-1001-7000-8000-000000000202',
    '019f3350-0001-7000-8000-000000000202',
    'V1', '', 'APPROVED', TRUE,
    $json${"identity":{"name":"Phone"},"continuity":true}$json$::jsonb
),
(
    '019f3350-1001-7000-8000-000000000203',
    '019f3350-0001-7000-8000-000000000203',
    'V1', '', 'APPROVED', TRUE,
    $json${"identity":{"name":"Medicine box"},"continuity":true}$json$::jsonb
),
(
    '019f3350-1001-7000-8000-000000000301',
    '019f3350-0001-7000-8000-000000000301',
    'V1', '', 'APPROVED', TRUE,
    $json${"identity":{"name":"School uniform"},"pieces":["white shirt","navy pants"]}$json$::jsonb
),
(
    '019f3350-1001-7000-8000-000000000302',
    '019f3350-0001-7000-8000-000000000302',
    'V1', '', 'APPROVED', TRUE,
    $json${"identity":{"name":"Home clothes"},"pieces":["soft t-shirt"]}$json$::jsonb
)
ON CONFLICT (asset_id, version, era) DO NOTHING;

INSERT INTO pack_content.video_asset_reference (id, version_id, kind, path, is_primary, is_secondary, qa_status)
VALUES
    ('019f3350-2001-7000-8000-000000000001', '019f3350-1001-7000-8000-000000000001',
     'FRONT', '/content/famixa/canon/CHAR-001-FRONT.png', TRUE, FALSE, 'PASS'),
    ('019f3350-2001-7000-8000-000000000011', '019f3350-1001-7000-8000-000000000001',
     'THREE_Q_LEFT', '/content/famixa/canon/CHAR-001-34L.png', FALSE, TRUE, 'PASS'),
    ('019f3350-2001-7000-8000-000000000101', '019f3350-1001-7000-8000-000000000101',
     'WIDE', '/content/famixa/canon/LOC-001-WIDE.png', TRUE, FALSE, 'PASS')
ON CONFLICT (version_id, kind) DO NOTHING;

DO $$
DECLARE
  r text;
  t text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      FOREACH t IN ARRAY ARRAY[
        'video_asset', 'video_asset_version', 'video_asset_reference', 'video_asset_usage',
        'video_scene_master', 'video_shot_package', 'video_keyframe_snapshot'
      ] LOOP
        EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.%I TO %I', t, r);
      END LOOP;
    END IF;
  END LOOP;
END $$;
