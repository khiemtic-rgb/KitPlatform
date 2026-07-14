# Success P2 — Epic: Loss Prevention v0 (Quản lý thất thoát)

**Mã:** NVX-PRD-03-EP03 · **Capability:** Process Excellence (#2) · neo Business Performance (#4) qua Cockpit  
**Phase:** P2 → mép P3 (incident đầy đủ **không** thuộc epic này)  
**Trạng thái:** In progress — AC2 lab (2026-07-14)  
**Neo:** [pharmacy-success-capability-map-v1.md](./pharmacy-success-capability-map-v1.md) · [Owner Cockpit EP01](./success-p2-owner-cockpit-epic-v1.md) · [Checklist ca EP02](./success-p2-02-shift-checklist-epic-v1.md) · [KIT-BP-ASBUILT](../03-solution/kitplatform-enterprise-blueprint-asbuilt-v2.1.md)  
**Điều kiện mở:** EP01 + EP02 lab ✅ · Clinic P0 trên prod ✅ · ưu tiên sau / song song CL-GO-01b (không block lab EP03)

> **Go-to-market:** “Phân hệ Quản lý thất thoát” — điểm khác biệt NT nhỏ.  
> **Build:** capability **Loss Prevention v0** trong Success (Admin + Cockpit strip) — **không** pack/QMS mới, **không** ML anomaly.

---

## Mục tiêu

Chủ nhà thuốc **thấy và siết** rủi ro thất thoát trong ngày: ai thao tác nhạy cảm, lệch quỹ cuối ca, lệch tồn từ kiểm kê cuốn chiếu, báo cáo theo nhân viên — kèm gate phê duyệt tối thiểu.

**Không phải:** camera, HR kỷ luật, hay “theo dõi nhân viên”. Product voice = **minh bạch vận hành**.

---

## Bộ 5 chức năng cốt lõi (MVP)

| # | Chức năng | Ý nghĩa NT nhỏ |
|---|-----------|----------------|
| **1** | Nhật ký thao tác (Loss Audit feed) | Ai tạo/sửa/hủy HĐ, giảm giá, trả hàng, xuất nội bộ, điều chỉnh tồn — có người + thời điểm |
| **2** | Đối chiếu tiền mặt ↔ doanh thu cuối ca | Phát hiện lệch quỹ **trong ngày** (từ `sales_shifts`) |
| **3** | Kiểm kê cuốn chiếu (cycle count 10–20 SP/ngày) | Không đợi cuối tháng; lệch tồn sớm theo SKU |
| **4** | Báo cáo theo nhân viên | Hủy HĐ · giảm giá · điều chỉnh tồn — 3 báo cáo cố định |
| **5** | Phân quyền & phê duyệt | Thao tác nhạy cảm: quyền hoặc xác nhận quản lý (bổ sung chỗ còn thủng) |

**Cảnh báo (rule-first, tối đa 3 trong epic này):**

1. Số lần hủy hóa đơn / NV hoặc / ca vượt ngưỡng  
2. Điều chỉnh tồn lặp (cùng SP hoặc cùng NV trong cửa sổ thời gian)  
3. Lệch quỹ ca vượt ngưỡng (gắn #2)

Catalog cảnh báo mở rộng (DT ca thấp bất thường, tồn âm, xuất nội bộ không duyệt, SP chênh thường xuyên) → **EP03b** / P3.

---

## Kiểm tra kế thừa / xung đột (pre-implement)

| Rủi ro | Quyết định |
|--------|------------|
| Trùng cột tiền trên `sales_shifts` | **Không** thêm `counted_*` / `opening_cash` mới — dùng `closing_cash` (= tiền đếm), `expected_cash`, `cash_variance` (mig 015) |
| Trùng EP02 checklist ca | Checklist = SOP tick; **không** gộp schema với quỹ. Deep-link tùy chọn sau |
| Phá EP01 Cockpit | Chỉ **thêm** `riskStrip?` cuối DTO — giữ Overview/Sales/Inv/Customers/KAP |
| Đếm cả ngân hàng | As-built chỉ variance **tiền mặt**; bank/card trong summary phương thức — **không** đổi công thức đóng ca trong AC2 |
| Expected cash multi-WH | Công thức đóng ca hiện có có thể lệch multi-kho (nợ kỹ thuật Sales); AC2 **đọc** variance đã persist, không rewrite POS close |

## Map as-built → việc làm

| # | As-built (KIT / Pharmacy) | Gap EP03 phải đóng |
|---|---------------------------|-------------------|
| 1 | `audit_logs`; timestamps/actor trên sales & inventory; Soft deletes | **Loss feed** lọc event types + actor + deep-link chứng từ; đảm bảo đủ event: tạo/sửa/hủy HĐ, discount, return, internal issue, stock adjust (bổ sung write audit nếu thiếu) |
| 2 | `sales_shifts` mở/đóng ca (POS Admin + Staff); báo cáo ca | ✅ AC2 lab: compose `GET /api/success/loss/cash-variance` + Cockpit `riskStrip` (threshold mặc định 10 000); không bảng tiền mới |
| 3 | Inventory count / adjustments (`inventory_adjustments`, màn kho) | **Cycle count session**: chọn 10–20 SKU (hot / random / FEFO), chốt lệch ngày, report theo SKU |
| 4 | Reports Wave 1; sales/return/adjust data theo user có phần | **3 report Loss** cố định: hủy · giảm giá · adjust theo `employee`/`user` + date range + branch |
| 5 | RBAC kernel; workflow `pos_discount_override`; PO approve | **Gate matrix**: hủy HĐ, sửa/giảm giá (đã có mầm), xuất nội bộ — thiếu policy/WF thì thêm mỏng; **không** redesign RBAC toàn nền |

**Tái sử dụng Success:**

- Owner Cockpit → strip **“Rủi ro hôm nay”** (tiles: lệch quỹ, cảnh báo hủy, cycle count chưa làm)  
- Checklist ca (EP02) → optional deep-link “đã đối chiếu quỹ?” / “đã cycle count?” (không bắt buộc merge schema)

**Gate module Admin:** `reports` (cùng `success`) + thao tác ghi cần `sales` / `inventory` write tương ứng.

---

## Acceptance criteria

### AC0 — Khung sản phẩm

- [ ] Surface Admin: `/success/loss` (hoặc `/success/risk`) + tile trên `/success/cockpit`  
- [ ] Copy/i18n: “Rủi ro / thất thoát”, không “giám sát nhân viên”  
- [ ] Pharmacy-only vertical; smoke script local  
- [ ] Doc epic + link capability map cập nhật khi Done lab  

### AC1 — Nhật ký thao tác (Loss Audit)

- [ ] Feed filter theo khoảng ngày, branch, loại sự kiện, user  
- [ ] Í thiểu loại: tạo HĐ · sửa HĐ · hủy HĐ · giảm giá · trả hàng · xuất nội bộ · điều chỉnh tồn  
- [ ] Mỗi dòng: actor, thời điểm, loại, tóm tắt, link chứng từ (nếu có)  
- [ ] Event thiếu write-path → bổ sung ghi `audit_logs` (hoặc bảng slim `success_loss_event` nếu compose audit quá chậm — **một** nguồn đọc cho UI)  

### AC2 — Đối chiếu tiền cuối ca

- [x] Từ ca đóng (`sales_shifts`): `cash_variance = closing_cash − expected_cash` (đã persist lúc đóng ca)  
- [x] Cockpit `riskStrip` + Admin `/success/loss`  
- [x] Danh sách ca `|variance| > threshold` (mặc định 10 000; query `?threshold=`)  
- [x] Không đếm tờ tiền / không cột counted_* mới  
- [ ] Soft-enforce “phải đóng ca có closing_cash” (đã bắt buộc trên CloseShift UI — xác nhận pilot)  
- [ ] Screenshot UAT / deploy VPS còn Open  

### AC3 — Kiểm kê cuốn chiếu

- [ ] Tạo phiên cycle count: branch + ngày + danh sách 10–20 product (gợi ý: bán chạy 7 ngày / min stock / random)  
- [ ] Nhập SL đếm → chốt → sinh lệch (system vs counted); điều chỉnh tồn theo flow inventory hiện có **hoặc** tạo draft adjust (chốt lúc implement — ưu tiên reuse)  
- [ ] Báo cáo lệch cycle count theo ngày / SKU  
- [ ] Tile Cockpit: “Cycle count hôm nay: chưa / xong / có lệch”  

### AC4 — Báo cáo theo nhân viên

- [ ] Hủy hóa đơn theo NV (count + giá trị)  
- [ ] Giảm giá theo NV (count + tổng tiền giảm)  
- [ ] Điều chỉnh tồn theo NV (count + giá trị tuyệt đối lệch nếu tính được)  
- [ ] Filter: từ ngày–đến ngày, branch; export CSV optional  

### AC5 — Phân quyền & phê duyệt

- [ ] Ma trận V0 (bảng trong PR / brief): thao tác → permission / workflow hiện có → gap  
- [ ] Đóng gap bắt buộc cho: **hủy hóa đơn**, **giảm giá vượt ngưỡng** (nối `pos_discount_override`), **xuất nội bộ** (approve hoặc role quản lý)  
- [ ] Thao tác bị chặn trả message rõ; có audit khi override/approve  
- [ ] Không bắt buộc Soft-CKS / chữ ký số  

### AC6 — Cảnh báo (3 rule)

- [ ] Hủy nhiều: ngưỡng theo NV/ngày hoặc ca (config đơn giản)  
- [ ] Adjust tồn lặp: cùng product hoặc cùng NV ≥ N lần / 7 ngày  
- [ ] Lệch quỹ ca > ngưỡng  
- [ ] Hiển thị trên Loss home + optional badge Cockpit; **không** spam push/SMS trong V0  

### AC7 — KPI đo được

- [ ] Định nghĩa metric hypercare: % ca đóng có đối chiếu ghi nhận **hoặc** số lần chủ mở Loss ≥ 2 lần/tuần  
- [ ] Pilot ≥1 NT (vd. NT_XUANHOA hoặc founding) ghi nhận trước/sau cảm tính “thấy lệch sớm hơn”  

---

## Deliverables (kỹ thuật — hướng)

1. Epic AC (doc này) + (nếu cần) mig mỏng cho cycle count session / loss event read model  
2. API dưới `api/success/loss/*` (hoặc compose FE từ sales/inventory/audit nếu đủ nhanh)  
3. Admin: Loss home + 3 report pages/tabs + cycle count wizard  
4. Cockpit strip “Rủi ro hôm nay”  
5. Gate/WF vá theo ma trận AC5  
6. Smoke: `scripts/smoke-success-loss-prevention-local.ps1`  

---

## Ngoài scope (EP03)

| Không làm | Đưa đi đâu |
|-----------|------------|
| Phân hệ menu độc lập phình to / pack `loss_prevention` bán riêng | Chỉ khi entitlement cần — sau adoption |
| Camera, IoT, facial, khóa két thông minh | Không bao giờ trong Success NT core |
| ML / anomaly scoring / “GD bất thường” tự học | P4+ sau data sạch |
| Incident QMS (ticket mất hàng, RCA, kỷ luật) | Capability incident **P3** |
| P&L / COGS thất thoát kế toán | Business Performance tài chính P3 |
| Đủ catalog 6+ cảnh báo | **EP03b** |
| Báo cáo thất thoát đa chiều SP × ca × TG kiểu BI | EP03b / Reports wave |
| Mobile-first Staff riêng cho Loss | Admin trước; Staff chỉ nếu cycle count tại quầy được ưu tiên sau |
| HRIS / hoa hồng / eNPS | People track |
| Gộp schema với `success_shift_checklist` bắt buộc | EP02 giữ độc lập; chỉ deep-link |

---

## KPI 90 ngày (pilot)

Một trong hai (đủ 1):

1. ≥70% ca đóng trong tuần hypercare có **đối chiếu quỹ ghi nhận** và chủ xem được lệch trên Cockpit/Loss, **hoặc**  
2. Chủ NT mở Loss / Cockpit risk ≥2 lần/tuần **và** dùng ≥1 báo cáo theo NV để hỏi/điều chỉnh quy trình.

---

## Thứ tự implement gợi ý (trong epic)

1. AC2 + Cockpit variance (tái sử dụng `sales_shifts` — thắng nhanh)  
2. AC4 reports (read-model / SQL)  
3. AC1 Loss feed (audit filter + backfill write nếu thiếu)  
4. AC5 gate matrix + vá hủy / xuất nội bộ  
5. AC3 cycle count  
6. AC6 3 alerts  

---

## Liên kết épics

| Epic | Quan hệ |
|------|---------|
| [EP01 Owner Cockpit](./success-p2-owner-cockpit-epic-v1.md) | Surface hiển thị rủi ro |
| [EP02 Checklist ca](./success-p2-02-shift-checklist-epic-v1.md) | Nhắc quy trình; không thay đối chiếu quỹ/cash |
| EP03b (chưa mở) | Thêm alerts + báo cáo sâu + incident nhẹ |
| CL-GO-01b | Clinic customer UAT — **không** block lab EP03 pharmacy |
