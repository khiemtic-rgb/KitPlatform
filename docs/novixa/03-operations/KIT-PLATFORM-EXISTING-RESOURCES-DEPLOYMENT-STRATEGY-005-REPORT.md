# KIT-PLATFORM-EXISTING-RESOURCES-DEPLOYMENT-STRATEGY-005

**Ngày:** 2026-10-16, khoảng 21:00–21:15 +07  
**Kết luận deploy:** `BLOCKED_WITH_EXACT_REASONS`  
**Review danh sách commit mục 5:** `APPROVE_CANDIDATE_COMMIT_LIST` (mục 9). Kết luận này chỉ duyệt danh sách file. Không mở deploy.  
**Không deploy.** Không commit, không push, không merge. Không restart service, không sửa cấu hình, không chạy migration, không ghi dữ liệu trên `novixa_prod`.

Báo cáo này kế thừa `KIT-PLATFORM-NOVIXA-FINAL-RELEASE-GATE-003-REPORT.md`, `KIT-PLATFORM-NOVIXA-RELEASE-BLOCKERS-002-REPORT.md`, `KIT-PLATFORM-NOVIXA-RELEASE-READINESS-001-REPORT.md` và `KITPLATFORM-NOVIXA-PRODUCTION-PARITY-001-REPORT.md`. Đường dẫn gate 002 trong chỉ thị không tồn tại; bản tương ứng là `KIT-PLATFORM-NOVIXA-RELEASE-BLOCKERS-002-REPORT.md`.

File này nằm trên cây `E:\KitPlatform`. Không nằm trong danh sách commit của candidate.

## 1. Baseline đã xác minh trong phiên này

| Cây | HEAD | Trạng thái phiên này |
|---|---|---|
| `E:\KitPlatform` | `main` `8c59ceb4f3be537898106ec058ea547406f722e5` | Không đổi HEAD. Cây vẫn dirty (Content/Famixa và các file khác có từ trước). Phiên này chỉ thêm báo cáo này. |
| `E:\KitPlatform-wt-phase5-novixa-product-proof` | `phase5-novixa-product-proof` `5c9f65103fd030e37f2788a9b99565a9f918a140` | Đúng HEAD của báo cáo 001–003. `git status --short` vẫn là 12 file tracked và 3 mục untracked. Không reset, clean, stash, checkout, xóa, stage hay commit. |

Tracked, chưa commit, không sửa thêm trong phiên này:

- `deploy/ubuntu/backup-db.sh`
- `deploy/ubuntu/run-incremental-migrations-prod.sh`
- `deploy/ubuntu/run-migrations-prod.sh`
- `src/KitPlatform.Api/Controllers/AuthController.cs`
- `src/KitPlatform.Application/Auth/AuthContracts.cs`
- `src/KitPlatform.Infrastructure/Auth/AuthService.cs`
- `src/KitPlatform.Infrastructure/CustomerApp/CustomerAppAuthService.cs`
- `tests/KitPlatform.Api.Tests/CatalogApiTests.cs`
- `tests/KitPlatform.Api.Tests/CustomerConsentApiTests.cs`
- `tests/KitPlatform.Api.Tests/InventoryApiTests.cs`
- `tests/KitPlatform.Api.Tests/ProcurementApiTests.cs`
- `tests/KitPlatform.Api.Tests/SalesApiTests.cs`

Untracked, có từ trước, không sửa:

- `deploy/ubuntu/greenfield-compat/060_family_members_status_align.sql`
- `migrations/371_pack_content_brand_narrative_dna.sql`
- `tests/KitPlatform.Api.Tests/DefaultTenantResolutionTests.cs`

`migrations/060_family_members_status_align.sql` không đổi. SHA-256 vẫn `E84257CC824305F66EA228092717403EA83FEF20388B0DD11D82856861B70180`.

`migration-files.prod.txt` có 221 tên file `.sql`. Không chứa `069_assessment_pharmacy_v1_seed.sql`, không chứa `371_pack_content_brand_narrative_dna.sql`, không chứa `greenfield-compat`.

Công cụ trên máy: `dotnet` `10.0.301`. PostgreSQL 18, service `postgresql-x64-18` đang chạy, nghe `5432`. `psql` không có trên PATH; binary là `C:\Program Files\PostgreSQL\18\bin\psql.exe`.

## 2. Môi trường đã dùng lại

### Clone cô lập

Cluster lab `C:\novixa-rr001-lab\pgdata` đang tắt khi phiên bắt đầu. Đã start lại chỉ trên `127.0.0.1:5433`. Không có database tên `novixa_prod` trên cluster này.

| Database | Vai trò đã xác minh |
|---|---|
| `novixa_rr001` | `current_database=novixa_rr001`, `inet_server_addr=127.0.0.1`, cổng `5433`. Ledger 232. Đây là bản restore dump `20261016_020001`. |
| `novixa_rr001_fresh` | Còn trên đĩa. Không chạy migration mới. |
| `novixa_rr002_greenfield` | Ledger 103 dòng. Có tên `060` và `090`. Không có tên `069`. Khớp kết quả cài dở của báo cáo 002. Không chạy lại. |

