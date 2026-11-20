# NOVIXA-PROD-SMOKE-NT_XUANHOA-01

**Kết luận tổng thể: `FAIL`**

Smoke test chỉ-đọc trên tenant đang vận hành. Có lỗi đối chiếu được tái hiện bằng bằng chứng. Kết luận này không có nghĩa toàn bộ NOVIXA đạt hoặc không đạt chất lượng production.

## 1. Phạm vi và thời điểm

| Hạng mục | Giá trị |
|---|---|
| Mã | NOVIXA-PROD-SMOKE-NT_XUANHOA-01 |
| Ngày | 2026-10-13 |
| Khung giờ | 10:11–10:25 +07 |
| Admin | https://admin.novixa.vn |
| API | https://api.novixa.vn |
| Phạm vi | S1–S6, chỉ đọc |
| Ngoài phạm vi | Tạo/sửa/xóa đơn, thanh toán, tồn kho, khách hàng, tài khoản, vai trò, migration, deploy, restart, sửa mã |

API health lúc 10:11:28 +07: `/api/health` = `ok`, `/api/health/db` = `ok`.

## 2. Tenant và vai trò đã kiểm tra

| Hạng mục | Kết quả |
|---|---|
| Tenant | `NT_XUANHOA` (xác nhận lúc đăng nhập API và trong phiên giao diện) |
| Tài khoản | `admin` |
| Vai trò | `ADMIN` |
| Quyền | 51 quyền, gồm `reports.read`, `reports.export`, `system.read`, `system.write`, `sales.read`, `inventory.read`, `catalog.read` |
| Chọn workspace | Không |

Header giao diện hiện tên `admin`. Mã tenant không hiện trên header; mã được xác nhận từ phiên đăng nhập.

Đã thấy menu: Tổng quan, Cockpit chủ NT, Bán hàng, Connect, Mua hàng, Kho hàng, Công nợ, Khách hàng, Sản phẩm, Báo cáo, Phát triển Nhân sự, Cấu hình.

Trang Tài khoản (`/system/users`) tải 5 tài khoản, tất cả trạng thái Hoạt động. Hai dòng chỉ hiện vai trò `ADMIN`. Ba dòng còn lại hiện tổ hợp vai trò gồm `STAFF`, `WAREHOUSE` và `CASHIER`. Không ghi tên đăng nhập, email hay số điện thoại.

## 3. Kết quả S1–S6

| Mã | Mong đợi | Thực tế | Trạng thái |
|---|---|---|---|
| S1 | Đăng nhập được và đang ở `NT_XUANHOA` | API và giao diện đăng nhập thành công. Tenant `NT_XUANHOA`, vai trò `ADMIN`. Tổng quan mở ra, lời chào `admin`. | `PASS` |
| S2 | Vai trò hiện tại thấy đúng các màn được phép; không đổi quyền | `ADMIN` vào được Tổng quan, Báo cáo, Kho, Khách hàng, Đơn bán, Tài khoản. Không thử chặn STAFF và không sửa vai trò. | `PASS` cho vai trò đã đăng nhập. Phủ định quyền STAFF: `NOT TESTED` |
| S3 | Đọc được sản phẩm, tồn, lô/HSD. Chỉ kết luận FEFO khi đủ bằng chứng | 3.024 sản phẩm; tồn theo lô có HSD và sắp theo HSD tăng dần. Báo cáo “HSD trong 30 ngày tới” trả về lô đã hết hạn. Chọn lô khi bán không được kiểm. | `FAIL` |
| S4 | Tra cứu khách chỉ-đọc, không đưa danh tính vào báo cáo | Danh sách 3.432 hồ sơ. Tìm theo mã khách nội bộ trả đúng 1 hồ sơ. Giao diện `/customer/list` tải được. | `PASS` |
| S5 | Đọc đơn đã có và đối chiếu các màn chỉ-đọc | Đơn ngày 13/10/2026 trên danh sách và báo cáo doanh thu cùng là 32. Tổng quan ghi 87. Chi tiết một đơn hoàn tất có lô và HSD. | `FAIL` |
| S6 | Ghi lỗi giao diện/API nếu có | Các trang đã mở không có request API HTTP ≥ 400 và không có thông báo lỗi. Lệch nghiệp vụ ghi ở S3 và S5. | `PASS` |

## 4. Bằng chứng và lỗi đã quan sát

Số tiền, tên khách, số điện thoại, email và thông tin đăng nhập không đưa vào báo cáo.

