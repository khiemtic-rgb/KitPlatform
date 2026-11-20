# NOVIXA Intro Film — kế hoạch hình V1

Ngày kiểm: 2026-10-23. Đây là kế hoạch sẵn sàng, chưa phải bản dựng. `durationLocked` vẫn là false. Chưa có `finalVideo`.

Nguồn âm thanh đã duyệt là voice-read V3:

`proof/kit-video-production-v1/outputs/novixa-intro-film/20261023T084726Z-voice-read/`

Đã kiểm 12 file `s*.wav` và `full-read.wav`. Mỗi file cảnh là 48 kHz, mono. Thời lượng file khớp bảng dưới đây. Tổng file ghép 198,145 giây. Tổng lời, trừ lặng đầu và cuối từng file, 183,44 giây. Tổng mốc hết lời 187,38 giây.

Ước lượng cũ 250,68 giây và gợi ý 180 giây không phải thời lượng đã khóa.

## Quyết định đã duyệt

- Giọng V3 đạt yêu cầu nghe. Cách đọc chữ `NOVIXA` trong TTS là `Nôvixa`, theo `20261023T083501Z-pronunciation-probe-v2/novixa-closed.wav`.
- S04 giữ nghỉ đã duyệt: chèn 0,60 giây trước “đúng không?”. File V3 đo được nghỉ 0,66 giây.
- S12 đọc địa chỉ là “novixa chấm vn”. Chữ trên hình vẫn là `novixa.vn`.
- Logo, chữ trên hình và phụ đề giữ `NOVIXA`.
- `script.md` không bị viết lại.

Bản đọc được duyệt không có nghĩa là hình của từng cảnh đã duyệt.

## Tài sản đã kiểm trên đĩa

| Tài sản | Đường dẫn | Kiểm tra | Trạng thái dùng cho phim |
| --- | --- | --- | --- |
| Lời 12 cảnh | `outputs/novixa-intro-film/20261023T084726Z-voice-read/s01` … `s12` `.wav` | 12 file, 48 kHz, mono | Âm thanh đã duyệt |
| File ghép | cùng thư mục, `full-read.wav` | 19.022.042 byte | Tham chiếu, gồm lặng kỹ thuật |
| Logo gốc | `docs/novixa/assets/brand/novixa-logo.png` | 855.023 byte | Được dùng, không sửa file |
| Tấm logo s05/s11 | `outputs/novixa-intro-film/20261023T063730Z-logo-plates/s05-s11-logo.png` | 1280×720 | Ảnh đứng, chưa phải video |
| Tấm kết s12 | `.../20261023T063730Z-logo-plates/s12-cta.png` | 1280×720, có `novixa.vn` | Ảnh đứng, chưa phải video |
| Dòng s03 | `.../20261023T070441Z-graphics/s03-caption.png` | 1280×720 | Chỉ là chữ, không phải ẩn dụ chuyển động |
| Dòng s04 | `.../20261023T070441Z-graphics/s04-caption.png` | 1280×720 | Chỉ là chữ, không có lịch lật |
| Sơ đồ s08 | `.../20261023T070441Z-graphics/s08-flow.png` | 1280×720 | Sơ đồ ba nhãn, không phải màn báo cáo |
| POS | `docs/novixa/launch/DOC-008/assets/screenshots/10-staff-pos.png` | 780×1688, có chữ DEMO_PHARMACY | Ảnh giao diện thật, chưa được gắn vào phim |
| Tồn kho | `.../04-inventory-stock.png` | 2880×1800, màn Kho hàng | Ảnh giao diện thật, chưa được gắn vào phim |
| Báo cáo | `.../08-reports-home.png` | 2880×1800, trang danh mục báo cáo | Không phải một báo cáo đã mở số liệu |
| Chân dung v3 | `projects/novixa-pharmacy-commercial/assets/pharmacist-nova-cartoon-v3.jpg` | Có file | Ảnh một tư thế, chưa duyệt cho intro |
| Khung v7 | `.../keyframes/v7/s01-pharmacy.jpg` | 1280×720 | Ảnh đứng đang chờ xem của quảng cáo 25 giây |
| Storyboard 8 khung | `.../probes/character-reference-v1/` | Có crop | Tham chiếu, không phải keyframe sản xuất |
| Style proof | `outputs/novixa-style-proof/20261023T021435Z-style-proof/video.mp4` | 15,5 giây, hình học | Không phải cảnh s03 của phim |
| Graphics hold quảng cáo | `outputs/novixa-pharmacy-commercial/20261023T063019Z-graphics-holds/` | Ba mp4 ảnh đứng | Bảng số mô phỏng, không dùng cho intro |
| Video tham chiếu Gemini | `docs/kit-video-production/references/novixa-gemini-reference.mp4` | Không có file | Thiếu |

Không có footage chuyển động của người, cửa hàng hay thao tác lấy thuốc. Không có nhạc được cấp quyền. `musicPolicy` vẫn là `none`.

