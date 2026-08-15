-- Local OS: city event sources (gov / báo / lễ hội / mythainguyen). Never crawl.
-- Filter: public events students in Thái Nguyên can attend. Skip politics, past items.
-- Isolated park. Do not apply via Pharmacy / Family / Content manifests.

INSERT INTO pack_local.source (
    id, source_kind, name, url, status, platform, category, audience, geo, notes
)
VALUES
    ('b1111111-1111-1111-1111-111111111205', 'official_web',
     'Cổng TTĐT tỉnh — Tin tức & sự kiện',
     'https://thainguyen.gov.vn/tin-tuc-su-kien',
     'active', 'web', 'event', 'student', 'thai_nguyen',
     'Cổng chính thức UBND tỉnh. Chỉ lấy lễ hội / ngày hội / thể thao / workshop SV có thể tới. Bỏ tin chỉ đạo, HĐND, GPMB. Dán một bài. Không crawl.'),
    ('b1111111-1111-1111-1111-111111111206', 'official_web',
     'Báo Thái Nguyên — Thời sự',
     'https://baothainguyen.vn/thoi-su-thai-nguyen/',
     'active', 'web', 'event', 'student', 'thai_nguyen',
     'Báo tỉnh. Lọc sự kiện công chúng (lễ hội, giải, ngày hội). Bỏ thời sự hành chính. Dán một bài. Không crawl.'),
    ('b1111111-1111-1111-1111-111111111207', 'partner',
     'Lễ hội Việt Nam — Thái Nguyên',
     'https://lehoivietnam.com.vn/vi/dia-diem/tinh-thai-nguyen-l40063454937088',
     'active', 'web', 'event', 'mixed', 'thai_nguyen',
     'Lịch lễ hội theo tỉnh. Chỉ lấy sự kiện còn hạn, SV có thể tới. Bỏ mục đã kết thúc. Dán một sự kiện. Không crawl.'),
    ('b1111111-1111-1111-1111-111111111208', 'official_web',
     'My Thái Nguyên — Sự kiện',
     'https://mythainguyen.vn/vi/events',
     'active', 'web', 'event', 'student', 'thai_nguyen',
     'Cổng du lịch tỉnh. Nhiều tin 2023 đã hết hạn — chỉ lấy sự kiện còn mở. Dán một bài. Không crawl.')
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name,
    url = EXCLUDED.url,
    source_kind = EXCLUDED.source_kind,
    category = EXCLUDED.category,
    audience = EXCLUDED.audience,
    notes = EXCLUDED.notes,
    status = EXCLUDED.status,
    updated_at = NOW();

-- Curated: Festival Trà 2026 — only upcoming public event that passed the student filter
-- (gov/báo mostly admin news; lehoivietnam list is past; mythainguyen events are 2023).
INSERT INTO pack_local.listing (
    kind, title, summary, organization_name, place_text, audience, city_code,
    source_kind, source_url, source_id, contact_phone, contact_name,
    salary_text, working_time, employment_type, category, requirements,
    registration_url, start_at, end_at, trust, safety_flag, status,
    published_at, last_checked_at, expires_at
)
SELECT
    'event',
    'Festival Trà Quốc tế — Thái Nguyên 2026',
    'Sự kiện lớn của tỉnh: tôn vinh văn hóa trà, nghệ thuật, không gian vùng chè. Khai mạc dự kiến 19:30 ngày 30/10 tại Quảng trường Võ Nguyên Giáp; chuỗi hoạt động 30/10–7/11/2026 (kỷ niệm 195 năm thành lập tỉnh). Sinh viên có thể tới các hoạt động công cộng — lịch chi tiết trên tin gốc. Thái Nguyên Life không bán vé, không nhận đăng ký.',
    'UBND tỉnh Thái Nguyên — Sở VHTTDL',
    'Quảng trường Võ Nguyên Giáp, phường Phan Đình Phùng · các vùng chè Tân Cương, Đồng Hỷ, Vô Tranh, La Bằng, Đồng Phúc',
    ARRAY['student']::text[],
    'thai_nguyen',
    'official_web',
    'https://thainguyen.gov.vn/thong-tin-ke-hoach/ke-hoach-312-to-chuc-festival-tra-quoc-te-thai-nguyen-nam-2026-279902',
    'b1111111-1111-1111-1111-111111111205',
    NULL,
    'Ban Tổ chức Festival Trà Quốc tế',
    NULL,
    'Khai mạc 19:30 30/10/2026 · Diễn ra 30/10–7/11/2026 · Hoạt động vùng chè dự kiến 2–6/11',
    NULL,
    'fair',
    'Công chúng, sinh viên, khách du lịch. Xem lịch từng ngày trên cổng tỉnh / Báo Thái Nguyên.',
    'https://thainguyen.gov.vn/tin-tuc-su-kien',
    TIMESTAMPTZ '2026-10-30 19:30+07',
    TIMESTAMPTZ '2026-11-07 23:59+07',
    'SOURCE_TRUSTED',
    FALSE,
    'ACTIVE',
    NOW(),
    NOW(),
    TIMESTAMPTZ '2026-11-08 23:59+07'
WHERE NOT EXISTS (
    SELECT 1 FROM pack_local.listing
    WHERE source_url = 'https://thainguyen.gov.vn/thong-tin-ke-hoach/ke-hoach-312-to-chuc-festival-tra-quoc-te-thai-nguyen-nam-2026-279902'
);
