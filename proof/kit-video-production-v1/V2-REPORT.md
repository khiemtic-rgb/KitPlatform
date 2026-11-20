# KIT Video Production V2

Hai video dài 180 giây đã dựng xong. Thư viện ảnh thật chưa đủ 200 tài nguyên vì repo không có ảnh nhà thuốc, bao bì, người hoặc bàn giao dịch kèm giấy phép. Không nhân bản ảnh và không gọi API ảnh trả phí.

## 1. Vì sao video 20 giây trước bị lặp

Bốn cảnh đều là bảng vẽ bằng code (logo, lane, cột, ba cột). Shot plan không trỏ tới ảnh khác nhau. Không có bước chọn ảnh theo câu nói, cũng không có bước nhìn cả timeline. Thiếu ảnh, thiếu metadata và thiếu bộ chọn là cùng một nguyên nhân.

## 2. Cái được dùng lại, cái thêm

Dùng lại pipeline `kv.py`: manifest, shot plan, `designed_board`, `brand_reveal`, camera, font Segoe, edge-tts tốc độ +0%, QA và FFmpeg. Logo Novixa giữ nguyên byte. Ảnh chụp giao diện trong `docs/novixa` được đưa vào catalog, không sửa file.

Thêm `kit_video/library.py`, `kit_video/chart.py`, template `annotated_plate` và `market_chart`. Catalog, file nến và hai dự án `novixa-long`, `trading-long` nằm trong `proof/kit-video-production-v1`.

## 3. Số tài nguyên độc lập

| Nhóm | Đã duyệt, không phải bản dẫn xuất | Mục tiêu |
|---|---:|---:|
| NOVIXA | 21 (20 ảnh giao diện + 1 logo) | 80 |
| Agent Trading | 0 | 80 |
| Shared | 0 | 40 |
| Tổng | 21 | 200 |

Hai ảnh gần giống bị loại khỏi số đếm: `nvx-qld-stock`, `nvx-qld-sync`. Hai file bị từ chối, không dùng: thẻ thuốc phẳng và cảnh nhà thuốc hoạt hình. Biểu đồ không được tính vào 21.

Cách bổ sung: chụp hoặc mua ảnh có giấy phép cho không gian nhà thuốc, bao bì minh họa không mang tên thuốc thật, người làm việc, và bàn giao dịch. Chưa mua gì trong đợt này.

## 4. Cách chọn hình

Script được tách bằng `propose_shot_plan`. Mỗi cảnh ảnh có từ khóa. `choose_asset` chỉ lấy ảnh đúng thương hiệu, đúng từ khóa, `approved`, chưa dùng trong video đó, và không đứng cạnh ảnh cùng nhóm hoặc hash gần. Không có ảnh thì trả `gap`.

Cảnh hộp thuốc của Novixa là gap. Hình trên video ghi "Chưa có ảnh hộp thuốc" thay vì gắn một màn hình khác. Cảnh dược sĩ và bàn giao dịch cũng là gap trong `selection-report.json`, không bị lấp bằng ảnh Novixa.

## 5. Chart engine

Nến giờ BTCUSDT và ETHUSDT, 1000 nến mỗi mã, tải từ `data-api.binance.vision` `/api/v3/klines`, lưu tại `library/market/`. Nguồn `binance-public-klines`, chế độ `historical`. Khoảng dữ liệu 2026-09-17 12:00 UTC đến 2026-10-29 03:00 UTC. Kiểm tra OHLCV: 0 lỗi trên cả hai file.

Trước khi vẽ, high/low phải bao open/close, high >= low, volume không âm, timestamp tăng. SMA 20, RSI 14 và MACD tính từ giá đóng cửa của đúng cửa sổ. Khối lượng là volume của các nến đó. Không có nến bịa và không có lợi nhuận chiến lược.

| Cảnh | Dữ liệu | Cửa sổ | Việc giải thích |
|---|---|---|---|
| s2-up | BTCUSDT 1h | index 70, 48 nến, +7,4% | đoạn tăng và SMA 20 |
| s3-pull | cùng đoạn BTC | 32 nến quanh nến 42, hồi 2,6% | nhịp hồi trong đoạn tăng |
| s4-down | BTCUSDT 1h | index 438, 48 nến cộng 12 nến pan, −3,5% trên cửa sổ đang vẽ | mức giảm của giá |
| s5-range | ETHUSDT 1h | index 519, 48 nến, đóng cửa +0,7% | biên hẹp và RSI 14 |
| s6-macd | BTCUSDT 1h | index 144, không trùng hai cửa sổ trên | MACD âm ở cuối cửa sổ |

## 6. File bàn giao

Novixa:

- `proof/kit-video-production-v1/outputs/novixa-long/20261029T032551Z-cut/video.mp4`
- `proof/kit-video-production-v1/outputs/novixa-long/20261029T032551Z-cut/timeline-contact-sheet.png`
- Khung đầy đủ: thư mục `frames/` trong cùng cut
- `proof/kit-video-production-v1/projects/novixa-long/manifest.json`
- `proof/kit-video-production-v1/projects/novixa-long/shot-plan.json`

Agent Trading:

- `proof/kit-video-production-v1/outputs/trading-long/20261029T033225Z-cut/video.mp4`
- `proof/kit-video-production-v1/outputs/trading-long/20261029T033225Z-cut/timeline-contact-sheet.png`
- Khung đầy đủ: thư mục `frames/` trong cùng cut
- `proof/kit-video-production-v1/projects/trading-long/manifest.json`
- `proof/kit-video-production-v1/projects/trading-long/shot-plan.json`

Dùng chung: `library/catalog.json`, `library/selection-report.json`, `library/README.md`, `library/market/BTCUSDT-1h.json`, `library/market/ETHUSDT-1h.json`.

## 7. Đã xem gì

Đã xem contact sheet mỗi 8 giây và các khung đầy đủ của logo, tổng quan, tồn kho, đoạn tăng, nhịp hồi, đoạn giảm, biên ETH và MACD. Cả hai file dài 180,000 giây, 1280×720, 24 fps, H.264 và AAC. QA kỹ thuật PASS sau khi khai các khoảng giữ hình. Chưa nghe giọng đọc. Track vẫn `proposed`.

Đã sửa trước khi encode: chú thích đè lên số trên giao diện; tiêu đề "tăng" đặt trên một đoạn nến đang giảm; cửa sổ "đi ngang" bị pan sang một nhịp tăng.

Còn lại khi xem:

- Các màn Novixa chung một chrome phần mềm. Nội dung từng màn khác nhau, không phải một file lặp.
- Câu thoại ngắn hơn cảnh. Hình giữ sau khi câu hết. Nhịp chưa chặt như phim thương mại.
- Nhịp hồi BTC chỉ khoảng 2,6% ở vùng đỉnh, đúng dữ liệu, không phải một cú hồi sâu.
- ETH biên hẹp vẫn nhích khoảng 0,7% vì trục giá bám sát vùng đó.
- Không có ảnh hộp thuốc, dược sĩ, khách hay bàn giao dịch.

## 8. Chưa xong

200 ảnh độc lập chưa đạt. Agent Trading và Shared chưa có ảnh tĩnh. Hộp thuốc và người thật không được thay bằng khối vẽ. Muốn đủ thư mục cần ảnh có giấy phép, không phải thêm code.
