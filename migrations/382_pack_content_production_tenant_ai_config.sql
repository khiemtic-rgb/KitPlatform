-- KitPlatform 382: Tenant-owned production AI configuration (defaults per capability)
-- Manifest: deploy/ubuntu/migration-files.content.txt only
-- One row per (tenant, capability). Credential must belong to the same tenant.
-- Does not read, alter, copy or drop pack_content.org_settings.
-- Does not backfill. Does not touch production_ai_selection rows.

BEGIN;

CREATE TABLE IF NOT EXISTS pack_content.production_tenant_ai_config (
    tenant_id       UUID NOT NULL REFERENCES public.tenants (id),
    capability      VARCHAR(16) NOT NULL,
    provider_id     VARCHAR(64) NOT NULL,
    model_id        VARCHAR(80) NOT NULL,
    credential_id   UUID NULL,
    enabled         BOOLEAN NOT NULL DEFAULT TRUE,
    updated_by      VARCHAR(120) NOT NULL DEFAULT '',
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_prod_tenant_ai_config PRIMARY KEY (tenant_id, capability),
    CONSTRAINT ck_prod_tenant_ai_config_capability
        CHECK (capability IN ('TEXT', 'IMAGE', 'VIDEO', 'VOICE', 'LIPSYNC')),
    CONSTRAINT fk_prod_tenant_ai_config_credential_owner
        FOREIGN KEY (credential_id, tenant_id)
        REFERENCES pack_content.production_credentials (credential_id, tenant_id)
);

COMMENT ON TABLE pack_content.production_tenant_ai_config IS
    'Tenant default provider/model/credential per capability. Not org_settings. Build/shot overrides live in production_ai_selection.';

DROP TRIGGER IF EXISTS trg_production_tenant_ai_config_tenant ON pack_content.production_tenant_ai_config;
CREATE TRIGGER trg_production_tenant_ai_config_tenant
    BEFORE INSERT OR UPDATE ON pack_content.production_tenant_ai_config
    FOR EACH ROW EXECUTE FUNCTION pack_content.fn_production_tenant_boundary();

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.production_tenant_ai_config TO %I', r);
    END IF;
  END LOOP;
END $$;

COMMIT;
