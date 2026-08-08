# KitPlatform Pharmacy Storefront

Multi-tenant pharmacy websites (Astro → **Cloudflare Workers** SSR).

- **Pilot:** https://xuanhoa.novixa.vn (static registry dual-run; do not treat as CMS template for others)
- **Other pharmacies:** Host `{slug}.novixa.vn` → published CMS only; unpublished → soft 404 (never Xuân Hòa)

## Local development

```bash
cd client/pharmacy-storefront
npm install
npm run dev
```

| Env | Purpose |
|-----|---------|
| `PUBLIC_STOREFRONT_TENANT` | Dev fallback slug (default `xuanhoa`) |
| `PUBLIC_API_BASE_URL` | Public CMS API origin |

## Deploy

Workflow [`.github/workflows/pharmacy-storefront-pages.yml`](../../.github/workflows/pharmacy-storefront-pages.yml):

1. `astro build` with `@astrojs/cloudflare`
2. `wrangler deploy` (Workers — not Pages Functions)
3. Attach `xuanhoa.novixa.vn` + `demo-pharmacy.novixa.vn` as Worker custom domains

Admin CMS: Hệ thống → Website NT → Publish.
