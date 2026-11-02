# NOVIXA Intro Film — phát triển hình V2

Ngày: 2026-10-23. Tài liệu này phát triển hình từ kế hoạch V1 và bản xem ảnh đứng V1. Không thay kế hoạch V1. Không phải bản dựng phim. `finalVideo` vẫn null. `durationLocked` vẫn false.

Lời đã duyệt là voice-read V3 tại `proof/kit-video-production-v1/outputs/novixa-intro-film/20261023T084726Z-voice-read/`. Cách đọc `NOVIXA` trong TTS vẫn là `Nôvixa`. Chữ trên hình vẫn là `NOVIXA`. Địa chỉ trên hình là `novixa.vn`, lời đọc là “novixa chấm vn”.

Bản xem V1 tại `outputs/novixa-intro-film/20261023T091740Z-partial-graphics-preview/preview.mp4` là năm ảnh đứng có lời: S03, S04, S08, S11, S12. Không phát hiện lỗi cần render lại. Bản đó không được tính là footage và không được tính là cảnh đã xong.

Khổ 1280×720 chỉ là khổ của bản xem. Khổ phim 1920×1080 trong manifest dự án vẫn là đề xuất, chưa khóa. Các khoảng giữ hình đã nêu trước đây (S05 thêm 1,2 giây, S09 thêm 0,8 giây, S12 thêm 3 giây) vẫn là đề xuất, không nằm trong WAV và không được khóa.

## Ba lớp tài sản

Được phép dùng ngay:

- Logo gốc `docs/novixa/assets/brand/novixa-logo.png`. Không vẽ lại file.
- Tấm logo `20261023T063730Z-logo-plates/s05-s11-logo.png`.
- Tấm kết `20261023T063730Z-logo-plates/s12-cta.png`.
- Tấm chữ S03, tấm chữ S04 và sơ đồ S08 trong `20261023T070441Z-graphics/`.
- Mười hai WAV V3 và pipeline ghép ảnh với lời. Pipeline này giữ ảnh, không tạo chuyển động nhân vật.

Đã có file nhưng chưa được duyệt cho phim:

- `docs/novixa/launch/DOC-008/assets/screenshots/10-staff-pos.png`, 780×1688. Màn POS của `DEMO_PHARMACY`, giỏ trống, tạm tính 0 đ. Không có nhãn “Dữ liệu minh họa”.
- `04-inventory-stock.png`, 2880×1800. Màn tồn kho có số lượng và tên hàng. Không mở hạn dùng và không mở giá trị tồn.
- `08-reports-home.png`, 2880×1800. Trang chọn báo cáo. Có tên báo cáo “Tồn kho & giá trị” và “Sắp hết hạn sử dụng”. Không phải báo cáo đã mở số liệu.
- Chân dung `pharmacist-nova-cartoon-v3.jpg`, khung `keyframes/v7/s01-pharmacy.jpg` và storyboard `probes/character-reference-v1/`. Đây là ảnh của dự án quảng cáo, chưa duyệt cho intro.
- Ba graphics hold của quảng cáo và style proof hình học. Không dùng thay cảnh intro.

Còn thiếu:

- Footage chuyển động của cửa hàng, khách, quầy và dược sĩ cho S01, S02, S09, S10.
- Chuyển động ẩn dụ hộp, két và lịch cho S03.
- Lịch lật và nhóm hộp không mang số lô cho S04.
- Khung thiết bị S05 có POS đã được phép dùng.
- Màn đã xác minh cho “sắp hết hạn” và “giá trị hàng” ở S07. Ảnh tồn kho hiện có không chứng minh hai ý này.
- Màn báo cáo đã mở và đã được phép dùng cho S08.
- Nhạc được cấp quyền. Chính sách nhạc vẫn là không nhạc.

## Cách làm

Code và FFmpeg làm chữ, logo, sơ đồ, đường nối và motion graphics của vật thể đơn giản. Cảnh cần người hoặc không gian nhà thuốc chuyển động thì phải có footage riêng. Không phóng, không lia một ảnh đứng rồi gọi là footage. Không vẽ bảng số để thay giao diện sản phẩm. Không gọi Runway trong bước này.

