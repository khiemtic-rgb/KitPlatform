-- KitPlatform 177: L0 Onboarding — sẵn sàng ca đầu tiên (gộp chào đón + làm quen Novixa)

UPDATE pack_learning.module
SET
    title = 'L0 — Onboarding: Sẵn sàng cho ca làm việc đầu tiên',
    summary = 'Văn hóa · nội quy cơ bản · Novixa · Dashboard · bảo mật · checklist đầu ca. ~20 phút · ký cam kết · quiz ≥80% · quan sát soft.',
    body_markdown = $md$## Mục tiêu

Sau bài này bạn có thể:
- Hiểu nhà thuốc, văn hóa và quy định cơ bản.
- Hiểu vai trò Novixa trong vận hành.
- Đăng nhập / dùng tài khoản đúng quy định.
- Biết quy trình bắt đầu một ca (Dashboard → Checklist đầu ca).
- Sẵn sàng bước sang **L1**.

## Vì sao cần học?

Ngày đầu rất quan trọng: làm đúng quy trình, dùng đúng hệ thống, phối hợp đồng nghiệp.
Novixa giúp nhà thuốc vận hành thống nhất, giảm sai sót, nâng chất lượng phục vụ.
Bài này giúp bạn **bắt đầu đúng ngay từ ca đầu tiên**.

## Chương 1 — Chào mừng

Chào mừng bạn gia nhập đội ngũ.
Nhà thuốc không chỉ bán thuốc — còn chăm sóc và đồng hành bảo vệ sức khỏe khách hàng.
Mỗi người góp phần tạo uy tín dịch vụ.

Môi trường chúng ta hướng tới: chuyên nghiệp · trung thực · tôn trọng · hợp tác · không ngừng học hỏi.

## Chương 2 — Giá trị cốt lõi

1. **Khách hàng là trung tâm** — lắng nghe, hỗ trợ, thái độ tích cực.
2. **Đúng chuyên môn** — không tư vấn vượt quyền; chưa chắc → hỏi dược sĩ phụ trách.
3. **Trung thực** — không gian dối / che giấu sai sót; báo cáo khi có vấn đề.
4. **Hợp tác** — hỗ trợ đồng nghiệp, làm theo quy trình.
5. **Học hỏi** — hoàn thành bài trên Novixa, cập nhật kiến thức.

## Chương 3 — Nội quy cơ bản

Đúng giờ · đồng phục · bảng tên · vệ sinh khu vực · không dùng điện thoại cá nhân khi đang phục vụ · bảo mật thông tin khách · tuân thủ quy trình nhà thuốc.

Chủ có thể chỉnh chi tiết SOP (giờ ca, xử lý tiền…) qua **Sửa SOP**.

## Chương 4 — Novixa là gì?

Không chỉ phần mềm bán hàng — nền tảng vận hành nhà thuốc:
bán hàng · CSKH · tồn kho · checklist · học tập · nhiệm vụ · đánh giá · lộ trình phát triển.

## Chương 5 — Một ca với Novixa

Đăng nhập → Dashboard / Hub → **Checklist đầu ca** → Phục vụ khách → CRM (khi cần) → Checklist cuối ca → **Đăng xuất**.

## Chương 6 — Dashboard / Hub

Màn hình đầu ca giúp bạn biết: việc hôm nay · thông báo · checklist · bài học · thành tích / nhiệm vụ (tùy quyền).

## Chương 7 — Bảo mật tài khoản

Mỗi người một tài khoản.
Đăng nhập bằng tài khoản mình · không chia sẻ tài khoản / mật khẩu · không dùng tài khoản người khác · đăng xuất hết ca.
Nghi lộ → báo quản lý ngay.

## Chương 8 — Checklist đầu ca

Trước khi bán: đăng nhập · xem Dashboard/Hub · đọc thông báo · hoàn thành checklist đầu ca · kiểm tra quầy · sẵn sàng phục vụ.

## Lỗi thường gặp

Quên đăng nhập · dùng chung tài khoản · bỏ Dashboard · bỏ checklist đầu ca · quên đăng xuất · chia sẻ mật khẩu.

## Thực hành trên ca (một nhiệm vụ)

Đăng nhập tài khoản mình → mở Hub/Dashboard → đọc thông báo → mở & làm checklist đầu ca (nếu được giao) → kết ca nhớ đăng xuất.
Không cần 7 bài lab riêng trên app.

## Quan sát tại quầy (soft)

QL xác nhận: đăng nhập đúng · biết Dashboard/Hub · mở checklist · đọc thông báo · làm đầu ca · đăng xuất đúng.
Đạt **≥ 80%** → «Đã áp dụng tại quầy». Không khóa bán.

