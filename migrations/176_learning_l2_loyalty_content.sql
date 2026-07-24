-- KitPlatform 176: L2 — Chăm sóc khách & khách thân thiết (linh hồn giữ khách)

UPDATE pack_learning.module
SET
    title = 'L2 — Chăm sóc khách hàng & Xây dựng khách thân thiết',
    summary = 'CRM · lịch sử mua · điểm · nhắc thuốc · gợi ý đúng mực · giữ khách quay lại. ~15 phút · quiz ≥80% · quan sát soft.',
    body_markdown = $md$## Mục tiêu

Sau bài này bạn có thể:
- Hiểu vai trò chăm sóc khách và dùng CRM trên Novixa.
- Tra cứu / cập nhật khách, xem lịch sử mua hàng.
- Giới thiệu điểm thưởng đúng chính sách (không ép).
- Đặt lịch nhắc thuốc khi phù hợp.
- Gợi ý sản phẩm đúng mực; giữ khách quay lại.

## Vì sao đây là «linh hồn» Novixa?

L1 dạy **bán đúng**. L2 dạy **giữ khách quay lại**.
Khách mới tốn công sức; khách hài lòng quay lại nhiều lần và giới thiệu thêm người thân.
Mục tiêu không chỉ một đơn — mà **niềm tin** để khách trở lại khi có nhu cầu.

## CRM là gì?

**CRM** (chăm sóc quan hệ khách) giúp nhà thuốc: lưu thông tin · lịch sử mua · theo dõi liệu trình · chăm sóc sau bán · xây khách thân thiết.
CRM không phải «quản lý dữ liệu cho vui» — CRM để **chăm sóc tốt hơn**.

Dùng sau mỗi giao dịch, khi khách quay lại, khi khách hỏi: «Lần trước mua thuốc gì?» · «Còn bao nhiêu điểm?» · «Tôi đã mua ở đây chưa?»

## Quy trình chăm sóc (tóm tắt)

### 1. Tra cứu khách
POS → tìm theo SĐT / tên / mã. Chưa có → tạo mới (khi khách đồng ý).

### 2. Cập nhật thông tin
Họ tên · SĐT · ngày sinh · giới tính · địa chỉ (nếu cần) · ghi chú.
Thông tin đủ → chăm sóc tốt hơn. **Sai SĐT = mất khách.**

### 3. Xem lịch sử mua
Biết từng mua gì / khi nào / bao lần — không đoán khi khách quên tên thuốc.

### 4. Điểm thưởng
Giải thích ngắn đúng chính sách nhà thuốc. Không ép tham gia · không hứa ngoài chương trình.

### 5. Đặt lịch nhắc thuốc
Với khách điều trị dài ngày / liệu trình / mua định kỳ (VD huyết áp hàng tháng). Novixa nhắc đúng lịch đã đặt.

### 6. Chăm sóc sau bán
Quan tâm, không làm phiền: hỏi đỡ hơn chưa / có vấn đề gì không — theo kênh nhà thuốc cho phép.

### 7. Gợi ý sản phẩm phù hợp
Chỉ khi thật sự cần (VD cảm → nước muối / khẩu trang / vitamin C nếu phù hợp). Không ép · không gợi ý vượt quyền / kê đơn theo cảm tính.

### 8. Khách thân thiết
Gọi đúng tên · nhớ lịch sử · chủ động hỗ trợ — đó là khác biệt của nhà thuốc.

## Lỗi thường gặp

Không lưu khách · sai SĐT · không xem lịch sử · quên điểm · quên nhắc thuốc · ép bán thêm · chỉ quan tâm chốt đơn.

## Checklist nhớ

**Trước / đầu đơn:** tìm khách · xem lịch sử nếu cần.
**Trong:** cập nhật CRM · tích điểm · giới thiệu chương trình nếu phù hợp.
**Sau:** nhắc thuốc khi cần · ghi chú · cảm ơn.

## Tình huống

