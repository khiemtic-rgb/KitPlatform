# Thư viện cảnh video KIT: hiện trạng và quy tắc kiểm tra

Cập nhật: 01/11/2026. Đọc file này trước khi nói "chưa có" hoặc làm lại bất cứ thứ gì về hình ảnh video.
Lần trước đã có lỗi: kết luận "chưa có biểu tượng, nến" trong khi thư viện đã có ở `tools/kit-video-library`. Nguyên nhân: chỉ tìm trong `kit_video` (engine), không tìm ở các thư mục bên dưới.

## 1. Các nơi cần kiểm tra trước khi kết luận

| Nơi | Chứa gì |
|---|---|
| `tools/kit-video-production/kit_video/` | Engine dựng (Pillow), các template cảnh, kiểm tra chất lượng, `chart.py` (nến thật từ file OHLCV) |
| `tools/kit-video-library/novixa-series/src/illus.py` | Đồ vật vẽ bằng mã, nền xanh navy: tiền, ví, khóa, dấu tích/cảnh báo, đồng hồ, lịch, phone, hóa đơn, nến, BTC, vàng, ngoại tệ, cổ phiếu, biểu đồ, đường vốn, đồng hồ đo rủi ro |
| `tools/kit-video-library/trading_props.py`, `novixa_props.py` | Hình giải thích cho Agent Trading: biểu đồ nến chú thích SMA, giải phẫu cây nến, sơ đồ 5 bước, khiên "Cổng rủi ro" |
| `proof/kit-video-library/` | Ảnh đã dựng của các lần chạy (ví dụ `20261029T094355Z-agent-trading-explainers`) |
| `proof/kit-video-production-v1/library/` | `catalog.json` (sổ tài nguyên đã duyệt), ảnh tham chiếu CC BY-SA, `market/BTCUSDT-1h.json`, `ETHUSDT-1h.json` |
| `tools/kit-video-library/novixa-series/src/studio_*.py` | Bố cục kiểu Studio của loạt Novixa |
| Giao diện Studio (bố cục `place/data/icon/interface`) | Chưa tìm thấy mã trong repo. Cần xác định trước khi sửa 4 lỗi hình đã thấy |

## 2. Đã có và đã kiểm (01/11/2026)

### Mẫu cảnh kiến thức mới (đã đăng ký trong `templates.py`)
`edu_title`, `edu_contrast`, `edu_steps`, `edu_bignumber`, `edu_rules`, `edu_flow`, `edu_chart`
- Mã: `kit_video/edu_scenes.py`. Chạy được ở khổ dọc 1080x1920 và ngang 1920x1080, nền tối và sáng.
- Chữ, số, màu đều lấy từ kế hoạch cảnh và bảng màu của kênh. Không có mã riêng cho từng cảnh.
- Khung tự co theo chữ. Quá dài thì bỏ ý phụ (gạch đầu dòng, hệ quả cuối) trước. Dưới mức tối thiểu thì báo lỗi vào `ctx.violations`, không vẽ chữ tràn.
- Kiểm tra: `tests/test_edu_scenes.py` (7 mẫu x 2 khổ, không có lỗi). Đã xem bằng mắt ảnh xem thử cả hai khổ.

### Biểu tượng vẽ bằng mã, theo bảng màu kênh
`kit_video/edu_props.py`, 18 cái: coin, coins, wallet, wallet_empty, lock, shield, check, cross, warn, clock, scale, flame, line_down, line_up, candles, candles_down, gauge, phone.
- Dùng trong mẫu cảnh qua tham số `icon` (tiêu đề, hai bên, bước, số liệu, bể tiền) và `icons` (danh sách quy tắc).
- Giấy phép: tự vẽ, không dùng file ngoài.

### Biểu đồ nến
- `edu_chart`: nến giả lập có hạt giống, luôn ghi "Dữ liệu minh họa, không phải giá thị trường". Có dòng mức lỗ trung tính do kịch bản đặt tên.
- Dữ liệu thật: tham số `dataFile` + `start` + `source`. Đọc bằng `chart.load_candles`. Không ghi `source` thì bị báo lỗi.
- `market_chart` (có sẵn): nến thật + SMA, RSI, MACD từ `market/BTCUSDT-1h.json` (1000 nến, 0 lỗi OHLCV, Binance public).

## 3. Đánh giá thư viện cũ (illus.py) cho kênh Người Giữ Vốn

Dùng được, sau khi đổi màu theo bảng màu kênh: coin, coins, wallet, lock, check, clock, scale, cracked_coin, candle_img, equity_img, gauge_img.

