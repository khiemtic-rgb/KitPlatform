-- KitPlatform 163: Rewrite Learning L0–L5 content from pharmacy docs
-- Sources: docs/novixa/06-compliance/gpp-operational-context-v1.md (NVX-CPL-01)
--          docs/novixa/07-customer/onboarding-playbook-v1.md (NVX-CS-01)
--          docs/novixa/07-customer/go-live-checklist-customer-v1.md (NVX-CS-02)
--          Admin GPP vận hành checklist (Inventory → GPP)

UPDATE pack_learning.program
SET
    title = 'Onboarding nhà thuốc Novixa — L0 đến L5',
    summary = 'Theo tài liệu GPP vận hành + Go-live checklist nhà thuốc: chào mừng → POS/FEFO → CSKH → GRN/lô → đóng ca → trực ca. Học ngắn, quiz, ký xác nhận.',
    updated_at = NOW()
WHERE id = '22222222-2222-2222-2222-222222222201';

UPDATE pack_learning.module SET
    title = 'L0 — Làm việc đúng vai trên Novixa',
    summary = 'Đăng nhập, phân quyền, bảo mật tài khoản — theo kickoff / go-live nhà thuốc.',
    body_markdown = E'## Mục tiêu
Sau bài này bạn dùng đúng tài khoản Novixa của mình và biết khi nào phải hỏi quản lý / dược sĩ.

## Theo tài liệu nhà thuốc (NVX-CS-02 §A)
1. Mỗi người **một tài khoản** (Quản trị / Thu ngân / DS) — **không dùng chung** mật khẩu.
2. Đăng nhập đúng **chi nhánh / quầy** được giao.
3. Chỉ làm việc trong **quyền** đã được cấp (RBAC). Không nhờ người khác đăng nhập giúp.

## Đầu ca (GPP vận hành — hàng ngày)
- Kiểm tra thiết bị / app Staff sẵn sàng bán.
- Xem checklist đầu ca / Success nếu được giao.
- Bảo mật: khóa máy khi rời quầy.

## Ranh giới chuyên môn (NVX-CPL-01 §5)
- Novixa **không thay** tư vấn dược lâm sàng.
- Không chắc về liều / tương tác / thuốc hạn chế → **hỏi dược sĩ phụ trách**, không đoán.

## Thực hành trên hệ thống
Staff app → Hub → các mục bạn được phân quyền · Admin chỉ khi được giao.

> **Nguồn tài liệu:** NVX-CS-02 Go-live checklist §A · NVX-CPL-01 GPP vận hành §5 · GPP vận hành (ca bán).',
    duration_minutes = 5,
    updated_at = NOW()
WHERE id = '22222222-2222-2222-2222-222222222211';

UPDATE pack_learning.module SET
    title = 'L1 — Bán lẻ POS đúng FEFO',
    summary = 'Mở ca → bán → FEFO/lô → thanh toán → in bill — theo SOP bán lẻ GPP & go-live D.1.',
    body_markdown = E'## Mục tiêu
Tự bán một đơn OTC trên Novixa đúng quy trình nhà thuốc: **ca → chọn hàng → lô FEFO → thanh toán → bill**.

## SOP bán lẻ (NVX-CPL-01 §4.2)
1. **Mở ca POS** trước khi bán.
2. Quét barcode / tìm sản phẩm.
3. Hệ thống **ưu tiên lô sắp hết hạn (FEFO)** — không đổi lô tùy tiện trừ khi có quy trình đặc biệt và được phép.
4. Chọn khách (nếu có) → thanh toán → **in bill / lưu đơn**.
5. **Đóng ca** và đối soát khi hết ca (chi tiết ở bài L4).

## Kiểm tra trước go-live (NVX-CS-02 §D.1)
- Quét/chọn SP · thanh toán · in bill OK.
- FEFO đúng kỳ vọng quầy.
- Biết quy trình **trả hàng** thử (khi được giao).

