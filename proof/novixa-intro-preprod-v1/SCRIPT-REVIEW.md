# NOVIXA — Video giới thiệu sản phẩm

Trạng thái: **PRE-PRODUCTION — READY FOR SCRIPT REVIEW**

Chưa READY FOR PRODUCTION. Chưa dựng video, chưa xuất MP4.

Thời lượng mục tiêu 53 giây. Hai khung khi sản xuất: 1080×1920 và 1080×1350. Cùng lời thoại, hai bố cục riêng.

## 1. Mạch kịch bản

Giữ mạch vấn đề → chuyển giải pháp → ba bằng chứng giao diện → lời kết.

| Cảnh | Khung đề xuất | Lời đọc | Chữ | Hình |
| --- | --- | --- | --- | --- |
| 1 | 0:00–0:05 | Doanh thu cao… chưa chắc nhà thuốc đã kinh doanh hiệu quả. | DOANH THU CAO… rồi CHƯA CHẮC LỢI NHUẬN TỐT | Nền tối, chữ lớn. Cụm thứ hai vào sau nhịp dừng. Không logo, không nhân vật. |
| 2 | 0:05–0:13 | Bởi đằng sau những con số doanh thu là hàng tồn, giá vốn và vô số khoản chi cần được kiểm soát. | HÀNG TỒN, GIÁ VỐN, CHI PHÍ, mỗi thẻ một nhịp | Ba thẻ vào, dừng, tách. Không gắn số tiền. |
| 3 | 0:13–0:21 | Khi dữ liệu nằm rời rạc, việc điều hành dễ trở nên khó khăn. Điều bạn cần là một bức tranh vận hành rõ ràng hơn. | ĐỪNG QUẢN LÝ BẰNG CẢM GIÁC, rồi NHÌN RÕ DỮ LIỆU VẬN HÀNH | Thẻ tối thu về một điểm. Nền sang sáng. Xóa hết chữ vấn đề trước cảnh 4. |
| 4 | 0:21–0:34 | Đó là lý do NOVIXA kết hợp các công cụ vận hành nhà thuốc trên một nền tảng, từ bán hàng đến quản lý tồn kho và theo dõi dữ liệu hoạt động. | NOVIXA, rồi BÁN HÀNG, QUẢN LÝ TỒN KHO, THEO DÕI DỮ LIỆU VẬN HÀNH | Ba vùng giao diện thật, lần lượt, có nhãn Dữ liệu minh họa. |
| 5 | 0:34–0:44 | Khi thông tin được tổ chức rõ ràng, chủ nhà thuốc có thêm cơ sở để theo dõi tình hình, nhận biết những điểm cần chú ý và đưa ra quyết định. | NHÌN RÕ HƠN, rồi CHỦ ĐỘNG HƠN | Ba vùng vừa xem xếp lại thành một bố cục. Không biểu đồ lợi nhuận. |
| 6 | 0:44–0:53 | NOVIXA — nền tảng vận hành nhà thuốc thông minh. Khám phá NOVIXA tại novixa.vn. | Logo, tagline, novixa.vn | Logo và chữ lần lượt. Giữ khung đến hết. |

Cảnh 3 trong bản gửi là 6 giây. Câu tương đương đã render ở marketing V2 dài 8,4 giây. Đề xuất nhích cảnh 3 lên 8 giây và lấy 2 giây từ cảnh 4. Tổng vẫn 53 giây. Đây là ước lượng từ câu đã đo, chưa phải file đọc của kịch bản này.

## 2. Giao diện được dùng

Nguồn: `docs/novixa/launch/DOC-008/assets/screenshots/`. Manifest ghi chụp 2026-07-04, app admin/staff, dữ liệu demo. Cả bộ là ảnh ngang 2880×1800, trừ POS nhân viên là màn điện thoại.

