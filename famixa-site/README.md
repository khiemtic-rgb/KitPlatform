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

1. Pages → Create project → Connect KitPlatform repo
2. Project name: `famixa` (or `famixa-site`)
3. Root directory: `famixa-site`
4. Build command: `npm run build`
5. Output: `dist`
6. Custom domain: `famixa.vn` (+ `www` → apex)
