-- FamilyOS calendar periods + summer_day routine (DEMO_FAMILY)
-- Depends on: 006 + 007 + 008 + 009 + migration 221
-- Idempotent. Summer 2026 covers current pilot window.

-- -----------------------------------------------------------------------------
-- Routine: Ngày hè (T2–T6 during summer) — wake 08:00 like weekend
-- -----------------------------------------------------------------------------
INSERT INTO pack_family.routine (
    id, tenant_id, family_id, code, display_name, kind, weekdays, is_active, sort_order
)
VALUES (
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'summer_day',
    'Ngày hè',
    'holiday',
    ARRAY[]::SMALLINT[],  -- selected via calendar_period slots, not weekdays fallback
    TRUE,
    3
)
ON CONFLICT (id) DO UPDATE SET
    display_name = EXCLUDED.display_name,
    kind = EXCLUDED.kind,
    weekdays = EXCLUDED.weekdays,
    is_active = TRUE,
    deleted_at = NULL,
    updated_at = NOW();

-- Bảo Nhi summer templates (clone of weekend af01–af08 → ag01–ag08)
INSERT INTO pack_family.commitment_template (
    id, tenant_id, routine_id, member_id, title, description,
    window_start, window_end, sort_order, is_active,
    priority, expected_duration_minutes, context_anchor, depends_on_template_ids,
    star_reward, allow_early_complete, early_lead_minutes, on_time_grace_minutes
)
VALUES
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab101', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Dậy', 'Ngày hè dậy lúc 8h', TIME '08:00', TIME '08:15', 10, TRUE,
 'critical', 15, 'after_wake', '{}'::uuid[], 10, FALSE, 0, 0),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab102', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Đánh răng sáng', NULL, TIME '08:15', TIME '08:25', 20, TRUE,
 'normal', 10, 'after_wake', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab101']::uuid[], 15, FALSE, 0, 0),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab103', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Ăn sáng', NULL, TIME '08:25', TIME '08:50', 30, TRUE,
 'critical', 25, 'before_breakfast', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab102']::uuid[], 10, FALSE, 0, 0),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab104', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Đi tắm', NULL, TIME '17:00', TIME '17:30', 70, TRUE,
 'normal', 30, 'after_school', '{}'::uuid[], 10, FALSE, 0, 0),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab105', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Ăn cơm', NULL, TIME '18:00', TIME '18:45', 80, TRUE,
 'critical', 45, 'before_dinner', '{}'::uuid[], 10, FALSE, 0, 0),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab106', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Đọc sách', NULL, TIME '19:00', TIME '19:30', 90, TRUE,
 'optional', 30, 'after_dinner', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab105']::uuid[], 10, TRUE, 0, 10),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab107', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Đánh răng tối', NULL, TIME '20:30', TIME '20:40', 100, TRUE,
 'normal', 10, 'before_sleep', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab106']::uuid[], 15, FALSE, 0, 0),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab108', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Đi ngủ', NULL, TIME '21:00', TIME '21:15', 110, TRUE,
 'critical', 15, 'before_sleep', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab107']::uuid[], 15, FALSE, 0, 0),
