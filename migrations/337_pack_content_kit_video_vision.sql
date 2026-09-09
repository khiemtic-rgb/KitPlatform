-- KitPlatform 337: KIT Video Engine Phase 04 — Visual Contract / Attempt / Vision QA
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Keyframe only. Do not call Runway. Gemini is a provider, not the engine.

CREATE TABLE IF NOT EXISTS pack_content.video_visual_contract (
    id              UUID PRIMARY KEY,
    production_id   UUID NOT NULL REFERENCES pack_content.video_production (id) ON DELETE RESTRICT,
    shot_code       VARCHAR(32) NOT NULL,
    contract_json   JSONB NOT NULL DEFAULT '{}'::jsonb,
    prompt_json     JSONB NOT NULL DEFAULT '{}'::jsonb,
    fingerprint     VARCHAR(80) NOT NULL DEFAULT '',
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_video_visual_contract UNIQUE (production_id, shot_code)
);

CREATE TABLE IF NOT EXISTS pack_content.video_keyframe_attempt (
    id              UUID PRIMARY KEY,
    production_id   UUID NOT NULL REFERENCES pack_content.video_production (id) ON DELETE RESTRICT,
    shot_code       VARCHAR(32) NOT NULL,
    attempt_no      INT NOT NULL,
    status          VARCHAR(24) NOT NULL DEFAULT 'GENERATED',
    fingerprint     VARCHAR(80) NOT NULL,
    prompt_json     JSONB NOT NULL DEFAULT '{}'::jsonb,
    qa_json         JSONB NOT NULL DEFAULT '{}'::jsonb,
    repair_json     JSONB NOT NULL DEFAULT '{}'::jsonb,
    image_path      TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_video_keyframe_attempt UNIQUE (production_id, shot_code, attempt_no),
    CONSTRAINT ck_video_keyframe_attempt_status CHECK (status IN (
        'GENERATED', 'QA_PASS', 'QA_FAIL', 'REVIEW_REQUIRED', 'REJECTED', 'APPROVED'
    ))
);

CREATE INDEX IF NOT EXISTS ix_video_keyframe_attempt_shot
    ON pack_content.video_keyframe_attempt (production_id, shot_code, attempt_no DESC);

COMMENT ON TABLE pack_content.video_visual_contract IS
    'Phase 04 VisualContract + compiled prompt. Contract ≠ prompt. No dialogue in prompt_json.';
COMMENT ON TABLE pack_content.video_keyframe_attempt IS
    'Immutable attempts. Never overwrite. Same fingerprint after FAIL = blind retry block.';
