# NOVIXA-FIX-PLAN-NT_XUANHOA-01

Kế hoạch truy vết và sửa có giới hạn cho bốn lệch số trong `NOVIXA-PROD-SMOKE-NT_XUANHOA-01`.

Nhiệm vụ này không truy cập tenant `NT_XUANHOA`, không chạy SQL hay API production, không sửa mã và không deploy. Số liệu vận hành chỉ lấy từ báo cáo smoke ngày 13/10/2026, khung 10:11–10:25 +07.

## Phạm vi đã đọc

Chỉ các đường đi từ màn hình và endpoint đã nêu trong báo cáo smoke tới query tương ứng. Không rà toàn bộ repository.

---

## A. Tổng quan — đơn hàng và doanh thu hôm nay

**Phân loại: lỗi đã xác nhận** về mốc “hôm nay” trên Tổng quan. Công thức tiền và bộ đếm đơn không phải cùng một biểu thức; sửa mốc ngày chưa được coi là làm doanh thu khớp cột “Doanh thu” của báo cáo.

### 1. Đường đi

Ngày nghiệp vụ mà giao diện tuyên bố là ngày lịch Việt Nam, UTC+7. Nhãn “Đơn bán hôm nay” có hint `Theo ngày VN (UTC+7)` trong `client/admin/src/shared/i18n/locales/vi-VN/dashboard.json`.

**Tổng quan**

| Bước | Vị trí |
|---|---|
| UI | `client/admin/src/modules/dashboard/DashboardPage.tsx` — “Doanh thu hôm nay” đọc `sales.todayNetTotal`; “Đơn bán hôm nay” đọc `sales.todayOrderCount` |
| Client | `client/admin/src/shared/api/dashboard.api.ts` — `GET /api/dashboard/overview` |
| API | `src/KitPlatform.Api/Controllers/Dashboard/DashboardController.cs` — `Overview`, mặc định `expiryDays=30`, `lowStockThreshold=10` |
| Service | `src/KitPlatform.Infrastructure/Dashboard/DashboardService.cs` — `GetOverviewAsync` |
| Mốc ngày | `src/KitPlatform.Infrastructure/Dashboard/VietnamBusinessCalendar.cs` — `TodayRangeUtc` |
| Query | `src/KitPlatform.Infrastructure/Dashboard/DashboardRepository.cs` — `salesTodaySql` |

`TodayRangeUtc` lấy `DateTime.UtcNow`, cộng 7 giờ để ra ngày lịch Việt Nam, rồi trừ 7 giờ để được nửa khoảng `[00:00 ngày VN, 00:00 ngày hôm sau)`. Kết quả để `DateTimeKind.Unspecified`.

Với thời điểm smoke khoảng 10:15 +07 (`2026-10-13 03:15` UTC), giá trị đồng hồ của tham số là:

- `TodayStart` = `2026-10-12 17:00`, `Kind=Unspecified`
- `TodayEnd` = `2026-10-13 17:00`, `Kind=Unspecified`

Query đơn và tiền đều dùng cùng cặp mốc, nửa khoảng đóng-mở:

```sql
o.order_date >= @TodayStart AND o.order_date < @TodayEnd
```

`order_date` là `TIMESTAMPTZ NOT NULL DEFAULT NOW()` (`migrations/006_sales.sql`). Đơn tạo từ POS không ghi đè cột này (`InsertSaleHeaderAsync` trong `SalesRepository.cs`), nên giá trị là instant của `NOW()`.

Bộ đếm:

```sql
COUNT(*) WHERE status IN (2, 4) AND order_date trong mốc trên
```

`2` = Completed, `4` = Refunded (`SalesOrderStatuses` trong `SalesConstants.cs`, ghép bởi `SalesAccrualSql.SoldStatusFilter`).

Doanh thu trên thẻ không phải bộ đếm đó. `TodayNetTotal` là:

- cộng `SalesAccrualSql.OriginalTotalExpr()` của đơn status 2 hoặc 4 có `order_date` trong mốc;
- trừ `SUM(sales_return_items.refund_amount)` của phiếu trả status Completed có `return_date` trong cùng mốc.

