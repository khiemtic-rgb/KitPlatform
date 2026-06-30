# Novixa — Website giới thiệu

Site marketing **tách biệt** khỏi lõi ERP PharmaCore (`client/admin`, `src/PharmaCore.*`).

- **Domain dự kiến:** [novixa.vn](https://novixa.vn)
- **Ngôn ngữ:** Tiếng Việt (`/vi/…`). Khung i18n sẵn; **English chưa publish**.
- **Tin tức:** Markdown trong `src/content/tin-tuc/`

## Xem trên web (không cần localhost)

Sau khi **push GitHub**, Cloudflare Pages tự build và publish lên **https://novixa.vn** (vài phút).

Workflow hàng ngày: `.github/workflows/novixa-scheduled-publish.yml` — bài tin mới lên đúng `pubDate`.

## Chạy local (tuỳ chọn)

```powershell
cd novixa-site
npm install
npm run dev
```

Mở http://localhost:4321 → redirect `/vi`.

## Build

```powershell
npm run build
npm run preview
```

## Deploy (miễn phí)

### Cloudflare Pages / Vercel

1. Push repo lên GitHub (monorepo: root = `novixa-site` hoặc repo riêng).
2. Import project → **Root directory:** `novixa-site`
3. Build: `npm run build` — Output: `dist`
4. Gán domain `novixa.vn` / `www.novixa.vn` trong DNS.

**Không** deploy chung với PharmaCore API.

## Cấu trúc

```
novixa-site/
  src/
    content/tin-tuc/     # Bài viết (.md)
    i18n/vi.json         # Chuỗi UI tiếng Việt
    i18n/en.json         # Skeleton EN (chưa dùng route)
    pages/vi/            # Trang công khai
```

## Thêm tin tức

### Cách 1 — Excel / CSV (khuyến nghị)

1. Đặt file vào `import/tin-tuc.xlsx` (hoặc `tin-tuc.csv`).
2. Cột: `title` hoặc `description` (tiêu đề), `pubDate`, `slug` (tuỳ chọn), `content`.
3. Chạy:

```powershell
cd novixa-site
npm run import:news
git add src/content/tin-tuc import/
git commit -m "Import tin tuc"
git push
```

- **pubDate** trong tương lai → bài **ẩn** đến đúng ngày (giờ VN).
- **Trùng slug hoặc title** → **cập nhật** file `.md` cũ.
- Mẫu: `import/tin-tuc.template.csv`

GitHub Actions `novixa-scheduled-publish.yml` chạy import + deploy hàng ngày. Tuỳ chọn: secret `CF_DEPLOY_HOOK` (Cloudflare Pages → Deploy hooks).

### Cách 2 — Markdown tay

Tạo file `src/content/tin-tuc/ten-bai.md`:

```markdown
---
title: "Tiêu đề"
description: "Mô tả ngắn"
pubDate: 2026-06-20
lang: vi
---

Nội dung...
```

## Đăng tự động fanpage Facebook

Cùng lịch `pubDate` với site — workflow hàng ngày chạy `npm run post:fanpage`.

**Local:** `import/Id_Fanpage.txt` (gitignore) — xem mẫu `Id_Fanpage.template.txt`.

**GitHub Secrets** (Settings → Actions):

| Secret | Giá trị |
|--------|---------|
| `FB_PAGE_ID` | Page ID (số) |
| `FB_PAGE_ACCESS_TOKEN` | Page Access Token |

Log đã đăng: `import/fanpage-posted.json`.

```powershell
npm run post:fanpage:dry
npm run post:fanpage:dry -- --date=2026-07-01
npm run post:fanpage
```

## SEO bài tin (tự động)

Mỗi bài tin có:

| Thành phần | Mô tả |
|------------|--------|
| **Ảnh OG 1200×630** | `public/images/tin-tuc/{slug}.png` — **mỗi bài layout/scene khác nhau**, tagline *Novixa — Nền tảng quản trị nhà thuốc thế hệ mới* |
| **JSON-LD Article** | Schema.org trên trang chi tiết |
| **Open Graph / Twitter** | Title, description, ảnh riêng từng bài |
| **Sitemap** | `/sitemap-index.xml` (Astro sitemap) |
| **robots.txt** | Trỏ sitemap |
| **CTA cuối bài** | Link Giải pháp + Liên hệ |

Sinh ảnh (ưu tiên **Cloudflare Workers AI Flux** — free):

| Lệnh | Mô tả |
|------|--------|
| `npm run generate:news-images` | CF Flux (nếu có creds) → OpenAI → SVG |
| `npm run generate:news-images:cf` | Chỉ Cloudflare Flux Schnell |
| `npm run generate:news-images:ai` | Chỉ OpenAI (có phí) |
| `npm run generate:news-images:svg` | Chỉ SVG (miễn phí, 8 layout/bài) |

**Cloudflare (khuyến nghị):** Dashboard → [Workers AI](https://dash.cloudflare.com/) → **Use REST API** → copy **Account ID** + tạo **API Token**. Điền vào `.env` hoặc file local `import/cf-workers-ai.txt` (gitignored):

```
Account ID: your-account-id
API Token: your-token
```

GitHub Actions: secrets `CLOUDFLARE_ACCOUNT_ID`, `CLOUDFLARE_API_TOKEN`.

Tự chạy khi `import:news` và `prebuild`.

Sau deploy: submit sitemap tại [Google Search Console](https://search.google.com/search-console).

## Liên hệ (Zalo, Fanpage, form email)

Cấu hình trong `src/i18n/vi.json` → mục `contact`:

| Trường | Ví dụ |
|--------|--------|
| `phoneDisplay` | `0901 234 567` (hiển thị) |
| `phoneTel` | `+84901234567` (gọi / tel:) |
| `zaloPhone` | `0901234567` (nút Chat Zalo) |
| `facebookUrl` | `https://www.facebook.com/tenfanpage` |
| `facebookPageName` | `Novixa` |

Form gửi email qua [Formsubmit](https://formsubmit.co) → `khiemtic@gmail.com`. **Lần đầu** cần bấm link xác nhận trong email Formsubmit gửi tới hộp thư.

## Theo dõi lượt truy cập

Site dùng **[Cloudflare Web Analytics](https://developers.cloudflare.com/web-analytics/)** (miễn phí, không cookie banner, không hiện số công khai trên web).

1. Cloudflare Dashboard → **Analytics & Logs** → **Web Analytics** → **Add a site** → chọn `novixa.vn`.
2. Copy **token** (beacon).
3. Cloudflare Pages → project `novixa-site` → **Settings** → **Environment variables**:
   - Name: `PUBLIC_CF_WEB_ANALYTICS_TOKEN`
   - Value: token vừa copy
   - Environment: **Production** (và Preview nếu muốn)
4. **Redeploy** site (Deployments → Retry deployment).

Xem số liệu: Dashboard → **Web Analytics** → chọn site — lượt xem, trang, nguồn truy cập, quốc gia. Trang quan trọng: `/vi`, `/vi/giai-phap`, `/vi/lien-he`.

Local: đặt token trong `.env` (gitignore) rồi `npm run dev` / `npm run build`.

## Xem thống kê trên web (không cần vào Cloudflare Dashboard)

Trang riêng: **https://novixa.vn/vi/thong-ke** (không hiện trên menu, có mật khẩu).

**Cấu hình một lần** — Cloudflare Pages → Settings → Environment variables (Production):

| Biến | Cách lấy |
|------|----------|
| `STATS_VIEW_KEY` | Mật khẩu bạn tự đặt (vd. `novixa2026`) — nhập khi mở trang thống kê |
| `CF_ZONE_ID` | Cloudflare → domain **novixa.vn** → Overview → **Zone ID** (cột phải) |
| `CLOUDFLARE_API_TOKEN` | API Token có quyền **Zone → Analytics → Read** |

Sau khi thêm biến → **Retry deployment**. Mở `/vi/thong-ke`, nhập `STATS_VIEW_KEY`, xem visitor hôm nay / 24h / 7 ngày, biểu đồ và trang được xem nhiều.

## Bảo mật

- Site **tĩnh** — không kết nối PostgreSQL / API ERP.
- Form liên hệ: `mailto:khiemtic@gmail.com` (có thể thay Formspree sau).
- ERP demo sau này: `app.novixa.vn` (VPS riêng).

## Liên quan PharmaCore

Trong repo ERP, module **Sales/POS** đã mở lại phát triển sau khi site v1 live — xem `README.md` gốc mục *Sales / POS*.
