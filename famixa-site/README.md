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

## Xem thống kê trên web

Trang: **https://famixa.vn/vi/thong-ke** — mật khẩu mặc định **`famixa2026`** (đổi trong `lib/stats-config.ts`).

- **Tải lại:** gọi `/api/stats` (Cloudflare Pages Function) — cần `STATS_VIEW_KEY` trên project `famixa` (= cùng mật khẩu).
- **Dự phòng:** `public/stats-snapshot.json` cập nhật qua GitHub Actions mỗi 6 giờ (`Famixa update stats`).

**Cloudflare Pages** (Settings → Variables, Production): `STATS_VIEW_KEY`, `CF_ZONE_ID` (zone famixa.vn), `CLOUDFLARE_API_TOKEN` (Analytics Read).

**GitHub Secrets:** `CF_ANALYTICS_API_TOKEN` hoặc `CLOUDFLARE_API_TOKEN`; tuỳ chọn `FAMIXA_CF_ZONE_ID`, `FAMIXA_CF_DEPLOY_HOOK`.

**Web Analytics (thu thập):** set `NEXT_PUBLIC_CF_WEB_ANALYTICS_TOKEN` khi build nếu dùng Cloudflare Web Analytics beacon.

## Chuyên trang P0

| VI | EN |
|----|----|
| `/vi/goi/` | `/en/plans/` |
| `/vi/cau-chuyen/` | `/en/stories/` |
| `/vi/goi-cha-me/` | _(VI only — blog)_ |
| `/vi/huong-dan/` | _(VI only — guide)_ |
| `/vi/ve-famixa/` | `/en/about/` |
| `/vi/chinh-sach-bao-mat/` | `/en/privacy/` |
| `/vi/dieu-khoan/` | `/en/terms/` |

Nội dung chuyên trang: `content/specialty.ts`. Blog: `content/blog/*.md`. Hướng dẫn: `content/guide/*.md`.

## Blog — Góc cha mẹ

| URL | Mô tả |
|-----|--------|
| `/vi/goi-cha-me/` | Hub bài viết |
| `/vi/goi-cha-me/[slug]/` | Bài chi tiết |

- SoT: `content/blog/*.md` (frontmatter + markdown body)
- Loader: `lib/blog/content.ts`
- UI: `components/blog/*`
- Chủ đề: `lib/blog/categories.ts`
- Bài `draft: true` hoặc `pubDate` tương lai (giờ VN) sẽ ẩn trên web

## CMS đăng bài (Sveltia)

| URL | Mô tả |
|-----|--------|
| **https://famixa.vn/admin/** | Giao diện đăng bài |
| `public/admin/config.yml` | Schema collection |
| `public/admin/HUONG-DAN.md` | Hướng dẫn nhanh |

Đăng nhập bằng **GitHub Access Token** (Contents: Read/Write trên repo `KitPlatform`). Lưu bài → commit lên `main` → Cloudflare Pages tự build.

**Phân vai nội dung:**

| Mục | Cách sửa |
|-----|----------|
| Góc cha mẹ (blog) | CMS `/admin/` |
| Câu chuyện | `content/specialty.ts` |
| Hướng dẫn app | `content/guide/*.md` |
| Landing | `content/landing.json` |
