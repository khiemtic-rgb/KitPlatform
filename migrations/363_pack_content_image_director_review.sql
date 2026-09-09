-- KitPlatform 363: PRODUCTION_IMAGE_DIRECTOR_REVIEW_V1
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Director review of an existing READY_FOR_DIRECTOR production still.
-- Does not generate. Does not call Gemini/Runway. Does not modify Master/DNA/PRP/Contract/Prompt/IGC/Golden.

ALTER TABLE pack_content.video_image_generation_execution
    DROP CONSTRAINT IF EXISTS ck_video_img_gen_exec_status;

ALTER TABLE pack_content.video_image_generation_execution
    ADD CONSTRAINT ck_video_img_gen_exec_status CHECK (execution_status IN (
        'PREFLIGHT', 'BLOCKED', 'REQUESTED', 'ACCEPTED', 'PROCESSING',
        'SUCCEEDED', 'FAILED', 'QA_FAILED', 'READY_FOR_DIRECTOR',
        'APPROVED', 'REJECTED', 'IMAGE_APPROVED', 'IMAGE_REJECTED'
    ));

CREATE OR REPLACE FUNCTION pack_content.fn_video_img_gen_exec_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN
    IF OLD.execution_status IN ('SUCCEEDED', 'QA_FAILED', 'READY_FOR_DIRECTOR', 'APPROVED', 'REJECTED', 'FAILED', 'IMAGE_APPROVED', 'IMAGE_REJECTED') THEN
      RAISE EXCEPTION 'IMAGE_GENERATION_EXECUTION_LOCKED: artifact/fingerprint immutable.';
    END IF;
    RETURN OLD;
  END IF;
  IF OLD.execution_status = 'READY_FOR_DIRECTOR'
     AND NEW.execution_status IN ('APPROVED', 'REJECTED', 'IMAGE_APPROVED', 'IMAGE_REJECTED')
     AND NEW.artifact_sha256 = OLD.artifact_sha256
     AND NEW.artifact_path IS NOT DISTINCT FROM OLD.artifact_path
     AND NEW.execution_fingerprint = OLD.execution_fingerprint
     AND NEW.igc_sha256 = OLD.igc_sha256
     AND NEW.prompt_sha256 = OLD.prompt_sha256
     AND NEW.master_sha256 = OLD.master_sha256
     AND NEW.dna_sha256 = OLD.dna_sha256
     AND NEW.prp_sha256 = OLD.prp_sha256 THEN
    RETURN NEW;
  END IF;
  IF OLD.execution_status IN ('SUCCEEDED', 'QA_FAILED', 'READY_FOR_DIRECTOR', 'APPROVED', 'REJECTED', 'FAILED', 'IMAGE_APPROVED', 'IMAGE_REJECTED') THEN
    IF NEW.artifact_sha256 IS DISTINCT FROM OLD.artifact_sha256
       OR NEW.artifact_path IS DISTINCT FROM OLD.artifact_path
       OR NEW.execution_fingerprint IS DISTINCT FROM OLD.execution_fingerprint
       OR NEW.igc_sha256 IS DISTINCT FROM OLD.igc_sha256
       OR NEW.prompt_sha256 IS DISTINCT FROM OLD.prompt_sha256
       OR NEW.master_sha256 IS DISTINCT FROM OLD.master_sha256 THEN
      RAISE EXCEPTION 'IMAGE_GENERATION_EXECUTION_LOCKED: artifact/fingerprint immutable.';
    END IF;
    IF OLD.execution_status IN ('APPROVED', 'REJECTED', 'IMAGE_APPROVED', 'IMAGE_REJECTED') THEN
      RAISE EXCEPTION 'IMAGE_GENERATION_EXECUTION_LOCKED: Director decision immutable.';
    END IF;
  END IF;
  RETURN NEW;
END;
$$;

