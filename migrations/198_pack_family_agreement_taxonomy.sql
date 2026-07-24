-- KitPlatform 198: FamilyOS Agreement taxonomy (A1)
-- Categories + structured fields; Foundation seed for DEMO_FAMILY
-- Depends on: 193
-- Local / pilot only until deploy is explicitly approved

-- Drop old check first so we can rewrite legacy codes → taxonomy
ALTER TABLE pack_family.agreement DROP CONSTRAINT IF EXISTS ck_agreement_target;

-- Map legacy target_type → taxonomy category
UPDATE pack_family.agreement
SET target_type = CASE target_type
    WHEN 'accountability_rule' THEN 'accountability'
    WHEN 'reward_rule' THEN 'reward'
    WHEN 'routine_change' THEN 'change'
    WHEN 'commitment_change' THEN 'change'
    WHEN 'other' THEN 'value'
    ELSE target_type
END
WHERE target_type IN (
    'accountability_rule', 'reward_rule', 'routine_change', 'commitment_change', 'other'
);

-- Any unexpected legacy codes → value (keeps constraint applyable)
UPDATE pack_family.agreement
SET target_type = 'value'
WHERE target_type NOT IN (
    'foundation', 'routine', 'commitment', 'reward', 'accountability',
    'grace', 'exception', 'change', 'value'
);

ALTER TABLE pack_family.agreement
    ADD CONSTRAINT ck_agreement_target CHECK (
        target_type IN (
            'foundation',
            'routine',
            'commitment',
            'reward',
            'accountability',
            'grace',
            'exception',
            'change',
            'value'
        )
    );

ALTER TABLE pack_family.agreement
    ADD COLUMN IF NOT EXISTS purpose TEXT,
    ADD COLUMN IF NOT EXISTS effective_on DATE,
    ADD COLUMN IF NOT EXISTS review_after_days INT,
    ADD COLUMN IF NOT EXISTS applies_to_member_id UUID REFERENCES pack_family.membership(id);

COMMENT ON COLUMN pack_family.agreement.target_type IS
    'Agreement category: foundation|routine|commitment|reward|accountability|grace|exception|change|value';
COMMENT ON COLUMN pack_family.agreement.purpose IS
    'Why the family agreed — supports values / self-regulation goals';
COMMENT ON COLUMN pack_family.agreement.effective_on IS
    'Date the agreement takes effect (family local calendar intent)';
COMMENT ON COLUMN pack_family.agreement.review_after_days IS
    'Suggested review window in days (30, 90, …)';
COMMENT ON COLUMN pack_family.agreement.applies_to_member_id IS
    'Primary member the agreement applies to (optional)';

COMMENT ON TABLE pack_family.agreement IS
    'Family Agreement — điều đã thống nhất (không phải kho luật/phạt). Nền Accountability + Wizard.';

-- Upgrade demo accountability terms to schemaVersion 2 (safe if row missing)
UPDATE pack_family.agreement
SET purpose = COALESCE(purpose, 'Giúp xây dựng tính tự giác và trách nhiệm với cam kết học tập.'),
    effective_on = COALESCE(effective_on, (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date - 2),
    review_after_days = COALESCE(review_after_days, 30),
    applies_to_member_id = COALESCE(
        applies_to_member_id,
        NULLIF(terms->>'appliesToMemberId', '')::uuid
    ),
    terms = terms || jsonb_build_object(
        'schemaVersion', 2,
        'purpose', 'Giúp xây dựng tính tự giác và trách nhiệm với cam kết học tập.',
        'conditions', jsonb_build_array(
            'Chưa hoàn thành cam kết Làm bài tập trong khung giờ đã thống nhất'
        ),
        'exceptions', jsonb_build_array('sick', 'travel', 'parent_approved'),
        'result', jsonb_build_object(
            'kind', 'consequence',
            'code', COALESCE(terms->>'consequenceCode', 'screen_no_game_today'),
            'labelVi', 'Không chơi game tối hôm đó'
        ),
        'supportsValues', jsonb_build_array('responsibility', 'self_discipline')
    ),
    updated_at = NOW()
WHERE id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae01';

UPDATE pack_family.agreement
SET purpose = COALESCE(purpose, 'Điều chỉnh khung giờ đọc sách cho hợp nhịp ăn tối.'),
    review_after_days = COALESCE(review_after_days, 30),
    terms = CASE
        WHEN terms = '{}'::jsonb OR terms IS NULL THEN jsonb_build_object(
            'schemaVersion', 2,
            'purpose', 'Điều chỉnh khung giờ đọc sách cho hợp nhịp ăn tối.',
            'conditions', jsonb_build_array('Đổi cửa sổ Đọc sách sang khoảng 20:00'),
            'exceptions', jsonb_build_array(),
            'result', jsonb_build_object('kind', 'none'),
            'supportsValues', jsonb_build_array('punctuality')
        )
        ELSE terms || jsonb_build_object('schemaVersion', 2)
    END,
    updated_at = NOW()
WHERE id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae02';

-- Foundation agreements (culture) — DEMO_FAMILY only when demo tenant exists (skip on prod)
DO $demo$
BEGIN
  IF EXISTS (
    SELECT 1 FROM public.tenants
    WHERE id = '11111111-1111-1111-1111-111111111104'::uuid
  ) THEN
INSERT INTO pack_family.agreement (
    id, tenant_id, family_id, proposed_by, title, proposal_body,
    target_type, target_id, status, terms, purpose, effective_on, review_after_days,
    decided_at, decided_by, decision_note
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
    365,
    NOW() - INTERVAL '10 days',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02',
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
    365,
    NOW() - INTERVAL '10 days',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02',
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
    365,
    NOW() - INTERVAL '10 days',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02',
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
    365,
    NOW() - INTERVAL '10 days',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02',
    'Family Constitution v1.0'
)
ON CONFLICT (id) DO UPDATE SET
    title = EXCLUDED.title,
    proposal_body = EXCLUDED.proposal_body,
    target_type = EXCLUDED.target_type,
    status = EXCLUDED.status,
    terms = EXCLUDED.terms,
    purpose = EXCLUDED.purpose,
    effective_on = EXCLUDED.effective_on,
    review_after_days = EXCLUDED.review_after_days,
    decided_at = EXCLUDED.decided_at,
    decided_by = EXCLUDED.decided_by,
    decision_note = EXCLUDED.decision_note,
    deleted_at = NULL,
    updated_at = NOW();

  END IF;
END
$demo$;
