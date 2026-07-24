-- KitPlatform 157: Pharmacy onboarding track L0–L2 (platform catalog, pack novixa_pharmacy)

INSERT INTO pack_learning.program (
    id, tenant_id, code, pack_code, title, summary, locale, version, status, sort_order
)
VALUES (
    '22222222-2222-2222-2222-222222222201',
    NULL,
    'pharmacy_onboarding_l0_l2',
    'novixa_pharmacy',
    'Onboarding nhà thuốc — L0 đến L2',
    'Chào mừng → Bán hàng cơ bản → Chăm sóc khách. Học từng bài ngắn, làm quiz, ký xác nhận.',
    'vi-VN',
    1,
    'published',
    10
)
ON CONFLICT (id) DO UPDATE SET
    title = EXCLUDED.title,
    summary = EXCLUDED.summary,
    status = EXCLUDED.status,
    updated_at = NOW();

INSERT INTO pack_learning.module (
    id, program_id, code, title, summary, body_markdown, duration_minutes,
    level_code, competency_codes, sort_order, pass_score_pct, require_ack
)
VALUES
(
    '22222222-2222-2222-2222-222222222211',
    '22222222-2222-2222-2222-222222222201',
    'l0_welcome',
    'Chào mừng nhân viên mới',
    'Làm quen Novixa và quy tắc làm việc tại quầy.',
    E'## Mục tiêu\nSau bài này bạn biết đăng nhập, mở ca và thái độ phục vụ cơ bản.\n\n## Việc cần nhớ\n1. Đăng nhập đúng chi nhánh / ca của mình.\n2. Checklist đầu ca — làm trước khi bán.\n3. Bảo mật: không cho người khác dùng tài khoản.\n4. Khi không chắc — hỏi quản lý / dược sĩ, không đoán.\n\n## Thực hành\nMở app nhân viên → Hub → xem các mục được phân quyền.',
    3,
    'L0',
    ARRAY['app_basics', 'tone_of_service'],
    10,
    70,
    TRUE
),
(
    '22222222-2222-2222-2222-222222222212',
    '22222222-2222-2222-2222-222222222201',
    'l1_pos_basic',
    'Quy trình bán thuốc cơ bản',
    'POS: chọn sản phẩm, thanh toán, in hóa đơn.',
    E'## Mục tiêu\nTự bán một đơn OTC đơn giản trên Novixa.\n\n## Các bước\n1. Mở POS / bán hàng.\n2. Tìm sản phẩm (mã / tên).\n3. Kiểm tra số lượng, hạn dùng (FEFO khi có lô).\n4. Chọn khách (nếu có) → thanh toán → in hóa đơn.\n5. Không tự ý giảm giá ngoài quyền được giao.\n\n## Ranh giới\nThuốc kê đơn / tư vấn chuyên sâu — chuyển dược sĩ phụ trách.',
    5,
    'L1',
    ARRAY['pos_basic', 'payment', 'return_policy'],
    20,
    70,
    TRUE
),
(
    '22222222-2222-2222-2222-222222222213',
    '22222222-2222-2222-2222-222222222201',
    'l2_customer_care',
    'Chăm sóc khách hàng tại quầy',
    'Tìm khách, tích điểm, biết khi nào cần hỏi thêm.',
    E'## Mục tiêu\nPhục vụ khách lịch sự và dùng đúng hồ sơ khách trên hệ thống.\n\n## Các bước\n1. Tìm / tạo khách theo SĐT.\n2. Xác nhận đúng người trước khi gắn đơn.\n3. Giải thích điểm thưởng / ưu đãi ngắn gọn.\n4. Khi khách hỏi liều / tương tác thuốc — không tự ý; mời dược sĩ.\n\n## Thái độ\nLắng nghe → xác nhận nhu cầu → xử lý hoặc chuyển đúng người.',
    6,
    'L2',
    ARRAY['customer_lookup', 'loyalty', 'advise_boundary'],
    30,
    70,
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

-- Quizzes (idempotent by deleting existing for these modules then insert)
DELETE FROM pack_learning.quiz_question
WHERE module_id IN (
    '22222222-2222-2222-2222-222222222211',
    '22222222-2222-2222-2222-222222222212',
    '22222222-2222-2222-2222-222222222213'
);

INSERT INTO pack_learning.quiz_question (module_id, sort_order, prompt, options_json, correct_option_index)
VALUES
(
    '22222222-2222-2222-2222-222222222211', 1,
    'Khi không chắc cách xử lý tại quầy, bạn nên?',
    '["Đoán theo kinh nghiệm cá nhân","Hỏi quản lý / dược sĩ trước khi làm","Nhờ khách tự quyết"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222211', 2,
    'Checklist đầu ca dùng để làm gì?',
    '["Làm sau khi hết giờ","Chuẩn bị quầy trước khi bán","Chỉ dành cho kế toán"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222212', 1,
    'Trước khi bán, cần kiểm tra điều gì với hàng có lô?',
    '["Màu bao bì yêu thích","Hạn dùng / FEFO khi hệ thống gợi ý","Tên nhà cung cấp trên hóa đơn VAT"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222212', 2,
    'Giảm giá ngoài quyền được giao thì?',
    '["Tự giảm để giữ khách","Xin phép đúng quy trình / người có quyền","Hẹn khách ngày mai rồi tự quyết"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222213', 1,
    'Khách hỏi tương tác thuốc / liều dùng phức tạp, bạn?',
    '["Trả lời theo Google","Chuyển dược sĩ phụ trách","Bán thêm sản phẩm liên quan ngay"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222213', 2,
    'Trước khi gắn đơn vào khách, cần?',
    '["Xác nhận đúng người (SĐT / tên)","Chỉ cần tên gần đúng","Không cần gắn khách"]'::jsonb,
    0
);
