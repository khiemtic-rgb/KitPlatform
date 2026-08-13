# CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM
# Độc lập - Tự do - Hạnh phúc

---

| | |
|---|---|
| **CÔNG TY TNHH TRUYỀN THÔNG VÀ CÔNG NGHỆ KIT** | **Số:** 01/BB-KT-CSDL/2026 |
| Mã số thuế: 4601239671 | **Thái Nguyên**, ngày 13 tháng 08 năm 2026 |

# BIÊN BẢN
## KIỂM THỬ KẾT NỐI API HỆ THỐNG CƠ SỞ DỮ LIỆU VỀ DƯỢC
### (Môi trường Sandbox — theo Tài liệu kỹ thuật đặc tả API phiên bản 1.1)

| Hạng mục | Nội dung |
|----------|----------|
| Mã tài liệu | NVX-CQD-UAT-01 |
| Phiên bản biên bản | 1.1 |
| Phần mềm | Novixa — phần mềm quản lý nhà thuốc |
| Đơn vị thực hiện | Công ty TNHH Truyền thông và Công nghệ KIT |
| Cơ quan tiếp nhận / vận hành CSDL | Trung tâm Thông tin Y tế Quốc gia — Bộ Y tế |
| Môi trường kiểm thử | Sandbox API v2 |
| Base URL | https://api-sandbox.csdlduoc.com.vn/v2 |
| Tài khoản API kiểm thử | 019189002577 |
| Căn cứ kỹ thuật | Tài liệu kỹ thuật đặc tả API Hệ thống cơ sở dữ liệu về dược phiên bản 1.1 và Hướng dẫn sử dụng (ký số ngày 17/07/2026) |

---

## I. MỤC ĐÍCH

Biên bản này ghi nhận kết quả kiểm thử kỹ thuật kết nối API giữa phần mềm **Novixa** với **Hệ thống cơ sở dữ liệu về dược** trên môi trường **Sandbox**, nhằm:

1. Xác nhận khả năng xác thực, đọc danh mục thuốc và ghi giao dịch nhập–xuất theo đặc tả API phiên bản 1.1;
2. Làm căn cứ đối soát kỹ thuật trước khi phối hợp triển khai trên môi trường Production;
3. Phục vụ trao đổi với chuyên viên hỗ trợ tích hợp của Trung tâm Thông tin Y tế Quốc gia.

Biên bản này là tài liệu kỹ thuật phục vụ kiểm thử; **không** thay thế hồ sơ pháp lý đăng ký liên thông.

---

## II. THÔNG TIN ĐƠN VỊ VÀ SẢN PHẨM

| Hạng mục | Nội dung |
|----------|----------|
| Tên pháp nhân | Công ty TNHH Truyền thông và Công nghệ KIT |
| Thương hiệu sản phẩm | Novixa |
| Mã số thuế | 4601239671 |
| Địa chỉ trụ sở | KĐT Hồ Xương Rồng, phường Phan Đình Phùng, tỉnh Thái Nguyên |
| Website | https://novixa.vn |
| Email kỹ thuật | care@novixa.vn |
| Điện thoại | 0984.660.399 |
| Người phụ trách tích hợp | Ông Tuấn |

---

## III. CĂN CỨ VÀ PHẠM VI KIỂM THỬ

### 1. Căn cứ

- Tài liệu kỹ thuật đặc tả API Hệ thống cơ sở dữ liệu về dược phiên bản **1.1** (thay thế API 1.0 theo 522/QĐ-TTYQG ngày 18/12/2025);
- Hướng dẫn sử dụng kèm theo; môi trường Sandbox `https://api-sandbox.csdlduoc.com.vn/v2`.

### 2. Phạm vi

| Nhóm | Nội dung | Kết quả |
|------|----------|---------|
| A | Xác thực tài khoản API (`/auth/login`) | Đạt |
| B | Danh mục thuốc (`/master/drugs`) | Đạt |
| C0 | Nhập tồn đầu kỳ (`/transactions/stock-in`, `reason=opening-balance`) | Đạt |
| C1 | Xuất bán lẻ (`/transactions/stock-out`, `reason=sale-retail`) | Đạt |

*Portal web `https://csdlduoc.com.vn/` không thuộc phạm vi biên bản này.*

### 3. Phương pháp

