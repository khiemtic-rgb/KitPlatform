# PharmaCore — Customer App Phase 3

> Chat title: **PharmaCore · Customer App — P3: Đơn hàng O2O**
> Entity: `customer_draft_orders` (tách khỏi `sales_orders` nháp POS)

## ĐÃ XONG

- Migration **029** — bảng `customer_draft_orders` + items
- Migration **030** — `hidden_by_customer_at` (khách ẩn khỏi app, admin vẫn thấy)
- Admin: **Bán hàng → Đơn tạm app**, Chat KH drawer, POS **Gửi khách hàng** / **Nạp POS**
- Customer app: **Đơn hàng** (`/orders`) — tab **Đặt** / **Đã mua**, xác nhận tuỳ chọn, **Ẩn khỏi app**
- Push khi gửi đơn (`SendDraftOrderPushAsync`)
- API khách: `/api/customer-app/draft-orders/*` (+ `POST .../hide`)
- API admin: `/api/sales/customer-draft-orders/*`

## DEMO

- SĐT: `0909123456`, tenant `DEMO_PHARMACY`, OTP `000000`
- Flow: Chat KH → Đơn tạm → Gửi khách → app `/orders` → POS Nạp POS → thanh toán

## LƯU Ý KỸ THUẬT

- List khách: `excludeHiddenByCustomer: true` + filter status
- SQL list: **không** ghép raw string `"""` + `ORDER BY` (đã lỗi `NULLORDER` — dùng `" ORDER BY ..."`)
- Restart API: `.\scripts\restart-api.ps1`
- Migration 030 riêng: `psql ... -f migrations/030_customer_draft_order_customer_hide.sql`

## CHƯA LÀM (tuỳ chọn sau)

- Khách **hủy đơn** trên app (đã chọn soft hide thay vì hủy)
- Tab **Đơn hàng** trên bottom nav customer app (hiện vào qua Trang chủ)
- Push admin khi khách xác nhận tạm
