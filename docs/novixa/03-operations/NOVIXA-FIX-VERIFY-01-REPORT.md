# NOVIXA-FIX-VERIFY-01

Xác minh hồi quy có mục tiêu cho patch A/B/D trong `NOVIXA-FIX-IMPLEMENT-01`. Không sửa mã, không sửa test, không commit, không push, không deploy, không migration, không seed, không SQL ghi. Không đọc hay ghi tenant `NT_XUANHOA`.

Ngày chạy: 2026-10-13. Postgres và API là bản local `Host=localhost`, database `kitplatform`. Phiên UI là `DEMO_PHARMACY`.

## Kết quả

| Mục | Kết quả |
|---|---|
| 1. Caller trực tiếp của `VietnamBusinessCalendar` | **PASS** |
| 2. Mốc ngày / tuần / tháng tại các caller đó | **PASS** |
| 3. Caller của `GET /api/inventory/stock/low-stock` không còn giả định mảng | **PASS** |
| 4. UI `/inventory/low-stock` | **PASS** |
| 5. Build / test backend tối thiểu | **PASS** |
| 6. 382 lỗi TypeScript đối chiếu với diff | **PASS** cho phạm vi file patch. Không tuyên bố admin build PASS |
| 7. Diff chỉ thuộc A/B/D, test và tài liệu | **FAIL** cho toàn working tree |

## 1. Caller trực tiếp — PASS

`VietnamBusinessCalendar` là `internal`. Caller sản xuất, ngoài Tổng quan và Cockpit đã có trong test, là các chỗ sau.

### Loss Prevention

| Chỗ | Hàm lịch | Cách dùng |
|---|---|---|
| `LossPreventionService.GetCashVarianceTodayAsync` | `Today` | `DateOnly` ghi vào DTO. Không đưa vào tham số `timestamp`. |
| `LossPreventionRepository.ListTodayShiftsAsync` | `TodayRangeUtc` | `closed_at >= @DayStart AND closed_at < @DayEnd`. Ca `open` không bị lọc theo ngày. |
| `LossPreventionRepository.GetCycleCountStatusTodayAsync` | `Today` và `TodayRangeUtc` | Nhãn ngày là `DateOnly`. Phiếu đã duyệt lọc `approved_at >= @DayStart AND approved_at < @DayEnd`. |
| `LossPreventionRepository.ListCycleCountSuggestionsAsync` | `TodayRangeUtc` rồi `AddDays(-7)` | Cửa sổ bán nóng bên dưới. |
| `LossPreventionRepository.ListCycleCountVarianceAsync` | `Today(approvedAt)` | Chỉ đổi `approved_at` đã đọc thành `DateOnly` để hiển thị. Bộ lọc thời gian là `@FromUtc` / `@ToUtc` do caller truyền. |

`GetEmployeeReportsAsync` và `GetAuditFeedAsync` không gọi `VietnamBusinessCalendar`. Khi thiếu `from` hoặc `to`, chúng gọi `ReportsDateHelper.ResolveRangeUtc`. Nhánh mặc định của helper đã được patch A đặt `Kind=Utc`. Test `Report_default_range_is_utc_and_explicit_range_is_unchanged` khóa nhánh đó.

### Growth Desk

| Chỗ | Hàm lịch | Cách dùng |
|---|---|---|
| `GrowthDeskService.GetDormantBuyersAsync` | `RollingDaysRangeUtc(utcNow, dormantDays).StartUtc` | `dormantDays` bị kẹp 7..365. SQL: `HAVING MAX(o.order_date) < @DormantBefore`. |
| `GrowthDeskService.CareDormantBuyerAsync` | cùng biểu thức, `dormantDays` cố định 30 | Cùng so sánh `< @DormantBefore`. |

Báo cáo tuần refill không gọi `VietnamBusinessCalendar`. `GetWeeklyRefillReportAsync` lấy ngày từ `(NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date`, rồi lấy thứ Hai đến Chủ nhật bằng `DateOnly`. `notified_at` và `converted_at` được đổi sang ngày lịch VN trong SQL.

### Shift Checklist

