# Style proof V2 — khảo sát, quyết định và kết quả

Bản này chốt hướng hình cho đoạn lời đã dùng ở proof V1. Không phải lệnh dựng phim NOVIXA bốn phút. Shot plan phim đầy đủ vẫn `proposed`.

## Quyết định kỹ thuật

Máy đang có Python 3.14, Pillow, NumPy, FFmpeg 8.1 và edge-tts. Không có Blender, Inkscape, Cairo, Skia hay Manim. Không cài thêm và không mua dịch vụ.

| Tiêu chí | A. Sân khấu vector có khớp, vẽ bằng Pillow | B. Ảnh minh họa cắt lớp rồi trượt |
| --- | --- | --- |
| Chất lượng hình có thể đạt | Khối, nét và màu thống nhất. Trần là hình vẽ code, không phải nét họa sĩ. | Một khung có thể đẹp hơn. Nhiều khung dễ lệch nét. |
| Diễn hoạt và tương tác đạo cụ | Tay với, thùng nén, bay theo cung, rơi, mặt đổi. | Khó bẻ khớp. Chủ yếu trượt lớp. |
| Kể theo từng kịch bản | `mode` chọn người, đạo cụ, hoặc cả hai. | Mỗi kịch bản cần bộ ảnh mới. |
| Tái sử dụng | Palette, câu chữ, số thùng, beat, tỷ lệ khung là dữ liệu. | Bộ cắt dính một nhân vật. |
| Thời gian và độ ổn định | Khoảng 0,3 giây một khung 1080p. Encode đã chạy hết. | Ổn nếu ảnh có sẵn. Không có bộ ảnh. |
| Gắn vào pipeline hiện có | Thêm template. Lõi manifest không đổi. | Vẫn phải viết compositor. |
| Chi phí | Không. | Ảnh thuê hoặc mô hình trả phí chưa được duyệt. |
| Sửa và mở rộng | Sửa params hoặc thêm template. | Sửa ảnh gốc từng lần. |

Chọn A. B sẽ cao hơn trần hình nếu có một bộ minh họa được duyệt, nhưng máy không có bộ đó và không được tự mua. Khoảng cách còn lại: người và két vẫn là khối có chủ đích, chưa phải thiết kế nhân vật cuối; không có nhạc có bản quyền; giọng chưa được người nghe duyệt.

## Kiến trúc sau nâng cấp

- Lõi: manifest, shot plan, thời lượng, asset, encode, QA. Template V1, gồm `metaphor_locked_capital`, vẫn chạy.
- Lớp hình: `kit_video/picture.py`, template `stock_story`.
- Lớp tiếng: lời, SFX, ducking. Proof này `musicPolicy: none`.
- Brand: palette của dự án. `skin`, `wood`, `wall`, `floor`, `cloth`, `coin` là khóa tùy chọn.

## Ba hướng trước khi chốt

Cùng lời, cùng nhịp, chỉ khác `mode`. Khung nằm ở `proof/kit-video-production-v1/projects/novixa-style-proof-v2/storyboard/`.

1. `acting` — người lớn hơn, với tay tới kệ.
2. `metaphor` — không người, thùng và két lớn.
3. `hybrid` — người gây ra cú bán nhanh, rồi hàng còn lại thành vốn trong két.

Chốt `hybrid`. Hai hướng kia giữ làm storyboard, không dựng thành hai phim.

## So với V1

Proof V1 giữ nguyên: `proof/kit-video-production-v1/outputs/novixa-style-proof/20261023T021435Z-style-proof/video.mp4`.

| | V1 | V2 |
| --- | --- | --- |
| Người | Đầu tròn, thân khối | Tóc, mặt, áo, tạp dề, tay khớp, mặt đổi lúc hàng nặng |
| Đạo cụ | Khối bo góc | Thùng có nắp và hông, kệ có mặt, két có khóa |
| Chuyển động | Trượt và đổi độ mờ | Lấy đà, với tay, bay theo cung có xoay, lịch đổi số, đồng lăn ra trước, hàng rơi vào két |
| Lời | Một mạch, khoảng 9,9 giây | Cùng giọng NamMinh, `+0%`, tách bốn nhịp, file 12,048 giây |
| Nhạc | Không | Không |
| SFX | Placeholder | Placeholder, ghi trong log |

Nghĩa kinh doanh không đổi: hàng bán nhanh đi, hàng nằm lại, tiền đi trước.

## File để xem

`proof/kit-video-production-v1/outputs/novixa-style-proof-v2/20261023T025435Z-style-proof-v2/video.mp4`

Mở bằng trình phát bất kỳ. 16,633 giây, 1920×1080, 30 fps, H.264 và AAC. 499 khung hình. 2 189 748 byte. SHA-256 `73e5af15009310cd9705aaaa99daad333d08fd51f63eb7edd8ba326dc1cf3f89`.

Thư mục `20261023T025100Z-style-proof-v2` là lần rơi quá muộn. Không dùng lần đó để duyệt.

Lệnh dựng lại:

