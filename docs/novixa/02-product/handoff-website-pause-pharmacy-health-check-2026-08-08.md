# Handoff — Website pause → Pharmacy Health Check (2026-08-08)

## Quyết định

**Tạm dừng** phát triển white-label pharmacy website / storefront commerce.  
**Ưu tiên tiếp theo:** **Pharmacy Health Check** (funnel đánh giá sức khỏe nhà thuốc → lead → tư vấn → triển khai phần pharmacy đã có).

Lý do: cạnh tranh GTM; cần kênh tiếp cận + chốt triển khai nhanh hơn là mở rộng website.

---

## Website — dừng tại đây (resume sau)

| Hạng mục | Trạng thái |
|----------|------------|
| Admin CMS Website (`/website`) | Đã có — lưu Postgres `pharmacy_storefront_profiles` |
| Xuân Hòa hybrid (static brand + CMS articles) | Đã có — **không đụng** `xuanhoa.ts` / nội dung static pilot |
| Migration `276` web commerce | Local đã apply |
| P1 public catalog | `GET /api/public/pharmacy-storefront/{slug}/products` — rate limit, chỉ `show_on_web` |
| P2 web → draft | `POST …/orders` — OTC, giá server, honeypot, rate limit; `source=web` |
| Local DEMO + Xuân Hòa | Profile published + `accept_web_orders` + 24 SP `show_on_web` (dev) |
| Storefront `.env` | `PUBLIC_API_BASE_URL=http://localhost:5290` |
| Script enable | `scripts/enable-local-web-commerce.ps1`, `scripts/sql/enable-local-web-commerce-demo-xuanhoa.sql`, `scripts/apply-276-web-commerce.ps1` |

**Cố ý chưa làm / chưa prod:** deploy storefront commerce lên live, Stripe/payment web, Rx online, mở rộng CMS ngoài P0–P2.

**Resume sau:** bắt đầu từ `client/pharmacy-storefront/README.md` § Local P1/P2 + migration 276.

---

## Trọng tâm mới — Pharmacy Health Check

Sản phẩm GTM: landing **Kiểm tra sức khỏe nhà thuốc** → survey ~7 phút → AI score / PDF → tư vấn 1-1 → bán & triển khai Novixa pharmacy đã có.

### Đã có (điểm neo)

| Thành phần | Vị trí |
|------------|--------|
| Landing CRO | `novixa-site/src/pages/vi/health-check.astro` (+ spa twin) |
| URL survey | `novixa-site/src/lib/assessment.ts` → `survey.novixa.vn` |
| Wireframe UX | `docs/novixa/07-customer/assessment-public-wireframe-v1.md` |
| API engine | `docs/novixa/03-solution/assessment-engine-api-v1.md` |
| Schema / seed | `migrations/068_assessment_engine.sql`, `069_assessment_pharmacy_v1_seed.sql` |
| Survey pack | `src/Packs/Survey/` · admin KAP RBAC (mig 136+) |
| Deploy KAP note | `docs/novixa/03-operations/pharmacy-rbac-deploy-sync-runbook-v1.md` |

### Mục tiêu gần (để “đi triển khai được”)

1. Funnel Health Check **ổn định end-to-end** (landing → survey → complete → lead → PDF/báo cáo).
2. Lead/handoff đủ để sales/CS **chốt go-live** phần pharmacy core đã ship (POS, tồn, NCC, …).
3. Không mở scope website / marketplace trong phase này.

---

*Ghi bởi agent theo yêu cầu user 2026-08-08 — tạm dừng website, tập trung Pharmacy Health Check.*