Dump `C:\novixa-rr001-lab\novixa_prod_20261016_020001.sql.gz` vẫn 15,019,608 byte. Restore của dump này đã exit 0 trong phiên 001 và database đó vẫn truy cập được. Phiên này không restore lại và không đọc các archive khác.

`NT_XUANHOA` trên clone: 3 lô hết hạn, `quantity_available` tổng `231.000`. Lô còn hàng không ngày: 0. Ca `status = 1`: 0. Ca `status = 2`: 150. Không sửa 231 đơn vị. Không tạo đơn trên clone.

### Database local `kitplatform` cổng 5432

`current_database=kitplatform`, cổng 5432. Ledger 141. Có `DEMO_PHARMACY`, `NT_XUANHOA` và `NVX-CS08`. Đây không phải bản sao schema Production. Tài khoản local `DEMO_PHARMACY` / `admin` và `pharmacist` còn `status = 1`. Không in email hay hash mật khẩu.

### Production, chỉ đọc, 2026-10-16 21:05 +07

Host `novixa`. Không restart, không sửa file, không chạy migration, không ghi.

| Hạng mục | Kết quả |
|---|---|
| `kit-platform-api` | `active`. `ActiveEnterTimestamp=Tue 2026-09-29 21:00:52 +07` |
| DLL đang chạy | `/var/www/kit-platform/api/KitPlatform.Api.dll`, 1,786,368 byte, mtime `2026-09-27 11:47:15 +07`, SHA-256 `26afdaa7f123fa36496f31652a01a6d87942e975c4baf72b0b49bfff977a8941` |
| Health | `GET http://127.0.0.1:5000/api/health` = `{"status":"ok"}`. `health/db` = `database: true` |
| Runtime an toàn đã đọc | `ASPNETCORE_ENVIRONMENT=Production`. `NationalDrugCatalog__Mode=sandbox`. `NationalDrugCatalog__EnableStockOutSync=false`. Không in connection string |
| Bản phát hành trước | Không có `/var/www/kit-platform.prev`. Không có `/opt/kit-platform/releases` |
| Database | `current_database=novixa_prod` qua socket peer. Ledger 232 |
| File nguy hiểm trong ledger | Không có `069*`, `383*`, `384*`, `387*`, `388*`, không có `371_pack_content_brand_narrative_dna.sql` |
| Ca `NT_XUANHOA` | `status = 1`: 2. `status = 2`: 147. Ca mở: `SH-000148` lúc `2026-10-16 09:34:32 +07`, `SH-000149` lúc `2026-10-16 14:18:45 +07`. Không in người mở ca, không in tiền |
| Hết hạn | 3 lô, `231.000` đơn vị. Lô còn hàng không ngày: 0 |

Lần đọc 17:02 trong báo cáo 001 có 3 ca mở. Lúc 21:05 còn 2 ca, cả hai mở trong ngày. Ca `2026-07-27` không còn trong tập đang mở. Không đóng ca.

Câu đếm đơn mới nhất bị lỗi cột `created_at` mơ hồ và transaction chỉ đọc đã dừng. Không có câu ghi. Không chạy lại câu đó.

## 3. Migration

Đối chiếu tên file trên clone `novixa_rr001` với `migration-files.prod.txt` của candidate:

| Chỉ số | Giá trị |
|---|---|
| File `.sql` trong manifest | 221 |
| Ledger clone | 232 |
| Pending (có trong manifest, chưa có trong ledger) | **0** |
| Có trong ledger, không có trong manifest | 11: `230`, `231`, `281`–`288`, `290` |

Ledger Production cũng là 232 dòng và không chứa các file Content `383`–`388` hay `371_pack_content_brand_narrative_dna.sql`. Đường nâng database Xuân Hòa hiện tại không có file chờ. Lần cắt này không được bật `-RunMigrations`.

Cài database trống từ manifest vẫn là blocker riêng, đã chứng minh trong báo cáo 002 và không chạy lại: runner vượt BOM và file `060` nhờ thân `greenfield-compat`, rồi dừng ở `090` vì thiếu `069`. Đưa `069` vào manifest sẽ thành file pending và lỗi trên ledger đã có template `PHARMACY_V1` version `1.1`. Không thêm `069`. Không sửa file lịch sử `060`.

Greenfield không chặn lần nâng ledger hiện có.

## 4. Kiểm thử phiên này

Cây: `E:\KitPlatform-wt-phase5-novixa-product-proof`. Configuration `Release`. Source candidate không đổi so với báo cáo 003, nên các probe HTTP trên clone trong báo cáo 001 và 002 vẫn gắn với đúng cây này. Phiên này không tạo thêm đơn trên clone và không chạy lại bảy test bán đang khóa giá seed `500`.

