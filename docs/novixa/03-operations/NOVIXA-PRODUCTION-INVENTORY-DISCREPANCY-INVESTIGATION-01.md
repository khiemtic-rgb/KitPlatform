# NOVIXA — Điều tra lệch tồn kho Production sau đợt 04/11/2026 (01)

Thời điểm điều tra: 05/11/2026, 08:58–09:15 (giờ VN). Máy chủ: VPS A `103.200.23.229`, DB `novixa_prod`. Không truy cập VPS B.
Cách làm: chỉ đọc. Mọi truy vấn chạy trong `BEGIN TRANSACTION READ ONLY … ROLLBACK`. Không INSERT/UPDATE/DELETE, không migration, không restart, không tạo phiếu.
Bản đang chạy: Pharmacy V1 `521fdeb3` (swap 22:34:45 ngày 04/11), rồi Staff V1 `89adfe85` (swap 22:42:47).

## 1. Executive Summary

**Đợt triển khai không làm sai số tồn trong cơ sở dữ liệu.** Có bằng chứng ba lớp:

- Tồn của cả 5.561 lô Xuân Hòa khớp đúng sổ cái biến động (`stock_movements`): 0 lô lệch.
- So với bản sao lưu lúc 22:34:01 (trước migration và trước swap), 37.261 dòng sổ kho cũ còn nguyên, không dòng nào bị sửa hay xóa. Không lô nào đổi kho, sản phẩm, số lô, hạn dùng hay giá vốn. 30 lô có số lượng thay đổi, và cả 30 đều đúng bằng các đơn bán phát sinh sau đó.
- Sau triển khai chỉ có giao dịch bán: 19 đơn, 33 dòng phân bổ. Số lượng × hệ số quy đổi = phân bổ FEFO = dòng sổ kho, không lệch. Không có phiếu nhập, trả, chuyển, kiểm kê hay điều chỉnh nào.

Không có dấu hiệu migration 411–437 động vào tồn. Các migration này chỉ tạo bảng và quyền mới cho kiến thức, phác đồ và đào tạo, cùng 9 chỉ mục báo cáo. Riêng 411 thêm một cột cho phiếu trả.

**Thứ thay đổi là cách hiển thị và cách tính trên một số màn hình.** Bản mới có bốn thay đổi, mỗi cái đều có thể khiến người dùng thấy "số không khớp":

1. POS không còn tính lô đã hết hạn vào số bán được. Màn Tồn kho Admin vẫn tính. Có 3 sản phẩm, 4 lô bị ảnh hưởng (mục 4.2).
2. Báo cáo Nhập–Xuất–Tồn có thêm hai cột "Kiểm kê tăng/giảm". Hai cột này đã nằm sẵn trong Nhập/Xuất. Nếu cộng chúng thêm một lần, 582/1.948 dòng tháng 10 sẽ trông như lệch (mục 4.3).
3. Nút "Xuất Excel" mới ở màn Tồn kho phân trang trên một thứ tự sắp xếp không duy nhất. Khi chọn "Tất cả kho", file có thể lặp hoặc sót vài lô; lần mô phỏng thật cho thấy 2 lô lặp và 2 lô sót trên 2.506 (mục 4.4).
4. Màn Tồn thấp, báo cáo Cận hạn và ô cận hạn trên Dashboard bỏ qua lô đã hết hạn. Màn Tồn thấp giờ phân trang 20 dòng.

Ngoài ra còn một bất thường dữ liệu có từ trước, không do đợt này gây ra: 577 lô vẫn còn tồn nhưng thuộc 371 sản phẩm đã xóa từ 26–28/07. Phần lớn là lô `TON-DAU-SAPO` với số lượng phi thực tế, ví dụ 350.022 hộp. Chúng bị ẩn khỏi màn Tồn kho (mục 4.5).

Chưa có thông tin chủ nhà thuốc nhìn thấy lệch ở màn hình nào và trên SKU nào. Vì vậy các giải thích ở mục 1–3 trên chỉ ở mức **PROBABLE** cho lời phản ánh, dù bản thân từng thay đổi là **CONFIRMED**.

## 2. Phạm vi và thời điểm

