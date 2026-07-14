# KIT Platform — Platform Kernel & Solution Packs

**Mã:** KIT-PLT-01 · **Tier:** T2/T3 · **Trạng thái:** Active · **Version:** 1.0  
**Repo:** KitPlatform (modular monolith) · **Pilot:** Novixa Pharmacy Pack — 3 nhà thuốc

> **Định vị:** **KIT Platform** = nền tảng SaaS dùng chung. **Novixa** = **Solution Pack đầu tiên** (nhà thuốc / health retail), **không** phải toàn bộ platform.  
> **Nguyên tắc:** Evolve additive — **không đập đi làm lại** Novixa đang pilot.

**Liên quan:** [kitplatform-enterprise-blueprint-asbuilt-v2.1.md](./kitplatform-enterprise-blueprint-asbuilt-v2.1.md) (KIT-BP-ASBUILT — **SSOT kỹ thuật as-built**) · [enterprise-architecture-evolution-v1.md](./enterprise-architecture-evolution-v1.md) (NVX-SOL-07) · [domain-map-v1.md](../domains/domain-map-v1.md) · migration `051_platform_multi_branch_i18n.sql`

---

## 1. Tóm tắt điều hành

| Khái niệm | Ý nghĩa |
|-----------|---------|
| **KIT Platform** | Kernel + capabilities dùng chung cho mọi giải pháp (tenant, identity, catalog, sales, kho, event, module registry…) |
| **Solution Pack** | Gói nghiệp vụ theo lĩnh vực — bật module, NSF pack, bảng/API mở rộng additive |
| **Novixa Pharmacy Pack** | Pack 1 đang pilot: ERP NT + Care channel + medication/health wallet |
| **KitPlatform** | Tên repo / deployable API — host **một** monolith chứa Platform + các Pack |
| **Kênh (channel)** | Admin Web, Customer App, Staff App — **adapter UI**, không phải domain |

```
┌─────────────────────────────────────────────────────────────┐
│  KIT Platform — Layer A (Kernel) + Layer B (Capabilities)   │
├─────────────────────────────────────────────────────────────┤
│  Layer C — Solution Packs                                    │
│    • Novixa Pharmacy (pilot)  • FamilyOS  • ClinicOS  …     │
└─────────────────────────────────────────────────────────────┘
         ▲                              ▲
    dùng chung                    additive only
    tenant · customer · sales     pack tables · pack NSF
```

---

## 2. Ba lớp kiến trúc

### Layer A — Platform Kernel (đổi rất ít)

Dữ liệu và dịch vụ **mọi pack phải trỏ về** — không gắn logic “thuốc” hoặc “family”.

| Thành phần | Hiện có (repo) | Ghi chú |
|------------|-----------------|---------|
| Tenant / Org | `tenants`, `organizations` | `business_vertical`, `settings.platform` |
| Branch / Location | `branches`, `warehouses` | Mở rộng `Location` sau (UDM) |
| Identity | `users`, `employees`, RBAC | Actor spine — roadmap |
| Audit | `audit_logs`, `IAuditEngine` | Write-path G2b |
| Module registry | `platform_module_registry`, `enabled_modules` | Pack Manager |
| Locale / i18n | `platform_locales`, `entity_translations` | migration 051 |
| Integration | `integration_outbox` | Mầm Event Bus |
| Settings | `tenants.settings` JSONB | Feature flags theo pack |

**North star (chưa làm — additive sau pilot):** Universal Domain Model (`Actor`), Universal Event Bus (envelope nội bộ), Routine Engine.

### Layer B — Platform Capabilities (dùng chung, pack bật/tắt)

| Capability | Domain EA | Code / module | Pack nào thường bật |
|------------|-----------|---------------|---------------------|
| Catalog | Master Data | `Catalog/*`, `products.product_kind` | Mọi pack retail |
| Sales / POS | Sales | `Sales/*` | Novixa, RetailOS |
| Inventory | Inventory | `Inventory/*`, `IInventoryEngine` | Novixa (FEFO), Retail |
| Procurement | Procurement | `Procurement/*` | Novixa, Retail |
| Pricing | Pricing | `IPricingEngine` | Mọi pack bán hàng |
| Finance (AR/AP) | Finance | Payments, receivables | Mọi pack |
| Customer / CRM | Customer | `Customers/*`, loyalty | Mọi pack |
| Notification | Notification | Push, OTP, in-app | Mọi pack |
| Reporting | Reporting | `Reports/*`, Dashboard | Mọi pack |
| AI Orchestrator | AI | `IAiOrchestrator` | Pack định nghĩa context |

**Quy tắc:** Logic trong **Core Engines** phải **generic** — rule theo vertical inject qua policy / pack, không hard-code thuốc trong engine.

### Layer C — Solution Packs (additive)

| Pack | `business_vertical` | Module đặc thù | Trạng thái |
|------|---------------------|----------------|------------|
| **Novixa Pharmacy** | `pharmacy`, `pharmacy_chain` | medication, health_wallet, national_drug, reservations | ✅ Pilot |
| Supplement / TBYT | `supplement_store`, `medical_equipment_store` | Tắt medication; catalog `product_kind` | Roadmap |
| FamilyOS | (TBD) | Routine, rewards, family graph | Roadmap |
| ClinicOS | `clinic`, `hybrid` | appointments, EMR-lite | Registry only (`051`) |