Pipeline hiện có thể ghép WAV, vẽ chữ và logo, giữ một ảnh thành video, rồi encode H.264/AAC. Lệnh ghép cả phim intro đang bị chặn khi chưa đủ cảnh. Không dùng lia hoặc phóng ảnh đứng để gọi là chuyển động.

Khổ hình trong `manifest.json` của dự án là đề xuất 1920×1080, chưa khóa. Tấm đã vẽ và khung v7 là 1280×720. Preview đầu nên theo 1280×720 cho đến khi khổ hình được khóa.

## Sẵn sàng theo cảnh

| Cảnh | File lời | Hết lời | Hình hiện có | Sẵn sàng |
| --- | ---: | ---: | --- | --- |
| s01 | 17,784 | 16,88 | Không có footage | Chặn, thiếu chuyển động |
| s02 | 19,944 | 19,02 | Không có footage | Chặn, thiếu chuyển động |
| s03 | 14,784 | 13,90 | Tấm chữ | Preview chữ được, thiếu ẩn dụ |
| s04 | 13,417 | 12,52 | Tấm chữ | Preview chữ được, thiếu lịch |
| s05 | 15,288 | 14,38 | Tấm logo | Preview logo được, thiếu POS |
| s06 | 15,504 | 14,60 | Ảnh POS chưa gắn | Chờ duyệt ảnh |
| s07 | 17,784 | 16,88 | Ảnh tồn kho chưa gắn | Chờ duyệt ảnh, ảnh chưa đủ câu hỏi |
| s08 | 16,824 | 15,92 | Sơ đồ; ảnh danh mục báo cáo chưa gắn | Sơ đồ làm được, báo cáo chờ duyệt |
| s09 | 14,928 | 14,06 | Chỉ có chân dung một tư thế | Chặn, thiếu chuyển động |
| s10 | 16,104 | 15,22 | Không có toàn cảnh đã duyệt | Chặn, thiếu chuyển động |
| s11 | 15,912 | 15,04 | Tấm logo | Preview logo được |
| s12 | 19,872 | 18,96 | Tấm logo và `novixa.vn` | Preview thẻ kết được |

Ba giây giữ hình sau lời ở s12, 1,2 giây logo trước lời ở s05 và 0,8 giây sau lời ở s09 vẫn là đề xuất hình, chưa nằm trong file âm thanh và chưa được duyệt.

## Từng cảnh

### s01-day-opens

Một ngày nhà thuốc bắt đầu. Lời 16,48 giây trong file 17,784 giây. Hình cần cửa mở, khách, quầy và nhịp việc tăng. Chữ trên hình: “Một ngày ở nhà thuốc”. Không có footage. Chân dung và khung v7 là ảnh đứng chưa duyệt, không được lia thành cảnh này. Cần keyframe đã duyệt rồi mới tính chuyển động. Nghiệm thu: có nhịp việc, lời giữ nguyên, không biến thành một ảnh đứng.

### s02-busy

Bận cả ngày nhưng chưa chắc đang kiểm soát. Lời 18,72 giây, hết lời 19,02. Hình là nhiều việc dồn lại rồi dừng ở câu hỏi. Chữ: Bán hàng, Tồn kho, Nhân sự, Khách hàng. Không có footage. Không giả hành động bằng ảnh tĩnh. Nghiệm thu: câu hỏi cuối có khoảng nghỉ đã có trong lời, hình không thành danh sách tính năng.

### s03-stock-cash

Hàng nằm yên là vốn. Lời 13,64 giây, hết lời 13,90. Ẩn dụ là hộp, két và lịch, không cần nhân vật ở mọi khung. Tấm chữ “Hàng tồn kho cũng là vốn” đã có. Style proof hình học không phải cảnh này. Preview có thể giữ tấm chữ đúng độ dài file WAV. Ẩn dụ chuyển động vẫn chưa có và không được làm bằng cách trượt tấm chữ. Nghiệm thu: đọc được ẩn dụ mà không có số doanh thu.

### s04-expiry

Hạn dùng không chờ trí nhớ. Lời 12,20 giây, hết lời 12,52, gồm nghỉ 0,66 giây trước “đúng không?”. Tấm chữ “Theo dõi tồn kho · Chú ý hạn dùng” đã có. Chưa có lịch lật hay nhóm hộp. Không bịa số lô. Không dùng bảng tồn kho mô phỏng của quảng cáo. Preview chữ được. Nghiệm thu: câu hỏi không bị đọc gấp, không có hạn dùng bịa.

### s05-introduce

NOVIXA ra đời để việc rõ hơn. Lời 14,06 giây, hết lời 14,38. Chữ: NOVIXA và “Nền tảng vận hành nhà thuốc thông minh”. Tấm logo đã có, logo không bị vẽ lại. Kịch bản còn muốn POS thật trong khung thiết bị. Ảnh POS tồn tại nhưng chưa được duyệt để đưa vào phim, và là màn dọc 780×1688 ghi DEMO_PHARMACY. Preview chỉ logo là một phần, chưa đủ cảnh. Nghiệm thu: logo không méo, giọng là Nôvixa, không phóng đại sản phẩm.

