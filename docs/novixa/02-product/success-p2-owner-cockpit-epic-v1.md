# Success P2 — Epic: Owner Cockpit (KPI chủ NT)

**Mã:** NVX-PRD-03-EP01 · **Capability:** Business Performance (#4)  
**Phase:** P2 · **Trạng thái:** Ready to build  
**Neo:** [pharmacy-success-capability-map-v1.md](./pharmacy-success-capability-map-v1.md) · Reports Wave 1  
**Điều kiện mở:** Clinic P0 lab (CL-GO-01) ✅ — 2026-07-14

> **Chọn thay vì Checklist ca** lần này: tái sử dụng dashboard/reports đã có, đo được trong 90 ngày, không mở QMS.

---

## Mục tiêu

Một màn **Chủ nhà thuốc** (Admin) thấy sức khỏe cửa hàng trong 1 viewport — không phải báo cáo rời.

## Scope MVP

| KPI | Nguồn as-built | Ghi chú |
|-----|----------------|---------|
| Doanh thu hôm nay / 7 ngày / tháng | Dashboard / SALES-01 | |
| Số đơn | Sales | |
| Tồn cận HSD (số SKU / giá trị) | INV-02 | |
| Cảnh báo tồn thấp | INV alerts | |
| Khách mới / quay lại (7 ngày) | Customers + sales | Ước lượng từ đơn gắn KH |
| Điểm KAP gần nhất (nếu bật assessment) | Survey pack optional | Deep-link KAP |

**Không làm trong epic này:** P&L, forecast, checklist SOP, People hoa hồng.

## Deliverables

1. Route Admin vd. `/success/cockpit` hoặc mở rộng `/` khi vertical=pharmacy  
2. API tổng hợp mỏng `GET /api/success/owner-cockpit?branchId=` (hoặc compose FE từ API hiện có)  
3. Gate module: `reports` (+ `sales`); optional `assessment`  
4. Smoke script local + 1 screenshot UAT

## KPI 90 ngày (pilot)

Chủ NT mở cockpit ≥3 lần/tuần trong hypercare **hoặc** dùng thay vì Excel tóm tắt ngày.

## Ngoài scope → epic sau

- **Success-P2-02** Process: Checklist mở/đóng ca  
- Scorecard quý / SWOT (Continuous Improvement)
