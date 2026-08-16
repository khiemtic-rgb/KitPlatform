-- Local OS: public culture / sport / tourism / fair events still open.
-- Official sources only. Do not invent dates. Isolated park — not Pharmacy.

INSERT INTO pack_local.source (
    id, source_kind, name, url, status, platform, category, audience, geo, notes
)
VALUES
    ('b1111111-1111-1111-1111-111111111209', 'official_web',
     'Sở VHTTDL Thái Nguyên',
     'https://sovhttdl.thainguyen.gov.vn/',
     'active', 'web', 'event', 'mixed', 'thai_nguyen',
     'Sở Văn hóa, Thể thao và Du lịch. Chỉ lấy lễ hội / giải / hội chợ / du lịch công chúng còn hạn. Bỏ tin chỉ đạo, tập huấn nội bộ. Dán một bài. Không crawl.')
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name,
    url = EXCLUDED.url,
    source_kind = EXCLUDED.source_kind,
    category = EXCLUDED.category,
    audience = EXCLUDED.audience,
    notes = EXCLUDED.notes,
    status = EXCLUDED.status,
    updated_at = NOW();

-- 1) Liên hoan ẩm thực từ trà — Festival, phố đi bộ
INSERT INTO pack_local.listing (
    kind, title, summary, organization_name, place_text, audience, city_code,
    source_kind, source_url, source_id, contact_phone, contact_name,
    salary_text, working_time, employment_type, category, requirements,
    registration_url, start_at, end_at, trust, safety_flag, status,
    published_at, last_checked_at, expires_at
)
SELECT
    'event',
    'Liên hoan ẩm thực từ trà — Festival Trà 2026',
    'Trong khuôn khổ Festival Trà Quốc tế: khoảng 150 món / đồ uống từ trà, trải nghiệm ẩm thực vùng miền (dự kiến 10 tỉnh). Công chúng và sinh viên có thể tới phố đi bộ. Thái Nguyên Life không bán vé, không nhận đăng ký.',
    'Liên minh Hợp tác xã tỉnh Thái Nguyên',
    'Trung tâm phố đi bộ, phường Phan Đình Phùng',
    ARRAY['student']::text[],
    'thai_nguyen',
    'official_web',
    'https://thainguyen.gov.vn/festival-tra/lien-hoan-am-thuc-tu-tra-se-dien-ra-trong-khuon-kho-festival-tra-quoc-te-thai-nguyen-nam-2026-285393',
    'b1111111-1111-1111-1111-111111111205',
    NULL,
    'Liên minh HTX tỉnh Thái Nguyên',
    NULL,
    '30/10–7/11/2026 · Phố đi bộ Phan Đình Phùng',
    NULL,
    'fair',
    'Công chúng, sinh viên, khách du lịch. Lịch gian hàng trên tin gốc cổng tỉnh.',
    'https://thainguyen.gov.vn/festival-tra/lien-hoan-am-thuc-tu-tra-se-dien-ra-trong-khuon-kho-festival-tra-quoc-te-thai-nguyen-nam-2026-285393',
    TIMESTAMPTZ '2026-10-30 09:00+07',
    TIMESTAMPTZ '2026-11-07 21:00+07',
    'SOURCE_TRUSTED',
    FALSE,
    'ACTIVE',
    NOW(),
    NOW(),
    TIMESTAMPTZ '2026-11-08 23:59+07'
WHERE NOT EXISTS (
    SELECT 1 FROM pack_local.listing
    WHERE source_url = 'https://thainguyen.gov.vn/festival-tra/lien-hoan-am-thuc-tu-tra-se-dien-ra-trong-khuon-kho-festival-tra-quoc-te-thai-nguyen-nam-2026-285393'
);

