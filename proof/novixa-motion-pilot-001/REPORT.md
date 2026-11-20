# NOVIXA-MOTION-PILOT-001

Trạng thái: **NEEDS REVISION**

Không READY FOR PUBLISHING. Mẫu kỹ thuật đã dựng xong. Hai tiêu chí nghe giọng chưa đạt vì môi trường này không nghe được file.

## File đầu vào đã dùng

- `docs/novixa/assets/brand/novixa-logo.png`
- `docs/novixa/launch/DOC-008/assets/screenshots/04-inventory-stock.png`, crop `(540, 580, 1500, 1320)` — đủ cột Mã SP và Tên SP, không có số tiền
- Giọng `vi-VN-NamMinhNeural` qua `edge-tts` sẵn có. Bản trong phim là `assets/voice/full-48.mp3`, rate `-48%`, cắt `0.30–15.45` rồi `atempo=1.055`
- Font `segoeuib.ttf` và `segoeui.ttf`
- Đã đọc `proof/novixa-marketing-v1/shot-list.md`, `proof/novixa-marketing-v2/script.md`, `proof/novixa-marketing-v2/qa-report.md`

Đã mở `08-reports-home.png` rồi không dùng: vùng nhìn thử bị cắt chữ ở cạnh phải.

Lời đọc đúng câu đã chốt. Không thêm NOVIXA vào lời. Không thêm tuyên bố hiệu quả hay tiết kiệm.

## File đầu ra

| Hạng mục | Giá trị |
| --- | --- |
| File | `proof/novixa-motion-pilot-001/final/novixa-motion-pilot-001.mp4` |
| Bytes | 1,094,353 |
| SHA-256 | `d0d851eeb476690efeee13158c4a1bc78f949ae82060e000a8a499125a762191` |
| Duration | 15.000s |
| Hình | h264, 1080×1920 |
| Tiếng | aac, 48 kHz, stereo |
| Decode | `ffmpeg -f null` exit 0 |

Dựng bằng `render.py`: từng frame Pillow, không zoom một ảnh tĩnh. Logo chỉ vào từ giây 11.

## Kiểm tra kỹ thuật

`silencedetect` −40 dB, đoạn im ≥ 0.35s:

| Khoảng | Việc đo được |
| --- | --- |
| 1.37–1.89s | nghỉ sau “Doanh thu cao…” |
| 5.40–7.22s | nghỉ 1.82s giữa hai câu |
| 8.06–8.56s, 9.55–10.15s, 10.99–11.55s | nghỉ giữa Hàng tồn, giá vốn, chi phí |
| 14.15–15.02s | 0.87s sau khi mức lời đọc đã xuống |

## Xem và nghe

Đã đọc frame ở 0.4s, 0.8s, 1.8s, 2.4s, 4.4s, 6.3s, 8.8s, 12.6s, 14.4s.

- 0.4s: chỉ “Doanh thu”, chưa có logo.
- 2.4s: “Doanh thu / cao / chưa chắc đã kinh doanh hiệu quả”.
- 4.4s: HÀNG TỒN và GIÁ VỐN đã vào; CHI PHÍ còn đang đi lên.
- 6.3s: ba lớp đã xếp.
- 8.8s: bảng tồn kho thật, nhãn “Dữ liệu minh họa”, đủ sáu dòng mã và tên, khung không cắt giữa hàng.
- 12.6s và 14.4s: logo, “Nền tảng vận hành nhà thuốc thông minh”, `novixa.vn`.

visual QA chưa hoàn tất theo nghĩa xem liên tục. Chưa mở trên điện thoại thật.

audio QA chưa hoàn tất. Không nghe được file. `novixa-boundaries.txt` ghi token `WordBoundary:NOVIXA` — engine không tách thành sáu chữ cái. Đó không phải cách đọc đã được tai xác nhận. Lời trong phim không chứa từ NOVIXA.

## Tiêu chí

| # | Tiêu chí | Kết quả |
| --- | --- | --- |
| 1 | 1080×1920, 14.5–15.5s | Đạt |
| 2 | Giải mã toàn bộ | Đạt |
| 3 | Chuyển động chữ và đồ họa | Đạt trên các frame đã đọc; chưa xem playback liên tục |
| 4 | UI NOVIXA thật, đọc được trên khung dọc | Đạt trên frame 8.8s; chưa thử máy thật |
| 5 | Giọng tự nhiên, NOVIXA đã nghe và thống nhất | Chưa đạt |
| 6 | Không lỗi chính tả, không dữ liệu gây hiểu nhầm, không im ngoài chủ đích | Chính tả và nhãn demo đạt trên frame đã đọc. Nhịp giọng và âm cụt chưa nghe được. Nghỉ 1.82s giữa hai câu cần người nghe quyết định |
| 7 | Báo cáo và SHA-256 | Đạt |
| 8 | Giữ nguyên tài sản gốc | Đạt |

## Tài sản gốc

| File | Bytes | SHA-256 |
| --- | --- | --- |
| V1 | 11,400,707 | `6a2701e85a3891f4f93b69cc05f208b3a976d167b830bad899a9d7027c83cb06` |
| V2 | 4,153,526 | `b67897d2ae5e29423da52549441168d7bfe0af074eb39c407c23e8cfb7e21cab` |
| E2E | 575,741 | `054397f90a6ac3f2660b1e7a3bc71bf7e18b3d031c25b921f5d326362b176f1e` |
| Famixa keyframe | 113,493 | `9fe2bd76034203a14531631fd1911e3ef15f1249c588259a17c692c952305d57` |

## Cần quyết định trước video dài

- Một người nghe `full-48.mp3` và `probe-novixa.mp3`, rồi chốt rate và cách đọc NOVIXA.
- Giữ hay rút nhịp nghỉ 1.82s giữa hai câu.
- Bảng tồn kho demo có nhãn “Dữ liệu minh họa” có được dùng trong phim dài hay không. Mẫu này không đưa số doanh thu.
- Chưa có ảnh nhà thuốc thật có quyền sử dụng, và chưa có công cụ animation nhân vật. Mẫu này cố ý không tạo nhân vật 3D.
