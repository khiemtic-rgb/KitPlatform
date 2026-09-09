-- KitPlatform 330: Famixa Character Registry + Canon version (JSONB)
-- Manifest: deploy/ubuntu/migration-files.content.txt only
-- Not 12 DNA tables. Not Episode SQL. Pixels stay local / public path — no dataUrl in canon_json.

CREATE TABLE IF NOT EXISTS pack_content.famixa_character (
    id                  UUID PRIMARY KEY,
    character_code      VARCHAR(32) NOT NULL,
    name                VARCHAR(120) NOT NULL,
    role                VARCHAR(80) NOT NULL DEFAULT '',
    universe            VARCHAR(24) NOT NULL DEFAULT 'CORE',
    visual              VARCHAR(16) NOT NULL DEFAULT 'frame',
    lifecycle           VARCHAR(24) NOT NULL DEFAULT 'draft',
    current_version_id  UUID,
    current_era         VARCHAR(16) NOT NULL DEFAULT 'A11',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_famixa_character_code UNIQUE (character_code),
    CONSTRAINT ck_famixa_character_code CHECK (character_code ~ '^(CHAR-[0-9]{3}|CHAR-VO)$'),
    CONSTRAINT ck_famixa_character_universe CHECK (universe IN ('CORE', 'RECURRING', 'SUPPORTING', 'BACKGROUND', 'VOICE')),
    CONSTRAINT ck_famixa_character_visual CHECK (visual IN ('frame', 'mention', 'voice')),
    CONSTRAINT ck_famixa_character_lifecycle CHECK (
        lifecycle IN ('draft', 'designing', 'review', 'approved', 'locked', 'archived')
    )
);