`GetTodayAsync` (khi không có chi nhánh và khi có) và `StartOrGetAsync` chỉ gọi `Today`. Giá trị là `DateOnly`, ghi vào cột `business_date`. Không có cửa sổ `timestamp`, không có tuần, không có tháng.

### Tháng

Không có caller Loss Prevention, Growth Desk hay Shift Checklist gọi `MonthToDateRangeUtc`. Tháng của ba màn này không đi qua patch `Kind`.

## 2. Mốc ngày / tuần / tháng — PASS

Không thêm test. Ba service không được gọi trên database trong lượt này. Mốc được đối chiếu từ code path và từ test lịch đã có, chạy lại trong mục 5: `Today_range_is_vietnam_calendar_day_as_utc`, `Rolling_and_month_ranges_keep_utc_kind`, `Vietnam_session_shifts_unspecified_bounds_and_keeps_utc_bounds`.

Mốc chuẩn của test, `2026-10-13 03:15Z`: ngày lịch VN 13/10, `[2026-10-12 17:00Z, 2026-10-13 17:00Z)`, cả hai `Kind=Utc`. `DateTime.AddDays` giữ `Kind`.

### Ngày

- Ca đã đóng và kiểm kê đã duyệt dùng đúng nửa khoảng `TodayRangeUtc`. Sau patch, cả hai đầu là `Kind=Utc`, nên phiên `TimeZone=Asia/Ho_Chi_Minh` không còn hiểu đồng hồ `17:00` là giờ +7.
- `Today()` cộng 7 giờ rồi lấy `DateOnly`. Thân hàm không đổi trong diff. Shift Checklist và nhãn Loss Prevention đi đường này, không chịu `DateTimeKind` của Npgsql.
- Ranh giới ngày của checklist: trước `17:00Z` là ngày lịch VN hiện tại; từ `17:00Z` là ngày lịch kế tiếp. Cùng quy ước với khoảng `TodayRangeUtc`.

### Tuần

- Growth Desk refill: tuần lịch thứ Hai–Chủ nhật, kiểu `date`, tính trong Postgres theo `Asia/Ho_Chi_Minh`. Patch `Kind` không chạm đường này.
- Loss Prevention, gợi ý bán nóng: `weekStart = dayStart.AddDays(-7)`, cận trên gửi vào SQL là `DateTime.UtcNow`, không phải `todayEnd`. Với mốc test, cận dưới là `2026-10-05 17:00Z` (`Kind=Utc`), tức 00:00 ngày 06/10 theo lịch VN, đến thời điểm UTC hiện tại. `RollingDaysRangeUtc(7)` sẽ bắt đầu muộn hơn một ngày (`AddDays(-6)`) và kết thúc ở hết ngày lịch. Đây là cửa sổ sẵn có của caller, không phải hồi quy của phép tính ngày. Phần `Kind` của cận dưới đi theo `TodayRangeUtc`; cận trên vốn đã là `UtcNow`.

### Tháng

Không có mốc tháng trên ba caller trực tiếp. Mốc tháng mặc định khi Loss Prevention bỏ trống `from`/`to` nằm ở `ReportsDateHelper`, đã được test `Kind=Utc` trong bộ `NovixaFixImplement01`.

## 3. Caller low-stock không còn giả định mảng — PASS

Tìm `GET /api/inventory/stock/low-stock` và `fetchLowStockProducts` trong client, script và test.

| Caller | Cách đọc |
|---|---|
| `client/admin/src/shared/api/inventory.api.ts` `fetchLowStockProducts` | `normalizePaged`: `items`/`Items`, `total`/`Total`, `page`/`Page`, `pageSize`/`PageSize`. Kiểu trả về `PagedLowStockProducts`. |
| `LowStockPage.tsx` | `result.items`, `result.total`. Không gọi `.map` trên response thô. |
| `api-schema.ts` | `200` là `PagedLowStockProductsResult` (`items`, `total`, `page`, `pageSize`). Sửa tay. |
| `scripts/smoke-admin-navigation.ps1` | `Invoke-RestMethod ... \| Out-Null`. Không đọc phần tử. |
| `scripts/smoke-all-modules.ps1` | `Test-Api` không có `Assert` cho endpoint này. Chỉ cần HTTP thành công. |
| Test `NovixaFixImplement01` | Gọi service, không gọi HTTP, và đọc `Items`/`Total`. |

