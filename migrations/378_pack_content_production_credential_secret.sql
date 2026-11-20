-- KitPlatform 378: Famixa Production credential ciphertext (ADR-04 Gate B)
-- Manifest: deploy/ubuntu/migration-files.production-credentials.txt only
-- AES-256-GCM envelope on production_credential_versions.
-- Envelope version, key id, nonce, ciphertext, and tag.
-- The data-encryption key is not stored here.
-- Does not alter org_settings or production execution tables.
-- Does not backfill and does not copy legacy secrets.

BEGIN;

ALTER TABLE pack_content.production_credential_versions
    ADD COLUMN IF NOT EXISTS secret_envelope_version INT,
    ADD COLUMN IF NOT EXISTS secret_key_id VARCHAR(64),
    ADD COLUMN IF NOT EXISTS secret_nonce BYTEA,
    ADD COLUMN IF NOT EXISTS secret_ciphertext BYTEA,
    ADD COLUMN IF NOT EXISTS secret_tag BYTEA;

ALTER TABLE pack_content.production_credential_versions
    DROP CONSTRAINT IF EXISTS ck_prod_credential_secret_envelope;

ALTER TABLE pack_content.production_credential_versions
    ADD CONSTRAINT ck_prod_credential_secret_envelope CHECK (
        (
            secret_envelope_version IS NULL
            AND secret_key_id IS NULL
            AND secret_nonce IS NULL
            AND secret_ciphertext IS NULL
            AND secret_tag IS NULL
        )
        OR (
            secret_envelope_version = 1
            AND secret_key_id IS NOT NULL
            AND length(btrim(secret_key_id)) > 0
            AND octet_length(secret_nonce) = 12
            AND octet_length(secret_tag) = 16
            AND octet_length(secret_ciphertext) > 0
        )
    );

COMMENT ON COLUMN pack_content.production_credential_versions.secret_ciphertext IS
    'AES-256-GCM ciphertext for this credential version. Not plaintext. Not a credential identity.';
COMMENT ON COLUMN pack_content.production_credential_versions.secret_key_id IS
    'Non-secret data-encryption key id required to decrypt secret_ciphertext. Not the key.';

COMMIT;
