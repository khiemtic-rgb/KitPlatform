-- KitPlatform 439: YouTube playlist made for a content series on one YouTube channel target.
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Does not UPDATE or DELETE existing rows. Does not seed playlists; the publish job creates one the first time.
-- Precondition: pack_content.content_series and pack_content.channel_target exist.
-- Postcondition: at most one playlist id per (series, channel target).
-- One transaction. Forward repair: re-run this file. Applied-file rollback is a later migration.
BEGIN;
CREATE TABLE IF NOT EXISTS pack_content.youtube_series_playlist (
    series_id         UUID NOT NULL REFERENCES pack_content.content_series(id) ON DELETE CASCADE,
    channel_target_id UUID NOT NULL REFERENCES pack_content.channel_target(id) ON DELETE CASCADE,
    playlist_id       VARCHAR(64) NOT NULL,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (series_id, channel_target_id)
);
COMMIT;
