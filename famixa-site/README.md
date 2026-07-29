# Famixa site (Next.js 15)

Marketing landing for **Famixa** — AI Human Growth OS for Families.

## Stack

- Next.js 15 (App Router) + React 19
- Tailwind CSS 4 + CSS variables (Design System)
- Framer Motion + Lucide Icons
- Static export → Cloudflare Pages (`out/`)

## Design tokens

| Token | Value |
|-------|--------|
| Primary | `#1FA45A` |
| Dark | `#103B2B` |
| Background | `#F9FBF8` |
| Text | `#1D1D1F` |
| Secondary | `#666` |
| Border | `#E7ECE8` |
| Shadow | `0 10px 30px rgba(0,0,0,.06)` |

Radius: 8 / 12 / 20 / 32 · Spacing: 8…96 · Type: Inter (Hero 64 / Section 40 / Card 22 / Body 18 / Small 14)

## Content

All copy & image paths live in [`content/landing.ts`](content/landing.ts).

- Edit Vietnamese text there
- Swap images by changing `src` under `public/images/`
- Sections receive content as props — no hardcoded marketing strings in UI

## Structure

```
app/vi/page.tsx          # composes sections
content/landing.ts       # editable SoT
components/ui/           # Design System
components/sections/     # isolated landing blocks
components/motion/       # FadeUp, Float, Parallax
```

## Commands

```bash
npm install
npm run dev      # http://localhost:3002
npm run build    # writes ./out
```

## Deploy

Cloudflare Pages project `famixa` — `pages_build_output_dir: ./out` (see `wrangler.jsonc`).

Root `/` redirects to `/vi/`. CTA → `https://home.famixa.vn`.
