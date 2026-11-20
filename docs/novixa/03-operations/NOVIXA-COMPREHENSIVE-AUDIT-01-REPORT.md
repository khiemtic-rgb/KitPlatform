# NOVIXA-COMPREHENSIVE-AUDIT-01 — Báo cáo audit tổng hợp Park Novixa

**Ngày:** 04/11/2026 · **Loại:** AUDIT chỉ đọc · **Không** sửa code, không chạy migration/SQL, không deploy, không đụng `NT_XUANHOA`.

**Phạm vi mã:** working tree `E:\KitPlatform`, nhánh `feat/famixa-linesheet-build-m1` (HEAD `dbd96772`, đi trước `origin/main` 22 commit). Phát hiện nào chỉ có trên nhánh được ghi rõ “chỉ nhánh”; còn lại có cả trên `origin/main`.

---

## 1. Executive Summary

Lõi giao dịch Pharmacy **vững ở các điểm khó nhất**:

- Trừ tồn có khóa dòng, có `UPDATE ... AND quantity_available >= @Qty` và có `CHECK >= 0` ở DB, nên không bán âm khi nhiều quầy bán đồng thời.
- Đơn bán có idempotency bằng unique index ở DB.
- Trả hàng không vượt được số đã bán.
- Chuyển kho nguyên tử.
- Mọi thay đổi tồn đều ghi `stock_movements`.
- Không thấy SQL injection.
- Token khách, bác sĩ và đối tác không gọi được API admin.
- Gate doanh thu/báo cáo/Cockpit ở backend đúng rule RBAC.

Yếu nằm ở **tính đúng của số liệu tài chính phụ** (hoàn tiền, công nợ, ca, giá vốn), **quy đổi đơn vị khi nhập hàng**, **bảo mật bề mặt auth phụ** (OTP bác sĩ, brute force), và **lưới an toàn phát hành**: CI đang đỏ, test tích hợp không chạy trong CI, còn nhánh hiện tại lệch schema prod.

| Mức | Số phát hiện |
|---|---|
| Critical | 1 |
| High | 15 |
| Medium | 25 |
| Low | ~20 |

**Kết luận ngắn:**

- Có thể tiếp tục phát triển tính năng trên `origin/main` **nếu** khóa nhánh hiện tại khỏi deploy và xử lý nhóm “bắt buộc trước phát hành” ở §10.
- **Không** nên deploy nhánh hiện tại lên prod.

---

## 2. Bản đồ kiến trúc và điểm phụ thuộc quan trọng

```
Domain  ◄── Application  ◄── Infrastructure ──► Packs.Pharmacy.Application   (ARC-01)
                                   ▲
Packs.*.Infrastructure ──► Packs.*.Application, Application, Infrastructure
Packs.Clinic.Infrastructure ──► Packs.Connect.Application                    (ARC-02)
Api ──► Application, Infrastructure, mọi Packs.*.Infrastructure
```

**Pharmacy tách đôi (ARC-03):**

- Nghiệp vụ giao dịch nằm ở `src/Packs/Pharmacy` (Sales, Inventory, Procurement, Rx, Catalog).
- Báo cáo, Dashboard, Success (Cockpit/Loss/Growth), CustomerApp, Customers, Loyalty nằm ở `src/KitPlatform.Infrastructure` và **viết SQL trực tiếp lên cùng bảng**.

Bảng ngoài pack truy cập nhiều nhất là `sales_orders`, `inventory_batches`, `products` và `customers`. Mỗi bảng có 15–25 file ở Core, cộng thêm Connect, Content và Clinic.

**Thành phần dùng chung: sửa một chỗ, nhiều màn đổi theo**

| Thành phần | Nơi dùng |
|---|---|
| `VietnamBusinessCalendar` | Dashboard, Cockpit, Loss, GrowthDesk, ShiftChecklist |
| `SalesAccrualSql` | Reports, Dashboard, OwnerCockpit |
| `LowStockThresholdSql` (định nghĩa ở Pack, dùng ở Core) | Inventory, Dashboard |
| `PlatformModuleAuthorization` | ~90 action `[RequirePlatformModule]` |
| `DbPermissionsClaimsTransformation` | Toàn bộ RBAC admin |
| `TenantScopedNpgsqlConnectionFactory` | Mọi kết nối DB qua `IDbConnectionFactory` |
| `IncreaseBatchQuantityAsync` | Nhập hàng, trả hàng, hủy chuyển kho, tồn đầu kỳ |

**Worker nền:**