`OriginalTotalExpr` = `total_amount` hiện tại cộng hoàn của các phiếu trả đã hoàn tất trên đơn đó, vì phiếu trả làm đổi header. `TodayCollected` lọc `sales_payments.paid_at` và `sales_return_payments.paid_at`. `TodayNewDebt` là `originalTotal` trừ các payment được `IsCheckout` coi là thu tại quầy, vẫn theo `order_date`.

**Danh sách đơn**

| Bước | Vị trí |
|---|---|
| UI | `client/admin/src/modules/sales/SalesOrderListPage.tsx` — `todayRange()` và `load` |
| Client | `client/admin/src/shared/api/sales.api.ts` — `fetchSalesOrders` |
| API | `src/KitPlatform.Api/Controllers/Pharmacy/SalesController.cs` — `GET /api/sales/orders`, `[FromQuery] DateTime? from/to` |
| Query | `SalesRepository.GetSalesOrdersAsync` — `o.order_date >= @From AND o.order_date < @To` |

UI gửi `startOf('day').toISOString()` tới `startOf('day')` của ngày kế. Trên trình duyệt UTC+7, ngày 13/10/2026 thành:

- `from` = `2026-10-12T17:00:00.000Z`
- `to` = `2026-10-13T17:00:00.000Z`

Chuỗi có `Z` bind thành `DateTimeKind.Utc`. Danh sách không lọc status. Tổng `total` là mọi trạng thái trong nửa khoảng đó.

**Báo cáo doanh thu theo ngày**

| Bước | Vị trí |
|---|---|
| UI | `client/admin/src/modules/reports/SalesRevenuePage.tsx` — `toIsoRange` cùng cách `toISOString()` |
| API | `src/KitPlatform.Api/Controllers/Reports/ReportsController.cs` — `GET /api/reports/sales/revenue-by-period` |
| Mốc | `ReportsDateHelper.ResolveRangeUtc` — nếu cả `from` và `to` có giá trị thì giữ nguyên, không trừ thêm 7 giờ |
| Query | `ReportsRepository.GetSalesRevenueByPeriodAsync` |

Bucket ngày là:

```sql
date_trunc('day', timezone('Asia/Ho_Chi_Minh', o.order_date))
```

Nhãn kỳ cộng lại 7 giờ trong `ReportsDateHelper.FormatVnDateRange`. Số đơn của dòng là `COUNT(*)` đơn status 2 hoặc 4. Cột tiền tách riêng, xem mục doanh thu bên dưới.

### 2. Nguyên nhân đã chứng minh

Smoke đo cùng lúc:

| Cửa sổ trên API đơn | Số đơn |
|---|---|
| 13/10 00:00 → 13/10 10:15 | 32 |
| 12/10 17:00 → 13/10 00:00 | 55 |
| 12/10 17:00 → 13/10 17:00 | 87 |

Danh sách và dòng báo cáo ngày 13/10 đều là 32. Tổng quan là 87. 55 + 32 = 87.

Đường danh sách và báo cáo gửi instant UTC (`Kind=Utc`): nửa khoảng `[2026-10-12 17:00Z, 2026-10-13 17:00Z)` đúng là ngày lịch 13/10 tại UTC+7. Bucket `timezone('Asia/Ho_Chi_Minh', order_date)` của báo cáo cũng ra 32 cho ngày đó.

Đường Tổng quan tính cùng đồng hồ `17:00` nhưng `Kind=Unspecified`. Repo dùng Npgsql 10.0.3 và không bật `Npgsql.EnableLegacyTimestampBehavior`. Với chế độ này, `DateTimeKind.Utc` đi thành `timestamptz`, còn `DateTimeKind.Unspecified` đi thành `timestamp without time zone`. PostgreSQL khi so `timestamptz` với `timestamp without time zone` hiểu timestamp theo `TimeZone` của phiên.

