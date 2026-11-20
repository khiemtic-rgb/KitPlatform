# NOVIXA-FIX-IMPLEMENT-01

Sửa có giới hạn A, B, D trên working tree phát triển. Chưa commit, chưa deploy, chưa đụng tenant `NT_XUANHOA`.

C không sửa. Đề xuất đối chiếu nằm ở mục 6.

## 1. Nguyên nhân đã sửa

### A — mốc “hôm nay” trên Tổng quan

`VietnamBusinessCalendar` tính đúng nửa khoảng ngày lịch Việt Nam, nhưng kết quả để `DateTimeKind.Unspecified`. Npgsql 10 gửi giá trị đó thành `timestamp without time zone`. Phiên Postgres `TimeZone=Asia/Ho_Chi_Minh` hiểu đồng hồ đó là UTC+7, nên mốc sớm hơn ngày lịch 7 giờ. Danh sách đơn và báo cáo gửi instant `Kind=Utc`, nên vẫn đúng ngày 13/10.

Sửa chỉ `DateTimeKind` của instant đã trừ 7 giờ. Không đổi phép tính ngày, `SoldStatusFilter`, `OriginalTotalExpr`, cách trừ hoàn, SQL danh sách đơn hay SQL báo cáo doanh thu.

`ReportsDateHelper.ResolveRangeUtc` giữ nguyên khi request đã có cả `from` và `to`. Nhánh mặc định khi thiếu mốc cũng trả `Kind=Utc`.

### B — “HSD trong 30 ngày tới”

Trần `<= hôm nay + expiryDays` đã đúng. Điều kiện thiếu cận dưới, nên lô đã hết hạn còn tồn vẫn lọt vào báo cáo và thẻ Tổng quan.

Quy ước đang có, dùng cho lần sửa này:

- Hết hạn trên màn tồn: `expiry_date < CURRENT_DATE`. Lô hết hạn đúng hôm nay không thuộc lọc này.
- Cửa sổ 30 ngày: `expiry_date >= ngày lịch VN hôm nay` và `expiry_date <= ngày lịch VN hôm nay + expiryDays`.

Cận dưới được thêm vào `ReportsRepository.GetInventoryNearExpiryAsync` và `NearExpiryBatchCount` của Tổng quan. Không đổi `expiryDays` mặc định, không đổi `<=` thành `<`, không đổi preset `within_1m`.

### D — tồn thấp bị cắt 500 dòng

`GET /api/inventory/stock/low-stock` trả một mảng và `LIMIT 500` cố định, không có `total`. Hạt đếm và biểu thức ngưỡng giữ nguyên.

API nay trả `{ items, total, page, pageSize }`. `pageSize` kẹp 1..100 như danh sách lô. `total` không bị cắt theo trang. Màn `/inventory/low-stock` nói rõ đang lọc một kho hay mọi kho, phân trang theo `total`, và đề xuất nhập ghi rõ chỉ lấy trang hiện tại (hoặc dòng đã chọn trên trang đó).

## 2. File đã đổi

| File | Việc |
|---|---|
| `src/KitPlatform.Infrastructure/Dashboard/VietnamBusinessCalendar.cs` | `TodayRangeUtc` và `MonthToDateRangeUtc` trả `Kind=Utc`. `RollingDaysRangeUtc` đi theo `TodayRangeUtc`. |
| `src/KitPlatform.Infrastructure/Reports/ReportsDateHelper.cs` | Mốc mặc định khi thiếu `from`/`to` là `Kind=Utc`. |
| `src/KitPlatform.Infrastructure/Dashboard/DashboardRepository.cs` | `NearExpiryBatchCount` thêm `expiry_date >= ngày VN hôm nay`. |
| `src/KitPlatform.Infrastructure/Reports/ReportsService.cs` | Truyền ngày VN hôm nay vào báo cáo cận hạn. Trần vẫn là hôm nay + `expiryDays`. |
| `src/KitPlatform.Infrastructure/Reports/ReportsRepository.cs` | Báo cáo cận hạn thêm cận dưới. |
| `src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Infrastructure/Inventory/InventoryRepository.cs` | Bỏ `LIMIT 500`. Đếm `total` rồi phân trang. |
| `src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Infrastructure/Inventory/InventoryService.cs` | Kẹp `page` / `pageSize`. |
| `src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Application/Inventory/IInventoryService.cs` | Chữ ký phân trang. |
| `src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Application/Inventory/InventoryDtos.cs` | `PagedLowStockProductsResult`. |
| `src/KitPlatform.Api/Controllers/Pharmacy/StockController.cs` | `page`, `pageSize`; response có `total`. |
| `client/admin/src/shared/api/inventory.api.ts` | Đọc kết quả phân trang. |
| `client/admin/src/shared/api/inventory.types.ts` | `PagedLowStockProducts`. |
| `client/admin/src/shared/api/generated/api-schema.ts` | Sửa tay contract endpoint này. Không chạy `openapi:sync`. |
| `client/admin/src/modules/inventory/LowStockPage.tsx` | Phạm vi kho, `total`, đề xuất theo trang hiện tại. |
| `client/admin/src/shared/i18n/locales/vi-VN/inventory.json` | Nhãn phạm vi, tổng bản ghi, đề xuất trang hiện tại. |
| `client/admin/src/shared/i18n/locales/en-US/inventory.json` | Cùng các khóa. |
| `tests/KitPlatform.Application.Tests/NovixaFixImplement01Tests.cs` | Test mới. |
| `tests/KitPlatform.Application.Tests/KitPlatform.Application.Tests.csproj` | Tham chiếu `Dapper` 2.1.79 và `Npgsql` 10.0.3 cho test. |

