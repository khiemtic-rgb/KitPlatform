# Kiểm kê & xử lý lệch tồn — quy trình chuẩn

**Menu:** Kho → Kiểm kê (`/inventory/adjustments`)

## Khi nào dùng gì?

| Tình huống | Cách xử lý |
|------------|------------|
| Kiểm kê định kỳ / đột xuất nhiều SP | **Phiên kiểm kê** → màn **Đếm** |
| Lệch 1–vài lô, đã biết số thực tế | **Phiếu theo lô** → duyệt tại danh sách |
| Lệch do sai GRN / đơn bán / chuyển kho | Sửa **chứng từ gốc** trước — không điều chỉnh tồn |

## 4 bước (phiên kiểm kê)

1. **Chuẩn bị** — Mở phiên, chọn loại kiểm kê, tick xác nhận hạn chế bán/nhập xuất. POS hiện cảnh báo.
2. **Ghi nhận đếm** — Quét barcode hoặc chọn SP + lô + SL thực tế → **Ghi nhận**.
3. **Đối chiếu lệch** — Bảng HT · Đếm · Lệch; rà GRN/POS/chuyển kho nếu lệch lớn.
4. **Duyệt** — Hoàn tất checklist → tồn cập nhật theo lô, phiên khóa.

## Demo nhanh

- [ ] Mở phiên kiểm kê kho mặc định (loại: định kỳ)
- [ ] Đếm 2–3 lô có tồn
- [ ] Xem preview lệch
- [ ] Duyệt với checklist
- [ ] Kiểm tra **Tồn kho** / báo cáo **INV-01**
