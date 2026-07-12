# Novixa Connect — C4 Booking Stub + Notify Brief

**Mã:** NVX-PRD-20-C4 · **Tier:** T1 · **Trạng thái:** Implementing · **Version:** 1.0  
**Ngày:** 2026-07-11 · **Owner:** Product + Architecture  
**Phụ thuộc:** [C3 Referral](./novixa-connect-c3-referral-brief.md) · Clinic org profile

---

## 1. Mục tiêu

Clinic **đặt lịch stub** (slot thời gian + trạng thái) cho bệnh nhân — thường sau referral đã `accepted`. Notify bệnh nhân (SMS nếu có SĐT) + log in-app cho Pharmacy/Clinic.

| Bên | C4.0 |
|-----|------|
| **Clinic** | Tạo / confirm / cancel / complete / no_show booking |
| **Pharmacy** | Xem booking gắn pharmacy (read-only) |
| **Patient** | Nhận SMS stub (không portal) |

**Không:** calendar thật, thanh toán, telemedicine, Clinic Lite EMR.

---

## 2. Model `pack_connect.bookings`

| Cột | Ý nghĩa |
|-----|---------|
| `clinic_tenant_id` | Org clinic (bắt buộc) |
| `pharmacy_tenant_id` | Optional — NT liên quan |
| `referral_id` | Optional — phải thuộc clinic; status `accepted` hoặc `completed` khi tạo |
| `doctor_id` | Optional — membership active tại clinic |
| `patient_display_name`, `patient_phone` | Định danh nhẹ |
| `scheduled_at` | Slot stub (TIMESTAMPTZ) |
| `duration_minutes` | Mặc định 30 |
| `booking_status` | state machine |
| `notified_at` | Lần notify gần nhất |

```
Clinic create ──► proposed ──confirm──► confirmed ──complete──► completed
                     │                      │
                     │ cancel               │ no_show / cancel
                     ▼                      ▼
                 cancelled               no_show / cancelled
```

---

## 3. Gates

1. Write: caller `org_kind=clinic` và `clinic_tenant_id = current`.
2. Nếu `referral_id`: referral thuộc clinic; status ∈ `{accepted, completed}`; copy pharmacy/patient nếu thiếu.
3. **NT→PK:** nếu có `pharmacy_tenant_id` mà **không** có `referral_id` hợp lệ → reject. Pharmacy (nếu gửi kèm) phải trùng referral.
4. Nếu `pharmacy_tenant_id` (sau khi resolve từ referral): C1 org link `active` với pharmacy.
5. Nếu `doctor_id`: membership active tại clinic.
6. **Lịch nội bộ PK:** không `referral_id` và không `pharmacy_tenant_id` → được phép.
7. Pharmacy list: chỉ rows `pharmacy_tenant_id = current`.

Ops checklist: [ops-checklist-connect-nt-to-clinic-v1.md](../07-customer/ops-checklist-connect-nt-to-clinic-v1.md)

---

## 4. Notify (stub)

`IConnectNotifyService`:

- `BookingProposed` / `BookingConfirmed` → SMS patient nếu phone hợp lệ (`ISmsTextSender`); không fail business flow.
- Luôn `LogInformation` deep-link Admin `/connect/bookings`.

---

## 5. API `/api/connect/bookings`

| Method | Path | Ai |
|--------|------|-----|
| GET | `/` | List POV |
| POST | `/` | Clinic create |
| POST | `/{id}/confirm` | Clinic |
| POST | `/{id}/cancel` | Clinic |
| POST | `/{id}/complete` | Clinic (từ confirmed) |
| POST | `/{id}/no-show` | Clinic (từ confirmed) |

---

## 6. Acceptance C4.0

- [x] Migration bookings
- [x] Service + notify + API
- [x] Admin Bookings tab
- [x] Overview `C4_booking`
- [x] Smoke: Clinic create từ referral accepted → confirm → complete; SMS/log không crash
