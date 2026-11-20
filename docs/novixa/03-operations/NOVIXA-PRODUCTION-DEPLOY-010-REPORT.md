# NOVIXA-PRODUCTION-DEPLOY-010

**Ngày:** 2026-10-16  
**Trạng thái cuối:** `DEPLOYED`  
**Nghiệm thu sau cắt:** `docs/novixa/03-operations/NOVIXA-PRODUCTION-ACCEPTANCE-011-REPORT.md`  
**Release:** `66afd2d96512eaf8c0a6f533f2a571616b2781f6`  
**Staging:** `C:\novixa-rc-008\staging-root`  
**Script:** `E:\KitPlatform-wt-phase5-novixa-product-proof\scripts\deploy-update-vps.ps1`  
Không truyền `-RunMigrations`. Không chạy migration. Không đóng ca. Không sửa 231 đơn vị hết hạn.

## 1. Kết luận

Production đang chạy bản release. `kit-platform-api` khởi động lại lúc 22:41:57 +07, DLL trên đĩa và trên mirror pharmacore có SHA-256 `EC974ECCDD0E6CD4D83C5E3480F6757650D8A040C2FA80A7F747E9B403B73319`. Năm `index.html` phục vụ công khai khớp từng byte với staging. Health, health/db và `smoke-test.sh` đạt. Catalog runtime vẫn `sandbox`, stock-out sync vẫn tắt. Ledger vẫn 232. Ca demo `DEMO_PHARMACY` / `AUD-SHIFT-TODAY` vẫn mở, không bị đóng và không bị sửa.

Đăng nhập `NT_XUANHOA` / `admin` bằng mật khẩu trong file vận hành trả HTTP 401. User `admin` vẫn `status = 1`, hash bcrypt còn nguyên. Đây là mật khẩu không khớp, không phải service chết. Không đổi mật khẩu, không tạo đơn bán, không rollback.

## 2. Gate trước khi chạy script

Đọc lúc 22:38:16 +07, rồi đọc lại ngay trước script lúc 22:39:51 +07. Chỉ `SELECT`.

| Điều kiện | Kết quả |
|---|---|
| HEAD worktree | `66afd2d96512eaf8c0a6f533f2a571616b2781f6`. File tracked sửa duy nhất vẫn là `scripts/deploy-update-vps.ps1`. Worktree còn file untracked `migrations/371_pack_content_brand_narrative_dna.sql`; file này không nằm trong staging |
| Patch | Còn `Invoke-CheckedNative`, hash DLL bắt buộc `EC974ECCDD0E6CD4D83C5E3480F6757650D8A040C2FA80A7F747E9B403B73319`, và `family-app\index.html` là artifact bắt buộc trước upload |
| Staging | 878/878, thiếu 0, lệch 0, thừa 0. DLL 1,808,896 byte, đúng hash trên. Không có `appsettings.json`, `appsettings.Development.json`, `api.env` |
| Xuân Hòa ca mở | **PASS**. `XH_OPEN_COUNT=0`. Ca đóng: 151. Lần đọc 22:24 còn `SH-000151` mở; lần này ca đó đã đóng |
| Ca demo | `DEMO_PHARMACY` / `AUD-SHIFT-TODAY`, status 1, mở `2026-07-27 07:00:00 +07`. Không đóng, không sửa, không dùng làm điều kiện chặn |
| Backup | **PASS**. Thư mục `/var/backups/novixa-deploy-009/20261016T220533+0700` còn. Chín file payload trùng `SHA256SUMS.payload.txt` trên máy operator. `gzip -t` đạt. Header dump là PostgreSQL 16.14 |
| Pending migration | **PASS**. Ledger 232, manifest staging 221, pending 0, file cấm 0 |
| Hết hạn | 3 lô, `231.000` đơn vị. Không sửa |
| Catalog | Process và `api.env`: `NationalDrugCatalog__Mode=sandbox`, `EnableStockOutSync=false`. JSON vẫn `Mode=mock`; env ghi đè |
| DLL đang chạy trước cắt | Vẫn bản cũ `26afdaa7f123fa36496f31652a01a6d87942e975c4baf72b0b49bfff977a8941`, service active từ 2026-09-29 |

## 3. Deploy

