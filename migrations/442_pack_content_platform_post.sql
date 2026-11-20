-- KitPlatform 442: lịch đọc từ Fanpage (Business Suite) và YouTube Studio.
-- Manifest: deploy/ubuntu/migration-files.content.txt only. Không gộp Pharmacy / Family / Local OS.
-- Chỉ lưu bản sao để Agent tránh trùng bài và trùng giờ. Không sửa, không xoá bài trên nền tảng.
-- Precondition: pack_content.channel_target, pack_content.topic (281).
-- Chạy lại an toàn (IF NOT EXISTS). Chưa áp vào DB cho đến khi được yêu cầu.

BEGIN;

CREATE TABLE IF NOT EXISTS pack_content.platform_post (
    id                UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    channel_id        UUID NOT NULL REFERENCES pack_content.channel_target(id) ON DELETE CASCADE,
    connector         VARCHAR(64) NOT NULL,
    external_id       TEXT NOT NULL,
    kind              VARCHAR(16) NOT NULL,
    title             VARCHAR(300),
    caption           VARCHAR(500),
    permalink         TEXT,
    status            VARCHAR(16) NOT NULL,
    publish_at        TIMESTAMPTZ,
    matched_topic_id  UUID REFERENCES pack_content.topic(id) ON DELETE SET NULL,
    match_source      VARCHAR(16),
    synced_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_content_platform_post_connector CHECK (connector IN ('facebook_page', 'youtube_channel')),
    CONSTRAINT ck_content_platform_post_kind CHECK (kind IN ('post', 'reel', 'video', 'short')),
    CONSTRAINT ck_content_platform_post_status CHECK (status IN ('scheduled', 'published')),
    CONSTRAINT ck_content_platform_post_match CHECK (match_source IS NULL OR match_source IN ('auto', 'manual')),
    CONSTRAINT uq_content_platform_post_channel_external UNIQUE (channel_id, external_id)
);

CREATE INDEX IF NOT EXISTS ix_content_platform_post_channel_time
    ON pack_content.platform_post (channel_id, publish_at);

CREATE INDEX IF NOT EXISTS ix_content_platform_post_topic
    ON pack_content.platform_post (matched_topic_id)
    WHERE matched_topic_id IS NOT NULL;

COMMENT ON TABLE pack_content.platform_post IS
    'Bản sao chỉ đọc của bài đã hẹn hoặc đã đăng trên Fanpage / YouTube. Agent dùng để không đề xuất trùng.';

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.platform_post TO %I', r);
    END IF;
  END LOOP;
END $$;

COMMIT;