Không dùng cho NGV:
- `chart_big_img` có nhãn "Chốt lời / Vào / Cắt lỗ" kèm RSI. Đó là nhãn vào lệnh, trái luật kênh (không kèo, không gợi ý). Muốn dùng phải đổi nhãn trung tính.
- `receipt_img` ("Thuốc A", "Vitamin C"), `calendar_img` ("HSD"), `box/bottle/blister/shelf`: chỉ cho Novixa (nhà thuốc).
- `stock_img` ("MÃ A, MÃ B ... +1,8%"): chỉ dùng nếu không giống gợi ý mã.
- `trader_img`, `person_img`: nhân vật. Đã quyết định không dùng nhân vật cho NGV (hướng chữ + hình khối).
- Nền navy cố định, không theo bảng màu. Biểu đồ cũ không có nhãn "Dữ liệu minh họa".

Các PNG Agent Trading trong `proof/kit-video-library/20261029T09*` đều ở trạng thái `approval: proposed`, chưa vào `catalog.json`. Chỉ cho video về Agent Trading, kèm nhãn "Paper / Demo — không phải khuyến nghị". Không dùng cho NGV.

## 4. Chưa đạt / chưa làm

1. Đã dựng thử video giả lập (thời gian giả, âm im lặng) qua đường Studio cho cả 9:16 và 16:9, chạy được. Chưa chạy thật trên giao diện với giọng ElevenLabs. Chưa dùng `kv.py render`.
2. Chưa có chuyển cảnh giữa các mẫu, mới có hiệu ứng xuất hiện.
3. Chưa có bảng màu thương hiệu riêng cho NGV và Novixa. Hiện dùng bảng màu thử.
4. Giao diện Studio dùng bố cục khác. 4 lỗi hình đã thấy ở đó (thẻ bị cắt chữ, mảng mờ, hộp đè biểu đồ, nhãn "Alpha", chữ vàng khó đọc) chưa sửa. Cần tìm mã của nó.
5. Kiểm tra chất lượng hình hiện bắt: chữ ra ngoài vùng an toàn, thiếu glyph, nội dung quá dài. Chưa bắt: chữ đè lên hình, tương phản chữ trên hình.
6. Chưa chuyển các biểu tượng cũ còn dùng được (cracked_coin, equity, bill) sang bộ mới; chưa có biểu tượng sổ tay, hộp thuốc, kệ, thang giảm dần, tháp tiền.
7. Biểu tượng nến trong bể của `edu_flow` còn nhỏ.
8. Danh mục 80/80/40 tài nguyên trong `catalog.json` chưa đạt (NOVIXA 26 ảnh, Agent Trading 0, dùng chung 0). Bộ biểu tượng mới tự vẽ chưa được đăng ký vào catalog.

## 5. Quy tắc nội dung cho hình NGV (nhắc lại)
Không kèo, không gợi ý mã, không quảng cáo sàn. Số liệu có nguồn và ngày. Đồ thị giả lập phải ghi "Dữ liệu minh họa". Không dùng người thật. Không dùng ảnh hay biểu tượng ngoài giấy phép thương mại.

## 6. Nối vào giao diện Video Studio (A2 chạy trên giao diện)

Đường chạy: giao diện Studio -> `NovixaStockMotionRunner.cs` -> `build_stock_motion.py` -> `novixa_motion_graphics.py`.

- `tools/kit-video-library/ngv-edu/edu_studio.py`: cầu nối. Đọc các tệp `docs/marketing/*/kich-ban/*.hinh.json`, ghép từng cảnh vào câu đầu của nó (khớp theo đầu câu, phải tìm thấy đủ và đúng thứ tự, câu đầu là dòng 0), vẽ ảnh duyệt và từng khung video.
- `docs/marketing/nguoigiuvon/kich-ban/A2-von-song-von-giao-dich.hinh.json`: 12 cảnh của A2, gồm bảng màu và tham số từng mẫu.
- `build_stock_motion.py`: `_stills_job` vẽ ảnh bằng `edu_studio` cho các câu có cảnh khớp, ghi `source: "edu"` và `edu: {...}` vào `stills.json`. Ảnh Gemini người dùng đã chọn vẫn được giữ.
- `novixa_motion_graphics.py`: `plans_for_take` gắn `edu` vào kế hoạch, `compose` vẽ khung theo giờ của cả cảnh (từ câu đầu), rồi phủ phụ đề như các cảnh khác. `frame_size` trả đúng 1080x1920 hoặc 1920x1080.
- Tắt khi cần: biến môi trường `NOVIXA_STUDIO_EDU=0` hoặc `useEdu: false` trong job.
- Kịch bản không có tệp `.hinh.json` khớp thì chạy y như cũ (không đổi gì).
- Đã thử: 49/49 câu A2 ghép đúng, 0 lỗi kiểm tra chữ ở cả hai hướng, dựng được video giả lập 16 giây mỗi hướng.
- Chưa thử trên máy thật: phông Segoe UI (cầu nối ưu tiên, nếu thiếu thì dùng Inter/DejaVu), giọng thật, nút trên giao diện.
- Thêm kịch bản mới: chỉ cần viết một tệp `.hinh.json` cạnh kịch bản (khoá `first` là câu đầu của cảnh).
- Số liệu "457 nghìn tỷ đồng" trong cảnh 8 vẫn là cụm "số liệu tổng hợp công bố cuối tháng 10/2026". Cần nguồn có tên trước khi đăng.

