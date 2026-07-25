-- FamilyOS weekend commitments (DEMO_FAMILY) — wake 08:00
-- Depends on: 006 + 007 + 008 (weekend routine ac02, children ab03/ab04)
-- Idempotent upsert on fixed demo IDs (af**); backfills today if Sat/Sun.

-- Ensure weekend routine exists / active
INSERT INTO pack_family.routine (
    id, tenant_id, family_id, code, display_name, kind, weekdays, is_active, sort_order
)
VALUES (
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac02',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'weekend',
    'Cuối tuần',
    'weekend',
    ARRAY[6, 7]::SMALLINT[],
    TRUE,
    2
)
ON CONFLICT (id) DO UPDATE SET
    display_name = EXCLUDED.display_name,
    kind = EXCLUDED.kind,
    weekdays = EXCLUDED.weekdays,
    is_active = TRUE,
    deleted_at = NULL,
    updated_at = NOW();

-- Bảo Nhi (ab03) — weekend
INSERT INTO pack_family.commitment_template (
    id, tenant_id, routine_id, member_id, title, description,
    window_start, window_end, sort_order, is_active,
    priority, expected_duration_minutes, context_anchor, depends_on_template_ids,
    star_reward, allow_early_complete, early_lead_minutes, on_time_grace_minutes
)
VALUES
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf01', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac02',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Dậy', 'Cuối tuần dậy lúc 8h', TIME '08:00', TIME '08:15', 10, TRUE,
 'critical', 15, 'after_wake', '{}'::uuid[], 10, FALSE, 0, 0),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf02', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac02',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Đánh răng sáng', NULL, TIME '08:15', TIME '08:25', 20, TRUE,
 'normal', 10, 'after_wake', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf01']::uuid[], 15, FALSE, 0, 0),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf03', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac02',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Ăn sáng', NULL, TIME '08:25', TIME '08:50', 30, TRUE,
 'critical', 25, 'before_breakfast', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf02']::uuid[], 10, FALSE, 0, 0),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf04', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac02',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Đi tắm', NULL, TIME '17:00', TIME '17:30', 70, TRUE,
 'normal', 30, 'after_school', '{}'::uuid[], 10, FALSE, 0, 0),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf05', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac02',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Ăn cơm', NULL, TIME '18:00', TIME '18:45', 80, TRUE,
 'critical', 45, 'before_dinner', '{}'::uuid[], 10, FALSE, 0, 0),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf06', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac02',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Đọc sách', NULL, TIME '19:00', TIME '19:30', 90, TRUE,
 'optional', 30, 'after_dinner', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf05']::uuid[], 10, TRUE, 0, 10),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf07', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac02',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Đánh răng tối', NULL, TIME '20:30', TIME '20:40', 100, TRUE,
 'normal', 10, 'before_sleep', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf06']::uuid[], 15, FALSE, 0, 0),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf08', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac02',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Đi ngủ', NULL, TIME '21:00', TIME '21:15', 110, TRUE,
 'critical', 15, 'before_sleep', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf07']::uuid[], 15, FALSE, 0, 0)
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

-- Đức Huy (ab04) — weekend, same 08:00 wake
INSERT INTO pack_family.commitment_template (
    id, tenant_id, routine_id, member_id, title, description,
    window_start, window_end, sort_order, is_active,
    priority, expected_duration_minutes, context_anchor, depends_on_template_ids,
    star_reward, allow_early_complete, early_lead_minutes, on_time_grace_minutes
)
VALUES
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf11', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac02',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Dậy', 'Cuối tuần dậy lúc 8h', TIME '08:00', TIME '08:15', 210, TRUE,
 'critical', 15, 'after_wake', '{}'::uuid[], 10, FALSE, 0, 0),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf12', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac02',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Đánh răng sáng', NULL, TIME '08:15', TIME '08:25', 220, TRUE,
 'normal', 10, 'after_wake', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf11']::uuid[], 15, FALSE, 0, 0),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf13', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac02',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Ăn sáng', NULL, TIME '08:25', TIME '08:50', 230, TRUE,
 'critical', 25, 'before_breakfast', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf12']::uuid[], 10, FALSE, 0, 0),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf14', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac02',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Đi tắm', NULL, TIME '16:40', TIME '17:10', 270, TRUE,
 'normal', 30, 'after_school', '{}'::uuid[], 10, FALSE, 0, 0),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf15', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac02',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Ăn cơm', NULL, TIME '18:00', TIME '18:45', 280, TRUE,
 'critical', 45, 'before_dinner', '{}'::uuid[], 10, FALSE, 0, 0),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf16', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac02',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Đọc sách', 'Khoảng 15 phút', TIME '19:00', TIME '19:20', 290, TRUE,
 'optional', 20, 'after_dinner', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf15']::uuid[], 10, TRUE, 0, 10),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf17', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac02',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Đánh răng tối', NULL, TIME '20:20', TIME '20:30', 300, TRUE,
 'normal', 10, 'before_sleep', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf16']::uuid[], 15, FALSE, 0, 0),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf18', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac02',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Đi ngủ', NULL, TIME '20:45', TIME '21:00', 310, TRUE,
 'critical', 15, 'before_sleep', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf17']::uuid[], 15, FALSE, 0, 0)
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

