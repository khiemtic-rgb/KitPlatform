# KIT-PLATFORM-NOVIXA-RELEASE-BLOCKERS-002

**Ngày:** 2026-10-16  
**Kết luận:** `BLOCKED`  
**Không deploy.** Không commit, không push, không merge, không restart service Production, không chạy migration trên Production, không sửa cron hoặc file backup trên VPS.

Báo cáo này tiếp nối `KIT-PLATFORM-NOVIXA-RELEASE-READINESS-001-REPORT.md`. Candidate vẫn là worktree `E:\KitPlatform-wt-phase5-novixa-product-proof`.

## 1. Baseline đã xác minh trước khi sửa

| Cây | HEAD | Trạng thái |
|---|---|---|
| `E:\KitPlatform` | `main` `8c59ceb4f3be537898106ec058ea547406f722e5` | Dirty từ trước (Content/Famixa và các file khác). Phiên này chỉ thêm báo cáo này. |
| `E:\KitPlatform-wt-phase5-novixa-product-proof` | `phase5-novixa-product-proof` `5c9f65103fd030e37f2788a9b99565a9f918a140` | Đúng HEAD của báo cáo 001. Không reset, clean, stash, checkout đè hoặc xóa thay đổi. |

File untracked có từ trước, phiên này không tạo và không sửa: `migrations/371_pack_content_brand_narrative_dna.sql`.

`643932c0ba35cac0cad069a789a7197671f4ba24` và `5c9f6510` không còn đại diện cho source sau các sửa chưa commit. DLL lab `FC90329B…` là bản compile lúc 16:30 +07 từ source auth chưa commit, trước khi phiên này sửa script migration. Hash đó không khóa source hiện tại.

## 2. Diff candidate

Đã đọc `git diff` từng file tracked. Không có sửa ngoài các mục dưới.

### Đã có từ báo cáo 001

| File | Phạm vi |
|---|---|
| `src/KitPlatform.Infrastructure/Auth/AuthService.cs` | Bỏ fallback `Assessment:EventTenantCode` và tenant ngầm `DEMO_PHARMACY`. Thiếu tenant thì ném `AuthTenantRequiredException`. |
| `src/KitPlatform.Application/Auth/AuthContracts.cs` | Exception «Mã nhà thuốc là bắt buộc khi chưa cấu hình tenant mặc định.» |
| `src/KitPlatform.Api/Controllers/AuthController.cs` | Trả 400, không cấp token. |
| `src/KitPlatform.Infrastructure/CustomerApp/CustomerAppAuthService.cs` | OTP khách không còn lấy tenant sự kiện khi key mặc định rỗng. |
| `tests/KitPlatform.Api.Tests/DefaultTenantResolutionTests.cs` | Untracked. Hai test tenant. |
| `CatalogApiTests.cs`, `CustomerConsentApiTests.cs`, `InventoryApiTests.cs`, `ProcurementApiTests.cs`, `SalesApiTests.cs` | `tenantCode = DEMO_PHARMACY`, `vatTreatmentId`, đọc danh sách qua `items`. |
| `deploy/ubuntu/backup-db.sh` | Bản copy của script đang chạy trên VPS: dump peer không `-U`, ghi `.partial`, `gzip -t`, kiểm tra header, retention không xóa khi chưa có cờ. |

### Thêm trong phiên này

| File | Phạm vi |
|---|---|
| `deploy/ubuntu/run-migrations-prod.sh` | Trước `psql -f`, bỏ BOM UTF-8 trên bản tạm. Nếu có `deploy/ubuntu/greenfield-compat/<tên file manifest>` thì thực thi bản đó. Ledger vẫn ghi tên file trong manifest. |
| `deploy/ubuntu/run-incremental-migrations-prod.sh` | Cùng quy tắc, chỉ với file chưa có trong `kit_schema_migrations`. |
| `deploy/ubuntu/greenfield-compat/060_family_members_status_align.sql` | Thân thay thế cho file lịch sử `060`. Không nằm trong `migration-files.prod.txt`. |

