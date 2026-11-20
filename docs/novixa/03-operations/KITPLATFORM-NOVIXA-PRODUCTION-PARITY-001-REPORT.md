# KITPLATFORM-NOVIXA-PRODUCTION-PARITY-001

**Ngày đọc:** 2026-10-16, khoảng 16:01–16:10 +07  
**Kết luận:** `PARITY_BLOCKED`  
**Phạm vi:** kiểm chứng và báo cáo. Không commit, không push, không deploy, không restart, không migration, không ghi Production.

Production `NT_XUANHOA` đang bán hàng. Phiên này chỉ đọc. Mọi số liệu nghiệp vụ dưới đây là một lát cắt trong lúc có ca đang mở, không phải baseline deploy.

## 1. Baseline và commit

Worktree đang mở là `E:\KitPlatform`, nhánh `main`, HEAD `8c59ceb4f3be537898106ec058ea547406f722e5` (`fix(content): treat a video without a file as failed`, 2026-10-16 10:28 +07). Nhánh này lệch `origin/main`: ahead 6, behind 142. Merge-base là `73d3c4a6c509ab25229d07a475229a6c8c1cf79d`. `origin/main` tại thời điểm đọc là `44fd508552ac47e4f2366aa0cfe1f523cc65be2c`.

HEAD này không phải release candidate. Cây đang dirty và chứa Content/Famixa ngoài Pharmacy.

Mục tiêu kiểm chứng là nhánh `phase5-novixa-product-proof`, worktree `E:\KitPlatform-wt-phase5-novixa-product-proof`.

| Vai trò | Commit | Ý nghĩa |
|---|---|---|
| Architecture freeze | `0c4adc87eb834d2b79c0207dd48fc08f77b71754` | Phase 4. Đóng quyết định, không cấp quyền triển khai. |
| Authority V1 | `d54652d4a7734b3116db62bd356a831f9942fc73` | Canon kiến trúc. Commit `b64f6772` rút các khẳng định tenant chưa được chứng minh. |
| Core/pack split | `c447814d4c3056bb5affe3670cf8b858671686c5` | Đưa workflow Pharmacy ra khỏi Core. Có trên candidate, không có trên `origin/main`. |
| Pharmacy runtime | `76e8177b134017164db5297ffa4b049be713d9d7` | Từ chối lô đã hết hạn. Không thêm SQL Pharmacy. |
| Candidate đã được ghi nhận | `643932c0ba35cac0cad069a789a7197671f4ba24` | Cây build được từ một commit bất biến. Quyết định production-ready trước đó: **NOT READY**. |
| Tip của nhánh | `5c9f65103fd030e37f2788a9b99565a9f918a140` | Chỉ thêm tài liệu so với `643932c0`. Runtime giống candidate. |

Build và test trong phiên này chạy trên tip `5c9f6510`, vì phần code so với `643932c0` không đổi. Candidate chưa được push.

Code đang chạy trên Production **không** có commit đã xác minh. Bằng chứng phiên này:

- `kit-platform-api` active, start `2026-09-29 21:00:52 +07`.
- `/var/www/kit-platform/api/KitPlatform.Api.dll`: 1,786,368 byte, mtime `2026-09-27 11:47:15 +07`, SHA-256 `26afdaa7f123fa36496f31652a01a6d87942e975c4baf72b0b49bfff977a8941`.
- Admin và POS public `Last-Modified` là 2026-09-27. POS `version.json` có `build` `2026-09-25T08:59:48.376Z`. Không có commit nhúng trong file đó.

DLL candidate build trong phiên này khác hẳn: 1,808,896 byte, SHA-256 `B7508B4A6C287410519D6670DE909936A146B9C4B7A2EFFBE76F926EA0E76162`. Build .NET không deterministic, nên hash này định danh lần compile này, không phải một artifact đã upload.

Canon đã đọc: `docs/architecture/INDEX.md`, `docs/KIT-PLATFORM-MASTER-ARCHITECTURE-V1.md`, ADR-03/ADR-04, và hồ sơ Phase 5 trên worktree candidate. Báo cáo cũ không được dùng thay cho số đọc trong phiên này.

