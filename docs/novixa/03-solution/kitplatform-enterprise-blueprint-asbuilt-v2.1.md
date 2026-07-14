# KIT Platform — Enterprise Blueprint (As-Built)

**Mã:** KIT-BP-ASBUILT · **Version:** 2.1.1 · **Trạng thái:** Active  
**Ngày đồng bộ:** 2026-07-14 · **Scope:** Mirror kiến trúc **đã xây** trên repo KitPlatform  
**Hub:** DOC-010 (Technical Standard) · **Verify:** `scripts/check-kit-bp-asbuilt.ps1`  
**Liên quan:** [platform-kernel-and-solution-packs-v1.md](./platform-kernel-and-solution-packs-v1.md) (KIT-PLT-01) · [solution-architecture-v1.md](./solution-architecture-v1.md) · [domain-map-v1.md](../domains/domain-map-v1.md) · [pharmacy-success-capability-map-v1.md](../02-product/pharmacy-success-capability-map-v1.md) (NVX-PRD-03 — GTM capability → module)

> Đây là **single source of truth kỹ thuật as-built** cho AI/agent và contributor.  
> Không mô tả tầm nhìn HospitalOS/Blazor/EF trừ khi ghi rõ `status: roadmap`.  
> Evolve **additive** — không redesign stack trừ khi có chỉ thị tường minh.  
> Nguồn pack metadata: `*PackDefinition.cs` — nếu lệch code, **ưu tiên code** rồi cập nhật blueprint.

Machine-readable copy: [`kitplatform-blueprint-asbuilt-v2.1.yaml`](./kitplatform-blueprint-asbuilt-v2.1.yaml)

---

## 1. Platform (đã ship)

| Hạng mục | Giá trị as-built |
|----------|------------------|
| Architecture | **Modular monolith** — 1 deployable API host + Solution Packs |
| Runtime | **.NET 10** (`net10.0`) |
| Language | C# |
| Database | **PostgreSQL** |
| Data access | **Dapper** + SQL thuần (không EF Core) |
| Schema change | **Database-first** — `migrations/*.sql` + ledger `public.kit_schema_migrations` |
| API | REST — prefix `/api/...` (**không** bắt buộc `/api/v1/`) |
| Auth | JWT (access + refresh) |
| Authorization | RBAC permissions + **platform module/feature gates** |
| Multi-tenant | `tenant_id` trên dữ liệu nghiệp vụ; Core ceiling `allowed_modules` / `max_branches` |
| Cache / Redis | Optional / ready — không bắt buộc mọi path |
| Object storage | File service (S3-compatible khi cấu hình) |
| Search | PostgreSQL (ILIKE / full-text nơi đã có) |
| UI channels | **React + Vite + TypeScript** SPAs — **không** Blazor |

### Deployable channels (adapters UI)

| App | Path | Vai trò |
|-----|------|---------|
| Admin Web | `client/admin` | ERP + Clinic + Connect + KAP + System |
| Staff App | `client/staff-app` | POS / kho mobile (PWA) |
| Customer App | `client/customer-app` | O2O khách (PWA) |
| Assessment Web | `client/assessment-web` | KAP public survey |
| Prescriber Portal | `client/prescriber-portal` | Rx-2 portal BS (pharmacy path; freeze mở rộng legal) |
| Partner Portal | `client/partner-portal` | KAP CTV / partner |

---

## 2. Design principles (as practiced)

1. **Modular monolith** — pack additive, không split microservice sớm  
2. **Platform Kernel + Capabilities + Packs** (3 lớp)  
3. **API + SQL first** — migration SQL là nguồn schema  
4. **Multi-tenant + vertical** (`business_vertical`, module registry)  
5. **Entitlement by SKU** — Core `allowed_modules` / `max_branches`; tenant bật trong ceiling  
6. **Event-ready** — `integration_outbox` (outbound webhook), chưa event bus nội bộ đầy đủ  
7. **Security by design** — JWT, RBAC, audit logs, soft-delete nơi áp dụng  
8. **AI where useful** — KAP narrative / copilot hooks; không “AI First” mọi module  
9. **Clean-ish layers** — Application contracts + Infrastructure; Domain mỏng; **không** ép Aggregate mọi bảng  