`migrations/060_family_members_status_align.sql` không đổi. `git diff --exit-code` với file đó trả 0. SHA-256 `E84257CC824305F66EA228092717403EA83FEF20388B0DD11D82856861B70180`. Git blob `f6dae5ea3cd85f9d219f50528a3709803642887b`.

SHA-256 thân thay thế: `E01F0824B7143D76E5D352928D1E83400B3ECF289CCBB688F61C7DE6DFA52226`.

## 3. Cách khóa source bằng commit

Phiên này không commit. Lệnh dưới đây là quy trình để chủ sở hữu chạy sau, khi chấp nhận đúng các file này.

```powershell
cd E:\KitPlatform-wt-phase5-novixa-product-proof
git add -- `
  deploy/ubuntu/backup-db.sh `
  deploy/ubuntu/run-incremental-migrations-prod.sh `
  deploy/ubuntu/run-migrations-prod.sh `
  deploy/ubuntu/greenfield-compat/060_family_members_status_align.sql `
  src/KitPlatform.Api/Controllers/AuthController.cs `
  src/KitPlatform.Application/Auth/AuthContracts.cs `
  src/KitPlatform.Infrastructure/Auth/AuthService.cs `
  src/KitPlatform.Infrastructure/CustomerApp/CustomerAppAuthService.cs `
  tests/KitPlatform.Api.Tests/CatalogApiTests.cs `
  tests/KitPlatform.Api.Tests/CustomerConsentApiTests.cs `
  tests/KitPlatform.Api.Tests/DefaultTenantResolutionTests.cs `
  tests/KitPlatform.Api.Tests/InventoryApiTests.cs `
  tests/KitPlatform.Api.Tests/ProcurementApiTests.cs `
  tests/KitPlatform.Api.Tests/SalesApiTests.cs
git commit -m "fix(novixa): fail closed without a tenant and keep fresh schema apply compatible with the existing ledger"
```

Không đưa `migrations/371_pack_content_brand_narrative_dna.sql` vào commit đó.

Sau commit, build lại Release và ghi hash DLL mới. Hash `FC90329B467C66765531E58CF77C3AA7DB5AAC18D1FC6B995AD677B6A762350A` chỉ định danh file `C:\novixa-rr001-lab\publish-api\KitPlatform.Api.dll` (1,808,896 byte, 16:30 +07). Build .NET không deterministic. Hash này không gồm script shell vừa sửa.

### Build tái lập API

Cây: `E:\KitPlatform-wt-phase5-novixa-product-proof`, sau khi commit ở trên.

```powershell
dotnet publish src\KitPlatform.Api\KitPlatform.Api.csproj -c Release -o <thư-mục-publish> --no-self-contained
Get-FileHash <thư-mục-publish>\KitPlatform.Api.dll -Algorithm SHA256
```

