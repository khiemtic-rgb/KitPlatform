-- KitPlatform 171: L0 chào đón nhà thuốc (thay giọng «Nội quy»)
-- Giữ id/code l0_noi_quy; tenant SOP override (nếu có) vẫn ưu tiên hơn body nền tảng.

UPDATE pack_learning.module
SET
    title = 'Chào mừng đến với Nhà thuốc',
    summary = 'Thư chào · giá trị · cách làm việc đúng · một ngày tại quầy · cam kết. Đọc ngắn, làm quiz, xác nhận.',
    body_markdown = $md$## Thư chào mừng

Chào bạn,

Cảm ơn bạn đã đồng hành cùng nhà thuốc. Ở đây chúng ta bán thuốc đúng quy định, lắng nghe khách hàng và hỗ trợ lẫn nhau mỗi ca.

Bài này giúp bạn **làm quen môi trường làm việc** trước khi vào ca. Chủ nhà thuốc có thể chỉnh nội dung cho đúng SOP của mình (Phát triển Nhân sự → Sửa SOP).

Chúc bạn một khởi đầu tốt.
— Ban quản lý nhà thuốc

---

## Sứ mệnh và giá trị

Chúng ta hướng tới môi trường **chuyên nghiệp, an toàn, thân thiện** và tuân thủ chuyên môn.

Giá trị cốt lõi:
- Trung thực
- Tận tâm với khách
- Học hỏi mỗi ngày
- Trách nhiệm trên ca
- Hợp tác với đồng nghiệp
- Phát triển cùng nhau

---

## Cách làm việc đúng (nội quy rút gọn)

### Thái độ phục vụ
- Chào hỏi lịch sự, lắng nghe trước khi tư vấn.
- Không tranh cãi / lớn tiếng với khách.
- Bảo mật thông tin khách hàng.

**Nhớ:** khách không chỉ mua thuốc — họ cần được lắng nghe và chăm sóc.

### Tuân thủ chuyên môn
- Bán đúng quy định; không bán khi chưa đủ điều kiện.
- Tuân thủ GPP và nguyên tắc lấy hàng gần hạn trước (FEFO).
- Kiểm tra hạn dùng trước khi bán.
- Không tự ý đổi giá / khuyến mãi ngoài quyền được giao.

**Nhớ:** an toàn của khách luôn là ưu tiên số một.

### Trong ca
**Đầu ca:** đúng giờ · đồng phục · bảng tên · kiểm tra quầy / nhiệt độ / vệ sinh · checklist đầu ca.

**Giữa ca:** quầy gọn · không dùng điện thoại cá nhân khi đang phục vụ · kiểm tra hạn dùng khi lấy thuốc · hướng dẫn khách đủ.

**Cuối ca:** kiểm kê / đối chiếu theo quy định · checklist cuối ca · bàn giao đầy đủ.

### Giao tiếp
- Với khách: lịch sự, không bỏ mặc.
- Với đồng nghiệp: hỗ trợ, không nói xấu.
- Với quản lý: báo cáo trung thực; xin hỗ trợ khi khó; tiếp nhận góp ý.

### Bảo mật & tài khoản Novixa
Không chia sẻ: dữ liệu khách, doanh thu, tài khoản / mật khẩu Novixa, thông tin kinh doanh.

Nhân viên:
- Đăng nhập bằng tài khoản cá nhân — không dùng chung.
- Hoàn thành bài học bắt buộc và checklist được giao.
- Tham gia đánh giá định kỳ khi được yêu cầu.

### Khi lệch chuẩn
Đi muộn nhiều lần, bỏ checklist, không tuân thủ GPP, không trung thực, tiết lộ thông tin, vi phạm chuyên môn hoặc ứng xử kém với khách — sẽ được nhắc nhở / xử lý theo mức độ và quy định nhà thuốc.

---

## Một ngày tại quầy (tóm tắt)

1. Chuẩn bị đầu ca (checklist).
2. Phục vụ khách đúng thái độ và chuyên môn.
3. Dùng Novixa đúng tài khoản của mình.
4. Kết ca: checklist + bàn giao.

