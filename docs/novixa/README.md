# Novixa — Bộ tài liệu doanh nghiệp (Documentation Hub)

> **Giai đoạn:** V1 · 2026–2027 · Thị trường Việt Nam  
> **Mục đích:** Vận hành ra mắt, triển khai, bán hàng và mở rộng — không chỉ trình bày.  
> **Sản phẩm kỹ thuật:** KitPlatform (ERP lõi) · **Thương hiệu:** Novixa · **Website:** [novixa.vn](https://novixa.vn)

**Entry point thống nhất (Launch 10 DOC + Operations):** [**DOC-MASTER-INDEX.md**](./DOC-MASTER-INDEX.md)

---

## Cách dùng bộ tài liệu

| Ai | Bắt đầu từ |
|----|------------|
| Lãnh đạo / GTM | [01-company/vision-and-strategy-v1.md](./01-company/vision-and-strategy-v1.md) → [04-gtm/icp-positioning-pricing-v1.md](./04-gtm/icp-positioning-pricing-v1.md) |
| Sales / CS | [launch/DOC-008/sales-deck-v1.md](./launch/DOC-008/sales-deck-v1.md) · [sales-playbook-v1.md](./04-gtm/sales-playbook-v1.md) → [go-live-checklist-customer-v1.md](./07-customer/go-live-checklist-customer-v1.md) |
| Triển khai / Kỹ thuật | [03-solution/kitplatform-enterprise-blueprint-asbuilt-v2.1.md](./03-solution/kitplatform-enterprise-blueprint-asbuilt-v2.1.md) (KIT-BP-ASBUILT) → [03-solution/platform-kernel-and-solution-packs-v1.md](./03-solution/platform-kernel-and-solution-packs-v1.md) (KIT-PLT-01) → [03-solution/solution-architecture-v1.md](./03-solution/solution-architecture-v1.md) → [05-operations/deployment-model-v1.md](./05-operations/deployment-model-v1.md) |
| Sản phẩm | [02-product/product-overview-v1.md](./02-product/product-overview-v1.md) → [02-product/module-catalog-v1.md](./02-product/module-catalog-v1.md) |
| Tuân thủ / QA | [06-compliance/gpp-operational-context-v1.md](./06-compliance/gpp-operational-context-v1.md) |

**Kế hoạch đầy đủ & lộ trình hoàn thiện:** [V1-DOCUMENTATION-PLAN.md](./V1-DOCUMENTATION-PLAN.md)

---

## Cấu trúc thư mục

```
docs/novixa/
├── DOC-MASTER-INDEX.md                ← Entry point: 10 DOC ↔ NVX + timeline quý
├── README.md                          ← Hub Operations Pack (bạn đang ở đây)
├── V1-DOCUMENTATION-PLAN.md           ← Ma trận NVX-* đầy đủ
├── launch/                            ← Launch Pack (deck, web guideline)
├── 01-company/                        ← Chiến lược, tổ chức, thương hiệu
├── 02-product/                        ← Sản phẩm, module, roadmap
├── 03-solution/                       ← Kiến trúc giải pháp, tích hợp
├── 04-gtm/                            ← ICP, pricing, sales, marketing
├── 05-operations/                     ← Deploy, vận hành, SLA, support
├── 06-compliance/                     ← GPP, dữ liệu, bảo mật
├── 07-customer/                       ← Onboarding, đào tạo, user guide
├── 08-internal/                       ← Quy trình nội bộ, governance
└── templates/                         ← Mẫu tài liệu chuẩn
```

---

## Phân loại (Tier)

| Tier | Đối tượng | Ví dụ |
|------|-----------|-------|
| **T0 — Public** | Khách hàng tiềm năng, web | novixa.vn, one-pager |
| **T1 — Customer** | Khách hàng đang dùng | User guide, onboarding |
| **T2 — Partner** | Đối tác triển khai | Runbook, checklist go-live |
| **T3 — Internal** | Team Novixa | Chiến lược, pricing nội bộ, kiến trúc chi tiết |

Mỗi file ghi **Tier** và **Trạng thái:** `Draft` · `Review` · `Approved` · `Published`

---

## Quy ước đặt tên

`NVX-{NHÓM}-{SỐ}-{slug}-v1.md`

Ví dụ: `NVX-PRD-01-product-overview-v1.md` (bản rút gọn dùng tên thư mục như hiện tại).

---

## Liên kết repo

| Thành phần | Path |
|------------|------|
| ERP Admin | `client/admin/` |
| Customer App | `client/customer-app/` |
| Staff POS mobile | `client/staff-app/` |
| API | `src/KitPlatform.*` |
| Marketing site | `novixa-site/` |
| Pilot checklist | `client/admin/pilot-go-live-checklist.md` |
| Deploy ERP | `docs/novixa-deploy.md` |
| Production env | `docs/novixa-production.env.example` |
| Launch Pack | [launch/README.md](./launch/README.md) |
| Founding terms | [04-gtm/founding-program-terms-v1.md](./04-gtm/founding-program-terms-v1.md) |

---

*Cập nhật lần cuối: 2026-07-04 · Owner: Product / GTM*
