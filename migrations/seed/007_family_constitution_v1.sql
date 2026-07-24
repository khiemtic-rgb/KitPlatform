-- FamilyOS Default Family Constitution v1.0 — DEMO_FAMILY seed
-- Depends on: 198 + 006 (family, members, school_day routine + templates)
-- Idempotent: soft-delete old agreements, upsert constitution set

-- -----------------------------------------------------------------------------
-- Soft-delete ALL existing agreements for DEMO_FAMILY (clean slate)
-- -----------------------------------------------------------------------------
UPDATE pack_family.agreement
SET deleted_at = NOW(), updated_at = NOW()
WHERE family_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01'
  AND deleted_at IS NULL;

-- Weekend routine (R002)
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

-- Helper: constitution upsert columns
-- -----------------------------------------------------------------------------
-- 1. Foundation F001–F004
-- -----------------------------------------------------------------------------
INSERT INTO pack_family.agreement (
    id, tenant_id, family_id, proposed_by, title, proposal_body,
    target_type, target_id, status, terms, purpose, effective_on, review_after_days,
    applies_to_member_id, decided_at, decided_by, decision_note
)
VALUES
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf01',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab01',
    'F001 · Tôn trọng mọi thành viên trong gia đình',
    'Mọi người nói chuyện lịch sự, không xúc phạm, không quát mắng hay bạo lực. Áp dụng toàn bộ gia đình.',
    'foundation', NULL, 'accepted',
    jsonb_build_object(
        'schemaVersion', 2,
        'code', 'F001',
        'constitution', 'v1.0',
        'purpose', 'Giữ văn hóa tôn trọng trong nhà.',
        'conditions', jsonb_build_array('Áp dụng mọi lúc với mọi thành viên'),
        'exceptions', jsonb_build_array(),
        'result', jsonb_build_object('kind', 'none'),
        'supportsValues', jsonb_build_array('respect')
    ),
    'Giữ văn hóa tôn trọng trong nhà.',
    (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date,
    365, NULL,
    NOW() - INTERVAL '14 days', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02',
    'Family Constitution v1.0'
),
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf02',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab01',
    'F002 · Mọi người đều có quyền đề xuất thay đổi',
    'Nếu thấy một Routine hoặc Thỏa thuận không còn phù hợp, bất kỳ thành viên nào cũng có thể tạo đề xuất mới để cả gia đình cùng xem xét.',
    'foundation', NULL, 'accepted',
    jsonb_build_object(
        'schemaVersion', 2,
        'code', 'F002',
        'constitution', 'v1.0',
        'purpose', 'Cho phép điều chỉnh công bằng.',
        'conditions', jsonb_build_array('Khi muốn đổi Routine hoặc Thỏa thuận'),
        'exceptions', jsonb_build_array(),
        'result', jsonb_build_object('kind', 'none'),
        'supportsValues', jsonb_build_array('honesty', 'respect')
    ),
    'Cho phép điều chỉnh công bằng.',
    (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date,
    365, NULL,
    NOW() - INTERVAL '14 days', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02',
    'Family Constitution v1.0'
),
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf03',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab01',
    'F003 · Cam kết đã đồng ý thì cùng thực hiện',
    'Khi một thỏa thuận đã được tất cả các bên đồng ý, mọi người cùng có trách nhiệm thực hiện.',
    'foundation', NULL, 'accepted',
    jsonb_build_object(
        'schemaVersion', 2,
        'code', 'F003',
        'constitution', 'v1.0',
        'purpose', 'Gắn đồng thuận với hành động.',
        'conditions', jsonb_build_array('Áp dụng với thỏa thuận đã Đồng ý'),
        'exceptions', jsonb_build_array('sick', 'parent_approved'),
        'result', jsonb_build_object('kind', 'none'),
        'supportsValues', jsonb_build_array('responsibility')
    ),
    'Gắn đồng thuận với hành động.',
    (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date,
    365, NULL,
    NOW() - INTERVAL '14 days', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02',
    'Family Constitution v1.0'
),
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaf04',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab01',
    'F004 · Không tự ý thay đổi',
    'Routine hoặc Thỏa thuận chỉ được thay đổi sau khi có sự đồng ý của các bên liên quan.',
    'foundation', NULL, 'accepted',
    jsonb_build_object(
        'schemaVersion', 2,
        'code', 'F004',
        'constitution', 'v1.0',
        'purpose', 'Bảo vệ thỏa thuận đã đồng thuận.',
        'conditions', jsonb_build_array('Mọi thay đổi phải qua đề xuất và đồng ý'),
        'exceptions', jsonb_build_array(),
        'result', jsonb_build_object('kind', 'none'),
        'supportsValues', jsonb_build_array('responsibility', 'respect')
    ),
    'Bảo vệ thỏa thuận đã đồng thuận.',
    (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date,
    365, NULL,
    NOW() - INTERVAL '14 days', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02',
    'Family Constitution v1.0'
)
ON CONFLICT (id) DO UPDATE SET
    title = EXCLUDED.title,
    proposal_body = EXCLUDED.proposal_body,
    target_type = EXCLUDED.target_type,
    status = 'accepted',
    terms = EXCLUDED.terms,
    purpose = EXCLUDED.purpose,
    effective_on = EXCLUDED.effective_on,
    review_after_days = EXCLUDED.review_after_days,
    decided_at = EXCLUDED.decided_at,
    decided_by = EXCLUDED.decided_by,
    decision_note = EXCLUDED.decision_note,
    deleted_at = NULL,
    updated_at = NOW();

