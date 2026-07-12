# Novixa Connect — Product & Architecture Brief v1

**Mã:** NVX-PRD-20 · **Tier:** T1 · **Trạng thái:** Approved (scaffold) · **Version:** 1.0  
**Ngày:** 2026-07-11 · **Owner:** Product + Architecture  
**Phụ thuộc:** Định hướng Healthcare Network · [platform-kernel-and-solution-packs-v1.md](./platform-kernel-and-solution-packs-v1.md)

---

## 1. Mục tiêu

Xây **Novixa Connect** như workspace / solution pack **độc lập** — nền tảng điều phối giữa nhà thuốc, phòng khám, bác sĩ và bệnh nhân.

Connect **không**:

- Khám chữa bệnh
- Phát hành / ký số đơn thuốc
- Thay thế HIS / EMR đầy đủ
- Nhúng vào module Nhà thuốc (Pharmacy / Đơn BS)

Connect **có**:

- Mạng tổ chức (Pharmacy ↔ Clinic ↔ Doctor membership)
- Referral / booking (Phase 2+)
- Theo dõi trạng thái đơn & điều phối (consume từ Clinic / đơn quốc gia — sau)
- Partner & thống kê mạng

---

## 2. Ranh giới pháp lý (bắt buộc)

| Vai trò | Chủ thể |
|---------|---------|
| Khám / lập bệnh án / kê / ký số | Cơ sở khám được cấp phép (Clinic Lite — Phase 3) |
| Xác thực đơn & bán thuốc | Nhà thuốc (Pharmacy) |
| Điều phối, liên kết, booking, trạng thái | **Novixa Connect** |
| Phát hành e-Rx quốc gia | Hệ thống đơn thuốc quốc gia + Clinic issuer |

**Freeze:** không mở rộng portal BS kê `signed` vào tenant NT như e-Rx pháp lý (Rx-2 issuer). Rx-1 POS/verify/audit **giữ** trong Pharmacy.

---

## 3. Định vị trong hệ sinh thái

```
KitPlatform kernel (IAM, org, events, notify, audit)
├── Pack:Pharmacy     → Novixa Pharmacy workspace
├── Pack:Connect      → Novixa Connect workspace   ← brief này
├── Pack:Clinic       → Clinic Lite (sau Connect ổn)
└── Pack:Survey       → KAP
```

Admin surface: **`/connect/*`** — top-level nav, **không** nằm dưới `/rx` hay `/sales`.

API prefix: **`/api/connect/*`**  
Schema (khi có bảng): **`pack_connect.*`**  
Tenant package: **`novixa_connect`**  
Platform module gate: **`novixa_connect`** (+ module con sau này)

---

## 4. Phase Connect (trong Phase 2 lộ trình lớn)

| Sprint | Phạm vi |
|--------|---------|
| **C0 — Scaffold (done)** | Pack + DI + overview API + admin shell + migration module/package |
| **C1 — Org links (done)** | Org types (pharmacy/clinic), partner link state machine |
| **C2 — Doctor membership (done C2.0)** | [Brief C2](./novixa-connect-c2-doctor-membership-brief.md) — BS thuộc Clinic; strangler identity từ Rx-2 |
| **C3 — Referral (done C3.0)** | [Brief C3](./novixa-connect-c3-referral-brief.md) — Pharmacy → Clinic |
| **C4 — Booking stub (done C4.0)** | [Brief C4](./novixa-connect-c4-booking-brief.md) — slot + notify |
| **C5** | Status sync hooks (đơn sẵn sàng bán → Pharmacy consume) — **done C5.0** |

Clinic Lite (ký số, đơn quốc gia) = **sau ClinicOS GĐ1**. GĐ1 = MVP thay phần mềm PK — xem [novixa-clinic-gd1-brief.md](./novixa-clinic-gd1-brief.md).

---

## 5. Quan hệ với Rx hiện tại

