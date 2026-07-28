# Famixa marketing site — famixa.vn

Astro static site. Deploy: Cloudflare Pages (project root `famixa-site`, output `dist`).

## Local

```powershell
cd famixa-site
npm install
npm run dev    # http://localhost:4322/vi
npm run build
```

## Domain

- Marketing: `https://famixa.vn`
- App CTA: `https://family.kittech.vn` (pilot)

## Cloudflare Pages

1. Workers & Pages → Create → Connect to Git → `khiemtic-rgb/KitPlatform`
2. Project name: **`famixa`** (không tái dùng project Novixa `KitPlatform`)
3. Root directory: **`famixa-site`**
4. Build: `npm run build` · Output: **`dist`** · Node **22**
5. Custom domains: `famixa.vn`, `www.famixa.vn`
6. Hoặc GHA `Famixa Pages provision` sau khi set secret `CLOUDFLARE_API_TOKEN`
