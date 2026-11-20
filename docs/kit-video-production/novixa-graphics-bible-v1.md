# Graphics Bible — NOVIXA

Mọi số trên thẻ và bảng đọc từ `demoData` trong `commercial.json`. Không chép một bộ số thứ hai vào từng cảnh.

## Chữ

Font đang dùng là Segoe UI và Segoe UI Bold vì máy có sẵn và có dấu tiếng Việt. Inter chưa có trên máy, không tự cài. Một thành phần dùng tối đa hai độ đậm: thường cho nhãn và đơn vị, đậm cho giá trị.

Chú thích trên màn số là dòng nhỏ `Dữ liệu mô phỏng`. Không dùng câu đó làm tiêu đề. Trường `label` cũ `Dữ liệu minh họa` vẫn nằm trong dữ liệu cho đường UI showcase, không vẽ thành tiêu đề của quảng cáo.

## Màu

Ngà `#F7F9F7`, mint `#BFE7D8`, teal `#168C88`, xanh nhạt `#DCEBFA`, mực `#002060`. Cam `#F5A65B` chỉ là dải nhận diện của một sản phẩm. Ánh sáng cảnh nhân vật là trắng, không giờ vàng.

## Thành phần

Biển kệ là một dòng, gắn mép kệ, không kéo thành banner. Ba biển đang dùng: THUỐC KÊ ĐƠN, THUỐC KHÔNG KÊ ĐƠN, THỰC PHẨM BẢO VỆ SỨC KHỎE.

Mặt hộp có dải màu, dòng nhóm và tên. Không in `MOCKUP` lớn. Trạng thái mockup nằm trong metadata.

Năm thẻ vận hành cùng cấu trúc nhãn, giá trị, đơn vị. Thẻ doanh thu rộng hơn vì chuỗi số dài hơn. Đơn vị không dính vào số.

Bảng tồn có ba cột: Sản phẩm căn trái, Số lượng căn phải, Hạn sử dụng căn trái. Header có nền riêng. Đường kẻ mảnh. Hàng xen kẽ rất nhẹ. Tổng bằng `inventoryOnShelf`.

Biểu đồ đơn theo ngày, nếu dùng, lấy `orderSeries`. Cột cuối phải bằng `orders`.
