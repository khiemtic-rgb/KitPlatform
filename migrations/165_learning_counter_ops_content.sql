-- KitPlatform 165: Rewrite L0–L5 for counter reality
-- Focus: phục vụ khách, đầu/giữa/cuối ca, động lực bán tăng DT (trong khung GPP)
-- Local-first; apply via scripts/run-migrations.ps1

UPDATE pack_learning.program
SET
    title = 'Onboarding quầy nhà thuốc — L0 đến L5',
    summary = 'Thực tế ca làm việc: phục vụ khách, nhiệm vụ đầu–giữa–cuối ca, bán đúng GPP và tăng doanh thu bền vững trên Novixa.',
    updated_at = NOW()
WHERE id = '22222222-2222-2222-2222-222222222201';

-- ─── L0 ─────────────────────────────────────────────────────────────────────
UPDATE pack_learning.module SET
    title = 'L0 — Vai trò, bảo mật & bản đồ một ngày tại quầy',
    summary = 'Ai làm việc gì; bảo mật tài khoản; khung đầu–giữa–cuối ca (chi tiết ở L4).',
    body_markdown = E'## Mục tiêu
Bạn biết **mình được phép làm gì** trên Novixa, bảo vệ tài khoản, và hình dung **một ngày tại quầy** trước khi vào bài bán / CSKH.

## Tài khoản & vai trò
1. Mỗi người **một tài khoản** — không dùng chung mật khẩu thu ngân.
2. Đăng nhập đúng **chi nhánh / quầy** được giao.
3. Làm đúng quyền: thu ngân / NV bán ≠ tự sửa giá lớn, ≠ điều chỉnh kho lớn (trừ khi được cấp).
4. Khóa máy / đăng xuất khi rời quầy.

## Bản đồ một ngày (tóm tắt — chi tiết L4)
| Giai đoạn | Việc chính |
|-----------|------------|
| **Đầu ca** | Mở ca, kiểm tra máy/POS, tiền quỹ đầu, nhìn checklist ngày, biết hàng ưu tiên bán |
| **Giữa ca** | Đón khách → tư vấn trong quyền → bán FEFO → gắn khách/điểm → giữ quầy gọn |
| **Cuối ca** | Đóng ca, đối quỹ, ghi lệch (nếu có), báo việc chưa xong cho ca sau |

## Ranh giới chuyên môn (GPP)
- Novixa **không thay** tư vấn dược lâm sàng.
- Không chắc liều / tương tác / thuốc hạn chế → **mời dược sĩ phụ trách**, không đoán / tra mạng rồi bán.

## Lợi ích với bạn
Làm đúng vai = ít lỗi = được tin giao ca / lên bậc. Sai quyền hoặc giấu lỗi = rủi ro cho cả nhà thuốc.

> **Nguồn:** NVX-CS-02 §A · NVX-CPL-01 §5 & §8 · checklist GPP vận hành.',
    duration_minutes = 10,
    updated_at = NOW()
WHERE id = '22222222-2222-2222-2222-222222222211';

-- ─── L1 ─────────────────────────────────────────────────────────────────────
UPDATE pack_learning.module SET
    title = 'L1 — Phục vụ khách tại quầy & bán POS (FEFO)',
    summary = 'Luồng đón khách → hỏi nhu cầu → bán đúng FEFO → thanh toán → thái độ quầy.',
    body_markdown = E'## Mục tiêu
Bạn phục vụ khách **từ lúc chào đến lúc đưa bill** đúng quy trình nhà thuốc trên Novixa — không chỉ «bấm POS».

## Luồng phục vụ khách (thực tế quầy)
1. **Chào & định hướng:** nhìn khách, chào ngắn, hỏi «Anh/chị cần hỗ trợ gì ạ?»
2. **Lắng nghe nhu cầu:** triệu chứng / tên thuốc / mang đơn / mua theo đơn cũ.
3. **Phân loại nhanh:**
   - OTC / hàng thông thường trong quyền → bạn xử lý.
   - Kê đơn / hỏi liều phức tạp / tương tác / thuốc hạn chế → **mời dược sĩ**.
