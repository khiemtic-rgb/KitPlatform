-- KitPlatform 441: hàng chờ bình luận đầu sau khi YouTube hoặc bài Facebook hẹn giờ lên sóng.
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Không UPDATE/DELETE bài đã đăng. Không seed.
-- Precondition: pack_content.publish_job và pack_content.channel_target.
-- Chạy lại an toàn (IF NOT EXISTS).
BEGIN;
CREATE TABLE IF NOT EXISTS pack_content.first_comment (
    id                UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    publish_job_id    UUID NOT NULL REFERENCES pack_content.publish_job(id) ON DELETE CASCADE,
    channel_target_id UUID NOT NULL REFERENCES pack_content.channel_target(id) ON DELETE CASCADE,
    connector         VARCHAR(64) NOT NULL,
    external_id       TEXT NOT NULL,
    body              TEXT NOT NULL,
    live_at           TIMESTAMPTZ NOT NULL,
    status            VARCHAR(16) NOT NULL DEFAULT 'pending',
    attempts          INT NOT NULL DEFAULT 0,
    next_attempt_at   TIMESTAMPTZ,
    last_error        TEXT,
    pin_note          TEXT,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_content_first_comment_status CHECK (status IN ('pending', 'posted', 'failed')),
    CONSTRAINT ck_content_first_comment_connector CHECK (connector IN ('facebook_page', 'youtube_channel'))
);
CREATE INDEX IF NOT EXISTS ix_content_first_comment_due
    ON pack_content.first_comment (status, live_at, next_attempt_at);
COMMIT;
