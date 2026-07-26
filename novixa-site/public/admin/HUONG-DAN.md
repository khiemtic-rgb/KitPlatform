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

## Cấu trúc CMS

| Trong CMS | Ghi chú |
|---|---|
| **Quản trị nhà thuốc (Management)** | Mục cha — chọn thêm **Mục con** (bắt buộc) |
| **Kiến thức & Tin tức khác** | Vận hành, Academy, Bán hàng, AI, Connect, Câu chuyện KH, Tin tức Novixa |

### Mục con của Quản trị nhà thuốc (Management)

- Chiến lược phát triển
- KPI
- Doanh thu
- Lợi nhuận
- Dòng tiền
- Nhân sự
- Mở chuỗi
- Chuyển đổi số

Trên **novixa.vn** mọi bài Quản trị vẫn hiện chung trong menu **Quản trị nhà thuốc** (không tách menu con).

## Đăng bài Quản trị

1. Chọn **Quản trị nhà thuốc (Management)** → **New**.
2. Điền tiêu đề, **Mục con** (bắt buộc), ảnh, ngày đăng, nội dung. Mô tả ngắn có thể bỏ trống — web tự cắt từ phần đầu nội dung.
3. **Publish** / Lưu → chờ Cloudflare vài phút.
4. Kiểm tra: https://novixa.vn/vi/kien-thuc/quan-tri-nha-thuoc/

Mẹo: thanh bên → **Theo mục con** để lọc Chiến lược / KPI / Doanh thu…

## Đăng bài nhóm khác

1. Chọn **Kiến thức & Tin tức khác** → **New**.
2. Chọn **Nhóm bài viết** (Vận hành, Academy, Tin tức Novixa…).
3. Lưu như trên.

| Nhóm trong CMS | Hiện trên web |
|---|---|
| Vận hành nhà thuốc | Kiến thức nhà thuốc → Vận hành nhà thuốc |
| Novixa Academy | Kiến thức nhà thuốc → Novixa Academy |
| Bán hàng & Chăm sóc khách hàng | Kiến thức nhà thuốc → Bán hàng & CSKH |
| AI trong nhà thuốc | Kiến thức nhà thuốc → AI trong nhà thuốc |
| Chăm sóc khách hàng (Connect) | Kiến thức nhà thuốc → Connect |
| Câu chuyện khách hàng | Kiến thức nhà thuốc → Câu chuyện khách hàng |
| Tin tức Novixa | **Về Novixa** → Tin tức Novixa |

## Ảnh đại diện

Trường **Ảnh hiển thị** → Upload (khuyến nghị **1200 × 630** px).

## Lưu ý

- Sau khi lưu, website tự cập nhật qua Cloudflare.
- Gặp lỗi đăng nhập: liên hệ IT (invite GitHub / token).
