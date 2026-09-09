-- KitPlatform 331: Famixa Character change audit (creation workspace)
-- Manifest: deploy/ubuntu/migration-files.content.txt only. Does not overwrite Canon rows.

CREATE TABLE IF NOT EXISTS pack_content.famixa_character_audit (
    id             UUID PRIMARY KEY,
    character_id   UUID NOT NULL REFERENCES pack_content.famixa_character (id) ON DELETE RESTRICT,
    character_code VARCHAR(32) NOT NULL,
    version        VARCHAR(16) NOT NULL DEFAULT 'V1',
    field_changed  VARCHAR(80) NOT NULL,
    old_value      TEXT,
    new_value      TEXT,
    changed_by     VARCHAR(120),
    changed_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    reason         TEXT,
    approval       VARCHAR(24)
);

CREATE INDEX IF NOT EXISTS ix_famixa_character_audit_char
    ON pack_content.famixa_character_audit (character_code, changed_at DESC);

COMMENT ON TABLE pack_content.famixa_character_audit IS
    'Who changed which Character Canon field. LOCKED edits must go through a new version.';

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.famixa_character_audit TO %I', r);
    END IF;
  END LOOP;
END $$;