1. Khách quên tên thuốc lần trước → **tra cứu lịch sử**, không đoán.
2. Không muốn tích điểm → **tôn trọng quyết định**.
3. Thuốc huyết áp hàng tháng → **đặt lịch nhắc**.
4. Mua thuốc cảm → **chỉ gợi ý sản phẩm thật sự cần**, không ép nhiều món.

## Thực hành trên ca (một nhiệm vụ)

Trong ca hôm nay: tra cứu/tạo ≥3 khách · gắn đơn + điểm khi đồng ý · xem lịch sử ít nhất 1 lần · đặt ≥1 lịch nhắc (nếu có khách liệu trình) · gợi ý đúng mực khi phù hợp.

## Quan sát tại quầy (soft)

QL quan sát chăm sóc khách thật. Rubric L2 đạt **≥ 80%** → «Đã áp dụng tại quầy». Không khóa bán · không chặn L3.

## Gợi ý tương lai (AI Coach — chưa bật)

Sau mỗi ca, hệ thống có thể tóm tắt: % khách đã lưu · số lịch nhắc · điểm yếu (chưa lưu / quên nhắc) → gợi ý cải thiện. Học → làm → phản hồi → cải thiện.

## Sau bài này

Đọc → quiz ≥80% → áp dụng trên ca → QL quan sát (soft) → tiếp tục **L3 — Hàng hóa & an toàn thuốc**.
$md$,
    duration_minutes = 15,
    pass_score_pct = 80,
    require_ack = TRUE,
    require_observation = TRUE,
    level_code = 'L2',
    updated_at = NOW()
WHERE id = '22222222-2222-2222-2222-222222222213'
   OR (program_id = '22222222-2222-2222-2222-222222222201' AND code = 'l2_customer_care');

DELETE FROM pack_learning.quiz_question
WHERE module_id = '22222222-2222-2222-2222-222222222213';

INSERT INTO pack_learning.quiz_question (module_id, sort_order, prompt, options_json, correct_option_index)
VALUES
(
    '22222222-2222-2222-2222-222222222213', 1,
    'CRM trên nhà thuốc giúp điều gì trước hết?',
    '["Chỉ lưu dữ liệu cho báo cáo","Chăm sóc khách tốt hơn nhờ thông tin & lịch sử mua","Thay thế tư vấn dược sĩ"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222213', 2,
    'Khách quên tên thuốc lần trước — bạn nên?',
    '["Đoán theo cảm giác","Tra cứu lịch sử mua hàng trên hệ thống","Bán thuốc đắt nhất"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222213', 3,
    'Khách không muốn tham gia tích điểm — bạn nên?',
    '["Ép khách mới bán","Tôn trọng quyết định, giải thích ngắn nếu cần","Từ chối bán"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222213', 4,
    'Khách mua thuốc huyết áp hàng tháng — việc nào phù hợp?',
    '["Không làm gì thêm","Đặt lịch nhắc thuốc / mua định kỳ","Ép mua thêm nhiều vitamin ngay"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222213', 5,
    'Khi nào nên gợi ý thêm sản phẩm?',
    '["Luôn ép mua thêm cho tăng doanh thu","Chỉ khi thật sự liên quan nhu cầu, không vượt quyền","Chỉ khi quản lý đứng cạnh"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222213', 6,
    'Vì sao cần lưu số điện thoại đúng?',
    '["Để gọi mời mua mọi lúc","Để tìm lại khách, điểm, lịch sử, nhắc thuốc khi khách đồng ý","Để bán SĐT cho bên thứ ba"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222213', 7,
    'Mục tiêu lớn nhất của chăm sóc khách (L2) là gì?',
    '["Chỉ tăng giá trị đơn hôm nay","Xây niềm tin để khách quay lại bền vững","Hoàn thành checklist cho quản lý"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222213', 8,
    'Sau khi bán, việc nào đúng chuẩn CSKH?',
    '["Đưa thuốc rồi thôi","Cập nhật CRM / điểm / nhắc thuốc khi cần + cảm ơn","Hứa ưu đãi ngoài chính sách để giữ khách"]'::jsonb,
    1
);
