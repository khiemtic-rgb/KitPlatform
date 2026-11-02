# Quy trình hình ảnh dùng chung

Áp dụng cho mọi video. Không viết painter riêng cho từng cảnh.

## 1. Khai báo ý nghĩa

Trong `params.intent` của cảnh: `idea`, `subject`, `focal`. `storyGoal` là mục đích. `action` là hành động. `visualMetaphor` là quan hệ giữa các bước. `onScreenText` là chữ bắt buộc phải đúng.

`subject` chọn cách dựng: `still` và `goods` cần ảnh; `portrait` là người ở tư thế tĩnh; `transaction` cần ảnh giao diện; `relation` là sơ đồ minh họa; `lockup` cần logo. Không mặc định mọi cảnh là thẻ chữ.

Nếu người trong cảnh phải quan sát, quyết định, đánh dấu hoặc chỉ tay, khai báo `params.picture.actions`. Ảnh tĩnh không có `actions` tương ứng thì cảnh bị `pose_unsupported`.

## 2. Đưa tài sản vào registry sẵn có

Thêm một dòng trong `manifest.assets`. Không tạo registry thứ hai.

Dòng được dùng trong phim cần `approval: approved` và `usageRights` là một câu cụ thể. `proposed`, `demoData: true`, nhãn có chữ mockup, hoặc quyền để trống đều bị từ chối. Không tự đổi `approval`.

Gắn id vào `scene.assets`, hoặc `params.assetId` / `params.logoAssetId`.

`select_scene_asset` chỉ lấy asset đã gắn và đúng vai trò của cảnh. `setting` là ảnh nền. `product` là hàng. `product_ui` là giao diện. `character` hoặc `portrait` là người. `brand_logo` là logo. Asset sai vai trò không được chọn thay.

Hash là sha256 của nội dung file. Hash lệch thì dừng và không tự ghi hash mới. Hash chưa có trong registry thì `integrity.verified` là false. Cảnh `approved` không được render thương mại khi thiếu hash. Cảnh `style_proof` có thể xem trước, nhưng đó không phải xác minh toàn vẹn. Không tự thêm sha256, không đổi approval.

`ui_evidence` chỉ kiểm phép tính của một màn bán nếu scene khai số. Phép tính khớp không phải approval của ảnh giao diện. Giỏ trống, chữ mẫu, hoặc thiếu đơn giá thì không phải bằng chứng sản phẩm.

## 3. Chọn phương pháp rồi mới vẽ

`select_method` đứng giữa kế hoạch và painter.

- Ảnh bối cảnh, sản phẩm hoặc người đã duyệt: dùng chính tệp đó. Có thể ghép với chữ do code vẽ.
- Thiếu những ảnh đó: dừng. Provider ảnh tĩnh hiện chỉ trả `NOT_INTEGRATED`, không gửi request, không tính phí, và không được thay bằng thẻ chữ. `execute_still` chỉ ghi `success` khi một tệp ảnh cục bộ thực sự tồn tại. Fixture và mock không được ghi là ảnh do provider tạo. Candidate mới có `approval: proposed` và không đi vào commercial render.
- Giao diện chính thức thiếu ảnh đã duyệt: dừng. Mockup và ảnh demo không được chọn làm giao diện thật.
- Chữ, số, logo và sơ đồ cần chính xác: vẽ bằng code. Logo vẫn phải là tệp đã duyệt.
- Biểu đồ có độ dài khác nhau mà chưa được xác nhận: không đưa vào commercial render.
- Ảnh chân dung không có `actions` phù hợp thì không được dựng thành người đang thao tác.

`review_layers` tách lớp kỹ thuật và lớp nghĩa. Lớp kỹ thuật đạt không làm lớp nghĩa đạt. `attempt_round` ghi một lần thử. Hết ba lần, hoặc lần sau lặp cùng phương pháp, thì dừng.

## 4. Đọc cổng trước khi vẽ

`visual_plan` tạo bản mô tả. `production_gate` trả `PASS`, `FAIL` hoặc `BLOCKED`.

- `BLOCKED`: thiếu ảnh, quyền, tư thế, hoặc chưa có người xem phần nghĩa. Kèm yêu cầu tài sản.
- `FAIL`: có lỗi đã kiểm được, như cột biểu đồ khác độ dài khi so sánh chưa được xác nhận.
- `PASS`: chỉ khi không còn lớp nào mở. Cảnh có chủ thể hình không tự đạt nếu chưa có đánh giá thị giác đúng đủ tiêu chí.
- `validate_project` báo trong một lượt các mã shot đã biết, gồm `ui_unpriced` và `semantic_unreviewed` khi cảnh khai chủ thể cần người xem. Hash khớp không xóa hai mã đó.
- Preview `style_proof` của một `scene_ids` không bị lỗi shot khác chặn. `semantic_unreviewed` được ghi nhận và không biến preview thành commercial render. Cảnh `approved`, hoặc lần dựng không có `preview_only`, vẫn dùng cổng của cả dự án.

