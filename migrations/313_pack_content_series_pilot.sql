-- KitPlatform 313: Famixa Series production graph blob (not Episode/Character tables)
-- Manifest: deploy/ubuntu/migration-files.content.txt only
-- Graph JSON = Characters / Scenes / Shots / Voice Canon / Continuity. No image data URLs.

CREATE TABLE IF NOT EXISTS pack_content.series_pilot (
    series_code VARCHAR(64) PRIMARY KEY,
    graph_json  JSONB NOT NULL DEFAULT '{}'::jsonb,
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_series_pilot_code CHECK (series_code ~ '^[A-Z][A-Z0-9_]{1,62}$')
);

COMMENT ON TABLE pack_content.series_pilot IS
    'Famixa Series production graph (one JSON blob per series_code). Not Episode/Character SQL. Strip data URLs before write.';
