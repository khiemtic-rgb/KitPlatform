# Novixa Clinic — CL3-A Khám từ xa nhẹ (+ hook video B)

**Mã:** NVX-PRD-31-CL3A · **Date:** 2026-07-11  
**Phụ thuộc:** ClinicOS GĐ1 · Connect C3–C5 · Soft-CKS CL2 (không đổi)

## 1. Mục tiêu

MVP **phương án A:** lịch / visit loại online (`remote_async`); bác sĩ gọi ĐT/Zalo **ngoài** Novixa; ghi chẩn đoán + kê đơn trên Clinic → gửi NT (C5 hiện có).

BN **không cần tới PK**. Đơn về NT qua luồng đã có.

**Hạ tầng sẵn cho B:** modality `remote_video`, bảng `encounter_session`, `IEncounterMediaProvider` — **chưa** WebRTC / phòng chờ.

## 2. Giá trị nhà thuốc

```
NT: Giới thiệu + Đặt lịch (modality remote)
  → Connect confirm → clinic_appointment.encounter_modality=remote_async
PK: Bắt đầu khám từ xa (cùng API check-in)
  → BS gọi ngoài → chẩn đoán / kê đơn → Gửi sẵn sàng bán (C5)
NT: Nhận đơn / bán tại quầy
```

## 3. Mô hình dữ liệu

| Thành phần | Giá trị / ghi chú |
|------------|-------------------|
| `encounter_modality` | `in_person` (default) \| `remote_async` (A) \| `remote_video` (B — reserved) |
| Cột trên | `clinic_appointment`, `clinic_visit`, `pack_connect.bookings` |
| `pack_clinic.encounter_session` | Stub Phase A: `session_status=none`, `media_provider=null` |
| Module | `clinic_telemed_remote` (DEMO_CLINIC); `clinic_telemed_video` tắt |

Migration: `migrations/120_clinic_cl3_encounter_modality.sql`  
(Local note: nếu `pack_clinic` owned bởi `pharmacore`, chạy phần DDL clinic bằng role owner rồi `GRANT` cho `kitplatform`.)

## 4. Semantics A

| Hành động | Hành vi |
|-----------|---------|
| Check-in / “Bắt đầu khám từ xa” | Cùng API; copy modality appointment → visit; tạo session stub |
| Walk-in remote | `CreateClinicVisit` + `encounterModality=remote_async` |
| `remote_video` | Reject UI/API cho đến CL3-B |
| Gửi NT | Không đổi path finalize + send |

## 5. Extension points B (không làm sprint này)

```csharp
IEncounterMediaProvider.StartAsync / EndAsync
```

Khi bật `clinic_telemed_video`: modality `remote_video` → điền join URLs + `waiting`/`live` → UI phòng chờ. Schema/session/API modality đã có — không migrate lại encounter core.

## 6. Pháp lý / vận hành (ghi chú)

- BS vẫn chịu trách nhiệm kê đơn như khám tại chỗ.
- Gọi ĐT/Zalo ngoài = **vận hành PK**, không phải tính năng Novixa.
- Soft-CKS / e-Rx quốc gia **không** đổi trong CL3-A.
- Không tuyên bố “telemedicine đầy đủ” / video trong app cho đến CL3-B.

## 7. Non-goals (Phase A)

- WebRTC, phòng chờ, join URL thật
- App BN video
- Map Connect doctor → `clinic_provider` tự động (follow-up)

## 8. Acceptance CL3-A

- [x] Booking / appointment / visit mang `encounter_modality`
- [x] Bridge confirm ghi modality lên `clinic_appointment`
- [x] Check-in remote tạo `encounter_session` status `none`
- [x] UI: Connect chọn hình thức; Clinic tag + **Bắt đầu khám từ xa**; hint visit
- [x] Day summary: số hẹn từ xa trong ngày
- [x] Smoke `scripts/smoke-clinic-cl3-remote-local.ps1`
- [x] Ops checklist nhánh Khám từ xa

## 9. Tài liệu liên quan

- Ops: `docs/novixa/07-customer/ops-checklist-connect-nt-to-clinic-v1.md`
- Soft-CKS: `novixa-clinic-cl2-cks-brief.md`