-- -----------------------------------------------------------------------------
-- 2. Routine Agreement R001–R002
-- -----------------------------------------------------------------------------
INSERT INTO pack_family.agreement (
    id, tenant_id, family_id, proposed_by, title, proposal_body,
    target_type, target_id, status, terms, purpose, effective_on, review_after_days,
    decided_at, decided_by, decision_note
)
VALUES
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae10',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab01',
    'R001 · Ngày đi học',
    'Áp dụng Thứ Hai → Thứ Sáu theo Routine School Day (Ngày đi học).',
    'routine', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01', 'accepted',
    jsonb_build_object(
        'schemaVersion', 2, 'code', 'R001', 'constitution', 'v1.0',
        'purpose', 'Nhịp sống ngày đi học.',
        'conditions', jsonb_build_array('Thứ Hai đến Thứ Sáu'),
        'exceptions', jsonb_build_array('sick', 'travel', 'birthday'),
        'result', jsonb_build_object('kind', 'none'),
        'schedule', jsonb_build_object('weekdays', jsonb_build_array(1,2,3,4,5)),
        'routineCode', 'school_day',
        'supportsValues', jsonb_build_array('punctuality', 'responsibility')
    ),
    'Nhịp sống ngày đi học.',
    (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date, 90,
    NOW() - INTERVAL '14 days', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02', 'Family Constitution v1.0'
),
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae11',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab01',
    'R002 · Cuối tuần',
    'Áp dụng Thứ Bảy – Chủ Nhật theo Routine Weekend.',
    'routine', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac02', 'accepted',
    jsonb_build_object(
        'schemaVersion', 2, 'code', 'R002', 'constitution', 'v1.0',
        'purpose', 'Nhịp sống cuối tuần.',
        'conditions', jsonb_build_array('Thứ Bảy và Chủ Nhật'),
        'exceptions', jsonb_build_array('travel'),
        'result', jsonb_build_object('kind', 'none'),
        'schedule', jsonb_build_object('weekdays', jsonb_build_array(6,7)),
        'routineCode', 'weekend',
        'supportsValues', jsonb_build_array('helping', 'respect')
    ),
    'Nhịp sống cuối tuần.',
    (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date, 90,
    NOW() - INTERVAL '14 days', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02', 'Family Constitution v1.0'
)
ON CONFLICT (id) DO UPDATE SET
    title = EXCLUDED.title, proposal_body = EXCLUDED.proposal_body,
    target_type = EXCLUDED.target_type, target_id = EXCLUDED.target_id,
    status = 'accepted', terms = EXCLUDED.terms, purpose = EXCLUDED.purpose,
    effective_on = EXCLUDED.effective_on, review_after_days = EXCLUDED.review_after_days,
    decided_at = EXCLUDED.decided_at, decided_by = EXCLUDED.decided_by,
    decision_note = EXCLUDED.decision_note, deleted_at = NULL, updated_at = NOW();