### S1 — Đăng nhập

- 10:11:28 +07: `POST /api/auth/login` với tenant `NT_XUANHOA` trả phiên hợp lệ. `GET /api/auth/me` lặp lại tenant `NT_XUANHOA`, username `admin`, vai trò `ADMIN`.
- Ngay sau đó, giao diện https://admin.novixa.vn/login, chế độ Mã cửa hàng, vào được Tổng quan.

### S3 — Sản phẩm, tồn, HSD

- `GET /api/catalog/products?page=1&pageSize=20`: tổng 3.024. Trang đầu đủ giá bán; 19/20 có mã vạch. Mã mẫu: `PVN650`, `PVN683`, `PVN4106`, `PVN2735`, `PVN655`.
- `GET /api/inventory/stock/products`: 1.402 sản phẩm có tồn. Trang 50 dòng có 26 sản phẩm nhiều hơn một lô.
- Tab **Theo lô (FEFO)** trên `/inventory/stock`: cột Kho, Mã SP, Lô, NSX, HSD, Tồn. Khoảng 116 trang × 20 dòng, khớp tổng API 2.308 lô còn tồn. Thứ tự HSD trên đầu trang: `31-03-2026`, `31-07-2026`, `31-01-2027`, `28-02-2027`.
- Sản phẩm `PVN2735` có 3 lô, HSD `2028-09-25` rồi hai lô `2030-12-31`, API trả theo HSD tăng dần.
- Lọc `expiry=expired`: đúng 2 lô còn tồn, `PVN1229` lô `020326` HSD `2026-03-31`, và `PVN4153` lô `60551` HSD `2026-07-31`.
- Lọc `expiry=within_1m`: 0 lô.
- `/reports/inventory/near-expiry`: nhãn **HSD trong 30 ngày tới · HSD trước 12/11/2026**, nhưng chỉ có hai dòng trên, HSD `31-03-2026` và `31-07-2026`. Cả hai đã qua ngày kiểm thử 13/10/2026.
- Tổng quan ghi **Sắp hết HSD (30 ngày): 2**, cùng hai lô đã hết hạn.
- Tổng quan ghi **Lô đang tồn: 2.884**. Màn tồn theo lô đếm khoảng 2.308. Hai số không khớp.
- Tổng quan ghi **SP tồn thấp: 5.373**. `GET /api/inventory/stock/low-stock` trả đúng 500 dòng.
- `/inventory/stock` có cảnh báo: một số lô đang có nhiều NSX hoặc HSD. Không mở bảng sửa lô.

### S4 — Khách hàng

- `GET /api/customers?page=1&pageSize=5`: tổng 3.432. Bản ghi có mã, tên và số điện thoại. Các giá trị này không ghi lại.
- Tìm lại bằng mã khách nội bộ của bản ghi đầu: tổng 1, khớp đúng mã đó.
- `/customer/list`: bảng có cột Mã KH, Họ tên, SĐT; phân trang 172 trang. Không gõ số điện thoại hay tên vào ô tìm.

### S5 — Đơn hàng

Cửa sổ lịch Việt Nam ngày 13/10/2026, đọc lúc khoảng 10:13 +07:

| Nguồn | Số đơn ngày 13/10/2026 |
|---|---|
| `/sales/orders`, lọc 13/10/2026–13/10/2026 | 32 |
| Báo cáo doanh thu theo ngày, dòng 13/10/2026 | 32 |
| Tổng quan, “Đơn bán hôm nay” | 87 |

Đối chiếu cửa sổ thời gian trên API đơn, cùng thời điểm:

| Cửa sổ | Số đơn |
|---|---|
| 13/10 00:00 đến 13/10 10:15 | 32 |
| 12/10 17:00 đến 13/10 00:00 | 55 |
| 12/10 17:00 đến 13/10 17:00 | 87 |

55 + 32 = 87. Tổng quan đang đếm thêm phần tối 12/10 từ 17:00, lệch 7 giờ so với ngày lịch mà danh sách đơn và báo cáo doanh thu đang dùng. Doanh thu “hôm nay” trên Tổng quan cũng không bằng doanh thu ngày 13/10 trên báo cáo. Số tiền không ghi.

Chi tiết một đơn trạng thái Hoàn tất ngày 13/10/2026: có cột Lô và HSD; một dòng có số lô và HSD `06-08-2028`. Khách trên đơn này trống. Không bấm In hóa đơn, Trả hàng hay Lưu ghi chú.