## 3. Test đã chạy

Lệnh, ngày 2026-10-13, Postgres local `kitplatform`, chuỗi kết nối development cộng `Timezone=Asia/Ho_Chi_Minh`:

```text
dotnet test tests/KitPlatform.Application.Tests/KitPlatform.Application.Tests.csproj --filter FullyQualifiedName~NovixaFixImplement01 --nologo
```

Kết quả lần cuối: **Passed 7, Failed 0, Skipped 0**, khoảng 2 giây.

Fixture ghi vào tenant mã `FIXIMPL01_` rồi xóa trong `finally`. Test từ chối xóa nếu mã không có prefix đó, gồm `NT_XUANHOA` và `DEMO_PHARMACY`. Lần chạy đạt yêu cầu xóa đúng một dòng `tenants`. Không migration, không seed, không SQL trên production.

| Test | Chứng minh |
|---|---|
| `Today_range_is_vietnam_calendar_day_as_utc` | `2026-10-13 03:15Z` ra `[2026-10-12 17:00Z, 2026-10-13 17:00Z)`, cả hai `Kind=Utc`. Đơn `2026-10-12 20:00+07` và `23:59+07` ở ngoài. `00:00+07` và `08:00+07` ngày 13/10 ở trong. |
| `Rolling_and_month_ranges_keep_utc_kind` | Tuần và tháng giữ `Kind=Utc`. |
| `Report_default_range_is_utc_and_explicit_range_is_unchanged` | Thiếu mốc thì `Kind=Utc`. Có đủ hai mốc thì giữ nguyên, kể cả `Unspecified`. |
| `Vietnam_session_shifts_unspecified_bounds_and_keeps_utc_bounds` | `SHOW TimeZone` = `Asia/Ho_Chi_Minh`. Cùng đồng hồ `17:00`, `Kind=Unspecified` nhận đơn tối hôm trước và loại đơn 18:00 cùng ngày lịch. `Kind=Utc` làm ngược lại. |
| `Dashboard_order_list_and_revenue_share_the_vietnam_day_including_returns` | Đơn trước nửa đêm VN không vào Tổng quan, danh sách hay dòng báo cáo của ngày lịch hôm nay. Đơn đúng `00:00` và sau đó thì có. Đơn nháp có trong danh sách, không có trong `todayOrderCount` hay `orderCount`. `todayOrderCount` bằng `orderCount`. Phiếu trả trong ngày của đơn ngày khác: `todayNetTotal` bằng `netAmount`, và nhỏ hơn `salesAmount` đúng khoản hoàn. `todayCollected` theo `paid_at` trừ payment hoàn, không bằng `salesAmount`. Cockpit: đơn đúng đầu tháng VN được tính, đơn một phút trước thì không; `monthNetTotal` và `weekOrderCount` khớp cửa sổ `Kind=Utc`. |
| `Near_expiry_keeps_today_through_cutoff_and_drops_expired_lots` | Hôm qua, `2026-03-31`, `2026-07-31`: vắng khỏi báo cáo và `nearExpiryBatchCount`, có trong `expiry=expired`. Đúng hôm nay, trong cửa sổ, và đúng ngày trần: có trong báo cáo. Ngày trần + 1 và lô tồn 0: không có. Báo cáo và thẻ cùng 3 lô còn trong cửa sổ. Lô hết hạn đúng hôm nay không nằm trong `expired`. |
| `Low_stock_total_pages_under_exact_and_over_500` | 400, 500 rồi 502 cặp. `total` khác số dòng trang. Ghép mọi trang không thiếu, không lặp. Sau khi thêm cặp tồn 100 (không tính) và tồn 3 (có tính): `total` không lọc kho bằng `lowStockProductCount` ở cùng ngưỡng 10. Một kho nhỏ hơn tổng hai kho và bằng số cặp của kho đó. Ngưỡng 0 khác ngưỡng 10 ở cả hai đường, và hai đường vẫn bằng nhau khi dùng cùng ngưỡng. Sản phẩm không có lô vẫn có, tồn 0. `pageSize=500` bị kẹp còn 100 dòng, `total` vẫn đủ. |

