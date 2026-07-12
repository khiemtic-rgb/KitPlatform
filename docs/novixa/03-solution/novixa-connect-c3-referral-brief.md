# Novixa Connect — C3 Referral Brief (Pharmacy → Clinic)

**Mã:** NVX-PRD-20-C3 · **Tier:** T1 · **Trạng thái:** Implementing · **Version:** 1.0  
**Ngày:** 2026-07-11 · **Owner:** Product + Architecture  
**Phụ thuộc:** C1 org links + C2 membership + [clinic org foundation](./novixa-connect-c2-doctor-membership-brief.md) (`DEMO_CLINIC`)

---

## 1. Mục tiêu

Pharmacy **giới thiệu / chuyển bệnh nhân** sang Clinic đã liên kết (C1 `active`) — điều phối, không khám / không kê đơn trong Connect.

| Bên | Việc C3.0 |
|-----|-----------|
| **Pharmacy** | Tạo referral → Clinic (+ optional BS membership active); hủy khi còn pending; xem outbound |
| **Clinic** | Inbox pending; accept / reject; complete sau khi đã tiếp nhận |
| **Doctor** | Chỉ là người được gợi ý (optional); chưa OTP |

---

## 2. Ranh giới pháp lý

| Được | Không |
|------|-------|
| Điều phối referral + trạng thái | Khám / bệnh án / kê / ký số |
| Gắn `doctor_id` thuộc Clinic membership | Phát hành e-Rx vào tenant NT |
| Notify stub (log) sau | Booking slot thật (C4) |

---

## 3. Model `pack_connect.referrals`

| Cột | Ý nghĩa |
|-----|---------|
| `pharmacy_tenant_id` | Người gửi (org_kind=pharmacy) |
| `clinic_tenant_id` | Người nhận (org_kind=clinic) |
| `doctor_id` | Optional — phải membership `active` tại clinic |
| `patient_display_name`, `patient_phone` | Định danh nhẹ (không PHI sâu) |
| `reason`, `notes` | Lý do giới thiệu |
| `referral_status` | state machine |

```
Pharmacy create
      │
      ▼
pending_clinic_accept ──Clinic accept──► accepted ──complete──► completed
      │                                    │
      │ reject                             │
      ▼                                    ▼
   rejected                    (cancel only from pending by pharmacy)
```

Pharmacy **cancel** chỉ khi `pending_clinic_accept`.

---

## 4. Gates (bắt buộc)

1. Tạo: caller `org_kind=pharmacy`; clinic `org_kind=clinic`; C1 org link `active`.
2. `doctor_id` (nếu có): membership active tại `clinic_tenant_id`.
3. Accept/reject/complete: caller = clinic tenant của referral.
4. Cancel: caller = pharmacy tenant của referral.

---

## 5. API

`/api/connect/referrals` · gate `novixa_connect`

| Method | Path | Ai |
|--------|------|-----|
| GET | `/` | List POV (outbound pharmacy / inbound clinic) |
| GET | `/inbox` | Clinic pending |
| POST | `/` | Pharmacy create |
| POST | `/{id}/accept` | Clinic |
| POST | `/{id}/reject` | Clinic |
| POST | `/{id}/complete` | Clinic (từ accepted) |
| POST | `/{id}/cancel` | Pharmacy (từ pending) |

---

## 6. Admin

Tab **Referrals** (`/connect/referrals`) — cả pharmacy lẫn clinic.

---

## 7. Acceptance C3.0

- [x] Migration `pack_connect.referrals`
- [x] Service + API + gates
- [x] Admin Referrals UI
- [x] Overview `C3_referral`
- [x] Smoke: NT → DEMO_CLINIC create → accept → complete; pharmacy→pharmacy fail; no link fail

---

## 8. Non-goals

- Booking (C4), status sync đơn bán (C5)
- Clinic Lite / CKS / e-Rx
- Doctor OTP / patient app deep link