| Lệnh | Kết quả |
|---|---|
| `Application.Tests` lọc `NovixaReleaseSafetyBehaviorTests`, `PharmacyConsultationOtcGateTests`, `CoreEngineTests`, `SalesAccrualSqlTests` | **PASS.** 13 passed, 0 failed |
| `Platform.Tests` lọc lot rules, pack definition, module gate, sale/return completed handlers, pack sync | **PASS.** 28 passed, 0 failed |
| `Api.Tests` lọc `DefaultTenantResolutionTests`, `SalesReadOnlyPrincipalTests` | **PASS.** 3 passed, 0 failed. Login không tenant trả 400, không có token, không vào `NVX-CS08`. Login `DEMO_PHARMACY` trả đúng tenant. Chạy trên `kitplatform:5432` |

Warning `NU1903` cho `Newtonsoft.Json` 10.0.3 lặp lại. Không có lỗi biên dịch.

Phân loại giữ từ 001 và 002, không chạy lại, vì source và ledger local không đổi:

| Nhóm | Trạng thái | Lý do |
|---|---|---|
| Bảy test bán trên `kitplatform` | FAIL do dữ liệu/fixture | API local trả giá `5000` và lô `5555`. Assertion giữ giá seed `500` và lô `LOT2026A`. Không sửa assertion |
| `Customer_consent_upsert_writes_outbox_event` | BLOCKED môi trường | Không nối được webhook `localhost:80` |
| Bốn test Content isolation/ownership | BLOCKED môi trường | Đòi `KPA_PHASE15_DISPOSABLE_PG`. Ngoài cổng Pharmacy |
| Hai test Local OS text extract | FAIL assertion, ngoài cổng Pharmacy | Không sửa pack Local OS |
| Probe clone: login, cô lập tenant, FEFO, từ chối hết hạn, nhập, trả, ca, cashier 403 | PASS, kế thừa 001/002 | Cùng source chưa commit. Clone vẫn là `novixa_rr001` ledger 232, hết hạn vẫn 231 |

`SellableLotRules` vẫn từ chối lô có ngày trước hôm nay và vẫn bán lô không ngày. Production và clone đều có 231 đơn vị hết hạn và 0 lô không ngày còn hàng. Không xóa và không điều chỉnh số đó.

`dotnet publish` không chạy. Source chưa commit nên không có hash phát hành. Hash lab `FC90329B…` và hash `41C921CB…` của commit `643932c0` không đại diện cho cây chưa commit hiện tại.

## 5. Source phát hành dự kiến

Chưa khóa. HEAD `5c9f6510` không chứa các sửa chưa commit. Không stage, không commit.

Danh sách đúng để chủ dự án duyệt, không thêm file:

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

Không đưa `migrations/371_pack_content_brand_narrative_dna.sql`. Không đưa `069` vào `migration-files.prod.txt`. Không build từ `E:\KitPlatform`.

Sau khi commit được duyệt, publish từ đúng commit đó:

```powershell
cd E:\KitPlatform-wt-phase5-novixa-product-proof
dotnet publish src\KitPlatform.Api\KitPlatform.Api.csproj -c Release -o <thư-mục-publish>\api --no-self-contained
Get-FileHash <thư-mục-publish>\api\KitPlatform.Api.dll -Algorithm SHA256
```

SPA của `scripts/deploy-update-vps.ps1`, với `VITE_API_BASE_URL=https://api.novixa.vn`: admin, customer-app, staff-app, prescriber-portal, family-app. Bản trong `C:\novixa-rr001-lab\spa\` là build phiên 001 từ cây chưa commit. Phải build lại từ commit đã khóa trước khi đưa vào phiếu phát hành.

`deploy-update-vps.ps1` mặc định `-Root E:\KitPlatform` và mặc định không chạy migration. Cây đó là `main` dirty, không phải candidate. Script vẫn upload toàn bộ `migrations\`, rsync `deploy/ubuntu\` lên `/opt/kit-platform\`, có thể sửa nginx `family.kittech.vn`, reload nginx và `systemctl restart kit-platform-api`. Vì vậy không gọi script trước khi commit đã khóa và `-Root` trỏ vào cây publish của commit đó. Worktree dirty sẽ kéo theo file untracked `371_pack_content_brand_narrative_dna.sql` nếu upload cả thư mục `migrations`. Commit sạch không chứa file đó.

`/etc/kit-platform/api.env` không nằm trong Git và không được đóng gói. `appsettings.Production.json` của candidate vẫn đặt `Auth:DefaultTenantCode` = `NT_XUANHOA`.

## 6. Kế hoạch deploy, chưa thực hiện

### Trước khi cắt

Dừng nếu một điều kiện sau sai:

- Commit mục 5 chưa có, hoặc DLL không được publish từ đúng commit đó.
- `-Root` không phải cây publish của commit đó.
- Có ý định bật `-RunMigrations`, hoặc pending không còn là 0.
- Manifest hoặc cây upload chứa `069` hoặc `371_pack_content_brand_narrative_dna.sql`.
- DLL đang chạy không còn hash `26afdaa7f123fa36496f31652a01a6d87942e975c4baf72b0b49bfff977a8941` mà chưa có bản kế thừa được chấp nhận. Phải đo lại ngay trước cắt.
- `NationalDrugCatalog__Mode` khác `sandbox`, hoặc stock-out sync khác `false`.
- Ledger xuất hiện `383`, `384`, `387`, `388` hoặc file Content `371`.
- Còn ca `status = 1`, hoặc quầy còn bán. Lúc 21:05 còn `SH-000148` và `SH-000149`.
- Chủ nhà thuốc chưa chấp nhận việc candidate từ chối bán 231 đơn vị hết hạn.
- Chưa copy DLL, năm cây SPA và `/opt/kit-platform` ra thư mục ngoài cây đang chạy, và chưa ghi hash DLL.

Dump `20261016_020001` là mốc đã restore vào `novixa_rr001`. Nó cũ hơn sổ đang bán trong ngày. Trước cửa deploy cần một dump mới đã `gzip -t`, hoặc chủ nhà thuốc chấp nhận mốc 02:00 chỉ là bản đã diễn tập restore, không phải điểm quay lui của cả ngày bán.

### Trong lúc cắt

Thứ tự, chỉ sau khi các cửa trên đã xong. Phiên này không chạy.

1. Trên Production, copy ra ngoài cây đang chạy: `KitPlatform.Api.dll`, `admin`, `customer-app`, `staff-app`, `prescriber-portal`, `family-app`, và `/opt/kit-platform`. Ghi `sha256sum` DLL.
2. Gọi `scripts/deploy-update-vps.ps1` với `-Root` là cây đã publish. Không truyền `-RunMigrations`.
3. Script sẽ ghi đè `/var/www/kit-platform/api` không `--delete`, ghi đè SPA có `--delete`, rsync `deploy/ubuntu` vào `/opt/kit-platform`, reload nginx, restart `kit-platform-api`.
4. Đứng tại log: `journalctl -u kit-platform-api -n 50 --no-pager`, rồi `curl -sf https://api.novixa.vn/api/health` và `/api/health/db`.

