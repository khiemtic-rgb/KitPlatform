-- KitPlatform 388: per-customer uniqueness for production shot and the two generation fingerprints.
-- Manifest: deploy/ubuntu/migration-files.content.txt only. Apply after 387.
-- Does not alter pack_content.series_build. Does not backfill. Does not enable row-level security.
-- Partial indexes are created before the old global uniques are dropped.
--
-- Recovery: the old global uniques can be recreated only while every row still has tenant_id NULL
-- and the partial indexes still hold. After two customers use the same shot code or fingerprint,
-- the global unique cannot be restored. Recovery does not assign an owner and does not delete rows.

CREATE UNIQUE INDEX IF NOT EXISTS uq_video_prod_shot_code_unowned
    ON pack_content.video_production_shot (shot_code, shot_version)
    WHERE tenant_id IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_video_prod_shot_code_owner
    ON pack_content.video_production_shot (tenant_id, shot_code, shot_version)
    WHERE tenant_id IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_video_prod_shot_seq_unowned
    ON pack_content.video_production_shot (character_id, era_id, shot_seq, shot_version)
    WHERE tenant_id IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_video_prod_shot_seq_owner
    ON pack_content.video_production_shot (tenant_id, character_id, era_id, shot_seq, shot_version)
    WHERE tenant_id IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_video_img_gen_exec_fp_unowned
    ON pack_content.video_image_generation_execution (execution_fingerprint)
    WHERE tenant_id IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_video_img_gen_exec_fp_owner
    ON pack_content.video_image_generation_execution (tenant_id, execution_fingerprint)
    WHERE tenant_id IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_video_gen_exec_fp_unowned
    ON pack_content.video_generation_execution (execution_fingerprint)
    WHERE tenant_id IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_video_gen_exec_fp_owner
    ON pack_content.video_generation_execution (tenant_id, execution_fingerprint)
    WHERE tenant_id IS NOT NULL;

ALTER TABLE pack_content.video_production_shot
    DROP CONSTRAINT IF EXISTS uq_video_prod_shot_code;
ALTER TABLE pack_content.video_production_shot
    DROP CONSTRAINT IF EXISTS uq_video_prod_shot_seq;
ALTER TABLE pack_content.video_image_generation_execution
    DROP CONSTRAINT IF EXISTS uq_video_img_gen_exec_fingerprint;
ALTER TABLE pack_content.video_generation_execution
    DROP CONSTRAINT IF EXISTS uq_video_gen_exec_fingerprint;