Cửa sổ smoke `12/10 17:00 → 13/10 17:00` đúng bằng đồng hồ Unspecified đó nếu phiên hiểu nó là UTC+7, tức instant thực `[2026-10-12 10:00Z, 2026-10-13 10:00Z)`. Đó là tối 12/10 từ 17:00 tới 24:00 cộng phần ngày 13/10 cho tới 17:00. Lúc 10:15 +07, phần sau 10:15 chưa có đơn, nên 55 + 32 = 87.

`NpgsqlConnectionFactory` không đặt `TimeZone` trên connection. Múi giờ phiên trên production không được đọc trong nhiệm vụ này. Hướng lệch và đúng 7 giờ đã đo được; không cần đoán tên biến môi trường để kết luận mốc Tổng quan đang sớm hơn ngày lịch Việt Nam 7 giờ.

Ba số khép kín (32, 55, 87) nên lọc status của Tổng quan không phải nguồn của 55 đơn này: API đơn không lọc status mà cửa sổ lệch vẫn bằng đúng bộ đếm status 2 và 4. Lọc status vẫn là khác biệt định nghĩa khi so về sau.

### 3. Doanh thu — không suy từ bộ đếm đơn

Thẻ “Doanh thu hôm nay” là `TodayNetTotal`, không phải `TodayOrderCount` và không phải cột “Doanh thu” trên báo cáo.

| Chỉ số | Công thức | Cột thời gian |
|---|---|---|
| Tổng quan `todayNetTotal` | `originalTotal` của đơn bán − hoàn của phiếu trả trong mốc | `order_date` và `return_date` |
| Báo cáo `salesAmount` (“Doanh thu”) | `SUM(originalTotal)` | `order_date`, bucket ngày VN |
| Báo cáo `netAmount` (“DT ròng”) | `salesAmount − refundAmount` | hoàn theo `return_date` |
| Tổng quan `todayCollected` | payment − payment hoàn | `paid_at` |
| Tổng quan `todayNewDebt` | `originalTotal − checkout_paid` | `order_date` |

`originalTotal`, status bán và cách trừ hoàn theo ngày phiếu trả giống nhau giữa `TodayNetTotal` và `netAmount`. Sau khi mốc UTC khớp, hai số đó mới là cặp cần đối chiếu.

Cột “Doanh thu” (`salesAmount`) không trừ hoàn. Smoke chỉ ghi doanh thu Tổng quan khác doanh thu ngày 13/10, không ghi số tiền và không ghi tên cột. Phần chênh tiền do 55 đơn là hệ quả của cùng mốc lệch; phần còn lại so với cột “Doanh thu” là do công thức. Không gộp hai việc đó thành một sửa.

`weekNetTotal` dùng `RollingDaysRangeUtc`, vốn gọi `TodayRangeUtc`, nên cùng `Kind=Unspecified`. Smoke không đo tuần. Sửa helper sẽ đổi luôn tuần.

`ReportsDateHelper.ResolveRangeUtc` chỉ trừ 7 giờ khi request thiếu `from` hoặc `to`, và cũng để `Kind=Unspecified`. Màn báo cáo smoke có gửi đủ hai mốc dạng `Z`, nên không đi vào nhánh đó.

### 4. Phạm vi sửa tối thiểu

Chỉ chỗ tạo `DateTime` đem so với `timestamptz`:

- `VietnamBusinessCalendar.TodayRangeUtc`, `RollingDaysRangeUtc`, `MonthToDateRangeUtc`: trả về `DateTimeKind.Utc` cho instant đã trừ 7 giờ. Không đổi phép tính ngày.
- `ReportsDateHelper.ResolveRangeUtc`: nhánh mặc định khi thiếu mốc cũng trả `Kind=Utc`.

Không đổi `SoldStatusFilter`, `OriginalTotalExpr`, cách trừ hoàn, hay SQL danh sách đơn và báo cáo. Hai đường đó đang khớp ngày lịch 13/10.

### 5. Kiểm thử không ghi dữ liệu vận hành

Không chạy trên `NT_XUANHOA`.