Dừng và trả bản đã copy nếu health khác 200, login vào nhầm tenant, POS không tạo được đơn còn hạn, hoặc tồn một lô còn hạn giảm khi chưa có đơn.

### Sau khi cắt

- Health và health/db trả 200, database sống.
- Login `NT_XUANHOA` trả đúng tenant.
- Một tra cứu POS.
- Một preview không chọn lô hết hạn `60551` và không giảm 231 đơn vị hết hạn.
- Tài khoản cashier không mở báo cáo doanh thu.
- Mode catalog vẫn `sandbox`, stock-out sync vẫn `false`.
- Ledger vẫn không có file Content mới.
- `deploy/ubuntu/smoke-test.sh` kiểm thêm index SPA và CORS.

Kết luận thành công chỉ khi các mục trên khớp và không có lỗi mới trong log của lần restart. Nếu không khớp, phục hồi binary và static đã copy, restart, lặp smoke. Không restore database khi migration không chạy.

### Phục hồi

Đã diễn tập: restore dump `20261016_020001` vào `novixa_rr001` trên cổng 5433, và incremental trên clone trả `applied=0`. Đã đổi file trong `C:\novixa-rr001-lab\rollback-drill` và đã tắt process lab cổng `5299` ở các phiên trước. Những việc đó không phải restart `systemd` và không phải rollback Production.

Chưa diễn tập: `systemctl restart kit-platform-api` trên một máy không phải Production, rồi copy DLL cũ trở lại và restart lần hai. Máy làm việc này không có systemd, nginx hay cây `/var/www/kit-platform`. Không cài WSL, Docker hay Hyper-V. Không mua máy. Thiếu lab Linux không chặn các kiểm chứng ở trên; nó chỉ có nghĩa rollback service chưa được bấm giờ.

Phương án khả thi với VPS hiện có: vì không có thư mục bản trước, người vận hành phải tự copy DLL và SPA ra ngoài cây đang chạy trước khi script ghi đè. Rollback sau cắt, nếu migration không chạy, là copy các file đó trở lại và restart service. Restore `novixa_prod` từ dump 02:00 sẽ bỏ các đơn phát sinh sau 02:00, gồm hai ca đang mở lúc 21:05. Đó là quyết định riêng, chưa diễn tập trên Production.

## 7. Blocker còn lại

| # | Việc | Bằng chứng | Tác động | Vì sao chưa xong bằng nguồn lực hiện có | Phần khác vẫn làm được |
|---|---|---|---|---|---|
| 1 | Khóa source và artifact | HEAD `5c9f6510`. Sửa chưa commit. Không có hash DLL của cây này | Chưa có bản phát hành xác định | Commit phải được chủ dự án duyệt. Phiên này không tự commit | Danh sách file mục 5 đã khóa để duyệt |
| 2 | Ca đang mở | Production 21:05: `SH-000148`, `SH-000149`, cả hai mở trong ngày | Cắt lúc này ngắt quầy đang bán | Không tự đóng ca. Người vận hành phải xác nhận cửa sổ yên | Đọc trạng thái đã xong |
| 3 | 231 đơn vị hết hạn | Production và clone đều còn 3 lô, 231 đơn vị. Candidate từ chối bán lô có ngày trước hôm nay | Đổi khả năng bán của số hàng đó | Quyết định nghiệp vụ của chủ nhà thuốc | Test và probe không đụng số này |
| 4 | Rollback service | Không có thư mục bản trước trên VPS. Chưa có restart systemd ngoài Production | Một bản lỗi không tự có binary cũ trên máy | Không có host Linux thứ hai. Không dùng Production để diễn tập | Quy trình copy-trước đã viết ở mục 6. Chủ dự án có thể chấp nhận rủi ro này khi duyệt deploy |
| 5 | Cài database trống | Greenfield dừng ở `090` nếu không có `069`. `069` làm hỏng ledger version `1.1` | Không ảnh hưởng đường pending = 0 của Xuân Hòa | Không được thêm `069` vào manifest Production | Nâng ledger hiện có đã đối chiếu, pending = 0 |