CREATE TABLE IF NOT EXISTS pack_content.famixa_character_version (
    id                 UUID PRIMARY KEY,
    character_id       UUID NOT NULL REFERENCES pack_content.famixa_character (id) ON DELETE RESTRICT,
    version            VARCHAR(16) NOT NULL DEFAULT 'V1',
    era                VARCHAR(16) NOT NULL DEFAULT 'A11',
    status             VARCHAR(24) NOT NULL DEFAULT 'draft',
    is_current_canon   BOOLEAN NOT NULL DEFAULT FALSE,
    approved_at        TIMESTAMPTZ,
    approved_by        VARCHAR(120),
    canon_json         JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_famixa_character_version UNIQUE (character_id, version, era),
    CONSTRAINT ck_famixa_character_version_status CHECK (
        status IN ('draft', 'designing', 'review', 'approved', 'locked', 'archived')
    )
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_famixa_character_current_canon
    ON pack_content.famixa_character_version (character_id)
    WHERE is_current_canon;

CREATE INDEX IF NOT EXISTS ix_famixa_character_lifecycle
    ON pack_content.famixa_character (lifecycle, character_code);

COMMENT ON TABLE pack_content.famixa_character IS
    'Famixa Character Registry. ID = character_code. LOCKED rows are not silently overwritten. Archive instead of delete.';
COMMENT ON TABLE pack_content.famixa_character_version IS
    'Canon snapshot per Character+Era+Version. JSONB memory — not a prompt dump. No image data URLs.';

INSERT INTO pack_content.famixa_character (
    id, character_code, name, role, universe, visual, lifecycle, current_version_id, current_era
) VALUES
    ('019f3300-0001-7000-8000-000000000001', 'CHAR-001', 'Minh', 'Con', 'CORE', 'frame', 'approved',
     '019f3300-1001-7000-8000-000000000001', 'A11'),
    ('019f3300-0001-7000-8000-000000000002', 'CHAR-002', 'Nam', 'Bố', 'CORE', 'frame', 'approved',
     '019f3300-1001-7000-8000-000000000002', 'A11'),
    ('019f3300-0001-7000-8000-000000000003', 'CHAR-003', 'Linh', 'Mẹ', 'CORE', 'frame', 'approved',
     '019f3300-1001-7000-8000-000000000003', 'A11'),
    ('019f3300-0001-7000-8000-000000000004', 'CHAR-004', 'An', 'Bạn', 'RECURRING', 'mention', 'approved',
     '019f3300-1001-7000-8000-000000000004', 'A11'),
    ('019f3300-0001-7000-8000-0000000000aa', 'CHAR-VO', 'Lời bình', 'Lời bình', 'VOICE', 'voice', 'approved',
     '019f3300-1001-7000-8000-0000000000aa', 'A11')
ON CONFLICT (character_code) DO NOTHING;

INSERT INTO pack_content.famixa_character_version (
    id, character_id, version, era, status, is_current_canon, approved_at, approved_by, canon_json
) VALUES
(
    '019f3300-1001-7000-8000-000000000001',
    '019f3300-0001-7000-8000-000000000001',
    'V1', 'A11', 'approved', TRUE, NOW(), 'seed',
    $json${
      "identity": {"characterId":"CHAR-001","name":"Minh","role":"Con","gender":"male","currentAge":11,"currentEra":"A11"},
      "visualDna": {"face":"round youthful face","hair":"short black slightly messy hair","eyes":"large natural dark eyes","eyebrows":"natural dark","nose":"small youthful","mouth":"child mouth","skin":"warm Vietnamese","bodyProportion":"child","height":"short","build":"slim child body","distinctiveFeatures":"boy 11","defaultClothing":"HOME","visualStyle":"FAMIXA_VISUAL_STYLE"},
      "personalityDna": ["curious","sensitive","proud","sometimes stubborn","needs recognition"],
      "behaviorDna": [{"when":"embarrassed","does":"looks down"},{"when":"hurt","does":"becomes quiet rather than crying immediately"}],
      "voiceDna": {"voiceId":"","language":"vi","gender":"male","ageCharacter":"child","tone":"northern","speakingStyle":"direct","speed":"medium"},
      "wardrobe": [{"id":"OUTFIT-HOME-01","set":"HOME","label":"Áo nhà tối"}],
      "relationships": [{"to":"CHAR-003","type":"mother"},{"to":"CHAR-002","type":"father"},{"to":"CHAR-004","type":"friend"}],
      "continuityRules": ["Do not redesign face or hair","Wardrobe from previous KF unless shot override","Emotion is Shot State"],
      "famixaVisualStyle": {"summary":"Cinematic stylized-human Vietnamese family drama. Natural proportions, subtle stylization, emotional. Not cartoon comedy. Not anime. Not a bright catalog portrait."},
      "references": [{"kind":"FRONT","path":"/content/famixa/canon/CHAR-001-minh-master.png","label":"Minh A11 FRONT"}]
    }$json$::jsonb
),
(
    '019f3300-1001-7000-8000-000000000002',
    '019f3300-0001-7000-8000-000000000002',
    'V1', 'A11', 'approved', TRUE, NOW(), 'seed',
    $json${
      "identity": {"characterId":"CHAR-002","name":"Nam","role":"Bố","gender":"male","currentAge":38,"currentEra":"A11"},
      "visualDna": {"face":"adult Vietnamese father face","hair":"short black hair","eyes":"dark","build":"medium adult","distinctiveFeatures":"father Nam"},
      "personalityDna": [],
      "behaviorDna": [],
      "voiceDna": {"voiceId":"","language":"vi","gender":"male","ageCharacter":"adult","tone":"northern"},
      "wardrobe": [{"id":"OUTFIT-HOME-01","set":"HOME","label":"Áo nhà"}],
      "relationships": [{"to":"CHAR-001","type":"son"},{"to":"CHAR-003","type":"wife"}],
      "continuityRules": ["Do not redesign face","Copy wardrobe from previous KF when already in frame"],
      "famixaVisualStyle": {"summary":"Cinematic stylized-human Vietnamese family drama. Natural proportions, subtle stylization, emotional. Not cartoon comedy. Not anime. Not a bright catalog portrait."},
      "references": [{"kind":"FRONT","path":"/content/famixa/canon/CHAR-002-nam-master.png","label":"Nam A11 FRONT"}]
    }$json$::jsonb
),
(
    '019f3300-1001-7000-8000-000000000003',
    '019f3300-0001-7000-8000-000000000003',
    'V1', 'A11', 'approved', TRUE, NOW(), 'seed',
    $json${
      "identity": {"characterId":"CHAR-003","name":"Linh","role":"Mẹ","gender":"female","currentAge":36,"currentEra":"A11"},
      "visualDna": {"face":"adult Vietnamese mother face","hair":"long dark hair","eyes":"dark","build":"slim adult","distinctiveFeatures":"mother Linh"},
      "personalityDna": [],
      "behaviorDna": [],
      "voiceDna": {"voiceId":"","language":"vi","gender":"female","ageCharacter":"adult","tone":"northern"},
      "wardrobe": [{"id":"OUTFIT-HOME-01","set":"HOME","label":"Áo nhà"}],
      "relationships": [{"to":"CHAR-001","type":"son"},{"to":"CHAR-002","type":"husband"}],
      "continuityRules": ["Do not redesign face","Copy wardrobe from previous KF when already in frame"],
      "famixaVisualStyle": {"summary":"Cinematic stylized-human Vietnamese family drama. Natural proportions, subtle stylization, emotional. Not cartoon comedy. Not anime. Not a bright catalog portrait."},
      "references": [{"kind":"FRONT","path":"/content/famixa/canon/CHAR-003-linh-master.png","label":"Linh A11 FRONT"}]
    }$json$::jsonb
),
(
    '019f3300-1001-7000-8000-000000000004',
    '019f3300-0001-7000-8000-000000000004',
    'V1', 'A11', 'approved', TRUE, NOW(), 'seed',
    $json${
      "identity": {"characterId":"CHAR-004","name":"An","role":"Bạn","gender":"male","currentAge":11,"currentEra":"A11"},
      "visualDna": {"face":"mention only — off frame"},
      "personalityDna": [],
      "behaviorDna": [],
      "voiceDna": {"voiceId":"","language":"vi","gender":"male","ageCharacter":"child"},
      "wardrobe": [],
      "relationships": [{"to":"CHAR-001","type":"friend"}],
      "continuityRules": ["An is mention-only. Do not draw An in frame."],
      "famixaVisualStyle": {"summary":"Cinematic stylized-human Vietnamese family drama. Natural proportions, subtle stylization, emotional. Not cartoon comedy. Not anime. Not a bright catalog portrait."},
      "references": []
    }$json$::jsonb
),
(
    '019f3300-1001-7000-8000-0000000000aa',
    '019f3300-0001-7000-8000-0000000000aa',
    'V1', 'A11', 'approved', TRUE, NOW(), 'seed',
    $json${
      "identity": {"characterId":"CHAR-VO","name":"Lời bình","role":"Lời bình","gender":"","currentAge":0,"currentEra":"A11"},
      "visualDna": {},
      "personalityDna": [],
      "behaviorDna": [],
      "voiceDna": {"voiceId":"","language":"vi","gender":"male","ageCharacter":"adult","tone":"northern"},
      "wardrobe": [],
      "relationships": [],
      "continuityRules": ["Voice only. No body in frame."],
      "famixaVisualStyle": {"summary":"Cinematic stylized-human Vietnamese family drama. Natural proportions, subtle stylization, emotional. Not cartoon comedy. Not anime. Not a bright catalog portrait."},
      "references": []
    }$json$::jsonb
)
ON CONFLICT (character_id, version, era) DO NOTHING;

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.famixa_character TO %I', r);
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.famixa_character_version TO %I', r);
    END IF;
  END LOOP;
END $$;
