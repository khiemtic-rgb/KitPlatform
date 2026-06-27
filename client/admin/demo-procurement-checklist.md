# Checklist demo Mua hàng — PharmaCore

Dùng trước pilot / demo đối tác hoặc sau release lớn module procurement. Login admin: `admin` / `Admin@123`.

Liên quan: [demo-pos-checklist.md](./demo-pos-checklist.md) · [demo-customer-app-checklist.md](../customer-app/demo-customer-app-checklist.md)

## Chuẩn bị

- [ ] PostgreSQL + API (`http://localhost:5290`) + Admin (`http://localhost:5173`) đang chạy
- [ ] `.\scripts\run-migrations.ps1` (hoặc seed mới) nếu DB trống
- [ ] `.\scripts\restart-api.ps1` nếu API treo
- [ ] Hard refresh trình duyệt (Ctrl+Shift+R)

## Ghi chú demo

| Mã / đối tượng | Mô tả |
|----------------|--------|
| `NCC001` | Công ty Dược phẩm ABC (seed) |
| `PARA500` | Paracetamol 500mg |
| `vat_8` | Loại thuế GTGT 8% (mặc định trên PO) |
| Kho chính | Warehouse demo seed |

**Quy ước tiền Giai đoạn 1:** công nợ & thanh toán NCC theo **GRN trước thuế**; PO hiển thị thêm **tổng sau thuế** (tham chiếu).

---

## 1. Cấu hình thuế GTGT

- [ ] **Mua hàng → Thuế GTGT** — danh sách loại thuế load OK
- [ ] Xem loại `vat_8` (8%) và loại không chịu thuế (nếu có)
- [ ] *(Tùy chọn)* Tạo loại thuế mới → lưu → dùng được trên PO

## 2. Nhà cung cấp

- [ ] **Mua hàng → Nhà cung cấp** — thấy `NCC001`
- [ ] Mở chi tiết / sửa hạn thanh toán (payment terms) nếu cần demo aging

## 3. Đơn đặt hàng (PO)

- [ ] **Tạo PO** cho `NCC001`, kho chính, ≥1 dòng SP (vd. Paracetamol)
- [ ] Chọn **loại thuế GTGT** → footer **Tạm tính / Thuế / Tổng** khớp % thuế
- [ ] **Lưu** PO trạng thái nháp → **Gửi duyệt** (hoặc workflow tương đương) → PO **đã duyệt / chờ nhận**
- [ ] Mở drawer PO — bảng dòng SP trong panel màu, cột tiền căn phải

## 4. Phiếu nhập hàng (GRN)

- [ ] **Tạo GRN** từ PO đã duyệt (hoặc tạo mới gắn PO)
- [ ] Nhập số lượng, đơn giá / gợi ý giá PO → **Hoàn tất nhập kho**
- [ ] Trạng thái GRN: **Chờ nhập kho** → **Hoàn tất** (không còn nhãn «Nháp»)
- [ ] Xem chi tiết GRN — footer thuế (nếu có PO liên kết) hiển thị Tạm tính / Thuế / Tổng
- [ ] **Kho hàng → Tồn** — tồn SP tăng sau GRN

## 5. Công nợ NCC

- [ ] **Mua hàng → Công nợ NCC** — NCC có số **Còn phải trả** > 0
- [ ] Mở chi tiết công nợ — dòng GRN: Giá trị GRN, Đã trả, **Còn lại**, tuổi nợ
- [ ] Giá trị GRN = tổng dòng **trước thuế** (khác tổng PO sau thuế nếu có VAT)

## 6. Thanh toán NCC

- [ ] Từ công nợ — **Thanh toán** trên dòng GRN → mở form TT với NCC + GRN + số tiền gợi ý
- [ ] Form hiển thị chú thích **trước thuế / sau thuế** và nút **Điền số còn lại**
- [ ] **Lưu** phiếu TT trạng thái **Chờ ghi sổ**
- [ ] **Ghi sổ** → trạng thái đã ghi sổ; công nợ GRN giảm đúng
- [ ] *(Tùy chọn)* Thanh toán không gắn GRN — bù trừ theo thứ tự GRN cũ nhất

## 7. Hồi quy nhanh

- [ ] Lọc PO «chờ nhận hàng» trên dashboard / danh sách PO
- [ ] Export CSV danh sách thanh toán (nếu có dữ liệu)
- [ ] Refresh trang — không lỗi, tab Mua hàng load bình thường

## 8. Liên thông POS (tùy chọn)

- [ ] Sau GRN — bán thử SP vừa nhập trên POS ([demo-pos-checklist](./demo-pos-checklist.md))
- [ ] Giá vốn / tồn phản ánh đúng sau nhập hàng