## 8. Kết luận

`BLOCKED_WITH_EXACT_REASONS`

Đường nâng `novixa_prod` vẫn pending = 0. Candidate vẫn build được các test hợp đồng đã chạy trong phiên này: fail-closed khi thiếu tenant, ranh giới pack, FEFO trong bộ test đơn vị, và cashier không đọc báo cáo. Luồng bán trên bản sao dump 02:00 giữ nguyên bằng chứng của báo cáo 001 và 002 vì source candidate không đổi. Production lúc 21:05 vẫn là DLL tháng 9, catalog sandbox, stock-out sync tắt, 231 đơn vị hết hạn, và hai ca mở trong ngày.

Chưa đủ `READY_FOR_OWNER_DEPLOY_REVIEW` vì chưa có commit được duyệt và chưa có artifact publish từ commit đó, vì hai ca vẫn mở, và vì chủ nhà thuốc chưa nhận quy tắc từ chối 231 đơn vị hết hạn. Rollback systemd chưa được diễn tập; đó là rủi ro để chủ dự án quyết định, không phải lý do dừng các kiểm chứng còn lại. Cài mới từ manifest vẫn là việc riêng và không chặn lần nâng này.

Việc tiếp theo, đúng một bước: chủ dự án duyệt hoặc từ chối danh sách commit ở mục 5. Sau khi duyệt, mới publish và ghi hash. Không deploy từ báo cáo này.

## 9. Review diff danh sách commit mục 5

**Kết luận mục này:** `APPROVE_CANDIDATE_COMMIT_LIST`

Phiên review đọc `git diff` trên `E:\KitPlatform-wt-phase5-novixa-product-proof` so với HEAD `5c9f65103fd030e37f2788a9b99565a9f918a140`. Không chạy lại test, không sửa source, không stage, không commit, không checkout, không reset. Worktree candidate vẫn để nguyên 12 file tracked sửa và 3 mục untracked. Mười bốn file dưới đây đều `SAFE_TO_INCLUDE`.

### 9.1 Từng file

#### `deploy/ubuntu/backup-db.sh`

1. Bỏ BOM và bỏ `-U "$DB_USER"`. `pg_dump` chạy bằng `sudo -u postgres` theo socket peer, ghi vào `*.sql.gz.partial`. Script kiểm tra mã thoát của cả `pg_dump` và `gzip`, rồi `gzip -t`, kích thước giải nén lớn hơn 0, và nội dung có dòng dump PostgreSQL cùng `CREATE TABLE public.tenants`, không có `FATAL` hoặc lỗi peer ở đầu file. Đạt thì `mv` thành archive cuối. Xóa bản cũ hơn 14 ngày chỉ khi `RETENTION_DELETE_CONFIRMED=true`; thiếu cờ thì chỉ in `RETENTION_WOULD_DELETE`. Nếu cùng thư mục có `backup-production-credential-key.sh` thì gọi thêm script đó.
2. Vai trò peer của user `postgres` không đổi được sang role `KitPlatform`. Lần dump lỗi trước đây vẫn bị nén thành archive vài chục byte ngay tại đường dẫn cuối. Bản mới chỉ công bố archive sau khi kiểm tra đạt, và không xóa bản cũ khi chưa có xác nhận lưu ngoài máy.
3. Commit không chạy script. Sau khi rsync đè file cron, lần backup kế tiếp vẫn chỉ đọc database (`SELECT current_database()` và `pg_dump`). Script không áp migration. Thiếu `python3` thì lần chạy thất bại và xóa file partial; archive cũ còn nguyên. Bản cũ không còn bị xóa mặc định, nên đĩa backup có thể tăng. File key-backup không có trong `deploy/ubuntu` của candidate và không nằm trong 14 file; nhánh gọi đó không chạy với cây này.
4. Khớp cách backup Ubuntu bằng peer auth. Không đổi API, schema, hay dữ liệu `NT_XUANHOA`.
5. `SAFE_TO_INCLUDE`

#### `deploy/ubuntu/run-incremental-migrations-prod.sh`

