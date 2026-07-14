# Novixa ClinicOS — Giai đoạn 1 Brief (MVP thay phần mềm PK)

**Mã:** NVX-PRD-30-G1 · **Tier:** T1 · **Trạng thái:** Approved (scope) · **Version:** 1.0  
**Ngày:** 2026-07-11 · **Owner:** Product + Architecture  
**Phụ thuộc:** [Connect pack brief](./novixa-connect-pack-brief-v1.md) · Pack:Clinic (`078`) · Connect C0–C5 done  
**Checklist chi tiết:** [novixa-clinic-gd1-mvp-checklist.md](./novixa-clinic-gd1-mvp-checklist.md)

---

## 1. Mục tiêu kinh doanh

Triển khai **Novixa Clinic** cho một số phòng khám pilot để họ **không phải chạy đồng thời nhiều phần mềm** (lịch + hồ sơ khám + kê đơn + gửi nhà thuốc).

Khi PK sống trên Clinic:

- **Novixa Pharmacy** phát huy mạng (referral / booking / ready queue / bán) qua **Connect**.
- Nền tảng cho **khám online** sau (video trên Clinic, điều phối qua Connect) — **không** thuộc GĐ1.

| Được GĐ1 | Không GĐ1 |
|----------|-----------|
| ClinicOS MVP vận hành hàng ngày tại PK | HIS / viện đầy đủ |
| Kê đơn nội bộ + gửi NT liên kết | CKS CA production / e-Rx quốc gia |
| Admin `/clinic/*` thay app lõi PK | Telemedicine / video consult |
| Bridge Connect referral/booking → care | Issuer trong Pharmacy tenant (Rx-2 freeze giữ) |

---

## 2. Định vị trong hệ sinh thái

```
Pack:Clinic (ClinicOS GĐ1)  →  khám / lịch / visit / kê đơn nội bộ
Pack:Connect (C0–C5)        →  mạng NT↔PK, referral, booking, ready
Pack:Pharmacy               →  verify + POS + chăm sóc
```

- Tenant Clinic = `org_kind=clinic` (+ bật modules Clinic).
- `DEMO_CLINIC` lab: **được phép** bật Clinic modules (đổi quyết định Connect-only trước đây).
- Không kê/ký dưới `/api/connect/*`.

---

## 3. Persona & ngày làm việc pilot

| Vai trò | Việc GĐ1 phải làm trên Novixa |
|---------|-------------------------------|
| Lễ tân | Tìm/tạo BN · đặt lịch · check-in · mở visit |
| Bác sĩ | Xem lịch · khám visit · ghi chú · kê đơn · hoàn tất · gửi NT |
| Admin PK | Quản lý BS (provider) · liên kết Connect với NT · báo cáo ngày |

**Definition of replace:** sau 2 tuần pilot, PK **ngừng dùng** phần mềm lịch/hồ sơ/kê đơn cũ cho luồng walk-in + hẹn (trừ kế toán/BHYT nếu ngoài scope).

---

## 4. Phạm vi chức năng GĐ1 (tóm tắt)

Chi tiết màn hình/API: **[checklist MVP](./novixa-clinic-gd1-mvp-checklist.md)**.

### Must (P0) — không ship pilot thiếu

1. **Bệnh nhân** — tìm / tạo / sửa (reuse `customers`)
2. **Bác sĩ / provider** — CRUD `clinic_provider` (+ map Connect doctor khi có)
3. **Lịch hẹn** — tạo / đổi trạng thái / ngày
4. **Visit EMR-lite** — mở từ lịch hoặc walk-in · chẩn đoán · notes · đóng
5. **Đơn thuốc nội bộ** — draft + finalize (PDF/in) · gắn visit · **chưa CKS**
6. **Gửi nhà thuốc** — emit Connect `ready_to_dispense` + (ưu tiên) tạo/consume bản đơn Pharmacy từ Clinic
7. **Admin shell `/clinic/*`** + bật module trên tenant PK
8. **RBAC tối thiểu** — lễ tân / BS / admin PK

### Should (P1) — pilot tuần 2–4

- Bridge Connect booking → `clinic_appointment`
- Inbox referral Connect trên Clinic UI
- Lịch sử visit theo BN
- Báo cáo ngày (số khám / đơn gửi NT)

### Out (GĐ1) — ghi rõ với PK

- CKS / USB token / CA
- Liên thông đơn thuốc quốc gia (BYT)
- Video khám online
- Viện phí / BHYT / LIS / PACS
- CRM opportunity pipeline (giữ lead tối thiểu nếu cần)

---

## 5. Gap hiện trạng repo (as-of 2026-07-11)

| Hạng mục | Hiện có | Thiếu GĐ1 |
|----------|---------|-----------|
| Appointments API | `GET/POST /api/clinic/appointments` | Update status, Admin UI |
| Visits + notes API | `/api/clinic/visits*` | Admin UI, close UX |
| `clinic_provider` | Table only | API + UI |
| Patients | `/api/customers` | Clinic-facing UX |
| Clinic Admin nav | Không | `/clinic/*` layout |
| Đơn trong Clinic | Không | `pack_clinic` Rx + PDF |
| Gửi NT | Connect C5 manual/booking | Từ visit finalize |
| DEMO_CLINIC modules | Connect-only | Bật `clinic_*` |
| CKS / national | Không | Out GĐ1 |

---

## 6. Sprint đề xuất GĐ1

