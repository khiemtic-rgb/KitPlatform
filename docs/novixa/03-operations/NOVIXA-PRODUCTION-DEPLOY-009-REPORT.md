# NOVIXA-PRODUCTION-DEPLOY-009

**Ngày:** 2026-10-16  
**Trạng thái cuối:** `BLOCKED`  
**Lần thử lại:** `docs/novixa/03-operations/NOVIXA-PRODUCTION-DEPLOY-010-REPORT.md`  
**Production không được cắt trong phiên này.** Không chạy `deploy-update-vps.ps1`. Không `-RunMigrations`. Không restart `kit-platform-api`. Không reload Nginx. Không đóng ca. Không sửa dữ liệu bán hàng.

## 1. Kết luận

Patch an toàn của `scripts\deploy-update-vps.ps1` đã nằm trong worktree và đã được kiểm thử cục bộ. Staging `C:\novixa-rc-008\staging-root` vẫn khớp release `66afd2d96512eaf8c0a6f533f2a571616b2781f6`. Backup `/var/backups/novixa-deploy-009/20261016T220533+0700` còn nguyên, hash khớp bản trên máy operator, dump đọc được và archive rollback giải được đúng DLL đang chạy.

Đọc lại Production lúc 22:24 +07 thì Nhà thuốc Xuân Hòa đã có ca thực tế đang mở: `SH-000151`, kho Thương mại, mở 22:11:41 +07 bởi `admin`, đã có 2 đơn. Cửa sổ nghỉ không còn. Deploy dừng trước cutover. Ca demo `DEMO_PHARMACY` / `AUD-SHIFT-TODAY` giữ nguyên, không đóng và không sửa.

## 2. Patch

File đã sửa: `E:\KitPlatform-wt-phase5-novixa-product-proof\scripts\deploy-update-vps.ps1`.  
HEAD worktree vẫn `66afd2d96512eaf8c0a6f533f2a571616b2781f6`. Chỉ file script này thành `M`. Chưa commit. Không đổi staging, manifest, migration hay runtime config.

Nội dung patch:

1. Mọi `plink` và `pscp` đi qua `Invoke-CheckedNative`. Hàm lấy `$LASTEXITCODE` ngay sau lệnh native. Khác 0 thì `throw` và không chạy lệnh sau. Lỗi trước `update.sh` ghi rõ remote update chưa bắt đầu.
2. `family-app\index.html` là artifact bắt buộc. Upload family-app không còn nhánh bỏ qua.
3. SHA-256 DLL local phải là `EC974ECCDD0E6CD4D83C5E3480F6757650D8A040C2FA80A7F747E9B403B73319` trước khi mở SSH. Sai thì dừng trước upload.
4. Remote script, trước mọi `rsync`, yêu cầu `KitPlatform.Api.dll` và `index.html` của admin, customer-app, staff-app, prescriber-portal, family-app. File thiếu hoặc rỗng thì `exit 1`.
5. SHA-256 DLL vừa upload phải đúng digest trên trước khi rsync API. Lệch thì `exit 1`.
6. Không truyền `-RunMigrations` thì remote script vẫn là `RUN_MIG=0` và in `Skip migrations`.

Kiểm thử cục bộ, không đụng Production:

| Kiểm thử | Kết quả |
|---|---|
| Parser PowerShell | PASS, 0 lỗi |
| Không còn lời gọi `plink`/`pscp` trần | PASS |
| `cmd /c exit 3` | Dừng, không chạy bước sau |
| `cmd /c exit 9` ở bước cutover | Báo partial update |
| `cmd /c exit 0` | Đi tiếp |
| Gate bash: thiếu `index.html` | Exit khác 0, chưa tới rsync |
| Gate bash: `family-app/index.html` rỗng | Exit khác 0, chưa tới rsync |
| Gate bash: DLL sai hash | Exit khác 0, chưa tới rsync |
| Gate bash: DLL staging và đủ 5 `index.html` | Tới điểm rsync, in đúng digest |
| Diff | Chỉ các cổng trên. Không đổi commit, `-Root` mặc định, hay nhánh migration |

`LocalMachine` và `CurrentUser` vẫn `Undefined`. Không gọi `Set-ExecutionPolicy`. Các lệnh kiểm tra dùng `-ExecutionPolicy Bypass` ở đúng process đó.

## 3. Đọc lại Production