---

## 3. Solution packs (đã có trong code)

Nguồn: `src/Packs/*/…/*PackDefinition.cs`. Hai danh sách:

- **`PackModuleCodes`** — module “thuộc” pack (pack-owned SKUs)  
- **`DefaultEnabledModules`** — gợi ý bật khi provision pack (có thể gồm capability dùng chung)

| Pack | Pack code | Tenant package | Display (code) |
|------|-----------|----------------|----------------|
| Pharmacy | `pharmacy` | `novixa_pharmacy` | Novixa Pharmacy Pack |
| Clinic | `clinic_crm` | (workspace `clinic_crm`) | Novixa Clinic (ClinicOS) |
| Connect | `novixa_connect` | `novixa_connect` | Novixa Connect |
| Survey / KAP | `pharmacy_survey` | `pharmacy_survey` | Pharmacy Survey Pack |

### 3.1 Defaults & pack-owned (đồng bộ code)

| Pack | DefaultEnabledModules | PackModuleCodes |
|------|----------------------|-----------------|
| **Pharmacy** | inventory, procurement, sales, loyalty, customer_app, medication, health_wallet, reservations, reports, **e_rx, prescriber_network, prescriber_portal** | medication, health_wallet, reservations, e_rx, prescriber_network, prescriber_portal |
| **Clinic** | clinic_appointments, clinic_emr_lite, **novixa_connect** *(không sales / retail — align mig. 126)* | clinic_appointments, clinic_emr_lite, **crm_leads** *(pack SKU, không nằm trong default)* |
| **Connect** | novixa_connect | novixa_connect |
| **Survey** | assessment, pharmacy_survey, **reports** | assessment, pharmacy_survey |

### 3.2 Workspace auto-provision (`WorkspacePackProvisioner`)

Khi lưu entitlement / đảm bảo workspace:

| Pack workspace | Điều kiện |
|----------------|-----------|
| `novixa_pharmacy` | **Luôn** provision |
| `clinic_crm` | Bật bất kỳ: `clinic_appointments`, `clinic_emr_lite`, `crm_leads` |
| `novixa_connect` | Bật `novixa_connect` |
| `pharmacy_survey` | **Không** auto trong provisioner runtime — historically migration `084` backfill |

### 3.3 Commercial UI tree vs pack ownership

`client/admin/.../platform-module-tree.ts` nhóm SKU theo **gói bán** (Pharmacy / Clinic / Connect / Survey KAP). Có thể khác ownership trong `*PackDefinition` (ví dụ e_rx hiển thị gần Connect; `crm_leads` gần Survey).

**SSOT kỹ thuật:** PackDefinition + `PlatformModuleCodes`. UI tree = presentation.

Templates Core (`PLATFORM_PACK_TEMPLATES`): `novixa_pharmacy`, `novixa_pharmacy_core`, `novixa_clinic`, `novixa_clinic_plus_telemed`, `novixa_connect`, `survey_kap`.

**Brand thương mại:** Novixa = solution trên KIT Platform.

**Roadmap (không generate như đã ship):** FamilyOS, HospitalOS, RetailOS, EnterpriseOS, Hard-CKS, BHYT, HIS adapter.

**Clinic non-goals GĐ1:** full HIS, BHYT, hard-CKS, national e-rx. Module registry có `clinic_telemed_remote` / `clinic_telemed_video` (gate) — product video chưa full ship; chưa có trong `PlatformModuleCodes.All`.

---

## 4. Layer map

### Layer A — Platform Kernel

Tenant/org, branch/warehouse, identity (users/employees/roles), settings JSONB, module registry, locales/i18n, audit, integration outbox, platform entitlement APIs (`/api/platform/...`).

### Layer B — Shared capabilities (pack bật/tắt)

Catalog, sales/POS, inventory (FEFO), procurement, pricing, customer/loyalty, AR/AP (receivables/payables), notifications (OTP/push), reports/dashboard, workflow engine (mầm), AI orchestrator hooks.

