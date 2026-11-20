# Novixa — Product quality & release readiness 01

Ngày: 2026-11-04. Repo duy nhất `E:\KitPlatform`, nhánh `feat/famixa-linesheet-build-m1` (HEAD `dbd96772`). Mọi thay đổi **chưa commit, chưa push, chưa deploy**. Không đụng Production, không chạy migration trên prod/dev, không sửa dữ liệu lịch sử.

## 1. Kết luận

**NOVIXA_RELEASE_READY: CHƯA ĐẠT — đạt có điều kiện.** Code đã sửa các lỗi P0 và P1 về tiền, công nợ, tồn kho. Mỗi lỗi P1 có test tái hiện (đỏ trên code cũ, xanh sau khi sửa). Báo cáo bán hàng ở cỡ Xuân Hòa hết timeout khi có migration 431. Chỉ phát hành khi xong mục 7.

## 2. Lỗi đã sửa

### P0 — bảo mật, vận hành

| Mã | Sửa | Nơi |
|---|---|---|
| OPS-01 | Script migration không in URI kết nối có mật khẩu | `deploy/ubuntu/*.sh`, `scripts/run-migrations*.ps1` |
| SEC-02 | Endpoint vận hành Family OS billing yêu cầu `PaymentPolicies.OpsRead` | `FamilyOsBillingController.cs` |
| SEC-03/04 | Rate limit + khóa đăng nhập sau nhiều lần sai (429 `login_locked`); `UseForwardedHeaders` để IP thật đi qua nginx | `LoginAttemptGuard.cs`, `AuthRateLimitPolicies.cs`, `AuthController.cs`, `Program.cs` |
| SEC-01 | Cổng bác sĩ: chưa có OTP (chấp nhận rủi ro theo quyết định), thêm rate limit | `PrescriberPortalAuthController.cs` |
| QA-01 | Admin `tsc` sạch; Platform.Tests biên dịch lại được | admin (2 file), `PharmacyPlatformSettingsStub.cs` |

### P1 — tiền, công nợ, tồn kho

| Mã | Lỗi cũ | Sửa | Test |
|---|---|---|---|
| INV-01 | PO/GRN nhập theo Hộp ghi tồn như Viên; dòng GRN lấy được dòng của PO khác | Tạm chỉ nhận đơn vị gốc; dòng PO phải thuộc đúng PO, lọc tenant | `ProcurementIntegrityApiTests` (2 test) |
| PRC-02 | Số chứng từ COUNT+1 → trùng số, lỗi 500 sau khi xóa phiếu cũ | MAX+1 dưới advisory lock (PO/GRN/phiếu chi/phiếu thu KH) | `New_purchase_order_gets_a_fresh_number…` |
| PRC-01 | Phiếu chi gắn phiếu nhập vượt số còn nợ | Khóa dòng, chặn vượt còn nợ | `Supplier_payment_linked_to_a_receipt…` |
| SAL-01 | Trả hàng đơn có voucher/điểm hoàn 1.000 thay vì 850 | Phân bổ đủ CK đơn + voucher + điểm (server và client) | `Sales_full_return_of_voucher_order…` |
| SAL-02 | Phiếu thu không gắn đơn bị trừ 2 lần (700 thay vì 400) | Bỏ trừ lần 2 (ghi sổ đã FIFO) | `Receivables_count_an_unlinked_collection_once` |
| SAL-03 | Thu nợ sau đóng ca cộng vào ca đã đóng | Ca chỉ tính thu tới `closed_at`, theo giờ DB | `Debt_collection_cash_lands_in_the_shift…` |
| INV-02 | Duyệt kiểm kê sau khi đã bán tiếp → chênh lệch sai | Chênh lệch = đếm − tồn tại lúc đếm | `Stock_count_keeps_sales_made_after_counting` |
| INV-03 | Tồn đầu kỳ ghi 2 lần cho cùng lô | Chặn lô còn tồn đầu kỳ hiệu lực; import báo lỗi theo dòng | `Opening_balance_cannot_be_recorded_twice…` |

### UX / tư vấn

