# Novixa — DOC Master Index

**Phiên bản:** 1.0 · **Phạm vi:** V1 · 2026–2027 · Việt Nam  
**Mục tiêu:** Single entry point cho **hai lớp tài liệu** — Launch Pack (10 DOC) và Operations Pack (`docs/novixa/`).

> **Bắt đầu tại đây** nếu bạn không chắc nên đọc file nào.  
> Chi tiết ma trận NVX-*: [V1-DOCUMENTATION-PLAN.md](./V1-DOCUMENTATION-PLAN.md) · Hub vận hành: [README.md](./README.md)

---

## 1. Hai lớp tài liệu

| Lớp | Tên | Mã | Ai dùng | Mục đích |
|-----|-----|-----|---------|----------|
| **A** | **Launch Pack** | DOC-001 … DOC-010 | CEO, Marketing, Agency, Sales | Ra mắt, thương hiệu, deck, web, narrative |
| **B** | **Operations Pack** | NVX-CMP … NVX-INT | Product, Sales, CS, DevOps, Eng | Vận hành đúng sự thật product, triển khai, không over-promise |

**Quy tắc ghép:**

- **DOC = “face outward”** (brand, pitch, guideline) — có thể xuất PDF/Notion cho agency.
- **NVX = “source of truth”** (module catalog, deploy, GPP, pricing nội bộ) — canonical trong repo.
- Mọi DOC-004/005/007/008 **phải tham chiếu** [module-catalog-v1.md](./02-product/module-catalog-v1.md) và `client/admin/PHASE_SCOPE.md` trước khi publish.

```
                    ┌─────────────────────┐
                    │  DOC MASTER INDEX   │  ← bạn ở đây
                    └──────────┬──────────┘
              ┌────────────────┴────────────────┐
              ▼                                 ▼
    ┌──────────────────┐              ┌──────────────────┐
    │  Lớp A: 10 DOC   │              │ Lớp B: docs/novixa│
    │  Launch & Brand  │──map────────▶│  Operations      │
    └──────────────────┘              └──────────────────┘
              │                                 │
              └──────────────┬──────────────────┘
                             ▼
                    Repo · novixa.vn · Pilot
```

---

## 2. Bản đồ 10 DOC ↔ Operations Pack

**Legend trạng thái:** ✅ Có (Draft+) · 📝 Planned · 🔗 Repo live (không doc riêng) · ➕ Cần tạo mới (Launch)

