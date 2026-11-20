# Novixa — Product Experience & Pharmacy Intelligence Audit 01

Ngày: 2026-11-04. Repo `E:\KitPlatform`, HEAD `dbd96772` + working tree hiện tại. Chỉ đọc code và chạy thử trên DB dùng một lần `novixa_ux_audit` (cổng 5436; dựng từ manifest prod + migration Pharmacy chưa vào manifest + seed demo). Không đụng prod, dev DB, `NT_XUANHOA`.

Ký hiệu mức triển khai: **(1)** đã chạy được, đã truy vết hoặc chạy thử · **(2)** có nhưng chưa hoàn chỉnh / chưa liên kết · **(3)** có giao diện, nghiệp vụ chưa chạy đủ · **(4)** chưa có · **(5)** có nhưng giá trị hoặc trải nghiệm thấp.
Nguồn bằng chứng: **[RUN]** chạy thử API trên `novixa_ux_audit` · **[CODE]** file:dòng · **[GT]** giả thuyết cần kiểm với người dùng thật hoặc DB prod.

Viết tắt đường dẫn: `LR` = `src/KitPlatform.Infrastructure/Learning/LearningRepository.cs`; `STR` = `src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Infrastructure/Consultation/SymptomTaxonomyRepository.cs`.

---

## 0. Trả lời câu hỏi chính

**Novixa đã giúp chủ nhà thuốc quản trị nhân sự, giúp nhân viên nâng năng lực và giúp dược sĩ tư vấn tốt hơn đến mức nào?**

| Mục tiêu | Mức hiện tại | Lý do ngắn |
|---|---|---|
| Quản trị nhân sự | **Thấp – trung bình** | Có hồ sơ nhân viên, ca thu ngân, checklist, đánh giá tháng, ghi nhận, báo cáo thất thoát theo nhân viên. Chưa có chuỗi trách nhiệm: không chấm công, không lịch ca, không vi phạm, không KPI dữ liệu thật. Doanh số trong đánh giá gán sai người. |
| Nâng năng lực nhân viên | **Trung bình (học), thấp (chứng minh hiệu quả)** | Học → ký cam kết → quiz → chứng nhận chạy được. Không có liên kết nào từ kết quả học sang chất lượng công việc. Không chứng minh được nhân viên làm tốt hơn sau đào tạo. |
| Dược sĩ tư vấn tốt hơn | **Thấp, có rủi ro an toàn** | Chốt an toàn (thai phụ → chuyển dược sĩ) chạy đúng. Nhưng gợi ý sản phẩm dùng quy tắc **chưa được duyệt** và hiểu sai phủ định ("không sốt" → gợi ý thuốc hạ sốt). Tri thức riêng của nhà thuốc không xuất hiện ở POS. |

Kết luận: các module có đủ màn hình và API, nhưng **chưa tạo thành vòng cải tiến** Tri thức → Đào tạo → Năng lực → Công việc → Đánh giá → Bổ sung. Giá trị thực hiện nay chủ yếu ở bán hàng/kho và ở "đào tạo onboarding có quiz". Chưa thể kết luận đạt.

---

## 1. UX tổng thể theo vai trò

### Thực trạng phân quyền khi chạy thử [RUN]
DEMO có 2 tài khoản: `admin` (ADMIN) và `pharmacist` (role STAFF). Tenant có role MANAGER, BRANCH_MANAGER, CASHIER, WAREHOUSE nhưng không có người dùng.

| Năng lực | Chủ (ADMIN) | Dược sĩ (STAFF) |
|---|---|---|
| Cockpit, Thất thoát, Tăng trưởng | 200 | 403 (đúng quy tắc) |
| Học (chương trình, bài, quiz) | 200 | 200 — qua `sales.pos` (`LearningAuthorizationExtensions.cs:16-23`) |
| Roster, đánh giá, quan sát, khóa học được giao | 200 | 403 |
| Checklist ca hôm nay | 200 | 200 |
| Quy tắc tri thức (`knowledge-rules`) | 200 | 403 |
| Tri thức riêng — quản lý | 200 | 403 |
| Tri thức riêng — đang dùng | 200 (rỗng) | 200 (rỗng) |
| Tư vấn POS | 200 | 200 |

