# KitPlatform Pharmacy Storefront

Multi-tenant white-label pharmacy websites (Astro + Cloudflare Pages SSR).

- **Template / pilot:** https://xuanhoa.novixa.vn (`NT_XUANHOA`)
- **Self-serve:** Admin → Hệ thống → Website NT → seed Core → checklist Publish → `{slug}.novixa.vn`
- Runtime resolves Host → `GET /api/public/pharmacy-storefront?slug=` then merges with template defaults.

## Local development

```bash
cd client/pharmacy-storefront
npm install
npm run dev
```

Open [http://localhost:4330](http://localhost:4330). Default registry tenant **xuanhoa**.

| Env | Purpose |
|-----|---------|
| `PUBLIC_STOREFRONT_TENANT` | Fallback slug when Host has no subdomain (default `xuanhoa`) |
| `PUBLIC_API_BASE_URL` | KitPlatform API origin for published profiles (e.g. `https://api.novixa.vn`) |

| Script | Mô tả |
|--------|--------|
| `npm run dev` | Dev server port **4330** |
| `npm run build` | Cloudflare SSR build → `dist/` |
| `npm run preview` | Local preview of SSR build |

## Structure

```
src/
  tenants/
    types.ts          # PharmacyTenantConfig
    xuanhoa.ts        # Pilot content + merge template source
    template-seed.ts  # Generic Core defaults from xuanhoa
    registry.ts       # Static fallback tenants
  lib/
    tenant.ts         # Host → API / registry resolve
    content-mapper.ts # Public JSON → PharmacyTenantConfig
    load-tenant.ts    # Page helper + 404
  pages/              # Core routes (+ /404)
```

## Onboard a pharmacy (no code)

1. Admin → **Website NT** (`/system/storefront-settings`).
2. **Áp dụng mẫu Core** (seed) → điền brand / liên hệ / SP / dịch vụ.
3. Tab **Xuất bản**: checklist xanh → bật Publish → Save.
4. DNS: `{slug}.novixa.vn` must hit the Pages project (see wildcard below).
5. Open `https://{slug}.novixa.vn`.

API:

- Admin: `GET/PUT /api/pharmacy/storefront-profile`, `POST .../seed-default`, `POST .../publish-readiness` (`sales.read` / `sales.write`)
- Public: `GET /api/public/pharmacy-storefront?slug=` (published only; short CDN cache)

DB: `migrations/274_pharmacy_storefront_profiles.sql`.

## Deploy

Workflow: [`.github/workflows/pharmacy-storefront-pages.yml`](../../.github/workflows/pharmacy-storefront-pages.yml)

- Builds with `@astrojs/cloudflare` (`output: server`)
- Sets `PUBLIC_API_BASE_URL=https://api.novixa.vn`
- Deploys Pages project `pharmacy-storefront`
- Attaches `xuanhoa.novixa.vn`

### Wildcard DNS (`*.novixa.vn`)

For self-serve subdomains without attaching each hostname in CI:

1. Cloudflare DNS for `novixa.vn`: CNAME `*` → `{project}.pages.dev` (or the Pages target Cloudflare shows), proxy on.
2. Cloudflare Pages → Custom domains → add `*.novixa.vn` (or rely on wildcard CNAME to Pages).
3. Each published Admin slug then resolves as `{slug}.novixa.vn` without a new deploy.

Pilot/static registry tenants (e.g. file `xuanhoa.ts`) remain fallback when the public API is unreachable.
