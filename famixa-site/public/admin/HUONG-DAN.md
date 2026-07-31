# Hướng dẫn nhanh — đăng bài Famixa (Góc cha mẹ)

Mở: **https://famixa.vn/admin/**

## Đăng nhập (Access Token)

1. Bấm **Sign In Using Access Token**.
2. Vào https://github.com/settings/personal-access-tokens
3. Tạo token (Fine-grained):
   - Repository: chỉ **KitPlatform**
   - Permissions → **Contents: Read and write**
4. Copy token → dán vào CMS → đăng nhập.

Tài khoản GitHub phải có quyền **Write** trên repo `khiemtic-rgb/KitPlatform`.

## Đăng bài mỗi ngày

1. Chọn **Góc cha mẹ** → **New**.
2. Điền **Tiêu đề** — URL/file tự sinh (bỏ dấu, gạch ngang). Ví dụ *5 phút buổi sáng...* → `/vi/goi-cha-me/5-phut-buoi-sang/`.
3. Chọn **Chủ đề**, upload **Ảnh hiển thị** (1200×630 khuyến nghị).
4. Chọn **Ngày đăng** — có thể hẹn ngày tương lai, bài sẽ tự ẩn đến đúng ngày (giờ VN).
5. Viết **Nội dung** (markdown).
6. **Publish** → chờ Cloudflare build vài phút → xem tại https://famixa.vn/vi/goi-cha-me/

## Lưu ý

| Mục | Ghi chú |
|---|---|
| **Góc cha mẹ** (`/vi/goi-cha-me/`) | Blog tips — đăng hàng ngày |
| **Câu chuyện** (`/vi/cau-chuyen/`) | Gia đình thật — chỉnh trong code / riêng, không qua CMS này |
| **Hướng dẫn** (`/vi/huong-dan/`) | How-to app — markdown trong repo |

- Bật **Bản nháp** nếu chưa muốn hiện web.
- **Mô tả ngắn** có thể bỏ trống — web tự cắt từ đầu bài.
- Gặp lỗi đăng nhập: kiểm tra token GitHub hoặc liên hệ IT.