- `IntegrationOutboxWorker` và `PlatformEventWorker` (đồng bộ CSDL Dược).
- `NotifyQueueWorker`, `MedicationReminderPushWorker`.
- Các worker Content/Famixa/Exchange/KitSales/LocalOs/FamilyOs chạy **chung host** với POS.

**Hotspot regression (số dòng):**

| File | Dòng |
|---|---|
| `SalesRepository.cs` | ~4.400 |
| `InventoryRepository.cs` | ~3.000 |
| `PosPage.tsx` | ~2.360 |
| `ReportsRepository.cs` | ~2.100 |
| `ProcurementRepository.cs` | ~2.070 |

---

## 3. Danh sách lỗi theo mức độ

Quy ước: **C** = Confirmed · **L** = Likely · **NV** = Needs verification. Những mục đánh ✔ đã được Cursor tự đọc lại code để kiểm chéo.

### Critical

**SEC-01 ✔ — OTP bác sĩ kê đơn trả thẳng trong response ở Production — C (code + config); env VPS cần xác minh**

- **Vị trí:**
  - `src/KitPlatform.Api/appsettings.Production.json:28`: `"ExposePilotOtpInResponse": true`. `origin/main` cũng vậy.
  - `PrescriberPortalAuthSettings.cs:20`: mặc định `= true`.
  - `PrescriberPortalAuthService.cs:66,92`.
  - `Program.cs:685-693`: kiểm tra khi khởi động chỉ chặn cờ của customer app, không chặn cờ này.
- **Tái hiện:** gọi `POST /api/prescriber-portal/auth/otp-request` ẩn danh với SĐT bác sĩ. Response trả về mã. Gọi tiếp `otp-verify` là có token bác sĩ.
- **Hậu quả:**
  - Chiếm tài khoản bác sĩ chỉ cần số điện thoại.
  - Kẻ tấn công kê, sửa hoặc hủy đơn điện tử.
  - Kẻ tấn công xem tên và SĐT khách của các nhà thuốc đã liên kết với bác sĩ đó.
- **Hướng xử lý:**
  - Đổi mặc định và cấu hình Production thành `false`.
  - Chặn khởi động khi cờ bật ở Production.
  - Thêm rate limit cho `otp-request` và `otp-verify`.
  - Kiểm env VPS (`PrescriberPortalAuth__ExposePilotOtpInResponse`).

### High

