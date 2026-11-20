# KIT-PLATFORM-NOVIXA-END-TO-END-RELEASE-008

**Ngày:** 2026-10-16, khoảng 21:28–21:45 +07  
**Trạng thái:** `READY_FOR_OWNER_DEPLOY_APPROVAL`  
**Không deploy.** Không push, không merge, không restart service, không ghi `novixa_prod`, không chạy migration trên Production.

Release candidate nằm tại `C:\novixa-rc-008`. Source phát hành là worktree `E:\KitPlatform-wt-phase5-novixa-product-proof`, không phải `E:\KitPlatform`.

## 1. Trạng thái cuối

Source đã khóa bằng một commit đúng 14 file đã duyệt. API và năm SPA được build từ commit đó. Các kiểm thử Pharmacy trên commit này đạt, trừ bảy bài bán trên database local `kitplatform` lệch giá seed và một bài đồng ý khách hàng không nối được webhook. Ledger nâng của bản sao Xuân Hòa vẫn pending = 0 khi chạy đúng script incremental của commit. Gói upload không chứa file cấm và không chứa secret của file cấu hình Development.

Việc còn lại là quyết định của chủ dự án và các phép đọc ngay trước cắt. Phiên này không nối được vào Production để đo lại các mốc của báo cáo 005.

## 2. Commit

| Mục | Giá trị |
|---|---|
| Worktree | `E:\KitPlatform-wt-phase5-novixa-product-proof` |
| Branch | `phase5-novixa-product-proof` |
| HEAD trước commit | `5c9f65103fd030e37f2788a9b99565a9f918a140` |
| Commit | `66afd2d96512eaf8c0a6f533f2a571616b2781f6` |
| Parent | `5c9f65103fd030e37f2788a9b99565a9f918a140` |
| Thời điểm | 2026-10-16 21:28:40 +07 |
| Subject | `fix(novixa): fail closed without a tenant and keep fresh schema apply compatible with the existing ledger` |

Subject đúng câu đã duyệt. Công cụ commit thêm trailer `Co-authored-by: Cursor <cursoragent@cursor.com>`. Không amend.

Trước khi stage, `git status` đúng 12 file tracked sửa và 3 mục untracked như báo cáo 005. SHA-256 của `migrations/060_family_members_status_align.sql` là `E84257CC824305F66EA228092717403EA83FEF20388B0DD11D82856861B70180`. Manifest không chứa `069_assessment_pharmacy_v1_seed.sql` và không chứa `371_pack_content_brand_narrative_dna.sql`.

Staged diff trước commit đúng 14 file, 357 dòng thêm, 33 dòng xóa. `371` không được stage.

File trong commit:

- `deploy/ubuntu/backup-db.sh`
- `deploy/ubuntu/greenfield-compat/060_family_members_status_align.sql`
- `deploy/ubuntu/run-incremental-migrations-prod.sh`
- `deploy/ubuntu/run-migrations-prod.sh`
- `src/KitPlatform.Api/Controllers/AuthController.cs`
- `src/KitPlatform.Application/Auth/AuthContracts.cs`
- `src/KitPlatform.Infrastructure/Auth/AuthService.cs`
- `src/KitPlatform.Infrastructure/CustomerApp/CustomerAppAuthService.cs`
- `tests/KitPlatform.Api.Tests/CatalogApiTests.cs`
- `tests/KitPlatform.Api.Tests/CustomerConsentApiTests.cs`
- `tests/KitPlatform.Api.Tests/DefaultTenantResolutionTests.cs`
- `tests/KitPlatform.Api.Tests/InventoryApiTests.cs`
- `tests/KitPlatform.Api.Tests/ProcurementApiTests.cs`
- `tests/KitPlatform.Api.Tests/SalesApiTests.cs`

Sau commit, `git status --short` chỉ còn:

```text
?? migrations/371_pack_content_brand_narrative_dna.sql
```

`migrations/060_family_members_status_align.sql` không nằm trong commit và hash không đổi. `migration-files.prod.txt` không nằm trong commit và vẫn không có `069`.

## 3. Artifact

Build từ HEAD `66afd2d96512eaf8c0a6f533f2a571616b2781f6`. SDK `10.0.301`. Host runtime trên máy build `10.0.9`. Artifact `net10.0`, framework `Microsoft.NETCore.App` và `Microsoft.AspNetCore.App` `10.0.0`. Node `v24.18.0`, npm `11.16.0`.

