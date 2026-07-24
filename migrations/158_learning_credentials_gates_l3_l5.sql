-- KitPlatform 158: Learning OS P0.5 — credentials, soft gates, Pharmacy L3–L5

CREATE TABLE IF NOT EXISTS pack_learning.credential (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id         UUID         NOT NULL REFERENCES public.tenants(id),
    employee_id       UUID         NOT NULL REFERENCES public.employees(id),
    competency_code   VARCHAR(80)  NOT NULL,
    level_code        VARCHAR(10)  NOT NULL DEFAULT 'L0',
    source_module_id  UUID REFERENCES pack_learning.module(id) ON DELETE SET NULL,
    score_pct         INT,
    earned_at         TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_learning_credential UNIQUE (tenant_id, employee_id, competency_code)
);

CREATE INDEX IF NOT EXISTS ix_learning_credential_employee
    ON pack_learning.credential (tenant_id, employee_id);

CREATE TABLE IF NOT EXISTS pack_learning.gate_policy (
    id                         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    pack_code                  VARCHAR(60)  NOT NULL,
    permission_code            VARCHAR(80)  NOT NULL,
    required_competency_codes  TEXT[]       NOT NULL DEFAULT '{}',
    mode                       VARCHAR(10)  NOT NULL DEFAULT 'soft',
    is_active                  BOOLEAN      NOT NULL DEFAULT TRUE,
    CONSTRAINT uq_learning_gate_policy UNIQUE (pack_code, permission_code),
    CONSTRAINT ck_learning_gate_mode CHECK (mode IN ('soft', 'hard'))
);

