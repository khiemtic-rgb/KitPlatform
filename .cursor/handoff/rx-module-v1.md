# Handoff — Module e-Rx (Rx-1 done · Rx-2 Phase A done)

**Doc:** `docs/novixa/02-product/rx-prescription-module-v1.md` (v1.3)  
**Network:** `docs/novixa/02-product/rx-prescriber-network-v1.md` (v1.0)  
**Pilot:** `NT_XUANHOA`  
**Smoke script:** `scripts/smoke-rx2-portal.ps1` — **15/15 PASS** (2026-07-10, gồm D16 + revoke link)

## Trình tự triển khai (đã chốt)

| Phase | Nội dung | Trạng thái |
|-------|----------|------------|
| **0** | Spec v1.3 (D11–D16), roadmap, migration **100** | ✅ Doc + SQL |
| **A** | Rx-2 MVP: portal, link, directory, kê signed | ✅ Code + UI + smoke local 12/12 |
| **B** | Network v1: mẫu đơn, dashboard, QR | Backlog |
| **C** | P13, tele, BYT | Backlog |

## Decisions (D1–D16)

D1 strict · D2 one receipt · D3 verify configurable · D4 7 days · D5 portal sprint 1  
D7 photo at verify not submit · D8 partial dispense · D9 POS wizard · D10 same branch  
**D11** NT invite → BS accept · **D12** BS request NT → NT approve  
**D13** Portal must pick active NT · **D14** patient = CRM customer  
**D15** CCHN manual verify pilot · **D16** no controlled on portal phase 1

## Kiến trúc Phase A (Healthcare Network v0)

- **Platform contracts:** `src/KitPlatform.Application/Healthcare/` — `IPrescriberIdentityService`, `IPrescriberNetworkService`, `IPrescriptionIssuanceService`, DTO + constants
- **Pharmacy pack adapters:** `src/Packs/Pharmacy/.../Healthcare/*HealthcareAdapter.cs` (strangler — API pack cũ giữ nguyên)
- **Portal channel riêng:** JWT `token_type=prescriber`, routes `/api/prescriber-portal/*`
- **Events:** `healthcare.prescriber.link.active.v1`, `healthcare.prescription.signed.v1` (+ stubs dispensed/consult)
- **Module registry:** migration **103** — `e_rx`, `prescriber_network`, `prescriber_portal`, `telehealth`

## Schema

### Rx-1 (096–099) — deployed pilot

- `linked_prescribers`, `electronic_prescriptions`, lines, attachments, audit, dispense events
- `sales_orders.prescription_id`, `sales_order_items.prescription_line_id`
- `products.dispensing_class`

### Rx-2 foundation (100) — apply before portal code

- `pack_pharmacy.prescribers` — platform identity (phone OTP, CCHN)
- `pack_pharmacy.prescriber_tenant_links` — state: `pending_nt_invite` | `pending_nt_approval` | `active` | `rejected` | `revoked`
- `linked_prescribers.prescriber_id`, `link_id`
- `electronic_prescriptions.prescriber_id`
- `tenants.rx_directory_discoverable`

Backfill: NT_XUANHOA pilot BS → prescriber + link `active` (initiated_by `system`).

### Healthcare network (101 + 103)

| Migration | Nội dung |
|-----------|----------|
| **101** | `prescriber_portal_otp_sessions` (OTP portal) |
| **103** | `care_episode_id` on Rx, `source` + `telehealth`, module registry, backfill `enabled_modules` |

> **Lưu ý:** spec gốc ghi 101 foundation — thực tế **101 = OTP**; foundation module registry dùng **103**.

## Rx-2 build order (Phase A) — checklist

