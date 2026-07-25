-- FamilyOS summer schedule 2026 — Gia đình Khiêm Hiền
-- Source: handwritten «Thời gian biểu 2026» (nhánh Hôm ở nhà)
-- Depends on: 010 (summer_day routine ac03, children ab03/ab04)
-- Idempotent: soft-delete old summer templates, upsert new set.

-- Soft-delete previous summer_day templates
UPDATE pack_family.commitment_template
SET deleted_at = NOW(), is_active = FALSE, updated_at = NOW()
WHERE routine_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03'
  AND deleted_at IS NULL;

-- Ensure summer_day routine label
UPDATE pack_family.routine
SET display_name = 'Ngày hè',
    kind = 'holiday',
    is_active = TRUE,
    deleted_at = NULL,
    updated_at = NOW()
WHERE id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03';

-- =============================================================================
-- Bảo Nhi (ab03) — Thời gian biểu hè 2026
-- =============================================================================
INSERT INTO pack_family.commitment_template (
    id, tenant_id, routine_id, member_id, title, description,
    window_start, window_end, sort_order, is_active,
    priority, expected_duration_minutes, context_anchor, depends_on_template_ids,
    star_reward, allow_early_complete, early_lead_minutes, on_time_grace_minutes
)
VALUES
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab201', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Dậy', 'Thời gian biểu hè — thức dậy 8h', TIME '08:00', TIME '08:15', 10, TRUE,
 'critical', 15, 'after_wake', '{}'::uuid[], 10, FALSE, 0, 5),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab202', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Đánh răng rửa mặt', NULL, TIME '08:15', TIME '08:30', 20, TRUE,
 'normal', 15, 'after_wake', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab201']::uuid[], 10, FALSE, 0, 5),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab203', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Ăn sáng', NULL, TIME '08:30', TIME '08:45', 30, TRUE,
 'critical', 15, 'before_breakfast', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab202']::uuid[], 10, FALSE, 0, 5),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab204', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Dọn nhà', 'Dọn nhà cùng cả nhà 9h–11h', TIME '09:00', TIME '11:00', 40, TRUE,
 'critical', 120, 'after_breakfast', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab203']::uuid[], 20, TRUE, 15, 15),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab205', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Phụ nấu cơm trưa', 'Cắm cơm, phụ nấu', TIME '11:30', TIME '12:15', 50, TRUE,
 'normal', 45, 'before_lunch', '{}'::uuid[], 15, FALSE, 0, 10),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab206', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Ăn cơm trưa', 'Dọn cơm, ăn cơm', TIME '12:15', TIME '12:45', 60, TRUE,
 'critical', 30, 'before_lunch', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab205']::uuid[], 10, FALSE, 0, 10),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab207', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Rửa bát', NULL, TIME '12:45', TIME '13:00', 70, TRUE,
 'normal', 15, 'after_lunch', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab206']::uuid[], 10, FALSE, 0, 5),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab208', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Ngủ trưa', NULL, TIME '13:00', TIME '15:00', 80, TRUE,
 'critical', 120, 'after_lunch', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab207']::uuid[], 15, TRUE, 10, 15),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab209', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Rửa mặt chiều', NULL, TIME '15:00', TIME '15:15', 90, TRUE,
 'normal', 15, 'after_nap', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab208']::uuid[], 5, FALSE, 0, 5),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab20a', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Rút / gấp quần áo', NULL, TIME '15:15', TIME '16:00', 100, TRUE,
 'normal', 45, 'after_nap', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab209']::uuid[], 10, FALSE, 0, 10),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab20b', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Học bài / ôn bài', NULL, TIME '16:00', TIME '16:30', 110, TRUE,
 'critical', 30, 'after_school', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab20a']::uuid[], 20, FALSE, 0, 10),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab20c', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Phụ nấu cơm chiều', NULL, TIME '17:30', TIME '18:00', 120, TRUE,
 'normal', 30, 'before_dinner', '{}'::uuid[], 15, FALSE, 0, 10),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab20d', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Tắm', NULL, TIME '18:00', TIME '18:20', 130, TRUE,
 'normal', 20, 'before_dinner', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab20c']::uuid[], 10, FALSE, 0, 5),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab20e', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Ăn cơm tối', NULL, TIME '18:20', TIME '18:45', 140, TRUE,
 'critical', 25, 'before_dinner', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab20d']::uuid[], 10, FALSE, 0, 10),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab20f', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Ôn bài / làm bài tập', 'Buổi tối hôm ở nhà', TIME '19:00', TIME '21:00', 150, TRUE,
 'critical', 120, 'after_dinner', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab20e']::uuid[], 25, FALSE, 0, 15),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab210', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Giải trí', NULL, TIME '21:15', TIME '22:30', 160, TRUE,
 'optional', 75, 'after_dinner', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab20f']::uuid[], 5, TRUE, 0, 15),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab211', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Đánh răng tối', NULL, TIME '22:30', TIME '22:40', 170, TRUE,
 'normal', 10, 'before_sleep', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab210']::uuid[], 10, FALSE, 0, 5),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab212', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Đi ngủ', NULL, TIME '22:45', TIME '23:00', 180, TRUE,
 'critical', 15, 'before_sleep', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab211']::uuid[], 15, FALSE, 0, 10)