**Lưu ý vai trò:** "dược sĩ" trên DEMO dùng role STAFF, trùng với nhân viên bán hàng. Hệ thống không phân biệt dược sĩ (người có quyền chuyên môn) với nhân viên bán hàng. Trong khi đó luồng tư vấn lại dựa trên giả định "chuyển dược sĩ" (`refer_pharmacist`). **(4)** cho vai trò dược sĩ chuyên môn.

### Theo vai trò

- **Chủ nhà thuốc:**
  - Cockpit chỉ có doanh thu, trả hàng, lô, khách (`OwnerCockpitRepository.cs`). Không có mục nhân sự **(4)**.
  - Thông tin nhân sự phân tán ở 3 nơi: Thất thoát theo nhân viên, Bảng People, Đánh giá **(2)**.
  - Gate STAFF không thấy doanh thu / Cockpit được giữ đúng (`SuccessAuthorizationExtensions.cs:50-59`, `usePermission.ts:132-153`) **(1)**.
- **Quản lý:** chấm đánh giá tháng có gợi ý điểm, giao việc vận hành, quan sát tại quầy, ghi nhận. Không có màn "việc chưa xong / ai chịu trách nhiệm" **(4)**.
- **Dược sĩ:** không có vai trò riêng. Quyền duyệt tri thức (`pharmacy.knowledge.review`, `verify-source`, `platform.knowledge.publish`) **không có trong bảng permissions** của DB dựng từ manifest (51 quyền, 0 quyền "knowledge") [RUN]. Không ai vận hành được quy trình tri thức nền tảng nếu chưa chạy SQL tay.
- **Nhân viên bán hàng:** POS, tư vấn, học, checklist ca. Tri thức riêng của nhà thuốc không có trong POS **(4)**. Phác đồ nội bộ tra được trong POS qua bộ lọc theo chuỗi tiêu đề cứng (`PosSupportSale.tsx:181`) **(5)**.

### Điều hướng và nhất quán
- **Menu Learning** chỉ dựa vào quyền và cờ `audit_slim_nav`, không kiểm tra module đã bật (`AppLayout.tsx:181,243`; chú thích `audit-slim-nav.ts:8-9`). Tenant chưa bật `learning` vẫn thấy menu, nhưng mọi API trả 403 "Module is not enabled" [RUN]. **(5)**
- **Menu Tri thức** hiện theo loại hình nhà thuốc, không theo module. Trên prod thiếu bảng 416/424/429, nên trang có thể rỗng hoặc báo "chưa bật" (xem mục 2) [GT].
- **Thuật ngữ kỹ thuật** lộ với người dùng: "credential", "Evaluate", "pos_basic", "soft" (ví dụ `careerMissingReasons`: "Thiếu credential: pos_basic", "Điểm Evaluate ≥ 70") [RUN]. **(5)**
- **Luồng bán, nhập, kiểm kê, công nợ, giao ca:** đã rà và sửa ở đợt release (`NOVIXA-RELEASE-READINESS-01-REPORT.md` mục 2). Ở đó đã sửa đơn vị nhập, chặn bấm 2 lần, đóng ca không điền sẵn tiền đếm, và ẩn nút thêm kháng sinh. Còn lại: chưa có báo cáo lãi gộp, chưa kiểm tra tương tác thuốc.
- **Màn hình nhỏ:** chưa kiểm thử trên thiết bị thật trong đợt này [GT].

---

## 2. Phân hệ Tri thức (ưu tiên cao nhất)

