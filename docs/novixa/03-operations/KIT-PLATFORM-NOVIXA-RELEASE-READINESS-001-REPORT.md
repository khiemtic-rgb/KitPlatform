# KIT-PLATFORM-NOVIXA-RELEASE-READINESS-001

**Ngày:** 2026-10-16, khoảng 16:25–17:05 +07  
**Kết luận:** `BLOCKED`  
**Phạm vi đã làm:** sửa tenant fallback trên candidate, test Platform/Pharmacy, restore dump đã `gzip -t` vào database cô lập, đối chiếu migration, chạy các luồng Pharmacy trên bản sao, build API và các SPA của script deploy, diễn tập đổi binary trên thư mục không phải Production.

Không commit, không push, không merge, không deploy, không restart service Production, không chạy migration trên Production, không tạo đơn hay đóng ca trên `novixa_prod`.

## 1. Baseline trước và sau

Đã đọc lại, không dùng trạng thái cũ khi chưa kiểm.

| Cây | Trước | Sau phiên này |
|---|---|---|
| `E:\KitPlatform` | `main` `8c59ceb4f3be537898106ec058ea547406f722e5`, dirty (Content/Famixa) | Cùng HEAD. Thêm duy nhất báo cáo này. Không sửa file tracked khác. Không commit. |
| `E:\KitPlatform-wt-phase5-novixa-product-proof` | `phase5-novixa-product-proof` `5c9f65103fd030e37f2788a9b99565a9f918a140`, sạch ngoài untracked `migrations/371_pack_content_brand_narrative_dna.sql` | Cùng HEAD. File untracked `371_…` giữ nguyên, phiên này không tạo và không xóa. Các sửa bên dưới chưa commit. |

Candidate đã ghi nhận `643932c0ba35cac0cad069a789a7197671f4ba24` vẫn là commit runtime bất biến. Tip `5c9f6510` chỉ thêm tài liệu so với commit đó. Artifact trong phiên này được build từ worktree **đã sửa chưa commit**, không phải từ `643932c0` thuần.

Production lúc 17:02 +07:

- Host `novixa`, service `kit-platform-api` active.
- `/var/www/kit-platform/api/KitPlatform.Api.dll`: 1,786,368 byte, mtime 2026-09-27 11:47:15 +07, SHA-256 `26afdaa7f123fa36496f31652a01a6d87942e975c4baf72b0b49bfff977a8941`. Không đổi so với audit parity.
- Database `novixa_prod`. Ba ca `status = 1`. Đơn mới nhất `2026-10-16 17:02:38 +07`.

## 2. File đã sửa trên candidate và lý do

Tất cả nằm ở worktree Phase 5. Không đụng `E:\KitPlatform` ngoài báo cáo này.

| File | Lý do |
|---|---|
| `src/KitPlatform.Infrastructure/Auth/AuthService.cs` | Bỏ fallback sang `Assessment:EventTenantCode` và bỏ tenant ngầm `DEMO_PHARMACY` khi `Auth:DefaultTenantCode` rỗng. Login không có tenant thì ném `AuthTenantRequiredException`. |
| `src/KitPlatform.Application/Auth/AuthContracts.cs` | Exception mô tả lỗi thiếu tenant. |
| `src/KitPlatform.Api/Controllers/AuthController.cs` | Trả 400 với thông báo rõ, không cấp token. |
| `src/KitPlatform.Infrastructure/CustomerApp/CustomerAppAuthService.cs` | Cùng lỗ hổng: OTP khách không còn lấy `NVX-CS08` khi key mặc định rỗng. Thiếu tenant thì API trả «Mã nhà thuốc là bắt buộc». |
| `tests/KitPlatform.Api.Tests/DefaultTenantResolutionTests.cs` | Regression: login không `tenantCode` không vào `NVX-CS08`. Login `DEMO_PHARMACY` tường minh thì tenant trả về đúng mã đó. |
| `SalesApiTests.cs`, `InventoryApiTests.cs`, `ProcurementApiTests.cs`, `CatalogApiTests.cs`, `CustomerConsentApiTests.cs` | Khai báo `tenantCode = DEMO_PHARMACY`. |
| `ProcurementApiTests.cs` | Gửi `vatTreatmentId` đang active. API bắt buộc field này; thiếu thì 400 «Loại thuế GTGT không tồn tại». |
| `InventoryApiTests.cs`, `ProcurementApiTests.cs`, `SalesApiTests.cs` | Đọc danh sách phân trang qua `items`, đúng contract API. |
| `deploy/ubuntu/backup-db.sh` | Bản copy của script **đang chạy** trên VPS. Bản trong Git cũ dùng `pg_dump -U` và ghi thẳng file cuối, chính là nguyên nhân file 20 byte. `deploy-update-vps.ps1` rsync `deploy/ubuntu/` lên `/opt/kit-platform/`. Giữ bản cũ trong candidate sẽ đè script đã sửa trên máy khi deploy sau này. Không sửa cron và không sửa file trên VPS. |