CREATE TABLE IF NOT EXISTS pack_content.video_image_director_review (
    id                                  UUID PRIMARY KEY,
    shot_id                             UUID NOT NULL REFERENCES pack_content.video_production_shot (id) ON DELETE RESTRICT,
    execution_id                        UUID NOT NULL REFERENCES pack_content.video_image_generation_execution (id) ON DELETE RESTRICT,
    review_version                      VARCHAR(16) NOT NULL DEFAULT 'V1',
    review_status                       VARCHAR(24) NOT NULL DEFAULT 'PENDING',
    document_id                         VARCHAR(80) NOT NULL DEFAULT 'PRODUCTION_IMAGE_DIRECTOR_REVIEW_V1',
    series_id                           VARCHAR(40) NOT NULL DEFAULT 'FAMIXA',
    character_id                        VARCHAR(32) NOT NULL,
    era_id                              VARCHAR(16) NOT NULL,
    master_sha256                       VARCHAR(64) NOT NULL DEFAULT '',
    dna_sha256                          VARCHAR(64) NOT NULL DEFAULT '',
    prp_sha256                          VARCHAR(64) NOT NULL DEFAULT '',
    shot_contract_sha256                VARCHAR(64) NOT NULL DEFAULT '',
    prompt_sha256                       VARCHAR(64) NOT NULL DEFAULT '',
    image_generation_contract_sha256    VARCHAR(64) NOT NULL DEFAULT '',
    artifact_sha256                     VARCHAR(64) NOT NULL DEFAULT '',
    director_approval                   VARCHAR(16) NOT NULL DEFAULT 'PENDING',
    director_id                         VARCHAR(120),
    director_at                         TIMESTAMPTZ,
    rejection_reason                    TEXT,
    created_at                          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by                          VARCHAR(120),
    updated_at                          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by                          VARCHAR(120),
    extra_json                          JSONB NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT uq_video_img_dir_review_ver UNIQUE (execution_id, review_version),
    CONSTRAINT ck_video_img_dir_review_status CHECK (review_status IN (
        'PENDING', 'APPROVED', 'REJECTED', 'SUPERSEDED'
    )),
    CONSTRAINT ck_video_img_dir_review_no_gen CHECK (review_status NOT IN (
        'GENERATED', 'VIDEO_READY', 'IMAGE_READY'
    ))
);

CREATE TABLE IF NOT EXISTS pack_content.video_image_director_review_event (
    id                      UUID PRIMARY KEY,
    review_id               UUID REFERENCES pack_content.video_image_director_review (id) ON DELETE RESTRICT,
    execution_id            UUID NOT NULL REFERENCES pack_content.video_image_generation_execution (id) ON DELETE RESTRICT,
    shot_id                 UUID NOT NULL REFERENCES pack_content.video_production_shot (id) ON DELETE RESTRICT,
    event_type              VARCHAR(40) NOT NULL,
    payload_json            JSONB NOT NULL DEFAULT '{}'::jsonb,
    actor                   VARCHAR(120),
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_video_img_dir_review_shot
    ON pack_content.video_image_director_review (shot_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_video_img_dir_review_event
    ON pack_content.video_image_director_review_event (shot_id, created_at DESC);

CREATE OR REPLACE FUNCTION pack_content.fn_video_img_dir_review_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN
    IF OLD.review_status IN ('APPROVED', 'REJECTED', 'SUPERSEDED') THEN
      RAISE EXCEPTION 'IMAGE_DIRECTOR_REVIEW_LOCKED: review history immutable.';
    END IF;
    RETURN OLD;
  END IF;
  IF OLD.review_status IN ('APPROVED', 'REJECTED', 'SUPERSEDED') THEN
    IF NEW.artifact_sha256 IS DISTINCT FROM OLD.artifact_sha256
       OR NEW.master_sha256 IS DISTINCT FROM OLD.master_sha256
       OR NEW.dna_sha256 IS DISTINCT FROM OLD.dna_sha256
       OR NEW.prp_sha256 IS DISTINCT FROM OLD.prp_sha256
       OR NEW.shot_contract_sha256 IS DISTINCT FROM OLD.shot_contract_sha256
       OR NEW.prompt_sha256 IS DISTINCT FROM OLD.prompt_sha256
       OR NEW.image_generation_contract_sha256 IS DISTINCT FROM OLD.image_generation_contract_sha256
       OR NEW.execution_id IS DISTINCT FROM OLD.execution_id THEN
      RAISE EXCEPTION 'IMAGE_DIRECTOR_REVIEW_LOCKED: artifact/provenance immutable.';
    END IF;
    IF OLD.review_status IN ('APPROVED', 'REJECTED') AND NEW.review_status = OLD.review_status THEN
      RAISE EXCEPTION 'IMAGE_DIRECTOR_REVIEW_LOCKED: Director decision immutable.';
    END IF;
    IF OLD.review_status IN ('APPROVED', 'REJECTED') AND NEW.review_status NOT IN ('SUPERSEDED') THEN
      RAISE EXCEPTION 'IMAGE_DIRECTOR_REVIEW_LOCKED: Director decision immutable.';
    END IF;
  END IF;
  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_video_img_dir_review_immutable ON pack_content.video_image_director_review;
CREATE TRIGGER trg_video_img_dir_review_immutable
  BEFORE UPDATE OR DELETE ON pack_content.video_image_director_review
  FOR EACH ROW EXECUTE FUNCTION pack_content.fn_video_img_dir_review_immutable();

CREATE OR REPLACE FUNCTION pack_content.fn_video_img_dir_review_event_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'IMAGE_DIRECTOR_REVIEW_LOCKED: audit event is immutable';
END;
$$;

DROP TRIGGER IF EXISTS trg_video_img_dir_review_event_immutable ON pack_content.video_image_director_review_event;
CREATE TRIGGER trg_video_img_dir_review_event_immutable
  BEFORE UPDATE OR DELETE ON pack_content.video_image_director_review_event
  FOR EACH ROW EXECUTE FUNCTION pack_content.fn_video_img_dir_review_event_immutable();