Host `novixa`, 2026-10-16 22:24:48 +07. Chỉ lệnh đọc. `nginx -t` không reload.

| Hạng mục | Giá trị |
|---|---|
| `kit-platform-api` | `active`. `ActiveEnterTimestamp=Tue 2026-09-29 21:00:52 +07`. `NRestarts=524` là bộ đếm từ lúc unit được nạp; process hiện tại không restart trong lần đọc này |
| `pharmacore-api` | `inactive` |
| PostgreSQL | `active` |
| Unit failed | Không có |
| DLL đang chạy | 1,786,368 byte, mtime `2026-09-27 11:47:15 +07`, SHA-256 `26afdaa7f123fa36496f31652a01a6d87942e975c4baf72b0b49bfff977a8941` |
| Runtime | `Microsoft.NETCore.App/10.0.9`, `Microsoft.AspNetCore.App/10.0.9` |
| Health | `http://127.0.0.1:5000/api/health` = `{"status":"ok"}`. `health/db` = `database: true` |
| Env process | `ASPNETCORE_ENVIRONMENT=Production`. `ASPNETCORE_URLS=http://127.0.0.1:5000`. `NationalDrugCatalog__Mode=sandbox`. `NationalDrugCatalog__EnableStockOutSync=false`. Không có `Auth__DefaultTenantCode` |
| JSON đang chạy | `Mode=mock`. `EnableStockOutSync=false`. `DefaultTenantCode=NT_XUANHOA`. Connection string rỗng. JWT secret rỗng. Env `sandbox` ghi đè `mock` |
| `api.env` mtime | `2026-10-15 13:21:56 +07`, trước backup |
| Đĩa `/` | 52.4G trống |
| Nginx | `nginx -t` thành công |

`index.html` đang phục vụ lúc 22:24 +07:

| App | SHA-256 | So với staging |
|---|---|---|
| admin | `d239f9e9cceb462bf96eb7350f59d46462665e54c4605b2f529e4a03d8f5d3d9` | Khác. Staging `5f095fc326f88102b38576b48c3b83d240e211df39196a6cc0f2218d04fad834` |
| customer-app | `749bcd01906782911225330155959f81b981102406c91afb6de238b1c811eed6` | Khớp |
| staff-app | `8d626a0ce41e3346a36759cf1b0674f34cba590ef15e525844a4e4fc546aad88` | Khớp |
| prescriber-portal | `9ad5709161ab9abb17719b7f18660d183da38ada5c92c76d33bf20e1a23fe0b0` | Khớp |
| family-app | `5aa9b87113188ecbe4596645fd417a578810d469642fbe34598ed5bb82cc79e8` | Khác. Staging `3b10c59379ea2d34f0ccc4b85369b44093aa99b45caf88582bf184e116e7aa40` |

## 4. Gate trước cutover

| Điều kiện | Kết quả |
|---|---|
| Patch và kiểm thử script | **PASS**. Mục 2. Script chưa được thực thi lên VPS |
| Xuân Hòa không còn ca mở | **FAIL**. `NT_XUANHOA` / `SH-000151` / Kho Thương mại / `admin` / mở `2026-10-16 22:11:41 +07` / status 1. Đã có 2 đơn trên ca này. Không đóng |
| Ca demo | Giữ nguyên. `DEMO_PHARMACY` / `AUD-SHIFT-TODAY`, mở `2026-07-27 07:00:00 +07`, status 1. Không đóng, không sửa |
| Backup còn và dùng được | **PASS**. Mục 5 |
| Release / staging checksum | **PASS**. 878/878, 0 thiếu, 0 lệch, 0 file thừa trong cây upload |
| DLL staging | 1,808,896 byte, SHA-256 `EC974ECCDD0E6CD4D83C5E3480F6757650D8A040C2FA80A7F747E9B403B73319` |
| Database / pending | **PASS**. `current_database=novixa_prod`. Ledger 232. Pending so với manifest staging 221 tên = 0 |
| Ledger thừa | Đúng 11 tên cũ: `230`, `231`, `281`–`288`, `290` |
| File cấm trong ledger | **PASS**. 0 dòng `069*`, `383*`, `384*`, `387*`, `388*`, `371_pack_content_brand_narrative_dna.sql` |
| Catalog / stock-out | **PASS**. Runtime `sandbox`, `EnableStockOutSync=false` |
| Deploy | **NOT RUN** |
| Smoke test | **NOT RUN** |

