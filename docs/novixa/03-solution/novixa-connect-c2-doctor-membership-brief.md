# Novixa Connect — C2 Doctor Membership Brief

**Mã:** NVX-PRD-20-C2 · **Tier:** T1 · **Trạng thái:** Draft (ready to implement) · **Version:** 1.0  
**Ngày:** 2026-07-11 · **Owner:** Product + Architecture  
**Phụ thuộc:** [novixa-connect-pack-brief-v1.md](./novixa-connect-pack-brief-v1.md) (C0–C1 done)  
**Strangler nguồn:** [rx-prescriber-network-v1.md](../02-product/rx-prescriber-network-v1.md) (`pack_pharmacy.prescribers` / `prescriber_tenant_links`)

---

## 1. Mục tiêu C2

Đưa **bác sĩ (doctor)** vào mạng Connect như **membership thuộc Clinic org** — không thuộc tenant nhà thuốc, không cấp quyền kê đơn pháp lý trong Connect.

| Stakeholder | Giá trị C2 |
|-------------|------------|
| **Clinic** | Quản lý đội ngũ BS thuộc cơ sở (invite / approve / revoke) |
| **Pharmacy** | Thấy BS của Clinic đã liên kết (C1 active) — chuẩn bị C3 referral |
| **Doctor** | Một identity; membership nhiều Clinic; không “ký đơn vào NT” qua Connect |
| **Novixa** | Tách identity BS khỏi Pharmacy Rx-2; chuẩn bị Clinic Lite (Phase 3) |

**Câu định vị một dòng:**  
*C2 = ai thuộc Clinic nào trong mạng Connect — không phải ai được kê đơn vào nhà thuốc.*

---

## 2. Ranh giới pháp lý (bắt buộc)

| Được làm | Không được làm |
|----------|----------------|
| Identity BS (SĐT, CCHN, specialty, status verify) | Phát hành / ký số đơn trong Connect |
| Membership BS ↔ Clinic (state machine) | Coi membership = giấy phép hành nghề tại NT |
| Admin Connect UI quản lý membership | Mở rộng portal kê `signed` → tenant NT |
| Read-model: Pharmacy xem BS của Clinic đã link | Sync/ghi đè `linked_prescribers` Rx-1 như nguồn sự thật mới |

**Freeze giữ nguyên:** Rx-2 portal issuer; Rx-1 POS/verify/audit trong Pharmacy.

---

## 3. Quan hệ với C1 và Rx-2

```
C1 org_links (Pharmacy ↔ Clinic)          [done]
        │
        ▼
C2 doctor_membership (Doctor ↔ Clinic)    ← brief này
        │
        ├─► C3 referral NT → Clinic (dùng membership + org link)
        └─► Strangler: pack_pharmacy.prescribers vẫn tồn tại (read/legacy)
```

| Thành phần hiện có | Quyết định C2 |
|--------------------|---------------|
| `pack_pharmacy.prescribers` | **Giữ** làm identity legacy Rx-2; C2 tạo `pack_connect.doctors` (+ optional `legacy_prescriber_id`) |
| `prescriber_tenant_links` (BS↔NT) | **Không migrate sang Connect**; vẫn phục vụ portal/legacy. Membership Connect = BS↔**Clinic**, không phải BS↔NT |
| `linked_prescribers` | Giữ Pharmacy Rx-1; C2 không thay CRUD sổ NT |
| `pack_connect.org_links` | Membership chỉ hợp lệ khi Clinic tenant tồn tại; Pharmacy xem BS chỉ qua org link `active` |

**Nguyên tắc strangler:** dual-write / backfill tùy chọn trong C2; **cutover đọc** (Pharmacy/Connect UI ưu tiên Connect) có thể ở C2.1 — không bắt buộc xóa bảng Pharmacy trong C2.

