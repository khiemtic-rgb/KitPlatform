-- KitPlatform 362: PRODUCTION_IMAGE_GENERATION_EXECUTION_V1
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- One controlled Gemini still from a DIRECTOR_APPROVED Image Generation Contract.
-- Does not modify Master/DNA/PRP/Shot Contract/Prompt/IGC. Does not touch Golden SH01-01.
-- No Runway. No auto-approve. Artifact + SHA immutable after persist.

CREATE TABLE IF NOT EXISTS pack_content.video_image_generation_execution (
    id                          UUID PRIMARY KEY,
    shot_id                     UUID NOT NULL REFERENCES pack_content.video_production_shot (id) ON DELETE RESTRICT,
    igc_id                      UUID NOT NULL REFERENCES pack_content.video_image_generation_contract (id) ON DELETE RESTRICT,
    shot_contract_id            UUID NOT NULL REFERENCES pack_content.video_production_shot_contract (id) ON DELETE RESTRICT,
    prompt_id                   UUID NOT NULL REFERENCES pack_content.video_production_prompt (id) ON DELETE RESTRICT,
    series_id                   VARCHAR(40) NOT NULL,
    character_id                VARCHAR(32) NOT NULL,
    era_id                      VARCHAR(16) NOT NULL,
    document_id                 VARCHAR(80) NOT NULL DEFAULT 'PRODUCTION_IMAGE_GENERATION_EXECUTION_V1',
    execution_status            VARCHAR(32) NOT NULL DEFAULT 'PREFLIGHT',
    provider                    VARCHAR(24) NOT NULL DEFAULT 'GEMINI',
    provider_status             VARCHAR(32) NOT NULL DEFAULT '',
    provider_request_id         VARCHAR(80),
    execution_fingerprint       VARCHAR(64) NOT NULL,
    idempotency_key             VARCHAR(64) NOT NULL,
    master_sha256               VARCHAR(64) NOT NULL DEFAULT '',
    dna_sha256                  VARCHAR(64) NOT NULL DEFAULT '',
    prp_sha256                  VARCHAR(64) NOT NULL DEFAULT '',
    shot_contract_sha256        VARCHAR(64) NOT NULL DEFAULT '',
    prompt_sha256               VARCHAR(64) NOT NULL DEFAULT '',
    igc_sha256                  VARCHAR(64) NOT NULL DEFAULT '',
    igc_version                 VARCHAR(16) NOT NULL DEFAULT '',
    prompt_version              VARCHAR(16) NOT NULL DEFAULT '',
    shot_contract_version       VARCHAR(16) NOT NULL DEFAULT '',
    artifact_path               TEXT,
    artifact_sha256             VARCHAR(64) NOT NULL DEFAULT '',
    artifact_mime               VARCHAR(40) NOT NULL DEFAULT '',
    qa_json                     JSONB NOT NULL DEFAULT '{}'::jsonb,
    credit_status               VARCHAR(16) NOT NULL DEFAULT 'UNKNOWN',
    credit_value                NUMERIC,
    requested_at                TIMESTAMPTZ,
    accepted_at                 TIMESTAMPTZ,
    completed_at                TIMESTAMPTZ,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by                  VARCHAR(120),
    updated_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by                  VARCHAR(120),
    approved_at                 TIMESTAMPTZ,
    approved_by                 VARCHAR(120),
    rejected_at                 TIMESTAMPTZ,
    rejected_by                 VARCHAR(120),
    extra_json                  JSONB NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT uq_video_img_gen_exec_fingerprint UNIQUE (execution_fingerprint),
    CONSTRAINT ck_video_img_gen_exec_status CHECK (execution_status IN (
        'PREFLIGHT', 'BLOCKED', 'REQUESTED', 'ACCEPTED', 'PROCESSING',
        'SUCCEEDED', 'FAILED', 'QA_FAILED', 'READY_FOR_DIRECTOR', 'APPROVED', 'REJECTED'
    )),
    CONSTRAINT ck_video_img_gen_exec_provider CHECK (provider = 'GEMINI'),
    CONSTRAINT ck_video_img_gen_exec_no_runway CHECK (provider <> 'RUNWAY')
);

