# NOVIXA-PRODUCTION-ACCEPTANCE-011

**Ngày:** 2026-10-16  
**Trạng thái cuối:** `OPERATING_WITH_GAPS`  
**Release:** `66afd2d96512eaf8c0a6f533f2a571616b2781f6`  
**Nguồn trước:** `docs/novixa/03-operations/NOVIXA-PRODUCTION-DEPLOY-010-REPORT.md`

Phiên này không deploy, không restart, không rollback, không chạy migration. Không đăng nhập lại, không đổi mật khẩu, không tạo đơn, không ghi sổ, không xuất kho, không đổi `NationalDrugCatalog`.

## 1. Kết luận

Production đang phục vụ bản release. Lúc 23:07 +07, health và health/db vẫn đạt. Năm `index.html` công khai khớp từng byte với bản đã cắt lúc 22:41. Người vận hành đã đăng xuất rồi đăng nhập lại thành công. HTTP 401 của mật khẩu trong file vận hành, ghi ở báo cáo 010, không còn là lỗi đăng nhập hiện tại.

Nhà thuốc Xuân Hòa có thể tiếp tục vận hành trên bản này. Chưa có bằng chứng trên process mới cho tra cứu thuốc, tồn theo lô, hay một lệnh POS có phiên. Một lỗi đã nằm trong mã đang chạy: báo cáo và ô tổng quan “cận hạn” lấy mọi lô có HSD trước mốc, kể cả lô đã hết hạn. Lần thấy trên màn hình là 2026-10-13, lúc còn binary cũ. Process mới chưa được mở lại màn đó.

## 2. Sức khỏe sau cắt

Đo lại lúc 23:07:36 +07, chỉ HTTP GET.

| Kiểm tra | Kết quả |
|---|---|
| `GET https://api.novixa.vn/api/health` | 200, `{"status":"ok"}` |
| `GET https://api.novixa.vn/api/health/db` | 200, `database: true` |
| `GET /api/platform/setup-status` | 200, `tenantsCount: 8`, `setupRequired: false` |
| `GET /api/sales/pos/lookup` không token | 401 |
| `GET /api/sales/shifts/current` không token | 401 |
| `GET /api/reports/catalog` không token | 401 |
| `https://admin.novixa.vn/` | 1.058 byte, SHA-256 `5f095fc326f88102b38576b48c3b83d240e211df39196a6cc0f2218d04fad834` |
| `https://app.novixa.vn/` | 1.515 byte, SHA-256 `749bcd01906782911225330155959f81b981102406c91afb6de238b1c811eed6` |
| `https://pos.novixa.vn/` | 1.099 byte, SHA-256 `8d626a0ce41e3346a36759cf1b0674f34cba590ef15e525844a4e4fc546aad88` |
| `https://prescriber.novixa.vn/` | 777 byte, SHA-256 `9ad5709161ab9abb17719b7f18660d183da38ada5c92c76d33bf20e1a23fe0b0` |
| `https://family.kittech.vn/` | 1.185 byte, SHA-256 `3b10c59379ea2d34f0ccc4b85369b44093aa99b45caf88582bf184e116e7aa40` |

Năm hash trùng bảng sau cắt của báo cáo 010. Service vẫn là process khởi động 22:41:57 +07 theo lần đọc đó. Phiên này không SSH, không đọc lại PID, ledger, ca, hay biến môi trường.

Các mốc vẫn lấy từ lần đọc 22:42–22:44 +07 trong báo cáo 010, vì không có lý do đo lại: DLL SHA-256 `ec974eccdd0e6cd4d83c5e3480f6757650d8a040c2fa80a7f747e9b403b73319`, catalog runtime `sandbox`, `EnableStockOutSync=false`, ledger 232, Xuân Hòa 0 ca mở và 151 ca đóng, ca `DEMO_PHARMACY` / `AUD-SHIFT-TODAY` còn mở và không bị sửa, 3 lô hết hạn còn `231.000` đơn vị. `smoke-test.sh` lúc 22:44 kết thúc `All checks passed`.