| ID | Nhóm | Vấn đề | Vị trí | Hậu quả | Chắc chắn |
|---|---|---|---|---|---|
| **REL-01** ✔ | DB/Phát hành (chỉ nhánh) | Code trên nhánh luôn `INSERT sales_returns(... client_return_id)`. Mig 411 không có trong `migration-files.prod.txt`, chỉ có trong `pharmacy-ops-v2`. 414–429 chưa có trong manifest prod, và 416–417, 423–429 không nằm trong manifest nào. Tài liệu W5 ghi prod chưa có cột này | `SalesRepository.cs:1915-1919`; `migrations/411_*.sql:1-2`; `NOVIXA_W5_PRODUCTION_READINESS_V1.md:31` | Merge rồi deploy là **mọi phiếu trả hàng lỗi 42703**; các màn Knowledge cũng lỗi | C nhánh / NV prod |
| **QA-01** ✔ | Kỹ thuật/CI | `KitPlatform.Platform.Tests` không biên dịch (CS7036, constructor `PharmacyKnowledgeApprovalController` thêm `platform`). Admin `tsc --noEmit` có 2 lỗi. `pharmacore-ci.yml` **chỉ** chạy Platform.Tests, không chạy Api.Tests/Application.Tests (nơi có test Sales/Procurement/Inventory) | `NovixaW5LifecycleIsolatedTests.cs:37,41`; `NovixaW5TenantKnowledgeIsolatedTests.cs:269,274`; `AdjustmentListPage.tsx:556`; `PharmacyLocalRegimenPage.tsx:168` | Cả 2 workflow CI đỏ trên HEAD; `npm run build` chặn deploy admin; lưới regression Pharmacy không có trong CI | C |
| **SEC-02** ✔ | Tenant/PII (Family OS, chung host API) | `GET /api/family-os/ops/trial-signups` chỉ yêu cầu `[Authorize]` + module FamilyOs. Query `family_os_trial_signup` không lọc tenant (tối đa 500 dòng), còn chạy `UPDATE` toàn bảng | `FamilyOsBillingController.cs:95-102`; `FamilyCommercialService.cs:970-1023` | Tự đăng ký tenant là đọc được email/username/tên của mọi tenant Family. `ops/demo-house-views` tương tự | C |
| **SEC-03** | Auth | `/api/auth/login`, `/select-workspace` không rate limit, không khóa tài khoản sau nhiều lần sai | `AuthController.cs:21-41`; `AuthService.cs:38-61` | Dò mật khẩu `admin` của từng nhà thuốc không giới hạn | C |
| **OPS-01** ✔ | Secret | `deploy-update-vps.ps1` ghép mật khẩu DB vào URI; cả 3 runner `echo "Database: $CONN"` | `scripts/deploy-update-vps.ps1:~205`; `run-incremental-migrations-prod.sh:53`; `run-migrations-prod.sh:37`; `run-kit-migrations-prod.sh:48` | Mật khẩu DB prod nằm trong stdout/log deploy và argv (`ps`) | C |
| **SAL-01** ✔ | Tiền/Trả hàng | `ComputeLineRefundAmount` chỉ phân bổ chiết khấu đơn, bỏ qua `voucher_discount_amount` và `loyalty_discount_amount` | `SalesPricing.cs:126-144`; `SalesRepository.cs:1976-1981` | Ví dụ: đơn 500k, voucher 50k, trả 1/2 hàng thì hoàn 250k thay vì 225k. `OriginalTotalExpr` (`total_amount` + hoàn) thổi doanh thu gốc trên Dashboard và báo cáo | C |
| **SAL-02** ✔ | Công nợ KH | Phiếu thu không gắn đơn đã trừ vào `sales_orders.outstanding` lúc ghi sổ (và chặn nếu còn dư), nhưng báo cáo vẫn cộng thành “credit” rồi `ApplyUnlinkedCredit` trừ thêm lần nữa | `SalesRepository.cs:4051-4081, 4193-4219`; `CustomerReceivablesService.cs:132,145-159` | Công nợ hiển thị thấp hơn thực tế đúng bằng tổng phiếu thu không gắn đơn | C |
| **SAL-03** ✔ | Ca/Quỹ | Thu nợ chèn `sales_payments` vào **đơn gốc**; tổng kết ca cộng theo `o.sales_shift_id`; `sales_payments` không có cột ca | `SalesRepository.cs:2466-2473, 4258-4268`; `006_sales.sql:66-73` | Tiền thu nợ hôm nay rơi vào ca cũ (kể cả ca đã đóng); ca hiện tại lệch quỹ | C |
| **INV-01** ✔ | Tồn/Giá vốn | Hoàn tất phiếu nhập cộng `item.Quantity` thô vào tồn; Procurement không dùng `conversion_factor` ở đâu. UI cho chọn đơn vị “Hộp (×10)” (mặc định đơn vị gốc) | `ProcurementRepository.cs:1126,1141,1146`; `ProcurementPricing.cs:99-100`; `GoodsReceiptListPage.tsx:976` | Nhập 5 Hộp×10 thì tồn +5 thay vì +50, giá vốn mỗi viên ×10; sai lãi gộp, sai dữ liệu đẩy CSDL Dược. Chuyển kho **không** bị (quy đổi ở client) | C |
| **INV-02** ✔ | Kiểm kê | Phiên đếm: `diff = counted - QuantityAvailable` **tại lúc duyệt**; không lưu tồn hệ thống lúc đếm; bán hàng không bị chặn khi kho đang đếm | `InventoryRepository.cs:2718` | Ví dụ: đếm 10, bán 2, duyệt thì tồn quay về 10 trong khi thực tế còn 8 (tồn ảo) | C |
| **INV-03** | Tồn đầu kỳ | Lô đã có thì cộng dồn; không có khóa chống trùng; import lưu một phần | `InventoryRepository.cs:1057-1063`; `InventoryImportService.cs:48-75` | Lưu 2 lần, hoặc import lại file sau khi sửa dòng lỗi, là tồn gấp đôi | C |
| **PRC-01** | Công nợ NCC | Không so số tiền với số còn nợ của phiếu nhập; công nợ `Math.Max(0, …)` | `SupplierPaymentService.cs:117-156`; `SupplierPayablesService.cs:95` | Khoản trả dư biến mất khỏi báo cáo; phiếu đã ghi sổ không đảo được | C |
| **PRC-02** ✔ | Đánh số | `SELECT COUNT(*)::int + 1` cho PO/GRN/PAY; có đường purge PO/GRN | `ProcurementRepository.cs:228-238, 749-757, 1206-1214` | Sau purge, số mới trùng số cũ, vi phạm UNIQUE, **mọi lần tạo phiếu của tenant lỗi**; đồng thời cũng đụng UNIQUE | C |
| **ARC-01** | Kiến trúc | Core `KitPlatform.Infrastructure` tham chiếu `Packs.Pharmacy.Application` | `KitPlatform.Infrastructure.csproj:6` | Core không tách được khỏi pack; vòng phụ thuộc ngầm | C |
| **SEC-05** | RBAC (vi phạm rule repo) | `GET /api/sales/orders` với `sales.read` nhận `from/to` tùy ý, trả `TotalAmount`; giới hạn “hôm nay” chỉ có ở frontend. CSV QĐ540 `table1.csv` chỉ cần `inventory.read`, có giá bán + số lượng bán | `SalesController.cs:196-210`; `Qd540IntegrationController.cs:40-60` | STAFF cộng ra doanh thu kỳ bất kỳ và xuất CSV không cần `reports.export` | C (cần chốt QĐ540 là pháp quy hay tài chính) |