Log: `C:\novixa-rc-008\evidence\api-publish.log`, `dotnet-info.txt`, `spa-build.log`.

### API

Lệnh, exit 0, lúc 21:32:18 +07:

```powershell
dotnet publish src\KitPlatform.Api\KitPlatform.Api.csproj -c Release -o C:\novixa-rc-008\publish\api --no-self-contained
```

| File | Kết quả |
|---|---|
| `KitPlatform.Api.dll` | 1,808,896 byte, mtime 21:32:17 +07, SHA-256 `EC974ECCDD0E6CD4D83C5E3480F6757650D8A040C2FA80A7F747E9B403B73319` |
| `KitPlatform.Application.dll` | SHA-256 `90ED3BBC4BF5CDA6744C7CAC2BBF1DF9520BA72A28CDACFC0AC38F1D92FAD254`. Chuỗi UTF-16 `Mã nhà thuốc là bắt buộc khi chưa cấu hình tenant mặc định.` có trong file này |

DLL đang chạy trên Production theo báo cáo 005 có hash khác: `26afdaa7f123fa36496f31652a01a6d87942e975c4baf72b0b49bfff977a8941`, 1,786,368 byte. Phiên này không đo lại file trên máy chủ.

Publish có warning `NU1903` cho `Newtonsoft.Json` 10.0.3. Build không lỗi.

`appsettings.json` và `appsettings.Development.json` của output có connection string development và secret development. Chúng bị loại khỏi cây upload. `appsettings.Production.json` được giữ: connection string rỗng, JWT secret rỗng, `Auth:DefaultTenantCode = NT_XUANHOA`, `NationalDrugCatalog:EnableStockOutSync = false`, `NationalDrugCatalog:Mode = mock`. Mode runtime phải vẫn là biến môi trường `sandbox`. File này không thuộc 14 file commit và không bị sửa.

### Năm SPA

Mỗi app: `VITE_API_BASE_URL=https://api.novixa.vn`, rồi `npm run build` (`tsc --noEmit && vite build`), exit 0. Output copy từ `dist` sang `C:\novixa-rc-008\publish\<app>`.

| App | index.html | SHA-256 index.html | File JS chính | SHA-256 JS | `api.novixa.vn` trong JS |
|---|---|---|---|---|---|
| admin | 1,058 byte, 308 file | `5F095FC326F88102B38576B48C3B83D240E211DF39196A6CC0F2218D04FAD834` | `assets/index-BCy9L4_Y.js` 827,018 byte | `EF088CC9AD2DC4C43BDAD3861E4DEEECBD7BC6D2D681A8CA6DAB0B6188E3C001` | Có |
| customer-app | 1,515 byte, 115 file | `749BCD01906782911225330155959F81B981102406C91AFB6DE238B1C811EED6` | `assets/index-DeyxL0Px.js` 628,753 byte | `E1F051D1D73926BFAFAB089D224F48AEAEF2B420568FEDDF475428C3586BB34C` | Có |
| staff-app | 1,099 byte, 11 file | `8D626A0CE41E3346A36759CF1B0674F34CBA590EF15E525844A4E4FC546AAD88` | `assets/index-5OdKaG-L.js` 1,747,142 byte | `EFA419398556A4A26A8965F2475CEECB0D2D2487C2A8CDD2FD5939935ACA944C` | Có |
| prescriber-portal | 777 byte, 26 file | `9AD5709161AB9ABB17719B7F18660D183DA38ADA5C92C76D33BF20E1A23FE0B0` | `assets/prescriber-portal.api-CUST2fX1.js` 136,220 byte | `27BEA09E9EB832885582B96C32416405FD2392B6AE3691A24BC0EE52A395C3B4` | Có |
| family-app | 1,185 byte, 49 file | `3B10C59379EA2D34F0CCC4B85369B44093AA99B45CAF88582BF184E116E7AA40` | `assets/index-CrIkq-vx.js` 1,058,802 byte | `C431629B9DEE17E53B152226D4340A095398D57BE0D68D98D5E55D9A3B86B3BB` | Không |