| Cảnh | Lớp hình đúng | Vì sao chưa sẵn sàng |
| --- | --- | --- |
| S01 | Footage nhà thuốc | Chưa có chuyển động cửa, khách, quầy |
| S02 | Footage áp lực việc | Chưa có hành động dồn rồi dừng |
| S03 | Motion graphics | Mới có tấm chữ trong bản xem V1 |
| S04 | Motion graphics | Mới có tấm chữ, chưa có lịch |
| S05 | Logo và POS đã duyệt | Có tấm logo, chưa có POS được phép |
| S06 | UI POS đã duyệt | Ảnh POS chưa được duyệt, dữ liệu demo |
| S07 | UI tồn kho đã duyệt | Ảnh chưa duyệt và không thể hiện hạn hay giá trị |
| S08 | Sơ đồ, rồi UI báo cáo đã duyệt | Sơ đồ mới là ảnh đứng; ảnh báo cáo là trang chọn |
| S09 | Footage người | Mới có chân dung một tư thế, chưa duyệt |
| S10 | Footage toàn cảnh | Chưa có không gian nhà thuốc đã duyệt |
| S11 | Logo, có thể thêm đường nối | Tấm logo đã xem trong V1, chưa có đường nối |
| S12 | Thẻ kết | Tấm đã xem trong V1; thời lượng giữ cuối chưa khóa |

Không cảnh nào được coi là sẵn sàng để vào phim hoàn chỉnh.

## Từng cảnh

### S01 — Một ngày ở nhà thuốc

Lời mô tả mở cửa, khách vào ra, lấy thuốc, hỏi giá, điện thoại reo, rồi nói phía sau quầy là một đống việc. File WAV 17,784 giây, hết lời 16,88 giây.

Hình cần một nhà thuốc đang bắt đầu ca, không phải chân dung. Bố cục đi từ cửa sáng, vào quầy, rồi tới nhịp việc dày hơn: khách, tay lấy hàng, máy quét, điện thoại. Chữ “Một ngày ở nhà thuốc” nằm gọn, không thành danh sách tính năng. Logo chưa xuất hiện. Cảnh cắt khi một hộp thuốc chiếm khung, dẫn sang S02.

Đây là footage. Ảnh v7 và chân dung không đủ, và không được lia thành cảnh. Asset cần tạo là một chuỗi hành động đã duyệt: cửa mở, người ở quầy, khách, lấy hàng. Nghiệm thu: nhịp tăng trong chính cảnh, lời không đổi.

### S02 — Bận nhưng chưa kiểm soát

Lời đi từ bán nhanh, kiểm hàng, nhân viên hỏi, nhà cung cấp gọi, rồi dừng ở câu “Nhà thuốc mình đang thực sự kinh doanh thế nào?”. File 19,944 giây, hết lời 19,02 giây.

Hình là cùng nhà thuốc, việc chồng lên nhau: hóa đơn dài, hộp xếp cao, người xoay giữa quầy và điện thoại. Bốn chữ Bán hàng, Tồn kho, Nhân sự, Khách hàng có thể hiện như việc đang xảy ra, không phải bốn tính năng. Câu hỏi cuối để khung thưa và chậm. Một hộp trượt khỏi chồng hàng để sang S03.

Đây là footage. Chưa có. Không dùng bốn nhãn chữ đứng yên để thay cảnh bận. Nghiệm thu: câu hỏi có khoảng nghỉ sẵn trong lời, cảnh không biến thành menu sản phẩm.

### S03 — Hàng nằm yên là vốn

Lời nói hàng bán nhanh và hàng nằm yên, rồi “tiền thì đã đi trước”. File 14,784 giây, hết lời 13,90 giây. Bản xem V1 đang giữ tấm “Hàng tồn kho cũng là vốn”.

