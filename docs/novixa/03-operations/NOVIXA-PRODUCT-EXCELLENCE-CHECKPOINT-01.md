# Novixa Product Excellence — Checkpoint 01

> **Đã thay bằng báo cáo cuối** `NOVIXA-PRODUCT-EXCELLENCE-REPORT.md` (2026-11-04, chiều). Mục 2–4 dưới đây là trạng thái lúc 13:00; người dùng đã chọn "không gợi ý OTC tới khi có người duyệt" và "để sau" lỗ hổng phân quyền nền tảng.

Ngày: 2026-11-04, 13:00 (UTC+7). Dừng theo yêu cầu người dùng (tắt máy).
Nhiệm vụ: "MASTER MISSION: NOVIXA — PRODUCT EXCELLENCE & OPERATIONAL READINESS".
Báo cáo cuối (chưa viết): `docs/novixa/03-operations/NOVIXA-PRODUCT-EXCELLENCE-REPORT.md`.
Nền tảng đánh giá: `docs/novixa/03-operations/NOVIXA-PRODUCT-EXPERIENCE-INTELLIGENCE-AUDIT-01.md`.

**Trạng thái:**
- Chưa commit, push hay deploy. Không đụng Production, `NT_XUANHOA` hay DB dev `kitplatform`.
- Mọi thay đổi nằm trên đĩa, trong working tree của nhánh `feat/famixa-linesheet-build-m1`. Working tree còn có thay đổi của các đợt trước (release readiness) và thay đổi Famixa/Content của người dùng; không được reset, stash hay clean.
- Một API dev (PID 16020, `dotnet run --no-build` từ `src/KitPlatform.Api/bin/Debug`) khởi động lúc 12:20 đang giữ file trong `bin`. Không phải do đợt này chạy, nên tôi không tắt. Mọi lệnh build/test dùng `--artifacts-path .tmp/artifacts-ws1`.

---

## 1. Đã hoàn thành

### A. An toàn tư vấn (P0) — xong, có test
| Việc | File chính |
|---|---|
| Đường fallback chỉ lấy quy tắc `approval_status = 'APPROVED'` | `src/Packs/Pharmacy/.../Consultation/SymptomTaxonomyRepository.cs` (~251) |
| Bỏ "kệ gợi ý" viết cứng (`ShelvesFor`: Cảm→Decolgen, Đau đầu→hoạt huyết…) vì chưa qua duyệt | `PharmacyConsultationService.cs` |
| Xử lý phủ định theo token ("không/ko/k/chưa/hết/khỏi" + "bị/có/còn" + cụm triệu chứng; nối "hay/hoặc"). Cụm báo động (khó thở…) cố ý không phủ định | `ConsultationTextParser.cs` (`ScanNegations`, `DropNegatedSymptoms`, `ApplyTextSymptoms`), `ConsultationPhraseBook.cs` (`All`) |
| `HasFever = false` thì loại triệu chứng `fever`; text phủ định thắng kết quả model, nhưng chip nhân viên chọn thắng text | `ConsultationSafetyRules.cs` (`NormalizeFacts`), `ConsultationTextParser.cs` |
| Câu hỏi sàng lọc bắt buộc theo hoạt chất (co mạch, NSAID, paracetamol, kháng histamin gây ngủ, domperidon, loperamid) → cờ `screen_*` mức `caution` | mới `ConsultationProductScreening.cs`; UI `client/admin/src/modules/sales/consultation-screening.ts` + khung "Hỏi khách trước khi bán" trong `PosConsultationModal.tsx` |
| Ca `refer_pharmacist` chỉ ADMIN hoặc quyền `pharmacy.consultation.pharmacist` mới xem danh sách thuốc (trước: trang hỗ trợ bán luôn gửi `keepOrderWithReferral: true`); `refer_medical` không ai xem | `PharmacyConsultationService.cs`, mới `Application/Consultation/PharmacyConsultationPermissions.cs`, mig mới `436_pharmacy_consultation_pharmacist_permission.sql` (chỉ thêm quyền, không gán role) |
| AI tư vấn không còn dùng key Gemini của Content Park | `PharmacyConsultationAiConfigProvider.cs`, `TenantSettingsService.cs` |
| Test | mới `tests/KitPlatform.Platform.Tests/Pharmacy/ConsultationNegationAndScreeningTests.cs` (13/13 xanh); `NovixaOtcSafetyGateIsolatedTests` mở lại test từng skip "accepted risk" → Otc + W5 17/17 xanh trên `novixa_release_iso` |

