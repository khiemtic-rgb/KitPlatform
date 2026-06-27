# Báo cáo — Thống kê (Wave 1)

Module **Báo cáo** trên admin: `/reports`

## Wave 1 — 7 báo cáo

| Mã | Tên | API |
|----|-----|-----|
| SALES-01 | Doanh thu theo kỳ | `GET /api/reports/sales/revenue-by-period` |
| SALES-02 | Doanh thu theo HTTT | `GET /api/reports/sales/revenue-by-payment-method` |
| SALES-03 | Ca làm việc | `GET /api/reports/sales/shifts` |
| PROC-01 | Giá trị nhập GRN | `GET /api/reports/procurement/grn-value` |
| PROC-03 | Công nợ NCC snapshot | `GET /api/reports/procurement/payables-snapshot` |
| INV-01 | Tồn & giá trị | `GET /api/reports/inventory/stock-snapshot` |
| INV-02 | Sắp hết HSD | `GET /api/reports/inventory/near-expiry` |

## Quyền

Migration `039_reports_permissions.sql`:

- `reports.read` — xem báo cáo
- `reports.export` — xuất (CSV client-side; quyền dùng sau)

User có `sales.read` / `procurement.read` / `inventory.read` cũng xem được (policy `ReportsRead`).

## Ghi chú tiền

- **PROC-01 / PROC-03:** trước thuế GTGT (GRN hoàn tất)
- **SALES-*:** thu ròng = thu bán − hoàn trả, theo giờ VN (UTC+7)

## Wave 2 (chưa có)

- `reports.taxInput` — thuế đầu vào
- `reports.accountingExport` — export kế toán