SPA mà `deploy-update-vps.ps1` rsync, biến `VITE_API_BASE_URL=https://api.novixa.vn`: admin, customer-app, staff-app, prescriber-portal, family-app. Đã build trong phiên 001 tại `C:\novixa-rr001-lab\spa\`. Phiên này không build lại. `partner-portal` và `assessment-web` vẫn nằm ngoài bước rsync bắt buộc của script update.

Cấu hình Production (`/etc/kit-platform/api.env`, secrets) không nằm trong Git và không được đóng gói.

`deploy-update-vps.ps1` mặc định bỏ qua migration. Cờ `-RunMigrations` mới gọi incremental. Với ledger Xuân Hòa và manifest hiện tại, pending = 0 (mục 5). Script vẫn rsync `deploy/ubuntu/` lên `/opt/kit-platform/` kể cả khi bỏ qua migration, nên lần deploy sau sẽ thay script trên đĩa. Phiên này không chạy script đó.

## 4. Rollback service

**Trạng thái: còn mở.**

Đã tìm môi trường lab có layout tương đương máy triển khai:

| Thành phần Production | Máy làm việc này |
|---|---|
| systemd unit `kit-platform-api` | Không có systemd |
| nginx, site `/var/www/kit-platform` | Không có nginx, không có cây `/var/www/kit-platform` |
| `/opt/kit-platform`, `/etc/kit-platform/api.env` | Không có |
| WSL | Chưa cài |
| Docker | Không có lệnh `docker` |
| Hyper-V / NSSM | Không có `Get-VM`, không có `nssm` |

Không dựng VPS mới. Không restart `kit-platform-api`. Không rsync. Không sửa Nginx.

Diễn tập đổi file DLL trong `C:\novixa-rr001-lab\rollback-drill` thuộc phiên 001. Đó là copy trên đĩa, không phải restart service và không phải rollback một process đang phục vụ.

Phiên này có chạy và tắt process lab `dotnet KitPlatform.Api.dll` nghe `http://127.0.0.1:5299`, connection `127.0.0.1:5433` / `novixa_rr001`. Process đó không dùng systemd, không dùng nginx, không dùng đường dẫn Production. Tắt process lúc khoảng 20:14 +07. Việc này không chứng minh rollback Production.

Điều kiện còn thiếu: một máy không phải Production, có systemd, nginx và cây `/var/www/kit-platform`, để lưu bản cũ, đổi binary và static, `systemctl restart kit-platform-api`, health check, copy bản cũ trở lại, restart lần nữa, rồi smoke. Chưa có máy đó nên mục này chưa đạt.

## 5. Migration cài mới

**Nâng ledger Xuân Hòa: pending = 0, đã kiểm lại.**  
**Cài database trống từ manifest: vẫn FAIL. Không đưa file migration mới vào `migration-files.prod.txt`.**

Ledger khóa theo `filename`, không theo hash nội dung (`deploy/ubuntu/run-incremental-migrations-prod.sh`).

### Lab

Cluster `C:\novixa-rr001-lab\pgdata`, chỉ `127.0.0.1:5433`. Lần start đầu với `listen_addresses=localhost` bị từ chối bind IPv6. Start lại với `-c listen_addresses=127.0.0.1 -c port=5433` thì lên. Cuối phiên đã `pg_ctl stop`. Dữ liệu còn trên đĩa lab.

| Database | Vai trò |
|---|---|
| `novixa_rr001` | Bản restore dump `20261016_020001` từ phiên 001 |
| `novixa_rr001_fresh` | Lần cài dở của phiên 001, dừng ở file 060 |
| `novixa_rr002_greenfield` | Database trống tạo trong phiên này |

Không có database tên `novixa_prod` trên cluster này. Không dùng `kitplatform` cổng 5432.

### BOM

41 file `migrations/*.sql` có BOM UTF-8. 35 file trong số đó nằm trong `migration-files.prod.txt` (221 file). `001_extensions.sql` bắt đầu `EF BB BF`. `psql` báo syntax error nếu chạy file gốc. Đã thấy lại trong `pg.log` của phiên 001.

Runner mới ghi bản tạm đã bỏ BOM rồi mới `psql -f`. File trong Git không bị sửa. Trên `novixa_rr002_greenfield`, `001_extensions.sql` chạy qua (`CREATE EXTENSION`).

### File 060

Schema mới từ `052_family_members.sql` đã có `status` và `gender`, không có `is_active`. File lịch sử 060 vẫn `UPDATE … WHEN is_active`. `EXISTS` trong `WHERE` không chặn lỗi parse. Chạy lại file gốc trên `novixa_rr001_fresh`: exit 3, `column "is_active" does not exist`.

Bản sao Production `novixa_rr001`:

- Ledger 232 dòng, có `060_family_members_status_align.sql`.
- `family_members` có `status SMALLINT NOT NULL DEFAULT 1`, không có `is_active`. 0 dòng.

