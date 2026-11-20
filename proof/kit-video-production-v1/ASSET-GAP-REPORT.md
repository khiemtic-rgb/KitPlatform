# Khoảng trống tài nguyên và nhịp cảnh

Hai video 180 giây không được dựng lại. Bản này thêm ảnh có giấy phép, một cách ghép giao diện với ảnh thật, và hai preview ngắn. Thư viện chưa đạt 200 tài nguyên.

## Tài nguyên mới

Năm ảnh được duyệt, đều từ Wikimedia Commons, file gốc nằm trong `library/stills/`. Không mua asset và không gọi API ảnh trả phí.

| assetId | Nội dung | Tác giả | Giấy phép |
|---|---|---|---|
| `ref-blister-foil` | Ba vỉ thuốc, không tên thương hiệu lớn | Tarasna0922 | CC BY-SA 4.0 |
| `ref-pack-still` | Vỉ, chai và lọ trong ảnh studio | BuhaM | CC BY-SA 4.0 |
| `ref-pharmacy-floor` | Tủ kính của một nhà thuốc bán lẻ | Mike1979 Russia | CC BY-SA 3.0 |
| `ref-hands-blister` | Hai tay đang kiểm một vỉ | Shixart1985 | CC BY 2.0 |
| `ref-pharmacy-wait` | Phòng chờ, quầy thuốc ở phía sau | YipshaiukOWL | CC BY-SA 4.0 |

Trang nguồn ghi trong `catalog.json`, trường `sourcePage`. Ảnh CC BY-SA cần ghi công. Nếu xuất bản một video cắt từ các ảnh đó, điều khoản share-alike có thể áp lên phần phái sinh.

Đã xem và không duyệt:

- Vỉ có tên thuốc thật (`ref-branded-blisters`).
- Quầy có nhãn mật khẩu trên màn hình (`ref-counter-credential`).
- Tờ rơi nhà thuốc (`ref-pharmacy-brochure`).
- Hai màn hình tắt trong phòng máy (`ref-gaming-monitors`). Không dùng làm bàn giao dịch.

Số độc lập sau catalog: NOVIXA 26 (21 ảnh cũ + 5 ảnh này), Agent Trading 0, Shared 0. Mục tiêu 80 / 80 / 40 vẫn chưa đạt.

## Nhóm vẫn thiếu

- Hộp thuốc bán lẻ đủ các mặt, chai và vỉ mang nhãn riêng của phim.
- Kho, cửa nhận hàng, phòng tư vấn.
- Dược sĩ và khách tại quầy. Ảnh đôi tay không phải dược sĩ.
- Bàn giao dịch, màn hình đang có biểu đồ, thiết bị phân tích.

Cảnh thiếu hộp thuốc trong video 180 giây của Novixa vẫn là thẻ chữ. Không thay bằng ảnh giao diện.

## Đoạn đã giảm lặp

Đánh giá cả timeline, không chỉ file trùng. Video trading dài giữ một biểu đồ hoặc một infographic từ 16 đến 26 giây ở `s2-up`, `s3-pull`, `s4-down`, `s5-range`, `s6-macd`, `s7-agent`, `s8-risk`, `s9-close`. Video Novixa dài có sáu cảnh giao diện liên tiếp từ `s8-grn` đến `s13-link`; chủ đề từng màn khác nhau nên không bị thay ảnh.

Bản proof không còn cảnh giữ im 16 giây. Bốn cảnh Novixa đầu cùng kiểu khung có chú thích, nhưng ảnh lần lượt là vỉ, đôi tay, nhà thuốc và phần mềm. Cảnh ghép dùng lại màn tồn và ảnh vỉ vì câu nói đang đối chiếu hai nguồn đó. Bốn nhịp biểu đồ cùng khung nến, nhưng cửa sổ, vùng tô và đường trung bình đổi theo dữ liệu.

## Biểu đồ

File `market/BTCUSDT-1h.json` không đổi: 1000 nến, 0 lỗi OHLCV, nguồn `binance-public-klines`, khung 1 giờ, `historical`. Đoạn proof là nến 70 đến 117, từ 2026-09-20 10:00 UTC đến 2026-09-22 09:00 UTC, đóng cửa tăng 7,4%.

SMA 20 là trung bình 20 giá đóng cửa. Ở nến 70, giá trị là 80915,0065, gồm 19 nến ngay trước đoạn vẽ. RSI vẫn là Wilder 14. MACD vẫn là EMA 12/26 và signal 9. Bản proof không vẽ RSI hay MACD. Tham số `warmup` mặc định bằng 0, nên video dài nếu dựng lại vẫn ra cùng một hình. Nhịp SMA của proof truyền `warmup` 19 để đường trung bình dùng đủ nến trước đó.

Nến được tô là nến tăng mạnh nhất trong đoạn, thân từ giá mở lên giá đóng. Vùng 16 nến nằm trong đoạn tăng, không phải một cửa sổ khác.

## Preview

Trước, contact sheet của hai video dài:

- `outputs/novixa-long/20261029T032551Z-cut/timeline-contact-sheet.png`
- `outputs/trading-long/20261029T033225Z-cut/timeline-contact-sheet.png`

Sau, mỗi cảnh một khung, 25 giây, 1280×720:

- `outputs/novixa-asset-proof/20261029T040842Z-asset-proof/video.mp4`
- `outputs/novixa-asset-proof/20261029T040842Z-asset-proof/timeline-contact-sheet.png`
- `outputs/trading-beat-proof/20261029T040657Z-beat-proof/video.mp4`
- `outputs/trading-beat-proof/20261029T040657Z-beat-proof/timeline-contact-sheet.png`

Đã xem từng khung: vỉ và nhà thuốc là ảnh chụp, chú thích nằm dưới hình, màn tồn không bị chú thích đè lên số. Cảnh ghép ghi rõ hai nguồn. Biểu đồ rộng không có đường trung bình. Vùng thu hẹp đúng nến tăng cao. Nhịp sau mới có đường SMA. Câu kết ghi 7,4% và “Không phải khuyến nghị.” Hai preview chưa có lời đọc.

## Giới hạn và bước tiếp

Chưa dựng lại hai video dài. Chưa có ảnh dược sĩ tại quầy và chưa có ảnh bàn giao dịch đủ điều kiện. Ảnh studio `ref-pack-still` có trong thư viện nhưng chưa vào preview, vì preview đã có một vỉ rõ hơn. Bước tiếp là bổ sung ảnh kho, tư vấn và bàn giao dịch còn thiếu, rồi mới tách các cảnh 16–26 giây của video dài theo nhịp đã xem ở bản proof.
