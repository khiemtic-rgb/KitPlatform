-- KitPlatform 366: CHARACTER_REFERENCE_PACK_V1
-- Visual representation of locked Character DNA. Not DNA. Not PRP. Not a production shot.
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Does not generate images or video. Does not modify Master V1, DNA V1, PRP V1, or Golden SH01-01.

CREATE TABLE IF NOT EXISTS pack_content.video_character_reference_pack (
    id                      UUID PRIMARY KEY,
    pack_code               VARCHAR(96) NOT NULL,
    document_id             VARCHAR(80) NOT NULL DEFAULT 'CHARACTER_REFERENCE_PACK_V1',
    project_code            VARCHAR(32) NOT NULL DEFAULT 'FAMIXA',
    character_id            VARCHAR(32) NOT NULL,
    character_name          VARCHAR(80) NOT NULL DEFAULT '',
    era_id                  VARCHAR(16) NOT NULL DEFAULT 'ERA-01',
    pack_version            VARCHAR(16) NOT NULL DEFAULT 'V1',
    master_reference_id     UUID NOT NULL REFERENCES pack_content.video_master_reference (id) ON DELETE RESTRICT,
    master_sha256           VARCHAR(64) NOT NULL,
    character_dna_id        UUID NOT NULL REFERENCES pack_content.video_character_dna (id) ON DELETE RESTRICT,
    dna_sha256              VARCHAR(64) NOT NULL,
    status                  VARCHAR(24) NOT NULL DEFAULT 'DRAFT',
    notes                   TEXT NOT NULL DEFAULT '',
    created_by              VARCHAR(120),
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    approved_by             VARCHAR(120),
    approved_at             TIMESTAMPTZ,
    locked_at               TIMESTAMPTZ,
    locked_by               VARCHAR(120),
    pack_sha256             VARCHAR(64),
    supersedes_id           UUID REFERENCES pack_content.video_character_reference_pack (id) ON DELETE RESTRICT,
    extra_json              JSONB NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT uq_video_crp_code UNIQUE (pack_code),
    CONSTRAINT uq_video_crp_version UNIQUE (character_id, era_id, pack_version),
    CONSTRAINT ck_video_crp_status CHECK (status IN ('DRAFT', 'REVIEW', 'APPROVED', 'LOCKED', 'REJECTED'))
);

CREATE TABLE IF NOT EXISTS pack_content.video_character_reference_item (
    id                      UUID PRIMARY KEY,
    pack_id                 UUID NOT NULL REFERENCES pack_content.video_character_reference_pack (id) ON DELETE RESTRICT,
    ref_type                VARCHAR(40) NOT NULL,
    required                BOOLEAN NOT NULL DEFAULT FALSE,
    artifact_path           VARCHAR(400) NOT NULL DEFAULT '',
    artifact_sha256         VARCHAR(64) NOT NULL DEFAULT '',
    status                  VARCHAR(24) NOT NULL DEFAULT 'MISSING',
    metadata_json           JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_video_crp_item UNIQUE (pack_id, ref_type)
);

CREATE TABLE IF NOT EXISTS pack_content.video_character_reference_pack_event (
    id                      UUID PRIMARY KEY,
    pack_id                 UUID NOT NULL REFERENCES pack_content.video_character_reference_pack (id) ON DELETE RESTRICT,
    event_type              VARCHAR(48) NOT NULL,
    payload_json            JSONB NOT NULL DEFAULT '{}'::jsonb,
    actor                   VARCHAR(120),
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_video_crp_character
    ON pack_content.video_character_reference_pack (character_id, era_id, pack_version);
CREATE INDEX IF NOT EXISTS ix_video_crp_item_pack
    ON pack_content.video_character_reference_item (pack_id);
CREATE INDEX IF NOT EXISTS ix_video_crp_event_pack
    ON pack_content.video_character_reference_pack_event (pack_id, created_at DESC);

CREATE OR REPLACE FUNCTION pack_content.fn_video_crp_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN
    RAISE EXCEPTION 'CRP_LOCKED: cannot delete a Character Reference Pack';
  END IF;
  IF OLD.status = 'LOCKED' THEN
    RAISE EXCEPTION 'CRP_LOCKED: V1 không overwrite. Tạo REFERENCE PACK V2.';
  END IF;
  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_video_crp_immutable ON pack_content.video_character_reference_pack;
CREATE TRIGGER trg_video_crp_immutable
  BEFORE UPDATE OR DELETE ON pack_content.video_character_reference_pack
  FOR EACH ROW EXECUTE FUNCTION pack_content.fn_video_crp_immutable();

CREATE OR REPLACE FUNCTION pack_content.fn_video_crp_item_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE
  pack_status VARCHAR(24);
BEGIN
  SELECT status INTO pack_status FROM pack_content.video_character_reference_pack WHERE id = COALESCE(NEW.pack_id, OLD.pack_id);
  IF pack_status = 'LOCKED' THEN
    RAISE EXCEPTION 'CRP_LOCKED: không sửa reference khi pack đã khóa';
  END IF;
  IF TG_OP = 'DELETE' THEN
    RETURN OLD;
  END IF;
  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_video_crp_item_immutable ON pack_content.video_character_reference_item;
CREATE TRIGGER trg_video_crp_item_immutable
  BEFORE UPDATE OR DELETE ON pack_content.video_character_reference_item
  FOR EACH ROW EXECUTE FUNCTION pack_content.fn_video_crp_item_immutable();

CREATE OR REPLACE FUNCTION pack_content.fn_video_crp_event_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'CRP_LOCKED: audit event is immutable';
END;
$$;

DROP TRIGGER IF EXISTS trg_video_crp_event_immutable ON pack_content.video_character_reference_pack_event;
CREATE TRIGGER trg_video_crp_event_immutable
  BEFORE UPDATE OR DELETE ON pack_content.video_character_reference_pack_event
  FOR EACH ROW EXECUTE FUNCTION pack_content.fn_video_crp_event_immutable();

COMMENT ON TABLE pack_content.video_character_reference_pack IS
    'CHARACTER_REFERENCE_PACK_V1. Visual representation of locked DNA. Authority: MASTER > DNA > REFERENCE > PRODUCTION.';

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.video_character_reference_pack TO %I', r);
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.video_character_reference_item TO %I', r);
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.video_character_reference_pack_event TO %I', r);
    END IF;
  END LOOP;
END $$;