Thân tương thích đưa `UPDATE` từ `is_active` vào `EXECUTE` bên trong `IF EXISTS` cột. Khi cột không có, nhánh đó không chạy. Các lệnh còn lại giữ nguyên ý: thêm cột nếu thiếu, gán status 1 khi null, default 1, `NOT NULL`, index.

Đã chạy thân này trên `novixa_rr001` trong `BEGIN` … `ROLLBACK`: exit 0, `UPDATE 0`, `is_active` vẫn 0 cột, ledger sau rollback vẫn 232.

### Incremental trên bản sao

```text
bash deploy/ubuntu/run-incremental-migrations-prod.sh \
  postgresql://postgres@127.0.0.1:5433/novixa_rr001
```

Kết quả: `applied=0 skipped=221 pending_ran=0`. Log không có `COMPAT_BODY` và không có `APPLY`. Ledger sau chạy vẫn 232. File 060 đã ghi sổ nên runner không thực thi thân thay thế và không đọc lại file lịch sử.

### Cài mới

`run-migrations-prod.sh` trên `novixa_rr002_greenfield`:

- Qua BOM của file 001.
- Tại 060: log `COMPAT_BODY 060_family_members_status_align.sql historical_file_not_executed`. Sau đó `family_members.status` là `NOT NULL`, default 1, không có `is_active`.
- Dừng ở `090_kap_pharmacy_intelligence_seed.sql`, exit 3: `template_id a0000001-0000-4000-8000-000000000001` không có trong `assessment_template`.

Nguyên nhân: `090` ghi phụ thuộc `069_assessment_pharmacy_v1_seed.sql`. File 069 không có trong `migration-files.prod.txt`. Ledger bản sao cũng không có tên file đó. Template trên bản sao đã tồn tại với `code = PHARMACY_V1`, `version = 1.1` (đã được migration sau nâng từ 1.0).

Chạy 069 trên bản sao trong transaction rồi rollback: `duplicate key value violates unique constraint "assessment_template_pkey"`. `ON CONFLICT (code, version)` không khớp vì bản sao đang ở version `1.1` còn file 069 chèn version `1.0` cùng khóa chính. Đưa 069 vào manifest Production sẽ thành một file pending và lần `-RunMigrations` sau sẽ lỗi trên ledger hiện hữu. Vì vậy không thêm 069 vào manifest.

Tiếp tục chỉ trên `novixa_rr002_greenfield`, sau khi áp 069 vào database trống đó và `CREATE ROLE kitplatform` trên cluster lab (role này có trên Production, cluster lab mới không có). Từ file 090 đến `371_inventory_stock_review.sql` chạy hết. Bảng `inventory_stock_reviews` có mặt. Template `PHARMACY_V1` version `1.1`. Ledger do file 098 backfill còn 103 dòng, trong đó có tên `060` và không có tên `069`.

Kết luận cài mới: runner đã vượt BOM và file 060 mà không sửa file lịch sử và không áp gì lên ledger đã có. Manifest Production một mình vẫn không cài được database trống, vì thiếu 069 và vì 069 không tương thích với ledger đã nâng version template. Blocker cài mới giữ nguyên. Đường nâng Xuân Hòa vẫn pending = 0.

## 6. Kiểm thử NOVIXA trên bản sao

API lab `http://127.0.0.1:5299`, database `novixa_rr001`, cổng 5433. `Auth__DefaultTenantCode` rỗng. Worker outbox, platform events, notify, push, family reminder, content và đồng bộ danh mục thuốc quốc gia tắt. Không gọi API AI trả phí. Process đã tắt sau probe.

`python C:\novixa-rr001-lab\business_probe.py` lúc database ghi `2026-10-16 20:12 +07`. Identity `novixa_rr001|127.0.0.1|5433`.

