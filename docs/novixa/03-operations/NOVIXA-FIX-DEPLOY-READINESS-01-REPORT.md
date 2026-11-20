# NOVIXA-FIX-DEPLOY-READINESS-01

Phương án triển khai commit `0d8568ae6e57003ae0f5e2813f1a35fd000ba6be` sau khi A/B/D đã được xác minh trên local. Lượt này chỉ đọc git và hồ sơ vận hành. Không push, không deploy, không restart, không migration, không seed, không SQL ghi, không sửa source, không đụng index, không dọn working tree. Không đọc và không ghi tenant `NT_XUANHOA`. Không gọi API production.

Ngày lập: 2026-10-13.

## Kết luận

**BLOCKED**

Chưa đủ điều kiện để Architect phê duyệt triển khai. Ba điều kiện dừng trong nhiệm vụ đang đúng ở máy này: working tree bẩn, artifact chưa truy nguyên được về commit mục tiêu, và baseline production đã ghi không phải commit cha của patch. Dừng tại báo cáo này.

## Baseline production và commit mục tiêu

### Commit mục tiêu

| Mục | Giá trị |
|---|---|
| SHA | `0d8568ae6e57003ae0f5e2813f1a35fd000ba6be` |
| Parent | `b64f6772213dd0e095a49ae9a84983b37116f696` |
| Thời điểm | 2026-10-13 12:43:03 +0700 |
| Subject | `fix(novixa): page low-stock without dropping tied rows` |
| `git rev-parse HEAD` | Khớp SHA trên. Nhánh `main`. |

Xác minh local A/B/D của commit này nằm ở `NOVIXA-FIX-POSTCOMMIT-VERIFY-01-REPORT.md`. Báo cáo đó không phải biên bản deploy.

### Tổ tiên chưa có trên `origin/main`

Ref local `origin/main` là `73d3c4a6c509ab25229d07a475229a6c8c1cf79d`, subject `Merge remote-tracking branch 'origin/main'`, 2026-09-29 16:14:34 +0700. Reflog local ghi cập nhật ref này lúc 2026-09-29 16:41:48 +0700 bằng push. Lượt này không `git fetch`. Remote có thể đã đổi sau mốc đó; chưa đo.

`main` đang ahead `origin/main` 5 commit, lịch sử thẳng:

| SHA | Thời điểm | Subject |
|---|---|---|
| `c447814d4c3056bb5affe3670cf8b858671686c5` | 2026-10-04 17:21:51 +0700 | `refactor(core): move pharmacy workflow out of core` |
| `98f0632cab983750edb032eb365c91b15db5eb44` | 2026-10-04 20:35:34 +0700 | `feat(content): consolidate editorial workflow from kit-mkt` |
| `d54652d4a7734b3116db62bd356a831f9942fc73` | 2026-10-05 20:12:15 +0700 | `docs(architecture): establish canonical architecture authority V1` |
| `b64f6772213dd0e095a49ae9a84983b37116f696` | 2026-10-12 14:09:38 +0700 | `docs(architecture): retract unproven tenant claims and bound D-05` |
| `0d8568ae6e57003ae0f5e2813f1a35fd000ba6be` | 2026-10-13 12:43:03 +0700 | patch A/B/D |

Năm commit này chưa được push trong hồ sơ local. Runbook `pharmacy-rbac-deploy-sync-runbook-v1.md` và `pharmacy-pos-customer-incidents-v1.md` yêu cầu `HEAD == origin/main` rồi mới build và deploy. Điều kiện đó chưa đạt. Nhiệm vụ này cấm push.

### Baseline đã ghi trong hồ sơ vận hành

Không có hồ sơ nào ghi production API/Admin hiện đang chạy `b64f677` hay `0d8568ae`.

