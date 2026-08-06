# KitPlatform Pharmacy Storefront

Multi-tenant white-label pharmacy storefronts (Astro). Mỗi nhà thuốc = một file config dưới `src/tenants/`; Host (hoặc `PUBLIC_STOREFRONT_TENANT`) chọn brand.

Pilot subdomain: **https://xuanhoa.novixa.vn** · tenant code `NT_XUANHOA`.

## Local development

```bash
cd client/pharmacy-storefront
npm install
npm run dev
```

Mở [http://localhost:4330](http://localhost:4330). Mặc định tenant **xuanhoa**.

| Script | Mô tả |
|--------|--------|
| `npm run dev` | Dev server port **4330** |
| `npm run build` | Production build |
| `npm run preview` | Preview build port **4330** |

## Cấu trúc

```
src/
  tenants/
    types.ts       # PharmacyTenantConfig
    xuanhoa.ts     # Nội dung / brand NT Xuân Hòa
    registry.ts    # Đăng ký tenant + match Host
  lib/tenant.ts    # resolveTenant(request)
  components/      # Header, Footer, Home sections
  layouts/         # BaseLayout, SimplePage
  pages/           # /, /gioi-thieu, /san-pham, /kien-thuc, /lien-he
  styles/storefront.css
```

## Thêm nhà thuốc mới (ít sửa code)

1. Copy `src/tenants/xuanhoa.ts` → `src/tenants/<slug>.ts`, sửa brand, màu, contact, copy, CTA App (`?tenantCode=...`), hosts.
2. Trong `registry.ts`: import, thêm vào `tenants[]`, thêm `hostMatchers` (`includes: '<slug>'`).
3. DNS: trỏ `<slug>.novixa.vn` (hoặc domain trong `hosts`) về deployment storefront.
4. (Tuỳ chọn) `PUBLIC_STOREFRONT_TENANT=<slug>` khi build/static không đọc được Host.

Không cần fork layout — UI đọc từ `PharmacyTenantConfig`.

## Deploy pilot `xuanhoa.novixa.vn`

**Track:** Cloudflare Pages (cùng mô hình `novixa-site` / `famixa-site`) — **không** đi qua `deploy-update-vps.ps1`.

1. Secrets GitHub: `CLOUDFLARE_API_TOKEN`, `CLOUDFLARE_ACCOUNT_ID` (đã dùng cho marketing sites).
2. Chạy workflow [`.github/workflows/pharmacy-storefront-pages.yml`](../../.github/workflows/pharmacy-storefront-pages.yml) (`workflow_dispatch` hoặc push đổi `client/pharmacy-storefront/**`).
3. Workflow build `dist/`, tạo project Pages `pharmacy-storefront` nếu chưa có, deploy, gắn domain `xuanhoa.novixa.vn`.
4. DNS zone `novixa.vn`: Cloudflare sẽ hướng dẫn CNAME Pages (thường tự gắn khi domain trên cùng account).
5. Local preview: `npm run preview` → http://localhost:4330/

`wrangler.jsonc` trong thư mục này: project name `pharmacy-storefront`, output `./dist`.

### CMS Admin

- Admin: **Hệ thống → Website NT** (`/system/storefront-settings`) — brand, contact, hero, publish.
- API admin: `GET/PUT /api/pharmacy/storefront-profile` (cần `sales.read` / `sales.write`).
- API public: `GET /api/public/pharmacy-storefront?slug=xuanhoa` hoặc `?tenantCode=NT_XUANHOA` (chỉ khi `isPublished`).
- Phase 1: site static vẫn đọc file `src/tenants/*.ts`. CMS lưu DB để đồng bộ / SSR ở bước sau (contract JSON ≈ `PharmacyTenantConfig`).

Migration: `migrations/274_pharmacy_storefront_profiles.sql`.