## Giảm giá & quyền
Không tự giảm giá ngoài quyền. Xin đúng người có quyền / quy trình nội bộ.

## Ranh giới
Thuốc kê đơn / tư vấn chuyên sâu → chuyển **dược sĩ phụ trách**.

> **Nguồn tài liệu:** NVX-CPL-01 §4.2 Bán lẻ · NVX-CS-02 §D.1 POS · GPP vận hành: «Bán theo FEFO / ghi nhận lô khi bán».',
    duration_minutes = 8,
    updated_at = NOW()
WHERE id = '22222222-2222-2222-2222-222222222212';

UPDATE pack_learning.module SET
    title = 'L2 — Khách hàng, điểm thưởng & ranh giới tư vấn',
    summary = 'Hồ sơ khách, consent, loyalty — và khi nào phải mời dược sĩ (GPP + CRM).',
    body_markdown = E'## Mục tiêu
Dùng đúng hồ sơ khách trên Novixa, giải thích ưu đãi ngắn gọn, và **không vượt ranh giới tư vấn**.

## Hồ sơ khách (NVX-CPL-01 §3 & §6)
- Tìm / tạo khách theo **SĐT** sạch.
- Xác nhận đúng người trước khi gắn đơn / điểm.
- Consent marketing / chăm sóc theo cấu hình nhà thuốc — không ép khách.

## Loyalty / ưu đãi
- Giải thích điểm / ưu đãi **ngắn, đúng cấu hình** trên hệ thống.
- Không hứa ưu đãi ngoài chương trình đã bật.

## Ranh giới tư vấn (bắt buộc)
Khi khách hỏi **liều dùng phức tạp / tương tác thuốc / thuốc hạn chế**:
- **Không** trả lời theo mạng / đoán.
- Mời **dược sĩ phụ trách** tại quầy.
- Có thể dùng ghi chú tư vấn / dispense trên đơn hoàn tất (nếu được giao) — theo checklist GPP.

## Thái độ quầy
Lắng nghe → xác nhận nhu cầu → xử lý trong quyền hoặc chuyển đúng người.

> **Nguồn tài liệu:** NVX-CPL-01 §3 CRM · §5–§6 giới hạn & consent · GPP vận hành: «Ghi chú tư vấn / dispense».',
    duration_minutes = 7,
    updated_at = NOW()
WHERE id = '22222222-2222-2222-2222-222222222213';

UPDATE pack_learning.module SET
    title = 'L3 — Nhập hàng GRN đúng lô & HSD',
    summary = 'PO → GRN → lô/HSD → đối chiếu thực tế — theo SOP nhập hàng GPP & go-live D.2.',
    body_markdown = E'## Mục tiêu
Nhận hàng đúng quy trình nhà thuốc: **đủ số lô, hạn dùng, số lượng** và không tự «vá» lệch lớn.

## SOP nhập hàng (NVX-CPL-01 §4.1)
1. Tạo / chọn **PO** (đơn mua) đúng NCC.
2. Lập **GRN (phiếu nhập)** với: số lượng, **số lô**, **HSD**.
3. **Đối chiếu hàng thực tế** với GRN trước khi xác nhận.
4. Lưu chứng từ NCC (hóa đơn giấy) theo nội quy — Novixa lưu số liệu GRN.
5. Lệch số lượng lớn → **báo quản lý**, ghi nhận lệch — không tự sửa cho khớp hệ thống.

## Go-live (NVX-CS-02 §D.2)
- Tạo GRN thử → tồn tăng đúng theo lô.

## FEFO & cận date (NVX-CPL-01 §4.4)
- Sau khi nhập đúng lô/HSD, bán sẽ ưu tiên FEFO.
- Định kỳ rà báo cáo cận date / tồn thấp theo lịch nhà thuốc.

## Kiểm kê (gợi ý §4.3)
Khi được giao kiểm kê: đếm theo lô → duyệt chênh lệch có log — không chỉ cuối năm.

