-- KitPlatform 396: Content format and the link from an engine production to a Content topic.
-- Manifest: deploy/ubuntu/migration-files.content.txt
-- Does not create publish jobs and does not change topic status.
-- video_job stays the factory execution record. No third output table.
-- Requires 395 because the brand check reads video_project.brand_id.
-- Precondition: topic and video_production exist. Existing topics receive content_format article.
-- Postcondition: a production may link only a video topic of the same brand;
-- clearing the topic clears selected_for_publish; at most one selected production per topic.
-- One transaction. Forward repair: re-run this file. Applied-file rollback is a later migration.

BEGIN;

ALTER TABLE pack_content.topic
    ADD COLUMN IF NOT EXISTS content_format VARCHAR(32) NOT NULL DEFAULT 'article';

ALTER TABLE pack_content.topic
    DROP CONSTRAINT IF EXISTS ck_content_topic_format;

ALTER TABLE pack_content.topic
    ADD CONSTRAINT ck_content_topic_format CHECK (content_format IN (
        'article', 'video', 'infographic', 'other'
    ));

COMMENT ON COLUMN pack_content.topic.content_format IS
    'Editorial format of the content unit. Production workflow is not stored here.';

ALTER TABLE pack_content.video_production
    ADD COLUMN IF NOT EXISTS content_topic_id UUID REFERENCES pack_content.topic(id) ON DELETE SET NULL;

ALTER TABLE pack_content.video_production
    ADD COLUMN IF NOT EXISTS selected_for_publish BOOLEAN NOT NULL DEFAULT FALSE;

CREATE UNIQUE INDEX IF NOT EXISTS uq_video_production_selected_topic
    ON pack_content.video_production (content_topic_id)
    WHERE selected_for_publish AND content_topic_id IS NOT NULL;

CREATE OR REPLACE FUNCTION pack_content.enforce_production_content_brand()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    topic_brand UUID;
    topic_format VARCHAR(32);
    project_brand UUID;
BEGIN
    IF NEW.content_topic_id IS NULL THEN
        NEW.selected_for_publish := FALSE;
        RETURN NEW;
    END IF;
    SELECT brand_id, content_format INTO topic_brand, topic_format
    FROM pack_content.topic
    WHERE id = NEW.content_topic_id;
    IF topic_brand IS NULL THEN
        RAISE EXCEPTION 'content topic % does not exist', NEW.content_topic_id;
    END IF;
    IF topic_format <> 'video' THEN
        RAISE EXCEPTION 'production can link only a video content';
    END IF;
    SELECT brand_id INTO project_brand
    FROM pack_content.video_project
    WHERE id = NEW.project_id;
    IF project_brand IS NULL OR project_brand <> topic_brand THEN
        RAISE EXCEPTION 'production project brand and content brand must match';
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_video_production_content_brand ON pack_content.video_production;
CREATE TRIGGER trg_video_production_content_brand
    BEFORE INSERT OR UPDATE OF content_topic_id, selected_for_publish, project_id
    ON pack_content.video_production
    FOR EACH ROW
    EXECUTE FUNCTION pack_content.enforce_production_content_brand();

CREATE OR REPLACE FUNCTION pack_content.enforce_project_brand_linked_content()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pack_content.video_production vp
        INNER JOIN pack_content.topic t ON t.id = vp.content_topic_id
        WHERE vp.project_id = NEW.id
          AND (NEW.brand_id IS NULL OR t.brand_id <> NEW.brand_id)
    ) THEN
        RAISE EXCEPTION 'project brand cannot diverge from linked content';
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_video_project_linked_content ON pack_content.video_project;
CREATE TRIGGER trg_video_project_linked_content
    BEFORE UPDATE OF brand_id
    ON pack_content.video_project
    FOR EACH ROW
    EXECUTE FUNCTION pack_content.enforce_project_brand_linked_content();

COMMIT;