| Kịch bản | Kết quả |
|---|---|
| `GET /api/health` | 200 |
| Login không tenant | 400 «Mã nhà thuốc là bắt buộc khi chưa cấu hình tenant mặc định.» |
| Login `NT_XUANHOA` (`rr001lab`, `rr001cashier`) | 200, `tenantCode=NT_XUANHOA` |
| Báo cáo doanh thu admin | 200 |
| Cùng URL bằng cashier | 403 |
| GET sản phẩm tenant khác | 404 |
| GET sản phẩm cùng tenant | 200 |
| Preview 1 đơn vị | 200, lô `TON-DAU-SAPO`. Không có lô hết hạn `60551` |
| Mở ca, bán, trả, đóng ca | Ca 200 status 1 rồi 2. Đơn 201 status 2. Trả 201. Hết hạn trước/sau `2.000`. Lô còn hạn `1.000` → `0.000` → `1.000` |
| Phiếu nhập không PO, lô `RR001LAB57B6`, hạn 2030-12-31, số lượng 1 | Tạo 201, hoàn tất 200. `quantity` dòng nhập = 1. Tồn lô trên clone thành `2.000` vì phiên 001 đã nhập 1 |
| Mở và đóng ca kho còn lại | 200 status 1 rồi 2 |

Probe in `QTY 0` vì chuỗi tra cứu chữ thường `RR001LAB57b6` không khớp `RR001LAB57B6` trong database. Số liệu SQL ở trên là số đã đối chiếu.

User lab `rr001lab` và `rr001cashier` chỉ có trên clone, tạo từ phiên 001. Không tạo user trên Production.

`NT_XUANHOA` trên clone vẫn có 3 lô hết hạn, 231 đơn vị (hai lô hết `2026-03-31` tổng 229; một lô hết `2026-07-31` số lượng 2). Probe không bán các lô đó. Candidate từ chối bán lô có ngày trước hôm nay. Chủ nhà thuốc chưa xác nhận quy tắc này.

### Test tự động

Phiên này không chạy lại suite trên database `kitplatform` cổng 5432. Database đó không phải bản sao Production. Phân loại dưới đây dựa trên source test, seed `migrations/seed/001_demo_data.sql`, và kết quả phiên 001 trên cùng source chưa commit.

Seed hợp đồng: barcode `8934567890012`, giá bán lẻ `500`, lô gợi ý `LOT2026A`. Không thấy migration prod nào đổi giá barcode đó thành `5000`. Phiên 001 ghi API local trả giá `5000.00` và lô `5555`. Đó là dữ liệu local lệch seed. Không sửa assertion để khớp dữ liệu lệch.

| Test | Trạng thái phiên 001 | Lý do giữ nguyên |
|---|---|---|
| `Sales_barcode_lookup_returns_price` | Fail | Khóa giá `500` đúng seed |
| `Sales_barcode_lookup_includes_fefo_batch_hints` | Fail | Khóa `LOT2026A` đúng seed |
| `Sales_pos_preview_allocation_matches_fefo` | Fail | Khóa `LOT2026A` đúng seed |
| `Sales_draft_complete_and_return_restores_stock` | Fail | Thanh toán `500` theo giá seed |
| `Sales_line_and_order_discount_reduce_total` | Fail | Subtotal `1000` và thanh toán `855` tính từ giá `500` |
| `Sales_return_with_discount_refunds_net_amount` | Fail | `855` / `428` tính từ giá `500` |
| `Sales_draft_update_then_complete` | Fail | Tổng `1000` cho số lượng 2 ở giá `500` |
| `Customer_consent_upsert_writes_outbox_event` | Fail | Không nối được webhook `localhost:80`. Môi trường, ngoài luồng nhà thuốc |
| `DefaultTenantResolutionTests` (2 test) | Pass trong phiên 001 | Login không tenant trả 400; login `DEMO_PHARMACY` trả đúng tenant |
| `ControlledPilotSaleTests`, `SalesReadOnlyPrincipalTests` | Pass trong phiên 001 | FEFO local và từ chối lô hết hạn; cashier không đọc báo cáo |
| Bốn test Content (`ProductionCustomerIsolationTests`, `ProductionOwnershipHttpTests`) | Fail | Đòi `KPA_PHASE15_DISPOSABLE_PG` tới `kpa_phase15_once` cổng `54341`. Ngoài cổng Pharmacy. Không tạo Postgres Content |
| `LocalOsTextExtractTests`, `LocalOsTextExtractSummaryTests` | Fail assertion | Ngoài cổng Pharmacy. Không sửa pack Local OS |