Quyết định đã chốt: lần trước người dùng chọn giữ fallback; mission này yêu cầu "không dùng tri thức chưa duyệt", nên đổi sang chỉ lấy quy tắc đã duyệt. Hệ quả, xem mục 3 câu 1.

### B. Đào tạo — agent [Đào tạo](601ff75a-d5b9-4dee-a24f-c0378240edb0)
- **Bằng chứng đánh giá:**
  - doanh số theo `sales_orders.employee_id`, không theo người mở ca;
  - ngày kỷ luật chỉ tính khi đủ mục bắt buộc, theo giờ VN;
  - điểm gợi ý có lý do kèm theo, đổi nhãn từ "AI" sang "Gợi ý theo dữ liệu".
- **Quiz:**
  - làm lại sai không hạ trạng thái đã đạt, giữ điểm cao nhất, không thu hồi chứng nhận;
  - xáo câu và đáp án phía server, ánh xạ ngược khi chấm (`LearningQuizShuffle.cs`).
- **Ôn tháng:**
  - điểm = số câu đúng ÷ số câu của đề;
  - kỳ tính theo tháng VN (Cursor sửa sau).
- **Quan sát tại quầy:** lưu cả lần không đạt (mig mới `435_learning_observation_history.sql`, trong manifest staff-enablement).
- **Góp ý riêng tư:** góp ý gửi ở chế độ riêng; backend chỉ cho người nhận, `learning.write` hoặc ADMIN xem.
- **Bỏ số liệu giả:** Team Health và hồ sơ năng lực dùng số thật hoặc hiện "Chưa có dữ liệu".
- **Lộ trình:** hiện mọi lộ trình được giao, không chỉ lộ trình đầu tiên.
- **Test:** 24 test mới (`LearningScoringRulesTests`, `LearningQuizShuffleTests`).
- **Cursor sửa thêm:**
  - doanh thu tháng ở màn đánh giá chỉ trả cho `reports.read`, `reports.export` hoặc ADMIN (`LearningController.cs`, endpoint evidence);
  - ô "Hạn hoàn thành", tag "Quá hạn" và bộ lọc trong `LearningTasksPage.tsx`.

### C. Nhân sự — agent [Nhân sự](2193ac4f-83ac-477b-93c7-bd6e42563c33)
- **Chênh tiền:** ghi người mở và đóng ca, cộng bảng "Chênh tiền theo người đóng ca" (`LossPreventionRepository.cs`, mới `CashVarianceAccountability.cs`, `LossCashVariancePage.tsx`).
- **Việc quá hạn:** thêm `isOverdue`, bộ lọc `overdue=true`, việc quá hạn lên đầu (`OperationalTaskRules.cs`, `OperationalTaskService.cs`).
- **Mục "Nhân sự hôm nay" trong Owner Cockpit:**
  - endpoint `GET api/success/owner-cockpit/staff-today`, dùng `SuccessPolicies.Owner`, không có doanh thu;
  - file mới `OwnerStaffToday*`, giao diện `OwnerStaffTodaySection.tsx`.
- **Chức danh và ngày vào làm:** mig mới `434_employee_job_title.sql` (manifest staff-enablement), có fallback khi thiếu cột; sửa `UserFormDrawer.tsx`.
- **Test:** 19 test mới (`tests/KitPlatform.Application.Tests/StaffAccountabilityTests.cs`).

### D. Tri thức — agent [Tri thức](316d46ef-facf-4621-a77f-51bf30f32810)
- **Mig 432:** seed `pharmacy.knowledge.approve`, gán ADMIN, bỏ qua `NT_XUANHOA`. Các quyền nền tảng cố ý không seed; xem mục 3 câu 2.
- **Tách người viết và người duyệt:** áp cho tri thức riêng và phác đồ nội bộ (`PharmacyKnowledgeOperations.cs`, `...Governance.cs`, `PharmacyLocalRegimenService.cs`).
- **`SavePolicy`:** thêm kiểm tra loại hình nhà thuốc.
- **Tìm kiếm:** tìm cả nội dung, không phân biệt dấu.
- **Mig 433:**
  - `review_due_on` mặc định +12 tháng, tag "Quá hạn rà soát" và bộ lọc;
  - bảng `pharmacy_knowledge_feedback` (lượt mở / thiếu / không giúp được), có màn tổng hợp cho chủ.
- **POS:** thêm `CustomerKnowledgeCard`, `OwnedKnowledgePosLookup` (mới) và `LocalRegimenPosLookup` vào `PosSupportSale.tsx`.
  - Cursor khôi phục bộ lọc "không dùng để tư vấn" ở cả `PosSupportSale.tsx` và `LocalRegimenPosLookup.tsx`.