| Mốc đã ghi | Ý nghĩa | Có phải baseline để triển khai patch này |
|---|---|---|
| `ad9231df42f842bfd38b242cb3a8954b1f5092c4` | Lần đối hash bundle admin gần nhất, 2026-07-24. `pharmacy-pos-customer-incidents-v1.md` §5: `PosPage-D0dEe6fS.js` và `index-DWyL7DqL.js` khớp tip `ad9231d`. | Không. Đây là tổ tiên của HEAD (1013 commit tới `origin/main`, 2815 file và 224 file trong `migrations/` tới HEAD). Smoke 13/10 không đo SHA trên VPS. |
| `73d3c4a6c509ab25229d07a475229a6c8c1cf79d` | Tip `origin/main` trên ref local, 2026-09-29. Runbook gọi `origin/main` là nguồn chuẩn, VPS chỉ giữ artifact. | Chưa chứng minh VPS đang ở SHA này. Không fetch, không đọc VPS. |
| `b64f6772213dd0e095a49ae9a84983b37116f696` | Parent của patch. Hồ sơ kiến trúc dùng SHA này làm mốc bằng chứng mã, không phải biên bản deploy. | Chưa chứng minh VPS đang ở SHA này. |

`NOVIXA-PROD-SMOKE-NT_XUANHOA-01` (2026-10-13, 10:11–10:25 +07) đo hành vi trước patch: Tổng quan lệch 7 giờ, cận hạn gồm lô đã hết hạn, low-stock trả 500 dòng. Hành vi đó có trong mọi tổ tiên của `0d8568ae`. Smoke không gắn SHA.

## Phạm vi chênh lệch

### Patch so với parent — đây là phạm vi A/B/D đã xác minh

`git diff --shortstat b64f677 0d8568ae`: 21 file, 1272 dòng thêm, 67 dòng xóa. Không có file trong `migrations/` và không có `deploy/ubuntu/migration-files.prod.txt`.

| File | Việc |
|---|---|
| `src/KitPlatform.Infrastructure/Dashboard/VietnamBusinessCalendar.cs` | A. Mốc `Kind=Utc`. |
| `src/KitPlatform.Infrastructure/Reports/ReportsDateHelper.cs` | A. Mốc mặc định `Kind=Utc`. |
| `src/KitPlatform.Infrastructure/Dashboard/DashboardRepository.cs` | B. Cận dưới HSD. |
| `src/KitPlatform.Infrastructure/Reports/ReportsRepository.cs` | B. Cận dưới HSD. |
| `src/KitPlatform.Infrastructure/Reports/ReportsService.cs` | B. Truyền ngày VN hôm nay. |
| `src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Infrastructure/Inventory/InventoryRepository.cs` | D. Đếm, phân trang, tie-breaker. |
| `src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Infrastructure/Inventory/InventoryService.cs` | D. Kẹp trang. |
| `src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Application/Inventory/IInventoryService.cs` | D. Chữ ký phân trang. |
| `src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Application/Inventory/InventoryDtos.cs` | D. `PagedLowStockProductsResult`. |
| `src/KitPlatform.Api/Controllers/Pharmacy/StockController.cs` | D. `page`, `pageSize`, body object. |
| `client/admin/src/shared/api/inventory.api.ts` | D. Đọc object phân trang. |
| `client/admin/src/shared/api/inventory.types.ts` | D. `PagedLowStockProducts`. |
| `client/admin/src/shared/api/generated/api-schema.ts` | D. Contract low-stock. |
| `client/admin/src/modules/inventory/LowStockPage.tsx` | D. Phạm vi kho, `total`, đề xuất theo trang. |
| `client/admin/src/shared/i18n/locales/vi-VN/inventory.json` | D. Nhãn. |
| `client/admin/src/shared/i18n/locales/en-US/inventory.json` | D. Nhãn. |
| `client/admin/openapi/swagger.json` | D. Nguồn schema. Không phải artifact chạy. |
| `scripts/export-openapi.ps1` | Chặn sync ghi lại contract mảng. Không phải artifact chạy. |
| `tests/KitPlatform.Application.Tests/NovixaFixImplement01Tests.cs` | Test. Không deploy. |
| `tests/KitPlatform.Application.Tests/KitPlatform.Application.Tests.csproj` | Test. Không deploy. |
| `docs/novixa/03-operations/NOVIXA-FIX-PACKAGE-01-REPORT.md` | Tài liệu. Không deploy. |

`GET /api/inventory/stock/low-stock` đổi từ mảng cắt 500 dòng sang object `{ items, total, page, pageSize }`. Admin cũ gọi `.map` trên mảng sẽ lỗi khi API mới đã chạy. Admin mới đọc `items`; nếu body vẫn là mảng thì `normalizePaged` ra danh sách rỗng. A và B chỉ nằm ở API. Vì D nằm cùng commit, API và Admin phải lên cùng một cửa sổ. Không tách API trước hoặc Admin trước rồi để người dùng ở giữa hai bản.