Bước tiếp theo là motion graphics, không cần người ở mọi khung. Kệ có vài hộp không nhãn thuốc, không hàm lượng, không số lô. Một phần hộp đứng yên trong khi lịch chỉ lật trang trắng. Nhóm hộp đứng yên đổi thành một khối đặt lên chiếc két không có số tiền. Chữ đúng câu trên hình. Logo chưa cần chiếm giữa. Khóa két xoay thành vòng tròn để sang S04.

Không lấy số doanh thu, phần trăm tiết kiệm hay bảng tồn của quảng cáo. Style proof hình học không phải cảnh này. Nghiệm thu: người xem đọc được “hàng là vốn” mà không thấy số liệu.

### S04 — Hạn dùng không chờ trí nhớ

Lời nói kiểm tra hàng mới, theo dõi hàng trên kệ, chú ý hàng sắp hết hạn, rồi nghỉ trước “đúng không?”. File 13,417 giây, hết lời 12,52 giây, nghỉ đo được 0,66 giây. Bản xem V1 đang giữ tấm chữ.

Motion graphics: lịch lật trang trắng, không in ngày tháng trông như hạn thật. Hộp không nhãn được chia ba nhóm bằng vị trí, không bằng số lô. Một vòng quét làm sáng nhóm cần xem. Người chỉ xuất hiện nếu đã có footage đã duyệt; không bắt buộc để cảnh đúng. Vòng quét mở thành khung thiết bị để sang S05. Chữ: “Theo dõi tồn kho · Chú ý hạn dùng”.

Không bịa lô, hạn hoặc tên thuốc. Không dùng bảng tồn mô phỏng. Nghiệm thu: câu hỏi không bị cắt, không có dữ liệu hạn trông như dữ liệu thật.

### S05 — NOVIXA xuất hiện

Lời giới thiệu lý do NOVIXA ra đời: tổ chức việc, không biến chủ nhà thuốc thành người ngồi nhìn màn hình. File 15,288 giây, hết lời 14,38 giây. Tấm logo đã có đúng hai dòng chữ và đã được phép dùng. Cảnh này không nằm trong bản xem V1 vì còn thiếu POS.

Bố cục: nền giấy thương hiệu, logo nguyên file, câu “Nền tảng vận hành nhà thuốc thông minh”, và một khung thiết bị. Bên trong khung chỉ được là POS đã duyệt. Khi chưa duyệt, khung để trống hoặc chỉ còn mặt brand, không dán ảnh `10-staff-pos.png`. Không vẽ một màn POS giả. Chuyển cảnh giữ logo rồi mở sang thao tác bán hàng khi S06 được phép.

Nghiệm thu: logo không méo, chữ hình là NOVIXA, giọng vẫn là Nôvixa, không phóng đại sản phẩm.

### S06 — Bán hàng tại quầy

Lời nói bán hàng, ghi nhận và theo dõi hoạt động quầy. File 15,504 giây, hết lời 14,60 giây. Chữ: “Bán hàng”, “Ghi nhận dữ liệu”.

Ảnh POS hiện có là giao diện thật, trạng thái trong hồ sơ vẫn là đề xuất. Màn ghi `DEMO_PHARMACY`, ca `SH-000001`, giỏ trống, tạm tính 0 đ. Ảnh không mang nhãn “Dữ liệu minh họa”. Vì thế cảnh chưa được dựng.

Nếu sau này được duyệt, ảnh chỉ được dùng kèm nhãn đó suốt thời gian UI xuất hiện, đặt trong khung quầy, và không thêm đơn hàng hay số tiền. Một đường đồ họa có thể nối thao tác bán sang chữ “dữ liệu”, nhưng đường đó không được chứa số mới. Nếu ảnh bị từ chối, cảnh tiếp tục chặn cho đến khi có bản ghi màn hình POS khác đã xác minh.

Nghiệm thu: chỉ UI đã được phép, không tự tạo số liệu.

### S07 — Tồn kho

Lời hỏi ba việc: còn bao nhiêu, sắp hết hạn, và giá trị hàng trên kệ. File 17,784 giây, hết lời 16,88 giây.