## 2. Trạng thái worktree

**Trước**

| Cây | HEAD | Trạng thái |
|---|---|---|
| `E:\KitPlatform` | `8c59ceb4` | 171 file tracked đã sửa, 314 file untracked. Giữ nguyên. |
| Phase 5 | `5c9f6510` | Sạch khi bắt đầu đọc. |

**Sau**

| Cây | Thay đổi của phiên này |
|---|---|
| `E:\KitPlatform` | Thêm duy nhất báo cáo này. Không sửa file tracked khác. Không commit. |
| Phase 5 | Không sửa file tracked. Trong lúc test, xuất hiện file untracked `migrations/371_pack_content_brand_narrative_dna.sql` (63,399 byte, 16:01:36). Phiên này không tạo file đó và không xóa nó. |

`bin/` và `obj/` của build Release nằm trong cây Phase 5 và bị gitignore.

## 3. Build và test

Cây: `E:\KitPlatform-wt-phase5-novixa-product-proof`. Không `dotnet clean`. Không xóa artifact có sẵn.

| Lệnh | Exit | Kết quả |
|---|---|---|
| `dotnet build src\KitPlatform.Api\KitPlatform.Api.csproj -c Release` | 0 | 0 error, 58 warning. Khoảng 27 giây. |
| `dotnet test` `KitPlatform.Application.Tests` `-c Release` | 0 | Passed 26, failed 0, skipped 0. |
| `dotnet test` `KitPlatform.Platform.Tests` `-c Release` | 1 | Passed 163, failed 6, skipped 0. |
| `dotnet test` `KitPlatform.Api.Tests` `-c Release` | 1 | Passed 9, failed 23, skipped 0. |

Warning lặp lại: `NU1903` cho `Newtonsoft.Json` 10.0.3; `CS8602` trong `Program.cs`; `CS0618` Skia trong pack Survey. Không có lỗi biên dịch từ interface Core đã chuyển đi. `IWorkflowEngine` nằm ở `src/Packs/Pharmacy`. `Program.cs` gọi `AddPharmacyPack`. `KitPlatform.Infrastructure.csproj` vẫn reference `KitPlatform.Packs.Pharmacy.Application`.

`KitPlatform.Api.csproj` reference Pharmacy, Clinic, Survey, Connect, FamilyOS, Care, Content, LocalOS và Sales. Content nằm trong binary API. SQL Content không nằm trong `migration-files.prod.txt`.

SPA: `npm run build` là **NOT TESTED**. Worktree Phase 5 không có `node_modules`. Không tạo junction và không build admin trong phiên này.

### Test fail đã phân loại

**Platform.Tests, 6 fail**

- Bốn test Content (`ProductionCustomerIsolationTests`, `ProductionOwnershipHttpTests`) dừng vì biến `KPA_PHASE15_DISPOSABLE_PG` không được đặt. Đây là môi trường test, không phải lỗi Pharmacy.
- `LocalOsTextExtractTests.Extracts_org_pay_and_type` và `LocalOsTextExtractSummaryTests.StripHtml_keeps_lead_that_starts_with_title_not_truncated_meta` fail assertion. Thuộc Local OS, ngoài cổng Pharmacy.

**Api.Tests, 23 fail**

Nguyên nhân đã đối chiếu, không phải kho Xuân Hòa bị trống.

`AuthService` khi `Auth:DefaultTenantCode` rỗng thì lấy `Assessment:EventTenantCode`. Đoạn này có từ `9ca9e340`, là tổ tiên của `origin/main`, không phải commit Phase 5. `appsettings.Development.json` đặt event tenant là `NVX-CS08`. Các test bán hàng, kho và nhập hàng đăng nhập `admin` **không** gửi `tenantCode`, nên đi vào `NVX-CS08`. Tenant local đó có admin đang active, 0 kho và 0 sản phẩm. `GetWarehousesAsync` trả mảng rỗng; `First()` ném `Sequence contains no elements`. Một test tồn kho nhận `total < 1`.

