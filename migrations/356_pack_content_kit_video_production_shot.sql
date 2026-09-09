-- KitPlatform 356: CHAR-001 Minh Production Shot Specification V1 (uses locked Master + DNA + PRP).
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Shot spec only. Does not generate images or video. Does not modify Master/DNA/PRP. Does not touch Golden SH01-01.

CREATE TABLE IF NOT EXISTS pack_content.video_production_shot (
    id                      UUID PRIMARY KEY,
    shot_code               VARCHAR(80) NOT NULL,
    document_id             VARCHAR(80) NOT NULL DEFAULT 'CHAR-001_MINH_PRODUCTION_SHOT_SPEC_V1',
    project_code            VARCHAR(32) NOT NULL DEFAULT 'FAMIXA',
    character_id            VARCHAR(32) NOT NULL DEFAULT 'CHAR-001',
    character_name          VARCHAR(32) NOT NULL DEFAULT 'MINH',
    era_id                  VARCHAR(16) NOT NULL DEFAULT 'ERA-01',
    shot_seq                INTEGER NOT NULL DEFAULT 1,
    shot_version            VARCHAR(16) NOT NULL DEFAULT 'V1',
    shot_status             VARCHAR(24) NOT NULL DEFAULT 'DRAFT',
    master_reference_id     UUID NOT NULL REFERENCES pack_content.video_master_reference (id) ON DELETE RESTRICT,
    master_code             VARCHAR(80) NOT NULL,
    master_sha256           VARCHAR(64) NOT NULL,
    character_dna_id        UUID NOT NULL REFERENCES pack_content.video_character_dna (id) ON DELETE RESTRICT,
    dna_code                VARCHAR(80) NOT NULL,
    dna_sha256              VARCHAR(64) NOT NULL,
    production_pack_id      UUID NOT NULL REFERENCES pack_content.video_production_reference_pack (id) ON DELETE RESTRICT,
    prp_code                VARCHAR(80) NOT NULL,
    prp_sha256              VARCHAR(64) NOT NULL,
    spec_json               JSONB NOT NULL DEFAULT '{}'::jsonb,
    note                    TEXT NOT NULL DEFAULT '',
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by              VARCHAR(120),
    approved_at             TIMESTAMPTZ,
    approved_by             VARCHAR(120),
    locked_at               TIMESTAMPTZ,
    locked_by               VARCHAR(120),
    extra_json              JSONB NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT uq_video_prod_shot_code UNIQUE (shot_code, shot_version),
    CONSTRAINT uq_video_prod_shot_seq UNIQUE (character_id, era_id, shot_seq, shot_version),
    CONSTRAINT ck_video_prod_shot_status CHECK (shot_status IN (
        'DRAFT', 'IDENTITY_CHECK', 'DIRECTOR_REVIEW', 'APPROVED', 'LOCKED', 'REJECTED'
    )),
    CONSTRAINT ck_video_prod_shot_scope CHECK (
        project_code = 'FAMIXA' AND character_id = 'CHAR-001' AND era_id = 'ERA-01'
    )
);

CREATE TABLE IF NOT EXISTS pack_content.video_production_shot_event (
    id                      UUID PRIMARY KEY,
    shot_id                 UUID NOT NULL REFERENCES pack_content.video_production_shot (id) ON DELETE RESTRICT,
    event_type              VARCHAR(40) NOT NULL,
    payload_json            JSONB NOT NULL DEFAULT '{}'::jsonb,
    actor                   VARCHAR(120),
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_video_prod_shot_char
    ON pack_content.video_production_shot (character_id, era_id, shot_seq DESC);

CREATE INDEX IF NOT EXISTS ix_video_prod_shot_event
    ON pack_content.video_production_shot_event (shot_id, created_at DESC);

CREATE OR REPLACE FUNCTION pack_content.fn_video_prod_shot_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN
    RAISE EXCEPTION 'SHOT_LOCKED: cannot delete CHAR-001 production shot V1';
  END IF;
  IF OLD.shot_status = 'LOCKED' THEN
    RAISE EXCEPTION 'SHOT_LOCKED: V1 không overwrite. Dùng SHOT-V2.';
  END IF;
  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_video_prod_shot_immutable ON pack_content.video_production_shot;
CREATE TRIGGER trg_video_prod_shot_immutable
  BEFORE UPDATE OR DELETE ON pack_content.video_production_shot
  FOR EACH ROW EXECUTE FUNCTION pack_content.fn_video_prod_shot_immutable();

CREATE OR REPLACE FUNCTION pack_content.fn_video_prod_shot_event_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'SHOT_LOCKED: audit event is immutable';
END;
$$;

DROP TRIGGER IF EXISTS trg_video_prod_shot_event_immutable ON pack_content.video_production_shot_event;
CREATE TRIGGER trg_video_prod_shot_event_immutable
  BEFORE UPDATE OR DELETE ON pack_content.video_production_shot_event
  FOR EACH ROW EXECUTE FUNCTION pack_content.fn_video_prod_shot_event_immutable();

COMMENT ON TABLE pack_content.video_production_shot IS
    'CHAR-001 ERA-01 Production Shot Specification V1. Uses locked Master + DNA + PRP. Not an image or video.';

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.video_production_shot TO %I', r);
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.video_production_shot_event TO %I', r);
    END IF;
  END LOOP;
END $$;
