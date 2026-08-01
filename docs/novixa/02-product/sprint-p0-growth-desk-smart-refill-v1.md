# Sprint P0 — Growth Desk + Smart Refill E2E

| | |
|---|---|
| **Mã** | NVX-SPR-P0-GROWTH |
| **Phiên bản** | 1.0 |
| **Ngày** | 2026-08-01 |
| **Pilot** | `NT_XUANHOA` |
| **North star** | Biến dữ liệu POS + App thành **đơn hàng quay lại đo được** |

**Liên quan:** [operational-positioning-v1.md](../01-company/operational-positioning-v1.md) §8.3 · care-os T3 data plane · repurchase migrations `055`/`063`/`195`

---

## 1. Mục tiêu sprint

Chứng minh tại Xuân Hòa vòng lặp:

```text
POS bán liệu trình
  → repurchase_suggestions (đã có)
  → nhắc “còn thuốc không?” + CTA đặt lại
  → draft/reservation vào POS
  → chủ thấy “hôm nay có N cơ hội / đã tạo M đơn từ refill”
```

**Không** làm trong sprint: Marketplace, Clinic, Academy GTM, HR, Connect pack mở rộng, Health Check mới.

---

## 2. Thành công đo được (Definition of Done sprint)

| Metric | Mục tiêu pilot (2 tuần sau ship) |
|--------|----------------------------------|
| % đơn POS có gắn `reminder_days_supply` (SKU liệu trình) | ≥ 40% đơn có khách định danh |
| Push/refill due gửi đúng hạn | ≥ 90% suggestion due có `notified_at` trong 24h |
| Conversion: suggestion → draft/reservation tạo | ≥ 10% suggestion due trong tuần |
| Conversion: draft refill → POS hoàn tất | ≥ 50% draft refill được quầy xử lý |
| Chủ mở Growth Desk ≥ 4 ngày/tuần | Qualitative + event `growth_desk_opened` |
| Attribution | Mỗi đơn refill gắn `source_repurchase_suggestion_id` (hoặc tương đương) |

---

## 3. Hiện trạng (không xây lại)

| Đã có | Ghi chú |
|-------|---------|
| `repurchase_suggestions` + upsert khi complete sale | `SalesRepository.TryUpsertRepurchaseSuggestionAsync` |
| POS checkout: label + days supply | `PosCheckoutModal` |
| Customer API list/accept/snooze/dismiss | Accept = **tạo nhắc uống**, chưa đặt hàng |
| Push due: “Đơn thuốc sắp hết” → `/medications` | `DispatchRepurchaseNotificationsAsync` |
| Draft orders / reservations → load POS | Chưa nối từ repurchase |
| Care KPI schema `repurchase_reminder_conversion` | Dual-write **chưa** |

---

## 4. Backlog P0 (ưu tiên)

### Epic A — Smart Refill E2E (App ↔ POS)

| ID | Story | AC | Est | Phụ thuộc |
|----|-------|-----|-----|-----------|
| **A1** | CTA “Đặt lại” trên card refill (song song “Tạo nhắc uống”) | Soft-gate member; tạo **reservation** từ dòng đơn gốc; deep-link `/reservations` | M | ✅ local `POST …/reorder` |
| **A2** | Accept/reorder đổi trạng thái suggestion | Status `converted` + `converted_at` + `converted_reservation_id`; giữ snooze/dismiss + accept nhắc uống | S | ✅ mig `260` |
| **A3** | Push copy + deep-link hành động | Copy VI hỏi còn thuốc + CTA; deep-link `/reminders?tab=repurchase` | S | ✅ local |
| **A4** | POS nhận đơn refill rõ nguồn | `source_repurchase_suggestion_id` + badge Tái mua trên POS | M | ✅ local |
| **A5** | Staff-app POS: gắn days-supply khi bán | Parity với Admin checkout reminder fields | S | ✅ local |
| **A6** | Attribution bán từ refill | `converted_sales_order_id` khi link reservation→sale; Pending loadable | M | ✅ local |

### Epic B — Growth Desk (Admin)

| ID | Story | AC | Est | Phụ thuộc |
|----|-------|-----|-----|-----------|
| **B1** | API `GET /api/…/growth/opportunities/today` | Buckets: `refill_due`, `refill_overdue`, `snoozed_expiring` (P0 tối thiểu); mỗi item: khách, SĐT, SKU/label, ngày due, suggestion id, last buy | M | ✅ local |
| **B2** | Màn **Growth Desk** (route Admin) | “Hôm nay có N cơ hội”; list theo bucket; click → khách / gọi / mở tạo draft | L | ✅ `/success/growth` |
| **B3** | Action “Chăm sóc ngay” | 1 click: tạo draft refill từ suggestion (hoặc mở template Zalo/SMS Counter OTP flow nếu chưa có app) + log `care_action` | M | ✅ + mig `263` |
| **B4** | Tile trên Dashboard / Owner Cockpit | “Có thể bán thêm hôm nay: N”; click → Growth Desk | S | ✅ |
| **B5** | Báo cáo tuần refill | N due · N notified · N converted · doanh thu attributed | M | ✅ trên Growth Desk + API |

