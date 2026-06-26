# PharmaCore — Customer App Phase 4

> Chat title: **PharmaCore · Customer App — P4: Polish**
> Base commit: `39e5fbd` (sau P3 push)

## ĐÃ XONG (P4)

- **Bottom nav:** tab **Đơn hàng** (`/orders`) — 6 tab, badge đơn chưa xem
- **SSE realtime:** `GET /api/customer-app/draft-orders/events` (+ admin `/api/sales/customer-draft-orders/events`)
- **Thông báo khách:** toast/banner đơn mới qua SSE (poll 30s fallback)
- **Thông báo admin:** desktop notification khi khách **xác nhận** đơn tạm (SSE + poll)

## DEMO

- Customer: `http://localhost:5174` — tab **Đơn hàng** bottom nav
- Admin gửi đơn → khách thấy toast/badge ~ngay (SSE)
- Khách xác nhận → admin desktop *"Khách xác nhận đơn tạm"* (cần cho phép thông báo trình duyệt)

## KỸ THUẬT

- Hub: `IDraftOrderEventHub` — `sent`, `confirmed`
- Restart API: `.\scripts\restart-api.ps1` hoặc `run-dev.bat`

## CHƯA LÀM (tuỳ chọn)

- Khách hủy đơn trên app
- Push mobile admin (chỉ desktop notification hiện tại)