4. **Xác nhận lại** trước khi bán: đúng sản phẩm, số lượng, dạng bào chế.
5. **Bán trên POS:** mở ca (nếu chưa) → quét/tìm SP → hệ thống ưu tiên **FEFO** → gắn khách (nếu có) → thanh toán → in/đưa bill + cảm ơn.
6. **Sau bán:** nhắc ngắn cách bảo quản cơ bản nếu biết (theo nhãn), không bịa liều.

## SOP bán trên Novixa (GPP)
- Luôn **mở ca** trước khi bán.
- **Không đổi lô tùy tiện** — FEFO là mặc định an toàn hạn dùng.
- Không tự giảm giá ngoài quyền — xin đúng quy trình.
- Trả hàng: theo nội quy + quyền trên hệ thống (được giao mới làm).

## Thái độ làm tăng trải nghiệm (và doanh thu)
- Không để khách đứng chờ mà không ai nhìn.
- Nói rõ khi phải chờ DS: «Em mời dược sĩ hỗ trợ anh/chị ngay ạ.»
- Bill rõ ràng; kiểm tra tiền mặt / chuyển khoản trước khi kết thúc đơn.

## Việc không làm
- Bán «cho xong» sai sản phẩm / sai liều theo đoán.
- Tranh cãi với khách trước mặt người khác — chuyển quản lý / DS khi cần.

> **Nguồn:** NVX-CPL-01 §4.2 · NVX-CS-02 §D.1 · GPP vận hành (FEFO khi bán).',
    duration_minutes = 12,
    updated_at = NOW()
WHERE id = '22222222-2222-2222-2222-222222222212';

-- ─── L2 ─────────────────────────────────────────────────────────────────────
UPDATE pack_learning.module SET
    title = 'L2 — CSKH, gợi ý bán đúng mực & điểm thưởng',
    summary = 'Hồ sơ khách, loyalty, gợi ý sản phẩm hỗ trợ — tăng giá trị đơn trong khung GPP.',
    body_markdown = E'## Mục tiêu
Bạn dùng CRM/loyalty đúng cách và **gợi ý bán thêm có trách nhiệm** — giúp khách + tăng doanh thu, không ép mua / không vượt chuyên môn.

## Hồ sơ khách trên Novixa
1. Tìm / tạo theo **SĐT sạch**, xác nhận đúng người trước khi gắn đơn / điểm.
2. Consent marketing / chăm sóc theo cấu hình nhà thuốc — **không ép**.
3. Gắn khách vào đơn khi khách đồng ý: tích điểm, lịch sử mua hỗ trợ CSKH lần sau.

## Gợi ý bán đúng mực (upsell / cross-sell có đạo đức)
Chỉ gợi ý khi **liên quan nhu cầu** và trong quyền / sau khi DS đã chốt thuốc chính (nếu cần DS):
- Thuốc uống → hỏi đã có **nước / dụng cụ đo** (nếu phù hợp chương trình nhà thuốc).
- Cảm cúm thông thường → có thể nhắc **khẩu trang / vitamin C** nếu khách quan tâm (không khẳng định chữa bệnh).
- Hết hàng gần hạn ưu tiên bán đúng FEFO — giải thích lợi ích hạn dùng còn tốt, không «đẩy hàng xấu».
- **Không** gợi ý thuốc kê đơn / kháng sinh thêm theo cảm tính.

Câu mẫu: «Anh/chị đang dùng kèm gì ạ? Nhà thuốc đang có chương trình điểm / combo X nếu phù hợp.»

## Điểm thưởng & ưu đãi
- Giải thích **ngắn, đúng cấu hình** trên hệ thống.
- Không hứa ưu đãi ngoài chương trình đã bật.
- Khi khách thắc mắc điểm: kiểm tra trên màn hình, không đoán.

## Ranh giới tư vấn (bắt buộc)
Khách hỏi liều phức tạp / tương tác / thuốc hạn chế:
- Không trả lời theo mạng.
- Mời **dược sĩ phụ trách**.
- Có thể ghi chú tư vấn / dispense trên đơn (nếu được giao).

## Động lực bán (an toàn)
Doanh thu tốt = **đúng nhu cầu × đủ đơn × khách quay lại**. Ép mua hoặc tư vấn sai = mất uy tín + rủi ro GPP.

> **Nguồn:** NVX-CPL-01 §3 CRM · §5–§6 · GPP (ghi chú tư vấn / dispense).',
    duration_minutes = 12,
    updated_at = NOW()
