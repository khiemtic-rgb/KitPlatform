-- KitPlatform 315: Famixa Series bản dựng (one row per production snapshot)
-- Manifest: deploy/ubuntu/migration-files.content.txt only
-- Graph JSON = script / shots / locks / take URLs. No image/audio bytes — files stay on the operator machine.

CREATE TABLE IF NOT EXISTS pack_content.series_build (
    id            UUID PRIMARY KEY,
    series_code   VARCHAR(64) NOT NULL,
    episode_code  VARCHAR(32) NOT NULL DEFAULT '',
    title         VARCHAR(240) NOT NULL DEFAULT '',
    status        VARCHAR(32) NOT NULL DEFAULT 'draft',
    shot_count    INTEGER NOT NULL DEFAULT 0,
    voice_lines   INTEGER NOT NULL DEFAULT 0,
    kf_count      INTEGER NOT NULL DEFAULT 0,
    video_count   INTEGER NOT NULL DEFAULT 0,
    graph_json    JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_series_build_code CHECK (series_code ~ '^[A-Z][A-Z0-9_]{1,62}$'),
    CONSTRAINT ck_series_build_status CHECK (status IN ('draft', 'script_locked', 'voice_locked', 'in_prod', 'final'))
);

CREATE INDEX IF NOT EXISTS ix_series_build_series_updated
    ON pack_content.series_build (series_code, updated_at DESC);

COMMENT ON TABLE pack_content.series_build IS
    'Famixa Series production snapshot. Unlimited episodes. Graph only — KF/TTS/take files stay local.';

ALTER TABLE pack_content.series_pilot
    ADD COLUMN IF NOT EXISTS active_build_id UUID;

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.series_build TO %I', r);
    END IF;
  END LOOP;
END $$;
