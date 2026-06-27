# Checklist demo Customer App — PharmaCore

Kiểm tra app khách trước pilot hoặc demo O2O. Chạy song song Admin (`5173`) + API (`5290`) + Customer App (`5174`).

Liên quan: [demo-pos-checklist.md](../admin/demo-pos-checklist.md) · [demo-procurement-checklist.md](../admin/demo-procurement-checklist.md)

## Chuẩn bị

- [ ] `.\run-dev.bat` hoặc API + `.\run-customer-app.bat`
- [ ] Customer App: http://localhost:5174
- [ ] Admin: http://localhost:5173 (`admin` / `Admin@123`)
- [ ] Hard refresh / xóa cache PWA nếu test push

## Thông tin demo

| Mục | Giá trị |
|-----|---------|
| Tenant | `DEMO_PHARMACY` |
| SĐT | `0909123456` (Trần Thị Mai) |
| OTP (Development) | `000000` |
| SP gợi ý nhắc thuốc | Vitamin C 1000mg (`PARA500` / catalog demo) |

---

## 1. Đăng nhập OTP

- [ ] Màn login — nhập SĐT `0909123456`
- [ ] Nhận OTP (dev: `000000`) → vào **Trang chủ**
- [ ] Đăng xuất → đăng nhập lại (session ổn)

## 2. Trang chủ

- [ ] Hiển thị tóm tắt **điểm thưởng** và **nhắc thuốc** (nếu có)
- [ ] Điều hướng menu dưới: Trang chủ / Điểm / Nhắc thuốc / Tài khoản

## 3. Tích điểm (Loyalty)

- [ ] **Điểm thưởng** — số dư hiển thị (seed: khách Mai có điểm)
- [ ] Lịch sử giao dịch điểm load OK
- [ ] Danh sách **voucher** của khách (nếu đã phát từ admin)
- [ ] *(Liên thông admin)* Bán POS gắn khách Mai → điểm cộng / đổi điểm trên app cập nhật sau refresh

## 4. Nhắc thuốc (Reminders)

- [ ] Xem danh sách nhắc — bật/tắt từng nhắc (toggle theo dòng)
- [ ] **Thêm nhắc** — tra cứu SP qua API catalog (không còn list demo cứng)
- [ ] Sửa giờ uống / ghi chú → lưu OK
- [ ] Xóa nhắc (nếu có quyền)

## 5. Tài khoản & CDP

- [ ] **Tài khoản** — hiển thị họ tên, SĐT
- [ ] **Đồng ý CDP** (SMS / app push) — bật/tắt, lưu được
- [ ] *(Push)* Bật **Thông báo push** khi API đã cấu hình VAPID (`appsettings.Development.json`)

## 6. Liên thông Admin — Đơn từ app

- [ ] *(Nếu có luồng đặt hàng app)* Khách tạo đơn tạm → Admin **Bán hàng → Đơn hàng từ app** thấy badge
- [ ] Admin duyệt / đưa vào POS → hoàn tất đơn

## 7. Liên thông Admin — Đặt trước

- [ ] Khách đặt trước SP (nếu bật trên app) → Admin **Đặt trước app**
- [ ] Duyệt → **Sẵn sàng** → khách nhận / admin **Thu POS** load giỏ

## 8. Liên thông Admin — Chat

- [ ] Khách gửi tin (nếu có UI chat app) → Admin **Chat KH** badge chưa đọc
- [ ] Admin trả lời → khách thấy tin mới

## 9. Hồi quy nhanh

- [ ] Refresh app — không màn trắng
- [ ] Mở trên mobile viewport / Add to Home Screen (PWA) — layout ổn
- [ ] API tắt → thông báo lỗi rõ, không treo vô hạn