`client/staff-app` không gọi endpoint này. Dashboard và Cockpit chỉ điều hướng tới route `/inventory/low-stock`; số trên thẻ là `lowStockProductCount`, không phải body của endpoint.

`client/admin/openapi/swagger.json` vẫn khai báo `200` là `type: array`. File này không nằm trong diff và không được runtime admin đọc. `openapi:sync` nếu chạy lại sẽ ghi đè `api-schema.ts` từ file swagger cũ. Đó là contract nguồn chưa cập nhật, không phải caller đang chạy.

Trên API local, tenant `DEMO_PHARMACY`: body bắt đầu bằng `{`, `isArray=False`, khóa `items,total,page,pageSize`.

## 4. UI `/inventory/low-stock` — PASS

API local được bật vì cổng 5290 chưa nghe. Vite `dev:vite` tại `http://localhost:5173`. Không đụng production. Phiên trình duyệt đang mở ghi `DEMO_PHARMACY` trên Tổng quan; chữ `NT_XUANHOA` không có trên trang. Thẻ Tổng quan **SP tồn thấp 62675** khớp `total` của endpoint khi không lọc kho.

Không bấm Lưu nháp và không tạo PO.

| Việc | Quan sát |
|---|---|
| Kiểu | API trả object. UI hiện 25 dòng, cột số lượng và ngưỡng là số. `pageSize=500` bị kẹp: `items=100`, `pageSize=100`, `total` vẫn 62675. |
| Phân trang | Kho chính: trang 1 và trang 2, mỗi trang 25 dòng, `total` 3187, không trùng khóa sản phẩm–kho. Nút sang trang đổi dòng đầu (trang 1 khác trang 2) và nhãn trang active. |
| `total` | Nhãn `3187 bản ghi` khi lọc Kho chính; `62675 bản ghi` khi bỏ lọc kho. `total` không bằng số dòng trên trang. |
| Phạm vi kho | Mặc định: `Đang lọc một kho: Kho chính.` Bỏ chọn kho: `Đang xem mọi kho.` và bảng thêm cột Kho, Chi nhánh. Một kho trên API: `total` 3989, nhỏ hơn 62675, mọi dòng đúng `warehouseId` đã gửi. |
| Đề xuất nhập | Ghi chú form, chưa lưu: `Đề xuất nhập từ cảnh báo tồn thấp · trang hiện tại · Kho chính · 25/3187`. Form có dòng `items_0` đến `items_24`, tức 25 dòng của trang, không phải 3187 hay 62675. Dòng đầu là SKU đang đứng đầu trang Kho chính. |

Khi không lọc kho, hai request `pageSize=25` trang 1 và trang 2 dùng chung 1 khóa sản phẩm–kho (vị trí 19 trên trang 1 và vị trí 0 trên trang 2). Gọi lại trang 1 không đổi tập khóa. `ORDER BY` là số lượng, tên kho, tên sản phẩm, không có khóa duy nhất, nên các dòng cùng mức sắp xếp có thể đổi trang giữa hai request. Kho chính không tái hiện trùng khóa. Test patch vẫn chứng minh ghép trang trên fixture có mã riêng.

## 5. Build và test backend — PASS

Lệnh, working tree `E:\KitPlatform`:

```text
dotnet build src/KitPlatform.Api/KitPlatform.Api.csproj --nologo -v q --no-restore
```

Exit code **0**. `Build succeeded.` **0 Error(s), 12 Warning(s).** Cả 12 cảnh báo là `NU1903` cho `Newtonsoft.Json` 10.0.3 trên đồ thị phụ thuộc của API, gồm `KitPlatform.Infrastructure` và `KitPlatform.Packs.Pharmacy.Infrastructure`. Không có cảnh báo trình biên dịch C# trong bản tóm tắt này. Build kéo theo các project backend bị patch: Infrastructure, Pharmacy Application, Pharmacy Infrastructure, API.