### Epic C — POS capture chất lượng (để máy refill chạy)

| ID | Story | AC | Est |
|----|-------|-----|-----|
| **C1** | Default days-supply theo nhóm thuốc (HA / vitamin / kháng sinh) | Gợi ý sẵn trên checkout; NV vẫn sửa được | M |
| **C2** | Bắt buộc / soft-warn khi bán đơn có khách nhưng thiếu days-supply (feature flag tenant) | Pilot bật soft-warn trước | S |
| **C3** | CRM list filter “sắp tái mua” | Deep-link từ Growth Desk; reuse B1 data | S |

### Epic D — GTM / copy (không code nặng)

| ID | Story | AC | Est |
|----|-------|-----|-----|
| **D1** | Demo script 5 phút: Growth Desk → 1 refill | Sales/pilot dùng được | S |
| **D2** | Landing / pitch 1 promise | “Nhiều khách quay lại hơn” + 1 flow refill (website tối thiểu hoặc 1 trang) | S |
| **D3** | Discovery song song: 10 NT phỏng vấn | 3 câu: dùng hàng ngày? trả tiền vì gì? ngại gì? | — |

---

## 5. Thứ tự ship (2 tuần)

```text
Tuần 1
  C1/C2 (tăng input) → A2 (status) → A1 (CTA đặt lại) → A3 (push)
  B1 (API) song song

Tuần 2
  A4 + A6 (POS + attribution) → B2 + B3 (Growth Desk + chăm sóc ngay)
  B4 tile · B5 báo cáo tối thiểu · A5 staff-app · D1 demo
```

**Freeze:** Clinic, Marketplace, Connect GTM rename lớn, AI “coach” mới (chỉ cho phép action chip “Đặt lại” trên AI nếu còn bandwidth → optional **A7**).

---

## 6. Out of scope (icebox có chủ đích)

- Marketplace so sánh 5 nhà thuốc  
- Cohort sinh nhật / dormant / bỏ liệu trình đầy đủ (sau khi refill P0 ổn → P1 buckets)  
- AI Health Coach chủ động “ho 10 ngày”  
- Dual-write Care OS đầy đủ mọi event (chỉ `repurchase_converted` tối thiểu)  
- Đổi brand “Novixa Growth” thành sản phẩm tách (chỉ narrative + desk)

---

## 7. Rủi ro & quyết định cần chốt trước code

| Quyết định | Đề xuất mặc định |
|------------|------------------|
| Đặt lại = reservation hay draft? | **Reservation** (khách chủ động) + staff có thể tạo **draft** từ Growth Desk |
| Prospect (chưa member) bấm Đặt lại? | Soft-gate QR / Counter OTP như commerce hiện tại |
| Ai thấy Growth Desk? | Owner + role có `customers.read` / sales ops |
| Attribution window | Complete sale trong 14 ngày sau `converted` suggestion |

---

## 8. Checklist kỹ thuật trước merge

- [ ] Mig: status `converted` (+ `converted_at`, `converted_sales_order_id` hoặc `converted_reservation_id`)  
- [ ] Feature flag tenant: `growth_desk`, `smart_refill_reorder`  
- [ ] i18n VI/EN: push, CTA, Growth Desk  
- [ ] Smoke pilot: 1 đơn test → due date giả → push → đặt lại → POS → báo cáo +1  
- [ ] Không phá accept = tạo nhắc uống (giữ parallel CTA)

---

## Changelog

| Version | Ngày | Thay đổi |
|---------|------|----------|
| 1.0 | 2026-08-01 | Backlog P0 từ đánh giá Growth strategy |
| 1.1 | 2026-08-01 | A1+A2 local: `POST …/reorder` + status `converted` + CTA Đặt lại |
| 1.2 | 2026-08-01 | A3+A4 local: push “Còn thuốc không?” + POS badge tái mua |
| 1.3 | 2026-08-01 | A5+A6 local: staff-app days-supply + attribution sale |
| 1.4 | 2026-08-01 | B1–B5 local: Growth Desk API + Admin + care-now draft + tiles + weekly report |