| DOC | Tên Launch Pack | Mục tiêu | Ưu tiên founding | Map NVX / repo | Trạng thái |
|-----|-----------------|----------|------------------|----------------|------------|
| **DOC-001** | Brand Strategy | Định vị, vision, persona, USP | ⭐⭐⭐⭐ | [vision-and-strategy-v1.md](./01-company/vision-and-strategy-v1.md) (NVX-CMP-01) · bổ sung: NVX-CMP-04 OKRs | ✅ Draft — cần mở rộng persona/USP |
| **DOC-002** | Brand Guideline | Logo, màu, tone, template | ⭐⭐⭐⭐ | NVX-CMP-02 `brand-voice-and-messaging-v1.md` (Planned) · tone T0: `novixa-site/src/i18n/vi.json` | 📝 Planned |
| **DOC-003** | Visual Identity | Danh thiếp, booth, PPT, roll-up | ⭐⭐ | Không map NVX — **asset folder** `docs/novixa/assets/brand/` (Planned) | ➕ Q1/2027 |
| **DOC-004** | Website Guideline | Sitemap, landing, blog SEO | ⭐⭐⭐⭐ | [DOC-004-website-guideline-v1.md](./launch/DOC-004-website-guideline-v1.md) · `novixa-site/` | ✅ Draft |
| **DOC-005** | Product Guideline | Kiến trúc sản phẩm, naming | ⭐⭐⭐⭐⭐ | [product-overview-v1.md](./02-product/product-overview-v1.md) (NVX-PRD-01) · [module-catalog-v1.md](./02-product/module-catalog-v1.md) (NVX-PRD-02) · `PHASE_SCOPE.md` | ✅ Draft — **bắt buộc gắn nhãn Phase** |
| **DOC-006** | UI Design System | ERP / POS / App / Web | ⭐⭐⭐ | Ant Design (admin) + app tokens — NVX doc **delta-only** (Planned) · không viết lại Ant | 📝 Q4/2026 skeleton |
| **DOC-007** | Marketing Playbook | Content, social, lịch đăng | ⭐⭐⭐ | NVX-GTM-06 `marketing-plan-2026-v1.md` · NVX-GTM-07 `content-calendar-v1.md` | 📝 Founding cadence nhẹ (1 bài/tuần) |
| **DOC-008** | Sales Playbook | Lead → ký → handoff | ⭐⭐⭐⭐⭐ | [sales-playbook-v1.md](./04-gtm/sales-playbook-v1.md) · [sales-deck-v1.md](./launch/DOC-008/sales-deck-v1.md) · [founding-program-terms-v1.md](./04-gtm/founding-program-terms-v1.md) | ✅ Draft |
| **DOC-009** | Customer Success | Sau ký, onboarding | ⭐⭐⭐⭐⭐ | [onboarding-playbook-v1.md](./07-customer/onboarding-playbook-v1.md) · [go-live-checklist-customer-v1.md](./07-customer/go-live-checklist-customer-v1.md) · pilot-go-live | ✅ Draft |
| **DOC-010** | Technical Standard | Code, API, deploy, security | ⭐⭐⭐⭐ | **As-built SSOT:** [kitplatform-enterprise-blueprint-asbuilt-v2.1.md](./03-solution/kitplatform-enterprise-blueprint-asbuilt-v2.1.md) (KIT-BP-ASBUILT) · [kitplatform-blueprint-asbuilt-v2.1.yaml](./03-solution/kitplatform-blueprint-asbuilt-v2.1.yaml) · [platform-kernel-and-solution-packs-v1.md](./03-solution/platform-kernel-and-solution-packs-v1.md) (KIT-PLT-01) · [solution-architecture-v1.md](./03-solution/solution-architecture-v1.md) (NVX-SOL-01) · [enterprise-architecture-evolution-v1.md](./03-solution/enterprise-architecture-evolution-v1.md) (NVX-SOL-07) · [enterprise-architecture-gap-matrix-v1.md](./03-solution/enterprise-architecture-gap-matrix-v1.md) (NVX-SOL-06) · [standards/](./standards/) · [deployment-model-v1.md](./05-operations/deployment-model-v1.md) (NVX-OPS-01) · 🔗 [novixa-deploy.md](../novixa-deploy.md) · NVX-SOL-02/05 · NVX-OPS-02 · verify `scripts/check-kit-bp-asbuilt.ps1` | ✅ Draft ops — as-built Active; coding std Planned |

### 2.1 Product naming map (DOC-005 ↔ thực tế)

| Tên marketing (DOC-005) | Module / app thực tế | Phase V1 |
|-------------------------|----------------------|----------|
| KIT Platform (kernel) | KitPlatform API + multi-tenant + module registry | ✅ |
| Novixa Pharmacy Pack | ERP NT + Care trên KIT Platform | ✅ pilot |
| Novixa POS | Admin `/sales` + Staff app | ✅ |
| Novixa Inventory | `/inventory` + batches FEFO | ✅ |
| Novixa CRM | `/customer` + loyalty | ✅ |
| Novixa Care | Customer App (OTP, O2O, chat) | ✅ |
| Novixa AI | Rule-based copilot | 🧪 Roadmap |
| Novixa Analytics | Reports Wave 1 | ✅ (không phải BI riêng) |
| Novixa Admin | Admin Web | ✅ |
| Novixa API | `KitPlatform.Api` | ✅ |

### 2.2 Deliverables V1 ↔ DOC

| Deliverable (cuối 2027) | DOC chính | NVX / repo |
|---------------------------|-----------|------------|
| Website chính thức | DOC-004 | novixa.vn |
| Bộ nhận diện | DOC-002, DOC-003 | assets/brand |
| Company Profile / Brochure | DOC-001 + DOC-005 | export từ NVX-CMP-01, PRD-01 |
| Sales Deck | DOC-008 | NVX-GTM-03 |
| Demo System | DOC-008, DOC-010 | NVX-INT-03 · demo checklists |
| Case Study 3–5 | DOC-007, DOC-009 | CS onboarding § case study |
| Sales / CS Playbook | DOC-008, DOC-009 | NVX-GTM-02, NVX-CS-01 |
| Technical Standard | DOC-010 | NVX-SOL + NVX-OPS |