**C2.1 soft (2026-07):** Connect Partners/Team đã đọc `pack_connect.doctors`; NT có hint UX. **Chưa làm:** OTP Doctor, backfill `legacy_prescriber_id`, POS/`/rx/prescribers` ưu tiên Connect. Chi tiết: `.cursor/handoff/novixa-connect-harden.md`.

---

## 4. Mô hình dữ liệu (`pack_connect`)

### 4.1 `pack_connect.doctors`

Identity bác sĩ cấp platform (1 người / SĐT; CCHN unique khi có).

| Cột | Ý nghĩa |
|-----|---------|
| `id` | UUID v7 |
| `full_name`, `phone`, `license_number`, `specialty` | Hồ sơ |
| `status` | `pending_verification` \| `active` \| `suspended` |
| `legacy_prescriber_id` | FK nullable → `pack_pharmacy.prescribers(id)` (strangler) |
| `verified_at` / `verified_by` | Manual verify pilot |
| soft-delete `deleted_at` | |

Unique: phone (active), license_number (active, non-empty) — cùng tinh thần migration 100.

### 4.2 `pack_connect.doctor_memberships`

Grain: **1 cặp Doctor ↔ Clinic tenant**.

| Cột | Ý nghĩa |
|-----|---------|
| `doctor_id` | FK doctors |
| `clinic_tenant_id` | FK tenants (org đóng vai `clinic` trong mạng) |
| `membership_role` | `attending` \| `consultant` \| `owner` (mặc định `attending`) |
| `membership_status` | xem state machine |
| `initiated_by` | `clinic` \| `doctor` \| `system` |
| invite/respond/revoke timestamps + actors | Giống pattern C1 / Rx-2 links |
| `notes` | |

Unique `(doctor_id, clinic_tenant_id)`.

**Không** lưu `pharmacy_tenant_id` trên membership — quan hệ Pharmacy chỉ qua C1 `org_links`.

### 4.3 State machine

```
                 Clinic invite
        ┌──────────────────────────► pending_doctor_accept
        │                                    │
        │ Doctor request                     │ Doctor accept
        ▼                                    ▼
 pending_clinic_approval ──Clinic approve──► active
        │                                    │
        │ reject                             │ revoke (clinic hoặc doctor)
        ▼                                    ▼
     rejected                             revoked
```

POV (giống C1): Clinic thấy lời mời đã gửi = `pending_doctor_accept`; Doctor / phía nhận thấy = `pending_our_approval` (API map, DB chỉ lưu một status canonical: `pending_doctor_accept` khi Clinic invite, `pending_clinic_approval` khi Doctor request).

### 4.4 Clinic tenant trong Connect

- Mỗi tenant tham gia Connect **phải** có `pack_connect.org_profiles.org_kind` ∈ `{pharmacy, clinic}`.
- Membership BS chỉ trên tenant `org_kind = clinic` (ví dụ pilot `DEMO_CLINIC`).
- C1 org link roles **derive** từ profile — không cho Pharmacy tự xưng clinic trên cạnh.
- Clinic Lite (appointments/EMR/`pack_clinic`) = Phase 3+; **không** bắt buộc cho Connect org identity.

C2 tạo tenant Clinic mới qua seed/pilot (`DEMO_CLINIC`); không dùng NT giả Clinic.
---

## 5. API (đề xuất)

Prefix: `/api/connect/*` · Gate: `novixa_connect`

| Method | Path | Ai gọi | Việc |
|--------|------|--------|------|
| GET | `/doctors/me` | Doctor JWT (sau) / stub admin | Hồ sơ doctor (C2.1 portal nếu có) |
| GET | `/doctors` | Clinic admin | Search/list doctors trong directory opt-in hoặc theo phone |
| POST | `/clinics/memberships/invite` | Clinic admin | Mời BS (tạo doctor nếu chưa có theo phone+CCHN) |
| POST | `/clinics/memberships/request` | Doctor | Xin vào Clinic |
| GET | `/clinics/memberships` | Clinic admin | List membership POV |
| GET | `/clinics/memberships/pending` | Clinic admin | Hàng chờ approve |
| POST | `/clinics/memberships/{id}/accept` | Doctor | Chấp nhận invite |
| POST | `/clinics/memberships/{id}/approve` | Clinic | Duyệt request |
| POST | `/clinics/memberships/{id}/reject` | Clinic hoặc Doctor | Từ chối pending |
| POST | `/clinics/memberships/{id}/revoke` | Clinic hoặc Doctor | Thu hồi active |
| GET | `/partners/{partnerTenantId}/doctors` | Pharmacy admin | BS `active` của Clinic đã `org_links.active` |