> **Nguồn tài liệu:** NVX-CPL-01 §4.1 Nhập hàng · §3 FEFO/GRN · NVX-CS-02 §D.2 · Training W3 (PO/GRN).',
    duration_minutes = 8,
    updated_at = NOW()
WHERE id = '22222222-2222-2222-2222-222222222214';

UPDATE pack_learning.module SET
    title = 'L4 — Mở ca, đóng ca & checklist GPP ngày',
    summary = 'Ca bán, đối quỹ, checklist GPP hàng ngày — theo Success + GPP vận hành.',
    body_markdown = E'## Mục tiêu
Tự **mở/đóng ca** minh bạch và hoàn thành thói quen GPP **hàng ngày** trên Novixa.

## Quy trình ca (NVX-CS-02 §C & §D.1)
1. **Mở ca** trước khi bán.
2. Trong ca: bán đúng FEFO, ghi chú tư vấn/dispense khi cần.
3. **Đóng ca**: đối soát tiền mặt / hình thức thanh toán với hệ thống.
4. Lệch quỹ vượt ngưỡng nội bộ → **ghi chú + báo quản lý** (không giấu).
5. Đã thử quy trình mở–đóng ca ít nhất 1 lần khi đào tạo.

## Checklist GPP vận hành — hàng ngày (ca bán)
Trên Admin → Kho / GPP vận hành (đồng bộ theo nhà thuốc), các mục điển hình:
- Bán theo FEFO / ghi nhận lô khi bán.
- Ghi chú tư vấn / dispense trên đơn hoàn tất (khi áp dụng).
- **Chốt ca + đối soát thu chi**.
- Rà tồn thấp → đề xuất PO nếu được giao.

## Báo cáo
Biết xem báo cáo doanh thu / tồn cơ bản sau ca (NVX-CS-02 §D.3).

> **Nguồn tài liệu:** NVX-CS-02 §C Đào tạo · §D.1–D.3 · GPP vận hành (Admin) · Success checklist ca.',
    duration_minutes = 8,
    updated_at = NOW()
WHERE id = '22222222-2222-2222-2222-222222222215';

UPDATE pack_learning.module SET
    title = 'L5 — Trực ca an toàn & leo thang đúng lúc',
    summary = 'Tổng hợp L0–L4: sự cố, escalation, giới hạn pháp lý — theo GPP & hypercare.',
    body_markdown = E'## Mục tiêu
Trực ca cơ bản **an toàn** khi đã nắm L0–L4: biết làm gì trong quyền và **khi nào phải gọi quản lý / DS**.

## Việc phải nắm (tổng hợp tài liệu nhà thuốc)
1. **POS + FEFO** đúng SOP bán lẻ.
2. **CSKH** trong ranh giới — không thay DS khi luật/nội quy yêu cầu.
3. **GRN / lô / HSD** không để sai nguồn gốc hạn dùng.
4. **Đóng ca** minh bạch + checklist GPP ngày.
5. Phân quyền: thu ngân ≠ tự sửa giá / điều chỉnh kho lớn (NVX-CPL-01 §8).

## Sự cố thường gặp (Hypercare NVX-CS-01 §7)
- Mất điện / máy treo / không bán được → bình tĩnh, ghi nhận, gọi quản lý / hotline Novixa nếu P0.
- Lệch quỹ / sai tồn nghiêm trọng → báo ngay, không tự «chỉnh cho đẹp».
- Khách bức xúc / khiếu nại chuyên môn → chuyển DS / quản lý.

## Giới hạn phần mềm (NVX-CPL-01 §5)
Novixa hỗ trợ **ghi nhận & truy vết** mua–tồn–bán.  
**Không** thay: chứng nhận GPP, bảo quản vật lý, tư vấn y khoa, trách nhiệm DS tại quầy.

> **Nguồn tài liệu:** NVX-CPL-01 §5 & §8 · NVX-CS-01 Hypercare · Go-live gate NVX-CS-02.',
    duration_minutes = 10,
    updated_at = NOW()