### Nếu mốc so sánh là `origin/main`

`73d3c4a..0d8568ae`: 64 file, 9918 dòng thêm, 1495 dòng xóa. Không có file `migrations/` trong khoảng này. Ngoài patch, còn:

- `c447814d` đụng `SalesController.cs`, `SalesRepository.cs`, `SalesService.cs`, `PurchaseOrderService.cs`, chuyển workflow engine sang pack Pharmacy.
- `98f0632c` đụng `ContentController.cs`, repository/service Content, và nhiều trang admin Content.
- `d54652d4` và `b64f677` chỉ tài liệu và `.gitignore`.

Khoảng này chưa được xác minh như một gói deploy Pharmacy. Local A/B/D không chứng minh hai commit code phía trước.

### Nếu mốc so sánh là hash admin đã đối trên VPS

`ad9231d..HEAD` là 2815 file, gồm 224 file migration và cả bốn manifest `migration-files.*.txt`. Triển khai HEAD lên một VPS còn ở `ad9231d` không phải triển khai patch A/B/D. Không được dùng `-RunMigrations` để “đuổi” khoảng đó trong nhiệm vụ này.

## Artifact

Build chuẩn `scripts/deploy-production.ps1` xóa `publish/`, publish API, rồi `npm run build` cho admin, customer-app, staff-app, prescriber-portal, partner-portal, assessment-web và family-app. Script đó biên dịch working tree, không checkout `0d8568ae`. `scripts/deploy-update-vps.ps1` sau đó đưa lên VPS cả API, admin, customer-app, staff-app, prescriber-portal, family-app nếu có, cả thư mục `migrations/`, và script trong `deploy/ubuntu/`, rồi restart `kit-platform-api`. Mặc định script bỏ qua migration, nhưng vẫn copy cây migration. Không dùng hai script này nguyên trạng cho patch này.

Project API không ghi `SourceRevisionId`. DLL không tự mang SHA. `publish/` nếu đang có trên đĩa không phải bằng chứng của commit mục tiêu.

Artifact được phép, khi một lượt sau được phê duyệt và build từ checkout sạch của đúng SHA:

| Thành phần | Lệnh trên checkout sạch | Đích |
|---|---|---|
| API | `dotnet publish src/KitPlatform.Api/KitPlatform.Api.csproj -c Release -o <thư mục tách>/api --no-self-contained` | Cây publish, gồm `KitPlatform.Api.dll` và assembly phụ thuộc mà publish kéo theo (`KitPlatform.Infrastructure`, pack Pharmacy). |
| Admin | Trong `client/admin`, `VITE_API_BASE_URL=https://api.novixa.vn`, rồi `npm run build` (`tsc --noEmit && vite build`) | Nội dung `client/admin/dist`, tức `index.html` và `assets/*` đã hash. |

Không đưa lên: test, docs, `scripts/export-openapi.ps1`, các SPA còn lại, thư mục `migrations/`, manifest, `backup-db.sh` bản dirty.

`npm run build` admin chưa PASS cho commit này. `NOVIXA-FIX-POSTCOMMIT-VERIFY-01` chạy `tsc --noEmit` trên working tree: exit 2, 382 lỗi, 51 file, không lỗi trong file admin của patch. `ContentFamixaSeriesTab.tsx` đang lệch so với HEAD. 382 lỗi đó chưa được đo trên checkout sạch của `0d8568ae`. Gate deploy admin trong runbook là `npm run build`, không phải `vite build` đơn lẻ. Không sửa 382 lỗi TypeScript trong phạm vi này. Nếu checkout sạch vẫn fail `tsc`, dừng; không bỏ qua `tsc`.

Working tree lúc lập báo cáo: `main...origin/main [ahead 5]`, 471 dòng porcelain. Trong đó 98 đường dẫn dưới `src`, 171 dưới `client/admin`, 18 dưới `migrations`, 6 dưới `deploy`. Mười tám file migration dirty gồm sửa `migrations/281_pack_content_wave0.sql` và các file chưa track từ `368` đến `386`. `deploy/ubuntu/backup-db.sh` và `migration-files.content.txt` đang sửa; thêm script backup key và hai manifest chưa track. Các file runtime của patch khớp HEAD, trừ `KitPlatform.Application.Tests.csproj` còn unstaged. File test đó không vào DLL API hay bundle admin. Các file dirty khác trong `src` và `client/admin` sẽ vào artifact nếu build tại chỗ.

