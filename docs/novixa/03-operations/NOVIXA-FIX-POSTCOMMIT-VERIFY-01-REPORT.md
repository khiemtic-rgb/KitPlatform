# NOVIXA-FIX-POSTCOMMIT-VERIFY-01

Xác minh sau commit rằng khoảng ngày dashboard/báo cáo dùng mốc UTC, danh sách sắp hết hạn không gồm thuốc đã hết hạn, phân trang tồn kho thấp không lặp hoặc bỏ sót khi khóa sắp xếp trùng, và frontend không thêm lỗi type mới từ patch.

Ngày chạy: 2026-10-13. Postgres local `Host=localhost`, database `kitplatform`. Phiên UI là `DEMO_PHARMACY`. Fixture test dùng tenant mã `FIXIMPL01_`. Không đọc, không ghi `NT_XUANHOA`.

## Commit SHA đã xác minh

`0d8568ae6e57003ae0f5e2813f1a35fd000ba6be`

`git rev-parse HEAD` trả đúng SHA đó. `git log -1`: parent `b64f6772213dd0e095a49ae9a84983b37116f696`, ngày `2026-10-13 12:43:03 +0700`, subject `fix(novixa): page low-stock without dropping tied rows`. Nhánh `main` đang ahead `origin/main` 5 commit. Không push.

File của patch A/B/D trong commit khớp working tree, trừ `tests/KitPlatform.Application.Tests/KitPlatform.Application.Tests.csproj`: working tree còn 3 dòng unstaged đã ghi ở `NOVIXA-FIX-PACKAGE-01` (`Microsoft.Extensions.Options` 10.0.9, `System.IdentityModel.Tokens.Jwt` 8.14.0, `ProjectReference` tới `KitPlatform.Api`). File test `NovixaFixImplement01Tests.cs` không lệch so với commit. Không `git add`, không đổi index.

## Kết quả

| Mục | Kết quả |
|---|---|
| HEAD đúng SHA | **PASS** |
| A — mốc UTC | **PASS** — 5 test lịch/báo cáo/Tổng quan trong bộ 8 |
| B — sắp hết hạn không gồm lô đã hết hạn | **PASS** — test fixture `FIXIMPL01_` và báo cáo demo 1 lô còn trong cửa sổ |
| D — phân trang khi khóa sắp xếp trùng | **PASS** — test fixture và UI `/inventory/low-stock` sau tie-breaker |
| Frontend type | **PASS** trong phạm vi file patch. `tsc` vẫn 382 lỗi, cùng hồ sơ với `NOVIXA-FIX-VERIFY-01`. Không chạy `npm run build` |

## A — mốc UTC

Bộ `NovixaFixImplement01` chạy trên commit hiện tại, Postgres local, phiên `TimeZone=Asia/Ho_Chi_Minh` trong test phiên. Năm test khóa mốc:

| Test | Việc đã khóa |
|---|---|
| `Today_range_is_vietnam_calendar_day_as_utc` | `2026-10-13 03:15Z` ra `[2026-10-12 17:00Z, 2026-10-13 17:00Z)`, cả hai `Kind=Utc`. |
| `Rolling_and_month_ranges_keep_utc_kind` | Tuần và tháng giữ `Kind=Utc`. |
| `Report_default_range_is_utc_and_explicit_range_is_unchanged` | Thiếu `from`/`to` thì `Kind=Utc`. Đủ hai mốc thì giữ nguyên. |
| `Vietnam_session_shifts_unspecified_bounds_and_keeps_utc_bounds` | Cùng đồng hồ `17:00`, `Unspecified` và `Utc` chọn hai phía ngược nhau của ngày lịch VN. |
| `Dashboard_order_list_and_revenue_share_the_vietnam_day_including_returns` | Đơn trước nửa đêm VN không vào Tổng quan, danh sách hay dòng báo cáo của ngày lịch hôm nay. Cửa sổ tháng/tuần Cockpit theo `Kind=Utc`. |

Trên UI `DEMO_PHARMACY`, Tổng quan ghi doanh thu 7 ngày theo lịch VN và trục `07/10` … `13/10`. Hôm nay lịch VN là 13/10/2026. Doanh thu hôm nay trên demo là `0 ₫`; số 0 không tự chứng minh `DateTimeKind`. Mốc UTC được tính bằng các test trên.

## B — sắp hết hạn

Test `Near_expiry_keeps_today_through_cutoff_and_drops_expired_lots` đạt. Fixture `FIXIMPL01_`: hôm qua, `2026-03-31`, `2026-07-31` vắng khỏi báo cáo và `nearExpiryBatchCount`; đúng hôm nay, trong cửa sổ, và đúng ngày trần thì có. Ngày trần + 1 và tồn 0 thì không.

Trên UI demo, thẻ Tổng quan **Sắp hết HSD (30 ngày)** là **1**. Báo cáo `/reports/inventory/near-expiry`:

- Nhãn: `HSD trong 30 ngày tới · HSD trước 12/11/2026 · Kho Tất cả kho · Tạo lúc: 13/10/2026 13:02`.
- Một dòng: `AMOX500`, lô `55555`, HSD `31-10-2026`, kho Kho chính, tồn 236.
- `31-10-2026` nằm sau 13/10 và trước trần 12/11. Không có dòng HSD trước hôm nay trên báo cáo này.