- [x] **Backend:** Prescriber OTP auth (`/api/prescriber-portal/auth/*`), JWT `token_type=prescriber`
- [x] **Link APIs:** invite / accept / request / approve / reject / revoke
- [x] **Directory:** `GET .../pharmacies/directory` (discoverable tenants)
- [x] **Portal prescriptions:** create → `signed` (D13 active link, D16 block controlled)
- [x] **Portal catalog:** `GET .../customers`, `GET .../products` (D14 CRM customer)
- [x] **Admin UI:** `/rx/prescriber-links` — hàng chờ, mời, duyệt, thu hồi (tách module cấp 1 **Đơn thuốc**)
- [x] **Portal UI:** `client/prescriber-portal` — OTP login, links, directory, invites, list Rx, `/prescriptions/new`
- [x] **Platform events:** link active + prescription signed (TX)
- [x] **Healthcare adapters** wired in `PharmacyPackDependencyInjection`
- [ ] Apply migration **100 + 101 + 103** on VPS / local DB chưa migrate
- [x] Full E2E smoke local: BS active → signed Rx → admin queue (`scripts/smoke-rx2-portal.ps1` **15/15**)
- [x] `run-migrations.ps1` includes **094, 098–103**

## Key paths

| Area | Path |
|------|------|
| Healthcare contracts | `src/KitPlatform.Application/Healthcare/` |
| Portal Rx service | `src/Packs/Pharmacy/.../Rx/PrescriberPortalPrescriptionService.cs` |
| Portal API | `src/KitPlatform.Api/Controllers/PrescriberPortal/` |
| Admin link UI | `client/admin/src/modules/sales/PrescriberLinksPage.tsx` |
| Prescriber portal | `client/prescriber-portal/` (dev `:5175`, proxy API `:5290`) |
| Migrations | `migrations/100_*.sql`, `101_prescriber_portal_otp.sql`, `103_healthcare_network_foundation.sql` |

## Permissions

- `rx.prescriber.manage` — CRUD BS trong sổ NT (Rx-1)
- `rx.prescriber.link.manage` — mời / duyệt / thu hồi link (Rx-2 admin)
- `rx.prescription.*` — unchanged
- `rx.prescriber_portal.access` — BS portal (JWT, not ERP role)

## Smoke test local (2026-07-10) — **12/12 PASS**

**Chạy:** `.\scripts\ensure-api.ps1` → `.\scripts\smoke-rx2-portal.ps1`  
**Portal dev:** `cd client/prescriber-portal && npm run dev` (port **5175**, proxy API **5290**)

### Setup DB (lần đầu / sau reset)

```powershell
# Clone dữ liệu dev từ pharmacore (cần postgres superuser)
.\scripts\setup-kitplatform-local.ps1 -PostgresPassword <postgres_superuser>
# Nếu kitplatform đã tồn tại nhưng thiếu quyền sau clone:
# GRANT ALL ON SCHEMA kit_* / pack_* / public TO kitplatform (xem script smoke log)

# Apply Rx migrations (run-migrations.ps1 chưa include 098–103):
psql postgresql://kitplatform:kitplatform_dev_2026@localhost:5432/kitplatform -f migrations/095_rx_dispensing_class.sql
# ... 096, 097, 098, 099, 100, 101, 103

# Reset admin password nếu login 401:
psql ... -f migrations/seed/002_admin_password.sql

# Restart API sau khi sửa DB
.\scripts\ensure-api.ps1
```

**Seed smoke prescriber:** admin mời BS (`/api/pharmacy/prescribers/links/invite`) → BS accept qua portal, hoặc active link thủ công trong DB.

| Bước | Kết quả |
|------|---------|
| API `GET /api/health/db` | ✅ |
| Admin login (`tenantCode=NT_XUANHOA`) | ✅ |
| Prescriber OTP → JWT | ✅ |
| `/auth/me`, linked pharmacies | ✅ |
| Search customers + products (D14) | ✅ |
| `POST /prescriber-portal/prescriptions` → `signed` + `prescriber_portal` | ✅ |
| Admin list signed Rx (không cần verify) | ✅ |
| Block prescribe khi không có active link | ✅ |
| Admin prescriber links API | ✅ |
| Portal build + `:5175` HTTP 200 | ✅ |