`ControlledPilotSaleTests` gửi `tenantCode=DEMO_PHARMACY` và pass cả 3 test, gồm một bán hàng FEFO trên local và từ chối lô hết hạn. `SalesReadOnlyPrincipalTests` pass. Bốn test còn lại chỉ kiểm tra 401 khi chưa đăng nhập.

`Customer_consent_upsert_writes_outbox_event` fail vì không kết nối được `localhost:80`. Lỗi môi trường.

Database của các test ghi là local `kitplatform` trên `127.0.0.1:5432`, không phải `novixa_prod`. Trong 30 phút quanh lúc chạy test, local `DEMO_PHARMACY` có 1 đơn mới; local `NT_XUANHOA` có 0 đơn mới. Ledger local chỉ có 141 dòng, trong khi Production có 232. Local không phải bản sao schema Production.

## 4. Ma trận Platform / Pharmacy

Đọc Production lúc 2026-10-16 16:01 +07, hostname `novixa`, cùng máy mà `scripts/deploy-update-vps.ps1` trỏ tới. `GET https://api.novixa.vn/api/health` = 200 `{"status":"ok"}`. `GET /api/health/db` = 200, `database: true`. `GET /api/sales/pos/consultation/symptom-options` không token = 401.

| Hạng mục | Trạng thái | Bằng chứng phiên này |
|---|---|---|
| API process và health | PASS | Service active. Health và health/db 200. |
| Binary là candidate | FAIL | DLL 2026-09-27, hash khác build candidate. Commit nguồn của DLL: **BLOCKED**. |
| Runtime | PASS | `/usr/bin/dotnet`, `Microsoft.NETCore.App` và `Microsoft.AspNetCore.App` 10.0.9. Candidate target `net10.0`. |
| Auth / current user trên Production | NOT TESTED | Không đăng nhập Production. |
| Tenant mặc định của process | PASS cho file đang publish | `api.env` không có `Auth__DefaultTenantCode`. `appsettings.Production.json` trên máy đặt `DefaultTenantCode` = `NT_XUANHOA`. Không dump bộ nhớ process. |
| Fallback tenant khi key rỗng | FAIL như một hợp đồng dev, không phải sự cố đang chạy | Code lấy assessment tenant. Production đang set `NT_XUANHOA`, nên fallback không được chọn. |
| Cô lập tenant khi bán | NOT TESTED trên Production | Pilot local từ chối chạy nếu tenant là `NT_XUANHOA`. |
| Quyền báo cáo không đi theo `sales.pos` | PASS trên dữ liệu vai trò | `CASHIER` và `STAFF` có `sales.pos`, không có `reports.read`. `ADMIN` và `MANAGER` có `reports.read`. 5 user active có `sales.pos`; 3 người trong số đó không có `reports.read` và không phải `ADMIN`. Không liệt kê tên. |
| Đăng nhập quầy thật | NOT TESTED | |
| Module registry chi tiết | NOT TESTED | Không đọc JSON module của tenant. |
| Vertical và batch mode | PASS | `business_vertical=pharmacy`, `settings.platform.vertical=pharmacy`, `batch_mode=suggest`. |
| Biên dịch ranh giới pack | PASS | API Release 0 error. |
| Nợ ranh giới Core | PASS như quan sát, chưa đóng | Infrastructure vẫn reference Pharmacy.Application. Không chặn compile. |
| Catalog mode | PASS | `NationalDrugCatalog__Mode=sandbox`, `EnableStockOutSync=false`. |
| Liên kết CSDL Dược của `NT_XUANHOA` | PASS | 0 dòng `tenant_csdl_duoc_link`, 0 dòng `csdl_duoc_sync_log`. |

## 5. Schema và migration

Database Production: `novixa_prod`, kết nối peer local socket trên chính VPS. `default_transaction_read_only=on`. Không áp migration.

| Nguồn | Số file |
|---|---|
| Ledger `kit_schema_migrations` | 232 |
| `migration-files.prod.txt` của candidate | 221, mọi tên đều có file trong `migrations/` |
| `origin/main` manifest | 223 |

Candidate bỏ `230_pack_family_parent_emotion_push.sql` và `231_pack_family_memory.sql` so với `origin/main`. Cả hai **đã có** trong ledger Production.