## 3. Build và test

Cây: `E:\KitPlatform-wt-phase5-novixa-product-proof`. Configuration `Release`.

| Lệnh | Exit | Kết quả |
|---|---|---|
| `dotnet build src\KitPlatform.Api\KitPlatform.Api.csproj -c Release` | 0 | 0 error. Warning `NU1903` Newtonsoft.Json 10.0.3 lặp lại. |
| `dotnet publish … -c Release -o C:\novixa-rr001-lab\publish-api --no-self-contained` | 0 | DLL 1,808,896 byte. SHA-256 `FC90329B467C66765531E58CF77C3AA7DB5AAC18D1FC6B995AD677B6A762350A`. |
| `dotnet test` Application.Tests `-c Release` | 0 | Passed 26, failed 0. |
| `dotnet test` Platform.Tests `-c Release` | 1 | Passed 163, failed 6, total 169. |
| `dotnet test` Api.Tests `-c Release` (lần cuối) | 1 | Passed 26, failed 8, total 34. |

So với audit parity (API 9 pass / 23 fail), các test đăng nhập nhầm `NVX-CS08` đã hết. Hai test tenant mới đều pass: thiếu tenant trả 400 và không có `accessToken` hay `NVX-CS08`; login `DEMO_PHARMACY` trả đúng tenant.

**8 API fail còn lại**

- Bảy test bán hàng trên local `kitplatform` / `DEMO_PHARMACY` vẫn giả định giá seed `500` và lô `LOT2026A`. API trả giá `5000.00` và lô gợi ý `5555`. Thanh toán tính theo giá cũ nên một số lệnh tạo đơn nhận 400. Đây là dữ liệu local đã lệch seed, không phải kho Xuân Hòa. FEFO trên bản sao Production được chứng minh riêng ở mục 6.
- `Customer_consent_upsert_writes_outbox_event` không kết nối được `localhost:80`. Lỗi môi trường webhook, không phải Pharmacy.

**6 Platform fail**

- Bốn test Content (`ProductionCustomerIsolationTests`, `ProductionOwnershipHttpTests`) đòi `KPA_PHASE15_DISPOSABLE_PG` trỏ database `kpa_phase15_once` cổng `54341`, và từ chối cổng `5432`. Biến không được đặt. Không tạo Postgres thứ hai cho Content.
- Hai test Local OS (`LocalOsTextExtractTests`, `LocalOsTextExtractSummaryTests`) fail assertion. Ngoài cổng Pharmacy. Không sửa pack Local OS.

`ControlledPilotSaleTests` (gồm bán FEFO local và từ chối lô hết hạn) và `SalesReadOnlyPrincipalTests` nằm trong nhóm pass.

## 4. Tenant fallback

`appsettings.json` đặt `Auth:DefaultTenantCode` rỗng. `appsettings.Development.json` đặt `Assessment:EventTenantCode` = `NVX-CS08`. Trước sửa, login không gửi tenant đi vào `NVX-CS08`.

Sau sửa, nhánh đó không còn. Production `appsettings.Production.json` vẫn đặt `DefaultTenantCode` = `NT_XUANHOA`, nên process đang chạy và bản candidate khi publish với file đó vẫn đăng nhập được khi client không gửi tenant. Development và test host không còn tenant ngầm.

## 5. Backup và restore

### Nguyên nhân file 20 byte

Đã đọc `/opt/kit-platform/backup-db.sh` và `/var/log/kit-platform-backup.log`. Không sửa chúng trên VPS.

Script Git cũ chạy `sudo -u postgres pg_dump -U kitplatform`. Socket local dùng peer: OS user `postgres` không được yêu cầu role khác. `pg_dump` thất bại, stdout rỗng, `gzip` vẫn tạo archive 20 byte (`1f8b 0800 …`). Redirect ghi thẳng file cuối nên file hỏng nằm lại. `set -e` không xóa file đó. Retention không chạy khi pipeline fail, nên các file 20 byte tích lại.