-- Đức Huy
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab111', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Dậy', 'Ngày hè dậy lúc 8h', TIME '08:00', TIME '08:15', 210, TRUE,
 'critical', 15, 'after_wake', '{}'::uuid[], 10, FALSE, 0, 0),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab112', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Đánh răng sáng', NULL, TIME '08:15', TIME '08:25', 220, TRUE,
 'normal', 10, 'after_wake', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab111']::uuid[], 15, FALSE, 0, 0),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab113', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Ăn sáng', NULL, TIME '08:25', TIME '08:50', 230, TRUE,
 'critical', 25, 'before_breakfast', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab112']::uuid[], 10, FALSE, 0, 0),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab114', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Đi tắm', NULL, TIME '16:40', TIME '17:10', 270, TRUE,
 'normal', 30, 'after_school', '{}'::uuid[], 10, FALSE, 0, 0),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab115', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Ăn cơm', NULL, TIME '18:00', TIME '18:45', 280, TRUE,
 'critical', 45, 'before_dinner', '{}'::uuid[], 10, FALSE, 0, 0),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab116', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Đọc sách', 'Khoảng 15 phút', TIME '19:00', TIME '19:20', 290, TRUE,
 'optional', 20, 'after_dinner', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab115']::uuid[], 10, TRUE, 0, 10),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab117', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Đánh răng tối', NULL, TIME '20:20', TIME '20:30', 300, TRUE,
 'normal', 10, 'before_sleep', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab116']::uuid[], 15, FALSE, 0, 0),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab118', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Đi ngủ', NULL, TIME '20:45', TIME '21:00', 310, TRUE,
 'critical', 15, 'before_sleep', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaab117']::uuid[], 15, FALSE, 0, 0)
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

-- -----------------------------------------------------------------------------
-- Periods
-- -----------------------------------------------------------------------------
INSERT INTO pack_family.calendar_period (
    id, tenant_id, family_id, code, display_name, kind,
    start_date, end_date, priority, is_active, notes
)
VALUES
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaae101',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'school_year_2025',
    'Năm học 2025–2026',
    'school_year',
    DATE '2025-08-25',
    DATE '2026-05-31',
    20,
    TRUE,
    'Ngày đi học T2–T6; cuối tuần giữ nhịp nhẹ.'
),
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaae102',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'summer_2026',
    'Nghỉ hè 2026',
    'summer',
    DATE '2026-06-01',
    DATE '2026-08-24',
    40,
    TRUE,
    'Cả tuần (T2–CN) dùng Ngày hè — dậy 8h theo thời gian biểu hè.'
),
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaae103',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'school_year_2026',
    'Năm học 2026–2027',
    'school_year',
    DATE '2026-08-25',
    DATE '2027-05-31',
    20,
    TRUE,
    NULL
)
ON CONFLICT (id) DO UPDATE SET
    display_name = EXCLUDED.display_name,
    kind = EXCLUDED.kind,
    start_date = EXCLUDED.start_date,
    end_date = EXCLUDED.end_date,
    priority = EXCLUDED.priority,
    is_active = TRUE,
    notes = EXCLUDED.notes,
    deleted_at = NULL,
    updated_at = NOW();

-- Soft-replace slots for seeded periods
UPDATE pack_family.calendar_period_slot
SET deleted_at = NOW(), updated_at = NOW()
WHERE period_id IN (
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaae101',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaae102',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaae103'
)
  AND deleted_at IS NULL;

INSERT INTO pack_family.calendar_period_slot (
    id, tenant_id, period_id, weekdays, routine_id, sort_order
)
VALUES
-- school_year_2025
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaf101', '11111111-1111-1111-1111-111111111104',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaae101', ARRAY[1,2,3,4,5]::SMALLINT[],
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01', 1),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaf102', '11111111-1111-1111-1111-111111111104',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaae101', ARRAY[6,7]::SMALLINT[],
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac02', 2),
-- summer_2026 — Ngày hè áp dụng cả tuần (T2–CN)
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaf103', '11111111-1111-1111-1111-111111111104',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaae102', ARRAY[1,2,3,4,5,6,7]::SMALLINT[],
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac03', 1),
-- school_year_2026
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaf105', '11111111-1111-1111-1111-111111111104',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaae103', ARRAY[1,2,3,4,5]::SMALLINT[],
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01', 1),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaf106', '11111111-1111-1111-1111-111111111104',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaae103', ARRAY[6,7]::SMALLINT[],
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac02', 2)
ON CONFLICT (id) DO UPDATE SET
    period_id = EXCLUDED.period_id,
    weekdays = EXCLUDED.weekdays,
    routine_id = EXCLUDED.routine_id,
    sort_order = EXCLUDED.sort_order,
    deleted_at = NULL,
    updated_at = NOW();
