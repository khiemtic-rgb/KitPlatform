-- Local OS: curated official events + scholarships (not aggregator scrape). Isolated park.

ALTER TABLE pack_local.listing DROP CONSTRAINT IF EXISTS ck_local_listing_kind;
ALTER TABLE pack_local.listing
    ADD CONSTRAINT ck_local_listing_kind CHECK (kind IN ('job', 'event', 'room', 'grant'));

INSERT INTO pack_local.source (
    id, source_kind, name, url, status, platform, category, audience, geo, notes
)
VALUES (
    'b1111111-1111-1111-1111-111111111204',
    'official_web',
    'Đại học Thái Nguyên (TNU)',
    'https://tnu.edu.vn/',
    'active', 'web', 'event', 'student', 'thai_nguyen',
    'Website chính thức ĐHTN. Dán link một bài. Không crawl.'
)
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name,
    url = EXCLUDED.url,
    notes = EXCLUDED.notes,
    updated_at = NOW();

INSERT INTO pack_local.listing (
    kind, title, summary, organization_name, place_text, audience, city_code,
    source_kind, source_url, source_id, contact_phone, contact_name,
    salary_text, working_time, employment_type, category, requirements,
    registration_url, start_at, end_at, trust, safety_flag, status,
    published_at, last_checked_at, expires_at
)
SELECT
    'event',
    'AIMCS 2026 — Track 5 AI tại ICTU (nộp bài đến 28/8)',
    'Hội thảo quốc tế AIMCS 2026 (28–30/10, Paris) có Track 5 tổ chức hybrid tại ICTU. SV / HVCH / NCS có thể nộp bài: AI mechatronics, robot, IoT, tự động hóa. Công bố IEEE/Scopus. Phí track ICTU 300€. Thái Nguyên Life không nhận bài.',
    'Khoa Kỹ thuật và Công nghệ — ICTU',
    'ICTU, Thái Nguyên (hybrid) · hội thảo chính tại Paris',
    ARRAY['student']::text[],
    'thai_nguyen',
    'official_web',
    'https://fet.ictu.edu.vn/call-for-papers-aimcs-2026/',
    'b1111111-1111-1111-1111-111111111201',
    NULL,
    'Khoa Kỹ thuật và Công nghệ — ICTU',
    NULL,
    'Nộp full paper: 28/8/2026 · Early registration: 13/9/2026 · Hội thảo: 28–30/10/2026',
    NULL,
    'conference',
    'Giảng viên, NCS, HVCH, SV nghiên cứu khoa học. Hướng: robotics, AGV/UAV, IIoT, tự động hóa.',
    'https://aimcs2026.org/#cfp',
    TIMESTAMPTZ '2026-10-28 08:00+07',
    TIMESTAMPTZ '2026-10-30 18:00+07',
    'SOURCE_TRUSTED',
    FALSE,
    'ACTIVE',
    NOW(),
    NOW(),
    TIMESTAMPTZ '2026-10-31 23:59+07'
WHERE NOT EXISTS (
    SELECT 1 FROM pack_local.listing
    WHERE source_url = 'https://fet.ictu.edu.vn/call-for-papers-aimcs-2026/'
);

INSERT INTO pack_local.listing (
    kind, title, summary, organization_name, place_text, audience, city_code,
    source_kind, source_url, source_id, contact_phone, contact_name,
    salary_text, category, requirements, registration_url,
    trust, safety_flag, status, published_at, last_checked_at, expires_at
)
SELECT
    'grant',
    'Học bổng Nghị định 179 — ngành kỹ thuật ICTU',
    'SV các ngành kỹ thuật then chốt / công nghệ chiến lược tại Khoa KT&CN ICTU được xét hỗ trợ theo Nghị định 179/2026/NĐ-CP. Mức công bố trên trang khoa: đến 3,7 triệu/tháng (ĐH). Đăng ký / xét theo quy định nhà trường — không nộp hồ sơ cho Thái Nguyên Life.',
    'Khoa Kỹ thuật và Công nghệ — ICTU',
    'ICTU, Thái Nguyên',
    ARRAY['student']::text[],
    'thai_nguyen',
    'official_web',
    'https://fet.ictu.edu.vn/co-hoi-vang-cho-tan-sinh-vien-khoi-ky-thuat-cong-nghe-hoc-nganh-hot-nhan-hoc-bong-theo-nghi-dinh-chinh-phu/',
    'b1111111-1111-1111-1111-111111111201',
    NULL,
    'Khoa Kỹ thuật và Công nghệ — ICTU',
    'Đến 3,7 triệu/tháng (ĐH, theo NĐ 179)',
    'scholarship',
    'Học ngành kỹ thuật then chốt / công nghệ chiến lược tại khoa (cơ điện tử, ô tô, điện, tự động hóa, vi mạch…). Điều kiện xét theo Nghị định và nhà trường.',
    'https://fet.ictu.edu.vn/co-hoi-vang-cho-tan-sinh-vien-khoi-ky-thuat-cong-nghe-hoc-nganh-hot-nhan-hoc-bong-theo-nghi-dinh-chinh-phu/',
    'SOURCE_TRUSTED',
    FALSE,
    'ACTIVE',
    NOW(),
    NOW(),
    TIMESTAMPTZ '2026-12-31 23:59+07'
