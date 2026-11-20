-- KitPlatform 397: Series code is unique per brand. Episode code is unique per series.
-- Manifest: deploy/ubuntu/migration-files.content.txt
-- Aborts when the new key collides. Does not rename, suffix, or delete rows.
-- Keeps uq_content_episode_series_no and uq_content_episode_topic.
-- Precondition: no duplicate (brand_id, code) on content_series and no duplicate (series_id, code) on content_episode.
-- Postcondition: uq_content_series_brand_code and uq_content_episode_series_code exist; the old global code constraints are gone.
-- One transaction. A collision rolls this file back and names the keys. Applied-file rollback is a later migration.

BEGIN;

DO $$
DECLARE
    series_collision INT;
    episode_collision INT;
    series_keys TEXT;
    episode_keys TEXT;
BEGIN
    SELECT COUNT(*), COALESCE(string_agg(brand_id::text || ' ' || code, '; '), '')
      INTO series_collision, series_keys
    FROM (
        SELECT brand_id, code
        FROM pack_content.content_series
        GROUP BY brand_id, code
        HAVING COUNT(*) > 1
    ) s;
    SELECT COUNT(*), COALESCE(string_agg(series_id::text || ' ' || code, '; '), '')
      INTO episode_collision, episode_keys
    FROM (
        SELECT series_id, code
        FROM pack_content.content_episode
        GROUP BY series_id, code
        HAVING COUNT(*) > 1
    ) e;
    IF series_collision > 0 OR episode_collision > 0 THEN
        RAISE EXCEPTION
            'code collision blocks unique change: series (brand_id, code) groups % [%], episode (series_id, code) groups % [%]',
            series_collision, series_keys, episode_collision, episode_keys;
    END IF;
END $$;

ALTER TABLE pack_content.content_series
    DROP CONSTRAINT IF EXISTS uq_content_series_code;

CREATE UNIQUE INDEX IF NOT EXISTS uq_content_series_brand_code
    ON pack_content.content_series (brand_id, code);

ALTER TABLE pack_content.content_episode
    DROP CONSTRAINT IF EXISTS uq_content_episode_code;

CREATE UNIQUE INDEX IF NOT EXISTS uq_content_episode_series_code
    ON pack_content.content_episode (series_id, code);

COMMIT;
