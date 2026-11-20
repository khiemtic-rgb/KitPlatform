-- KitPlatform 370: Production OS scope anchor — FILM identity for production_* build_id
-- Manifest: deploy/ubuntu/migration-files.content.txt only
-- Checkpoint 1: create production_scope_anchor, FILM backfill, retarget FKs.
-- Does not create TestRun / TestScope / TestShot.
-- Does not INSERT/UPDATE/DELETE series_build, Canon, VA, or graph_json.
-- Does not generate media. No provider calls.

CREATE TABLE IF NOT EXISTS pack_content.production_scope_anchor (
    scope_id            UUID PRIMARY KEY,
    kind                VARCHAR(16) NOT NULL,
    series_build_id     UUID REFERENCES pack_content.series_build (id) ON DELETE CASCADE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_prod_scope_kind CHECK (kind IN ('FILM', 'TEST')),
    CONSTRAINT ck_prod_scope_film_has_build CHECK (kind <> 'FILM' OR series_build_id IS NOT NULL),
    CONSTRAINT ck_prod_scope_film_id_match CHECK (kind <> 'FILM' OR series_build_id = scope_id),
    CONSTRAINT ck_prod_scope_test_no_build CHECK (kind <> 'TEST' OR series_build_id IS NULL)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_prod_scope_film_build
    ON pack_content.production_scope_anchor (series_build_id)
    WHERE kind = 'FILM' AND series_build_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_prod_scope_kind
    ON pack_content.production_scope_anchor (kind);

COMMENT ON TABLE pack_content.production_scope_anchor IS
    'OS scope identity. FILM scope_id = series_build.id. Checkpoint 1 does not insert TEST rows.';

INSERT INTO pack_content.production_scope_anchor (scope_id, kind, series_build_id, created_at)
SELECT b.id, 'FILM', b.id, NOW()
FROM pack_content.series_build b
ON CONFLICT (scope_id) DO NOTHING;

CREATE OR REPLACE FUNCTION pack_content.fn_series_build_ensure_film_anchor()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  INSERT INTO pack_content.production_scope_anchor (scope_id, kind, series_build_id, created_at)
  VALUES (NEW.id, 'FILM', NEW.id, NOW())
  ON CONFLICT (scope_id) DO NOTHING;
  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_series_build_film_anchor ON pack_content.series_build;
CREATE TRIGGER trg_series_build_film_anchor
  AFTER INSERT ON pack_content.series_build
  FOR EACH ROW EXECUTE FUNCTION pack_content.fn_series_build_ensure_film_anchor();

DO $$
DECLARE
  rec record;
BEGIN
  FOR rec IN
    SELECT *
    FROM (VALUES
      ('pack_content.production_requests',            'production_requests_build_id_fkey',            'fk_prod_requests_scope'),
      ('pack_content.production_tasks',               'production_tasks_build_id_fkey',               'fk_prod_tasks_scope'),
      ('pack_content.production_bindings',            'production_bindings_build_id_fkey',            'fk_prod_bindings_scope'),
      ('pack_content.production_film_assemblies',     'production_film_assemblies_build_id_fkey',     'fk_prod_assemblies_scope'),
      ('pack_content.production_film_assembly_shots', 'production_film_assembly_shots_build_id_fkey', 'fk_prod_assembly_shots_scope')
    ) AS t(tbl, old_name, new_name)
  LOOP
    EXECUTE format('ALTER TABLE %s DROP CONSTRAINT IF EXISTS %I', rec.tbl, rec.old_name);
    EXECUTE format('ALTER TABLE %s DROP CONSTRAINT IF EXISTS %I', rec.tbl, rec.new_name);
    EXECUTE format(
      'ALTER TABLE %s ADD CONSTRAINT %I FOREIGN KEY (build_id) REFERENCES pack_content.production_scope_anchor (scope_id) ON DELETE RESTRICT',
      rec.tbl, rec.new_name);
  END LOOP;
END $$;

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.production_scope_anchor TO %I', r);
    END IF;
  END LOOP;
END $$;