### Layer C — Pack-specific

| Pack | Ví dụ |
|------|--------|
| Pharmacy | medication reminders, health wallet, national drug, QD540 export foundation |
| Clinic | appointments, visits EMR-lite, clinic Rx + PDF + soft-CKS, day summary |
| Connect | org links, doctor membership, referrals, bookings, status events, rx_handoffs |
| Survey | assessment engine, KAP admin, partner portal, campaign |

Pack **không** copy `customers` / `sales_orders` — kế thừa ID kernel.

---

## 5. Platform module catalog (C#)

`PlatformModuleCodes.All` (`src/KitPlatform.Application/Core/PlatformModuleCodes.cs`):

`inventory`, `procurement`, `sales`, `loyalty`, `customer_app`, `medication`, `health_wallet`, `reservations`, `reports`, `clinic`, `clinic_appointments`, `clinic_emr_lite`, `crm_leads`, `lab`, `spa`, `assessment`, `pharmacy_survey`, `e_rx`, `prescriber_network`, `prescriber_portal`, `telehealth`, `novixa_connect`

Registry SQL có thể có thêm code (vd. telemed clinic) trước khi sync vào C#.

Admin sidebar: `client/admin/src/modules/registry.tsx` — map key UI → `platformModule`; `rx` đang **temp-hidden**.

---

## 6. Code layout (generate theo đây)

```
src/
  KitPlatform.Api/                 # Presentation — controllers, filters, Program.cs
  KitPlatform.Application/         # Contracts, DTOs, validators, Core engines interfaces
  KitPlatform.Domain/              # Thin / shared domain types
  KitPlatform.Infrastructure/      # Dapper repos, engines, workers
  Packs/
    Pharmacy|Clinic|Connect|Survey/
      *.Application/               # Pack contracts + pack definition
      *.Infrastructure/            # Pack repos/services
client/                            # React SPAs (channel adapters)
migrations/                        # Numbered SQL — single source of schema
tests/                             # xUnit (platform + packs)
```

**Module template as-built (mỗi capability/pack feature):**

1. Migration SQL (additive)  
2. Dapper repository / SQL  
3. Application interface + DTO  
4. Infrastructure service  
5. API controller + `[RequirePlatformModule]` / roles khi cần  
6. Permission seed (nếu có)  
7. Admin/Staff UI (React) khi có surface  
8. Unit/smoke test khi rủi ro cao  
9. Cập nhật blueprint nếu đổi PackDefinition / module catalog  

**Cấm khi generate:**

- EF Core DbContext / Blazor pages  
- Duplicate table / entity đã có  
- Business logic nặng trong Controller  
- Hard-code “thuốc” trong Core engines  
- Đập schema / rename breaking không migration  
- Invent HospitalOS / HIS adapter như đã ship  

---

## 7. Entity & database rules (as-built)

| Rule | Practice |
|------|----------|
| PK | UUID (`id`) |
| Tenant | `tenant_id` trên hầu hết bảng nghiệp vụ |
| Branch | `branch_id` **khi** thuộc ngữ cảnh chi nhánh — **không** bắt buộc mọi bảng |
| Naming | `snake_case` tables/columns |
| Soft delete | `deleted_at` (ưu tiên) — không bắt buộc cột `IsDeleted` boolean riêng |
| Audit | `created_at` / `updated_at` (+ by khi có); `audit_logs` cho thao tác quan trọng |
| Status | `SMALLINT` hoặc `VARCHAR` theo domain đã có — không ép một enum toàn cục |

Base fields “mọi entity phải có Code/Name/BranchId” — **không áp dụng**.

---

## 8. API rules (as-built)

- Routes: `/api/{area}/...` (system, platform, sales, inventory, clinic, connect, survey, public/assessment, …)  
- Auth: Bearer JWT; một số public/OTP/platform-key  
- Success: thường `200/201` + JSON body DTO trực tiếp  
- Error: `400/401/403/404` + `{ message }` hoặc ProblemDetails — **không** bắt buộc envelope `{ success, data }`  
- Module gate: `RequirePlatformModule` / feature filters  
- Platform Core: `X-Platform-Key` cho `/api/platform/tenants/.../entitlement`  