| Mốc (giờ VN) | Sự kiện |
|---|---|
| 04/11 14:22 → 18:33 | Phiếu chuyển TR-000089/090/091 (WH_MAIN ↔ WH_CN02) được tạo lúc 14h và hoàn tất lúc 18:33:01–18:33:07: 110 dòng sổ kho, khoảng 790 đơn vị. Việc này do người dùng thực hiện, trước deploy. |
| 04/11 21:xx | Đơn bán cuối ngày |
| 04/11 22:34:01 | Sao lưu `novixa_prod_20261104_223401.sql.gz` |
| 04/11 22:34:06–10 | Migration Pharmacy (16 file) |
| 04/11 22:34:45 | Swap API/Admin Pharmacy |
| 04/11 22:42:07 / 22:42:47 | Sao lưu thứ hai; migration Staff (434, 435, 437); swap Staff |
| 05/11 06:00 → 09:00 | 19 đơn bán, 30 dòng sổ kho |

Từ 22:00 ngày 04/11 đến 06:00 ngày 05/11 không có giao dịch kho nào. Tenant có dữ liệu kho: `NT_XUANHOA` (2 kho, 5.561 lô) và `DEMO_PHARMACY` (1 kho, 27 lô). Không tenant nào khác có lô.

## 3. Quy mô sai lệch

| Loại | SKU | Lô | Kho | Bản chất |
|---|---|---|---|---|
| Tồn DB lệch sổ cái | 0 | 0 | 0 | Không có |
| Tồn DB đổi do deploy | 0 | 0 | 0 | Không có (so với backup) |
| POS khác Admin do lô hết hạn | 3 | 4 | 2 | Hiển thị, theo chủ đích |
| Báo cáo NXT dễ đọc nhầm | 582 dòng SP×kho (T10); 18 dòng (01–05/11) | — | 2 | Trình bày |
| Xuất Excel lặp/sót lô ("Tất cả kho") | 2 lô sót, 2 dòng lặp (một lần chạy) | 2 | 1 | Lỗi phân trang |
| Lô còn tồn của SP đã xóa (có từ trước) | 371 | 577 | 2 | Dữ liệu cũ từ đợt nhập Sapo |

## 4. Truy vết

### 4.1 Đối chiếu trước và sau deploy (toàn bộ Xuân Hòa)

So bản sao lưu 22:34:01 với DB lúc 09:00 ngày 05/11:

- Lô: 5.561 trước, 5.561 sau.
- Dòng sổ kho tạo trước 22:34:01: 37.261 trong backup, 37.261 trên DB, 0 dòng khác nhau.
- Thuộc tính lô (kho, SP, số lô, HSD, giá vốn) đổi: 0.
- Lô đổi số lượng: 30. Phần không giải thích được bằng giao dịch sau backup: 0.
- 228 lô có `updated_at` sau 18:00 ngày 04/11: cả 228 đều có dòng sổ kho tương ứng (phiếu chuyển 18:33 và đơn bán).

### 4.2 POS và Admin khác nhau vì lô hết hạn

Trước bản V1, POS vẫn cho bán lô quá hạn. Từ `521fdeb3`, `SellableLotRules`/`SellableBatchRules` (`expiry_date IS NULL OR expiry_date >= CURRENT_DATE`) loại lô hết hạn khỏi các truy vấn sau:
- `GetPosStockByUnitAsync`, `GetPosStockBulkAsync`, `SearchPosProductsAsync`;
- kế hoạch FEFO (`BuildFifoPlansAsync`, `BatchResolver`);
- Tồn thấp, Cận hạn, Dashboard.

Màn Tồn kho Admin (`GetStockProductsAsync`/`GetStockBatchesAsync`) không đổi: vẫn hiển thị mọi lô còn tồn.

| SKU | Sản phẩm | Kho | Lô hết hạn | HSD | ĐVT | Tồn sổ lô đó | Tồn Admin (SP×kho) | Tồn POS bán được | Chênh |
|---|---|---|---|---|---|---|---|---|---|
| PVN1229 | Xi lanh 1 | WH_MAIN | 020326 | 31/03/2026 | Cái | 194 | 229 | 35 | 194 |
| PVN1229 | Xi lanh 1 | WH_CN02 | 020326 | 31/03/2026 | Cái | 28 | 28 | 0 | 28 |
| PVN1239 | Xi lanh 10ml/cc | WH_MAIN | 090526 | 31/05/2026 | Cái | 92 | 110 | 18 | 92 |
| PVN4153 | Pro Calcitrate D3K2 | WH_MAIN | 60551 | 31/07/2026 | Lọ | 2 | 3 | 1 | 2 |