Script trên VPS đã được sửa từ trước phiên này: dump bằng `sudo -u postgres pg_dump -d "$DB_NAME"` không có `-U`, ghi `.partial`, `gzip -t`, kiểm tra header dump và `CREATE TABLE public.tenants`, rồi mới `mv`. Cron `0 2 * * *` trong `/etc/cron.d/kit-platform-backup`. Log `2026-10-16 02:00`: `BACKUP_OK` 15,019,608 byte, uncompressed 73,578,246. Xóa file cũ đang treo (`RETENTION_DELETE_SUSPENDED`) cho đến khi operator đặt `RETENTION_DELETE_CONFIRMED=true`.

Histogram thư mục backup: 81 file 20 byte, bốn dump tháng 7 khoảng 4 MB, hai dump lớn đã `gzip -t` trên VPS.

### Restore cô lập

Dump dùng: `/var/backups/KitPlatform/novixa_prod_20261016_020001.sql.gz`.

- Tải về `C:\novixa-rr001-lab\`. Local size 15,019,608. Python `gzip` đọc hết 73,578,246 byte, CRC hợp lệ.
- Không có `\connect` và không có `CREATE DATABASE` trong dump.
- Cluster mới `C:\novixa-rr001-lab\pgdata`, chỉ `127.0.0.1:5433`, không đụng Postgres Windows cổng `5432`.
- Trước restore: `current_database=novixa_rr001`, `host=127.0.0.1`, `port=5433`. Không có database tên `novixa_prod` trên cluster này.
- `psql` restore exit 0.
- Cluster đã `pg_ctl stop` cuối phiên. Dữ liệu còn trên đĩa lab.

Đối chiếu số liệu. Dump là 02:00; Production được đọc lại lúc khoảng 16:40 và 17:02 nên sổ bán đã chạy thêm. Đó là lệch thời điểm, không phải restore hỏng.

| Chỉ số | Clone `novixa_rr001` | Production lúc đọc |
|---|---|---|
| Ledger | 232 | 232 |
| Tenant | 8 mã giống nhau, gồm `NT_XUANHOA` | 8 |
| Sản phẩm chưa xóa | 3,056 | 3,056 |
| Lô hết hạn mọi tenant | 11 / 1,564 | 11 / 1,564 |
| `NT_XUANHOA` lô hết hạn còn hàng | 3 lô, 231 đơn vị | không đếm lại từng lô; tổng mọi tenant không đổi |
| Đơn hoàn tất | 6,546 | 6,627 lúc ~16:40 |
| Phiếu nhập | 431 | 433 lúc ~16:40 |
| Ca mở | 1 trong dump 02:00 | 3 lúc 17:02 |

Hạn của ba lô `NT_XUANHOA` trên clone: hai lô hết `2026-03-31` tổng 229; một lô hết `2026-07-31` số lượng 2. Không có lô `expiry_date` null còn hàng. Không sửa và không xóa 231 đơn vị này trên Production hay trên clone ngoài một phép bán thử đã trả lại (mục 6), và phép bán đó không đụng lô hết hạn.

Local `kitplatform` cổng `5432` không được dùng làm bản sao Production.

## 6. Schema, migration, và nghiệp vụ trên clone

### Nâng ledger hiện có

Đối chiếu 221 dòng `migration-files.prod.txt` với 232 dòng ledger clone, trên `127.0.0.1:5433` / `novixa_rr001`.

- Pending: **0**. Skipped: 221. Ledger sau kiểm vẫn 232.
- Có trên ledger, không có trong manifest candidate: `230`, `231` (Family), `281`–`288`, `290` (Content). Đúng như audit parity. Runner incremental sẽ không gỡ chúng.
- Không có file Pharmacy mới so với ledger.

Đường deploy thật của Xuân Hòa là nâng ledger này. Với candidate hiện tại, bước migration là no-op.

### Database mới từ manifest

`novixa_rr001_fresh` trên cùng cluster `5433`, không phải `novixa_prod`.

- 41 file SQL trong `migrations/` có BOM UTF-8. `001_extensions.sql` bắt đầu `EF BB BF`. `psql` 18 báo syntax error nếu chạy file gốc.
- Sau khi bỏ BOM chỉ trong bản copy lab (không sửa file Git), manifest chạy tới file 40, rồi dừng ở file 57 `060_family_members_status_align.sql`: `UPDATE` tham chiếu `is_active` trong khi cột không tồn tại trên schema mới. `EXISTS` trong `WHERE` không cứu được lỗi parse. Exit 3.
- Greenfield từ manifest candidate: **FAIL**. Không sửa migration lịch sử vì ledger Production đã ghi nhận file này; sửa file đã apply là rủi ro schema không thuộc cửa sổ deploy no-op.

### Luồng API trên clone

API candidate listen `http://127.0.0.1:5299`, connection `Host=127.0.0.1;Port=5433;Database=novixa_rr001`. Worker Content, outbox, platform events, notify, push và Family reminder tắt. Process đã dừng cuối phiên.

