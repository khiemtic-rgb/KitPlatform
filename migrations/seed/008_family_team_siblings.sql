-- Family Team Play — second child Missions + backfill today's Day Flow
-- DEMO_FAMILY · local seed only (not prod)
-- Safe to re-run after 006 / 007

-- Rename stock Minh → Bảo Nhi on primary child slot
UPDATE pack_family.membership
SET display_name = 'Bảo Nhi',
    updated_at = NOW()
WHERE id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03'
  AND family_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01'
  AND display_name = 'Minh'
  AND deleted_at IS NULL;

-- Ensure canonical Đức Huy (ab04)
INSERT INTO pack_family.membership (
    id, tenant_id, family_id, display_name, role_code, date_of_birth, sort_order, status
)
VALUES (
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'Đức Huy',
    'child',
    DATE '2018-09-20',
    4,
    'active'
)
ON CONFLICT (id) DO UPDATE SET
    role_code = 'child',
    status = 'active',
    deleted_at = NULL,
    sort_order = COALESCE(pack_family.membership.sort_order, 4),
    updated_at = NOW();

-- If another active "Đức Huy" exists (admin-created), keep them visible but
-- attach Missions to ab04 and soft-delete the duplicate name collision.
UPDATE pack_family.membership
SET status = 'archived',
    deleted_at = COALESCE(deleted_at, NOW()),
    updated_at = NOW()
WHERE family_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01'
  AND id <> 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04'
  AND role_code = 'child'
  AND deleted_at IS NULL
  AND lower(display_name) IN ('đức huy', 'duc huy');

UPDATE pack_family.membership
SET display_name = 'Đức Huy',
    updated_at = NOW()
WHERE id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04'
  AND family_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01';

INSERT INTO pack_family.commitment_template (
    id, tenant_id, routine_id, member_id, title, description,
    window_start, window_end, sort_order, is_active,
    priority, expected_duration_minutes, context_anchor, depends_on_template_ids
)
VALUES
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae01', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Dậy', 'Dậy cùng chị / đúng giờ', TIME '06:10', TIME '06:25', 210, TRUE,
 'critical', 15, 'after_wake', '{}'::uuid[]),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae02', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Đánh răng sáng', NULL, TIME '06:25', TIME '06:35', 220, TRUE,
 'normal', 10, 'after_wake', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae01']::uuid[]),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae03', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Ăn sáng', NULL, TIME '06:35', TIME '06:55', 230, TRUE,
 'critical', 20, 'before_breakfast', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae02']::uuid[]),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae04', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Mặc đồng phục', NULL, TIME '06:55', TIME '07:05', 240, TRUE,
 'normal', 10, 'after_breakfast', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae03']::uuid[]),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae05', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Chuẩn bị cặp', 'Sách vở nhỏ + hộp cơm', TIME '07:05', TIME '07:15', 250, TRUE,
 'critical', 10, 'before_school', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae04']::uuid[]),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae06', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Làm bài tập', 'Bài ngắn / ôn với chị', TIME '16:00', TIME '16:40', 260, TRUE,
 'critical', 40, 'after_school', '{}'::uuid[]),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae07', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Đi tắm', NULL, TIME '16:40', TIME '17:10', 270, TRUE,
 'normal', 30, 'after_school', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae06']::uuid[]),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae08', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Ăn cơm', NULL, TIME '18:00', TIME '18:45', 280, TRUE,
 'critical', 45, 'before_dinner', '{}'::uuid[]),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae09', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Đọc sách', 'Khoảng 15 phút', TIME '19:00', TIME '19:20', 290, TRUE,
 'optional', 20, 'after_dinner', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae08']::uuid[]),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae10', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Đánh răng tối', NULL, TIME '20:20', TIME '20:30', 300, TRUE,
 'normal', 10, 'before_sleep', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae09']::uuid[]),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae11', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Đi ngủ', NULL, TIME '20:45', TIME '21:00', 310, TRUE,
 'critical', 15, 'before_sleep', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae10']::uuid[])
ON CONFLICT (id) DO UPDATE SET
    member_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04',
    title = EXCLUDED.title,
    description = EXCLUDED.description,
    window_start = EXCLUDED.window_start,
    window_end = EXCLUDED.window_end,
    sort_order = EXCLUDED.sort_order,
    priority = EXCLUDED.priority,
    expected_duration_minutes = EXCLUDED.expected_duration_minutes,
    context_anchor = EXCLUDED.context_anchor,
    depends_on_template_ids = EXCLUDED.depends_on_template_ids,
    is_active = TRUE,
    deleted_at = NULL,
    updated_at = NOW();

-- Backfill today's day_flow for Đức Huy
INSERT INTO pack_family.commitment (
    tenant_id, day_flow_id, template_id, member_id, title, description,
    window_start, window_end, sort_order,
    priority, expected_duration_minutes, context_anchor, depends_on_template_ids
)
SELECT
    '11111111-1111-1111-1111-111111111104',
    d.id,
    t.id,
    t.member_id,
    t.title,
    t.description,
    t.window_start,
    t.window_end,
    t.sort_order,
    t.priority,
    t.expected_duration_minutes,
    t.context_anchor,
    t.depends_on_template_ids
FROM pack_family.day_flow d
JOIN pack_family.family f ON f.id = d.family_id AND f.deleted_at IS NULL
JOIN pack_family.commitment_template t
  ON t.routine_id = d.routine_id
 AND t.member_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04'
 AND t.deleted_at IS NULL
 AND t.is_active
WHERE d.family_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01'
  AND d.deleted_at IS NULL
  AND d.flow_date = (NOW() AT TIME ZONE COALESCE(f.timezone, 'Asia/Ho_Chi_Minh'))::date
  AND NOT EXISTS (
      SELECT 1
      FROM pack_family.commitment c
      WHERE c.day_flow_id = d.id
        AND c.template_id = t.id
        AND c.deleted_at IS NULL
  );