Auth C2.0: **Admin JWT tenant** (Clinic / Pharmacy) là đủ. Doctor OTP portal Connect = **C2.1 optional** (có thể tái dùng pattern prescriber-portal, namespace Connect — không mở kê đơn).

---

## 6. Admin UI (`/connect/*`)

| Tab / bề mặt | C2.0 |
|--------------|------|
| **Network** | Giữ C1 org links |
| **Partners** | Active org links; drill-in: danh sách BS của Clinic partner (read-only phía Pharmacy) |
| **Team / Membership** (tab mới hoặc dưới Partners khi role=clinic) | Invite BS, pending, active, revoke |
| Overview | Phase `C2_doctor_membership`; capabilities `doctor_identity`, `clinic_membership` |

i18n: `connect` namespace (vi + en).

---

## 7. Migration & backfill

1. **Migration 107** (tên dự kiến): `pack_connect.doctors`, `pack_connect.doctor_memberships` + indexes.
2. **Backfill optional (pilot):**  
   - Copy `pack_pharmacy.prescribers` → `pack_connect.doctors` (`legacy_prescriber_id`).  
   - **Không** auto-tạo membership BS↔Clinic từ `prescriber_tenant_links` (vì links hiện là BS↔NT).  
   - Pilot manual: NT_XUANHOA / DEMO đóng role clinic trên 1 chiều C1 → invite 1–2 BS test.
3. Đăng ký file vào `scripts/run-migrations.ps1` + `deploy/ubuntu/migration-files.prod.txt`.
4. Prefer bảng trong `pack_connect` (tránh `ALTER public.tenants` — bài học C1 ownership).

---

## 8. Acceptance C2.0

- [x] Brief này trong `docs/novixa/03-solution/`
- [x] Migration `doctors` + `doctor_memberships`
- [x] Service/repo + API invite / accept|approve / reject / revoke + list pending
- [x] Pharmacy read: `/partners/{id}/doctors` chỉ khi `org_links` active
- [x] Admin Clinic: UI membership; Admin Pharmacy: xem BS partner
- [x] Overview phase `C2_doctor_membership`
- [x] Smoke: Clinic invite doctor → accept → Pharmacy (linked) list thấy BS; Pharmacy không-link → 403/empty
- [x] Explicit: không endpoint kê đơn / ký số trong Connect

> C2.0 dùng **confirm** (clinic pilot) thay doctor OTP accept — C2.1.
---

## 9. Non-goals C2

- Referral / booking (C3–C4)
- Clinic Lite, CKS, đơn quốc gia
- Doctor OTP portal đầy đủ (C2.1)
- Xóa hoặc thay thế `prescriber_tenant_links` / portal kê signed
- Đồng bộ ghi `linked_prescribers` từ Connect membership
- Telemedicine / HIS

---

## 10. Thứ tự implement đề xuất

1. Migration + contracts + DI  
2. Clinic membership write path + pending POV  
3. Pharmacy partner doctors read (gated C1 active)  
4. Admin UI Team + Partners drill-in  
5. Overview + smoke script `smoke-connect-c2-local.ps1`  
6. Handoff `.cursor/handoff/novixa-connect-c2.md`

---

*Next after C2.0: C2.1 soft (Partners hint) done — see harden handoff. Full OTP / Pharmacy read cutover still optional if product asks; otherwise C3–C5 already shipped.*