User chỉ tạo trên clone: `rr001lab` (role `ADMIN`) và `rr001cashier` (role `CASHIER`), password thử nghiệm `Admin@123`. Không tạo user trên Production.

| Kịch bản | API | Database |
|---|---|---|
| Login không tenant | 400 «Mã nhà thuốc là bắt buộc khi chưa cấu hình tenant mặc định.» | Không cấp session |
| Login `NT_XUANHOA` | 200, `tenantCode=NT_XUANHOA` | |
| Báo cáo doanh thu admin | 200 | |
| Cùng URL bằng cashier (`sales.pos`, không `reports.read`) | 403 | |
| GET sản phẩm tenant khác | 404 | |
| GET sản phẩm cùng tenant | 200 | |
| Preview phân bổ 1 đơn vị | 200, lô `TON-DAU-SAPO`. Không có lô hết hạn `60551` | Trước bán: hết hạn `60551` = 2.000; lô còn hạn = 1.000 |
| Mở ca kho đó | 200, status 1 | |
| Tạo đơn | 201, status 2 (hoàn tất) | Hết hạn vẫn 2.000. Lô còn hạn còn 0.000. Movement `sales_order` gắn `TON-DAU-SAPO` |
| Trả hàng | 201 | Movement `sales_return` trên cùng lô. Tồn về 1.000. Hết hạn vẫn 2.000 |
| Đóng ca | 200, status 2 | |
| Phiếu nhập không PO, lô `RR001LAB57B6`, hạn 2030-12-31, số lượng 1 | Tạo 201, hoàn tất 200 | `quantity_received=1`, `quantity_available=1`, movement `goods_receipt`. Chỉ trên clone |
| Mở và đóng ca ở kho chưa có ca | 200 rồi 200, status 1 rồi 2 | |

Lô không ngày: trên clone, số lô còn hàng với `expiry_date` null là 0, khớp contract `SellableLotRules` (lô không ngày vẫn bán được, nhưng Xuân Hòa hiện không có lô như vậy).

**Tác động nghiệp vụ P5, cần chủ nhà thuốc xác nhận trước khi bật binary mới.** Candidate từ chối bán lô có ngày và ngày đó trước hôm nay. `NT_XUANHOA` có 3 lô hết hạn, 231 đơn vị, sẽ không bán được nữa. Lô không ngày không bị chặn. Phiên này không điều chỉnh tồn đó.

## 7. Release artifact, frontend, rollback

### Artifact

- Commit nền: `5c9f6510`, cộng sửa chưa commit ở mục 2.
- DLL lab: `C:\novixa-rr001-lab\publish-api\KitPlatform.Api.dll`, hash ở mục 3.
- Hash này khác DLL Production `26afdaa7…`. Build .NET không deterministic; hash định danh lần compile này.
- Cấu hình Production (`/etc/kit-platform/api.env`, secrets) không nằm trong Git và không được đóng gói.
- `deploy-update-vps.ps1` mặc định không chạy migration (`-RunMigrations` mới bật). Với pending = 0, bật cờ cũng không apply file mới. Vẫn phải tách khỏi bước đổi binary và không chạy trên Production từ phiên này.
- Script ghi đè `/var/www/kit-platform` bằng rsync. API không `--delete`. Admin, staff, customer, prescriber, family có `--delete`. Không có `/var/www/kit-platform.prev` trên VPS (đã ghi ở audit parity, không tạo trên Production).

### Frontend

`npm ci` và `npm run build` với `VITE_API_BASE_URL=https://api.novixa.vn` cho các SPA mà `deploy-update-vps.ps1` đưa lên:

| SPA | `index.html` | `version.json` | `api.novixa.vn` trong bundle |
|---|---|---|---|
| admin | có | không | có |
| customer-app | có | không | có trong bộ build cùng biến |
| staff-app | có | có, `build` `2026-10-16T10:01:18.960Z` | có |
| prescriber-portal | có | không | build cùng biến |
| family-app | có | có | gọi path tương đối `/family-os/…`, đúng ghi chú deploy: nginx same-origin |

