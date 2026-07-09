# Novixa hypercare — Tuần 1 → Tuần 2–4 (v1)

Runbook sau go-live G0/G1. Cập nhật: 2026-07.

## Tuần 1 — Ổn định quầy & admin

| Hạng mục | Việc làm | Nơi trong app |
|----------|----------|---------------|
| Dispense notes | Ghi tư vấn trên đơn hoàn tất | Staff POS → Receipt · Admin → Sales orders (drawer) |
| Mock drug DB | Banner cảnh báo trên danh mục SP | Admin → Catalog → Products |
| POS hotkeys | F2 tìm · F8 thanh toán · F9 nháp · F12 chốt · P in · N đơn mới · Shift+S lưu ghi chú | Staff POS |
| In nhiệt | Hướng dẫn + «In thử» | Admin → Sales → Receipt settings |
| SMS OTP | Http provider trên VPS (không stub pilot) | `CustomerAppSms__*` trong `/etc/kit-platform/api.env` |

### Phím tắt POS (staff)

- **F2** — focus ô tìm SP  
- **F8** — thanh toán (cần mở ca)  
- **F9** — lưu nháp  
- **F12** — hoàn tất thanh toán (màn checkout)  
- **P** — in lại bill (màn receipt)  
- **N** — đơn mới  
- **Shift+S** — lưu ghi chú dispense  

### Phím tắt admin POS / PO

- **F2 / F8 / F9** — tương tự staff (admin POS)  
- **Ctrl+P** — in bill đơn vừa bán  
- **Ctrl+S** — lưu PO nháp (drawer tạo PO)  
- **Ctrl+Enter** — gửi duyệt PO (drawer chi tiết, trạng thái nháp)  

## Tuần 2–4 — Hypercare vận hành

| Hạng mục | Việc làm | Nơi trong app |
|----------|----------|---------------|
| GPP checklist | Tick daily/weekly/monthly (localStorage) | Admin → Inventory → Checklist GPP |
| Đề xuất nhập | Tồn thấp → prefill PO | Inventory → Low stock → «Đề xuất nhập hàng» |
| PO workflow | Gửi duyệt / drawer pending | Procurement → Purchase orders |
| Web Push | VAPID + verify script | Xem bên dưới |

### Checklist GPP (local)

Không thay sổ GPP giấy. Dùng để coach nhân viên tuần đầu. Reset đầu tháng nếu cần.

### Đề xuất PO từ tồn thấp

1. Chọn **một kho** (hoặc tick các dòng cùng kho).  
2. Bấm **Đề xuất nhập hàng**.  
3. Form PO mở với số lượng gợi ý = `ngưỡng − tồn` (tối thiểu 1).  
4. Chọn NCC thật, giá, rồi **Lưu nháp** → **Gửi duyệt**.

## Web Push (G1.2)

```powershell
# Sinh VAPID ( một lần )
.\scripts\generate-vapid-keys.ps1 -Subject "mailto:care@novixa.vn"

# Gán vào api.env hoặc appsettings.Production.json:
# CustomerAppPush__Enabled=true
# CustomerAppPush__PublicKey=...
# CustomerAppPush__PrivateKey=...

# Restart API rồi verify:
.\scripts\verify-push-config.ps1 -BaseUrl "https://api.novixa.vn"
```

Production bắt buộc **HTTPS** cho PWA + Service Worker.

## Smoke test sau deploy frontends

1. Staff POS: F2 → thêm SP → F8 → F12 → bill in · ghi chú dispense.  
2. Admin: đơn hoàn tất → panel dispense · In thử receipt settings.  
3. Low stock → đề xuất PO → Ctrl+S → Ctrl+Enter (role phù hợp).  
4. `verify-push-config.ps1` → `[OK]`.

## Escalation

- In lệch khổ: chỉnh OS printer 80mm, không sửa code V1.  
- OTP fail: kiểm tra `CustomerAppSms__HttpUrl` và log API.  
- PO stuck pending: Admin → PO chờ duyệt drawer.
