-- KitPlatform 447: KAP PHARMACY_V2 v2.0 — 'Soát nhanh nhà thuốc'
-- Sinh tự động từ tools/kap-v2/build.py + gen_sql.py (chạy trong thư mục đó). Không sửa tay; sửa build.py rồi sinh lại.
-- Depends on: 068, 089. PHARMACY_V1 giữ nguyên (active) để bài nháp cũ làm tiếp được.
-- Không seed benchmark cohort cho V2: chỉ so sánh khi đã có dữ liệu thật (xem AssessmentSettings).

BEGIN;

INSERT INTO assessment_template (id, code, name, version, description, verticals, status)
VALUES ('38c527c2-8c96-58d7-8699-b893ac606c78', 'PHARMACY_V2', 'Soát nhanh nhà thuốc', '2.0',
        '31 câu hiển thị (36 câu trong ngân hàng, rẽ nhánh theo nỗi đau ở P3). 24 câu tính điểm, 6 nhóm.',
        ARRAY['pharmacy','pharmacy_chain'], 'active')
ON CONFLICT (code, version) DO UPDATE SET name = EXCLUDED.name, description = EXCLUDED.description,
    status = EXCLUDED.status, updated_at = NOW();

-- Bắt đầu (PROFILE)
INSERT INTO assessment_category (id, template_id, code, name, sort_order, weight)
VALUES ('59982a79-e5a1-5f01-b06d-a240a5367a98', '38c527c2-8c96-58d7-8699-b893ac606c78', 'PROFILE', 'Bắt đầu', 0, 0)
ON CONFLICT (template_id, code) DO UPDATE SET name = EXCLUDED.name, sort_order = EXCLUDED.sort_order, weight = EXCLUDED.weight;
INSERT INTO assessment_dimension (id, category_id, code, name, sort_order, weight)
VALUES ('08b69a85-9d15-59c3-b513-a0d853994eca', '59982a79-e5a1-5f01-b06d-a240a5367a98', 'PROFILE_OVERALL', 'Bắt đầu (tổng)', 1, 1)
ON CONFLICT (category_id, code) DO UPDATE SET name = EXCLUDED.name;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('91972416-83f6-54e3-927a-d3cfa6e1a6fb', '08b69a85-9d15-59c3-b513-a0d853994eca', 'P2', 'Nhà thuốc của anh/chị có mấy cơ sở?', NULL,
        'single_choice', FALSE, TRUE, 1, 1, '{"role": "segment"}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('9e54d47d-f93c-5a6c-ac80-aa6f33f088d5', '91972416-83f6-54e3-927a-d3cfa6e1a6fb', 'SITES_1', '1 cơ sở', NULL, 1, '{}'::jsonb),
    ('84d4b222-ffe4-561b-940c-e0e8d9540aee', '91972416-83f6-54e3-927a-d3cfa6e1a6fb', 'SITES_2_3', '2–3 cơ sở', NULL, 2, '{}'::jsonb),
    ('74b0b868-9a09-5e27-bf85-29c220e6567a', '91972416-83f6-54e3-927a-d3cfa6e1a6fb', 'SITES_4_5', '4–5 cơ sở', NULL, 3, '{}'::jsonb),
    ('671c7ff4-d05b-5bfa-bdc4-15c16dddbba6', '91972416-83f6-54e3-927a-d3cfa6e1a6fb', 'SITES_6P', 'Trên 5 cơ sở', NULL, 4, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('3e7e0768-ae76-59ca-b1e0-0cd2215c1d61', '08b69a85-9d15-59c3-b513-a0d853994eca', 'P3', 'Việc nào đang làm anh/chị đau đầu nhất lúc này?', 'Chọn một việc. Phần sau sẽ hỏi kỹ hơn đúng việc này.',
        'single_choice', FALSE, TRUE, 2, 1, '{"leadTag": "pain_point"}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('46d35c94-a2fa-5890-9ece-c9bf5fe73b9b', '3e7e0768-ae76-59ca-b1e0-0cd2215c1d61', 'PAIN_CUSTOMER', 'Khách ít quay lại', NULL, 1, '{"tag": "pain_retention"}'::jsonb),
    ('ada746d0-aadb-5ebf-b0d0-eecb84cc4628', '3e7e0768-ae76-59ca-b1e0-0cd2215c1d61', 'PAIN_INVENTORY', 'Hàng tồn, cận hạn, hết hàng', NULL, 2, '{"tag": "pain_inventory"}'::jsonb),
    ('dc4877cb-a55a-59ce-832c-0b4135da498b', '3e7e0768-ae76-59ca-b1e0-0cd2215c1d61', 'PAIN_STAFF', 'Nhân viên làm không đều tay', NULL, 3, '{"tag": "pain_staff"}'::jsonb),
    ('aa2bbd42-3ad3-5c53-952c-7ee994f92d34', '3e7e0768-ae76-59ca-b1e0-0cd2215c1d61', 'PAIN_NUMBERS', 'Không nắm rõ lãi lỗ', NULL, 4, '{"tag": "pain_numbers"}'::jsonb),
    ('c0471d4e-74ac-5817-b634-ef66b0f1542f', '3e7e0768-ae76-59ca-b1e0-0cd2215c1d61', 'PAIN_COMPLIANCE', 'Sổ sách, lo khi có kiểm tra', NULL, 5, '{"tag": "pain_compliance"}'::jsonb),
    ('404bb442-1990-50aa-8a03-07d2d331ade3', '3e7e0768-ae76-59ca-b1e0-0cd2215c1d61', 'PAIN_GROWTH', 'Muốn mở thêm nhưng chưa sẵn sàng', NULL, 6, '{"tag": "pain_growth"}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('946a1f0a-8fde-5be3-872e-cc0d3cb13797', '08b69a85-9d15-59c3-b513-a0d853994eca', 'P5', 'Mỗi đơn khách trả trung bình khoảng bao nhiêu?', 'Ước lượng là được.',
        'single_choice', FALSE, TRUE, 3, 1, '{"role": "input"}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('ddd4e004-7585-5a1b-b9ec-08b16e5a29ef', '946a1f0a-8fde-5be3-872e-cc0d3cb13797', 'AOV_LT80', 'Dưới 80.000đ', NULL, 1, '{}'::jsonb),
    ('37498fe0-c358-5bd4-b3aa-1b0489782dbb', '946a1f0a-8fde-5be3-872e-cc0d3cb13797', 'AOV_80_150', '80.000–150.000đ', NULL, 2, '{}'::jsonb),
    ('1ecc0d8e-4af8-5c0e-a7f3-10c95353f17d', '946a1f0a-8fde-5be3-872e-cc0d3cb13797', 'AOV_150_300', '150.000–300.000đ', NULL, 3, '{}'::jsonb),
    ('8029906b-538f-54ef-b505-2287e30401d7', '946a1f0a-8fde-5be3-872e-cc0d3cb13797', 'AOV_GT300', 'Trên 300.000đ', NULL, 4, '{}'::jsonb),
    ('36646433-3a4f-5369-86ad-0e81746eac15', '946a1f0a-8fde-5be3-872e-cc0d3cb13797', 'UNKNOWN', 'Không rõ', NULL, 5, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;

-- Khách hàng (CUSTOMER)
INSERT INTO assessment_category (id, template_id, code, name, sort_order, weight)
VALUES ('9642ed12-35a0-5c46-afb9-be9a3691ca04', '38c527c2-8c96-58d7-8699-b893ac606c78', 'CUSTOMER', 'Khách hàng', 1, 1)
ON CONFLICT (template_id, code) DO UPDATE SET name = EXCLUDED.name, sort_order = EXCLUDED.sort_order, weight = EXCLUDED.weight;
INSERT INTO assessment_dimension (id, category_id, code, name, sort_order, weight)
VALUES ('360a1281-a29c-5474-a7df-2ceb2cabf979', '9642ed12-35a0-5c46-afb9-be9a3691ca04', 'CUSTOMER_OVERALL', 'Khách hàng (tổng)', 1, 1)
ON CONFLICT (category_id, code) DO UPDATE SET name = EXCLUDED.name;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('aac03077-b3b6-5f97-9367-3b9c577a3086', '360a1281-a29c-5474-a7df-2ceb2cabf979', 'C1', 'Trong 10 khách gần nhất, khoảng mấy người là khách quen?', 'Không có đáp án đúng sai. Nhà thuốc gần bệnh viện thường ít khách quen hơn nhà thuốc trong khu dân cư.',
        'single_choice', FALSE, TRUE, 1, 1, '{"role": "input"}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('b868218d-5b2f-548a-94d6-b1f400293207', 'aac03077-b3b6-5f97-9367-3b9c577a3086', 'REG_0_2', '0–2 người', NULL, 1, '{}'::jsonb),
    ('497bf07c-73aa-5928-9437-108f28007c6e', 'aac03077-b3b6-5f97-9367-3b9c577a3086', 'REG_3_5', '3–5 người', NULL, 2, '{}'::jsonb),
    ('18bec6c0-270a-5941-b4ce-e971a9b88f1a', 'aac03077-b3b6-5f97-9367-3b9c577a3086', 'REG_6_8', '6–8 người', NULL, 3, '{}'::jsonb),
    ('46503a86-e0c5-5e9f-b09a-3e72d0396f2b', 'aac03077-b3b6-5f97-9367-3b9c577a3086', 'REG_9_10', '9–10 người', NULL, 4, '{}'::jsonb),
    ('44713c26-c565-5aa8-b0b3-4075de3021c8', 'aac03077-b3b6-5f97-9367-3b9c577a3086', 'UNKNOWN', 'Không để ý', NULL, 5, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('6445a1b2-e5d5-5929-9560-20ee84612bf7', '360a1281-a29c-5474-a7df-2ceb2cabf979', 'C2', 'Khách quen quay lại, nhân viên biết lần trước họ mua gì bằng cách nào?', NULL,
        'single_choice', TRUE, TRUE, 2, 1, '{"impact": 1.0}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('f983e51b-a788-5667-8820-057fe9be96c8', '6445a1b2-e5d5-5929-9560-20ee84612bf7', 'OPT1', 'Không biết — phải hỏi lại khách', 1, 1, '{}'::jsonb),
    ('27e5f12a-33a2-5a46-954b-d352a9ea45c8', '6445a1b2-e5d5-5929-9560-20ee84612bf7', 'OPT2', 'Nhân viên nhớ mặt, nhớ thuốc', 2, 2, '{}'::jsonb),
    ('c3f38157-903d-56e3-bc35-2e8e4552081d', '6445a1b2-e5d5-5929-9560-20ee84612bf7', 'OPT3', 'Tra sổ hoặc tìm trong máy, mất vài phút', 3, 3, '{}'::jsonb),
    ('3eb8e603-119f-56e2-91db-8a4301243c44', '6445a1b2-e5d5-5929-9560-20ee84612bf7', 'OPT4', 'Nhập số điện thoại là thấy ngay', 4, 4, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('7069cdf7-0d51-5387-b961-a2413e32e995', '360a1281-a29c-5474-a7df-2ceb2cabf979', 'C3', 'Khách dùng thuốc lâu dài (huyết áp, tiểu đường…) sắp hết thuốc, nhà thuốc có nhắc không?', NULL,
        'single_choice', TRUE, TRUE, 3, 1, '{"impact": 1.3}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('b88cf6cd-f3fa-58da-8953-a2ab0211daac', '7069cdf7-0d51-5387-b961-a2413e32e995', 'OPT1', 'Không nhắc', 1, 1, '{}'::jsonb),
    ('0f077d7a-a149-5c4d-9b98-80fd696bde58', '7069cdf7-0d51-5387-b961-a2413e32e995', 'OPT2', 'Chỉ khi khách nhờ', 2, 2, '{}'::jsonb),
    ('c889ed32-5792-5d5d-a877-6281a9855a37', '7069cdf7-0d51-5387-b961-a2413e32e995', 'OPT3', 'Nhắc vài khách quen qua điện thoại, Zalo', 3, 3, '{}'::jsonb),
    ('ea08a43b-de78-5659-a028-29ffe72d99a0', '7069cdf7-0d51-5387-b961-a2413e32e995', 'OPT4', 'Có danh sách, nhắc đều theo lịch', 4, 4, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('e12aa262-a8e6-56e4-a008-26aacb98d43a', '360a1281-a29c-5474-a7df-2ceb2cabf979', 'C4', 'Tháng trước, nhà thuốc chủ động hỏi thăm lại khoảng bao nhiêu khách sau khi bán?', NULL,
        'single_choice', TRUE, TRUE, 4, 1, '{"impact": 0.9}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('4ebf6257-42f3-55b9-8b5f-85d6ef31a429', 'e12aa262-a8e6-56e4-a008-26aacb98d43a', 'OPT1', 'Không ai', 1, 1, '{}'::jsonb),
    ('a4423034-7a31-5c6d-a287-869195a36a83', 'e12aa262-a8e6-56e4-a008-26aacb98d43a', 'OPT2', 'Vài khách', 2, 2, '{}'::jsonb),
    ('28f73044-74ee-5e8b-be6c-9d9f4e1a6599', 'e12aa262-a8e6-56e4-a008-26aacb98d43a', 'OPT3', 'Khoảng 10–30 khách', 3, 3, '{}'::jsonb),
    ('5c1cbd99-eabc-5b41-a2b8-8baf384ff567', 'e12aa262-a8e6-56e4-a008-26aacb98d43a', 'OPT4', 'Trên 30 khách', 4, 4, '{}'::jsonb),
    ('de722eaa-4a33-551e-9664-c38a2c79cf05', 'e12aa262-a8e6-56e4-a008-26aacb98d43a', 'UNKNOWN', 'Không theo dõi', NULL, 5, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('244c03d2-2253-5663-8943-9722d24fbcf9', '360a1281-a29c-5474-a7df-2ceb2cabf979', 'C5', 'Anh/chị có biết tháng này bao nhiêu khách mới, bao nhiêu khách cũ quay lại không?', NULL,
        'single_choice', TRUE, TRUE, 5, 1, '{"impact": 0.8}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('221eb28f-a75b-50a8-80e1-0113a599a67e', '244c03d2-2253-5663-8943-9722d24fbcf9', 'OPT1', 'Không biết', 1, 1, '{}'::jsonb),
    ('fb87acab-336d-5696-92d7-b95a79d6d673', '244c03d2-2253-5663-8943-9722d24fbcf9', 'OPT2', 'Chỉ có cảm giác chung', 2, 2, '{}'::jsonb),
    ('80158c70-8b67-5ba9-bf0a-f0de6a99fb73', '244c03d2-2253-5663-8943-9722d24fbcf9', 'OPT3', 'Đếm được nếu ngồi lục sổ', 3, 3, '{}'::jsonb),
    ('2123033a-1c63-512f-9ab9-b07fa8d663cd', '244c03d2-2253-5663-8943-9722d24fbcf9', 'OPT4', 'Xem được con số ngay', 4, 4, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('9985c77b-00ad-5957-ac3d-9642343bb414', '360a1281-a29c-5474-a7df-2ceb2cabf979', 'C6', 'Theo anh/chị, khách không quay lại chủ yếu vì sao?', NULL,
        'single_choice', FALSE, TRUE, 6, 1, '{"showIf": {"questionCode": "P3", "optionCode": "PAIN_CUSTOMER"}}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('cda3a6d8-28e3-5e68-8d90-73aa3bb6f6eb', '9985c77b-00ad-5957-ac3d-9642343bb414', 'WHY_PRICE', 'Giá cao hơn chỗ khác', NULL, 1, '{}'::jsonb),
    ('27c9eb16-9c34-554a-b034-c77b65e54c0a', '9985c77b-00ad-5957-ac3d-9642343bb414', 'WHY_CONVENIENCE', 'Chỗ khác tiện đường hơn', NULL, 2, '{}'::jsonb),
    ('f0f0166c-99de-56d3-9f7c-99901b625146', '9985c77b-00ad-5957-ac3d-9642343bb414', 'WHY_FORGET', 'Không được nhắc nên quên', NULL, 3, '{}'::jsonb),
    ('32a0ef8c-3946-5ada-9d88-0ea8f9342323', '9985c77b-00ad-5957-ac3d-9642343bb414', 'WHY_STOCKOUT', 'Lúc cần thì nhà thuốc hết hàng', NULL, 4, '{}'::jsonb),
    ('a12c5165-6ba4-53c1-a907-03dce88c6bae', '9985c77b-00ad-5957-ac3d-9642343bb414', 'UNKNOWN', 'Không rõ', NULL, 5, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;

-- Kho (INVENTORY)
INSERT INTO assessment_category (id, template_id, code, name, sort_order, weight)
VALUES ('51c8a7a0-b989-5a31-9442-edb2ee4d6214', '38c527c2-8c96-58d7-8699-b893ac606c78', 'INVENTORY', 'Kho', 2, 1)
ON CONFLICT (template_id, code) DO UPDATE SET name = EXCLUDED.name, sort_order = EXCLUDED.sort_order, weight = EXCLUDED.weight;
INSERT INTO assessment_dimension (id, category_id, code, name, sort_order, weight)
VALUES ('ccccf484-940c-5ae6-a37d-463ba73cc27a', '51c8a7a0-b989-5a31-9442-edb2ee4d6214', 'INVENTORY_OVERALL', 'Kho (tổng)', 1, 1)
ON CONFLICT (category_id, code) DO UPDATE SET name = EXCLUDED.name;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('cb42cbf5-6850-5fef-9dab-4b399d9322e9', 'ccccf484-940c-5ae6-a37d-463ba73cc27a', 'I1', 'Lần gần nhất anh/chị phát hiện một lô sắp hết hạn là nhờ đâu?', NULL,
        'single_choice', TRUE, TRUE, 1, 1, '{"impact": 1.2}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('66b81502-e0bb-53d4-98c6-bcfa658b07c3', 'cb42cbf5-6850-5fef-9dab-4b399d9322e9', 'OPT1', 'Tình cờ lúc lấy hàng cho khách', 1, 1, '{}'::jsonb),
    ('ed7ed8e3-f7c9-5b34-b2d9-9bb080ace951', 'cb42cbf5-6850-5fef-9dab-4b399d9322e9', 'OPT2', 'Lúc kiểm kê', 2, 2, '{}'::jsonb),
    ('b31523c7-f66d-5392-8cb1-047c394bf88f', 'cb42cbf5-6850-5fef-9dab-4b399d9322e9', 'OPT3', 'Rà sổ theo lịch định kỳ', 3, 3, '{}'::jsonb),
    ('db1f5f8d-8c46-5cc5-945d-47c3f47e1aba', 'cb42cbf5-6850-5fef-9dab-4b399d9322e9', 'OPT4', 'Phần mềm báo trước', 4, 4, '{}'::jsonb),
    ('577a6d46-67be-531c-93c8-85de4485de8b', 'cb42cbf5-6850-5fef-9dab-4b399d9322e9', 'UNKNOWN', 'Chưa gặp trường hợp này', NULL, 5, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('d8bafad9-dd78-5f1e-a97d-99e254b3dc33', 'ccccf484-940c-5ae6-a37d-463ba73cc27a', 'I2', 'Tháng trước, tiền hàng hết hạn phải hủy hoặc trả lại khoảng bao nhiêu?', 'Ước lượng là được.',
        'single_choice', FALSE, TRUE, 2, 1, '{"role": "input"}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('9658ff11-4175-5191-b8e2-193a89b58c15', 'd8bafad9-dd78-5f1e-a97d-99e254b3dc33', 'EXP_0', 'Gần như không có', NULL, 1, '{}'::jsonb),
    ('958fdbc2-096e-5fd2-8b1d-471e5d8c5fa4', 'd8bafad9-dd78-5f1e-a97d-99e254b3dc33', 'EXP_LT1M', 'Dưới 1 triệu', NULL, 2, '{}'::jsonb),
    ('a2f1bbb8-dc8f-50b8-8fea-a9078ecdf8a8', 'd8bafad9-dd78-5f1e-a97d-99e254b3dc33', 'EXP_1_3M', '1–3 triệu', NULL, 3, '{}'::jsonb),
    ('43f3260e-e74e-5921-a93a-a92d9d25b669', 'd8bafad9-dd78-5f1e-a97d-99e254b3dc33', 'EXP_3_7M', '3–7 triệu', NULL, 4, '{}'::jsonb),
    ('202c5005-1a58-5eec-bd63-15d60ac64dc7', 'd8bafad9-dd78-5f1e-a97d-99e254b3dc33', 'EXP_GT7M', 'Trên 7 triệu', NULL, 5, '{}'::jsonb),
    ('1ee6bf3b-9cfb-598c-8476-aacc504d276d', 'd8bafad9-dd78-5f1e-a97d-99e254b3dc33', 'UNKNOWN', 'Không rõ', NULL, 6, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('da3732dc-083d-5a32-bf0b-36aa43b56250', 'ccccf484-940c-5ae6-a37d-463ba73cc27a', 'I3', 'Tuần trước, khoảng bao nhiêu lần khách hỏi mua mà nhà thuốc hết hàng?', NULL,
        'single_choice', TRUE, TRUE, 3, 1, '{"impact": 1.2}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('a30efdf5-9848-5161-b274-188870932060', 'da3732dc-083d-5a32-bf0b-36aa43b56250', 'OPT1', 'Trên 15 lần', 1, 1, '{}'::jsonb),
    ('3499df93-3faf-5fd3-a439-c2b7db0b2ced', 'da3732dc-083d-5a32-bf0b-36aa43b56250', 'OPT2', '6–15 lần', 2, 2, '{}'::jsonb),
    ('ab042247-480a-5814-9d50-d8d6bdf50ff2', 'da3732dc-083d-5a32-bf0b-36aa43b56250', 'OPT3', '2–5 lần', 3, 3, '{}'::jsonb),
    ('4022cb7e-98cd-5450-bae9-18b620b39c4e', 'da3732dc-083d-5a32-bf0b-36aa43b56250', 'OPT4', '0–1 lần', 4, 4, '{}'::jsonb),
    ('116f74f5-f786-5037-914a-b2ec292e27af', 'da3732dc-083d-5a32-bf0b-36aa43b56250', 'UNKNOWN', 'Không để ý', NULL, 5, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('b780be2c-8fdc-5c6b-a0f3-878e1d4bbf29', 'ccccf484-940c-5ae6-a37d-463ba73cc27a', 'I4', 'Khi đặt hàng, anh/chị quyết định số lượng dựa vào đâu?', NULL,
        'single_choice', TRUE, TRUE, 4, 1, '{"impact": 1.0}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('4fbd50f9-69b4-5adc-8706-a259ded881b4', 'b780be2c-8fdc-5c6b-a0f3-878e1d4bbf29', 'OPT1', 'Thấy vơi thì nhập', 1, 1, '{}'::jsonb),
    ('f9b0051f-25eb-5416-ab1b-234a74db79fd', 'b780be2c-8fdc-5c6b-a0f3-878e1d4bbf29', 'OPT2', 'Theo kinh nghiệm và gợi ý của trình dược viên', 2, 2, '{}'::jsonb),
    ('74183e82-dc24-5ca7-afb6-6de0e0abfb08', 'b780be2c-8fdc-5c6b-a0f3-878e1d4bbf29', 'OPT3', 'Xem lại lượng bán tháng trước', 3, 3, '{}'::jsonb),
    ('aa5c9fd7-c07a-54ae-a7d4-e4495ac9d8f2', 'b780be2c-8fdc-5c6b-a0f3-878e1d4bbf29', 'OPT4', 'Có mức tồn tối thiểu cho từng mặt hàng', 4, 4, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('c81ea547-93d5-55a7-9196-9be1f0186fcb', 'ccccf484-940c-5ae6-a37d-463ba73cc27a', 'I5', 'Lần kiểm kê toàn bộ kho gần nhất là khi nào?', NULL,
        'single_choice', TRUE, TRUE, 5, 1, '{"impact": 0.8}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('4b2682f9-f320-5c35-84a0-2210b0461442', 'c81ea547-93d5-55a7-9196-9be1f0186fcb', 'OPT1', 'Chưa bao giờ hoặc không nhớ', 1, 1, '{}'::jsonb),
    ('be3b3d79-46e7-5c06-b124-2996f2efab12', 'c81ea547-93d5-55a7-9196-9be1f0186fcb', 'OPT2', 'Hơn 6 tháng trước', 2, 2, '{}'::jsonb),
    ('1871092b-9d82-5d5d-8e20-75ca2c84fa42', 'c81ea547-93d5-55a7-9196-9be1f0186fcb', 'OPT3', 'Trong 1–6 tháng qua', 3, 3, '{}'::jsonb),
    ('b59a0dee-7dc2-5ced-8632-b1bb47b855eb', 'c81ea547-93d5-55a7-9196-9be1f0186fcb', 'OPT4', 'Trong tháng này, hoặc kiểm từng nhóm hàng đều đặn', 4, 4, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('cdfb44c7-878e-5df1-a6af-947dd4ea1852', 'ccccf484-940c-5ae6-a37d-463ba73cc27a', 'I6', 'Anh/chị có kể ngay được 5 mặt hàng bán chậm nhất đang giữ vốn không?', NULL,
        'single_choice', TRUE, TRUE, 6, 1, '{"impact": 0.9}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('77e0c1d6-87f4-5a95-af94-c59bc1f1e81b', 'cdfb44c7-878e-5df1-a6af-947dd4ea1852', 'OPT1', 'Không', 1, 1, '{}'::jsonb),
    ('cddee7b6-6e4e-5be1-a0e4-5c90f54c6536', 'cdfb44c7-878e-5df1-a6af-947dd4ea1852', 'OPT2', 'Đoán được', 2, 2, '{}'::jsonb),
    ('8f3751b8-b439-5f1b-a28f-5ad297ab0bdf', 'cdfb44c7-878e-5df1-a6af-947dd4ea1852', 'OPT3', 'Có, nhưng phải lục sổ', 3, 3, '{}'::jsonb),
    ('7a1518bf-891f-5ad5-8593-899a2a2ad46d', 'cdfb44c7-878e-5df1-a6af-947dd4ea1852', 'OPT4', 'Có, xem được ngay', 4, 4, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('aadd62e5-fc3d-5d50-95c9-8431a3c59d43', 'ccccf484-940c-5ae6-a37d-463ba73cc27a', 'I7', 'Hàng cận hạn hiện được xử lý thế nào?', NULL,
        'single_choice', FALSE, TRUE, 7, 1, '{"showIf": {"questionCode": "P3", "optionCode": "PAIN_INVENTORY"}}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('0a9f72d0-ea61-5897-9941-1f76de379b3b', 'aadd62e5-fc3d-5d50-95c9-8431a3c59d43', 'NEAR_WAIT', 'Để đến khi hết hạn rồi hủy', NULL, 1, '{}'::jsonb),
    ('e45b23f7-782b-5fb0-9b5a-d8d3e0a731fd', 'aadd62e5-fc3d-5d50-95c9-8431a3c59d43', 'NEAR_RETURN', 'Trả nhà cung cấp nếu được', NULL, 2, '{}'::jsonb),
    ('1cb5c45d-8196-59ff-b47b-c168fe69893d', 'aadd62e5-fc3d-5d50-95c9-8431a3c59d43', 'NEAR_PUSH', 'Giảm giá, ưu tiên bán trước', NULL, 3, '{}'::jsonb),
    ('89d729a7-f8a8-5123-9028-61e4cfdce89f', 'aadd62e5-fc3d-5d50-95c9-8431a3c59d43', 'NEAR_NONE', 'Chưa có cách làm thống nhất', NULL, 4, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;

-- Vận hành (OPERATIONS)
INSERT INTO assessment_category (id, template_id, code, name, sort_order, weight)
VALUES ('ea0875d6-9bb9-52db-bc93-36faa9a78a6b', '38c527c2-8c96-58d7-8699-b893ac606c78', 'OPERATIONS', 'Vận hành', 3, 1)
ON CONFLICT (template_id, code) DO UPDATE SET name = EXCLUDED.name, sort_order = EXCLUDED.sort_order, weight = EXCLUDED.weight;
INSERT INTO assessment_dimension (id, category_id, code, name, sort_order, weight)
VALUES ('27ecfd92-15a2-561a-8557-53e45a22a129', 'ea0875d6-9bb9-52db-bc93-36faa9a78a6b', 'OPERATIONS_OVERALL', 'Vận hành (tổng)', 1, 1)
ON CONFLICT (category_id, code) DO UPDATE SET name = EXCLUDED.name;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('e6d6bf76-6cfc-5210-92e0-34da23f6516a', '27ecfd92-15a2-561a-8557-53e45a22a129', 'O1', 'Nếu anh/chị nghỉ 3 ngày, nhà thuốc chạy thế nào?', NULL,
        'single_choice', TRUE, TRUE, 1, 1, '{"impact": 1.1}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('f1b4d432-41a5-5192-b9ae-417bcbeda513', 'e6d6bf76-6cfc-5210-92e0-34da23f6516a', 'OPT1', 'Phải đóng cửa hoặc bán rất hạn chế', 1, 1, '{}'::jsonb),
    ('03233640-a013-57fe-a7ea-73eb83088f55', 'e6d6bf76-6cfc-5210-92e0-34da23f6516a', 'OPT2', 'Vẫn mở nhưng nhân viên gọi hỏi liên tục', 2, 2, '{}'::jsonb),
    ('f775bc73-c1a2-515e-bb71-3fcf4f570e9a', 'e6d6bf76-6cfc-5210-92e0-34da23f6516a', 'OPT3', 'Chạy được, vài việc phải chờ anh/chị về', 3, 3, '{}'::jsonb),
    ('bcbed341-8259-5c5b-b38e-41d580c5c429', 'e6d6bf76-6cfc-5210-92e0-34da23f6516a', 'OPT4', 'Chạy bình thường', 4, 4, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('50fe8082-699b-5889-bc82-71d35a0bf39c', '27ecfd92-15a2-561a-8557-53e45a22a129', 'O2', 'Nhân viên mới mất bao lâu thì tự đứng quầy được?', NULL,
        'single_choice', TRUE, TRUE, 2, 1, '{"impact": 0.8}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('1872b1fe-6f5a-5a29-b691-d0a8235d92db', '50fe8082-699b-5889-bc82-71d35a0bf39c', 'OPT1', 'Trên 2 tháng', 1, 1, '{}'::jsonb),
    ('e8861c29-9144-5dd7-a3e5-64344fe01b64', '50fe8082-699b-5889-bc82-71d35a0bf39c', 'OPT2', '1–2 tháng', 2, 2, '{}'::jsonb),
    ('6095b1ab-9111-5abd-9b6b-23b7fa88af0e', '50fe8082-699b-5889-bc82-71d35a0bf39c', 'OPT3', '2–4 tuần', 3, 3, '{}'::jsonb),
    ('70550e2a-cc51-5520-8a3b-2a1ef88f24fa', '50fe8082-699b-5889-bc82-71d35a0bf39c', 'OPT4', 'Dưới 2 tuần — có tài liệu và người kèm', 4, 4, '{}'::jsonb),
    ('92a579ee-8856-57e7-9caa-10600a3f66e8', '50fe8082-699b-5889-bc82-71d35a0bf39c', 'UNKNOWN', 'Chưa từng tuyển nhân viên', NULL, 5, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('e69f62dd-551f-5620-bc3f-9a5dbac30c84', '27ecfd92-15a2-561a-8557-53e45a22a129', 'O3', 'Cùng một bệnh thông thường như cảm cúm, các nhân viên có tư vấn giống nhau không?', NULL,
        'single_choice', TRUE, TRUE, 3, 1, '{"impact": 0.9}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('f94b3f21-729b-51a6-982a-72dd6b84027f', 'e69f62dd-551f-5620-bc3f-9a5dbac30c84', 'OPT1', 'Mỗi người một kiểu', 1, 1, '{}'::jsonb),
    ('42ff8a0a-7536-5fd2-9a85-4324b1fd7d50', 'e69f62dd-551f-5620-bc3f-9a5dbac30c84', 'OPT2', 'Gần giống nhau, do quen tay', 2, 2, '{}'::jsonb),
    ('3e8e52b9-da3c-5ca5-9ab9-4150bb8ef4c9', 'e69f62dd-551f-5620-bc3f-9a5dbac30c84', 'OPT3', 'Có hướng dẫn chung nhưng chưa đủ', 3, 3, '{}'::jsonb),
    ('1a37f549-a0db-525e-a877-2559ddc63403', 'e69f62dd-551f-5620-bc3f-9a5dbac30c84', 'OPT4', 'Có hướng dẫn cho các bệnh hay gặp', 4, 4, '{}'::jsonb),
    ('9bc93f8b-a4cf-5e78-a8c8-c61883fabf94', 'e69f62dd-551f-5620-bc3f-9a5dbac30c84', 'UNKNOWN', 'Chỉ mình tôi đứng quầy', NULL, 5, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('3838beac-50f7-55d4-aeb5-fdeeee07b478', '27ecfd92-15a2-561a-8557-53e45a22a129', 'O4', 'Cuối ngày, tiền thu có khớp với số đã bán không?', NULL,
        'single_choice', TRUE, TRUE, 4, 1, '{"impact": 1.0}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('9a139764-fbbd-5f48-b526-aa5bcce40282', '3838beac-50f7-55d4-aeb5-fdeeee07b478', 'OPT1', 'Thường lệch, không rõ vì sao', 1, 1, '{}'::jsonb),
    ('293cc2db-4180-5338-8c4e-16458a6e2ba8', '3838beac-50f7-55d4-aeb5-fdeeee07b478', 'OPT2', 'Đôi khi lệch', 2, 2, '{}'::jsonb),
    ('63db7610-7cc8-52d4-b874-82400ad69a35', '3838beac-50f7-55d4-aeb5-fdeeee07b478', 'OPT3', 'Ít lệch, tìm được nguyên nhân', 3, 3, '{}'::jsonb),
    ('400c291c-36cd-5a53-8104-0a7490db77c7', '3838beac-50f7-55d4-aeb5-fdeeee07b478', 'OPT4', 'Khớp, có đối chiếu hằng ngày', 4, 4, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('9c7ec7f1-abe7-5d64-bdb8-934821e93028', '27ecfd92-15a2-561a-8557-53e45a22a129', 'O5', 'Các việc định kỳ (ghi nhiệt độ, độ ẩm, vệ sinh, sổ sách) có người phụ trách và ghi lại không?', NULL,
        'single_choice', TRUE, TRUE, 5, 1, '{"review": "pharmacist", "impact": 1.1}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('7d1d47aa-c726-560e-9477-4d723523bf75', '9c7ec7f1-abe7-5d64-bdb8-934821e93028', 'OPT1', 'Chưa làm đều', 1, 1, '{}'::jsonb),
    ('e6bc7d29-0ca5-58cb-a75f-41fecac3a27f', '9c7ec7f1-abe7-5d64-bdb8-934821e93028', 'OPT2', 'Làm khi sắp có kiểm tra', 2, 2, '{}'::jsonb),
    ('7ca38196-1f98-5497-a954-d4a08ec8e02d', '9c7ec7f1-abe7-5d64-bdb8-934821e93028', 'OPT3', 'Làm đều nhưng hay thiếu sổ', 3, 3, '{}'::jsonb),
    ('7e902b9e-7347-5a95-9b58-014d369c9720', '9c7ec7f1-abe7-5d64-bdb8-934821e93028', 'OPT4', 'Có người phụ trách, ghi đủ hằng ngày', 4, 4, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('a1f0586b-258a-59a5-a6a6-93cbbf32469e', '27ecfd92-15a2-561a-8557-53e45a22a129', 'O6', 'Lần kiểm tra gần nhất, nhà thuốc bị nhắc ở phần nào?', NULL,
        'single_choice', FALSE, TRUE, 6, 1, '{"showIf": {"questionCode": "P3", "optionCode": "PAIN_COMPLIANCE"}, "review": "pharmacist"}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('aa9fc86c-47f7-5dfa-b23d-868b30d79fd1', 'a1f0586b-258a-59a5-a6a6-93cbbf32469e', 'INSP_NONE_YET', 'Chưa bị kiểm tra lần nào', NULL, 1, '{}'::jsonb),
    ('59f01f4f-1635-5406-8747-fb6dac3e340c', 'a1f0586b-258a-59a5-a6a6-93cbbf32469e', 'INSP_RECORDS', 'Sổ sách, ghi chép', NULL, 2, '{}'::jsonb),
    ('0a54f993-9eed-5d26-adbf-28307a3568d4', 'a1f0586b-258a-59a5-a6a6-93cbbf32469e', 'INSP_STORAGE', 'Bảo quản, sắp xếp thuốc', NULL, 3, '{}'::jsonb),
    ('0f8c50d2-135a-50f1-a34c-a5d25e81983f', 'a1f0586b-258a-59a5-a6a6-93cbbf32469e', 'INSP_STAFF', 'Hồ sơ nhân sự, bằng cấp', NULL, 4, '{}'::jsonb),
    ('8ff55f33-5eed-5d1d-8ed3-d9ce16800e77', 'a1f0586b-258a-59a5-a6a6-93cbbf32469e', 'INSP_OK', 'Không bị nhắc gì', NULL, 5, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('9a0724ed-dea8-5c7d-a578-bbc21e747d2e', '27ecfd92-15a2-561a-8557-53e45a22a129', 'O7', 'Việc nào với nhân viên làm anh/chị mệt nhất?', NULL,
        'single_choice', FALSE, TRUE, 7, 1, '{"showIf": {"questionCode": "P3", "optionCode": "PAIN_STAFF"}}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('14a5ce7f-e837-5945-87b2-aaa8035a9902', '9a0724ed-dea8-5c7d-a578-bbc21e747d2e', 'STAFF_TURNOVER', 'Nghỉ việc nhiều, phải tuyển lại', NULL, 1, '{}'::jsonb),
    ('ce57b5d8-da6d-52ed-afbf-e264c2b0869d', '9a0724ed-dea8-5c7d-a578-bbc21e747d2e', 'STAFF_ADVICE', 'Tư vấn chưa tốt', NULL, 2, '{}'::jsonb),
    ('f4ddbc3b-712c-5f57-a3ce-f70b100cf06e', '9a0724ed-dea8-5c7d-a578-bbc21e747d2e', 'STAFF_PASSIVE', 'Không chủ động, phải nhắc từng việc', NULL, 3, '{}'::jsonb),
    ('7b3494b1-7e7a-5fe2-ab84-4d2074a406ab', '9a0724ed-dea8-5c7d-a578-bbc21e747d2e', 'STAFF_CONTROL', 'Khó kiểm soát tiền, hàng', NULL, 4, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;

-- Kinh doanh (BUSINESS)
INSERT INTO assessment_category (id, template_id, code, name, sort_order, weight)
VALUES ('b588e8e3-512f-5763-b089-259a176331a4', '38c527c2-8c96-58d7-8699-b893ac606c78', 'BUSINESS', 'Kinh doanh', 4, 1)
ON CONFLICT (template_id, code) DO UPDATE SET name = EXCLUDED.name, sort_order = EXCLUDED.sort_order, weight = EXCLUDED.weight;
INSERT INTO assessment_dimension (id, category_id, code, name, sort_order, weight)
VALUES ('6a16cc15-8609-5af5-b7ed-04ee1208194f', 'b588e8e3-512f-5763-b089-259a176331a4', 'BUSINESS_OVERALL', 'Kinh doanh (tổng)', 1, 1)
ON CONFLICT (category_id, code) DO UPDATE SET name = EXCLUDED.name;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('12a3cc89-7d63-5b84-8cd5-05310eef8597', '6a16cc15-8609-5af5-b7ed-04ee1208194f', 'B1', 'Anh/chị có biết tháng trước nhà thuốc lãi bao nhiêu không?', NULL,
        'single_choice', TRUE, TRUE, 1, 1, '{"impact": 1.2}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('50c48262-a557-5937-b2c8-335ac1df9344', '12a3cc89-7d63-5b84-8cd5-05310eef8597', 'OPT1', 'Không — chỉ biết khi tổng kết cuối năm', 1, 1, '{}'::jsonb),
    ('4a323945-ed5d-5713-bda9-091eb53681b7', '12a3cc89-7d63-5b84-8cd5-05310eef8597', 'OPT2', 'Đoán theo tiền còn trong két', 2, 2, '{}'::jsonb),
    ('ef948c9c-1049-5764-84d3-478234e3dfb7', '12a3cc89-7d63-5b84-8cd5-05310eef8597', 'OPT3', 'Tính được nhưng mất công', 3, 3, '{}'::jsonb),
    ('4a51465c-a56b-5508-a375-6cfc3fe41866', '12a3cc89-7d63-5b84-8cd5-05310eef8597', 'OPT4', 'Biết rõ con số hằng tháng', 4, 4, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('fea3c563-f113-5129-b673-75784eb903fd', '6a16cc15-8609-5af5-b7ed-04ee1208194f', 'B2', 'Anh/chị có biết nhóm hàng nào lãi nhiều nhất (không phải bán nhiều nhất)?', NULL,
        'single_choice', TRUE, TRUE, 2, 1, '{"impact": 0.9}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('9d8fbbe8-05a6-5133-9618-db7771c4d76d', 'fea3c563-f113-5129-b673-75784eb903fd', 'OPT1', 'Không', 1, 1, '{}'::jsonb),
    ('dfbe605a-259e-5c1e-9666-143dbbe5ffdc', 'fea3c563-f113-5129-b673-75784eb903fd', 'OPT2', 'Đoán được', 2, 2, '{}'::jsonb),
    ('e4b31b9f-5155-56ba-ae9c-adedacee23ea', 'fea3c563-f113-5129-b673-75784eb903fd', 'OPT3', 'Biết, nhờ tính tay', 3, 3, '{}'::jsonb),
    ('661804e4-307e-52d2-9433-e070bb0dd1c9', 'fea3c563-f113-5129-b673-75784eb903fd', 'OPT4', 'Có số liệu rõ ràng', 4, 4, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('382cc7ca-ab0e-5cf7-84d8-f6622511f6b2', '6a16cc15-8609-5af5-b7ed-04ee1208194f', 'B3', 'Lần khuyến mại gần nhất, anh/chị có biết nó lời hay lỗ không?', NULL,
        'single_choice', TRUE, TRUE, 3, 1, '{"impact": 0.7}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('62faeb43-272b-52fd-b360-ae0f357b2e46', '382cc7ca-ab0e-5cf7-84d8-f6622511f6b2', 'OPT1', 'Không biết', 1, 1, '{}'::jsonb),
    ('ccea9597-5d79-58f9-91fb-7079a6575714', '382cc7ca-ab0e-5cf7-84d8-f6622511f6b2', 'OPT2', 'Chỉ thấy đông khách hơn', 2, 2, '{}'::jsonb),
    ('252d1364-6668-52ae-ac09-ae815fc03135', '382cc7ca-ab0e-5cf7-84d8-f6622511f6b2', 'OPT3', 'So được số bán trước và sau', 3, 3, '{}'::jsonb),
    ('0f515d93-d44c-542c-8710-3300743aa88d', '382cc7ca-ab0e-5cf7-84d8-f6622511f6b2', 'OPT4', 'Tính được phần lãi thêm sau khi trừ giảm giá', 4, 4, '{}'::jsonb),
    ('54d290b9-7711-520b-ac60-8f8c15f314e4', '382cc7ca-ab0e-5cf7-84d8-f6622511f6b2', 'UNKNOWN', 'Chưa làm khuyến mại', NULL, 5, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('f5bed43f-b3fd-519c-92dc-dadd79edd32b', '6a16cc15-8609-5af5-b7ed-04ee1208194f', 'B4', 'Tháng nào bán chậm hẳn, việc đầu tiên anh/chị làm là gì?', NULL,
        'single_choice', TRUE, TRUE, 4, 1, '{"impact": 0.9}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('0d86faa0-b337-5116-be69-7e12975ffd69', 'f5bed43f-b3fd-519c-92dc-dadd79edd32b', 'OPT1', 'Chờ xem tháng sau', 1, 1, '{}'::jsonb),
    ('69aa7256-7b12-545a-88f0-15a4b11cf2f7', 'f5bed43f-b3fd-519c-92dc-dadd79edd32b', 'OPT2', 'Đoán nguyên nhân, thử giảm giá', 2, 2, '{}'::jsonb),
    ('ee2a07ff-9ee2-5180-85d8-f6b185ae82b1', 'f5bed43f-b3fd-519c-92dc-dadd79edd32b', 'OPT3', 'Lục sổ xem chậm ở đâu', 3, 3, '{}'::jsonb),
    ('d24f9d4e-568a-547d-9fb1-6265cbfdabff', 'f5bed43f-b3fd-519c-92dc-dadd79edd32b', 'OPT4', 'Xem số theo nhóm hàng, nhóm khách để tìm chỗ chậm', 4, 4, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('0b0f8a80-2c3f-55d0-b454-4faaf2050fb5', '6a16cc15-8609-5af5-b7ed-04ee1208194f', 'B5', 'Anh/chị muốn mỗi ngày nhìn thấy con số nào nhất?', NULL,
        'single_choice', FALSE, TRUE, 5, 1, '{"showIf": {"questionCode": "P3", "optionCode": "PAIN_NUMBERS"}}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('4830b98b-9d44-5e11-bf00-81ee3dde136f', '0b0f8a80-2c3f-55d0-b454-4faaf2050fb5', 'NUM_MARGIN', 'Lãi trong ngày', NULL, 1, '{}'::jsonb),
    ('c58606d9-998e-592d-9ef4-7ce2227a6c5e', '0b0f8a80-2c3f-55d0-b454-4faaf2050fb5', 'NUM_STOCK', 'Hàng sắp hết, hàng sắp hết hạn', NULL, 2, '{}'::jsonb),
    ('4432a8e8-4e7b-5784-a274-a7fd2f4a7c7a', '0b0f8a80-2c3f-55d0-b454-4faaf2050fb5', 'NUM_CUSTOMER', 'Khách mới, khách quay lại', NULL, 3, '{}'::jsonb),
    ('bce1aa87-8d11-5a06-b420-bace4d3556f5', '0b0f8a80-2c3f-55d0-b454-4faaf2050fb5', 'NUM_STAFF', 'Kết quả của từng nhân viên', NULL, 4, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;

-- Dữ liệu & Công nghệ (TECH)
INSERT INTO assessment_category (id, template_id, code, name, sort_order, weight)
VALUES ('3f4674a1-7d01-5616-8117-c44ce8ab0771', '38c527c2-8c96-58d7-8699-b893ac606c78', 'TECH', 'Dữ liệu & Công nghệ', 5, 1)
ON CONFLICT (template_id, code) DO UPDATE SET name = EXCLUDED.name, sort_order = EXCLUDED.sort_order, weight = EXCLUDED.weight;
INSERT INTO assessment_dimension (id, category_id, code, name, sort_order, weight)
VALUES ('bcd895a6-5b12-581e-b11b-a61de047a957', '3f4674a1-7d01-5616-8117-c44ce8ab0771', 'TECH_OVERALL', 'Dữ liệu & Công nghệ (tổng)', 1, 1)
ON CONFLICT (category_id, code) DO UPDATE SET name = EXCLUDED.name;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('bf8580dc-bd12-525b-a470-3e83356b0789', 'bcd895a6-5b12-581e-b11b-a61de047a957', 'T1', 'Việc bán hàng hiện được ghi lại bằng gì?', NULL,
        'single_choice', TRUE, TRUE, 1, 1, '{"impact": 0.9}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('6f0f2427-987f-5ea7-b7e1-0085d3b69e89', 'bf8580dc-bd12-525b-a470-3e83356b0789', 'OPT1', 'Sổ tay, hoặc không ghi', 1, 1, '{}'::jsonb),
    ('43bd99ee-54c2-5787-8953-6c44103b9b1e', 'bf8580dc-bd12-525b-a470-3e83356b0789', 'OPT2', 'Excel', 2, 2, '{}'::jsonb),
    ('a21878bf-ee53-50b4-a616-318b22763a5e', 'bf8580dc-bd12-525b-a470-3e83356b0789', 'OPT3', 'Phần mềm trên một máy tại quầy', 3, 3, '{}'::jsonb),
    ('a8f77e65-a3ad-5b58-b900-be22cdaa1ea3', 'bf8580dc-bd12-525b-a470-3e83356b0789', 'OPT4', 'Phần mềm, xem được cả trên điện thoại', 4, 4, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('a865c1c1-cc2f-52c3-abd2-dd951188542b', 'bcd895a6-5b12-581e-b11b-a61de047a957', 'T2', 'Khi không ở nhà thuốc, anh/chị biết hôm nay bán được bao nhiêu bằng cách nào?', NULL,
        'single_choice', TRUE, TRUE, 2, 1, '{"impact": 0.8}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('7eba6088-a3d9-5604-acfb-4814c7e7e1b3', 'a865c1c1-cc2f-52c3-abd2-dd951188542b', 'OPT1', 'Không biết đến khi về', 1, 1, '{}'::jsonb),
    ('4f7c6d1f-13f6-58fa-80a6-c73216abcd0f', 'a865c1c1-cc2f-52c3-abd2-dd951188542b', 'OPT2', 'Gọi điện hỏi', 2, 2, '{}'::jsonb),
    ('564ef903-28d0-5909-83de-b01620398bab', 'a865c1c1-cc2f-52c3-abd2-dd951188542b', 'OPT3', 'Nhân viên chụp sổ gửi qua Zalo', 3, 3, '{}'::jsonb),
    ('49f7e5a9-2a96-5c2f-8786-0da755dcfe45', 'a865c1c1-cc2f-52c3-abd2-dd951188542b', 'OPT4', 'Tự xem trên điện thoại', 4, 4, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('4af84b0d-6c60-568d-9ff4-c02f603fad15', 'bcd895a6-5b12-581e-b11b-a61de047a957', 'T3', 'Thông tin bán hàng, kho và khách hàng đang nằm ở đâu?', NULL,
        'single_choice', TRUE, TRUE, 3, 1, '{"impact": 0.8}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('23c3d1a8-668f-53fb-83b5-031d1600973d', '4af84b0d-6c60-568d-9ff4-c02f603fad15', 'OPT1', 'Chưa ghi đủ cả ba', 1, 1, '{}'::jsonb),
    ('9beb76ca-4005-5280-a234-20f682a19247', '4af84b0d-6c60-568d-9ff4-c02f603fad15', 'OPT2', 'Mỗi thứ một nơi', 2, 2, '{}'::jsonb),
    ('848d6de3-286f-52f6-b5ce-2c26d162a232', '4af84b0d-6c60-568d-9ff4-c02f603fad15', 'OPT3', 'Cùng một phần mềm nhưng ít dùng phần kho hoặc khách', 3, 3, '{}'::jsonb),
    ('3cb08299-74af-57c5-b16d-24e28c209f8c', '4af84b0d-6c60-568d-9ff4-c02f603fad15', 'OPT4', 'Cùng một phần mềm, dùng đủ cả ba', 4, 4, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('70d8d788-7842-54a8-8d74-6d8e391bea51', 'bcd895a6-5b12-581e-b11b-a61de047a957', 'T4', 'Việc kết nối liên thông dữ liệu bán thuốc theo yêu cầu của ngành dược đang thế nào?', NULL,
        'single_choice', TRUE, TRUE, 4, 1, '{"review": "pharmacist", "impact": 1.0}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('bed8b2a3-6d62-5e9c-8f17-4c703fb332f7', '70d8d788-7842-54a8-8d74-6d8e391bea51', 'OPT1', 'Chưa kết nối', 1, 1, '{}'::jsonb),
    ('b5f22a56-3d73-59f8-a88f-e8e6f4d346a8', '70d8d788-7842-54a8-8d74-6d8e391bea51', 'OPT2', 'Đang làm, chưa ổn định', 2, 2, '{}'::jsonb),
    ('57558686-91da-570c-9504-2d78edd380b0', '70d8d788-7842-54a8-8d74-6d8e391bea51', 'OPT3', 'Có kết nối nhưng nhập tay nhiều', 3, 3, '{}'::jsonb),
    ('fd2de43f-ea42-59bc-8ef4-9c128dfb538f', '70d8d788-7842-54a8-8d74-6d8e391bea51', 'OPT4', 'Kết nối tự động từ phần mềm bán hàng', 4, 4, '{}'::jsonb),
    ('6546a758-c95d-5693-aec8-2c9cc8c04ba0', '70d8d788-7842-54a8-8d74-6d8e391bea51', 'UNKNOWN', 'Không rõ yêu cầu này', NULL, 5, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;

-- Phát triển (GROWTH)
INSERT INTO assessment_category (id, template_id, code, name, sort_order, weight)
VALUES ('76d5b4aa-7499-5194-a543-b67a066026bc', '38c527c2-8c96-58d7-8699-b893ac606c78', 'GROWTH', 'Phát triển', 6, 1)
ON CONFLICT (template_id, code) DO UPDATE SET name = EXCLUDED.name, sort_order = EXCLUDED.sort_order, weight = EXCLUDED.weight;
INSERT INTO assessment_dimension (id, category_id, code, name, sort_order, weight)
VALUES ('98a926aa-95c1-5a26-ab3b-fbfc1d97e78e', '76d5b4aa-7499-5194-a543-b67a066026bc', 'GROWTH_OVERALL', 'Phát triển (tổng)', 1, 1)
ON CONFLICT (category_id, code) DO UPDATE SET name = EXCLUDED.name;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('859a0bd0-cd93-5621-bf80-3072b49b192f', '98a926aa-95c1-5a26-ab3b-fbfc1d97e78e', 'G1', 'Anh/chị đã có mục tiêu cụ thể cho 12 tháng tới chưa?', NULL,
        'single_choice', TRUE, TRUE, 1, 1, '{"impact": 0.6}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('8b1bb78c-c371-5a2c-9414-42179eeb0e84', '859a0bd0-cd93-5621-bf80-3072b49b192f', 'OPT1', 'Chưa nghĩ tới', 1, 1, '{}'::jsonb),
    ('c5248854-f37e-5b16-9e50-fe276cfb1379', '859a0bd0-cd93-5621-bf80-3072b49b192f', 'OPT2', 'Có ý định chung', 2, 2, '{}'::jsonb),
    ('7793053f-1c2e-5096-a0a2-7f4d41b210a1', '859a0bd0-cd93-5621-bf80-3072b49b192f', 'OPT3', 'Có mục tiêu, chưa có kế hoạch', 3, 3, '{}'::jsonb),
    ('40e74cba-dcea-51a0-b298-d7aa09565808', '859a0bd0-cd93-5621-bf80-3072b49b192f', 'OPT4', 'Có mục tiêu, kế hoạch và theo dõi hằng tháng', 4, 4, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('d27d9584-cada-5ba9-9b18-a2ed8bbda858', '98a926aa-95c1-5a26-ab3b-fbfc1d97e78e', 'G2', 'Nếu mở thêm một cơ sở, cách làm hiện nay có áp dụng được ngay không?', NULL,
        'single_choice', TRUE, TRUE, 2, 1, '{"impact": 0.6}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('4ec4fc2d-e687-523e-af4d-067972bc86c6', 'd27d9584-cada-5ba9-9b18-a2ed8bbda858', 'OPT1', 'Phải làm lại từ đầu', 1, 1, '{}'::jsonb),
    ('51b3c15e-0e0e-50c0-a021-b86aeca7a926', 'd27d9584-cada-5ba9-9b18-a2ed8bbda858', 'OPT2', 'Chỉ áp dụng được một phần, phụ thuộc vào người', 2, 2, '{}'::jsonb),
    ('f1143312-7629-5244-90ac-bee350e61035', 'd27d9584-cada-5ba9-9b18-a2ed8bbda858', 'OPT3', 'Được, nếu anh/chị trực tiếp kèm', 3, 3, '{}'::jsonb),
    ('2b5a31e8-9830-5fd3-94a1-aa4fbe027ce1', 'd27d9584-cada-5ba9-9b18-a2ed8bbda858', 'OPT4', 'Được — đã có quy trình và phần mềm dùng chung', 4, 4, '{}'::jsonb),
    ('babbf800-ee35-52b6-bbd2-b1a7f736d730', 'd27d9584-cada-5ba9-9b18-a2ed8bbda858', 'UNKNOWN', 'Chưa có ý định mở thêm', NULL, 5, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('9cdc9218-ecb6-56d0-b58b-7ef7c68b2012', '98a926aa-95c1-5a26-ab3b-fbfc1d97e78e', 'G3', 'Anh/chị dự định mở thêm trong bao lâu?', NULL,
        'single_choice', FALSE, TRUE, 3, 1, '{"showIf": {"questionCode": "P3", "optionCode": "PAIN_GROWTH"}}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('201ac733-0eb5-5bcd-ac71-909587d1a5d2', '9cdc9218-ecb6-56d0-b58b-7ef7c68b2012', 'OPEN_6M', 'Trong 6 tháng', NULL, 1, '{}'::jsonb),
    ('4a1fd8f2-dd10-5ebd-9c89-5d3aa74bf7ab', '9cdc9218-ecb6-56d0-b58b-7ef7c68b2012', 'OPEN_12M', '6–12 tháng', NULL, 2, '{}'::jsonb),
    ('6f22dd50-7ecf-58d0-b394-73619611d2cd', '9cdc9218-ecb6-56d0-b58b-7ef7c68b2012', 'OPEN_LATER', 'Sau 1 năm', NULL, 3, '{}'::jsonb),
    ('cbba73d3-fde4-545c-bb3e-4b46f15fe5f8', '9cdc9218-ecb6-56d0-b58b-7ef7c68b2012', 'OPEN_UNSURE', 'Chưa chắc', NULL, 4, '{}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)
VALUES ('fd417b97-a021-5d43-b5ce-c89057cac60e', '98a926aa-95c1-5a26-ab3b-fbfc1d97e78e', 'G5', 'Nếu chỉ chọn một việc để cải thiện trong 30 ngày tới, anh/chị chọn việc nào?', 'Báo cáo sẽ đặt việc này lên đầu.',
        'single_choice', FALSE, TRUE, 4, 1, '{"leadTag": "priority_need"}'::jsonb)
ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,
    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;
INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES
    ('0471968f-9a73-5bdc-8cdd-e0f13204ee64', 'fd417b97-a021-5d43-b5ce-c89057cac60e', 'PRI_RETENTION', 'Giữ chân khách quen', NULL, 1, '{"tag": "need_crm"}'::jsonb),
    ('9388dddf-e0b2-5a16-b44d-95a114c7a97d', 'fd417b97-a021-5d43-b5ce-c89057cac60e', 'PRI_STOCK', 'Giảm hàng tồn, hàng cận hạn', NULL, 2, '{"tag": "need_inventory"}'::jsonb),
    ('a6a7f674-a349-5b71-8e53-7a69a4ce54a6', 'fd417b97-a021-5d43-b5ce-c89057cac60e', 'PRI_STAFF', 'Nhân viên làm đều tay', NULL, 3, '{"tag": "need_ops"}'::jsonb),
    ('6a49994a-e703-5da5-bcc9-b86df0707347', 'fd417b97-a021-5d43-b5ce-c89057cac60e', 'PRI_PROFIT', 'Nắm rõ lãi lỗ', NULL, 4, '{"tag": "need_profit"}'::jsonb),
    ('aa6f145c-66d0-5d4f-b9bb-a7448715b532', 'fd417b97-a021-5d43-b5ce-c89057cac60e', 'PRI_COMPLIANCE', 'Sổ sách gọn, sẵn sàng khi kiểm tra', NULL, 5, '{"tag": "need_compliance"}'::jsonb),
    ('7a32db00-3247-5a73-97ae-4a85b77a7411', 'fd417b97-a021-5d43-b5ce-c89057cac60e', 'PRI_DOCTOR', 'Kết nối bác sĩ, đơn thuốc online', NULL, 6, '{"tag": "need_telemed"}'::jsonb),
    ('9b9f67be-eef9-59b9-a754-3b49b70fe112', 'fd417b97-a021-5d43-b5ce-c89057cac60e', 'PRI_OTHER', 'Việc khác', NULL, 7, '{"tag": "need_other"}'::jsonb)
ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,
    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;

-- Luật phát hiện / việc nên làm / ước tính tiền
INSERT INTO assessment_rule (id, template_id, code, name, expression, action_type, action_payload, priority, is_active) VALUES
    ('b5644fb9-55e7-519f-8e68-865065bc1f18', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_INS_C2', 'V2 C2 thấp — phát hiện', 'response.C2.score <= 2', 'insight', '{"title": "Chưa tra được lịch sử mua của khách quen", "body": "Khách quen quay lại mà nhân viên không biết lần trước họ mua gì, nên khó tư vấn tiếp và khó bán thêm đúng nhu cầu.", "severity": "info", "scopeType": "category", "scopeCode": "CUSTOMER"}'::jsonb, 80, TRUE),
    ('0e2cd716-bc33-518c-9547-2a2347483c93', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_REC_C2', 'V2 C2 thấp — việc nên làm', 'response.C2.score <= 2', 'recommendation', '{"title": "Lập sổ 20 khách quen nhất", "body": "Tuần này: Mở một cuốn sổ (hoặc một trang Excel) cho 20 khách quen nhất: tên, số điện thoại, thuốc hay mua. Nhân viên ghi thêm sau mỗi lần bán cho họ.\nCách đo: Sau 30 ngày: số khách quen đã có hồ sơ.\nNếu muốn làm tự động: Hồ sơ khách gắn với từng đơn bán, tra bằng số điện thoại.", "priority": 2, "estimateHint": "Sau 30 ngày: số khách quen đã có hồ sơ.", "productArea": "crm"}'::jsonb, 80, TRUE),
    ('0cb9765c-d53b-5ccd-b82e-5d1562c17810', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_INS_C3', 'V2 C3 thấp — phát hiện', 'response.C3.score <= 2', 'insight', '{"title": "Khách dùng thuốc lâu dài chưa được nhắc", "body": "Khách dùng thuốc lâu dài không được nhắc khi sắp hết thuốc. Đây là nhóm khách dễ chuyển sang chỗ khác nhất.", "severity": "warning", "scopeType": "category", "scopeCode": "CUSTOMER"}'::jsonb, 86, TRUE),
    ('ec2e7bba-9185-5c9d-88e7-34e0d25ca56a', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_REC_C3', 'V2 C3 thấp — việc nên làm', 'response.C3.score <= 2', 'recommendation', '{"title": "Nhắc khách sắp hết thuốc dùng lâu dài", "body": "Tuần này: Lập danh sách khách mua thuốc huyết áp, tiểu đường trong tháng, ghi ngày dự kiến hết thuốc. Mỗi sáng xem danh sách và nhắn Zalo cho người sắp hết.\nCách đo: Sau 30 ngày: trong số khách được nhắc, bao nhiêu người quay lại mua.\nNếu muốn làm tự động: Nhắc lịch mua lại tự động theo đơn trước.", "priority": 1, "estimateHint": "Sau 30 ngày: trong số khách được nhắc, bao nhiêu người quay lại mua.", "productArea": "crm"}'::jsonb, 86, TRUE),
    ('f37d4691-c89e-5239-aace-e65aecdfae5e', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_INS_C4', 'V2 C4 thấp — phát hiện', 'response.C4.score <= 2', 'insight', '{"title": "Chưa hỏi thăm khách sau khi bán", "body": "Sau khi bán, nhà thuốc gần như không hỏi thăm lại khách.", "severity": "info", "scopeType": "category", "scopeCode": "CUSTOMER"}'::jsonb, 78, TRUE),
    ('b97c8555-3c5d-5df7-8463-4a38a17e917e', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_REC_C4', 'V2 C4 thấp — việc nên làm', 'response.C4.score <= 2', 'recommendation', '{"title": "Hỏi thăm 3 khách mỗi ngày", "body": "Tuần này: Mỗi ngày chọn 3 khách mua thuốc điều trị (kháng sinh, thuốc đau dạ dày…), 2–3 ngày sau nhắn hỏi thăm thuốc có hợp không.\nCách đo: Số khách được hỏi thăm mỗi tuần.\nNếu muốn làm tự động: Danh sách khách cần hỏi thăm tự sinh theo đơn.", "priority": 3, "estimateHint": "Số khách được hỏi thăm mỗi tuần.", "productArea": "crm"}'::jsonb, 78, TRUE),
    ('fe871ede-e9dd-5b1f-b638-d967b305ee97', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_INS_C5', 'V2 C5 thấp — phát hiện', 'response.C5.score <= 2', 'insight', '{"title": "Chưa đo được khách mới, khách quay lại", "body": "Nhà thuốc chưa biết mỗi tháng có bao nhiêu khách mới, bao nhiêu khách cũ quay lại, nên không biết việc giữ khách đang tốt lên hay xấu đi.", "severity": "info", "scopeType": "category", "scopeCode": "CUSTOMER"}'::jsonb, 76, TRUE),
    ('cae91657-3d3e-5e6a-8ceb-167154e0f011', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_REC_C5', 'V2 C5 thấp — việc nên làm', 'response.C5.score <= 2', 'recommendation', '{"title": "Đếm khách mới và khách quen trong 1 tuần", "body": "Tuần này: Trong 1 tuần, mỗi đơn bán đánh dấu ''mới'' hoặc ''quen'' vào sổ. Cuối tuần đếm tổng.\nCách đo: Tỷ lệ khách quen trên tổng khách, đo lại sau 30 ngày.\nNếu muốn làm tự động: Báo cáo khách mới và khách quay lại theo tháng.", "priority": 4, "estimateHint": "Tỷ lệ khách quen trên tổng khách, đo lại sau 30 ngày.", "productArea": "crm"}'::jsonb, 76, TRUE),
    ('6861c3e3-9771-5028-8ac9-43dbf971484d', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_INS_I1', 'V2 I1 thấp — phát hiện', 'response.I1.score <= 2', 'insight', '{"title": "Hàng cận hạn được phát hiện muộn", "body": "Hàng sắp hết hạn được phát hiện tình cờ hoặc khi đã quá muộn để xử lý.", "severity": "warning", "scopeType": "category", "scopeCode": "INVENTORY"}'::jsonb, 84, TRUE),
    ('d035bf1e-a941-541a-afb8-ae53832828bb', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_REC_I1', 'V2 I1 thấp — việc nên làm', 'response.I1.score <= 2', 'recommendation', '{"title": "Rà cận hạn mỗi thứ Hai", "body": "Tuần này: Thứ Hai hằng tuần, rà các lô hết hạn trong 3 tháng tới. Dán nhãn màu lên hộp và xếp ra trước để bán trước.\nCách đo: Tiền hàng phải hủy hoặc trả lại tháng sau so với tháng này.\nNếu muốn làm tự động: Cảnh báo cận hạn theo từng lô.", "priority": 1, "estimateHint": "Tiền hàng phải hủy hoặc trả lại tháng sau so với tháng này.", "productArea": "inventory"}'::jsonb, 84, TRUE),
    ('9694f4b5-e6cb-54a2-ab31-1d2604a6f028', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_INS_I3', 'V2 I3 thấp — phát hiện', 'response.I3.score <= 2', 'insight', '{"title": "Hết hàng khi khách hỏi mua", "body": "Khách hỏi mua mà nhà thuốc hết hàng khá thường xuyên. Mỗi lần như vậy là một đơn có thể mất, và khách có thể không quay lại.", "severity": "warning", "scopeType": "category", "scopeCode": "INVENTORY"}'::jsonb, 84, TRUE),
    ('fb6c8804-e136-58a8-824d-1daad16b3ee0', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_REC_I3', 'V2 I3 thấp — việc nên làm', 'response.I3.score <= 2', 'recommendation', '{"title": "Ghi lại mỗi lần hết hàng", "body": "Tuần này: Để một tờ giấy ở quầy: mỗi lần hết hàng khi khách hỏi, ghi tên thuốc. Cuối tuần nhập bổ sung những mặt hàng bị ghi từ 2 lần trở lên.\nCách đo: Số lần hết hàng mỗi tuần.\nNếu muốn làm tự động: Mức tồn tối thiểu và gợi ý đặt hàng.", "priority": 1, "estimateHint": "Số lần hết hàng mỗi tuần.", "productArea": "inventory"}'::jsonb, 84, TRUE),
    ('ceb7e9dc-ac1c-5000-9753-e8643f0c95d5', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_INS_I4', 'V2 I4 thấp — phát hiện', 'response.I4.score <= 2', 'insight', '{"title": "Đặt hàng theo cảm giác", "body": "Số lượng đặt hàng dựa vào cảm giác hoặc gợi ý của trình dược viên, nên dễ vừa thiếu hàng bán chạy vừa dư hàng bán chậm.", "severity": "info", "scopeType": "category", "scopeCode": "INVENTORY"}'::jsonb, 80, TRUE),
    ('68f9c88a-8855-5c48-9ff1-73506bbf5cc6', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_REC_I4', 'V2 I4 thấp — việc nên làm', 'response.I4.score <= 2', 'recommendation', '{"title": "Đặt mức tồn tối thiểu cho 30 mặt hàng chạy nhất", "body": "Tuần này: Chọn 30 mặt hàng bán chạy nhất. Ghi mức tồn tối thiểu cho từng món (bằng khoảng 1 tuần bán), dưới mức đó thì đặt.\nCách đo: Số lần hết hàng của 30 mặt hàng này.\nNếu muốn làm tự động: Mức tồn tối thiểu và gợi ý đặt hàng.", "priority": 2, "estimateHint": "Số lần hết hàng của 30 mặt hàng này.", "productArea": "inventory"}'::jsonb, 80, TRUE),
    ('247732ab-2ca4-541e-95bf-a2f51c9d3a68', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_INS_I5', 'V2 I5 thấp — phát hiện', 'response.I5.score <= 2', 'insight', '{"title": "Lâu chưa kiểm kê toàn bộ kho", "body": "Đã lâu chưa kiểm kê toàn bộ kho, nên số trên sổ hoặc trên máy có thể lệch xa với hàng thật.", "severity": "info", "scopeType": "category", "scopeCode": "INVENTORY"}'::jsonb, 76, TRUE),
    ('3cf043bd-dc73-5576-a0ac-980afad67c3c', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_REC_I5', 'V2 I5 thấp — việc nên làm', 'response.I5.score <= 2', 'recommendation', '{"title": "Kiểm kê xoay vòng từng nhóm hàng", "body": "Tuần này: Không cần kiểm kê cả kho cùng lúc. Mỗi tuần kiểm một nhóm hàng (ví dụ tuần này kiểm nhóm kháng sinh), ghi lại phần lệch.\nCách đo: Tỷ lệ lệch giữa sổ và hàng thật của từng nhóm.\nNếu muốn làm tự động: Kiểm kê theo nhóm hàng trên điện thoại.", "priority": 4, "estimateHint": "Tỷ lệ lệch giữa sổ và hàng thật của từng nhóm.", "productArea": "inventory"}'::jsonb, 76, TRUE),
    ('8dfc409a-6302-5a1f-bebb-37ba749604c0', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_INS_I6', 'V2 I6 thấp — phát hiện', 'response.I6.score <= 2', 'insight', '{"title": "Chưa biết hàng chậm bán đang giữ vốn", "body": "Chưa biết hàng nào bán chậm đang giữ vốn.", "severity": "info", "scopeType": "category", "scopeCode": "INVENTORY"}'::jsonb, 78, TRUE),
    ('41addcd4-20cd-54cd-9761-28b77f2faa25', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_REC_I6', 'V2 I6 thấp — việc nên làm', 'response.I6.score <= 2', 'recommendation', '{"title": "Lập danh sách 10 mặt hàng chậm nhất", "body": "Tuần này: Đi một vòng kệ, đánh dấu những hộp còn nguyên và có bụi. Lập danh sách 10 mặt hàng đó và ngừng nhập thêm.\nCách đo: Tiền vốn nằm trong 10 mặt hàng này, đo lại sau 60 ngày.\nNếu muốn làm tự động: Báo cáo hàng chậm bán theo giá trị tồn.", "priority": 3, "estimateHint": "Tiền vốn nằm trong 10 mặt hàng này, đo lại sau 60 ngày.", "productArea": "inventory"}'::jsonb, 78, TRUE),
    ('f801f00b-9ce0-5998-a7f9-22e29f2efdc4', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_INS_O1', 'V2 O1 thấp — phát hiện', 'response.O1.score <= 2', 'insight', '{"title": "Nhà thuốc phụ thuộc vào chủ", "body": "Nhà thuốc phụ thuộc vào anh/chị. Vắng mặt vài ngày là việc bán bị ảnh hưởng.", "severity": "warning", "scopeType": "category", "scopeCode": "OPERATIONS"}'::jsonb, 82, TRUE),
    ('4ab34a26-9f2b-5f48-8877-e97d477c663f', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_REC_O1', 'V2 O1 thấp — việc nên làm', 'response.O1.score <= 2', 'recommendation', '{"title": "Viết 10 câu nhân viên hay hỏi kèm câu trả lời", "body": "Tuần này: Viết ra 10 câu nhân viên hay gọi hỏi anh/chị nhất, kèm câu trả lời. Dán ở quầy.\nCách đo: Số cuộc gọi hỏi việc trong tuần anh/chị vắng mặt.", "priority": 2, "estimateHint": "Số cuộc gọi hỏi việc trong tuần anh/chị vắng mặt."}'::jsonb, 82, TRUE),
    ('510a1e61-f5b2-51d9-b31c-bfccc9e57b92', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_INS_O2', 'V2 O2 thấp — phát hiện', 'response.O2.score <= 2', 'insight', '{"title": "Nhân viên mới lâu mới tự đứng quầy", "body": "Nhân viên mới mất nhiều thời gian mới tự đứng quầy được.", "severity": "info", "scopeType": "category", "scopeCode": "OPERATIONS"}'::jsonb, 76, TRUE),
    ('95e1baf7-88ef-5091-a62a-6f4eb0d17851', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_REC_O2', 'V2 O2 thấp — việc nên làm', 'response.O2.score <= 2', 'recommendation', '{"title": "Viết danh sách việc tuần đầu cho nhân viên mới", "body": "Tuần này: Viết danh sách những việc nhân viên mới phải biết trong tuần đầu, và ai kèm từng việc.\nCách đo: Số ngày để nhân viên mới tự đứng quầy, ở lần tuyển tiếp theo.", "priority": 4, "estimateHint": "Số ngày để nhân viên mới tự đứng quầy, ở lần tuyển tiếp theo."}'::jsonb, 76, TRUE),
    ('eefb39a6-ff7f-512f-b8b7-9a5a138d4aeb', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_INS_O3', 'V2 O3 thấp — phát hiện', 'response.O3.score <= 2', 'insight', '{"title": "Mỗi nhân viên tư vấn một kiểu", "body": "Mỗi nhân viên tư vấn một kiểu. Khách có thể nhận lời khuyên khác nhau ở cùng một nhà thuốc.", "severity": "info", "scopeType": "category", "scopeCode": "OPERATIONS"}'::jsonb, 78, TRUE),
    ('73f19e91-d947-5749-ab30-238dcd265eee', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_REC_O3', 'V2 O3 thấp — việc nên làm', 'response.O3.score <= 2', 'recommendation', '{"title": "Thống nhất hướng dẫn cho 5 bệnh hay gặp", "body": "Tuần này: Chọn 5 bệnh hay gặp nhất. Mỗi bệnh viết 1 trang: hỏi khách gì, khuyên gì, khi nào khuyên đi khám. Cả nhóm cùng thống nhất.\nCách đo: Số bệnh đã có hướng dẫn chung.", "priority": 3, "estimateHint": "Số bệnh đã có hướng dẫn chung."}'::jsonb, 78, TRUE),
    ('79e7fba2-79aa-5dc4-98e1-f0faa3776d6e', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_INS_O4', 'V2 O4 thấp — phát hiện', 'response.O4.score <= 2', 'insight', '{"title": "Tiền cuối ngày hay lệch", "body": "Tiền cuối ngày hay lệch với số đã bán mà không rõ nguyên nhân. Đây có thể là thất thoát.", "severity": "info", "scopeType": "category", "scopeCode": "OPERATIONS"}'::jsonb, 80, TRUE),
    ('e87ee5f5-5a63-58d9-88ba-3a4bf5d13dec', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_REC_O4', 'V2 O4 thấp — việc nên làm', 'response.O4.score <= 2', 'recommendation', '{"title": "Đối chiếu tiền cuối mỗi ca", "body": "Tuần này: Đối chiếu tiền với số bán ngay cuối mỗi ca, không để đến cuối ngày. Ghi phần lệch và tên người trực ca.\nCách đo: Số ngày lệch tiền trong tháng.\nNếu muốn làm tự động: Chốt ca theo từng nhân viên.", "priority": 2, "estimateHint": "Số ngày lệch tiền trong tháng.", "productArea": "ops"}'::jsonb, 80, TRUE),
    ('471235d3-f8b9-5bab-9062-81242a9a4188', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_INS_O5', 'V2 O5 thấp — phát hiện', 'response.O5.score <= 2', 'insight', '{"title": "Việc định kỳ chưa được ghi đều", "body": "Các việc định kỳ như ghi nhiệt độ, độ ẩm và sổ sách chưa được làm đều. Đây thường là phần bị nhắc khi có kiểm tra.", "severity": "warning", "scopeType": "category", "scopeCode": "OPERATIONS"}'::jsonb, 82, TRUE),
    ('6b89aee2-f092-5d90-9553-ed53d0d37897', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_REC_O5', 'V2 O5 thấp — việc nên làm', 'response.O5.score <= 2', 'recommendation', '{"title": "Giao người và giờ cố định cho sổ định kỳ", "body": "Tuần này: Giao cho một người cụ thể, đặt giờ cố định mỗi ngày, và để sổ ngay cạnh nhiệt kế.\nCách đo: Số ngày trong tháng có ghi đủ.", "priority": 2, "estimateHint": "Số ngày trong tháng có ghi đủ."}'::jsonb, 82, TRUE),
    ('7a36270d-a204-5c32-ab66-305222fa209f', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_INS_B1', 'V2 B1 thấp — phát hiện', 'response.B1.score <= 2', 'insight', '{"title": "Chưa biết rõ lãi mỗi tháng", "body": "Chưa biết rõ mỗi tháng nhà thuốc lãi bao nhiêu.", "severity": "warning", "scopeType": "category", "scopeCode": "BUSINESS"}'::jsonb, 84, TRUE),
    ('e003309c-2858-585f-9972-a8221c7fa267', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_REC_B1', 'V2 B1 thấp — việc nên làm', 'response.B1.score <= 2', 'recommendation', '{"title": "Tính con số lãi tháng đầu tiên", "body": "Tuần này: Cuối tháng này, ghi 4 con số: tiền bán, tiền nhập hàng, tiền thuê, tiền lương và các chi phí khác. Lấy tiền bán trừ ba khoản sau là ra con số lãi đầu tiên.\nCách đo: Có con số lãi của 2 tháng liền để so sánh.\nNếu muốn làm tự động: Báo cáo lãi gộp theo ngày và theo tháng.", "priority": 1, "estimateHint": "Có con số lãi của 2 tháng liền để so sánh.", "productArea": "reports"}'::jsonb, 84, TRUE),
    ('b33ea443-19ed-5d5e-87e1-3c12430200cf', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_INS_B2', 'V2 B2 thấp — phát hiện', 'response.B2.score <= 2', 'insight', '{"title": "Chưa biết nhóm hàng nào lãi nhất", "body": "Chưa biết nhóm hàng nào thật sự mang lại lãi. Bán nhiều chưa chắc đã lãi nhiều.", "severity": "info", "scopeType": "category", "scopeCode": "BUSINESS"}'::jsonb, 78, TRUE),
    ('f225ffdf-f1d9-5177-9f05-22c729bec227', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_REC_B2', 'V2 B2 thấp — việc nên làm', 'response.B2.score <= 2', 'recommendation', '{"title": "Xếp 20 mặt hàng chạy nhất theo tiền lãi", "body": "Tuần này: Lấy 20 mặt hàng bán chạy nhất, ghi giá nhập và giá bán của từng món. Xếp theo tiền lãi mỗi hộp.\nCách đo: Danh sách 10 mặt hàng lãi nhất để ưu tiên trưng bày và tư vấn.\nNếu muốn làm tự động: Lãi gộp theo mặt hàng và nhóm hàng.", "priority": 3, "estimateHint": "Danh sách 10 mặt hàng lãi nhất để ưu tiên trưng bày và tư vấn.", "productArea": "reports"}'::jsonb, 78, TRUE),
    ('f458364b-1b58-51db-af29-efa0f1320784', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_INS_B3', 'V2 B3 thấp — phát hiện', 'response.B3.score <= 2', 'insight', '{"title": "Khuyến mại chưa biết lời hay lỗ", "body": "Khuyến mại được làm mà chưa biết lời hay lỗ.", "severity": "info", "scopeType": "category", "scopeCode": "BUSINESS"}'::jsonb, 74, TRUE),
    ('fa40451d-149f-5872-8824-6395344511a7', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_REC_B3', 'V2 B3 thấp — việc nên làm', 'response.B3.score <= 2', 'recommendation', '{"title": "Đo lời lỗ cho đợt khuyến mại tới", "body": "Tuần này: Lần khuyến mại tới, ghi số bán của mặt hàng đó 2 tuần trước và 2 tuần trong đợt. Trừ phần đã giảm giá để biết lời hay lỗ.\nCách đo: Đợt khuyến mại tới có lãi thêm hay không.", "priority": 4, "estimateHint": "Đợt khuyến mại tới có lãi thêm hay không."}'::jsonb, 74, TRUE),
    ('c3560ce0-70b4-5bb1-8826-0673919eef77', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_INS_B4', 'V2 B4 thấp — phát hiện', 'response.B4.score <= 2', 'insight', '{"title": "Chưa có cách tìm chỗ bán chậm", "body": "Khi bán chậm lại, chưa có cách tìm ra chậm ở đâu.", "severity": "info", "scopeType": "category", "scopeCode": "BUSINESS"}'::jsonb, 78, TRUE),
    ('116e2dd4-1543-5f5f-881c-3d2b2cdc4135', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_REC_B4', 'V2 B4 thấp — việc nên làm', 'response.B4.score <= 2', 'recommendation', '{"title": "Ghi số bán theo 5 nhóm hàng mỗi tuần", "body": "Tuần này: Chia tiền bán theo 5 nhóm hàng chính và ghi theo tuần. Khi bán chậm thì so nhóm nào giảm.\nCách đo: Có số bán theo nhóm hàng của 4 tuần liền.\nNếu muốn làm tự động: Số bán theo nhóm hàng, nhóm khách.", "priority": 3, "estimateHint": "Có số bán theo nhóm hàng của 4 tuần liền.", "productArea": "reports"}'::jsonb, 78, TRUE),
    ('af70d439-34a7-5eaf-8680-05a56558fc55', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_INS_T1', 'V2 T1 thấp — phát hiện', 'response.T1.score <= 2', 'insight', '{"title": "Việc bán hàng chưa được ghi để tra cứu", "body": "Việc bán hàng chưa được ghi lại để tra cứu và tính toán được.", "severity": "info", "scopeType": "category", "scopeCode": "TECH"}'::jsonb, 78, TRUE),
    ('ef50f025-2b55-5cab-b704-4e9bfdf2ec50', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_REC_T1', 'V2 T1 thấp — việc nên làm', 'response.T1.score <= 2', 'recommendation', '{"title": "Ghi bán hàng 4 cột trong 1 tuần", "body": "Tuần này: Bắt đầu bằng một file Excel với 4 cột: ngày, mặt hàng, số lượng, tiền. Ghi trong 1 tuần để thấy mình cần gì.\nCách đo: Số ngày trong tuần có ghi đủ.\nNếu muốn làm tự động: Phần mềm bán hàng cho nhà thuốc.", "priority": 3, "estimateHint": "Số ngày trong tuần có ghi đủ.", "productArea": "tech"}'::jsonb, 78, TRUE),
    ('7dbbde4f-8207-5dc4-a727-0f8d9516fc0b', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_INS_T2', 'V2 T2 thấp — phát hiện', 'response.T2.score <= 2', 'insight', '{"title": "Không nắm tình hình khi vắng mặt", "body": "Không ở nhà thuốc thì không nắm được tình hình bán hàng.", "severity": "info", "scopeType": "category", "scopeCode": "TECH"}'::jsonb, 76, TRUE),
    ('51aebb17-7e16-5cc3-afb4-b02498358f4c', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_REC_T2', 'V2 T2 thấp — việc nên làm', 'response.T2.score <= 2', 'recommendation', '{"title": "Nhân viên nhắn 3 con số cuối mỗi ca", "body": "Tuần này: Cuối mỗi ca, nhân viên nhắn Zalo 3 con số: số đơn, tiền thu, những mặt hàng bị hết.\nCách đo: Số ngày nhận đủ tin nhắn.\nNếu muốn làm tự động: Xem số bán trên điện thoại.", "priority": 4, "estimateHint": "Số ngày nhận đủ tin nhắn.", "productArea": "tech"}'::jsonb, 76, TRUE),
    ('34ab37f5-9fea-53eb-8133-fdc783cb3f82', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_INS_T3', 'V2 T3 thấp — phát hiện', 'response.T3.score <= 2', 'insight', '{"title": "Thông tin bán hàng, kho, khách nằm rời rạc", "body": "Thông tin bán hàng, kho và khách mỗi thứ một nơi, nên không trả lời được những câu như ''khách nào hay mua hàng sắp hết hạn này''.", "severity": "info", "scopeType": "category", "scopeCode": "TECH"}'::jsonb, 76, TRUE),
    ('4ebbba6f-d97c-5bcb-b83f-879c70edaf59', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_REC_T3', 'V2 T3 thấp — việc nên làm', 'response.T3.score <= 2', 'recommendation', '{"title": "Ghi số điện thoại khách cùng đơn bán", "body": "Tuần này: Ghi số điện thoại của khách vào cùng chỗ ghi đơn bán, kể cả khi chỉ dùng sổ.\nCách đo: Tỷ lệ đơn bán có ghi số điện thoại khách.\nNếu muốn làm tự động: Bán hàng, kho và khách hàng trên cùng một phần mềm.", "priority": 4, "estimateHint": "Tỷ lệ đơn bán có ghi số điện thoại khách.", "productArea": "tech"}'::jsonb, 76, TRUE),
    ('cd6ba558-1150-5499-a742-487ab7807504', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_INS_T4', 'V2 T4 thấp — phát hiện', 'response.T4.score <= 2', 'insight', '{"title": "Liên thông dữ liệu chưa ổn định", "body": "Việc liên thông dữ liệu bán thuốc chưa ổn định hoặc chưa rõ yêu cầu.", "severity": "info", "scopeType": "category", "scopeCode": "TECH"}'::jsonb, 80, TRUE),
    ('ca055fe6-10e7-5714-ab27-e6260d66aa62', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_REC_T4', 'V2 T4 thấp — việc nên làm', 'response.T4.score <= 2', 'recommendation', '{"title": "Hỏi rõ nhà thuốc còn thiếu bước liên thông nào", "body": "Tuần này: Hỏi lại cơ quan quản lý dược địa phương hoặc nhà cung cấp phần mềm xem nhà thuốc đang thiếu bước nào.\nCách đo: Nhà thuốc biết rõ mình đã đủ yêu cầu hay chưa.\nNếu muốn làm tự động: Kết nối liên thông từ phần mềm bán hàng (cần xác nhận phạm vi hỗ trợ trước khi ghi vào báo cáo).", "priority": 2, "estimateHint": "Nhà thuốc biết rõ mình đã đủ yêu cầu hay chưa.", "productArea": "tech"}'::jsonb, 80, TRUE),
    ('c3b8f19f-10b8-5c22-a968-660fb40ee0b5', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_INS_G1', 'V2 G1 thấp — phát hiện', 'response.G1.score <= 2', 'insight', '{"title": "Chưa có mục tiêu cho năm tới", "body": "Chưa có mục tiêu cụ thể cho năm tới.", "severity": "info", "scopeType": "category", "scopeCode": "GROWTH"}'::jsonb, 72, TRUE),
    ('fa95a1c5-8960-5d3c-acb7-e8b568246cba', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_REC_G1', 'V2 G1 thấp — việc nên làm', 'response.G1.score <= 2', 'recommendation', '{"title": "Đặt 1 mục tiêu đo được cho 90 ngày", "body": "Tuần này: Viết 1 mục tiêu đo được cho 90 ngày tới, ví dụ ''giảm hàng hủy còn dưới 1 triệu/tháng''.\nCách đo: Có mục tiêu và được xem lại mỗi tháng.", "priority": 4, "estimateHint": "Có mục tiêu và được xem lại mỗi tháng."}'::jsonb, 72, TRUE),
    ('88ebbc1a-10b2-54f2-8531-7fc5c2efd15e', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_INS_G2', 'V2 G2 thấp — phát hiện', 'response.G2.score <= 2', 'insight', '{"title": "Cách làm còn phụ thuộc vào người", "body": "Cách làm hiện nay phụ thuộc vào người, nên khó áp dụng sang cơ sở mới.", "severity": "info", "scopeType": "category", "scopeCode": "GROWTH"}'::jsonb, 72, TRUE),
    ('ca757588-e38f-5e25-a02b-2b49a5bb8c8f', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_REC_G2', 'V2 G2 thấp — việc nên làm', 'response.G2.score <= 2', 'recommendation', '{"title": "Viết ra 3 quy trình cốt lõi", "body": "Tuần này: Ghi lại 3 quy trình quan trọng nhất (nhận hàng, bán hàng, chốt ca) thành từng bước.\nCách đo: Số quy trình đã được viết ra.\nNếu muốn làm tự động: Phần mềm dùng chung cho nhiều cơ sở.", "priority": 4, "estimateHint": "Số quy trình đã được viết ra.", "productArea": "multi_branch"}'::jsonb, 72, TRUE),
    ('8750fd2b-8286-5d1d-8d1a-827293f61e3b', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PINS_PAIN_CUSTOMER_C2', 'V2 PAIN_CUSTOMER C2 — phát hiện theo nỗi đau', 'response.P3.option_code = ''PAIN_CUSTOMER'' AND response.C2.score <= 2', 'insight', '{"title": "Chưa tra được lịch sử mua của khách quen", "body": "Khách quen quay lại mà nhân viên không biết lần trước họ mua gì, nên khó tư vấn tiếp và khó bán thêm đúng nhu cầu.", "severity": "info", "scopeType": "category", "scopeCode": "CUSTOMER"}'::jsonb, 97, TRUE),
    ('42bfc1ec-1997-507d-82a6-28bf778b3fcf', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PREC_PAIN_CUSTOMER_C2', 'V2 PAIN_CUSTOMER C2 — việc nên làm theo nỗi đau', 'response.P3.option_code = ''PAIN_CUSTOMER'' AND response.C2.score <= 2', 'recommendation', '{"title": "Lập sổ 20 khách quen nhất", "body": "Tuần này: Mở một cuốn sổ (hoặc một trang Excel) cho 20 khách quen nhất: tên, số điện thoại, thuốc hay mua. Nhân viên ghi thêm sau mỗi lần bán cho họ.\nCách đo: Sau 30 ngày: số khách quen đã có hồ sơ.\nNếu muốn làm tự động: Hồ sơ khách gắn với từng đơn bán, tra bằng số điện thoại.", "priority": 1, "estimateHint": "Sau 30 ngày: số khách quen đã có hồ sơ.", "productArea": "crm"}'::jsonb, 95, TRUE),
    ('5544131f-2968-5e04-9846-afa8183d3f14', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PINS_PAIN_CUSTOMER_C3', 'V2 PAIN_CUSTOMER C3 — phát hiện theo nỗi đau', 'response.P3.option_code = ''PAIN_CUSTOMER'' AND response.C3.score <= 2', 'insight', '{"title": "Khách dùng thuốc lâu dài chưa được nhắc", "body": "Khách dùng thuốc lâu dài không được nhắc khi sắp hết thuốc. Đây là nhóm khách dễ chuyển sang chỗ khác nhất.", "severity": "warning", "scopeType": "category", "scopeCode": "CUSTOMER"}'::jsonb, 98, TRUE),
    ('2374703e-c32c-59f1-b5c7-15beba5b0da8', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PREC_PAIN_CUSTOMER_C3', 'V2 PAIN_CUSTOMER C3 — việc nên làm theo nỗi đau', 'response.P3.option_code = ''PAIN_CUSTOMER'' AND response.C3.score <= 2', 'recommendation', '{"title": "Nhắc khách sắp hết thuốc dùng lâu dài", "body": "Tuần này: Lập danh sách khách mua thuốc huyết áp, tiểu đường trong tháng, ghi ngày dự kiến hết thuốc. Mỗi sáng xem danh sách và nhắn Zalo cho người sắp hết.\nCách đo: Sau 30 ngày: trong số khách được nhắc, bao nhiêu người quay lại mua.\nNếu muốn làm tự động: Nhắc lịch mua lại tự động theo đơn trước.", "priority": 1, "estimateHint": "Sau 30 ngày: trong số khách được nhắc, bao nhiêu người quay lại mua.", "productArea": "crm"}'::jsonb, 95, TRUE),
    ('1b10585f-9dda-5ccc-883f-dadc9251d991', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PINS_PAIN_CUSTOMER_C4', 'V2 PAIN_CUSTOMER C4 — phát hiện theo nỗi đau', 'response.P3.option_code = ''PAIN_CUSTOMER'' AND response.C4.score <= 2', 'insight', '{"title": "Chưa hỏi thăm khách sau khi bán", "body": "Sau khi bán, nhà thuốc gần như không hỏi thăm lại khách.", "severity": "info", "scopeType": "category", "scopeCode": "CUSTOMER"}'::jsonb, 97, TRUE),
    ('2c56595a-acf2-5265-b2ee-bd16aef19917', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PREC_PAIN_CUSTOMER_C4', 'V2 PAIN_CUSTOMER C4 — việc nên làm theo nỗi đau', 'response.P3.option_code = ''PAIN_CUSTOMER'' AND response.C4.score <= 2', 'recommendation', '{"title": "Hỏi thăm 3 khách mỗi ngày", "body": "Tuần này: Mỗi ngày chọn 3 khách mua thuốc điều trị (kháng sinh, thuốc đau dạ dày…), 2–3 ngày sau nhắn hỏi thăm thuốc có hợp không.\nCách đo: Số khách được hỏi thăm mỗi tuần.\nNếu muốn làm tự động: Danh sách khách cần hỏi thăm tự sinh theo đơn.", "priority": 1, "estimateHint": "Số khách được hỏi thăm mỗi tuần.", "productArea": "crm"}'::jsonb, 95, TRUE),
    ('39fa3257-5a91-5df9-8aec-ab99b3466112', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PINS_PAIN_CUSTOMER_C5', 'V2 PAIN_CUSTOMER C5 — phát hiện theo nỗi đau', 'response.P3.option_code = ''PAIN_CUSTOMER'' AND response.C5.score <= 2', 'insight', '{"title": "Chưa đo được khách mới, khách quay lại", "body": "Nhà thuốc chưa biết mỗi tháng có bao nhiêu khách mới, bao nhiêu khách cũ quay lại, nên không biết việc giữ khách đang tốt lên hay xấu đi.", "severity": "info", "scopeType": "category", "scopeCode": "CUSTOMER"}'::jsonb, 97, TRUE),
    ('8400ddf6-1c19-5f9d-9930-5114fd306111', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PREC_PAIN_CUSTOMER_C5', 'V2 PAIN_CUSTOMER C5 — việc nên làm theo nỗi đau', 'response.P3.option_code = ''PAIN_CUSTOMER'' AND response.C5.score <= 2', 'recommendation', '{"title": "Đếm khách mới và khách quen trong 1 tuần", "body": "Tuần này: Trong 1 tuần, mỗi đơn bán đánh dấu ''mới'' hoặc ''quen'' vào sổ. Cuối tuần đếm tổng.\nCách đo: Tỷ lệ khách quen trên tổng khách, đo lại sau 30 ngày.\nNếu muốn làm tự động: Báo cáo khách mới và khách quay lại theo tháng.", "priority": 1, "estimateHint": "Tỷ lệ khách quen trên tổng khách, đo lại sau 30 ngày.", "productArea": "crm"}'::jsonb, 95, TRUE),
    ('2032de65-a99f-5e5d-850a-8081060bcf17', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PINS_PAIN_INVENTORY_I1', 'V2 PAIN_INVENTORY I1 — phát hiện theo nỗi đau', 'response.P3.option_code = ''PAIN_INVENTORY'' AND response.I1.score <= 2', 'insight', '{"title": "Hàng cận hạn được phát hiện muộn", "body": "Hàng sắp hết hạn được phát hiện tình cờ hoặc khi đã quá muộn để xử lý.", "severity": "warning", "scopeType": "category", "scopeCode": "INVENTORY"}'::jsonb, 98, TRUE),
    ('35f373a6-285c-552e-bce7-49af359d328a', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PREC_PAIN_INVENTORY_I1', 'V2 PAIN_INVENTORY I1 — việc nên làm theo nỗi đau', 'response.P3.option_code = ''PAIN_INVENTORY'' AND response.I1.score <= 2', 'recommendation', '{"title": "Rà cận hạn mỗi thứ Hai", "body": "Tuần này: Thứ Hai hằng tuần, rà các lô hết hạn trong 3 tháng tới. Dán nhãn màu lên hộp và xếp ra trước để bán trước.\nCách đo: Tiền hàng phải hủy hoặc trả lại tháng sau so với tháng này.\nNếu muốn làm tự động: Cảnh báo cận hạn theo từng lô.", "priority": 1, "estimateHint": "Tiền hàng phải hủy hoặc trả lại tháng sau so với tháng này.", "productArea": "inventory"}'::jsonb, 95, TRUE),
    ('63e8be99-259b-5fd6-88e7-9cbc81853b13', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PINS_PAIN_INVENTORY_I3', 'V2 PAIN_INVENTORY I3 — phát hiện theo nỗi đau', 'response.P3.option_code = ''PAIN_INVENTORY'' AND response.I3.score <= 2', 'insight', '{"title": "Hết hàng khi khách hỏi mua", "body": "Khách hỏi mua mà nhà thuốc hết hàng khá thường xuyên. Mỗi lần như vậy là một đơn có thể mất, và khách có thể không quay lại.", "severity": "warning", "scopeType": "category", "scopeCode": "INVENTORY"}'::jsonb, 98, TRUE),
    ('c090a766-c486-5dd5-ab9f-e6514440da55', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PREC_PAIN_INVENTORY_I3', 'V2 PAIN_INVENTORY I3 — việc nên làm theo nỗi đau', 'response.P3.option_code = ''PAIN_INVENTORY'' AND response.I3.score <= 2', 'recommendation', '{"title": "Ghi lại mỗi lần hết hàng", "body": "Tuần này: Để một tờ giấy ở quầy: mỗi lần hết hàng khi khách hỏi, ghi tên thuốc. Cuối tuần nhập bổ sung những mặt hàng bị ghi từ 2 lần trở lên.\nCách đo: Số lần hết hàng mỗi tuần.\nNếu muốn làm tự động: Mức tồn tối thiểu và gợi ý đặt hàng.", "priority": 1, "estimateHint": "Số lần hết hàng mỗi tuần.", "productArea": "inventory"}'::jsonb, 95, TRUE),
    ('e3a5d22c-bcf2-5bb5-8d32-0410c2acee6b', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PINS_PAIN_INVENTORY_I4', 'V2 PAIN_INVENTORY I4 — phát hiện theo nỗi đau', 'response.P3.option_code = ''PAIN_INVENTORY'' AND response.I4.score <= 2', 'insight', '{"title": "Đặt hàng theo cảm giác", "body": "Số lượng đặt hàng dựa vào cảm giác hoặc gợi ý của trình dược viên, nên dễ vừa thiếu hàng bán chạy vừa dư hàng bán chậm.", "severity": "info", "scopeType": "category", "scopeCode": "INVENTORY"}'::jsonb, 97, TRUE),
    ('dcac9f8f-ed52-58e4-a9fd-8e623e61a367', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PREC_PAIN_INVENTORY_I4', 'V2 PAIN_INVENTORY I4 — việc nên làm theo nỗi đau', 'response.P3.option_code = ''PAIN_INVENTORY'' AND response.I4.score <= 2', 'recommendation', '{"title": "Đặt mức tồn tối thiểu cho 30 mặt hàng chạy nhất", "body": "Tuần này: Chọn 30 mặt hàng bán chạy nhất. Ghi mức tồn tối thiểu cho từng món (bằng khoảng 1 tuần bán), dưới mức đó thì đặt.\nCách đo: Số lần hết hàng của 30 mặt hàng này.\nNếu muốn làm tự động: Mức tồn tối thiểu và gợi ý đặt hàng.", "priority": 1, "estimateHint": "Số lần hết hàng của 30 mặt hàng này.", "productArea": "inventory"}'::jsonb, 95, TRUE),
    ('eafa67a8-33de-51cb-afdf-10bff4c04931', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PINS_PAIN_INVENTORY_I5', 'V2 PAIN_INVENTORY I5 — phát hiện theo nỗi đau', 'response.P3.option_code = ''PAIN_INVENTORY'' AND response.I5.score <= 2', 'insight', '{"title": "Lâu chưa kiểm kê toàn bộ kho", "body": "Đã lâu chưa kiểm kê toàn bộ kho, nên số trên sổ hoặc trên máy có thể lệch xa với hàng thật.", "severity": "info", "scopeType": "category", "scopeCode": "INVENTORY"}'::jsonb, 97, TRUE),
    ('caee1910-8053-57d2-9b6a-81579ba029db', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PREC_PAIN_INVENTORY_I5', 'V2 PAIN_INVENTORY I5 — việc nên làm theo nỗi đau', 'response.P3.option_code = ''PAIN_INVENTORY'' AND response.I5.score <= 2', 'recommendation', '{"title": "Kiểm kê xoay vòng từng nhóm hàng", "body": "Tuần này: Không cần kiểm kê cả kho cùng lúc. Mỗi tuần kiểm một nhóm hàng (ví dụ tuần này kiểm nhóm kháng sinh), ghi lại phần lệch.\nCách đo: Tỷ lệ lệch giữa sổ và hàng thật của từng nhóm.\nNếu muốn làm tự động: Kiểm kê theo nhóm hàng trên điện thoại.", "priority": 1, "estimateHint": "Tỷ lệ lệch giữa sổ và hàng thật của từng nhóm.", "productArea": "inventory"}'::jsonb, 95, TRUE),
    ('1d7c1b4b-40aa-59bf-a7d8-5bb6a72c7cd7', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PINS_PAIN_INVENTORY_I6', 'V2 PAIN_INVENTORY I6 — phát hiện theo nỗi đau', 'response.P3.option_code = ''PAIN_INVENTORY'' AND response.I6.score <= 2', 'insight', '{"title": "Chưa biết hàng chậm bán đang giữ vốn", "body": "Chưa biết hàng nào bán chậm đang giữ vốn.", "severity": "info", "scopeType": "category", "scopeCode": "INVENTORY"}'::jsonb, 97, TRUE),
    ('6e2e9dc1-91b6-5e5a-bbee-3d36bbcd930c', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PREC_PAIN_INVENTORY_I6', 'V2 PAIN_INVENTORY I6 — việc nên làm theo nỗi đau', 'response.P3.option_code = ''PAIN_INVENTORY'' AND response.I6.score <= 2', 'recommendation', '{"title": "Lập danh sách 10 mặt hàng chậm nhất", "body": "Tuần này: Đi một vòng kệ, đánh dấu những hộp còn nguyên và có bụi. Lập danh sách 10 mặt hàng đó và ngừng nhập thêm.\nCách đo: Tiền vốn nằm trong 10 mặt hàng này, đo lại sau 60 ngày.\nNếu muốn làm tự động: Báo cáo hàng chậm bán theo giá trị tồn.", "priority": 1, "estimateHint": "Tiền vốn nằm trong 10 mặt hàng này, đo lại sau 60 ngày.", "productArea": "inventory"}'::jsonb, 95, TRUE),
    ('373a68e0-4640-5ba8-bcef-8bce6ce770c8', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PINS_PAIN_STAFF_O1', 'V2 PAIN_STAFF O1 — phát hiện theo nỗi đau', 'response.P3.option_code = ''PAIN_STAFF'' AND response.O1.score <= 2', 'insight', '{"title": "Nhà thuốc phụ thuộc vào chủ", "body": "Nhà thuốc phụ thuộc vào anh/chị. Vắng mặt vài ngày là việc bán bị ảnh hưởng.", "severity": "warning", "scopeType": "category", "scopeCode": "OPERATIONS"}'::jsonb, 98, TRUE),
    ('e642df16-cea8-5d6b-a755-4022644201a2', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PREC_PAIN_STAFF_O1', 'V2 PAIN_STAFF O1 — việc nên làm theo nỗi đau', 'response.P3.option_code = ''PAIN_STAFF'' AND response.O1.score <= 2', 'recommendation', '{"title": "Viết 10 câu nhân viên hay hỏi kèm câu trả lời", "body": "Tuần này: Viết ra 10 câu nhân viên hay gọi hỏi anh/chị nhất, kèm câu trả lời. Dán ở quầy.\nCách đo: Số cuộc gọi hỏi việc trong tuần anh/chị vắng mặt.", "priority": 1, "estimateHint": "Số cuộc gọi hỏi việc trong tuần anh/chị vắng mặt."}'::jsonb, 95, TRUE),
    ('7f617cd4-c7ad-5fb8-bee4-79d46ccbea53', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PINS_PAIN_STAFF_O2', 'V2 PAIN_STAFF O2 — phát hiện theo nỗi đau', 'response.P3.option_code = ''PAIN_STAFF'' AND response.O2.score <= 2', 'insight', '{"title": "Nhân viên mới lâu mới tự đứng quầy", "body": "Nhân viên mới mất nhiều thời gian mới tự đứng quầy được.", "severity": "info", "scopeType": "category", "scopeCode": "OPERATIONS"}'::jsonb, 97, TRUE),
    ('25d975d7-f85c-51f6-8605-a1fa69ebeac1', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PREC_PAIN_STAFF_O2', 'V2 PAIN_STAFF O2 — việc nên làm theo nỗi đau', 'response.P3.option_code = ''PAIN_STAFF'' AND response.O2.score <= 2', 'recommendation', '{"title": "Viết danh sách việc tuần đầu cho nhân viên mới", "body": "Tuần này: Viết danh sách những việc nhân viên mới phải biết trong tuần đầu, và ai kèm từng việc.\nCách đo: Số ngày để nhân viên mới tự đứng quầy, ở lần tuyển tiếp theo.", "priority": 1, "estimateHint": "Số ngày để nhân viên mới tự đứng quầy, ở lần tuyển tiếp theo."}'::jsonb, 95, TRUE),
    ('5c8c8c5a-cba9-5b4f-91eb-01b7aec1f710', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PINS_PAIN_STAFF_O3', 'V2 PAIN_STAFF O3 — phát hiện theo nỗi đau', 'response.P3.option_code = ''PAIN_STAFF'' AND response.O3.score <= 2', 'insight', '{"title": "Mỗi nhân viên tư vấn một kiểu", "body": "Mỗi nhân viên tư vấn một kiểu. Khách có thể nhận lời khuyên khác nhau ở cùng một nhà thuốc.", "severity": "info", "scopeType": "category", "scopeCode": "OPERATIONS"}'::jsonb, 97, TRUE),
    ('fbc02405-05f3-523c-a428-ccfcef062f7d', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PREC_PAIN_STAFF_O3', 'V2 PAIN_STAFF O3 — việc nên làm theo nỗi đau', 'response.P3.option_code = ''PAIN_STAFF'' AND response.O3.score <= 2', 'recommendation', '{"title": "Thống nhất hướng dẫn cho 5 bệnh hay gặp", "body": "Tuần này: Chọn 5 bệnh hay gặp nhất. Mỗi bệnh viết 1 trang: hỏi khách gì, khuyên gì, khi nào khuyên đi khám. Cả nhóm cùng thống nhất.\nCách đo: Số bệnh đã có hướng dẫn chung.", "priority": 1, "estimateHint": "Số bệnh đã có hướng dẫn chung."}'::jsonb, 95, TRUE),
    ('fa62407d-4edb-563a-b87c-8e175054e8d4', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PINS_PAIN_STAFF_O4', 'V2 PAIN_STAFF O4 — phát hiện theo nỗi đau', 'response.P3.option_code = ''PAIN_STAFF'' AND response.O4.score <= 2', 'insight', '{"title": "Tiền cuối ngày hay lệch", "body": "Tiền cuối ngày hay lệch với số đã bán mà không rõ nguyên nhân. Đây có thể là thất thoát.", "severity": "info", "scopeType": "category", "scopeCode": "OPERATIONS"}'::jsonb, 97, TRUE),
    ('cdfcdfbb-71d8-5bff-add4-0654e98b1399', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PREC_PAIN_STAFF_O4', 'V2 PAIN_STAFF O4 — việc nên làm theo nỗi đau', 'response.P3.option_code = ''PAIN_STAFF'' AND response.O4.score <= 2', 'recommendation', '{"title": "Đối chiếu tiền cuối mỗi ca", "body": "Tuần này: Đối chiếu tiền với số bán ngay cuối mỗi ca, không để đến cuối ngày. Ghi phần lệch và tên người trực ca.\nCách đo: Số ngày lệch tiền trong tháng.\nNếu muốn làm tự động: Chốt ca theo từng nhân viên.", "priority": 1, "estimateHint": "Số ngày lệch tiền trong tháng.", "productArea": "ops"}'::jsonb, 95, TRUE),
    ('be1020ea-9807-5e72-87e7-6364cfd6e4df', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PINS_PAIN_NUMBERS_B1', 'V2 PAIN_NUMBERS B1 — phát hiện theo nỗi đau', 'response.P3.option_code = ''PAIN_NUMBERS'' AND response.B1.score <= 2', 'insight', '{"title": "Chưa biết rõ lãi mỗi tháng", "body": "Chưa biết rõ mỗi tháng nhà thuốc lãi bao nhiêu.", "severity": "warning", "scopeType": "category", "scopeCode": "BUSINESS"}'::jsonb, 98, TRUE),
    ('2d4baa94-68d9-52ad-9f6c-b34a32925a91', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PREC_PAIN_NUMBERS_B1', 'V2 PAIN_NUMBERS B1 — việc nên làm theo nỗi đau', 'response.P3.option_code = ''PAIN_NUMBERS'' AND response.B1.score <= 2', 'recommendation', '{"title": "Tính con số lãi tháng đầu tiên", "body": "Tuần này: Cuối tháng này, ghi 4 con số: tiền bán, tiền nhập hàng, tiền thuê, tiền lương và các chi phí khác. Lấy tiền bán trừ ba khoản sau là ra con số lãi đầu tiên.\nCách đo: Có con số lãi của 2 tháng liền để so sánh.\nNếu muốn làm tự động: Báo cáo lãi gộp theo ngày và theo tháng.", "priority": 1, "estimateHint": "Có con số lãi của 2 tháng liền để so sánh.", "productArea": "reports"}'::jsonb, 95, TRUE),
    ('9dd008c3-3ec2-5324-b00f-ef78822bf980', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PINS_PAIN_NUMBERS_B2', 'V2 PAIN_NUMBERS B2 — phát hiện theo nỗi đau', 'response.P3.option_code = ''PAIN_NUMBERS'' AND response.B2.score <= 2', 'insight', '{"title": "Chưa biết nhóm hàng nào lãi nhất", "body": "Chưa biết nhóm hàng nào thật sự mang lại lãi. Bán nhiều chưa chắc đã lãi nhiều.", "severity": "info", "scopeType": "category", "scopeCode": "BUSINESS"}'::jsonb, 97, TRUE),
    ('2a7ee4ce-2a4d-59ef-9cc3-4de4c4a8f702', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PREC_PAIN_NUMBERS_B2', 'V2 PAIN_NUMBERS B2 — việc nên làm theo nỗi đau', 'response.P3.option_code = ''PAIN_NUMBERS'' AND response.B2.score <= 2', 'recommendation', '{"title": "Xếp 20 mặt hàng chạy nhất theo tiền lãi", "body": "Tuần này: Lấy 20 mặt hàng bán chạy nhất, ghi giá nhập và giá bán của từng món. Xếp theo tiền lãi mỗi hộp.\nCách đo: Danh sách 10 mặt hàng lãi nhất để ưu tiên trưng bày và tư vấn.\nNếu muốn làm tự động: Lãi gộp theo mặt hàng và nhóm hàng.", "priority": 1, "estimateHint": "Danh sách 10 mặt hàng lãi nhất để ưu tiên trưng bày và tư vấn.", "productArea": "reports"}'::jsonb, 95, TRUE),
    ('5fad891e-0eb0-59b0-aea3-8163e4867546', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PINS_PAIN_NUMBERS_B3', 'V2 PAIN_NUMBERS B3 — phát hiện theo nỗi đau', 'response.P3.option_code = ''PAIN_NUMBERS'' AND response.B3.score <= 2', 'insight', '{"title": "Khuyến mại chưa biết lời hay lỗ", "body": "Khuyến mại được làm mà chưa biết lời hay lỗ.", "severity": "info", "scopeType": "category", "scopeCode": "BUSINESS"}'::jsonb, 97, TRUE),
    ('bccde7a1-ebc2-5b87-b01a-5eda44dfb9e3', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PREC_PAIN_NUMBERS_B3', 'V2 PAIN_NUMBERS B3 — việc nên làm theo nỗi đau', 'response.P3.option_code = ''PAIN_NUMBERS'' AND response.B3.score <= 2', 'recommendation', '{"title": "Đo lời lỗ cho đợt khuyến mại tới", "body": "Tuần này: Lần khuyến mại tới, ghi số bán của mặt hàng đó 2 tuần trước và 2 tuần trong đợt. Trừ phần đã giảm giá để biết lời hay lỗ.\nCách đo: Đợt khuyến mại tới có lãi thêm hay không.", "priority": 1, "estimateHint": "Đợt khuyến mại tới có lãi thêm hay không."}'::jsonb, 95, TRUE),
    ('a9a676e0-ed4b-544a-bb11-81ee908ea454', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PINS_PAIN_NUMBERS_B4', 'V2 PAIN_NUMBERS B4 — phát hiện theo nỗi đau', 'response.P3.option_code = ''PAIN_NUMBERS'' AND response.B4.score <= 2', 'insight', '{"title": "Chưa có cách tìm chỗ bán chậm", "body": "Khi bán chậm lại, chưa có cách tìm ra chậm ở đâu.", "severity": "info", "scopeType": "category", "scopeCode": "BUSINESS"}'::jsonb, 97, TRUE),
    ('76be1a48-6cda-5992-a24c-8bffae4c86cf', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PREC_PAIN_NUMBERS_B4', 'V2 PAIN_NUMBERS B4 — việc nên làm theo nỗi đau', 'response.P3.option_code = ''PAIN_NUMBERS'' AND response.B4.score <= 2', 'recommendation', '{"title": "Ghi số bán theo 5 nhóm hàng mỗi tuần", "body": "Tuần này: Chia tiền bán theo 5 nhóm hàng chính và ghi theo tuần. Khi bán chậm thì so nhóm nào giảm.\nCách đo: Có số bán theo nhóm hàng của 4 tuần liền.\nNếu muốn làm tự động: Số bán theo nhóm hàng, nhóm khách.", "priority": 1, "estimateHint": "Có số bán theo nhóm hàng của 4 tuần liền.", "productArea": "reports"}'::jsonb, 95, TRUE),
    ('03349500-4843-57ca-8f05-d44c2982a4b0', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PINS_PAIN_COMPLIANCE_O5', 'V2 PAIN_COMPLIANCE O5 — phát hiện theo nỗi đau', 'response.P3.option_code = ''PAIN_COMPLIANCE'' AND response.O5.score <= 2', 'insight', '{"title": "Việc định kỳ chưa được ghi đều", "body": "Các việc định kỳ như ghi nhiệt độ, độ ẩm và sổ sách chưa được làm đều. Đây thường là phần bị nhắc khi có kiểm tra.", "severity": "warning", "scopeType": "category", "scopeCode": "OPERATIONS"}'::jsonb, 98, TRUE),
    ('7ec14172-08eb-5ff7-83be-5ebc69e952e0', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PREC_PAIN_COMPLIANCE_O5', 'V2 PAIN_COMPLIANCE O5 — việc nên làm theo nỗi đau', 'response.P3.option_code = ''PAIN_COMPLIANCE'' AND response.O5.score <= 2', 'recommendation', '{"title": "Giao người và giờ cố định cho sổ định kỳ", "body": "Tuần này: Giao cho một người cụ thể, đặt giờ cố định mỗi ngày, và để sổ ngay cạnh nhiệt kế.\nCách đo: Số ngày trong tháng có ghi đủ.", "priority": 1, "estimateHint": "Số ngày trong tháng có ghi đủ."}'::jsonb, 95, TRUE),
    ('2beabecd-83f9-5f6b-ac03-8647164ba62f', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PINS_PAIN_COMPLIANCE_T4', 'V2 PAIN_COMPLIANCE T4 — phát hiện theo nỗi đau', 'response.P3.option_code = ''PAIN_COMPLIANCE'' AND response.T4.score <= 2', 'insight', '{"title": "Liên thông dữ liệu chưa ổn định", "body": "Việc liên thông dữ liệu bán thuốc chưa ổn định hoặc chưa rõ yêu cầu.", "severity": "info", "scopeType": "category", "scopeCode": "TECH"}'::jsonb, 97, TRUE),
    ('95a01f83-097b-57eb-b49e-546ced5caf43', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PREC_PAIN_COMPLIANCE_T4', 'V2 PAIN_COMPLIANCE T4 — việc nên làm theo nỗi đau', 'response.P3.option_code = ''PAIN_COMPLIANCE'' AND response.T4.score <= 2', 'recommendation', '{"title": "Hỏi rõ nhà thuốc còn thiếu bước liên thông nào", "body": "Tuần này: Hỏi lại cơ quan quản lý dược địa phương hoặc nhà cung cấp phần mềm xem nhà thuốc đang thiếu bước nào.\nCách đo: Nhà thuốc biết rõ mình đã đủ yêu cầu hay chưa.\nNếu muốn làm tự động: Kết nối liên thông từ phần mềm bán hàng (cần xác nhận phạm vi hỗ trợ trước khi ghi vào báo cáo).", "priority": 1, "estimateHint": "Nhà thuốc biết rõ mình đã đủ yêu cầu hay chưa.", "productArea": "tech"}'::jsonb, 95, TRUE),
    ('782bbfd1-ffb6-55f8-beab-45fc0e9ae671', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PINS_PAIN_GROWTH_G1', 'V2 PAIN_GROWTH G1 — phát hiện theo nỗi đau', 'response.P3.option_code = ''PAIN_GROWTH'' AND response.G1.score <= 2', 'insight', '{"title": "Chưa có mục tiêu cho năm tới", "body": "Chưa có mục tiêu cụ thể cho năm tới.", "severity": "info", "scopeType": "category", "scopeCode": "GROWTH"}'::jsonb, 97, TRUE),
    ('8482809a-5bb5-59a7-acfb-c1f701cc6d2d', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PREC_PAIN_GROWTH_G1', 'V2 PAIN_GROWTH G1 — việc nên làm theo nỗi đau', 'response.P3.option_code = ''PAIN_GROWTH'' AND response.G1.score <= 2', 'recommendation', '{"title": "Đặt 1 mục tiêu đo được cho 90 ngày", "body": "Tuần này: Viết 1 mục tiêu đo được cho 90 ngày tới, ví dụ ''giảm hàng hủy còn dưới 1 triệu/tháng''.\nCách đo: Có mục tiêu và được xem lại mỗi tháng.", "priority": 1, "estimateHint": "Có mục tiêu và được xem lại mỗi tháng."}'::jsonb, 95, TRUE),
    ('529fd905-51c6-565a-ae55-41446a06f287', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PINS_PAIN_GROWTH_G2', 'V2 PAIN_GROWTH G2 — phát hiện theo nỗi đau', 'response.P3.option_code = ''PAIN_GROWTH'' AND response.G2.score <= 2', 'insight', '{"title": "Cách làm còn phụ thuộc vào người", "body": "Cách làm hiện nay phụ thuộc vào người, nên khó áp dụng sang cơ sở mới.", "severity": "info", "scopeType": "category", "scopeCode": "GROWTH"}'::jsonb, 97, TRUE),
    ('1e008f9b-b5fe-59c8-a5a4-985eb9a80ae5', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PREC_PAIN_GROWTH_G2', 'V2 PAIN_GROWTH G2 — việc nên làm theo nỗi đau', 'response.P3.option_code = ''PAIN_GROWTH'' AND response.G2.score <= 2', 'recommendation', '{"title": "Viết ra 3 quy trình cốt lõi", "body": "Tuần này: Ghi lại 3 quy trình quan trọng nhất (nhận hàng, bán hàng, chốt ca) thành từng bước.\nCách đo: Số quy trình đã được viết ra.\nNếu muốn làm tự động: Phần mềm dùng chung cho nhiều cơ sở.", "priority": 1, "estimateHint": "Số quy trình đã được viết ra.", "productArea": "multi_branch"}'::jsonb, 95, TRUE),
    ('40b798c1-57c9-591e-ac09-d637d2dd56ed', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PINS_PAIN_GROWTH_O1', 'V2 PAIN_GROWTH O1 — phát hiện theo nỗi đau', 'response.P3.option_code = ''PAIN_GROWTH'' AND response.O1.score <= 2', 'insight', '{"title": "Nhà thuốc phụ thuộc vào chủ", "body": "Nhà thuốc phụ thuộc vào anh/chị. Vắng mặt vài ngày là việc bán bị ảnh hưởng.", "severity": "warning", "scopeType": "category", "scopeCode": "OPERATIONS"}'::jsonb, 98, TRUE),
    ('08911c59-2409-5f2c-a99e-01d8ad2d7815', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PREC_PAIN_GROWTH_O1', 'V2 PAIN_GROWTH O1 — việc nên làm theo nỗi đau', 'response.P3.option_code = ''PAIN_GROWTH'' AND response.O1.score <= 2', 'recommendation', '{"title": "Viết 10 câu nhân viên hay hỏi kèm câu trả lời", "body": "Tuần này: Viết ra 10 câu nhân viên hay gọi hỏi anh/chị nhất, kèm câu trả lời. Dán ở quầy.\nCách đo: Số cuộc gọi hỏi việc trong tuần anh/chị vắng mặt.", "priority": 1, "estimateHint": "Số cuộc gọi hỏi việc trong tuần anh/chị vắng mặt."}'::jsonb, 95, TRUE),
    ('25d13e0b-6b8c-51f9-ac4d-d2ce8947f199', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PINS_PAIN_GROWTH_O2', 'V2 PAIN_GROWTH O2 — phát hiện theo nỗi đau', 'response.P3.option_code = ''PAIN_GROWTH'' AND response.O2.score <= 2', 'insight', '{"title": "Nhân viên mới lâu mới tự đứng quầy", "body": "Nhân viên mới mất nhiều thời gian mới tự đứng quầy được.", "severity": "info", "scopeType": "category", "scopeCode": "OPERATIONS"}'::jsonb, 97, TRUE),
    ('15242c6a-6c16-5054-9bd9-cab592b0b2e5', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_PREC_PAIN_GROWTH_O2', 'V2 PAIN_GROWTH O2 — việc nên làm theo nỗi đau', 'response.P3.option_code = ''PAIN_GROWTH'' AND response.O2.score <= 2', 'recommendation', '{"title": "Viết danh sách việc tuần đầu cho nhân viên mới", "body": "Tuần này: Viết danh sách những việc nhân viên mới phải biết trong tuần đầu, và ai kèm từng việc.\nCách đo: Số ngày để nhân viên mới tự đứng quầy, ở lần tuyển tiếp theo.", "priority": 1, "estimateHint": "Số ngày để nhân viên mới tự đứng quầy, ở lần tuyển tiếp theo."}'::jsonb, 95, TRUE),
    ('94190a8a-8f0f-5639-a10d-a997df8c7732', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_EST_EXPIRED_EXP_1_3M', 'V2 ước tính hàng hết hạn/năm', 'response.I2.option_code = ''EXP_1_3M''', 'insight', '{"title": "Hàng hết hạn: khoảng 25 triệu đồng mỗi năm", "body": "Theo số anh/chị nhập, hàng hết hạn phải hủy hoặc trả lại khoảng 25 triệu đồng mỗi năm. Ước tính từ con số anh/chị nhập, nhân 12 tháng. Chưa trừ phần trả lại được nhà cung cấp.", "severity": "warning", "scopeType": "overall", "scopeCode": "INVENTORY"}'::jsonb, 99, TRUE),
    ('58c23e89-1da2-59a4-b3b4-4963423dda9c', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_EST_EXPIRED_EXP_3_7M', 'V2 ước tính hàng hết hạn/năm', 'response.I2.option_code = ''EXP_3_7M''', 'insight', '{"title": "Hàng hết hạn: khoảng 60 triệu đồng mỗi năm", "body": "Theo số anh/chị nhập, hàng hết hạn phải hủy hoặc trả lại khoảng 60 triệu đồng mỗi năm. Ước tính từ con số anh/chị nhập, nhân 12 tháng. Chưa trừ phần trả lại được nhà cung cấp.", "severity": "warning", "scopeType": "overall", "scopeCode": "INVENTORY"}'::jsonb, 99, TRUE),
    ('48a0496f-cf89-578d-9bc0-6ff597d6db9a', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_EST_EXPIRED_EXP_GT7M', 'V2 ước tính hàng hết hạn/năm', 'response.I2.option_code = ''EXP_GT7M''', 'insight', '{"title": "Hàng hết hạn: khoảng trên 84 triệu đồng mỗi năm", "body": "Theo số anh/chị nhập, hàng hết hạn phải hủy hoặc trả lại khoảng trên 84 triệu đồng mỗi năm. Ước tính từ con số anh/chị nhập, nhân 12 tháng. Chưa trừ phần trả lại được nhà cung cấp.", "severity": "warning", "scopeType": "overall", "scopeCode": "INVENTORY"}'::jsonb, 99, TRUE),
    ('ba0ce339-1ff1-5d99-85e9-e3aa31205197', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_EST_STOCKOUT_OPT1_AOV_LT80', 'V2 ước tính tiền bán bị lọt do hết hàng/năm', 'response.I3.option_code = ''OPT1'' AND response.P5.option_code = ''AOV_LT80''', 'insight', '{"title": "Hết hàng: có thể lọt tới 55 triệu đồng mỗi năm", "body": "Nếu mỗi lần hết hàng là một đơn không bán được, nhà thuốc đang để lọt khoảng 55 triệu đồng tiền bán mỗi năm. Đây là mức trên. Có khách sẽ chờ hoặc đổi sang thuốc khác, nên con số thật thấp hơn.", "severity": "warning", "scopeType": "overall", "scopeCode": "INVENTORY"}'::jsonb, 96, TRUE),
    ('8ae490d4-4111-55b8-b065-74364157dfd4', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_EST_STOCKOUT_OPT1_AOV_80_150', 'V2 ước tính tiền bán bị lọt do hết hàng/năm', 'response.I3.option_code = ''OPT1'' AND response.P5.option_code = ''AOV_80_150''', 'insight', '{"title": "Hết hàng: có thể lọt tới 110 triệu đồng mỗi năm", "body": "Nếu mỗi lần hết hàng là một đơn không bán được, nhà thuốc đang để lọt khoảng 110 triệu đồng tiền bán mỗi năm. Đây là mức trên. Có khách sẽ chờ hoặc đổi sang thuốc khác, nên con số thật thấp hơn.", "severity": "warning", "scopeType": "overall", "scopeCode": "INVENTORY"}'::jsonb, 96, TRUE),
    ('c2c3fb94-fcf5-57a4-8aca-aae0c7877252', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_EST_STOCKOUT_OPT1_AOV_150_300', 'V2 ước tính tiền bán bị lọt do hết hàng/năm', 'response.I3.option_code = ''OPT1'' AND response.P5.option_code = ''AOV_150_300''', 'insight', '{"title": "Hết hàng: có thể lọt tới 210 triệu đồng mỗi năm", "body": "Nếu mỗi lần hết hàng là một đơn không bán được, nhà thuốc đang để lọt khoảng 210 triệu đồng tiền bán mỗi năm. Đây là mức trên. Có khách sẽ chờ hoặc đổi sang thuốc khác, nên con số thật thấp hơn.", "severity": "warning", "scopeType": "overall", "scopeCode": "INVENTORY"}'::jsonb, 96, TRUE),
    ('e645df01-97fb-5e8a-b1f9-b14235ca66da', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_EST_STOCKOUT_OPT1_AOV_GT300', 'V2 ước tính tiền bán bị lọt do hết hàng/năm', 'response.I3.option_code = ''OPT1'' AND response.P5.option_code = ''AOV_GT300''', 'insight', '{"title": "Hết hàng: có thể lọt tới 330 triệu đồng mỗi năm", "body": "Nếu mỗi lần hết hàng là một đơn không bán được, nhà thuốc đang để lọt khoảng 330 triệu đồng tiền bán mỗi năm. Đây là mức trên. Có khách sẽ chờ hoặc đổi sang thuốc khác, nên con số thật thấp hơn.", "severity": "warning", "scopeType": "overall", "scopeCode": "INVENTORY"}'::jsonb, 96, TRUE),
    ('6d5921d4-dc6c-57c0-8f8e-56e5af3c4c8c', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_EST_STOCKOUT_OPT2_AOV_LT80', 'V2 ước tính tiền bán bị lọt do hết hàng/năm', 'response.I3.option_code = ''OPT2'' AND response.P5.option_code = ''AOV_LT80''', 'insight', '{"title": "Hết hàng: có thể lọt tới 30 triệu đồng mỗi năm", "body": "Nếu mỗi lần hết hàng là một đơn không bán được, nhà thuốc đang để lọt khoảng 30 triệu đồng tiền bán mỗi năm. Đây là mức trên. Có khách sẽ chờ hoặc đổi sang thuốc khác, nên con số thật thấp hơn.", "severity": "warning", "scopeType": "overall", "scopeCode": "INVENTORY"}'::jsonb, 96, TRUE),
    ('f1086db9-4f44-5cdf-9d45-fe4c817cf236', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_EST_STOCKOUT_OPT2_AOV_80_150', 'V2 ước tính tiền bán bị lọt do hết hàng/năm', 'response.I3.option_code = ''OPT2'' AND response.P5.option_code = ''AOV_80_150''', 'insight', '{"title": "Hết hàng: có thể lọt tới 60 triệu đồng mỗi năm", "body": "Nếu mỗi lần hết hàng là một đơn không bán được, nhà thuốc đang để lọt khoảng 60 triệu đồng tiền bán mỗi năm. Đây là mức trên. Có khách sẽ chờ hoặc đổi sang thuốc khác, nên con số thật thấp hơn.", "severity": "warning", "scopeType": "overall", "scopeCode": "INVENTORY"}'::jsonb, 96, TRUE),
    ('4424b579-6d5d-5ec7-9212-c75aba1cf896', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_EST_STOCKOUT_OPT2_AOV_150_300', 'V2 ước tính tiền bán bị lọt do hết hàng/năm', 'response.I3.option_code = ''OPT2'' AND response.P5.option_code = ''AOV_150_300''', 'insight', '{"title": "Hết hàng: có thể lọt tới 115 triệu đồng mỗi năm", "body": "Nếu mỗi lần hết hàng là một đơn không bán được, nhà thuốc đang để lọt khoảng 115 triệu đồng tiền bán mỗi năm. Đây là mức trên. Có khách sẽ chờ hoặc đổi sang thuốc khác, nên con số thật thấp hơn.", "severity": "warning", "scopeType": "overall", "scopeCode": "INVENTORY"}'::jsonb, 96, TRUE),
    ('3c91761f-6704-5a70-abca-142ce80299bb', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_EST_STOCKOUT_OPT2_AOV_GT300', 'V2 ước tính tiền bán bị lọt do hết hàng/năm', 'response.I3.option_code = ''OPT2'' AND response.P5.option_code = ''AOV_GT300''', 'insight', '{"title": "Hết hàng: có thể lọt tới 180 triệu đồng mỗi năm", "body": "Nếu mỗi lần hết hàng là một đơn không bán được, nhà thuốc đang để lọt khoảng 180 triệu đồng tiền bán mỗi năm. Đây là mức trên. Có khách sẽ chờ hoặc đổi sang thuốc khác, nên con số thật thấp hơn.", "severity": "warning", "scopeType": "overall", "scopeCode": "INVENTORY"}'::jsonb, 96, TRUE),
    ('047e4b5a-1487-5c76-889d-b982c189797b', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_EST_STOCKOUT_OPT3_AOV_LT80', 'V2 ước tính tiền bán bị lọt do hết hàng/năm', 'response.I3.option_code = ''OPT3'' AND response.P5.option_code = ''AOV_LT80''', 'insight', '{"title": "Hết hàng: có thể lọt tới 11 triệu đồng mỗi năm", "body": "Nếu mỗi lần hết hàng là một đơn không bán được, nhà thuốc đang để lọt khoảng 11 triệu đồng tiền bán mỗi năm. Đây là mức trên. Có khách sẽ chờ hoặc đổi sang thuốc khác, nên con số thật thấp hơn.", "severity": "warning", "scopeType": "overall", "scopeCode": "INVENTORY"}'::jsonb, 96, TRUE),
    ('02a1edf6-7e90-5e67-a381-39219452274b', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_EST_STOCKOUT_OPT3_AOV_80_150', 'V2 ước tính tiền bán bị lọt do hết hàng/năm', 'response.I3.option_code = ''OPT3'' AND response.P5.option_code = ''AOV_80_150''', 'insight', '{"title": "Hết hàng: có thể lọt tới 20 triệu đồng mỗi năm", "body": "Nếu mỗi lần hết hàng là một đơn không bán được, nhà thuốc đang để lọt khoảng 20 triệu đồng tiền bán mỗi năm. Đây là mức trên. Có khách sẽ chờ hoặc đổi sang thuốc khác, nên con số thật thấp hơn.", "severity": "warning", "scopeType": "overall", "scopeCode": "INVENTORY"}'::jsonb, 96, TRUE),
    ('5bb308de-9e20-5c9a-b9a8-e4eb2f4e81a6', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_EST_STOCKOUT_OPT3_AOV_150_300', 'V2 ước tính tiền bán bị lọt do hết hàng/năm', 'response.I3.option_code = ''OPT3'' AND response.P5.option_code = ''AOV_150_300''', 'insight', '{"title": "Hết hàng: có thể lọt tới 40 triệu đồng mỗi năm", "body": "Nếu mỗi lần hết hàng là một đơn không bán được, nhà thuốc đang để lọt khoảng 40 triệu đồng tiền bán mỗi năm. Đây là mức trên. Có khách sẽ chờ hoặc đổi sang thuốc khác, nên con số thật thấp hơn.", "severity": "warning", "scopeType": "overall", "scopeCode": "INVENTORY"}'::jsonb, 96, TRUE),
    ('516b1419-8a9d-50fd-8490-44f326163af6', '38c527c2-8c96-58d7-8699-b893ac606c78', 'V2_EST_STOCKOUT_OPT3_AOV_GT300', 'V2 ước tính tiền bán bị lọt do hết hàng/năm', 'response.I3.option_code = ''OPT3'' AND response.P5.option_code = ''AOV_GT300''', 'insight', '{"title": "Hết hàng: có thể lọt tới 65 triệu đồng mỗi năm", "body": "Nếu mỗi lần hết hàng là một đơn không bán được, nhà thuốc đang để lọt khoảng 65 triệu đồng tiền bán mỗi năm. Đây là mức trên. Có khách sẽ chờ hoặc đổi sang thuốc khác, nên con số thật thấp hơn.", "severity": "warning", "scopeType": "overall", "scopeCode": "INVENTORY"}'::jsonb, 96, TRUE)
ON CONFLICT (template_id, code) DO UPDATE SET name = EXCLUDED.name, expression = EXCLUDED.expression,
    action_type = EXCLUDED.action_type, action_payload = EXCLUDED.action_payload,
    priority = EXCLUDED.priority, is_active = EXCLUDED.is_active;

-- Mức trưởng thành: dùng lại thang của V1 (theo điểm tổng 1–4, không phụ thuộc câu hỏi)
INSERT INTO assessment_maturity_level (template_id, vertical_code, level, code, name, description, score_min, score_max, sort_order, is_active)
SELECT '38c527c2-8c96-58d7-8699-b893ac606c78', vertical_code, level, code, name, description, score_min, score_max, sort_order, is_active
FROM assessment_maturity_level WHERE template_id = 'a0000001-0000-4000-8000-000000000001'
ON CONFLICT (template_id, vertical_code, level) DO NOTHING;

-- Nguyên nhân gốc V2: bản nháp, chờ dược sĩ duyệt (xem PHARMACY_V2.review.md)
INSERT INTO assessment_root_cause_kb (
    id, template_id, question_code, category_code, trigger_expression,
    cause_code, cause_title, cause_body, evidence_hint, sort_order
) VALUES
    ('2181de28-0cf8-5a6d-9ec9-9cc685fc8dc3', '38c527c2-8c96-58d7-8699-b893ac606c78', 'C3', 'CUSTOMER', 'response.C3.score <= 2', 'RC_V2_C3_REFILL', 'Khách thuốc dài ngày không được nhắc', 'Khách dùng thuốc lâu dài không được nhắc khi sắp hết thuốc. Đây là nhóm khách dễ chuyển sang chỗ khác nhất.', 'Câu C3: nhắc khách dùng thuốc lâu dài', 10),
    ('eaca42f5-ff4a-55db-94bc-e78237633cf5', '38c527c2-8c96-58d7-8699-b893ac606c78', 'I1', 'INVENTORY', 'response.I1.score <= 2', 'RC_V2_I1_EXPIRY', 'Hàng cận hạn được phát hiện muộn', 'Hàng sắp hết hạn được phát hiện tình cờ hoặc khi đã quá muộn để xử lý.', 'Câu I1: theo dõi hàng cận hạn', 20),
    ('f75fab20-ae50-51e8-9d7a-1ab145a8d2f7', '38c527c2-8c96-58d7-8699-b893ac606c78', 'I3', 'INVENTORY', 'response.I3.score <= 2', 'RC_V2_I3_STOCKOUT', 'Hết hàng khi khách hỏi mua', 'Khách hỏi mua mà nhà thuốc hết hàng khá thường xuyên. Mỗi lần như vậy là một đơn có thể mất, và khách có thể không quay lại.', 'Câu I3: hết hàng lúc khách hỏi', 30),
    ('f976dfb2-04a9-5901-bfbc-2d219aeedd59', '38c527c2-8c96-58d7-8699-b893ac606c78', 'B1', 'BUSINESS', 'response.B1.score <= 2', 'RC_V2_B1_PROFIT', 'Chưa biết rõ lãi mỗi tháng', 'Chưa biết rõ mỗi tháng nhà thuốc lãi bao nhiêu.', 'Câu B1: lãi mỗi tháng', 40),
    ('a92a9c97-7576-5ad8-be2d-16edcf132115', '38c527c2-8c96-58d7-8699-b893ac606c78', 'O1', 'OPERATIONS', 'response.O1.score <= 2', 'RC_V2_O1_OWNER', 'Nhà thuốc phụ thuộc vào chủ', 'Nhà thuốc phụ thuộc vào anh/chị. Vắng mặt vài ngày là việc bán bị ảnh hưởng.', 'Câu O1: nhà thuốc khi chủ vắng', 50),
    ('67268b54-12b7-51a7-8de0-c1e5960e73ba', '38c527c2-8c96-58d7-8699-b893ac606c78', 'O5', 'OPERATIONS', 'response.O5.score <= 2', 'RC_V2_O5_LOG', 'Việc định kỳ chưa được ghi đều', 'Các việc định kỳ như ghi nhiệt độ, độ ẩm và sổ sách chưa được làm đều. Đây thường là phần bị nhắc khi có kiểm tra.', 'Câu O5: sổ định kỳ', 60),
    ('f2f83579-0ce1-55cd-99f1-44a8dfd0e10a', '38c527c2-8c96-58d7-8699-b893ac606c78', 'I4', 'INVENTORY', 'response.I4.score <= 2', 'RC_V2_I4_ORDER', 'Đặt hàng theo cảm giác', 'Số lượng đặt hàng dựa vào cảm giác hoặc gợi ý của trình dược viên, nên dễ vừa thiếu hàng bán chạy vừa dư hàng bán chậm.', 'Câu I4: cách đặt hàng', 70),
    ('ed9eec4c-1f6c-5e4f-b4bf-34aaef3701db', '38c527c2-8c96-58d7-8699-b893ac606c78', 'C2', 'CUSTOMER', 'response.C2.score <= 2', 'RC_V2_C2_PROFILE', 'Khách quen không có hồ sơ', 'Khách quen quay lại mà nhân viên không biết lần trước họ mua gì, nên khó tư vấn tiếp và khó bán thêm đúng nhu cầu.', 'Câu C2: hồ sơ khách quen', 80)
ON CONFLICT (template_id, cause_code) DO UPDATE SET
    question_code = EXCLUDED.question_code,
    category_code = EXCLUDED.category_code,
    trigger_expression = EXCLUDED.trigger_expression,
    cause_title = EXCLUDED.cause_title,
    cause_body = EXCLUDED.cause_body,
    evidence_hint = EXCLUDED.evidence_hint,
    sort_order = EXCLUDED.sort_order;

COMMIT;