---

## 9. Entitlement & commercial SKUs

```
settings.platform:
  schema_version
  vertical                 # pharmacy | clinic | …
  allowed_modules[]        # Core ceiling
  enabled_modules[]        # ⊆ allowed
  max_branches             # null = unlimited
  features{}
  i18n{}
```

| Thành phần | Path |
|------------|------|
| Validator | `TenantPlatformSettingsValidator` |
| Services | `TenantPlatformSettingsService`, `PlatformTenantService` |
| Core APIs | `GET/PUT /api/platform/tenants/{id}/entitlement`, `GET /api/platform/modules` |
| Tenant APIs | `GET/PUT /api/system/tenant-platform` |
| UI Core | `/setup/organizations` |
| UI Tenant | `/system/platform-pack` |

- Hợp đồng ghi **module_code**, không ghi tên tab UI  
- IAM (chi nhánh/user/role) đi kèm tenant — không bán như SKU riêng  
- Hạ `max_branches` không xóa chi nhánh hiện có; chặn tạo mới khi vượt quota  

---

## 10. Security / workflow / report / AI

| Area | As-built |
|------|----------|
| AuthN | JWT |
| AuthZ | RBAC + module gates |
| Audit | `audit_logs` + soft-delete |
| Workflow | Engine có; dùng cho PO approve, discount override, … |
| Report export | CSV / PDF (QuestPDF) nơi đã có |
| AI | KAP narrative / health hooks; không generate AI cho mọi CRUD |

---

## 11. Future-ready (có nền / chưa đủ)

| Flag | Status |
|------|--------|
| multi_language | ✅ i18n admin + locales |
| multi_currency | ⚠️ tenant default currency |
| multi_timezone | ⚠️ |
| offline_ready | ⚠️ Staff PWA partial |
| microservice_ready | 🔶 monolith boundaries only |
| event_bus_ready | ⚠️ outbox outbound only |
| HIS / HL7 / FHIR | ❌ project adapter — không có sẵn |
| HospitalOS / full HIS | ❌ non-goal của Clinic GĐ1 |

---

## 12. AI generation policy (bắt buộc)

Khi generate code trên KitPlatform:

1. Đọc blueprint **as-built này** + `*PackDefinition` liên quan  
2. Ưu tiên mở rộng pack/API/UI hiện có trước khi tạo module mới  
3. Schema chỉ qua `migrations/NNN_*.sql` additive; cập nhật `migration-files.prod.txt` + `run-migrations.ps1`  
4. Data access = Dapper; UI = React trong `client/*` phù hợp channel  
5. Gate module bằng `PlatformModuleCodes` / registry  
6. Không đổi .NET / ORM / UI framework  
7. Không invent HospitalOS/Blazor/EF “cho đủ blueprint cũ”  
8. Chạy `scripts/check-kit-bp-asbuilt.ps1` + `dotnet test` platform tests khi đổi pack/module  

---

## 13. Verification

| Check | Lệnh |
|-------|------|
| Doc hub + stack + pack strings | `pwsh scripts/check-kit-bp-asbuilt.ps1` |
| PackDefinition sync tests | `dotnet test tests/KitPlatform.Platform.Tests --filter "FullyQualifiedName~PackDefinition"` |
| Entitlement validator | `dotnet test tests/KitPlatform.Platform.Tests --filter "FullyQualifiedName~TenantPlatformSettingsValidator"` |

---

**Changelog**

- **2.1.1 (2026-07-14):** Đồng bộ PackDefinition defaults (Pharmacy e_rx*, Clinic sales/connect/crm_leads, Survey reports); document WorkspacePackProvisioner; module catalog; commercial UI tree note; wire DOC-010 / README; verify script + sync tests.  
- **2.1:** Viết lại từ blueprint vision (EF/Blazor/.NET 9/HospitalOS) → mirror KitPlatform (packs Pharmacy/Clinic/Connect/Survey, entitlement ceiling, React channels).
