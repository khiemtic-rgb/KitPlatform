-- KitPlatform 369: Production OS pipeline graph — typed CURRENT bindings + snapshot lineage.
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Does not generate media. Does not delete graph_json.

ALTER TABLE pack_content.production_task_input_snapshots
    ADD COLUMN IF NOT EXISTS keyframe_artifact_id UUID,
    ADD COLUMN IF NOT EXISTS aspect_ratio VARCHAR(16) NOT NULL DEFAULT '16:9',
    ADD COLUMN IF NOT EXISTS duration_sec NUMERIC NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS compiler_version VARCHAR(64) NOT NULL DEFAULT 'SHOT_EXECUTION_COMPILER_V1',
    ADD COLUMN IF NOT EXISTS parent_artifact_ids JSONB NOT NULL DEFAULT '[]'::jsonb;

ALTER TABLE pack_content.production_artifacts
    ADD COLUMN IF NOT EXISTS parent_artifact_ids JSONB NOT NULL DEFAULT '[]'::jsonb;

ALTER TABLE pack_content.production_bindings
    DROP CONSTRAINT IF EXISTS ck_prod_binding_role;

ALTER TABLE pack_content.production_bindings
    ADD CONSTRAINT ck_prod_binding_role CHECK (
        role IN ('CURRENT', 'ACCEPTED', 'VISIBLE', 'EDITORIAL_SOURCE', 'FINAL')
        OR role LIKE 'CURRENT:%'
        OR role LIKE 'ACCEPTED:%'
        OR role LIKE 'VISIBLE:%'
    );

CREATE INDEX IF NOT EXISTS ix_prod_snapshot_keyframe
    ON pack_content.production_task_input_snapshots (keyframe_artifact_id)
    WHERE keyframe_artifact_id IS NOT NULL;

COMMENT ON COLUMN pack_content.production_artifacts.parent_artifact_ids IS
    'Explicit upstream artifactIds. LipSync → motion+voice. Mix → lipsync/motion. Never latest-take.';
COMMENT ON COLUMN pack_content.production_task_input_snapshots.keyframe_artifact_id IS
    'CREATE_VIDEO freezes the approved keyframe artifact, not a URL.';