## Migration

Patch `b64f677..0d8568ae` không có migration. Khoảng `origin/main..HEAD` cũng không có file migration. Không chạy migration, không gọi `deploy-update-vps.ps1 -RunMigrations`, không chạy `run-migrations-prod.sh` hay `run-incremental-migrations-prod.sh`.

Nếu baseline sống không phải parent và không phải `73d3c4a`, câu “không cần migration” không còn đúng cho toàn bộ cây HEAD. Đó là lý do phải khóa SHA đang chạy trước khi build.

## Trình tự khi được phê duyệt riêng

Chưa thực hiện. Chỉ viết để Architect thấy cửa sổ và điểm dừng.

1. Dừng nếu working tree dùng để build không sạch, hoặc SHA checkout khác `0d8568ae6e57003ae0f5e2813f1a35fd000ba6be`. Không dọn cây hiện tại để lấy chỗ build. Dùng một checkout khác của đúng commit.
2. Dừng nếu chưa đọc lại baseline production bằng cách không đăng nhập `NT_XUANHOA`. Cách đọc: hash asset admin đang phục vụ so với bản build đã biết, hoặc dấu vết SHA nếu lúc đó đã có. Nếu SHA sống khác parent `b64f6772213dd0e095a49ae9a84983b37116f696`, dừng và lập lại phạm vi. Smoke hành vi sáng 13/10 không đủ để gọi VPS là parent.
3. Dừng nếu `npm run build` admin trên checkout sạch không exit 0. Dừng nếu `dotnet publish` API không exit 0.
4. Ghi SHA, hash `KitPlatform.Api.dll`, và hash `index.html` cùng các file `assets` mà `index.html` trỏ tới. Artifact không có các dấu này thì không đưa lên.
5. Trên VPS, copy nguyên cây đang chạy ra thư mục có dấu thời gian, trước khi ghi đè:
   - `/var/www/kit-platform/api`
   - `/var/www/kit-platform/admin`
   Giữ bản copy đến khi smoke sau deploy xong. `deploy-update-vps.ps1` không tự làm bước này. Admin trên script dùng `rsync --delete`. API dùng `rsync` không `--delete`, nên file cũ có thể còn nếu không thay cả thư mục từ bản snapshot.
6. Backup DB bằng script backup đã có trên VPS, không bằng bản dirty của `backup-db.sh` trong working tree. `pg_dump` của `novixa_prod` là phòng khi cần đối chiếu. Patch không đổi schema nên rollback không restore dump. Không chạy dump trong lượt này.
7. Health trước, chỉ hai URL public, không tenant:
   - `GET https://api.novixa.vn/api/health` kỳ vọng `{ "status": "ok" }`
   - `GET https://api.novixa.vn/api/health/db` kỳ vọng `{ "status": "ok", "database": true, "value": 1 }`
   `HealthController` chỉ `SELECT 1`. Nếu một trong hai không phải 200, không ghi đè artifact.
8. Đưa API và Admin lên trong cùng một cửa sổ. Không để admin mới được nginx phục vụ trong khi process API cũ còn sống, và không restart API mới trong khi admin cũ còn được phục vụ. Cách làm: stage cả hai cây, thay thư mục API, `systemctl restart kit-platform-api`, thay thư mục admin ngay sau khi process mới active. Không reload nginx để đổi site, không sync SPA khác, không copy `migrations/`.
9. Health lại hai URL trên. `systemctl is-active kit-platform-api` phải là `active`.
10. So hash admin live với hash đã ghi ở bước 4. Lệch thì rollback, không sửa tay trên VPS.
11. Rollback: dừng, copy trả hai thư mục đã snapshot, restart `kit-platform-api`, health lại. Không restore database. Không chạy migration xuống.

## Smoke production chỉ đọc

Không đăng nhập `NT_XUANHOA`. Không mở Tổng quan, đơn, báo cáo, tồn, khách, low-stock có phiên, và không gọi endpoint nghiệp vụ bằng token tenant đó. Hành vi A/B/D đã khóa trên local `DEMO_PHARMACY` và fixture `FIXIMPL01_` trong báo cáo post-commit. Production không lặp các số đó trong phương án này.