-- -----------------------------------------------------------------------------
-- 3. Reward Agreement RW001–RW004
-- -----------------------------------------------------------------------------
INSERT INTO pack_family.agreement (
    id, tenant_id, family_id, proposed_by, title, proposal_body,
    target_type, target_id, status, terms, purpose, effective_on, review_after_days,
    applies_to_member_id, decided_at, decided_by, decision_note
)
VALUES
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae20',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab01',
    'RW001 · Hoàn thành 5 ngày liên tiếp',
    'Nếu hoàn thành toàn bộ cam kết trong 5 ngày liên tiếp thì được +20 phút Game cuối tuần.',
    'reward', NULL, 'accepted',
    jsonb_build_object(
        'schemaVersion', 2, 'code', 'RW001', 'constitution', 'v1.0',
        'purpose', 'Động lực giữ nhịp đều đặn.',
        'conditions', jsonb_build_array('Hoàn thành toàn bộ Commitment trong 5 ngày liên tiếp'),
        'exceptions', jsonb_build_array('sick', 'travel'),
        'result', jsonb_build_object('kind', 'reward', 'code', 'reward_extra_game_20', 'labelVi', '+20 phút Game cuối tuần'),
        'rewardCode', 'reward_extra_game_20',
        'streakDays', 5,
        'supportsValues', jsonb_build_array('self_discipline')
    ),
    'Động lực giữ nhịp đều đặn.',
    (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date, 90,
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03',
    NOW() - INTERVAL '14 days', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02', 'Family Constitution v1.0'
),
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae21',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab01',
    'RW002 · Hoàn thành 7 ngày liên tiếp',
    'Nếu hoàn thành 7 ngày liên tiếp thì được chọn món ăn cuối tuần.',
    'reward', NULL, 'accepted',
    jsonb_build_object(
        'schemaVersion', 2, 'code', 'RW002', 'constitution', 'v1.0',
        'purpose', 'Ghi nhận tuần tự giác.',
        'conditions', jsonb_build_array('Hoàn thành toàn bộ Commitment trong 7 ngày liên tiếp'),
        'exceptions', jsonb_build_array('sick', 'travel'),
        'result', jsonb_build_object('kind', 'reward', 'code', 'reward_choose_dinner', 'labelVi', 'Chọn món ăn cuối tuần'),
        'rewardCode', 'reward_choose_dinner',
        'streakDays', 7,
        'supportsValues', jsonb_build_array('self_discipline', 'responsibility')
    ),
    'Ghi nhận tuần tự giác.',
    (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date, 90,
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03',
    NOW() - INTERVAL '14 days', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02', 'Family Constitution v1.0'
),
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae22',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab01',
    'RW003 · Hoàn thành 30 ngày',
    'Nếu hoàn thành 30 ngày thì được chọn hoạt động gia đình cuối tuần (công viên / xem phim / bơi…).',
    'reward', NULL, 'accepted',
    jsonb_build_object(
        'schemaVersion', 2, 'code', 'RW003', 'constitution', 'v1.0',
        'purpose', 'Ghi nhận thói quen dài hạn.',
        'conditions', jsonb_build_array('Hoàn thành cam kết trong 30 ngày (theo quy ước nhà)'),
        'exceptions', jsonb_build_array('sick', 'travel'),
        'result', jsonb_build_object('kind', 'reward', 'code', 'reward_family_activity', 'labelVi', 'Chọn hoạt động gia đình cuối tuần'),
        'rewardCode', 'reward_family_activity',
        'streakDays', 30,
        'supportsValues', jsonb_build_array('self_discipline', 'helping')
    ),
    'Ghi nhận thói quen dài hạn.',
    (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date, 90,
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03',
    NOW() - INTERVAL '14 days', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02', 'Family Constitution v1.0'
),
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae23',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab01',
    'RW004 · Đạt 100% Commitment hôm nay',
    'Nếu hoàn thành 100% cam kết trong ngày thì được thêm 15 phút Screen Time hôm đó.',
    'reward', NULL, 'accepted',
    jsonb_build_object(
        'schemaVersion', 2, 'code', 'RW004', 'constitution', 'v1.0',
        'purpose', 'Khuyến khích hoàn thành đủ trong ngày.',
        'conditions', jsonb_build_array('Đạt 100% Commitment trong ngày'),
        'exceptions', jsonb_build_array('sick'),
        'result', jsonb_build_object('kind', 'reward', 'code', 'reward_screen_15_today', 'labelVi', '+15 phút Screen Time hôm nay'),
        'rewardCode', 'reward_screen_15_today',
        'supportsValues', jsonb_build_array('responsibility', 'punctuality')
    ),
    'Khuyến khích hoàn thành đủ trong ngày.',
    (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date, 90,
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03',
    NOW() - INTERVAL '14 days', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02', 'Family Constitution v1.0'
)
ON CONFLICT (id) DO UPDATE SET
    title = EXCLUDED.title, proposal_body = EXCLUDED.proposal_body,
    target_type = EXCLUDED.target_type, status = 'accepted', terms = EXCLUDED.terms,
    purpose = EXCLUDED.purpose, applies_to_member_id = EXCLUDED.applies_to_member_id,
    review_after_days = EXCLUDED.review_after_days, decided_at = EXCLUDED.decided_at,
    decided_by = EXCLUDED.decided_by, decision_note = EXCLUDED.decision_note,
    deleted_at = NULL, updated_at = NOW();