ON CONFLICT (id) DO UPDATE SET
    routine_id = EXCLUDED.routine_id,
    member_id = EXCLUDED.member_id,
    title = EXCLUDED.title,
    description = EXCLUDED.description,
    window_start = EXCLUDED.window_start,
    window_end = EXCLUDED.window_end,
    sort_order = EXCLUDED.sort_order,
    priority = EXCLUDED.priority,
    expected_duration_minutes = EXCLUDED.expected_duration_minutes,
    context_anchor = EXCLUDED.context_anchor,
    depends_on_template_ids = EXCLUDED.depends_on_template_ids,
    star_reward = EXCLUDED.star_reward,
    allow_early_complete = EXCLUDED.allow_early_complete,
    early_lead_minutes = EXCLUDED.early_lead_minutes,
    on_time_grace_minutes = EXCLUDED.on_time_grace_minutes,
    is_active = TRUE,
    deleted_at = NULL,
    updated_at = NOW();

-- =============================================================================
-- Đức Huy (ab04) — cùng nhịp hè (đồng bộ việc nhà với chị)
-- =============================================================================
INSERT INTO pack_family.commitment_template (
    id, tenant_id, routine_id, member_id, title, description,
    window_start, window_end, sort_order, is_active,
    priority, expected_duration_minutes, context_anchor, depends_on_template_ids,
    star_reward, allow_early_complete, early_lead_minutes, on_time_grace_minutes
)
VALUES
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab301', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Dậy', 'Thời gian biểu hè — thức dậy 8h', TIME '08:00', TIME '08:15', 210, TRUE,
 'critical', 15, 'after_wake', '{}'::uuid[], 10, FALSE, 0, 5),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab302', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Đánh răng rửa mặt', NULL, TIME '08:15', TIME '08:30', 220, TRUE,
 'normal', 15, 'after_wake', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab301']::uuid[], 10, FALSE, 0, 5),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab303', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Ăn sáng', NULL, TIME '08:30', TIME '08:45', 230, TRUE,
 'critical', 15, 'before_breakfast', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab302']::uuid[], 10, FALSE, 0, 5),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab304', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Dọn nhà', 'Dọn nhà cùng chị 9h–11h', TIME '09:00', TIME '11:00', 240, TRUE,
 'critical', 120, 'after_breakfast', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab303']::uuid[], 20, TRUE, 15, 15),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab305', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Phụ nấu cơm trưa', 'Cắm cơm, phụ nấu', TIME '11:30', TIME '12:15', 250, TRUE,
 'normal', 45, 'before_lunch', '{}'::uuid[], 15, FALSE, 0, 10),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab306', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Ăn cơm trưa', 'Dọn cơm, ăn cơm', TIME '12:15', TIME '12:45', 260, TRUE,
 'critical', 30, 'before_lunch', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab305']::uuid[], 10, FALSE, 0, 10),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab307', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Rửa bát', NULL, TIME '12:45', TIME '13:00', 270, TRUE,
 'normal', 15, 'after_lunch', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab306']::uuid[], 10, FALSE, 0, 5),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab308', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Ngủ trưa', NULL, TIME '13:00', TIME '15:00', 280, TRUE,
 'critical', 120, 'after_lunch', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab307']::uuid[], 15, TRUE, 10, 15),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab309', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Rửa mặt chiều', NULL, TIME '15:00', TIME '15:15', 290, TRUE,
 'normal', 15, 'after_nap', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab308']::uuid[], 5, FALSE, 0, 5),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab30a', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Rút / gấp quần áo', NULL, TIME '15:15', TIME '16:00', 300, TRUE,
 'normal', 45, 'after_nap', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab309']::uuid[], 10, FALSE, 0, 10),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab30b', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Học bài / ôn bài', 'Bài ngắn / ôn với chị', TIME '16:00', TIME '16:30', 310, TRUE,
 'critical', 30, 'after_school', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab30a']::uuid[], 20, FALSE, 0, 10),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab30c', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Phụ nấu cơm chiều', NULL, TIME '17:30', TIME '18:00', 320, TRUE,
 'normal', 30, 'before_dinner', '{}'::uuid[], 15, FALSE, 0, 10),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab30d', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Tắm', NULL, TIME '18:00', TIME '18:20', 330, TRUE,
 'normal', 20, 'before_dinner', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab30c']::uuid[], 10, FALSE, 0, 5),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab30e', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Ăn cơm tối', NULL, TIME '18:20', TIME '18:45', 340, TRUE,
 'critical', 25, 'before_dinner', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab30d']::uuid[], 10, FALSE, 0, 10),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab30f', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Ôn bài / làm bài tập', 'Buổi tối hôm ở nhà — bài ngắn hơn chị', TIME '19:00', TIME '20:30', 350, TRUE,
 'critical', 90, 'after_dinner', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab30e']::uuid[], 25, FALSE, 0, 15),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab310', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Giải trí', NULL, TIME '20:45', TIME '22:15', 360, TRUE,
 'optional', 90, 'after_dinner', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab30f']::uuid[], 5, TRUE, 0, 15),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab311', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Đánh răng tối', NULL, TIME '22:15', TIME '22:25', 370, TRUE,
 'normal', 10, 'before_sleep', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab310']::uuid[], 10, FALSE, 0, 5),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab312', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Đi ngủ', NULL, TIME '22:30', TIME '22:45', 380, TRUE,
 'critical', 15, 'before_sleep', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab311']::uuid[], 15, FALSE, 0, 10)