### 2.1 Hiện trạng theo vòng đời
| Năng lực | Mức | Bằng chứng |
|---|---|---|
| Quy tắc nền tảng: nháp → duyệt → phát hành → ngừng, có phiên bản | (1) code / (2) vận hành | `PharmacyKnowledgeApproval.cs:93-110`; `416:20-38`. Quyền vận hành chưa được seed [RUN] |
| Xác minh nguồn trước khi phát hành (quy tắc nền tảng) | (1) | Sửa nội dung thì phải xác minh lại nguồn: `423:70-136` |
| Nhà thuốc chọn áp dụng (pin) phiên bản, chỉ được thu hẹp | (1) code | `PharmacyKnowledgeApproval.cs:112-142` |
| Tri thức riêng của nhà thuốc: nháp → duyệt → áp dụng / dừng | (1) code | `427:17-70,142-168`; `PharmacyOwnedKnowledgeController.cs:148-209` |
| Nhật ký sự kiện (append-only), trả lại phải có ghi chú | (1) | `424:100-116`, `427:100-111` |
| Hạn rà soát định kỳ | (4) | Không có `review_due`; chỉ có `effective_to` ở quy tắc nền tảng |
| Tìm kiếm | (5) | Tri thức riêng: `ILIKE` trên tiêu đề và phân loại, bỏ qua nội dung (`PharmacyOwnedKnowledgeService.cs:394`). Không full-text / trigram / AI |
| Nội dung thuốc / hoạt chất | (5) | Thông tin thuốc cho app khách là danh sách cứng, không nguồn (`DrugKnowledgeRules.cs:5-30`) |
| Phát hiện tri thức thiếu, lỗi thời, mâu thuẫn | (4) | Không có job hết hạn, thống kê sử dụng, phản hồi, kiểm tra mâu thuẫn |
| Liên kết với Learning | (4) | Không có tham chiếu nào giữa hai phân hệ |

### 2.2 Đã xác minh khi chạy thử [RUN]
- **Gợi ý tư vấn dùng quy tắc chưa duyệt (P0).**
  - Cả 35 quy tắc trong DB có `approval_status = PENDING_REVIEW`; số adoption = 0.
  - Vẫn có gợi ý: "Sốt - hạ sốt paracetamol OTC" từ `KR_FEVER`.
  - Nguyên nhân: khi chưa có pin, đường dự phòng đọc `pharmacy_knowledge_rule WHERE is_active = TRUE`, không xét duyệt (`STR:236-258`). Điều này trái với `414` ("Only APPROVED rules may be used by the counter copilot").
- **Hiểu sai phủ định (P0).**
  - Câu "Người lớn ho khan 2 ngày, không sốt" cho ra `symptoms: [cough_dry, fever]` cùng lúc với `hasFever: false`.
  - Tóm tắt tự mâu thuẫn ("Ho khan + Sốt … chưa ghi nhận sốt").
  - Cả 5 gợi ý đều là thuốc hạ sốt, có Decolgen (phenylephrine) mà không hỏi về huyết áp.
  - Cờ `hasFever` có xử lý phủ định (`ConsultationTextParser.cs:66-77`), nhưng bảng cụm từ thêm triệu chứng `fever` từ chữ "sot" mà không xét phủ định (`ConsultationPhraseBook.cs:83-85`).
- **Chốt an toàn thai phụ chạy đúng (1):** `safetyLevel = refer_pharmacist`, không gợi ý sản phẩm. Tuy vậy kết quả trả về `blocked = false` kèm `blockReason`, dễ khiến UI hiểu nhầm là vẫn bán được [GT].
- **Tư vấn phụ thuộc phân hệ Content (P1).**
  - Thiếu `pack_content.org_settings` thì `/consultation/extract` trả **500**.
  - Tenant không có key riêng thì dùng key Gemini của Content Park, đọc bằng `ORDER BY updated_at DESC LIMIT 1` không lọc tenant (`ContentParkGeminiKeyResolver.cs:10-44`; `PharmacyConsultationAiConfigProvider.cs:41`).
  - Mô tả sức khỏe của khách (đã khử định danh: `PharmacyConsultationService.cs:68`) có thể đi qua key của bộ phận marketing. Cần quyết định chính sách dữ liệu.
- **AI chỉ dùng để trích triệu chứng (1):** Gemini không quyết định gợi ý (`PharmacyOtcSuggestionGate.cs:5`). Có giới hạn thời gian và đường dự phòng chạy cục bộ. Không có RAG hay trích dẫn nguồn cho nhân viên **(4)**.

### 2.3 Trạng thái triển khai
- Migration 416, 417, 423–429 **không có trong manifest nào**. 414/415 chỉ có trong `pharmacy-ops-v2` [CODE].
- Thiếu bảng 416 thì truy vấn bắt lỗi và trả rỗng (`STR:262-265`): trên prod, tư vấn có thể **không có gợi ý theo quy tắc**, còn trang tri thức trả rỗng hoặc báo "chưa bật" [GT — cần kiểm DB prod].

