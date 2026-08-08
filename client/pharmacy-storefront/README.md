# KitPlatform Pharmacy Storefront

White-label pharmacy websites (Astro static → Cloudflare Pages).

- **Pilot live:** https://xuanhoa.novixa.vn (`NT_XUANHOA`)
- **Self-serve CMS:** Admin → Hệ thống → Website NT (content API ready; Host-based SSR needs Cloudflare Workers migration — Astro 7 adapter is Workers-only)

## Local development

```bash
cd client/pharmacy-storefront
npm install
npm run dev
```

Open [http://localhost:4330](http://localhost:4330). Build-time tenant **xuanhoa** (`PUBLIC_STOREFRONT_TENANT`).

| Env | Purpose |
|-----|---------|
| `PUBLIC_STOREFRONT_TENANT` | Registry slug baked into static HTML (default `xuanhoa`) |

| Script | Mô tả |
|--------|--------|
| `npm run dev` | Dev server port **4330** |
| `npm run build` | Static build → `dist/` |
| `npm run preview` | Preview static build |

## Deploy

Workflow: [`.github/workflows/pharmacy-storefront-pages.yml`](../../.github/workflows/pharmacy-storefront-pages.yml)

- Static Astro build (`PUBLIC_STOREFRONT_TENANT=xuanhoa`)
- Deploys Pages project `pharmacy-storefront`
- Attaches `xuanhoa.novixa.vn`

> **Note:** Multi-tenant `{slug}.novixa.vn` from CMS publish requires migrating this app to **Cloudflare Workers** (`wrangler deploy`). Astro 7 `@astrojs/cloudflare` no longer supports Pages Functions.
