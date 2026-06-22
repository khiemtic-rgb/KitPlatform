# Novixa — Website giới thiệu

Site marketing **tách biệt** khỏi lõi ERP PharmaCore (`client/admin`, `src/PharmaCore.*`).

- **Domain dự kiến:** [novixa.vn](https://novixa.vn)
- **Ngôn ngữ:** Tiếng Việt (`/vi/…`). Khung i18n sẵn; **English chưa publish**.
- **Tin tức:** Markdown trong `src/content/tin-tuc/`

## Chạy local

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

## Bảo mật

- Site **tĩnh** — không kết nối PostgreSQL / API ERP.
- Form liên hệ: `mailto:khiemtic@gmail.com` (có thể thay Formspree sau).
- ERP demo sau này: `app.novixa.vn` (VPS riêng).

## Liên quan PharmaCore

Trong repo ERP, module **Sales/POS** tạm **freeze feature mới** đến khi site v1 live — xem `README.md` gốc mục *Development freeze*.