WHERE id = '22222222-2222-2222-2222-222222222213';

-- ─── L3 ─────────────────────────────────────────────────────────────────────
UPDATE pack_learning.module SET
    title = 'L3 — Tồn kho phục vụ bán: nhập hàng, lô & cận date',
    summary = 'GRN đúng lô/HSD; biết tồn thấp / cận date ảnh hưởng bán hàng hàng ngày.',
    body_markdown = E'## Mục tiêu
Bạn hiểu **tồn đúng → bán được, FEFO đúng, khách tin**. Nếu được giao nhận hàng: làm GRN đủ lô/HSD; nếu chỉ bán: biết báo khi hết / cận date.

## Vì sao NV bán cần quan tâm kho?
- Hết hàng giữa ca = mất doanh thu + khách đi chỗ khác.
- Sai lô/HSD = rủi ro GPP và khiếu nại.
- Cận date không ưu tiên bán = hao hụt.

## Khi được giao nhận hàng (SOP GRN)
1. PO đúng NCC.
2. GRN: **số lượng + số lô + HSD**, đối chiếu thùng thực tế trước xác nhận.
3. Lệch lớn → báo quản lý, ghi nhận — không tự «vá» cho khớp.
4. Chứng từ giấy theo nội quy nhà thuốc.

## Việc hàng ngày tại quầy (giữa ca)
- Thấy sắp hết / đã hết mặt hàng bán chạy → **báo quản lý / ghi đề xuất** (đừng đợi hết ca nếu đang cao điểm).
- Hệ thống cảnh báo cận date / tồn thấp: ưu tiên bán đúng FEFO, không giấu hàng mới phía trước che hàng cũ.
- Không tự điều chỉnh tồn lớn nếu không có quyền.

## Kiểm kê (khi được giao)
Đếm theo lô → duyệt chênh lệch có log — không chỉ cuối năm.

> **Nguồn:** NVX-CPL-01 §4.1–4.4 · NVX-CS-02 §D.2 · Training PO/GRN.',
    duration_minutes = 10,
    updated_at = NOW()
WHERE id = '22222222-2222-2222-2222-222222222214';

-- ─── L4 ─────────────────────────────────────────────────────────────────────
UPDATE pack_learning.module SET
    title = 'L4 — Nhiệm vụ đầu ca · giữa ca · cuối ca',
    summary = 'Checklist thực tế một ca làm việc tại quầy + đối quỹ + GPP ngày.',
    body_markdown = E'## Mục tiêu
Bạn làm đúng **nhịp một ca**: đầu ca sẵn sàng bán, giữa ca phục vụ & giữ chuẩn, cuối ca chốt sạch để ca sau không «dọn hộ».

## ĐẦU CA (trước khi đón khách)
1. Đăng nhập đúng tài khoản / quầy.
2. **Mở ca POS** — kiểm tra tiền quỹ đầu (nếu quy trình nhà thuốc yêu cầu).
3. Máy in bill, máy quét, kết nối — thử nhanh.
4. Nhìn **checklist ngày / Success** (nếu nhà thuốc bật): việc ưu tiên hôm nay.
5. Quan sát quầy: sạch, giá/kệ gọn, hàng cận date không bị che.
6. Biết **DS / quản lý** trực ca hôm nay để leo thang.

## GIỮA CA (trong giờ bán)
1. Luồng phục vụ L1 + gợi ý bán L2.
2. Bán **FEFO**; gắn khách/điểm khi phù hợp.
3. Giữ quầy: thu dọn nhanh giữa các khách, không để bill/tiền lung tung.
4. Hết hàng / lệch bất thường / khách khó → báo đúng người, ghi nhận.
5. Không bỏ ca / giao máy cho người khác dùng chung tài khoản.
6. Nghỉ ngắn: khóa máy; bàn giao miệng nếu có người thay.

## CUỐI CA (trước khi về)
1. Không nhận đơn mới khi đã chốt (theo nội quy) — hoàn tất đơn dang dở.
2. **Đóng ca**: đối soát tiền mặt / CK / thẻ với số trên hệ thống.
3. Lệch quỹ vượt ngưỡng → **ghi chú + báo quản lý**, không giấu / không tự sửa sổ cho đẹp.
4. Tick các mục checklist GPP ngày còn thiếu (FEFO, ghi chú tư vấn nếu có, chốt ca…).
5. Báo ca sau: hàng sắp hết, việc chưa xong, sự cố trong ca.
6. Đăng xuất / khóa máy.

