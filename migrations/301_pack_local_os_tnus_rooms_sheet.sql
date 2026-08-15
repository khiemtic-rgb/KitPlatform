-- Local OS: TNUS room list on Google Sheets — info only, never publish prices, never import all rows.
-- Isolated park. Do not apply via Pharmacy / Family / Content manifests.

INSERT INTO pack_local.source (
    id, source_kind, name, url, status, platform, category, audience, geo, notes
)
VALUES
    ('b1111111-1111-1111-1111-111111111204', 'official_web',
     'TNUS — danh sách nhà trọ gần trường (Google Sheet)',
     'https://docs.google.com/spreadsheets/d/1zAv9IswrXGwDB4iKu7icj38SjpwPIwjH/edit',
     'active', 'web', 'room', 'student', 'thai_nguyen',
     'File «Danh sách nhà trọ gần TNUS». Lấy địa chỉ / SĐT / tiện nghi. KHÔNG đăng giá. KHÔNG import cả sheet. Khớp theo ID file, không khớp mọi docs.google.com.')
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name,
    url = EXCLUDED.url,
    source_kind = EXCLUDED.source_kind,
    category = EXCLUDED.category,
    audience = EXCLUDED.audience,
    notes = EXCLUDED.notes,
    status = EXCLUDED.status,
    updated_at = NOW();