Cột "Tồn Admin" bằng lô hết hạn cộng các lô còn hạn cùng kho. Sau deploy, 33 dòng phân bổ bán đều rơi vào lô còn hạn: 0 lô hết hạn. Log API không có lần nào báo "Lô đã hết hạn" hay "Không đủ tồn kho".

### 4.3 Báo cáo Nhập–Xuất–Tồn

`ReportsRepository` (bản V1) thêm hai cột `AdjustmentInQty`/`AdjustmentOutQty`. Công thức tồn cuối không đổi: `Tồn cuối = Tồn đầu + Nhập − Xuất`, trong đó Nhập/Xuất đã gồm kiểm kê. Ghi chú bộ lọc có nói rõ điều này, nhưng trên lưới số liệu, hai cột mới đứng cạnh nhau như các khoản riêng.

| Kỳ | Dòng báo cáo | Dòng có kiểm kê | Kiểm kê tăng | Kiểm kê giảm |
|---|---|---|---|---|
| Tháng 10/2026 | 1.948 | 582 | 951 | 18.801 |
| 01–05/11/2026 | 676 | 18 | 24 | 276 |

Nếu người đọc lấy `Tồn đầu + Nhập − Xuất + Kiểm kê tăng − Kiểm kê giảm`, mọi dòng có kiểm kê đều lệch với Tồn cuối. Số Tồn cuối thì vẫn đúng với sổ cái, do mục 4.1 đã chứng minh tồn từng lô khớp sổ cái.

### 4.4 Xuất Excel ở màn Tồn kho

`StockListPage` (mới) gom `/inventory/stock/batches` theo trang 100 dòng. Truy vấn sắp `ORDER BY b.expiry_date NULLS LAST, p.product_name, b.batch_number`, có từ trước V1. Bộ khóa này không duy nhất: 535 nhóm, tức 1.070 lô, có cùng HSD, tên SP và số lô ở cả hai kho do chuyển kho. Với LIMIT/OFFSET, PostgreSQL không giữ thứ tự giữa các dòng hòa nhau qua các truy vấn.

Mô phỏng đúng cách file Excel tải, mỗi trang một truy vấn, trên dữ liệu thật:

| Phạm vi | Lô cần có | Lô tải về | Dòng lặp | Lô sót |
|---|---|---|---|---|
| Tất cả kho | 2.506 | 2.506 | 2 | 2 |
| WH_MAIN | 1.563 | 1.563 | 0 | 0 |
| WH_CN02 | 943 | 943 | 0 | 0 |

Lô bị sót trong lần chạy đó, cả hai đều thuộc WH_MAIN: PVN747 Tomax genta gel lô 010726, HSD 31/07/2028, 7 tuýp; PVN1260 XISAT Xanh lô 0180526, HSD 31/05/2029, 10 lọ. Mỗi lần xuất có thể khác.

Cột tổng tồn theo SP trong file lấy từ truy vấn theo SP và vẫn đúng; chỉ cột "Chi tiết lô" có thể lặp hoặc sót. Tải theo SP (`ORDER BY p.product_name`) hiện không có tên trùng nên không lệch, nhưng cũng chưa có khóa phụ.

### 4.5 Lô còn tồn của sản phẩm đã xóa (có từ trước, không do V1)

| Kho | Lô | SP | Số lượng | Xóa SP trong khoảng |
|---|---|---|---|---|
| WH_MAIN | 343 | 340 | 3.083.954 | 26/07–18/10 |
| WH_CN02 | 234 | 234 | 746.265 | 26/07–18/09 |

Đa số bị xóa ngày 26–28/07, trong đợt dọn dữ liệu nhập Sapo. Ví dụ: PVN2239 Homtamin gingseng lô `TON-DAU-SAPO` 350.022; PVN2152 Zilgo 102M 311.469; PVN2936 Lipvar 20mg 246.667.

Màn Tồn kho, Tồn thấp và các báo cáo có `p.deleted_at IS NULL` không hiển thị nhóm này. Tổng `quantity_available` thô ở DB vì thế lớn hơn số trên màn hình. Không có SP nào bị xóa sau 18:00 ngày 04/11.

### 4.6 Các kiểm tra khác: không thấy vấn đề

