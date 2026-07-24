-- KitPlatform 170: Bài Nội quy nhà thuốc — NV đọc trên app thay tờ giấy
-- Module riêng (không đè L0 welcome). Chủ sửa nội dung qua SOP tenant override.

INSERT INTO pack_learning.module (
    id, program_id, code, title, summary, body_markdown, duration_minutes,
    level_code, competency_codes, sort_order, pass_score_pct, require_ack
)
VALUES (
    '22222222-2222-2222-2222-222222222210',
    '22222222-2222-2222-2222-222222222201',
    'l0_noi_quy',
    'Nội quy nhà thuốc',
    'Đọc nội quy làm việc tại quầy — thay tờ giấy treo tường. Chủ sửa theo SOP nhà thuốc.',
    E'## Mục tiêu
Bạn biết và cam kết tuân thủ nội quy nhà thuốc khi làm việc tại quầy.

## Lưu ý cho quản lý
Đây là **mẫu khung**. Vào Phát triển Nhân sự → bài này → **Sửa SOP** để dán nội quy thật của nhà thuốc (giờ làm, đồng phục, bảo mật, xử lý tiền…).

## 1. Giờ làm & chuyên cần
- Có mặt đúng giờ theo lịch được giao.
- Xin phép khi đi muộn / về sớm / nghỉ — theo quy định nhà thuốc.
- Không tự ý đổi ca nếu chưa được quản lý xác nhận.

## 2. Thái độ & phục vụ khách
- Chào đón lịch sự, lắng nghe nhu cầu trước khi bán.
- Không tranh cãi với khách tại quầy; escalate cho quản lý / dược sĩ khi cần.
- Giữ khu vực quầy gọn, sạch trong ca.

## 3. Bảo mật & tài khoản
- Không cho người khác dùng tài khoản Novixa của mình.
- Không chia sẻ mật khẩu / OTP / dữ liệu khách ra ngoài.
- Khóa máy khi rời quầy.

## 4. Tiền hàng & tài sản
- Không tự ý lấy hàng / tiền quỹ ngoài quy trình.
- Báo ngay khi lệch quỹ, mất mát hoặc sự cố.
- Đóng ca / checklist cuối ca trước khi về (nếu được giao).

## 5. An toàn & tuân thủ
- Không tư vấn vượt quyền — thuốc kê đơn / liều phức tạp chuyển dược sĩ.
- Tuân thủ FEFO / bảo quản khi được giao việc kho.
- Khi không chắc: hỏi trước, không đoán.

## Cam kết
Sau khi đọc xong, xác nhận đã hiểu và làm quiz ngắn. Nội quy có thể được cập nhật — hãy đọc lại khi quản lý thông báo.',
    5,
    'L0',
    ARRAY['store_policy'],
    5,
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

-- Quiz nhẹ (không xóa câu khác; chỉ thêm nếu chưa có)
INSERT INTO pack_learning.quiz_question (module_id, sort_order, prompt, options_json, correct_option_index)
SELECT v.module_id, v.sort_order, v.prompt, v.options_json::jsonb, v.correct
FROM (VALUES
    (
        '22222222-2222-2222-2222-222222222210'::uuid, 1,
        'Khi không chắc việc có đúng nội quy không, bạn nên?',
        '["Tự quyết cho nhanh","Hỏi quản lý / dược sĩ trước","Làm theo thói quen cá nhân"]',
        1
    ),
    (
        '22222222-2222-2222-2222-222222222210'::uuid, 2,
        'Về tài khoản Novixa, điều nào đúng theo nội quy mẫu?',
        '["Cho đồng nghiệp mượn khi bận","Không cho người khác dùng tài khoản của mình","Ghi mật khẩu ra giấy dán quầy"]',
        1
    ),
    (
        '22222222-2222-2222-2222-222222222210'::uuid, 3,
        'Phát hiện lệch quỹ hoặc mất mát, bạn?',
        '["Im đến hết tháng","Báo ngay theo quy trình nhà thuốc","Tự bù rồi không ghi"]',
        1
    )
) AS v(module_id, sort_order, prompt, options_json, correct)
WHERE NOT EXISTS (
    SELECT 1 FROM pack_learning.quiz_question q
    WHERE q.module_id = v.module_id AND q.sort_order = v.sort_order
);

-- Backfill progress cho enrollment đang mở (để NV thấy bài mới trong lộ trình)
INSERT INTO pack_learning.module_progress (enrollment_id, module_id, status)
SELECT e.id, '22222222-2222-2222-2222-222222222210'::uuid, 'not_started'
FROM pack_learning.enrollment e
WHERE e.program_id = '22222222-2222-2222-2222-222222222201'
  AND e.status <> 'cancelled'
  AND NOT EXISTS (
      SELECT 1 FROM pack_learning.module_progress mp
      WHERE mp.enrollment_id = e.id
        AND mp.module_id = '22222222-2222-2222-2222-222222222210'
  );
