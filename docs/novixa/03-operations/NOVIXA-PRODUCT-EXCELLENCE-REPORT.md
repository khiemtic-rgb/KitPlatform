# Novixa — Product Excellence & Operational Readiness Report

Ngày: 2026-11-04 (UTC+7). Repo `E:\KitPlatform`, nhánh `feat/famixa-linesheet-build-m1` (HEAD `dbd96772`), nguồn chuẩn `origin/main` `7505e016`.
Phạm vi: "MASTER MISSION: NOVIXA — PRODUCT EXCELLENCE & OPERATIONAL READINESS".
Đầu vào: `NOVIXA-PRODUCT-EXPERIENCE-INTELLIGENCE-AUDIT-01.md`, `NOVIXA-RELEASE-READINESS-01-REPORT.md`. Tiến độ giữa chừng: `NOVIXA-PRODUCT-EXCELLENCE-CHECKPOINT-01.md`.

**Kết luận: `NOVIXA_PRODUCT_EXCELLENCE_NOT_READY`.**

- **Mã đã làm và đã kiểm thử trên DB cô lập:** build backend 0 lỗi, `npm run build` admin sạch.
  - Api 45/45, Application 198/198.
  - Tư vấn và tri thức (Platform) 30/30, chạy được với quy trình CI mới.
  - Kiểm thử đầu-cuối đạt.
- **Chưa đủ điều kiện phát hành**, vì còn các việc ở mục 4:
  - Chưa commit hay tách nhánh phát hành, CI chưa chạy trên remote.
  - Chưa có phê duyệt deploy đêm và smoke sau deploy.
  - Còn hai rủi ro người dùng đã chấp nhận, phải xử lý trước bước kế tiếp của tri thức.

Không commit, push, deploy hay đụng Production. Không đụng `NT_XUANHOA` hay DB dev `kitplatform`.

---

## 1. Đã làm theo từng trụ cột

### A. Nghiệp vụ lõi và bảo mật
- **Kế thừa từ đợt release readiness:**
  - P0 (rate limit đăng nhập/OTP, ForwardedHeaders, bỏ in URI có mật khẩu);
  - P1 tồn kho, bán hàng, mua hàng (INV-01/02/03, SAL-01/02/03, PRC-01/02).
- **Cổng doanh thu không regress:**
  - "Nhân sự hôm nay" trong Owner Cockpit (`GET api/success/owner-cockpit/staff-today`, `SuccessPolicies.Owner`) không trả trường tiền nào; kiểm thử thật: admin 200, nhân viên 403, không có khóa doanh thu.
  - Doanh thu tháng ở màn bằng chứng đánh giá chỉ trả cho `reports.read`, `reports.export` hoặc ADMIN (`LearningController.EmployeeEvidence`).
  - Báo cáo khoảng trống đào tạo chỉ dùng số đơn, không có tiền.

### B. Trải nghiệm theo vai trò
- **Nhân viên (STAFF):**
  - Ca cần dược sĩ không hiện danh sách thuốc. Màn hỗ trợ bán (`PosSupportSale.tsx`) giờ ghi rõ lý do ("Không gợi ý thuốc" + `blockReason`) thay vì để trống.
  - Câu hỏi bắt buộc "Hỏi khách trước khi bán" hiện trong `PosConsultationModal.tsx`.
- **Dược sĩ phụ trách:** quyền mới `pharmacy.consultation.pharmacist` (mig 436, chưa gán role nào). Chủ nhà thuốc gán cho đúng người.
- **Quản lý/chủ:**
  - thẻ "Khoảng trống đào tạo" trong Tổng quan đội ngũ (chỉ hiện khi có quyền ghi Learning);
  - việc quá hạn, chênh tiền theo người đóng ca, chức danh và ngày vào làm.
- **Menu:** mục Learning chỉ hiện khi tenant bật module `learning`.