**Bug fixed trong smoke:** `SearchProducts` dùng cột `product_units.sort_order` không tồn tại → đổi `ORDER BY pu.is_base_unit DESC, pu.unit_name ASC` (`PrescriberPortalPrescriptionRepository.cs`).

**Dev OTP:** `ExposePilotOtpInResponse=true` (appsettings.json). Có thể thêm `PrescriberPortalAuth:DevBypassCode: "000000"` vào Development.json.

## Acceptance Rx-2 (pilot)

- [x] BS without active link cannot prescribe to that NT — smoke step pass
- [x] NT invite → BS accept → active → prescribe works — verified local (BS 0909999001 @ NT_XUANHOA)
- [x] BS directory request → NT approve → active — smoke E2E `BS request link -> NT approve` (2026-07-10)
- [x] Portal prescription requires NT selection + customer search in that tenant — smoke pass
- [x] Controlled lines rejected on portal create (D16) — smoke pass
- [x] Signed Rx visible on admin without staff verify — smoke pass
- [x] Revoked link blocks new Rx; old Rx still valid until expiry — smoke pass (fix `RevokeLinkAsync` thiếu `@Active`)

## Out of scope Rx-2 MVP

- OCR (P13), tele visit, BYT interchange
- BS public ranking, pharma promos
- Cross-tenant customer merge
- Push/SMS notify on invite (stub only)

## Next steps

1. ~~**VPS pilot NT_XUANHOA:** apply **098–103**, chạy smoke~~ ✅ 2026-07-10 (migrations 102+103 applied, smoke 6/6)
2. ~~**Manual UI:** admin `/rx/prescriber-links` + portal walkthrough~~ ✅ admin prod verified; portal local `:5175`
3. ~~**POS strict dispense:** signed Rx → POS wizard~~ ✅ `smoke-rx2-pos-strict.ps1` 5/5 (fix `product_unit_id` + pos-load COALESCE)
4. **Phase B (kickoff):** B4 dashboard API + B6 POS deep link — ✅ code; còn B1 templates, B3 stock warn, B5 NT dashboard, notify

## Catalog demo (BS gõ thuốc theo NT)

**Seed:** `migrations/seed/005_rx_portal_catalog_demo.sql` (NT_XUANHOA)

| Code | Loại | Gõ thử |
|------|------|--------|
| `RXDEMO-PARA` | OTC | Para |
| `RXDEMO-AMOX` | Rx | Amox |
| `RXDEMO-AMLO` | Rx | Amlo |
| `RXDEMO-CTRL` | Controlled | *ẩn portal* (D16) |
| KH `RXDEMO-KH` | — | `0908888001` |

```powershell
.\scripts\smoke-rx2-catalog.ps1 -ApplySeed
```

Portal: chọn NT → ô thuốc load catalog; gõ lọc live (tag Rx/OTC).

## VPS pilot (2026-07-10)

- `deploy-update-vps.ps1 -RunMigrations` — applied **102**, **103** (100/101 skipped/already applied)
- `smoke-rx2-vps.ps1` — **6/6 PASS** on `https://api.novixa.vn`
- Prescriber portal: **https://prescriber.novixa.vn** ✅ (DNS + certbot 2026-07-10, HTTPS 200)

## Phase B kickoff (2026-07-10)

| Item | Trạng thái |
|------|------------|
| **B4** Dashboard BS | ✅ `GET /api/prescriber-portal/dashboard` + Home UI stats |
| **B6** POS deep link | ✅ `GET .../prescriptions/{id}/share` + copy link; admin `?rx=` opens drawer |
| B1 Mẫu đơn | Backlog |
| B3 Cảnh báo tồn | ✅ Portal `stockAvailableQty` + UI warning (không chặn ký) |
| B5 Dashboard NT | Backlog |
| Notify push/SMS | Backlog |