---

## 3. Timeline ưu tiên theo quý

### Q3/2026 — Founding ready (bán + triển khai được)

**Mục tiêu:** Ký founding đầu tiên, demo không lệch product.

| Ưu tiên | DOC | NVX / hành động | Tiêu chí xong |
|---------|-----|-----------------|---------------|
| P0 | DOC-008 | NVX-GTM-02 Approved · NVX-GTM-01 · sales deck v1 | Demo script + pricing founding thống nhất |
| P0 | DOC-009 | NVX-CS-01 · pilot-go-live · NVX-CS-02 draft | Go-live checklist ký nội bộ |
| P0 | DOC-005 | NVX-PRD-01/02 Approved · PHASE_SCOPE sync | Catalog ✅/🧪/📋 |
| P0 | DOC-010 | NVX-OPS-01 · novixa-deploy.md | Deploy 1 tenant trên Model B |
| P1 | DOC-001 | NVX-CMP-01 Review → Approved | Leadership sign-off positioning |
| P1 | DOC-004 | Founding sitemap (không full Pricing page) | Khớp `novixa-site` hiện tại |
| P1 | DOC-002 | Logo + màu + 1 slide template | Đủ cho deck & web |
| P2 | DOC-007 | Content 1 bài/tuần SEO | Không bắt 3+2/tuần |
| — | DOC-003, DOC-006 | Defer | Sau pilot đầu |

**Wave repo:** Wave A ✅ Draft → Wave B bắt đầu.

---

### Q4/2026 — Pilot thật + case study #1

**Mục tiêu:** 1–3 founding go-live, hypercare, material cho marketing.

| Ưu tiên | DOC | NVX / hành động | Tiêu chí xong |
|---------|-----|-----------------|---------------|
| P0 | DOC-009 | NVX-CS-07 migration · training outline | Onboard ≤ 4 tuần |
| P0 | DOC-008 | NVX-GTM-08 founding terms · NVX-GTM-05 objections | HĐ 6 tháng + FAQ sales |
| P1 | DOC-007 | 1 case study draft · NVX-GTM-06 | Social proof |
| P1 | DOC-004 | Blog guideline · internal link rules | 5 bài chuẩn SEO |
| P1 | DOC-005 | NVX-PRD-03 roadmap public-safe | Web không over-promise |
| P2 | DOC-003 | Company profile 8–12 tr | PDF sales |
| P2 | DOC-006 | UI delta doc (admin) | Không full design system |
| P0 | DOC-010 | NVX-OPS-02 production runbook | Update release không downtime |

**Wave repo:** Wave B hoàn thành · Wave C bắt đầu.

---

### Q1/2027 — Chuẩn hóa sau phản hồi (3 pilot)

**Mục tiêu:** 3 case study, playbook v2 từ thực tế.

| Ưu tiên | DOC | NVX / hành động | Tiêu chí xong |
|---------|-----|-----------------|---------------|
| P0 | DOC-009 | NVX-CS-04/05/06 user guides T1 | Khách tự tra 80% FAQ |
| P0 | DOC-008 | Sales playbook v2 + ROI calculator | Cycle demo→ký đo được |
| P1 | DOC-007 | 1 case study/tháng · video demo | 3 case published |
| P1 | DOC-002 | Brand guideline Approved | Agency dùng 1 file |
| P1 | DOC-006 | App + web component notes | Consistency 4 surface |
| P2 | DOC-003 | Roll-up, booth template | Sự kiện / hội thảo |
| P1 | — | NVX-CPL-02 privacy · NVX-OPS-05 SLA draft | Trust pack |

**Wave repo:** Wave C.

---

### Q2–Q4/2027 — Scale VN

**Mục tiêu:** Catalog giá công khai, multi-tenant scale, brand full kit.