- Unit test `VietnamBusinessCalendar`: `utcNow = 2026-10-13 03:15Z` cho ra `[2026-10-12 17:00Z, 2026-10-13 17:00Z)`, cả hai `Kind=Utc`. Đơn giả định tại `2026-10-12 20:00+07` nằm ngoài; đơn tại `2026-10-13 08:00+07` nằm trong.
- Test tích hợp trên Postgres local có `TimeZone=Asia/Ho_Chi_Minh`, tenant fixture: một đơn tối ngày lịch hôm trước và một đơn trong ngày lịch hôm nay. Bộ đếm Tổng quan bằng số đơn status 2/4 của ngày lịch đó, và bằng `orderCount` của dòng báo cáo cùng ngày.
- Cùng fixture, thêm một phiếu trả trong ngày cho đơn của ngày khác: `todayNetTotal` bằng `netAmount` của dòng đó, và khác `salesAmount` đúng bằng khoản hoàn. Đối chiếu `todayCollected` với payment theo `paid_at`, không với `salesAmount`.

### 6. Rủi ro hồi quy

`VietnamBusinessCalendar` còn được `OwnerCockpitRepository`, `LossPreventionRepository`, `LossPreventionService`, `GrowthDeskService`, `ShiftChecklistService` dùng. Sửa `Kind` sửa luôn mốc của các màn đó theo cùng ngày lịch Việt Nam. Xác minh trên fixture, không trên production: đơn sát 17:00 UTC không bị rơi sang ngày hôm trước trên Tổng quan, Cockpit và báo cáo.

Danh sách đơn đếm mọi status. Sau khi sửa, so “Đơn bán hôm nay” với báo cáo (`status IN (2, 4)`), không với tổng thô của danh sách nếu trong ngày có đơn nháp hoặc đã hủy.

---

## B. Báo cáo sắp hết hạn

**Phân loại: lỗi đã xác nhận.** Nhãn và ngày trần đúng với “30 ngày tới”. Điều kiện thiếu cận dưới, nên lô đã hết hạn còn tồn vẫn lọt vào.

### 1. Đường đi

**Báo cáo**

| Bước | Vị trí |
|---|---|
| UI | `/reports/inventory/near-expiry` — `reports-catalog.ts` mã `INV-02`, `ReportViewPage.tsx` gửi `expiryDays` mặc định 30 |
| API | `ReportsController` → `ReportsService.RunInventoryNearExpiryAsync` |
| Trần | `DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(expiryDays)` |
| Query | `ReportsRepository.GetInventoryNearExpiryAsync` trên `pack_pharmacy.v_inventory_batch` |

View trong `migrations/085_pack_pharmacy_inventory_views.sql` là `SELECT` từ `public.inventory_batches`, không lọc thêm.

Nhãn do service ghi:

- `HSD trong` = `{expiryDays} ngày tới`
- `HSD trước` = `cutoff` dạng `dd/MM/yyyy`

Ngày smoke 13/10/2026 + 30 ngày = 12/11/2026. Khớp nhãn đã thấy.

Điều kiện:

```sql
b.quantity_available > 0
AND b.expiry_date IS NOT NULL
AND b.expiry_date <= @ExpiryBefore
AND p.deleted_at IS NULL
```

Không có `expiry_date >= ngày hôm nay`.

**Thẻ Tổng quan “Sắp hết HSD (30 ngày)”**

`DashboardRepository` đếm:

```sql
COUNT(*) FILTER (
  WHERE b.quantity_available > 0
    AND b.expiry_date IS NOT NULL
    AND b.expiry_date <= @ExpiryBefore
)
```

`@ExpiryBefore` = `VietnamBusinessCalendar.Today(utcNow).AddDays(expiryDays)`, cùng ngày 12/11/2026. Query này không loại sản phẩm hay kho đã xóa.

**Đối chiếu đã có trên màn tồn**

`InventoryRepository.AppendExpiryFilter` và nhánh expiry của danh sách lô:

| Preset | Điều kiện |
|---|---|
| `expired` | `expiry_date IS NOT NULL AND expiry_date < CURRENT_DATE` |
| `within_1m` (và 3m/6m/12m) | `expiry_date >= CURRENT_DATE` và `< CURRENT_DATE + N tháng` |