CREATE TABLE IF NOT EXISTS pack_content.video_image_generation_execution_event (
    id                      UUID PRIMARY KEY,
    execution_id            UUID REFERENCES pack_content.video_image_generation_execution (id) ON DELETE RESTRICT,
    shot_id                 UUID NOT NULL REFERENCES pack_content.video_production_shot (id) ON DELETE RESTRICT,
    event_type              VARCHAR(40) NOT NULL,
    payload_json            JSONB NOT NULL DEFAULT '{}'::jsonb,
    actor                   VARCHAR(120),
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_video_img_gen_exec_shot
    ON pack_content.video_image_generation_execution (shot_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_video_img_gen_exec_event
    ON pack_content.video_image_generation_execution_event (shot_id, created_at DESC);

CREATE OR REPLACE FUNCTION pack_content.fn_video_img_gen_exec_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN
    IF OLD.execution_status IN ('SUCCEEDED', 'QA_FAILED', 'READY_FOR_DIRECTOR', 'APPROVED', 'REJECTED', 'FAILED') THEN
      RAISE EXCEPTION 'IMAGE_GENERATION_EXECUTION_LOCKED: artifact/fingerprint immutable.';
    END IF;
    RETURN OLD;
  END IF;
  IF OLD.execution_status IN ('READY_FOR_DIRECTOR')
     AND NEW.execution_status IN ('APPROVED', 'REJECTED')
     AND NEW.artifact_sha256 = OLD.artifact_sha256
     AND NEW.artifact_path IS NOT DISTINCT FROM OLD.artifact_path
     AND NEW.execution_fingerprint = OLD.execution_fingerprint THEN
    RETURN NEW;
  END IF;
  IF OLD.execution_status IN ('SUCCEEDED', 'QA_FAILED', 'READY_FOR_DIRECTOR', 'APPROVED', 'REJECTED', 'FAILED') THEN
    IF NEW.artifact_sha256 IS DISTINCT FROM OLD.artifact_sha256
       OR NEW.artifact_path IS DISTINCT FROM OLD.artifact_path
       OR NEW.execution_fingerprint IS DISTINCT FROM OLD.execution_fingerprint
       OR NEW.igc_sha256 IS DISTINCT FROM OLD.igc_sha256
       OR NEW.prompt_sha256 IS DISTINCT FROM OLD.prompt_sha256
       OR NEW.master_sha256 IS DISTINCT FROM OLD.master_sha256 THEN
      RAISE EXCEPTION 'IMAGE_GENERATION_EXECUTION_LOCKED: artifact/fingerprint immutable.';
    END IF;
    IF OLD.execution_status IN ('APPROVED', 'REJECTED') THEN
      RAISE EXCEPTION 'IMAGE_GENERATION_EXECUTION_LOCKED: Director decision immutable.';
    END IF;
  END IF;
  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_video_img_gen_exec_immutable ON pack_content.video_image_generation_execution;
CREATE TRIGGER trg_video_img_gen_exec_immutable
  BEFORE UPDATE OR DELETE ON pack_content.video_image_generation_execution
  FOR EACH ROW EXECUTE FUNCTION pack_content.fn_video_img_gen_exec_immutable();

CREATE OR REPLACE FUNCTION pack_content.fn_video_img_gen_exec_event_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'IMAGE_GENERATION_EXECUTION_LOCKED: audit event is immutable';
END;
$$;

DROP TRIGGER IF EXISTS trg_video_img_gen_exec_event_immutable ON pack_content.video_image_generation_execution_event;
CREATE TRIGGER trg_video_img_gen_exec_event_immutable
  BEFORE UPDATE OR DELETE ON pack_content.video_image_generation_execution_event
  FOR EACH ROW EXECUTE FUNCTION pack_content.fn_video_img_gen_exec_event_immutable();
