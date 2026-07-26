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

Một mục duy nhất: **Kiến thức nhà thuốc**.

| Trường | Ghi chú |
|---|---|
| **Nhóm cha** | Hiện trên menu novixa.vn (Quản trị, Vận hành, Academy…) |
| **Nhóm bài viết** | Chỉ khi Nhóm cha = **Quản trị nhà thuốc** — 8 mục con bên dưới |

### Nhóm bài viết thuộc nhóm cha Quản trị nhà thuốc (Management)

- Chiến lược phát triển
- KPI
- Doanh thu
- Lợi nhuận
- Dòng tiền
- Nhân sự
- Mở chuỗi
- Chuyển đổi số

Trên **novixa.vn** mọi bài Quản trị vẫn hiện chung trong menu **Quản trị nhà thuốc** (không tách menu con).

## Đăng bài

1. Chọn **Kiến thức nhà thuốc** → **New**.
2. Điền tiêu đề, chọn **Nhóm cha**.
3. Nếu Nhóm cha = Quản trị nhà thuốc → chọn thêm **Nhóm bài viết** (Chiến lược / KPI…).
4. Ảnh, ngày đăng, nội dung. Mô tả ngắn có thể bỏ trống — web tự cắt từ phần đầu nội dung.
5. **Publish** / Lưu → chờ Cloudflare vài phút.

Mẹo: thanh bên → **Theo nhóm cha** hoặc **Theo nhóm bài viết (Quản trị)** để lọc nhanh.

| Nhóm cha trong CMS | Hiện trên web |
|---|---|
| Quản trị nhà thuốc | Kiến thức nhà thuốc → Quản trị nhà thuốc |
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
