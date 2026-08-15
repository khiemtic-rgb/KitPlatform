-- Local OS: national job boards filtered to Thái Nguyên — partner sources, never crawl.
-- Isolated park. Do not apply via Pharmacy / Family / Content manifests.

INSERT INTO pack_local.source (
    id, source_kind, name, url, status, platform, category, audience, geo, notes
)
VALUES
    ('b1111111-1111-1111-1111-111111111301', 'partner',
     'Indeed Việt Nam — việc Thái Nguyên / sinh viên',
     'https://vn.indeed.com/',
     'active', 'web', 'job', 'student', 'thai_nguyen',
     'Cổng việc quốc gia. Dán link một tin Indeed, không quét trang tìm kiếm. Bỏ query vjk= (đó là một tin).'),
    ('b1111111-1111-1111-1111-111111111302', 'partner',
     'TopCV — việc mới tại Thái Nguyên',
     'https://www.topcv.vn/tim-viec-lam-moi-nhat-tai-thai-nguyen-l59',
     'active', 'web', 'job', 'mixed', 'thai_nguyen',
     'Cổng việc quốc gia lọc tỉnh. Dán link một tin TopCV. Không crawl.'),
    ('b1111111-1111-1111-1111-111111111303', 'partner',
     'Viecoi — part-time Thái Nguyên',
     'https://viecoi.vn/tim-viec/key-part-time-khu-vuc-thai-nguyen-52.html',
     'active', 'web', 'job', 'student', 'thai_nguyen',
     'Cổng việc quốc gia, part-time. Dán link một tin. Không crawl.'),
    ('b1111111-1111-1111-1111-111111111304', 'partner',
     'Việc Làm 24h — Thái Nguyên',
     'https://vieclam24h.vn/viec-lam-thai-nguyen-p84.html',
     'active', 'web', 'job', 'mixed', 'thai_nguyen',
     'Cổng việc quốc gia lọc tỉnh. Trang thực tập marketing là một tin/mục con — không đăng ký riêng.'),
    ('b1111111-1111-1111-1111-111111111305', 'partner',
     'JobsGO — part-time Thái Nguyên',
     'https://jobsgo.vn/viec-lam-part-time-tai-thai-nguyen.html',
     'active', 'web', 'job', 'student', 'thai_nguyen',
     'Cổng việc quốc gia, part-time. Dán link một tin. Không crawl.'),
    ('b1111111-1111-1111-1111-111111111306', 'partner',
     'Timviec365 — việc sinh viên Thái Nguyên',
     'https://timviec365.vn/viec-lam-sinh-vien-lam-them-tai-thai-nguyen-c3v22',
     'active', 'web', 'job', 'student', 'thai_nguyen',
     'Cổng việc quốc gia, việc làm thêm SV. Dán link một tin. Không crawl.')
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name,
    url = EXCLUDED.url,
    source_kind = EXCLUDED.source_kind,
    category = EXCLUDED.category,
    audience = EXCLUDED.audience,
    notes = EXCLUDED.notes,
    status = EXCLUDED.status,
    updated_at = NOW();
