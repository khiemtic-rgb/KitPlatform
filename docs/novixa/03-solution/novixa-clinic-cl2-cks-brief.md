# Novixa Clinic — CL2.0 Soft-CKS (mock trước, CA/USB sau)

**Mã:** NVX-PRD-30-CL2 · **Date:** 2026-07-11  
**Phụ thuộc:** ClinicOS GĐ1 (CL1.0–CL1.4) hoàn tất

## 1. Mục tiêu

Thêm bước **ký số có cấu trúc** trên đơn Clinic (`finalized → signed`) bằng **mock signer**, với seam `IClinicPrescriptionSigner` để sau gắn CA/USB thật **không đập** luồng kê đơn / gửi NT.

Soft-CKS **không** có giá trị pháp lý CA. UI/PDF phải ghi rõ **mock / thử nghiệm**.

## 2. Luồng trạng thái

```
draft → finalized → signed → (send-to-pharmacy)
         ↘ cancelled (chỉ từ draft)
```

| Status | Ý nghĩa |
|--------|---------|
| `draft` | Nháp |
| `finalized` | Hoàn tất nội bộ (GĐ1) — **chưa** ký |
| `signed` | Đã ký Soft-CKS (mock) hoặc Hard-CKS (sau) |
| `cancelled` | Hủy nháp |

## 3. Seam kỹ thuật

```csharp
IClinicPrescriptionSigner.SignAsync(ClinicSignRequest) → ClinicSignatureResult
```

| Provider | Code | Khi nào |
|----------|------|---------|
| Mock HMAC | `mock` | CL2.0 — `Clinic:Cks:Provider=mock` |
| CA/USB | `usb_ca` (sau) | Hard-CKS sprint |

Payload ký: `pdf_sha256` (+ prescription id, tenant) — không lưu PDF blob trong bảng chữ ký nếu không cần.

## 4. Lưu trữ

Bảng `pack_connect.clinic_rx_signatures` (ownership app role; tránh ALTER `pack_clinic` owner pharmacore):

- `clinic_prescription_id`, `clinic_tenant_id`
- `signed_at`, `signed_by`
- `signature_alg`, `signature_value`, `signer_cert_thumbprint`
- `signature_provider` (`mock` | …)
- `pdf_sha256` tại thời điểm ký

DTO API: nếu có signature → `prescriptionStatus = signed` (overlay).  
Khi `pack_clinic` cho phép: đồng bộ `prescription_status = signed` (migration optional ALTER).

## 5. API

| Endpoint | Hành vi |
|----------|---------|
| `POST /api/clinic/prescriptions/{id}/sign` | `finalized` → ký → `signed` |
| Send-to-pharmacy | Cho phép `finalized` **hoặc** `signed` (pilot) |

Config: `Clinic:Cks:Enabled`, `Clinic:Cks:Provider`, `Clinic:Cks:MockSigningKey`.

## 6. UI

- Nút **Ký số (thử nghiệm)** sau Hoàn tất
- Tag: **Đã ký (mock)** — không “Đã ký số CA”
- PDF footer phân biệt finalized / mock-signed

## 7. Non-goals

- USB token / HSM / CA production
- e-Rx quốc gia (CL4)
- Endpoint ký dưới `/api/connect/*`
- Marketing Soft-CKS = đủ pháp lý

## 8. Acceptance CL2.0

- [x] Finalize → Sign → status/provider `mock`
- [x] PDF/UI không gọi là CKS CA
- [x] Send-to-pharmacy sau signed vẫn OK
- [x] Đổi `Provider=usb_ca` chỉ cần implement mới + DI (không đổi handoff)
- [x] Smoke `scripts/smoke-clinic-gd1-cl2-cks-local.ps1`

## 9. Hard-CKS (sau — không làm trong CL2.0)

Chọn vendor → middleware desktop → `CaUsbClinicPrescriptionSigner` → `RequireSignedBeforeSend` optional.