### Medium

| ID | Vấn đề | Vị trí | Chắc chắn |
|---|---|---|---|
| SAL-05 | Trả hàng không trừ điểm đã tích, không hoàn điểm/voucher đã dùng | `PharmacySalesReturnCompletedHandler.cs:18-36` | C |
| SAL-06 | Phiếu trả, danh sách ca, tổng kết ca không áp giới hạn chi nhánh | `SalesService.cs:301-332`; `SalesController.cs:345-401` | C |
| SAL-07 | Kiểm hạn mức nợ không khóa khách, 2 quầy cùng lúc vượt `credit_limit` | `SalesRepository.cs:2773-2796` | L |
| SAL-08 | `EnsureOpenShift` không khóa; đơn commit vào ca vừa đóng | `SalesRepository.cs:2538-2553` | L |
| SAL-09 | Hoàn tất nháp ghi đè `prescription_id` bằng null (trang danh sách không gửi) | `SalesRepository.cs:1553,1607`; `SalesOrderListPage.tsx:342-355` | C |
| SAL-10 | `UpdateDraft`/`CompleteDraft` không kiểm `customerId` cùng tenant; FK chỉ `customers(id)` | `SalesRepository.cs:1415-1434, 1541, 969` | C (cần biết UUID) |
| PRC-03 | Nhận vượt PO khi tách nhiều dòng/lô; không kiểm lại khi hoàn tất; 2 phiếu nháp đồng thời | `ProcurementRepository.cs:872-882, 1033-1044` | C |
| PRC-04 | `UPDATE purchase_order_items ... WHERE id=@Id` không lọc tenant/PO/sản phẩm | `ProcurementRepository.cs:874-877, 1146` | C |
| PRC-05 | PO đã hủy “sống lại” khi hoàn tất phiếu nhập nháp kèm theo | `PurchaseOrderService.cs:252-268`; `ProcurementRepository.cs:1151,1255-1279` | C |
| INV-04 | Nhập thêm vào lô đã có: giữ `unit_cost` cũ, ghi đè `supplier_id` | `ProcurementRepository.cs:1122-1129` | C |
| INV-05 | Phiếu kiểm kê nháp trùng lô bị trừ 2 lần | `InventoryRepository.cs:1475-1486, 2350-2359` | C |
| INV-06 | Xóa lô tồn đầu kỳ, xóa dòng đếm không kiểm chi nhánh | `InventoryService.cs:256-257, 557-568` | C |
| DASH-01 | `ActiveBatchCount` vẫn không lọc `deleted_at` (báo cáo cũ mục C, chờ Architect chốt định nghĩa) | `DashboardRepository.cs:174-186` | C |
| SEC-04 | Rate limit partition theo `RemoteIpAddress`; sau nginx luôn là 127.0.0.1, không có `UseForwardedHeaders` | `Program.cs:179-205` | L |
| SEC-06 | OTP người đăng tin Local OS không đếm lần thử, không cooldown | `LocalOsPublisherService.cs:37-107` | C |
| SEC-07 / DB-05 | RLS chỉ trên `pack_*`/payment/kernel; bảng lõi Pharmacy `public.*` không RLS; `kit_rls_tenant_match` fail-open khi `app.tenant_id` trống; không `FORCE` | `080_kit_workspace_pack_rls.sql:138-146` | C (role owner trên prod: NV) |
| SEC-08 | Nhân viên `sales.pos`/`sales.read` tự cấp/đọc OTP khách, vào được app khách (hồ sơ sức khỏe) | `CustomersController.cs:140-144, 276-299` | C |
| SEC-09 | Khóa thật/dev trong `appsettings.Development.json` đã commit (DataEncryptionKey, VAPID PrivateKey, ProvisioningKey, PublicFeedSecret...) | xem tên key trong báo cáo SEC | NV (VPS có dùng lại không) |
| DB-02 | `run-migrations-prod.sh` (bootstrap) không ghi ledger sau 098; lần incremental đầu chạy lại và dừng ở `235:25` (`CREATE TRIGGER` không idempotent) | `bootstrap-vps.sh:106-107`; `run-migrations-prod.sh:40-50` | C logic |
| DB-03 | Mỗi file mig không `--single-transaction`; ledger ghi bằng phiên `psql` riêng | `run-incremental-migrations-prod.sh:88-89` | C |
| DB-04 | Mig 275 hard-code PIN quầy + mã mời `NT_XUANHOA`, `ON CONFLICT DO UPDATE` ghi đè nếu chạy lại | `275_customer_app_login_requests.sql:44-59` | C code / NV prod |
| OPS-02 | Payment fulfillment chạy **sau** commit, không retry; webhook lặp thấy Paid thì thoát | `PaymentService.cs:551-555, 670-689` | C |
| OPS-05 | Outbox/PlatformEvent giữ `FOR UPDATE` suốt HTTP call; retry 5×5s không backoff; event CSDL Dược có thể bị bỏ hoặc gửi trùng | `PlatformEventWorker.cs:153-195`; `IntegrationOutboxWorker.cs:153-190` | C |
| OPS-07 | Nhắc thuốc không claim: gửi push rồi mới advance; lỗi thì lặp mỗi 60s | `CustomerPushRepository.cs:65-102` | C |
| OPS-08 | Audit log ghi bằng connection riêng sau commit nghiệp vụ (PO, duyệt chiết khấu có thể mất audit) | `AuditLogRepository.cs:37-63`; `SalesService.cs:175-187` | C |
| GOV-01 ✔ | Commit `94780487` “snapshot … work in progress” gom file đỏ RBAC (`SuccessAuthorizationExtensions.cs`, `DbPermissionsClaimsTransformation.cs`) + `SalesRepository.cs` chung với Famixa; 22 commit chưa push; nhánh khác `origin/main` ~16.7k file. Nội dung diff auth **siết chặt hơn** (kiểm cross-tenant, tách `ChecklistMutate`), không phải regression | `git log origin/main..HEAD` | C |
| PERF-01..03 | Thiếu index FK ở bảng dòng phiếu; công nợ NCC tải toàn bộ phiếu nhập vào bộ nhớ; import tồn N+1 (6–8 query/dòng) | `004_inventory.sql`, `005_procurement.sql`; `ProcurementRepository.cs:1647-1711`; `InventoryImportService.cs:48-56` | L / C / C |
| ARC-02/03 | Clinic → Connect; Pharmacy tách đôi Pack/Core cùng bảng | xem §2 | C |

