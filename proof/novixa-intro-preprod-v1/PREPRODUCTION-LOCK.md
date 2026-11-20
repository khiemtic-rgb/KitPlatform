# NOVIXA INTRO V1 — Khóa tiền sản xuất

Trạng thái: **READY FOR OWNER APPROVAL**

Chưa READY FOR PRODUCTION. Chưa dựng video. File này không thay `SCRIPT-REVIEW.md`.

Phép đo lời đọc dùng `edge-tts`, giọng `vi-VN-NamMinhNeural`, tốc độ mặc định. File đo nằm ở thư mục tạm, không phải voice-over của phim và không được đưa vào dựng.

## 1. Đã xác minh

### Timeline gốc không khớp lời đo được

Năm cảnh trong kịch bản gửi cộng đúng 53 giây:

`5 + 8 + 6 + 13 + 10 + 11 = 53`

Cảnh 3 trong kịch bản gửi chỉ có 6 giây. File đọc câu đó dài 7,896 giây. Âm lời kết thúc ở 7,027 giây, đuôi im của file kéo đến 7,896 giây. Sáu giây cắt mất phần cuối câu.

Bảng trong `SCRIPT-REVIEW.md` ghi cảnh 6 còn 9 giây (`0:44–0:53`), trong khi đoạn chữ nói lấy 2 giây từ cảnh 4. Hai chỗ không cùng một phép tính. Chín giây không chứa nổi file cảnh 6 dài 8,256 giây và không còn chỗ giữ logo.

### Thời lượng lời đo được

| Cảnh | File | Lời còn đến | Im trong file |
| --- | ---: | ---: | --- |
| 1 | 3,984s | 3,116s | Đầu 0,25s, đuôi 0,87s |
| 2 | 5,952s | 5,100s | Đầu 0,25s, một nhịp 0,33s, đuôi 0,85s |
| 3 | 7,896s | 7,027s | Đầu 0,28s, giữa hai câu 0,99s, đuôi 0,87s |
| 4 | 9,096s | 8,232s | Đầu 0,26s, đuôi 0,86s |
| 5 | 7,824s | 6,981s | Đầu 0,29s, đuôi 0,84s |
| 6 | 8,256s | 7,411s | Đầu 0,26s, giữa câu 0,99s, đuôi 0,85s |

Cảnh 6 ở bảng này là câu “Khám phá NOVIXA tại novixa.vn.” Nếu đổi tagline hoặc đổi câu kêu gọi, số giây phải đo lại.

### Giọng mẫu NOVIXA

`proof/novixa-motion-pilot-001/assets/voice/probe-novixa.mp3` có thật: MP3, 24 kHz, mono, 1,896 giây, 11.376 byte. Biên từ ghi một token `NOVIXA`, không tách sáu chữ cái.

Môi trường này không nghe được file. Chưa có nhận xét phát âm.

### Ba ảnh được phép

| Việc | File | Kích thước | Crop đã xem |
| --- | --- | --- | --- |
| Bán hàng | `10-staff-pos.png` | 780×1688 | Cả khung. Có DEMO_PHARMACY, giỏ trống, tạm tính 0 đ, nút thanh toán. |
| Tồn kho | `04-inventory-stock.png` | 2880×1800 | `(540, 580, 1500, 1320)` đủ tiêu đề Mã SP, Tên SP và sáu dòng tên. Không có cột bị cắt. |
| Theo dõi | `08-reports-home.png` | 2880×1800 | `(500, 860, 1900, 1140)` đủ hai thẻ INV-01 “Tồn kho & giá trị” và INV-02 “Sắp hết hạn sử dụng”, kể cả câu mô tả. |

Ảnh nhân viên cao hơn tỷ lệ 9:16. Khi dựng, thu theo chiều cao để giữ nút thanh toán. Không cắt thành một dải ngang.

Crop báo cáo còn một mép của hàng thẻ phía trên. Lúc dựng, hạ cạnh trên vừa đủ để mất mép đó và vẫn giữ chữ “INV-01”.

Logo `docs/novixa/assets/brand/novixa-logo.png` còn nguyên, 1254×1254. Không sửa.

## 2. Chưa xác minh

- Cách đọc NOVIXA. Chưa nghe `probe-novixa.mp3`.
- Nhịp và chỗ nhấn của sáu câu. Mới đo độ dài, chưa nghe.
- Hộp thư form `novixa.vn/vi/lien-he/` có người nhận và có buổi demo được sắp hay không.
- Nhạc nền. Không có file nhạc được cấp quyền trong hồ sơ này.
- Bố cục điện thoại thật. Chưa có bản dựng để kiểm vùng an toàn.

