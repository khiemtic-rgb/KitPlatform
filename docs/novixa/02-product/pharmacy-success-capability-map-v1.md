# Novixa — Pharmacy Success Capability Map

**Mã:** NVX-PRD-03 · **Tier:** T2/T3 · **Version:** 1.0 · **Trạng thái:** Active  
**Ngày:** 2026-07-14  
**Bám:** [KIT-BP-ASBUILT](../03-solution/kitplatform-enterprise-blueprint-asbuilt-v2.1.md) · [module-catalog-v1.md](./module-catalog-v1.md) (NVX-PRD-02) · [product-overview-v1.md](./product-overview-v1.md)

> **Một trang** — north star *Pharmacy Success Platform* map xuống module as-built.  
> **GTM / UX** nói theo **Business Capability**. **Code / bán** vẫn Pack + `module_code` (không fork 5 app).  
> **SSOT kỹ thuật:** PackDefinition + `PlatformModuleCodes` (xem blueprint).

---

## Vòng cải tiến (trục chính)

```
Assessment (KAP) → Xác định vấn đề → Act (People|Process|Customer|Business)
        → AI / Roadmap gợi ý → Đánh giá lại ~90 ngày
```

| Khái niệm | As-built |
|-----------|----------|
| Assessment | Pack `pharmacy_survey` · modules `assessment`, `pharmacy_survey` · channels assessment-web, KAP admin, partner-portal |
| Pharmacy ops | Pack `novixa_pharmacy` · sales, inventory, procurement, loyalty, customer_app, medication, … |
| Entitlement | `allowed_modules` / `enabled_modules` / `max_branches` trong `settings.platform` |

---

## Capability → Module → Gap → Phase

| # | Capability | Module / surface hiện có | Gap (chưa đủ Success) | Phase |
|---|------------|--------------------------|------------------------|-------|
| **0** | **Assessment & Diagnose** | `assessment`, `pharmacy_survey`; KAP leads/PDF; partner portal | Business Score quý; SWOT/roadmap theo dõi; ngành benchmark; re-measure 90d đóng vòng trong product | **P1** (đang) → Scorecard **P2** |
| **1** | **People Success** | `users`/`employees`+RBAC (kernel); doanh số từ `sales`; ca POS (báo cáo ca) | Hồ sơ chứng chỉ; đổi ca/chấm công; đào tạo/quiz; hoa hồng/xhạng; nghỉ việc; eNPS | Hiệu suất từ sales **P2**; HRIS/học **P3–P4** |
| **2** | **Process Excellence** | Workflow PO approve / discount override; inventory count; audit_logs | SOP thư viện; checklist mở/đóng ca–tuần; audit tuân thủ; incident (sai sót/khiếu nại/mất hàng) | Checklist ca + SOP mỏng **P2**; QMS/incident **P3** |
| **3** | **Customer Success** | `loyalty`, `customer_app`, CRM admin; medication reminder; O2O; survey lead | CS sau bán đóng vòng; referral có chương trình; churn score; CSAT định kỳ ngoài KAP NT | Giữ & siết dùng **P1**; referral/CSAT/churn **P2–P3** |
| **4** | **Business Performance** | Dashboard; Reports Wave 1; FEFO/tồn/cận date; receivables/AP cơ bản | Owner cockpit 1 màn; lợi nhuận/P&L; forecast; phân tích rủi ro; DO theo NV/nhóm thuốc sâu | Cockpit KPI **P1–P2**; tài chính/forecast **P3** (sau Clinic go-live) |
| **5** | **Continuous Improvement** | KAP narrative/AI hooks; assessment loop | Score Q1→Q4; roadmap progress; coaching; Learning path; đối chiếu peer | Score+roadmap **P2–P3**; Coaching/Learning **P4** |
| **×** | **AI (xuyên suốt)** | KAP AI narrative; health/copilot hooks (blueprint: opportunistic) | Reminder checklist; gợi ý nhập; churn teaser; PDF SWOT — **rule-first**, không module AI SKU riêng | Copilot mỏng **P3**; ML nặng sau data sạch |

**Ngoài phạm vi Success NT (giữ tách):** Clinic (`clinic_*`), Connect (`novixa_connect`) — healthcare network; không gộp vào 5 trụ NT trên cùng backlog Success.

---

## Phase (để không quá tải)

| Phase | Trọng tâm Success | Ship / không ship |
|-------|-------------------|-------------------|
| **P1** | POS + kho + CRM + Dashboard + Assessment | Đúng hướng hiện tại; siết adoption + KAP loop |
| **P2** | People *performance* + SOP/Checklist mỏng + KPI owner + Scorecard v0 | **Epic đang mở:** [Owner Cockpit](./success-p2-owner-cockpit-epic-v1.md) (NVX-PRD-03-EP01). **Không** HRIS full / QMS full |
| **P3** | AI Copilot rule-based + Business Insight + Benchmark nhẹ | **Không** P&L/coaching nặng trước data ổn |
| **P4** | Coaching + Learning + Continuous Improvement sâu + Partner mở rộng | Sau P2–P3 đã đo được tiến bộ quý |

**Ưu tiên song song nền tảng:** Clinic go-live (pack riêng) — **không** bị Success P2 chiếm hết capacity.

---

## Quy tắc triển khai

1. Epic phải gắn **một capability** + **một KPI 90 ngày** (vd. checklist %, khách quay lại, điểm KAP).  
2. Ưu tiên mở rộng module/`PackDefinition` hiện có trước khi tạo pack mới (`people_ops`, `ops_excellence` chỉ khi entitlement cần bán riêng).  
3. UI có thể nhóm theo 5 trụ; registry vẫn map `platformModule` theo KIT-BP-ASBUILT.  
4. AI chỉ ship khi có **signal từ data hoặc KAP** — không invent insight giả.

---

## Thông điệp (khi nào dùng)

| Giai đoạn | Message |
|-----------|---------|
| P1 | Vận hành chuẩn + đánh giá sức khỏe cửa hàng (KAP). |
| P2+ Score sống | Giúp nhà thuốc vận hành tốt hơn mỗi quý — nhân sự, quy trình, khách, quyết định bằng dữ liệu. |

---

**Changelog 1.0:** Map Success Platform → as-built modules/gaps/phases; neo KIT-BP-ASBUILT 2.1.1.
