# QA — NOVIXA marketing video V1

Trạng thái: **PASS**, với các mục nghe và xem liên tục được ghi ở dưới.

## File

`proof/novixa-marketing-v1/final/novixa-marketing-v1.mp4`

| Mục | Giá trị |
|---|---|
| Kích thước | 11400707 bytes |
| SHA-256 | `6a2701e85a3891f4f93b69cc05f208b3a976d167b830bad899a9d7027c83cb06` |
| Thời lượng | 59,568 giây |
| Video | h264, 1080×1920, 59,500 giây |
| Audio | aac, 48000 Hz, stereo, 59,568 giây |
| Giải mã | `ffmpeg -i file -f null -` thoát 0 |
| Frame đen ≥ 0,2 giây | không có ở lần dựng trước khi chỉnh end card; lần cuối được xem frame chuyển và frame kết |

Mean volume `-25,5 dB` vì các khoảng giữ hình không có nhạc. Đỉnh `-6,0 dB`. Không có nhạc nền.

## Đã xem bằng frame

Đã lấy frame tại khoảng 1s, 6,8s, 9s, 16,6s, 18,5s, 26,4s, 29s, 34,5s, 39,5s, 41,2s, 46s, 53,5s và 57s.

- Shot 1–3 và 5 là cùng một người: tóc đen ngang vai, áo blouse trắng, áo teal, khuyên tròn nhỏ.
- Hộp thuốc và thùng không có nhãn thương hiệu đọc được.
- Shot 4 là screenshot thật: Tổng quan `60.058.235,08 đ` và 72 đơn, màn Tồn kho, màn Báo cáo. Không vẽ lại số.
- Shot 6 dùng logo file thương hiệu. Dòng chữ được vẽ bằng Segoe UI từ đúng chuỗi Unicode, không phải logo AI.
- Không thấy frame đen ở các điểm chuyển đã lấy.

## Chưa làm bằng tai và bằng mắt liên tục

Không phát video trong một trình phát và không nghe file âm thanh. Lời đọc được kiểm qua đúng câu gửi vào `vi-VN-NamMinhNeural`, thời lượng từng file, và stream audio của MP4. Chưa xác nhận bằng tai lỗi phát âm, ngắt từ, hoặc nhạc lấn lời. Không có nhạc nên không có va chạm nhạc và lời.

Chuyển động camera là zoom/pan trên ảnh tĩnh, không phải diễn hoạt 3D. Không có AI video.

## Kiến trúc

Pipeline Production OS đã chứng minh một shot tĩnh cộng một voice. Phim này có sáu shot nên được ghép bằng `assemble.py` trong thư mục này. Không tạo binding `ACCEPTED` và không gọi mix của platform.

## Không đụng phần cũ

MP4 E2E một shot vẫn 575741 bytes, SHA-256 `054397f90a6ac3f2660b1e7a3bc71bf7e18b3d031c25b921f5d326362b176f1e`.

Keyframe Famixa `os-kf/5a1325ae8a7b4f389f496e24f483ace7.mp4` vẫn 113493 bytes, SHA-256 `9fe2bd76034203a14531631fd1911e3ef15f1249c588259a17c692c952305d57`.

Không deploy, không migration, không commit.

## File của phiên này

Toàn bộ nằm trong `proof/novixa-marketing-v1/`: `brief.md`, `script.md`, `shot-list.md`, `qa-report.md`, `assemble.py`, `assets/`, `renders/`, `final/novixa-marketing-v1.mp4`.