Đơn 7 ngày gần nhất trên API: 561 đơn. Không dùng làm mốc “hôm nay”.

### S6 — Lỗi giao diện và API

Các trang đã mở (Tổng quan, Đơn bán, Tồn kho, Khách hàng, Doanh thu theo ngày, Hàng cận hạn, Tài khoản) không phát sinh resource API có HTTP ≥ 400 và không thấy alert lỗi tải trang.

## 5. Mục `NOT TESTED` và lý do

| Mục | Lý do |
|---|---|
| FEFO lúc bán | Chọn lô khi bán chỉ chứng minh được bằng một đơn mới. Không tạo đơn. Thứ tự HSD trên màn tồn chỉ là cách hiển thị. |
| Chặn quyền STAFF | Chỉ có phiên `ADMIN`. Không đoán mật khẩu tài khoản khác và không đổi vai trò. |
| Sửa lô lệch NSX/HSD | Banner chỉ ra dữ liệu lệch. Mở bảng và áp ngày có thể ghi dữ liệu. |
| In hóa đơn, trả hàng, thanh toán, đổi tồn, sửa khách, sửa tài khoản | Có nguy cơ ghi dữ liệu hoặc chưa chứng minh được là chỉ-đọc. |
| POS tạo đơn | Ngoài phạm vi chỉ-đọc. |

## 6. Xác nhận không đổi dữ liệu vận hành

Đã không tạo, sửa, xóa hoặc hủy đơn; không tạo hóa đơn, thanh toán, hoàn tiền; không chỉnh tồn, lô, hạn dùng, giá hoặc sản phẩm; không tạo hoặc sửa khách hàng; không đổi mật khẩu, vai trò, quyền hoặc cấu hình tenant.

Đã không chạy migration, seed, SQL ghi, deploy, restart hay sửa mã nguồn.

Thao tác ghi duy nhất là đăng nhập và đăng xuất phiên do chính lượt kiểm thử tạo ra:

- Các phiên API hoàn tất đã gọi logout cho đúng refresh token của phiên đó.
- Một lần gọi API đầu bị đứt sau khi đăng nhập, trước logout. Refresh token của lần đó còn đến hạn thông thường (cấu hình hiện tại 30 ngày). Không có token đó để thu hồi mà không đụng phiên khác.
- Phiên giao diện trên trình duyệt kiểm thử chưa đóng được từ menu tài khoản.

Không bấm Lưu ghi chú, Trả hàng, Mở bảng sửa lô, Thêm tài khoản, Sửa hoặc Xóa.

## 7. Việc cần Architect quyết định

| Mức | Vấn đề | Bằng chứng | Hướng phân loại |
|---|---|---|---|
| Cao | “Đơn bán hôm nay” và doanh thu hôm nay trên Tổng quan lệch với danh sách đơn và báo cáo doanh thu cùng ngày lịch | 87 so với 32 ngày 13/10/2026. Phần lệch 55 đơn nằm ở 12/10 từ 17:00 đến 24:00. Doanh thu Tổng quan cũng khác báo cáo ngày 13/10. | Logic mốc ngày của dashboard |
| Trung bình | Báo cáo và thẻ “Sắp hết HSD (30 ngày)” đang liệt kê lô đã hết hạn | Nhãn đến 12/11/2026. Hai dòng HSD 31/03/2026 và 31/07/2026. Lọc còn hạn trong 1 tháng = 0. | Nhãn/API báo cáo cận hạn |
| Trung bình | Danh sách tồn thấp không đủ so với số trên Tổng quan | Tổng quan 5.373. API low-stock trả 500 dòng. | API danh sách bị cắt, hoặc định nghĩa đếm khác |
| Thấp | “Lô đang tồn” 2.884 khác danh sách lô còn tồn khoảng 2.308 | Hai màn đọc cùng lúc. Chưa chứng minh phần chênh là lô của sản phẩm/kho đã xóa. | Cần Architect chọn có đối chiếu tiếp hay không |
| Thấp | Banner lô lệch nhiều NSX/HSD | Thấy trên `/inventory/stock`. Không mở màn sửa. | Dữ liệu vận hành; chưa kiểm cách sửa |

FEFO khi bán vẫn `NOT TESTED`. Không suy ra quầy đang xuất lô gần hết hạn trước.

---

Chờ Architect xem báo cáo và quyết định bước tiếp theo. Không sửa mã, không deploy và không chạy thêm vòng audit repository.
