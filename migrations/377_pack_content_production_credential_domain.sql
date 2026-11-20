-- KitPlatform 377: Famixa Production credential domain (ADR-04 Gate A)
-- Manifest: deploy/ubuntu/migration-files.production-credentials.txt only
-- Tenant-owned credential identity and version identity.
-- Does not store a secret or ciphertext. Gate B owns secret storage.
-- Does not alter org_settings, production_tasks, production_attempts,
-- production_artifacts, production_bindings, or provider resolution.
-- Does not backfill. Does not copy legacy credentials.

BEGIN;

CREATE TABLE IF NOT EXISTS pack_content.production_credentials (
    credential_id   UUID PRIMARY KEY,
    tenant_id       UUID NOT NULL REFERENCES public.tenants (id),
    display_name    VARCHAR(200) NOT NULL,
    status          VARCHAR(24) NOT NULL DEFAULT 'ACTIVE',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_prod_credential_status CHECK (status IN ('ACTIVE', 'REVOKED'))
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_prod_credential_id_tenant
    ON pack_content.production_credentials (credential_id, tenant_id);

CREATE TABLE IF NOT EXISTS pack_content.production_credential_versions (
    credential_version_id   UUID PRIMARY KEY,
    credential_id           UUID NOT NULL,
    tenant_id               UUID NOT NULL REFERENCES public.tenants (id),
    version_number          INT NOT NULL,
    provider_id             VARCHAR(40),
    model_id                VARCHAR(80),
    status                  VARCHAR(24) NOT NULL DEFAULT 'ACTIVE',
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_prod_credential_version UNIQUE (credential_id, version_number),
    CONSTRAINT ck_prod_credential_version_number CHECK (version_number >= 1),
    CONSTRAINT ck_prod_credential_version_status CHECK (status IN ('ACTIVE', 'SUPERSEDED', 'REVOKED')),
    CONSTRAINT fk_prod_credential_version_owner
        FOREIGN KEY (credential_id, tenant_id)
        REFERENCES pack_content.production_credentials (credential_id, tenant_id)
);

CREATE INDEX IF NOT EXISTS ix_prod_credential_tenant
    ON pack_content.production_credentials (tenant_id, credential_id);
CREATE INDEX IF NOT EXISTS ix_prod_credential_version_tenant
    ON pack_content.production_credential_versions (tenant_id, credential_id, version_number);

COMMENT ON TABLE pack_content.production_credentials IS
    'ADR-04 Gate A. One Tenant owns the credential. No secret payload. Not a legacy settings row.';
COMMENT ON TABLE pack_content.production_credential_versions IS
    'ADR-04 Gate A. Version identity under one credential. Provider and model are metadata, not owners.';

CREATE OR REPLACE FUNCTION pack_content.fn_production_credential_owner()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'INSERT' AND NEW.tenant_id IS NULL THEN
        RAISE EXCEPTION 'PRODUCTION_TENANT_REQUIRED';
    END IF;
    IF TG_OP = 'UPDATE' AND NEW.tenant_id IS DISTINCT FROM OLD.tenant_id THEN
        RAISE EXCEPTION 'PRODUCTION_TENANT_IMMUTABLE';
    END IF;
    IF TG_OP = 'UPDATE' AND NEW.credential_id IS DISTINCT FROM OLD.credential_id THEN
        RAISE EXCEPTION 'PRODUCTION_CREDENTIAL_IDENTITY_IMMUTABLE';
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_production_credentials_owner ON pack_content.production_credentials;
CREATE TRIGGER trg_production_credentials_owner
    BEFORE INSERT OR UPDATE ON pack_content.production_credentials
    FOR EACH ROW EXECUTE FUNCTION pack_content.fn_production_credential_owner();

CREATE OR REPLACE FUNCTION pack_content.fn_production_credential_version_owner()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'INSERT' AND NEW.tenant_id IS NULL THEN
        RAISE EXCEPTION 'PRODUCTION_TENANT_REQUIRED';
    END IF;
    IF TG_OP = 'UPDATE' AND (
        NEW.credential_version_id IS DISTINCT FROM OLD.credential_version_id
        OR NEW.credential_id IS DISTINCT FROM OLD.credential_id
        OR NEW.tenant_id IS DISTINCT FROM OLD.tenant_id
        OR NEW.version_number IS DISTINCT FROM OLD.version_number
    ) THEN
        RAISE EXCEPTION 'PRODUCTION_CREDENTIAL_VERSION_OWNER_IMMUTABLE';
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_production_credential_versions_owner ON pack_content.production_credential_versions;
CREATE TRIGGER trg_production_credential_versions_owner
    BEFORE INSERT OR UPDATE ON pack_content.production_credential_versions
    FOR EACH ROW EXECUTE FUNCTION pack_content.fn_production_credential_version_owner();

ALTER TABLE pack_content.production_credential_versions
    DROP CONSTRAINT IF EXISTS ck_prod_credential_version_status;

ALTER TABLE pack_content.production_credential_versions
    ADD CONSTRAINT ck_prod_credential_version_status
    CHECK (status IN ('ACTIVE', 'SUPERSEDED', 'REVOKED'));

COMMIT;