Pack **không** copy `customers` / `sales_orders` — **kế thừa** kernel IDs.

---

## 3. Novixa = Pack 1 — map thực tế

### 3.1 Platform (dùng lại, không rename)

| Dữ liệu / API | Bảng / path | Ghi chú pack |
|--------------|-------------|--------------|
| Khách hàng | `customers`, `customer_accounts` | Platform party — ClinicOS sau này cùng ID |
| Đơn bán | `sales_orders` | Platform transaction |
| Sản phẩm | `products` + `product_kind` | `pharmacy_drug` = default pilot |
| Kho / lô | batches, `stock_movements` | Capability; TBYT có thể tắt `batch_tracking` |
| Tổ chức | `tenants`, `branches` | Mọi pack |

### 3.2 Novixa Pharmacy Pack (scope pack)

| Capability | Bảng / service | Không đưa vào Platform Kernel |
|------------|----------------|-------------------------------|
| Nhắc uống thuốc | `medication_reminders`, adherence | ✅ Pack / Care |
| Ví sức khỏe | `health_records`, `care_reminders` | ✅ Pack |
| Tái mua / repurchase | `repurchase_suggestions` | ✅ Pack |
| Drug knowledge tạm | `DrugKnowledgeRules`, `IDrugKnowledgeQuery` | ✅ Pack → G4 KB |
| National drug link | `national_drug` migrations | ✅ Pack |
| AI health copilot | `CustomerAiHealthService` | ✅ Pack context qua `IAiOrchestrator` |

### 3.3 Kênh vs Pack

`CustomerApp/*` = **channel adapter** — gọi contract Platform + Novixa Pack. Refactor dần (G3), không coi folder này là “domain Novixa duy nhất”.

---

## 4. Module registry & Pack Manager

Nguồn: migration `051` — `platform_module_registry` + `tenants.settings.platform.enabled_modules`.

**Pack mặc định pilot (Novixa Pharmacy):**

`inventory`, `procurement`, `sales`, `loyalty`, `customer_app`, `medication`, `health_wallet`, `reservations`, `reports`

**API / UI:** Feature thuộc pack phải check `ITenantPlatformSettings` trước khi expose.

| Layer | Implementation |
|-------|------------------|
| API | `[RequirePlatformModule]` / `[RequirePlatformFeature]` action filters — `PlatformModuleAuthorization.cs` |
| Constants | `PlatformModuleCodes`, `PlatformFeatureCodes` — `Application/Core/` |
| Admin | `TenantPlatformHydrator` + `useTenantPlatformStore` + `product-phases` gate |
| Tests | `tests/KitPlatform.Platform.Tests/` |

Pilot default (`settings.platform` backfill 051) bật đủ module Novixa Pharmacy — **403 chỉ khi tenant tắt module**.

**Admin UI:** Hệ thống → **KIT Platform / Pack** (`/system/platform-pack`) — ADMIN chỉnh vertical, module, feature flags (PUT `api/system/tenant-platform`).

**Thêm pack mới:**

1. INSERT `platform_module_registry` (metadata + `verticals[]`)
2. Cấu hình `enabled_modules` + `features` trên tenant
3. Migration **chỉ additive** cho bảng pack
4. Không sửa Sales/Inventory core — subscribe event / gọi contract

---

## 5. Governance — mọi PR ghi rõ scope

### 5.1 Nhãn bắt buộc (xem PR template)

| Trường | Giá trị ví dụ |
|--------|----------------|
| **Layer** | `Platform` · `Pack:Pharmacy` · `Pack:FamilyOS` |
| **Domain** | `inventory`, `care`, … — [domain-map-v1.md](../domains/domain-map-v1.md) |
| **Capability** | POS checkout, module gate, … |
| **BR-ID** | `BR-INV-001` hoặc "new BR — mô tả" |

### 5.2 Quy tắc

1. **Platform PR** — không import logic thuốc/family vào `Core/Engines`
2. **Pack PR** — có thể phụ thuộc Platform; không ngược lại (Platform không reference Pack)
3. **Schema** — additive only; kernel tables không rename/xóa cột
4. **API** — không breaking route pilot; pack mới = route mới hoặc flag
5. **Feature mới** — nếu chỉ Novixa cần → `Layer: Pack:Pharmacy`, không gắn nhãn Platform

### 5.3 Anti-pattern

| ❌ Tránh | ✅ Thay bằng |
|---------|-------------|
| Logic clinic trong `SalesRepository` | Pack ClinicOS + event/contract |
| Fork repo "Novixa v2" | Cùng monolith, pack additive |
| Big-bang `actors` thay `customers` | Sync strangler, API cũ giữ |
| Mọi feature marketing gọi "Novixa" | Platform capability vs Novixa pack |

---

## 6. Lộ trình Platform (không phá pilot)

