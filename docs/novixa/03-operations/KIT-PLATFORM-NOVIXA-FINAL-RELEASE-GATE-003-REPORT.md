# KIT-PLATFORM-NOVIXA-FINAL-RELEASE-GATE-003

**Ngày:** 2026-10-16  
**Kết luận:** `BLOCKED`  
**Môi trường rollback:** `BLOCKED_ENVIRONMENT_UNAVAILABLE`  
**Không deploy Production.** Không commit, không push, không merge, không rebase. Không restart service, không sửa cấu hình, không chạy migration, không ghi dữ liệu và không tạo giao dịch trên `novixa_prod`.

Báo cáo này tiếp nối `KIT-PLATFORM-NOVIXA-RELEASE-BLOCKERS-002-REPORT.md` và `KIT-PLATFORM-NOVIXA-RELEASE-READINESS-001-REPORT.md`. Candidate vẫn là worktree `E:\KitPlatform-wt-phase5-novixa-product-proof`. File này nằm trên cây `E:\KitPlatform`, không nằm trong danh sách commit của candidate.

## 1. Baseline đã xác minh

| Cây | HEAD | Trạng thái phiên này |
|---|---|---|
| `E:\KitPlatform` | `main` `8c59ceb4f3be537898106ec058ea547406f722e5` | Không đổi HEAD. Báo cáo 001, 002 và parity vẫn untracked. Phiên này chỉ thêm báo cáo này. |
| `E:\KitPlatform-wt-phase5-novixa-product-proof` | `phase5-novixa-product-proof` `5c9f65103fd030e37f2788a9b99565a9f918a140` | Đúng HEAD của báo cáo 001 và 002. Không reset, clean, stash, checkout đè hoặc xóa thay đổi. Không `git add`, không commit. Staging trống. |

`git status --short` trên candidate, trước và sau lệnh build, là cùng 12 file tracked và 3 mục untracked dưới đây. Không có sửa mới so với báo cáo 002.

Tracked:

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

Untracked, có từ trước, phiên này không sửa:

- `deploy/ubuntu/greenfield-compat/060_family_members_status_align.sql`
- `migrations/371_pack_content_brand_narrative_dna.sql`
- `tests/KitPlatform.Api.Tests/DefaultTenantResolutionTests.cs`

`migrations/060_family_members_status_align.sql`: `git diff --exit-code` trả 0. SHA-256 `E84257CC824305F66EA228092717403EA83FEF20388B0DD11D82856861B70180`. Git blob `f6dae5ea3cd85f9d219f50528a3709803642887b`.

Thân thay thế `deploy/ubuntu/greenfield-compat/060_family_members_status_align.sql`: SHA-256 `E01F0824B7143D76E5D352928D1E83400B3ECF289CCBB688F61C7DE6DFA52226`. File này không có tên trong `migration-files.prod.txt`.

`migration-files.prod.txt` vẫn 221 dòng dữ liệu. Không có `069_assessment_pharmacy_v1_seed.sql`, không có `371_pack_content_brand_narrative_dna.sql`, không có `greenfield-compat`.

## 2. File đã đọc, đã đổi, đã xác minh

Đã đọc lại báo cáo 001, 002 và phần VPS của `KITPLATFORM-NOVIXA-PRODUCTION-PARITY-001-REPORT.md`.

Đã đọc `git diff` của đúng các file auth, test và migration runner trong báo cáo 002. Nội dung khớp mô tả đó. Không có file tracked mới.

| Nhóm | Đã kiểm trong phiên này | Đã được probe trước đó |
|---|---|---|
| Auth fail-closed | Diff khớp: bỏ fallback `Assessment:EventTenantCode` và tenant ngầm `DEMO_PHARMACY`; thiếu tenant thì `AuthTenantRequiredException`; API trả 400. | Probe clone trong 001 và 002. Hai test `DefaultTenantResolutionTests` pass trong 001. Không chạy lại. |
| Test Pharmacy | Diff chỉ thêm `tenantCode = DEMO_PHARMACY`, `vatTreatmentId`, và đọc danh sách qua `items`. Không đổi assertion giá `500` hay lô `LOT2026A`. | Kết quả suite trong 001. Không chạy lại. |
| Runner migration | Diff khớp: bỏ BOM trên bản tạm, dùng `greenfield-compat/<tên file>` nếu có, ledger vẫn ghi tên file manifest. | Lab 002: incremental trên `novixa_rr001` là `applied=0`; cài mới vượt 001 và 060 rồi dừng ở 090. Không chạy lại. |
| `backup-db.sh` | Vẫn nằm trong diff candidate, 97 dòng thêm theo `git diff --stat`. Không sửa thêm. | Bản copy script đang chạy trên VPS, đã ghi trong 001. Không đọc lại file trên VPS. |

