# Sandbox NOVIXA 2026-10-29 — báo cáo
Thư mục này do Claude tạo, chỉ chứa bản sao và kết quả thử. Không có file gốc nào bị sửa, không DB, không production, không gọi ElevenLabs/Gemini.
Xóa an toàn: xóa cả thư mục `E:\KitPlatform\_sandbox_novixa_20261029`.

## Nguồn kịch bản
`E:\KitPlatform\proof\kit-video-production-v1\outputs\novixa-studio\7c3f98037ae749ac88b0b08ae3a031ae\job.json` (job NOVIXA mới nhất trên đĩa, sửa 2026-10-26; 64 câu "Những thất thoát vô hình trong nhà thuốc"). Bản sao: `source-job.json`. Chưa xác nhận đây là "kịch bản sau cùng" trong DB (không truy cập DB).

## Đã làm
- Chạy `novixa_scene_board.py` (kế hoạch hình) trên 64 câu, trong môi trường cloud, font DejaVu thay Segoe UI. 64/64 khung 1080×1920 dựng được. Kết quả: `visual-plan-assessment.json`, `sheet0..3.png` (mỗi sheet 16 khung).

## Chưa làm được (và lý do)
- **Chưa có video MP4.** Cần giọng đọc: `build_stock_motion.py` lấy khóa ElevenLabs từ DB production qua appsettings rồi gọi API tính phí; tôi không làm việc đó khi bạn vắng. Cũng cần bước "duyệt ảnh" của bạn theo thiết kế UI.
- Không chạy được code C# (`ContentVideoService`, renderer) vì môi trường không có .NET và không chạy được lệnh trên máy bạn.

## Đánh giá khung hình theo tiêu chí "người chưa biết gì phải hiểu đang xem gì": CHƯA ĐẠT
1. Kịch bản 64 câu (~4 phút) không khớp template `tiktok_45s_hooks` (45 giây, 5 beat). Cần bản rút gọn hoặc template dài.
2. Hình trừu tượng, nhiều nhãn rời nghĩa: "Nhóm này", "Ít khi nói", "Không ai để ý", "Hạn/Chậm/Lúc", "T1–T4"; có khung gần như trống (ví dụ khung 33, 34).
3. Phụ đề bị cắt ở 3 dòng (khung 4, 32, 33, 35, 39…) làm mất cuối câu.
4. Hình không minh họa đúng nội dung câu (ví dụ "Có loại vừa nhập về đã bán hết" hiện cột biểu đồ nghiêng).
5. Nhãn "Dữ liệu minh họa" xuất hiện nhưng số liệu vẫn trông như thật.
Lưu ý: bản font thay thế có thể làm tràn chữ khác Segoe; mục 3 cần kiểm lại trên máy bạn.

## Đề xuất bước kế (khi bạn quay lại)
1. Chọn bản kịch bản 45 giây (5 beat) làm đầu vào, hoặc duyệt rút gọn từ 64 câu.
2. Cho phép tôi sửa trong sandbox `novixa_scene_board.py` để mỗi câu có hình diễn đúng ý (hộp thuốc, kệ, ngày hết hạn, dòng tiền, màn hình Novixa thật) và nhãn có nghĩa; sửa cắt phụ đề.
3. Bạn chạy pha stills, duyệt ảnh, rồi mới gọi giọng và dựng MP4 (bạn quyết chi phí ElevenLabs).