```text
dotnet test tests/KitPlatform.Application.Tests/KitPlatform.Application.Tests.csproj --filter FullyQualifiedName~NovixaFixImplement01 --nologo
```

Exit code **0**. `Passed: 7, Failed: 0, Skipped: 0`, khoảng 2 giây. Fixture `FIXIMPL01_` do test tạo và xóa; test từ chối xóa tenant không mang prefix đó. Không migration, không seed.

## 6. 382 lỗi TypeScript — PASS trong phạm vi file patch

Danh sách 382 lỗi không được lưu thành file trong báo cáo triển khai. Lượt này chạy lại:

```text
npx tsc --noEmit --pretty false --incremental false
```

Thư mục `client/admin`. Exit code **2**. **382** dòng `error TS`, **51** file. Không có dòng nào thuộc `LowStockPage.tsx`, `inventory.api.ts`, `inventory.types.ts`, `api-schema.ts`, `inventory.json`.

Nhóm lớn nhất vẫn ở content, ví dụ `ContentFamixaSeriesTab.tsx` 179 lỗi. Mã gặp nhiều: `TS18048` 76, `TS2345` 66, `TS2580` 60, `TS6133` 46. Không sửa các lỗi này.

Không chạy `npm run build`. Admin build **không** được coi là PASS.

## 7. Phạm vi diff — FAIL

Hunk của các file mà `NOVIXA-FIX-IMPLEMENT-01` nêu là đúng A, B, D và test:

- A: `VietnamBusinessCalendar.cs` (`AsUtc` trên `TodayRangeUtc` và `MonthToDateRangeUtc`), `ReportsDateHelper.cs` (mốc mặc định `Kind=Utc`).
- B: `DashboardRepository.cs`, `ReportsRepository.cs`, `ReportsService.cs` thêm `expiry_date >=` ngày lịch VN hôm nay. Trần vẫn là hôm nay + `expiryDays`.
- D: `StockController.cs`, `IInventoryService.cs`, `InventoryDtos.cs` (`PagedLowStockProductsResult`), `InventoryService.cs` (kẹp trang 1..100), `InventoryRepository.cs` (đếm `total`, `LIMIT`/`OFFSET`), admin API, trang tồn thấp, hai file `inventory.json`, `api-schema.ts`.
- Test: `NovixaFixImplement01Tests.cs` (chưa track) và `KitPlatform.Application.Tests.csproj` (tham chiếu `Dapper` 2.1.79, `Npgsql` 10.0.3).
- Tài liệu triển khai: `docs/novixa/03-operations/NOVIXA-FIX-IMPLEMENT-01-REPORT.md` (chưa track).

`git diff --stat` của 17 file đã track trong danh sách đó: 174 dòng thêm, 51 dòng xóa. Không thấy hunk ngoài A/B/D trong các file này.

Working tree không chỉ có các file đó. `git status --short` có 488 dòng; **469** đường dẫn nằm ngoài danh sách patch, test và báo cáo triển khai. Nhóm lớn: `client` 193, `docs` 107, `src` 98, `migrations` 18, `tests` 15. Trong `src/Packs/Pharmacy` còn diff không thuộc A/B/D, đã mở để phân loại: email thông báo tồn thấp, ném lại lỗi đồng bộ CSDL, `BatchResolver` / `IBatchResolver`, đăng ký `ICustomerPosContextService`, đổi giá phiếu nhập, và phần thêm trong `SalesRepository.cs`. Commit cả working tree sẽ kéo những thay đổi này.

`swagger.json` không đổi và vẫn mô tả response là mảng.

## Việc đã không làm

- Không sửa mã hoặc test.
- Không commit, push, deploy.
- Không migration, seed, SQL ghi.
- Không đọc hoặc ghi `NT_XUANHOA`.
- Không chạy `npm run build`.
- Không regenerate OpenAPI.
- Không gọi service Loss Prevention, Growth Desk, Shift Checklist trên database. Mốc của chúng được chứng minh bằng code path và test lịch.

API local `http://localhost:5290` và Vite `http://localhost:5173` được bật vì chưa có tiến trình nghe cổng. Không bật watcher tự restart.

Chờ Architect quyết định commit. Không chuyển sang triển khai production.