### 2.4 Trả lời 5 câu hỏi
1. **Tìm được câu trả lời tin cậy khi đang phục vụ khách không?** Chưa. Trong POS chỉ có gợi ý theo triệu chứng (hiện dùng quy tắc chưa duyệt) và phác đồ nội bộ. Tri thức riêng không có trong POS; tìm kiếm không đọc nội dung.
2. **Hỗ trợ quyết định hay chỉ là kho tài liệu?** Quy tắc triệu chứng → sản phẩm là hỗ trợ quyết định, nhưng chất lượng chưa an toàn. Tri thức riêng hiện là kho tài liệu có quy trình duyệt.
3. **Dược sĩ đóng góp kinh nghiệm được không?** Về code thì có (nháp → duyệt, cấp quyền theo người và loại tài liệu). Nhưng không tách người viết với người duyệt (một ADMIN tự viết, tự duyệt, tự áp dụng: `Governance.cs:59-80`), và chưa có vai trò dược sĩ.
4. **Chủ nhà thuốc kiểm soát chất lượng không?** Một phần: nhật ký, duyệt, xác minh nguồn (chỉ quy tắc nền tảng). Không có hạn rà soát, không có thống kê sử dụng.
5. **Cập nhật và cải tiến liên tục không?** Không. Không có cơ chế phát hiện lỗi thời, thiếu hoặc mâu thuẫn, cũng không có phản hồi từ quầy.

---

## 3. Đào tạo và phát triển năng lực

### 3.1 Theo chu trình
| Bước | Mức | Bằng chứng |
|---|---|---|
| Hồ sơ năng lực | (2)/(5) | Mỗi mã năng lực một chứng nhận, đè lên bản cũ, không có lịch sử (`LR:533-578`). "Điểm năng lực" = tỷ lệ bài đã đạt; một số % trong UI được gán cứng (`competency-profile.ts:110-126`) |
| Xác định khoảng trống | (2) | Chỉ kiểm tra thiếu `pos_basic` và chứng nhận cho bậc tiếp theo (`LR:1345-1350,1619-1630`) |
| Lộ trình | (2) | 1 chương trình L0–L6 dùng chung toàn nền tảng. Không theo vị trí công việc. Nhân viên chỉ thấy chương trình đầu tiên (`LearningServices.cs:53-54`) [RUN: 1 chương trình, 8 bài] |
| Học, ký cam kết, quiz | (1) | [RUN] Ghi danh 200, bắt đầu 204, ký 204, nộp quiz 200. Không lộ đáp án trong payload |
| Thiết kế bài kiểm tra | (5) | [RUN] Chọn cùng một lựa chọn (thứ 2) cho cả 8 câu bài L0 → đạt 100%. Làm lại không giới hạn. Thứ tự câu cố định khi thi thật |
| Trạng thái sau khi làm lại | (2) **lỗi** | [RUN] Đạt 100% rồi làm lại sai thì bài bị hạ về `failed` nhưng vẫn giữ 3 chứng nhận. Roster: "credentialCount 3, modulesPassed 0" |
| Đánh giá thực hành (quan sát tại quầy) | (2) | Có rubric theo bậc. Lần quan sát dưới 80% không được lưu; lần mới đè lần cũ (`LearningObservationRules.cs:84-88`, `LR:2509`). Không bắt buộc để nhận chứng nhận hay lên bậc |
| Kiểm tra năng lực trước khi làm việc (gate) | (3) | API có, nhưng không client hay POS nào gọi; cấu hình ngoại lệ không có tác dụng (`LR:759`) |
| Đánh giá tháng | (1)/(5) | 5 điểm 0–100 + nhận xét + phản hồi của nhân viên [RUN 200]. Điểm gợi ý là công thức cứng, ví dụ `hasTone ? 82 : 68` (`LR:1860-1870`), nhưng UI gọi là "AI" (`LearningEvaluationsPage.tsx:188-232`) |
| Áp dụng vào công việc | (3) | Thẻ "nhiệm vụ áp dụng" tĩnh, không ghi nhận việc làm thật (`LearningTakeModulePage.tsx:659-684`) |
| Đo tiến bộ | (4) | Chỉ có số liệu tháng hiện tại. Không so trước/sau với trả hàng, chênh tiền, sửa giá, chất lượng tư vấn, đánh giá của khách |

### 3.2 Câu hỏi trọng tâm
**Hệ thống có chứng minh được nhân viên làm tốt hơn sau đào tạo không? — Không.** Không có dữ liệu so sánh trước/sau đào tạo, và không có chỉ số vận hành nào được gắn với từng bài học. Chỉ số "Team Health" là hằng số cứng (`LearningProgramsPage.tsx:262-293`).

