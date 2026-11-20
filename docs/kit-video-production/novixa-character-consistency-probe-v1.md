# Probe nhận diện nhân vật — V1

Trạng thái `AWAITING_REVIEW`. Probe này không chứng minh nhân vật dùng lại được cho nhiều tập.

## Công cụ

Đã đọc `visual-standard-v1.json`, Character Bible, và các module trong `tools/kit-video-production/kit_video`.

Ảnh đối chiếu là `assets/pharmacist-nova-cartoon-v3.jpg`. Tấm cùng góc trong quầy là `keyframes/v3/s01-pharmacy.jpg`. Cả hai đều gần chính diện. SHA-256 của chân dung: `9f6bc1da98b24abd9adf3740667cb6b74f9b2c3d660cb690b3b89364729afb10`.

Pipeline không có model tạo ảnh. Pillow ghép chữ và chép file. `picture.py` vẽ mặt bằng ellipse cho phim hình học, không phải dược sĩ v3. `motion.py` chỉ lập request video và không được gọi.

## File đã tạo

Thư mục `probes/character-consistency-v1/`:

| Mã | Trạng thái | Cách tạo |
| --- | --- | --- |
| `probe-bust-near-front` | `ASSET_EXISTS`, `VISUAL_REVIEW_REQUIRED` | Chép nguyên byte từ chân dung v3 |
| `probe-three-quarter-left` | `BLOCKED` | Không tạo |
| `probe-three-quarter-right` | `BLOCKED` | Không tạo |
| `probe-profile` | `BLOCKED` | Không tạo |
| `probe-reach` | `BLOCKED` | Không tạo |
| `probe-grasp` | `BLOCKED` | Không tạo |
| `probe-lift` | `BLOCKED` | Không tạo |
| `probe-point` | `BLOCKED` | Không tạo |

Contact sheet: `probes/character-consistency-v1/contact-sheet.jpg`. Ô bị chặn chỉ có chữ. Không lật, không xoay, không kéo giãn.

Chân dung gốc và toàn bộ v1–v7 không bị ghi đè. Không có mục nào ở `APPROVED`.

## So sánh nhận diện

Hai file đang có — chân dung và tấm quầy — cùng một kiểu góc gần chính diện. Mắt lớn, nâu, có điểm sáng. Lông mày nhướng. Nụ cười mở. Tóc búi khối. Áo blouse trắng, áo mint, thẻ tên trống. Chất hoạt hình 3D giữ trên hai tấm này.

Chân dung cắt ở ngực, không thấy bàn tay. Tấm quầy cắt ở eo, cũng không thấy bàn tay và không có tay chạm hộp. Vì chỉ có một góc, tính nhất quán khi xoay mặt hoặc đổi tư thế chưa được chứng minh.

## Việc còn lại trước khi thử video

Cần ảnh thật cho 3/4 trái, 3/4 phải, nghiêng, đưa tay, nắm hộp, nhấc hộp và chỉ nhãn. Người xem duyệt bộ ảnh đó, rồi cho phép dùng provider. Sau đó mới thử một clip lấy hộp.
