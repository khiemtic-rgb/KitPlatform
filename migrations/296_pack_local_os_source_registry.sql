-- Local OS: Source Registry (Facebook group = one source type, never crawl).
-- Isolated park. Do not apply via Pharmacy / Family / Content manifests.

ALTER TABLE pack_local.source
    ADD COLUMN IF NOT EXISTS platform VARCHAR(24) NOT NULL DEFAULT 'web',
    ADD COLUMN IF NOT EXISTS category VARCHAR(32) NOT NULL DEFAULT 'mixed',
    ADD COLUMN IF NOT EXISTS audience VARCHAR(32) NOT NULL DEFAULT 'mixed',
    ADD COLUMN IF NOT EXISTS geo VARCHAR(32) NOT NULL DEFAULT 'thai_nguyen',
    ADD COLUMN IF NOT EXISTS notes TEXT,
    ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW();

UPDATE pack_local.source
SET source_kind = CASE
        WHEN source_kind IN ('facebook_group', 'facebook_page', 'official_web', 'rss', 'partner', 'user')
            THEN source_kind
        WHEN source_kind IN ('group_manual', 'group', 'facebook') THEN 'facebook_group'
        ELSE 'official_web'
    END
WHERE source_kind NOT IN (
    'facebook_group', 'facebook_page', 'official_web', 'rss', 'partner', 'user'
);

ALTER TABLE pack_local.source DROP CONSTRAINT IF EXISTS ck_local_source_kind;
ALTER TABLE pack_local.source
    ADD CONSTRAINT ck_local_source_kind CHECK (source_kind IN (
        'facebook_group', 'facebook_page', 'official_web', 'rss', 'partner', 'user'
    ));

ALTER TABLE pack_local.source DROP CONSTRAINT IF EXISTS ck_local_source_status;
ALTER TABLE pack_local.source
    ADD CONSTRAINT ck_local_source_status CHECK (status IN ('active', 'paused'));

CREATE UNIQUE INDEX IF NOT EXISTS uq_local_source_url
    ON pack_local.source (url)
    WHERE url IS NOT NULL AND length(trim(url)) > 0;

ALTER TABLE pack_local.listing
    ADD COLUMN IF NOT EXISTS source_id UUID REFERENCES pack_local.source (id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS ix_local_listing_source_id
    ON pack_local.listing (source_id)
    WHERE source_id IS NOT NULL;

ALTER TABLE pack_local.community_group
    ADD COLUMN IF NOT EXISTS source_id UUID REFERENCES pack_local.source (id) ON DELETE SET NULL;

-- 7 Facebook groups already in community_group — register as sources (metadata only).
INSERT INTO pack_local.source (
    id, source_kind, name, url, status, platform, category, audience, geo, notes
)
VALUES
    ('b1111111-1111-1111-1111-111111111101', 'facebook_group',
     'Group việc / SV Thái Nguyên (G1)',
     'https://www.facebook.com/groups/5719882594776915/',
     'active', 'facebook', 'job', 'student', 'thai_nguyen',
     'Nguồn cộng đồng. Không quét group. Chỉ nhận khi dán link một bài.'),
    ('b1111111-1111-1111-1111-111111111102', 'facebook_group',
     'Group việc / SV Thái Nguyên (G2)',
     'https://www.facebook.com/groups/159548018082242/',
     'active', 'facebook', 'job', 'student', 'thai_nguyen',
     'Nguồn cộng đồng. Không quét group.'),
    ('b1111111-1111-1111-1111-111111111103', 'facebook_group',
     'Group việc / SV Thái Nguyên (G3)',
     'https://www.facebook.com/groups/552660281927244/',
     'active', 'facebook', 'job', 'student', 'thai_nguyen',
     'Nguồn cộng đồng. Không quét group.'),
    ('b1111111-1111-1111-1111-111111111104', 'facebook_group',
     'Group việc / SV Thái Nguyên (G4)',
     'https://www.facebook.com/groups/783713308689243/',
     'active', 'facebook', 'job', 'mixed', 'thai_nguyen',
     'Nguồn cộng đồng. Không quét group.'),
    ('b1111111-1111-1111-1111-111111111105', 'facebook_group',
     'Group việc / SV Thái Nguyên (G5)',
     'https://www.facebook.com/groups/1156179755511070/',
     'active', 'facebook', 'job', 'student', 'thai_nguyen',
     'Nguồn cộng đồng. Không quét group.'),
    ('b1111111-1111-1111-1111-111111111106', 'facebook_group',
     'Group việc / SV Thái Nguyên (G6)',
     'https://www.facebook.com/groups/2188436011518830/',
     'active', 'facebook', 'job', 'mixed', 'thai_nguyen',
     'Nguồn cộng đồng. Không quét group.'),
    ('b1111111-1111-1111-1111-111111111107', 'facebook_group',
     'Group việc / SV Thái Nguyên (G7)',
     'https://www.facebook.com/groups/250881661082648/',
     'active', 'facebook', 'job', 'student', 'thai_nguyen',
     'Nguồn cộng đồng. Không quét group.'),
    ('b1111111-1111-1111-1111-111111111201', 'official_web',
     'ICTU — Đại học CNTT & Truyền thông',
     'https://ictu.edu.vn/',
     'active', 'web', 'event', 'student', 'thai_nguyen',
     'Website chính thức. Có thể lấy title/mô tả công khai khi dán link bài, không crawl Facebook.')
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name,
    url = EXCLUDED.url,
    source_kind = EXCLUDED.source_kind,
    category = EXCLUDED.category,
    audience = EXCLUDED.audience,
    notes = EXCLUDED.notes,
    updated_at = NOW();

UPDATE pack_local.community_group g
SET source_id = s.id
FROM pack_local.source s
WHERE g.source_id IS NULL
  AND s.source_kind = 'facebook_group'
  AND regexp_replace(g.url, '/+$', '') = regexp_replace(s.url, '/+$', '');
