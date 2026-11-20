# QA — NOVIXA marketing V2

Kết quả: **BLOCKED**

Không READY FOR PUBLISHING. FFmpeg giải mã thành công không được tính là xem hoặc nghe xong.

## Đã đo

| Hạng mục | Kết quả |
| --- | --- |
| File | `proof/novixa-marketing-v2/final/novixa-marketing-v2.mp4` |
| Bytes | 4,153,526 |
| SHA-256 | `b67897d2ae5e29423da52549441168d7bfe0af074eb39c407c23e8cfb7e21cab` |
| Duration | 56.386s |
| Video | h264, 1080×1920 |
| Audio | aac, 48 kHz, stereo |
| Decode | `ffmpeg -f null` exit 0 |

Mức âm lượng từng giây cho thấy sáu đoạn lời đọc, khoảng nghỉ giữa câu, và im từ khoảng giây 52 đến hết phim. Logo đã có trong frame ở giây 51 và vẫn còn ở giây 55.

## Hình đã xem bằng frame

Đã đọc frame tại 1.2s, 16s, 26s, 32s, 38s, 51s, 55s.

- 1.2s: toàn cảnh, áo blouse trắng, áo teal, tay trên sổ.
- 16s: cận tay xếp hộp không nhãn.
- 26s: nghiêng người đặt thùng; nhãn thùng không có chữ đọc được.
- 32s: `60.058.235,08 đ` và `159.006.588,01 đ`, đủ bảy mốc ngày, nhãn “Dữ liệu minh họa”.
- 38s: đủ Mã SP / Tên SP của sáu dòng, cùng biểu đồ, không có cột bị cắt.
- 51s và 55s: logo NOVIXA, dòng “Nền tảng vận hành nhà thuốc thông minh”, `novixa.vn`.

visual QA chưa hoàn tất. Đây là frame rời, không phải xem liên tục. Chưa kiểm tra trên điện thoại thật. Frame 1080×1920 là preview 9:16; nhãn demo cỡ 42px.

## Âm thanh

audio QA chưa hoàn tất. Không có bộ nhận dạng giọng Việt trên máy và không nghe được file. `boundaries.txt` rỗng vì lần lấy WordBoundary không trả text. Chưa xác nhận cách đọc `NOVIXA`, `nhà thuốc`, `hàng tồn`, `lợi nhuận` hay câu kết. Chưa thể kết luận có cụm nào bị cụt.

## Nguồn không đổi

| File | Bytes | SHA-256 |
| --- | --- | --- |
| V1 `novixa-marketing-v1.mp4` | 11,400,707 | `6a2701e85a3891f4f93b69cc05f208b3a976d167b830bad899a9d7027c83cb06` |
| E2E `1a5e640dd8d4401db2ec6f005abdb444.mp4` | 575,741 | `054397f90a6ac3f2660b1e7a3bc71bf7e18b3d031c25b921f5d326362b176f1e` |
| Famixa keyframe `5a1325ae8a7b4f389f496e24f483ace7.mp4` | 113,493 | `9fe2bd76034203a14531631fd1911e3ef15f1249c588259a17c692c952305d57` |
