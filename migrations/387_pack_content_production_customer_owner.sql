-- KitPlatform 387: optional customer owner on episode, production shot, and two generation records.
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Depends on public.tenants (identity migration 002) and on migrations 333, 356, 362, and 365.
-- Does not alter pack_content.series_build. Does not backfill. Does not enable row-level security.
-- The content runner applies each statement on its own. Do not use CREATE INDEX CONCURRENTLY.
--
-- Recovery: if no second owned episode has been inserted, the partial indexes and triggers can be
-- dropped and uq_video_production recreated, and the nullable columns can be dropped while every
-- tenant_id is still NULL. After two customers legally share one episode code, the old unique
-- cannot be restored. Recovery does not assign an owner and does not delete rows.

ALTER TABLE pack_content.video_production
    ADD COLUMN IF NOT EXISTS tenant_id UUID NULL REFERENCES public.tenants (id);

ALTER TABLE pack_content.video_production_shot
    ADD COLUMN IF NOT EXISTS tenant_id UUID NULL REFERENCES public.tenants (id);

ALTER TABLE pack_content.video_image_generation_execution
    ADD COLUMN IF NOT EXISTS tenant_id UUID NULL REFERENCES public.tenants (id);

ALTER TABLE pack_content.video_generation_execution
    ADD COLUMN IF NOT EXISTS tenant_id UUID NULL REFERENCES public.tenants (id);

CREATE UNIQUE INDEX IF NOT EXISTS uq_video_production_unowned
    ON pack_content.video_production (project_id, production_code)
    WHERE tenant_id IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_video_production_owner
    ON pack_content.video_production (tenant_id, project_id, production_code)
    WHERE tenant_id IS NOT NULL;

CREATE OR REPLACE FUNCTION pack_content.fn_customer_owner_guard()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        IF NEW.tenant_id IS NULL THEN
            RAISE EXCEPTION 'OWNER_REQUIRED: new production row must name a customer';
        END IF;
        RETURN NEW;
    END IF;
    IF TG_OP = 'UPDATE' THEN
        IF OLD.tenant_id IS NULL THEN
            RAISE EXCEPTION 'OWNER_UNASSIGNED: historical row stays unowned';
        END IF;
        IF NEW.tenant_id IS DISTINCT FROM OLD.tenant_id THEN
            RAISE EXCEPTION 'OWNER_IMMUTABLE: customer owner cannot change';
        END IF;
        RETURN NEW;
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_video_production_owner ON pack_content.video_production;
CREATE TRIGGER trg_video_production_owner
    BEFORE INSERT OR UPDATE ON pack_content.video_production
    FOR EACH ROW EXECUTE FUNCTION pack_content.fn_customer_owner_guard();

DROP TRIGGER IF EXISTS trg_video_production_shot_owner ON pack_content.video_production_shot;
CREATE TRIGGER trg_video_production_shot_owner
    BEFORE INSERT OR UPDATE ON pack_content.video_production_shot
    FOR EACH ROW EXECUTE FUNCTION pack_content.fn_customer_owner_guard();

DROP TRIGGER IF EXISTS trg_video_img_gen_exec_owner ON pack_content.video_image_generation_execution;
CREATE TRIGGER trg_video_img_gen_exec_owner
    BEFORE INSERT OR UPDATE ON pack_content.video_image_generation_execution
    FOR EACH ROW EXECUTE FUNCTION pack_content.fn_customer_owner_guard();

DROP TRIGGER IF EXISTS trg_video_gen_exec_owner ON pack_content.video_generation_execution;
CREATE TRIGGER trg_video_gen_exec_owner
    BEFORE INSERT OR UPDATE ON pack_content.video_generation_execution
    FOR EACH ROW EXECUTE FUNCTION pack_content.fn_customer_owner_guard();

ALTER TABLE pack_content.video_production
    DROP CONSTRAINT IF EXISTS uq_video_production;
