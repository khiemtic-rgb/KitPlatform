# POC 002 — pipeline 3D stylized

Ngày: 2026-10-22. Không tạo MP4. Không gọi API tạo ảnh. Chi phí: 0.

## Kết luận

Lượt đầu dừng vì chưa có ảnh. Lượt sau đã dựng file, nhưng chưa đạt: xem mục «Lượt dựng» ở cuối file.

`NEEDS REVIEW`

Hướng 3D stylized đã được chọn thay cho footage thật. Máy này không có công cụ miễn phí hoặc cục bộ để vẽ bốn cảnh 3D cùng một nhân vật. Dừng trước khi dựng. Không thay bằng khối hình, không zoom một ảnh giao diện, không tái tạo dashboard bằng AI.

## Công cụ đã kiểm

| Công cụ | Đường dẫn / phiên bản | Dùng lại được không |
|---|---|---|
| ffmpeg, ffprobe | 8.1.2, WinGet Gyan full build | Ghép khung và tiếng. Không tạo nhân vật 3D |
| Python | `C:\Python314\python.exe` | Có |
| Pillow | đã cài | Cắt ảnh và chữ. Không vẽ người 3D đạt yêu cầu |
| edge-tts | đã cài | Giọng `vi-VN-NamMinhNeural` đã dùng ở POC 001, không tốn key. Chưa thu cho POC 002 vì chưa có hình để cắt |
| onnxruntime | 1.27.0 | Runtime trống. Không có weight trong `.cache\huggingface` hay `.cache\torch` |
| torch, diffusers | không cài | Không sinh ảnh cục bộ |
| Blender | không có trên PATH | Không render 3D |
| ComfyUI, Stable Diffusion WebUI | không thấy ở các thư mục thường gặp | Không |
| moderngl, trimesh, pyrender, OpenGL, pygame, manim | không cài | Không |

`MarketingLocalVideoRenderer` có `FrameSequence` và `FrameDurationSec`. POC 001 đã mux chuỗi PNG sẵn bằng đường này. Renderer giữ từng khung một khoảng thời gian rồi ghép tiếng. Nó không tạo chuyển động camera, không parallax, không hoạt hình nhân vật. Muốn có chuyển động thì khung phải được vẽ sẵn từ ngoài. File renderer không sửa trong lượt này.

Công cụ tạo ảnh trong Cursor không được gọi. Không có bằng chứng chi phí bằng 0, và brief cấm tự phát sinh chi phí. Pollinations trong pipeline sản xuất cũng không được gọi.

## Asset POC 002 giữ nguyên

- Kịch bản 15 giây trong `POC-002-REPORT.md` mục 2: đóng cửa, sổ cuối ngày, hàng và hạn dùng, rồi nhu cầu nhìn số. Không có câu lợi nhuận.
- Cảnh sản phẩm, khi nào được dựng: chỉ `docs/novixa/launch/DOC-008/assets/screenshots/01-dashboard.png`, SHA-256 `94c726755cdfaa2b38691e3c089e92a1d39eb98325f5d12ed222b39382e544e9`, kèm dòng «Giao diện thật · dữ liệu demo». Không vẽ lại màn hình, không sửa số.
- Logo `docs/novixa/assets/brand/novixa-logo.png` chỉ là nốt kết.
- Ảnh storefront vẫn `RIGHTS_UNVERIFIED`. Không dùng làm nhân vật.

## Vì sao chưa dựng

Bốn cảnh cần bốn hình riêng, cùng một dược sĩ, cùng áo, cùng nhà thuốc, cùng bảng màu. Không có model cục bộ để sinh và không có file nhân vật nguồn. Pillow không đủ để đạt «không có lỗi hình thể». Một MP4 làm từ khung tĩnh hoặc từ dashboard sẽ là slideshow, trái tiêu chí nghiệm thu.

## Điều kiện để mở lại

Cần một trong hai, do người phụ trách chỉ định và xác nhận không phát sinh phí:

- Bộ bốn ảnh 3D stylized đã vẽ, cùng nhân vật, giao vào `proof/novixa-video-poc-002/`. Ảnh không giống người thật. Không phải storyboard ghép nhiều cảnh.
- Hoặc một công cụ cục bộ đã cài (Blender hoặc một model diffusion có weight sẵn) và được phép chạy offline.

Khi có ảnh, bước sau mới là: kiểm tra hình thể và sự nhất quán, thu lời bằng edge-tts, cắt bằng ffmpeg hoặc `FrameSequence`. Chuyển động phải là chuỗi khung khác nhau hoặc lớp parallax. Không zoom một kiểu cho cả bốn cảnh. Cảnh 4 vẫn là screenshot thật.

## Lượt dựng

Người dùng giao lại cùng brief. Đã tạo ảnh bằng công cụ ảnh của Cursor, rồi ghép bằng ffmpeg. File: `job/output.mp4`. Báo cáo đủ ở `RENDER-REPORT.md`.

Kết luận chất lượng của file này: `NEEDS REVIEW`. Chuyển động là crop trên ảnh tĩnh, không phải hoạt hình nhân vật. Cảnh 1 không có cửa cuốn. Ảnh gốc 720×1280 bị phóng lên 1080×1920.
