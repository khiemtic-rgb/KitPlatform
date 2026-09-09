-- KitPlatform 364: PRODUCTION_VIDEO_CONTRACT_V1
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Structured motion/timing contract anchored to a Director IMAGE_APPROVED still.
-- Does not generate video. Does not call Runway/Gemini. Does not modify Master/DNA/PRP/Contract/Prompt/IGC/Execution/Golden.

CREATE TABLE IF NOT EXISTS pack_content.video_production_video_contract (
    id                              UUID PRIMARY KEY,
    shot_id                         UUID NOT NULL REFERENCES pack_content.video_production_shot (id) ON DELETE RESTRICT,
    shot_contract_id                UUID NOT NULL REFERENCES pack_content.video_production_shot_contract (id) ON DELETE RESTRICT,
    still_execution_id              UUID NOT NULL REFERENCES pack_content.video_image_generation_execution (id) ON DELETE RESTRICT,
    series_id                       VARCHAR(40) NOT NULL DEFAULT 'FAMIXA',
    character_id                    VARCHAR(32) NOT NULL,
    era_id                          VARCHAR(16) NOT NULL,
    contract_version                VARCHAR(16) NOT NULL DEFAULT 'V1',
    contract_status                 VARCHAR(24) NOT NULL DEFAULT 'DRAFT',
    document_id                     VARCHAR(80) NOT NULL DEFAULT 'PRODUCTION_VIDEO_CONTRACT_V1',
    payload_json                    JSONB NOT NULL DEFAULT '{}'::jsonb,
    canonical_json                  TEXT NOT NULL DEFAULT '',
    contract_sha256                 VARCHAR(64) NOT NULL DEFAULT '',
    master_id                       UUID,
    master_sha256                   VARCHAR(64) NOT NULL DEFAULT '',
    dna_id                          UUID,
    dna_sha256                      VARCHAR(64) NOT NULL DEFAULT '',
    prp_id                          UUID,
    prp_sha256                      VARCHAR(64) NOT NULL DEFAULT '',
    shot_contract_sha256            VARCHAR(64) NOT NULL DEFAULT '',
    still_artifact_sha256           VARCHAR(64) NOT NULL DEFAULT '',
    created_at                      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by                      VARCHAR(120),
    updated_at                      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by                      VARCHAR(120),
    validated_at                    TIMESTAMPTZ,
    validated_by                    VARCHAR(120),
    approved_at                     TIMESTAMPTZ,
    approved_by                     VARCHAR(120),
    rejected_at                     TIMESTAMPTZ,
    rejected_by                     VARCHAR(120),
    rejection_reason                TEXT,
    superseded_by                   UUID,
    extra_json                      JSONB NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT uq_video_prod_video_contract_ver UNIQUE (shot_id, contract_version),
    CONSTRAINT ck_video_prod_video_contract_status CHECK (contract_status IN (
        'DRAFT', 'VALIDATED', 'DIRECTOR_APPROVED', 'REJECTED', 'SUPERSEDED'
    )),
    CONSTRAINT ck_video_prod_video_contract_no_gen CHECK (contract_status NOT IN (
        'GENERATED', 'VIDEO_READY', 'VIDEO_APPROVED', 'IMAGE_READY'
    ))
);

CREATE TABLE IF NOT EXISTS pack_content.video_production_video_contract_event (
    id                      UUID PRIMARY KEY,
    contract_id             UUID REFERENCES pack_content.video_production_video_contract (id) ON DELETE RESTRICT,
    shot_id                 UUID NOT NULL REFERENCES pack_content.video_production_shot (id) ON DELETE RESTRICT,
    event_type              VARCHAR(40) NOT NULL,
    payload_json            JSONB NOT NULL DEFAULT '{}'::jsonb,
    actor                   VARCHAR(120),
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_video_prod_video_contract_shot
    ON pack_content.video_production_video_contract (shot_id, contract_version DESC);

CREATE INDEX IF NOT EXISTS ix_video_prod_video_contract_event
    ON pack_content.video_production_video_contract_event (shot_id, created_at DESC);

CREATE OR REPLACE FUNCTION pack_content.fn_video_prod_video_contract_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN
    IF OLD.contract_status IN ('DIRECTOR_APPROVED', 'SUPERSEDED') THEN
      RAISE EXCEPTION 'VIDEO_CONTRACT_LOCKED: V1 không overwrite. Dùng V2.';
    END IF;
    RETURN OLD;
  END IF;
  IF OLD.contract_status = 'DIRECTOR_APPROVED'
     AND NEW.contract_status = 'SUPERSEDED'
     AND NEW.payload_json = OLD.payload_json
     AND NEW.contract_sha256 = OLD.contract_sha256
     AND NEW.canonical_json = OLD.canonical_json
     AND NEW.still_execution_id = OLD.still_execution_id
     AND NEW.still_artifact_sha256 = OLD.still_artifact_sha256 THEN
    RETURN NEW;
  END IF;
  IF OLD.contract_status IN ('DIRECTOR_APPROVED', 'SUPERSEDED') THEN
    RAISE EXCEPTION 'VIDEO_CONTRACT_LOCKED: V1 không overwrite. Dùng V2.';
  END IF;
  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_video_prod_video_contract_immutable ON pack_content.video_production_video_contract;
CREATE TRIGGER trg_video_prod_video_contract_immutable
  BEFORE UPDATE OR DELETE ON pack_content.video_production_video_contract
  FOR EACH ROW EXECUTE FUNCTION pack_content.fn_video_prod_video_contract_immutable();

CREATE OR REPLACE FUNCTION pack_content.fn_video_prod_video_contract_event_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'VIDEO_CONTRACT_LOCKED: audit event is immutable';
END;
$$;

DROP TRIGGER IF EXISTS trg_video_prod_video_contract_event_immutable ON pack_content.video_production_video_contract_event;
CREATE TRIGGER trg_video_prod_video_contract_event_immutable
  BEFORE UPDATE OR DELETE ON pack_content.video_production_video_contract_event
  FOR EACH ROW EXECUTE FUNCTION pack_content.fn_video_prod_video_contract_event_immutable();
