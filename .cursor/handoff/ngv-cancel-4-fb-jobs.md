# Huỷ 4 lượt đăng Fanpage cũ của Người giữ vốn trên VPS B

Chủ dự án đã duyệt: huỷ 4 job dưới đây. Không xoá dòng, chỉ đổi trạng thái để còn lịch sử.
Thương hiệu Người giữ vốn (code `trading`, brand_id 01a18af7-3833-7777-a59a-3e64255f72ff) đang tạm dừng đăng; Fanpage Người Giữ Vốn đã có lịch đăng tay riêng trong Business Suite cùng giờ 19:30, nên các job này sẽ đăng trùng khi bấm "Chạy lại đăng bài".

Job cần huỷ (pack_content.publish_job, kênh Facebook Page, status = 'Queued'):
| Giờ đăng (VN) | Bài |
|---|---|
| 10/11/2026 19:30 | Cách đơn giản nhất là chia tiền của bạn thành ba ngăn | Người Giữ Vốn |
| 11/11/2026 19:30 | Mình gọi đó là sự khác nhau giữa cháy tài khoản và cháy ví | Người Giữ Vốn |
| 12/11/2026 19:30 | Với tiền vay, bạn có thể mất hết vốn mà vẫn còn nợ phải trả | Người Giữ Vốn |
| 13/11/2026 19:30 | chỉ mang vào thị trường số tiền mà nếu mất hết | Người Giữ Vốn |

## Việc cần làm
1. SELECT trước để xác nhận đúng 4 dòng: brand_id như trên, status 'Queued', kênh facebook_page, publish_at từ 2026-11-10 12:30Z đến 2026-11-13 12:30Z. Ghi id, giờ, tiêu đề bài vào báo cáo. Nếu ra khác 4 dòng thì dừng và báo lại.
2. UPDATE đúng 4 id đó: status = 'Cancelled' (dùng đúng giá trị trạng thái huỷ mà code đang dùng; nếu có cột ghi chú/lỗi thì ghi "Huỷ: trùng lịch Fanpage đăng tay (chủ dự án duyệt 10/11)"). Nếu job gắn schedule_proposal thì đặt proposal tương ứng về trạng thái huỷ bằng API `POST /api/content/agent/proposals/{id}/cancel` thay vì sửa tay.
3. Kiểm tra lại trang Lịch đăng: 4 lượt này không còn "Queued".
4. Không bỏ tạm dừng đăng của Người giữ vốn và Novixa. Không đụng job khác.