- **Trùng hoặc idempotency:** 4 nhóm "trùng" ở phiếu chuyển TR-000025 (14/09) và TR-000053 (15/10) thực chất là hai dòng hàng cùng lô trên một phiếu; tổng sổ kho bằng đúng số trên phiếu. 4 nhóm `COST_REVALUE` (23–24/09) mang số lượng 0, là hai lần sửa giá liên tiếp.
- **Đơn không ghi kho:** 0 trên 598 đơn hoàn tất từ 01/11.
- **Log API sau swap:** không có deadlock, lỗi serialize, `duplicate key` hay vi phạm ràng buộc tồn âm, và không có lỗi 5xx trên đường dẫn kho, bán hay báo cáo. Các lỗi có trong log, gồm `NotSupportedException` ở kênh realtime Customer App và bảng `pack_sales.facebook_settings` chưa có, đều đã có từ trước với tần suất cao hơn.
- **Múi giờ:** máy chủ và DB đều dùng `Asia/Ho_Chi_Minh`, nên `DateTime.Today` trong C# và `CURRENT_DATE` trong SQL cùng một ngày.

## 5. Đối chiếu trước/sau deploy

Như mục 4.1: dữ liệu kho không bị đợt deploy làm thay đổi. Thứ khác đi sau deploy là logic hiển thị và tính toán:

| Màn hình / API | Trước V1 | Sau V1 |
|---|---|---|
| POS: tìm SP, tồn theo ĐVT | Gồm lô hết hạn | Bỏ lô hết hạn |
| Bán hàng (FEFO) | Có thể lấy lô hết hạn | Bỏ qua lô hết hạn; báo lỗi nếu chỉ còn lô hết hạn |
| Tồn thấp | Tính cả lô hết hạn, tối đa 500 dòng | Bỏ lô hết hạn, 20 dòng/trang |
| Cận hạn (báo cáo, Dashboard) | Gồm lô đã quá hạn | Chỉ lô còn hạn đến mốc |
| NXT | 3 cột Nhập/Xuất/Tồn cuối | Thêm 2 cột kiểm kê (đã nằm trong Nhập/Xuất) |
| Tồn kho Admin | Không có Xuất Excel | Có Xuất Excel (lỗi phân trang, mục 4.4) |
| Kiểm kê (duyệt) | Chênh = số đếm − tồn hiện tại | Chênh = (số đếm − lượng xuất/nhập của lô kể từ lúc đếm) − tồn hiện tại. Chưa có phiếu nào duyệt theo cách mới. |

## 6. Nguyên nhân đã chứng minh (CONFIRMED)

- **C1. Không có sai lệch dữ liệu kho do deploy.** Bằng chứng ở mục 4.1 và 4.6.
- **C2. POS hiển thị ít hơn Admin ở 3 SKU, 4 lô**, do quy tắc mới loại lô hết hạn (mục 4.2). Đây là thay đổi chủ đích; số liệu trong DB vẫn đúng.
- **C3. Xuất Excel có thể lặp hoặc sót lô** khi chọn "Tất cả kho", do truy vấn phân trang thiếu khóa sắp xếp duy nhất (mục 4.4). Đây là lỗi thật, ở mức file xuất, không ảnh hưởng DB.
- **C4. 577 lô của SP đã xóa còn tồn phi thực tế** từ tháng 7 (mục 4.5). Không do V1.

## 7. Nguyên nhân còn nghi vấn

- **P1 (PROBABLE).** Chủ nhà thuốc đọc báo cáo NXT với hai cột kiểm kê mới và cộng chúng thêm một lần (mục 4.3). Nếu đúng vậy, 582 dòng tháng 10 sẽ trông như lệch, khớp với mô tả "lệch hàng loạt". Cần hỏi chủ nhà thuốc đã xem màn nào.
- **P2 (PROBABLE).** Chủ nhà thuốc so file Xuất Excel hoặc màn POS với màn Tồn kho (C2, C3).
- **U1 (UNCONFIRMED).** Lệch giữa tồn thực tế trên kệ và tồn sổ. Cần chủ nhà thuốc xác nhận riêng, xem mục 10.4; chưa có số đếm thực tế nào sau 04/11.
- **U2 (UNCONFIRMED), là rủi ro chứ chưa xảy ra.** Phiếu nháp ADJ-000080 (từ 23/09) có dòng PVN1566 Nexium 10mg lô `TON-DAU-SAPO`: tồn hệ thống 120.020, thực tế 0, chênh −120.020. Tồn sổ hiện là 0. Nếu ai đó mở và duyệt phiếu này, cần xem lại trước.

