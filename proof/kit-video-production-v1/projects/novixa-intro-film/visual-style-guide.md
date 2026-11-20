# NOVIXA Intro Film — Visual Style Guide (tối thiểu)

Tài liệu này áp dụng cho Intro Film. Màu, font, vùng an toàn và kích thước khung vẫn lấy từ `manifest.json` → `visualStyle` và `format`. Không tạo bảng màu thứ hai.

Trạng thái: đề xuất để dùng cho pilot. Chưa phải duyệt hình. `finalVideo` vẫn null. `durationLocked` vẫn false.

## Tông và ánh sáng

- Nền phim: giấy ấm `#E8E2D6`, mặt giấy `#FAF7F0`, mực `#002060`, nhấn `#0090E0`.
- Ánh sáng dịu, tương phản đủ để đọc chữ mực trên giấy. Không neon, không nền đen trừ cảnh chữ đã dùng mặt mực.
- Cảm giác: hiện đại, gọn, đáng tin. Phần mềm nhà thuốc, không phải phim hoạt hình giải trí và không phải ảnh chứng từ.

## Hai lớp hình, một phim

- Cảnh kể chuyện (`pharmacy-plate`, `pharmacist-portrait`): hình stylized 3D đã duyệt. Giữ nguyên. Chưa thay.
- Cảnh giao diện (`s06-selling`): ảnh phần mềm thật, `visualSource: SOFTWARE_SCREENSHOT`. Đặt trên cùng mặt giấy ấm. Không vẽ lại POS bằng code khi ảnh đã xác minh.
- Không trộn một cảnh thành cả stylized và screenshot. Không dùng Gemini để vẽ giao diện rồi gọi đó là ảnh chụp phần mềm.

## Nhà thuốc, người, hàng

- Dược sĩ, nếu xuất hiện, cùng một người với `pharmacist-portrait`: tóc búi, áo blouse trắng, áo trong xanh. S03 của pilot này không thêm người.
- Quầy và kệ bám `pharmacy-plate`: kệ sáng, hộp trắng, ánh sáng dịu. S03 không dùng chính ảnh quầy đó thay cho hình hàng.
- Hàng hóa minh họa không có tên thuốc, giá, mã vạch, số lô hay hạn dùng đọc được.

## Chữ, logo, số

- Logo, tiếng Việt, tên thuốc, giá và tổng tiền do code vẽ từ dữ liệu đã duyệt, hoặc nằm sẵn trong screenshot đã đối chiếu.
- Không nhờ model ảnh viết chữ.
- Nhãn `Dữ liệu minh họa` xuất hiện một lần, trên vùng an toàn, không đè giỏ hàng hay tổng tiền.
- Không sửa số đã duyệt cho đẹp. Giỏ demo không phải đơn hay doanh thu production.

## Khung

- Candidate: 16:9, 1280×720. Vùng an toàn 6% mỗi cạnh. Tỷ lệ phát hành chưa khóa.
- Tiêu đề và nhãn nằm trong vùng an toàn, phía trên ảnh chính. Ảnh chính nằm bên dưới, căn giữa, không cắt mất bảng giá.

## Nguồn ảnh

| Nguồn | Nghĩa | Cách nhận |
| --- | --- | --- |
| `SOFTWARE_SCREENSHOT` | Ảnh chụp phần mềm thật | `kind: screenshot`, không phải file generated |
| `AI_RECONSTRUCTED_UI` | Giao diện vẽ lại cho rõ | Minh họa. Không khai `kind: screenshot` |
| `AI_VISUAL` | Bối cảnh, hàng, ẩn dụ | Không chữ, không số liệu dạng dữ liệu thật |
| `CODE_EXACT` | Logo, chữ, số, nhãn | Lớp code hiện có |

Ảnh AI dùng chung hướng sáng, chất liệu và bảng màu ở trên. Không viết prompt riêng từng cảnh ngoài guide này.
