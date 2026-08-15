-- Local OS: curated official school job posts (not aggregator scrape). Isolated park.

INSERT INTO pack_local.listing (
    kind, title, summary, organization_name, place_text, audience, city_code,
    source_kind, source_url, source_id, contact_phone, contact_name,
    salary_text, working_time, employment_type, category, requirements,
    registration_url, price_month, trust, safety_flag, status,
    published_at, last_checked_at, expires_at
)
SELECT
    'job',
    'Samsung R&D Vietnam tuyển thực tập sinh 2026 (qua ICTU)',
    'Thực tập 4 tuần tại Samsung R&D Center Vietnam dành cho SV ICTU và trường đối tác. Phần mềm (C/C++/Java) hoặc phần cứng – mạng. Đăng ký trên link của Samsung. Thái Nguyên Life không nhận hồ sơ.',
    'Samsung R&D Center Vietnam',
    'Sinh viên ICTU — thực tập tại Samsung SRV',
    ARRAY['student']::text[],
    'thai_nguyen',
    'official_web',
    'https://fit.ictu.edu.vn/samsung-rd-center-vietnam-tuyen-thuc-tap-sinh-nam-2026/',
    'b1111111-1111-1111-1111-111111111201',
    NULL,
    'Khoa CNTT — ICTU',
    '3,5 triệu/khóa (4 tuần)',
    '8h–17h, T2–T6 · đợt 6/2026, 7/2026, 8/2026',
    'full_time',
    'internship',
    'SV CNTT / Điện tử viễn thông; tốt nghiệp 12/2026–06/2027; GPA ≥ 2.5/4; chưa thực tập SRV.',
    'https://fit.ictu.edu.vn/samsung-rd-center-vietnam-tuyen-thuc-tap-sinh-nam-2026/',
    NULL,
    'SOURCE_TRUSTED',
    FALSE,
    'ACTIVE',
    NOW(),
    NOW(),
    NOW() + INTERVAL '90 days'
WHERE NOT EXISTS (
    SELECT 1 FROM pack_local.listing
    WHERE source_url = 'https://fit.ictu.edu.vn/samsung-rd-center-vietnam-tuyen-thuc-tap-sinh-nam-2026/'
);
