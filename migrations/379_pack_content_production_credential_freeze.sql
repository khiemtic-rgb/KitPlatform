-- KitPlatform 379: Famixa Production credential freeze (ADR-04 Gate D)
-- Manifest: deploy/ubuntu/migration-files.production-credentials.txt only
-- Pins non-secret credential_id and credential_version_id on the existing input snapshot.
-- Does not store a secret, ciphertext, or encryption key.
-- Does not backfill historical snapshots. Existing rows stay NULL.
-- Does not alter org_settings.

BEGIN;

ALTER TABLE pack_content.production_task_input_snapshots
    ADD COLUMN IF NOT EXISTS credential_id UUID,
    ADD COLUMN IF NOT EXISTS credential_version_id UUID;

ALTER TABLE pack_content.production_task_input_snapshots
    DROP CONSTRAINT IF EXISTS ck_prod_snapshot_credential_pair;

ALTER TABLE pack_content.production_task_input_snapshots
    ADD CONSTRAINT ck_prod_snapshot_credential_pair CHECK (
        (credential_id IS NULL AND credential_version_id IS NULL)
        OR (credential_id IS NOT NULL AND credential_version_id IS NOT NULL)
    );

CREATE UNIQUE INDEX IF NOT EXISTS uq_prod_credential_version_identity
    ON pack_content.production_credential_versions (credential_id, credential_version_id);

ALTER TABLE pack_content.production_task_input_snapshots
    DROP CONSTRAINT IF EXISTS fk_prod_snapshot_credential_version;

ALTER TABLE pack_content.production_task_input_snapshots
    ADD CONSTRAINT fk_prod_snapshot_credential_version
    FOREIGN KEY (credential_id, credential_version_id)
    REFERENCES pack_content.production_credential_versions (credential_id, credential_version_id);

CREATE OR REPLACE FUNCTION pack_content.fn_prod_snapshot_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN
    RAISE EXCEPTION 'PRODUCTION_SNAPSHOT_LOCKED: input snapshot is immutable';
  END IF;
  IF NEW.snapshot_hash IS DISTINCT FROM OLD.snapshot_hash
     OR NEW.story IS DISTINCT FROM OLD.story
     OR NEW.compiler_output IS DISTINCT FROM OLD.compiler_output
     OR NEW.keyframe_hash IS DISTINCT FROM OLD.keyframe_hash
     OR NEW.credential_id IS DISTINCT FROM OLD.credential_id
     OR NEW.credential_version_id IS DISTINCT FROM OLD.credential_version_id THEN
    RAISE EXCEPTION 'PRODUCTION_SNAPSHOT_LOCKED: input snapshot is immutable';
  END IF;
  RETURN NEW;
END;
$$;

COMMENT ON COLUMN pack_content.production_task_input_snapshots.credential_id IS
    'Non-secret credential identity pinned at freeze. NULL on historical snapshots. Not a secret.';
COMMENT ON COLUMN pack_content.production_task_input_snapshots.credential_version_id IS
    'Non-secret credential version pinned at freeze. NULL on historical snapshots. Not a secret.';

COMMIT;