## Cam kết điện tử

Ký ở hộp bên dưới: đã đọc · hiểu quy định cơ bản · dùng đúng tài khoản · hỏi khi chưa chắc.

## Sau bài này

Ký cam kết → quiz ≥80% → thực hành trên ca → QL quan sát (soft) → mở **L1 — Tiếp đón khách & bán POS**.
$md$,
    duration_minutes = 20,
    pass_score_pct = 80,
    require_ack = TRUE,
    require_observation = TRUE,
    level_code = 'L0',
    competency_codes = ARRAY['store_policy', 'tone_of_service', 'app_basics'],
    sort_order = 5,
    updated_at = NOW()
WHERE id = '22222222-2222-2222-2222-222222222210'
   OR (program_id = '22222222-2222-2222-2222-222222222201' AND code = 'l0_noi_quy');

-- Quiz L0 (8 câu)
DELETE FROM pack_learning.quiz_question
WHERE module_id = '22222222-2222-2222-2222-222222222210';

INSERT INTO pack_learning.quiz_question (module_id, sort_order, prompt, options_json, correct_option_index)
VALUES
(
    '22222222-2222-2222-2222-222222222210', 1,
    'Novixa được dùng để làm gì?',
    '["Chỉ in hóa đơn","Hỗ trợ vận hành nhà thuốc: bán, CSKH, checklist, học tập…","Thay thế hoàn toàn dược sĩ"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222210', 2,
    'Khi bắt đầu ca, bạn nên làm gì trước?',
    '["Bán hàng ngay không cần đăng nhập","Đăng nhập và xem Dashboard/Hub + checklist đầu ca","Dùng tài khoản đồng nghiệp cho nhanh"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222210', 3,
    'Vì sao không được dùng chung tài khoản?',
    '["Vì máy chạy chậm hơn","Mọi thao tác gắn đúng người — dùng chung gây rủi ro và sai trách nhiệm","Vì quản lý không thích"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222210', 4,
    'Checklist đầu ca có mục đích gì?',
    '["Làm sau khi hết giờ cho đẹp số","Chuẩn bị quầy / hệ thống trước khi phục vụ khách","Chỉ dành cho kế toán"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222210', 5,
    'Đồng nghiệp nhờ dùng tài khoản của họ để bán — bạn nên?',
    '["Đồng ý cho nhanh","Từ chối, dùng tài khoản của mình","Ghi chung mật khẩu ra giấy"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222210', 6,
    'Quên đăng xuất cuối ca — rủi ro nào?',
    '["Không ảnh hưởng","Người khác có thể thao tác dưới tên bạn","Chỉ mất điểm thưởng"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222210', 7,
    'Khi chưa chắc chuyên môn / quy định — bạn nên?',
    '["Đoán cho xong","Hỏi quản lý / dược sĩ phụ trách","Hỏi khách tự quyết"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222210', 8,
    'Sau khi hoàn thành L0 (onboarding), bạn sẵn sàng bước sang?',
    '["L5 — Trực ca độc lập ngay","L1 — Tiếp đón khách & bán hàng trên POS","Bỏ qua học, chỉ bán"]'::jsonb,
    1
);

-- Bài phụ: thực hành ngắn (không trùng thư chào)
UPDATE pack_learning.module
SET
    title = 'L0 — Thực hành: Hub, checklist & đăng xuất',
    summary = 'Áp dụng nhanh sau Onboarding: Hub → checklist đầu ca → bảo mật đăng xuất. ~5 phút.',
    body_markdown = $md$## Mục tiêu

Bạn đã đọc Onboarding — giờ **làm thử trên app** vài thao tác then chốt.

## Việc cần làm

1. Đăng nhập bằng tài khoản cá nhân.
2. Mở Hub / Dashboard — xem thông báo và mục được phân quyền.
3. Mở Checklist đầu ca (nếu được giao) và tick các mục.
4. Nhớ: không chia sẻ tài khoản; hết ca **đăng xuất**.

## Cam kết nhỏ

Khi xác nhận đã đọc: bạn sẽ thực hành đúng các bước trên trong ca hôm nay.
$md$,
    duration_minutes = 5,
    pass_score_pct = 80,
    require_ack = TRUE,
    require_observation = TRUE,
    level_code = 'L0',
    competency_codes = ARRAY['app_basics'],
    sort_order = 10,
    updated_at = NOW()
WHERE id = '22222222-2222-2222-2222-222222222211'
   OR (program_id = '22222222-2222-2222-2222-222222222201' AND code = 'l0_welcome');