-- 2) Dân ca / dân vũ Festival
INSERT INTO pack_local.listing (
    kind, title, summary, organization_name, place_text, audience, city_code,
    source_kind, source_url, source_id, contact_phone, contact_name,
    salary_text, working_time, employment_type, category, requirements,
    registration_url, start_at, end_at, trust, safety_flag, status,
    published_at, last_checked_at, expires_at
)
SELECT
    'event',
    'Dân ca, dân vũ — Hương trà kết nối di sản',
    'Chương trình nghệ thuật dân ca, dân vũ trong Festival Trà Quốc tế 2026. Công chúng xem tại không gian lễ hội (Quảng trường Võ Nguyên Giáp / phố đi bộ). Lịch từng đêm trên kế hoạch tỉnh. Thái Nguyên Life không bán vé.',
    'Sở Văn hóa, Thể thao và Du lịch Thái Nguyên',
    'Quảng trường Võ Nguyên Giáp · phố đi bộ Phan Đình Phùng',
    ARRAY['student']::text[],
    'thai_nguyen',
    'official_web',
    'https://thainguyen.gov.vn/thong-tin-ke-hoach/ke-hoach-312-to-chuc-festival-tra-quoc-te-thai-nguyen-nam-2026-279902#dan-ca',
    'b1111111-1111-1111-1111-111111111209',
    NULL,
    'Sở VHTTDL Thái Nguyên',
    NULL,
    'Trong chuỗi Festival 30/10–7/11/2026 · lịch từng đêm trên tin gốc',
    NULL,
    'culture',
    'Công chúng. Xem kế hoạch 312/KH-UBND trên cổng tỉnh.',
    'https://thainguyen.gov.vn/thong-tin-ke-hoach/ke-hoach-312-to-chuc-festival-tra-quoc-te-thai-nguyen-nam-2026-279902',
    TIMESTAMPTZ '2026-10-30 19:30+07',
    TIMESTAMPTZ '2026-11-07 22:00+07',
    'SOURCE_TRUSTED',
    FALSE,
    'ACTIVE',
    NOW(),
    NOW(),
    TIMESTAMPTZ '2026-11-08 23:59+07'
WHERE NOT EXISTS (
    SELECT 1 FROM pack_local.listing
    WHERE source_url = 'https://thainguyen.gov.vn/thong-tin-ke-hoach/ke-hoach-312-to-chuc-festival-tra-quoc-te-thai-nguyen-nam-2026-279902#dan-ca'
);

-- 3) Thưởng trà 5 vùng chè (tháng 9–10)
INSERT INTO pack_local.listing (
    kind, title, summary, organization_name, place_text, audience, city_code,
    source_kind, source_url, source_id, contact_phone, contact_name,
    salary_text, working_time, employment_type, category, requirements,
    registration_url, start_at, end_at, trust, safety_flag, status,
    published_at, last_checked_at, expires_at
)
SELECT
    'event',
    'Giao lưu thưởng trà — 5 vùng chè Thái Nguyên',
    'Theo kế hoạch Festival: giao lưu, thưởng trà cùng khách mời tại Tân Cương, Đồng Hỷ, Vô Tranh, La Bằng, Đồng Phúc. Tháng 9–10/2026 — lịch từng điểm trên tin gốc. Sinh viên / khách có thể theo dõi lịch vùng chè. Thái Nguyên Life không tổ chức tour.',
    'UBND tỉnh Thái Nguyên — Sở VHTTDL',
    'Tân Cương · Đồng Hỷ · Vô Tranh · La Bằng · Đồng Phúc',
    ARRAY['student']::text[],
    'thai_nguyen',
    'official_web',
    'https://thainguyen.gov.vn/thong-tin-ke-hoach/ke-hoach-312-to-chuc-festival-tra-quoc-te-thai-nguyen-nam-2026-279902#vung-che',
    'b1111111-1111-1111-1111-111111111205',
    NULL,
    'Ban Tổ chức Festival Trà Quốc tế',
    NULL,
    'Tháng 9–10/2026 · 5 vùng chè tiêu biểu',
    NULL,
    'tourism',
    'Công chúng, khách du lịch. Xem lịch từng xã trên cổng tỉnh / Báo Thái Nguyên.',
    'https://thainguyen.gov.vn/thong-tin-ke-hoach/ke-hoach-312-to-chuc-festival-tra-quoc-te-thai-nguyen-nam-2026-279902',
    TIMESTAMPTZ '2026-09-01 08:00+07',
    TIMESTAMPTZ '2026-10-31 18:00+07',
    'SOURCE_TRUSTED',
    FALSE,
    'ACTIVE',
    NOW(),
    NOW(),
    TIMESTAMPTZ '2026-11-01 23:59+07'
WHERE NOT EXISTS (
    SELECT 1 FROM pack_local.listing
    WHERE source_url = 'https://thainguyen.gov.vn/thong-tin-ke-hoach/ke-hoach-312-to-chuc-festival-tra-quoc-te-thai-nguyen-nam-2026-279902#vung-che'
);