Chi tiết bán hàng, FEFO sâu hơn và checklist sẽ học ở các bài / mục tiếp theo — không cần nhớ hết ngay hôm nay.

---

## Văn hóa đội ngũ

Chúng ta xây dựng nhà thuốc bằng: trung thực · tận tâm · học hỏi · trách nhiệm · hợp tác · phát triển cùng nhau.

Thành tích tốt có thể được ghi nhận trên Novixa (chứng nhận, huy hiệu, xét level / bậc nghề, thưởng theo quy định nhà thuốc).

---

## Cam kết

Khi bạn **ký cam kết điện tử** ở hộp bên dưới, hệ thống ghi nhận trên tài khoản Novixa rằng bạn đã:
- Đọc và hiểu các điểm chính trong bài này.
- Đồng ý tuân thủ khi làm việc tại quầy.
- Biết hỏi quản lý / dược sĩ khi chưa chắc.

Sau bài này: làm quiz (cần đạt tối thiểu 80%), rồi tiếp tục lộ trình L1 khi sẵn sàng.
$md$,
    duration_minutes = 12,
    level_code = 'L0',
    competency_codes = ARRAY['store_policy', 'tone_of_service'],
    sort_order = 5,
    pass_score_pct = 80,
    require_ack = TRUE,
    updated_at = NOW()
WHERE id = '22222222-2222-2222-2222-222222222210'
   OR (program_id = '22222222-2222-2222-2222-222222222201' AND code = 'l0_noi_quy');

-- Bài L0 tiếp: làm quen app (tránh trùng thư chào)
UPDATE pack_learning.module
SET
    title = 'Làm quen Novixa trên ca',
    summary = 'Đăng nhập đúng · checklist đầu ca · bảo mật tài khoản — bước tiếp sau khi đã chào đón nhà thuốc.',
    body_markdown = $md$## Mục tiêu

Sau bài chào đón, bạn biết dùng Novixa đúng cách trên ca.

## Việc cần nhớ

1. Đăng nhập đúng chi nhánh / ca của mình.
2. Checklist đầu ca — làm trước khi bán.
3. Bảo mật: không cho người khác dùng tài khoản.
4. Khi không chắc — hỏi quản lý / dược sĩ, không đoán.

## Thực hành

Mở app nhân viên → Hub → xem các mục được phân quyền. Làm checklist đầu ca nếu được giao.
$md$,
    duration_minutes = 4,
    sort_order = 10,
    updated_at = NOW()
WHERE id = '22222222-2222-2222-2222-222222222211'
   OR (program_id = '22222222-2222-2222-2222-222222222201' AND code = 'l0_welcome');

-- Quiz chào đón: thay toàn bộ câu cũ của module này
DELETE FROM pack_learning.quiz_question
WHERE module_id = '22222222-2222-2222-2222-222222222210';

INSERT INTO pack_learning.quiz_question (module_id, sort_order, prompt, options_json, correct_option_index)
VALUES
(
    '22222222-2222-2222-2222-222222222210', 1,
    'Khi chưa chắc việc có đúng quy định nhà thuốc không, bạn nên?',
    '["Tự quyết cho nhanh","Hỏi quản lý / dược sĩ trước","Làm theo thói quen cá nhân"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222210', 2,
    'Về tài khoản Novixa, điều nào đúng?',
    '["Cho đồng nghiệp mượn khi bận","Không cho người khác dùng tài khoản của mình","Ghi mật khẩu ra giấy dán quầy"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222210', 3,
    'Trước khi bán thuốc có lô, việc nào là ưu tiên?',
    '["Chọn bao bì đẹp nhất","Kiểm tra hạn dùng / lấy hàng gần hạn trước","Giảm giá ngay để giữ khách"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222210', 4,
    'Thái độ phục vụ đúng là?',
    '["Tranh luận đến khi khách chịu","Lắng nghe trước, chào hỏi lịch sự, không lớn tiếng","Chỉ tập trung bán thêm sản phẩm"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222210', 5,
    'Dữ liệu khách hàng và mật khẩu Novixa thì?',
    '["Được chia sẻ trong nhóm chat cá nhân","Không chia sẻ ra ngoài; giữ bảo mật","In ra dán quầy cho tiện"]'::jsonb,
    1
);
