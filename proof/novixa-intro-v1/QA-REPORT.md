# NOVIXA INTRO V1 — Báo cáo sản xuất

Trạng thái: **PRODUCTION COMPLETE — QA BLOCKED**

Hai file đã xuất. Giải mã hết, đúng khung hình và đúng 53 giây. Chưa xem liên tục từ đầu đến cuối và chưa nghe âm thanh, nên chưa xác nhận chất lượng hình hoặc tiếng.

## 1. File xuất

| Bản | Đường dẫn | Khung | Thời lượng | Dung lượng |
| --- | --- | --- | --- | ---: |
| 9:16 | `proof/novixa-intro-v1/final/novixa-intro-v1-9x16.mp4` | 1080×1920, 30 fps | 53,000 giây | 2.134.593 byte |
| 4:5 | `proof/novixa-intro-v1/final/novixa-intro-v1-4x5.mp4` | 1080×1350, 30 fps | 53,000 giây | 1.909.800 byte |

Cả hai: H.264 High, AAC 48 kHz stereo, 192 kbps. Hai bố cục dựng riêng. Bản 4:5 không phải là crop của bản 9:16.

SHA-256:

- 9:16 `0FE50B0DD38249728C43060CFE82756FB3E05D4E5D81DE5BA31CEC774968830C`
- 4:5 `50B7D3F88FB4B0188226B3FA9C5C4F2DC2CD33B9231CEC30FBEA8AFC33C3DB99`

## 2. Giọng

Giọng `vi-VN-NamMinhNeural`, tốc độ `+0%`, cao độ `+0Hz`, âm lượng `+0%`. Không nhạc.

Tên thương hiệu đưa vào máy đọc là `Nôvixa`, một token. Trên hình và trên logo vẫn là `NOVIXA`.

Chuỗi `nôvixa.vn` bị máy tách thành `nô` và `vixa.vn`. Câu kêu gọi vì vậy đọc `Nôvixa chấm vn`. Chữ trên hình vẫn là `novixa.vn`.

Ba lần `Nôvixa` trong lời đọc đều là một token, không tách âm tiết. Lần đầu cảnh 6 có khoảng 0,16 giây trước từ “nền” vì câu kịch bản có gạch ngang.

## 3. Kiểm thử

| Hạng mục | Kết quả |
| --- | --- |
| Độ phân giải, tỷ lệ, thời lượng | Đạt trên cả hai file |
| Giải mã từ đầu đến cuối | Đạt. `ffmpeg` trả về 0, không báo lỗi |
| Im lặng | Có các khoảng im đã tính. Không có khoảng trống giữa các từ trong một câu ngoài dấu câu |
| Cách đọc NOVIXA | Token đúng mẫu đã duyệt. Chưa nghe |
| Chữ và logo trong vùng an toàn | Các khung đã xem nằm trong vùng an toàn. Riêng ba thẻ cảnh 2 của bản 4:5 khi tách ra nằm sát mép hơn mức 64 px, chữ vẫn đủ |
| Nhãn “Dữ liệu minh họa” | Có trên pill và trên từng bảng trong suốt cảnh 4 và cảnh 5 |
| Giữ logo cuối | Lời hết lúc 47,5 giây. Logo, tagline và `novixa.vn` còn đến 53,0 giây |
| SHA-256 | Đã ghi ở mục 1 |

Khoảng im đã tính, tính từ đầu file:

- 3,11–5,25 giây: hết câu cảnh 1, sang câu cảnh 2
- 10,09–12,30 giây: hết câu cảnh 2
- 15,50–16,50 giây: giữa hai câu cảnh 3
- 19,01–20,26 giây: hết câu cảnh 3
- 27,55–31,30 giây: hết câu cảnh 4, hình sản phẩm còn
- 37,96–41,27 giây: hết câu cảnh 5
- 43,97–45,01 giây: giữa tagline và câu kêu gọi
- 47,48–53,01 giây: giữ logo

## 4. Đã xem và chưa xem

Đã xem từng khung ở 0,5 / 1,1 / 2,4 / 6,4 / 8,6 / 11,4 / 13,2 / 16,6 / 18,4 / 21,2 / 25,0 / 27,2 / 29,8 / 33,2 / 38,2 / 42,2 / 46,5 / 52,4 giây của cả hai bố cục. Hai khung lấy từ chính file MP4 đã xuất, giây 29,8 của bản 9:16 và giây 52,4 của bản 4:5, khớp bản dựng.

Chưa xem liên tục 53 giây như một phim. Chưa nghe. Chưa xem trên điện thoại thật.

## 5. Việc còn lại

1. Nghe bản 9:16, nhất là “Nôvixa” ở cảnh 4 và cả câu cảnh 6. Nếu “chấm vn” hoặc nhịp gạch ngang chưa đạt, dừng và đọc lại trước khi sửa hình.
2. Ảnh bán hàng, tồn kho và báo cáo mỗi ảnh giữ khoảng 2,2 giây. Lời liệt kê nhanh hơn, nên từ “tồn kho” và “theo dõi” được nói hơi trước khi ảnh tương ứng chiếm khung.
3. Không thêm nhạc, số điện thoại, Zalo, hay kết quả kinh doanh.

V1, V2, shot E2E, pilot motion, file nghe mẫu, logo và hai hồ sơ khóa không bị ghi đè.
