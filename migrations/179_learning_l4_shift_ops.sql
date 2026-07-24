-- KitPlatform 179: L4 — Vận hành ca làm việc (đầu · giữa · cuối · bàn giao)

UPDATE pack_learning.module
SET
    title = 'L4 — Vận hành ca làm việc',
    summary = 'Nhận ca · checklist đầu/giữa/cuối · xử lý phát sinh · bàn giao · đăng xuất. ~20 phút · quiz ≥80% · quan sát soft.',
    body_markdown = $md$## Mục tiêu

Sau bài này bạn có thể:
- Chuẩn bị và nhận ca đúng quy trình.
- Làm đủ Checklist đầu ca / giữa ca / cuối ca trên Novixa.
- Quản lý việc trong ca, xử lý phát sinh đúng thẩm quyền.
- Bàn giao đầy đủ; kết ca an toàn, liên tục, đúng quy trình.

## Vì sao quan trọng?

Một ca không chỉ là bán hàng — còn chuẩn bị quầy, thiết bị, hàng hóa, việc phát sinh, bàn giao ca sau.
Làm đúng checklist → vận hành ổn định, ít quên việc, phục vụ nhất quán.

## Quy trình một ca

Nhận ca → Chuẩn bị → Phục vụ khách → Kiểm tra định kỳ → Bàn giao → Kết thúc ca.

## Chương 1 — Nhận ca

Đúng giờ · đăng nhập Novixa · đọc thông báo · nhận nhiệm vụ · mở Checklist đầu ca · trao đổi nhanh với ca trước (nếu có).

## Chương 2 — Checklist đầu ca

Sẵn sàng trước khi bán: đồng phục · bảng tên · POS · máy quét · máy in · mạng · tiền lẻ · quầy sạch · tủ gọn · nhiệt độ bảo quản (nếu áp dụng).
Bất thường → **báo quản lý**. Mở ca POS / quỹ đầu theo nội quy nhà thuốc.

## Chương 3 — Trong ca

Ngoài phục vụ (L1–L2): theo dõi Dashboard/Hub · nhiệm vụ · thông báo · checklist định kỳ · CRM · cận hạn (L3).
Không đợi quản lý nhắc mới làm.

## Chương 4 — Việc phát sinh

Khiếu nại · mất kết nối · thiếu thuốc · hàng giao đến · cận hạn · thiết bị lỗi.
Nguyên tắc: bình tĩnh → đánh giá → báo QL nếu vượt quyền → ghi nhận trên Novixa nếu cần.

## Chương 5 — Checklist giữa ca

Quầy · cận hạn · tồn bất thường · tiền mặt · thiết bị · khu vực phục vụ · nhiệm vụ còn mở.
Giúp phát hiện sớm — không chờ cuối ca.

## Chương 6 — Bàn giao ca

Nếu có ca sau: việc chưa xong · khách đang chờ · thuốc đặt trước · hàng vừa nhập · thuốc cần theo dõi · sự cố · lưu ý quan trọng.
Bàn giao rõ → không mất thông tin, ca liên tục.

## Chương 7 — Checklist cuối ca

Hoàn thành nhiệm vụ · checklist cuối · kiểm quầy · đối chiếu tiền (đóng ca) · thiết bị · ghi chú bàn giao · **đăng xuất**.
Lệch quỹ vượt ngưỡng → ghi chú + báo QL, không giấu.
Không rời ca khi chưa xong checklist cuối (theo nội quy).

## Novixa hỗ trợ

Dashboard/Hub · Checklist · Notification · nhiệm vụ · tiến độ · bàn giao / lịch sử thao tác — minh bạch cho đánh giá sau.

## Lỗi thường gặp

Muộn · bỏ Dashboard/thông báo · bỏ checklist · không bàn giao · quên đăng xuất · không báo sự cố · để việc tồn ca sau.

## Checklist nhớ

**Đầu:** đăng nhập · Hub · thông báo · checklist đầu · chuẩn bị quầy / mở ca.
**Giữa:** Hub · nhiệm vụ · CRM · FEFO · checklist giữa.
**Cuối:** nhiệm vụ · đối chiếu · bàn giao · checklist cuối · đăng xuất.

