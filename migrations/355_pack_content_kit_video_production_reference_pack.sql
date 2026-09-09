-- KitPlatform 355: CHAR-001 Minh Production Reference Pack V1 (uses locked Master + locked DNA).
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Does not generate images or video. Does not modify Master V1 or DNA V1. Does not touch Golden SH01-01.

CREATE TABLE IF NOT EXISTS pack_content.video_production_reference_pack (
    id                      UUID PRIMARY KEY,
    pack_code               VARCHAR(80) NOT NULL,
    document_id             VARCHAR(80) NOT NULL DEFAULT 'CHAR-001_MINH_PRODUCTION_REFERENCE_PACK_V1',
    project_code            VARCHAR(32) NOT NULL DEFAULT 'FAMIXA',
    character_id            VARCHAR(32) NOT NULL DEFAULT 'CHAR-001',
    character_name          VARCHAR(32) NOT NULL DEFAULT 'MINH',
    era_id                  VARCHAR(16) NOT NULL DEFAULT 'ERA-01',
    master_reference_id     UUID NOT NULL REFERENCES pack_content.video_master_reference (id) ON DELETE RESTRICT,
    master_code             VARCHAR(80) NOT NULL,
    master_sha256           VARCHAR(64) NOT NULL,
    character_dna_id        UUID NOT NULL REFERENCES pack_content.video_character_dna (id) ON DELETE RESTRICT,
    dna_code                VARCHAR(80) NOT NULL,
    dna_sha256              VARCHAR(64) NOT NULL,
    pack_version            VARCHAR(16) NOT NULL DEFAULT 'V1',
    status                  VARCHAR(24) NOT NULL DEFAULT 'DRAFT',
    spec_json               JSONB NOT NULL DEFAULT '{}'::jsonb,
    note                    TEXT NOT NULL DEFAULT '',
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    approved_at             TIMESTAMPTZ,
    approved_by             VARCHAR(120),
    locked_at               TIMESTAMPTZ,
    locked_by               VARCHAR(120),
    extra_json              JSONB NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT uq_video_prp_code UNIQUE (pack_code),
    CONSTRAINT uq_video_prp_version UNIQUE (character_id, era_id, pack_version),
    CONSTRAINT ck_video_prp_status CHECK (status IN ('DRAFT', 'APPROVED', 'LOCKED', 'REJECTED')),
    CONSTRAINT ck_video_prp_scope CHECK (
        project_code = 'FAMIXA' AND character_id = 'CHAR-001' AND era_id = 'ERA-01'
    )
);

CREATE TABLE IF NOT EXISTS pack_content.video_production_reference_pack_event (
    id                      UUID PRIMARY KEY,
    pack_id                 UUID NOT NULL REFERENCES pack_content.video_production_reference_pack (id) ON DELETE RESTRICT,
    event_type              VARCHAR(40) NOT NULL,
    payload_json            JSONB NOT NULL DEFAULT '{}'::jsonb,
    actor                   VARCHAR(120),
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_video_prp_event_pack
    ON pack_content.video_production_reference_pack_event (pack_id, created_at DESC);

CREATE OR REPLACE FUNCTION pack_content.fn_video_prp_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN
    RAISE EXCEPTION 'PRP_LOCKED: cannot delete CHAR-001-MINH-ERA01-PROD-REF-V1';
  END IF;
  IF OLD.status = 'LOCKED' THEN
    RAISE EXCEPTION 'PRP_LOCKED: V1 không overwrite. Dùng PROD-REF-V2.';
  END IF;
  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_video_prp_immutable ON pack_content.video_production_reference_pack;
CREATE TRIGGER trg_video_prp_immutable
  BEFORE UPDATE OR DELETE ON pack_content.video_production_reference_pack
  FOR EACH ROW EXECUTE FUNCTION pack_content.fn_video_prp_immutable();

CREATE OR REPLACE FUNCTION pack_content.fn_video_prp_event_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'PRP_LOCKED: audit event is immutable';
END;
$$;

DROP TRIGGER IF EXISTS trg_video_prp_event_immutable ON pack_content.video_production_reference_pack_event;
CREATE TRIGGER trg_video_prp_event_immutable
  BEFORE UPDATE OR DELETE ON pack_content.video_production_reference_pack_event
  FOR EACH ROW EXECUTE FUNCTION pack_content.fn_video_prp_event_immutable();

COMMENT ON TABLE pack_content.video_production_reference_pack IS
    'CHAR-001 ERA-01 Production Reference Pack V1. Uses locked Master + locked DNA. Not a new identity. Not an image.';

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.video_production_reference_pack TO %I', r);
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.video_production_reference_pack_event TO %I', r);
    END IF;
  END LOOP;
END $$;
