# POC 002 — bản dựng 15 giây

Ngày: 2026-10-22.

## Kết luận

`NEEDS REVIEW`

File đã tạo: `proof/novixa-video-poc-002/job/output.mp4`. Có hình dọc, có lời, có cắt cảnh, có dashboard thật. Chưa đạt mức hoạt hình. Chuyển động là máy quay giả lập trên ảnh tĩnh. Không có cử động người: bút không viết, mắt không chớp.

## File và công cụ

- Tạo: `speak.py`, `compose.py`, `assets/character-ref.jpg`, `assets/scene1-closed.jpg`, `assets/scene2-ledger.jpg`, `assets/scene3-shelf.jpg`, `job/output.mp4`, `job/voice.wav`, `job/frames/`.
- Sửa thêm mục vào `POC-002-REPORT.md` và `STYLIZED-PIPELINE-REPORT.md`. Không xóa các mục kiểm kê cũ.
- Không sửa POC 001, `MarketingLocalVideoRenderer.cs`, `ContentVideoService.cs`. Không commit, không deploy.
- Ảnh nhân vật: công cụ tạo ảnh của Cursor, 5 lần (ảnh mẫu, một cảnh cửa còn sáng bị loại, cảnh quầy, cảnh sổ, cảnh kệ). Không gọi ElevenLabs, Pollinations, Creatomate, Runway hay Fal.
- Tiếng: edge-tts, giọng `vi-VN-NamMinhNeural`, không key.
- Ghép: ffmpeg 8.1.2. Renderer C# không được gọi vì nó chỉ giữ khung, không tạo máy quay.
- Chi phí hóa đơn riêng: không có. Lượt tạo ảnh nằm trong công cụ Cursor; máy này không đo được quota.

## Nguồn hình

| Cảnh | File | Cách có |
|---|---|---|
| 1. Sau quầy | `assets/scene1-closed.jpg` | Ảnh 3D stylized, 720×1280 |
| 2. Sổ | `assets/scene2-ledger.jpg` | Cùng nhân vật đưa vào làm mẫu |
| 3. Kệ, hạn 08/2026 | `assets/scene3-shelf.jpg` | Cùng nhân vật đưa vào làm mẫu |
| 4. Sản phẩm | crop từ `docs/novixa/launch/DOC-008/assets/screenshots/01-dashboard.png` | Ảnh chụp thật, không vẽ lại, không sửa số |
| Kết | `docs/novixa/assets/brand/novixa-logo.png` | Logo trong repo, nền trắng được tách |

Dashboard trên hình là thẻ **Doanh thu hôm nay** `60.058.235,08 đ` và hàng **Cần chú ý** (sắp hết HSD 8, SP tồn thấp 336, lô tồn thấp 28), kèm `DEMO_PHARMACY` và dòng «Giao diện thật · dữ liệu demo».

## Lời và nhịp

1. 0,40–3,27 s: «Đóng cửa rồi. Hôm nay bán cũng đều.»
2. 4,17–6,87 s: «Nhưng doanh thu không nói hộ hàng nào còn nằm trong kho,»
3. 7,32–8,66 s: «lô nào sắp hết hạn.»
4. 9,21–11,98 s: «Muốn hết thấp thỏm, phải nhìn được những số đó.»

Không có câu về lợi nhuận.

## Chuyển động

Tất cả là giả lập. Không có hoạt hình thật.

- Cảnh 1: zoom rất chậm vào ảnh đứng.
- Cảnh 2: khung hình trượt từ mặt xuống sổ. Đây không phải zoom.
- Cảnh 3: khung hình nhích từ hộp thuốc lên mặt.
- Cảnh 4: cắt cứng sang dashboard, giữ yên, cắt sang logo lúc 13,70 s.

## Kiểm tra kỹ thuật

- 1080×1920, H.264 High, 30 fps, 15,000 giây.
- AAC LC, 48 kHz, stereo. Đỉnh −4,3 dB, trung bình −21,7 dB.
- Dung lượng 2.096.843 byte.
- SHA-256: `882d34838f100e96f878ae2d26c5c7362154d93d2abb2aa0692b6911557ca9d7`
- Khoảng lặng từ −40 dB: 0,72 s sau câu đầu, 0,96 s trước câu sổ, 0,51 s và 0,61 s giữa các vế, rồi 3,09 s từ 11,93 s đến hết (đọc dashboard và logo, không có lời).

## Việc người xem cần xét

- Cảnh 1 không thấy cửa cuốn. «Đóng cửa» chỉ có trong lời và trong cảnh quầy vắng.
- Cảnh sổ để tóc xõa. Cảnh quầy và cảnh kệ là búi thấp. Áo blouse và áo xanh lá thì giống nhau.
- Tay trên sổ và tay cầm hộp nhìn ổn trong khung đã rút, nhưng cần mắt người đếm ngón.
- Ảnh nguồn 720×1280 phóng lên 1080×1920, cảnh 2 và 3 còn crop thêm nên mềm.
- 3 giây cuối không có tiếng nói.
- Giọng là giọng máy, chậm, không phải người thu.