Mười một tên có trên ledger và không có trong manifest candidate:

- `230_pack_family_parent_emotion_push.sql`
- `231_pack_family_memory.sql`
- `281`–`288` (Content wave, brand, video factory)
- `290_pack_content_video_mvp_v1.sql`

`294`–`300` (tư vấn quầy), `289`, `292`, `371_inventory_stock_review.sql` và `seed-prod/001_base_permissions.sql` có trong cả ledger và manifest candidate. `383`, `384`, `387`, `388` không có trong ledger. Chúng chỉ nằm trên `migration-files.content.txt`.

Candidate không thêm file SQL Pharmacy mới. Nâng ledger hiện tại bằng manifest candidate, nếu runner chỉ áp file chưa có, không thấy file Pharmacy còn thiếu. Điều này là đối chiếu tên, chưa chạy runner.

Hai kịch bản chưa được thực thi:

- **Nâng database hiện có:** NOT TESTED. Không có bản sao.
- **Tạo database mới từ manifest candidate:** NOT TESTED. Manifest đó sẽ không áp 230, 231 và các file Content 281–288, 290 đã có trên Production.

Index `ix_batches_fefo` có trên `inventory_batches`. Khóa ngoại tới tenant, warehouse, product, supplier và dòng phiếu nhập đều có. Không đọc định nghĩa đầy đủ của mọi constraint.

Local `kitplatform` có 141 dòng ledger. Không được dùng làm chứng cứ schema Production.

## 6. Nghiệp vụ NT_XUANHOA

Chỉ số đọc được. Không tạo đơn, không nhập, không trả, không mở ca.

| Nhóm | Trạng thái | Quan sát |
|---|---|---|
| Danh mục và đơn vị | PASS cho số lượng | 3,032 sản phẩm chưa xóa. `drug_type` 1: 3,029; `drug_type` 2: 3. 4,094 `product_units`; 4,088 base; 4,094 sale. Không mở từng dòng giá. |
| Lô, hạn dùng, FEFO | PASS cho tồn; NOT TESTED cho phép phân bổ trên Production | 3,094 lô còn số lượng. 3,091 còn hạn, số lượng 3,874,528.001. 3 lô hết hạn, số lượng 231. 0 lô không hạn còn số lượng. Index FEFO tồn tại. |
| POS và đơn hoàn tất | PASS cho sổ đang có; NOT TESTED cho một phiên bán của candidate | Đơn status 2 (completed): 6,448. Status 4 (refunded): 8. Không có draft hay cancelled trong nhóm này. `stock_movements` `sales_order`: 12,146. |
| Nhập hàng | PASS cho sổ | 431 `goods_receipts`. Movement `goods_receipt`: 2,222. Không lập phiếu mới. |
| Trả hàng | PASS cho sổ | 14 `sales_returns`. Movement `sales_return`: 23. Không tạo phiếu trả. |
| Ca bán | PASS cho lát cắt | Status 1 (open): 2. Status 2 (closed): 147. Quầy đang mở. |
| Tư vấn quầy | PASS cho schema và số phiên | Bảng và 190 dòng `pharmacy_symptom` có. Phiên của `NT_XUANHOA`: 0. API candidate chưa phục vụ quầy này. |
| Đồng bộ danh mục quốc gia | PASS | Mode sandbox, sync tắt, 0 link, 0 log. |
| Hành vi candidate trên dữ liệu này | NOT TESTED | Từ chối lô hết hạn mới có trong `SellableLotRules`: lô có ngày và ngày đó trước hôm nay thì không bán; lô không ngày vẫn bán được. Production hiện có 231 đơn vị ở 3 lô hết hạn. Candidate sẽ đổi khả năng bán của số đó. Chưa bán thử trên bản sao. |

Movement khác của cùng tenant, chỉ để thấy sổ kho đang sống: `opening_balance` 6,891; `StockReset` 2,890; `inventory_adjustment` 2,883; `inventory_transfer` 2,300; `COST_REVALUE` 835; `product_merge` 398.

## 7. Lỗi và khoảng trống, theo thứ tự phụ thuộc

Chưa sửa. Architect chốt phạm vi.

### P1 — Không có bản sao đã xác minh