Không mở lọc `expired` của màn tồn trong lượt này. Việc loại lô đã hết hạn được chứng minh bằng fixture, không bằng cách liệt kê toàn bộ lô hết hạn của demo.

## D — phân trang tồn kho thấp

Test `Low_stock_tied_sort_keys_cover_every_page_without_duplicate_or_loss` đạt: 27 khóa, `pageSize` 5, ghép trang không trùng không thiếu, lần đi lại thứ hai cùng thứ tự. Test `Low_stock_total_pages_under_exact_and_over_500` đạt.

`ORDER BY` trong commit: `TotalQuantity ASC, w.warehouse_name, p.product_name, p.id, w.id`.

UI `http://localhost:5173/inventory/low-stock`, tenant `DEMO_PHARMACY`, user `admin`. Không bấm Lưu nháp, Tạo đơn, Lưu ngưỡng chung, hay Ghi ngưỡng chung lên SP.

| Việc | Quan sát |
|---|---|
| Kho chính | `Đang lọc một kho: Kho chính.` Trang 1 và trang 2, mỗi trang 25 khóa `productId-warehouseId` duy nhất, giao nhau **0**. Mọi khóa cùng kho `22222222-2222-2222-2222-222222222201`. Nhãn `3187 bản ghi`. Dòng đầu trang 1 là `PVN651`; trang 2 là `PVN3329`. Quay lại trang 1, dòng đầu lại là `PVN651`. |
| Mọi kho | `Đang xem mọi kho.` Cột Kho và Chi nhánh hiện ra. `25/62675`, khớp thẻ Tổng quan **SP tồn thấp 62675**. Trang 1 và trang 2 mỗi trang 25 khóa, giao nhau **0**. Trang 1 kết thúc một sản phẩm ở kho kế trước; trang 2 bắt đầu cùng sản phẩm ở kho kế tiếp. Bấm Tải lại trên trang 2 giữ nguyên thứ tự 25 khóa. |
| Kho chi nhánh test | `Đang lọc một kho: Kho chi nhánh test.` `25/3988 bản ghi`. Nhỏ hơn 62675. |
| Đề xuất nhập | Mọi kho: nút không tạo nháp (`sessionStorage` chưa có draft). Một kho: form `/procurement/purchase-orders?requisition=1` có ghi chú `Đề xuất nhập từ cảnh báo tồn thấp · trang hiện tại · Kho chi nhánh test · 25/3988`, dòng form `items_0` … `items_24`, dòng đầu `PVN651`. Chữ trên form: `Đã điền 25 dòng từ đề xuất tồn thấp`. Không bấm Lưu nháp hay Tạo đơn. |

Lượt `NOVIXA-FIX-VERIFY-01`, trước tie-breaker, hai trang mọi kho dùng chung một khóa sản phẩm–kho. Lượt này giao nhau 0 và Tải lại không đổi thứ tự.

## Test mục tiêu

```text
dotnet test tests/KitPlatform.Application.Tests/KitPlatform.Application.Tests.csproj --filter FullyQualifiedName~NovixaFixImplement01 --nologo
```

Exit code **0**. `Passed: 8, Failed: 0, Skipped: 0, Total: 8`, khoảng 3 giây. Fixture `FIXIMPL01_` do test tạo và xóa; test từ chối xóa tenant không mang prefix đó.

Binary test biên dịch cả working tree. Các file A/B/D khớp commit. Ba dòng unstaged trong csproj test không nằm trong commit. Các file dirty khác của working tree có trong các project được biên dịch; assertion đi vào các hàm đã commit.

API local được bật vì cổng 5290 chưa nghe. `Ensure-ApiDev -SkipWatcher`, profile Development, `http://localhost:5290`. Build API trong bước đó: **0 Error(s)**. Không bật watcher. Vite `dev:vite` tại `http://localhost:5173`. Không restart dịch vụ production.

## Frontend

```text
npx tsc --noEmit --pretty false --incremental false
```

Thư mục `client/admin`. Exit code **2**. **382** dòng `error TS`, **51** file. Không có dòng nào thuộc `LowStockPage.tsx`, `inventory.api.ts`, `inventory.types.ts`, `api-schema.ts`, `inventory.json`.

Đối chiếu `NOVIXA-FIX-VERIFY-01`: cùng 382 lỗi, 51 file, `ContentFamixaSeriesTab.tsx` 179, `TS18048` 76, `TS2345` 66, `TS2580` 60, `TS6133` 46. Không sửa các lỗi này. Không chạy `npm run build`. Admin build **không** được coi là PASS.

## Việc đã không làm

- Không sửa source, không commit, không `git add`, reset, clean, stash, checkout.
- Không push, deploy, migration, seed.
- Không đọc hoặc ghi `NT_XUANHOA`.
- Không lưu phiếu, không lưu ngưỡng tồn thấp.
- Không chạy `npm run build`, không cài hoặc nâng dependency, không audit toàn repository.
- Không dọn working tree ngoài patch.