| Mã | Sửa |
|---|---|
| UX-01 | Ô đơn vị PO/GRN chỉ hiện đơn vị gốc (`baseUnitOnly`) |
| UX-03 | Đóng ca không điền sẵn tiền đếm = tiền dự kiến |
| UX-04 | Hoàn tất phiếu nhập có xác nhận, chặn bấm 2 lần |
| UX-07 | Trả hàng gửi `clientReturnId`, chặn bấm 2 lần (file đỏ `SalesOrderListPage.tsx`: chỉ thêm kiểu + chốt chặn, gate quyền giữ nguyên) |
| CON-01 | Nhóm “Kháng sinh” ở hỗ trợ bán: “Cần đơn bác sĩ” thay nút Thêm |
| UX-02 | Đã xác minh backend chặn bán khi chưa mở ca và chặn thuốc kê đơn ở chế độ nghiêm — không sửa |

### Hiệu năng

`migrations/431_pharmacy_report_fk_indexes.sql`: 9 index khóa ngoại trên bảng dòng (`CREATE INDEX CONCURRENTLY`, không đổi dữ liệu, 0,5 s trên DB đo).

Dữ liệu giả cỡ Xuân Hòa (`novixa_perf`: 6.024 SP, 12.027 lô, 32.886 đơn, 127.208 biến động, 2.000 phiếu nhập). Đo tại server.

| Báo cáo | Trước | Sau |
|---|---|---|
| Doanh thu theo kỳ 30 ngày | 93 s | 56 ms |
| Doanh thu theo kỳ 365 ngày | >120 s (500) | 240 ms |
| Doanh thu theo nhóm hàng 365 ngày | >120 s (500) | 581 ms |
| Biến động công nợ 365 ngày | >120 s (500) | 106 ms |
| Danh sách ca 30 ngày | >120 s (500) | 85 ms |
| Dashboard | 3,4 s | 56 ms |
| Tồn kho / Xuất-nhập-tồn / Doanh thu theo KH | 70–230 ms (SQL 37 ms), JSON 1,3–2,4 MB không nén | không đổi |
| Công nợ NCC (PERF-02) | 48 ms | không cần sửa |

## 3. Build / CI / test

| Hạng mục | Kết quả |
|---|---|
| Admin `npm run build` (tsc + vite) | Xanh (chỉ cảnh báo CSS cũ) |
| Api tests (DB mới dựng từ manifest) | 45/45 (loại 2 test fixture schema tự chế) |
| Application tests (bộ lọc CI) | 124/124 |
| Platform tests | 231/235 — 3 lỗi có từ trước (2 Local OS, 1 hình dạng K1), 1 skip có lý do |
| CI mới | `.github/workflows/novixa-pharmacy-db-tests.yml` + `scripts/ci/build-pharmacy-test-db.ps1` (postgres:16, cổng 5436, chỉ cho phép 4 file lỗi đã biết). **Chưa chạy trên GitHub** |

An toàn test:
- Api factory: bắt buộc `KIT_API_TEST_CONNECTION`, từ chối cổng 5432 / `kitplatform` / `postgres` / tên có `prod`. Factory còn gỡ worker chạy nền và để trống feed/webhook ra ngoài.
- 3 test Application trước đây hardcode DB dev `kitplatform:5432` kèm mật khẩu. Nay chúng dùng `KIT_APP_TEST_CONNECTION` với cùng chặn.
- Sự cố 2026-11-04: một lần chạy cả project Application đã chạy 3 test này vào DB dev. Kiểm tra chỉ-đọc sau đó không thấy dữ liệu sót (0 tenant tạm, 0 marker).

## 4. Rà lại sau sửa

- **Kiến trúc:** tồn và giá vốn vẫn theo đơn vị gốc. Tồn chỉ đổi qua Increase/Decrease + movement. Không gộp Family OS / Local OS / Pharmacy auth trong thay đổi Pharmacy.
- **Bảo mật:** gate STAFF / báo cáo / Cockpit không đổi. `ReportsPolicies.Read` không mở thêm. Không thêm secret vào code.
- **Hiệu năng:** điểm nghẽn là index thiếu (đã có 431). Còn payload lớn không nén.
- **UX:** các sửa ở mục 2. Chưa làm: báo cáo lãi gộp, lối vào Learning, kiểm tra tương tác thuốc, thuật ngữ kỹ thuật hiện với nhân viên.

