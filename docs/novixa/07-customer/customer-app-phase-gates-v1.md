# Novixa Customer App — Phase gates (G0–G5)

**Mã:** NVX-CS-09 · **Tier:** T2 · **Trạng thái:** Active · **Version:** 1.0  
**Ngày:** 2026-07-09 · **Owner:** Product + CS + Engineering

**Liên quan:**
- [customer-app-roadmap-p12-p18.md](./customer-app-roadmap-p12-p18.md) (NVX-PRD-09)
- [go-live-checklist-customer-v1.md](./go-live-checklist-customer-v1.md) (NVX-CS-02)
- [pilot-smoke-test-checklist-v1.md](./pilot-smoke-test-checklist-v1.md) (NVX-CS-08)
- Handoff đang chạy: `.cursor/handoff/customer-app-p11b.md`

> ⛔ **Không mở phase kế tiếp** nếu gate hiện tại chưa pass. Tick cùng Novixa + chủ NT.

**Maturity:** L0 (P11a) → L1 (P11b) → L2 (P15) → L3 (P12+P13) → L4 (P14) → L5 (P16–P18)

---

## Thông tin pilot

| NT | Mã tenant | Ngày bắt đầu pilot app | Người phụ trách NT | Người phụ trách Novixa |
|---|---|---|---|---|
| | | | | |
| | | | | |
| | | | | |

---

## Gate G0 → G1 — Vào P11b / hoàn tất P11b (L0 → L1)

**Phase:** P11b · **Mục tiêu:** Production stable — OTP thật, push, monitoring.

### G0 — Điều kiện bắt đầu P11b

- [ ] P11a dev checklist pass trên `DEMO_PHARMACY`
- [ ] Migration chạy tới `064_p10b_customer_app_i18n.sql` trên staging/production
- [ ] Đã chọn ≥1 NT pilot (mục tiêu 3 NT)

### G1.1 — OTP SMS production

- [ ] `CustomerAppSms:Provider` = `Http` (không `Log`) trên production
- [ ] `CustomerAppSms:HttpUrl` + `ApiKey` cấu hình đúng gateway NT/Novixa
- [ ] `ExposePilotOtpOnCustomerApp` = `false` trên production
- [ ] 3 NT: login OTP thật **7 ngày liên tiếp** không lỗi mass

| NT | Ngày bắt đầu 7 ngày | Pass? |
|---|---|---|
| 1 | | ☐ |
| 2 | | ☐ |
| 3 | | ☐ |

### G1.2 — Push Web (VAPID)

- [ ] VAPID keys sinh và gán `CustomerAppPush`
- [ ] `CustomerAppPush:Enabled=true`
- [ ] `verify-push-config.ps1` pass trên API production
- [ ] Delivery rate subscription hoạt động ≥ **90%** (đo 7 ngày)

Chi tiết: [docs/customer-app-push-pilot.md](../../customer-app-push-pilot.md)

### G1.3 — API ổn định

- [ ] Customer-app API 5xx < **1%** / 24h (7 ngày rolling)
- [ ] CORS origin app + admin đúng
- [ ] HTTPS app + API

### G1.4 — UAT P11a (6 kịch bản / NT)

> **Dev UAT (DEMO_PHARMACY)** — 2026-07-11 · UI `http://localhost:5174` + API `:5290`  
> Tenant pilot thật (NT1–3) vẫn ☐ — cần ops S1.

| # | Kịch bản | DEMO | NT1 | NT2 | NT3 |
|---|---|---|---|---|---|
| 1 | Đăng nhập OTP → Home | ✅ | ☐ | ☐ | ☐ |
| 2 | Profile → bật push → nhận nhắc uống thuốc | ⚠️ consent+push On; delivery push chưa đo | ☐ | ☐ | ☐ |
| 3 | Profile → English → refresh giữ locale | ✅ | ☐ | ☐ | ☐ |
| 4 | Nhắc tái khám / hết đơn → in-app + push | ✅ in-app; push delivery chưa đo | ☐ | ☐ | ☐ |
| 5 | Chat dược sĩ (consent + tin nhắn) | ✅ (có cảnh báo load chat thoáng qua) | ☐ | ☐ | ☐ |
| 6 | Đơn O2O / đặt trước / POS hoàn tất | ✅ xem draft/reservation/purchase; POS quầy chưa UAT | ☐ | ☐ | ☐ |