Smoke: `expiry=expired` đúng hai lô `PVN1229` HSD 31/03/2026 và `PVN4153` HSD 31/07/2026; `expiry=within_1m` = 0. Báo cáo và thẻ Tổng quan đều ra đúng hai lô đó.

### 2. Định nghĩa nghiệp vụ đang có

| Nguồn | Nội dung |
|---|---|
| Tên báo cáo | “Sắp hết hạn sử dụng” (`ReportsService`) |
| Catalog `vi-VN/reports.json` `INV-02` | “Lô còn tồn có hạn dùng trong số ngày cảnh báo.” |
| Nhãn filter | “HSD trong 30 ngày tới”, “HSD trước 12/11/2026” |
| Nhãn thẻ | “Sắp hết HSD (30 ngày)” |
| Preset tồn | Hết hạn và còn hạn trong cửa sổ là hai lọc riêng. Cửa sổ còn hạn bắt đầu từ `CURRENT_DATE` |

Checklist GPP gọi báo cáo này là “cận date / hết hạn” (`vi-VN/inventory.json`, `nearExpiryReport`). Đó là câu chữ rộng hơn nhãn “30 ngày tới”. Không lấy câu checklist để giữ lô đã hết hạn trong chỉ số 30 ngày.

`CURRENT_DATE` của preset tồn phụ thuộc `TimeZone` phiên. Trần báo cáo dùng ngày VN tính bằng UTC+7, không dùng `CURRENT_DATE`. Lúc 10:13 +07 cả hai cùng ngày 13/10, nên lệch này không giải thích hai dòng HSD tháng 3 và tháng 7.

`within_1m` là một tháng lịch, cận trên loại trừ. Báo cáo là 30 ngày, cận trên `<=` ngày trần. Hai cửa sổ không định nghĩa để luôn bằng nhau. Với dữ liệu smoke cả hai đều không có lô còn hạn, nên cùng bằng 0 sau khi loại lô đã hết hạn.

### 3. Phạm vi sửa tối thiểu

Thêm cận dưới vào đúng hai query smoke đã đối chiếu:

- `ReportsRepository.GetInventoryNearExpiryAsync`
- `DashboardRepository` nhánh `NearExpiryBatchCount`

Điều kiện đề xuất, giữ nguyên trần đang dùng:

```text
expiry_date >= ngày lịch VN hôm nay
AND expiry_date <= ngày lịch VN hôm nay + expiryDays
AND quantity_available > 0
AND expiry_date IS NOT NULL
```

Lô đã hết hạn còn tồn giữ ở preset `expired` (`expiry_date < ngày hôm nay`). Không đưa chúng vào chỉ số “30 ngày tới”.

Không đổi `expiryDays` mặc định, không đổi `<=` thành `<`, không đổi preset `within_1m` từ tháng lịch sang 30 ngày trong cùng một sửa.

`OwnerCockpitRepository` có cùng dạng `expiry_date <= @ExpiryBefore` không có cận dưới, đếm SKU chứ không đếm lô. Nằm ngoài hai bề mặt smoke. Ghi để khỏi sửa một nơi rồi Cockpit vẫn liệt kê HSD đã qua; chỉ đưa vào cùng patch nếu Architect muốn một điều kiện dùng chung.

### 4. Kiểm thử không ghi dữ liệu vận hành

Fixture local, không đụng `NT_XUANHOA`:

- Lô tồn, HSD hôm qua: vắng khỏi báo cáo và khỏi `nearExpiryBatchCount`; có trong `expiry=expired`.
- Lô tồn, HSD đúng ngày trần: có trong báo cáo.
- Lô tồn, HSD ngày trần + 1: không có.
- Lô HSD trong cửa sổ nhưng `quantity_available = 0`: không có.
- Với dữ liệu giống smoke (chỉ hai lô HSD 31/03/2026 và 31/07/2026 còn tồn, không có lô còn hạn đến 12/11/2026): báo cáo và thẻ đều 0, khớp `within_1m`.

### 5. Rủi ro hồi quy