-- -----------------------------------------------------------------------------
-- 4. Accountability AC001–AC004 (ngôn ngữ thỏa thuận, không “phạt”)
-- -----------------------------------------------------------------------------
INSERT INTO pack_family.agreement (
    id, tenant_id, family_id, proposed_by, title, proposal_body,
    target_type, target_id, status, terms, purpose, effective_on, review_after_days,
    applies_to_member_id, decided_at, decided_by, decision_note
)
VALUES
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae01',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab01',
    'AC001 · Nếu chưa hoàn thành bài tập trước giờ kết thúc thì hôm đó không chơi game',
    'Mục tiêu: Giúp hình thành thói quen hoàn thành trách nhiệm trước khi giải trí. Ngoại lệ: ốm, việc đột xuất, hoặc được phụ huynh chấp thuận.',
    'accountability', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad06', 'accepted',
    jsonb_build_object(
        'schemaVersion', 2, 'code', 'AC001', 'constitution', 'v1.0',
        'purpose', 'Giúp hình thành thói quen hoàn thành trách nhiệm trước khi giải trí.',
        'conditions', jsonb_build_array('Cam kết Làm bài tập chưa hoàn thành trước thời gian kết thúc'),
        'exceptions', jsonb_build_array('sick', 'busy', 'parent_approved'),
        'result', jsonb_build_object('kind', 'consequence', 'code', 'screen_no_game_today', 'labelVi', 'Không chơi game trong ngày'),
        'triggerCommitmentTemplateId', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad06',
        'consequenceCode', 'screen_no_game_today',
        'appliesToMemberId', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03',
        'supportsValues', jsonb_build_array('responsibility', 'self_discipline')
    ),
    'Giúp hình thành thói quen hoàn thành trách nhiệm trước khi giải trí.',
    (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date, 30,
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03',
    NOW() - INTERVAL '14 days', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02', 'Family Constitution v1.0'
),
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae31',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab01',
    'AC002 · Nếu chưa chuẩn bị cặp thì chuẩn bị trước khi ngủ',
    'Mục tiêu: Chủ động chuẩn bị cho ngày hôm sau. Chỉ yêu cầu hoàn thành trước khi ngủ — không áp dụng thỏa thuận accountability khác cho cùng việc này.',
    'accountability', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad05', 'accepted',
    jsonb_build_object(
        'schemaVersion', 2, 'code', 'AC002', 'constitution', 'v1.0',
        'purpose', 'Việc chuẩn bị cặp từ tối giúp rèn luyện tính trách nhiệm và chủ động.',
        'conditions', jsonb_build_array('Cam kết Chuẩn bị cặp chưa xong trong ngày'),
        'exceptions', jsonb_build_array('sick', 'travel', 'parent_approved'),
        'result', jsonb_build_object('kind', 'none', 'labelVi', 'Hoàn thành trước khi ngủ'),
        'remediation', 'complete_before_sleep',
        'triggerCommitmentTemplateId', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad05',
        'appliesToMemberId', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03',
        'supportsValues', jsonb_build_array('responsibility', 'self_discipline')
    ),
    'Việc chuẩn bị cặp từ tối giúp rèn luyện tính trách nhiệm và chủ động.',
    (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date, 30,
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03',
    NOW() - INTERVAL '14 days', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02', 'Family Constitution v1.0'
),
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae32',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab01',
    'AC003 · Nếu chưa dọn phòng thì làm thêm một việc nhà vào cuối tuần',
    'Mục tiêu: Giữ không gian sống gọn và chia sẻ việc nhà. Ngoại lệ: ốm hoặc được phụ huynh chấp thuận.',
    'accountability', NULL, 'accepted',
    jsonb_build_object(
        'schemaVersion', 2, 'code', 'AC003', 'constitution', 'v1.0',
        'purpose', 'Chia sẻ trách nhiệm không gian sống.',
        'conditions', jsonb_build_array('Chưa dọn phòng theo cam kết trong tuần'),
        'exceptions', jsonb_build_array('sick', 'parent_approved'),
        'result', jsonb_build_object('kind', 'consequence', 'code', 'duty_extra_chore', 'labelVi', 'Thêm một việc nhà cuối tuần'),
        'consequenceCode', 'duty_extra_chore',
        'appliesToMemberId', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03',
        'supportsValues', jsonb_build_array('helping', 'responsibility')
    ),
    'Chia sẻ trách nhiệm không gian sống.',
    (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date, 30,
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03',
    NOW() - INTERVAL '14 days', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02', 'Family Constitution v1.0'
),
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae33',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab01',
    'AC004 · Nếu đi ngủ muộn liên tiếp 3 ngày thì giảm 30 phút Screen Time cuối tuần',
    'Mục tiêu: Bảo vệ giấc ngủ và giữ giờ nghỉ đều. Ngoại lệ: sinh nhật, du lịch, hoặc được phụ huynh chấp thuận.',
    'accountability', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad11', 'accepted',
    jsonb_build_object(
        'schemaVersion', 2, 'code', 'AC004', 'constitution', 'v1.0',
        'purpose', 'Bảo vệ giấc ngủ và giữ giờ nghỉ đều.',
        'conditions', jsonb_build_array('Đi ngủ muộn liên tiếp 3 ngày'),
        'exceptions', jsonb_build_array('birthday', 'travel', 'parent_approved'),
        'result', jsonb_build_object('kind', 'consequence', 'code', 'screen_reduce_30_weekend', 'labelVi', 'Giảm 30 phút Screen Time cuối tuần'),
        'consequenceCode', 'screen_reduce_30_weekend',
        'triggerCommitmentTemplateId', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad11',
        'streakLateDays', 3,
        'appliesToMemberId', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03',
        'supportsValues', jsonb_build_array('punctuality', 'self_discipline')
    ),
    'Bảo vệ giấc ngủ và giữ giờ nghỉ đều.',
    (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date, 30,
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03',
    NOW() - INTERVAL '14 days', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02', 'Family Constitution v1.0'
)
ON CONFLICT (id) DO UPDATE SET
    title = EXCLUDED.title, proposal_body = EXCLUDED.proposal_body,
    target_type = EXCLUDED.target_type, target_id = EXCLUDED.target_id,
    status = 'accepted', terms = EXCLUDED.terms, purpose = EXCLUDED.purpose,
    applies_to_member_id = EXCLUDED.applies_to_member_id,
    review_after_days = EXCLUDED.review_after_days, decided_at = EXCLUDED.decided_at,
    decided_by = EXCLUDED.decided_by, decision_note = EXCLUDED.decision_note,
    deleted_at = NULL, updated_at = NOW();