ON CONFLICT (id) DO UPDATE SET
    routine_id = EXCLUDED.routine_id,
    member_id = EXCLUDED.member_id,
    title = EXCLUDED.title,
    description = EXCLUDED.description,
    window_start = EXCLUDED.window_start,
    window_end = EXCLUDED.window_end,
    sort_order = EXCLUDED.sort_order,
    priority = EXCLUDED.priority,
    expected_duration_minutes = EXCLUDED.expected_duration_minutes,
    context_anchor = EXCLUDED.context_anchor,
    depends_on_template_ids = EXCLUDED.depends_on_template_ids,
    star_reward = EXCLUDED.star_reward,
    allow_early_complete = EXCLUDED.allow_early_complete,
    early_lead_minutes = EXCLUDED.early_lead_minutes,
    on_time_grace_minutes = EXCLUDED.on_time_grace_minutes,
    is_active = TRUE,
    deleted_at = NULL,
    updated_at = NOW();

-- Summer period uses Ngày hè for the whole week (T2–CN)
UPDATE pack_family.calendar_period
SET is_active = TRUE, deleted_at = NULL, updated_at = NOW(),
    notes = 'Thời gian biểu hè 2026 (tay ghi) — cả tuần T2–CN dùng Ngày hè.'
WHERE id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaae102';

UPDATE pack_family.calendar_period_slot
SET weekdays = ARRAY[1,2,3,4,5,6,7]::SMALLINT[],
    routine_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
    deleted_at = NULL,
    updated_at = NOW()
WHERE id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaf103';

-- Drop the old weekend-only slot so Sat/Sun no longer fall back to Cuối tuần
UPDATE pack_family.calendar_period_slot
SET deleted_at = NOW(), updated_at = NOW()
WHERE period_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaae102'
  AND id <> 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaf103'
  AND deleted_at IS NULL;

\echo === SUMMER PERIOD SLOTS ===
SELECT p.code, s.weekdays, r.code AS routine
FROM pack_family.calendar_period_slot s
JOIN pack_family.calendar_period p ON p.id = s.period_id
JOIN pack_family.routine r ON r.id = s.routine_id
WHERE p.id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaae102'
  AND s.deleted_at IS NULL
ORDER BY s.sort_order;

\echo === SUMMER DAY TEMPLATES ===
SELECT m.display_name, t.title, t.window_start, t.window_end, t.priority
FROM pack_family.commitment_template t
JOIN pack_family.membership m ON m.id = t.member_id
WHERE t.routine_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03'
  AND t.deleted_at IS NULL
ORDER BY m.sort_order, t.sort_order;
