-- KitPlatform 371: Production OS TEST scope foundation
-- Manifest: deploy/ubuntu/migration-files.content.txt only
-- Checkpoint 2: TestRun / TestScope / TestShot + TEST anchors.
-- Does not insert/update/delete series_build, Canon, VA, or graph_json.
-- Does not rewrite FILM scope_id or production artifact ids.
-- Does not generate media. No provider calls.

BEGIN;

CREATE TABLE IF NOT EXISTS pack_content.production_test_run (
    test_run_id     UUID PRIMARY KEY,
    name            VARCHAR(240) NOT NULL,
    status          VARCHAR(24) NOT NULL DEFAULT 'OPEN',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by      VARCHAR(120) NOT NULL DEFAULT '',
    metadata        JSONB NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT ck_prod_test_run_status CHECK (status IN ('OPEN', 'CLOSED'))
);

ALTER TABLE pack_content.production_scope_anchor
    ADD COLUMN IF NOT EXISTS test_run_id UUID;

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint
    WHERE conname = 'production_scope_anchor_test_run_id_fkey'
      AND conrelid = 'pack_content.production_scope_anchor'::regclass
  ) THEN
    ALTER TABLE pack_content.production_scope_anchor
      ADD CONSTRAINT production_scope_anchor_test_run_id_fkey
      FOREIGN KEY (test_run_id) REFERENCES pack_content.production_test_run (test_run_id) ON DELETE RESTRICT;
  END IF;
END $$;

ALTER TABLE pack_content.production_scope_anchor
    DROP CONSTRAINT IF EXISTS ck_prod_scope_test_has_run;
ALTER TABLE pack_content.production_scope_anchor
    ADD CONSTRAINT ck_prod_scope_test_has_run
    CHECK (kind <> 'TEST' OR test_run_id IS NOT NULL);

ALTER TABLE pack_content.production_scope_anchor
    DROP CONSTRAINT IF EXISTS ck_prod_scope_film_no_run;
ALTER TABLE pack_content.production_scope_anchor
    ADD CONSTRAINT ck_prod_scope_film_no_run
    CHECK (kind <> 'FILM' OR test_run_id IS NULL);

CREATE INDEX IF NOT EXISTS ix_prod_scope_test_run
    ON pack_content.production_scope_anchor (test_run_id)
    WHERE test_run_id IS NOT NULL;

CREATE TABLE IF NOT EXISTS pack_content.production_test_scope (
    scope_id        UUID PRIMARY KEY REFERENCES pack_content.production_scope_anchor (scope_id) ON DELETE RESTRICT,
    test_run_id     UUID NOT NULL REFERENCES pack_content.production_test_run (test_run_id) ON DELETE RESTRICT,
    episode_code    VARCHAR(32) NOT NULL DEFAULT 'TEST',
    scene_code      VARCHAR(32) NOT NULL DEFAULT 'SC01',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    metadata        JSONB NOT NULL DEFAULT '{}'::jsonb
);

CREATE INDEX IF NOT EXISTS ix_prod_test_scope_run
    ON pack_content.production_test_scope (test_run_id);

CREATE TABLE IF NOT EXISTS pack_content.production_test_shot (
    test_shot_id    UUID PRIMARY KEY,
    scope_id        UUID NOT NULL REFERENCES pack_content.production_test_scope (scope_id) ON DELETE RESTRICT,
    shot_id         VARCHAR(80) NOT NULL,
    episode_code    VARCHAR(32) NOT NULL,
    scene_code      VARCHAR(32) NOT NULL,
    shot_code       VARCHAR(80) NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    metadata        JSONB NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT uq_prod_test_shot_scope_code UNIQUE (scope_id, shot_code)
);

CREATE INDEX IF NOT EXISTS ix_prod_test_shot_scope
    ON pack_content.production_test_shot (scope_id, shot_code);

COMMENT ON TABLE pack_content.production_test_run IS
    'TEST harness run. Not a series_build. Not a Famixa film.';
COMMENT ON TABLE pack_content.production_test_scope IS
    'TEST OS scope. scope_id is production_scope_anchor.scope_id with kind=TEST.';
COMMENT ON TABLE pack_content.production_test_shot IS
    'TEST shot addressable as ProductionScope(scope_id, episode, scene, shot_code).';

DO $$
DECLARE
  r text;
  t text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      FOREACH t IN ARRAY ARRAY['production_test_run', 'production_test_scope', 'production_test_shot'] LOOP
        EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.%I TO %I', t, r);
      END LOOP;
    END IF;
  END LOOP;
END $$;

COMMIT;
