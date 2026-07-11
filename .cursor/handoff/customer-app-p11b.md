# Customer app P11b — Production hardening (ĐANG LÀM)

> **Trạng thái:** Active · **Maturity:** L0 → L1 · **Gate:** G1  
> Roadmap: [customer-app-roadmap-p12-p18.md](../../docs/novixa/07-customer/customer-app-roadmap-p12-p18.md)  
> Gate checklist: [customer-app-phase-gates-v1.md](../../docs/novixa/07-customer/customer-app-phase-gates-v1.md)  
> Backlog sau P11b: [customer-app-backlog-p12-p18.md](../../docs/novixa/07-customer/customer-app-backlog-p12-p18.md)

## Mục tiêu P11b

Pilot 1–3 NT chạy **production** ổn định: OTP SMS thật, push VAPID, UAT, monitoring. **Không** mở code P12/P15 trước Gate G1.

---

## Sprint plan

| Sprint | Trạng thái | Deliverable |
|---|---|---|
| **S1** | 🔵 In progress | SMS OTP prod, chọn NT, deploy HTTPS, branding |
| **S2** | ✅ Done | Notification i18n server — migration `091`, `ICustomerNotificationTextService` |
| **S3** | ⬜ Pending | Monitoring, UAT 3 NT, sign-off G1 |

---

## S1 — Checklist (ưu tiên ngay)

### Ops / CS

- [ ] Điền bảng NT pilot trong [customer-app-phase-gates-v1.md](../../docs/novixa/07-customer/customer-app-phase-gates-v1.md)
- [ ] Domain HTTPS: `https://app.<tenant>` + `https://api.<tenant>`
- [ ] PostgreSQL production — migration tới `064`
- [ ] CORS: origin app + admin trong `Cors:AllowedOrigins`
- [ ] Admin: branding app khách (logo, màu, `support_phone`)
- [ ] QR / link tenant: `/login?tenant=TENANT_CODE`

### SMS OTP

- [ ] Chốt gateway SMS (provider, URL, API key)
- [ ] Production env:
  ```bash
  CustomerAppSms__Provider=Http
  CustomerAppSms__HttpUrl=<gateway-url>
  CustomerAppSms__ApiKey=<secret>
  CustomerAppAuth__ExposePilotOtpOnCustomerApp=false
  ```
- [ ] Test 1 SĐT thật trên staging trước production

### Push VAPID

```powershell
.\scripts\generate-vapid-keys.ps1 -Subject "mailto:care@domain.vn"
```

- [ ] `CustomerAppPush:PublicKey` / `PrivateKey`
- [ ] `CustomerAppPush:Enabled=true`
- [ ] `.\scripts\verify-push-config.ps1 -BaseUrl https://api.domain.vn`
- [ ] Chi tiết: [docs/customer-app-push-pilot.md](../../docs/customer-app-push-pilot.md)

### Deploy customer-app

```powershell
cd client\customer-app
npm ci
npm run build
# deploy publish/customer-app → HTTPS static host
```

- [ ] `VITE_API_BASE` trỏ đúng API khi build (nếu dùng env)
- [ ] PWA manifest + SW trên HTTPS
- [ ] `.\scripts\deploy-production.ps1` (nếu dùng script chung)

### Dev verify (trước khi lên NT)

```powershell
.\scripts\restart-api.ps1
.\scripts\smoke-test-dev.ps1
cd client\customer-app; npm run build
```

---

## S2 — Notification i18n server ✅

- Migration: `migrations/091_p11b_customer_notification_i18n.sql`
- Service: `ICustomerNotificationTextService` / `CustomerNotificationTextService`
- Keys: `CustomerNotificationTextKeys` (Application)
- Wired: `CustomerPushService`, `CustomerCareReminderService`
- Locale: `customer_accounts.preferred_locale` → fallback tenant default → `vi-VN`
- Tenant override: `tenant_string_translations` keys `customer.notify.*`

**Verify dev:**

```powershell
.\scripts\run-migrations.ps1   # includes 091
.\scripts\restart-api.ps1
```

1. Profile → English (`en-US`)
2. Trigger nhắc thuốc / tái khám / đơn tạm
3. In-app `/notifications` + push title/body phải tiếng Anh

**Exit:** G1.5 trong [phase gates](../../docs/novixa/07-customer/customer-app-phase-gates-v1.md).

---

## S3 — UAT & Gate G1

- [x] Dev UAT 6 kịch bản trên `DEMO_PHARMACY` (2026-07-11) — xem G1.4 cột DEMO
- [ ] Chạy UAT 6 kịch bản × 3 NT pilot — [phase gates G1.4](../../docs/novixa/07-customer/customer-app-phase-gates-v1.md)
- [ ] Đo G1.2 push ≥90%, G1.3 API <1% 5xx (7 ngày)
- [ ] Rollback playbook: tắt `CustomerAppPush:Enabled` nếu spam
- [ ] Chữ ký G1 — mở **P15** (Track B)

---

## CHƯA LÀM (cố ý — xem backlog P12–P18)

- P15 caregiver đa TK
- P12 giỏ hàng / checkout
- P13 e-Rx / OCR
- P14 LLM
- P16–P18

→ [customer-app-backlog-p12-p18.md](../../docs/novixa/07-customer/customer-app-backlog-p12-p18.md)

---

## Lệnh dev nhanh

```powershell
.\scripts\restart-api.ps1
.\scripts\smoke-test-dev.ps1
cd client\customer-app; npm run dev   # localhost:5174
```

Demo dev: `DEMO_PHARMACY` / `0909123456` / OTP `000000`