- Mức: cao. Thành phần: môi trường kiểm chứng.
- Bằng chứng: Production là `novixa_prod` trên VPS. Local `kitplatform` có 141 migration và một dòng tenant `NT_XUANHOA`, nhưng không phải bản sao ledger 232.
- Nguyên nhân: đã xác minh là thiếu môi trường, không phải lỗi code.
- Ảnh hưởng `NT_XUANHOA`: chưa đụng dữ liệu. Không thể chứng minh nâng schema hay một bán hàng candidate trên hình dạng dữ liệu thật.
- Phạm vi sửa tối thiểu: một bản sao đọc được, restore từ dump đã `gzip -t`, trên máy không phải Production. Xác nhận `inet_server_addr` và tên database trước khi chạy gì thêm.
- Test cần thêm: runner manifest ở chế độ nâng; một database trống tạo từ cùng manifest; bán FEFO, từ chối lô hết hạn, nhập, trả, mở/đóng ca; đối chiếu API với SQL.

### P2 — Rollback chưa được thực thi

- Mức: cao cho một lần deploy sau này. Thành phần: `scripts/deploy-update-vps.ps1`, đĩa `/var/www/kit-platform`.
- Bằng chứng: API `rsync` không `--delete`; admin, staff, customer, prescriber và family `rsync --delete`. Script ghi đè cây đang chạy. `/var/www/kit-platform.prev` và `/opt/kit-platform/releases` không tồn tại.
- Ba trạng thái rollback: được mô tả trong hồ sơ Phase 5; **không** có script giữ bản cũ; **chưa** thử thành công trên môi trường cô lập.
- Ảnh hưởng: một deploy lỗi không có bản binary trước đó trên máy, trừ khi operator tự copy trước.
- Phạm vi: không sửa script trong nhiệm vụ này. Cần một host không phải Production để deploy candidate rồi trả lại DLL `26afdaa7…`.

### P3 — Backup cron không ổn định

- Mức: cao cho khả năng restore, không phải lỗi pack.
- Bằng chứng: cron `0 2 * * *` gọi `/opt/kit-platform/backup-db.sh` với `DB_NAME=novixa_prod`. Hầu hết file ngày từ 2026-07-27 đến 2026-10-15 02:00 là 20 byte. Hai file lớn qua `gzip -t`: `novixa_prod_20261015_111346.sql.gz` (14,836,182 byte) và `novixa_prod_20261016_020001.sql.gz` (15,019,608 byte). Không restore.
- Ảnh hưởng: lịch 02:00 không chứng minh được một dump dùng được mỗi ngày. File 20 byte không phải bản sao lưu.
- Phạm vi tối thiểu: sửa script backup trên một bản sao, rồi restore một dump `gzip -t` vào database không phải `novixa_prod`.

### P4 — Hợp đồng tenant mặc định khi key rỗng

- Mức: trung bình. File: `src/KitPlatform.Infrastructure/Auth/AuthService.cs`.
- Bằng chứng: key rỗng thì dùng `Assessment:EventTenantCode`. Development đặt `NVX-CS08`. 23 API test Pharmacy fail vì đăng nhập nhầm tenant không có kho. Production publish đang set `NT_XUANHOA`, nên process hiện tại không đi nhánh này.
- Nguyên nhân: đã đọc code và dữ liệu local. Không capture JWT của test fail, nhưng số kho 0 khớp đúng tenant fallback.
- Ảnh hưởng `NT_XUANHOA` hôm nay: không, chừng nào file publish còn giữ `DefaultTenantCode`.
- Phạm vi tối thiểu: bỏ fallback sang assessment tenant, hoặc bắt test Pharmacy gửi `tenantCode`. Không nuốt lỗi và không tắt test.
- Test: đăng nhập không `tenantCode` khi default rỗng phải thất bại rõ, không vào `NVX-CS08`.

### P5 — Đổi nghĩa bán lô hết hạn

- Mức: trung bình, cần chủ nhà thuốc chấp nhận trước deploy. File: `SellableLotRules.cs`.
- Bằng chứng code và test local pass. Production có 231 đơn vị trên 3 lô hết hạn. Candidate sẽ không bán các lô đó. Lô không ngày vẫn bán được; Production hiện có 0 lô không ngày còn hàng.
- Chưa chứng minh trên bản sao Production.

