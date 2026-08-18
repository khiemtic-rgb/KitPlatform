-- KitPlatform 292: Multi-brand adaptation work kind (no new editorial table)
-- Manifest: deploy/ubuntu/migration-files.content.txt only
-- content_package remains the Core Idea unit; fit report lives in extra_json.

ALTER TABLE pack_content.work_job
    DROP CONSTRAINT IF EXISTS ck_content_work_kind;

ALTER TABLE pack_content.work_job
    ADD CONSTRAINT ck_content_work_kind CHECK (kind IN (
        'generate_topic',
        'generate_package',
        'publish_topic',
        'video_mvp',
        'video_render',
        'brand_adapt'
    ));

COMMENT ON TABLE pack_content.content_package IS
    'Content Package = Core Idea (or a brand adaptation via source_package_id). extra_json.coreIdea + extra_json.brandFit; never copy one article to every brand.';