1. Thêm `prepare_migration_sql`, `psql_file` và dọn file tạm. Hàm chuẩn bị thấy `greenfield-compat/<tên manifest>` thì dùng thân đó, và cắt BOM `EF BB BF` trước khi đưa cho `psql`. Nhánh `SKIP` khi `kit_schema_migrations` đã có tên file vẫn đứng trước bước chuẩn bị.
2. `psql` từ chối BOM. File lịch sử `060` không parse được trên database chưa ghi tên đó. Database đã ghi tên `060` phải bỏ qua, không chạy thân thay thế và không đọc lại file lịch sử.
3. Script chỉ chạy khi có URI và được gọi tường minh. Với pending = 0 ở mục 3, vòng lặp in `SKIP` trước `prepare_migration_sql`, nên thân compat `060` không chạy trên `novixa_prod`. Nhánh bootstrap `098` khi thiếu sổ là hành vi có sẵn từ trước diff. Diff này không thêm dòng manifest.
4. Khóa sổ vẫn là tên file trong manifest. `migration-files.prod.txt` không thuộc commit. Ledger Production 232 và pending = 0 nghĩa là lần incremental không áp file nào.
5. `SAFE_TO_INCLUDE`

#### `deploy/ubuntu/run-migrations-prod.sh`

1. Cùng lớp chuẩn bị BOM và thân compat. Script vẫn không đọc sổ: mọi dòng manifest đều được thực thi, và file có anh em trong `greenfield-compat/` dùng thân thay thế.
2. Cài database trống cần thân `060` parse được và cần cắt BOM. File trong `migrations/` giữ nguyên.
3. Gọi script này với URI `novixa_prod` sẽ chạy lại toàn bộ manifest. Việc chạy lại toàn bộ đã có từ trước diff. Phần mới là `060` sẽ chạy thân compat, gồm `ADD COLUMN IF NOT EXISTS`, backfill có điều kiện, default và index `IF NOT EXISTS`. Các file manifest còn lại không được diff này làm thành idempotent. Không gọi script full trên ledger Xuân Hòa. Mục 6 đã cấm `-RunMigrations`. Commit không tự gọi script.
4. Đây là đường cài mới. Đường đó vẫn dừng ở `090` khi thiếu `069`. Commit này không thêm `069`.
5. `SAFE_TO_INCLUDE`

#### `deploy/ubuntu/greenfield-compat/060_family_members_status_align.sql`

1. File mới, chưa theo dõi. Cùng kết cục với `migrations/060_family_members_status_align.sql`: thêm `status` và `gender`, gán status còn null, default `1`, `NOT NULL` khi hết null, index `ix_family_members_account_status`. Khác một chỗ: tham chiếu `is_active` nằm trong `EXECUTE` bên trong `DO` block, chỉ khi `information_schema` thấy cột đó. `git diff` không có thay đổi ở `migrations/060_family_members_status_align.sql`.
2. PostgreSQL parse `is_active` trong câu `UPDATE` lịch sử ngay cả khi mệnh đề `EXISTS` sau đó sai. Database chưa có cột sẽ dừng ở `060`. Thân compat để câu backfill chỉ được parse khi cột tồn tại. Tên ghi sổ vẫn là tên manifest, không phải đường dẫn compat.
3. Trên ledger đã có `060`, runner incremental không chạy file này. Chạy tay thân compat trên database đã align chỉ lặp lệnh có `IF NOT EXISTS` và `UPDATE` các dòng `status IS NULL`. File không nằm trong manifest và header ghi rõ không được thêm vào `migration-files.prod.txt`.
4. Khớp mục tiêu commit: cài schema mới đi qua được `060` trong khi ledger hiện có vẫn giữ tên file lịch sử. Không sửa migration đã ghi trên Production.
5. `SAFE_TO_INCLUDE`

#### `src/KitPlatform.Api/Controllers/AuthController.cs`

1. `Login` bắt `AuthTenantRequiredException` và trả `400` với `{ message }`. Các action chọn workspace, refresh, logout và `me` giữ nguyên.
2. Thiếu tenant khi không có default phải thành từ chối có thông báo, và không được đi vào nhánh `401` sau khi đã tạo session.
3. Đổi mã HTTP của đúng trường hợp này từ chỗ trước đây `return null` thành `401`, sang `400`. Không cấp `accessToken`. Exception không bị bắt ở action khác vì chỉ `LoginAsync` ném kiểu này.
4. Khớp hợp đồng fail-closed của login nhân viên. File cấu hình Production không nằm trong diff.
5. `SAFE_TO_INCLUDE`

#### `src/KitPlatform.Application/Auth/AuthContracts.cs`

1. Thêm `AuthTenantRequiredException`, kế thừa `InvalidOperationException`, thông báo cố định: `Mã nhà thuốc là bắt buộc khi chưa cấu hình tenant mặc định.`
2. Tách lỗi thiếu tenant khỏi lỗi sai mật khẩu, và ghi rõ assessment tenant không phải tenant thay thế.
3. Thông báo không chứa mã tenant, token hay tên người dùng. Không đổi `LoginRequest`.
4. Nằm đúng tầng contract để API và test dùng chung một kiểu lỗi.
5. `SAFE_TO_INCLUDE`

#### `src/KitPlatform.Infrastructure/Auth/AuthService.cs`

