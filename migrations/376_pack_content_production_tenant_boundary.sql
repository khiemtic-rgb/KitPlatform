-- KitPlatform 376: Famixa Production OS tenant boundary (ADR-03)
-- Manifest: deploy/ubuntu/migration-files.content.txt only
-- Adds tenant_id. Does not backfill. Does not assign a default tenant.
-- Existing rows stay NULL until a later checkpoint can prove ownership.
-- Official reads and new writes fail closed on NULL or mismatched tenant.

BEGIN;

ALTER TABLE pack_content.series_build
    ADD COLUMN IF NOT EXISTS tenant_id UUID NULL REFERENCES public.tenants (id);

ALTER TABLE pack_content.production_requests
    ADD COLUMN IF NOT EXISTS tenant_id UUID NULL REFERENCES public.tenants (id);
ALTER TABLE pack_content.production_tasks
    ADD COLUMN IF NOT EXISTS tenant_id UUID NULL REFERENCES public.tenants (id);
ALTER TABLE pack_content.production_task_input_snapshots
    ADD COLUMN IF NOT EXISTS tenant_id UUID NULL REFERENCES public.tenants (id);
ALTER TABLE pack_content.production_attempts
    ADD COLUMN IF NOT EXISTS tenant_id UUID NULL REFERENCES public.tenants (id);
ALTER TABLE pack_content.production_artifacts
    ADD COLUMN IF NOT EXISTS tenant_id UUID NULL REFERENCES public.tenants (id);
ALTER TABLE pack_content.production_bindings
    ADD COLUMN IF NOT EXISTS tenant_id UUID NULL REFERENCES public.tenants (id);
ALTER TABLE pack_content.production_task_events
    ADD COLUMN IF NOT EXISTS tenant_id UUID NULL REFERENCES public.tenants (id);
ALTER TABLE pack_content.production_film_assemblies
    ADD COLUMN IF NOT EXISTS tenant_id UUID NULL REFERENCES public.tenants (id);
ALTER TABLE pack_content.production_film_assembly_shots
    ADD COLUMN IF NOT EXISTS tenant_id UUID NULL REFERENCES public.tenants (id);
ALTER TABLE pack_content.production_ai_selection
    ADD COLUMN IF NOT EXISTS tenant_id UUID NULL REFERENCES public.tenants (id);
ALTER TABLE pack_content.production_execution_permit
    ADD COLUMN IF NOT EXISTS tenant_id UUID NULL REFERENCES public.tenants (id);

CREATE UNIQUE INDEX IF NOT EXISTS uq_series_build_id_tenant
    ON pack_content.series_build (id, tenant_id);
CREATE UNIQUE INDEX IF NOT EXISTS uq_prod_request_id_tenant
    ON pack_content.production_requests (request_id, tenant_id);
CREATE UNIQUE INDEX IF NOT EXISTS uq_prod_task_id_tenant
    ON pack_content.production_tasks (task_id, tenant_id);
CREATE UNIQUE INDEX IF NOT EXISTS uq_prod_snapshot_id_tenant
    ON pack_content.production_task_input_snapshots (snapshot_id, tenant_id);
CREATE UNIQUE INDEX IF NOT EXISTS uq_prod_attempt_id_tenant
    ON pack_content.production_attempts (attempt_id, tenant_id);
CREATE UNIQUE INDEX IF NOT EXISTS uq_prod_artifact_id_tenant
    ON pack_content.production_artifacts (artifact_id, tenant_id);
CREATE UNIQUE INDEX IF NOT EXISTS uq_prod_binding_id_tenant
    ON pack_content.production_bindings (binding_id, tenant_id);
CREATE UNIQUE INDEX IF NOT EXISTS uq_prod_assembly_id_tenant
    ON pack_content.production_film_assemblies (assembly_id, tenant_id);

ALTER TABLE pack_content.production_requests
    DROP CONSTRAINT IF EXISTS uq_prod_request_idempotency;

CREATE UNIQUE INDEX IF NOT EXISTS uq_prod_request_tenant_idempotency
    ON pack_content.production_requests (tenant_id, idempotency_key)
    WHERE tenant_id IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_prod_request_legacy_idempotency
    ON pack_content.production_requests (idempotency_key)
    WHERE tenant_id IS NULL;

DROP INDEX IF EXISTS pack_content.uq_prod_attempt_provider_job;

CREATE UNIQUE INDEX IF NOT EXISTS uq_prod_attempt_provider_job_tenant
    ON pack_content.production_attempts (tenant_id, provider_id, provider_job_id)
    WHERE tenant_id IS NOT NULL AND provider_job_id IS NOT NULL AND provider_job_id <> '';

CREATE UNIQUE INDEX IF NOT EXISTS uq_prod_attempt_provider_job_legacy
    ON pack_content.production_attempts (provider_id, provider_job_id)
    WHERE tenant_id IS NULL AND provider_job_id IS NOT NULL AND provider_job_id <> '';

CREATE INDEX IF NOT EXISTS ix_series_build_tenant
    ON pack_content.series_build (tenant_id, updated_at DESC);
CREATE INDEX IF NOT EXISTS ix_prod_request_tenant
    ON pack_content.production_requests (tenant_id, build_id, shot_code);
