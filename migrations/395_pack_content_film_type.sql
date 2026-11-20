-- KitPlatform 395: Film Type belongs to a brand. A video project may point at one brand and one film type.
-- Manifest: deploy/ubuntu/migration-files.content.txt
-- Does not seed film type names. Does not create brands.
-- Famixa brand_id is filled only when exactly one brand row has code famixa, case-insensitive.
-- Does not set film_type_id and does not create a Film Type.
-- Precondition: pack_content.brand and pack_content.video_project exist.
-- Postcondition: film_type exists; video_project.brand_id and film_type_id are nullable;
-- a project cannot keep a film type from another brand; FAMIXA.brand_id is set only under the count rule.
-- One transaction. Forward repair: re-run this file. Applied-file rollback is a later migration.

BEGIN;

CREATE TABLE IF NOT EXISTS pack_content.film_type (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    brand_id        UUID NOT NULL REFERENCES pack_content.brand(id) ON DELETE CASCADE,
    code            VARCHAR(64) NOT NULL,
    name            VARCHAR(200) NOT NULL,
    workflow_key    VARCHAR(32) NOT NULL,
    status          VARCHAR(24) NOT NULL DEFAULT 'draft',
    config_json     JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_film_type_brand_code UNIQUE (brand_id, code),
    CONSTRAINT ck_film_type_workflow CHECK (workflow_key IN (
        'famixa_series', 'factory_short', 'guided_production'
    )),
    CONSTRAINT ck_film_type_status CHECK (status IN ('draft', 'active', 'archived'))
);

CREATE INDEX IF NOT EXISTS ix_film_type_brand
    ON pack_content.film_type (brand_id, status);

COMMENT ON TABLE pack_content.film_type IS
    'Business film branch for one brand. Not video_universe.';

ALTER TABLE pack_content.video_project
    ADD COLUMN IF NOT EXISTS brand_id UUID REFERENCES pack_content.brand(id) ON DELETE RESTRICT;

ALTER TABLE pack_content.video_project
    ADD COLUMN IF NOT EXISTS film_type_id UUID REFERENCES pack_content.film_type(id) ON DELETE RESTRICT;

CREATE INDEX IF NOT EXISTS ix_video_project_brand
    ON pack_content.video_project (brand_id);

CREATE OR REPLACE FUNCTION pack_content.enforce_video_project_film_brand()
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
        RAISE EXCEPTION 'video project brand and film type brand must match';
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_video_project_film_brand ON pack_content.video_project;
CREATE TRIGGER trg_video_project_film_brand
    BEFORE INSERT OR UPDATE OF brand_id, film_type_id
    ON pack_content.video_project
    FOR EACH ROW
    EXECUTE FUNCTION pack_content.enforce_video_project_film_brand();

DO $$
DECLARE
    match_count INT;
    matched_brand UUID;
    matched_code VARCHAR(64);
BEGIN
    SELECT COUNT(*) INTO match_count
    FROM pack_content.brand
    WHERE lower(code) = 'famixa';
    IF match_count = 1 THEN
        SELECT id, code INTO matched_brand, matched_code
        FROM pack_content.brand
        WHERE lower(code) = 'famixa';
        UPDATE pack_content.video_project
        SET brand_id = matched_brand,
            brand_code = matched_code,
            updated_at = NOW()
        WHERE project_code = 'FAMIXA'
          AND brand_id IS NULL;
    END IF;
END $$;

COMMIT;