## 8. Ảnh hưởng

- **Bán hàng:** không bị chặn sai. Sau deploy không có lần nào bị từ chối vì thiếu tồn hay lô hết hạn. Lô hết hạn của Xi lanh 1 tại WH_CN02 khiến kho này hiển thị 0 bán được.
- **FEFO:** đúng thứ tự và không dùng lô hết hạn: 33/33 dòng phân bổ sau deploy.
- **Báo cáo:** Tồn cuối NXT đúng. Hai cột mới dễ đọc nhầm. Cận hạn ít dòng hơn vì đã bỏ lô quá hạn.
- **Giá trị tồn kho:** không đổi do deploy. Nếu có công cụ ngoài tính tổng thô từ DB mà không lọc SP đã xóa, giá trị sẽ phình rất lớn do mục 4.5.

## 9. Tenant bị ảnh hưởng

- `NT_XUANHOA`: chỉ có các tác động hiển thị và báo cáo ở trên; dữ liệu không lệch.
- `DEMO_PHARMACY`: 5 lô (LOT2026A/C, LOT2027B, LOTAMOX01, LOTVITC01) có tồn khác sổ cái, tổng 6.600. Đây là dữ liệu seed từ 27/07; lần sửa cuối là 01/11 lúc 06:12, trước deploy. Không do V1.
- Không tenant nào khác có dữ liệu kho.

## 10. Phương án đề xuất (chưa thực hiện)

1. **Xác nhận với chủ nhà thuốc** màn hình, SKU và lô cụ thể đang thấy lệch, và đang so với cái gì: POS, Excel, báo cáo NXT hay số đếm trên kệ. Chưa cần kiểm kê trên hệ thống.
2. **Báo cáo NXT:** đổi tiêu đề thành "trong đó: Kiểm kê tăng/giảm", hoặc tách nhóm cột cho rõ là số đã nằm trong Nhập/Xuất. Chỉ sửa trình bày, không đổi số.
3. **Xuất Excel và API danh sách lô:** thêm khóa phụ duy nhất `b.id` vào `ORDER BY` của `GetStockBatchesAsync`, và `p.id` vào `GetStockProductsAsync`. Trong lúc chờ bản sửa, xuất theo từng kho (đã kiểm: không lệch) hoặc dùng báo cáo NXT.
4. **Danh sách nhờ chủ nhà thuốc đếm tay, không nhập vào hệ thống:**
   - PVN1229 Xi lanh 1, lô 020326 (HSD 31/03/2026): WH_MAIN sổ 194, WH_CN02 sổ 28.
   - PVN1239 Xi lanh 10ml/cc, lô 090526 (HSD 31/05/2026): WH_MAIN sổ 92.
   - PVN4153 Pro Calcitrate D3K2, lô 60551 (HSD 31/07/2026): WH_MAIN sổ 2.
   - Hàng còn trên kệ thì làm phiếu xuất hủy theo quy trình; hết hàng rồi thì kiểm kê về 0. Cả hai việc đều do chủ nhà thuốc quyết và thực hiện sau khi có hướng dẫn.
5. **577 lô của SP đã xóa:** lập danh sách chỉ đọc gửi chủ nhà thuốc xác nhận. Sau đó, nếu được duyệt, viết migration hoặc phiếu hủy tồn đầu kỳ có guard `tenant_code = 'NT_XUANHOA'`, có backup, chạy trong khung giờ. Không tự sửa.
6. **ADJ-000080:** đề nghị chủ nhà thuốc hủy phiếu nháp này trên giao diện, hoặc ghi rõ không duyệt.
7. **Hỗ trợ thêm:** nếu cần, đối chiếu thêm qua API bằng tài khoản Xuân Hòa ở chế độ chỉ đọc, khi chủ nhà thuốc đồng ý.

## Phụ lục: bằng chứng

Các truy vấn và kết quả nằm trong `.tmp/inv-investigation-20261105/`:
- `q1-overview` (sổ cái, biến động theo ngày và giờ, trùng lặp, kiểm kê, lô hết hạn, đơn không ghi kho);
- `compare-backup` (đối chiếu với backup);
- `simulate-export` (mô phỏng Xuất Excel);
- `q2-hidden-lots`, `q3-reports-pos`, `q4-dup-transfers`;
- `logs`, `logs2` (log API, chỉ đọc).

Các file tạm trên máy chủ đã được xóa sau mỗi lần chạy.
