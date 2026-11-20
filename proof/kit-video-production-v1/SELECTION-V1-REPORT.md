# Scene selection v1

Bộ chọn cảnh nằm ở `tools/kit-video-production/kit_video/selection.py`. Hai phim 180 giây không được dựng lại. Thư viện ảnh độc lập vẫn là 26 NOVIXA, 0 AgentTrading, 0 Shared. Máy không có model embedding local; không gọi API trả phí.

## Cách chọn

Mỗi câu thoại thành một Scene Brief: khoảng thời gian, thương hiệu, chủ đề, thực thể, hành động, ý chính, ý đồ hình, loại tài nguyên bắt buộc, tag ưu tiên và tag cấm, vai trò, yêu cầu chart, khoảng thời lượng, ràng buộc liên tục, rồi mới là tài nguyên đã chọn và lý do.

Lọc cứng đi trước điểm số: thương hiệu (hoặc Shared), quyền, trạng thái duyệt, loại tài nguyên, category bắt buộc, category cấm. Embedding không được thay các điều kiện này. Ứng viên dưới ngưỡng nghĩa 0.50 bị loại kèm lý do. Kết quả là một nhóm đã chấm điểm, không phải dòng đầu tiên trong catalog.

Điểm thử nghiệm, mỗi thành phần trong khoảng 0–1:

```
0.40 * semantic_relevance
+ 0.20 * scene_role_fit
+ 0.15 * asset_quality
+ 0.10 * composition_fit
+ 0.15 * underuse_bonus
```

`semantic_relevance` = 0.6 điểm category + 0.4 điểm tag. Nếu câu thoại nêu một từ phân biệt (liên thông, tồn, vỉ, quầy, …) mà metadata ảnh không có, điểm nghĩa bị trần ở 0.35. Mỗi từ phân biệt thừa trên ảnh mà câu không nói trừ thêm 0.35. “Nhà thuốc” không bị đọc thành từ “thuốc”.

Lần đo đầu, `underuse_bonus` rơi từ 1 xuống khoảng 0.1 khi ảnh đã dùng. Ảnh gần đúng nhưng chưa dùng thắng màn đúng đã dùng một lần. Tỷ lệ phù hợp lúc đó là 34/39 = 0.8718. Bonus được nới thành 1 / 0.85 / 0.7 theo số lần dùng trong lịch sử. Phạt dùng lại trong cùng một phim nằm ở bước tối ưu chuỗi (0.28 mỗi lần), không nằm trong hệ số này. Sau chỉnh, cùng bộ nhãn cho 39/39. Đây là kết quả đo, không phải bằng chứng các trọng số đã tối ưu.

Chuỗi dùng beam search rộng 16 trên tối đa 8 ứng viên mỗi cảnh. Phạt ảnh gần liền kề một lần khi khoảng cách dHash lớn hơn 0 và không quá 6 (trừ 0.18). Lặp có chủ đích vì continuity không bị phạt. `forbid_reuse` cho điểm −1 nên một gap (điểm 0) thắng một lần dùng lại bị cấm. Gap không bao giờ thắng một ứng viên còn điểm dương.

Một câu chart có bốn ý (khung rộng, nến tăng mạnh nhất, SMA 20, kết luận) thành bốn beat có độ dài khác nhau. Một ảnh tĩnh cho một ý, kể cả kéo dài, vẫn là một beat; nếu từ 16 giây trở lên thì ghi lý do giữ khung. Không cắt theo đồng hồ cố định.

Chart chỉ được chọn khi có một cửa sổ nến trên file lưu sẵn thỏa điều kiện. OHLCV lỗi thì không vẽ. “Lợi nhuận chiến lược” bị từ chối, không bịa nến. Nhãn mode lấy từ file (`historical`).

## Chống lặp

Ngưỡng lấy từ catalog thật, không đặt trước rồi tuyên bố xong:

| Việc | Ngưỡng dHash | Căn cứ |
|---|---|---|
| Coi là cùng một ảnh | ≤ 3 | Cặp phái sinh đo được là 0 và 1. Không có cặp đã duyệt nào ≤ 3. |
| Cảnh báo gần giống | ≤ 6 | Cặp đã duyệt gần nhất là `nvx-doc008-pos` và `nvx-qld-pos`, khoảng cách 6. |

Hash đúng file vẫn do pipeline ảnh hiện có. Giống bố cục và giống ngữ nghĩa trong bộ chọn này là luật từ khóa, category và dHash, không phải embedding.

## Embedding

`probe_embedding()` báo `available: false`, `used: false`. Không có torch, CLIP hay sentence-transformers. Thử Jaccard trên nhãn cho 30/34 = 0.882 và không áp hard filter loại/thương hiệu, nên không được dùng. Baseline bag-of-words (`choose_asset`, không lọc loại, chọn từng cảnh) là 23/34 = 0.676. Bộ chọn có cấu trúc cao hơn cả hai trên cùng 34 cảnh ảnh.

## Bộ đánh giá

39 Scene Brief trong `tests/test_selection.py` và `tests/selection_cases.py`. Nhãn viết như phán đoán về catalog: ảnh đúng chủ thể, ảnh sai, ảnh gần, lần lặp được phép, chart có điều kiện. Đo sau đó bằng `evaluate()`, kết quả ghi ở `library/selection-eval.json`. Đây không phải một người duyệt độc lập chấm sau khi thuật toán đã chạy.