Ảnh `04-inventory-stock.png` là màn Kho hàng thật, chưa được duyệt. Ảnh cho thấy mã, tên hàng kèm hàm lượng và số lượng. Ảnh không mở danh sách sắp hết hạn và không mở giá trị tồn. Tên hai báo cáo đó chỉ nằm trên trang chọn báo cáo ở S08. Không được nói ảnh này đã thể hiện hạn dùng hoặc giá trị.

Phần đầu cảnh có thể là hộp không nhãn xếp nhóm, làm bằng code. Phần UI chỉ được gắn sau khi từng vùng được duyệt. “Còn bao nhiêu” mới có ảnh ứng viên. “Sắp hết hạn” và “giá trị hàng” vẫn thiếu một màn đã xác minh. Không vẽ bảng giá trị để lấp chỗ trống.

Nghiệm thu: mỗi câu hỏi trên hình phải trỏ tới một vùng UI đã được phép, không có giá trị bịa.

### S08 — Dữ liệu nối lại

Lời nói dữ liệu giúp nhìn việc rõ hơn, và phần mềm không quyết định thay người. File 16,824 giây, hết lời 15,92 giây. Bản xem V1 giữ sơ đồ đứng “Bán hàng / Tồn kho / Báo cáo”.

Nửa đầu làm bằng motion graphics: ba nhãn tách nhau, rồi các đường nối lại thành một sơ đồ. Đó là hình cho “từ thao tác rời rạc đến vận hành có hệ thống”. Nửa sau, nếu có màn báo cáo, phải là màn đã duyệt. `08-reports-home.png` chỉ là trang chọn báo cáo, chưa được duyệt, và không được trình bày như một báo cáo đã có số. Có thể để trống nửa sau cho đến khi có ảnh đúng hoặc người dùng chấp nhận chỉ dừng ở trang chọn, không hiện số.

Đồ họa lùi ở cuối để nhường người ở S09. Nghiệm thu: không có câu hình nào nói phần mềm tự quyết định.

### S09 — Người ở phía trước

Lời nói dược sĩ cần tư vấn, khách cần được lắng nghe. File 14,928 giây, hết lời 14,06 giây. Chữ: “Vận hành tốt hơn. Phục vụ tốt hơn.”

Hình là một hành động: dược sĩ nhìn khách và trao đổi, nhân viên tư vấn ở quầy, các lớp chữ việc mờ và lùi ra sau. Chân dung một tư thế không phải cảnh này. Storyboard tám khung chỉ là tham chiếu, chưa phải keyframe sản xuất. Không đưa POS hay sơ đồ trở lại giữa cảnh.

Cần footage đã duyệt của hai người trong cùng không gian. Nghiệm thu: người là chủ thể, cảnh không thành demo tính năng.

### S10 — Nhà thuốc rõ hơn

Lời nói nhà thuốc không cần thật lớn, nhưng cần biết mình có gì và việc nào ưu tiên. File 16,104 giây, hết lời 15,22 giây. Ba dòng chữ: “Rõ ràng hơn”, “Chủ động hơn”, “Có hệ thống hơn”.

Hình là toàn cảnh rộng hơn S01: quầy, kệ và lối đi trong cùng một khung, việc đã được xếp gọn hơn cảnh S02. Ba dòng chữ nằm trong không gian, không thay cho không gian. Không có mũi tên tăng trưởng, không có số. Cảnh gom về logo để sang S11.

Chưa có toàn cảnh đã duyệt. Ba dòng chữ tự vẽ không đủ nghiệm thu của kịch bản. Nghiệm thu: không có “hiệu quả thần kỳ” và không có số tăng.

### S11 — Định vị

Lời đọc đúng câu định vị và nói công nghệ phải có ích trong việc hằng ngày. File 15,912 giây, hết lời 15,04 giây. Tấm logo đã có, đã dùng trong bản xem V1, và khớp hai dòng chữ. Logo không được vẽ lại.

Tấm đứng đã truyền được câu chữ, nhưng kịch bản còn muốn đường nối dữ liệu và khung UI thật xen kẽ. Đường nối có thể làm bằng code quanh logo. Khung UI chỉ được thêm khi ảnh UI đã duyệt; trước đó không vẽ màn hình giả. Chuyển cảnh chậm để vào thẻ kết. Thời lượng cảnh vẫn theo WAV, không khóa thêm giây.

