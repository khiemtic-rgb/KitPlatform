# Yêu cầu: lọc loại nội dung theo từng nơi đăng (Fanpage)

## Bối cảnh
Thương hiệu `famixa` có 2 Fanpage đang bật "Đang dùng" và đều đã kết nối API Meta:
- `fb-1446082751919974` "Famixa - Vũ trụ phim gia đình" (trang phim): nhận video phim từ Video Studio.
- `fb-main` "Famixa" (page 1127836703756567, trang mẹ): chỉ đăng **tin tức / bài viết**, KHÔNG đăng video phim.

Hiện tại mọi channel `facebook_page` đang active của brand đều nhận mọi thứ (xem `ContentScheduleAdvisorService`: FB page nhận `["fb_page","fb_short","social_caption"]`, và `ContentPublishPlanRules.VideoConnectors` cho video Video Studio đi mọi Fanpage). Kết quả: duyệt một tập phim Famixa sẽ đăng trùng lên cả trang mẹ.

## Cần làm
1. Thêm cấu hình cho từng channel (lưu trong `configJson`, ví dụ `"accepts": ["video","post"]`):
   - `video`: video từ Video Studio (long/short, reel).
   - `post`: bài viết/tin tức (fb_page, social_caption, bài có ảnh).
   - Không có khoá `accepts` = nhận cả hai (giữ hành vi cũ cho các brand khác).
2. Áp dụng bộ lọc ở **mọi** chỗ tạo job/đề xuất lịch:
   - Video Studio bước 5 / publish plan (`VideoConnectors`).
   - Schedule advisor / Agent proposals.
   - Đăng thủ công từ topic (`topics/{id}/publish`) khi chọn "mọi nơi phù hợp".
   Channel không nhận loại đó thì bỏ qua và ghi cảnh báo dễ hiểu, ví dụ: "Fanpage «Famixa» chỉ nhận bài viết, bỏ qua video".
3. UI drawer "Nơi đăng" (form Facebook Fanpage): thêm ô chọn "Nhận nội dung: Video / Bài viết" (checkbox, mặc định tick cả hai).
4. Sau khi deploy, đặt dữ liệu:
   - famixa `fb-main` (trang mẹ) → `["post"]`
   - famixa `fb-1446082751919974` (trang phim) → `["video","post"]`
   - Không đổi các brand khác.
5. Test: unit test cho bộ lọc (thiếu `accepts` = nhận hết; `["post"]` loại video; `["video"]` loại bài viết).

## Không làm
- Không restart service hay sửa nginx/systemd khi chưa hỏi.
- Không commit `appsettings.Development.json`, không in hay đẩy secret.
- Không bật TikTok Direct Post.
- Không động tới channel `fb-1265322806657716` (Vân Đỉnh Trà gắn nhầm dưới famixa, đang tắt).

## Báo lại
Commit hash, file đã sửa, kết quả test. Thêm ảnh chụp drawer có ô "Nhận nội dung".
