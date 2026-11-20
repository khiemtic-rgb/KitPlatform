-- KitPlatform 398: a series build may store the brand and film type chosen when the film was created.
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Does not UPDATE or DELETE existing rows. A missing film_type_id stays missing.
-- Does not seed pipelines. Does not rename series_code.
-- Precondition: pack_content.series_build, pack_content.brand, and pack_content.film_type exist.
-- Postcondition: series_build.brand_id and series_build.film_type_id are nullable;
-- a film cannot keep a film type from another brand.
-- One transaction. Forward repair: re-run this file. Applied-file rollback is a later migration.

BEGIN;

ALTER TABLE pack_content.series_build
    ADD COLUMN IF NOT EXISTS brand_id UUID REFERENCES pack_content.brand(id) ON DELETE RESTRICT;

ALTER TABLE pack_content.series_build
    ADD COLUMN IF NOT EXISTS film_type_id UUID REFERENCES pack_content.film_type(id) ON DELETE RESTRICT;

CREATE INDEX IF NOT EXISTS ix_series_build_brand
    ON pack_content.series_build (brand_id, updated_at DESC);

CREATE INDEX IF NOT EXISTS ix_series_build_film_type
    ON pack_content.series_build (film_type_id);

CREATE OR REPLACE FUNCTION pack_content.enforce_series_build_pipeline_brand()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    film_brand UUID;
BEGIN
    IF NEW.film_type_id IS NULL THEN
        RETURN NEW;
    END IF;
    SELECT brand_id INTO film_brand
    FROM pack_content.film_type
    WHERE id = NEW.film_type_id;
    IF film_brand IS NULL THEN
        RAISE EXCEPTION 'film type % does not exist', NEW.film_type_id;
    END IF;
    IF NEW.brand_id IS NULL OR NEW.brand_id <> film_brand THEN
        RAISE EXCEPTION 'series build brand and film type brand must match';
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_series_build_pipeline_brand ON pack_content.series_build;
CREATE TRIGGER trg_series_build_pipeline_brand
    BEFORE INSERT OR UPDATE OF brand_id, film_type_id
    ON pack_content.series_build
    FOR EACH ROW
    EXECUTE FUNCTION pack_content.enforce_series_build_pipeline_brand();

COMMENT ON COLUMN pack_content.series_build.film_type_id IS
    'Video pipeline chosen for this film. Null on rows created before the column existed.';

COMMIT;
