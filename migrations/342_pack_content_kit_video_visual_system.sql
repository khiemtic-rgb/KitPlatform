-- KitPlatform 342: generic Visual Style System (project-scoped, versioned).
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Reuses video_project. Not a FamixaVisualEngine. Do not call Gemini/Runway. Do not lock characters.

CREATE TABLE IF NOT EXISTS pack_content.video_visual_system (
    id             UUID PRIMARY KEY,
    project_id     UUID NOT NULL REFERENCES pack_content.video_project (id) ON DELETE RESTRICT,
    system_code    VARCHAR(64) NOT NULL,
    version        VARCHAR(16) NOT NULL,
    status         VARCHAR(24) NOT NULL DEFAULT 'draft',
    rules_json     JSONB NOT NULL DEFAULT '{}'::jsonb,
    locked_at      TIMESTAMPTZ,
    locked_by      VARCHAR(120),
    superseded_by  UUID,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_video_visual_system UNIQUE (project_id, system_code, version),
    CONSTRAINT ck_video_visual_system_code CHECK (system_code ~ '^[A-Z][A-Z0-9_]{1,62}$'),
    CONSTRAINT ck_video_visual_system_version CHECK (version ~ '^V[0-9]{1,3}$'),
    CONSTRAINT ck_video_visual_system_status CHECK (status IN ('draft', 'proposed', 'locked', 'superseded'))
);

CREATE INDEX IF NOT EXISTS ix_video_visual_system_project
    ON pack_content.video_visual_system (project_id, system_code, version);

COMMENT ON TABLE pack_content.video_visual_system IS
    'Generic project Visual Style System. Famixa values live in Project=FAMIXA. Locked version is immutable.';

INSERT INTO pack_content.video_visual_system (
    id, project_id, system_code, version, status, rules_json
)
SELECT
    '019f3420-0001-7000-8000-000000000001',
    p.id,
    'VISUAL_STYLE_SYSTEM',
    'V1',
    'proposed',
    $json${
      "documentId":"FAMIXA_VISUAL_STYLE_SYSTEM_V1",
      "systemCode":"VISUAL_STYLE_SYSTEM",
      "version":"V1",
      "status":"proposed",
      "projectCode":"FAMIXA",
      "providerIndependent":true,
      "providers":[],
      "purpose":"Recognizable as Famixa, not merely recognizable as AI.",
      "principle":"Do not compete with generic photorealistic AI films.",
      "promptHierarchy":["CHARACTER_CANON","LOCATION_CANON","PROP_CANON","WARDROBE_CANON","SCENE_REQUIREMENT","GENERATION_PROMPT"],
      "era":{"identityPersists":true,"example":"CHAR-001 ERA-01/11 ERA-02/16 ERA-03/23 = one identity"},
      "masterReference":{"required":true,"notI2vSource":true},
      "productionStill":{"oneFrame":true,"forbidden":["character sheet","collage","multiple panels","storyboard","reference board","text-heavy","logo","watermark"]},
      "forbiddenDrift":["face redesign","hairstyle redesign","photorealism drift","anime drift","cartoon drift","location drift","wardrobe drift"],
      "forbiddenGeneration":["invent characters","invent family members","invent locations","character sheet when production still requested","add text"],
      "qualityVsIdentity":"Beautiful + wrong identity = FAIL",
      "versioning":"V1 immutable once locked. Change = V2."
    }$json$::jsonb
FROM pack_content.video_project p
WHERE p.project_code = 'FAMIXA'
ON CONFLICT (project_id, system_code, version) DO NOTHING;

UPDATE pack_content.video_project
SET visual_json = COALESCE(visual_json, '{}'::jsonb) ||
    '{"visualSystemCode":"VISUAL_STYLE_SYSTEM","visualSystemVersion":"V1"}'::jsonb,
    updated_at = NOW()
WHERE project_code = 'FAMIXA';