Nghiệm thu: đúng câu định vị, logo nguyên file.

### S12 — Thẻ kết

Lời mời tìm hiểu và đọc địa chỉ. File 19,872 giây, hết lời 18,96 giây. Tấm kết đã có logo, câu định vị và `novixa.vn`, đã dùng trong bản xem V1.

Tấm đứng đạt chữ cần có. Có thể cho logo và địa chỉ hiện chậm bằng code, vẫn trong đúng độ dài WAV. Kịch bản muốn khung cuối giữ sau khi lời hết. Độ dài phần giữ đó chưa được duyệt, nên không thêm vào bản sau và không khóa. Không thêm số điện thoại, giải thưởng hay cam kết. Địa chỉ trên hình vẫn là `novixa.vn`.

Nghiệm thu: địa chỉ đọc được, nằm trong vùng an toàn, không có nội dung mới ngoài kịch bản.

## Asset cần tạo, theo mức chặn phim

1. Quyết định dùng hay không dùng ba ảnh DOC-008. Đây không phải asset vẽ mới, nhưng đang chặn S05, S06, S07 và nửa báo cáo của S08. Nếu dùng, nhãn “Dữ liệu minh họa” phải hiện suốt lúc UI có mặt.
2. Motion graphics S03: hộp không nhãn, két không số, lịch trang trắng. Dùng cho S03. Nghiệm thu khi ẩn dụ vốn đọc được và không có số liệu.
3. Motion graphics S04: lịch trắng, ba nhóm hộp, vòng quét. Dùng cho S04. Nghiệm thu khi không có lô hoặc hạn trông như dữ liệu thật.
4. Motion graphics S08: ba nhãn từ tách tới nối. Dùng cho nửa đầu S08. Nghiệm thu khi không có số báo cáo và không nói phần mềm tự quyết định.
5. Footage S01 và S02: cửa, quầy, khách, nhịp việc dồn rồi dừng. Đây là khoảng trống lớn nhất của phần mở. Chưa tạo cho đến khi phương án hình được duyệt. Không gọi dịch vụ video trong bước này.
6. Footage S09 và S10: dược sĩ đang tư vấn, rồi toàn cảnh nhà thuốc. Cùng điều kiện với mục 5.
7. Màn đã xác minh cho hạn dùng, giá trị tồn và một báo cáo đã mở. Ảnh hiện có không đủ ba việc này. Chỉ đi tìm hoặc chụp sau khi người dùng cho phép dùng UI demo, hoặc chỉ định một bản ghi khác.
8. Đường nối quanh logo S11 và nhịp hiện của thẻ S12. Làm bằng code sau các mục trên. Không khóa giây giữ cuối.

## Điều kiện trước khi render tiếp

- Mỗi cảnh đưa vào bản xem mới phải có nguồn hình đúng lớp: footage, UI đã duyệt, hoặc motion graphics không chứa số liệu giả.
- Ảnh DOC-008 chỉ vào phim sau một quyết định riêng. Ảnh tồn kho không được ghi chú là ảnh hạn dùng hoặc ảnh giá trị.
- Logo dùng file gốc. Không sửa `script.md`, không đổi WAV V3, không đổi `Nôvixa`.
- Không thêm nhạc.
- Không khóa khổ hình và không khóa các giây giữ hình.
- Bản render tiếp vẫn để `finalVideo` null cho đến khi đủ cảnh và được nghiệm thu.

## Bước đáng làm tiếp

Quyết định có tác dụng lớn nhất là duyệt hoặc từ chối ba ảnh DOC-008, kèm cách xử lý phần ảnh không chứng minh được: S07 chưa có hạn dùng và giá trị, S08 chưa có báo cáo đã mở.

Việc sản xuất làm được ngay, không cần dịch vụ trả phí và không đụng ảnh chưa duyệt, là motion graphics S03 và S04 theo đúng WAV V3, trong thư mục mới. Bản xem V1 giữ nguyên. Việc đó vẫn chưa phải phim.
