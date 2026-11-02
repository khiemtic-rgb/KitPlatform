# NOVIXA Intro — phương án sản xuất V1

Quyết định này dùng cho phim giới thiệu khoảng 198 giây, bám kịch bản hiện hành và lời đọc V3. `finalVideo` vẫn null. `durationLocked` vẫn false. Không có bản preview mới.

## Quyết định

Sản xuất phim như một câu chuyện nhà thuốc cách điệu, một ngôn ngữ hình xuyên suốt:

- Chỗ có người, dùng dược sĩ cartoon 3D và nhà thuốc sáng đã mô tả trong visual bible của quảng cáo. Không vẽ người hình khối. Không lấy một ảnh đứng làm cả cảnh.
- Chỗ nói về NOVIXA, dùng đồ họa chuyển động diễn đúng thứ tự việc: bán, ghi nhận, theo dõi tồn, nối thông tin. Giao diện chưa duyệt thì ghi `Dữ liệu minh họa` suốt lúc nó xuất hiện. Không đưa số lô, hạn, doanh số hay kết quả báo cáo như dữ liệu thật.
- S11 và S12 là logo gốc, câu định vị và `novixa.vn`. Không vẽ lại logo.

Chưa dựng toàn phim. Bước tiếp theo là một thử nghiệm 5 giây: dược sĩ với tay lấy một hộp không chữ liều, trong đúng nhà thuốc của tấm quầy v3. Thử nghiệm chỉ chạy sau hai việc: người phụ trách duyệt tạo hình này cho intro, và cho phép một lần gọi image-to-video. Đường đã khai trong quảng cáo là Runway `gen4_turbo`, 1280×720, 5 giây. Khóa chưa có. Lệnh hiện tại không gửi mạng khi keyframe chưa `APPROVED`.

Nếu thử nghiệm giữ được mặt, áo, ánh sáng và hành động đọc được, mới làm một đoạn ba nhịp: áp lực một ngày, một giao dịch thành phiếu ghi, và tấm kết. Đoạn đó đạt thì mới sản xuất 12 cảnh.

## Vì sao chọn hướng này

Người xem là chủ nhà thuốc. Lời đọc đã kể một ngày làm việc, rồi NOVIXA giúp việc đó có tổ chức hơn, rồi mời xem `novixa.vn`. Hình phải làm người đó nhận ra việc của mình và tin sản phẩm, không chỉ chứng minh một công cụ chạy được.

Cartoon 3D hợp lời này hơn quay nhà thuốc thật: phim cần ẩn dụ vốn nằm trên kệ và lịch hạn dùng, mà không được gán số của một cửa hàng. Ảnh phần mềm hiện có không chứng minh những câu đó. Ảnh tồn kho không có hạn dùng và giá trị. Ảnh báo cáo chỉ là menu. Dùng chúng như bằng chứng sản phẩm sẽ làm phim kém tin hơn.

V2 chứng minh timeline và tiếng. V3 và V4 kể được thứ tự cảnh bằng người vẽ code, nhưng hình chưa đạt mức phim giới thiệu. Bản khôi phục định hướng cho thấy đúng chất cartoon 3D trên một tấm, rồi lặp tấm đó. Lặp một ảnh không phải hành động. Tiếp tục sửa tấm đó, hoặc vẽ thêm một vòng người hình khối, không đưa phim tới chất lượng cần bán.

## Dự phòng

Nếu tạo hình cartoon 3D bị từ chối, dừng mọi việc dựng người. Phương án thay là phim đồ họa chuyển động thuần, không có dược sĩ 3D, vẫn một ngôn ngữ, vẫn lời V3, phần sản phẩm vẫn là minh họa có nhãn. Không mở phương án này song song với thử nghiệm 3D.

Nếu tạo hình được duyệt nhưng không có phép gọi dịch vụ trả phí, phim dừng ở cổng hình. Máy hiện tại có FFmpeg và Pillow, không có mesh, Blender hay model tạo khung. Phóng, lia, ghép ảnh và nội suy không được tính là thử nghiệm đạt.

## Những gì giữ

- `script.md` và 12 WAV trong `outputs/novixa-intro-film/20261023T084726Z-voice-read`. Giọng đã duyệt. Chữ đọc thương hiệu là `Nôvixa`. Chữ trên hình là NOVIXA. Cách đọc `novixa.vn` đã duyệt.
- Logo `docs/novixa/assets/brand/novixa-logo.png`.
- Cách ghép một lần PCM của 12 WAV. Bản xem sau này dùng lại cách đó. Thời lượng lời đo được 198,145 giây, chưa khóa.
- Màu và mô tả nhận diện trong visual bible: mắt lớn, tóc búi khối, áo trắng, áo mint, ánh sáng trắng, nền ngà, mint và teal.

Những file nhân vật và tấm quầy vẫn `AWAITING_REVIEW`. Chúng là ứng viên để duyệt, không phải tài sản đã duyệt. `pharmacist-nova-style.jpg`, style proof hình học, ảnh DOC-008, bảng v7 có số lượng và hạn, và nhân vật Famixa không đưa vào phim.

## Tiêu chuẩn một phim xong