-- -----------------------------------------------------------------------------
-- 5. Grace G001–G002
-- -----------------------------------------------------------------------------
INSERT INTO pack_family.agreement (
    id, tenant_id, family_id, proposed_by, title, proposal_body,
    target_type, status, terms, purpose, effective_on, review_after_days,
    decided_at, decided_by, decision_note
)
VALUES
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae40',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab01',
    'G001 · Được xin gia hạn tối đa 30 phút',
    'Nếu phụ huynh đồng ý gia hạn thì được thêm tối đa 30 phút. Trong Grace không áp dụng thỏa thuận accountability liên quan.',
    'grace', 'accepted',
    jsonb_build_object(
        'schemaVersion', 2, 'code', 'G001', 'constitution', 'v1.0',
        'purpose', 'Linh hoạt khi cần thêm thời gian có đồng ý.',
        'conditions', jsonb_build_array('Con xin gia hạn', 'Phụ huynh đồng ý'),
        'exceptions', jsonb_build_array(),
        'result', jsonb_build_object('kind', 'grace', 'labelVi', 'Gia hạn tối đa 30 phút'),
        'graceMinutes', 30,
        'supportsValues', jsonb_build_array('honesty', 'respect')
    ),
    'Linh hoạt khi cần thêm thời gian có đồng ý.',
    (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date, 90,
    NOW() - INTERVAL '14 days', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02', 'Family Constitution v1.0'
),
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae41',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab01',
    'G002 · Hoàn thành trong Grace Period vẫn giữ Streak',
    'Nếu hoàn thành sau khung giờ nhưng vẫn trong Grace Period thì ghi nhận Late Complete và không mất Streak.',
    'grace', 'accepted',
    jsonb_build_object(
        'schemaVersion', 2, 'code', 'G002', 'constitution', 'v1.0',
        'purpose', 'Khuyến khích hoàn thành thay vì bỏ cuộc.',
        'conditions', jsonb_build_array('Hoàn thành sau window_end nhưng trong Grace Period đã thống nhất'),
        'exceptions', jsonb_build_array(),
        'result', jsonb_build_object('kind', 'grace', 'labelVi', 'Late Complete — giữ Streak'),
        'lateCompleteKeepsStreak', true,
        'supportsValues', jsonb_build_array('self_discipline', 'responsibility')
    ),
    'Khuyến khích hoàn thành thay vì bỏ cuộc.',
    (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date, 90,
    NOW() - INTERVAL '14 days', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02', 'Family Constitution v1.0'
)
ON CONFLICT (id) DO UPDATE SET
    title = EXCLUDED.title, proposal_body = EXCLUDED.proposal_body,
    target_type = EXCLUDED.target_type, status = 'accepted', terms = EXCLUDED.terms,
    purpose = EXCLUDED.purpose, review_after_days = EXCLUDED.review_after_days,
    decided_at = EXCLUDED.decided_at, decided_by = EXCLUDED.decided_by,
    decision_note = EXCLUDED.decision_note, deleted_at = NULL, updated_at = NOW();

