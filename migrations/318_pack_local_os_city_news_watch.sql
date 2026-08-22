-- Local OS: more official city-news indexes so the public site stays fresh daily.
-- Event sources already allow city life (filter invert). Jobs stay keyword-strict.
-- Official web only. No Facebook crawl. Isolated park.

INSERT INTO pack_local.source (
    id, source_kind, name, url, status, platform, category, audience, geo, notes, watch_enabled
)
VALUES
    (
        'b1111111-1111-1111-1111-111111111211',
        'official_web',
        'Báo Thái Nguyên — Văn hóa',
        'https://baothainguyen.vn/van-hoa/',
        'active',
        'web',
        'event',
        'mixed',
        'thai_nguyen',
        'Mục lục văn hóa tỉnh. Lấy lễ hội, đêm nhạc, ngày hội, tin đời sống văn hóa còn hạn. Bỏ HĐND / tiếp đoàn. Canh mục lục → ACTIVE. Không crawl Facebook.',
        TRUE
    ),
    (
        'b1111111-1111-1111-1111-111111111212',
        'official_web',
        'Báo Thái Nguyên — Giáo dục',
        'https://baothainguyen.vn/giao-duc/',
        'active',
        'web',
        'event',
        'student',
        'thai_nguyen',
        'Mục lục giáo dục tỉnh. Lấy đón tân SV, học bổng, năm học, tin trường. Bỏ HĐND / tiếp đoàn. Canh mục lục → ACTIVE. Không crawl Facebook.',
        TRUE
    )
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name,
    url = EXCLUDED.url,
    source_kind = EXCLUDED.source_kind,
    category = EXCLUDED.category,
    audience = EXCLUDED.audience,
    notes = EXCLUDED.notes,
    status = EXCLUDED.status,
    watch_enabled = TRUE,
    updated_at = NOW();

UPDATE pack_local.source
SET notes = 'Báo tỉnh. Lấy tin đời sống / sự kiện công chúng (văn hóa, giáo dục, thể thao, lễ hội). Bỏ HĐND, công văn, tiếp đoàn. Canh mục lục → ACTIVE. Không crawl Facebook.',
    watch_enabled = TRUE,
    updated_at = NOW()
WHERE id = 'b1111111-1111-1111-1111-111111111206';

-- Keep undated city news on the public list a bit longer (watch items have no start_at).
UPDATE pack_local.listing
SET expires_at = GREATEST(expires_at, NOW() + INTERVAL '30 days'),
    last_checked_at = NOW()
WHERE kind = 'event'
  AND status = 'ACTIVE'
  AND start_at IS NULL
  AND expires_at < NOW() + INTERVAL '14 days';
