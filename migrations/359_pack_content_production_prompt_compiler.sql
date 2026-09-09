-- KitPlatform 359: PRODUCTION_PROMPT_COMPILER_V1
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Compiles DIRECTOR_APPROVED shot contract → model-agnostic prompt + SHA.
-- No pixels. No Gemini. No Runway. Does not modify Master/DNA/PRP/Contract payload. Does not touch Golden SH01-01.

CREATE TABLE IF NOT EXISTS pack_content.video_production_prompt (
    id                      UUID PRIMARY KEY,
    shot_id                 UUID NOT NULL REFERENCES pack_content.video_production_shot (id) ON DELETE RESTRICT,
    contract_id             UUID NOT NULL REFERENCES pack_content.video_production_shot_contract (id) ON DELETE RESTRICT,
    series_id               VARCHAR(40) NOT NULL,
    character_id            VARCHAR(32) NOT NULL,
    era_id                  VARCHAR(16) NOT NULL,
    contract_version        VARCHAR(16) NOT NULL,
    prompt_version          VARCHAR(16) NOT NULL DEFAULT 'V1',
    prompt_status           VARCHAR(24) NOT NULL DEFAULT 'NOT_READY',
    document_id             VARCHAR(80) NOT NULL DEFAULT 'PRODUCTION_PROMPT_COMPILER_V1',
    project_code            VARCHAR(32) NOT NULL DEFAULT 'FAMIXA',
    prompt_text             TEXT NOT NULL DEFAULT '',
    negative_json           JSONB NOT NULL DEFAULT '[]'::jsonb,
    prompt_sha256           VARCHAR(64) NOT NULL DEFAULT '',
    contract_sha256         VARCHAR(64) NOT NULL DEFAULT '',
    master_id               UUID,
    master_sha256           VARCHAR(64) NOT NULL DEFAULT '',
    dna_id                  UUID,
    dna_sha256              VARCHAR(64) NOT NULL DEFAULT '',
    prp_id                  UUID,
    prp_sha256              VARCHAR(64) NOT NULL DEFAULT '',
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by              VARCHAR(120),
    superseded_by           UUID,
    extra_json              JSONB NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT uq_video_prod_prompt_ver UNIQUE (shot_id, prompt_version),
    CONSTRAINT ck_video_prod_prompt_status CHECK (prompt_status IN (
        'NOT_READY', 'BLOCKED', 'COMPILED', 'SUPERSEDED'
    )),
    CONSTRAINT ck_video_prod_prompt_no_generated CHECK (prompt_status NOT IN (
        'GENERATED', 'IMAGE_READY', 'VIDEO_READY'
    ))
);

CREATE TABLE IF NOT EXISTS pack_content.video_production_prompt_event (
    id                      UUID PRIMARY KEY,
    prompt_id               UUID REFERENCES pack_content.video_production_prompt (id) ON DELETE RESTRICT,
    shot_id                 UUID NOT NULL REFERENCES pack_content.video_production_shot (id) ON DELETE RESTRICT,
    contract_id             UUID REFERENCES pack_content.video_production_shot_contract (id) ON DELETE RESTRICT,
    event_type              VARCHAR(40) NOT NULL,
    payload_json            JSONB NOT NULL DEFAULT '{}'::jsonb,
    actor                   VARCHAR(120),
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_video_prod_prompt_shot
    ON pack_content.video_production_prompt (shot_id, prompt_version DESC);

CREATE INDEX IF NOT EXISTS ix_video_prod_prompt_event
    ON pack_content.video_production_prompt_event (shot_id, created_at DESC);

CREATE OR REPLACE FUNCTION pack_content.fn_video_prod_prompt_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN
    IF OLD.prompt_status = 'COMPILED' THEN
      RAISE EXCEPTION 'PROMPT_COMPILER_LOCKED: Prompt V1 không overwrite. Dùng V2.';
    END IF;
    RETURN OLD;
  END IF;
  IF OLD.prompt_status = 'COMPILED' THEN
    IF NEW.prompt_status = 'SUPERSEDED'
       AND NEW.prompt_text = OLD.prompt_text
       AND NEW.prompt_sha256 = OLD.prompt_sha256
       AND NEW.contract_sha256 = OLD.contract_sha256 THEN
      RETURN NEW;
    END IF;
    RAISE EXCEPTION 'PROMPT_COMPILER_LOCKED: Prompt V1 không overwrite. Dùng V2.';
  END IF;
  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_video_prod_prompt_immutable ON pack_content.video_production_prompt;
CREATE TRIGGER trg_video_prod_prompt_immutable
  BEFORE UPDATE OR DELETE ON pack_content.video_production_prompt
  FOR EACH ROW EXECUTE FUNCTION pack_content.fn_video_prod_prompt_immutable();

CREATE OR REPLACE FUNCTION pack_content.fn_video_prod_prompt_event_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'PROMPT_COMPILER_LOCKED: audit event is immutable';
END;
$$;

DROP TRIGGER IF EXISTS trg_video_prod_prompt_event_immutable ON pack_content.video_production_prompt_event;
CREATE TRIGGER trg_video_prod_prompt_event_immutable
  BEFORE UPDATE OR DELETE ON pack_content.video_production_prompt_event
  FOR EACH ROW EXECUTE FUNCTION pack_content.fn_video_prod_prompt_event_immutable();