Số phiên 001: Application.Tests 26 pass. Platform.Tests 163 pass / 6 fail. Api.Tests lần cuối 26 pass / 8 fail. Phiên này không lập lại các lệnh `dotnet test` đó.

Luồng nhà thuốc dùng để xét deploy là probe trên `novixa_rr001`, mục bảng ở trên. Suite `kitplatform:5432` không được dùng làm bằng chứng Production.

## 7. Backup

Không xóa file trên VPS. Không sửa cron. Không đặt `RETENTION_DELETE_CONFIRMED`.

Dump local `C:\novixa-rr001-lab\novixa_prod_20261016_020001.sql.gz` kiểm lại trong phiên này:

| Chỉ số | Giá trị |
|---|---|
| Kích thước | 15,019,608 byte |
| Header | `1f8b0800` |
| `gzip` đọc hết | 73,578,246 byte, CRC hợp lệ |
| `\connect` | không có |
| `CREATE DATABASE` | không có |
| `CREATE TABLE public.tenants` | có |

**Restore đã thử:** dump này, vào `novixa_rr001` trên cổng 5433, phiên 001, `psql` exit 0. Phiên này dùng chính database đó cho probe và cho incremental skip. Đối chiếu số liệu dump 02:00 với Production lúc đọc đã ghi trong báo cáo 001; phiên này không đọc lại Production.

**Restore chưa thử:** các dump lớn còn lại và các dump tháng 7 khoảng 4 MB đã nêu trong báo cáo 001. 81 file 20 byte không phải bản restore.

File 20 byte là gzip rỗng (header cộng trailer), stdout `pg_dump` thất bại vì script Git cũ dùng `pg_dump -U` trên socket peer. Script trên VPS và bản `backup-db.sh` trong candidate đã ghi `.partial` và chỉ `mv` sau khi kiểm tra. Cron `0 2 * * *` đã ra `BACKUP_OK` ngày 2026-10-16 theo báo cáo 001. Retention đang treo (`RETENTION_DELETE_SUSPENDED`).

Cách xử lý đề xuất, chưa thực hiện:

1. Liệt kê từng file: kích thước, `gzip -t`, số byte sau giải nén, có dòng `PostgreSQL database dump` và `CREATE TABLE public.tenants` hay không.
2. File đúng cả bốn kiểm tra là backup hợp lệ.
3. File đúng 20 byte, giải nén rỗng, không có header dump: archive hỏng. Giữ trên đĩa cho đến khi một người vận hành xác nhận danh sách, rồi chuyển (không `rm`) vào thư mục cách ly cùng máy. Không dùng `find -delete`. Không bật `RETENTION_DELETE_CONFIRMED` chỉ để dọn các file này.
4. Trước cửa deploy, lấy một dump mới đã `gzip -t` và mới hơn sổ cần giữ, hoặc chủ nhà thuốc chấp nhận dump `20261016_020001` chỉ là mốc đã restore thử.

## 8. Trạng thái từng blocker