Sau sửa, tenant nào chỉ còn lô đã hết hạn sẽ thấy chỉ số 30 ngày về 0. Đó là nội dung nhãn hiện tại. Người vận hành vẫn xem hai lô đó bằng lọc `expired` trên `/inventory/stock`.

Thẻ Tổng quan đang trỏ `/inventory/stock?tab=fefo`, không trỏ báo cáo cận hạn. Sửa query không đổi link đó.

Báo cáo loại sản phẩm `deleted_at`; thẻ Tổng quan không loại. Không phải nguyên nhân hai dòng smoke (cả hai bề mặt cùng hai lô). Không gộp vào patch cận hạn.

---

## C. Chênh lệch số lô đang tồn

**Phân loại: khác định nghĩa nghiệp vụ.** Chưa đủ bằng chứng để gọi 2.884 và 2.308 là một chỉ số bị đếm sai. Hướng chênh khớp một khác biệt điều kiện đã thấy trong mã; phần 576 chưa được đếm trên dữ liệu.

### 1. Đường đi

**2.884 — thẻ “Lô đang tồn”**

| Bước | Vị trí |
|---|---|
| UI | `DashboardPage.tsx` — `inventory.activeBatchCount`, link `/inventory/stock?tab=fefo` |
| API | `GET /api/dashboard/overview` |
| Query | `DashboardRepository`, `ActiveBatchCount` |

```sql
COUNT(*) FILTER (WHERE b.quantity_available > 0)
FROM inventory_batches b
INNER JOIN products p ON p.id = b.product_id AND p.tenant_id = b.tenant_id
INNER JOIN warehouses w ON w.id = b.warehouse_id AND w.tenant_id = b.tenant_id
WHERE b.tenant_id = @TenantId
```

Không có `p.deleted_at IS NULL`, không có `w.deleted_at IS NULL`, không lọc `status` sản phẩm hay kho, không lọc `b.status`.

**2.308 — tab Theo lô (FEFO)**

| Bước | Vị trí |
|---|---|
| UI | `client/admin/src/modules/inventory/StockListPage.tsx` — tab `fefo`, `fetchStockBatches` |
| API | `GET /api/inventory/stock/batches` — `StockController.Batches` |
| Service | `InventoryService.GetStockBatchesAsync` — `pageSize` kẹp 1..100, mặc định 20 |
| Query | `InventoryRepository.GetStockBatchesAsync` |

```sql
WHERE b.tenant_id = @TenantId
  AND p.deleted_at IS NULL
  AND w.deleted_at IS NULL
  AND b.quantity_available > 0
```

`SELECT COUNT(*)` trả `total` trong kết quả phân trang. Smoke ghi khoảng 116 trang × 20 khớp tổng API 2.308. 20 là cỡ trang, không phải trần cắt mất tổng.

Cùng bảng `inventory_batches`. View `v_inventory_batch` không tham gia query này.

### 2. Phần đã chứng minh và phần chưa đủ

Hai chỉ số đều đếm dòng lô có `quantity_available > 0`. Chúng khác ở:

| Điều kiện | Tổng quan 2.884 | Danh sách 2.308 |
|---|---|---|
| Tồn > 0 | Có | Có |
| Sản phẩm chưa xóa mềm | Không lọc | `p.deleted_at IS NULL` |
| Kho chưa xóa mềm | Không lọc | `w.deleted_at IS NULL` |
| Khớp `tenant_id` trên join SP/kho | Có | Không |

Join tenant chặt hơn ở Tổng quan làm Tổng quan nhỏ đi hoặc bằng, không giải thích Tổng quan lớn hơn. Bỏ lọc xóa mềm làm Tổng quan lớn hơn. Hướng 2.884 > 2.308 khớp hướng đó.

Chưa chứng minh 576 = số lô còn tồn của sản phẩm hoặc kho đã xóa mềm. Có thể còn phần khác không nằm trong hai query này. Nhiệm vụ này không chạy `COUNT` trên production để tách phần đó.

Chưa có căn cứ gọi đây là lỗi cho đến khi Architect chọn một định nghĩa: lô còn tồn của hàng còn hiệu lực, hay mọi lô còn số lượng kể cả danh mục đã xóa.