`appsettings.json` đặt `Auth:DefaultTenantCode` rỗng. `appsettings.Production.json` vẫn đặt `NT_XUANHOA`. Process Production đang chạy và bản publish kèm file Production vẫn đăng nhập được khi client không gửi tenant. Development và test host không còn tenant ngầm.

Phiên này không sửa file candidate. File duy nhất được tạo là báo cáo này.

## 3. Build và test trong phiên này

| Lệnh | Cây | Exit | Kết quả |
|---|---|---|---|
| `dotnet build src\KitPlatform.Api\KitPlatform.Api.csproj -c Release --nologo` | worktree candidate | 0 | 0 error. Warning `NU1903` Newtonsoft.Json 10.0.3 lặp lại, đã có trong báo cáo 001. |
| `dotnet test` | không chạy | | Dùng lại phân loại 001 và 002. Không sửa test. |
| `dotnet publish` | không chạy | | Source chưa commit nên không tạo hash artifact. |

SDK trên máy: `dotnet` `10.0.301`.

Lệnh build tái lập, chỉ chạy sau khi chủ dự án đã commit đúng danh sách mục 6:

```powershell
cd E:\KitPlatform-wt-phase5-novixa-product-proof
dotnet publish src\KitPlatform.Api\KitPlatform.Api.csproj -c Release -o <thư-mục-publish> --no-self-contained
Get-FileHash <thư-mục-publish>\KitPlatform.Api.dll -Algorithm SHA256
```

`dotnet build` ở trên chỉ chứng minh source hiện tại biên dịch được. Output `src\KitPlatform.Api\bin\Release` không phải artifact deploy. Không có hash phát hành cho source này.

File lab `C:\novixa-rr001-lab\publish-api\KitPlatform.Api.dll` vẫn là bản 1,808,896 byte, 16:30 +07, SHA-256 bắt đầu `FC90329B`. Đó là compile từ source auth chưa commit, trước khi runner migration được sửa. Hash đó không phải hash phát hành của source hiện tại. Build .NET không deterministic. Phiên này không publish đè file đó.