### 3.3 Nội dung
- Nội dung do nền tảng soạn (`tenant_id NULL`). Nhà thuốc chỉ sửa được tiêu đề, tóm tắt và nội dung bài, không thêm được bài hay câu hỏi (`LR:163-210`) **(5)**.
- Thời lượng, nhiệm vụ áp dụng và gợi ý kèm cặp nằm cứng ở frontend (`learning-module-meta.ts:23-175`), dễ lệch với nội dung trong DB.

---

## 4. Quản trị nhân sự (ưu tiên cao nhất)

| Năng lực | Mức | Bằng chứng |
|---|---|---|
| Hồ sơ nhân viên tách khỏi tài khoản đăng nhập | (1) | `002_identity.sql:34-62`; `UserFormDrawer.tsx:337-485` |
| Chức danh / vị trí | (4) | Không có trường; `hire_date` có trong DB nhưng form không hiển thị |
| Ca thu ngân: ai mở / đóng, chênh tiền | (1) | `015_sales_shifts.sql:7-15` |
| Chênh tiền gán cho người chịu trách nhiệm | (2) | Danh sách chênh tiền ở Thất thoát không ghi người mở / đóng (`LossPreventionRepository.cs:53-66`) |
| Lịch ca, chấm công | (4) | [RUN] Không có bảng attendance / timesheet / schedule |
| Checklist đầu / cuối ca, người tích từng mục | (1) | `132_success_shift_checklist.sql:29-60`. [RUN] 2 mẫu, 12 mục. Hôm nay: chưa có lượt chạy |
| Checklist theo từng ca | (2) | Cần mig 412, chỉ có trong manifest `pharmacy-ops-v2` và `staff-enablement-v1` |
| Giao việc: người làm, hạn, bằng chứng | (3) | Mig 413 không có trong manifest prod. Không có cờ quá hạn hay nhắc nhở (`OperationalTaskService.cs:73`) |
| Thất thoát theo nhân viên (hủy đơn, giảm giá, điều chỉnh kho) | (1) | `LossPreventionRepository.cs:117-233` |
| Doanh số trong đánh giá | **(5) sai dữ liệu** | Tính cho **người mở ca** thay vì người bán (`LR:1838-1845`), dù `sales_orders.employee_id` đã có người bán (`SalesRepository.cs:3037,3074`) |
| Đánh giá định kỳ / KPI | (2) | Điểm tay có gợi ý. Điểm thái độ và chăm sóc suy ra từ badge đào tạo, không từ hành vi. Ngày kỷ luật = có tích ít nhất một mục checklist; ranh giới tháng dùng UTC (`LR:1787-1801`) |
| Thái độ, chất lượng phục vụ | (2) | Có bảng đánh giá của khách sau mua 1–5 sao (`183:35-46`); chưa thấy luồng thu tại quầy |
| Chất lượng tư vấn | (4) | Có lưu phiên tư vấn, nhưng không gắn với đánh giá nhân viên |
| Khen thưởng | (1) | Ghi nhận và badge (`161`, `186`). Badge `zero_error_shift` trao tay, không tính từ chênh tiền |
| Xử lý vi phạm | (4) | Không có |
| Lương, phụ cấp, thưởng | (4) | Không có |
| Góp ý riêng tư | (2) **rủi ro** | Góp ý đăng `isPublic: true` (`LearningRecognizePage.tsx:604`), ai có quyền Read cũng xem được |

**Trả lời các câu kiểm tra:**
- **Trách nhiệm theo nhân viên và theo ca?** Một phần: ai mở / đóng ca và ai tích mục checklist có ghi nhận. Chênh tiền không gán người; không có lịch ca.
- **Biết việc nào chưa xong, ai chịu?** Chỉ có trong danh sách việc. Không có cảnh báo quá hạn, không có tổng hợp trong Cockpit.
- **Đánh giá có bằng chứng không?** Bán bằng chứng. Gợi ý lấy tiến độ học, số ngày checklist, số đơn (đơn gán sai người). Thái độ và chăm sóc vẫn cảm tính.
- **Liên kết với đào tạo?** Có, một chiều: đánh giá đọc kết quả học. Đánh giá không đẩy ngược bài học cần học.
- **Gánh nặng nhập liệu?** Mỗi tháng 5 điểm × N nhân viên, checklist mỗi ca, giao việc tay, ghi nhận tay. Lịch ca, chấm công, lương vẫn phải làm ngoài hệ thống [GT].