- **Manifest mới:** `deploy/ubuntu/migration-files.pharmacy-knowledge-v1.txt` (416, 417, 423–429, 432, 433; **chưa có 436** — cần thêm khi tiếp tục). Áp sạch trên DB mới, chạy lại vẫn sạch; mig 427 được sửa để chạy lại được.
  - Cursor sửa chú thích đầu mig 428 cho khớp manifest (đã kiểm tra: 424–427 không tạo grant 'clinical').
- **Test:** 29/29 (`PharmacyKnowledgeOperationsTests` + K1).

### E. Kỹ thuật chung
- **Menu:** mục Learning chỉ hiện khi tenant bật module `learning` (`modules/registry.tsx` thêm `platformModule`; sửa chú thích `audit-slim-nav.ts`). Mig 156 đã bật `learning` cho `NT_XUANHOA`.
- **Script DB test:** `scripts/ci/build-pharmacy-test-db.ps1` thêm 432–436.
- **Chú thích script đêm:** sửa `deploy/ubuntu/run-staff-enablement-v1-night.sh`.
- **Kiểm tra lần cuối (12:55):**
  - `dotnet build KitPlatform.slnx` 0 lỗi;
  - `npx tsc --noEmit` trong `client/admin` sạch;
  - DB test đã dựng lại sạch: `novixa_ci_api`, `novixa_ci_app` (247 file), `novixa_ci_noseed` (243 file).

---

## 2. Đang làm khi dừng

Bộ test Api và Application chạy trên `novixa_ci_api` / `novixa_ci_app` vừa khởi động thì dừng, **chưa có kết quả**. Đã tắt tiến trình test; DB vẫn còn.

---

## 3. Cần người dùng quyết định (chặn phát hành)

1. **Gợi ý OTC sau bản tri thức.**
   - Không code nào đặt `pharmacy_knowledge_rule.approval_status = 'APPROVED'`. Đường gợi ý hợp lệ duy nhất là nhà thuốc pin một phiên bản `PUBLISHED`, mà phát hành cần `platform.knowledge.publish`, quyền hiện chưa ai được cấp.
   - Kết quả: sau khi phát hành bản này, **mọi nhà thuốc (kể cả Xuân Hòa) không có gợi ý OTC** cho tới khi có người vận hành nền tảng duyệt và phát hành, rồi nhà thuốc pin. Cảnh báo an toàn vẫn chạy.
   - Lựa chọn:
     - (a) giữ chặt (an toàn trước) và dựng mô hình người vận hành nền tảng;
     - (b) cho dược sĩ phụ trách của từng nhà thuốc duyệt quy tắc gốc cho riêng tenant mình;
     - (c) khác.
2. **Lỗ hổng phân quyền quyền nền tảng (phát hiện, chưa sửa).**
   - Tenant mới được chép mọi quyền cho ADMIN (`PlatformTenantRepository.cs` ~239-247).
   - Tenant admin gán được bất kỳ mã quyền nào cho role (`IdentityAdminRepository.UpdateRolePermissionsAsync` ~608-640).
   - Hiện an toàn chỉ vì quyền `platform.*` và `pharmacy.knowledge.review` / `verify-source` không có trong bảng. Muốn seed chúng thì phải chặn hai đường này trước. Gắn với câu 1.

---

## 4. Việc còn lại (theo thứ tự)

1. **Chạy lại test** (lệnh ở mục 5). Sửa lỗi nếu có. Chạy thêm Platform.Tests đầy đủ.
2. **Kiểm thử đầu-cuối trên `novixa_ux_audit`:**
   - "Người lớn ho khan 2 ngày, không sốt" phải ra không có sốt, không có thuốc hạ sốt;
   - khách mang thai, đăng nhập `pharmacist` (STAFF): không có danh sách thuốc;
   - admin: có danh sách và câu thông báo dành cho dược sĩ;
   - Owner "Nhân sự hôm nay";
   - đánh giá tháng không lộ doanh thu với MANAGER không có reports.
3. **Vòng liên kết (mục F của mission), chưa làm:**
   - lưu lịch sử từng lần làm quiz và kết quả từng câu (mig 437);
   - điểm yếu (câu sai, quan sát không đạt, chênh tiền, hủy/giảm giá, trả hàng) → gợi ý bài học;
   - báo cáo trước/sau 30 ngày theo người bán quanh ngày đạt bài;
   - gắn bài học với tri thức đã duyệt.
4. **Phân quyền nền tảng** sau khi người dùng chốt mục 3.
5. **CI:**
   - thêm bước Platform.Tests (dùng DB `-NoSeed` cho `POS_IDEMPOTENCY_TEST_CONNECTION`) vào `.github/workflows/novixa-pharmacy-db-tests.yml`;
   - ghi các file test đang loại (`PharmacyKnowledgeExperienceApiTests` có fixture cũ; `PharmacyOwnedKnowledgeM1Tests` tìm chuỗi không còn).