## Checklist GPP vận hành — hàng ngày (gợi ý)
- Bán theo FEFO / ghi nhận lô khi bán.
- Ghi chú tư vấn / dispense khi áp dụng.
- Chốt ca + đối soát thu chi.
- Rà tồn thấp → đề xuất nhập nếu được giao.

## Lợi ích
Ca sạch = ít mất tiền, ít mất hàng, chủ tin giao việc, bạn dễ được ghi nhận / lên bậc.

> **Nguồn:** NVX-CS-02 §C & §D.1–D.3 · GPP vận hành (Admin) · Success checklist ca.',
    duration_minutes = 14,
    updated_at = NOW()
WHERE id = '22222222-2222-2222-2222-222222222215';

-- ─── L5 ─────────────────────────────────────────────────────────────────────
UPDATE pack_learning.module SET
    title = 'L5 — Tăng doanh thu bền vững & xử lý tình huống',
    summary = 'Động lực bán, KPI đơn giản, sự cố thường gặp, leo thang đúng lúc — không phá GPP.',
    body_markdown = E'## Mục tiêu
Bạn biết **làm sao tăng doanh thu đúng cách** tại quầy và xử lý tình huống mà không phá quy trình / GPP.

## Động lực bán — 5 việc mang lại DT thật
1. **Đón đủ khách** — không để khách tự phục vụ rồi bỏ đi vì không ai hỏi.
2. **Đủ đơn đúng nhu cầu** — hỏi thêm (L2), không chỉ bán đúng một món khách chỉ tay nếu còn nhu cầu liên quan hợp lệ.
3. **Gắn khách / điểm** — khách quay lại = DT dài hạn.
4. **Ưu tiên hàng cận date hợp lệ (FEFO)** — giảm hủy = giữ biên lợi nhuận.
5. **Báo hết hàng sớm** — có hàng để bán = không mất đơn.

## Việc không phải «tăng DT»
- Ép mua thuốc không cần / sai chỉ định.
- Tư vấn vượt quyền để «chốt đơn».
- Giấu lệch quỹ / lệch tồn cho đẹp số.
- Bán sai lô, tắt cảnh báo hạn dùng.

## KPI đơn giản NV có thể tự theo dõi
- Số đơn / ca, giá trị trung bình đơn (AOV), % đơn có gắn khách.
- Số lần phải mời DS (bình thường — không xấu).
- Lệch quỹ = 0 hoặc đã báo đúng quy trình.

Chủ có thể ghi nhận / chấm tháng / duyệt bậc dựa trên học + thực tế ca (module Đào tạo).

## Sự cố thường gặp — xử lý
| Tình huống | Làm ngay |
|------------|----------|
| POS treo / mất điện | Bình tĩnh, không tự «bán tay» ngoài quy trình; gọi quản lý / hotline nếu P0 |
| Khách bức xúc | Lắng nghe, không cãi; mời quản lý / DS |
| Lệch quỹ / sai tồn lớn | Báo ngay + ghi nhận |
| Không chắc chuyên môn | Dừng bán đoán — mời DS |

## Giới hạn phần mềm
Novixa hỗ trợ **ghi nhận & truy vết**. Không thay chứng nhận GPP, bảo quản vật lý, tư vấn y khoa, trách nhiệm DS tại quầy.

## Kết thúc lộ trình
Bạn đã có: phục vụ khách (L1), CSKH & gợi ý bán (L2), kho phục vụ bán (L3), nhịp ca ngày (L4), DT & tình huống (L5). Thực hành trên ca thật + checklist ngày.

> **Nguồn:** NVX-CPL-01 §5 & §8 · NVX-CS-01 Hypercare · Go-live NVX-CS-02.',
    duration_minutes = 12,
    updated_at = NOW()
WHERE id = '22222222-2222-2222-2222-222222222216';

