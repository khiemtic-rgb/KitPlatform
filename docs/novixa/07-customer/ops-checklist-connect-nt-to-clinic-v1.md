# Checklist vận hành NT → PK (Connect)

**Mã:** NVX-OPS-CONNECT-NT-PK · **Date:** 2026-07-11  
**Phụ thuộc:** C1 org links · C3 referral · C4 booking · Clinic appointments (CL1)

## Nguyên tắc

| Được | Không |
|------|-------|
| Giới thiệu qua referral + PK accept | Sync / kéo danh sách BN từ CRM NT |
| Booking gắn NT **chỉ** từ referral đã accept | Booking “gắn NT” không có referral |
| BN CRM tạo trên **tenant PK** khi confirm | Dùng chung `customer_id` của NT |
| Walk-in / lịch nội bộ PK **không** cần referral | Coi Connect = EMR / bệnh án |

---

## A. Chuẩn bị mạng (một lần)

- [ ] NT và PK đều có Connect org profile đúng `org_kind`
- [ ] C1: org link **active** (invite → accept)
- [ ] (Tuỳ chọn) C2: bác sĩ membership active tại PK nếu gợi ý BS trên referral
- [ ] Login kiểm tra: NT thấy PK trong directory/partners; PK thấy NT

**Gate kỹ thuật:** Không link active → không tạo được referral.

---

## B. Luồng giới thiệu chuẩn (mỗi BN)

| Bước | Ai | Việc | Trạng thái / UI |
|------|-----|------|-----------------|
| 1 | NT | Chọn **KH CRM có sẵn** + Clinic → tạo referral | `pending_clinic_accept` · `/connect/referrals` |
| 2 | PK | Inbox → **Nhận** — tạo/khớp BN trên CRM PK | `accepted` · BN hiện ở Clinic → Bệnh nhân |
| 3 | PK | **Đặt lịch** từ referral (không gõ lại) → Confirm | `proposed`→`confirmed` · lịch Clinic |
| 3b | NT/PK | (Tuỳ chọn) Chọn **Khám từ xa** khi đặt lịch | `encounter_modality=remote_async` |
| 4 | PK | Clinic → Lịch hẹn → **Check-in** / **Bắt đầu khám từ xa** → mở visit | Được khám (tại chỗ hoặc gọi ngoài) |
| 5 | PK | (Sau khám) **Hoàn tất** referral = báo NT | C5 ready — không phải bước khám |

**Huỷ đúng chỗ**

- NT chỉ **cancel** referral khi còn `pending_clinic_accept`
- PK **reject** nếu không nhận case
- PK **cancel / no-show** booking theo state machine C4

---

## B2. Nhánh Khám từ xa (CL3-A)

| Bước | Ai | Việc | Ghi chú |
|------|-----|------|---------|
| 1 | NT hoặc PK | Đặt lịch Connect / Clinic với hình thức **Khám từ xa** | Không chọn Video (CL3-B) |
| 2 | PK | Confirm booking → lịch Clinic tag **Từ xa** | Bridge copy `encounter_modality` |
| 3 | PK | **Bắt đầu khám từ xa** (cùng API check-in) | BN không cần có mặt tại quầy |
| 4 | BS | Gọi ĐT/Zalo **ngoài** Novixa | Vận hành PK — không video trong app |
| 5 | PK | Chẩn đoán + kê đơn trên visit → gửi NT (C5) | Giống visit tại chỗ |

**Không làm Phase A:** WebRTC, phòng chờ, app BN video.

---

## C. Hai loại đặt lịch (quan trọng)

| Loại | Referral | Gắn NT (`pharmacy_tenant_id`) | Khi nào dùng |
|------|----------|-------------------------------|--------------|
| **Nội bộ PK** | Không | Không | Walk-in, BN tự đến, lịch PK tự tạo |
| **NT → PK** | Bắt buộc `accepted` \| `completed` | Lấy từ referral | BN do NT giới thiệu |

**Gate cứng (API C4):**

