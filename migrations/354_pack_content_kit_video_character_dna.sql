-- KitPlatform 354: CHAR-001 Minh Character DNA V1 (derived from locked Master).
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Does not generate images. Does not modify Master V1. Does not touch Golden SH01-01.

CREATE TABLE IF NOT EXISTS pack_content.video_character_dna (
    id                      UUID PRIMARY KEY,
    dna_code                VARCHAR(80) NOT NULL,
    document_id             VARCHAR(80) NOT NULL DEFAULT 'CHAR-001_MINH_CHARACTER_DNA_V1',
    project_code            VARCHAR(32) NOT NULL DEFAULT 'FAMIXA',
    character_id            VARCHAR(32) NOT NULL DEFAULT 'CHAR-001',
    character_name          VARCHAR(32) NOT NULL DEFAULT 'MINH',
    era_id                  VARCHAR(16) NOT NULL DEFAULT 'ERA-01',
    master_reference_id     UUID NOT NULL REFERENCES pack_content.video_master_reference (id) ON DELETE RESTRICT,
    master_code             VARCHAR(80) NOT NULL,
    master_sha256           VARCHAR(64) NOT NULL,
    dna_version             VARCHAR(16) NOT NULL DEFAULT 'V1',
    status                  VARCHAR(24) NOT NULL DEFAULT 'DRAFT',
    spec_json               JSONB NOT NULL DEFAULT '{}'::jsonb,
    note                    TEXT NOT NULL DEFAULT '',
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    approved_at             TIMESTAMPTZ,
    approved_by             VARCHAR(120),
    locked_at               TIMESTAMPTZ,
    locked_by               VARCHAR(120),
    extra_json              JSONB NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT uq_video_character_dna_code UNIQUE (dna_code),
    CONSTRAINT uq_video_character_dna_version UNIQUE (character_id, era_id, dna_version),
    CONSTRAINT ck_video_character_dna_status CHECK (status IN ('DRAFT', 'APPROVED', 'LOCKED', 'REJECTED')),
    CONSTRAINT ck_video_character_dna_scope CHECK (
        project_code = 'FAMIXA' AND character_id = 'CHAR-001' AND era_id = 'ERA-01'
    )
);

CREATE TABLE IF NOT EXISTS pack_content.video_character_dna_event (
    id                      UUID PRIMARY KEY,
    dna_id                  UUID NOT NULL REFERENCES pack_content.video_character_dna (id) ON DELETE RESTRICT,
    event_type              VARCHAR(40) NOT NULL,
    payload_json            JSONB NOT NULL DEFAULT '{}'::jsonb,
    actor                   VARCHAR(120),
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_video_character_dna_event_dna
    ON pack_content.video_character_dna_event (dna_id, created_at DESC);

CREATE OR REPLACE FUNCTION pack_content.fn_video_character_dna_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN
    RAISE EXCEPTION 'DNA_LOCKED: cannot delete CHAR-001-MINH-ERA01-DNA-V1';
  END IF;
  IF OLD.status = 'LOCKED' THEN
    RAISE EXCEPTION 'DNA_LOCKED: V1 không overwrite. Dùng DNA-V2.';
  END IF;
  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_video_character_dna_immutable ON pack_content.video_character_dna;
CREATE TRIGGER trg_video_character_dna_immutable
  BEFORE UPDATE OR DELETE ON pack_content.video_character_dna
  FOR EACH ROW EXECUTE FUNCTION pack_content.fn_video_character_dna_immutable();

CREATE OR REPLACE FUNCTION pack_content.fn_video_character_dna_event_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'DNA_LOCKED: audit event is immutable';
END;
$$;

DROP TRIGGER IF EXISTS trg_video_character_dna_event_immutable ON pack_content.video_character_dna_event;
CREATE TRIGGER trg_video_character_dna_event_immutable
  BEFORE UPDATE OR DELETE ON pack_content.video_character_dna_event
  FOR EACH ROW EXECUTE FUNCTION pack_content.fn_video_character_dna_event_immutable();

COMMENT ON TABLE pack_content.video_character_dna IS
    'CHAR-001 ERA-01 Character DNA V1. Identity specification derived from locked Master. Not an image. Not a new Master.';

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.video_character_dna TO %I', r);
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.video_character_dna_event TO %I', r);
    END IF;
  END LOOP;
END $$;