Không có lỗi biên dịch Platform. Không có migration Pharmacy còn thiếu trên ledger hiện tại.

## 8. VPS hiện có

Đọc lúc 2026-10-16 16:01:49 +07. Không restart, không sửa file, không ghi.

| Hạng mục | Kết quả |
|---|---|
| CPU | 6, load `0.00 0.00 0.00` |
| RAM | 9,968 MB, dùng 1,665 MB, available 8,303 MB, không swap |
| Đĩa `/` | 58 GB, dùng 4.0 GB (7%) |
| `dotnet` trên PATH của shell | không có. Service dùng `/usr/bin/dotnet` và runtime 10.0.9 |
| Service | `kit-platform-api` active. `pharmacore-api` inactive. User `www-data`. `EnvironmentFile=/etc/kit-platform/api.env` |
| Nginx | 1.24.0. `listen` 80 và 443. `server_name` gồm `api.novixa.vn`, `admin.novixa.vn`, `pos.novixa.vn`, `app.novixa.vn`, `survey.novixa.vn`, `prescriber.novixa.vn`, `home.famixa.vn`, `family.kittech.vn` |
| Cổng | 80/443 public. API `127.0.0.1:5000`. Postgres `127.0.0.1:5432` và `[::1]:5432` |
| Database | `novixa_prod` |
| Artifact | script deploy ghi đè cây đang chạy, không giữ release trước |
| Backup | cron có. Hai dump gần đây qua `gzip -t`. Nhiều file 20 byte. Restore: NOT TESTED |
| Rollback đã thử | NOT TESTED |

Không mua VPS mới. Không triển khai.

## 9. Chưa chứng minh được

- Commit Git của DLL đang chạy.
- Một phiên đăng nhập, bán, nhập hoặc trả trên Production. Cố ý không làm.
- Candidate áp lên schema 232 dòng, vì không có bản sao.
- Database mới tạo từ manifest candidate có đủ bảng mà Production đang dùng.
- Bộ module đang bật trong JSON settings, ngoài vertical và `batch_mode`.
- `npm run build` của admin và các SPA trong `deploy-production.ps1` trên cây này.
- Restore dump và rollback binary.
- Rằng 23 API test fail là lỗi kho của `DEMO_PHARMACY` hoặc `NT_XUANHOA`. Bằng chứng hiện có nói chúng đăng nhập nhầm tenant.

## 10. Điều kiện trước khi làm pack khác

1. Giữ Production chỉ đọc. Mọi ghi và migration chỉ trên bản sao đã chứng minh không phải `novixa_prod`.
2. Architect chốt P4 (fallback tenant) và P5 (lô hết hạn) trước khi sửa code.
3. Có một database clone, chạy đủ manifest candidate theo hai lối: nâng ledger hiện có và tạo database trống.
4. Trên clone, chạy bán FEFO, từ chối lô hết hạn, nhập, trả, ca, và kiểm tra cô lập tenant.
5. Build lại API và toàn bộ test Platform/Pharmacy, cộng SPA của script deploy, trên đúng commit.
6. Có một host không phải Production để cài candidate rồi trả lại binary cũ. Ghi cả hai hash.
7. Một lần restore dump `gzip -t` vào database không phải Production, sau khi cron backup không còn ra file 20 byte hoặc chủ sở hữu chấp nhận lịch backup khác.
8. Không deploy candidate lên `api.novixa.vn` từ báo cáo này.

## 11. Kết luận

`PARITY_BLOCKED`

Candidate `643932c0` / tip tài liệu `5c9f6510` biên dịch được và không đòi migration Pharmacy mới so với ledger `novixa_prod` đã đọc. Process Xuân Hòa vẫn là binary 2026-09-27, quầy đang mở, và candidate chưa được chứng minh trên một bản sao của database đó. Rollback binary và restore database đều chưa được thực hiện. Vì vậy chưa đủ bằng chứng để tiếp tục hoàn thiện pack khác.