1. Bỏ `IHostEnvironment`, hằng `DEMO_PHARMACY` và fallback `Assessment:EventTenantCode`. `Auth:DefaultTenantCode` rỗng thì biến cấu hình giữ `null`. `ResolveTenantCode` trả tenant của request, hoặc default đã cấu hình, hoặc `null`. `LoginAsync` ném `AuthTenantRequiredException` khi kết quả đó rỗng. Nhánh username có `@` và không gửi tenant vẫn gọi `LoginByKitEmailAsync` trước bước này; diff không sửa thân hàm email.
2. Đóng lỗ login không tenant đi vào tenant của sự kiện assessment, và đóng fallback Development `DEMO_PHARMACY`.
3. `appsettings.Production.json` không thuộc 14 file và vẫn có `Auth:DefaultTenantCode` = `NT_XUANHOA`. Trên cấu hình đó, login bỏ trống tenant vẫn vào `NT_XUANHOA`. Fail-closed xảy ra khi default cũng rỗng: không token, không `NVX-CS08`. Login bằng email Kit vẫn chọn workspace của tài khoản, không đọc `Assessment:EventTenantCode`. Phiên 005 không in `Auth__DefaultTenantCode` từ `/etc/kit-platform/api.env`; mục 6 đã yêu cầu sau cắt phải thấy login `NT_XUANHOA` đúng tenant.
4. Khớp nguyên tắc thiếu tenant thì không mở session khi không có default. Production hiện cấu hình default trong file của candidate, nên hành vi bỏ trống tenant trên file đó giữ `NT_XUANHOA`.
5. `SAFE_TO_INCLUDE`

#### `src/KitPlatform.Infrastructure/CustomerApp/CustomerAppAuthService.cs`

1. Cùng cách bỏ fallback assessment và fallback Development `DEMO_PHARMACY`. `RequestOtpAsync` dùng `ResolveTenantCode(...) ?? throw` với thông báo `Mã nhà thuốc là bắt buộc.` trước khi chuẩn hóa số điện thoại. `VerifyOtpAsync` thấy tenant `null` thì `return null` trước khi tìm challenge và trước `IssueTokensAsync`. `_env` vẫn chỉ phục vụ bypass OTP khi `IsDevelopment` và có `DevBypassCode`.
2. App khách không được nhận tenant sự kiện assessment khi request và default đều trống.
3. Production `ASPNETCORE_ENVIRONMENT=Production` nên nhánh bypass OTP Development không chạy. Default `NT_XUANHOA` trong file Production vẫn áp cho request khách thiếu tenant, cùng quy tắc với login nhân viên. Không có default thì yêu cầu OTP dừng trước khi gửi, và xác minh OTP không cấp token.
4. Giữ fail-closed ở cả hai cửa khách. Không đổi hash OTP, cooldown hay SMS.
5. `SAFE_TO_INCLUDE`

#### `tests/KitPlatform.Api.Tests/CatalogApiTests.cs`

1. Một dòng thêm vào body login: `tenantCode = "DEMO_PHARMACY"`.
2. Host test để `Auth:DefaultTenantCode` rỗng. Login cũ không tenant sẽ nhận `400` và không vào được các assertion danh mục.
3. Không xóa assertion. Diff không đụng giá, tồn hay quyền.
4. Test gọi HTTP login thật và đi tiếp bằng token của đúng tenant demo.
5. `SAFE_TO_INCLUDE`

#### `tests/KitPlatform.Api.Tests/CustomerConsentApiTests.cs`

1. Một dòng thêm `tenantCode = "DEMO_PHARMACY"` vào login. `EnsureSuccessStatusCode()` còn nguyên.
2. Cùng lý do với catalog: login phải chỉ rõ tenant sau khi đóng fallback.
3. Không nới điều kiện thành công và không bỏ kiểm tra outbox.
4. Vẫn là bài test API đồng ý khách hàng trên tenant được chỉ định.
5. `SAFE_TO_INCLUDE`

#### `tests/KitPlatform.Api.Tests/DefaultTenantResolutionTests.cs`

1. File mới. `Login_without_tenant_fails_and_does_not_enter_NVX_CS08` gửi username và password, không gửi tenant. Kỳ vọng `400`, thân chứa `Mã nhà thuốc là bắt buộc`, không chứa `NVX-CS08`, không chứa `accessToken`, không có `user`, không có `tenantCode`. Bài thứ hai gửi `tenantCode = "DEMO_PHARMACY"` và kỳ vọng `user.tenantCode` đúng giá trị đó.
2. Chứng minh host test, nơi default rỗng và assessment tenant là `NVX-CS08`, không cấp session cho tenant sự kiện.
3. Không thay assertion của bài test khác. Không đụng Production.
4. Đây là kiểm tra hành vi HTTP thật qua `KitPlatformWebApplicationFactory`, đúng lỗ hổng mà diff auth đóng.
5. `SAFE_TO_INCLUDE`

#### `tests/KitPlatform.Api.Tests/InventoryApiTests.cs`