Process PowerShell bọc script deploy thoát mã 1 vì `nginx -t` ghi dòng thành công ra stderr. Remote đã restart xong trước đó. Đây là lỗi của process bọc, không phải service chết. Không chạy lại script.

## 3. Luồng đã có bằng chứng

| Luồng | Mức | Kết quả |
|---|---|---|
| API sống, database sống | Vận hành, process mới | 200 lúc 22:44 và lại 200 lúc 23:07 |
| Năm SPA của release | Vận hành | Hash byte lúc 23:07 trùng staging |
| Route POS, ca, báo cáo đã gắn | Kỹ thuật, không phiên | Không token trả 401 lúc 22:44 và lúc 23:07 |
| CORS sáu origin, manifest POS, setup-status | Kỹ thuật | `smoke-test.sh` 22:44 đạt. Setup-status đọc lại 23:07 vẫn 200 |
| Đăng nhập `NT_XUANHOA` | Vận hành, do người vận hành xác nhận | Đăng xuất rồi đăng nhập lại thành công sau lần 401 |
| User `admin` còn hoạt động, hash không bị migration đụng | Đọc DB lúc 22:44 | `status=1`, bcrypt 60 ký tự. Không đổi mật khẩu |
| Quyền trên sổ | Đọc DB lúc 22:44 | User admin có `reports.read`, `reports.export`, `sales.pos`, `sales.read`. Role `CASHIER` không có `reports.read`, có 1 user đang hoạt động |
| Bán, FEFO, ca, catalog, tồn trên database local | Kỹ thuật, trước cắt | Báo cáo 008. Không phải process Production |
| Từ chối bán lô hết hạn | Kỹ thuật, trước cắt | `SellableLotRules` trên bộ đơn vị của commit. Chưa gọi trên Production |
| Login bỏ trống tenant khi không có tenant mặc định | Kỹ thuật, trước cắt | Test trả 400. Production có `Auth:DefaultTenantCode=NT_XUANHOA`, nên nhánh 400 không áp cho site này |

Mật khẩu trong file vận hành vẫn không khớp hash. Lần 010 gửi mật khẩu đó và nhận 401, body chỉ có `message`. Cùng user, bỏ trống tenant, cũng 401: site đang có tenant mặc định `NT_XUANHOA`, mật khẩu sai bị trả 401 trước khi có phiên. Không gửi lại mật khẩu đó.

## 4. Luồng chưa kiểm trên process mới

| Luồng | Lý do dừng |
|---|---|
| Tải danh mục, khách, tổng quan sau đăng nhập | Cần phiên. Không dùng lại mật khẩu đã 401. Không mượn phiên của người vận hành |
| Tra cứu thuốc và tra cứu POS có token | Cùng lý do. Route không token chỉ chứng minh đã gắn |
| Tồn theo lô, HSD, chọn lô lúc bán | Cần phiên, và chọn lô để bán là một đơn mới |
| Tạo đơn, thu tiền, trừ tồn, đóng ca | Cần phê duyệt. Không gọi |
| Cashier mở báo cáo và bị từ chối | Chỉ có thành phần quyền trên sổ. Chưa có phiên cashier |
| Ca Xuân Hòa sau 22:44 | Lần đọc cuối là 0 ca mở. Người vận hành đăng nhập sau đó. Không đọc lại sổ ca |
| Màn cận hạn và ô “Đơn bán hôm nay” trên process mới | Xem mục 5. Không mở bằng phiên mới |

Smoke chỉ-đọc ngày 2026-10-13 (`NOVIXA-PROD-SMOKE-NT_XUANHOA-01`) chạy trên binary cũ, hash `26afdaa7…`, process từ 2026-09-29. Các số 3.024 sản phẩm, 2.308 lô, 3.432 khách, 32 đơn trong ngày không được dùng làm kết quả của bản này.

## 5. Lỗi

### Đã xác nhận trong mã đang chạy

Báo cáo cận hạn của commit này lọc `expiry_date <= mốc`, mốc là hôm nay theo giờ Việt Nam cộng số ngày. Không có cận dưới là hôm nay.

