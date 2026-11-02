# Tham chiếu 8 khung nhân vật

Trạng thái `AWAITING_REVIEW`. Chưa gắn vào cảnh production. Chưa gọi provider video.

Ảnh gốc được giữ nguyên tại `probes/character-reference-v1/source-storyboard.jpg`. SHA-256 `865b475eb9ccbb7dfa571bc5a206df8e57fd765f08ce3d82d8b5565a132da478`. Tám ô được cắt theo dải nền trắng đo được, không phóng, không lật, không vẽ lại. Chân dung `assets/pharmacist-nova-cartoon-v3.jpg` và keyframe v1–v7 không đổi.

Contact sheet: `probes/character-reference-v1/contact-sheet.jpg`.

## Từng khung

| Khung | File | Việc cần làm |
| --- | --- | --- |
| Gần chính diện | `f01-near-front.jpg` | Giữ để duyệt |
| 3/4 trái | `f02-three-quarter-left.jpg` | Giữ để duyệt. Không phải ảnh lật của khung 3 |
| 3/4 phải | `f03-three-quarter-right.jpg` | Giữ để duyệt. Không phải ảnh lật của khung 2 |
| Nghiêng | `f04-profile.jpg` | Cần tạo lại. Vẫn thấy hai mắt |
| Đưa tay tới hộp | `f05-reach.jpg` | Cần tạo lại. Tay đã đặt lên hộp |
| Nắm hộp | `f06-grasp.jpg` | Cần tạo lại. Bàn tay mở, chữ trên hộp bị sai |
| Nhấc hộp | `f07-lift.jpg` | Giữ tư thế để duyệt. Hộp đã rời kệ |
| Chỉ vào nhãn | `f08-point.jpg` | Cần tạo lại. Ngón chỉ vào hộp, chưa khóa vào tên |

Tám ô cùng búi tóc, mắt lớn có điểm sáng, áo blouse trắng và áo mint. Mỗi ô chỉ khoảng 248 pixel ngang, nên chưa dùng làm keyframe 1280×720.

## Chữ trên hộp

Chữ nằm sẵn trong ảnh không phải dữ liệu. Có dòng `500 mg`, tên mất dấu, và một tên không có trong `demoData`. Khi một khung được duyệt, chữ đó được che bằng mặt hộp do code vẽ: tên AMPICILLIN, không có hàm lượng. Biển kệ dùng đúng danh mục đã khóa.