| Ưu tiên | DOC | NVX / hành động | Tiêu chí xong |
|---------|-----|-----------------|---------------|
| P0 | DOC-004 | Sitemap Scale: Pricing, Resources EN stub | Post-founding GTM |
| P0 | DOC-008 | NVX-GTM-03 deck v3 · partner pack | Scale sales |
| P1 | DOC-003 | Full VI kit | Deliverables 2027 ✅ |
| P1 | DOC-007 | Content machine (scale cadence) | 2–3 bài/tuần nếu có headcount |
| P1 | DOC-010 | NVX-SOL-02/04/05 · NVX-OPS-03 backup drill | ≥3 tenant Model B |
| P1 | DOC-001 | Roadmap 2028 draft | NVX-CMP-04 OKRs |

**Wave repo:** Wave D.

---

## 4. Ma trận nhanh: Ai → DOC → File

| Vai trò | DOC ưu tiên | Đọc ngay |
|---------|-------------|----------|
| **CEO / Leadership** | DOC-001 | [vision-and-strategy-v1.md](./01-company/vision-and-strategy-v1.md) |
| **Marketing** | DOC-002, 004, 007 | [DOC-004 §](#) → `novixa-site/` · NVX-GTM-06 (Planned) |
| **Designer / Agency** | DOC-002, 003, 006 | DOC-002 (Planned) · DOC-006 delta |
| **Sales** | DOC-008, 005 | [sales-playbook-v1.md](./04-gtm/sales-playbook-v1.md) · [module-catalog-v1.md](./02-product/module-catalog-v1.md) |
| **Customer Success** | DOC-009 | [onboarding-playbook-v1.md](./07-customer/onboarding-playbook-v1.md) · [pilot-go-live-checklist.md](../../client/admin/pilot-go-live-checklist.md) |
| **Product** | DOC-005 | [product-overview-v1.md](./02-product/product-overview-v1.md) · `PHASE_SCOPE.md` |
| **Engineering / DevOps** | DOC-010 | [solution-architecture-v1.md](./03-solution/solution-architecture-v1.md) · [enterprise-architecture-gap-matrix-v1.md](./03-solution/enterprise-architecture-gap-matrix-v1.md) · [novixa-deploy.md](../novixa-deploy.md) |
| **Compliance / Dược sĩ** | DOC-009, 005 | [gpp-operational-context-v1.md](./06-compliance/gpp-operational-context-v1.md) |
| **Ops / Doc owner** | All | [document-governance-v1.md](./08-internal/document-governance-v1.md) |

---

## 5. Thư mục đề xuất (Launch assets)

```
docs/novixa/
├── DOC-MASTER-INDEX.md          ← entry point (file này)
├── launch/                      ← Launch Pack: DOC-004, DOC-008 (+ Planned)
│   ├── README.md
│   ├── DOC-004-website-guideline-v1.md
│   └── DOC-008/sales-deck-v1.md
├── assets/brand/                ← Planned: logo, PPT, VI kit (DOC-003)
└── [01-company … 08-internal]   ← Operations Pack (hiện có)
```

Repo giữ **markdown canonical**; PDF/FIGMA export vào `launch/` hoặc Drive — không thay NVX trong `01–08`.

---

## 6. Governance

| Hạng mục | Quy tắc |
|----------|---------|
| **Owner DOC** | Marketing (002–004, 007) · Sales (008) · CS (009) · Eng (010) · Leadership (001) |
| **Owner NVX** | Theo [document-governance-v1.md](./08-internal/document-governance-v1.md) |
| **Review DOC vs product** | Mỗi release minor → NVX-PRD-02 trước, DOC-005/008 sau |
| **Publish T0** | DOC-002/004 subset + novixa.vn — không paste T3 pricing |
| **Cập nhật index** | Mỗi quý hoặc khi thêm DOC/NVX mới |

---

## 7. Liên kết nhanh

| Tài liệu | Link |
|----------|------|
| Operations Hub | [README.md](./README.md) |
| Ma trận đầy đủ NVX-* | [V1-DOCUMENTATION-PLAN.md](./V1-DOCUMENTATION-PLAN.md) |
| Template doc mới | [templates/doc-template-v1.md](./templates/doc-template-v1.md) |
| Website handoff | `.cursor/handoff/novixa-website.md` |
| Phase truth | `client/admin/PHASE_SCOPE.md` |

---

*Owner: Product / GTM · Version 1.0 · 2026-07-04 · Review: đầu mỗi quý*
