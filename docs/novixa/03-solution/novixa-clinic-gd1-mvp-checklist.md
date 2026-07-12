# Novixa ClinicOS GĐ1 — MVP Checklist (thay phần mềm PK)

**Mã:** NVX-PRD-30-G1-CHK · **Brief:** [novixa-clinic-gd1-brief.md](./novixa-clinic-gd1-brief.md)  
**Ngày:** 2026-07-11 · **Pilot goal:** PK chạy một Novixa Clinic cho lịch + khám + kê + gửi NT

Chú thích cột **Gap:** `Ready` = API/schema sẵn · `Partial` = có nền · `Build` = phải làm GĐ1

---

## A. Màn hình Admin bắt buộc (`/clinic/*`)

| ID | Màn hình | Việc người dùng | Prio | Gap |
|----|----------|-----------------|------|-----|
| S01 | **Tổng quan ngày** | Số hẹn hôm nay, visit mở, đơn chờ gửi NT | P0 | Build |
| S02 | **Bệnh nhân** | Tìm SĐT/tên · tạo · sửa hồ sơ tối thiểu | P0 | Partial (`/customers`) |
| S03 | **Bác sĩ / Provider** | Thêm BS, chuyên khoa, CCHN/license, active | P0 | Build (table sẵn) |
| S04 | **Lịch hẹn** | Lịch ngày/tuần · tạo hẹn · check-in · hủy · no-show | P0 | Partial (API create/list) |
| S05 | **Hàng đợi / Visit** | Mở visit từ hẹn hoặc walk-in · đang khám | P0 | Partial (API) |
| S06 | **Chi tiết Visit** | Lý do khám · chẩn đoán · notes · đóng khám | P0 | Partial (API) |
| S07 | **Kê đơn (trong Visit)** | Thêm dòng thuốc · liều · lưu draft · finalize · in/PDF | P0 | Build |
| S08 | **Gửi nhà thuốc** | Chọn NT Connect active · gửi ready + đơn | P0 | Ready (CL1.3) |
| S09 | **Lịch sử BN** | Visits + đơn trước đây | P1 | Build |
| S10 | **Connect (embedded)** | Referral inbox / booking từ NT · deep-link `/connect/*` OK | P1 | Ready (Connect UI) |
| S11 | **Báo cáo ngày** | Số khám, no-show, đơn gửi NT | P1 | Build |
| S12 | **Cấu hình PK** | Chi nhánh, giờ làm việc đơn giản, mẫu in | P1 | Ready (S12 tối thiểu: `/clinic/settings` + PDF header) |

**Không bắt buộc GĐ1 (màn hình):** viện phí, BHYT, kho thuốc PK đầy đủ, video call, ký số CA, CRM pipeline.

---

## B. API bắt buộc

### B1. Đã có — phải dùng / bổ sung

| API | Method | GĐ1 cần thêm | Prio |
|-----|--------|--------------|------|
| `/api/customers` | CRUD hiện có | Clinic UI dùng lại; filter nhanh SĐT | P0 |
| `/api/clinic/appointments` | GET, POST | **PATCH/POST status** (`checked_in`, `completed`, `cancelled`, `no_show`) | P0 |
| `/api/clinic/visits` | GET list/get, POST, PATCH | Đảm bảo close visit (`closed`) | P0 |
| `/api/clinic/visits/{id}/notes` | GET, POST | — | P0 |
| `/api/connect/org-links` | hiện có | PK thấy NT liên kết | P0 |
| `/api/connect/status-events` | POST ready | Gọi từ finalize đơn / đóng khám | P0 |
| `/api/connect/referrals*`, `/bookings*` | hiện có | UI Clinic hoặc deep-link | P1 |

### B2. Phải build GĐ1

| API | Method | Mục đích | Prio |
|-----|--------|----------|------|
| `/api/clinic/providers` | GET, POST, PATCH | CRUD bác sĩ PK | P0 |
| `/api/clinic/prescriptions` | GET, POST | Đơn gắn `visit_id` · lines | P0 |
| `/api/clinic/prescriptions/{id}` | GET, PATCH | draft → finalized | P0 |
| `/api/clinic/prescriptions/{id}/pdf` | GET | In/PDF nội bộ | P0 |
| `/api/clinic/prescriptions/{id}/send-to-pharmacy` | POST | Handoff NT (Connect + Pharmacy consume) | P0 |
| `/api/clinic/day-summary` | GET | Tổng quan S01 | P1 |
| Bridge: Connect booking → appointment | POST internal/API | Đồng bộ lịch | P1 |

### B3. Cấm / Out GĐ1

| Không làm | Lý do |
|-----------|--------|
| `/api/connect/*/prescribe` hoặc ký số | Connect không issuer |
| CKS / CA sign endpoints | GĐ2+ |
| National e-Rx sync | GĐ2+ |
| Video session API | Khám online sau GĐ1 |
| Mở rộng Rx-2 portal legal issuer | Freeze |

---

## C. Luồng end-to-end pilot (acceptance)

```
1. Lễ tân: tạo/tìm BN (S02) → tạo hẹn (S04) hoặc walk-in
2. Check-in → mở Visit (S05–S06)
3. BS: ghi chú + chẩn đoán + kê đơn draft (S07)
4. Finalize đơn → in PDF (S07)
5. Gửi NT liên kết (S08) → Connect ready (+ Pharmacy record)
6. NT: inbox Connect / Rx → verify → bán (Pharmacy)
7. Đóng visit (S06)
```

Pilot **PASS** khi lặp được luồng trên trong 1 ngày làm việc thật, không cần app lịch/hồ sơ/kê cũ.

---

## D. Module & tenant pilot

| Module code | Bắt buộc GĐ1 |
|-------------|--------------|
| `clinic_appointments` | Có |
| `clinic_emr_lite` | Có |
| `novixa_connect` | Có (handoff NT) |
| Customers (kernel) | Có |
| `crm_leads` | Không bắt buộc (optional) |
| Pharmacy POS modules | Không trên tenant PK (trừ hybrid cố ý) |

Tenant: `org_profiles.org_kind = clinic` + bật modules trên.

---

## E. Dữ liệu tối thiểu đơn GĐ1 (không CKS)

| Trường | Bắt buộc |
|--------|----------|
| visit_id, patient, provider, lines (tên thuốc, hàm lượng, số lượng, cách dùng) | Có |
| status: `draft` \| `finalized` | Có |
| finalized_at, finalized_by | Có |
| PDF snapshot hash (optional) | Nên |
| Nhãn UI: “Đã hoàn tất (nội bộ)” — **không** “Đã ký số” | Có |

---

## F. Tiêu chí “thay được phần mềm”

| Tiêu chí | Đo |
|----------|-----|
| Lịch | 100% hẹn pilot trên Novixa |
| Hồ sơ khám | 100% visit pilot trên Novixa |
| Kê đơn | 100% đơn pilot in/gửi từ Novixa |
| Gửi NT | ≥1 NT Connect nhận được mỗi ngày có đơn |
| App cũ | PK xác nhận tắt luồng tương ứng |

---

## G. Mapping sprint ↔ checklist

| Sprint | Screens | APIs |
|--------|---------|------|
| CL1.0 | S02, S03 + shell | providers + customers UX |
| CL1.1 | S01*, S04, S05, S06 | appointment status + visits polish |
| CL1.2 | S07 | clinic prescriptions + PDF |
| CL1.3 | S08 | send-to-pharmacy |
| CL1.4 | S09–S12 | day-summary, bridge, RBAC |
