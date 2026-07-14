# Success P2 — Epic: Owner Cockpit (KPI chủ NT)

**Mã:** NVX-PRD-03-EP01 · **Capability:** Business Performance (#4)  
**Phase:** P2 · **Trạng thái:** Implemented (lab) · 2026-07-14  
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

1. ✅ Route Admin `/success/cockpit` + nav module `success` (gate `reports`) + quick action Dashboard  
2. ✅ `GET /api/success/owner-cockpit` (overview + sales/inventory/customer extras + KAP optional)  
3. ✅ Gate: `DashboardPolicies.Read` / platform `reports`; KAP tile khi `assessment`  
4. ✅ Smoke: `scripts/smoke-success-owner-cockpit-local.ps1` · screenshot UAT còn Open

## KPI 90 ngày (pilot)

Chủ NT mở cockpit ≥3 lần/tuần trong hypercare **hoặc** dùng thay vì Excel tóm tắt ngày.

## Ngoài scope → epic sau

- **Success-P2-02** Process: [Checklist mở/đóng ca](./success-p2-02-shift-checklist-epic-v1.md) (NVX-PRD-03-EP02)  
- Scorecard quý / SWOT (Continuous Improvement)