| Sprint | Phạm vi | Exit |
|--------|---------|------|
| **CL1.0 Shell** | Modules trên tenant PK · `/clinic` nav · patients + providers | PK login thấy workspace Clinic |
| **CL1.1 Schedule + Visit** | UI lịch + visit + notes · status transitions | Walk-in / hẹn → đóng khám |
| **CL1.2 Rx nội bộ** | Đơn gắn visit · dòng thuốc · PDF/in · audit | BS kê + in được |
| **CL1.3 Handoff NT** | Finalize → Connect ready (+ Pharmacy Rx consume path) | NT thấy & xử lý được |
| **CL1.4 Pilot harden** | Bridge booking/referral · báo cáo ngày · RBAC · smoke | 1 PK bỏ app lịch/hồ sơ cũ |

**Sau GĐ1 (không lẫn):** CL2 CKS · CL3 khám online · CL4 e-Rx quốc gia.

---

## 7. Quyết định đã chốt (GĐ1)

| # | Quyết định |
|---|------------|
| D1 | GĐ1 = **ClinicOS MVP thay phần mềm**, không phải chỉ “Lite ký số” |
| D2 | CKS + national e-Rx + video = **sau** GĐ1 |
| D3 | Issuer lâm sàng = **Clinic tenant**; Pharmacy = verify/bán; Connect = điều phối |
| D4 | Rx-2 portal BS→NT **giữ freeze** (không mở rộng legal issuer) |
| D5 | `DEMO_CLINIC` / PK pilot **được bật** Clinic modules |
| D6 | Đơn GĐ1: trạng thái `draft` / `finalized` (nội bộ) — **không** gọi là “đã ký số” |

---

## 8. Acceptance GĐ1 (pilot PK)

### CL1.0

- [x] `/clinic` Admin shell (overview · patients · providers)
- [x] `/api/clinic/providers` CRUD
- [x] DEMO_CLINIC bật `clinic_*` + seed provider
- [x] Smoke `scripts/smoke-clinic-gd1-local.ps1`

### CL1.1

- [x] Appointment status + check-in → visit
- [x] Admin Lịch hẹn + Lượt khám (notes, đóng khám, walk-in)
- [x] Smoke `scripts/smoke-clinic-gd1-cl11-local.ps1`

### CL1.2

- [x] `pack_clinic.clinic_prescription` + lines
- [x] API create/finalize/cancel/pdf (nhãn nội bộ — không CKS)
- [x] UI kê đơn trong drawer Visit
- [x] Smoke `scripts/smoke-clinic-gd1-cl12-local.ps1`

### CL1.3

- [x] `POST /api/clinic/prescriptions/{id}/send-to-pharmacy`
- [x] `pack_connect.rx_handoffs` snapshot + status `clinic_rx`
- [x] Pharmacy xem đơn / consume (Connect status)
- [x] Smoke `scripts/smoke-clinic-gd1-cl13-local.ps1`

### CL1.4

- [x] `GET /api/clinic/day-summary` + Overview UI
- [x] Bridge Connect booking confirm → `clinic_appointment`
- [x] Patient visit history (Clinic → Bệnh nhân)
- [x] `clinic.read` / `clinic.write` permissions
- [x] Smoke `scripts/smoke-clinic-gd1-cl14-local.ps1`

### GĐ1 full / go-live P0 (CL-GO)

| ID | Hạng mục | Status |
|----|----------|--------|
| CL-GO-02 | Roles `CLINIC_RECEPTION` / `CLINIC_PROVIDER` + API `ClinicRead`/`ClinicWrite` | Done (mig. 131) |
| CL-GO-03 | Reschedule appointment (`PATCH /api/clinic/appointments/{id}`) | Done |
| CL-GO-04 | ClinicPack defaults không `sales` (align mig. 126) | Done |
| CL-GO-05 | Checklist gap column đồng bộ as-built | Done |
| CL-GO-01 | Ops acceptance DEMO_CLINIC ↔ NT (lab smoke) | **Done** (2026-07-14) — `smoke-clinic-chain-local.ps1` CL1.0–CL1.4 + CL2 + settings; handoff → NT_XUANHOA |
| CL-GO-01b | Sign-off PK khách thật tắt app cũ | **Open** (customer acceptance — ngoài lab) |

### GĐ1 full (còn lại)

- [x] Checklist P0 màn hình + API (code) — [mvp-checklist](./novixa-clinic-gd1-mvp-checklist.md)
- [x] **CL-GO-01 (lab):** DEMO_CLINIC walk-in / lịch / Rx / gửi NT_XUANHOA end-to-end (`smoke-clinic-chain-local.ps1`)
- [x] Đơn finalized gửi được tới ≥1 NT Connect (smoke CL1.3 / CL2)
- [ ] **CL-GO-01b:** PK khách thật xác nhận ngừng app lịch + hồ sơ + kê đơn cũ
- [x] Không có endpoint kê đơn dưới `/api/connect/*`
- [x] Smoke scripts CL1.0–CL1.4 (+ CL2 Soft-CKS)

---

## 9. Non-goals & rủi ro

**Non-goals:** HIS đầy đủ · BHYT · CKS · BYT đơn · telemedicine · CRM sales pipeline.

**Rủi ro:** scope creep “full viện”; gọi `finalized` = CKS; nhét kê đơn vào Pharmacy; làm video trước khi offline ổn.

---

## 10. Next

1. Implement **CL1.0** theo checklist P0 (shell + patients + providers).  
2. Handoff kỹ thuật: `.cursor/handoff/novixa-clinic-gd1.md` (khi bắt đầu code).
