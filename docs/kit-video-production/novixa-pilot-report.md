# Báo cáo pilot NOVIXA Intro Film

Ngày ghi: 2026-10-23. Phạm vi dừng ở hệ thống V1 và một style proof. Phim khoảng ba phút chưa được dựng.

## Kết luận ngắn

Hệ thống đọc thời lượng và số cảnh từ manifest, đã xuất hai dự án kiểm thử khác nhau và một style proof 15,533 giây. QA kỹ thuật của các file này PASS. Chưa có người xem liền mạch và chưa có người nghe, nên chưa có mục nào được gọi là chất lượng nghệ thuật đã đạt.

File để duyệt phong cách:

`proof/kit-video-production-v1/outputs/novixa-style-proof/20261023T021435Z-style-proof/video.mp4`

1920×1080, 30 fps, H.264 + AAC, 15,533 giây, 602.315 byte, SHA-256 `2bde679b77f650a2568611785d2688152a005184651d2fb466c3477dd96fac3e`.

## Style proof đang cho xem cái gì

Trích đúng hai câu cuối của phân đoạn 3, không viết lại:

“Có những mặt hàng bán vèo vèo, nhưng cũng có những hộp cứ nằm yên trên kệ hết ngày này sang ngày khác. Hàng thì vẫn ở đó… còn tiền thì đã đi trước một bước rồi.”

Câu đầy đủ của phân đoạn 3 vẫn nằm trong `script.md` và trong cảnh đề xuất `s03-stock-cash`. Đoạn proof không chứa từ NOVIXA, nên chưa kiểm được cách đọc “Nô-vi-xa”.

Giọng là edge-tts `vi-VN-NamMinhNeural`, tốc độ `+0%`. File lời 9,936 giây. Cảnh cộng 3,2 giây mở hình và 2,4 giây giữ hình. Không dùng `atempo`. 15–20 giây đạt bằng khoảng mở và khoảng giữ, không bằng cách đọc nhanh.

Các khung đã mở và xem, không phải xem liền 15 giây:

- Đầu cảnh: một hộp thuốc chiếm gần hết khung rồi thu vào kệ.
- Giữa: hai hộp tab xanh rời sang phải kèm vệt chuyển động; ba hộp tab nâu ở lại. Lịch ở khoảng trống lật tới số 4.
- Cuối: ba hộp thành khối navy nằm trên một két, mặt khóa thấy hết trong khung, một đồng không ghi số đi sang phải. Dòng `Hàng tồn kho cũng là vốn` nằm trên, không bị cắt.

Đó là chuyển động vật thể và một ẩn dụ vốn bị khóa. Không phải slideshow. Nhân vật là hình học tạm. SFX là placeholder. Không có nhạc. Không có UI sản phẩm và không có số liệu doanh thu.

Contact sheet và các khung: cùng thư mục render, `contact-sheet.png` và `frames/`.

Một bản encode trước đó bị lệch tiếng vì delay lời chưa vào mix. Bản đó đã xóa để khỏi bị xem nhầm. Bản `20261023T021435Z-style-proof` là bản để duyệt.

## Kịch bản đầy đủ

`analyze` ra 12 cảnh, tất cả `proposed`, tổng ước lượng 250,68 giây. Hint 180 giây của riêng dự án này bị cảnh báo và không bị ép.

| Cảnh | Ước lượng | Template |
| --- | ---: | --- |
| s01-day-opens | 20,15s | custom |
| s02-busy | 23,76s | custom |
| s03-stock-cash | 17,92s | metaphor_locked_capital |
| s04-expiry | 18,53s | calendar_flip |
| s05-introduce | 20,68s | brand_reveal |
| s06-selling | 20,53s | ui_showcase |
| s07-inventory | 20,07s | ui_showcase |
| s08-connected-data | 21,84s | data_connection |
| s09-people | 20,61s | character_entrance |
| s10-philosophy | 21,68s | custom |
| s11-positioning | 21,53s | brand_reveal |
| s12-cta | 23,38s | end_card |

Lệnh render cả phim trả BLOCKED, exit 2, vì 12 cảnh còn `proposed`. Không có file phim đầy đủ.

Ước lượng 250,68 giây là số ký tự, chưa phải giọng đọc. Khóa timing chỉ sau khi đọc cả bài ở tốc độ tự nhiên.

## Hai dự án chứng minh dùng lại pipeline

Cùng code, không sửa lõi giữa hai lần.