### G1.5 — P11b-S2 (notification i18n server)

- [x] Migration `091_p11b_customer_notification_i18n.sql`
- [x] `ICustomerNotificationTextService` — push/in-app theo `preferred_locale`
- [x] UAT: Profile EN → notification title/body tiếng Anh (dev) — ví dụ `Reminder scheduled` / `UAT G1 follow-up — …`

### Chữ ký Gate G1

| | Ngày | Chữ ký |
|---|---|---|
| Novixa Engineering | | |
| Novixa CS | | |
| Chủ NT pilot (≥1) | | |

**Khi G1 pass → mở P15 + P12 (Track B: ưu tiên P15).**

---

## Gate G2 — P15 Done (L1 → L2)

**Phase:** P15 · **Mục tiêu:** Continuous care — caregiver + analytics.

- [ ] G1 đã pass
- [ ] ≥1 luồng mời caregiver (SĐT) → accept thành công trên pilot
- [ ] Caregiver confirm dose (Đã uống) trên `FamilyCaregiverDuePanel`
- [ ] `/insights` load <3s; số liệu khớp POS (chi tiêu, adherence)
- [ ] Health PDF export 1 case pilot

| NT | Caregiver invite OK | Insights OK |
|---|---|---|
| 1 | ☐ | ☐ |
| 2 | ☐ | ☐ |
| 3 | ☐ | ☐ |

**Khi G2 pass → P12 full speed (Track B).**

---

## Gate G3 — P12 + P13 Done (L2 → L3)

**Phase:** P12 + P13 · **Mục tiêu:** Transaction complete + e-Rx.

### G3a — P12 commerce

- [ ] G2 pass (khuyến nghị) hoặc G1 + Track A approved
- [ ] 10 đơn OTC checkout E2E trên pilot không lỗi nghiêm trọng
- [ ] Rx upload → DS approve/reject ≥5 case
- [ ] COD hoặc QR chuyển khoản confirm ≥3 case

### G3b — P13 e-Rx

- [ ] `electronic_prescriptions` import từ POS/draft
- [ ] OCR → DS confirm ≥3 case (không auto kê đơn)
- [ ] Auto reminder từ e-Rx approved ≥3 case
- [ ] Treatment timeline hiển thị đúng

**Khi G3 pass → mở P14.**

---

## Gate G4 — P14 Done (L3 → L4)

- [ ] G3 pass
- [ ] 100% AI response có disclaimer
- [ ] Test suite ≥20 cặp hoạt chất interaction
- [ ] `suggestChat=true` khi confidence thấp — không tự chẩn đoán

**Khi G4 pass → mở P16.**

---

## Gate G5 — P16–P18 (L4 → L5)

### G5a — P16

- [ ] CSAT response rate ≥15% đơn hoàn tất (pilot 30 ngày)
- [ ] Knowledge Center ≥5 bài admin publish
- [ ] Campaign banner 1 chương trình pilot

### G5b — P17

- [ ] Store submission pass (iOS + Android hoặc 1 nền tảng đã chốt)
- [ ] Push native parity với PWA
- [ ] ≥1 gateway payment live (MoMo/VNPay/ZaloPay)

### G5c — P18

- [ ] Product review moderation workflow
- [ ] Assessment integration 1 template live
- [ ] BLE device POC demo nội bộ

---

## Decision: Track A exception

Chỉ **P12 trước P15** khi:

- [ ] G1 pass
- [ ] Sales lead ký (hợp đồng cần e-commerce ≤60 ngày)
- [ ] CS lead chấp nhận trễ L2

File ký: ghi chú ngày vào bảng pilot § trên.

---

**Changelog**

| Version | Ngày | Thay đổi |
|---|---|---|
| 1.0 | 2026-07-09 | Khởi tạo G0–G5 |