## 7. Cập nhật sau lần xem ảnh trên giao diện

- Ảnh duyệt là cảnh ở trạng thái cuối, kèm dải tối của phụ đề (không in chữ). Các câu cùng cảnh ra cùng một ảnh nên giao diện gộp thành một thẻ: A2 còn 12 ảnh để duyệt thay vì 49.
- Khoảng trống dưới ảnh ngang là vùng phụ đề (30% chiều cao, Studio phủ phụ đề lên đó khi dựng). Không phải lỗi bố cục.
- `edu_studio` tự chọn mức phóng (zoom) lớn nhất mà không tràn khung, rồi căn giữa theo chiều dọc trong vùng an toàn. Biểu đồ và bể dòng chảy đã kín vùng nên không phóng. Cảnh tiêu đề khổ ngang chừa chỗ cho biểu tượng.
- Kiểm tra mới: chữ nhỏ nhất dưới 30px (dọc) / 28px (ngang) và khung chiếm dưới 62% vùng an toàn được ghi vào `eduProblems`. Vẫn chưa bắt chữ đè hình và độ tương phản.
- Đã sửa: tiêu đề dài đè phụ đề ở khổ dọc (cảnh 7, 11); cảnh "Một chiều" bị chồng nhãn; biểu tượng đè tiêu đề ở khổ ngang.

## 8. Nhịp hình theo từng câu (A2: 16 cảnh, mỗi câu một thay đổi)

- A2 chia thành 16 cảnh (trước là 12). Cảnh dài được tách: tiêu đề mở đầu, ví dụ 20 triệu, biểu đồ giá đi ngược, biểu đồ dời điểm dừng lỗ, cháy tài khoản/cháy ví, kết và lưu ý.
- Tham số mới `focus` trong tệp `.hinh.json`: danh sách theo thứ tự câu của cảnh. Mỗi phần tử là số ô (bắt đầu từ 0), danh sách số ô, hoặc `null` (không nhấn ô nào). Ví dụ `"focus": [null, 0, 0, 1, 1, 2]`.
- Ô nào được nhắc đến thì hiện ra khi câu đó bắt đầu và sáng lên; các ô khác mờ đi (còn 42%). Ô chưa tới lượt chỉ hiện viền mờ để thấy cấu trúc cảnh. Câu `null` thì mọi ô sáng bình thường.
- Áp dụng cho `edu_steps`, `edu_rules`, `edu_contrast`, `edu_flow` và chuỗi hậu quả của `edu_bignumber`.
- Ảnh duyệt vẫn là cảnh ở trạng thái cuối, nên các câu cùng cảnh vẫn gộp một thẻ: A2 có 16 thẻ.
- Cơ chế chọn mức phóng không được bỏ nội dung (trước đây khổ dọc bỏ mất hai bước hậu quả ở cảnh số liệu).
- Đã thử bằng khung hình ghép từ kế hoạch thật. Chưa thử với giọng thật: nhịp theo giờ đọc có thể cần chỉnh.

## 9. Logo, nhãn kênh, nền, vùng an toàn (giống video 1)