1. Login helper thêm `tenantCode = "DEMO_PHARMACY"`. Bài void phiếu mở sổ đọc `batchIds` từ response tạo, rồi `GET` có `productId`, `search` và `pageSize`. Danh sách lấy từ `items`. Khớp theo `batchId` vừa tạo, vẫn `Assert.True(canVoid)`. Sau `DELETE` vẫn kỳ vọng `204` và `DoesNotContain` đúng `batchId` trong `items`.
2. Login cần tenant tường minh. API danh sách đã trả trang `{ items }` nên cách đọc mảng trần không còn khớp hợp đồng hiện tại.
3. Phạm vi quan sát hẹp lại theo `search` của chính số lô vừa tạo. Assertion void còn: lô tạo ra phải `canVoid`, và sau void không còn trong trang tìm đúng số đó. Không bỏ bước ghi tồn.
4. Sửa test cho khớp API đang có trên HEAD. Không đổi rule bán hay lô hết hạn.
5. `SAFE_TO_INCLUDE`

#### `tests/KitPlatform.Api.Tests/ProcurementApiTests.cs`

1. Login thêm `DEMO_PHARMACY`. Tạo và sửa đơn mua, cùng phiếu nhập, gửi `vatTreatmentId` lấy từ `GET /api/procurement/vat-treatments?activeOnly=true` hoặc từ chính đơn đã đọc. Hai danh sách lưu trữ chuyển từ mảng trần sang `items`, vẫn `Assert.Contains` đúng `id` và `deletedAt`, và `Assert.DoesNotContain` sau purge.
2. Login cần tenant. Đơn mua trên API hiện tại cần chế độ VAT. Danh sách trả về theo trang.
3. Helper lấy phần tử VAT active đầu tiên để làm dữ liệu vào, rồi các assertion nghiệp vụ của từng bài vẫn chạy. Không bỏ kiểm tra duyệt, vượt số lượng, lưu trữ hay purge.
4. Bám hợp đồng mua hàng hiện có. Không đổi schema Production và không thêm migration.
5. `SAFE_TO_INCLUDE`

#### `tests/KitPlatform.Api.Tests/SalesApiTests.cs`

1. Ba dòng: login thêm `tenantCode = "DEMO_PHARMACY"`; một `Assert.Contains` đọc `items` thay vì mảng trần. Các assertion giá seed `500` và lô `LOT2026A` không nằm trong diff.
2. Login cần tenant. Danh sách đơn trả về theo trang; bài test vẫn đòi đúng `orderId` trong `items`.
3. Không hạ assertion giá. Mục 4 của báo cáo này vẫn xếp bảy bài bán là fail vì dữ liệu local trả giá `5000`, và diff này không sửa chỗ đó.
4. Giữ kiểm tra đơn bán thật. Không đổi `SellableLotRules`.
5. `SAFE_TO_INCLUDE`

### 9.2 Các điểm kiểm riêng

| Điểm | Kết quả |
|---|---|
| Backup và runner có đụng Production khi mới commit không | Không. Commit không thực thi script. |
| `backup-db.sh` sau khi file được đưa vào đường cron | Lần chạy sau chỉ đọc `novixa_prod` và ghi archive ngoài database. Không áp SQL. Xóa archive cũ bị treo cho tới khi có `RETENTION_DELETE_CONFIRMED=true`. |
| `run-incremental-migrations-prod.sh` trên ledger hiện tại | Pending = 0 nên toàn `SKIP` trước thân compat. Không thực thi `060`. |
| `run-migrations-prod.sh` nếu bị gọi với URI Production | Chạy lại cả manifest và dùng thân compat cho `060`. Không dùng script này cho lần nâng ledger 232. Mục 6 giữ lệnh cấm `-RunMigrations`. |
| `greenfield-compat/060_family_members_status_align.sql` | Đúng mục tiêu và đúng nội dung cần thay: cùng kết cục schema với file lịch sử, chỉ chuyển backfill `is_active` sang SQL động. File lịch sử không đổi. Tên này không được thêm vào manifest. |
| Fail-closed khi thiếu tenant | Giữ. Default rỗng thì login nhân viên trả `400`, không token, không dùng `Assessment:EventTenantCode`, không dùng `DEMO_PHARMACY`. OTP khách dừng trước khi gửi; xác minh OTP trả null trước khi cấp token. File Production ngoài commit vẫn đặt default `NT_XUANHOA`, nên request bỏ trống tenant trên cấu hình đó vẫn vào `NT_XUANHOA`. |
| Test bị sửa | Các assertion hành vi còn. Phần thêm là tenant tường minh, trang `items`, và `vatTreatmentId` bắt buộc. Bài mới kiểm login HTTP thật. Không thấy assertion bị nới thành luôn đúng. |
| `migrations/371_pack_content_brand_narrative_dna.sql` | Không nằm trong danh sách 14 file. Vẫn untracked. Lệnh `git add` ở mục 5 không có tên này. |
| `069_assessment_pharmacy_v1_seed.sql` | `migration-files.prod.txt` không chứa tên này. Manifest không thuộc commit. |

### 9.3 Quyết định

`APPROVE_CANDIDATE_COMMIT_LIST`

Đủ bằng chứng để chủ dự án duyệt đúng khối `git add` ở mục 5, rồi commit bằng thông điệp đã ghi ở đó. Chưa đủ điều kiện deploy: mục 7 vẫn còn khóa source thành artifact, hai ca đang mở, 231 đơn vị hết hạn, và rollback service chưa diễn tập. Phiên này không commit và không deploy.