## 5. Thay đổi kiến trúc cần làm (chưa làm)

1. Quy đổi đơn vị thật khi nhập (số lượng × hệ số, giá vốn ÷ hệ số). INV-01 hiện chỉ là chặn tạm.
2. Đánh số chứng từ dùng chung một cơ chế (sequence hoặc bảng counter). Còn COUNT+1 ở `PrescriberPortalPrescriptionRepository.cs:228`, `CustomerDraftOrderRepository.cs:61`, `CustomerReservationRepository.cs:28`.
3. Manifest prod phải replay được từ DB rỗng: seed 090/102 lỗi khóa ngoại, 230/231 cần `pack_family`. Migration 416/417/423–429/431 chưa nằm trong manifest nào; 427 không idempotent.
4. Phiếu chi NCC đã ghi sổ chưa có bút toán đảo.
5. Báo cáo lớn cần phân trang hoặc nén (gzip nginx / `ResponseCompression`).
6. Đăng nhập không kèm mã tenant mơ hồ khi nhiều tenant cùng username `admin`.

## 6. Rủi ro còn lại

| Rủi ro | Mức | Ghi chú |
|---|---|---|
| Dữ liệu cũ nhập theo Hộp (INV-01) | Cao | Cần đếm chỉ-đọc trên prod (script mục 7) |
| Phiếu thu cũ không gắn đơn (SAL-02) | Trung bình | Cần đếm chỉ-đọc trên prod |
| Tổng ca cũ đã đóng có thể hiện số khác (SAL-03); thu nợ đơn không qua ca không vào ca nào | Trung bình | `expected_cash` đã lưu không đổi |
| `LocalOs.PublicFeedSecret` của `thainguyenlife.vn` nằm trên `origin/main` (`aecdadc2`) | Cao | Xoay secret ở Worker + VPS |
| `Newtonsoft.Json` 10.0.3 có lỗ hổng mức cao (NU1903) | Trung bình | Nâng gói |
| Cổng bác sĩ chưa có OTP; OTC fallback | Chấp nhận | Theo quyết định người dùng |
| Làm tròn từng dòng khi hoàn nhiều lần | Thấp | Lệch vài đồng |
| Test bị loại khỏi CI: knowledge (2 Api + 7 Application), `InventoryMovementSummaryTests`, TestPack, LocalOs | Trung bình | Fixture cần viết lại theo schema thật |
| Test Platform `CustomerPosContext*` chứa mật khẩu dev (trỏ `kitplatform_test`) | Thấp | Tự skip khi không kết nối |

## 7. Điều kiện phát hành

1. Port các sửa sang nhánh từ `66afd2d9` (base đang chạy prod). Tách commit: Pharmacy P0/P1/UX; test + CI; Family OS (SEC-02) riêng; không lẫn Content/Famixa đang dở.
2. Push, CI `novixa-pharmacy-db-tests` + `KitPlatform CI` xanh trên GitHub.
3. Người vận hành chạy `scripts/novixa-release-readonly-checks.sql` trên prod (chỉ đọc), cho `DEMO_PHARMACY` và `NT_XUANHOA`. Mục 3 và 4 khác 0 → quyết định cách xử lý dữ liệu cũ trước khi phát hành.
4. Đưa 431 vào manifest prod; chạy có chủ đích, theo dõi index INVALID.
5. Xoay `LocalOs.PublicFeedSecret`.
6. Sau deploy: smoke `DEMO_PHARMACY` (admin + pharmacist), xác nhận `NT_XUANHOA` đăng nhập và báo cáo chạy.

## 8. Tệp liên quan

- Số đo: `.tmp/release-20261104/perf-before.csv`, `perf-after-idx.csv`, `perf-measure.ps1`, `perf-seed.sql` (chỉ chạy trên `novixa_perf`)
- Ghi chú làm việc: `.tmp/release-20261104/p1-notes.md`
- Báo cáo audit trước: `docs/novixa/03-operations/NOVIXA-COMPREHENSIVE-AUDIT-01-REPORT.md`