- Sidecar `.hinh.json` có thêm các khoá cấp cao: `brand` ("Người Giữ Vốn"), `logo` (đường dẫn `ngv-edu/assets/ngv-logo-shield.png`, cắt từ banner, viền hơi mềm), `tagline` ("Giữ vốn trước — tính lãi sau"), `footer` ("Chia sẻ kiến thức · Không phải lời khuyên đầu tư"), `background` ("ngv": quầng sáng toả tròn + nến mờ).
- `edu_studio.py`: `_brand_mark` vẽ logo + tên kênh ở lề trên; `overlay()` vẽ chân trang SAU lớp phủ phụ đề; `_ngv_background` vẽ nền.
- Vùng an toàn: dọc {trái .07, phải .07, trên .080, dưới .25}; ngang {trái .06, phải .06, trên .125, dưới .30}. Phụ đề: dọc từ y≥1460, ngang từ y≥780.
- Ảnh duyệt có mẫu phụ đề (câu đầu của cảnh) để nhìn đúng bố cục sau khi dựng.
- Thẻ A2 hiện "LỘ TRÌNH GIỮ VỐN · BÀI 2" (A2 = giai đoạn A Nền tảng, mục 2). Thẻ cuối "LỜI KẾT".
- Mã code gốc dựng video 1 (A1) đã mất, không tìm thấy trong repo. Dáng video 1 được dựng lại từ khung hình `ngv-a1-trailer.mp4`. Nếu có mã gốc/logo gốc thì thay vào để A1 và A2 dùng chung một nguồn.

## 10. Dựng video thật trên Studio: số đo và bẫy đã gặp

- Thư mục kết quả: `E:\KitPlatform\proof\kit-video-production-v1\outputs\novixa-studio\<buildId>\<UTC>-motion|stills|shorts`. Mỗi `-motion` có `task.json` (ghi lúc bắt đầu, `status: running`), `voice.mp3/.wav`, `alignment.json`, `lines.json`, `video.mp4`; `episode.json` chỉ có khi XONG (`status: ready`, `videoPath`). Xem `episode.json` để biết đã xong chưa; `video.mp4` lớn dần khi đang dựng.
- Đo thực tế (01/11/2026, A2 ngang 1920x1080, 30 fps, giọng ElevenLabs eleven_v3): dựng mất ~19 phút (22:26 → 22:45), video ~37 MB. Nguyên nhân chậm: mỗi khung tính lại chữ/bố cục (`_fit_scene`, nền, logo). Hướng tối ưu chưa làm: lưu đệm theo cảnh (kết quả zoom/dy, nền, logo), chỉ tính lại phần động.
- Lỗi "chạy xong không thấy video": `NovixaStockMotionRunner.ReadTask` bỏ qua `task.json` cũ hơn 20 phút, nên lượt dựng dài hơn 20 phút bị coi như không tồn tại. Đã đổi thành 90 phút (commit10). Cần build lại API.
- Nút dựng chỉ chạy giọng ElevenLabs khi người dùng bấm; không tự chạy.
- Đăng bài: YouTube tự đăng qua API. TikTok chỉ "Upload to TikTok" (video vào Hộp thư/Thông báo hệ thống trong app, bấm Đăng tay; bản Production nộp 31/10, đang chờ duyệt). Meta khoá: Fanpage đăng tay ở tab "Đăng tay". Website chưa cấu hình.

## 11. Cách làm việc và đường tắt (để không phải tìm lại)

- Công cụ trên máy người dùng: chỉ có liệt kê thư mục, `stage` (sao chép lên môi trường làm việc) và `commit` (ghi lại, có `expectedMtimeMs` để chống ghi đè). Không có shell trên máy. Luôn stage lại ngay trước khi sửa tệp để lấy mtime mới.
- Vòng làm việc: stage tệp -> sửa bản sao -> thử bằng bộ thử ở máy làm việc -> commit về `E:\KitPlatform`. Không git commit nếu chưa được yêu cầu.
- Bộ thử (không nằm trong repo, mất khi hết phiên): chạy `edu_studio` trên 49 câu A2, dựng khung ghép, kiểm tra `eduProblems`. Nếu cần, viết lại từ `edu_studio.bind/still/frame`.
- Môi trường làm việc thiếu phông Windows (Segoe UI), `psycopg2`; render dài dễ hết thời gian chờ, chạy nền. Máy thật dùng Segoe UI.
- Việc còn mở: nguồn có tên cho "457 nghìn tỷ đồng"; duyệt kịch bản A2 ("Chờ duyệt"); kiểm nhịp nhấn từng câu với giọng thật; phông Segoe UI đủ glyph; chuyển cảnh; thêm mẫu cảnh; kiểm chữ đè hình/tương phản; cảnh đối lập khổ dọc đang bỏ gạch đầu dòng; `.dang.json` của A2 còn câu chữ cũ; nhãn "Audience student" cứng ở bộ đăng website; tối ưu tốc độ dựng ngang.