---

## 5. Liên kết ba phân hệ

```
Tri thức ──✗──> Đào tạo ──✓──> Năng lực (chứng nhận) ──△──> Công việc (POS/ca)
    ↑                                                          │
    ✗                                                          △
    │                                                          ↓
Bổ sung ✗── Phát hiện khoảng trống ✗── Đánh giá (tháng) <──△──┘
```
✓ chạy được · △ có một phần / gán sai · ✗ chưa có.

| Liên kết | Trạng thái |
|---|---|
| Tri thức → Đào tạo | ✗ Không có tham chiếu |
| Đào tạo → Chứng nhận | ✓ Quiz đạt → chứng nhận [RUN] |
| Chứng nhận → Công việc | ✗ Có API gate nhưng không ai gọi; POS không biết năng lực nhân viên |
| Công việc → Đánh giá | △ Chỉ số đơn (gán sai người) và số ngày checklist; không có trả hàng, chênh tiền, tư vấn |
| Đánh giá → Khoảng trống | △ Chỉ "thiếu pos_basic" và điều kiện lên bậc |
| Khoảng trống → Bổ sung tri thức / đào tạo | ✗ Không có gợi ý bài học theo điểm yếu, không có phản hồi về tri thức |
| Tư vấn → Tri thức | ✗ Phiên tư vấn không được dùng để phát hiện tri thức thiếu |

---

## 6. Vì sao nhà thuốc khó nhận được giá trị

1. Tư vấn hiện **không đáng tin**: dùng quy tắc chưa duyệt, hiểu sai câu phủ định.
2. Tri thức riêng không có ở nơi nhân viên cần (POS). Tìm kiếm yếu.
3. Không ai vận hành được quy trình tri thức nền tảng vì quyền chưa được tạo.
4. Không có vai trò dược sĩ, nên "chuyển dược sĩ" không đến được đúng người trong hệ thống.
5. Học xong không đổi gì ở quầy. Quiz dễ đoán; trạng thái học tự mâu thuẫn.
6. Chủ không có một màn "nhân sự hôm nay": ai làm, việc gì chưa xong, chênh tiền của ai.
7. Đánh giá trông như dữ liệu nhưng dựa vào hằng số và badge; doanh số gán sai người.
8. Nhiều tính năng phụ thuộc migration chưa có trong manifest prod; menu hiện nhưng trang trống hoặc 403.
9. Thuật ngữ kỹ thuật lộ ra giao diện.

---

## 7. Danh sách thiếu sót và đề xuất (đã phân ưu tiên)

### P0 — an toàn, bảo mật, sai nghiệp vụ nghiêm trọng
| # | Vấn đề | Bằng chứng | Đề xuất |
|---|---|---|---|
| P0-1 | Gợi ý POS dùng quy tắc chưa duyệt | `STR:236-258`; [RUN] 35/35 `PENDING_REVIEW`, 0 adoption, vẫn gợi ý | Đường dự phòng chỉ dùng `approval_status = 'APPROVED'`; không có quy tắc duyệt thì không gợi ý sản phẩm, chỉ hiện câu hỏi sàng lọc |
| P0-2 | "không sốt" → gợi ý thuốc hạ sốt | `ConsultationPhraseBook.cs:83-85` và `ConsultationTextParser.cs:66-77`; [RUN] | Xử lý phủ định ở bảng cụm từ; bỏ triệu chứng mâu thuẫn với cờ; viết test cho các câu phủ định thường gặp |
| P0-3 | Gợi ý thuốc có phenylephrine mà không sàng lọc huyết áp / bệnh nền | [RUN] gợi ý Decolgen cho ho khan | Thêm câu hỏi sàng lọc bắt buộc với nhóm hoạt chất có cảnh báo trước khi hiện sản phẩm |