CREATE TABLE IF NOT EXISTS pack_learning.gate_override (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id         UUID         NOT NULL REFERENCES public.tenants(id),
    employee_id       UUID         NOT NULL REFERENCES public.employees(id),
    permission_code   VARCHAR(80)  NOT NULL,
    reason            TEXT         NOT NULL,
    expires_at        TIMESTAMPTZ,
    created_by_user_id UUID REFERENCES public.users(id),
    created_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    revoked_at        TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_learning_gate_override_active
    ON pack_learning.gate_override (tenant_id, employee_id, permission_code)
    WHERE revoked_at IS NULL;

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA pack_learning TO kitplatform;

-- Soft gate policies (advisory P0.5 — does not hard-block APIs yet)
INSERT INTO pack_learning.gate_policy (pack_code, permission_code, required_competency_codes, mode)
VALUES
    ('novixa_pharmacy', 'sales.pos', ARRAY['pos_basic'], 'soft'),
    ('novixa_pharmacy', 'sales.write', ARRAY['pos_basic'], 'soft'),
    ('novixa_pharmacy', 'procurement.po', ARRAY['grn_receive'], 'soft'),
    ('novixa_pharmacy', 'inventory.write', ARRAY['count_basic'], 'soft'),
    ('novixa_pharmacy', 'success.write', ARRAY['shift_close'], 'soft')
ON CONFLICT (pack_code, permission_code) DO UPDATE SET
    required_competency_codes = EXCLUDED.required_competency_codes,
    mode = EXCLUDED.mode,
    is_active = TRUE;

-- Extend pharmacy onboarding program with L3–L5
INSERT INTO pack_learning.module (
    id, program_id, code, title, summary, body_markdown, duration_minutes,
    level_code, competency_codes, sort_order, pass_score_pct, require_ack
)
VALUES
(
    '22222222-2222-2222-2222-222222222214',
    '22222222-2222-2222-2222-222222222201',
    'l3_stock_grn',
    'Nhập hàng & tồn kho cơ bản',
    'Nhận hàng theo PO, FEFO, báo lệch kiểm kê.',
    E'## Mục tiêu\nTự nhận hàng theo phiếu / PO và hiểu FEFO.\n\n## Các bước\n1. Mở Nhập hàng (GRN) trên admin hoặc staff.\n2. Chọn PO / nhà cung cấp đúng.\n3. Nhập số lượng, số lô, hạn dùng.\n4. Đối chiếu với hàng thực tế trước khi xác nhận.\n5. Lệch số lượng → báo quản lý, không tự sửa sai lệch lớn.\n\n## FEFO\nBán / xuất ưu tiên lô hết hạn sớm hơn.',
    7,
    'L3',
    ARRAY['grn_receive', 'expiry_fefo', 'count_basic'],
    40,
    70,
    TRUE
),
(
    '22222222-2222-2222-2222-222222222215',
    '22222222-2222-2222-2222-222222222201',
    'l4_own_shift',
    'Làm chủ ca làm việc',
    'Đóng ca, đối quỹ, checklist GPP đầu/cuối ca.',
    E'## Mục tiêu\nTự mở/đóng ca và biết xử lý lệch quỹ.\n\n## Các bước\n1. Checklist mở ca trước khi bán.\n2. Theo dõi doanh thu / hình thức thanh toán trong ca.\n3. Đóng ca: đối quỹ tiền mặt vs hệ thống.\n4. Lệch vượt ngưỡng → ghi chú + báo quản lý (không giấu).\n5. Checklist đóng ca / GPP vận hành nếu được giao.',
    7,
    'L4',
    ARRAY['shift_close', 'cash_variance', 'gpp_daily'],
    50,
    70,
    TRUE
),
(
    '22222222-2222-2222-2222-222222222216',
    '22222222-2222-2222-2222-222222222201',
    'l5_solo_ready',
    'Sẵn sàng trực ca độc lập',
    'Xử lý sự cố cơ bản và biết khi nào phải leo thang.',
    E'## Mục tiêu\nTrực ca cơ bản an toàn khi đã hoàn thành L0–L4.\n\n## Việc phải nắm\n1. Bán + chăm sóc trong ranh giới quyền.\n2. Nhập / tồn cơ bản không để sai lô/HSD.\n3. Đóng ca minh bạch.\n4. Sự cố (mất điện, lệch quỹ, khách bức xúc) → bình tĩnh, ghi nhận, gọi quản lý / DS.\n\n## Ranh giới pháp lý\nKhông thay vai trò Dược sĩ phụ trách khi luật / nội quy yêu cầu có DS.',
    8,
    'L5',
    ARRAY['incident', 'escalate', 'multi_skill_pass'],
    60,
    80,
    TRUE
)
ON CONFLICT (program_id, code) DO UPDATE SET
    title = EXCLUDED.title,
    summary = EXCLUDED.summary,
    body_markdown = EXCLUDED.body_markdown,
    duration_minutes = EXCLUDED.duration_minutes,
    level_code = EXCLUDED.level_code,
    competency_codes = EXCLUDED.competency_codes,
    sort_order = EXCLUDED.sort_order,
    pass_score_pct = EXCLUDED.pass_score_pct,
    require_ack = EXCLUDED.require_ack,
    updated_at = NOW();

UPDATE pack_learning.program
SET
    title = 'Onboarding nhà thuốc — L0 đến L5',
    summary = 'Chào mừng → Bán → CSKH → Kho/GRN → Làm chủ ca → Solo ready. Học ngắn, quiz, ký xác nhận, hồ sơ năng lực.',
    updated_at = NOW()
WHERE id = '22222222-2222-2222-2222-222222222201';

DELETE FROM pack_learning.quiz_question
WHERE module_id IN (
    '22222222-2222-2222-2222-222222222214',
    '22222222-2222-2222-2222-222222222215',
    '22222222-2222-2222-2222-222222222216'
);

INSERT INTO pack_learning.quiz_question (module_id, sort_order, prompt, options_json, correct_option_index)
VALUES
(
    '22222222-2222-2222-2222-222222222214', 1,
    'Khi số lượng thực nhận lệch PO lớn, bạn nên?',
    '["Tự sửa cho khớp hệ thống","Báo quản lý / ghi nhận lệch trước khi xác nhận","Bỏ qua vì gần đúng"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222214', 2,
    'FEFO nghĩa là?',
    '["Ưu tiên lô nhập mới nhất","Ưu tiên lô hết hạn sớm hơn","Chỉ áp dụng thuốc kê đơn"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222215', 1,
    'Phát hiện lệch quỹ vượt ngưỡng khi đóng ca?',
    '["Giấu và bù tiền riêng","Ghi chú + báo quản lý theo quy trình","Xóa ca làm lại"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222215', 2,
    'Checklist mở ca nên làm khi nào?',
    '["Sau khi hết giờ","Trước khi bắt đầu bán","Chỉ cuối tháng"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222216', 1,
    'Gặp tình huống vượt quyền / chuyên môn, bạn?',
    '["Tự xử lý cho nhanh","Leo thang đúng người (QL / DS) và ghi nhận","Nhờ khách tự quyết hoàn toàn"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222216', 2,
    'Solo ready có thay Dược sĩ phụ trách theo luật không?',
    '["Có, luôn thay được","Không — vẫn tôn trọng ranh giới pháp lý / nội quy","Chỉ thay vào cuối tuần"]'::jsonb,
    1
);

-- Backfill progress rows for existing enrollments when new L3–L5 modules appear
INSERT INTO pack_learning.module_progress (enrollment_id, module_id, status)
SELECT e.id, m.id, 'not_started'
FROM pack_learning.enrollment e
INNER JOIN pack_learning.module m ON m.program_id = e.program_id
WHERE e.program_id = '22222222-2222-2222-2222-222222222201'
ON CONFLICT (enrollment_id, module_id) DO NOTHING;