### Low (tóm tắt)

**Sales:**
- SAL-11: link-sale đơn tạm không kiểm `salesOrderId`.
- SAL-12: voucher `used_count` vượt `max_uses` khi đồng thời.
- SAL-13: chương trình loyalty tắt vẫn tích.
- SAL-14: client không gửi `clientReturnId`.
- SAL-15: `LabelRequired` thiếu lô thì im lặng chạy FEFO.
- SAL-16: 2 dòng cùng sản phẩm báo thiếu lô giả.
- SAL-17/INV-07: trả hàng/hủy chuyển cộng cả `quantity_received`.
- SAL-18: làm tròn VND với số lượng lẻ.
- SAL-19: số phiếu thu `COUNT+1`.

**Inventory & Procurement:**
- PRC-06: PO nhận một phần không đóng được.
- INV-08: `qty_at_confirm` lấy từ client.
- INV-09: sửa giá vốn lô chỉ cần `inventory.write`.

**Bảo mật:**
- SEC-10: SVG công khai, có thể XSS.
- SEC-11: path traversal `/uploads/branding`, chỉ trên Windows.
- SEC-12/OPS-03: PayOS không so `amount`/`expires_at`, đọc `code` ngoài phần ký.
- SEC-13: webhook CDP bỏ chữ ký khi secret rỗng.
- SEC-14: refresh token đổi không nguyên tử; logout không thu hồi access token.
- SEC-15: `PharmacyDispensingNotesController` chỉ `[Authorize]`.
- SEC-16: trả nguyên `InvalidOperationException.Message`.
- SEC-17: PIN quầy/mã giới thiệu không giới hạn lần thử, SHA-256 không salt.
- SEC-18: bác sĩ liệt kê dần khách của nhà thuốc.

