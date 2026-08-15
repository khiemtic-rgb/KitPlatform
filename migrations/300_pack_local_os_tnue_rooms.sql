-- Local OS: TNUE dorm/room directory — info only, never publish listed prices.
-- Isolated park. Do not apply via Pharmacy / Family / Content manifests.

INSERT INTO pack_local.source (
    id, source_kind, name, url, status, platform, category, audience, geo, notes
)
VALUES
    ('b1111111-1111-1111-1111-111111111203', 'official_web',
     'TNUE — phòng trọ quanh ĐH Sư phạm',
     'https://tuyensinh.tnue.edu.vn/thong-tin-cac-phong-tro-khu-vuc-xung-quanh-truong-dai-hoc-su-pham-dhtn',
     'active', 'web', 'room', 'student', 'thai_nguyen',
     'Danh sách chủ trọ do trường công bố. Lấy địa chỉ / SĐT / tiện nghi / khoảng cách. KHÔNG đăng giá (giá đổi theo thời điểm). Không tách hàng loạt thành listing — dán từng phòng hoặc một tin dẫn về trang gốc.')
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name,
    url = EXCLUDED.url,
    source_kind = EXCLUDED.source_kind,
    category = EXCLUDED.category,
    audience = EXCLUDED.audience,
    notes = EXCLUDED.notes,
    status = EXCLUDED.status,
    updated_at = NOW();