SPA trong `C:\novixa-rr001-lab\spa\` vẫn còn các thư mục `admin`, `customer-app`, `staff-app`, `prescriber-portal`, `family-app` từ phiên 001. Phiên này không build lại. `partner-portal` và `assessment-web` vẫn ngoài bước rsync bắt buộc của `deploy-update-vps.ps1`.

## 4. Môi trường và rollback service

**`BLOCKED_ENVIRONMENT_UNAVAILABLE`.** Không có restart service và không có rollback service. Downtime chưa xác định.

Đã kiểm trên máy làm việc này:

| Thành phần cần có | Kết quả phiên này |
|---|---|
| systemd unit `kit-platform-api` | Không có. Máy là Windows. |
| nginx, cây `/var/www/kit-platform` | Không có nginx. Không có `/var/www/kit-platform`, `/opt/kit-platform`, `/etc/kit-platform/api.env`. |
| WSL | `wsl.exe` có trên đĩa nhưng `wsl.exe --status` báo Windows Subsystem for Linux chưa cài, exit 50. Không chạy `wsl.exe --install`. |
| Docker, Podman, QEMU, VirtualBox, NSSM | Không có lệnh. |
| Hyper-V | `Get-VM` không tồn tại. Truy vấn optional feature cần quyền elevation; không nâng quyền và không bật feature. |
| Service Windows tên `kit-platform` hoặc `nginx` | Không có. `postgresql-x64-18` đang chạy, đó là Postgres Windows cổng local, không phải cluster lab. |
| Cổng `5433` và `5299` | Không có listener. Cluster lab và API lab của phiên 002 không được bật lại. |

Hạ tầng đã được script deploy trỏ tới chỉ có một SSH target: `root@103.200.23.229`. Cùng host đó phục vụ `api.novixa.vn` và, theo báo cáo parity, cả `family.kittech.vn`. Đó là Production. Không dùng host này để diễn tập. Không mua VPS. Không cài phần mềm mới.

`C:\novixa-rr001-lab\rollback-drill` và process `dotnet` cổng `5299` của các phiên trước là đổi file hoặc tắt process trên Windows. Không có systemd, không có nginx, không có restart unit. Không tính là rollback service.

Điều kiện còn thiếu, đủ để mở mục này: một máy không phải Production, đã có sẵn systemd, nginx và cây thư mục triển khai, để lưu bản cũ, đổi binary và static, `systemctl restart kit-platform-api`, health check, copy bản cũ trở lại, restart lần nữa, rồi smoke. Chưa có máy đó.

## 5. Kiểm tra nghiệp vụ, dùng lại 001 và 002

Không đọc lại Production. Không tạo giao dịch. Không đóng ca. Không điều chỉnh 231 đơn vị hết hạn. Không sửa seed.

| Cổng | Bằng chứng đang có | Trạng thái dùng cho gate |
|---|---|---|
| Login thiếu tenant thì fail-closed | 001 và 002: 400 «Mã nhà thuốc là bắt buộc khi chưa cấu hình tenant mặc định.» Không có token. | Đạt trên clone và trên test host. |
| Login đúng tenant | 001 và 002: `NT_XUANHOA` trả `tenantCode=NT_XUANHOA`. | Đạt trên clone. |
| Cô lập tenant | GET sản phẩm tenant khác 404; cùng tenant 200. | Đạt trên clone. |
| FEFO và từ chối lô hết hạn | Preview chọn `TON-DAU-SAPO`. Lô hết hạn `60551` không giảm. | Đạt trên clone. |
| Nhập, trả, ca | Phiếu nhập không PO, trả hàng, mở và đóng ca đều có mã HTTP ghi trong 001 và 002. | Đạt trên clone. |
| Cashier không đọc báo cáo | Cùng URL doanh thu: admin 200, cashier 403. | Đạt trên clone. `SalesReadOnlyPrincipalTests` pass trong 001. |
| Ledger Xuân Hòa | Pending 0, skipped 221, ledger 232. Incremental 002: `applied=0 skipped=221 pending_ran=0`. | Đạt cho đường nâng ledger hiện có. |
| Cài database trống từ manifest | Vượt BOM và file 060 trên lab. Dừng ở `090` vì thiếu `069`. Đưa `069` vào manifest sẽ thành file pending và lỗi trên ledger đã có template version `1.1`. | Blocker riêng của cài mới. Không xử lý trong phiên này. Không thêm `069` vào `migration-files.prod.txt`. |
| Backup đã restore | `novixa_prod_20261016_020001.sql.gz`, 15,019,608 byte, gzip CRC hợp lệ, restore `psql` exit 0 vào `novixa_rr001`. | Đạt cho đúng dump này. |
| Backup chưa kiểm chứng | 81 file 20 byte. Các dump tháng 7 khoảng 4 MB và dump lớn còn lại ngoài file trên. | Chưa restore. Không xóa. Không bật `RETENTION_DELETE_CONFIRMED`. |
| 231 đơn vị hết hạn | Clone còn 3 lô, 231 đơn vị. Candidate từ chối bán lô có ngày trước hôm nay. | Chưa có xác nhận của chủ nhà thuốc. |
| Ca đang mở | Lần đọc Production cuối là báo cáo 001, 17:02 +07 ngày 2026-10-16: ba ca `status = 1`, đơn mới nhất `17:02:38`. | Chưa có xác nhận vận hành. Phiên này không đọc lại. |

Bảy test bán trên `kitplatform:5432` vẫn được phân loại là dữ liệu local lệch seed. Không sửa assertion. Bốn test Content và hai test Local OS vẫn ngoài cổng Pharmacy. Không chạy lại suite.

## 6. Khóa candidate

Source chưa khóa. HEAD `5c9f6510` không chứa các sửa chưa commit. Không có hash DLL chính thức.

Danh sách file dự kiến commit trong báo cáo 002 đã được đối chiếu với `git status`. Khớp. Không thêm file. Không gồm `migrations/371_pack_content_brand_narrative_dna.sql`.

Chủ dự án, sau khi duyệt diff, tự chạy. Phiên này không chạy hai lệnh này.

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

Sau commit mới được publish bằng lệnh mục 3 và ghi hash mới. Hash `FC90329B…` không được ghi vào phiếu phát hành.

`deploy-update-vps.ps1` mặc định lấy `-Root E:\KitPlatform` và mặc định bỏ qua migration. Cây `E:\KitPlatform` là `main`, dirty ngoài candidate. Không gọi script với root đó. Cờ `-RunMigrations` không được bật cho lần cắt này khi pending vẫn 0. Script vẫn rsync `deploy/ubuntu/` lên `/opt/kit-platform/` và tự `systemctl restart kit-platform-api`. Vì vậy không chạy script đó trước khi các cửa ở mục 8 đã xong.

Cấu hình `/etc/kit-platform/api.env` và secret không nằm trong Git và không được đóng gói.

## 7. Trạng thái từng release gate

| Gate | Trạng thái | Vì sao |
|---|---|---|
| Candidate truy vết được tới commit | Mở | HEAD `5c9f6510`. Sửa chưa commit. |
| Artifact hash từ source đã khóa | Mở | Build Release exit 0. Không publish. Hash lab 16:30 không phải hash phát hành. |
| Nghiệp vụ Pharmacy trọng yếu trên bản sao | Đạt, bằng chứng 001 và 002 | Login, cô lập tenant, FEFO, từ chối hết hạn, nhập, trả, ca, cashier 403. |
| Backup đã restore | Đạt một phần | Chỉ dump `20261016_020001`. Các archive còn lại chưa restore. |
| Restart và rollback service trên lab | Mở | `BLOCKED_ENVIRONMENT_UNAVAILABLE`. Không có systemd và nginx ngoài Production. |
| Regression nghiêm trọng chưa phân loại | Không thấy trong diff candidate | Diff khớp báo cáo 002. `NU1903` là warning đã biết. Cài mới từ manifest là blocker riêng, đã phân loại. |
| Kế hoạch smoke, dừng, rollback | Viết ở mục 9 | Chưa được thử trên service. |
| Xác nhận chủ nhà thuốc và ca đang mở | Mở | 231 đơn vị và ba ca lúc 17:02 chưa được người có thẩm quyền nhận. |
| Cài database trống từ manifest | Mở, riêng | Không chặn đường pending = 0 của Xuân Hòa. Không sửa bằng cách thêm `069`. |

## 8. Việc chủ nhà thuốc và người vận hành cần xác nhận

Chưa đủ để chọn giờ. Phiên này không giả định giờ đóng cửa.

- Người vận hành xác nhận từng ca đang mở, gồm ca `2026-07-27` nếu ca đó vẫn mở. Ca treo được đóng có chủ đích hoặc được chấp nhận bằng văn bản. Không deploy khi còn bán. Lần đọc cuối là 17:02 +07 ngày 2026-10-16; số liệu có thể đã đổi.
- Chủ nhà thuốc chấp nhận candidate từ chối bán 231 đơn vị hết hạn của `NT_XUANHOA`. Lô không ngày không bị chặn; trên clone số lô còn hàng không ngày là 0.
- Dump trong ngày deploy đã `gzip -t` và mới hơn sổ cần giữ, hoặc chủ nhà thuốc chấp nhận dump `20261016_020001` chỉ là mốc đã restore thử.
- Có máy lab không bán hàng, cùng kiểu systemd, nginx và `/var/www/kit-platform`, và diễn tập mục 9.2 đã ghi log, exit code, hash và downtime.
- Commit mục 6 đã xong, publish đã chạy, hash DLL mới đã ghi. Hash `FC90329B…` không được dùng.
- Không bật `-RunMigrations`. Không thêm `069` vào manifest. Không commit `371`.

## 9. Lệnh chủ dự án thực hiện tiếp theo, đúng thứ tự

Dừng sau bước đang bị chặn. Không nhảy tới Production.

### 9.1. Chấp nhận hoặc từ chối môi trường lab

Nếu chưa cho phép cài WSL, Docker hoặc mua máy, gate này tiếp tục `BLOCKED_ENVIRONMENT_UNAVAILABLE`. Bước 9.2 chưa làm được.

Khi đã có một máy Linux không phải `103.200.23.229`, đã có systemd, nginx, Postgres tách khỏi `novixa_prod`, và cây `/var/www/kit-platform` cùng `/etc/kit-platform/api.env`, chạy diễn tập dưới đây trên máy đó. `api.env` của lab trỏ database lab, không trỏ `novixa_prod`.

### 9.2. Diễn tập trên lab, trước mọi động chạm Production

```bash
set -euo pipefail
PREV=/var/backups/kit-platform-release-prev
mkdir -p "$PREV/api" "$PREV/admin" "$PREV/customer-app" "$PREV/staff-app" "$PREV/prescriber-portal" "$PREV/family-app" "$PREV/opt"
cp -a /var/www/kit-platform/api/KitPlatform.Api.dll "$PREV/api/"
cp -a /var/www/kit-platform/admin/. "$PREV/admin/"
cp -a /var/www/kit-platform/customer-app/. "$PREV/customer-app/"
cp -a /var/www/kit-platform/staff-app/. "$PREV/staff-app/"
cp -a /var/www/kit-platform/prescriber-portal/. "$PREV/prescriber-portal/"
cp -a /var/www/kit-platform/family-app/. "$PREV/family-app/"
cp -a /opt/kit-platform/. "$PREV/opt/"
sha256sum /var/www/kit-platform/api/KitPlatform.Api.dll | tee "$PREV/dll-before.txt"
systemctl is-active kit-platform-api
curl -sf http://127.0.0.1:5000/api/health; echo
curl -sf http://127.0.0.1:5000/api/health/db; echo
```

Sau đó mới copy binary và static của candidate đã khóa đè lên các cây tương ứng, giữ nguyên `api.env`. Rồi:

```bash
date -Is | tee /tmp/kit-lab-restart-start.txt
systemctl restart kit-platform-api
systemctl is-active kit-platform-api
curl -sf http://127.0.0.1:5000/api/health; echo
curl -sf http://127.0.0.1:5000/api/health/db; echo
date -Is | tee /tmp/kit-lab-restart-end.txt
sha256sum /var/www/kit-platform/api/KitPlatform.Api.dll | tee "$PREV/dll-after-deploy.txt"
```

Smoke lab: login đúng tenant của database lab, một tra cứu POS, một preview không chọn lô hết hạn, tài khoản cashier không mở báo cáo. Ghi mã HTTP.

Rollback lab, bắt buộc có restart lần hai:

```bash
cp -a "$PREV/api/KitPlatform.Api.dll" /var/www/kit-platform/api/KitPlatform.Api.dll
cp -a "$PREV/admin/." /var/www/kit-platform/admin/
cp -a "$PREV/customer-app/." /var/www/kit-platform/customer-app/
cp -a "$PREV/staff-app/." /var/www/kit-platform/staff-app/
cp -a "$PREV/prescriber-portal/." /var/www/kit-platform/prescriber-portal/
cp -a "$PREV/family-app/." /var/www/kit-platform/family-app/
date -Is | tee /tmp/kit-lab-rollback-start.txt
systemctl restart kit-platform-api
systemctl is-active kit-platform-api
curl -sf http://127.0.0.1:5000/api/health; echo
curl -sf http://127.0.0.1:5000/api/health/db; echo
date -Is | tee /tmp/kit-lab-rollback-end.txt
sha256sum /var/www/kit-platform/api/KitPlatform.Api.dll | tee "$PREV/dll-after-rollback.txt"
```

Hash sau rollback phải trùng `dll-before.txt`. Lặp smoke. Đổi file trên đĩa mà không `systemctl restart` không tính là đạt. Downtime là khoảng giữa hai mốc thời gian của mỗi lần restart, đo tới lúc health trả 200.

Không copy script `backup-db.sh` của Git HEAD cũ đè lên script lab nếu script đó vẫn là bản `pg_dump -U`. Bản candidate của `backup-db.sh` là bản đã bỏ `-U`.

### 9.3. Khóa source và tạo artifact

Sau khi diff được duyệt: chạy đúng `git add` và `git commit` ở mục 6. Không thêm `371`. Không thêm `069` vào manifest. Rồi chạy `dotnet publish` ở mục 3 từ commit vừa tạo. Ghi hash mới vào phiếu phát hành.

Build SPA với `VITE_API_BASE_URL=https://api.novixa.vn` cho admin, customer-app, staff-app, prescriber-portal và family-app, từ cùng commit, vào thư mục publish mà script deploy đọc. Đặt `-Root` của script bằng cây đã publish. Không dùng mặc định `E:\KitPlatform`.