**DB & vận hành:**
- OPS-04: PayOS HttpClient timeout 100s.
- OPS-06: batch notify một transaction.
- OPS-09: worker Content/Exchange/KitSales bật mặc định trên host POS.
- DB-06: thiếu `CHECK amount > 0` và idempotency key cho phiếu thu/chi.
- DB-07: không có `CONCURRENTLY` cho index trên bảng lớn.
- DB-08: 24 tiền tố mig trùng số, 35 file mồ côi.

**Kiến trúc & contract:**
- ARC-04: Api chạy SQL trực tiếp.
- ARC-07: `swagger.json` cũ.

---

## 4. Bảo mật và tenant isolation

**Ổn:**

- JWT kiểm đủ issuer, audience, lifetime, chữ ký; Production chặn secret dev.
- `tenant_id` lấy từ claim; `X-Workspace-Id` được kiểm thuộc đúng tenant.
- Customer app lấy customerId từ token.
- Token khác loại bị middleware và `IsAdminPrincipal` chặn.
- Quyền nạp lại từ DB mỗi 30 giây, gỡ quyền của user bị khóa.
- Gate Reports/Cockpit/Dashboard đúng rule repo (`DashboardService` trả doanh thu 0 cho người không có `reports.*`).
- Không có SQL injection từ input người dùng.

**Rủi ro chính:**

- **Auth phụ yếu:** SEC-01 (Critical), SEC-03, SEC-06, SEC-08, SEC-17.
- **Cách ly tenant chỉ một lớp:**
  - Bảng lõi Pharmacy không có RLS (SEC-07).
  - Mọi query phải tự `WHERE tenant_id`.
  - Đã thấy chỗ thiếu: SAL-10, PRC-04, SAL-11.
  - Endpoint xuyên tenant: SEC-02.
- **Chi nhánh:** nhiều endpoint đọc/xóa không áp `EnsureWarehouseAccess` (SAL-06, INV-06).
- **Secret:** OPS-01 (log deploy), SEC-09, DB-04.

## 5. Database và migration

- **Ledger và runner:**
  - Ledger `kit_schema_migrations` chỉ được runner incremental ghi; bootstrap không ghi (DB-02).
  - Không có `--single-transaction` (DB-03).
  - Nhiều manifest tách rời; `pharmacy-ops-v2` và 416–429 chưa có lộ trình prod (REL-01, DB-08).
- **Ràng buộc đã có:**
  - `CHECK quantity_available >= 0`.
  - Unique `(tenant_id, number)` cho chứng từ.
  - Unique `(tenant_id, client_sale_id)`.
  - Index `(tenant_id, order_date DESC)`.
- **Ràng buộc còn thiếu:**
  - `tenant_id` trên `sales_payments`/`sales_order_items`.
  - CHECK số tiền.
  - Idempotency cho phiếu thu/chi.
  - Index FK dòng phiếu.
- **Seed:** DEMO seed `239` có guard `DEMO_PHARMACY` đúng. Các mig nhắc `NT_XUANHOA` đều lọc theo `tenant_code`.

## 6. Luồng nghiệp vụ có nguy cơ sai hoặc mất nhất quán

| Luồng | Phát hiện | Bản chất |
|---|---|---|
| Trả hàng | SAL-01, SAL-05, SAL-14, SAL-17 | Tiền hoàn, điểm, doanh thu gốc, chống trùng |
| Công nợ khách | SAL-02, SAL-07 | Báo cáo trừ hai lần; hạn mức không khóa |
| Ca/quỹ | SAL-03, SAL-08 | Thu nợ rơi vào ca cũ; đơn vào ca vừa đóng |
| Nhập hàng/PO | INV-01, INV-04, PRC-02, PRC-03, PRC-05 | Quy đổi đơn vị, giá vốn, đánh số, nhận vượt, PO hủy |
| Kiểm kê | INV-02, INV-05 | Chênh lệch theo tồn lúc duyệt; dòng trùng |
| Tồn đầu kỳ/import | INV-03, PERF-03 | Cộng trùng; N+1 |
| Công nợ NCC | PRC-01, PERF-02 | Trả dư mất; tải toàn bộ |
| Đồng bộ CSDL Dược / thanh toán | OPS-02, OPS-05 | Mất hoặc trùng event; fulfillment không retry |

## 7. Khu vực nguy cơ regression khi sửa module