## Tình huống

1. Đúng giờ nhưng quên Hub → **mở Hub xem nhiệm vụ/thông báo trước khi bán**.
2. Còn khách đặt thuốc chưa lấy → **ghi bàn giao** cho ca sau.
3. Máy in lỗi → **báo QL + ghi nhận sự cố**.
4. Quên đăng xuất → người khác có thể thao tác **dưới tên bạn**.

## Thực hành trên ca (một nhiệm vụ)

Một ca đủ vòng: đăng nhập → checklist đầu → xem Hub/thông báo → phục vụ + việc trong ca → (checklist giữa nếu có) → bàn giao nếu cần → checklist cuối / đóng ca → đăng xuất.

## Quan sát tại quầy (soft)

QL đánh giá: đúng giờ · đăng nhập · checklist đầu · Hub · nhiệm vụ · giữa ca · bàn giao · checklist cuối · đăng xuất.
Đạt **≥ 80%** → «Đã áp dụng tại quầy». Không khóa bán · không chặn L5.

## Gợi ý tương lai — Shift Health Score (chưa bật)

Novixa có thể tự chấm «sức khỏe ca» từ dữ liệu: đúng giờ · % checklist · nhiệm vụ · bàn giao · đăng xuất… → góp Competency / đánh giá tháng. Học → làm → điểm ca → cải thiện.

## Sau bài này

Đọc → quiz ≥80% → thực hành đủ một ca → QL quan sát (soft) → **L5 — Tư vấn chuyên nghiệp & Phát triển doanh thu**.
$md$,
    duration_minutes = 20,
    pass_score_pct = 80,
    require_ack = TRUE,
    require_observation = TRUE,
    level_code = 'L4',
    updated_at = NOW()
WHERE id = '22222222-2222-2222-2222-222222222215'
   OR (program_id = '22222222-2222-2222-2222-222222222201' AND code = 'l4_own_shift');

DELETE FROM pack_learning.quiz_question
WHERE module_id = '22222222-2222-2222-2222-222222222215';

INSERT INTO pack_learning.quiz_question (module_id, sort_order, prompt, options_json, correct_option_index)
VALUES
(
    '22222222-2222-2222-2222-222222222215', 1,
    'Checklist đầu ca có mục đích gì?',
    '["Làm sau khi hết giờ","Đảm bảo quầy / thiết bị / hệ thống sẵn sàng trước khi bán","Chỉ dành cho quản lý"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222215', 2,
    'Đến đúng giờ nhưng quên mở Dashboard/Hub — bạn nên?',
    '["Bán hàng ngay","Mở Hub xem nhiệm vụ và thông báo trước","Nhờ đồng nghiệp làm hộ checklist"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222215', 3,
    'Cuối ca còn khách đặt thuốc chưa đến lấy — bạn nên?',
    '["Không ghi gì","Ghi bàn giao để ca sau theo dõi","Mang thuốc về nhà giữ hộ"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222215', 4,
    'Máy in hóa đơn lỗi — bạn nên?',
    '["Tiếp tục cố in","Báo quản lý và ghi nhận sự cố","Tắt máy im lặng"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222215', 5,
    'Checklist giữa ca giúp điều gì?',
    '["Chỉ để đẹp số","Phát hiện vấn đề sớm thay vì chờ cuối ca","Thay thế checklist cuối ca"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222215', 6,
    'Khi nào cần đăng xuất Novixa?',
    '["Không cần đăng xuất","Khi kết thúc ca / rời quầy theo quy trình","Chỉ khi đổi mật khẩu"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222215', 7,
    'Lệch quỹ vượt ngưỡng cuối ca — đúng cách?',
    '["Giấu và bù sau","Ghi chú + báo quản lý","Tự sửa số cho khớp"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222215', 8,
    'Điều quan trọng nhất để vận hành ca hiệu quả?',
    '["Chỉ bán thật nhanh","Làm đủ nhịp đầu–giữa–cuối ca + bàn giao + đăng xuất đúng","Bỏ checklist cho đỡ mất thời gian"]'::jsonb,
    1
);