| # | Blocker | Trạng thái | Bằng chứng |
|---|---|---|---|
| 1 | Rollback service trên layout systemd/nginx | Mở | Máy này không có systemd, nginx, WSL, Docker, Hyper-V. Không đụng service Production |
| 2 | Quầy đang bán, ba ca mở | Mở | Lần đọc Production cuối vẫn là báo cáo 001, lúc 17:02 +07. Phiên này không đọc lại Production và không chọn giờ |
| 3 | Chủ nhà thuốc chưa nhận quy tắc 231 đơn vị hết hạn | Mở | Clone vẫn còn 231 đơn vị. Probe không bán chúng. Chưa có xác nhận nghiệp vụ |
| 4 | Source chưa gắn commit | Mở | HEAD `5c9f6510`. Sửa chưa commit. Hash DLL `FC90329B…` không đại diện source sau sửa |
| 5 | Cài database trống từ manifest | Mở | BOM và 060 đã vượt trên lab mà không sửa file lịch sử. Manifest vẫn dừng ở 090 nếu không có 069. 069 làm hỏng ledger đã có version `1.1`. Không thêm file vào manifest prod |
| 6 | 81 archive 20 byte | Mở, đã có cách xử lý | Không xóa, không sửa cron. Một dump đã restore và được dùng lại. Các archive khác chưa restore |
| 7 | Test bán local và test ngoài Pharmacy | Đã phân loại, không sửa cho đẹp số | Bảy test bán khóa giá/lô của seed. Local `kitplatform` lệch seed. Một test webhook. Sáu test Content/Local OS ngoài phạm vi |

## 9. Điều kiện chủ nhà thuốc và người vận hành cần xác nhận

Chưa đủ để chọn cửa sổ. Không giả định giờ đóng cửa.

- Người vận hành xác nhận từng ca đang mở, gồm ca `2026-07-27`. Ca treo phải được đóng có chủ đích hoặc được chấp nhận bằng văn bản. Không deploy khi còn bán.
- Chủ nhà thuốc chấp nhận candidate từ chối bán 231 đơn vị hết hạn của `NT_XUANHOA`. Lô không ngày không bị chặn; trên clone số lô còn hàng không ngày là 0.
- Dump trong ngày deploy đã `gzip -t` và mới hơn sổ cần giữ, hoặc chủ nhà thuốc chấp nhận dump `20261016_020001` chỉ là mốc đã restore thử.
- Rollback đã thử trên một máy không bán hàng, cùng kiểu systemd và `/var/www/kit-platform`: lưu bản cũ, đổi binary và static, restart, health, rollback, restart, smoke. Hiện chưa có máy này.
- Commit đã khóa đúng danh sách file mục 3, rồi build lại và ghi hash DLL mới.
- Không bật `-RunMigrations` cho lần cắt này khi pending vẫn 0. Nếu ai đó thêm 069 vào manifest, không chạy lên `novixa_prod`.

Khi các điều kiện đó xong và pending vẫn 0, downtime dự kiến chỉ là restart `kit-platform-api` và reload static. Chưa bấm giờ. Smoke sau cắt: health, health/db, login `NT_XUANHOA`, một tra cứu POS, một preview không chọn lô hết hạn, cashier không mở báo cáo. Dừng và trả binary cũ nếu health khác 200, login vào nhầm tenant, POS không tạo được đơn hàng còn hạn, hoặc tồn một lô còn hạn giảm khi chưa có đơn.

Rollback lúc đó: copy DLL và cây SPA đã lưu đè lại, restart service, lặp smoke. Không rollback database nếu migration không chạy.

## 10. Kết luận

`BLOCKED`

Đường nâng ledger Xuân Hòa không có file chờ. Trên bản sao dump 02:00, candidate vẫn bỏ fallback tenant, bán FEFO, từ chối lô hết hạn, nhập, trả, mở/đóng ca, cô lập tenant và chặn cashier khỏi báo cáo. BOM và file 060 đã có cách chạy trên database mới mà không sửa file lịch sử và không đụng ledger đã ghi 060. Cài mới từ manifest Production vẫn chưa xong. Rollback service chưa thử. Source chưa commit. Chủ nhà thuốc chưa nhận quy tắc 231 đơn vị hết hạn. Quầy chưa được người vận hành xác nhận. Không deploy.