### 9.4. Cửa Production, chỉ khi 9.1 đến 9.3 và mục 8 đã xong

Chủ dự án tự chọn giờ sau khi quầy ngừng bán. Báo cáo này không chọn giờ.

Trước khi gọi script, trên Production, lưu DLL, năm cây SPA và `/opt/kit-platform` theo cùng kiểu mục 9.2, vào thư mục ngoài cây đang chạy. Ghi hash DLL đang phục vụ. Hash đã biết lúc 17:02 +07 ngày 2026-10-16 là `26afdaa7f123fa36496f31652a01a6d87942e975c4baf72b0b49bfff977a8941`, 1,786,368 byte. Phải đo lại ngay trước cắt vì file có thể đã đổi.

Không truyền `-RunMigrations`.

`deploy-update-vps.ps1` tự reload nginx và `systemctl restart kit-platform-api`. Người vận hành đứng cạnh log:

```bash
journalctl -u kit-platform-api -n 50 --no-pager
curl -sf https://api.novixa.vn/api/health; echo
curl -sf https://api.novixa.vn/api/health/db; echo
```

Smoke sau cắt: health 200, health/db báo database sống, login `NT_XUANHOA`, một tra cứu POS, một preview không chọn lô hết hạn, cashier không mở báo cáo. `deploy/ubuntu/smoke-test.sh` kiểm thêm index SPA, CORS và vài route đã mount.

