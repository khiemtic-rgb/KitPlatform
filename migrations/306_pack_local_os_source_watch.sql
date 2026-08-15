-- Local OS: official-web watch → NEEDS_REVIEW drafts. Never Facebook. Never auto-publish.
-- Isolated park. Do not apply via Pharmacy / Family / Content manifests.

ALTER TABLE pack_local.source
    ADD COLUMN IF NOT EXISTS watch_enabled BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS last_watched_at TIMESTAMPTZ;

CREATE TABLE IF NOT EXISTS pack_local.watch_run (
    id UUID PRIMARY KEY,
    started_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    finished_at TIMESTAMPTZ,
    trigger VARCHAR(16) NOT NULL DEFAULT 'manual',
    sources_scanned INT NOT NULL DEFAULT 0,
    links_seen INT NOT NULL DEFAULT 0,
    created_count INT NOT NULL DEFAULT 0,
    skipped_existing INT NOT NULL DEFAULT 0,
    skipped_filter INT NOT NULL DEFAULT 0,
    error_count INT NOT NULL DEFAULT 0,
    note TEXT
);

CREATE INDEX IF NOT EXISTS ix_local_watch_run_started
    ON pack_local.watch_run (started_at DESC);

ALTER TABLE pack_local.watch_run DROP CONSTRAINT IF EXISTS ck_local_watch_trigger;
ALTER TABLE pack_local.watch_run
    ADD CONSTRAINT ck_local_watch_trigger CHECK (trigger IN ('manual', 'scheduled'));

-- Watch official index pages only. Facebook + national job-search pages stay off.
UPDATE pack_local.source
SET watch_enabled = TRUE,
    notes = CASE
        WHEN notes IS NULL OR notes NOT LIKE '%Canh mục lục%'
            THEN trim(both FROM coalesce(notes, '') || ' Canh mục lục công khai → nháp chờ duyệt. Không tự đăng.')
        ELSE notes
    END,
    updated_at = NOW()
WHERE id IN (
    'b1111111-1111-1111-1111-111111111201',
    'b1111111-1111-1111-1111-111111111202',
    'b1111111-1111-1111-1111-111111111205',
    'b1111111-1111-1111-1111-111111111206',
    'b1111111-1111-1111-1111-111111111207',
    'b1111111-1111-1111-1111-111111111208'
)
AND source_kind IN ('official_web', 'partner', 'rss')
AND platform <> 'facebook';

INSERT INTO pack_local.source (
    id, source_kind, name, url, status, platform, category, audience, geo, notes, watch_enabled
)
VALUES (
    'b1111111-1111-1111-1111-111111111209',
    'official_web',
    'Cổng Du lịch Thái Nguyên — Tin tức',
    'http://thainguyentourism.vn/vi/news/Tin-tuc/',
    'active',
    'web',
    'event',
    'student',
    'thai_nguyen',
    'Cổng du lịch tỉnh. Canh mục lục tin → nháp sự kiện / địa điểm còn hạn. Không tự đăng. Không crawl Facebook.',
    TRUE
)
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name,
    url = EXCLUDED.url,
    notes = EXCLUDED.notes,
    watch_enabled = TRUE,
    updated_at = NOW();