| Lời thoại | File | Vùng được phép | Không lấy |
| --- | --- | --- | --- |
| Bán hàng | `10-staff-pos.png` | Cả màn: tìm sản phẩm, quét mã, giỏ trống, tạm tính 0 đ. Đầu trang đã ghi DEMO_PHARMACY. | `05-sales-pos.png` có dải “Thu ròng ca: 212.863.831,93 đ”. Không đưa dải này lên hình. |
| Quản lý tồn kho | `04-inventory-stock.png` | Crop đã kiểm ở pilot: mã và tên sản phẩm, từ `(540, 580)` đến `(1500, 1320)`. | Cột đơn vị và các cột bên phải bị cắt nếu lấy quá hẹp. |
| Theo dõi dữ liệu | `08-reports-home.png` | Hai hoặc ba thẻ nguyên: “Tồn kho & giá trị”, “Sắp hết hạn sử dụng”. Đây là tên báo cáo, không phải số kết quả. | Không crop sát cạnh phải. Lần crop thử trước đã cắt chữ. Không dùng biểu đồ doanh thu trên `01-dashboard.png`. |

Các file `02`, `03`, `06`, `07`, `09` có thật nhưng không nằm trong ba ý của lời đọc. Không thêm vào video này.

Mọi vùng có số hoặc tên hàng demo phải có nhãn **Dữ liệu minh họa** trong suốt thời gian ảnh nằm trên hình.

## 3. Logo, câu chữ, website

| Hạng mục | Tài sản | Ghi chú cho duyệt |
| --- | --- | --- |
| Logo | `docs/novixa/assets/brand/novixa-logo.png` | Giữ nguyên file. Logo in sẵn dòng tiếng Anh “SMART PHARMACY SOLUTIONS”. |
| Tagline trong kịch bản | Nền tảng vận hành nhà thuốc thông minh | Khác north star trong `docs/novixa/01-company/operational-positioning-v1.md`: “Nền tảng vận hành nhà thuốc vì sức khỏe cộng đồng”. |
| Website | `novixa.vn` | Đúng tên miền trong DOC-004. |

Cần chủ sản phẩm chọn một tagline trước khi dựng. Không sửa file logo.

## 4. Lời kêu gọi

Trang `novixa-site/src/pages/vi/lien-he.astro` có form gửi tới `care@novixa.vn`. Nút trên site là “Đăng ký demo”, không phải “đăng ký trải nghiệm”. Form không chứng minh đã có người nhận và một buổi trải nghiệm được sắp.

Vì vậy cảnh 6 trong bản dựng, khi được duyệt sản xuất, dùng:

“Khám phá NOVIXA tại novixa.vn.”

Không đưa số điện thoại, Zalo, QR hay đường dẫn form lên video cho đến khi chủ sản phẩm xác nhận quy trình tiếp nhận.

## 5. Giọng đọc

Công cụ sẵn có là `edge-tts`, giọng `vi-VN-NamMinhNeural`. File thăm dò `proof/novixa-motion-pilot-001/assets/voice/probe-novixa.mp3` có token `WordBoundary:NOVIXA`, tức một từ, không tách sáu chữ cái.

Chưa nghe file đó. Chưa chốt cách đọc. Trên hình chỉ viết NOVIXA. Không dựng lời đọc hoàn chỉnh ở bước này.

Chưa có file nhạc có quyền sử dụng trong `docs/novixa`. Bản này không nhạc, trừ khi chủ sản phẩm chỉ định một file được phép dùng.

## 6. Việc cần duyệt trước khi sản xuất

1. Chọn tagline: “thông minh” hay “vì sức khỏe cộng đồng”.
2. Chấp nhận chữ “CHƯA CHẮC LỢI NHUẬN TỐT” trong khi lời đọc nói “kinh doanh hiệu quả”.
3. Chấp nhận “CHỦ ĐỘNG HƠN” như cảm giác hình, không phải kết quả đo được.
4. Giữ CTA “Khám phá NOVIXA tại novixa.vn”, hay xác nhận form demo đang được tiếp nhận rồi mới đổi câu.
5. Nghe thử cách đọc NOVIXA và nhịp cảnh 3 trước khi xuất hình.
6. Chốt ba vùng giao diện ở mục 2, gồm nhãn “Dữ liệu minh họa”.

## 7. Bảo toàn

Chưa sửa V1, V2, Famixa, shot E2E, logo hay ảnh chụp nguồn. Tài liệu này chỉ nằm trong `proof/novixa-intro-preprod-v1/`.