family-app nhận biến môi trường lúc build nhưng client HTTP đang dùng `baseURL: '/api'` cùng origin. Chuỗi `localhost:5290` trong bundle family chỉ nằm ở nhánh hostname localhost của mirror agent. admin còn câu fallback `localhost:5290` khi biến môi trường rỗng; bản này đã gắn `https://api.novixa.vn`.

### Gói upload

`C:\novixa-rc-008\staging-root` có 878 file payload cộng `SHA256SUMS.txt`. Dòng DLL trong checksum trùng hash ở trên.

| Thành phần | Số file |
|---|---|
| `publish/api` | 117, đã bỏ hai file appsettings có secret |
| `publish/admin` | 308 |
| `publish/customer-app` | 115 |
| `publish/staff-app` | 11 |
| `publish/prescriber-portal` | 26 |
| `publish/family-app` | 49 |
| `migrations` | 221, đúng tên trong `migration-files.prod.txt` |
| `deploy/ubuntu` | 31, gồm script đã commit và `greenfield-compat` |

Đối soát tên file trên cả cây: không có `069_assessment_pharmacy_v1_seed.sql`, không có `371_pack_content_brand_narrative_dna.sql`, không có `api.env`, không có `appsettings.json`, không có `appsettings.Development.json`. JSON còn lại trong `publish/api` không có `Host=` và không có password khác rỗng.

`scripts/deploy-update-vps.ps1` upload cả thư mục `migrations` và `deploy\ubuntu`, rsync SPA với `--delete`, rsync API không `--delete`, rồi reload nginx và restart `kit-platform-api`. Vì vậy `-Root` phải là `C:\novixa-rc-008\staging-root`. Trỏ vào `E:\KitPlatform` hoặc worktree candidate sẽ kéo theo file untracked `371_pack_content_brand_narrative_dna.sql` và toàn bộ migration ngoài manifest, kể cả `069` đang có trên đĩa. Script deploy không được sửa trong phiên này và không được chạy.

## 4. Kiểm thử

Chạy sau commit, configuration Release, HEAD `66afd2d9`. Log `C:\novixa-rc-008\evidence\dotnet-test.log` và ba file `.trx`. API test ghi vào database local `kitplatform` cổng 5432, không ghi clone và không ghi Production.

| Nhóm | Kết quả | Bằng chứng |
|---|---|---|
| Application: `NovixaReleaseSafetyBehaviorTests`, `PharmacyConsultationOtcGateTests`, `CoreEngineTests`, `SalesAccrualSqlTests` | **PASS** 13/13 | exit 0 |
| Platform: lot rules, pack definition, module gate, sale/return handlers, pack sync | **PASS** 28/28 | exit 0 |
| `DefaultTenantResolutionTests` | **PASS** 2/2 | Login không tenant trả 400, không vào `NVX-CS08`. Login `DEMO_PHARMACY` đúng tenant |
| `SalesReadOnlyPrincipalTests` | **PASS** | Cashier được phân tích, không lưu và không đọc báo cáo |
| Catalog, inventory, procurement API | **PASS** | Gồm nhập hàng làm tăng tồn, lưu trữ và purge |
| `Sales_create_order_decreases_stock_via_fefo` | **PASS** | FEFO trừ tồn trên `kitplatform` |
| `Sales_pos_barcode_lookup_then_complete_sale` | **PASS** | |
| `Sales_shift_open_close_and_blocks_sale_without_shift` | **PASS** | |
| Bảy bài `SalesApiTests` còn lại | **FAIL** do fixture | Xem bảng dưới. Assertion không sửa |
| `Customer_consent_upsert_writes_outbox_event` | **BLOCKED_ENVIRONMENT** | `localhost:80` từ chối kết nối |
| Probe HTTP trên clone trong báo cáo 001 và 002 | **NOT_RUN** phiên này | Code bán và `SellableLotRules` không nằm trong commit. Clone vẫn ledger 232, 231 đơn vị hết hạn, 0 ca mở. Không tạo giao dịch mới trên clone |
| Test Content, Local OS, và các suite ngoài bộ lọc Pharmacy | **NOT_RUN** | Ngoài cổng quyết định lần cắt này |
| `run-migrations-prod.sh` trên database đã có ledger | **NOT_RUN** | Cấm chạy full runner trên ledger hiện hữu |
| Cài greenfield lại từ đầu | **NOT_RUN** | Không reset database lab |

