-- KitPlatform 168: People P0–P3 MVP — pulse gắn bó + quiz tình huống thêm
-- engagement_pulse: NV tự-score 1–5 khi phản hồi chấm tháng

ALTER TABLE pack_learning.evaluation
    ADD COLUMN IF NOT EXISTS engagement_pulse INT;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_learning_eval_engagement_pulse'
    ) THEN
        ALTER TABLE pack_learning.evaluation
            ADD CONSTRAINT ck_learning_eval_engagement_pulse
            CHECK (engagement_pulse IS NULL OR engagement_pulse BETWEEN 1 AND 5);
    END IF;
END $$;

-- Thêm 1 câu tình huống / level L1–L4 (không đụng câu cũ)
INSERT INTO pack_learning.quiz_question (module_id, sort_order, prompt, options_json, correct_option_index)
SELECT v.module_id, v.sort_order, v.prompt, v.options_json::jsonb, v.correct
FROM (VALUES
    (
        '22222222-2222-2222-2222-222222222212'::uuid, 10,
        'Khách đông, một người hỏi liều kháng sinh phức tạp — bạn làm gì trước?',
        '["Bán ngay theo kinh nghiệm để khỏi mất khách","Mời dược sĩ phụ trách; giữ thái độ chờ đợi lịch sự","Bảo khách tự tra mạng"]',
        1
    ),
    (
        '22222222-2222-2222-2222-222222222213'::uuid, 10,
        'Muốn tăng giá trị đơn đúng mực, cách nào phù hợp GPP?',
        '["Ép mua thêm kháng sinh","Gợi ý sản phẩm liên quan nhu cầu, trong quyền / sau DS nếu cần","Hứa ưu đãi ngoài chương trình"]',
        1
    ),
    (
        '22222222-2222-2222-2222-222222222214'::uuid, 10,
        'Giữa ca hết mặt hàng bán chạy — việc đúng là?',
        '["Im đến hết ca","Báo quản lý / ghi đề xuất sớm","Tự sửa tồn cho còn hàng"]',
        1
    ),
    (
        '22222222-2222-2222-2222-222222222215'::uuid, 10,
        'Cuối ca chưa đối quỹ đã về — đúng/sai?',
        '["Đúng nếu ca sau làm hộ","Sai — phải đóng ca / đối quỹ hoặc bàn giao rõ","Đúng nếu không có tiền mặt"]',
        1
    )
) AS v(module_id, sort_order, prompt, options_json, correct)
WHERE NOT EXISTS (
    SELECT 1 FROM pack_learning.quiz_question q
    WHERE q.module_id = v.module_id AND q.sort_order = v.sort_order
);