Output: `C:\novixa-rr001-lab\spa\`. `partner-portal` và `assessment-web` có trong `deploy-production.ps1` nhưng không nằm trong bước rsync bắt buộc của `deploy-update-vps.ps1`. Chưa build hai SPA đó.

`node_modules` được tạo trong worktree candidate khi build. Gitignore. Không xóa.

### Rollback

Diễn tập trên `C:\novixa-rr001-lab\rollback-drill`, không đụng VPS:

- Hash file đứng trước: `20A7EC84…`
- Thay bằng DLL candidate: `FC90329B…`
- Copy bản đã lưu đè lại: hash trở về `20A7EC84…`

Đó là đổi file trên đĩa lab. Không chạy `systemctl`, không rsync lên `/var/www/kit-platform`, không thử rollback service đang phục vụ nhà thuốc. **Rollback Production chưa được chứng minh.**

## 8. Blocker còn mở

1. **Rollback service chưa thử trên máy không phải Production mà có cùng layout systemd/nginx.** Diễn tập file không đủ để mở cổng deploy.
2. **Quầy đang bán.** Lúc 17:02 +07: 3 ca mở (`2026-07-27 07:00`, `2026-10-16 09:34`, `2026-10-16 14:18`), đơn mới nhất `17:02:38`. Ca từ 27/07 có thể là ca treo; không tự đóng.
3. **Chủ nhà thuốc chưa xác nhận** từ chối bán 231 đơn vị hết hạn.
4. **Sửa candidate chưa commit.** Artifact `FC90329B…` không phải commit bất biến. Không commit trong phiên này.
5. **Tạo database trống từ manifest fail** tại `060_family_members_status_align.sql`, sau khi bỏ BOM ở bản copy lab. Nâng ledger Xuân Hòa thì pending = 0, nên không chặn riêng bước migration của lần deploy này, nhưng chặn một lần cài mới từ manifest.
6. **Backup lịch sử:** 81 file 20 byte vẫn nằm trên đĩa. Cron hiện đã ra dump dùng được. Retention chưa xóa. Không sửa cron.
7. **Test bán local** còn 7 fail vì giá/lô seed `DEMO_PHARMACY` trên `kitplatform` lệch giả định test, cộng 1 fail webhook `localhost:80`, cộng 6 fail Platform Content/Local OS. Không có fail biên dịch.

## 9. Thời điểm deploy, downtime, tiêu chí dừng

Không chọn giờ đồng hồ. Nhà thuốc còn giao dịch lúc 17:02. Không giả định giờ đóng cửa.

Điều kiện để owner mới được cân nhắc một cửa sổ:

- Ba ca mở đã được người vận hành xác nhận. Ca `2026-07-27` phải được đóng có chủ đích hoặc được chấp nhận bằng văn bản là ca treo không còn giao dịch. Không deploy khi còn bán.
- Dump `gzip -t` trong ngày deploy còn mới hơn sổ cần giữ, hoặc owner chấp nhận dump `20261016_020001` chỉ là mốc đã restore thử, không phải bản ngay trước cắt.
- Rollback binary đã thử trên một cây thư mục cùng kiểu `/var/www/kit-platform` nhưng không phải máy đang bán, gồm cả restart process. Hiện chưa có.
- Commit đã khóa hash artifact. Hash `FC90329B…` chỉ có giá trị đến lần build kế.
- Chủ nhà thuốc đã chấp nhận không bán 231 đơn vị hết hạn.

Downtime dự kiến nếu các điều kiện trên xong và migration vẫn pending = 0: chỉ restart `kit-platform-api` và reload static. Chưa bấm giờ trên VPS. Kế hoạch smoke sau cắt: health, health/db, login `NT_XUANHOA`, một tra cứu POS, một preview phân bổ không chọn lô hết hạn, cashier không mở được báo cáo. Dừng và trả binary cũ nếu health khác 200, login vào nhầm tenant, POS không tạo được đơn hàng còn hạn, hoặc tồn một lô còn hạn giảm khi chưa có đơn.

Rollback: copy DLL và cây SPA đã lưu đè lại, restart service, lặp smoke. Không rollback database nếu migration không chạy. Nếu ai đó đã bật `-RunMigrations` và file 060 hoặc BOM gây lỗi trên một database khác, không áp pattern đó lên `novixa_prod`.

## 10. Kết luận

`BLOCKED`

Candidate đã bỏ fallback tenant sang `NVX-CS08`, build Release được, và trên bản sao dump 02:00 đã chứng minh FEFO, từ chối lô hết hạn, nhập, trả, mở/đóng ca, cô lập tenant và cashier không đọc báo cáo. Nâng ledger Production theo manifest candidate không có file chờ. Chưa đủ cửa release: rollback service chưa thử, quầy còn ba ca mở và đơn lúc 17:02, quy tắc 231 đơn vị hết hạn chưa được chủ nhà thuốc nhận, và artifact chưa gắn commit. Không deploy.
