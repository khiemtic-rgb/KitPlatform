# Novixa Connect — C5 Status Sync Brief (ready → Pharmacy consume)

**Mã:** NVX-PRD-20-C5 · **Tier:** T1 · **Trạng thái:** Implementing · **Version:** 1.0  
**Ngày:** 2026-07-11 · **Owner:** Product + Architecture  
**Phụ thuộc:** [C3](./novixa-connect-c3-referral-brief.md) · [C4](./novixa-connect-c4-booking-brief.md)

---

## 1. Mục tiêu

Đưa **tín hiệu trạng thái** từ Clinic/Connect sang **Pharmacy inbox** để NT “consume” (xác nhận đã nhận / xử lý quầy) — ví dụ bệnh nhân đã khám xong / sẵn sàng lấy thuốc hoặc tư vấn tại NT.

| Được | Không |
|------|-------|
| Event điều phối `ready_to_dispense` / hoàn tất referral|booking | Phát hành / ký số đơn trong Connect |
| Pharmacy consume / dismiss | Ghi đè `electronic_prescriptions` Rx-1 |
| Hook emit từ booking/referral complete | POS bán tự động |

**Định vị:** hàng đợi B5-style trong Connect — không phải e-Rx issuer.

---

## 2. Model `pack_connect.status_events`

| Cột | Ý nghĩa |
|-----|---------|
| `pharmacy_tenant_id` | NT nhận (consume) |
| `clinic_tenant_id` | Clinic nguồn |
| `event_type` | `ready_to_dispense` \| `referral_completed` \| `booking_completed` |
| `source_type` | `referral` \| `booking` \| `manual` |
| `source_id` | UUID nguồn (nullable nếu manual) |
| `patient_display_name`, `patient_phone` | Định danh nhẹ |
| `summary` | Mô tả ngắn |
| `event_status` | `pending_pharmacy` \| `consumed` \| `dismissed` |
| `consumed_at` / `consumed_by` | Khi NT xác nhận |

---

## 3. Emit rules

1. **Booking → completed** (có `pharmacy_tenant_id`): emit `booking_completed` + `ready_to_dispense`.
2. **Referral → completed**: emit `referral_completed` (+ `ready_to_dispense` nếu muốn gộp 1 event — C5.0: **một** event `ready_to_dispense` với `source_type=referral|booking`).
3. **Clinic manual**: `POST /status-events` tạo `ready_to_dispense` tới Pharmacy đã link active.

C5.0 chọn **một event `ready_to_dispense` mỗi lần complete/manual** (đơn giản inbox).

---

## 4. Gates

- Emit/manual create: Clinic + C1 link active với pharmacy đích.
- List/consume/dismiss: Pharmacy chỉ thấy `pharmacy_tenant_id = current`.
- Clinic có thể list outbound (events mình tạo) read-only.

---

## 5. API `/api/connect/status-events`

| Method | Path | Ai |
|--------|------|-----|
| GET | `/` | List POV (pharmacy inbox / clinic outbound) |
| GET | `/pending` | Pharmacy `pending_pharmacy` |
| POST | `/` | Clinic manual ready |
| POST | `/{id}/consume` | Pharmacy |
| POST | `/{id}/dismiss` | Pharmacy |

Notify: log deep-link Admin `/connect/status` (không fail business).

---

## 6. Acceptance C5.0

- [x] Migration `status_events`
- [x] Emit từ booking/referral complete
- [x] Pharmacy consume/dismiss + Admin UI
- [x] Overview `C5_status_sync`
- [x] Smoke: booking complete → pharmacy pending → consume