Dừng và trả bản đã lưu nếu health khác 200, login vào nhầm tenant, POS không tạo được đơn hàng còn hạn, hoặc tồn một lô còn hạn giảm khi chưa có đơn. Rollback lúc đó là copy DLL và cây SPA đã lưu đè lại, `systemctl restart kit-platform-api`, rồi lặp smoke. Không rollback database nếu migration không chạy. Không coi thao tác đổi DLL trên đĩa, khi chưa restart, là rollback service.

Nếu pending không còn là 0 vào lúc cắt, dừng. Không áp `069` và không chạy incremental cho đến khi có một quyết định riêng.

## 10. Downtime

Chưa xác định. Phiên này không restart service nào của Kit Platform. Không có mốc thời gian đo được.

Khi các cửa trên đã xong và pending vẫn 0, khoảng gián đoạn dự kiến chỉ là một lần restart `kit-platform-api` cộng thời gian reload static. Con số đó vẫn chưa đo. Diễn tập 9.2 là chỗ đo lần đầu.

## 11. Kết luận

`BLOCKED`

Đường nâng ledger Xuân Hòa vẫn không có file chờ, và các luồng nhà thuốc trọng yếu đã có bằng chứng trên bản sao dump `20261016_020001`. Source hiện tại vẫn build Release được. Những cửa bắt buộc cho `READY_FOR_OWNER_DEPLOY_APPROVAL` chưa đủ: chưa có máy lab để restart và rollback service, source chưa commit nên chưa có artifact khóa, chủ nhà thuốc chưa nhận quy tắc 231 đơn vị hết hạn, và người vận hành chưa xác nhận các ca đang mở. Cài mới từ manifest vẫn là blocker riêng vì thiếu `069`, và `069` không được đưa vào manifest Production. Không deploy.