## 3. Chờ chủ sản phẩm

1. Tagline trên hình và trong câu 6: “thông minh” hoặc “vì sức khỏe cộng đồng”. Logo gốc vẫn để nguyên dòng tiếng Anh.
2. Chữ “CHƯA CHẮC LỢI NHUẬN TỐT” trong khi lời nói “kinh doanh hiệu quả”.
3. Chữ “CHỦ ĐỘNG HƠN” là cảm giác hình, không phải kết quả đo được.
4. Giữ CTA “Khám phá NOVIXA tại novixa.vn”. Chỉ đổi khi xác nhận form demo đang được tiếp nhận.
5. Nghe `probe-novixa.mp3` và chấp nhận hoặc yêu cầu đọc lại trước khi dựng.
6. Chấp nhận timeline mục 4, gồm cảnh 3 dài 8 giây.

## 4. Timeline đề xuất

Giây hình tính để chứa hết lời đã đo, cộng khoảng giữ logo khoảng 4 giây sau âm cuối của cảnh 6.

| Cảnh | Bắt đầu | Kết thúc | Dài | Vì sao |
| --- | ---: | ---: | ---: | --- |
| 1 | 0 | 5 | 5 | Lời hết ở 3,1 giây. Năm giây đủ cho hai cụm chữ. |
| 2 | 5 | 12 | 7 | Lời hết ở 5,1 giây. Bảy giây cho ba thẻ. |
| 3 | 12 | 20 | 8 | Lời hết ở 7,0 giây. Tám giây chứa câu. Sáu giây thì không. |
| 4 | 20 | 31 | 11 | Lời hết ở 8,2 giây. Mười một giây cho ba vùng giao diện. |
| 5 | 31 | 41 | 10 | Lời hết ở 7,0 giây. Giữ 10 giây như kịch bản. |
| 6 | 41 | 53 | 12 | Lời hết ở 7,4 giây. Phần còn lại khoảng 4,4 giây cho logo và `novixa.vn`. |

`5 + 7 + 8 + 11 + 10 + 12 = 53`

So với kịch bản gửi: cảnh 2 bớt 1 giây, cảnh 3 thêm 2 giây, cảnh 4 bớt 2 giây, cảnh 5 giữ nguyên, cảnh 6 thêm 1 giây.

## 5. Tài sản

Được dùng:

- `docs/novixa/launch/DOC-008/assets/screenshots/10-staff-pos.png`
- `docs/novixa/launch/DOC-008/assets/screenshots/04-inventory-stock.png`, crop mục 1
- `docs/novixa/launch/DOC-008/assets/screenshots/08-reports-home.png`, crop hai thẻ INV-01 và INV-02
- `docs/novixa/assets/brand/novixa-logo.png`

Mỗi vùng giao diện mang nhãn **Dữ liệu minh họa** suốt lúc nó xuất hiện.

Không dùng:

- `01-dashboard.png`
- `05-sales-pos.png`
- `02`, `03`, `06`, `07`, `09` — có trong manifest, không thuộc ba ý của lời đọc

## 6. Điều kiện trước khi dựng

- Chủ sản phẩm duyệt mục 3 và timeline mục 4.
- Có file nghe NOVIXA được chấp nhận. Chưa có thì chưa đọc tên thương hiệu trong bản xuất.
- Motion graphics có chữ và thẻ chuyển động. Không zoom một ảnh tĩnh. Không nhân vật 3D làm hình chính.
- Mỗi nhịp một ý. Chữ lớn, đọc được trên điện thoại.
- Bản 1080×1920 và bản 1080×1350 dựng bố cục riêng.
- CTA trên hình là “Khám phá NOVIXA tại novixa.vn”, trừ khi mục 3.4 được đổi bằng xác nhận mới.
- Mỗi file xuất: 50–55 giây, H.264, AAC, giải mã hết, có người xem và nghe, có SHA-256 riêng.
- Không ghi đè V1, V2, Famixa, shot E2E, logo hay ảnh nguồn.

## 7. Kết luận

**READY FOR OWNER APPROVAL**

Hồ sơ đủ để duyệt. Chưa đủ để dựng. Phát âm NOVIXA và tagline vẫn là quyết định của chủ sản phẩm.