Tiêu chí: phù hợp khi ảnh đúng chủ thể của câu, hoặc khi không có tài nguyên đã duyệt cho chủ thể đó và kết quả là gap được ghi rõ. Ảnh giao diện không thay phòng, vỉ, hộp hay người. Tài nguyên bị từ chối hoặc sai thương hiệu không được chọn. Chart phù hợp khi cửa sổ trả về thỏa điều kiện trên file nến, hoặc khi không có cửa sổ nào và bộ chọn nói thiếu.

| Chỉ số | Kết quả |
|---|---|
| Phù hợp theo nhãn | 39/39 = 1.0 |
| Vi phạm hard filter | 0 |
| Cặp trùng hash đúng | 0 |
| Cặp gần trùng | 0 |
| Chuỗi bố cục lặp không chủ đích | 0 |
| Chart đúng dữ liệu | 5/5 |
| Gap được khai, không lấp bằng ảnh khác | 8/39 |

Tám gap: dược sĩ tư vấn khách, kho thật, hộp bán lẻ đủ các mặt, bàn giao dịch nhiều màn hình, sai thương hiệu, câu không có chủ thể, Bitcoin tăng 80% (file không có cửa sổ đó), và cảnh thứ hai bị cấm dùng lại màn tồn vì bản liên thông còn `review`.

Năm chart: BTC tăng (start 70, 48 nến, change 0.0739), ETH đi ngang, BTC giảm, BTC 80% thành gap, lợi nhuận chiến lược thành `refused`. SMA 20 tại nến đầu cửa sổ tăng bằng trung bình đóng cửa 20 nến trước: 80915.0065, `sma20_matches` true. Nến tăng mạnh nhất trong cửa sổ là index 22, mở 81720.01, đóng 83760.0.

`python -m unittest tests.test_selection tests.test_system.SystemTests.test_engine_does_not_bake_a_runtime` — 7 tests, OK. Engine không chứa `atempo`, không chứa hằng 180, không chứa `SCENES =`.

## Preview đã xem khung hình

Chỉ dựng sau khi bộ đánh giá đạt. Ảnh do `optimize_sequence` chọn, không gán tay.

NOVIXA, 25 giây, không tiếng:

`outputs/novixa-select-preview/20261029T051413Z-select-v1/video.mp4`

Contact sheet (một khung giữa mỗi cảnh): cùng thư mục, `timeline-contact-sheet.png`.

| Cảnh | Đã chọn | Nhìn trên khung |
|---|---|---|
| s1-pack | `ref-blister-foil` | Hai vỉ, caption dưới ảnh |
| s2-hands | `ref-hands-blister` | Tay kiểm vỉ |
| s3-floor | `ref-pharmacy-floor` | Nội thất tủ thuốc, không bị hiểu thành màn bán vì câu có “bán lẻ” |
| s4-stock | `nvx-doc008-stock` | Màn tồn |
| s5-join | `nvx-doc008-stock` + `ref-blister-foil` | Màn tồn và vỉ cạnh nhau, caption nói hai nguồn. Lặp có lý do đối chiếu |

Trace: `projects/novixa-select-preview/scene-trace.json`.

Agent Trading, 22 giây, không tiếng. Cửa sổ BTCUSDT 1h, start 70, count 48, mode historical, nguồn binance-public-klines:

`outputs/trading-select-preview/20261029T051511Z-select-v1/video.mp4`

| Beat | Độ dài | Khung đã xem |
|---|---|---|
| wide | 5.06s | Cả 48 nến, chưa có đường SMA |
| highlight | 6.83s | Phóng vùng nến xanh cao, nhãn “Nến tăng”, cột volume cùng vị trí cao |
| sma | 6.07s | Cùng cửa sổ, đường SMA 20, warmup 19 nến trước cửa sổ |
| conclusion | 4.05s | “Đoạn này tăng 7,4%” và “Không phải khuyến nghị.” 7,4% là `notes.change` của cửa sổ này |

Trace: `projects/trading-select-preview/scene-trace.json`.

Validate trước render: NOVIXA 5 cảnh / 25.00s, Trading 4 cảnh / 22.00s. Chưa có người nghe TTS vì preview cố ý im lặng (“Preview hình, chưa lồng tiếng.”). QA kỹ thuật của pipeline không được ghi là duyệt nghĩa hình; phần nghĩa ở trên là xem các khung render.

## Chưa làm

Chưa áp bộ chọn này vào hai phim 180 giây. `novixa-long` vẫn còn thẻ “Chưa có ảnh hộp thuốc” dù vỉ tham chiếu đã có, vì cut đó không được encode lại.

Vẫn thiếu ảnh đã duyệt: hộp thuốc bán lẻ đủ các mặt, kho, dược sĩ với khách, bàn giao dịch. AgentTrading và Shared vẫn 0 ảnh tĩnh. Ảnh bị từ chối (vỉ có tên thuốc thật, ảnh hoạt hình, brochure, màn hình có mật khẩu, hai màn hình trống) giữ nguyên trạng thái từ chối.

Năm ảnh Wikimedia trong preview NOVIXA có giấy phép CC BY hoặc CC BY-SA. Nếu phát hành phái sinh cần giữ điều khoản share-alike của file BY-SA.

Bộ 39 cảnh do cùng quá trình viết thuật toán đặt nhãn. Tỷ lệ 100% là khớp nhãn đó, cộng với việc xem khung hai preview. Chưa phải nghiệm thu của một người duyệt tách biệt trên toàn bộ thư viện.