Bảy bài bán fail trên `kitplatform` / `DEMO_PHARMACY`:

| Bài | Assertion | Thực tế |
|---|---|---|
| `Sales_barcode_lookup_returns_price` | giá `500` | `5000.00` |
| `Sales_barcode_lookup_includes_fefo_batch_hints` | lô `LOT2026A` | `5555` |
| `Sales_pos_preview_allocation_matches_fefo` | lô `LOT2026A` | `5555` |
| `Sales_draft_complete_and_return_restores_stock` | hoàn `500` | `5000.00` |
| `Sales_draft_update_then_complete` | subtotal `1000` | `10000.00` |
| `Sales_line_and_order_discount_reduce_total` | HTTP Created, thanh toán `855` tính từ giá `500` | BadRequest |
| `Sales_return_with_discount_refunds_net_amount` | HTTP Created, thanh toán `855` | BadRequest |

Tỷ lệ `5000/500` và `10000/1000` khớp giá local đang gấp mười giá seed. Đây không phải lỗi code của commit. `SellableLotRules` trong bộ đơn vị vẫn từ chối lô có ngày trước hôm nay.

Tổng API filter: 31 bài, 23 pass, 8 fail. Tám fail là bảy bài fixture và một bài webhook.

## 5. Migration và ledger

Clone `C:\novixa-rr001-lab\pgdata` đang nghe `127.0.0.1:5433`. Không có database tên `novixa_prod` trên cluster này.

`novixa_rr001`: `current_database=novixa_rr001`, cổng 5433, ledger 232. Manifest 221 file `.sql`. Pending = 0. Mười một tên có trong ledger nhưng không có trong manifest vẫn là `230`, `231`, `281`–`288`, `290`. Ledger không có `069` và không có `371_pack_content_brand_narrative_dna.sql`. Có `060` và `090`.

Đã chạy đúng `deploy/ubuntu/run-incremental-migrations-prod.sh` của commit, URI `postgresql://postgres@127.0.0.1:5433/novixa_rr001`. Kết quả: `applied=0 skipped=221 pending_ran=0`. Không có dòng `APPLY`, không có `COMPAT_BODY`. Ledger sau chạy vẫn 232. Hết hạn `NT_XUANHOA` vẫn 3 lô, `231.000`. Lô không ngày còn hàng: 0. Ca `status = 1`: 0. Ca `status = 2`: 150. Log: `C:\novixa-rc-008\evidence\incremental-clone.log`.

`371_inventory_stock_review.sql` nằm trong manifest và được `SKIP`. Đó là file khác với `371_pack_content_brand_narrative_dna.sql`.

`novixa_rr002_greenfield`: cùng cổng 5433, ledger 103, có `060_family_members_status_align.sql` và `090_kap_pharmacy_intelligence_seed.sql`, không có `069`. Không chạy lại. Trạng thái này là database lab đã được báo cáo 002 đưa qua `090` bằng cách áp thân `069` ngoài sổ. Lỗi cài database trống dừng ở `090` khi không có `069` vẫn là kết quả của báo cáo 002, không phải một lần chạy mới. Không thêm `069` vào manifest.

Hash thân compat `060`: `E01F0824B7143D76E5D352928D1E83400B3ECF289CCBB688F61C7DE6DFA52226`, 1,627 byte.

## 6. Production không bị ghi

Phiên này không deploy, không push, không restart `kit-platform-api` hoặc nginx, không chạy migration với URI Production, không đóng ca, không sửa 231 đơn vị hết hạn, không tạo dump mới trên Production.

Lệnh đọc Production qua SSH bị chặn trong phiên này và không được chạy lại. Các mốc trong báo cáo 005 lúc 21:05 +07 vì vậy chưa được xác nhận lại: service active, DLL hash `26afdaa7…`, mode `sandbox`, stock-out sync tắt, ledger 232, pending 0, ca `SH-000148` và `SH-000149`, 231 đơn vị hết hạn. Người vận hành phải đọc lại ngay trước cắt.

## 7. Backup và rollback

