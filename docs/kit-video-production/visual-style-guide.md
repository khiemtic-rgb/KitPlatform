# Quy chuẩn hình dùng chung

Engine không gắn bảng màu của một thương hiệu. Mỗi dự án khai `visualStyle.profileId`, `palette`, font và `safeArea`. Một video dùng một profile. Không trộn ảnh khác hệ trong cùng một lần xuất.

## Art direction

- Nền giấy ấm hoặc lạnh, một màu mực, một màu nhấn, một màu phụ. Bóng đổ một hướng, xuống và sang phải.
- Nét và khối phẳng. Chi tiết đủ để đọc ở kích thước điện thoại: hộp, két, lịch, người hình học. Không thêm họa tiết chỉ để kín khung.
- Nhân vật của template `metaphor_locked_capital` (proof V1) là hình học: đầu tròn, thân một khối. Template `stock_story` (proof V2) vẽ người có tóc, mặt, áo, tạp dề và tay khớp, cùng thùng có mặt trước/hông/nắp. Đó vẫn là hình vẽ bằng code, chưa phải thiết kế nhân vật cuối của một họa sĩ.
- Một cảnh một ý. Chữ lớn, ít dòng. Chữ kinetic đi theo nhịp câu, không chạy trang trí suốt cảnh.

## Nhân vật quảng cáo NOVIXA

Đường `produce` dùng visual bible trong `commercial.json`, hiện `bible-4`. Nhân vật giữ cartoon 3D sáng của bible-3. Màu cảnh là ngà `#F7F9F7`, mint `#BFE7D8`, teal `#168C88`, xanh nhạt `#DCEBFA`, cam `#F5A65B` chỉ làm điểm nhấn. Ánh sáng trắng, không giờ vàng. Nhãn kệ, tên bao bì và màn POS do code vẽ từ `demoData`, rồi ghép theo mặt phẳng. Năm danh mục kệ là cố định. Tên AMPICILLIN, HOẠT HUYẾT và VITAMIN C là mockup, không kèm liều dùng. Nhãn đúng `Dữ liệu minh họa`. Mockup không được gọi là ảnh chụp sản phẩm.

## Chuyển động

- Vật đổi vị trí, đổi kích thước, hoặc biến từ hộp thành khối. Easing `smoothstep`, `ease_in` cho rơi và lao đi, `ease_out` cho xuất hiện.
- Chuyển cảnh có vật nối ý: một hộp chiếm khung rồi thu vào kệ; mặt khóa két giữ để cảnh sau có thể mở thành lịch. Cắt thẳng chỉ dùng khi shot plan nói `cut`.
- Không dùng slideshow ảnh tĩnh cộng lời đọc, và không dùng pan/zoom trên một ảnh làm cách diễn hoạt chính.
- UI thật, khi được phép, nằm trọn trong khung thiết bị. Không zoom cắt mất nội dung. Nhãn `Dữ liệu minh họa` hiện suốt lúc ảnh demo có mặt.

## Chữ và vùng an toàn

`safeArea` là tỷ lệ bốn cạnh. Mọi chuỗi vẽ qua `FrameContext.text` bị đẩy vào vùng đó. Nếu chuỗi rộng hơn vùng an toàn, render ghi vi phạm và QA trả FAIL.

Font của dự án NOVIXA trong lần này là Segoe UI và Segoe UI Bold trên Windows vì chúng có dấu tiếng Việt. Dự án khác khai font của máy đó. Thiếu font thì BLOCKED.

## Ảnh được phép

| Nguồn | Cách dùng |
| --- | --- |
| Người dùng đưa | Giữ nguyên file, ghi version và quyền |
| Thư viện trong repo | Trỏ đường dẫn, không sửa file gốc |
| Code vẽ | Ghi `generated`, không gọi là ảnh chụp |
| Placeholder | Chỉ khi `allowPlaceholders` và phải hiện trong báo cáo. Không gắn nhãn đã duyệt |

Ảnh AI, ảnh stock và ảnh thật không được ghép ngẫu nhiên trong một phim. Render không gọi dịch vụ ảnh. Ảnh đã tạo được đăng ký trong manifest với `source: generated` và `approval: proposed`, rồi `reference_stage` chỉ ghép file đó. Prototype nhà thuốc dùng một bộ minh họa cùng kiểu, không trộn ảnh chụp.

## Profile NOVIXA dùng cho pilot

Profile `novixa-story-v1` lấy mực navy và xanh từ logo gốc `(0, 32, 96)` và `(0, 144, 224)`, nền giấy `(232, 226, 214)`. Logo file không bị sửa. Style proof của ẩn dụ tồn kho không đặt logo lên hình.

## Những lỗi cần tránh

- Nhiều phong cách nét trong một cảnh.
- Chữ sát mép hoặc đổi câu đã duyệt cho vừa khung.
- Số liệu doanh thu, phần trăm, giải thưởng không có trong kịch bản đã duyệt.
- UI tự vẽ đóng vai sản phẩm.
- Hiệu ứng trên mọi chuyển động. SFX chỉ đánh vào hành động đã khai.
- Làm méo logo hoặc che vùng số liệu trên UI thật.