1. Gọi API RESTful qua HTTPS theo đặc tả v1.1;
2. Xác thực bằng `POST /auth/login` (`application/x-www-form-urlencoded`); trường `password` truyền dưới dạng **chuỗi Base64** của mật khẩu được cấp;
3. Sử dụng `Authorization: Bearer {access_token}` cho các API nghiệp vụ;
4. Thực hiện đúng trình tự nghiệp vụ: **nhập tồn đầu kỳ** trước, sau đó **xuất bán lẻ**;
5. Tra cứu trạng thái xử lý qua API `/status` và ghi nhận kết quả.

Thời điểm kiểm thử chính thức ghi trong biên bản: **13/08/2026, khoảng 15:10 (GMT+7)**.

---

## IV. KẾT QUẢ KIỂM THỬ CHI TIẾT

### 1. Nhóm A — Xác thực

| STT | Hạng mục | Endpoint | Phương thức | Kết quả | HTTP |
|-----|----------|----------|-------------|---------|------|
| A1 | Đăng nhập lấy Access Token | `/auth/login` | POST | Đạt | 200 |
| A2 | Gọi API nghiệp vụ với Bearer Token | `/master/drugs` | GET | Đạt | 200 |

**Nhận xét:** Tài khoản `019189002577` đăng nhập thành công; nhận `access_token` (`token_type=Bearer`, `expires_in=86400`), có `refresh_token`.

### 2. Nhóm B — Danh mục thuốc

| STT | Hạng mục | Endpoint | Phương thức | Kết quả | HTTP | Ghi chú |
|-----|----------|----------|-------------|---------|------|---------|
| B1 | Danh sách thuốc (phân trang) | `/master/drugs?page=1&page_size=3` | GET | Đạt | 200 | `total` = 54.540 |
| B2 | Chi tiết thuốc theo `drug_id` | `/master/drugs/893110130226` | GET | Đạt | 200 | TBZemitin 500; `unit_id=U31` |

**Nhận xét:** Kết nối đọc danh mục thuốc trên Sandbox thành công.

### 3. Nhóm C0 — Nhập tồn đầu kỳ (`opening-balance`)

Theo Mục 5.4.1 và từ điển lý do nhập hàng Mục 6.4.1.1.

| STT | Hạng mục | Endpoint | Phương thức | Kết quả | HTTP | Ghi chú |
|-----|----------|----------|-------------|---------|------|---------|
| C0 | Tạo phiếu nhập tồn đầu kỳ | `/transactions/stock-in` | POST | Đạt | 200 | `transaction_id` = 580c233a-d0a3-c824-d3df-176c96820bc0 |
| C0s | Tra cứu trạng thái | `/transactions/stock-in/{id}/status` | GET | Đạt | 200 | **status = Completed** |

**Tóm tắt request** (đã che thông tin xác thực):

- `reason`: `opening-balance`
- `reference_number`: `NVX-OB-20260813-151002`
- Thuốc: `drug_id=893110130226`, `unit_id=U31`, `quantity=10`, `batch_no=OB20260813-151002`, `expiry_date=2027-12-31`

### 4. Nhóm C1 — Xuất bán lẻ (`sale-retail`)

Theo Mục 5.4.4 và Mục 6.4.2.1. Thực hiện **sau** khi phiếu tồn đầu kỳ đã ở trạng thái Completed.

| STT | Hạng mục | Endpoint | Phương thức | Kết quả | HTTP | Ghi chú |
|-----|----------|----------|-------------|---------|------|---------|
| C1 | Tạo phiếu xuất bán lẻ | `/transactions/stock-out` | POST | Đạt | 200 | `transaction_id` = 580c233a-f3ac-b476-e19e-0f07d83b6022 |
| C1s | Tra cứu trạng thái | `/transactions/stock-out/{id}/status` | GET | Đạt | 200 | **status = Completed** |

**Tóm tắt request** (đã che thông tin xác thực):

- `reason`: `sale-retail`
- `reference_number`: `NVX-SO-20260813-151002`
- Thuốc: cùng `drug_id` / `unit_id` / lô với phiếu tồn đầu kỳ; `quantity=1`

---

## V. TỔNG HỢP KẾT LUẬN