| Việc | Trạng thái phiên này |
|---|---|
| Dump `C:\novixa-rr001-lab\novixa_prod_20261016_020001.sql.gz` | Có trên đĩa, 15,019,608 byte, mtime 16:32 +07. Giải nén hết, 73,578,246 byte, có header `PostgreSQL database dump`. Không restore lại |
| Dump mới trên Production | Không tạo |
| Copy DLL, SPA và `/opt/kit-platform` đang chạy | Không làm. Báo cáo 005 ghi không có `/var/www/kit-platform.prev` và không có `/opt/kit-platform/releases`; phiên này không kiểm lại |
| Restore dump 02:00 vào `novixa_rr001` | Việc của phiên 001, không lặp lại |
| Incremental `applied=0` trên clone | Làm trong phiên này |
| Restart `systemd` và copy DLL cũ trở lại | Không diễn tập. Máy này không có systemd của site Novixa. Không cài WSL, Docker hay Hyper-V |

`backup-db.sh` đã đọc tĩnh: `pg_dump` bằng `sudo -u postgres`, ghi file `.partial`, kiểm tra mã thoát của `pg_dump` và `gzip`, `gzip -t`, kích thước giải nén, header dump và `CREATE TABLE public.tenants`, rồi mới `mv`. Thiếu `RETENTION_DELETE_CONFIRMED=true` thì không xóa archive cũ. Script không áp migration. Phiên này không chạy script đó.

Phục hồi được viết trong `C:\novixa-rc-008\DEPLOY-AND-ROLLBACK.md`. Nếu lần cắt không chạy migration, hoàn tác là copy trả DLL, năm SPA và `/opt/kit-platform` đã lưu ngoài cây chạy, rồi start service. Restore database từ dump 02:00 sẽ bỏ mọi giao dịch sau thời điểm đó, gồm các ca mở trong ngày. Việc đó chưa diễn tập và không nằm trong bước hoàn tác file.

## 8. Việc chủ nhà thuốc phải xác nhận

Các ô này để trống. Phiên này không điền thay.

| Việc | Xác nhận |
|---|---|
| Cửa sổ triển khai khi không còn ca và quầy đang bán | |
| Quy tắc từ chối bán các đơn vị đã hết hạn, sau khi đếm lại ngay trước cắt | |
| Điểm backup được chấp nhận: dump mới đã kiểm tra, không dùng dump 02:00 làm điểm quay lui của cả ngày | |
| Người smoke test POS sau deploy | |

## 9. Việc còn lại trước khi được phép deploy

1. Bốn xác nhận ở mục 8.
2. Đọc lại Production, chỉ lệnh đọc, vì phiên này không đo lại: service, hash DLL đang chạy, mode `sandbox`, stock-out sync tắt, ledger, pending = 0, không có file Content nguy hiểm, ca đang mở, số đơn vị hết hạn, runtime ASP.NET Core 10.
3. Copy DLL, năm SPA và `/opt/kit-platform` ra ngoài cây đang chạy và ghi checksum.
4. Tạo dump mới, `gzip -t`, lưu ngoài máy chạy.
5. Chủ dự án cho phép chạy đúng một lệnh deploy ở mục 10, không `-RunMigrations`.

Không thêm điều kiện khác. Lỗi cài database trống ở `090` không chặn lần nâng ledger đang pending = 0. Việc chưa diễn tập `systemd` là rủi ro chủ dự án chấp nhận khi duyệt lệnh deploy, không phải lỗi code.

## 10. Lệnh deploy đề xuất

Chi tiết và bước phục hồi: `C:\novixa-rc-008\DEPLOY-AND-ROLLBACK.md`.

Sau khi mục 9 xong:

```powershell
& "E:\KitPlatform-wt-phase5-novixa-product-proof\scripts\deploy-update-vps.ps1" `
  -Root "C:\novixa-rc-008\staging-root"
```

Không truyền `-RunMigrations`. Không đổi `-Root` sang `E:\KitPlatform` hoặc worktree candidate.

Sau restart: `journalctl -u kit-platform-api -n 50 --no-pager`, health, health/db, login đúng `NT_XUANHOA`, một tra cứu POS, một preview không đụng tồn hết hạn, cashier không mở báo cáo, mode catalog và stock-out sync không đổi, ledger không thêm file Content. Nếu lệch, copy trả bản đã lưu và start service. Không restore database khi migration không chạy.
