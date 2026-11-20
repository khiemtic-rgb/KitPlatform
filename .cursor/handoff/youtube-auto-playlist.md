# Yêu cầu: Agent tự chọn hoặc tạo danh sách phát YouTube theo chủ đề video

## Hiện trạng
Từ commit dac52b18, Agent chỉ thêm video vào danh sách phát khi ô "Danh sách phát" ở bước Xuất (Video Studio) có tên (`FindPlaylistByTitleAsync`, chưa có thì tạo). Ô để trống thì video không vào danh sách nào.

## Quy tắc mới
Khi Agent upload một video YouTube (long hoặc Shorts):
1. **Ô Danh sách phát có tên** → giữ như cũ: tìm đúng tên, chưa có thì tạo.
2. **Ô để trống** → Agent tự chọn:
   a. Lấy danh sách phát của kênh: `playlists.list mine=true`, đọc title + description.
   b. Chấm độ phù hợp giữa video (tiêu đề, mô tả, tags, series/episode nếu có) và từng danh sách phát. Dùng LLM sẵn có của Content pack (như chỗ viết caption). Trả JSON `{playlistId|null, score 0-1, reason}`.
   c. score ≥ 0.7 → thêm video vào danh sách đó (`playlistItems.insert`).
   d. Không danh sách nào đạt → tạo danh sách mới theo đúng chủ đề:
      - Tiêu đề ngắn (≤ 60 ký tự), nói chủ đề, không clickbait. Ví dụ: "Toán của thua lỗ: vì sao giữ vốn trước tiên".
      - Mô tả 1–2 câu + hashtag của brand. Brand trading (Người Giữ Vốn) phải có câu: "Nội dung chỉ chia sẻ kiến thức, không phải lời khuyên đầu tư. Giao dịch có rủi ro mất vốn."
      - Chế độ hiển thị: công khai.
   e. Video thuộc series có nhiều tập (episode của cùng series) → các tập sau ưu tiên vào cùng danh sách với tập trước. Lưu `playlistId` vào series/topic `extra_json.publish.youtubePlaylistId` để lần sau dùng lại, không cần chấm lại.
3. Ghi kết quả vào `resultJson.playlist`: `{id, title, action: "added"|"created", reason}`. Admin hiện dòng này trong chi tiết job.
4. Lỗi danh sách phát (quota, thiếu quyền) không được làm hỏng job upload. Chỉ ghi cảnh báo.
5. Bước Xuất của Video Studio: đổi chữ gợi ý của ô Danh sách phát thành "Để trống: Agent tự chọn danh sách hợp chủ đề hoặc tạo mới".

## Dữ liệu tham khảo (kênh Người Giữ Vốn hiện có 3 danh sách phát)
- "Toán của thua lỗ: vì sao giữ vốn trước tiên". Tập 1 "Vì sao phần lớn người giao dịch cá nhân thua lỗ?" đã được thêm tay vào đây (video 4zIZfHtDOSU).
- "Vốn sống và vốn giao dịch: tiền nào được mang vào thị trường"
- "Bắt đầu từ đây: Người Giữ Vốn là ai?"
Tập 2 "Đã cháy một lần, vì sao vẫn cháy lần hai" là phần tiếp của Tập 1 → phải vào "Toán của thua lỗ...".

## Test
Unit test cho: ô có tên → dùng tên; ô trống + có danh sách khớp → added; ô trống + không khớp → created; series đã có playlistId → dùng lại; lỗi playlist → job vẫn Succeeded kèm cảnh báo.

## Không làm
- Không restart kit-famixa-api, không sửa nginx/systemd khi chưa hỏi.
- Không commit `appsettings.Development.json`, không in secret.
- Không bật TikTok Direct Post.
- Không sửa danh sách phát hay video đã có trên kênh.

## Báo lại
Commit hash, file sửa, kết quả test. Muốn deploy thì xin phép restart trước.