-- -----------------------------------------------------------------------------
-- 6. Exception E001–E004
-- -----------------------------------------------------------------------------
INSERT INTO pack_family.agreement (
    id, tenant_id, family_id, proposed_by, title, proposal_body,
    target_type, status, terms, purpose, effective_on, review_after_days,
    decided_at, decided_by, decision_note
)
VALUES
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae50',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab01',
    'E001 · Ngày sinh nhật',
    'Ngày sinh nhật không áp dụng các thỏa thuận accountability.',
    'exception', 'accepted',
    jsonb_build_object(
        'schemaVersion', 2, 'code', 'E001', 'constitution', 'v1.0',
        'purpose', 'Ưu tiên ngày đặc biệt của thành viên.',
        'conditions', jsonb_build_array('Ngày sinh nhật của thành viên'),
        'exceptions', jsonb_build_array(),
        'result', jsonb_build_object('kind', 'none', 'labelVi', 'Tạm dừng accountability'),
        'exceptionKind', 'birthday',
        'supportsValues', jsonb_build_array('respect', 'helping')
    ),
    'Ưu tiên ngày đặc biệt của thành viên.',
    (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date, 365,
    NOW() - INTERVAL '14 days', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02', 'Family Constitution v1.0'
),
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae51',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab01',
    'E002 · Đi du lịch',
    'Khi đi du lịch, Routine ngày thường được thay bằng Travel Routine (khi có).',
    'exception', 'accepted',
    jsonb_build_object(
        'schemaVersion', 2, 'code', 'E002', 'constitution', 'v1.0',
        'purpose', 'Điều chỉnh nhịp sống khi xa nhà.',
        'conditions', jsonb_build_array('Gia đình đang đi du lịch'),
        'exceptions', jsonb_build_array(),
        'result', jsonb_build_object('kind', 'none', 'labelVi', 'Chuyển Travel Routine'),
        'exceptionKind', 'travel',
        'routineKind', 'travel',
        'supportsValues', jsonb_build_array('respect')
    ),
    'Điều chỉnh nhịp sống khi xa nhà.',
    (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date, 365,
    NOW() - INTERVAL '14 days', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02', 'Family Constitution v1.0'
),
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae52',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab01',
    'E003 · Ốm',
    'Khi ốm được phép Skip Commitment và không ảnh hưởng Streak.',
    'exception', 'accepted',
    jsonb_build_object(
        'schemaVersion', 2, 'code', 'E003', 'constitution', 'v1.0',
        'purpose', 'Ưu tiên sức khỏe trước cam kết.',
        'conditions', jsonb_build_array('Thành viên đang ốm / không khỏe'),
        'exceptions', jsonb_build_array(),
        'result', jsonb_build_object('kind', 'none', 'labelVi', 'Skip không mất Streak'),
        'exceptionKind', 'sick',
        'skipKeepsStreak', true,
        'supportsValues', jsonb_build_array('respect', 'honesty')
    ),
    'Ưu tiên sức khỏe trước cam kết.',
    (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date, 365,
    NOW() - INTERVAL '14 days', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02', 'Family Constitution v1.0'
),
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae53',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab01',
    'E004 · Sự kiện đặc biệt (Family Exception Day)',
    'Guardian có thể đánh dấu Family Exception Day khi có sự kiện đặc biệt.',
    'exception', 'accepted',
    jsonb_build_object(
        'schemaVersion', 2, 'code', 'E004', 'constitution', 'v1.0',
        'purpose', 'Linh hoạt cho sự kiện gia đình.',
        'conditions', jsonb_build_array('Guardian đánh dấu Family Exception Day'),
        'exceptions', jsonb_build_array(),
        'result', jsonb_build_object('kind', 'none', 'labelVi', 'Family Exception Day'),
        'exceptionKind', 'family_exception_day',
        'supportsValues', jsonb_build_array('helping', 'respect')
    ),
    'Linh hoạt cho sự kiện gia đình.',
    (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date, 365,
    NOW() - INTERVAL '14 days', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02', 'Family Constitution v1.0'
)
ON CONFLICT (id) DO UPDATE SET
    title = EXCLUDED.title, proposal_body = EXCLUDED.proposal_body,
    target_type = EXCLUDED.target_type, status = 'accepted', terms = EXCLUDED.terms,
    purpose = EXCLUDED.purpose, review_after_days = EXCLUDED.review_after_days,
    decided_at = EXCLUDED.decided_at, decided_by = EXCLUDED.decided_by,
    decision_note = EXCLUDED.decision_note, deleted_at = NULL, updated_at = NOW();

