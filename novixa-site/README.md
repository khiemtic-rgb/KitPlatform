# Novixa — Website giới thiệu

Site marketing **tách biệt** khỏi lõi ERP KitPlatform (`client/admin`, `src/KitPlatform.*`).

- **Domain dự kiến:** [novixa.vn](https://novixa.vn)
- **Ngôn ngữ:** Tiếng Việt (`/vi/…`). Khung i18n sẵn; **English chưa publish**.
- **Tin tức:** Markdown trong `src/content/tin-tuc/`

## Xem trên web (không cần localhost)

Sau khi **push GitHub**, Cloudflare Pages tự build và publish lên **https://novixa.vn** (vài phút).

Đăng tin **chỉ tay** (Markdown / Excel). Job GitHub tự viết bài + đăng fanpage đã tắt vì hay lỗi.

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

**Không** deploy chung với KitPlatform API.

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

### Cách 0 — CMS cho nhân viên (khuyến nghị)

Giao diện web: **https://novixa.vn/admin/**

1. Đăng nhập GitHub (tài khoản được **invite write** vào repo `KitPlatform`).
2. Vào **Tin tức** → **New Tin tức** (hoặc sửa bài cũ).
3. Điền tiêu đề, mô tả, ngày đăng, nội dung → **Publish / Lưu**.
4. (Tuỳ chọn) **Media** → tải ảnh PNG đặt **đúng tên slug** (vd. `ten-bai.png`), khuyến nghị **1200×630**.
5. Chờ Cloudflare build ~2–5 phút → bài lên [novixa.vn/vi/tin-tuc/](https://novixa.vn/vi/tin-tuc/).

**Đăng nhập lần đầu (admin cấu hình 1 lần):**

| Bước | Việc cần làm |
|------|----------------|
| 1 | GitHub → Settings → [Developer settings → OAuth Apps](https://github.com/settings/developers) → New OAuth App |
| 2 | Homepage: `https://novixa.vn` · Callback: `https://novixa.vn/callback` |
| 3 | Cloudflare Pages project → Variables: `GITHUB_CLIENT_ID`, `GITHUB_CLIENT_SECRET` (encrypt), `ALLOWED_DOMAINS=novixa.vn` |
| 4 | Retry deployment |

Chưa có OAuth? Nhân viên vẫn vào `/admin/` → **Sign in with token** (tạo [PAT](https://github.com/settings/personal-access-tokens) quyền `Contents: Read and write` trên repo KitPlatform).

### Cách 1 — Excel / CSV (nhập tay / chỉnh sửa)

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
- Site tĩnh: bài chỉ hiện sau khi **Cloudflare Pages build lại** (push commit có đổi trong `novixa-site/`).
- **Trùng slug hoặc title** → **cập nhật** file `.md` cũ.
- Mẫu: `import/tin-tuc.template.csv`

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

## Fanpage Facebook

Không còn job GitHub đăng fanpage hàng đêm. Nếu cần đăng tay local:

**Local:** `import/Id_Fanpage.txt` (gitignore) — xem mẫu `Id_Fanpage.template.txt`.

```powershell
npm run post:fanpage:dry
npm run post:fanpage
```

## SEO bài tin

Mỗi bài tin có:

| Thành phần | Mô tả |
|------------|--------|
| **Ảnh OG 1200×630** | `public/images/tin-tuc/{slug}.png` — **mỗi bài layout/scene khác nhau**, tagline *Novixa — Nền tảng quản trị nhà thuốc thế hệ mới* |
| **JSON-LD Article** | Schema.org trên trang chi tiết |
| **Open Graph / Twitter** | Title, description, ảnh riêng từng bài |
| **Sitemap** | `/sitemap-index.xml` (Astro sitemap) |
| **robots.txt** | Trỏ sitemap |
| **CTA cuối bài** | Link Giải pháp + Liên hệ |

Sinh ảnh **theo bài đăng** (tiết kiệm Gemini — không tạo lại cả thư viện mỗi lần):

| Lệnh | Mô tả |
|------|--------|
| `npm run publish:news` | Gemini viết bài + ảnh **bài đang đăng** |
| `npm run ensure:today-images` | Gemini/SVG chỉ cho bài **pubDate hôm nay** thiếu `.png` |
| `npm run generate:news-images` | Quét tất cả bài (chỉ khi cần bù hàng loạt) |
| `npm run generate:news-images:cf` | Chỉ Cloudflare Flux Schnell |
| `npm run generate:news-images:ai` | Chỉ Gemini ảnh (có phí) |
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

Form gửi email qua [Formsubmit](https://formsubmit.co) → `care@novixa.vn`. **Lần đầu** cần bấm link xác nhận trong email Formsubmit gửi tới hộp thư.

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

## Xem thống kê trên web

Trang: **https://novixa.vn/vi/thong-ke** — mật khẩu mặc định **`novixa2026`** (đổi trong `src/lib/stats-config.ts`).

- **Tải lại:** gọi `/api/stats` trực tiếp Cloudflare (cần secret **`STATS_VIEW_KEY`** trên Cloudflare Pages = cùng mật khẩu xem trang).
- **Theo từng trang:** bảng *Trang được xem nhiều (24h)* — top URL từ Cloudflare Analytics.
- **Dự phòng:** file `public/stats-snapshot.json` cập nhật qua GitHub Actions mỗi 6 giờ.

**Thiết lập Cloudflare Pages** (Settings → Variables, Production): `STATS_VIEW_KEY`, `CF_ZONE_ID`, `CLOUDFLARE_API_TOKEN` (Analytics Read).

**GitHub Secrets** cho workflow snapshot: `CLOUDFLARE_API_TOKEN` (hoặc `CF_ANALYTICS_API_TOKEN`), `CF_ZONE_ID` tuỳ chọn.

**Actions** → **Novixa update stats** → Run workflow (cập nhật snapshot + deploy).

## Bảo mật

- Site **tĩnh** — không kết nối PostgreSQL / API ERP.
- Form liên hệ: `mailto:care@novixa.vn` (có thể thay Formspree sau).
- ERP demo sau này: `app.novixa.vn` (VPS riêng).

## Liên quan KitPlatform

Trong repo ERP, module **Sales/POS** đã mở lại phát triển sau khi site v1 live — xem `README.md` gốc mục *Sales / POS*.