## 4. Rủi ro

`VietnamBusinessCalendar` còn được Cockpit, Loss Prevention, Growth Desk và Shift Checklist dùng. Sửa `Kind` đổi mốc của các màn đó theo cùng ngày lịch Việt Nam. Fixture đã đối chiếu tháng/tuần của Cockpit. Ba màn kia chưa được gọi.

Báo cáo khi thiếu `from` hoặc `to` cũng đổi `Kind`. Màn smoke gửi đủ hai mốc dạng `Z`, không đi vào nhánh đó.

Sau sửa B, tenant chỉ còn lô đã hết hạn sẽ thấy chỉ số 30 ngày về 0. Lọc `expired` trên `/inventory/stock` vẫn thấy các lô đó. Thẻ Tổng quan vẫn trỏ `/inventory/stock?tab=fefo`. Cockpit vẫn đếm SKU `expiry_date <= trần` không có cận dưới; không nằm trong hai bề mặt smoke nên không vá trong patch này.

Danh sách đơn đếm mọi trạng thái. “Đơn bán hôm nay” so với báo cáo (`status` 2 hoặc 4). Test có một đơn nháp để giữ khác biệt đó.

Phân trang tồn thấp không đổi thứ tự `COALESCE` của ngưỡng. Đề xuất nhập không còn nhận tối đa 500 dòng đầu; nó nhận trang đang mở, và chữ trên màn nói vậy. Người vận hành muốn toàn bộ kết quả lọc thì phải đổi trang hoặc chọn dòng, không có nút “lấy hết”.

`ActiveBatchCount` vẫn không lọc sản phẩm/kho đã xóa mềm. Thêm `deleted_at` vào `WHERE` chung của câu tồn Tổng quan sẽ đổi luôn cận hạn và lô tồn thấp. Không làm.

## 5. Chưa kiểm chứng

- Không mở `/inventory/low-stock` trên trình duyệt. Không có phiên admin đang chạy, và không khởi động lại API.
- `npx tsc --noEmit` trong `client/admin` vẫn có khoảng 382 lỗi sẵn. Các file admin của patch này không nằm trong danh sách lỗi đó. Không chạy `npm run build`.
- Không chạy Loss Prevention, Growth Desk, Shift Checklist.
- Không đối chiếu `within_1m` với cửa sổ 30 ngày. Hai cửa sổ cố ý khác nhau.
- Không regenerate toàn bộ OpenAPI. Chỉ sửa tay schema của `GET /api/inventory/stock/low-stock`.
- Không đọc hay ghi production. Không migration, không seed, không deploy, không commit, không push.

## 6. C — đề xuất đối chiếu, chưa sửa

2.884 và 2.308 cùng đếm lô `quantity_available > 0`. Tổng quan không loại sản phẩm hay kho đã xóa mềm; danh sách lô thì có. Hướng 2.884 > 2.308 khớp hướng đó. Phần 576 chưa được tách trên dữ liệu.

Khi được ủy quyền đọc riêng, trên bản sao hoặc bằng `SELECT` không ghi, không chạy trên `NT_XUANHOA` từ nhiệm vụ này:

Đếm lô `quantity_available > 0` mà `products.deleted_at` hoặc `warehouses.deleted_at` khác null, cùng kiểu join tenant của thẻ Tổng quan. Nếu số đó bằng 2.884 − 2.308, khác biệt định nghĩa giải thích được thẻ. Nếu không bằng, chưa siết `ActiveBatchCount`. Điều kiện, nếu sau này được chọn, đặt trong `FILTER` của riêng `ActiveBatchCount`, không đặt vào `WHERE` chung.

---

Chờ Architect review trước khi commit hoặc triển khai production.