### C. Tri thức và an toàn tư vấn
| Yêu cầu | Trạng thái | Bằng chứng |
|---|---|---|
| Hiểu phủ định ("không sốt", "ko ho", "chưa sốt"…) | Xong | `ConsultationTextParser.ScanNegations`; đầu-cuối: "Người lớn ho khan 2 ngày, không sốt" ra `hasFever=false`, chỉ còn `cough_dry` |
| Không dùng tri thức chưa duyệt | Xong | Fallback chỉ lấy `approval_status='APPROVED'` (`SymptomTaxonomyRepository.cs`); bỏ kệ gợi ý viết cứng (`ShelvesFor`) |
| Câu hỏi sàng lọc theo hoạt chất | Xong (ở code) | `ConsultationProductScreening.cs` (co mạch, NSAID, paracetamol, kháng histamin gây ngủ, domperidon, loperamid) |
| Chặn hoặc chuyển dược sĩ | Xong | `refer_pharmacist` chỉ người có quyền dược sĩ thấy thuốc; `refer_medical` không ai thấy; đầu-cuối thai phụ: STAFF 0 thuốc |
| AI tách khỏi Content Park | Xong | `PharmacyConsultationAiConfigProvider.cs`, `TenantSettingsService.cs` |
| Nguồn, tách người viết và người duyệt, hạn rà soát, phản hồi từ quầy | Xong | mig 432, 433; `PharmacyKnowledgeOperations.cs`; 29 test |

### D. Chu trình đào tạo
- **Quiz:** làm lại sai không hạ trạng thái đã đạt, giữ điểm tốt nhất; xáo câu phía server.
- **Ôn tháng:** chấm theo tổng số câu, kỳ tính theo giờ VN.
- **Quan sát tại quầy:** lưu cả lần không đạt (mig 435).
- **Góp ý riêng tư:** chặn ở backend.
- **Số liệu:** bỏ số giả trên Team Health và hồ sơ năng lực; điểm gợi ý có ghi căn cứ.
- **Mới đợt này:** mig `437_learning_quiz_attempt_history.sql` lưu mọi lần làm bài và từng câu trả lời (cả bài luyện tập). `question_id` không có khóa ngoại, để các mig nội dung xóa và seed lại câu hỏi không làm mất lịch sử.

### E. Nhân sự gọn nhẹ
- **Doanh số theo người bán:** tính theo `sales_orders.employee_id`, không theo người mở ca.
- **Chênh tiền:** ghi người mở và đóng ca, có bảng theo người đóng ca.
- **Việc vận hành:** có hạn, cờ quá hạn và bộ lọc.
- **Nhân sự hôm nay** trong Cockpit.
- **Chức danh và ngày vào làm** (mig 434).

### F. Vòng hệ thống (Tri thức → Đào tạo → Năng lực → Công việc → Kết quả → Đánh giá → Khoảng trống → Tri thức)
`GET api/learning/gaps?days=90` (quyền `learning.write`) cùng thẻ giao diện `LearningGapReportCard.tsx`:
- **Câu hay sai:** từ 3 lượt trả lời, sai từ 40% trở lên. Gợi ý quản lý xem lại nội dung bài, vì nhiều người cùng sai thường là bài viết chưa rõ. Đây là đường quay lại Tri thức.
- **Ai cần kèm thêm:**
  - trượt bài kiểm tra mà chưa đạt;
  - quan sát tại quầy không đạt mà chưa có lần đạt sau.
  - Mỗi dòng kèm việc nên làm.
- **Trước/sau khi đạt bài:** số đơn hoàn tất trung bình mỗi ngày, 30 ngày trước so với tối đa 30 ngày sau (cần ít nhất 7 ngày). Chỉ là tín hiệu tham khảo, không có tiền.
- **Luật thuần:** `LearningGapRules.cs`, 14 test.
- **Kiểm thử đầu-cuối:** 3 lần trượt bài L0 → báo cáo hiện 5 câu hay sai và tín hiệu "Chưa đạt bài kiểm tra" cho đúng nhân viên; nhân viên gọi API bị 403.