-- -----------------------------------------------------------------------------
-- 7. Change C001–C002
-- -----------------------------------------------------------------------------
INSERT INTO pack_family.agreement (
    id, tenant_id, family_id, proposed_by, title, proposal_body,
    target_type, target_id, status, terms, purpose, effective_on, review_after_days,
    decided_at, decided_by, decision_note
)
VALUES
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae60',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab01',
    'C001 · Đổi giờ đọc sách có hiệu lực từ ngày hôm sau',
    'Đề xuất đổi giờ đọc sách → gia đình đồng ý → có hiệu lực từ ngày hôm sau.',
    'change', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad09', 'accepted',
    jsonb_build_object(
        'schemaVersion', 2, 'code', 'C001', 'constitution', 'v1.0',
        'purpose', 'Quy trình đổi khung giờ cam kết.',
        'conditions', jsonb_build_array('Có đề xuất', 'Các bên liên quan đồng ý'),
        'exceptions', jsonb_build_array(),
        'result', jsonb_build_object('kind', 'none', 'labelVi', 'Hiệu lực từ ngày hôm sau'),
        'effectiveDelay', 'next_day',
        'supportsValues', jsonb_build_array('punctuality', 'respect')
    ),
    'Quy trình đổi khung giờ cam kết.',
    (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date, 180,
    NOW() - INTERVAL '14 days', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02', 'Family Constitution v1.0'
),
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae61',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab01',
    'C002 · Đổi Routine có hiệu lực từ tuần kế tiếp',
    'Đổi Routine đã đồng ý có hiệu lực từ tuần kế tiếp.',
    'change', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01', 'accepted',
    jsonb_build_object(
        'schemaVersion', 2, 'code', 'C002', 'constitution', 'v1.0',
        'purpose', 'Quy trình đổi nhịp sống nhà.',
        'conditions', jsonb_build_array('Có đề xuất đổi Routine', 'Các bên đồng ý'),
        'exceptions', jsonb_build_array(),
        'result', jsonb_build_object('kind', 'none', 'labelVi', 'Hiệu lực từ tuần kế tiếp'),
        'effectiveDelay', 'next_week',
        'supportsValues', jsonb_build_array('respect', 'responsibility')
    ),
    'Quy trình đổi nhịp sống nhà.',
    (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date, 180,
    NOW() - INTERVAL '14 days', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02', 'Family Constitution v1.0'
)
ON CONFLICT (id) DO UPDATE SET
    title = EXCLUDED.title, proposal_body = EXCLUDED.proposal_body,
    target_type = EXCLUDED.target_type, target_id = EXCLUDED.target_id,
    status = 'accepted', terms = EXCLUDED.terms, purpose = EXCLUDED.purpose,
    review_after_days = EXCLUDED.review_after_days, decided_at = EXCLUDED.decided_at,
    decided_by = EXCLUDED.decided_by, decision_note = EXCLUDED.decision_note,
    deleted_at = NULL, updated_at = NOW();

