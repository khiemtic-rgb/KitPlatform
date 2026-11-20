# NOVIXA-FIX-PACKAGE-01

Cô lập patch A/B/D để Architect duyệt một commit riêng. Chưa commit. Không đụng tenant `NT_XUANHOA`. Không migration, không seed, không deploy.

Ngày chạy: 2026-10-13. Postgres local `kitplatform`. Fixture test vẫn dùng tenant mã `FIXIMPL01_` và từ chối xóa tenant khác.

## Kết quả

| Mục | Kết quả |
|---|---|
| A — `DateTimeKind` | Giữ nguyên. Diff đã đọc lại, không có hunk ngoài phạm vi. |
| B — cận dưới HSD | Giữ nguyên. Diff đã đọc lại, không có hunk ngoài phạm vi. |
| D — tie-breaker phân trang | **Đã thêm** `p.id, w.id` vào `ORDER BY`. |
| Đếm `total` và lấy trang | Cùng mảnh SQL `grouped`: tenant, lọc kho, `GROUP BY`, `HAVING`. Hạt là sản phẩm × kho. |
| Test khóa sắp xếp trùng | **PASS** |
| 7 test cũ + test mới | **PASS** — 8/8 |
| Build API | **PASS** — 0 lỗi, 12 cảnh báo `NU1903` |
| OpenAPI low-stock | `swagger.json` là nguồn của `openapi:types`. Contract đã là object `items`, `total`, `page`, `pageSize`. |
| `openapi:sync` | Không chạy. Script xuất spec từ chối ghi đè nếu contract low-stock không còn là object phân trang. |

## D — phân trang

Hạt một dòng là cặp `products.id` × `warehouses.id`. Cả hai cột nằm trong `GROUP BY`. Câu đếm là `COUNT(*)` của `SELECT p.id, w.id` trên cùng mảnh `grouped` với câu lấy trang.

Thứ tự nghiệp vụ giữ nguyên. Tie-breaker đứng sau:

```text
ORDER BY TotalQuantity ASC, w.warehouse_name, p.product_name, p.id, w.id
```

`w.warehouse_name` và `p.product_name` không phải khóa duy nhất. Không có tie-breaker, `LIMIT`/`OFFSET` có thể lặp hoặc bỏ một cặp khi nhiều dòng cùng số lượng, cùng tên kho và cùng tên sản phẩm.

Test `Low_stock_tied_sort_keys_cover_every_page_without_duplicate_or_loss`:

- 8 sản phẩm cùng tên `San pham trung`, 3 kho cùng tên `Kho trung`, không có lô: 24 dòng, số lượng 0.
- 1 sản phẩm tồn 5 trên cả 3 kho: 3 dòng đứng sau vì `TotalQuantity ASC`.
- `pageSize` 5, `total` 27, sáu trang.
- Ghép trang: đúng 27 khóa `productId|warehouseId`, không trùng, không thiếu.
- Đi lại toàn bộ trang lần hai: cùng thứ tự khóa.
- 24 dòng đầu số lượng 0 và cùng tên; 3 dòng cuối số lượng 5.

## Test và build

```text
dotnet test tests/KitPlatform.Application.Tests/KitPlatform.Application.Tests.csproj --filter FullyQualifiedName~NovixaFixImplement01 --nologo
```

Exit code **0**. `Passed: 8, Failed: 0, Skipped: 0`, khoảng 2 giây. Bảy test cũ vẫn đạt. Test thứ tám là khóa sắp xếp trùng.

```text
dotnet build src/KitPlatform.Api/KitPlatform.Api.csproj --nologo -v q --no-restore
```

Exit code **0**. `Build succeeded.` **0 Error(s), 12 Warning(s).** Cả 12 cảnh báo là `NU1903` cho `Newtonsoft.Json` 10.0.3. Không có lỗi biên dịch C#.

## OpenAPI

Nguồn sinh schema client là `client/admin/openapi/swagger.json`.

| Lệnh | Việc |
|---|---|
| `npm run openapi:types` | `openapi-typescript ./openapi/swagger.json` ghi `src/shared/api/generated/api-schema.ts`. |
| `npm run openapi:spec` | `scripts/export-openapi.ps1`: build API, rồi `dotnet swagger tofile`. |
| `npm run openapi:sync` | spec rồi types. |

`GET /api/inventory/stock/low-stock` trong `swagger.json` nay trả `$ref` `PagedLowStockProductsResult`. Schema có `items`, `total`, `page`, `pageSize`. Query thêm `page` mặc định 1 và `pageSize` mặc định 20, khớp `StockController`.

Đã chạy generator thật, ra file tạm, không ghi đè nguyên `api-schema.ts`:

```text
npx --no-install openapi-typescript ./openapi/swagger.json -o %TEMP%\novixa-api-schema.generated.ts
```

`openapi-typescript` 7.13.0. Phần low-stock của file tạm khớp bản đã sửa trong repo, gồm vị trí schema theo thứ tự thành phần. Chênh lệch còn lại của file tạm là xóa `TransferItemDto.expiryDate` và `TransferItemDto.unitName`. Hai trường đó có trong `api-schema.ts` đã commit và không có trong `swagger.json`. Không áp xóa đó, vì nó đổi contract chuyển kho, ngoài patch này.

