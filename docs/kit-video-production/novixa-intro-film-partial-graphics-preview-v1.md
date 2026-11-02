# NOVIXA Intro — preview đồ họa một phần V1

Đây là bản xem năm cảnh đã có tấm hình và lời. Không phải phim hoàn chỉnh. `finalVideo` vẫn null. `durationLocked` vẫn false. Khổ 1280×720 chỉ thuộc bản xem này.

File để xem:

`proof/kit-video-production-v1/outputs/novixa-intro-film/20261023T091740Z-partial-graphics-preview/preview.mp4`

## Nội dung

Thứ tự đúng S03, S04, S08, S11, S12. Không có S01, S02, S05, S06, S07, S09, S10. Không nhạc. Không ảnh giao diện DOC-008. Mỗi cảnh là ảnh đứng đã có, không phải footage chuyển động.

| Cảnh | Ảnh nguồn | WAV V3 | Thời lượng file |
| --- | --- | --- | ---: |
| s03-stock-cash | `20261023T070441Z-graphics/s03-caption.png` | `s03-stock-cash.wav` | 14,784 |
| s04-expiry | `20261023T070441Z-graphics/s04-caption.png` | `s04-expiry.wav` | 13,417 |
| s08-connected-data | `20261023T070441Z-graphics/s08-flow.png` | `s08-connected-data.wav` | 16,824 |
| s11-positioning | `20261023T063730Z-logo-plates/s05-s11-logo.png` | `s11-positioning.wav` | 15,912 |
| s12-cta | `20261023T063730Z-logo-plates/s12-cta.png` | `s12-cta.wav` | 19,872 |

Năm file WAV cộng lại 80,809 giây. Không thêm khoảng giữ sau lời.

## Kiểm tra

`ffprobe` trên `preview.mp4`:

- Hình H.264 High, 1280×720, `yuv420p`, 30/1 fps, 2424 khung, bắt đầu ở 0.
- Tiếng AAC, 48 kHz, mono, một luồng, dài 80,809 giây, bắt đầu ở 0.
- Không có luồng nhạc.

Giải mã tiếng của `preview.mp4` và so với năm WAV ghép đúng thứ tự: lệch 0 giây, tương quan 0,99999. Lời bắt đầu ở 0,258 giây, nên không bị cắt đầu. Dòng hình dài 80,800 giây, ngắn hơn tiếng 0,009 giây ở phần đuôi im của S12. Phần đuôi đó vốn có trong WAV, không phải khoảng giữ mới.

Từng clip `s03` đến `s12` trong cùng thư mục có thời lượng container bằng WAV tương ứng. Clip S03 đối chiếu riêng với WAV nguồn cũng lệch 0 và tương quan 0,99999.

Logo và `script.md` không bị sửa. Các bản đọc và tấm hình cũ không bị ghi đè.

## Chưa xong

Bảy cảnh còn lại không có trong bản xem. S05 vẫn thiếu khung POS. S06, S07 và phần báo cáo của S08 chưa được phép dùng ảnh giao diện. S01, S02, S09, S10 chưa có footage chuyển động. 12 cảnh vẫn ở trạng thái đề xuất. Thời lượng phim chưa khóa.