### P1 — cần cải thiện trước khi coi sản phẩm hoàn thiện
| # | Vấn đề | Bằng chứng |
|---|---|---|
| P1-1 | Migration tri thức 416, 417, 423–429 và vận hành 412, 413 không có trong manifest prod | `deploy/ubuntu/migration-files.*.txt` |
| P1-2 | Quyền duyệt / xác minh / phát hành tri thức chưa được seed | [RUN] 0 quyền "knowledge"; `PharmacyKnowledgeApproval.cs:15-21` |
| P1-3 | Không tách người viết với người duyệt (tri thức riêng, phác đồ) | `Governance.cs:59-80` |
| P1-4 | Tư vấn phụ thuộc cứng `pack_content`; dùng key Gemini của Content Park cho dữ liệu sức khỏe | [RUN] 500; `ContentParkGeminiKeyResolver.cs:10-44` |
| P1-5 | Doanh số đánh giá gán cho người mở ca | `LR:1838-1845` |
| P1-6 | Làm lại quiz sau khi đạt bị hạ trạng thái; chứng nhận và trạng thái mâu thuẫn | [RUN] |
| P1-7 | Quiz dễ đoán (đáp án trùng vị trí), làm lại không giới hạn | [RUN] |
| P1-8 | Góp ý cá nhân đăng công khai | `LearningRecognizePage.tsx:604` |
| P1-9 | Điểm gợi ý và "Team Health" là hằng số nhưng gọi là "AI" | `LR:1860-1870`; `LearningProgramsPage.tsx:262-293` |
| P1-10 | Không có vai trò dược sĩ chuyên môn; `refer_pharmacist` không gắn người nhận | [RUN] role list |
| P1-11 | Menu Learning hiện khi module chưa bật (403) | `AppLayout.tsx:181,243`; [RUN] |
| P1-12 | Chênh tiền không gán người chịu trách nhiệm | `LossPreventionRepository.cs:53-66` |

### P2 — cải tiến quan trọng giai đoạn tới
- Đưa tri thức riêng vào POS; tìm kiếm cả nội dung (full-text tiếng Việt / trigram).
- Thêm hạn rà soát cho tri thức riêng và phác đồ; nhắc khi tới hạn; thống kê lượt dùng.
- Lưu cả lần quan sát không đạt; bắt buộc quan sát để lên bậc.
- Xáo trộn câu hỏi và đáp án khi thi thật; lưu kết quả từng câu để biết điểm yếu.
- Checklist: ngày kỷ luật phải đủ các mục bắt buộc; ranh giới tháng theo giờ Việt Nam.
- Giao việc: cờ quá hạn, tổng hợp "chưa xong / ai" trong Cockpit.
- Hiển thị trình soạn ghi chú sức khỏe khách (đã có backend, chưa render).
- Sửa thuật ngữ kỹ thuật trên UI.
- Thêm `blocked = true` (hoặc trạng thái rõ ràng) khi cần chuyển dược sĩ.

### P3 — định hướng dài hạn
- Lịch ca, chấm công, sổ vi phạm, chức danh; tính lương / thưởng cơ bản (nếu khách cần — xem GT).
- RAG có trích dẫn nguồn trên tri thức đã duyệt, chỉ hỗ trợ dược sĩ.
- Nhà thuốc tự soạn bài học / câu hỏi từ tri thức riêng.
- Phát hiện mâu thuẫn giữa tri thức riêng và quy tắc nền tảng.

---

## 8. Tiêu chí đo giá trị thực tế cho nhà thuốc

| Mục tiêu | Chỉ số | Nguồn dữ liệu hiện có / cần thêm |
|---|---|---|
| Tư vấn an toàn | % phiên tư vấn có cờ an toàn được chuyển dược sĩ và có người xác nhận | Phiên tư vấn (có) + người xác nhận (cần) |
| Tư vấn đúng | % gợi ý được nhân viên chọn; % đơn có trả hàng trong 7 ngày sau tư vấn | Phiên tư vấn + `link-order` + trả hàng (có) |
| Tri thức dùng được | Thời gian tìm được câu trả lời tại POS; % tri thức còn hạn rà soát; lượt dùng / tài liệu | Cần thêm log tra cứu và hạn rà soát |
| Đào tạo có hiệu quả | Thay đổi trước/sau 30 ngày của: sai giá, hủy đơn, trả hàng, chênh tiền, đánh giá của khách — theo nhân viên và theo bài học | Có dữ liệu vận hành; cần gắn theo `sales_orders.employee_id` và thời điểm đạt bài |
| Năng lực | % nhân viên đạt bậc theo vị trí; % có quan sát tại quầy đạt | Có (cần sửa P1-6) |
| Trách nhiệm | % ca có checklist đủ mục bắt buộc; chênh tiền theo người đóng ca; việc quá hạn theo người | Có một phần; cần gán người |
| Gánh nặng quản lý | Thời gian / tháng chủ dùng cho đánh giá nhân sự | Cần đo với người dùng thật |

