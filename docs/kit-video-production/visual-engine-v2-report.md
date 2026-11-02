# Visual engine V2 — prototype theo reference

Ngày 2026-10-23. Một cảnh 12 giây. Không phải phim NOVIXA. Shot plan của các dự án cũ không bị đổi. File V1 và V2 còn nguyên.

## 1. So sánh công nghệ

Đã kiểm tra trên máy này. Không cài thêm và không gọi API video trả phí.

| Hướng | Trạng thái | Bằng chứng | Chất lượng hình | Chuyển động | Chữ Việt | Tái sử dụng | Gắn pipeline | Thời gian / chi phí |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Ảnh thiết kế + ghép lớp | Đã làm và đã render | Prototype `novixa-visual-ref` | Nền nhà thuốc có chiều sâu, dược sĩ minh họa, hộp / lọ / vỉ khác nhau | Cung có lấy đà và dừng. Nhân vật là một tư thế, không có xương | Segoe UI, khớp dữ liệu, không biến dạng | `stage.json` + params + palette | Template `reference_stage`, QA kỹ thuật PASS | 360 khung, khoảng 19 giây encode. Không có hóa đơn |
| Vector / rig keyframe | Chưa cài | `inkscape`, `cairo`, `svglib`, `manim`, `skia`, `synfig` không có trên máy | Giả thuyết | Giả thuyết | Font hệ thống làm được nếu có công cụ | Giả thuyết | Chưa gắn | Cần cài. Chưa được duyệt |
| 3D | Chưa cài | `blender` không có trong PATH | Giả thuyết | Giả thuyết | Chữ 3D dễ méo nếu không vẽ ở lớp 2D | Giả thuyết | Chưa gắn | Cần cài. Chưa được duyệt |

Pillow, NumPy, FFmpeg 8.1.2 và Segoe UI là công cụ đã có và đã dùng. Chúng ghép và encode được. Chúng không tự vẽ được dược sĩ trong reference.

## 2. Phương án đã chọn

Ghép ảnh minh họa đã đăng ký, chuyển động bằng easing, chữ vẽ ở lớp riêng.

Đánh đổi:

- Ra hình gần reference hơn các khối của V1 và V2, trong môi trường hiện có.
- Dược sĩ là bán thân, một tư thế, tay để dọc. Hộp tự nhấc theo cung. Không có bàn tay cầm hộp.
- Mặt hộp để trống. Ngày `08/2026` là thẻ chữ đi cùng hộp, để dấu không bị méo trong file ảnh.
- Nền xuất ở 1280×720 vì ảnh nền sinh ra đúng cỡ đó. Phóng lên 1920×1080 sẽ làm mềm hình.

## 3. File

Đã đọc để làm: `kit_video/templates.py`, `canvas.py`, `render.py`, `validate.py`, `qa.py`, tài liệu kiến trúc, catalog, giới hạn, và các ảnh reference nhà thuốc / dược sĩ đã có trong phiên.

Đã thêm hoặc sửa:

- `tools/kit-video-production/kit_video/stage.py`
- `tools/kit-video-production/kit_video/templates.py`
- `tools/kit-video-production/tests/test_system.py`
- `proof/kit-video-production-v1/projects/novixa-visual-ref/`
- `docs/kit-video-production/` các file kiến trúc, catalog, giới hạn, quy chuẩn hình, hướng dẫn dự án, README

Không sửa video V1, video V2, và không đổi shot plan của các dự án cũ.

## 4. Video

`proof/kit-video-production-v1/outputs/novixa-visual-ref/20261023T032944Z-visual-ref/video.mp4`

Mở file đó bằng trình phát. Hoặc:

```text
ffplay proof/kit-video-production-v1/outputs/novixa-visual-ref/20261023T032944Z-visual-ref/video.mp4
```

12.000 giây, 1280×720, 30 fps, H.264 + AAC, 360 khung, 585.951 byte.

SHA-256 `80a326b9f44afd8f148a294c5d7169f4adc1db6c2eafb1c8ead5a229f8ad7506`

## 5. Test và QA kỹ thuật

`python -m unittest tests.test_system -q` — 10 test, OK.

QA của lần render: technical PASS, overall NEEDS HUMAN REVIEW.

Đúng 12.000 giây, 1280×720, 30/1, h264, aac. Không khung đen. Không chữ vượt vùng an toàn. Trên hình có đúng `08/2026` và `Hàng nằm lại là vốn`. Im có chủ đích, max −91 dB. Không placeholder.

`config-swap.jpg` trong thư mục dự án dùng cùng sân khấu, đổi thành `Lô 09/2026`, `Tiền đã đi trước`, và màu nhấn xanh lá. Test tự động còn đổi asset đang chuyển động từ hộp sang lọ.

## 6. Chuyển động đã kiểm

Đã xem khung tại khoảng 0,8 giây, 3,8 giây, 6,4 giây và 8,4 giây.

- 0,8 giây: hộp nằm trên kệ giữa, lọ ở kệ dưới, vỉ trên quầy, dược sĩ sau quầy.
- 3,8 giây: hộp ở trên không, nghiêng, nhãn đi cùng.
- 8,4 giây: hộp đã nằm trên mặt quầy, có bóng tiếp xúc, câu chữ đã hiện.

Đo 360 khung liên tiếp, thu về 160×90. Không có khung nào lệch quá ba lần mức median của các khung xung quanh. Độ lệch trung bình cao ở giây 1–6, rồi thấp khi hộp đã dừng. Đó là bằng chứng không có bước nhảy vị trí, không phải bằng chứng đã xem phát.

`ffplay` chạy hết một lượt và thoát mã 0. Cửa sổ nằm sau ứng dụng khác. Người làm không nhìn thấy hình lúc phát. Không kết luận độ mượt bằng mắt.

## 7. Chủ sản phẩm cần xem

1. Mặt dược sĩ và nhà thuốc có đúng hướng reference đã duyệt hay không.
2. Hộp rời kệ, đi cung, và dừng trên quầy khi xem ở tốc độ thường.
3. Hộp, lọ, vỉ có đọc được là ba loại thuốc hay không.
4. Câu `Hàng nằm lại là vốn` và ngày `08/2026` có rõ và đúng nhịp hay không.
5. Việc không có bàn tay cầm hộp có chấp nhận được cho bước sau hay không.

## 8. Chi phí

Không đăng ký dịch vụ mới. Không gọi API video trả phí. Không cài phần mềm. Ba ảnh (nền, người, bộ sản phẩm) được tạo bằng công cụ ảnh có sẵn trong editor, rồi tách nền cục bộ. Không có hóa đơn để ghi.

## 9. Giới hạn và bước sau

Chưa đạt reference ở chỗ dược sĩ không với tay và không cầm sản phẩm. Một tư thế không phải diễn hoạt nhân vật. Mặt hộp trắng, chưa phải bao bì in. Không có lời, không có tiếng thiết kế.

Chưa nên làm phim dài. Nếu hướng hình được duyệt, bước sau là thêm tư thế với tay chạm hộp, rồi mới xét cài công cụ rig. Việc cài Blender hoặc công cụ vector cần được duyệt trước.
