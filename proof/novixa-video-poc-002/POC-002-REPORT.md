# NOVIXA video POC 002 — báo cáo

Không xuất MP4. Thiếu hình chân thực được phép dùng. POC 001 không bị sửa.

## 1. Repository baseline

- Repo: `E:/KitPlatform`
- Branch: `recovery/production-source-20261021`
- HEAD: `8c59ceb4f3be537898106ec058ea547406f722e5`
- Working tree vẫn bẩn từ trước. `proof/novixa-video-poc-001/` và `MarketingLocalVideoRenderer.cs` vẫn là file chưa theo dõi.
- Không reset, stash, clean, checkout, commit, push, merge hay deploy.
- Không sửa file trong `proof/novixa-video-poc-001/`.
- Không sửa renderer và không đụng `ContentVideoService`.

Đã đọc `NOVIXA-VIDEO-POC-001-REPORT.md`, `SCRIPT.md` và `SHOT-PLAN.md`. POC 001 là video 60 giây dựng bằng chữ, thẻ màu và crop giao diện phần mềm. POC 002 không lặp cách đó.

## 2. Kịch bản cuối cùng

Bản nháp lấy từ đoạn mở của POC 001 («ba trăm triệu», «đơn ra đều», «cuối tháng vẫn không biết còn lại bao nhiêu») nghe như một luận điểm được đọc lên, chưa có giờ đóng cửa và chưa có người trong cảnh. Khóa lại lời dưới đây cho đúng chủ đề 15 giây.

Giọng trầm, chậm, như vừa kéo cửa xuống. Nghỉ ngắn sau câu đầu. Câu giữa rõ hơn, không cao giọng. Câu cuối hạ xuống.

1. Đóng cửa rồi. Hôm nay bán cũng đều.
2. Nhưng doanh thu không nói hộ hàng nào còn nằm trong kho, lô nào sắp hết hạn.
3. Muốn hết thấp thỏm, phải nhìn được những số đó.

Tự kiểm:

- Câu 1 đặt giờ đóng cửa và nỗi yên bề ngoài.
- Câu 2 nói đúng một việc doanh thu không cho biết: hàng còn nằm và lô sắp hết hạn.
- Câu 3 chỉ tới nhu cầu nhìn số, không giới thiệu tính năng.
- Không có tên khách, không có số doanh thu, không có lời hứa lợi nhuận.
- Không nhồi Tổng quan, giá vốn hay báo cáo ca vào 15 giây.
- Đọc thành tiếng khoảng 12–14 giây, cộng nghỉ ngắn vừa đủ 15 giây. Chưa thu tiếng vì chưa dựng hình.

## 3. Shot plan và asset

Bốn cảnh liền nhau. Chưa có file hình cho cảnh nào.

| Cảnh | Thời lượng | Lời | Mục đích | Chủ thể và hành động | Bố cục, góc, ánh sáng | Vào / ra |
|---|---|---|---|---|---|---|
| 1 | 0–4,5 giây | Đóng cửa rồi. Hôm nay bán cũng đều. | Mở bằng người vừa hết ngày bán. | Chủ nhà thuốc đứng sau quầy, một tay còn trên cửa hoặc trên sổ cuối ngày. Không nhìn máy. | Trung cảnh, ngang tầm mắt, đèn quầy đã dịu, phố phía sau tối hơn. | Vào từ cửa kéo xuống còn hé. Ra khi mắt người lia xuống sổ. |
| 2 | 4,5–8 giây | Nhưng doanh thu không nói hộ… | Doanh thu chỉ là một dòng. | Tay và trang sổ bán trong ngày, hoặc một tờ cộng tiền cuối ca. | Cận, hơi chếch, đèn ấm trên giấy, nền quầy mờ. | Cắt theo hướng mắt từ cảnh 1. Ra khi trang sổ hết việc phải nói. |
| 3 | 8–11,5 giây | …hàng nào còn nằm trong kho, lô nào sắp hết hạn. | Hàng trên kệ mới là phần doanh thu không kể. | Kệ thuốc thật, một hộp đưa gần hơn để thấy hạn dùng. Không có người nói. | Từ trung cảnh kệ cắt vào cận hạn dùng. Ánh sáng kệ, không flash. | Cắt khi lời chuyển từ «doanh thu» sang «hàng». Ra trên hạn dùng. |
| 4 | 11,5–15 giây | Muốn hết thấp thỏm, phải nhìn được những số đó. | Người cần thấy sổ và kệ cùng lúc. | Chủ nhìn từ sổ sang kệ, chưa mở phần mềm. | Trung cảnh rộng hơn cảnh 2, cùng ánh sáng cảnh 1. | Cắt từ hạn dùng trở lại người. Kết trên ánh mắt, không logo, không thẻ chữ. |

Asset hiện có và vì sao không dùng làm hình chính:

