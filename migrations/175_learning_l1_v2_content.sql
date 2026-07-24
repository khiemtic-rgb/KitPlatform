-- KitPlatform 175: L1 nội dung chuẩn (rút gọn đề xuất) + quiz 8 câu + quan sát L1

UPDATE pack_learning.module
SET
    title = 'L1 — Tiếp đón khách & bán hàng trên POS',
    summary = 'Chào → nhu cầu → tư vấn → POS/FEFO → thanh toán → hướng dẫn → cảm ơn. ~15 phút · quiz ≥80% · quan sát tại quầy (soft).',
    body_markdown = $md$## Mục tiêu

Sau bài này bạn có thể:
- Tiếp đón chuyên nghiệp, xác định đúng nhu cầu.
- Tư vấn trong quyền; chuyển dược sĩ khi vượt phạm vi.
- Tạo hóa đơn POS, bán đúng thuốc, tuân thủ FEFO.
- Thanh toán chính xác, hướng dẫn dùng, kết thúc đúng chuẩn.

## Vì sao quan trọng?

Khách hình thành ấn tượng trong **10–20 giây đầu**. Trải nghiệm tốt → tin tưởng, quay lại, ít sai sót, doanh thu bền vững.
Bạn không chỉ bán thuốc — bạn đang chăm sóc sức khỏe khách hàng.

## Quy trình chuẩn

Khách đến → **Chào** → **Lắng nghe** → **Khai thác nhu cầu** → **Tư vấn** → **Bán POS** → **Thanh toán** → **Hướng dẫn dùng** → **Cảm ơn** → (chăm sóc sau bán nếu phù hợp)

### Bước 1 — Tiếp đón
Mỉm cười, giao tiếp bằng mắt, chào: «Em chào anh/chị — em hỗ trợ gì ạ?»
Đang bận: «Anh/chị vui lòng chờ em khoảng 1 phút nhé.»
Không: không nhìn khách · dùng điện thoại · để chờ không phản hồi.

### Bước 2 — Lắng nghe & nhu cầu
Hỏi trước khi bán: gặp vấn đề gì? từ khi nào? sốt? đã uống gì? dị ứng? đang điều trị bệnh khác?
Lắng nghe nhiều hơn nói. Vượt phạm vi → **mời dược sĩ phụ trách**.

### Bước 3 — Tư vấn
Đúng chuyên môn · dễ hiểu · trung thực · không ép mua.
Giải thích ngắn: công dụng · liều · thời gian dùng · lưu ý · khi nào nên quay lại.

### Bước 4 — Tạo hóa đơn POS
1. Tìm / tạo khách (nếu khách đồng ý).
2. Quét barcode hoặc tìm tên.
3. Kiểm tra: đúng thuốc · hàm lượng · số lượng · đơn giá.
4. **FEFO:** ưu tiên lô hạn gần hơn còn chất lượng — Novixa gợi ý lô; không bỏ qua cảnh báo nếu không có lý do chính đáng.

### Bước 5 — Thanh toán
Trước khi thu: đúng thuốc · SL · khách · giá · điểm · KM (trong quyền) → thu tiền → in / đưa bill.

### Bước 6 — Hướng dẫn sử dụng
Không chỉ đưa thuốc. Ví dụ: uống sau ăn · ngày 2 lần · sau 3 ngày không đỡ thì quay lại / đi khám.

### Bước 7 — Kết thúc
Cảm ơn, chào tạm biệt. Có thể nhắc thuốc / tái khám / khách thân thiết khi phù hợp.

## Lỗi thường gặp

Không chào · không hỏi nhu cầu · sai thuốc/SL · bỏ FEFO · quên gắn khách · không hướng dẫn · không cảm ơn.

## Checklist nhớ

**Trước:** chào · lắng nghe · xác định nhu cầu.
**Trong:** tìm khách · đúng thuốc · FEFO · đúng SL · thanh toán.
**Sau:** hướng dẫn dùng · cảm ơn · lưu khách khi phù hợp.

## Tình huống

1. Đang bận phục vụ người khác → **chào và xin chờ ít phút** (không im lặng).
2. POS gợi ý lô gần hạn → **chọn FEFO** nếu còn chất lượng.
3. Khách từ chối SĐT → **giải thích lợi ích, tôn trọng quyết định**.
4. Không chắc thuốc phù hợp → **xin hỗ trợ dược sĩ**.

## Thực hành trên ca

Gộp thành một nhiệm vụ: đăng nhập POS → tìm/tạo khách → bán ≥3 SP (kiểm FEFO) → thanh toán → hướng dẫn ngắn → cảm ơn.
Không cần 7 bài lab riêng trên app.

## Quan sát tại quầy (soft)

Quản lý quan sát bạn phục vụ khách thật (gợi ý ≥3 khách). Tick tiêu chí L1; đạt **≥ 80%** → «Đã áp dụng tại quầy».
Không khóa bán · không chặn học L2.

## Sau bài này

Đọc → (ký nếu có) → quiz ≥80% → áp dụng trên ca → QL quan sát (soft) → tiếp tục **L2**.
$md$,
    duration_minutes = 15,
    pass_score_pct = 80,
    require_ack = TRUE,
    require_observation = TRUE,
    level_code = 'L1',
    updated_at = NOW()
WHERE id = '22222222-2222-2222-2222-222222222212'
   OR (program_id = '22222222-2222-2222-2222-222222222201' AND code = 'l1_pos_basic');

DELETE FROM pack_learning.quiz_question
WHERE module_id = '22222222-2222-2222-2222-222222222212';

INSERT INTO pack_learning.quiz_question (module_id, sort_order, prompt, options_json, correct_option_index)
VALUES
(
    '22222222-2222-2222-2222-222222222212', 1,
    'Bước đầu tiên khi khách vào nhà thuốc?',
    '["In bill trước","Chào hỏi và sẵn sàng hỗ trợ","Nhờ khách tự lấy hàng"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222212', 2,
    'Vì sao cần hỏi nhu cầu trước khi tư vấn / bán?',
    '["Để kéo dài thời gian","Để hiểu đúng nhu cầu, tránh bán sai","Để ép mua thêm"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222212', 3,
    'FEFO nghĩa là gì?',
    '["Bán lô mới nhất trước","Ưu tiên bán lô hết hạn sớm hơn (còn chất lượng)","Chỉ kiểm khi kiểm kê tháng"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222212', 4,
    'Đang bận phục vụ khách khác — khách mới vào. Bạn nên?',
    '["Im lặng để khách tự chờ","Chào và xin phép chờ ít phút","Bảo khách ra ngoài đợi"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222212', 5,
    'POS gợi ý lô gần hết hạn hơn — bạn nên?',
    '["Chọn lô mới hơn cho đẹp","Chọn theo FEFO / gợi ý nếu còn chất lượng","Tắt cảnh báo rồi bán"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222212', 6,
    'Khách từ chối cung cấp số điện thoại — bạn nên?',
    '["Bắt buộc mới bán","Giải thích lợi ích và tôn trọng quyết định","Từ chối bán"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222212', 7,
    'Không chắc thuốc khách yêu cầu có phù hợp — bạn nên?',
    '["Tự quyết cho nhanh","Xin hỗ trợ dược sĩ phụ trách","Bán theo đơn cũ của người khác"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222212', 8,
    'Sau khi bán, việc nào là đúng chuẩn?',
    '["Đưa thuốc rồi im lặng","Hướng dẫn dùng ngắn + cảm ơn / chào tạm biệt","Chỉ in bill là đủ"]'::jsonb,
    1
);