- `ReportsService.RunInventoryNearExpiryAsync` truyền một mình `cutoff`.
- `ReportsRepository.GetInventoryNearExpiryAsync` giữ `b.expiry_date <= @ExpiryBefore`.
- Ô tổng quan dùng cùng kiểu: `b.expiry_date <= @ExpiryBefore`, không có `>= hôm nay`.

Nhãn màn hình là “HSD trong 30 ngày tới”. Câu SQL này gồm cả lô đã qua ngày. Ngày 2026-10-13, trên binary cũ, màn đó hiện đúng hai lô đã hết hạn (`2026-03-31` và `2026-07-31`) dưới nhãn trước 12/11/2026. Lúc 22:44 ngày 2026-10-16, Production vẫn còn 3 lô hết hạn, `231.000` đơn vị, không sửa. Process mới chưa được mở lại báo cáo, nên danh sách dòng trên màn hình mới là suy ra từ câu SQL và số lô còn lại, chưa phải một response mới.

Sửa tối thiểu, khi được làm ở bản sau: thêm `expiry_date >= hôm nay` vào câu báo cáo và câu đếm tổng quan. Lô đã hết hạn ở lại bộ lọc hết hạn và luật từ chối bán. Không sửa 231 đơn vị. Không deploy worktree `E:\KitPlatform`.

### Không còn là lỗi đăng nhập

401 lúc 22:44 là mật khẩu file vận hành không khớp hash. Người vận hành đã vào lại được. Không đổi hash, không đổi mật khẩu.

### Lỗi script, service vẫn sống

`nginx -t` ghi “syntax is ok” ra stderr. Process PowerShell cha có `$ErrorActionPreference = Stop` nên thoát mã 1 sau khi remote đã xong. Sửa tối thiểu ở lần bọc sau: không coi stderr của lệnh native thành công là lỗi dừng, hoặc gom stderr của `nginx -t` trước khi trả mã. Không sửa script trong phiên này.

### Không kết luận là lỗi của bản này

Lệch “Đơn bán hôm nay” 87 so với 32 đơn ngày 13/10 là đo trên binary cũ: tổng quan đếm từ 17:00 ngày hôm trước. Trong commit này, `VietnamBusinessCalendar.TodayRangeUtc` lấy nửa đêm giờ Việt Nam. Chưa đo lại ô đó trên process mới. Không mở việc sửa cho đến khi một lần đọc có phiên cho thấy số vẫn lệch.

`SP tồn thấp: 5.373` so với API trả 500 dòng cũng là lần đọc 2026-10-13. Chưa đo lại.

## 6. Sẵn sàng tại Nhà thuốc Xuân Hòa

Có thể tiếp tục mở quầy trên bản đang chạy. Đăng nhập đã được người vận hành xác nhận. API và năm SPA của release còn đúng lúc 23:07. Catalog vẫn sandbox, đồng bộ xuất kho quốc gia vẫn tắt, ledger không thêm file, ca demo không bị đóng.

Chưa nghiệm thu xong thao tác bán: chưa có một tra cứu POS có phiên, chưa có một preview lô còn hạn, và chưa có một đơn trên process mới. Lô hết hạn vẫn bị từ chối ở luật bán trong bộ đơn vị; luật đó chưa được gọi trên Production sau cắt. Báo cáo cận hạn của bản này vẫn gom lô đã hết hạn vào cửa sổ “sắp tới”.

## 7. Việc tiếp theo

Một phiên chỉ đọc, do người vận hành đã đăng nhập `NT_XUANHOA` thực hiện hoặc đứng cạnh. Không tạo đơn, không thu tiền, không sửa lô.

1. Tra cứu một mã còn hạn trên POS. Ghi mã HTTP và có lô gợi ý hay không. Không bấm thanh toán.
2. Mở tồn theo lô. Ghi còn thấy HSD tăng dần hay không.
3. Mở báo cáo cận hạn 30 ngày. Nếu vẫn có HSD trước hôm nay, lỗi mục 5 được xác nhận trên process mới và việc sửa là thêm cận dưới ngày hôm nay.

Dừng trước khi ghi đơn. Ca `DEMO_PHARMACY` không dùng để thay ca Xuân Hòa, và không đóng ca demo.