-- -----------------------------------------------------------------------------
-- 8. Family Values
-- -----------------------------------------------------------------------------
INSERT INTO pack_family.agreement (
    id, tenant_id, family_id, proposed_by, title, proposal_body,
    target_type, status, terms, purpose, effective_on, review_after_days,
    decided_at, decided_by, decision_note
)
VALUES
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae70',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab01',
    'Giá trị gia đình hướng tới',
    'Gia đình chọn: Tự giác, Trách nhiệm, Đúng giờ, Trung thực, Giúp đỡ nhau, Tôn trọng. Coach dùng các giá trị này để giải thích ý nghĩa thỏa thuận.',
    'value', 'accepted',
    jsonb_build_object(
        'schemaVersion', 2, 'code', 'FV001', 'constitution', 'v1.0',
        'purpose', 'La bàn cho mọi thỏa thuận và Coach.',
        'conditions', jsonb_build_array('Áp dụng khi giải thích / đề xuất thỏa thuận'),
        'exceptions', jsonb_build_array(),
        'result', jsonb_build_object('kind', 'none'),
        'familyValues', jsonb_build_array(
            'self_discipline', 'responsibility', 'punctuality', 'honesty', 'helping', 'respect'
        ),
        'supportsValues', jsonb_build_array(
            'self_discipline', 'responsibility', 'punctuality', 'honesty', 'helping', 'respect'
        )
    ),
    'La bàn cho mọi thỏa thuận và Coach.',
    (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date, 365,
    NOW() - INTERVAL '14 days', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02', 'Family Constitution v1.0'
)
ON CONFLICT (id) DO UPDATE SET
    title = EXCLUDED.title, proposal_body = EXCLUDED.proposal_body,
    target_type = EXCLUDED.target_type, status = 'accepted', terms = EXCLUDED.terms,
    purpose = EXCLUDED.purpose, review_after_days = EXCLUDED.review_after_days,
    decided_at = EXCLUDED.decided_at, decided_by = EXCLUDED.decided_by,
    decision_note = EXCLUDED.decision_note, deleted_at = NULL, updated_at = NOW();

-- Soft-archive obsolete catalog codes (pre-Constitution) for DEMO_FAMILY
UPDATE pack_family.accountability_option
SET status = 'archived', updated_at = NOW()
WHERE family_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01'
  AND deleted_at IS NULL
  AND is_system = TRUE
  AND code IN (
    'community_help_table',
    'reward_sticker',
    'reward_movie_night',
    'reward_friend_play'
  );