| | North | South |
| --- | --- | --- |
| Project | `reuse-short-north` | `reuse-structured-south` |
| Cảnh | 3 | 5 |
| Thời lượng | 30,000s | 25,000s |
| Khung | 960×540, 16:9, 24 fps | 540×960, 9:16, 24 fps |
| Template | kinetic_type, object_move, end_card | character_entrance, calendar_flip, data_connection, object_transform, brand_reveal |
| File | `outputs/reuse-short-north/20261023T021023Z-full/video.mp4` | `outputs/reuse-structured-south/20261023T021114Z-full/video.mp4` |
| Byte | 143.710 | 194.001 |
| SHA-256 | `2ca97d06e485e18358a0d10914e2a58193b205ea8951e153a206b37cabe85b02` | `7e8d3a19bfd3b8ee6f3495aabc6dd784e4c66a7361ede907f95fd4300d8c4da5` |

Cả hai có nhãn `DỰ ÁN KIỂM THỬ` trên hình, không có lời đọc, SFX là placeholder. QA kỹ thuật PASS. Tổng thể NEEDS HUMAN REVIEW.

Dựng lại riêng cảnh `n2-move`: `outputs/reuse-short-north/20261023T021119Z-scene-n2/video.mp4`, 12,000 giây, 66.994 byte, SHA-256 `a834a8d82ab6cca8777649760ec6edae2356c8ed51f5d413ed11401af18c7e61`. File 30 giây vẫn còn.

## QA

Cả bốn file trên: giải mã exit 0, đúng khung, đúng fps, H.264/AAC, không đoạn đen, đỉnh tiếng dưới ngưỡng clipping, chữ bắt buộc có trong layout và không vượt vùng an toàn, placeholder được liệt kê. `overallStatus` của cả bốn là NEEDS HUMAN REVIEW vì hai mục xem hình và nghe tiếng.

Báo cáo máy: `qa-report.json` trong từng thư mục render.

`python -m unittest tests.test_system` tại `tools/kit-video-production`: 8 test, OK, khoảng 0,7 giây. Gồm validator, analyzer không ép lời, từ chối UI giả, và một render 1 giây.

## Nghiệm thu

| # | Hạng mục | Trạng thái |
| --- | --- | --- |
| 1 | Kiến trúc và tài liệu | PASS |
| 2 | Manifest và shot plan có validation | PASS |
| 3 | Không hardcode thời lượng hay số cảnh | PASS |
| 4 | Thư viện template dùng lại | PASS |
| 5 | Quy trình asset | PASS |
| 6 | Render ra video thật | PASS |
| 7 | Render lại một cảnh, không ghi đè | PASS |
| 8 | QA và báo cáo | PASS ở mức kỹ thuật. Tổng thể NEEDS HUMAN REVIEW |
| 9 | Hai dự án khác thời lượng và cấu trúc | PASS |
| 10 | Style proof NOVIXA | NEEDS HUMAN REVIEW. File có. Chưa được duyệt phong cách |
| 11 | Phân biệt việc máy làm và việc người làm | PASS, xem bảng trong kiến trúc |
| 12 | Hướng dẫn tạo dự án từ kịch bản | PASS |
| 13 | Không ghi đè asset và phim cũ | PASS. Chỉ xóa một bản encode hỏng và một bản lệch tiếng do chính lần chạy này tạo |
| 14 | Không đụng production ngoài phạm vi | PASS. Không sửa Content pack, không deploy, không commit |

## Blocker trước khi dựng cả phim

- Chưa có người nghe giọng, kể cả một câu có từ NOVIXA.
- Chưa có nhạc được cấp quyền.
- SFX còn là placeholder.
- Nhân vật chưa được duyệt. Cảnh 1, 2 và 10 là `custom`.
- Ba ảnh UI (POS nhân viên, tồn kho, báo cáo) còn `proposed` / `reference`. Chưa re-open pixel trong lần này. Chưa được gắn vào cảnh.
- Tỷ lệ 16:9 của proof là lựa chọn của dự án để kệ hàng có chiều ngang. Chưa khóa. Bản dọc làm bằng cách sửa `format`.
- Ước lượng chữ 250,68 giây so với hint 180 giây. Cần đo giọng cả bài rồi mới chia cảnh.

## Bước tiếp theo, sau khi style proof được xem

1. Nghe `video.mp4` của proof và file `assets/voice/style-s03.wav`. Duyệt hoặc yêu cầu đọc lại. Không tăng tốc.
2. Duyệt hoặc sửa hướng hình: người hình học, hộp, két, lịch, màu lấy từ logo.
3. Nếu duyệt, đọc cả 12 đoạn ở `+0%`, căn beat, rồi mới khóa shot plan.
4. Thiết kế riêng cảnh mở, cảnh bận và cảnh triết lý.
5. Xác minh lại từng màn UI trước khi đổi role thành `product_ui` và gắn nhãn `Dữ liệu minh họa`.
6. Đưa nhạc và SFX đã có quyền vào asset. Rồi render và QA. Vẫn cần người xem hết và nghe hết trước khi gọi là xong.

Không phát hành từ bản proof này.