### G. Hiệu năng, manifest, CI
- **Hiệu năng báo cáo:** index mig 431 (số đo trước/sau trong `NOVIXA-RELEASE-READINESS-01-REPORT.md`). Truy vấn mới có index: `quiz_attempt` (tenant, module) và (tenant, employee); `quiz_attempt_answer(question_id)`.
- **Manifest:**
  - `migration-files.pharmacy-knowledge-v1.txt`: 416, 417, 423–429, 432, 433, **436**.
  - `migration-files.staff-enablement-v1.txt`: 412, 413, 435, 434, **437**.
  - Không thêm gì vào `migration-files.prod.txt`.
  - Mig 436 và 437 đã thử: áp được trên DB mới, chạy lại vẫn sạch.
- **CI:** `novixa-pharmacy-db-tests.yml` thêm bước test an toàn tư vấn.
  - Script mới `scripts/ci/build-platform-isolated-db.ps1` dựng DB không có tenant: chép schema và 12 bảng tham chiếu `pharmacy_*`.
  - Script mới `scripts/ci/grant-isolated-test-role.ps1` tạo role hạn chế `novixa_iso`.
  - Đã chạy thử toàn bộ quy trình trên máy: 30/30.

---

## 2. Kiểm thử

Tất cả chạy trên cluster cô lập `127.0.0.1:5436`.

| Bộ | DB | Kết quả |
|---|---|---|
| `dotnet build KitPlatform.slnx` | — | 0 lỗi |
| Admin `npm run build` (có `tsc`) | — | sạch |
| Api.Tests (lọc như CI) | `novixa_ci_api` | 45/45 |
| Application.Tests (lọc như CI) | `novixa_ci_app` | 198/198 |
| Platform.Tests (Consultation, OTC gate, W5) | `novixa_release_iso` và `novixa_ci_plat_iso` (dựng như CI) | 30/30 |
| Kiểm thử đầu-cuối (`.tmp/release-20261104/ux-e2e-excellence.ps1`, API 5392, `GEMINI_API_KEY` rỗng) | `novixa_ux_audit` | đạt (mục 1C, 1F; Cockpit 200/403) |

**Test bị loại khỏi lọc**, đã ghi nhận:
- `PharmacyKnowledgeExperienceApiTests`: fixture viết cho schema tự tạo, nay đã cũ.
- `PharmacyOwnedKnowledgeM1Tests`: tìm một chuỗi không còn (lỗi có từ trước).
- `InventoryMovementSummaryTests`: cần DB riêng ở cổng 5433.
- TestPack và Local OS: cần manifest riêng.

---

## 3. Quyết định đã chốt và rủi ro chấp nhận

1. **Không có gợi ý OTC cho tới khi có người duyệt** (người dùng chọn 2026-11-04).
   - Chỉ quy tắc `APPROVED` hoặc phiên bản đã pin mới được dùng. Hiện chưa ai có quyền duyệt hay phát hành quy tắc nền tảng.
   - Sau khi phát hành bản tri thức, **mọi nhà thuốc (kể cả Xuân Hòa) không có danh sách thuốc gợi ý**. Cảnh báo an toàn, câu hỏi sàng lọc và tri thức riêng của nhà thuốc vẫn chạy.
   - Màn hình báo: "Chưa có quy tắc gợi ý đã được duyệt cho triệu chứng này — dược sĩ tư vấn trực tiếp."
   - Quyết định này thay cho lựa chọn "giữ fallback" ở đợt trước.
   - **Cần báo cho Xuân Hòa trước khi deploy.**
2. **Lỗ hổng phân quyền nền tảng: để sau** (người dùng chọn).
   - Tenant mới được chép mọi quyền cho ADMIN (`PlatformTenantRepository.cs` ~239-247).
   - Tenant admin gán được bất kỳ mã quyền nào (`IdentityAdminRepository.UpdateRolePermissionsAsync` ~608-640).
   - Hiện chưa khai thác được, vì `platform.*`, `pharmacy.knowledge.review` và `pharmacy.knowledge.verify-source` chưa có trong bảng `permissions`.
   - **Điều kiện cứng: phải chặn hai đường này trước khi seed bất kỳ quyền nền tảng nào.**