Không chạy `openapi:sync`. `swashbuckle.aspnetcore.cli` 6.9.0 không xuất được spec trên máy này:

- Không có runtime `net8.0`. CLI là app `net8.0`.
- Bật `DOTNET_ROLL_FORWARD=Major` thì CLI chạy trên `net10.0` nhưng ném `Startup`/`StartupProduction` không có trong `KitPlatform.Api`. API dùng minimal hosting.

Khi `tofile` thất bại, script cũ tải `http://localhost:5290/swagger/v1/swagger.json` và ghi đè cả file. Server cũ sẽ trả lại contract mảng.

`scripts/export-openapi.ps1` nay ghi bản xuất vào file tạm. Chỉ copy đè `swagger.json` khi low-stock `200` là `#/components/schemas/PagedLowStockProductsResult` và schema đó có `items`, `total`, `page`, `pageSize`. Đã gọi hàm kiểm tra trên `swagger.json` hiện tại: chấp nhận. Đã gọi trên một JSON low-stock kiểu mảng: từ chối, không ghi file đích.

## File dự kiến trong commit

Index trước đó có hai file `docs/architecture/` bị `.gitignore` nhưng đã được add. Đã bỏ khỏi index. File trên đĩa vẫn còn. Không xóa, không sửa nội dung.

`KitPlatform.Application.Tests.csproj` trên working tree còn ba dòng ngoài patch, phục vụ test chưa track khác trong cùng project: `Microsoft.Extensions.Options` 10.0.9, `System.IdentityModel.Tokens.Jwt` 8.14.0, và `ProjectReference` tới `KitPlatform.Api`. Index chỉ chứa `Dapper` 2.1.79 và `Npgsql` 10.0.3. Ba dòng kia vẫn nằm unstaged.

| File | Việc |
|---|---|
| `src/KitPlatform.Infrastructure/Dashboard/VietnamBusinessCalendar.cs` | A. `Kind=Utc`. |
| `src/KitPlatform.Infrastructure/Reports/ReportsDateHelper.cs` | A. Mốc mặc định `Kind=Utc`. |
| `src/KitPlatform.Infrastructure/Dashboard/DashboardRepository.cs` | B. Cận dưới HSD. |
| `src/KitPlatform.Infrastructure/Reports/ReportsRepository.cs` | B. Cận dưới HSD. |
| `src/KitPlatform.Infrastructure/Reports/ReportsService.cs` | B. Truyền ngày VN hôm nay. |
| `src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Infrastructure/Inventory/InventoryRepository.cs` | D. Đếm, phân trang, tie-breaker. |
| `src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Infrastructure/Inventory/InventoryService.cs` | D. Kẹp trang. |
| `src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Application/Inventory/IInventoryService.cs` | D. Chữ ký phân trang. |
| `src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Application/Inventory/InventoryDtos.cs` | D. `PagedLowStockProductsResult`. |
| `src/KitPlatform.Api/Controllers/Pharmacy/StockController.cs` | D. `page`, `pageSize`. |
| `client/admin/src/shared/api/inventory.api.ts` | D. Đọc object phân trang. |
| `client/admin/src/shared/api/inventory.types.ts` | D. `PagedLowStockProducts`. |
| `client/admin/src/shared/api/generated/api-schema.ts` | D. Contract low-stock. |
| `client/admin/src/modules/inventory/LowStockPage.tsx` | D. Phạm vi kho, `total`, đề xuất theo trang. |
| `client/admin/src/shared/i18n/locales/vi-VN/inventory.json` | D. Nhãn. |
| `client/admin/src/shared/i18n/locales/en-US/inventory.json` | D. Nhãn. |
| `client/admin/openapi/swagger.json` | D. Nguồn `openapi:types`. |
| `scripts/export-openapi.ps1` | Chặn sync ghi lại contract mảng. |
| `tests/KitPlatform.Application.Tests/NovixaFixImplement01Tests.cs` | Bảy test cũ và test khóa trùng. |
| `tests/KitPlatform.Application.Tests/KitPlatform.Application.Tests.csproj` | Chỉ `Dapper` 2.1.79 và `Npgsql` 10.0.3 trong index. |
| `docs/novixa/03-operations/NOVIXA-FIX-PACKAGE-01-REPORT.md` | Báo cáo này. |

## Chưa xác minh

- Không mở lại `/inventory/low-stock` trên trình duyệt trong lượt này. Lượt `NOVIXA-FIX-VERIFY-01` đã xem UI trước khi có tie-breaker.
- Không chạy `npm run build` và không chạy `tsc`. 382 lỗi TypeScript cũ không xử lý.
- Không chạy `openapi:sync` và không tải spec từ API đang nghe cổng 5290.
- Không gọi lại Loss Prevention, Growth Desk, Shift Checklist trên database.
- Không đọc, không ghi `NT_XUANHOA`.
- `docs/novixa/03-operations/NOVIXA-FIX-IMPLEMENT-01-REPORT.md` và `NOVIXA-FIX-VERIFY-01-REPORT.md` không nằm trong index.

Chưa commit. Chờ Architect duyệt `git diff --cached --stat` và `git diff --cached --name-status`.
