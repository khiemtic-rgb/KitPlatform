# Video đầu tay NOVIXA — cổng dừng

Chưa có video đầu tay. `finalVideo` là null. Shot plan đề xuất nằm ở `shot-plan.proposed.json` trong dự án quảng cáo: sáu cảnh, 25 giây, mọi cảnh `proposed`. `end-card.mp4` của lần `produce` trước chỉ là thẻ chữ 3 giây, `isFinalCommercial: false`.

Trạng thái trong `commercial.json`: `firstVideo.status` = `BLOCKED`, dừng ở cổng asset hình. Không có request provider. Ba biến `RUNWAY_API_KEY`, `VEO_JOIN_FILE`, `FAL_KEY` đều trống.

## Cổng

| Cổng | Kết quả | Bằng chứng |
| --- | --- | --- |
| A — Asset hình | Chưa đạt | Đã có storyboard 8 khung chờ duyệt. Nghiêng, đưa tay và nắm hộp cần tạo lại. Chưa đưa vào production |
| B — Clip tương tác | Chưa đạt | Không có file video của hành động lấy hộp |
| C — Video đầy đủ | Chưa đạt | Không có lời, nhạc, và không có phim để xem |
| D — Dùng lại cho tập sau | Một phần | Số liệu, nhãn và màu đã khóa. Nhân vật chưa đủ góc để dựng tập mới |

Pipeline nội bộ vẫn trả gate B vì keyframe đang `AWAITING_REVIEW`. Kể cả khi được duyệt, gate tiếp theo vẫn cần credential và video tham chiếu `docs/kit-video-production/references/novixa-gemini-reference.mp4`, file này chưa có.

## Từng nhánh

Nhân vật: chân dung v3 vẫn là ảnh chuẩn đối chiếu. Storyboard 8 khung nằm ở `probes/character-reference-v1/`, trạng thái chờ duyệt, chưa gắn vào cảnh production. Nghiêng, đưa tay, nắm hộp và chỉ nhãn cần tạo lại. Chữ trên hộp trong storyboard không phải dữ liệu.

Bối cảnh và đạo cụ: kệ, quầy và bao bì nằm trong tấm v3 và bản ghép v7. Biển kệ và mặt hộp v5 do code vẽ. Ba tên mockup lấy từ `demoData`: AMPICILLIN, HOẠT HUYẾT, VITAMIN C. Vỉ thuốc chưa có nhãn kiểm soát. POS thật với đúng bộ số chưa có. Logo là file thương hiệu, không vẽ lại.

Graphics: Segoe UI. Năm thẻ vận hành và bảng tồn v7 đọc cùng `demoData`. Chú thích nhỏ là `Dữ liệu mô phỏng`. Các khung này là ảnh tĩnh chờ duyệt, chưa phải cảnh trong video.

Chuyển động: s01 trỏ preview v7, chưa duyệt. s02, s03, s04 vẫn trỏ keyframe v2 của kiểu bán hiện thực đã loại. Ghi chú hold đã được sửa để các file đó không bị gửi. Không dịch chuyển ảnh tĩnh.

Âm thanh: `audio.voice` và `audio.music` đều null. Chưa thu lời. Chưa có nhạc được cấp quyền. Lời đề xuất, chưa ghi âm: “Mỗi hộp trên kệ đều là vốn đang nằm chờ. Hôm nay nhà thuốc có 214 mặt hàng. Hàng tồn kho cũng là vốn.” Số 214 lấy từ `demoData` và là số mô phỏng.

## Tiêu chí xem

| Tiêu chí | Kết quả |
| --- | --- |
| Nhận diện nhiều góc | Chưa đạt. Mới một góc |
| Môi trường và asset | Chưa duyệt. v7 đang chờ xem |
| Chữ, nhãn, số | Khớp một nguồn trên các khung tĩnh. Chưa thấy trong video |
| Chuyển động tự nhiên | Chưa có clip |
| Nhất quán giữa cảnh | Chưa đạt. s02–s04 không cùng kiểu với v3 |
| Lời và nhạc | Chưa có |
| Codec của phim hoàn chỉnh | Chưa có file để đo |
| Dùng lại cho tập sau | Graphics dùng lại được. Nhân vật thì chưa |

`python -m unittest tests.test_system -q` kiểm tra cổng kỹ thuật, không thay cho việc xem phim.

## Để tiếp tục

1. Có ảnh thật cho bảy tư thế còn thiếu, tạo bằng công cụ ảnh có ảnh v3 làm mẫu nhận diện. Lưu thư mục probe mới. Không ghi đè v3.
2. Người dùng duyệt hoặc từ chối các ảnh đó và preview v7.
3. Cho phép chi phí và cấp `RUNWAY_API_KEY` nếu vẫn dùng `gen4_turbo`, hoặc chọn `veo` / `fal` và cấp đúng biến của adapter đó. Repo không ghi đơn giá, nên không ước lượng số tiền.
4. Sau đó mới gửi một clip lấy hộp, khung đầu và khung cuối đã có tay. Graphics, lời và end card ghép sau bằng Pillow và FFmpeg. Không gửi khi chưa có phép.