### 3. Phạm vi sửa tối thiểu

Chưa sửa mã.

Nếu Architect chọn định nghĩa của màn tồn, sửa nhỏ nhất là thêm `p.deleted_at IS NULL` và `w.deleted_at IS NULL` vào `ActiveBatchCount`. Không đổi danh sách lô, không xóa dữ liệu.

Việc đó chỉ nên làm sau một lần đếm chỉ-đọc, được ủy quyền riêng, trên bản sao hoặc bằng `SELECT` không ghi: phần Tổng quan trừ phần danh sách có bằng số lô `quantity_available > 0` mà sản phẩm hoặc kho đã `deleted_at`. Lệnh đó không thuộc nhiệm vụ này và không chạy trên `NT_XUANHOA` từ kế hoạch này.

### 4. Kiểm thử không ghi dữ liệu vận hành

Fixture local:

- Lô tồn của sản phẩm `deleted_at` null: cả hai đường đếm.
- Lô tồn của sản phẩm đã xóa mềm: Tổng quan đếm, `GetStockBatchesAsync` không đếm. Kho xóa mềm tương tự.
- `pageSize=20` vẫn trả `total` đầy đủ, không dừng ở 20.

Chưa có test production.

### 5. Rủi ro hồi quy

Nếu siết Tổng quan theo danh sách mà 576 không chỉ là xóa mềm, thẻ vẫn lệch và sẽ bị coi là đã sửa. Vì vậy không vá trước lần đếm tách thành phần.

`NearExpiryBatchCount` và `LowStockBatchCount` nằm cùng câu `FROM` với `ActiveBatchCount`. Thêm `deleted_at` vào `WHERE` sẽ đổi cả ba. Nếu chỉ muốn đổi “Lô đang tồn”, đặt điều kiện trong `FILTER` của `ActiveBatchCount`.

---

## D. Chênh lệch tồn thấp

**Phân loại: lỗi đã xác nhận** rằng API danh sách bị cắt 500 dòng và không trả tổng. Hạt đếm của thẻ và của API là một định nghĩa khi không truyền `warehouseId`. Màn hình tồn thấp còn thu về một kho; báo cáo smoke không ghi query string, nên phạm vi kho của đúng request 500 dòng chưa chứng minh được.

### 1. Đường đi

**5.373 — thẻ “SP tồn thấp”**

| Bước | Vị trí |
|---|---|
| UI | `DashboardPage.tsx` — `inventory.lowStockProductCount`, hint “Số mã SP × kho dưới ngưỡng (không phải số lô)”, link `/inventory/low-stock` |
| API | `GET /api/dashboard/overview`, `lowStockThreshold` mặc định 10 |
| Query | `DashboardRepository`, `lowStockProductSql` |

Hạt đếm là cặp `(p.id, w.id)`:

- sản phẩm `deleted_at IS NULL`, `status = 1`;
- kho `deleted_at IS NULL`, `status = 1`;
- `COALESCE(SUM(b.quantity_available), 0) <=` ngưỡng hiệu lực;
- không `LIMIT`.

Cặp không có lô vẫn được `COALESCE` thành 0 và vẫn đếm nếu 0 không lớn hơn ngưỡng. Vì vậy 5.373 có thể lớn hơn 3.024 sản phẩm catalog: đó là số cặp sản phẩm × kho, không phải số mã sản phẩm.

Ngưỡng trong `LowStockThresholdSql.EffectiveMinStockExpr`:

```text
COALESCE(p.min_stock_qty, w.min_stock_qty, c.min_stock_qty,
         tenant.settings default_min_stock_qty, @FallbackThreshold)
```

`@FallbackThreshold` là tham số API, mặc định 10. `LowStockThresholdSql.SystemFallback` cũng là 10 khi tham số âm.

**500 — `GET /api/inventory/stock/low-stock`**

| Bước | Vị trí |
|---|---|
| API | `StockController.LowStock` — trả `IReadOnlyList<LowStockProductDto>`, không có `total`, không có `page` |
| Service | `InventoryService.GetLowStockProductsAsync` |
| Query | `InventoryRepository.GetLowStockProductsAsync` |