6. **Việc nhỏ còn lại:**
   - `COMPETENCY_CATALOG_SIZE = 24` viết cứng;
   - `GetObservationRowAsync` có thể trả trùng dòng;
   - xáo câu quiz chưa có khóa bí mật (rủi ro thấp);
   - `GEMINI_API_KEY` ở biến môi trường server có thể dùng chung (ghi vào báo cáo).
7. **Hiệu năng:** dùng lại số liệu mig 431 trong `NOVIXA-RELEASE-READINESS-01-REPORT.md`. Kiểm thêm truy vấn của endpoint mới `staff-today` và tìm kiếm tri thức (chưa có index).
8. **Viết báo cáo cuối** `NOVIXA-PRODUCT-EXCELLENCE-REPORT.md`, kết luận `NOT_READY` nếu mục 3 chưa chốt.
9. **Đóng gói phát hành:** tách commit theo nhánh từ `66afd2d9` như kế hoạch release cũ. Không gộp Pharmacy auth với Family/Content. Chỉ làm khi người dùng bảo.

---

## 5. Lệnh để tiếp tục

```powershell
cd E:\KitPlatform
$ps = 'C:\Program Files\PostgreSQL\18\bin\psql.exe'   # psql không có trong PATH

# Build (bin chính có thể đang bị API dev khóa)
dotnet build KitPlatform.slnx --artifacts-path .tmp/artifacts-ws1 -v q -nologo
cd client/admin; npx tsc --noEmit; cd ../..

# (Tuỳ chọn) dựng lại DB test cô lập — chỉ cổng 5436
./scripts/ci/build-pharmacy-test-db.ps1 -Port 5436 -Database novixa_ci_api -Psql $ps
./scripts/ci/build-pharmacy-test-db.ps1 -Port 5436 -Database novixa_ci_app -Psql $ps

# Api + Application như CI
$env:KIT_API_TEST_CONNECTION='Host=127.0.0.1;Port=5436;Database=novixa_ci_api;Username=pgadmin;Timeout=15;Command Timeout=60'
$env:KIT_APP_TEST_CONNECTION='Host=127.0.0.1;Port=5436;Database=novixa_ci_app;Username=pgadmin;Timeout=15;Command Timeout=60'
dotnet test tests/KitPlatform.Api.Tests/KitPlatform.Api.Tests.csproj --artifacts-path .tmp/artifacts-ws1 --filter "FullyQualifiedName!~PharmacyKnowledgeExperienceApiTests"
dotnet test tests/KitPlatform.Application.Tests/KitPlatform.Application.Tests.csproj --artifacts-path .tmp/artifacts-ws1 --filter "FullyQualifiedName!~InventoryMovementSummaryTests&FullyQualifiedName!~PharmacyOwnedKnowledge&FullyQualifiedName!~TestPackPersistenceTests&FullyQualifiedName!~LocalOsOnboardingTests"

# Tư vấn: unit + DB cô lập
dotnet test tests/KitPlatform.Platform.Tests/KitPlatform.Platform.Tests.csproj --artifacts-path .tmp/artifacts-ws1 --filter "FullyQualifiedName~ConsultationNegationAndScreeningTests"
$env:POS_IDEMPOTENCY_TEST_CONNECTION='Host=127.0.0.1;Port=5436;Database=novixa_release_iso;Username=novixa_iso;Timeout=15;Command Timeout=60'
dotnet test tests/KitPlatform.Platform.Tests/KitPlatform.Platform.Tests.csproj --artifacts-path .tmp/artifacts-ws1 --filter "FullyQualifiedName~NovixaOtcSafetyGateIsolatedTests|FullyQualifiedName~NovixaW5"
```

API kiểm thử đầu-cuối (trên `novixa_ux_audit`, cổng 5392): dùng lệnh `dotnet run` có các biến môi trường tắt worker trong transcript đợt audit, đổi `--artifacts-path` sang `.tmp/artifacts-ws1`. Trước đó phải áp thêm 432–436 vào `novixa_ux_audit`, vì DB này dựng trước khi có các mig đó.

**Quy tắc khi tiếp tục:**
- Chỉ dùng DB `novixa_*` trên cổng 5436 (hoặc 5433, 5434 cho DB cô lập cũ).
- Không dùng cổng 5432 hay DB `kitplatform`.
- Không seed hay sửa `NT_XUANHOA`.
- Không commit hay deploy khi chưa được bảo.
