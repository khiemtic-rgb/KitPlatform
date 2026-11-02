# Tham chiếu nhân vật V2

Trạng thái `AWAITING_REVIEW`. Bốn khung cần sửa chưa được tạo. Chúng không gắn vào cảnh production và không được đánh dấu `APPROVED`.

`f01`, `f02`, `f03` và `f07` giữ nguyên từ V1. Ảnh storyboard gốc và tám file V1 không bị ghi đè.

## Blocker

Pipeline không có công cụ tạo ảnh mới. Pillow không được dùng để vẽ góc nghiêng hay tư thế tay. Không lật và không kéo giãn khung V1.

Prompt của từng khung nằm trong `probes/character-reference-v2/reference.json`. Chưa gửi prompt này đi đâu.

## Bốn khung

| Khung | V2 | Vì sao V1 chưa đạt |
| --- | --- | --- |
| `f04-profile` | `BLOCKED` | Vẫn thấy hai mắt |
| `f05-reach` | `BLOCKED` | Tay đã đặt lên hộp |
| `f06-grasp` | `BLOCKED` | Bàn tay mở, chưa ôm hộp |
| `f08-point` | `BLOCKED` | Ngón chưa chỉ đúng vùng tên |

Contact sheet so sánh: `probes/character-reference-v2/contact-sheet.jpg`. Cột V2 chỉ có chữ.

Mỗi prompt yêu cầu mặt hộp trống. Tên AMPICILLIN sẽ do lớp graphics vẽ sau, không lấy chữ có sẵn trong ảnh V1.

Chưa có ảnh V2 nên chưa so được nhận diện của bốn tư thế mới. Bốn khung giữ lại vẫn là hoạt hình 3D, búi tóc, mắt lớn, áo trắng và áo mint.