| Thành phần | Quyết định |
|------------|------------|
| POS strict, verify, staff_entry | Giữ Pharmacy |
| `prescribers` / links BS↔NT | Strangler → Connect membership (C2) |
| Portal kê signed → NT | Freeze / legacy pilot label |
| B5 dashboard NT | Giữ khung; KPI chuyển dần sang Connect referral/queue |
| Application.Healthcare contracts | Spine dùng chung; Connect adapters sau |

---

## 6. Acceptance C0 (scaffold)

- [x] Brief này trong `docs/novixa/03-solution/`
- [x] `src/Packs/Connect/` Application + Infrastructure + `AddConnectPack`
- [x] `GET /api/connect/overview` gated `novixa_connect`
- [x] Admin `/connect/overview` (+ tabs stub Network / Partners)
- [x] Migration đăng ký `tenant_package` + `platform_module_registry`
- [x] Workspace provisioner gọi `novixa_connect` khi module bật

---

## 6b. Acceptance C1 (org links)

- [x] Migration `pack_connect.org_links` + `pack_connect.directory_opt_in`
- [x] API invite / accept / reject / revoke + directory + pending POV
- [x] Admin Network UI + Partners (active)
- [x] Overview phase `C1_org_links`
- [x] Smoke `scripts/smoke-connect-c1-local.ps1`

---

## 7. Non-goals C0–C1

- Referral/booking UI thật
- Video consult
- Ký số / đơn quốc gia
- Doctor membership (→ C2 brief)

---

## 8. C2 pointer

Chi tiết: **[novixa-connect-c2-doctor-membership-brief.md](./novixa-connect-c2-doctor-membership-brief.md)** · Handoff: `.cursor/handoff/novixa-connect-c2.md`

### Acceptance C2.0

- [x] Migration `doctors` + `doctor_memberships`
- [x] API invite / confirm / approve / reject / revoke + partner doctors (C1-gated)
- [x] Admin Team + Partners doctors
- [x] Overview `C2_doctor_membership`
- [x] Smoke `scripts/smoke-connect-c2-local.ps1`
- [x] Clinic org foundation: `org_profiles` + `DEMO_CLINIC` (108); membership chỉ clinic

Handoff nền Clinic: `.cursor/handoff/novixa-connect-clinic-org.md`

---

## 9. C3 pointer

Brief: **[novixa-connect-c3-referral-brief.md](./novixa-connect-c3-referral-brief.md)** · Handoff: `.cursor/handoff/novixa-connect-c3.md`

### Acceptance C3.0

- [x] Migration `pack_connect.referrals`
- [x] API create/accept/reject/complete/cancel + inbox
- [x] Admin Referrals
- [x] Overview `C3_referral`
- [x] Smoke `scripts/smoke-connect-c3-local.ps1`

---

*Next: ClinicOS GĐ1 (MVP thay phần mềm PK) — [brief](./novixa-clinic-gd1-brief.md) · [checklist](./novixa-clinic-gd1-mvp-checklist.md).*

---

## 10. C4 pointer

Brief: **[novixa-connect-c4-booking-brief.md](./novixa-connect-c4-booking-brief.md)** · Handoff: `.cursor/handoff/novixa-connect-c4.md`

### Acceptance C4.0

- [x] Migration `pack_connect.bookings`
- [x] API + notify stub
- [x] Admin Bookings
- [x] Overview `C4_booking`
- [x] Smoke `scripts/smoke-connect-c4-local.ps1`

---

## 11. C5 pointer

Brief: **[novixa-connect-c5-status-sync-brief.md](./novixa-connect-c5-status-sync-brief.md)** · Handoff: `.cursor/handoff/novixa-connect-c5.md`

### Acceptance C5.0

- [x] Migration `pack_connect.status_events`
- [x] Emit từ booking/referral complete + clinic manual
- [x] Pharmacy consume/dismiss + Admin Status UI
- [x] Overview `C5_status_sync`
- [x] Smoke `scripts/smoke-connect-c5-local.ps1`
