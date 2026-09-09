-- KitPlatform 357: Famixa Character Identity Governance V1 (all characters).
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Governance only. Does not generate images/video. Does not modify locked Master/DNA/PRP. Does not touch Golden SH01-01.

CREATE TABLE IF NOT EXISTS pack_content.video_identity_governance_audit (
    id                      UUID PRIMARY KEY,
    project_code            VARCHAR(32) NOT NULL DEFAULT 'FAMIXA',
    character_id            VARCHAR(32) NOT NULL,
    era_id                  VARCHAR(16) NOT NULL DEFAULT 'ERA-01',
    master_id               UUID,
    dna_id                  UUID,
    prp_id                  UUID,
    shot_id                 UUID,
    gate                    VARCHAR(40) NOT NULL,
    result                  VARCHAR(16) NOT NULL,
    code                    VARCHAR(64),
    source                  VARCHAR(40),
    attribute               VARCHAR(80),
    requested_value         TEXT,
    authoritative_value     TEXT,
    reason                  TEXT NOT NULL DEFAULT '',
    actor                   VARCHAR(120),
    payload_json            JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_video_gov_result CHECK (result IN ('PASS', 'FAIL', 'BLOCKED')),
    CONSTRAINT ck_video_gov_project CHECK (project_code = 'FAMIXA')
);

CREATE INDEX IF NOT EXISTS ix_video_gov_audit_char
    ON pack_content.video_identity_governance_audit (character_id, era_id, created_at DESC);

CREATE OR REPLACE FUNCTION pack_content.fn_video_gov_audit_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'GOVERNANCE_LOCKED: audit trail is immutable';
END;
$$;

DROP TRIGGER IF EXISTS trg_video_gov_audit_immutable ON pack_content.video_identity_governance_audit;
CREATE TRIGGER trg_video_gov_audit_immutable
  BEFORE UPDATE OR DELETE ON pack_content.video_identity_governance_audit
  FOR EACH ROW EXECUTE FUNCTION pack_content.fn_video_gov_audit_immutable();

COMMENT ON TABLE pack_content.video_identity_governance_audit IS
    'Famixa Character Identity Governance V1. Detect / report / block only. Not an image generator.';

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT SELECT, INSERT ON pack_content.video_identity_governance_audit TO %I', r);
    END IF;
  END LOOP;
END $$;