### s06-selling

Bán hàng tại quầy. Lời 14,34 giây, hết lời 14,60. Chữ: Bán hàng, Ghi nhận dữ liệu. Ảnh `10-staff-pos.png` là giao diện POS thật của DEMO_PHARMACY, giỏ trống, tạm tính 0 đ. Hồ sơ dự án vẫn để ảnh này ở trạng thái đề xuất. Ảnh không có nhãn “Dữ liệu minh họa”. Chưa được dựng. Nếu được duyệt, phải gắn nhãn đó suốt thời gian UI xuất hiện và không thêm số liệu. Nghiệm thu: chỉ UI đã được phép dùng.

### s07-inventory

Tồn, hạn và giá trị hàng. Lời 16,56 giây, hết lời 16,88. Ảnh `04-inventory-stock.png` là màn tồn kho thật, có số lượng và tên hàng kèm hàm lượng. Ảnh chưa được duyệt cho phim. Nó cho thấy số tồn, không mở “sắp hết hạn” hay “giá trị hàng”. Hai ý đó chỉ xuất hiện như tên báo cáo ở màn danh mục. Không dùng bảng số vẽ bằng code để thay màn này. Nghiệm thu: từng vùng UI được xác minh, không bịa giá trị.

### s08-connected-data

Dữ liệu nối lại, phần mềm không quyết định thay người. Lời 15,58 giây, hết lời 15,92. Sơ đồ Bán hàng / Tồn kho / Báo cáo đã vẽ được và bám câu chữ trên hình. Phần sau của cảnh cần màn báo cáo đã xác minh. `08-reports-home.png` là trang chọn báo cáo, không phải báo cáo đã có số. Có thể preview riêng sơ đồ. Chưa ghép ảnh báo cáo khi chưa được duyệt. Nghiệm thu: không nói phần mềm tự quyết định.

### s09-people

Người ở trước, đồ họa lùi. Lời 13,78 giây, hết lời 14,06. Chữ: “Vận hành tốt hơn. Phục vụ tốt hơn.” Chỉ có chân dung một tư thế và storyboard chưa duyệt. Không đủ để dựng dược sĩ lắng nghe khách. Không biến cảnh thành demo tính năng. Nghiệm thu: người là chủ thể.

### s10-philosophy

Nhà thuốc rõ, chủ động, có hệ thống. Lời 14,84 giây, hết lời 15,22. Chữ ba dòng: Rõ ràng hơn, Chủ động hơn, Có hệ thống hơn. Chưa có toàn cảnh nhà thuốc đã duyệt. Không thêm số tăng trưởng. Ba dòng chữ có thể vẽ bằng code sau, nhưng cảnh kịch bản là toàn cảnh chứ không phải một tấm chữ. Nghiệm thu: không có “hiệu quả thần kỳ”.

### s11-positioning

Định vị thương hiệu. Lời 14,72 giây, hết lời 15,04. Tấm logo đã có đúng hai dòng chữ. Có thể giữ tấm đó theo độ dài file WAV để xem thử. Đây vẫn là ảnh đứng, chưa phải cảnh có đường nối dữ liệu. Nghiệm thu: đúng câu định vị, logo không bị vẽ lại.

### s12-cta

Mời xem novixa.vn. Lời 18,52 giây, hết lời 18,96. Tấm kết đã có logo, câu định vị và `novixa.vn`. Có thể preview theo độ dài file WAV. Ba giây giữ thêm sau lời vẫn là đề xuất, chưa khóa. Không thêm số điện thoại, giải thưởng hay cam kết. Nghiệm thu: địa chỉ đọc được và nằm trong vùng an toàn.

## Preview đầu tiên có thể làm

Chưa đủ điều kiện để dựng cả phim. Pipeline hiện tại có thể làm một bản xem riêng, không gọi là phim:

- Chỉ s03, s04, s08, s11 và s12.
- Mỗi cảnh là ảnh đã có, giữ đúng bằng độ dài file WAV V3 của cảnh đó.
- Khổ 1280×720, 30 khung/giây, có tiếng nói, không nhạc.
- Thư mục phiên bản mới. `finalVideo` của phim vẫn null.
- Không đưa s01, s02, s06, s07, s09, s10 vào bản xem đó.
- s05 chỉ đưa nếu chấp nhận thiếu khung POS. Mặc định để s05 ngoài bản xem đầu.

## Việc cần duyệt trước khi đi tiếp

1. Cho phép hoặc từ chối ba ảnh DOC-008 cho s06, s07 và s08. Nếu dùng, nhãn “Dữ liệu minh họa” phải hiện suốt lúc ảnh có mặt.
2. Khổ hình preview là 1280×720 hay khóa 1920×1080.
3. Keyframe chuyển động cho s01, s02, s09 và s10. Chưa có thì không gọi dịch vụ video.

Nhiệm vụ triển khai tiếp theo nên là bản xem đồ họa một phần ở trên, không phải render cả phim và không phải gửi Runway.
