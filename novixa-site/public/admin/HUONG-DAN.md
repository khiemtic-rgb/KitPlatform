# Hướng dẫn nhanh — đăng tin Novixa

Mở: **https://novixa.vn/admin/**

## Đăng nhập (Access Token)

1. Bấm **Sign In Using Access Token**.
2. Mở link GitHub trong hộp thoại (hoặc vào https://github.com/settings/personal-access-tokens ).
3. Tạo token (Fine-grained):
   - Repository: chỉ chọn **KitPlatform**
   - Permissions → **Contents: Read and write**
   - Metadata: Read-only (mặc định)
4. Copy token → dán vào CMS → đăng nhập.

Tài khoản GitHub phải được **invite Write** vào repo `khiemtic-rgb/KitPlatform`.

## Đăng bài mới

1. Chọn **Kiến thức nhà thuốc** → **New**.
2. Điền:
   - **Tiêu đề**
   - **Mô tả ngắn** (1–2 câu)
   - **Nhóm bài viết** — Kiến thức nhà thuốc (các nhóm) hoặc **Tin tức Novixa** (menu Về Novixa)
   - **Ảnh hiển thị**
   - **Ngày đăng** — để ngày tương lai nếu muốn lên lịch
   - **Nội dung** — dùng tiêu đề phụ `##`, danh sách `*`, in đậm `**chữ**`
3. Bấm **Publish** / Lưu.
4. Đợi 2–5 phút rồi kiểm tra https://novixa.vn/vi/kien-thuc/

## Ảnh đại diện

Trong form bài viết, dùng trường **Ảnh hiển thị** → Upload / chọn ảnh (khuyến nghị **1200 × 630** px).

Không cần đặt tên file trùng slug — CMS lưu đường dẫn vào bài.

## Sửa / xóa bài

Vào **Kiến thức nhà thuốc** → chọn bài → sửa nội dung → Lưu. Xóa chỉ khi chắc chắn.

Mẹo: dùng bộ lọc **Theo nhóm** (thanh bên) để tìm bài theo nhóm nhanh hơn.

## Nhóm bài viết — bài sẽ hiện ở đâu?

| Nhóm chọn trong CMS | Hiện trên web |
|---|---|
| Quản trị nhà thuốc | Menu **Kiến thức nhà thuốc** → Quản trị nhà thuốc |
| Vận hành nhà thuốc | Menu **Kiến thức nhà thuốc** → Vận hành nhà thuốc |
| Novixa Academy | Menu **Kiến thức nhà thuốc** → Novixa Academy |
| Bán hàng & Chăm sóc khách hàng | Menu **Kiến thức nhà thuốc** → Bán hàng & CSKH |
| AI trong nhà thuốc | Menu **Kiến thức nhà thuốc** → AI trong nhà thuốc |
| Chăm sóc khách hàng (Connect) | Menu **Kiến thức nhà thuốc** → Connect |
| Câu chuyện khách hàng | Menu **Kiến thức nhà thuốc** → Câu chuyện khách hàng |
| Tin tức Novixa | Menu **Về Novixa** → Tin tức Novixa |

## Lưu ý

- Sau khi lưu, website tự cập nhật qua Cloudflare — không cần nhờ IT deploy.
- Không sửa file ngoài mục Kiến thức nhà thuốc trong CMS.
- Gặp lỗi đăng nhập: liên hệ IT (invite GitHub / token / OAuth).
