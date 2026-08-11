-- KitPlatform 284: Content Park — topic display_at for calendar / Excel import
-- Manifest: deploy/ubuntu/migration-files.content.txt only

ALTER TABLE pack_content.topic
    ADD COLUMN IF NOT EXISTS display_at TIMESTAMPTZ;

CREATE INDEX IF NOT EXISTS ix_content_topic_display_at
    ON pack_content.topic (display_at)
    WHERE display_at IS NOT NULL;

COMMENT ON COLUMN pack_content.topic.display_at IS
    'Intended public display / publish calendar date (from editorial Excel). Not the same as AI generate time.';