| Phase | Việc | Layer | Pilot-safe |
|-------|------|-------|------------|
| **P0** (now) | Doc + PR governance + module gate | Platform | ✅ |
| **P0** | Smoke pilot Novixa Pack | Pack:Pharmacy | ✅ |
| **P1** | Event envelope v1 + `platform_events` | Platform | ✅ Implemented — see §10 |
| **P1** | Namespace `Packs/Pharmacy/` (move dần) | Pack | ✅ Care/Knowledge infra moved |
| **P2** | Actor spine additive + sync | Platform | Additive |
| **P2** | Routine Engine v0 | Platform | Additive |
| **P3** | Pack 2 (Supplement hoặc FamilyOS) | Pack | Chứng minh kế thừa |

Chi tiết EA: [enterprise-architecture-evolution-v1.md](./enterprise-architecture-evolution-v1.md) (G3–G5).

---

## 7. Checklist — Solution Pack mới

- [ ] `module_code` trong `platform_module_registry` + `verticals`
- [ ] NSF pack (hoặc mở rộng NSF platform) — không sửa NSF kernel trái pack
- [ ] `enabled_modules` / `features` mẫu cho tenant demo
- [ ] Bảng mới có `tenant_id` (`BR-ID-001`)
- [ ] FK tới `customers` / `products` / `branches` — không duplicate party
- [ ] Event type mới (`{pack}.{entity}.{action}.v1`) — consumer riêng
- [ ] PR template: `Layer: Pack:…`
- [ ] Marketing: landing pack riêng; không over-promise Platform

---

## 8. Câu hỏi thường gặp

**Có phải đổi tên repo KitPlatform?**  
Không bắt buộc. Marketing: "Novixa on KIT Platform" hoặc "KIT Platform — Novixa Pharmacy Pack".

**Novixa có phải là toàn bộ KIT?**  
Không. Novixa là **một giải pháp** (pack) trên KIT Platform từ giai đoạn này (định vị); code refactor vào pack **dần** sau pilot.

**KIT Platform đã xong bao nhiêu %?**  
~55–65% nếu đo ERP multi-tenant + capabilities. ~25–35% nếu đo north star (UDM + Event Bus + Routine). Xem [enterprise-architecture-gap-matrix-v1.md](./enterprise-architecture-gap-matrix-v1.md).

**Microservices khi nào?**  
Khi **một pack** cần scale/deploy tách — sau boundary pack + event contract ổn định.

---

## 9. Tham chiếu code

| Chủ đề | Path |
|--------|------|
| Platform settings | `ITenantPlatformSettings`, `TenantPlatformSettingsService` |
| Module registry DB | `migrations/051_platform_multi_branch_i18n.sql` |
| Domain IDs | `src/KitPlatform.Application/Core/DomainIds.cs` |
| BR catalog | `src/KitPlatform.Application/Core/BusinessRuleIds.cs` |
| Core Engines | `src/KitPlatform.Application/Core/Engines/*` |
| Provisioning tenant | `PlatformTenantService`, `POST /api/platform/tenants` |
| Platform events v1 | `platform_events`, `IPlatformEventWriter`, `PlatformEventWorker` |

---

## 10. Platform events v1 (implemented)

| Item | Path / detail |
|------|----------------|
| Migration | `migrations/067_platform_events.sql` |
| Event types | `PlatformEventTypes` — e.g. `sales.order.completed.v1` |
| Writer | `IPlatformEventWriter` — transactional insert cùng nghiệp vụ |
| Dispatcher | `PlatformEventWorker` + `IPlatformEventHandler` (in-process, no Kafka) |
| Pilot publish | `SalesRepository` — order completed, return completed; `CustomerConsentRepository` + customer-app consent — consent updated |
| Pack consumers | `PharmacySalesOrderCompletedHandler`, `PharmacySalesReturnCompletedHandler` |
| CDP outbox | `integration_outbox` **giữ nguyên** — mục đích khác |

Envelope JSON: `eventId`, `eventType`, `tenantId`, `occurredAt`, `source`, `aggregateType`, `aggregateId`, `actorUserId`, `data`.

---

## 11. Packs/Pharmacy (strangler — started)

| Item | Path |
|------|------|
| Pack README | `src/Packs/Pharmacy/README.md` |
| Application project | `KitPlatform.Packs.Pharmacy.Application` |
| Infrastructure project | `KitPlatform.Packs.Pharmacy.Infrastructure` |
| Pack DI | `AddPharmacyPack()` in `Program.cs` |
| Pack metadata | `PharmacyPackDefinition` |
| Moved contracts | `Care/*`, `Knowledge/*`, `CustomerApp/CustomerActiveMedication*` → pack Application |
| Moved implementations | Care, Knowledge, active medications, pack event handlers |
| Shared kernel helper | `ReminderScheduleHelper` in `KitPlatform.Application` (reminder/push/adherence) |
| Event handlers | `sales.order.completed.v1`, `sales.return.completed.v1` |

Platform kernel **không** reference pack project. API wires `AddInfrastructure()` + `AddPharmacyPack()`. Pack infra uses `InternalsVisibleTo` for kernel repositories (strangler).

---

*Owner: Product / Architecture · Review: sau pilot smoke NVX-CS-08*
