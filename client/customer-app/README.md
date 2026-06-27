# PharmaCore Customer App

App web khách hàng (PWA-friendly) — gọi API `/api/customer-app/*`.

## Chạy dev

```powershell
# Terminal 1: API (nếu chưa chạy)
.\scripts\restart-api.ps1

# Terminal 2: Customer App
.\run-customer-app.bat
# hoặc
cd client/customer-app && npm install && npm run dev
```

| URL | Mô tả |
|-----|--------|
| http://localhost:5174 | Customer App |
| http://localhost:5290/swagger | API Swagger |

## Demo login

- SĐT: `0909123456` (Trần Thị Mai)
- Tenant: `DEMO_PHARMACY`
- OTP (Development): `000000`

## Màn hình

- **Trang chủ** — tóm tắt điểm & nhắc thuốc
- **Điểm thưởng** — số dư, lịch sử, voucher
- **Nhắc thuốc** — CRUD lịch uống thuốc
- **Tài khoản** — profile, đồng ý CDP, bật push PWA & đăng xuất

## Cấu trúc

```
client/customer-app/src/
├── app/           # router, providers
├── modules/       # auth, home, loyalty, reminders, profile
└── shared/        # api, auth, layout
```

Checklist demo: [demo-customer-app-checklist.md](./demo-customer-app-checklist.md) · Admin [POS](../admin/demo-pos-checklist.md) · [Mua hàng](../admin/demo-procurement-checklist.md)

Lưu ý: chọn sản phẩm khi tạo nhắc thuốc tra cứu qua API `/catalog/products`. Đồng ý CDP (SMS/App push — nhắc chăm sóc) quản lý tại **Tài khoản**. Bật **Thông báo push** sau khi API có `CustomerAppPush` VAPID keys (dev đã cấu hình trong `appsettings.Development.json`).