CREATE INDEX IF NOT EXISTS ix_prod_artifact_tenant
    ON pack_content.production_artifacts (tenant_id, artifact_id);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_prod_request_build_tenant') THEN
        ALTER TABLE pack_content.production_requests
            ADD CONSTRAINT fk_prod_request_build_tenant
            FOREIGN KEY (build_id, tenant_id)
            REFERENCES pack_content.series_build (id, tenant_id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_prod_task_request_tenant') THEN
        ALTER TABLE pack_content.production_tasks
            ADD CONSTRAINT fk_prod_task_request_tenant
            FOREIGN KEY (request_id, tenant_id)
            REFERENCES pack_content.production_requests (request_id, tenant_id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_prod_task_build_tenant') THEN
        ALTER TABLE pack_content.production_tasks
            ADD CONSTRAINT fk_prod_task_build_tenant
            FOREIGN KEY (build_id, tenant_id)
            REFERENCES pack_content.series_build (id, tenant_id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_prod_snapshot_task_tenant') THEN
        ALTER TABLE pack_content.production_task_input_snapshots
            ADD CONSTRAINT fk_prod_snapshot_task_tenant
            FOREIGN KEY (task_id, tenant_id)
            REFERENCES pack_content.production_tasks (task_id, tenant_id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_prod_attempt_task_tenant') THEN
        ALTER TABLE pack_content.production_attempts
            ADD CONSTRAINT fk_prod_attempt_task_tenant
            FOREIGN KEY (task_id, tenant_id)
            REFERENCES pack_content.production_tasks (task_id, tenant_id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_prod_artifact_task_tenant') THEN
        ALTER TABLE pack_content.production_artifacts
            ADD CONSTRAINT fk_prod_artifact_task_tenant
            FOREIGN KEY (task_id, tenant_id)
            REFERENCES pack_content.production_tasks (task_id, tenant_id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_prod_binding_build_tenant') THEN
        ALTER TABLE pack_content.production_bindings
            ADD CONSTRAINT fk_prod_binding_build_tenant
            FOREIGN KEY (build_id, tenant_id)
            REFERENCES pack_content.series_build (id, tenant_id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_prod_binding_artifact_tenant') THEN
        ALTER TABLE pack_content.production_bindings
            ADD CONSTRAINT fk_prod_binding_artifact_tenant
            FOREIGN KEY (artifact_id, tenant_id)
            REFERENCES pack_content.production_artifacts (artifact_id, tenant_id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_prod_assembly_build_tenant') THEN
        ALTER TABLE pack_content.production_film_assemblies
            ADD CONSTRAINT fk_prod_assembly_build_tenant
            FOREIGN KEY (build_id, tenant_id)
            REFERENCES pack_content.series_build (id, tenant_id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_prod_assembly_shot_tenant') THEN
        ALTER TABLE pack_content.production_film_assembly_shots
            ADD CONSTRAINT fk_prod_assembly_shot_tenant
            FOREIGN KEY (assembly_id, tenant_id)
            REFERENCES pack_content.production_film_assemblies (assembly_id, tenant_id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_prod_ai_sel_build_tenant') THEN
        ALTER TABLE pack_content.production_ai_selection
            ADD CONSTRAINT fk_prod_ai_sel_build_tenant
            FOREIGN KEY (build_id, tenant_id)
            REFERENCES pack_content.series_build (id, tenant_id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_prod_permit_build_tenant') THEN
        ALTER TABLE pack_content.production_execution_permit
            ADD CONSTRAINT fk_prod_permit_build_tenant
            FOREIGN KEY (build_id, tenant_id)
            REFERENCES pack_content.series_build (id, tenant_id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_prod_event_task_tenant') THEN
        ALTER TABLE pack_content.production_task_events
            ADD CONSTRAINT fk_prod_event_task_tenant
            FOREIGN KEY (task_id, tenant_id)
            REFERENCES pack_content.production_tasks (task_id, tenant_id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_prod_assembly_shot_artifact_tenant') THEN
        ALTER TABLE pack_content.production_film_assembly_shots
            ADD CONSTRAINT fk_prod_assembly_shot_artifact_tenant
            FOREIGN KEY (artifact_id, tenant_id)
            REFERENCES pack_content.production_artifacts (artifact_id, tenant_id);
    END IF;
END $$;

CREATE OR REPLACE FUNCTION pack_content.fn_production_tenant_boundary()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'INSERT' AND NEW.tenant_id IS NULL THEN
        RAISE EXCEPTION 'PRODUCTION_TENANT_REQUIRED';
    END IF;
    IF TG_OP = 'UPDATE'
       AND OLD.tenant_id IS NOT NULL
       AND NEW.tenant_id IS DISTINCT FROM OLD.tenant_id THEN
        RAISE EXCEPTION 'PRODUCTION_TENANT_IMMUTABLE';
    END IF;
    RETURN NEW;
END;
$$;

DO $$
DECLARE
    t text;
BEGIN
    FOREACH t IN ARRAY ARRAY[
        'series_build',
        'production_requests',
        'production_tasks',
        'production_task_input_snapshots',
        'production_attempts',
        'production_artifacts',
        'production_bindings',
        'production_task_events',
        'production_film_assemblies',
        'production_film_assembly_shots',
        'production_ai_selection',
        'production_execution_permit'
    ]
    LOOP
        EXECUTE format('DROP TRIGGER IF EXISTS trg_%s_tenant ON pack_content.%I', t, t);
        EXECUTE format(
            'CREATE TRIGGER trg_%s_tenant BEFORE INSERT OR UPDATE ON pack_content.%I FOR EACH ROW EXECUTE FUNCTION pack_content.fn_production_tenant_boundary()',
            t, t);
    END LOOP;
END $$;

COMMENT ON COLUMN pack_content.series_build.tenant_id IS
    'ADR-03 production root. NULL means ownership was not proven. Do not default to a tenant.';
COMMENT ON COLUMN pack_content.production_requests.tenant_id IS
    'Copied from the caller tenant after the build tenant matches. Not a client field.';

COMMIT;
