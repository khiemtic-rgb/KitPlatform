-- KitPlatform 373: Shared Performance Contract V1 Phase A
-- Manifest: deploy/ubuntu/migration-files.content.txt only
-- Durable PerformanceSnapshot payload. Not a pointer into graph_json.
-- No official film rows. No CREATE_VIDEO / CREATE_VOICE cutover.

BEGIN;

CREATE TABLE IF NOT EXISTS pack_content.performance_snapshot (
    performance_snapshot_id UUID PRIMARY KEY,
    build_id                UUID NOT NULL,
    shot_code               VARCHAR(80) NOT NULL,
    episode_code            VARCHAR(32) NOT NULL DEFAULT '',
    scene_code              VARCHAR(32) NOT NULL DEFAULT '',
    performance_hash        VARCHAR(96) NOT NULL,
    payload_json            TEXT NOT NULL,
    source_json             TEXT NOT NULL DEFAULT '{}',
    created_by              VARCHAR(120) NOT NULL DEFAULT '',
    frozen_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_perf_snap_hash_prefix CHECK (performance_hash LIKE 'PERFORMANCE_HASH_V1:%')
);

CREATE INDEX IF NOT EXISTS ix_perf_snap_scope
    ON pack_content.performance_snapshot (build_id, shot_code);

COMMENT ON TABLE pack_content.performance_snapshot IS
    'FAMIXA Shared Performance Contract V1 Phase A. Immutable WHAT snapshot. Not graph_json. Not VIDEO/VOICE cutover.';

COMMIT;