Preview thử nghiệm để riêng. Không gắn vào cảnh và không tính là đầu ra đã duyệt.

## 5. Thử lại

Tối đa ba phương án. Mỗi lần phải đổi `method` theo lỗi vừa thấy. Không bỏ tiêu chí để đổi FAIL thành PASS. Hết ba lần thì dừng và xem lại tài sản hoặc cách thể hiện.

## 6. Việc máy không kết luận

Máy kiểm lề, chữ đè chữ, tương phản, nhãn mockup và so sánh chưa xác nhận. Máy không kết luận một ảnh đã đẹp, một người đã đang quyết định, hoặc một giao diện demo đã là sản phẩm thật.

`missing_glyphs` đọc cmap Unicode của font đang khai trong `visualStyle.typography`. Ký tự không có glyph thành vi phạm trước khi vẽ. Font không có cmap Unicode thì mọi ký tự đều thiếu, không được coi là đã vẽ được. Dòng `onScreenText` mà board chưa vẽ được xuống dòng trong vùng an toàn.

## Trạng thái 24/10/2026

Nhiệm vụ 58317 không có trong repository. Engine `kit_video` 1.8.0 không gọi Gemini. Ảnh tĩnh generated vẫn `not_integrated`. Catalog Content riêng có model `gemini-3.1-flash-image` (trả phí) và ghi `gemini-2.5-flash-image` là đã nghỉ; pipeline Intro không dùng catalog đó và phiên này không gọi.

`validate_project` của Intro, đọc lúc `20261024T152431Z`, báo một lượt chín mã: `semantic_unreviewed` (S01), `product_source_missing` và `semantic_unreviewed` (S03), `official_ui_missing`, `semantic_unreviewed` và `ui_unpriced` (S06), rồi `semantic_unreviewed` cho S08, S09, S12. S02, S04, S05, S07, S10, S11 không nằm trong danh sách đó. S08 đã bỏ `chart.bars` và các số mẫu. `production_gate` của S08 là BLOCKED `semantic_unreviewed`, không còn `false_comparison`. Cảnh `style_proof` dùng chữ hoặc sơ đồ có thể nhận `production_gate` PASS khi `semantic` là `not_required`. PASS đó không phải nghiệm thu hình và không phải commercial render.

S01, S09, S12 BLOCKED `semantic_unreviewed`. `pharmacy-plate`, `pharmacist-portrait` và `logo-novixa` đã có sha256 khớp file. Approval không đổi. Hash khớp chưa phải duyệt mặt hình.

S03 BLOCKED. Không có asset `role: product`. Ảnh quầy, mockup hộp và screenshot demo không được gắn vào cảnh này.

S06 BLOCKED `official_ui_missing` và `ui_unpriced`. `ui-pos-demo-cart`, `ui-pos-demo-cart-v2` và `ui-pos-demo-cart-v3` giữ `proposed`, `demoData: true`, không gắn vào cảnh. `ui-pos-demo-cart-v3` trỏ tới `20261024T143900Z-pos-s06-evidence/pos-demo-cart.png`. Bảng shot plan vẫn là hàng mẫu.

S08 BLOCKED `semantic_unreviewed`. Shot plan giữ `style_proof`. Ba nhãn là Bán hàng, Tồn kho, Hạn dùng. Không còn cột và không còn số mẫu. Người vẫn phải xem hình trước khi cảnh này được tính là đạt.

`finalVideo` vẫn null. `durationLocked` vẫn false. Tổng thời lượng cảnh khoảng 250.68 giây so với hint 180 giây; hint không bị ép.

## Pilot S06 — 25/10/2026

`ui-pos-demo-cart-v3` đã `approved`, đã gắn vào `s06-selling`, và có `visualSource: SOFTWARE_SCREENSHOT`. Painter của họ `ticket` đặt pixel của ảnh đã xác minh hash vào khung. Lớp code chỉ vẽ nhãn `Dữ liệu minh họa` một lần và các dòng `onScreenText`. Không vẽ lại giỏ. `semantic_unreviewed` vẫn mở. Không có `visualReview`. `finalVideo` vẫn null.

## Cấu hình ảnh dùng chung — 25/10/2026

Ảnh của cả park chỉ đọc một dòng Model AI: capability `IMAGE`, `providerId` `GEMINI`, `modelId` `gemini-3.1-flash-image`, `enabled`, và `credentialId`. Không có khóa riêng, không đọc model ảnh cũ, và không nhận tài liệu cấu hình thứ hai. Dòng này dùng chung cho mọi cảnh. Không có transport của host thì không gửi. `finalVideo` vẫn null.
