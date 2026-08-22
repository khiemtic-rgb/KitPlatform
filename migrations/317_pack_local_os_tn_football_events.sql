-- Local OS: watch Báo TN Thể thao + publish remaining FC Thái Nguyên home friendly.
-- Official sources only. No Facebook crawl. Isolated park.

INSERT INTO pack_local.source (
    id, source_kind, name, url, status, platform, category, audience, geo, notes, watch_enabled
)
VALUES (
    'b1111111-1111-1111-1111-111111111210',
    'official_web',
    'Báo Thái Nguyên — Thể thao',
    'https://baothainguyen.vn/the-thao/',
    'active',
    'web',
    'event',
    'mixed',
    'thai_nguyen',
    'Mục lục thể thao tỉnh. Lấy trận / giải công chúng còn hạn (FC Thái Nguyên, giải tỉnh). Bỏ tin tổng kết đã đá xong. Canh mục lục → ACTIVE. Không crawl Facebook.',
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

-- Hide watch mistakes that ingested index pages as events.
UPDATE pack_local.listing
SET status = 'HIDDEN', last_checked_at = NOW()
WHERE kind = 'event'
  AND status = 'ACTIVE'
  AND (
      title ILIKE 'TIN TỨC SỰ KIỆN%'
      OR title ILIKE 'Sự kiện tại Thái Nguyên%'
      OR title ILIKE 'FestivalIndex%'
  );

INSERT INTO pack_local.listing (
    kind, title, summary, organization_name, place_text, audience, city_code,
    source_kind, source_url, source_id, contact_phone, contact_name,
    salary_text, working_time, employment_type, category, requirements,
    registration_url, start_at, end_at, trust, safety_flag, status,
    published_at, last_checked_at, expires_at
)
SELECT
    'event',
    'Giao hữu FC Thái Nguyên – Hà Nội FC',
    'Trận giao hữu trên sân nhà trước V.League 2. Báo Thái Nguyên: các trận tại Sân vận động Thái Nguyên mở cửa tự do cho khán giả. Giờ đá cụ thể theo thông báo từng trận — không bịa. Thái Nguyên Life không bán vé.',
    'FC Thái Nguyên',
    'Sân vận động Thái Nguyên',
    ARRAY['mixed']::text[],
    'thai_nguyen',
    'official_web',
    'https://baothainguyen.vn/the-thao/202608/cau-lac-bo-bong-da-nam-thai-nguyen-cong-bo-lich-thi-dau-giao-huu-truoc-mua-giai-f897f8d/',
    'b1111111-1111-1111-1111-111111111210',
    NULL,
    'FC Thái Nguyên',
    NULL,
    '25/8/2026 · Sân vận động Thái Nguyên · cửa tự do',
    NULL,
    'sport',
    'Khán giả vào sân tự do. Xem giờ đá trên Báo Thái Nguyên / thông báo CLB.',
    'https://baothainguyen.vn/the-thao/202608/cau-lac-bo-bong-da-nam-thai-nguyen-cong-bo-lich-thi-dau-giao-huu-truoc-mua-giai-f897f8d/',
    TIMESTAMPTZ '2026-08-25 16:00+07',
    TIMESTAMPTZ '2026-08-25 20:00+07',
    'SOURCE_TRUSTED',
    FALSE,
    'ACTIVE',
    NOW(),
    NOW(),
    TIMESTAMPTZ '2026-08-26 23:59+07'
WHERE NOT EXISTS (
    SELECT 1 FROM pack_local.listing
    WHERE source_url = 'https://baothainguyen.vn/the-thao/202608/cau-lac-bo-bong-da-nam-thai-nguyen-cong-bo-lich-thi-dau-giao-huu-truoc-mua-giai-f897f8d/'
);