- **Sales (`SalesRepository`, `SalesPricing`, `SalesAccrualSql`):** sửa tiền hoàn hoặc trạng thái ảnh hưởng Dashboard, Reports, Cockpit, Loyalty, CSDL Dược, CustomerApp (purchases/receivables).
- **Inventory (`IncreaseBatchQuantityAsync`, `BatchResolver`, `LowStockThresholdSql`):** ảnh hưởng nhập, trả, chuyển kho, đầu kỳ, POS FEFO, Dashboard.
- **Auth (file đỏ):** `usePermission.ts`, `AppLayout.tsx`, `DashboardService.cs`, `ReportsAuthorizationExtensions.cs`, `SuccessAuthorizationExtensions.cs`, `DbPermissionsClaimsTransformation.cs`, `PlatformModuleAuthorization.cs`.
- **Schema `sales_orders` / `inventory_batches` / `products` / `customers`:** phải rà Pack + 15–25 file Core + Connect/Content/Clinic.
- **`VietnamBusinessCalendar`:** Dashboard, Cockpit, Loss, Growth, Checklist.

**Cần regression test bảo vệ (ưu tiên):**

1. Trả hàng với voucher, điểm, chiết khấu đơn, trả một phần nhiều lần.
2. Công nợ khách với phiếu thu không gắn đơn.
3. Tổng kết ca có thu nợ.
4. Phiếu nhập theo đơn vị quy đổi.
5. Kiểm kê có bán xen giữa.
6. Ma trận RBAC HTTP: STAFF không thấy doanh thu, CSV, Cockpit; MANAGER/ADMIN thấy.
7. Đánh số chứng từ sau purge.
8. Cách ly tenant cho customerId/poItemId từ client.

**Quy tắc nên kiểm tự động:**

- CI chạy Api.Tests + Application.Tests với Postgres dịch vụ.
- Admin `tsc` bắt buộc xanh.
- Kiểm code tham chiếu cột/bảng mà manifest prod chưa có (so manifest với mig mà code dùng).
- Grep chặn `echo .*CONN` / `COUNT(*)+1` cho số chứng từ.
- Test khởi động Production fail khi bật bất kỳ cờ `ExposePilotOtp*` nguy hiểm.

## 8. Kết quả build, test và phạm vi đã kiểm chứng

| Hạng mục | Kết quả |
|---|---|
| `dotnet build` (qua `dotnet test KitPlatform.slnx`, artifacts tách vào `.tmp/audit-20261104`) | Api/Application/Infrastructure/Packs build được |
| `KitPlatform.Platform.Tests` | **Không biên dịch** (CS7036 ×4) |
| `KitPlatform.Api.Tests` | 13 pass / 25 fail — **do không có Postgres `127.0.0.1:5436`** (port đóng), không phải kết luận nghiệp vụ |
| `KitPlatform.Application.Tests` | 141 pass / 9 fail — 9 fail đều do không kết nối được Postgres |
| Admin `tsc --noEmit` | **2 lỗi** (`AdjustmentListPage.tsx:556`, `PharmacyLocalRegimenPage.tsx:168`) |
| Admin `tsc -p tsconfig.scripts.json` | Pass |
| Kiểm chéo thủ công bởi Cursor | SEC-01, SEC-02, OPS-01, SAL-01, SAL-02, SAL-03, REL-01, INV-01, INV-02, PRC-02, GOV-01, trạng thái 3 lỗi báo cáo cũ |

Trạng thái lỗi từ `NOVIXA-FIX-PLAN-NT_XUANHOA-01`:

- Mốc ngày Tổng quan (`AsUtc`): đã sửa.
- Cận hạn có cận dưới: đã sửa.
- Low-stock phân trang: đã sửa.
- `ActiveBatchCount`: chưa sửa.

## 9. Chưa thể xác minh và lý do

- **Trạng thái DB prod** (ledger, cột `client_return_id`, owner/role bảng để biết RLS có hiệu lực, index tạo tay, PIN `NT_XUANHOA` đã đổi chưa): cấm truy cập prod.
- **Env VPS** (`ExposePilotOtpInResponse`, `ASPNETCORE_FORWARDEDHEADERS_ENABLED`, cờ worker, dùng lại secret dev, số instance API): không đọc `api.env`/secret.
- **Hành vi chạy thật của Sales/Procurement/Inventory qua API:** test tích hợp cần Postgres local. Không dựng DB vì phạm vi cấm chạy migration/seed.
- **Dữ liệu thực tế bị ảnh hưởng:** có phiếu nhập nào dùng đơn vị có `conversion_factor > 1` không; tổng phiếu thu không gắn đơn; số lô xóa mềm còn tồn. Cần `SELECT` chỉ-đọc được ủy quyền riêng.
- **Chưa rà từng service:** ~40 controller Family OS, Connect, Care, Survey chỉ có `[Authorize]`; Content/PersonalIntelligence ngoài phạm vi.
- **CSDL Dược** có chống trùng theo `ReferenceNumber` không.

