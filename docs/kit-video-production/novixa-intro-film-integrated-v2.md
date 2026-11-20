# NOVIXA Intro — bản dựng tích hợp V2

Bản để xem:

`proof/kit-video-production-v1/outputs/novixa-intro-film/20261023T094110Z-integrated-v2/integrated.mp4`

Đây là bản xem đủ 12 cảnh, không phải phim cuối. `finalVideo` vẫn null. `durationLocked` vẫn false.

## Xem được đến đâu

Đủ 12 chương, đúng thứ tự kịch bản, dài theo lời V3. Tiếng AAC 48 kHz mono dài 198,145 giây, khớp tổng 12 WAV. Đối chiếu với WAV ghép mẫu cho tương quan 0,99999. Hình H.264, 1280×720, `yuv420p`, 30 fps, 5945 khung. Dòng hình dài hơn tiếng 0,022 giây ở đuôi, chưa tới một khung. Không nhạc. Không dùng ảnh DOC-008.

Khổ 1280×720 chỉ là khổ bản xem. Thời lượng phim chưa khóa.

| Cảnh | Trong bản dựng | Trạng thái |
| --- | --- | --- |
| S01, S02 | Thẻ “CHƯA DỰNG”, chữ của kịch bản | Chờ footage |
| S03 | Hộp không nhãn, két không số, lịch không ngày | Motion graphics |
| S04 | Lịch trắng, ba nhóm hộp, vòng quét | Motion graphics |
| S05 | Logo gốc và khung thiết bị trống | Chờ duyệt POS |
| S06, S07 | Thẻ chờ, không có số liệu | Chờ duyệt giao diện |
| S08 | Ba nhãn nối lại | Sơ đồ xong, chờ màn báo cáo |
| S09, S10 | Thẻ chờ | Chờ footage người và toàn cảnh |
| S11 | Logo gốc và đường nối | Motion graphics |
| S12 | Logo, câu định vị, `novixa.vn` | Motion graphics, chưa khóa giây giữ cuối |

Nguồn tạo lại là `outputs/novixa-intro-film/_build_integrated_v2.py`. Mốc cảnh và đường WAV nằm trong `build-record.json` cùng thư mục với file xem.

## Đã làm

S03, S04, S08 và S11 có chuyển động vẽ bằng code, không phải phóng một ảnh đứng. S05 và S12 dùng đúng file logo, không vẽ lại. S06 và S07 không nhận số tồn, số tiền, lô hay hạn. Các cảnh thiếu asset mang nhãn “CHƯA DỰNG” suốt cảnh.

## Còn chặn phim cuối

S01, S02, S09 và S10 chưa có footage chuyển động. S05, S06 và S07 chưa được phép dùng ảnh giao diện. S07 vẫn chưa có hình nào chứng minh hạn dùng và giá trị tồn. S08 chưa có màn báo cáo đã mở. Chưa có nhạc được cấp quyền.

## Bước tiếp theo

Quyết định dùng hay không dùng ba ảnh DOC-008. Nếu được dùng, phải kèm nhãn “Dữ liệu minh họa”, và ảnh tồn kho không được đứng cho hạn dùng hay giá trị. Quyết định đó mở được phần giữa phim bằng asset đang có, mà không gọi dịch vụ trả phí.

Bốn cảnh footage vẫn cần một nguồn chuyển động đã duyệt trước khi tính đến phim cuối.
