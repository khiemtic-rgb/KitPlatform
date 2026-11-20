# Yêu cầu: bình luận đầu tiên cho YouTube và cho bài Facebook hẹn giờ

## Hiện trạng (commit da227c74)
- YouTube: `ContentPublishService` không gọi API bình luận nào sau khi upload. Video YouTube không bao giờ có bình luận đầu tiên.
- Facebook: `FacebookFirstCommentAsync` trả về `"bỏ qua: bài hẹn giờ trên Facebook, chưa bình luận được"` khi `scheduled = true`. Mọi bài/Reel/video Agent hẹn giờ trước đều mất bình luận đầu tiên.
- Lý do kỹ thuật: video YouTube để private/hẹn giờ và bài FB hẹn giờ đều chưa công khai, nên chưa bình luận được lúc upload.

## Cần làm
1. **Hàng chờ "bình luận sau khi lên sóng"**
   - Khi đăng xong mà chưa bình luận được (FB scheduled, YouTube có `publishAt` hoặc đang private), lưu một việc chờ gồm: channel, connector, externalId (postId/videoId), nội dung bình luận, thời điểm lên sóng. Bảng mới hoặc dùng work queue sẵn có.
   - Worker chạy mỗi 5 phút. Với việc đã quá giờ lên sóng + 2 phút: kiểm tra bài đã công khai chưa, rồi đăng bình luận.
   - Thử lại tối đa 6 lần, cách 10 phút. Hết lượt thì ghi `lastError` dễ hiểu.
   - Lưu kết quả vào `resultJson.firstComment` của job gốc.
2. **YouTube**
   - Dùng `commentThreads.insert` (scope `youtube.force-ssl` đã có trong `ContentYoutubeClient`) để bình luận dưới tên kênh.
   - Nội dung lấy từ variant YouTube. Nếu có tách phần "bình luận đầu tiên" như FB (`ContentFacebookCaption.Split`) thì dùng phần đó. Không có thì dùng CTA link của brand + câu miễn trừ (brand NGV: "Nội dung chỉ chia sẻ kiến thức, không phải lời khuyên đầu tư. Giao dịch có rủi ro mất vốn.").
   - API YouTube không ghim được bình luận. Ghi rõ trong `resultJson` là "cần ghim tay" và hiện nhắc trên admin.
   - Video công khai ngay lúc upload thì bình luận ngay, không cần chờ.
   - Token cũ thiếu scope thì trả `NEED_RECONNECT` kèm thông báo "Kết nối lại YouTube để Agent bình luận được". Không làm hỏng job upload.
3. **Facebook**: bỏ nhánh `return "bỏ qua..."` khi scheduled. Thay bằng việc đưa vào hàng chờ ở mục 1.
4. **Admin**: trong chi tiết job đăng, hiện trạng thái bình luận đầu tiên: Đã bình luận / Đang chờ lên sóng / Lỗi + lý do / Cần ghim tay (YouTube).
5. **Test**: unit test cho bộ chọn việc đến hạn, nhánh scheduled → vào hàng chờ, và nhánh YouTube thiếu scope.

## Không làm
- Không đụng các bài đã lên lịch tay trong Business Suite hay YouTube Studio.
- Không restart kit-famixa-api, không sửa nginx/systemd khi chưa hỏi.
- Không commit `appsettings.Development.json`, không in secret.
- Không bật TikTok Direct Post. TikTok API không bình luận được, bỏ qua.

## Báo lại
Commit hash, file sửa, kết quả test, có cần migration DB không. Muốn deploy thì xin phép restart trước.