---

## 9. Roadmap cải tiến (theo thứ tự)

**Bước 1 — An toàn tư vấn (1–2 tuần, làm trước mọi việc khác):** P0-1, P0-2, P0-3; tách cấu hình AI của Pharmacy khỏi Content (P1-4); có test hồi quy cho câu phủ định và nhóm nguy cơ.

**Bước 2 — Cho phân hệ đã có chạy được trên prod (1–2 tuần):** P1-1 (đưa mig 412/413/416–429 vào manifest có chủ đích, kèm kiểm tra chỉ-đọc DB prod trước), P1-2 (seed quyền tri thức), P1-10 (vai trò dược sĩ), P1-11 (menu theo module), P1-3 (tách người viết / duyệt).

**Bước 3 — Số liệu nhân sự đúng và trung thực (2 tuần):** P1-5, P1-6, P1-7, P1-8, P1-9, P1-12; bỏ nhãn "AI" ở chỗ là công thức; ngày kỷ luật đủ mục bắt buộc.

**Bước 4 — Đưa tri thức tới quầy (3–4 tuần):** tri thức riêng trong POS, tìm kiếm nội dung, hạn rà soát, log tra cứu, phản hồi "không tìm thấy" từ quầy.

**Bước 5 — Khép vòng (4–6 tuần):** gắn bài học với chỉ số vận hành theo người bán; báo cáo trước/sau đào tạo; gợi ý bài học theo điểm yếu (từ câu quiz sai, quan sát, chênh tiền, trả hàng); màn "nhân sự hôm nay" cho chủ.

**Bước 6 — Dài hạn:** lịch ca / chấm công / vi phạm (nếu GT xác nhận nhu cầu), RAG có trích dẫn cho dược sĩ, nhà thuốc tự soạn bài học.

---

## 10. Giả thuyết cần kiểm với người dùng thật hoặc DB prod

1. DB prod có bảng 412/413/414/416–429 chưa (chạy kiểm tra chỉ-đọc). Điều này quyết định tư vấn trên prod có gợi ý hay không.
2. Mỗi nhân viên đăng nhập tài khoản riêng ở POS hay dùng chung. Nếu dùng chung, mọi chỉ số theo nhân viên đều sai.
3. Chủ nhà thuốc 3–8 nhân viên có chấm đánh giá hằng tháng không, hay cần kiểm tra nhẹ theo tuần.
4. Người mở ca có thường là người đóng ca và người chịu trách nhiệm tiền không.
5. Khách có để lại đánh giá sau mua không.
6. Nhân viên có dùng trang tra cứu riêng trong lúc bán không, hay cần mọi thứ ngay trong POS.
7. Thiếu lịch ca / chấm công / lương có phải lý do không dùng, hay đã làm bằng Excel / Zalo.
8. Nhân viên có ngại góp ý bị công khai không.
9. Tenant thật (`NT_XUANHOA`) có bật `learning` chưa; nếu chưa, menu Learning đang hiện mà không dùng được.

---

## Phụ lục — Cách kiểm thử

- DB `novixa_ux_audit` (cổng 5436) dựng bằng `scripts/ci/build-pharmacy-test-db.ps1`. Trên DB này đã bật thêm module `learning` cho DEMO và tạo bảng rỗng `pack_content.org_settings`; cả hai thay đổi đều có chốt `current_database()`.
- API chạy cục bộ (cổng 5392), tắt mọi worker và mọi kết nối ra ngoài; không có key Gemini nên tư vấn chạy cục bộ (`extractionSource = local_fast`).
- Script: `.tmp/release-20261104/ux-probe.ps1`, `ux-learning-flow.ps1`, `ux-flow2.ps1`, `ux-flow3.ps1`.
- Rà code chi tiết bởi 3 agent chỉ-đọc (Tri thức, Đào tạo, Nhân sự). Các phát hiện P0/P1 chính đã được kiểm tra lại trực tiếp trong code hoặc bằng chạy thử.