| Nhóm | Kết luận |
|------|----------|
| A — Xác thực | **Đạt** |
| B — Danh mục thuốc | **Đạt** |
| C0 — Nhập tồn đầu kỳ | **Đạt** (Completed) |
| C1 — Xuất bán lẻ | **Đạt** (Completed) |
| **Tổng thể** | Kết nối đọc danh mục và ghi giao dịch trên môi trường Sandbox với tài khoản được cấp **thành công**, theo đúng trình tự đặc tả API phiên bản 1.1 |

### Diễn biến các lần kiểm thử (tham chiếu)

| Lần | Ngày | Tài khoản | Kết quả tóm tắt |
|-----|------|-----------|-----------------|
| 1 | 13/08/2026 | 4601239671 | Xác thực / danh mục đạt; `stock-out` trả HTTP 500 |
| 2 | 13/08/2026 | 019189002577 | `stock-out` nhận phiếu nhưng trạng thái Error: *Chưa nhập phiếu tồn đầu kỳ* |
| 3 | 13/08/2026 (15:10) | 019189002577 | `opening-balance` rồi `sale-retail` đều **Completed** (biên bản này) |

---

## VI. KIẾN NGHỊ

Kính đề nghị Trung tâm Thông tin Y tế Quốc gia / chuyên viên hỗ trợ tích hợp:

1. Ghi nhận kết quả kiểm thử Sandbox theo đặc tả API 1.1 tại biên bản này;
2. Khi chuyển hỗ trợ kỹ thuật chuyên sâu, hỗ trợ các hạng mục tiếp theo (nếu có): kiểm kê (`stock-taking`), hướng dẫn tìm kiếm thuốc theo tên trên API danh mục, và cấu hình môi trường Production;
3. Đơn vị tiếp tục hoàn thiện tích hợp trong phần mềm Novixa và sẵn sàng phối hợp UAT Production khi được hướng dẫn.

---

## VII. CAM KẾT

1. Chỉ sử dụng tài khoản được cấp cho mục đích kiểm thử Sandbox; không đẩy dữ liệu giả lên môi trường Production.
2. Bảo mật thông tin đăng nhập; không công khai mật khẩu.
3. Tuân thủ đặc tả API phiên bản 1.1 và các hướng dẫn cập nhật từ cơ quan vận hành.
4. Phối hợp đầy đủ khi được yêu cầu bổ sung kịch bản kiểm thử hoặc đối soát nhật ký hệ thống.

---

## VIII. PHỤ LỤC

### Phụ lục 1 — Đối chiếu với mục đặc tả API 1.1

| Kiểm thử | Mục đặc tả |
|----------|------------|
| A1 Login | 5.1.1 |
| B1–B2 Drugs | 5.3.1 / 5.3.2 |
| C0 Stock-in | 5.4.1 |
| `reason=opening-balance` | 6.4.1.1 |
| C1 Stock-out | 5.4.4 |
| `reason=sale-retail` | 6.4.2.1 |

### Phụ lục 2 — Môi trường kỹ thuật

| Hạng mục | Giá trị |
|----------|---------|
| Base URL Production | https://api.csdlduoc.com.vn/v2 |
| Base URL Sandbox | https://api-sandbox.csdlduoc.com.vn/v2 |
| Phiên bản API | v2 |
| Định dạng | JSON UTF-8 |
| Xác thực | Bearer Token sau `/auth/login` |

---

## IX. XÁC NHẬN

Biên bản được lập thành **02** bản có giá trị như nhau; đơn vị phần mềm giữ **01** bản, gửi chuyên viên hỗ trợ tích hợp **01** bản (bản điện tử PDF).

| | ĐẠI DIỆN ĐƠN VỊ PHẦN MỀM | CHUYÊN VIÊN HỖ TRỢ *(nếu có)* |
|---|---|---|
| | Công ty TNHH Truyền thông và Công nghệ KIT | Trung tâm Thông tin Y tế Quốc gia |
| Họ và tên | | |
| Chức danh | | |
| Chữ ký | | |
| Ngày | 13/08/2026 | |

---

**Nơi nhận:**

- Chuyên viên hỗ trợ tích hợp — Trung tâm Thông tin Y tế Quốc gia;
- Lưu hồ sơ kỹ thuật Novixa tại Công ty TNHH Truyền thông và Công nghệ KIT.

---

*Tài liệu kỹ thuật phục vụ phối hợp kiểm thử liên thông CSDL dược — Công ty TNHH Truyền thông và Công nghệ KIT.*