WHERE id = '22222222-2222-2222-2222-222222222216';

-- Refresh quizzes to match pharmacy-doc content
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
    'Theo go-live nhà thuốc, về tài khoản đăng nhập bạn phải?',
    '["Dùng chung mật khẩu thu ngân cho nhanh","Mỗi người một tài khoản, không dùng chung","Cho khách đăng nhập giúp khi đông"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222211', 2,
    'Khi không chắc về liều / tương tác thuốc, theo GPP Novixa bạn nên?',
    '["Tra mạng rồi bán","Hỏi dược sĩ phụ trách — phần mềm không thay tư vấn lâm sàng","Đoán theo đơn cũ của khách"]'::jsonb,
    1
),
-- L1
(
    '22222222-2222-2222-2222-222222222212', 1,
    'Trong SOP bán lẻ GPP trên Novixa, bước nào đúng về lô?',
    '["Đổi lô tùy ý cho dễ lấy hàng","Ưu tiên FEFO — lô hết hạn sớm hơn","Bán không cần mở ca"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222212', 2,
    'Checklist go-live POS yêu cầu kiểm tra điều gì?',
    '["Chỉ xem báo cáo tháng","Quét/chọn SP, thanh toán, in bill và FEFO đúng kỳ vọng","Tắt hết cảnh báo hạn dùng"]'::jsonb,
    1
),
-- L2
(
    '22222222-2222-2222-2222-222222222213', 1,
    'Khách hỏi tương tác thuốc phức tạp, đúng quy trình nhà thuốc là?',
    '["Trả lời theo kinh nghiệm cá nhân","Mời dược sĩ phụ trách tại quầy","Bán thêm sản phẩm liên quan ngay"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222213', 2,
    'Về hồ sơ khách / marketing trên Novixa, điểm nào đúng?',
    '["Không cần consent","Consent theo cấu hình nhà thuốc, không ép khách","Gộp dữ liệu khách mọi nhà thuốc chung một kho"]'::jsonb,
    1
),
-- L3
(
    '22222222-2222-2222-2222-222222222214', 1,
    'Khi nhận hàng (GRN), theo SOP GPP bạn phải có?',
    '["Chỉ tổng số lượng, không cần lô","Số lượng + số lô + HSD, đối chiếu thực tế trước xác nhận","Nhập sau vài ngày cho đỡ sai"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222214', 2,
    'Số lượng thực nhận lệch PO lớn, bạn nên?',
    '["Tự sửa cho khớp hệ thống","Báo quản lý / ghi nhận lệch trước khi xác nhận","Bỏ qua vì gần đúng"]'::jsonb,
    1
),
-- L4
(
    '22222222-2222-2222-2222-222222222215', 1,
    'Checklist GPP vận hành hàng ngày (ca bán) gồm việc nào?',
    '["Chỉ kiểm kê cuối năm","Chốt ca + đối soát thu chi (và FEFO khi bán)","Xóa mọi lệch quỹ cho đẹp sổ"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222215', 2,
    'Lệch quỹ vượt ngưỡng khi đóng ca, đúng cách là?',
    '["Giấu và bù sau","Ghi chú + báo quản lý","Tự sửa báo cáo ca cho khớp"]'::jsonb,
    1
),
-- L5
(
    '22222222-2222-2222-2222-222222222216', 1,
    'Novixa hỗ trợ GPP chủ yếu bằng cách nào?',
    '["Cấp chứng nhận GPP thay nhà thuốc","Ghi nhận và truy vết mua–tồn–bán (lô, HSD, FEFO)","Thay dược sĩ tư vấn lâm sàng"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222216', 2,
    'Sự cố P0 (POS không bán / sai tồn nghiêm trọng), theo hypercare bạn nên?',
    '["Tự xử lý im lặng đến hết ca","Báo quản lý / escalation ngay, ghi nhận sự cố","Tắt máy và về"]'::jsonb,
    1
);