## 10. Roadmap xử lý theo ưu tiên

### P0 — Bắt buộc trước bất kỳ phát hành nào

1. **SEC-01:** tắt `ExposePilotOtpInResponse` (mặc định code + Production), chặn khởi động, kiểm env VPS. Commit riêng, không gộp Pharmacy auth/Family.
2. **OPS-01:** bỏ in URI có mật khẩu trong 3 runner + `deploy-update-vps.ps1`; rà log deploy cũ; cân nhắc đổi mật khẩu DB.
3. **SEC-02:** đổi 2 endpoint `ops/*` Family OS sang policy vận hành (commit Family OS riêng).
4. **REL-01 / GOV-01:** **không merge/deploy nhánh `feat/famixa-linesheet-build-m1`** cho tới khi:
   - tách commit snapshot `94780487` theo epic (auth / Pharmacy / Famixa);
   - chốt manifest cho 411 và 414–429 (hoặc chặn code theo cột tồn tại).
5. **QA-01:** sửa 2 lỗi `tsc` + 4 lỗi biên dịch Platform.Tests để CI xanh.

### P1 — Bắt buộc trước khi coi số liệu tài chính/tồn là tin cậy

1. SAL-01 hoàn tiền theo tổng thực trả (gồm voucher/điểm) + chặn tổng hoàn ≤ tổng gốc.
2. SAL-02 bỏ trừ credit lần hai trong báo cáo công nợ khách.
3. SAL-03 thêm `sales_shift_id` vào `sales_payments`, tổng kết ca theo cột này.
4. INV-01 quy đổi đơn vị khi hoàn tất phiếu nhập/PO (hoặc tạm khóa chỉ cho đơn vị gốc) + một `SELECT` chỉ-đọc đánh giá dữ liệu đã sai.
5. INV-02 lưu tồn hệ thống lúc đếm, áp delta.
6. PRC-02 bộ đếm số chứng từ theo tenant (sequence/counter có khóa).
7. INV-03 khóa chống trùng tồn đầu kỳ/import.
8. PRC-01 chặn trả vượt NCC + bút toán đảo.
9. SEC-03 rate limit + khóa tạm đăng nhập admin, kèm SEC-04 (`ForwardedHeaders`).
10. SEC-05 chốt nghiệp vụ, rồi giới hạn khoảng ngày ở backend cho người không có `reports.*`; QĐ540 CSV theo quyền phù hợp.

### P2 — Nên làm sớm (an toàn dữ liệu, vận hành)

- Lỗ hổng tenant/chi nhánh: SAL-10, PRC-04, SAL-11, SAL-06, INV-06.
- Nhất quán PO: PRC-03, PRC-05.
- Nghiệp vụ trả hàng/kiểm kê: SAL-05, SAL-09, INV-04, INV-05.
- Concurrency: SAL-07, SAL-08.
- Worker/thanh toán: OPS-02, OPS-05, OPS-07, OPS-08.
- Migration: DB-02, DB-03, DB-04.
- Auth phụ: SEC-06, SEC-08, SEC-09.
- Mở rộng CI chạy Api.Tests/Application.Tests với Postgres service.

### P3 — Cải thiện (có thể chấp nhận tạm thời)

- RLS fail-closed cho bảng lõi (SEC-07), sau khi xác minh role DB.
- ARC-01/02/03: gom SQL Pharmacy của Core về sau interface của pack; tách worker non-Pharmacy khỏi host POS (OPS-09).
- PERF-01..03.
- DB-06..08.
- Các mục Low còn lại.

### Kết luận

- **Phát triển tiếp an toàn ở mức:**
  - Sửa lỗi cục bộ và tính năng mới trên `origin/main`, có test kèm.
  - **Không** đụng schema bảng lõi hay file đỏ RBAC khi chưa có regression test ở §7.
- **Phải xử lý trước khi phát hành:** P0 (5 mục). Trước khi dùng số liệu tài chính/tồn làm căn cứ: P1.
- **Module cần regression test:** Sales (trả hàng, ca, công nợ), Procurement (nhập, đánh số, NCC), Inventory (kiểm kê, đầu kỳ), RBAC HTTP matrix, tenant isolation cho id từ client.
- **Rủi ro chấp nhận tạm thời:**
  - Thiếu RLS bảng lõi (đã có lọc `tenant_id` nhất quán ở đa số query).
  - Nợ kiến trúc Pack/Core.
  - Các mục Low.
  - PERF khi dữ liệu còn nhỏ.
  - SEC-08 nếu chấp nhận là thiết kế Mode A có audit log.