WHERE NOT EXISTS (
    SELECT 1 FROM pack_local.listing
    WHERE source_url = 'https://fet.ictu.edu.vn/co-hoi-vang-cho-tan-sinh-vien-khoi-ky-thuat-cong-nghe-hoc-nganh-hot-nhan-hoc-bong-theo-nghi-dinh-chinh-phu/'
);

INSERT INTO pack_local.listing (
    kind, title, summary, organization_name, place_text, audience, city_code,
    source_kind, source_url, source_id, contact_phone, contact_name,
    salary_text, category, requirements, registration_url,
    trust, safety_flag, status, published_at, last_checked_at, expires_at
)
SELECT
    'grant',
    'Học bổng đầu vào IIT ICTU 2026 (IELTS ≥ 6.5)',
    'Viện Đào tạo Quốc tế ICTU: 5 suất 8,5 triệu cho IT Global / AI Global; 1 suất giảm 50% học phí kỳ đầu KTPM liên kết KNU — IELTS 6.5 trở lên. Có học bổng khuyến khích theo kỳ. Nộp / hỏi tại IIT, không nộp cho Thái Nguyên Life.',
    'Viện Đào tạo Quốc tế — ICTU',
    'ICTU, Thái Nguyên',
    ARRAY['student']::text[],
    'thai_nguyen',
    'official_web',
    'https://iit.ictu.edu.vn/vien-dao-tao-quoc-te-tuyen-sinh-2026/',
    'b1111111-1111-1111-1111-111111111201',
    NULL,
    'Viện Đào tạo Quốc tế — ICTU',
    '8,5 triệu/suất hoặc giảm 50% học phí kỳ đầu',
    'scholarship',
    'Tân SV chương trình IIT; IELTS ≥ 6.5 (hoặc tương đương) với suất đầu vào.',
    'https://iit.ictu.edu.vn/vien-dao-tao-quoc-te-tuyen-sinh-2026/',
    'SOURCE_TRUSTED',
    FALSE,
    'ACTIVE',
    NOW(),
    NOW(),
    TIMESTAMPTZ '2026-12-31 23:59+07'
WHERE NOT EXISTS (
    SELECT 1 FROM pack_local.listing
    WHERE source_url = 'https://iit.ictu.edu.vn/vien-dao-tao-quoc-te-tuyen-sinh-2026/'
);

INSERT INTO pack_local.listing (
    kind, title, summary, organization_name, place_text, audience, city_code,
    source_kind, source_url, source_id, contact_phone, contact_name,
    salary_text, category, requirements, registration_url,
    trust, safety_flag, status, published_at, last_checked_at, expires_at
)
SELECT
    'grant',
    'Miễn giảm học phí Khoa Quốc tế ĐHTN 2026',
    'Chính sách 2026: miễn 100% học phí (con người có công, khuyết tật, mồ côi, DTTS hộ nghèo/cận nghèo); giảm 70% hoặc 50% theo đối tượng; hỗ trợ chi phí học tập đến 1.544.000đ/tháng. Gồm SV 36 xã đặc biệt khó khăn Thái Nguyên. Xét tại khoa — không nộp hồ sơ cho Thái Nguyên Life.',
    'Khoa Quốc tế — Đại học Thái Nguyên',
    'Khoa Quốc tế, ĐHTN, Thái Nguyên',
    ARRAY['student']::text[],
    'thai_nguyen',
    'official_web',
    'https://tnu.edu.vn/tin-tuc-su-kien/thong-tin-tuyen-sinh/khoa-quoc-te-dai-hoc-thai-nguyen-trien-khai-chinh-sach-mien-giam-hoc-phi-va-ho-tro-chi-phi-hoc-tap-nam-2026.html',
    'b1111111-1111-1111-1111-111111111204',
    '0203852650',
    'Cổng thông tin ĐHTN',
    'Miễn 100% / giảm 50–70% · hỗ trợ đến 1,544 triệu/tháng',
    'scholarship',
    'Đối tượng chính sách, DTTS, hộ nghèo/cận nghèo, mồ côi, khuyết tật, xã đặc biệt khó khăn — theo thông báo khoa.',
    'https://tnu.edu.vn/tin-tuc-su-kien/thong-tin-tuyen-sinh/khoa-quoc-te-dai-hoc-thai-nguyen-trien-khai-chinh-sach-mien-giam-hoc-phi-va-ho-tro-chi-phi-hoc-tap-nam-2026.html',
    'SOURCE_TRUSTED',
    FALSE,
    'ACTIVE',
    NOW(),
    NOW(),
    TIMESTAMPTZ '2026-12-31 23:59+07'
WHERE NOT EXISTS (
    SELECT 1 FROM pack_local.listing
    WHERE source_url = 'https://tnu.edu.vn/tin-tuc-su-kien/thong-tin-tuyen-sinh/khoa-quoc-te-dai-hoc-thai-nguyen-trien-khai-chinh-sach-mien-giam-hoc-phi-va-ho-tro-chi-phi-hoc-tap-nam-2026.html'
);