-- 4) Golf + thưởng trà trong mây (trong Festival)
INSERT INTO pack_local.listing (
    kind, title, summary, organization_name, place_text, audience, city_code,
    source_kind, source_url, source_id, contact_phone, contact_name,
    salary_text, working_time, employment_type, category, requirements,
    registration_url, start_at, end_at, trust, safety_flag, status,
    published_at, last_checked_at, expires_at
)
SELECT
    'event',
    'Giải Golf Thái Nguyên xanh & Thưởng trà trong mây',
    'Hoạt động thể thao / trải nghiệm trong Festival Trà 2026 tại Sân Golf Tân Thái (xã Đại Phúc). Lịch thi đấu và điều kiện tham dự trên kế hoạch tỉnh — không bịa giờ. Thái Nguyên Life không nhận ghi danh.',
    'Sở Văn hóa, Thể thao và Du lịch Thái Nguyên',
    'Sân Golf Tân Thái, xã Đại Phúc',
    ARRAY['student']::text[],
    'thai_nguyen',
    'official_web',
    'https://thainguyen.gov.vn/thong-tin-ke-hoach/ke-hoach-312-to-chuc-festival-tra-quoc-te-thai-nguyen-nam-2026-279902#golf',
    'b1111111-1111-1111-1111-111111111209',
    NULL,
    'Sở VHTTDL Thái Nguyên',
    NULL,
    'Trong chuỗi Festival 30/10–7/11/2026 · Sân Golf Tân Thái',
    NULL,
    'sport',
    'Xem điều kiện tham dự trên kế hoạch 312/KH-UBND.',
    'https://thainguyen.gov.vn/thong-tin-ke-hoach/ke-hoach-312-to-chuc-festival-tra-quoc-te-thai-nguyen-nam-2026-279902',
    TIMESTAMPTZ '2026-10-30 07:00+07',
    TIMESTAMPTZ '2026-11-07 18:00+07',
    'SOURCE_TRUSTED',
    FALSE,
    'ACTIVE',
    NOW(),
    NOW(),
    TIMESTAMPTZ '2026-11-08 23:59+07'
WHERE NOT EXISTS (
    SELECT 1 FROM pack_local.listing
    WHERE source_url = 'https://thainguyen.gov.vn/thong-tin-ke-hoach/ke-hoach-312-to-chuc-festival-tra-quoc-te-thai-nguyen-nam-2026-279902#golf'
);

-- 5) Bóng đá nữ VĐQG — sân nhà Thái Nguyên (còn hạn đến 22/8/2026)
INSERT INTO pack_local.listing (
    kind, title, summary, organization_name, place_text, audience, city_code,
    source_kind, source_url, source_id, contact_phone, contact_name,
    salary_text, working_time, employment_type, category, requirements,
    registration_url, start_at, end_at, trust, safety_flag, status,
    published_at, last_checked_at, expires_at
)
SELECT
    'event',
    'Bóng đá nữ VĐQG — Cúp Thái Sơn Bắc 2026 tại SVD Thái Nguyên',
    'Thái Nguyên đăng cai các trận sân nhà Giải bóng đá nữ vô địch quốc gia. Công chúng xem tại Sân vận động Thái Nguyên. Lịch từng trận do VFF / Báo Thái Nguyên công bố. Thái Nguyên Life không bán vé.',
    'Sở VHTTDL Thái Nguyên · VFF',
    'Sân vận động Thái Nguyên',
    ARRAY['student']::text[],
    'thai_nguyen',
    'official_web',
    'https://baothainguyen.vn/the-thao/202605/san-van-dong-thai-nguyen-san-sang-cho-giai-bong-da-nu-vo-dich-quoc-gia-f362256/',
    'b1111111-1111-1111-1111-111111111206',
    NULL,
    'Ban tổ chức địa phương — Sở VHTTDL',
    NULL,
    'Sân nhà trên SVD Thái Nguyên · khung 20/6–22/8/2026 · lịch trận trên VFF',
    NULL,
    'sport',
    'Người hâm mộ, sinh viên. Xem lịch trận trên VFF / Báo Thái Nguyên.',
    'https://vff.org.vn/thong-bao-so-2-ve-viec-ban-hanh-lich-thi-dau-giai-bong-da-nu-vdqg-cup-thai-son-bac-2026/',
    TIMESTAMPTZ '2026-06-20 16:00+07',
    TIMESTAMPTZ '2026-08-22 21:00+07',
    'SOURCE_TRUSTED',
    FALSE,
    'ACTIVE',
    NOW(),
    NOW(),
    TIMESTAMPTZ '2026-08-23 23:59+07'
WHERE NOT EXISTS (
    SELECT 1 FROM pack_local.listing
    WHERE source_url = 'https://baothainguyen.vn/the-thao/202605/san-van-dong-thai-nguyen-san-sang-cho-giai-bong-da-nu-vo-dich-quoc-gia-f362256/'
);