Người chưa xem kịch bản, xem một lần với tiếng, phải kể lại được ba ý: ngày làm nhà thuốc bề bộn hơn vẻ ngoài, NOVIXA giúp bán và theo dõi việc chứ không thay chủ quyết định, và nơi tìm hiểu là `novixa.vn`.

Đủ khi:

- Mười hai cảnh đi một mạch. Không có cảnh nào chỉ là câu chữ, thẻ chờ, hoặc cùng một tư thế đứng.
- S01–S02 thấy việc tăng rồi ngưng ở câu hỏi. S09–S10 thấy người, không thấy một bảng tính năng.
- Nếu có dược sĩ, cùng một người qua các cảnh: mặt, búi tóc, áo trắng, áo mint. Tay làm một việc đọc được. Khách chỉ xuất hiện khi có nhân vật khách cùng ngôn ngữ.
- S05–S08 cho thấy thứ tự bán, ghi nhận, câu hỏi tồn kho, rồi thông tin nối lại. Người xem phân biệt được minh họa với phần mềm thật. Không có số liệu giả.
- Logo không méo, không có chữ NOVIXA vẽ thêm dưới khóa logo. `novixa.vn` đọc được. Câu định vị đúng câu đã giao.
- Tiếng là đúng bản V3, không cắt lời, không nhạc khi chưa có file được cấp quyền.
- `finalVideo` chỉ được gán sau khi người phụ trách chấp nhận hình. Trước đó mọi bản đều là thử.

Độ phân giải, fps và độ khớp tiếng là điều kiện kỹ thuật của bản encode. Chúng không thay cho việc xem hình.

## Kế hoạch

### Giai đoạn 0 — đã xong ở báo cáo này

Đầu ra là quyết định trên. Chưa có video mới.

### Giai đoạn 1 — duyệt tạo hình

Người phụ trách xem `keyframes/v3/s01-pharmacy.jpg` và `assets/pharmacist-nova-cartoon-v3.jpg`, rồi duyệt hoặc từ chối cho intro. Không sửa hai file. Không gọi dịch vụ.

Đạt khi có một trong hai kết quả: duyệt cặp ảnh này, hoặc từ chối và chuyển sang phương án đồ họa thuần. Không có kết quả thứ ba kiểu “dùng tạm”.

### Giai đoạn 2 — một hành động

Chỉ khi giai đoạn 1 duyệt và có phép gọi trả phí. Một clip khoảng 5 giây: dược sĩ với tay tới hộp trắng không chữ, hộp chưa rời kệ hoặc vừa rời kệ, cùng ánh sáng với tấm quầy. Không khách. Không số.

Đạt khi người xem nhận ra cùng một người và đọc được hành động với tay. Trượt nếu mặt đổi, áo đổi, ánh sáng ngả vàng, hoặc chuyển động chỉ là ảnh trượt. Trượt thì dừng 3D, không làm thêm clip.

### Giai đoạn 3 — ba nhịp

Một bản xem ngắn, không đủ 12 cảnh: áp lực công việc, một lần bán thành phiếu trắng, tấm logo và `novixa.vn`. Cùng lời V3 của các cảnh tương ứng. Cùng cách dựng với clip đã đạt.

Đạt khi ba nhịp trông là một phim, sản phẩm không bị hiểu nhầm là ảnh chụp phần mềm, và không cần chữ để hiểu việc đang xảy ra.

### Giai đoạn 4 — toàn phim

Chỉ mở khi giai đoạn 3 đạt. Dựng đủ 12 cảnh theo lời. S07 không bịa hạn và giá trị. S08 không mở một báo cáo giả. Nhạc chỉ thêm khi có file được cấp quyền. Khóa thời lượng và `finalVideo` ở cuối giai đoạn này, sau khi xem trọn.

## Rủi ro cần tránh

- Render lại đủ 198 giây trước khi có clip 5 giây đạt. V2, V3, V4 và bản khôi phục đã tốn vòng đó.
- Dựng mesh hoặc cài công cụ 3D trước khi tạo hình được duyệt. Công cụ không làm phim đạt nếu mặt chưa chốt.
- Coi ảnh DOC-008 là lời giải cho S06–S08. Ảnh chưa duyệt, và không chứng minh các câu nói về hạn, giá trị và báo cáo.
- Thêm số minh họa cho “đủ thật”. Số không có trong kịch bản làm phim kém tin.
- Chấm phim xong vì tiếng khớp và encode đúng.

## Khi nào xem lại phương án

- Tạo hình cartoon 3D bị từ chối.
- Clip 5 giây không giữ được nhân vật hoặc không có hành động đọc được.
- Đoạn ba nhịp vẫn trông như ảnh đứng kèm chữ.
- Người phụ trách yêu cầu nhà thuốc quay thật, hoặc bắt buộc dùng giao diện đã duyệt. Khi đó dừng cartoon và lập lại phạm vi hình, vì ảnh giao diện hiện có không khớp các câu S07 và S08.
- Không có phép dùng dịch vụ trả phí và không có nguồn khung hình khác được giao. Phim không có đường hình cho đến khi một trong hai thứ đó có.