- `docs/novixa/launch/DOC-008/assets/screenshots/` và ảnh trong `docs/novixa/06-compliance/`: giao diện phần mềm. Nhiệm vụ không cho một màn hình chiếm vai trò hình chính của cả đoạn.
- `docs/novixa/assets/brand/novixa-logo.png`: logo, không phải cảnh.
- `client/admin/public` và `client/admin/public/content`: không có ảnh jpeg/webp.
- Không có footage quầy thuốc, chủ nhà thuốc, sổ tay hay kệ hàng kèm quyền sử dụng trong repository.

Không tải ảnh từ Internet. Không gọi Pollinations, Gemini, Runway, ElevenLabs hay Creatomate.

## 4. Thay đổi code

Không có.

`MarketingLocalVideoRenderer` đã có `FrameSequence` từ POC 001. Đường đó đủ để ghép khung nếu đã có hình. Không cần sửa renderer cho một đoạn chưa có hình.

## 5. Test

Không chạy test mới. Không encode.

## 6. Video

Không xuất file. Không có SHA-256.

## 7. QA kỹ thuật

Không áp dụng. Chưa có MP4.

## 8. QA sáng tạo

Chưa xem và chưa nghe một đoạn phim, nên không đánh giá chất lượng cảm nhận.

Kịch bản đã khóa ở mục 2. Hình thì chưa đủ để kiểm bố cục, liên tục cảnh hay cảm giác slideshow.

## 9. Chi phí

Không gọi API tính phí. Không mua credit. Không dùng credential production.

## 10. Production và POC 001

Không migration, deploy, commit, push. Không ghi đè video, script, timeline, asset hay báo cáo của POC 001.

## Khoảng trống cần để tiếp tục

Mỗi cảnh 1–4 thiếu ảnh hoặc footage.

Cần một bộ hình cùng một nhà thuốc, cùng một người, cùng một buổi tối đóng cửa:

- Trung cảnh chủ nhà thuốc sau quầy, cửa đã đóng hoặc đang đóng.
- Cận sổ hoặc tờ cộng tiền cuối ngày.
- Kệ thuốc thật và cận hạn dùng trên vỏ hộp.
- Trung cảnh người nhìn từ sổ sang kệ.

Người trong hình cần giấy cho phép sử dụng hình. Ảnh phải là tài sản của đội NOVIXA, hoặc một nguồn người dùng chỉ định và xác nhận quyền trước khi tải.

Có thể tiếp tục khi có một trong các nguồn sau, và chỉ sau khi quyền đã được xác nhận:

- Ảnh hoặc clip do chính nhà thuốc hoặc đội sản xuất quay.
- Một bộ ảnh người dùng giao, kèm xác nhận được phép dùng trong video marketing.

Cần người dùng phê duyệt nguồn đó trước khi dựng. Không dùng thẻ chữ hay khối hình để thế vào chỗ người và quầy.

## 7. Kiểm kê asset — 2026-10-22

Đã mở ảnh và ghi riêng bốn tài liệu: `ASSET-INVENTORY.md`, `SHOT-ASSET-MAP.md`, `FOOTAGE-CAPTURE-GUIDE.md`, `ASSET-PREP-REPORT.md`. Phần 1–6 ở trên giữ nguyên.

Đã duyệt cho cảnh giao diện: `01-dashboard.png` (Tổng quan, doanh thu hôm nay và chip sắp hết HSD, dữ liệu `DEMO_PHARMACY`). `08-reports-home.png` chỉ là phương án phụ vì các thẻ chưa có số đã chạy. Logo là nốt kết.

Ảnh about của storefront, kể cả kệ thuốc, ở trạng thái `RIGHTS_UNVERIFIED`. Không có video trong `docs/` và `client/`.

Kết luận kiểm kê: `BLOCKED — WAITING FOR LICENSED FOOTAGE`. Điều kiện mở bước dựng nằm ở cuối `ASSET-PREP-REPORT.md`.

## 8. Hướng 3D stylized — 2026-10-22

Người dùng chọn hoạt hình 3D stylized thay cho footage thật. Đã kiểm ffmpeg, Pillow, edge-tts, onnxruntime, Blender, torch, diffusers và các thư mục model thường gặp. Không có công cụ cục bộ để vẽ nhân vật. Không gọi dịch vụ tạo ảnh. Chi phí 0. Không tạo MP4.

Kết luận lượt này: `BLOCKED / NEEDS REVIEW`. Chi tiết ở `STYLIZED-PIPELINE-REPORT.md`. Kịch bản mục 2 và screenshot `01-dashboard.png` vẫn là nguồn cho lần dựng sau, khi đã có bốn ảnh 3D cùng một nhân vật.

## 9. Dựng thử 3D stylized — 2026-10-22

Đã tạo `job/output.mp4`. Không sửa POC 001, renderer hay production. Kết luận chất lượng: `NEEDS REVIEW`. Không coi là đạt chỉ vì đã có MP4. Chi tiết ở `RENDER-REPORT.md`.