3. **Câu hỏi sàng lọc đang viết trong code** (`ConsultationProductScreening.cs`). Đúng hướng là chuyển vào hệ tri thức có duyệt.
4. **`GEMINI_API_KEY` ở biến môi trường server** vẫn là nguồn dự phòng cho trích xuất tư vấn. Cần quyết định chính sách dữ liệu sức khỏe trước khi bật cho tenant thật.

---

## 4. Việc còn lại trước khi đạt READY

| # | Việc | Ai |
|---|---|---|
| 1 | Tách thay đổi thành các nhánh/PR phát hành từ `origin/main`: Pharmacy tư vấn và tri thức; Staff enablement (Learning, HR); CI. Không gộp với Famixa/Content (working tree đang trộn) | Cursor, khi người dùng bảo |
| 2 | CI xanh trên remote (workflow mới chưa từng chạy trên GitHub runner; client `pg_dump` phải cùng hoặc mới hơn server 16) | Cursor sau khi push |
| 3 | Phê duyệt deploy đêm; chạy manifest pharmacy-ops-v2 → pharmacy-knowledge-v1 → staff-enablement-v1 | Người dùng duyệt |
| 4 | Smoke sau deploy: `DEMO_PHARMACY` admin + pharmacist; xác nhận `NT_XUANHOA` đăng nhập và bán được; báo trước việc tắt gợi ý OTC | Người dùng và Cursor |
| 5 | Gán `pharmacy.consultation.pharmacist` cho dược sĩ phụ trách từng nhà thuốc (không làm bằng seed) | Chủ nhà thuốc |

**Tồn đọng không chặn phát hành:**
- `COMPETENCY_CATALOG_SIZE = 24` đang viết cứng.
- `GetObservationRowAsync` có thể trả trùng dòng.
- Xáo câu quiz chưa có khóa bí mật.
- Fixture `PharmacyKnowledgeExperienceApiTests` đã cũ.
- Mức "trước" của báo cáo trước/sau luôn chia 30 ngày, nên nhân viên mới vào sẽ thấy thấp hơn thực tế.
- Câu hỏi hay sai chưa nối tự động tới mục tri thức nguồn (quản lý mở bài để sửa).
- Trích xuất nhanh chưa đọc "người lớn" hay "2 ngày" thành tuổi/thời gian (vẫn hỏi lại qua gợi ý thiếu thông tin).

---

## 5. File thay đổi trong đợt này

Ngoài các file đã liệt kê trong checkpoint:
- **Mig mới:** `migrations/437_learning_quiz_attempt_history.sql`.
- **Manifest:** `deploy/ubuntu/migration-files.pharmacy-knowledge-v1.txt` (thêm 436), `deploy/ubuntu/migration-files.staff-enablement-v1.txt` (thêm 437).
- **Backend:**
  - `src/KitPlatform.Infrastructure/Learning/LearningRepository.cs` (`InsertQuizAttemptAsync`, `GetGapReportAsync`);
  - mới `LearningGapRules.cs`; `LearningServices.cs`;
  - `src/KitPlatform.Application/Learning/LearningContracts.cs`;
  - `src/KitPlatform.Api/Controllers/Learning/LearningController.cs` (`GET gaps`);
  - `PharmacyConsultationService.cs` (câu báo khi chưa có quy tắc duyệt).
- **Frontend:**
  - `client/admin/src/shared/api/learning.api.ts`;
  - mới `client/admin/src/modules/learning/LearningGapReportCard.tsx`;
  - `LearningPeopleDashboardPage.tsx`;
  - `client/admin/src/modules/sales/PosSupportSale.tsx`.
- **Test:** mới `tests/KitPlatform.Application.Tests/LearningGapRulesTests.cs`; `NovixaW5TenantKnowledgeIsolatedTests.cs` (chuỗi thông báo).
- **CI:** `.github/workflows/novixa-pharmacy-db-tests.yml`, mới `scripts/ci/build-platform-isolated-db.ps1`, mới `scripts/ci/grant-isolated-test-role.ps1`, `scripts/ci/build-pharmacy-test-db.ps1` (thêm 437).

`NOVIXA_PRODUCT_EXCELLENCE_NOT_READY`
