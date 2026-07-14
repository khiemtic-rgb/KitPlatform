# Novixa — Kiến trúc giải pháp V1

**Mã:** NVX-SOL-01 · **Tier:** T2/T3 · **Trạng thái:** Draft · **Version:** 1.1

> **EA evolution (pilot-safe):** [enterprise-architecture-evolution-v1.md](./enterprise-architecture-evolution-v1.md) (NVX-SOL-07) · Gap matrix [enterprise-architecture-gap-matrix-v1.md](./enterprise-architecture-gap-matrix-v1.md) (NVX-SOL-06) · **As-built SSOT:** [kitplatform-enterprise-blueprint-asbuilt-v2.1.md](./kitplatform-enterprise-blueprint-asbuilt-v2.1.md) (KIT-BP-ASBUILT) · **KIT Platform / Novixa Pack:** [platform-kernel-and-solution-packs-v1.md](./platform-kernel-and-solution-packs-v1.md) (KIT-PLT-01) · NSF `docs/novixa/standards/` · Core Engines trong `KitPlatform.Application/Core/Engines`.

---

## 1. Tổng quan

**Novixa** là **Solution Pack đầu tiên** (nhà thuốc / health retail) trên **KIT Platform** — host kỹ thuật **KitPlatform** (SaaS multi-tenant). Triển khai V1 gồm:

- **KitPlatform API** (.NET 10, PostgreSQL)
- **Admin Web** (React/Vite) — ERP + POS desktop
- **Staff POS Mobile** (React/PWA)
- **Customer App** (React/PWA)
- **Marketing site** (Astro) — **tách biệt**, không truy cập DB ERP

---

## 2. Sơ đồ triển khai (Production)

```
                    ┌─────────────────────┐
                    │  Cloudflare Pages   │
                    │  novixa.vn (T0)     │
                    └─────────────────────┘

┌─────────────── VPS (Ubuntu 24.04) ───────────────────────────────┐
│  Nginx (TLS)                                                      │
│    api.novixa.vn  → Kestrel :5000 (KitPlatform.Api)               │
│    admin.novixa.vn → /var/www/kit-platform/admin/ (static)         │
│    app.novixa.vn   → /var/www/kit-platform/customer-app/ (static)  │
│    pos.novixa.vn   → /var/www/kit-platform/admin/ (POS route)      │
│                                                                   │
│  PostgreSQL: novixa_prod (multi-tenant)                          │
│  SMS stub (pilot) → CustomerAppSms                               │
└───────────────────────────────────────────────────────────────────┘
```

**Nguyên tắc:** Marketing site không share secret/DB với ERP.

---

## 3. Multi-tenant (Model B — khuyến nghị)

| Khái niệm | Mô tả |
|-----------|--------|
| **Tenant** | Một nhà thuốc / chuỗi (tenant_code) |
| **Branch** | Chi nhánh / quầy |
| **Isolation** | `tenant_id` trên mọi bảng nghiệp vụ |
| **Auth** | JWT chứa `tenant_id`; login = tenantCode + user + pass |
| **Provisioning** | `/setup` + `Platform__ProvisioningKey` |

**Mô hình A (2 DB tách):** chỉ pilot đặc biệt — xem `client/admin/pilot-go-live-checklist.md`.

---

## 4. Luồng dữ liệu nghiệp vụ

```
Catalog (SP master)
    ↓
Procurement: PO → GRN (+ VAT cơ bản Phase 1)
    ↓
Inventory: batches, stock_movements (sổ cái), quantity_available (cache)
    ↓
Sales: POS checkout (FEFO), shifts, returns
    ↓
Receivables / Loyalty / Customer App (O2O, chat, reminders)
    ↓
Reports Wave 1 (read models / SQL views)
```

**Quy tắc vàng:** `stock_movements` là nguồn truth; không sửa tồn trực tiếp ngoài workflow chuẩn.

---

## 5. Thành phần kỹ thuật

| Layer | Công nghệ | Repo path |
|-------|-----------|-----------|
| API | ASP.NET Core 10 | `src/KitPlatform.*` |
| Admin | React 18, Ant Design, Vite | `client/admin/` |
| Customer App | React, PWA | `client/customer-app/` |
| Staff App | React, mobile-first | `client/staff-app/` |
| DB | PostgreSQL 16+ | `database/migrations/` |
| Marketing | Astro | `novixa-site/` |

---

## 6. Bảo mật (tóm tắt V1)

| Hạng mục | V1 |
|----------|-----|
| Transport | HTTPS (Certbot/nginx) |
| Auth | JWT access + refresh, RBAC permissions |
| Tenant | Middleware validate tenant context |
| Secrets | `/etc/kit-platform/api.env`, chmod 600 |
| Audit | `audit_logs` (System module) |
| CORS | Whitelist admin/app/pos origins |
| Pilot OTP | SMS stub / Log provider — **không production SMS mặc định** |

Chi tiết mở rộng: NVX-SOL-05 (Planned).

---

## 7. Tích hợp & phụ thuộc ngoài

| Tích hợp | V1 | Ghi chú |
|----------|-----|---------|
| SMS OTP | Pilot stub | Cần gateway thật trước go-live khách |
| Web Push | Tắt mặc định | `CustomerAppPush__Enabled=false` |
| Danh mục thuốc QG | Mock | `NationalDrugCatalog__Mode=mock` |
| HĐĐT | — | Phase 2 |
| Cloudflare Analytics | Marketing only | GHA stats snapshot |

---

## 8. Build & release

```powershell
# Build artifact production (mỗi URL API)
.\scripts\deploy-production.ps1 -ApiBaseUrl "https://api.novixa.vn"

# Upload VPS
.\scripts\upload-to-vps.ps1 -SshTarget user@host

# Bootstrap lần đầu (trên VPS)
sudo bash /tmp/kit-platform-upload/deploy/ubuntu/bootstrap-vps.sh
```

Chi tiết: [deployment-model-v1.md](../05-operations/deployment-model-v1.md), [docs/novixa-deploy.md](../../novixa-deploy.md)

---

## 9. Môi trường

| Môi trường | Mục đích |
|------------|----------|
| **Development** | Local API + admin dev server |
| **Demo / UAT** | Checklists trong `client/admin/demo-*` |
| **Production** | VPS Novixa, 1 DB `novixa_prod` |

---

## 10. Tham chiếu

- [Module catalog](../02-product/module-catalog-v1.md)
- [PHASE_SCOPE.md](../../../client/admin/PHASE_SCOPE.md)
- [Pilot go-live checklist](../../../client/admin/pilot-go-live-checklist.md)

---

*Owner: Engineering · Review: mỗi thay đổi kiến trúc lớn*
