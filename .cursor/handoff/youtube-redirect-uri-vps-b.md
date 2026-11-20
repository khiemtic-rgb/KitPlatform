# Sửa kết nối YouTube trên VPS B (thiếu địa chỉ quay lại OAuth)

Hiện tượng: trang Kênh đăng trên https://famixa.157.10.53.182.sslip.io bấm "Kết nối lại" YouTube không làm gì.
API `GET /api/content/youtube/oauth/start` trả 400: "Chưa cấu hình địa chỉ quay lại của Google OAuth — đặt Content__YoutubeRedirectUri …".
Kênh YouTube Novixa và Người Giữ Vốn đang "Cần kết nối lại" (refresh token hết hạn), nên Agent không đăng YouTube được.

Chủ dự án đã duyệt: sửa cấu hình và khởi động lại kit-famixa-api.

## Việc cần làm
1. Trong `/etc/kit-famixa/api.env` thêm (hoặc sửa) dòng:
   `Content__YoutubeRedirectUri=https://famixa.157.10.53.182.sslip.io/content/youtube/callback`
   Không in, không commit các giá trị bí mật khác trong file.
2. Kiểm tra Google Cloud Console → OAuth client đang dùng (YOUTUBE_CLIENT_ID trên B) → "Authorized redirect URIs" đã có đúng địa chỉ trên. Chưa có thì báo chủ dự án thêm (hoặc thêm nếu có quyền).
3. `systemctl restart kit-famixa-api`, xem log khởi động.
4. Thử: gọi `GET /api/content/youtube/oauth/start?brandId=019fec28-586c-7c7c-907e-38e11152dcf7` (Novixa) với token admin: phải trả 200 và url accounts.google.com có redirect_uri đúng như trên. Báo kết quả.
5. Không bỏ trạng thái tạm dừng đăng của Novixa và Người giữ vốn.