-- Quizzes khớp nội dung mới (thực tế quầy)
DELETE FROM pack_learning.quiz_question
WHERE module_id IN (
    '22222222-2222-2222-2222-222222222211',
    '22222222-2222-2222-2222-222222222212',
    '22222222-2222-2222-2222-222222222213',
    '22222222-2222-2222-2222-222222222214',
    '22222222-2222-2222-2222-222222222215',
    '22222222-2222-2222-2222-222222222216'
);

INSERT INTO pack_learning.quiz_question (module_id, sort_order, prompt, options_json, correct_option_index)
VALUES
-- L0
(
    '22222222-2222-2222-2222-222222222211', 1,
    'Về tài khoản làm việc tại quầy, đúng là?',
    '["Dùng chung mật khẩu thu ngân cho nhanh","Mỗi người một tài khoản, không dùng chung","Cho khách đăng nhập giúp khi đông"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222211', 2,
    'Ba giai đoạn chính của một ngày tại quầy là?',
    '["Chỉ bán và về","Đầu ca — giữa ca — cuối ca","Chỉ kiểm kê cuối tháng"]'::jsonb,
    1
),
-- L1
(
    '22222222-2222-2222-2222-222222222212', 1,
    'Khi khách hỏi liều / tương tác phức tạp, bạn nên?',
    '["Tra mạng rồi bán","Mời dược sĩ phụ trách tại quầy","Đoán theo đơn cũ"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222212', 2,
    'Bán trên Novixa, về lô hàng đúng là?',
    '["Đổi lô tùy ý cho dễ lấy","Ưu tiên FEFO — lô hết hạn sớm hơn","Bán không cần mở ca"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222212', 3,
    'Bước đầu trong luồng phục vụ khách là?',
    '["In bill trước","Chào hỏi và hỏi nhu cầu","Nhờ khách tự lấy hàng rồi tính tiền"]'::jsonb,
    1
),
-- L2
(
    '22222222-2222-2222-2222-222222222213', 1,
    'Gợi ý bán thêm đúng mực là?',
    '["Ép mua thêm bất kỳ sản phẩm nào","Gợi ý liên quan nhu cầu, trong quyền / sau DS nếu cần","Tự thêm kháng sinh cho tăng đơn"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222213', 2,
    'Về điểm thưởng / ưu đãi, đúng là?',
    '["Hứa ưu đãi ngoài chương trình cho chốt đơn","Giải thích đúng cấu hình hệ thống, không ép consent","Không cần gắn khách vào đơn"]'::jsonb,
    1
),
-- L3
(
    '22222222-2222-2222-2222-222222222214', 1,
    'Giữa ca thấy mặt hàng bán chạy sắp hết, bạn nên?',
    '["Im và đợi hết ca","Báo quản lý / ghi đề xuất sớm","Tự sửa tồn cho còn hàng"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222214', 2,
    'Khi nhận hàng (GRN), bắt buộc có?',
    '["Chỉ tổng số lượng","Số lượng + số lô + HSD, đối chiếu thực tế","Nhập sau vài ngày"]'::jsonb,
    1
),
-- L4
(
    '22222222-2222-2222-2222-222222222215', 1,
    'Việc nào thuộc ĐẦU CA?',
    '["Đóng ca và đối quỹ","Mở ca, kiểm tra máy/in, nhìn checklist ngày","Chỉ bán hàng không cần mở ca"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222215', 2,
    'Cuối ca lệch quỹ vượt ngưỡng, đúng cách?',
    '["Giấu và bù sau","Ghi chú + báo quản lý","Tự sửa báo cáo cho khớp"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222215', 3,
    'Giữa ca, việc nào đúng?',
    '["Giao máy cho người khác dùng chung tài khoản","Phục vụ FEFO, giữ quầy gọn, báo sự cố đúng người","Tắt mọi cảnh báo hạn dùng"]'::jsonb,
    1
),
-- L5
(
    '22222222-2222-2222-2222-222222222216', 1,
    'Cách tăng doanh thu bền vững tại quầy?',
    '["Ép mua và tư vấn vượt quyền","Đón đủ khách, đủ đơn đúng nhu cầu, gắn khách, FEFO, báo hết hàng sớm","Giấu lệch quỹ để số đẹp"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222216', 2,
    'POS không bán được (P0), bạn nên?',
    '["Tự xử lý im lặng đến hết ca","Báo quản lý / escalation ngay, ghi nhận","Tắt máy và về"]'::jsonb,
    1
);