```text
python tools/kit-video-production/kv.py render proof/kit-video-production-v1/projects/novixa-style-proof-v2 --repo E:\KitPlatform --scenes s03-style-v2 --allow-style-proof --label style-proof-v2
```

## Tái sử dụng

- Cùng hàm vẽ, project `reuse-story-v2`: headline `Hàng nằm lại`, 3 thùng, palette xanh lá, 640×360, 12 fps, 4,000 giây. File: `outputs/reuse-story-v2/20261023T025059Z-reuse/video.mp4`. QA kỹ thuật PASS.
- Test `test_stock_story_follows_brand_input` vẽ 480×270 và 270×480 với hai câu chữ, hai màu. Cả hai có chữ đã khai và hai ảnh khác nhau.
- Khung `storyboard/reuse-portrait.png` là 9:16.
- Shot plan phim `novixa-intro-film` không bị chuyển sang `approved`.

## QA kỹ thuật

Bản `20261023T025435Z`: technical PASS, overall NEEDS HUMAN REVIEW.

Đạt: giải mã, 16,633 giây khớp lịch render, 1920×1080, 30 fps, H.264/AAC, không khung đen, đỉnh tiếng −6,10 dB, hai khoảng lặng có lý do, chữ trong vùng an toàn, câu trên hình có mặt, SFX được ghi là placeholder.

Chín test trong `tests/test_system.py` đều OK.

Hai mục máy không làm thay người: xem hình liền mạch, nghe tiếng.

Ghi chú hash: `render-log.json` băm manifest tại lúc encode. Ghi chú pipeline trong manifest được sửa sau encode, nên hash manifest hiện tại không khớp log. Shot plan lúc encode đã có khoảng lặng mở đến 2,8 giây và tiếng đặt tại 12,48 giây. Ảnh và tiếng của file mp4 không đổi vì lần sửa ghi chú đó.

## QA hình — điểm từ các khung đã xem

Điểm dưới đây lấy từ các khung preview đã mở, không lấy từ việc render thành công. Chưa phát video liền mạch và chưa nghe tiếng. Người duyệt cần xem và nghe file trước khi nhận điểm.

| Tiêu chí | Điểm | Bằng chứng đã xem |
| --- | --- | --- |
| Art direction | 4 | Một phòng, một bảng màu navy/xanh/giấy/gỗ, một dòng chữ. Khung `f0.90`. |
| Thiết kế người và đạo cụ | 3 | Người có tóc, mặt, áo, tạp dề và tay. Vẫn là khối. Két còn giống tủ. Khung `f0.03`, `f0.25`. |
| Ẩn dụ | 4 | Thùng xanh bay đi (`f0.25`). Hàng còn lại xám rồi nằm trên két. Đồng đã ở bên phải két. Chữ “Hàng tồn kho cũng là vốn” (`f0.82`, `f0.90`). |
| Diễn hoạt | 4 | Tay với lên khi thùng đang bay và xoay (`f0.25`). Mặt cau khi hàng nặng (`f0.60`). Hàng ở mép kệ rồi nằm trên két (`f0.72`, `f0.82`). Cảm giác lúc phát cần người xem xác nhận. |
| Bố cục | 4 | Người bên trái, hàng bên phải, chữ trên cùng, két phía trước. Tường còn trống nhiều. |
| Nhịp | 4 | Mở, bay, lịch từ 02 sang 09, nặng, rơi, câu chữ. Một cảnh nên chưa có chuyển cảnh sang cảnh khác. |
| Giọng và tiếng | Chưa chấm | File lời 12,048 giây, `+0%`, bốn nhịp đo từ boundary. SFX là placeholder. Không có nhạc. Cần nghe. |
| Hợp một video thương hiệu | 3 | Đủ để xét hướng. Chưa đủ để khóa hình cho phim dài. |

Bốn tiêu chí cửa (art direction, bố cục, diễn hoạt, ẩn dụ) đang ở 4 trên khung tĩnh. Thiết kế người là 3. Giọng chưa có điểm nghe. Vì vậy chưa đề xuất dựng phim đầy đủ.

## Chi phí

Không phát sinh. Không đăng ký và không mua giọng, nhạc, hay công cụ.

Nếu sau khi nghe, giọng miễn phí không đạt: edge-tts có thêm `vi-VN-HoaiMyNeural`. Lần gọi đủ câu bị từ chối audio; một mẫu câu ngắn đã lưu ở `projects/novixa-style-proof-v2/assets/voice/v2-hoaimy-sample.wav` và không đưa vào phim. Giọng trả phí chưa được báo giá sống và chưa được mua.

## Việc chưa làm

- Chưa xem liền mạch và chưa nghe file. Đó là việc của người duyệt.
- Chưa có nhạc hợp pháp.
- Chưa có thiết kế nhân vật của họa sĩ.
- Chưa dựng 12 cảnh phim. Không tự duyệt shot plan đó.
- Bước tiếp, sau khi người xem file: nếu hướng hybrid được nhận, nâng thiết kế người và két trước, rồi mới tính cảnh tiếp theo. Nếu người chấm art direction hoặc ẩn dụ dưới 4 sau khi xem video, sửa proof này, không render phim dài để che.