Ca Xuân Hòa đóng trong sổ vẫn là 150. Ca mở mới xuất hiện sau lần đọc 22:02–22:07. Đây là ca bán, không phải ca seed.

Một lệnh đối soát ledger bị vỡ ngoặc kép trên máy operator, `psql` không kết nối được và không chạy câu SQL. Lần đọc sau dùng đúng user postgres, chỉ `SELECT`.

## 5. Backup và rollback

Backup tạo 2026-10-16 22:05:33 +07. Đọc lại sau 22:24 +07, thư mục vẫn còn.

Server: `/var/backups/novixa-deploy-009/20261016T220533+0700`  
Máy operator: `C:\novixa-rc-008\backups\20261016T220533+0700`

Chín file payload trên server có SHA-256 trùng `SHA256SUMS.payload.txt` trên máy operator. Hash local của chín file đó cũng trùng. `gzip -t` của dump trên server thành công. Đầu dump là header `PostgreSQL database dump`, pg_dump 16.14. Trong dump có `CREATE TABLE public.tenants`. Không có `FATAL` ở header.

| Archive | Entry đọc được | Kiểm tra thêm |
|---|---|---|
| `files/kit-platform-web-affected.tgz` | 983 | `api/KitPlatform.Api.dll` rút ra có SHA-256 `26afdaa7f123fa36496f31652a01a6d87942e975c4baf72b0b49bfff977a8941`, trùng DLL đang chạy. `staff-app/index.html` trùng file đang phục vụ |
| `files/pharmacore-web.tgz` | 956 | Đọc được danh sách entry |
| `files/opt-kit-platform.tgz` | 566 | Đọc được danh sách entry |
| `config/nginx-sites-and-snippets.tgz` | 18 | Đọc được danh sách entry |
| `config/systemd-units.tgz` | 1 | Đọc được danh sách entry |
| `config/systemd-sms-stub.tgz` | 1 | Đọc được danh sách entry |
| `config/kit-platform-etc.tgz` | 169 | Đọc được danh sách entry. Không in nội dung |
| `config/pharmacore-etc.tgz` | 129 | Đọc được danh sách entry. Không in nội dung |

Đường rollback file, chỉ dùng nếu một lần cắt sau không chạy migration:

1. `systemctl stop kit-platform-api`
2. Giải `kit-platform-web-affected.tgz` trở lại `/var/www/kit-platform` cho `api`, `admin`, `customer-app`, `staff-app`, `prescriber-portal`, `family-app`
3. Giải `pharmacore-web.tgz` trở lại `/var/www` nếu mirror pharmacore đã chạy
4. Giải `opt-kit-platform.tgz` trở lại `/opt` nếu cần hoàn tác script
5. Giải các archive trong `config/` trở lại `/etc/nginx`, `/etc/systemd/system`, `/etc/kit-platform`, `/etc/pharmacore`
6. `nginx -t`, `systemctl daemon-reload`, `systemctl start kit-platform-api`
7. Không restore database từ dump này để hoàn tác một lần cắt không chạy migration

Chưa diễn tập restore database và chưa diễn tập `systemctl`. Không cần rollback trong phiên này vì cutover không chạy.

## 6. Deploy và smoke

| Bước | Kết quả |
|---|---|
| `deploy-update-vps.ps1` | **NOT RUN** |
| `-RunMigrations` | Không truyền. Migration **không chạy** |
| Thời điểm cắt | Không có |
| API / health / SPA / login sau cắt | **NOT RUN** |
| POS sau restart | **NOT RUN**. Không tạo giao dịch thử |

Health ở mục 3 là của process đang chạy từ 2026-09-29.

## 7. Việc còn lại

Deploy chỉ được chạy khi đọc lại ngay trước lệnh và `NT_XUANHOA` không còn ca status 1. Ca `DEMO_PHARMACY` / `AUD-SHIFT-TODAY` không phải điều kiện chặn và không được đóng để lấy cửa sổ.

Khi điều kiện đó đạt, chạy đúng một lần:

```powershell
& "E:\KitPlatform-wt-phase5-novixa-product-proof\scripts\deploy-update-vps.ps1" `
  -Root "C:\novixa-rc-008\staging-root"
```

Không truyền `-RunMigrations`. Không đổi `-Root`. Không đổi commit `66afd2d96512eaf8c0a6f533f2a571616b2781f6`.