Chạy đúng một lần, 22:39:51 +07:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "E:\KitPlatform-wt-phase5-novixa-product-proof\scripts\deploy-update-vps.ps1" -Root "C:\novixa-rc-008\staging-root"
```

`-ExecutionPolicy Bypass` chỉ áp cho process đó. Không gọi `Set-ExecutionPolicy`. Không truyền `-RunMigrations`.

Remote script in `uploaded_dll_sha256=EC974ECCDD0E6CD4D83C5E3480F6757650D8A040C2FA80A7F747E9B403B73319` trước rsync, rồi `Skip migrations (schema already live)`. Service mới lên lúc 22:41:57 +07.

Process PowerShell bọc ngoài thoát mã 1 vì `nginx -t` ghi `nginx: the configuration file /etc/nginx/nginx.conf syntax is ok` ra stderr, và `$ErrorActionPreference = Stop` của process cha coi đó là lỗi. Lúc đó remote đã restart xong. Không chạy lại script. Không restart thêm.

## 4. Sau cắt

Đọc lúc 22:42:54 và 22:44:48 +07.

| Hạng mục | Giá trị |
|---|---|
| `kit-platform-api` | `active`. `ActiveEnterTimestamp=Fri 2026-10-16 22:41:57 +07`. PID `3676308`. `Result=success`. Journal lỗi từ 22:41:50: không có dòng |
| Health local và `https://api.novixa.vn/api/health` | `{"status":"ok"}` |
| Health DB local và public | `database: true` |
| DLL | 1,808,896 byte, SHA-256 `ec974eccdd0e6cd4d83c5e3480f6757650d8a040c2fa80a7f747e9b403b73319`. Mirror `/var/www/pharmacore/api` cùng hash |
| Runtime file | `KitPlatform.Api.runtimeconfig.json` yêu cầu `Microsoft.NETCore.App` 10.0.0 và `Microsoft.AspNetCore.App` 10.0.0. Process đã listen và health đạt |
| Catalog | Env process và `api.env`: `sandbox`, `EnableStockOutSync=false`. `api.env` mtime đổi thành `2026-10-16 22:41:56 +07` vì script CORS của release chạy. Hai khóa catalog không đổi |
| JSON | `Mode=mock`, `EnableStockOutSync=false`, `DefaultTenantCode=NT_XUANHOA` |
| Nginx | `nginx -t` đạt sau cắt |
| Ledger | 232. File cấm 0 |
| Hết hạn | Vẫn 3 lô, `231.000` |
| Xuân Hòa ca mở | Vẫn 0. Ca đóng vẫn 151 |
| Ca demo | Vẫn status 1, cùng mốc mở |

`index.html` công khai, hash byte thô, khớp staging:

| App | SHA-256 | Byte |
|---|---|---|
| admin | `5f095fc326f88102b38576b48c3b83d240e211df39196a6cc0f2218d04fad834` | 1,058 |
| customer-app | `749bcd01906782911225330155959f81b981102406c91afb6de238b1c811eed6` | 1,515 |
| staff-app | `8d626a0ce41e3346a36759cf1b0674f34cba590ef15e525844a4e4fc546aad88` | 1,099 |
| prescriber-portal | `9ad5709161ab9abb17719b7f18660d183da38ada5c92c76d33bf20e1a23fe0b0` | 777 |
| family-app | `3b10c59379ea2d34f0ccc4b85369b44093aa99b45caf88582bf184e116e7aa40` | 1,185 |

`smoke-test.sh` trên host, 22:44 +07: setup-status, năm trang SPA, manifest POS, health/db, CORS của sáu origin, bảy route API không token trả 401, manifest 279/280. Kết thúc `All checks passed`.

Đăng nhập và phân quyền:

| Kiểm tra | Kết quả |
|---|---|
| `POST /api/auth/login` với `NT_XUANHOA` / `admin` và mật khẩu file vận hành | HTTP 401, body chỉ có `message` |
| Cùng user, bỏ trống tenant | HTTP 401 |
| User `admin` trên DB | `status=1`, chưa xóa, hash bcrypt 60 ký tự. Không đổi hash, không đổi mật khẩu |
| Quyền gắn với user này | Có `reports.read`, `reports.export`, `sales.pos`, `sales.read` |
| Role `CASHIER` | 1 user đang hoạt động. Role không có `reports.read` |
| `GET /api/reports/catalog` không token | 401 |
| `GET /api/sales/pos/lookup` không token | 401 |
| Tạo đơn bán | Không gọi |

Phiên đăng nhập thành công và lệnh cashier gọi báo cáo chưa được chứng minh, vì mật khẩu vận hành không khớp hash đang lưu. Không rollback chỉ vì điểm này: binary mới đã phục vụ, smoke script đạt, và dữ liệu user không bị migration đụng tới.
