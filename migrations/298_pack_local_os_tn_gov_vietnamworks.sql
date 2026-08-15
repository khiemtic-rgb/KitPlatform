-- Local OS: tỉnh portal + VietnamWorks (Thái Nguyên). Never crawl.
-- Isolated park. Do not apply via Pharmacy / Family / Content manifests.

INSERT INTO pack_local.source (
    id, source_kind, name, url, status, platform, category, audience, geo, notes
)
VALUES
    ('b1111111-1111-1111-1111-111111111202', 'official_web',
     'Cổng việc làm Thái Nguyên (UBND tỉnh)',
     'https://vieclam.thainguyen.gov.vn/tin-tuyen-dung',
     'active', 'web', 'job', 'mixed', 'thai_nguyen',
     'Website chính thức của tỉnh. Dán link một tin tuyển dụng. Không crawl danh sách.'),
    ('b1111111-1111-1111-1111-111111111307', 'partner',
     'VietnamWorks — việc TP Thái Nguyên',
     'https://www.vietnamworks.com/viec-lam-thanh-pho-thai-nguyen-thai-nguyen-v55-vn',
     'active', 'web', 'job', 'worker', 'thai_nguyen',
     'Cổng việc quốc gia lọc tỉnh. Dán link một tin. Không crawl.')
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name,
    url = EXCLUDED.url,
    source_kind = EXCLUDED.source_kind,
    category = EXCLUDED.category,
    audience = EXCLUDED.audience,
    notes = EXCLUDED.notes,
    status = EXCLUDED.status,
    updated_at = NOW();