Cùng join, cùng `HAVING`, cùng biểu thức ngưỡng, cùng fallback mặc định 10. Cuối query:

```sql
ORDER BY TotalQuantity ASC, w.warehouse_name, p.product_name
LIMIT 500
```

500 là trần cứng của câu lệnh, không phải tổng bản ghi và không phải cỡ trang có thể sang trang 2. Response là một mảng. Đủ 500 phần tử không cho biết còn bao nhiêu phía sau.

Thẻ “Lô tồn thấp” (`lowStockBatchCount`) là số lô có `quantity_available > 0` và số lượng lô `<=` ngưỡng. API low-stock không đếm chỉ số đó. Smoke so 5.373 với API sản phẩm, không so với số lô.

### 2. Phần đã chứng minh và phần chưa đủ

Đã chứng minh: 500 không phải tổng 5.373. Cùng một định nghĩa cặp sản phẩm × kho, API không thể trả đủ các cặp đó.

Chưa ghi trong smoke: request 500 dòng có `warehouseId` hay không.

`LowStockPage.tsx` sau khi tải kho sẽ gọi API với kho mặc định, hoặc kho đầu danh sách. Một request từ màn hình đó là một kho và vẫn bị `LIMIT 500`. Request không có `warehouseId` là mọi kho active, cùng phạm vi với thẻ, rồi cắt 500. Cả hai cách đều không thể hiện 5.373. Không kết luận request smoke thuộc cách nào.

### 3. Phạm vi sửa tối thiểu

Giữ nguyên biểu thức ngưỡng và hạt đếm.

API `low-stock` trả kết quả phân trang có `total`, bỏ `LIMIT 500` cố định, `page` / `pageSize` theo kiểu `GetStockBatchesAsync` (trần cỡ trang có giới hạn, tổng không bị cắt). `total` khi không truyền `warehouseId` phải bằng `lowStockProductCount` với cùng `lowStockThreshold`.

Màn `/inventory/low-stock` cần nói rõ đang lọc một kho, hoặc có lựa chọn mọi kho. Không đổi hint thẻ Tổng quan: hint đã nói cặp mã × kho.

Không sửa `lowStockBatchCount` trong patch này.

### 4. Kiểm thử không ghi dữ liệu vận hành

Fixture local với hơn 500 cặp dưới ngưỡng, ít nhất hai kho:

- `GET /api/inventory/stock/low-stock` không tham số kho: `total` bằng `lowStockProductCount`, trang đầu không dài hơn `pageSize`, trang sau vẫn có dữ liệu.
- Cùng request với một `warehouseId`: `total` không lớn hơn tổng không lọc kho, và bằng số cặp của riêng kho đó.
- Một sản phẩm không có lô, ngưỡng mặc định 10: cặp đó có trong cả hai đường, vì tồn coi là 0.
- `defaultThreshold` khác nhau giữa hai API làm hai số khác nhau. Test dùng cùng một giá trị.

### 5. Rủi ro hồi quy

Bỏ `LIMIT 500` mà trả một mảng toàn bộ sẽ tải hàng nghìn dòng ra màn hình và vào nháp đặt hàng (`suggestRequisition` dùng đúng mảng đang tải). Phân trang tránh việc đó. Nháp đặt hàng phải nói đang lấy trang hiện tại hay toàn bộ kết quả lọc; không âm thầm chỉ lấy 500 dòng đầu theo tồn tăng dần.

Ngưỡng theo sản phẩm, kho, danh mục và `tenants.settings.default_min_stock_qty` ưu tiên hơn số 10. Sửa phân trang không được thay thứ tự `COALESCE`.

---

## Việc không làm trong kế hoạch này

- Không sửa mã, không tạo patch, không commit, không deploy, không restart.
- Không chạy SQL, API hay script trên `NT_XUANHOA`.
- Không đụng banner lệch NSX/HSD. Smoke ghi đó là dữ liệu vận hành, chưa mở màn sửa.
- Không kết luận FEFO lúc bán.

Dừng tại bản kế hoạch này để Architect xét trước khi sửa mã.