-- If today is Sat/Sun and day_flow already open: point it at weekend routine
-- and materialize any missing weekend commitments (do not wipe progress).
WITH today AS (
    SELECT
        f.id AS family_id,
        f.tenant_id,
        (NOW() AT TIME ZONE COALESCE(f.timezone, 'Asia/Ho_Chi_Minh'))::date AS flow_date,
        EXTRACT(ISODOW FROM (NOW() AT TIME ZONE COALESCE(f.timezone, 'Asia/Ho_Chi_Minh')))::int AS dow
    FROM pack_family.family f
    WHERE f.id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01'
      AND f.deleted_at IS NULL
),
weekend_flow AS (
    UPDATE pack_family.day_flow d
    SET routine_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac02',
        updated_at = NOW()
    FROM today t
    WHERE d.family_id = t.family_id
      AND d.flow_date = t.flow_date
      AND d.deleted_at IS NULL
      AND t.dow IN (6, 7)
      AND d.routine_id IS DISTINCT FROM 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac02'
    RETURNING d.id, d.tenant_id, d.routine_id
),
target_flow AS (
    SELECT d.id, d.tenant_id, d.routine_id
    FROM pack_family.day_flow d
    JOIN today t ON t.family_id = d.family_id AND t.flow_date = d.flow_date
    WHERE d.deleted_at IS NULL
      AND t.dow IN (6, 7)
    UNION
    SELECT id, tenant_id, routine_id FROM weekend_flow
)
INSERT INTO pack_family.commitment (
    tenant_id, day_flow_id, template_id, member_id, title, description,
    window_start, window_end, sort_order,
    priority, expected_duration_minutes, context_anchor, depends_on_template_ids,
    star_reward, allow_early_complete, early_lead_minutes, on_time_grace_minutes
)
SELECT
    tf.tenant_id,
    tf.id,
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
    t.depends_on_template_ids,
    t.star_reward,
    t.allow_early_complete,
    t.early_lead_minutes,
    t.on_time_grace_minutes
FROM target_flow tf
JOIN pack_family.commitment_template t
  ON t.routine_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac02'
 AND t.deleted_at IS NULL
 AND t.is_active
WHERE NOT EXISTS (
    SELECT 1
    FROM pack_family.commitment c
    WHERE c.day_flow_id = tf.id
      AND c.template_id = t.id
      AND c.deleted_at IS NULL
);

-- Soft-delete school-day commitments wrongly attached to today's weekend flow
-- (only pending/in_progress; keep done/skipped history)
UPDATE pack_family.commitment c
SET deleted_at = NOW(), updated_at = NOW()
FROM pack_family.day_flow d
JOIN pack_family.family f ON f.id = d.family_id
WHERE c.day_flow_id = d.id
  AND d.family_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01'
  AND d.deleted_at IS NULL
  AND c.deleted_at IS NULL
  AND c.status IN ('pending', 'in_progress')
  AND d.flow_date = (NOW() AT TIME ZONE COALESCE(f.timezone, 'Asia/Ho_Chi_Minh'))::date
  AND EXTRACT(ISODOW FROM d.flow_date) IN (6, 7)
  AND (
      c.template_id IS NULL
      OR NOT EXISTS (
          SELECT 1
          FROM pack_family.commitment_template t
          WHERE t.id = c.template_id
            AND t.routine_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac02'
            AND t.deleted_at IS NULL
      )
  );

\echo === WEEKEND TEMPLATES ===
SELECT m.display_name, t.title, t.window_start, t.window_end, t.sort_order
FROM pack_family.commitment_template t
JOIN pack_family.membership m ON m.id = t.member_id
WHERE t.routine_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac02'
  AND t.deleted_at IS NULL
ORDER BY m.sort_order, t.sort_order;

\echo === TODAY FLOW ===
SELECT d.flow_date, r.code AS routine, COUNT(c.id) FILTER (WHERE c.deleted_at IS NULL) AS commitments
FROM pack_family.day_flow d
JOIN pack_family.routine r ON r.id = d.routine_id
LEFT JOIN pack_family.commitment c ON c.day_flow_id = d.id
JOIN pack_family.family f ON f.id = d.family_id
WHERE d.family_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01'
  AND d.deleted_at IS NULL
  AND d.flow_date = (NOW() AT TIME ZONE COALESCE(f.timezone, 'Asia/Ho_Chi_Minh'))::date
GROUP BY d.flow_date, r.code;
