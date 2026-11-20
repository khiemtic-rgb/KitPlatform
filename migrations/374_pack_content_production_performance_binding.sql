-- KitPlatform 374: Production OS Performance Snapshot Binding V1 Phase B
-- Manifest: deploy/ubuntu/migration-files.content.txt only
-- Additive columns on production_task_input_snapshots.
-- Does not replace pack_content.performance_snapshot.
-- Does not cut over CREATE_VIDEO / CREATE_VOICE / CREATE_LIPSYNC identity.
-- No official film backfill.

BEGIN;

ALTER TABLE pack_content.production_task_input_snapshots
    ADD COLUMN IF NOT EXISTS performance_snapshot_id UUID;

ALTER TABLE pack_content.production_task_input_snapshots
    ADD COLUMN IF NOT EXISTS performance_hash VARCHAR(96);

ALTER TABLE pack_content.production_task_input_snapshots
    ADD COLUMN IF NOT EXISTS performance_payload_json TEXT;

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint
    WHERE conname = 'production_task_input_snapshots_performance_snapshot_id_fkey'
      AND conrelid = 'pack_content.production_task_input_snapshots'::regclass
  ) THEN
    ALTER TABLE pack_content.production_task_input_snapshots
      ADD CONSTRAINT production_task_input_snapshots_performance_snapshot_id_fkey
      FOREIGN KEY (performance_snapshot_id)
      REFERENCES pack_content.performance_snapshot (performance_snapshot_id) ON DELETE RESTRICT;
  END IF;
END $$;

ALTER TABLE pack_content.production_task_input_snapshots
    DROP CONSTRAINT IF EXISTS ck_prod_input_perf_hash_prefix;
ALTER TABLE pack_content.production_task_input_snapshots
    ADD CONSTRAINT ck_prod_input_perf_hash_prefix
    CHECK (performance_hash IS NULL OR performance_hash LIKE 'PERFORMANCE_HASH_V1:%');

CREATE INDEX IF NOT EXISTS ix_prod_input_perf_snap
    ON pack_content.production_task_input_snapshots (performance_snapshot_id)
    WHERE performance_snapshot_id IS NOT NULL;

COMMENT ON COLUMN pack_content.production_task_input_snapshots.performance_snapshot_id IS
    'Immutable execution copy pointer to Phase A PerformanceSnapshot. Not ActingHash.';

COMMIT;