Được phép sau khi artifact đã lên, vẫn không đụng nghiệp vụ tenant:

| Kiểm tra | Kỳ vọng |
|---|---|
| `GET /api/health` | 200, `status=ok` |
| `GET /api/health/db` | 200, `status=ok`, `database=true` |
| `GET https://admin.novixa.vn/` | HTML, tên asset trong `index.html` khớp bản build của `0d8568ae` |
| `GET /api/inventory/stock/low-stock` không kèm phiên | 401. Không đọc body nghiệp vụ. |

`deploy/ubuntu/smoke-test.sh` không phải smoke của patch này. Script đó đọc `setup-status`, CORS, và nhiều route bán hàng, mua hàng, tồn. Không chạy script đó trong cửa sổ này.

Đối chiếu số đơn, cận hạn và tồn thấp trên `NT_XUANHOA` cần một phê duyệt đọc riêng. Không nằm trong phương án này.

## Rủi ro

- Working tree bẩn làm `dotnet publish` và `npm run build` tại chỗ kéo code ngoài commit, gồm Content và migration chưa track.
- `deploy-update-vps.ps1` thay nhiều SPA và copy cả `migrations/` dù `-RunMigrations` tắt. Admin bị `rsync --delete` trước khi API restart.
- Contract low-stock không tương thích hai chiều. Cửa sổ lệch bản làm màn tồn thấp rỗng hoặc lỗi.
- A đổi `Kind` của `VietnamBusinessCalendar`. Cockpit, Loss Prevention, Growth Desk và Shift Checklist dùng cùng helper. Local đã đối chiếu caller; chưa đo các màn đó trên production, và phương án này không đo chúng trên `NT_XUANHOA`.
- B làm thẻ và báo cáo 30 ngày bỏ lô đã hết hạn. Lọc `expired` không đổi. Cockpit vẫn đếm không cận dưới; patch không sửa Cockpit.
- Baseline `ad9231d` cách HEAD 224 file migration. Nhầm baseline rồi deploy HEAD là đổi schema ngoài ý muốn nếu có người bật migration.
- Backup DB không chứa key giải mã credential. Rollback patch này không cần restore DB.

## Điều kiện dừng

Dừng, không build đưa lên và không restart, nếu một điều sau đúng:

- Working tree hoặc checkout dùng để publish còn file ngoài `0d8568ae`.
- `git rev-parse HEAD` của checkout build không phải `0d8568ae6e57003ae0f5e2813f1a35fd000ba6be`.
- Không ghi được hash DLL và hash asset admin gắn với SHA đó trước khi upload.
- Baseline production đọc lại khác `b64f6772213dd0e095a49ae9a84983b37116f696`, hoặc không đọc được baseline mà không đụng `NT_XUANHOA`.
- `origin/main` sau một lần fetch sau này không còn là `73d3c4a6c509ab25229d07a475229a6c8c1cf79d` và Architect chưa chấp nhận mốc mới. Lượt này chưa fetch.
- `npm run build` admin hoặc `dotnet publish` API khác exit 0.
- Health trước deploy không phải 200.
- Có yêu cầu chạy migration, seed, hoặc SQL ghi.
- Phạm vi upload gồm SPA khác, thư mục `migrations/`, hoặc script deploy dirty.

## Chưa xác minh

- SHA đang chạy trên VPS. Không SSH, không tải `index.html` production trong lượt này.
- Remote `origin/main` sau 2026-09-29 16:41 +07. Không fetch.
- `dotnet publish` và `npm run build` từ checkout sạch của `0d8568ae`. Build API đã ghi trong các báo cáo trước biên dịch working tree.
- `tsc` trên đúng cây của commit. 382 lỗi đã đo trên working tree.
- Schema `novixa_prod` so với `73d3c4a` hoặc `ad9231d`.
- Hành vi A/B/D trên production sau deploy. Cố ý không kiểm trên `NT_XUANHOA`.

## Việc đã không làm

- Không push, deploy, restart, không ghi production.
- Không đăng nhập và không gọi nghiệp vụ `NT_XUANHOA`.
- Không migration, seed, SQL ghi.
- Không sửa source, không commit, không `git add`, không dọn working tree.
- Không chạy `npm run build`, không sửa lỗi TypeScript sẵn có.