- Có `pharmacy_tenant_id` mà **không** có `referral_id` hợp lệ → **400**
- Có cả hai → `pharmacy_tenant_id` phải trùng referral
- Không pharmacy + không referral → **được** (lịch nội bộ)

→ Không chặn PK tự đặt lịch; chỉ chặn “gắn NT” ngoài quy trình giới thiệu.

---

## D. Bảo mật / pháp lý (checklist vận hành)

- [ ] Nhân viên NT chỉ gửi **định danh nhẹ** (tên, SĐT, lý do) — không dán bệnh án / đơn cũ vào notes nếu chưa có cơ sở pháp lý
- [ ] PK chỉ mở EMR **sau accept** (và thường sau check-in)
- [ ] Không export CRM NT sang PK; không share login giữa hai tenant
- [ ] BN được thông báo lịch / sẵn sàng lấy thuốc (SMS qua `ISmsTextSender`) khi có SĐT
- [ ] Không tuyên bố “đã chia sẻ hồ sơ y tế đầy đủ NT↔PK”

**SMS Prod:** `CustomerAppSms__Provider=Http` + `CustomerAppSms__HttpUrl` (+ ApiKey nếu gateway yêu cầu). Dev mặc định `Log` (chỉ ghi log).

---

## D2. Hai hộp thư NT (`/connect/status`)

| Tab | Dữ liệu | Việc nhân viên NT |
|-----|---------|-------------------|
| **Đơn → POS** | `rx_handoffs` | **Mở POS để bán** đơn Clinic |
| **Tín hiệu** | `status_events` (không gồm `clinic_rx`) | **Đã nhận tín hiệu** / Bỏ qua — referral/booking/manual |

Không bán từ tab Tín hiệu. Đơn Clinic không trùng hai tab.

PK: tab **Tín hiệu đã gửi** + **Đơn đã gửi** (chỉ xem).

---

## E. Smoke nhanh (DEV)

**Chuỗi API (fail-fast):**

```powershell
.\scripts\smoke-connect-chain-local.ps1
# tuỳ chọn khám từ xa:
.\scripts\smoke-connect-chain-local.ps1 -IncludeCl3A
```

Chạy lần lượt: C1 → C2 → C3 → C4 → C5 (+ CL3-A nếu bật).

**Smoke tay UI:**

1. Login NT → tạo referral tới DEMO_CLINIC  
2. Login PK → accept  
3. PK tạo booking chọn referral → confirm  
4. `/clinic/appointments` thấy lịch → Check-in  
5. Thử API booking có `pharmacyTenantId` không `referralId` → phải fail  
6. NT `/connect/status`: Đơn → POS vs Tín hiệu tách bạch  
7. **CL3-A:** `.\scripts\smoke-clinic-cl3-remote-local.ps1`

Tài khoản mẫu: `DEMO_CLINIC` / `NT_*` · `admin` / `Admin@123`

Brief: `docs/novixa/03-solution/novixa-clinic-cl3-remote-consult-a.md` · Handoff harden: `.cursor/handoff/novixa-connect-harden.md`

---

## F. Lỗi thường gặp

| Hiện tượng | Nguyên nhân | Xử lý |
|------------|-------------|--------|
| Không tạo referral | Chưa org link active / sai org_kind | C1 invite–accept |
| Không thấy referral trên booking | Chưa accept | Accept trước |
| Confirm booking không ra lịch Clinic | Pack Clinic chưa provision / bridge lỗi | Kiểm tra workspace clinic + log API |
| Kỳ vọng thấy BN NT trên PK | Hiểu nhầm sync CRM | Chỉ có BN mới/khớp SĐT trên tenant PK |
| NT tìm đơn trên tab Tín hiệu | Nhầm 2 hộp thư | Dùng tab **Đơn → POS** |
| Không nhận SMS BN | Provider=Log hoặc thiếu HttpUrl / SĐT | Cấu hình `CustomerAppSms__*` Prod; kiểm tra SĐT ≥ 9 số |
